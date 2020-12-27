using System;
using System.Runtime.InteropServices;

namespace Techsola.InstantReplay.Native
{
    internal static partial class Gdi32
    {
        public class GdiObjectSafeHandle : SafeHandle
        {
            protected GdiObjectSafeHandle(bool ownsHandle)
                : base(invalidHandleValue: IntPtr.Zero, ownsHandle)
            {
            }

            public GdiObjectSafeHandle(IntPtr handle, bool ownsHandle)
                : this(ownsHandle)
            {
                SetHandle(handle);
            }

            public sealed override bool IsInvalid => handle == IntPtr.Zero;

            protected sealed override bool ReleaseHandle() => DeleteObject(handle);

            /// <summary>
            /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-deleteobject"/>
            /// </summary>
            [DllImport("gdi32.dll")]
            private static extern bool DeleteObject(IntPtr ho);
        }
    }
}
