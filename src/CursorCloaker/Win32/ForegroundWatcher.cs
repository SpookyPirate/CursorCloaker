/*
 * ForegroundWatcher - describes the foreground window for CloakPolicy.
 *
 * Read() is polled from a UI-thread timer instead of using EVENT_SYSTEM_FOREGROUND, because a
 * window can turn fullscreen (or back) without the foreground changing. The poll is cheap:
 * process names are cached per process id.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

using CursorCloaker.Core;

namespace CursorCloaker.Win32
{
    internal sealed class ForegroundWatcher
    {
        // The desktop, wallpaper host and taskbars: never cloaked over, and not "fullscreen".
        private static readonly HashSet<string> SHELL_WINDOW_CLASSES = new HashSet<string>(StringComparer.Ordinal)
        {
            "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
        };

        private const int PROCESS_NAME_CACHE_LIMIT = 256;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr window, out Monitors.RECT rect);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);

        private readonly Dictionary<uint, string> processNames = new Dictionary<uint, string>();
        private readonly uint ownProcessId = (uint)Process.GetCurrentProcess().Id;

        public ForegroundInfo Read()
        {
            var window = GetForegroundWindow();
            if (window == IntPtr.Zero) return ForegroundInfo.None;

            GetWindowThreadProcessId(window, out uint processId);
            string processName = ProcessNameOf(processId);
            // CursorCloaker's own windows are treated like the shell: never cloaked over.
            bool isShell = processId == ownProcessId || SHELL_WINDOW_CLASSES.Contains(ClassNameOf(window));
            return new ForegroundInfo(processName, !isShell && IsFullscreen(window), isShell);
        }

        private static bool IsFullscreen(IntPtr window)
        {
            var monitor = MonitorFromWindow(window, Monitors.MONITOR_DEFAULTTONULL);
            if (monitor == IntPtr.Zero || !GetWindowRect(window, out var rect)) return false;
            return ScreenGeometry.CoversMonitor(Monitors.ToScreenRect(rect), Monitors.BoundsOf(monitor));
        }

        private static string ClassNameOf(IntPtr window)
        {
            var name = new StringBuilder(64);
            GetClassName(window, name, name.Capacity);
            return name.ToString();
        }

        private string ProcessNameOf(uint processId)
        {
            if (processNames.TryGetValue(processId, out var cached)) return cached;
            string name;
            try { name = Process.GetProcessById((int)processId).ProcessName; }
            catch (ArgumentException) { name = ""; }        // exited
            catch (InvalidOperationException) { name = ""; }

            // Ids are reused over time; a bounded cache can simply start over.
            if (processNames.Count >= PROCESS_NAME_CACHE_LIMIT) processNames.Clear();
            processNames[processId] = name;
            return name;
        }
    }
}
