namespace Techsola.InstantReplay
{
    internal ref struct ColorEnumerator
    {
        private unsafe byte* next;
        private unsafe byte* lineEnd;
        private readonly unsafe byte* imageEnd;
        private readonly uint stride;
        private readonly uint strideSkip;

        public unsafe ColorEnumerator(byte* start, uint width, uint stride, uint height)
        {
            next = start - 3;
            lineEnd = start + (width * 3);
            this.stride = stride;
            strideSkip = stride - (width * 3);
            imageEnd = start + (height * stride) - strideSkip;
        }

        public uint Current
        {
            get
            {
                unsafe
                {
                    return *(uint*)next;
                }
            }
        }

        public bool MoveNext()
        {
            unsafe
            {
                next += 3;
                if (next >= lineEnd)
                {
                    if (next >= imageEnd) return false;
                    next += strideSkip;
                    lineEnd += stride;
                }
                return true;
            }
        }
    }
}
