/*
 * Monitors - monitor bounds in physical pixels (the process is per-monitor DPI aware).
 */
using System;
using System.Runtime.InteropServices;

using CursorCloaker.Core;

namespace CursorCloaker.Win32
{
    internal static class Monitors
    {
        public const uint MONITOR_DEFAULTTONULL = 0;
        public const uint MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int Size;
            public RECT Monitor;
            public RECT WorkArea;
            public uint Flags;
        }

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

        public static ScreenRect BoundsOf(IntPtr monitor)
        {
            var info = new MONITORINFO { Size = Marshal.SizeOf(typeof(MONITORINFO)) };
            if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info)) return new ScreenRect();
            return ToScreenRect(info.Monitor);
        }

        public static ScreenRect ToScreenRect(RECT rect)
        {
            return new ScreenRect(rect.Left, rect.Top, rect.Right, rect.Bottom);
        }
    }
}
