using System.Runtime.InteropServices;

namespace Techsola.InstantReplay
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Color
    {
        public byte Channel3, Channel2, Channel1;
    }
}
