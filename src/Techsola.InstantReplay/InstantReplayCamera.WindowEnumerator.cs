using System;
using System.Collections.Generic;
using System.Diagnostics;
using Techsola.InstantReplay.Native;

namespace Techsola.InstantReplay
{
    partial class InstantReplayCamera
    {
        private sealed class WindowEnumerator
        {
            private readonly Process process = Process.GetCurrentProcess();
            private readonly List<IntPtr> list = new();
            private readonly User32.WNDENUMPROC callback;

            public WindowEnumerator()
            {
                callback = EnumThreadWindowsCallback;
            }

            public IntPtr[] GetCurrentWindowHandles()
            {
                foreach (ProcessThread thread in process.Threads)
                {
                    User32.EnumThreadWindows(thread.Id, callback, lParam: IntPtr.Zero);
                }

                process.Refresh();

                if (list.Count == 0) return Array.Empty<IntPtr>();

                var array = list.ToArray();
                list.Clear();
                return array;
            }

            private bool EnumThreadWindowsCallback(IntPtr hWnd, IntPtr lParam)
            {
                list.Add(hWnd);
                return true;
            }
        }
    }
}
