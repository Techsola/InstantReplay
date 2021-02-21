using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Techsola.InstantReplay.Native;

namespace Techsola.InstantReplay
{
    /// <summary>
    /// <para>
    /// Buffers timed screenshots for all windows in the current process and tracks the mouse cursor so that an animated
    /// GIF can be created on demand, for inclusion in a crash report for example.
    /// </para>
    /// <para>
    /// Call <see cref="Start"/> once when the application starts, and then call <see cref="SaveGif"/> to obtain a GIF
    /// of the last ten seconds up to that point in time.
    /// </para>
    /// </summary>
    public static partial class InstantReplayCamera
    {
        private const int MillisecondsBeforeBitBltingNewWindow = 300;
        private const int FramesPerSecond = 10;
        private const int DurationInSeconds = 10;
        private const int BufferSize = DurationInSeconds * FramesPerSecond;

        private static readonly FrequencyLimiter BackgroundExceptionReportLimiter = new(
            new(maximumCount: 3, withinDuration: TimeSpan.FromHours(1)));

        private static Timer? timer;
        private static Action<Exception>? reportBackgroundException;
        private static WindowEnumerator? windowEnumerator;
        private static Gdi32.DeviceContextSafeHandle? bitmapDC;
        private static readonly object FrameLock = new();
        private static readonly Dictionary<IntPtr, WindowState> InfoByWindowHandle = new();
        private static readonly CircularBuffer<(long Timestamp, (int X, int Y, IntPtr Handle)? Cursor)> Frames = new(BufferSize);
        private static bool isDisabled;

        /// <summary>
        /// <para>
        /// Begins buffering up to ten seconds of screenshots for all windows in the current process, including windows
        /// that have not been created yet, as well as the mouse cursor.
        /// </para>
        /// <para>
        /// Call this during the start of your application. <see cref="SaveGif"/> will only have access to frames that
        /// occurred after this call. Subsequent calls to this method have no effect.
        /// </para>
        /// <para>
        /// This method is thread-safe and does not behave differently when called from the UI thread or any other
        /// thread.
        /// </para>
        /// </summary>
        /// <param name="reportBackgroundException">
        /// <para>
        /// Please report exceptions just as you would for <see cref="AppDomain.UnhandledException"/> and other
        /// top-level exception events such as <c>TaskScheduler.UnobservedTaskException</c> and
        /// <c>Application.ThreadException</c>. When you come across an exception that appears to be a flaw in
        /// Techsola.InstantReplay, please report it at <see href="https://github.com/Techsola/InstantReplay/issues"/>.
        /// </para>
        /// <para>
        /// Ideally there will be no unhandled exceptions, but they are a normal part of the development cycle. This
        /// parameter is provided so that the runtime does not forcibly terminate your app due to an exception in the
        /// timer callback in Techsola.InstantReplay.
        /// </para>
        /// </param>
        public static void Start(Action<Exception> reportBackgroundException)
        {
            if (reportBackgroundException is null) throw new ArgumentNullException(nameof(reportBackgroundException));

            if (Interlocked.CompareExchange(ref InstantReplayCamera.reportBackgroundException, reportBackgroundException, null) is not null)
            {
                // This method has been called before. Ignore.
                return;
            }

            // Consider varying timer frequency when there are no visible windows to e.g. 1 second
            timer = new Timer(AddFrames, state: null, dueTime: TimeSpan.Zero, period: TimeSpan.FromSeconds(1.0 / FramesPerSecond));
        }

