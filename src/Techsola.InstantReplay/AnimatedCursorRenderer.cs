using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Techsola.InstantReplay.Native;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Techsola.InstantReplay
{
    internal sealed class AnimatedCursorRenderer
    {
        private readonly Dictionary<IntPtr, ((uint X, uint Y) Hotspot, (uint Width, uint Height) Size)> cursorInfoByHandle = new();
        private readonly Dictionary<IntPtr, (uint Current, uint Max)> cursorAnimationStepByHandle = new();

        public void Render(DeleteDCSafeHandle deviceContext, HCURSOR cursorHandle, int cursorX, int cursorY, out UInt16Rectangle changedArea)
        {
            if (!cursorInfoByHandle.TryGetValue(cursorHandle, out var cursorInfo))
            {
                // Workaround for https://github.com/microsoft/CsWin32/issues/256
                //                       ↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓
                if (!PInvoke.GetIconInfo(new UnownedHandle(cursorHandle), out var iconInfo)) throw new Win32Exception();
                new DeleteObjectSafeHandle(iconInfo.hbmColor).Dispose();

                using var bitmapHandle = new DeleteObjectSafeHandle(iconInfo.hbmMask);

                var bitmap = default(BITMAP);
                unsafe
                {
                    // Workaround for https://github.com/microsoft/CsWin32/issues/275
                    //                                  ↓↓↓↓↓↓↓↓↓            ↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓
                    var bytesCopied = PInvoke.GetObject((HGDIOBJ)bitmapHandle.DangerousGetHandle(), Marshal.SizeOf(typeof(BITMAP)), &bitmap);
                    if (bytesCopied != Marshal.SizeOf(typeof(BITMAP)))
                        throw new Win32Exception("GetObject returned an unexpected number of bytes.");
                }

                cursorInfo = ((iconInfo.xHotspot, iconInfo.yHotspot), ((uint)bitmap.bmWidth, (uint)bitmap.bmHeight));
                cursorInfoByHandle.Add(cursorHandle, cursorInfo);
            }

            if (!cursorAnimationStepByHandle.TryGetValue(cursorHandle, out var cursorAnimationStep))
                cursorAnimationStep = (Current: 0, Max: uint.MaxValue);

            var deviceContextNeedsRelease = false;
            deviceContext.DangerousAddRef(ref deviceContextNeedsRelease);
            try
            {
                while (!PInvoke.DrawIconEx(
                    (HDC)deviceContext.DangerousGetHandle(),
                    cursorX - (int)cursorInfo.Hotspot.X,
                    cursorY - (int)cursorInfo.Hotspot.Y,
                    /* Workaround for https://github.com/microsoft/CsWin32/issues/256
                    ↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓ */
                    new UnownedHandle(cursorHandle),
                    cxWidth: 0,
                    cyWidth: 0,
                    cursorAnimationStep.Current,
                    hbrFlickerFreeDraw: null,
                    DI_FLAGS.DI_NORMAL))
                {
                    var lastError = Marshal.GetLastWin32Error();

                    if ((ERROR)lastError == ERROR.INVALID_PARAMETER && cursorAnimationStep.Current > 0)
                    {
                        cursorAnimationStep = (Current: 0, Max: cursorAnimationStep.Current - 1);
                        continue;
                    }

                    throw new Win32Exception(lastError);
                }
            }
            finally
            {
                if (deviceContextNeedsRelease) deviceContext.DangerousRelease();
            }

            cursorAnimationStep.Current = cursorAnimationStep.Current == cursorAnimationStep.Max ? 0 : cursorAnimationStep.Current + 1;
            cursorAnimationStepByHandle[cursorHandle] = cursorAnimationStep;

            changedArea = new(
                (ushort)Math.Max(0, cursorX - (int)cursorInfo.Hotspot.X),
                (ushort)Math.Max(0, cursorY - (int)cursorInfo.Hotspot.Y),
                (ushort)cursorInfo.Size.Width,
                (ushort)cursorInfo.Size.Height);
        }
    }
}
