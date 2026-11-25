/*
 * SystemCursors - hides, restores and parks the mouse cursor system-wide.
 *
 * Hide replaces every standard system cursor with a fully transparent one (SetSystemCursor).
 * Windows keeps that replacement until the cursor scheme is reloaded, even after this process
 * exits, so Restore runs at every startup and exit (see Program). Restore reloads the user's
 * own scheme from the registry, so custom pointers and the accessibility size come back intact.
 */
using System;
using System.Runtime.InteropServices;

using CursorCloaker.Core;

namespace CursorCloaker.Win32
{
    internal static class SystemCursors
    {
        private const uint SPI_SETCURSORS = 0x0057;
        private const int CURSOR_SIZE = 32;

        // Every OCR_* id Windows draws for ordinary pointing, text, resizing, busy, link and help.
        private static readonly uint[] SYSTEM_CURSOR_IDS =
        {
            32512, // OCR_NORMAL
            32513, // OCR_IBEAM
            32514, // OCR_WAIT
            32515, // OCR_CROSS
            32516, // OCR_UP
            32642, // OCR_SIZENWSE
            32643, // OCR_SIZENESW
            32644, // OCR_SIZEWE
            32645, // OCR_SIZENS
            32646, // OCR_SIZEALL
            32648, // OCR_NO
            32649, // OCR_HAND
            32650, // OCR_APPSTARTING
            32651, // OCR_HELP
        };

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint action, uint param, IntPtr vparam, uint winIni);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreateCursor(IntPtr instance, int hotX, int hotY, int width, int height, byte[] andPlane, byte[] xorPlane);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetSystemCursor(IntPtr cursor, uint id);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyCursor(IntPtr cursor);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT point);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT point, uint flags);

        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT { public int X, Y; }

        /// <summary>True between Hide and the next Restore.</summary>
        public static bool IsHidden { get; private set; }

        /// <summary>Swap every system cursor for a transparent one. Returns false if any swap failed.</summary>
        public static bool Hide()
        {
            // AND plane all 1s + XOR plane all 0s = "leave the screen unchanged" for every pixel.
            int planeBytes = CURSOR_SIZE * CURSOR_SIZE / 8;
            var andPlane = new byte[planeBytes];
            for (int i = 0; i < planeBytes; i++) andPlane[i] = 0xFF;
            var xorPlane = new byte[planeBytes];

            IsHidden = true;
            bool allSwapped = true;
            foreach (var id in SYSTEM_CURSOR_IDS)
            {
                // SetSystemCursor takes ownership of (and destroys) the handle, so each id needs its own.
                var blank = CreateCursor(IntPtr.Zero, 0, 0, CURSOR_SIZE, CURSOR_SIZE, andPlane, xorPlane);
                if (blank == IntPtr.Zero) { allSwapped = false; continue; }
                if (!SetSystemCursor(blank, id)) { DestroyCursor(blank); allSwapped = false; }
            }
            return allSwapped;
        }

        /// <summary>Reload the user's cursor scheme, undoing any Hide.</summary>
        public static bool Restore()
        {
            IsHidden = false;
            return SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0);
        }

        public static POINT GetPosition()
        {
            GetCursorPos(out var point);
            return point;
        }

        /// <summary>Move the cursor to the given corner or edge of the monitor it is on.</summary>
        public static POINT Park(ParkPosition position)
        {
            var current = GetPosition();
            var monitor = Monitors.BoundsOf(MonitorFromPoint(current, Monitors.MONITOR_DEFAULTTONEAREST));
            ScreenGeometry.ParkPoint(monitor, position, out int x, out int y);
            SetCursorPos(x, y);
            return new POINT { X = x, Y = y };
        }
    }
}
