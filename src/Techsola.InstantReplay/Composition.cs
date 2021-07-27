using System.ComponentModel;
using System.Runtime.InteropServices;
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

            DeviceContext = PInvoke.CreateCompatibleDC(null).ThrowWithoutLastErrorAvailableIfInvalid(nameof(PInvoke.CreateCompatibleDC));
            unsafe
            {
                bitmap = PInvoke.CreateDIBSection(DeviceContext, new()
                {
                    bmiHeader =
                    {
                        biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER)),
                        biWidth = (int)width,
                        biHeight = -(int)height,
                        biPlanes = 1,
                        biBitCount = bitsPerPixel,
                    },
                }, DIB_USAGE.DIB_RGB_COLORS, out var pointer, hSection: null, offset: 0).ThrowLastErrorIfInvalid();

                PixelDataPointer = (byte*)pointer;
            }

            // Workaround for https://github.com/microsoft/CsWin32/issues/199
            if (PInvoke.SelectObject(DeviceContext, (HGDIOBJ)bitmap.DangerousGetHandle()).IsNull)
                throw new Win32Exception("SelectObject failed.");
        }

        public void Dispose()
        {
            bitmap.Dispose();
            DeviceContext.Dispose();
        }

        public void Clear(int x, int y, int width, int height, ref bool needsGdiFlush)
        {
            if (width <= 0 || height <= 0) return;

            if (!PInvoke.BitBlt(DeviceContext, x, y, width, height, null, 0, 0, ROP_CODE.BLACKNESS))
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
