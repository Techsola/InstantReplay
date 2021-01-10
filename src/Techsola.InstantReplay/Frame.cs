using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Techsola.InstantReplay.Native;

namespace Techsola.InstantReplay
{
    public static partial class InstantReplayCamera
    {
        private sealed class Frame : IDisposable
        {
            public const int BitsPerPixel = 24;

            private Gdi32.BitmapSafeHandle? bitmap;
            private int bitmapWidth;
            private int bitmapHeight;

            public int WindowClientLeft { get; private set; }
            public int WindowClientTop { get; private set; }
            public int WindowClientWidth { get; private set; }
            public int WindowClientHeight { get; private set; }
            public uint WindowDpi { get; private set; }
            public uint ZOrder { get; private set; }

            public void Dispose()
            {
                bitmap?.Dispose();
            }

            public void Overwrite(
                Gdi32.DeviceContextSafeHandle bitmapDC,
                Gdi32.DeviceContextSafeHandle windowDC,
                int windowClientLeft,
                int windowClientTop,
                int windowClientWidth,
                int windowClientHeight,
                uint windowDpi,
                uint zOrder)
            {
                if (windowClientWidth > 0 && windowClientHeight > 0)
                {
                    if (bitmap is null || bitmapWidth < windowClientWidth || bitmapHeight < windowClientHeight)
                    {
                        if (bitmap is null)
                        {
                            // Most of the time, windows don't resize, so save some space by not rounding up.
                            bitmapWidth = windowClientWidth;
                            bitmapHeight = windowClientHeight;
                        }
                        else
                        {
                            // Round up to the nearest 256 pixels to minimize the number of times that bitmaps are
                            // reallocated.
                            bitmapWidth = ((Math.Max(bitmapWidth, windowClientWidth) + 255) / 256) * 256;
                            bitmapHeight = ((Math.Max(bitmapHeight, windowClientHeight) + 255) / 256) * 256;

                            bitmap.Dispose();
                        }

                        bitmap = Gdi32.CreateDIBSection(windowDC, new()
                        {
                            bmiHeader =
                            {
                                biSize = Marshal.SizeOf(typeof(Gdi32.BITMAPINFOHEADER)),
                                biWidth = bitmapWidth,
                                biHeight = -bitmapHeight,
                                biPlanes = 1,
                                biBitCount = BitsPerPixel,
                            },
                        }, Gdi32.DIB.RGB_COLORS, ppvBits: out _, hSection: IntPtr.Zero, offset: 0).ThrowLastErrorIfInvalid();
                    }

                    Gdi32.SelectObject(bitmapDC, bitmap).ThrowWithoutLastErrorAvailableIfInvalid(nameof(Gdi32.SelectObject));

                    if (!Gdi32.BitBlt(bitmapDC, 0, 0, windowClientWidth, windowClientHeight, windowDC, 0, 0, Gdi32.RasterOperation.SRCCOPY))
                        throw new Win32Exception();
                }

                WindowClientLeft = windowClientLeft;
                WindowClientTop = windowClientTop;
                WindowClientWidth = windowClientWidth;
                WindowClientHeight = windowClientHeight;
                WindowDpi = windowDpi;
                ZOrder = zOrder;
            }

            public void Compose(Gdi32.DeviceContextSafeHandle bitmapDC, Gdi32.DeviceContextSafeHandle compositionDC, (int X, int Y) compositionOffset)
            {
                if (bitmap is null) return;

                Gdi32.SelectObject(bitmapDC, bitmap).ThrowWithoutLastErrorAvailableIfInvalid(nameof(Gdi32.SelectObject));

                if (!Gdi32.BitBlt(
                    compositionDC,
                    WindowClientLeft + compositionOffset.X,
                    WindowClientTop + compositionOffset.Y,
                    WindowClientWidth,
                    WindowClientHeight,
                    bitmapDC,
                    0,
                    0,
                    Gdi32.RasterOperation.SRCCOPY))
                {
                    throw new Win32Exception();
                }
            }
        }
    }
}
