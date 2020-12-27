using System.IO;

namespace Techsola.InstantReplay
{
    partial class GifWriter
    {
        private struct GifImageDataChunker
        {
            const byte MaxChunkLength = 255;

            private readonly BinaryWriter writer;
            private readonly byte[] buffer;
            private byte nextIndex;

            public GifImageDataChunker(BinaryWriter writer)
            {
                this.writer = writer;
                buffer = new byte[MaxChunkLength];
                nextIndex = 0;
            }

            public void AddByte(byte data)
            {
                buffer[nextIndex] = data;
                nextIndex++;

                if (nextIndex == MaxChunkLength) FlushCore();
            }

            public void Flush()
            {
                if (nextIndex > 0) FlushCore();
            }

            private void FlushCore()
            {
                writer.Write(nextIndex);
                writer.Write(buffer, 0, nextIndex);
                nextIndex = 0;
            }
        }
    }
}
