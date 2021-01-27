using System;
using Techsola.InstantReplay.Native;

namespace Techsola.InstantReplay
{
    partial class InstantReplayCamera
    {
        private sealed class WindowState : IDisposable
        {
            private User32.WindowDeviceContextSafeHandle? windowDC;
            private readonly CircularBuffer<Frame?> frames;
            private int disposedFrameCount;

            public long FirstSeen { get; }
            public long LastSeen { get; set; }

            public WindowState(IntPtr handle, long firstSeen, int bufferSize)
            {
                FirstSeen = firstSeen;
                LastSeen = firstSeen;

                windowDC = User32.GetDC(handle).ThrowWithoutLastErrorAvailableIfInvalid(nameof(User32.GetDC));

                frames = new(bufferSize);
            }

            public void Dispose()
            {
                foreach (var frame in frames.GetRawBuffer())
                    frame?.Dispose();

                windowDC?.Dispose();
            }

            public void AddFrame(
                Gdi32.DeviceContextSafeHandle bitmapDC,
                WindowMetrics windowMetrics,
                uint zOrder,
                ref bool needsGdiFlush)
            {
                if (windowDC is null) throw new InvalidOperationException("The window is closed.");

                var frame = frames.GetNextRef() ??= new();
                frame.Overwrite(bitmapDC, ref windowDC, windowMetrics, zOrder, ref needsGdiFlush);
            }

            public void AddInvisibleFrame()
            {
                frames.GetNextRef()?.SetInvisible();
            }

            public void MarkClosed()
            {
                if (windowDC is null) return;
                windowDC.Dispose();
                windowDC = null;
            }

            public void DisposeNextFrame(out bool allFramesDisposed)
            {
                if (frames.Count == 0)
                {
                    allFramesDisposed = true;
                    return;
                }

                ref var frameRef = ref frames.GetNextRef();
                frameRef?.Dispose();
                frameRef = null;

                disposedFrameCount++;
                allFramesDisposed = disposedFrameCount >= frames.Capacity;
            }

            public Frame?[] GetFramesSnapshot() => frames.ToArray();
        }
    }
}
