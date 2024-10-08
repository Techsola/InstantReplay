using System;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;

namespace Techsola.InstantReplay.Native;

// Workaround for https://github.com/microsoft/CsWin32/issues/209
internal sealed class DeleteDCSafeHandle : SafeHandle
{
    public DeleteDCSafeHandle(IntPtr handle) : base(invalidHandleValue: IntPtr.Zero, ownsHandle: true)
    {
        SetHandle(handle);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        return (bool)PInvoke.DeleteDC((HDC)handle);
    }
}
