using System;

namespace Techsola.InstantReplay.Native
{
    internal static partial class Gdi32
    {
        public sealed class BitmapSafeHandle : GdiObjectSafeHandle
        {
            private BitmapSafeHandle()
                : base(ownsHandle: true)
            {
            }

            public BitmapSafeHandle(IntPtr handle) : this()
            {
                SetHandle(handle);
            }
        }
    }
}
