using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Techsola.InstantReplay
{
    internal static class Extensions
    {
        public static T ThrowWithoutLastErrorAvailableIfInvalid<T>(this T safeHandle, string apiName)
            where T : SafeHandle
        {
            if (safeHandle.IsInvalid) throw new Win32Exception(apiName + " failed.");
            return safeHandle;
        }

        public static T ThrowLastErrorIfInvalid<T>(this T safeHandle)
            where T : SafeHandle
        {
            if (safeHandle.IsInvalid) throw new Win32Exception();
            return safeHandle;
        }
    }
}
