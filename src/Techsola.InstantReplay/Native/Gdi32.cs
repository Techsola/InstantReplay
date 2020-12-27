using System;
using System.Runtime.InteropServices;

#pragma warning disable 649

namespace Techsola.InstantReplay.Native
{
    internal static partial class Gdi32
    {
        /// <summary>
        /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-bitblt"/>
        /// </summary>
        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern bool BitBlt(DeviceContextSafeHandle hdc, int x, int y, int cx, int cy, DeviceContextSafeHandle hdcSrc, int x1, int y1, RasterOperation rop);

        /// <summary>
        /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-bitblt"/>
        /// </summary>
        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern bool BitBlt(DeviceContextSafeHandle hdc, int x, int y, int cx, int cy, IntPtr hdcSrc, int x1, int y1, RasterOperation rop);

        [Flags]
        public enum RasterOperation : uint
        {
            /// <summary>
            /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-bitblt#BLACKNESS"/>
            /// </summary>
            BLACKNESS = 0x42,
            /// <summary>
            /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-bitblt#SRCCOPY"/>
            /// </summary>
            SRCCOPY = 0xCC0020,
        }

        /// <summary>
        /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-createcompatibledc"/>
        /// </summary>
        public static DeviceContextSafeHandle CreateCompatibleDC(IntPtr hdc)
        {
            return new(hWnd: null, CreateCompatibleDC_PInvoke(hdc));
        }

        [DllImport("gdi32.dll", EntryPoint = "CreateCompatibleDC")]
        private static extern IntPtr CreateCompatibleDC_PInvoke(IntPtr hdc);

        /// <summary>
        /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-createdibsection"/>
        /// </summary>
        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern BitmapSafeHandle CreateDIBSection(DeviceContextSafeHandle hdc, in BITMAPINFO pbmi, DIB usage, out IntPtr ppvBits, IntPtr hSection, uint offset);

        /// <summary>
        /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-bitmapinfo"/>
        /// </summary>
        public struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
        }

        /// <summary>
        /// <seealso href="https://docs.microsoft.com/en-us/previous-versions/dd183376(v=vs.85)"/>
        /// </summary>
        public struct BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public BI biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        public enum BI : uint
        {
            RGB = 0,
        }

        public enum DIB : uint
        {
            RGB_COLORS = 0x00,
            PAL_COLORS = 0x01,
        }

        /// <summary>
        /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-gdiflush"/>
        /// </summary>
        [DllImport("gdi32.dll")]
        public static extern bool GdiFlush();

        /// <summary>
        /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-selectobject"/>
        /// </summary>
        public static GdiObjectSafeHandle SelectObject(DeviceContextSafeHandle hdc, GdiObjectSafeHandle h)
        {
            return new(SelectObject_PInvoke(hdc, h), ownsHandle: false);
        }

        [DllImport("gdi32.dll", EntryPoint = "SelectObject", SetLastError = true)]
        private static extern IntPtr SelectObject_PInvoke(DeviceContextSafeHandle hdc, GdiObjectSafeHandle h);
    }
}
