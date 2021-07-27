using System.Collections.Generic;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Techsola.InstantReplay
{
    internal sealed class WindowEnumerator
    {
        private readonly uint currentProcessId;
        private readonly List<HWND> list = new();
        private readonly WNDENUMPROC callback;

        public WindowEnumerator()
        {
            callback = EnumWindowsCallback;

#if !NETFRAMEWORK
            currentProcessId = (uint)System.Environment.ProcessId;
#else
            currentProcessId = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
#endif
        }

        public HWND[] GetCurrentWindowHandlesInZOrder()
        {
            PInvoke.EnumWindows(callback, lParam: default);

#if !NET35
            if (list.Count == 0) return System.Array.Empty<HWND>();
#endif

            var array = list.ToArray();
            list.Clear();
            return array;
        }

        private BOOL EnumWindowsCallback(HWND hWnd, LPARAM lParam)
        {
            var processId = default(uint);
            unsafe { _ = PInvoke.GetWindowThreadProcessId(hWnd, &processId); }
            if (processId == currentProcessId) list.Add(hWnd);
            return true;
        }
    }
}
