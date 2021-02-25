using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Techsola.InstantReplay.Native
{
    internal static partial class Kernel32
    {
        /// <summary>
        /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/errhandlingapi/nf-errhandlingapi-setlasterror"/>
        /// </summary>
        [SupportedOSPlatform("windows")]
        [DllImport("kernel32.dll")]
        public static extern void SetLastError(uint dwErrCode);
    }
}
