using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Techsola.InstantReplay.Native;

namespace Techsola.InstantReplay
{
    internal readonly ref struct Composition
    {
        private readonly byte bytesPerPixel;
        private readonly uint stride;
        private readonly Gdi32.BitmapSafeHandle bitmap;
        private readonly unsafe byte* compositionPixelDataPointer;

        public Gdi32.DeviceContextSafeHandle DeviceContext { get; }

        /// <summary>
        /// Call <see cref="Gdi32.GdiFlush"/> before accessing pixels after batchable GDI functions have been called.
        /// </summary>
        public ColorEnumerable EnumerateRange(UInt16Rectangle rectangle)
        {
            unsafe
            {
                return new(
                    compositionPixelDataPointer + (rectangle.Left * bytesPerPixel) + (rectangle.Top * stride),
                    rectangle.Width,
                    stride,
                    rectangle.Height);
            }
        }

        public Composition(uint width, uint height, ushort bitsPerPixel)
        {
            bytesPerPixel = (byte)(bitsPerPixel >> 3);
            stride = (((width * bytesPerPixel) + 3) / 4) * 4;

            DeviceContext = Gdi32.CreateCompatibleDC(IntPtr.Zero).ThrowWithoutLastErrorAvailableIfInvalid(nameof(Gdi32.CreateCompatibleDC));

            bitmap = Gdi32.CreateDIBSection(DeviceContext, new()
            {
                bmiHeader =
                {
                    biSize = Marshal.SizeOf(typeof(Gdi32.BITMAPINFOHEADER)),
                    biWidth = (int)width,
                    biHeight = -(int)height,
                    biPlanes = 1,
                    biBitCount = bitsPerPixel,
                },
            }, Gdi32.DIB.RGB_COLORS, out var pointer, hSection: IntPtr.Zero, offset: 0).ThrowLastErrorIfInvalid();

            unsafe { compositionPixelDataPointer = (byte*)pointer; }

            Gdi32.SelectObject(DeviceContext, bitmap).ThrowWithoutLastErrorAvailableIfInvalid(nameof(Gdi32.SelectObject));
        }

        public void Dispose()
        {
            bitmap.Dispose();
            DeviceContext.Dispose();
        }

        public void Clear(int x, int y, int width, int height, out bool needsGdiFlush)
        {
            if (!Gdi32.BitBlt(DeviceContext, x, y, width, height, IntPtr.Zero, 0, 0, Gdi32.RasterOperation.BLACKNESS))
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
    }
}
