using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Techsola.InstantReplay.Native;

namespace Techsola.InstantReplay
{
    internal sealed class AnimatedCursorRenderer
    {
        private readonly Dictionary<IntPtr, ((uint X, uint Y) Hotspot, (uint Width, uint Height) Size)> cursorInfoByHandle = new();
        private readonly Dictionary<IntPtr, (uint Current, uint Max)> cursorAnimationStepByHandle = new();

        public void Render(Gdi32.DeviceContextSafeHandle deviceContext, IntPtr cursorHandle, int cursorX, int cursorY, out UInt16Rectangle changedArea)
        {
            if (!cursorInfoByHandle.TryGetValue(cursorHandle, out var cursorInfo))
            {
                if (!User32.GetIconInfo(cursorHandle, out var iconInfo)) throw new Win32Exception();
                new Gdi32.BitmapSafeHandle(iconInfo.hbmColor).Dispose();

                using var bitmapHandle = new Gdi32.BitmapSafeHandle(iconInfo.hbmMask);

                var bytesCopied = Gdi32.GetObject(bitmapHandle, Marshal.SizeOf(typeof(Gdi32.BITMAP)), out var bitmap);
                if (bytesCopied != Marshal.SizeOf(typeof(Gdi32.BITMAP)))
                    throw new Win32Exception("GetObject returned an unexpected number of bytes.");

                cursorInfo = ((iconInfo.xHotspot, iconInfo.yHotspot), (bitmap.bmWidth, bitmap.bmHeight));
                cursorInfoByHandle.Add(cursorHandle, cursorInfo);
            }

            if (!cursorAnimationStepByHandle.TryGetValue(cursorHandle, out var cursorAnimationStep))
                cursorAnimationStep = (Current: 0, Max: uint.MaxValue);

            while (!User32.DrawIconEx(
                deviceContext,
                cursorX - (int)cursorInfo.Hotspot.X,
                cursorY - (int)cursorInfo.Hotspot.Y,
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

            changedArea = new(
                (ushort)Math.Max(0, cursorX - (int)cursorInfo.Hotspot.X),
                (ushort)Math.Max(0, cursorY - (int)cursorInfo.Hotspot.Y),
                (ushort)cursorInfo.Size.Width,
                (ushort)cursorInfo.Size.Height);
        }
    }
}
