using System;
using System.Runtime.InteropServices;

namespace Techsola.InstantReplay.Native
{
    internal static class User32
    {
        /// <summary>
        /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-clienttoscreen"/>
        /// </summary>
        [DllImport("user32.dll")]
        public static extern bool ClientToScreen(IntPtr hWnd, out POINT lpPoint);

        /// <summary>
        /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-drawiconex"/>
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DrawIconEx(Gdi32.DeviceContextSafeHandle hdc, int xLeft, int yTop, IntPtr hIcon, int cxWidth, int cyWidth, uint istepIfAniCur, IntPtr hbrFlickerFreeDraw, DI diFlags);

        [Flags]
        public enum DI : uint
        {
            /// <summary>
            /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-drawiconex#DI_MASK"/>
            /// </summary>
            MASK = 1,
            /// <summary>
            /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-drawiconex#DI_IMAGE"/>
            /// </summary>
            IMAGE = 2,
            /// <summary>
            /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-drawiconex#DI_NORMAL"/>
            /// </summary>
            NORMAL = MASK | IMAGE,
        }

        /// <summary>
        /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumwindows"/>
        /// </summary>
        [DllImport("user32.dll")]
        public static extern bool EnumWindows(WNDENUMPROC lpfn, IntPtr lParam);

        /// <summary>
        /// https://docs.microsoft.com/en-us/previous-versions/windows/desktop/legacy/ms633498(v=vs.85)
        /// </summary>
        public delegate bool WNDENUMPROC(IntPtr hWnd, IntPtr lParam);

        /// <summary>
        /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getclientrect"/>
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        /// <summary>
        /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getcursorinfo"/>
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetCursorInfo(ref CURSORINFO pci);

        /// <summary>
        /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-cursorinfo"/>
        /// </summary>
        public struct CURSORINFO
        {
            public int cbSize;
            public CURSOR flags;
            public IntPtr hCursor;
            public POINT ptScreenPos;
        }

        [Flags]
        public enum CURSOR : uint
        {
            /// <summary>
            /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-cursorinfo#CURSOR_SHOWING"/>
            /// </summary>
            SHOWING = 1,
            /// <summary>
            /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-cursorinfo#CURSOR_SUPPRESSED"/>
            /// </summary>
            SUPPRESSED = 2,
        }

        /// <summary>
        /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getdc"/>
        /// </summary>
        public static Gdi32.DeviceContextSafeHandle GetDC(IntPtr hWnd)
        {
            return new(hWnd, GetDC_PInvoke(hWnd));
        }

        [DllImport("user32.dll", EntryPoint = "GetDC")]
        private static extern IntPtr GetDC_PInvoke(IntPtr hWnd);

        /// <summary>
        /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getdpiforwindow"/>
        /// </summary>
        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(IntPtr hwnd);

        /// <summary>
        /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-geticoninfo"/>
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

        /// <summary>
        /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-iconinfo"/>
        /// </summary>
        public struct ICONINFO
        {
            public bool fIcon;
            public uint xHotspot;
            public uint yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        /// <summary>
        /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowthreadprocessid"/>
        /// </summary>
        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        /// <summary>
        /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-iswindowvisible"/>
        /// </summary>
        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);
    }
}
