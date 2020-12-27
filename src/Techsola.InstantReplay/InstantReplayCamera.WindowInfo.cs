using System;
using System.ComponentModel;
using Techsola.InstantReplay.Native;

namespace Techsola.InstantReplay
{
    partial class InstantReplayCamera
    {
        private sealed class WindowState : IDisposable
        {
            private readonly Gdi32.DeviceContextSafeHandle windowDC;
            private readonly CircularBuffer<Frame> frames;

            public long FirstSeen { get; }
            public long LastSeen { get; set; }

            public WindowState(IntPtr handle, long firstSeen, int bufferSize)
            {
                FirstSeen = firstSeen;
                LastSeen = firstSeen;

                windowDC = User32.GetDC(handle);
                if (windowDC.IsInvalid) throw new Win32Exception("GetDC failed.");

                frames = new(bufferSize);
            }

            public void Dispose()
            {
                foreach (var frame in frames.GetRawBuffer())
                    frame?.Dispose();

                windowDC.Dispose();
            }

            public void AddFrame(
                Gdi32.DeviceContextSafeHandle bitmapDC,
                int windowClientLeft,
                int windowClientTop,
                int windowClientWidth,
                int windowClientHeight,
                uint windowDpi)
            {
                var frame = frames.GetNextRef() ??= new();
                frame.Overwrite(bitmapDC, windowDC, windowClientLeft, windowClientTop, windowClientWidth, windowClientHeight, windowDpi);
            }

            public Frame[] GetFramesSnapshot() => frames.ToArray();
        }
    }
}
