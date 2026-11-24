/*
 * StartupRegistration - "Start with Windows" through the current user's Run key.
 *
 * HKCU\...\Run needs no elevation and no installer. The value always points at the exe that is
 * running now, so moving the portable folder and launching it once fixes the entry.
 */
using System;
using System.Reflection;

using Microsoft.Win32;

namespace CursorCloaker.Win32
{
    internal static class StartupRegistration
    {
        private const string RUN_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string VALUE_NAME = "CursorCloaker";

        public static string ExecutablePath => Assembly.GetExecutingAssembly().Location;

        /// <summary>Add or remove the Run entry to match the setting. Returns false if the registry refused.</summary>
        public static bool Apply(bool startWithWindows)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RUN_KEY))
                {
                    if (startWithWindows)
                    {
                        string command = "\"" + ExecutablePath + "\"";
                        if (!string.Equals(key.GetValue(VALUE_NAME) as string, command, StringComparison.OrdinalIgnoreCase))
                            key.SetValue(VALUE_NAME, command);
                    }
                    else if (key.GetValue(VALUE_NAME) != null)
                    {
                        key.DeleteValue(VALUE_NAME, false);
                    }
                }
                return true;
            }
            catch (UnauthorizedAccessException) { return false; }
            catch (System.Security.SecurityException) { return false; }
        }
    }
}
