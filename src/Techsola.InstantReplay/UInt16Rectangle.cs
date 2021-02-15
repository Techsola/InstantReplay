using System;

namespace Techsola.InstantReplay
{
    internal struct UInt16Rectangle
    {
        public UInt16Rectangle(ushort left, ushort top, ushort width, ushort height)
        {
            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }

        public ushort Left { get; set; }
        public ushort Top { get; set; }
        public ushort Width { get; set; }
        public ushort Height { get; set; }

        public bool IsEmpty => Width == 0 || Height == 0;

        public override string ToString()
        {
            return $"Left = {Left}, Top = {Top}, Width = {Width}, Height = {Height}";
        }

        public UInt16Rectangle Union(UInt16Rectangle other)
        {
            if (IsEmpty) return other;
            if (other.IsEmpty) return this;

            var left = Math.Min(Left, other.Left);
            var top = Math.Min(Top, other.Top);
            var right = Math.Max(Left + Width, other.Left + other.Width);
            var bottom = Math.Max(Top + Height, other.Top + other.Height);

            return new(left, top, (ushort)(right - left), (ushort)(bottom - top));
        }

        public UInt16Rectangle Intersect(UInt16Rectangle other)
        {
            var left = Math.Max(Left, other.Left);
            var top = Math.Max(Top, other.Top);
            var right = Math.Min(Left + Width, other.Left + other.Width);
            var bottom = Math.Min(Top + Height, other.Top + other.Height);

            if (right < left || bottom < top) return default;

            return new(left, top, (ushort)(right - left), (ushort)(bottom - top));
        }
    }
}
