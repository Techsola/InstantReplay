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
    }
}