        private static void AddFrames(object? state)
        {
            if (isDisabled) return;

            var now = Stopwatch.GetTimestamp();

            try
            {
                var lockTaken = false;
                try
                {
#if NET35
                    lockTaken = Monitor.TryEnter(FrameLock);
#else
                    Monitor.TryEnter(FrameLock, ref lockTaken);
#endif
                    if (!lockTaken) return;

                    if (isDisabled) return;

                    var cursorInfo = new User32.CURSORINFO { cbSize = Marshal.SizeOf(typeof(User32.CURSORINFO)) };
                    if (!User32.GetCursorInfo(ref cursorInfo))
                    {
                        var lastError = Marshal.GetLastWin32Error();
                        // Access is denied while the workstation is locked.
                        if ((ERROR)lastError != ERROR.ACCESS_DENIED)
                            throw new Win32Exception(lastError);
                    }

                    var currentWindows = (windowEnumerator ??= new()).GetCurrentWindowHandlesInZOrder();

                    bitmapDC ??= Gdi32.CreateCompatibleDC(IntPtr.Zero).ThrowWithoutLastErrorAvailableIfInvalid(nameof(Gdi32.CreateCompatibleDC));

                    lock (InfoByWindowHandle)
                    {
                        Frames.Add((
                            Timestamp: now,
                            Cursor: (cursorInfo.flags & (User32.CURSOR.SHOWING | User32.CURSOR.SUPPRESSED)) == User32.CURSOR.SHOWING
                                ? (cursorInfo.ptScreenPos.x, cursorInfo.ptScreenPos.y, cursorInfo.hCursor)
                                : null));

                        var zOrder = 0u;
                        var needsGdiFlush = false;

                        foreach (var window in currentWindows)
                        {
                            if (!InfoByWindowHandle.TryGetValue(window, out var windowState))
                            {
                                // The window hasn't been seen before
                                if (User32.IsWindowVisible(window) && User32.GetDC(window) is { IsInvalid: false } windowDC)
                                {
                                    windowState = new(windowDC, firstSeen: now, BufferSize);
                                    InfoByWindowHandle.Add(window, windowState);
                                }
                            }
                            else
                            {
                                // The window has been seen before
                                if ((now - windowState.FirstSeen) < Stopwatch.Frequency * MillisecondsBeforeBitBltingNewWindow / 1000)
                                {
                                    // No frames have been added yet
                                }
                                else if (!User32.IsWindowVisible(window))
                                {
                                    windowState.AddInvisibleFrame();
                                }
                                else if (GetWindowMetricsIfExists(window) is { } metrics)
                                {
                                    windowState.AddFrame(bitmapDC, metrics, zOrder, ref needsGdiFlush);
                                    zOrder++;
                                }
                                else
                                {
                                    // The window will be detected as closed
                                    continue;
                                }

                                windowState.LastSeen = now; // Keeps the window from being detected as closed
                            }
                        }

                        // Make sure to flush on the same thread that called the GDI function in case this thread goes away.
                        // (https://docs.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-gdiflush#remarks)
                        if (needsGdiFlush && !Gdi32.GdiFlush())
                            throw new Win32Exception("GdiFlush failed.");

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
                    if (lockTaken) Monitor.Exit(FrameLock);
                }
            }
#pragma warning disable CA1031 // If this is not caught, the runtime forcibly terminates the app.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                if (BackgroundExceptionReportLimiter.TryAddOccurrence(now))
                {
                    reportBackgroundException!.Invoke(ex);
                }
                else
                {
                    isDisabled = true;
                    timer!.Dispose();
                }
            }
        }

