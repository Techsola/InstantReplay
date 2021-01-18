using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Techsola.InstantReplay.Native;

namespace Techsola.InstantReplay
{
    internal readonly ref struct Composition
    {
        private readonly Gdi32.BitmapSafeHandle bitmap;

        public Gdi32.DeviceContextSafeHandle DeviceContext { get; }

        /// <summary>
        /// Call <see cref="Gdi32.GdiFlush"/> before accessing pixels after batchable GDI functions have been called.
        /// </summary>
        public ColorEnumerable Pixels { get; }

        public Composition(int width, int height, ushort bitsPerPixel)
        {
            DeviceContext = Gdi32.CreateCompatibleDC(IntPtr.Zero).ThrowWithoutLastErrorAvailableIfInvalid(nameof(Gdi32.CreateCompatibleDC));

            bitmap = Gdi32.CreateDIBSection(DeviceContext, new()
            {
                bmiHeader =
                {
                    biSize = Marshal.SizeOf(typeof(Gdi32.BITMAPINFOHEADER)),
                    biWidth = width,
                    biHeight = -height,
                    biPlanes = 1,
                    biBitCount = bitsPerPixel,
                },
            }, Gdi32.DIB.RGB_COLORS, out var compositionPixelDataPointer, hSection: IntPtr.Zero, offset: 0).ThrowLastErrorIfInvalid();

            Gdi32.SelectObject(DeviceContext, bitmap).ThrowWithoutLastErrorAvailableIfInvalid(nameof(Gdi32.SelectObject));

            unsafe
            {
                Pixels = new(
                    (byte*)compositionPixelDataPointer,
                    (uint)width,
                    stride: ((((uint)width * 3) + 3) / 4) * 4,
                    (uint)height);
            }
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
