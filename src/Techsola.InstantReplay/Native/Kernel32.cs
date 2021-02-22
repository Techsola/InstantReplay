using System.Runtime.InteropServices;

namespace Techsola.InstantReplay.Native
{
    internal static partial class Kernel32
    {
        [DllImport("kernel32.dll")]
        public static extern void SetLastError(uint dwErrCode);
    }
}
