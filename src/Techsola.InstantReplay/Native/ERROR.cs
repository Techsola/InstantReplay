namespace Techsola.InstantReplay.Native
{
    /// <summary>
    /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/debug/system-error-codes--0-499-"/>
    /// </summary>
    internal enum ERROR : ushort
    {
        /// <summary>
        /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/debug/system-error-codes--0-499-#ERROR_INVALID_PARAMETER"/>
        /// </summary>
        INVALID_PARAMETER = 0x57,
        /// <summary>
        /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/debug/system-error-codes--1300-1699-#ERROR_INVALID_WINDOW_HANDLE"/>
        /// </summary>
        INVALID_WINDOW_HANDLE = 0x578,
    }
}
