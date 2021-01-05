using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Techsola.InstantReplay.Native;

namespace Techsola.InstantReplay
{
    internal readonly struct AnimatedCursorRenderer
    {
        private readonly Gdi32.DeviceContextSafeHandle deviceContext;
        private readonly Dictionary<IntPtr, (uint X, uint Y)> cursorHotspotByHandle;
        private readonly Dictionary<IntPtr, (uint Current, uint Max)> cursorAnimationStepByHandle;

        public AnimatedCursorRenderer(Gdi32.DeviceContextSafeHandle deviceContext)
        {
            this.deviceContext = deviceContext ?? throw new ArgumentNullException(nameof(deviceContext));
            cursorHotspotByHandle = new();
            cursorAnimationStepByHandle = new();
        }

        public void Render(IntPtr cursorHandle, int cursorX, int cursorY)
        {
            if (!cursorHotspotByHandle.TryGetValue(cursorHandle, out var cursorHotspot))
            {
                if (!User32.GetIconInfo(cursorHandle, out var iconInfo)) throw new Win32Exception();
                new Gdi32.BitmapSafeHandle(iconInfo.hbmMask).Dispose();
                new Gdi32.BitmapSafeHandle(iconInfo.hbmColor).Dispose();

                cursorHotspot = (iconInfo.xHotspot, iconInfo.yHotspot);
                cursorHotspotByHandle.Add(cursorHandle, cursorHotspot);
            }

            if (!cursorAnimationStepByHandle.TryGetValue(cursorHandle, out var cursorAnimationStep))
                cursorAnimationStep = (Current: 0, Max: uint.MaxValue);

            while (!User32.DrawIconEx(
                deviceContext,
                cursorX - (int)cursorHotspot.X,
                cursorY - (int)cursorHotspot.Y,
                cursorHandle,
                cxWidth: 0,
                cyWidth: 0,
                cursorAnimationStep.Current,
                hbrFlickerFreeDraw: IntPtr.Zero,
                User32.DI.NORMAL))
            {
                var lastError = Marshal.GetLastWin32Error();

                if ((ERROR)lastError == ERROR.INVALID_PARAMETER && cursorAnimationStep.Current > 0)
                {
                    cursorAnimationStep = (Current: 0, Max: cursorAnimationStep.Current - 1);
                    continue;
                }

                throw new Win32Exception(lastError);
            }

            cursorAnimationStep.Current = cursorAnimationStep.Current == cursorAnimationStep.Max ? 0 : cursorAnimationStep.Current + 1;
            cursorAnimationStepByHandle[cursorHandle] = cursorAnimationStep;
        }
    }
}
