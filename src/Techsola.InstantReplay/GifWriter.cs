using System;
using System.IO;
using System.Text;

namespace Techsola.InstantReplay
{
    /// <summary>
    /// Implements <see href="https://www.w3.org/Graphics/GIF/spec-gif89a.txt"/>.
    /// </summary>
    internal sealed partial class GifWriter
    {
        private readonly BinaryWriter writer;

        public GifWriter(Stream stream)
        {
            // BinaryWriter does not have its own buffer to flush and does not do anything when disposed other than
            // dispose the stream if leaveOpen is false and flush the stream if leaveOpen is true.
#if !NET35
            writer = new(stream, Encoding.ASCII, leaveOpen: true);
#else
            writer = new(stream, Encoding.ASCII);
#endif
        }

        private static readonly byte[] HeaderSignatureAndVersion =
        {
            (byte)'G',
            (byte)'I',
            (byte)'F',
            (byte)'8',
            (byte)'9',
            (byte)'a',
        };

        /// <summary>
        /// Writes the required header block and the required logical screen descriptor block which must immediately
        /// follow it.
        /// </summary>
        public void BeginStream(
            ushort width,
            ushort height,
            bool globalColorTable,
            byte sourceImageBitsPerPrimaryColor,
            bool globalColorTableIsSorted,
            byte globalColorTableSize,
            byte globalColorTableBackgroundColorIndex,
            byte biasedPixelAspectRatioIn64ths = 0)
        {
            if (sourceImageBitsPerPrimaryColor < 1 || 8 < sourceImageBitsPerPrimaryColor)
                throw new ArgumentOutOfRangeException(nameof(sourceImageBitsPerPrimaryColor), sourceImageBitsPerPrimaryColor, "Source image bits per primary color must be between 1 and 8.");

            if (7 < globalColorTableSize)
                throw new ArgumentOutOfRangeException(nameof(globalColorTableSize), globalColorTableSize, "Global color table size must be between 0 and 7.");

            // Header block
            writer.Write(HeaderSignatureAndVersion);

            // Logical screen descriptor block
            writer.Write(width);
            writer.Write(height);
            writer.Write((byte)(
                (globalColorTable ? 0b1000_0000 : 0)
                | ((sourceImageBitsPerPrimaryColor - 1) << 4)
                | (globalColorTableIsSorted ? 0b1000 : 0)
                | globalColorTableSize));
            writer.Write(globalColorTableBackgroundColorIndex);
            writer.Write(biasedPixelAspectRatioIn64ths);
        }

        /// <summary>
        /// If present, this block must appear immediately after the global color table of the logical screen
        /// descriptor.
        /// </summary>
        /// <param name="loopCount">A loop count of <c>0</c> specifies infinite looping.</param>
        public void WriteLoopingExtensionBlock(ushort loopCount = 0)
        {
            BeginApplicationExtension(NetscapeApplicationIdentifierAndCode);

            BeginDataSubBlock(dataSize: 3);
            const byte loopCountSubBlockId = 0x01;
            writer.Write(loopCountSubBlockId);
            writer.Write(loopCount);

            WriteBlockTerminator();
        }

        /// <summary>
        /// This block may appear at most once before each image descriptor.
        /// </summary>
        public void WriteGraphicControlExtensionBlock(
            ushort delayInHundredthsOfASecond = 0,
            byte? transparentColorIndex = null)
        {
            BeginExtension(label: 0xF9, blockSize: 4);

            const byte disposalMethod = 0; // 0 = Not specified
            const bool waitForUserInput = false;

            writer.Write((byte)(
                (disposalMethod << 2)
                | (waitForUserInput ? 0b10 : 0)
                | (transparentColorIndex is not null ? 1 : 0)));
            writer.Write(delayInHundredthsOfASecond);
            writer.Write(transparentColorIndex ?? 0);

            WriteBlockTerminator();
        }

        public void WriteImageDescriptor(
            ushort left,
            ushort top,
            ushort width,
            ushort height,
            bool localColorTable,
            bool isInterlaced,
            bool localColorTableIsSorted,
            byte localColorTableSize)
        {
            if (7 < localColorTableSize)
                throw new ArgumentOutOfRangeException(nameof(localColorTableSize), localColorTableSize, "Local color table size must be between 0 and 7.");

            writer.Write((byte)0x2C);
            writer.Write(left);
            writer.Write(top);
            writer.Write(width);
            writer.Write(height);
            writer.Write((byte)(
                (localColorTable ? 0b1000_0000 : 0)
                | (isInterlaced ? 0b0100_0000 : 0)
                | (localColorTableIsSorted ? 0b0010_0000 : 0)
                | localColorTableSize));
        }

