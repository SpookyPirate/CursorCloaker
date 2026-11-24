/*
 * SystemCursors - restores the user's system cursor scheme.
 *
 * Hiding works by replacing the system cursors with a blank one (SetSystemCursor). Windows keeps
 * that replacement until something reloads the scheme, even after this process exits, so a
 * restore runs at every startup and exit. Restore reloads the user's own scheme from the
 * registry, so custom pointers come back as they were.
 */
using System;
using System.Runtime.InteropServices;

namespace CursorCloaker.Win32
{
    internal static class SystemCursors
    {
        private const uint SPI_SETCURSORS = 0x0057;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint action, uint param, IntPtr vparam, uint winIni);

        /// <summary>Reload the user's cursor scheme, undoing any blank-cursor swap.</summary>
        public static bool Restore()
        {
            return SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0);
        }
    }
}
