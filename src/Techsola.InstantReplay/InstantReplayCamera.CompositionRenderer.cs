using System;
using System.Collections.Generic;
using Techsola.InstantReplay.Native;

namespace Techsola.InstantReplay
{
    partial class InstantReplayCamera
    {
        private readonly struct CompositionRenderer
        {
            private readonly (long Timestamp, (int X, int Y, IntPtr Handle)? Cursor)[] frames;
            private readonly List<Frame?[]> framesByWindow;
            private readonly (int X, int Y) compositionOffset;
            private readonly AnimatedCursorRenderer cursorRenderer;

            public uint FrameCount { get; }
            public ushort CompositionWidth { get; }
            public ushort CompositionHeight { get; }

            public CompositionRenderer((long Timestamp, (int X, int Y, IntPtr Handle)? Cursor)[] frames, List<Frame?[]> framesByWindow)
            {
                this.frames = frames;
                this.framesByWindow = framesByWindow;

                var minLeft = int.MaxValue;
                var maxRight = int.MinValue;
                var minTop = int.MaxValue;
                var maxBottom = int.MinValue;
                var maxFrameCount = 0u;

                foreach (var frameList in framesByWindow)
                {
                    for (var i = 0u; i < frameList.Length; i++)
                    {
                        if (frameList[i]?.WindowMetrics is not { ClientWidth: > 0, ClientHeight: > 0 } metrics) continue;

                        var frameCount = (uint)frameList.Length - i;
                        if (maxFrameCount < frameCount) maxFrameCount = frameCount;

                        if (minLeft > metrics.ClientLeft) minLeft = metrics.ClientLeft;
                        if (minTop > metrics.ClientTop) minTop = metrics.ClientTop;
                        if (maxRight < metrics.ClientLeft + metrics.ClientWidth) maxRight = metrics.ClientLeft + metrics.ClientWidth;
                        if (maxBottom < metrics.ClientTop + metrics.ClientHeight) maxBottom = metrics.ClientTop + metrics.ClientHeight;
                    }
                }

                FrameCount = maxFrameCount;
                compositionOffset = (X: -minLeft, Y: -minTop);
                CompositionWidth = checked((ushort)(maxRight - minLeft));
                CompositionHeight = checked((ushort)(maxBottom - minTop));

                cursorRenderer = new();
            }

            public void Compose(
                int frameIndex,
                Composition buffer,
                Gdi32.DeviceContextSafeHandle bitmapDC,
                ref bool needsGdiFlush,
                out UInt16Rectangle changedArea)
            {
                var windowFramesToDraw = new List<Frame>();

                foreach (var frameList in framesByWindow)
                {
                    var index = frameIndex - FrameCount + frameList.Length;
                    if (index >= 0 && frameList[index] is { WindowMetrics: { ClientWidth: > 0, ClientHeight: > 0 } } windowFrame)
                        windowFramesToDraw.Add(windowFrame);
                }

                windowFramesToDraw.Sort((a, b) => b.ZOrder.CompareTo(a.ZOrder));

                changedArea = default;

                foreach (var windowFrame in windowFramesToDraw)
                {
                    windowFrame.Compose(bitmapDC, buffer.DeviceContext, compositionOffset, ref needsGdiFlush, out var additionalChangedArea);
                    changedArea = changedArea.Union(additionalChangedArea);
                }

                var frame = frames[frameIndex - FrameCount + frames.Length];

                if (frame.Cursor is { } cursor)
                {
                    cursorRenderer.Render(buffer.DeviceContext, cursor.Handle, cursor.X + compositionOffset.X, cursor.Y + compositionOffset.Y, out var additionalChangedArea);
                    changedArea = changedArea.Union(additionalChangedArea);
                }

                changedArea = changedArea.Intersect(new(0, 0, CompositionWidth, CompositionHeight));
            }
        }
    }
}
