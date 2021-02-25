using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Techsola.InstantReplay.Native
{
    partial class User32
    {
        public sealed class WindowDeviceContextSafeHandle : Gdi32.DeviceContextSafeHandle
        {
            public WindowDeviceContextSafeHandle(IntPtr hWnd, IntPtr handle)
                : base(handle)
            {
                HWnd = hWnd;
            }

            public IntPtr HWnd { get; }

            protected override bool ReleaseHandle() => ReleaseDC(HWnd, handle);

            /// <summary>
            /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-releasedc"/>
            /// </summary>
            [SupportedOSPlatform("windows")]
            [DllImport("user32.dll")]
            private static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDC);
        }
    }
}
