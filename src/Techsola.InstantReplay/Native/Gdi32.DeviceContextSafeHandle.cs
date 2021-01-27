using System;
using System.Runtime.InteropServices;

namespace Techsola.InstantReplay.Native
{
    partial class Gdi32
    {
        public abstract class DeviceContextSafeHandle : SafeHandle
        {
            protected DeviceContextSafeHandle()
                : base(invalidHandleValue: IntPtr.Zero, ownsHandle: true)
            {
            }

            protected DeviceContextSafeHandle(IntPtr handle)
                : this()
            {
                SetHandle(handle);
            }

            public sealed override bool IsInvalid => handle == IntPtr.Zero;
        }
    }
}
