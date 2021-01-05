using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Techsola.InstantReplay.Native;

namespace Techsola.InstantReplay
{
    public static partial class InstantReplayCamera
    {
        private const int MillisecondsBeforeBitBltingNewWindow = 300;
        private const int FramesPerSecond = 10;
        private const int DurationInSeconds = 10;
        private const int BufferSize = DurationInSeconds * FramesPerSecond;

        private static Timer? timer;
        private static WindowEnumerator? windowEnumerator;
        private static Gdi32.DeviceContextSafeHandle? bitmapDC;
        private static readonly ReaderWriterLockSlim FrameLock = new();
        private static readonly Dictionary<IntPtr, WindowState> InfoByWindowHandle = new();
        private static readonly CircularBuffer<(int X, int Y, IntPtr CursorHandle)?> CursorFrames = new(BufferSize);

        public static void Start()
        {
            if (Volatile.Read(ref timer) is not null) return;

            var newTimer = new Timer(AddFrames);

            if (Interlocked.CompareExchange(ref timer, newTimer, null) is not null)
            {
                newTimer.Dispose();
            }
            else
            {
                // Consider varying timer frequency when there are no visible windows to e.g. 1 second
                newTimer.Change(dueTime: TimeSpan.Zero, period: TimeSpan.FromSeconds(1.0 / FramesPerSecond));
            }
        }

        private static void AddFrames(object? state)
        {
            if (!FrameLock.TryEnterWriteLock(TimeSpan.Zero)) return;
            try
            {
                var cursorInfo = new User32.CURSORINFO { cbSize = Marshal.SizeOf<User32.CURSORINFO>() };
                if (!User32.GetCursorInfo(ref cursorInfo)) throw new Win32Exception();

                var currentWindows = (windowEnumerator ??= new()).GetCurrentWindowHandlesInZOrder();

                bitmapDC ??= CreateScreenDC();

                var now = Stopwatch.GetTimestamp();

                lock (InfoByWindowHandle)
                {
                    CursorFrames.Add((cursorInfo.flags & (User32.CURSOR.SHOWING | User32.CURSOR.SUPPRESSED)) == User32.CURSOR.SHOWING
                        ? (cursorInfo.ptScreenPos.x, cursorInfo.ptScreenPos.y, cursorInfo.hCursor)
                        : null);

                    var zOrder = 0u;
                    foreach (var window in currentWindows)
                    {
                        if (!InfoByWindowHandle.TryGetValue(window, out var windowState))
                        {
                            if (User32.IsWindowVisible(window))
                            {
                                windowState = new(window, firstSeen: now, BufferSize);
                                InfoByWindowHandle.Add(window, windowState);
                            }
                            continue;
                        }
                        else
                        {
                            windowState.LastSeen = now;

                            if ((now - windowState.FirstSeen) < Stopwatch.Frequency * MillisecondsBeforeBitBltingNewWindow / 1000)
                                continue;
                        }

                        if (!User32.IsWindowVisible(window)) continue;

                        var clientTopLeft = default(POINT);
                        if (!User32.ClientToScreen(window, ref clientTopLeft)) throw new Win32Exception("ClientToScreen failed.");
                        if (!User32.GetClientRect(window, out var clientRect)) throw new Win32Exception();

                        windowState.AddFrame(bitmapDC, clientTopLeft.x, clientTopLeft.y, clientRect.right, clientRect.bottom, User32.GetDpiForWindow(window), zOrder);
                        zOrder++;
                    }

                    var closedWindowsWithNoFrames = new List<IntPtr>();

                    foreach (var entry in InfoByWindowHandle)
                    {
                        if (entry.Value.LastSeen != now)
                        {
                            entry.Value.MarkClosed();
                            entry.Value.DisposeNextFrame(out var allFramesDisposed);

                            if (allFramesDisposed)
                            {
                                entry.Value.Dispose();
                                closedWindowsWithNoFrames.Add(entry.Key);
                            }
                        }
                    }

                    foreach (var window in closedWindowsWithNoFrames)
                        InfoByWindowHandle.Remove(window);
                }
            }
            finally
            {
                FrameLock.ExitWriteLock();
            }
        }

        private static Gdi32.DeviceContextSafeHandle CreateScreenDC()
        {
            var dc = Gdi32.CreateCompatibleDC(IntPtr.Zero);
            if (dc.IsInvalid) throw new Win32Exception("CreateCompatibleDC failed.");
            return dc;
        }

