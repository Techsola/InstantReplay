namespace Techsola.InstantReplay
{
    internal readonly ref struct ColorEnumerable
    {
        private readonly unsafe byte* start;
        private readonly uint width;
        private readonly uint stride;
        private readonly uint height;

        public unsafe ColorEnumerable(byte* start, uint width, uint stride, uint height)
        {
            this.start = start;
            this.width = width;
            this.stride = stride;
            this.height = height;
        }

        public ColorEnumerator GetEnumerator()
        {
            unsafe
            {
                return new(start, width, stride, height);
            }
        }
    }
}
