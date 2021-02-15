using System.IO;

namespace Techsola.InstantReplay
{
    partial class InstantReplayCamera
    {
        private struct FrameSink
        {
            private readonly MemoryStream stream;
            private readonly GifWriter writer;
            private readonly WuQuantizer quantizer;
            private readonly (byte R, byte G, byte B)[] paletteBuffer;
            private readonly byte[] indexedImageBuffer;

            public FrameSink(ushort compositionWidth, ushort compositionHeight)
            {
                stream = new MemoryStream();
                writer = new GifWriter(stream);

                writer.BeginStream(
                    compositionWidth,
                    compositionHeight,
                    globalColorTable: false, // TODO: optimize to use the global color table for the majority palette if more than one frame can use the same palette
                    sourceImageBitsPerPrimaryColor: 8, // Actually 24, but this is the maximum value. Not used anyway.
                    globalColorTableIsSorted: false,
                    globalColorTableSize: 0,
                    globalColorTableBackgroundColorIndex: 0);

                writer.WriteLoopingExtensionBlock();

                quantizer = new();

                paletteBuffer = new (byte R, byte G, byte B)[256];
                indexedImageBuffer = new byte[compositionWidth * compositionHeight];
            }

            public void EmitFrame(Composition source, UInt16Rectangle boundingRectangle, ushort delayInHundredthsOfASecond)
            {
                quantizer.Quantize(
                    source.EnumerateRange(boundingRectangle),
                    paletteBuffer,
                    out var paletteLength,
                    indexedImageBuffer,
                    out var indexedImageLength);

                var bitsPerIndexedPixel = GetBitsPerPixel(paletteLength);

                writer.WriteGraphicControlExtensionBlock(delayInHundredthsOfASecond, transparentColorIndex: null);

                writer.WriteImageDescriptor(
                    left: boundingRectangle.Left,
                    top: boundingRectangle.Top,
                    width: boundingRectangle.Width,
                    height: boundingRectangle.Height,
                    localColorTable: true,
                    isInterlaced: false,
                    localColorTableIsSorted: false,
                    localColorTableSize: (byte)(bitsPerIndexedPixel - 1)); // Means 2^(localColorTableSize+1) entries

                writer.WriteColorTable(paletteBuffer, paletteLength: 1 << bitsPerIndexedPixel);

                writer.WriteImageData(indexedImageBuffer, indexedImageLength, bitsPerIndexedPixel);
            }

            public byte[] End()
            {
                writer.EndStream();
                return stream.ToArray();
            }
        }
    }
}