        /// <summary>
        /// Immediately follows the logical screen descriptor if it has global color table and each image descriptor
        /// that has a local color table.
        /// </summary>
        public void WriteColorTable((byte R, byte G, byte B)[] paletteBuffer, int paletteLength)
        {
            if (paletteLength is not (2 or 4 or 8 or 16 or 32 or 64 or 128 or 256))
                throw new ArgumentOutOfRangeException(nameof(paletteLength), paletteLength, "The palette length must be a power of 2 between 2 and 256.");

            if (paletteBuffer.Length < paletteLength)
                throw new ArgumentException("The palette length must be less than or equal to the length of the buffer.");

            for (var i = 0; i < paletteLength; i++)
            {
                var (r, g, b) = paletteBuffer[i];
                writer.Write(r);
                writer.Write(g);
                writer.Write(b);
            }
        }

        /// <summary>
        /// Immediately follows each local color table and each image descriptor that has no local color table.
        /// </summary>
        public void WriteImageData(byte[] indexedImagePixels, byte bitsPerIndexedPixel)
        {
            // https://www.w3.org/Graphics/GIF/spec-gif89a.txt, page 31, "ESTABLISH CODE SIZE"
            if (bitsPerIndexedPixel < 2) bitsPerIndexedPixel = 2;

            writer.Write(bitsPerIndexedPixel);

            var currentCodeSize = (byte)(bitsPerIndexedPixel + 1);
            var clearCode = (ushort)(1u << bitsPerIndexedPixel);
            var endOfInformationCode = (ushort)(clearCode + 1);

            var bitPacker = new GifLzwBitPacker(writer);

            // Spec requires this to be the first code
            bitPacker.WriteCode(clearCode, currentCodeSize);

            if (indexedImagePixels.Length > 0)
            {
                var nextCode = (ushort)(endOfInformationCode + 1);

                var multibyteCodeRoots = new GraphNode?[256];

                var currentIndex = 0;
                while (true)
                {
                    var currentLength = 1;
                    var didAddChildNode = false;

                    var rootCode = indexedImagePixels[currentIndex];
                    var currentNode = multibyteCodeRoots[rootCode] ??= new(rootCode);

                    while (currentIndex + currentLength < indexedImagePixels.Length)
                    {
                        currentLength++;

                        var childNode = currentNode.GetOrAddChildNode(indexedImagePixels[currentIndex + currentLength - 1], nextCode, out didAddChildNode);
                        if (didAddChildNode)
                        {
                            nextCode++;
                            break;
                        }

                        currentNode = childNode;
                    }

                    bitPacker.WriteCode(currentNode.Code, currentCodeSize);

                    if (!didAddChildNode)
                    {
                        // Being here means that currentLength was equal to remainingBytes.Length.
                        break;
                    }

                    const ushort maxAllowedCodeValue = 4095;
                    if (nextCode > maxAllowedCodeValue)
                    {
                        bitPacker.WriteCode(clearCode, currentCodeSize);

                        currentCodeSize = (byte)(bitsPerIndexedPixel + 1);
                        nextCode = (ushort)(endOfInformationCode + 1);
                        Array.Clear(multibyteCodeRoots, 0, multibyteCodeRoots.Length);
                    }
                    else if (nextCode > 1u << currentCodeSize)
                    {
                        currentCodeSize++;
                    }

                    currentIndex += currentLength - 1;
                }
            }

            // Spec requires this to be the last code
            bitPacker.WriteCode(endOfInformationCode, currentCodeSize);
            bitPacker.Flush();

            WriteBlockTerminator();
        }

        public void EndStream()
        {
            writer.Write((byte)0x3B);
        }

        private static readonly byte[] NetscapeApplicationIdentifierAndCode =
        {
            (byte)'N',
            (byte)'E',
            (byte)'T',
            (byte)'S',
            (byte)'C',
            (byte)'A',
            (byte)'P',
            (byte)'E',
            (byte)'2',
            (byte)'.',
            (byte)'0',
        };

        private void BeginApplicationExtension(byte[] applicationIdentifierAndCode)
        {
            if (applicationIdentifierAndCode.Length != 11)
                throw new ArgumentException("The application identifier must be 8 bytes and the application authentication code must be 3 bytes.", nameof(applicationIdentifierAndCode));

            BeginExtension(label: 0xFF, blockSize: 11);
            writer.Write(applicationIdentifierAndCode);
        }

        private void BeginExtension(byte label, byte blockSize)
        {
            writer.Write((byte)0x21);
            writer.Write(label);
            writer.Write(blockSize);
        }

        private void BeginDataSubBlock(byte dataSize)
        {
            writer.Write(dataSize);
        }

        private void WriteBlockTerminator()
        {
            BeginDataSubBlock(dataSize: 0);
        }
    }
}
