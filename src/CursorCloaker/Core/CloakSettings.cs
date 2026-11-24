/*
 * CloakSettings - user settings and their .ini file format.
 *
 * Pure logic: no Win32 or file-system calls, so parsing is unit-testable. The file lives next to
 * the exe (portable app); SettingsStore handles reading and writing it.
 *
 * Unknown keys and unparseable values fall back to defaults instead of failing, so a hand-edited
 * file with a typo still starts the app with sensible behaviour.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CursorCloaker.Core
{
    /// <summary>What happens to the cursor when it is cloaked.</summary>
    internal enum CloakMode
    {
        /// <summary>Swap the system cursors for a blank one.</summary>
        Hide,
        /// <summary>Move the cursor to the bottom-right corner of its monitor.</summary>
        Park,
        /// <summary>Both: hidden, and parked in case a program draws its own cursor.</summary>
        HideAndPark,
    }

    /// <summary>Which foreground windows controller input may cloak the cursor over.</summary>
    internal enum CloakScope
    {
        /// <summary>Only fullscreen or borderless-fullscreen windows (games, Big Picture).</summary>
        FullscreenOnly,
        /// <summary>Any program.</summary>
        AllPrograms,
    }

    internal sealed class CloakSettings
    {
        public bool Enabled { get; set; } = true;
        public CloakMode Mode { get; set; } = CloakMode.HideAndPark;
        public CloakScope Scope { get; set; } = CloakScope.FullscreenOnly;

        /// <summary>Delay between controller input and cloaking. 0 = immediately.</summary>
        public int HideDelayMilliseconds { get; set; } = 0;

        public bool StartWithWindows { get; set; } = true;

        /// <summary>Process names (without .exe, case-insensitive) that are never cloaked over.</summary>
        public List<string> ExcludedProcesses { get; set; } = new List<string>();

        // Upper bound keeps a typo like "50000" from making the feature look broken.
        public const int MAX_HIDE_DELAY_MILLISECONDS = 10000;

        // ============================================
        // Parsing
        // ============================================

        public static CloakSettings Parse(string text)
        {
            var settings = new CloakSettings();
            if (string.IsNullOrEmpty(text)) return settings;

            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine.Trim();
                // Comments (; or #) and section headers carry no settings.
                if (line.Length == 0 || line[0] == ';' || line[0] == '#' || line[0] == '[') continue;

                int equals = line.IndexOf('=');
                if (equals <= 0) continue;
                ApplyValue(settings, line.Substring(0, equals).Trim(), line.Substring(equals + 1).Trim());
            }
            return settings;
        }

        private static void ApplyValue(CloakSettings settings, string key, string value)
        {
            switch (key.ToLowerInvariant())
            {
                case "enabled":
                    if (TryParseBool(value, out bool enabled)) settings.Enabled = enabled;
                    break;
                case "mode":
                    if (Enum.TryParse(value, true, out CloakMode mode) && Enum.IsDefined(typeof(CloakMode), mode)) settings.Mode = mode;
                    break;
                case "scope":
                    if (Enum.TryParse(value, true, out CloakScope scope) && Enum.IsDefined(typeof(CloakScope), scope)) settings.Scope = scope;
                    break;
                case "hidedelayms":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int delay))
                        settings.HideDelayMilliseconds = Math.Max(0, Math.Min(MAX_HIDE_DELAY_MILLISECONDS, delay));
                    break;
                case "startwithwindows":
                    if (TryParseBool(value, out bool start)) settings.StartWithWindows = start;
                    break;
                case "exclude":
                    settings.ExcludedProcesses = ParseProcessList(value);
                    break;
            }
        }

        private static bool TryParseBool(string value, out bool result)
        {
            switch (value.ToLowerInvariant())
            {
                case "true": case "yes": case "on": case "1": result = true; return true;
                case "false": case "no": case "off": case "0": result = false; return true;
                default: result = false; return false;
            }
        }

        // "steam.exe; Notepad ;;" -> ["steam", "notepad"]
        private static List<string> ParseProcessList(string value)
        {
            return value.Split(';')
                .Select(NormalizeProcessName)
                .Where(name => name.Length > 0)
                .Distinct()
                .ToList();
        }

        public static string NormalizeProcessName(string name)
        {
            var trimmed = (name ?? "").Trim().ToLowerInvariant();
            return trimmed.EndsWith(".exe") ? trimmed.Substring(0, trimmed.Length - 4) : trimmed;
        }

        // ============================================
        // Serialising
        // ============================================

        public string Serialize()
        {
            var text = new StringBuilder();
            text.AppendLine("; CursorCloaker settings. Edit while the app is closed, or use the tray menu.");
            text.AppendLine("[CursorCloaker]");
            text.AppendLine("; true | false");
            text.AppendLine("Enabled=" + (Enabled ? "true" : "false"));
            text.AppendLine("; Hide | Park | HideAndPark");
            text.AppendLine("Mode=" + Mode);
            text.AppendLine("; FullscreenOnly | AllPrograms");
            text.AppendLine("Scope=" + Scope);
            text.AppendLine("; Milliseconds between controller input and cloaking (0-" + MAX_HIDE_DELAY_MILLISECONDS + ")");
            text.AppendLine("HideDelayMs=" + HideDelayMilliseconds.ToString(CultureInfo.InvariantCulture));
            text.AppendLine("StartWithWindows=" + (StartWithWindows ? "true" : "false"));
            text.AppendLine("; Programs never cloaked over, separated by ; (e.g. obs64;notepad)");
            text.AppendLine("Exclude=" + string.Join(";", ExcludedProcesses));
            return text.ToString();
        }
    }
}
