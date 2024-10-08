using System.ComponentModel;
using System.Runtime.InteropServices;
using Techsola.InstantReplay.Native;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;

namespace Techsola.InstantReplay
{
    internal readonly ref struct Composition
    {
        private readonly DeleteObjectSafeHandle bitmap;

        public byte BytesPerPixel { get; }
        public uint Stride { get; }
        public DeleteDCSafeHandle DeviceContext { get; }
        public unsafe byte* PixelDataPointer { get; }

        /// <summary>
        /// Call <see cref="PInvoke.GdiFlush"/> before accessing pixels after batchable GDI functions have been called.
        /// </summary>
        public ColorEnumerable EnumerateRange(UInt16Rectangle rectangle)
        {
            unsafe
            {
                return new(
                    PixelDataPointer + (rectangle.Left * BytesPerPixel) + (rectangle.Top * Stride),
                    rectangle.Width,
                    Stride,
                    rectangle.Height);
            }
        }

        public Composition(uint width, uint height, ushort bitsPerPixel)
        {
            BytesPerPixel = (byte)(bitsPerPixel >> 3);
            Stride = (((width * BytesPerPixel) + 3) / 4) * 4;

            DeviceContext = new DeleteDCSafeHandle(PInvoke.CreateCompatibleDC(default)).ThrowWithoutLastErrorAvailableIfInvalid(nameof(PInvoke.CreateCompatibleDC));
            var deviceContextNeedsRelease = false;
            DeviceContext.DangerousAddRef(ref deviceContextNeedsRelease);
            try
            {
                unsafe
                {
                    var bitmapInfo = new BITMAPINFO
                    {
                        bmiHeader =
                        {
                            biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER)),
                            biWidth = (int)width,
                            biHeight = -(int)height,
                            biPlanes = 1,
                            biBitCount = bitsPerPixel,
                        },
                    };

                    bitmap = PInvoke.CreateDIBSection((HDC)DeviceContext.DangerousGetHandle(), &bitmapInfo, DIB_USAGE.DIB_RGB_COLORS, out var pointer, hSection: null, offset: 0).ThrowLastErrorIfInvalid();

                    PixelDataPointer = (byte*)pointer;
                }

                if (PInvoke.SelectObject((HDC)DeviceContext.DangerousGetHandle(), (HGDIOBJ)bitmap.DangerousGetHandle()).IsNull)
                    throw new Win32Exception("SelectObject failed.");
            }
            finally
            {
                if (deviceContextNeedsRelease) DeviceContext.DangerousRelease();
            }
        }

        public void Dispose()
        {
            bitmap.Dispose();
            DeviceContext.Dispose();
        }

        public void Clear(int x, int y, int width, int height, ref bool needsGdiFlush)
        {
            if (width <= 0 || height <= 0) return;

            var deviceContextNeedsRelease = false;
            DeviceContext.DangerousAddRef(ref deviceContextNeedsRelease);
            try
            {
                if (!PInvoke.BitBlt((HDC)DeviceContext.DangerousGetHandle(), x, y, width, height, hdcSrc: default, 0, 0, ROP_CODE.BLACKNESS))
                {
                    var lastError = Marshal.GetLastWin32Error();
                    if (lastError != 0) throw new Win32Exception(lastError);
                    needsGdiFlush = true;
                }
                else
                {
                    needsGdiFlush = false;
                }
            }
            finally
            {
                if (deviceContextNeedsRelease) DeviceContext.DangerousRelease();
            }
        }
    }
}

