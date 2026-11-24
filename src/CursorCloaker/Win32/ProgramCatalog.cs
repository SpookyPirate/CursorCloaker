/*
 * ProgramCatalog - finds programs to exclude: from an exe path, running processes, or open windows.
 *
 * Every source produces ProgramEntry values keyed by process name (lower case, no ".exe"), the
 * same key CloakSettings.ExcludedProcesses stores. Display names come from the exe's file
 * description, falling back to the file name.
 *
 * Paths come from QueryFullProcessImageName with PROCESS_QUERY_LIMITED_INFORMATION, which works
 * for elevated and 64-bit processes where Process.MainModule throws.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using CursorCloaker.Core;

namespace CursorCloaker.Win32
{
    /// <summary>One program as shown in the exclusion lists.</summary>
    internal sealed class ProgramEntry
    {
        public string ProcessName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        /// <summary>Second line: the exe file name, or the window title for the window picker.</summary>
        public string Detail { get; set; } = "";
        public string ExecutablePath { get; set; } = "";
        public Bitmap Icon { get; set; }
        /// <summary>Shown greyed out with "Excluded" in pickers.</summary>
        public bool IsAlreadyExcluded { get; set; }
    }

    internal static class ProgramCatalog
    {
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private const int GW_OWNER = 4;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int DWMWA_CLOAKED = 14;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inherit, uint processId);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder path, ref uint size);

        private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr window, int command);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr window, int index);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr window, StringBuilder text, int capacity);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);

        /// <summary>An entry for an exe chosen by browsing.</summary>
        public static ProgramEntry FromExecutable(string path)
        {
            return Describe(Path.GetFileNameWithoutExtension(path), path);
        }

        /// <summary>Entries for the stored exclusions; paths are filled in from running copies where possible.</summary>
        public static List<ProgramEntry> ForExclusions(IEnumerable<string> processNames)
        {
            var running = RunningPaths();
            return processNames
                .Select(name => Describe(name, running.TryGetValue(name, out var path) ? path : ""))
                .OrderBy(entry => entry.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        /// <summary>One entry per running program the user can see a path for, sorted by name.</summary>
        public static List<ProgramEntry> RunningPrograms(ICollection<string> excluded)
        {
            return RunningPaths()
                .Where(pair => !IsSystemPath(pair.Value))
                .Select(pair => Mark(Describe(pair.Key, pair.Value), excluded))
                .OrderBy(entry => entry.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        /// <summary>Visible top-level windows with a title, by title.</summary>
        public static List<ProgramEntry> OpenWindows(ICollection<string> excluded)
        {
            var entries = new List<ProgramEntry>();
            uint ownProcessId = (uint)Process.GetCurrentProcess().Id;
            EnumWindows((window, parameter) =>
            {
                if (!IsUserWindow(window)) return true;
                var title = new StringBuilder(256);
                if (GetWindowText(window, title, title.Capacity) == 0) return true;
                GetWindowThreadProcessId(window, out uint processId);
                if (processId == ownProcessId) return true;
                string path = PathOf(processId);
                if (path.Length == 0) return true;

                var entry = Mark(Describe(Path.GetFileNameWithoutExtension(path), path), excluded);
                entry.Detail = entry.DisplayName;
                entry.DisplayName = title.ToString();
                entries.Add(entry);
                return true;
            }, IntPtr.Zero);
            return entries.OrderBy(entry => entry.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        // ============================================
        // Helpers
        // ============================================

        private static ProgramEntry Describe(string processName, string path)
        {
            var entry = new ProgramEntry
            {
                ProcessName = CloakSettings.NormalizeProcessName(processName),
                ExecutablePath = path ?? "",
                Detail = (CloakSettings.NormalizeProcessName(processName)) + ".exe",
            };
            entry.DisplayName = DescriptionOf(path) ?? Path.GetFileNameWithoutExtension(path ?? "");
            if (string.IsNullOrWhiteSpace(entry.DisplayName)) entry.DisplayName = processName;
            entry.Icon = IconOf(path);
            return entry;
        }

        private static ProgramEntry Mark(ProgramEntry entry, ICollection<string> excluded)
        {
            entry.IsAlreadyExcluded = excluded.Contains(entry.ProcessName);
            return entry;
        }

        private static Dictionary<string, string> RunningPaths()
        {
            var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    string path = PathOf((uint)process.Id);
                    if (path.Length == 0) continue;
                    string name = CloakSettings.NormalizeProcessName(Path.GetFileNameWithoutExtension(path));
                    if (!paths.ContainsKey(name)) paths[name] = path;
                }
            }
            return paths;
        }

        private static string PathOf(uint processId)
        {
            var process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
            if (process == IntPtr.Zero) return "";
            try
            {
                var path = new StringBuilder(1024);
                uint size = (uint)path.Capacity;
                return QueryFullProcessImageName(process, 0, path, ref size) ? path.ToString() : "";
            }
            finally
            {
                CloseHandle(process);
            }
        }

        // Windows' own background processes (svchost, dwm, ...) are noise in a "pick a game" list.
        private static bool IsSystemPath(string path)
        {
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            return path.StartsWith(windows + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && !path.StartsWith(Path.Combine(windows, "explorer.exe"), StringComparison.OrdinalIgnoreCase);
        }

        // Alt-Tab rules: visible, unowned, not a tool window, not cloaked (hidden UWP frames).
        private static bool IsUserWindow(IntPtr window)
        {
            if (!IsWindowVisible(window) || GetWindow(window, GW_OWNER) != IntPtr.Zero) return false;
            if ((GetWindowLong(window, GWL_EXSTYLE) & WS_EX_TOOLWINDOW) != 0) return false;
            return DwmGetWindowAttribute(window, DWMWA_CLOAKED, out int cloaked, sizeof(int)) != 0 || cloaked == 0;
        }

        private static string DescriptionOf(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                var description = FileVersionInfo.GetVersionInfo(path).FileDescription;
                return string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            }
            catch (FileNotFoundException) { return null; }
        }

        private static Bitmap IconOf(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(path)) return icon?.ToBitmap();
            }
            catch (ArgumentException) { return null; }
            catch (IOException) { return null; }
        }
    }
}