        public static void SaveGif(Stream stream)
        {
            FrameLock.EnterReadLock();
            try
            {
                var cursorFrames = CursorFrames.ToArray();

                var framesByWindow = InfoByWindowHandle.Values.Select(i => i.GetFramesSnapshot()).ToList();

                var minLeft = int.MaxValue;
                var maxRight = int.MinValue;
                var minTop = int.MaxValue;
                var maxBottom = int.MinValue;
                var maxFrameCount = 0;

                foreach (var frameList in framesByWindow)
                {
                    for (var i = 0; i < frameList.Length; i++)
                    {
                        if (frameList[i] is not { } frame) continue;

                        var frameCount = frameList.Length - i;
                        if (maxFrameCount < frameCount) maxFrameCount = frameCount;

                        if (minLeft > frame.WindowClientLeft) minLeft = frame.WindowClientLeft;
                        if (minTop > frame.WindowClientTop) minTop = frame.WindowClientTop;
                        if (maxRight < frame.WindowClientLeft + frame.WindowClientWidth) maxRight = frame.WindowClientLeft + frame.WindowClientWidth;
                        if (maxBottom < frame.WindowClientTop + frame.WindowClientHeight) maxBottom = frame.WindowClientTop + frame.WindowClientHeight;
                    }
                }

                var compositionOffset = (X: -minLeft, Y: -minTop);
                var compositionWidth = maxRight - minLeft;
                var compositionHeight = maxBottom - minTop;

                if (bitmapDC is not { IsInvalid: false })
                    throw new InvalidOperationException("infoByWindowHandle should be empty if bitmapDC is not valid.");

                using var composition = new Composition(compositionWidth, compositionHeight, Frame.BitsPerPixel);

                var cursorRenderer = new AnimatedCursorRenderer(composition.DeviceContext);

                var writer = new GifWriter(stream);

                writer.BeginStream(
                    (ushort)compositionWidth,
                    (ushort)compositionHeight,
                    globalColorTable: false, // TODO: optimize to use the global color table for the majority palette if more than one frame can use the same palette
                    sourceImageBitsPerPrimaryColor: 8, // Actually 24, but this is the maximum value. Not used anyway.
                    globalColorTableIsSorted: false,
                    globalColorTableSize: 0,
                    globalColorTableBackgroundColorIndex: 0);

                writer.WriteLoopingExtensionBlock();

                var quantizer = new WuQuantizer();

                var paletteBuffer = new (byte R, byte G, byte B)[256];
                var indexedImageBuffer = new byte[compositionWidth * compositionHeight];

                var framesToDraw = new List<Frame>();

                for (var i = 0; i < maxFrameCount; i++)
                {
                    // TODO: be smarter about the area that actually needs to be cleared?
                    composition.Clear(0, 0, compositionWidth, compositionHeight);

                    framesToDraw.Clear();

                    foreach (var frameList in framesByWindow)
                    {
                        var index = i - maxFrameCount + frameList.Length;
                        if (index >= 0 && frameList[index] is { } frame)
                            framesToDraw.Add(frame);
                    }

                    framesToDraw.Sort((a, b) => b.ZOrder.CompareTo(a.ZOrder));

                    foreach (var frame in framesToDraw)
                        frame.Compose(bitmapDC, composition.DeviceContext, compositionOffset);

                    if (cursorFrames[i - maxFrameCount + cursorFrames.Length] is { } cursor)
                        cursorRenderer.Render(cursor.CursorHandle, cursor.X + compositionOffset.X, cursor.Y + compositionOffset.Y);

                    quantizer.Quantize(composition.Pixels, paletteBuffer, out var paletteLength, indexedImageBuffer);

                    var bitsPerIndexedPixel = GetBitsPerPixel(paletteLength);

                    var isLastFrame = i == maxFrameCount - 1;

                    writer.WriteGraphicControlExtensionBlock(
                        delayInHundredthsOfASecond: isLastFrame ? 400 : 10,
                        transparentColorIndex: null);

                    writer.WriteImageDescriptor(
                        left: 0, // TODO: optimize to crop to only changed pixels (before choosing a palette, too) and potentially double the length of the previous delay
                        top: 0,
                        width: (ushort)compositionWidth,
                        height: (ushort)compositionHeight,
                        localColorTable: true,
                        isInterlaced: false,
                        localColorTableIsSorted: false,
                        localColorTableSize: (byte)(bitsPerIndexedPixel - 1)); // Means 2^(localColorTableSize+1) entries

                    writer.WriteColorTable(paletteBuffer, paletteLength: 1 << bitsPerIndexedPixel);
                    writer.WriteImageData(indexedImageBuffer, bitsPerIndexedPixel);
                }

                writer.EndStream();
            }
            finally
            {
                FrameLock.ExitReadLock();
            }
        }

        private static byte GetBitsPerPixel(int paletteLength)
        {
            if (paletteLength > 256)
                throw new ArgumentOutOfRangeException(nameof(paletteLength), paletteLength, "Palette length must be no greater than 256.");

            // Distribution is expected to be heavily weighted towards large palettes
            if (paletteLength > 128) return 8;
            if (paletteLength > 64) return 7;
            if (paletteLength > 32) return 6;
            if (paletteLength > 16) return 5;
            if (paletteLength > 8) return 4;
            if (paletteLength > 4) return 3;
            if (paletteLength > 2) return 2;
            return 1;
        }
    }
}
