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
        private const int SecondsBeforeBitBltingNewWindow = 1;
        private const int FramesPerSecond = 10;
        private const int DurationInSeconds = 10;
        private const int BufferSize = DurationInSeconds * FramesPerSecond;

        private static Timer? timer;
        private static int isTakingSnapshot;
        private static WindowEnumerator? windowEnumerator;
        private static Gdi32.DeviceContextSafeHandle? bitmapDC;
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
            if (Interlocked.Exchange(ref isTakingSnapshot, 1) != 0) return;
            try
            {
                var cursorInfo = new User32.CURSORINFO { cbSize = Marshal.SizeOf<User32.CURSORINFO>() };
                if (!User32.GetCursorInfo(ref cursorInfo)) throw new Win32Exception();

                var currentWindows = (windowEnumerator ??= new()).GetCurrentWindowHandles();

                bitmapDC ??= CreateScreenDC();

                var now = Stopwatch.GetTimestamp();

                lock (InfoByWindowHandle)
                {
                    CursorFrames.Add((cursorInfo.flags & (User32.CURSOR.SHOWING | User32.CURSOR.SUPPRESSED)) == User32.CURSOR.SHOWING
                        ? (cursorInfo.ptScreenPos.x, cursorInfo.ptScreenPos.y, cursorInfo.hCursor)
                        : null);

                    foreach (var window in currentWindows)
                    {
                        if (!User32.IsWindowVisible(window)) continue;

                        if (!InfoByWindowHandle.TryGetValue(window, out var windowState))
                        {
                            windowState = new(window, firstSeen: now, BufferSize);
                            InfoByWindowHandle.Add(window, windowState);
                            continue;
                        }
                        else
                        {
                            windowState.LastSeen = now;

                            if ((now - windowState.FirstSeen) < SecondsBeforeBitBltingNewWindow * Stopwatch.Frequency)
                                continue;
                        }

                        var clientTopLeft = default(POINT);
                        if (!User32.ClientToScreen(window, ref clientTopLeft)) throw new Win32Exception("ClientToScreen failed.");
                        if (!User32.GetClientRect(window, out var clientRect)) throw new Win32Exception();

                        windowState.AddFrame(bitmapDC, clientTopLeft.x, clientTopLeft.y, clientRect.right, clientRect.bottom, User32.GetDpiForWindow(window));
                    }

                    foreach (var entry in InfoByWindowHandle.ToList())
                    {
                        if (entry.Value.LastSeen != now)
                        {
                            // TODO: Still keep around for ten seconds even though the window is gone
                            InfoByWindowHandle.Remove(entry.Key);
                            entry.Value.Dispose();
                        }
                    }
                }
            }
            finally
            {
                Volatile.Write(ref isTakingSnapshot, 0);
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
            lock (InfoByWindowHandle)
            {
                var cursorFrames = CursorFrames.ToArray();
                var cursorHotspotByHandle = new Dictionary<IntPtr, (uint X, uint Y)>();
                var cursorAnimationStepByHandle = new Dictionary<IntPtr, (uint Current, uint Max)>();

                var framesByWindow = InfoByWindowHandle.Values.Select(i => i.GetFramesSnapshot()).ToList();
                var frameCount = framesByWindow.Max(frames => frames.Length);

                var allFrames = framesByWindow.SelectMany(frames => frames);
                var compositionOffset = (X: -allFrames.Min(f => f.WindowClientLeft), Y: -allFrames.Min(f => f.WindowClientTop));
                var compositionWidth = allFrames.Max(f => f.WindowClientLeft + f.WindowClientWidth) + compositionOffset.X;
                var compositionHeight = allFrames.Max(f => f.WindowClientTop + f.WindowClientHeight) + compositionOffset.Y;

                if (bitmapDC is not { IsInvalid: false })
                    throw new InvalidOperationException("infoByWindowHandle should be empty if bitmapDC is not valid.");

                using var compositionDC = CreateScreenDC();
                using var compositionBitmap = Gdi32.CreateDIBSection(compositionDC, new()
                {
                    bmiHeader =
                    {
                        biSize = Marshal.SizeOf<Gdi32.BITMAPINFOHEADER>(),
                        biWidth = compositionWidth,
                        biHeight = -compositionHeight,
                        biPlanes = 1,
                        biBitCount = Frame.BitsPerPixel,
                    },
                }, Gdi32.DIB.RGB_COLORS, out var compositionPixelDataPointer, hSection: IntPtr.Zero, offset: 0);

                if (Gdi32.SelectObject(compositionDC, compositionBitmap).IsInvalid)
                    throw new Win32Exception("SelectObject failed.");

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

                ColorEnumerable colorEnumerable;
                unsafe
                {
                    colorEnumerable = new(
                        (byte*)compositionPixelDataPointer,
                        (uint)compositionWidth,
                        stride: ((((uint)compositionWidth * 3) + 3) / 4) * 4,
                        (uint)compositionHeight);
                }

                var paletteBuffer = new (byte R, byte G, byte B)[256];
                var indexedImageBuffer = new byte[compositionWidth * compositionHeight];

                for (var i = 0; i < frameCount; i++)
                {
                    // TODO: be smarter about the area that actually needs to be cleared?
                    if (!Gdi32.BitBlt(compositionDC, 0, 0, compositionWidth, compositionHeight, IntPtr.Zero, 0, 0, Gdi32.RasterOperation.BLACKNESS))
                        throw new Win32Exception("BitBlt failed.");

                    // TODO: z order
                    foreach (var frameList in framesByWindow)
                    {
                        var index = i - frameCount + frameList.Length;
                        if (index < 0) continue;
                        frameList[index].Compose(bitmapDC, compositionDC, compositionOffset);
                    }

                    if (cursorFrames[i - frameCount + cursorFrames.Length] is { } cursor)
                    {
                        if (!cursorHotspotByHandle.TryGetValue(cursor.CursorHandle, out var cursorHotspot))
                        {
                            if (!User32.GetIconInfo(cursor.CursorHandle, out var iconInfo)) throw new Win32Exception();
                            new Gdi32.BitmapSafeHandle(iconInfo.hbmMask).Dispose();
                            new Gdi32.BitmapSafeHandle(iconInfo.hbmColor).Dispose();

                            cursorHotspot = (iconInfo.xHotspot, iconInfo.yHotspot);
                            cursorHotspotByHandle.Add(cursor.CursorHandle, cursorHotspot);
                        }

                        if (!cursorAnimationStepByHandle.TryGetValue(cursor.CursorHandle, out var cursorAnimationStep))
                            cursorAnimationStep = (Current: 0, Max: uint.MaxValue);

                        while (!User32.DrawIconEx(
                            compositionDC,
                            compositionOffset.X + cursor.X - (int)cursorHotspot.X,
                            compositionOffset.Y + cursor.Y - (int)cursorHotspot.Y,
                            cursor.CursorHandle,
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
                        cursorAnimationStepByHandle[cursor.CursorHandle] = cursorAnimationStep;
                    }

                    quantizer.Quantize(colorEnumerable, paletteBuffer, out var paletteLength, indexedImageBuffer);

                    var bitsPerIndexedPixel = GetBitsPerPixel(paletteLength);

                    var isLastFrame = i == frameCount - 1;

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
