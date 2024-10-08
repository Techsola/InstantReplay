using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Techsola.InstantReplay.Native;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;

namespace Techsola.InstantReplay
{
    public static partial class InstantReplayCamera
    {
        private sealed class Frame : IDisposable
        {
            public const int BitsPerPixel = 24;

            private DeleteObjectSafeHandle? bitmap;
            private int bitmapWidth;
            private int bitmapHeight;

            public WindowMetrics WindowMetrics { get; private set; }
            public uint ZOrder { get; private set; }

            public void Dispose()
            {
                bitmap?.Dispose();
            }

            public void Overwrite(
                DeleteDCSafeHandle bitmapDC,
                ref WindowDeviceContextSafeHandle windowDC,
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

                        var bitmapDCNeedsRelease = false;
                        bitmapDC.DangerousAddRef(ref bitmapDCNeedsRelease);
                        try
                        {
                            unsafe
                            {
                                var bitmapInfo = new BITMAPINFO
                                {
                                    bmiHeader =
                                    {
                                        biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER)),
                                        biWidth = bitmapWidth,
                                        biHeight = -bitmapHeight,
                                        biPlanes = 1,
                                        biBitCount = BitsPerPixel,
                                    },
                                };

                                bitmap = PInvoke.CreateDIBSection((HDC)bitmapDC.DangerousGetHandle(), &bitmapInfo, DIB_USAGE.DIB_RGB_COLORS, ppvBits: out _, hSection: null, offset: 0).ThrowLastErrorIfInvalid();
                            }
                        }
                        finally
                        {
                            if (bitmapDCNeedsRelease) bitmapDC.DangerousRelease();
                        }
                    }

                    // Workaround for https://github.com/microsoft/CsWin32/issues/199
                    if (PInvoke.SelectObject((HDC)bitmapDC.DangerousGetHandle(), (HGDIOBJ)bitmap.DangerousGetHandle()).IsNull)
                        throw new Win32Exception("SelectObject failed.");

                    retryBitBlt:
                    PInvoke.SetLastError(0); // BitBlt doesn't set the last error if it returns false to indicate that the operation has been batched
                    if (!PInvoke.BitBlt((HDC)bitmapDC.DangerousGetHandle(), 0, 0, windowMetrics.ClientWidth, windowMetrics.ClientHeight, (HDC)windowDC.DangerousGetHandle(), 0, 0, ROP_CODE.SRCCOPY))
                    {
                        var lastError = Marshal.GetLastWin32Error();
                        if ((ERROR)lastError is ERROR.INVALID_WINDOW_HANDLE or ERROR.DC_NOT_FOUND)
                        {
                            windowDC.Dispose();
                            windowDC = new(windowDC.HWnd, PInvoke.GetDC(windowDC.HWnd));
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
                DeleteDCSafeHandle bitmapDC,
                DeleteDCSafeHandle compositionDC,
                (int X, int Y) compositionOffset,
                ref bool needsGdiFlush,
                out UInt16Rectangle changedArea)
            {
                if (bitmap is null || WindowMetrics.ClientWidth == 0 || WindowMetrics.ClientHeight == 0)
                {
                    changedArea = default;
                    return;
                }

                // Workaround for https://github.com/microsoft/CsWin32/issues/199
                if (PInvoke.SelectObject((HDC)bitmapDC.DangerousGetHandle(), (HGDIOBJ)bitmap.DangerousGetHandle()).IsNull)
                    throw new Win32Exception("SelectObject failed.");

                changedArea = new(
                    (ushort)(WindowMetrics.ClientLeft + compositionOffset.X),
                    (ushort)(WindowMetrics.ClientTop + compositionOffset.Y),
                    (ushort)WindowMetrics.ClientWidth,
                    (ushort)WindowMetrics.ClientHeight);

                PInvoke.SetLastError(0); // BitBlt doesn't set the last error if it returns false to indicate that the operation has been batched
                if (!PInvoke.BitBlt(
                    (HDC)compositionDC.DangerousGetHandle(),
                    changedArea.Left,
                    changedArea.Top,
                    changedArea.Width,
                    changedArea.Height,
                    (HDC)bitmapDC.DangerousGetHandle(),
                    0,
                    0,
                    ROP_CODE.SRCCOPY))
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
