namespace Techsola.InstantReplay.Native
{
    /// <summary>
    /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/debug/system-error-codes--0-499-"/>
    /// </summary>
    internal enum ERROR : ushort
    {
        /// <summary>
        /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/debug/system-error-codes--0-499-#ERROR_ACCESS_DENIED"/>
        /// </summary>
        ACCESS_DENIED = 0x5,
        /// <summary>
        /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/debug/system-error-codes--0-499-#ERROR_INVALID_PARAMETER"/>
        /// </summary>
        INVALID_PARAMETER = 0x57,
        /// <summary>
        /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/debug/system-error-codes--1300-1699-#ERROR_INVALID_WINDOW_HANDLE"/>
        /// </summary>
        INVALID_WINDOW_HANDLE = 0x578,
        /// <summary>
        /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/debug/system-error-codes--1300-1699-#ERROR_DC_NOT_FOUND"/>
        /// </summary>
        DC_NOT_FOUND = 0x591,
    }
}