        private static WindowMetrics? GetWindowMetricsIfExists(IntPtr window)
        {
            if (!User32.ClientToScreen(window, out var clientTopLeft))
                return null; // This is what happens when the window handle becomes invalid.

            if (!User32.GetClientRect(window, out var clientRect))
            {
                var lastError = Marshal.GetLastWin32Error();
                if ((ERROR)lastError == ERROR.INVALID_WINDOW_HANDLE) return null;
                throw new Win32Exception(lastError);
            }

            var dpi = User32.GetDpiForWindow(window);
            if (dpi == 0) return null; // This is what happens when the window handle becomes invalid.

            return new(clientTopLeft.x, clientTopLeft.y, clientRect.right, clientRect.bottom, dpi);
        }

#if !NET35
        /// <summary>
        /// <para>
        /// Blocks while synchronously compositing, quantizing, and encoding all buffered screenshots and cursor
        /// movements and writing them to the array that is returned. No frames are erased by this call, and no new
        /// frames are buffered while this method is executing.
        /// </para>
        /// <para>
        /// ⚠ Consider using <see cref="System.Threading.Tasks.Task.Run(Action)"/> to prevent the CPU-intensive
        /// quantizing and encoding from making the application unresponsive.
        /// </para>
        /// <para>
        /// This method is thread-safe and does not behave differently when called from the UI thread or any other
        /// thread.
        /// </para>
        /// </summary>
#else
        /// <summary>
        /// <para>
        /// Generates a GIF of the currently-buffered screenshots and cursor movements. Returns <see langword="null"/>
        /// if there are no screenshots currently buffered. No frames are erased by this call, and no new frames are
        /// buffered while this method is executing.
        /// </para>
        /// <para>
        /// This method is thread-safe and does not behave differently when called from the UI thread or any other
        /// thread.
        /// </para>
        /// </summary>
#endif
        public static byte[]? SaveGif()
        {
            if (isDisabled) return null;

            lock (FrameLock)
            {
                var frames = Frames.ToArray();
                var framesByWindow = InfoByWindowHandle.Values.Select(i => i.GetFramesSnapshot()).ToList();

                var renderer = new CompositionRenderer(frames, framesByWindow);
                if (renderer.FrameCount == 0)
                    return null;

                if (bitmapDC is not { IsInvalid: false })
                    throw new InvalidOperationException("infoByWindowHandle should be empty if bitmapDC is not valid.");

                using var composition1 = new Composition(renderer.CompositionWidth, renderer.CompositionHeight, Frame.BitsPerPixel);
                using var composition2 = new Composition(renderer.CompositionWidth, renderer.CompositionHeight, Frame.BitsPerPixel);

                var frameSink = new FrameSink(renderer.CompositionWidth, renderer.CompositionHeight);

                var comparisonBuffer = composition1;
                var emitBuffer = composition2;

                var startingTimestamp = frames[frames.Length - renderer.FrameCount].Timestamp;
                var totalEmittedDelays = 0L;

                // First frame
                var needsGdiFlush = false;
                renderer.Compose(frameIndex: 0, emitBuffer, bitmapDC, ref needsGdiFlush, out var emitBufferNonEmptyArea);
                var emitBoundingRectangle = new UInt16Rectangle(0, 0, renderer.CompositionWidth, renderer.CompositionHeight);

                var comparisonBufferNonEmptyArea = default(UInt16Rectangle);

                for (var i = 1; i < renderer.FrameCount; i++)
                {
                    comparisonBuffer.Clear(
                        comparisonBufferNonEmptyArea.Left,
                        comparisonBufferNonEmptyArea.Top,
                        comparisonBufferNonEmptyArea.Width,
                        comparisonBufferNonEmptyArea.Height,
                        ref needsGdiFlush);

                    renderer.Compose(i, comparisonBuffer, bitmapDC, ref needsGdiFlush, out comparisonBufferNonEmptyArea);

                    var boundingRectangle = emitBufferNonEmptyArea.Union(comparisonBufferNonEmptyArea);

                    // Required before accessing pixel data
                    if (needsGdiFlush)
                    {
                        if (!Gdi32.GdiFlush()) throw new Win32Exception("GdiFlush failed.");
                        needsGdiFlush = false;
                    }

                    DiffBoundsDetector.CropToChanges(emitBuffer, comparisonBuffer, ref boundingRectangle);

                    if (boundingRectangle.IsEmpty) continue;

                    var changeTimestamp = frames[i - renderer.FrameCount + frames.Length].Timestamp;
                    var stopwatchTicksPerHundredthOfASecond = Stopwatch.Frequency / 100;
                    var totalHundredthsOfASecond = (changeTimestamp - startingTimestamp) / stopwatchTicksPerHundredthOfASecond;

                    frameSink.EmitFrame(emitBuffer, emitBoundingRectangle, (ushort)(totalHundredthsOfASecond - totalEmittedDelays));
                    totalEmittedDelays = totalHundredthsOfASecond;

                    var nextBuffer = emitBuffer;
                    emitBuffer = comparisonBuffer;
                    comparisonBuffer = nextBuffer;

                    var nextBufferNonEmptyArea = emitBufferNonEmptyArea;
                    emitBufferNonEmptyArea = comparisonBufferNonEmptyArea;
                    comparisonBufferNonEmptyArea = nextBufferNonEmptyArea;

                    emitBoundingRectangle = boundingRectangle;
                }

                frameSink.EmitFrame(emitBuffer, emitBoundingRectangle, delayInHundredthsOfASecond: 400);

                return frameSink.End();
            }
        }

        private static byte GetBitsPerPixel(uint paletteLength)
        {
            if (paletteLength > 256)
                throw new ArgumentOutOfRangeException(nameof(paletteLength), paletteLength, "Palette length must be no greater than 256.");

#if NETFRAMEWORK
            // Distribution is expected to be heavily weighted towards large palettes
            if (paletteLength > 128) return 8;
            if (paletteLength > 64) return 7;
            if (paletteLength > 32) return 6;
            if (paletteLength > 16) return 5;
            if (paletteLength > 8) return 4;
            if (paletteLength > 4) return 3;
            if (paletteLength > 2) return 2;
            return 1;
#else
            return paletteLength <= 2 ? 1 :
                (byte)((sizeof(uint) * 8) - System.Numerics.BitOperations.LeadingZeroCount(paletteLength - 1));
#endif
        }
    }
}
