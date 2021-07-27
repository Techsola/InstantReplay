using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Techsola.InstantReplay.Native
{
    // Workaround for https://github.com/microsoft/CsWin32/issues/209
    internal sealed class WindowDeviceContextSafeHandle : DeleteDCSafeHandle
    {
        public WindowDeviceContextSafeHandle(HWND hWnd, IntPtr handle)
            : base(handle)
        {
            HWnd = hWnd;
        }

        public HWND HWnd { get; }

        protected override bool ReleaseHandle() => ReleaseDC(HWnd, handle);

        /// <summary>
        /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-releasedc"/>
        /// </summary>
        [SupportedOSPlatform("windows")]
        [DllImport("user32.dll")]
        private static extern bool ReleaseDC(HWND hWnd, IntPtr hDC);
    }
}
