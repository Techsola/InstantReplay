namespace Techsola.InstantReplay
{
    internal readonly struct UInt16Rectangle
    {
        public UInt16Rectangle(ushort left, ushort top, ushort width, ushort height)
        {
            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }

        public readonly ushort Left { get; }
        public readonly ushort Top { get; }
        public readonly ushort Width { get; }
        public readonly ushort Height { get; }

        public override string ToString()
        {
            return $"Left = {Left}, Top = {Top}, Width = {Width}, Height = {Height}";
        }
    }
}
