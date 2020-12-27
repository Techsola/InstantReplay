using System;
using System.IO;

namespace Techsola.InstantReplay
{
    partial class GifWriter
    {
        private struct GifLzwBitPacker
        {
            private GifImageDataChunker chunker;
            private byte buffer;

            /// <summary>
            /// Always between <c>0</c> and <c>7</c>.
            /// </summary>
            private byte nextBit;

            public GifLzwBitPacker(BinaryWriter writer)
            {
                chunker = new(writer);
                buffer = 0;
                nextBit = 0;
            }

            public void WriteCode(ushort code, byte codeLength)
            {
                if (codeLength > 16)
                    throw new ArgumentOutOfRangeException(nameof(codeLength), codeLength, "Code length must not be greater than the number of bits in the code parameter type.");

                if (code >= (1u << codeLength))
                    throw new ArgumentException("The specified code has bits set outside the range allowed by the specified code length.");

                while (codeLength > 0)
                {
                    var bufferBitsRemaining = (byte)(8 - nextBit);

                    buffer |= unchecked((byte)(code << nextBit));

                    if (codeLength < bufferBitsRemaining)
                    {
                        nextBit += codeLength;
                        break;
                    }

                    FlushCore();

                    codeLength -= bufferBitsRemaining;
                    code >>= bufferBitsRemaining;
                }
            }

            public void Flush()
            {
                if (nextBit > 0) FlushCore();

                chunker.Flush();
            }

            private void FlushCore()
            {
                chunker.AddByte(buffer);
                buffer = 0;
                nextBit = 0;
            }
        }
    }
}
