using System;
using System.Runtime.InteropServices;

namespace Techsola.InstantReplay.Native
{
    internal static partial class Gdi32
    {
        public sealed class DeviceContextSafeHandle : SafeHandle
        {
            public DeviceContextSafeHandle(IntPtr? hWnd, IntPtr handle)
                : base(invalidHandleValue: IntPtr.Zero, ownsHandle: true)
            {
                HWnd = hWnd;
                SetHandle(handle);
            }

            public IntPtr? HWnd { get; }

            public override bool IsInvalid => handle == IntPtr.Zero;

            protected override bool ReleaseHandle()
            {
                return HWnd is not null ? ReleaseDC(HWnd.Value, handle) : DeleteDC(handle);
            }

            /// <summary>
            /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-releasedc"/>
            /// </summary>
            [DllImport("user32.dll")]
            private static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDC);

            /// <summary>
            /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-deletedc"/>
            /// </summary>
            [DllImport("gdi32.dll")]
            private static extern bool DeleteDC(IntPtr hdc);
        }
    }
}
