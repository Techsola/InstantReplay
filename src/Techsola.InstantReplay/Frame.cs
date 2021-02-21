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

            public WindowMetrics WindowMetrics { get; private set; }
            public uint ZOrder { get; private set; }

            public void Dispose()
            {
                bitmap?.Dispose();
            }

            public void Overwrite(
                Gdi32.DeviceContextSafeHandle bitmapDC,
                ref User32.WindowDeviceContextSafeHandle windowDC,
                WindowMetrics windowMetrics,
                uint zOrder,
                ref bool needsGdiFlush)
            {
                if (windowMetrics.ClientWidth > 0 && windowMetrics.ClientHeight > 0)
                {
                    if (bitmap is null || bitmapWidth < windowMetrics.ClientWidth || bitmapHeight < windowMetrics.ClientHeight)
                    {
                        if (bitmap is null)
                        {
                            // Most of the time, windows don't resize, so save some space by not rounding up.
                            bitmapWidth = windowMetrics.ClientWidth;
                            bitmapHeight = windowMetrics.ClientHeight;
                        }
                        else
                        {
                            // Round up to the nearest 256 pixels to minimize the number of times that bitmaps are
                            // reallocated.
                            bitmapWidth = ((Math.Max(bitmapWidth, windowMetrics.ClientWidth) + 255) / 256) * 256;
                            bitmapHeight = ((Math.Max(bitmapHeight, windowMetrics.ClientHeight) + 255) / 256) * 256;

                            bitmap.Dispose();
                        }

                        bitmap = Gdi32.CreateDIBSection(bitmapDC, new()
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

                    retryBitBlt:
                    if (!Gdi32.BitBlt(bitmapDC, 0, 0, windowMetrics.ClientWidth, windowMetrics.ClientHeight, windowDC, 0, 0, Gdi32.RasterOperation.SRCCOPY))
                    {
                        var lastError = Marshal.GetLastWin32Error();
                        if ((ERROR)lastError is ERROR.INVALID_WINDOW_HANDLE or ERROR.DC_NOT_FOUND)
                        {
                            windowDC.Dispose();
                            windowDC = User32.GetDC(windowDC.HWnd);
                            if (windowDC.IsInvalid)
                            {
                                // This happens when the window goes away. Let this be detected on the next cycle, if it
                                // was actually due to the window closing and not some other failure. Just make sure a
                                // stale frame isn't drawn during this cycle.
                                SetInvisible();
                                return;
                            }

                            goto retryBitBlt;
                        }

                        if (lastError != 0) throw new Win32Exception(lastError);
                        needsGdiFlush = true;
                    }
                    else
                    {
                        needsGdiFlush = false;
                    }
                }

                WindowMetrics = windowMetrics;
                ZOrder = zOrder;
            }

            public void SetInvisible()
            {
                WindowMetrics = default;
            }

            public void Compose(
                Gdi32.DeviceContextSafeHandle bitmapDC,
                Gdi32.DeviceContextSafeHandle compositionDC,
                (int X, int Y) compositionOffset,
                ref bool needsGdiFlush,
                out UInt16Rectangle changedArea)
            {
                if (bitmap is null || WindowMetrics.ClientWidth == 0 || WindowMetrics.ClientHeight == 0)
                {
                    changedArea = default;
                    return;
                }

                Gdi32.SelectObject(bitmapDC, bitmap).ThrowWithoutLastErrorAvailableIfInvalid(nameof(Gdi32.SelectObject));

                changedArea = new(
                    (ushort)(WindowMetrics.ClientLeft + compositionOffset.X),
                    (ushort)(WindowMetrics.ClientTop + compositionOffset.Y),
                    (ushort)WindowMetrics.ClientWidth,
                    (ushort)WindowMetrics.ClientHeight);

                if (!Gdi32.BitBlt(
                    compositionDC,
                    changedArea.Left,
                    changedArea.Top,
                    changedArea.Width,
                    changedArea.Height,
                    bitmapDC,
                    0,
                    0,
                    Gdi32.RasterOperation.SRCCOPY))
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
}
