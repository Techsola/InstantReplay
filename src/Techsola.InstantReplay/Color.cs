using System;
using System.Runtime.InteropServices;

namespace Techsola.InstantReplay
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Color : IEquatable<Color>
    {
        public byte Channel3, Channel2, Channel1;

        public override bool Equals(object? obj)
        {
            return obj is Color color && Equals(color);
        }

        public bool Equals(Color other)
        {
            return Channel3 == other.Channel3 &&
                   Channel2 == other.Channel2 &&
                   Channel1 == other.Channel1;
        }

        public override int GetHashCode()
        {
            var hashCode = -2120317082;
            hashCode = hashCode * -1521134295 + Channel3.GetHashCode();
            hashCode = hashCode * -1521134295 + Channel2.GetHashCode();
            hashCode = hashCode * -1521134295 + Channel1.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(Color left, Color right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Color left, Color right)
        {
            return !(left == right);
        }
    }
}
