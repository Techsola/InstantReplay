using System;
using System.Collections.Generic;
using Techsola.InstantReplay.Native;

namespace Techsola.InstantReplay
{
    partial class InstantReplayCamera
    {
        private sealed class WindowEnumerator
        {
            private readonly uint currentProcessId;
            private readonly List<IntPtr> list = new();
            private readonly User32.WNDENUMPROC callback;

            public WindowEnumerator()
            {
                callback = EnumWindowsCallback;

#if !NETFRAMEWORK
                currentProcessId = (uint)Environment.ProcessId;
#else
                currentProcessId = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
#endif
            }

            public IntPtr[] GetCurrentWindowHandlesInZOrder()
            {
                User32.EnumWindows(callback, lParam: IntPtr.Zero);

#if !NET35
                if (list.Count == 0) return Array.Empty<IntPtr>();
#endif

                var array = list.ToArray();
                list.Clear();
                return array;
            }

            private bool EnumWindowsCallback(IntPtr hWnd, IntPtr lParam)
            {
                _ = User32.GetWindowThreadProcessId(hWnd, out var processId);
                if (processId == currentProcessId) list.Add(hWnd);
                return true;
            }
        }
    }
}
