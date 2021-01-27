using System;
using System.Runtime.InteropServices;

namespace Techsola.InstantReplay.Native
{
    partial class Gdi32
    {
        public sealed class CreatedDeviceContextSafeHandle : DeviceContextSafeHandle
        {
            private CreatedDeviceContextSafeHandle()
            {
            }

            protected override bool ReleaseHandle() => DeleteDC(handle);

            /// <summary>
            /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-deletedc"/>
            /// </summary>
            [DllImport("gdi32.dll")]
            private static extern bool DeleteDC(IntPtr hdc);
        }
    }
}
