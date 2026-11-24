/*
 * TrayContext - owns the running app: settings, the cloak engine, the tray icon and its menu.
 *
 * Settings flow one way: a menu or window produces an edited copy, ApplySettings saves it,
 * hands it to the engine, syncs "Start with Windows", and refreshes the menu and icon.
 */
using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

using Microsoft.Win32;

using CursorCloaker.Core;
using CursorCloaker.Ui;
using CursorCloaker.Win32;

namespace CursorCloaker
{
    internal sealed class TrayContext : ApplicationContext
    {
        private readonly NotifyIcon trayIcon;
        private readonly TrayMenu trayMenu;
        private readonly SettingsStore settingsStore;
        private readonly CloakEngine engine;
        private CloakSettings settings;
        private ExclusionsWindow exclusionsWindow;

        public TrayContext()
        {
            settingsStore = new SettingsStore(AppContext.BaseDirectory);
            settings = settingsStore.Load();
            // First run: write the defaults so the file exists to be found and edited.
            if (!System.IO.File.Exists(settingsStore.FilePath)) TrySave(settings);
            StartupRegistration.Apply(settings.StartWithWindows);

            engine = new CloakEngine(settings);

            trayMenu = new TrayMenu(VersionText(), settings);
            trayMenu.SettingsChanged += ApplySettings;
            trayMenu.ExclusionsRequested += ShowExclusions;
            trayMenu.ExitRequested += ExitThread;

            trayIcon = new NotifyIcon { ContextMenuStrip = trayMenu.Menu, Visible = true };
            trayIcon.MouseUp += OnTrayMouseUp;
            UpdateTrayIcon();

            // Never leave the cursor hidden behind a lock screen or across a sign-out.
            SystemEvents.SessionSwitch += OnSessionSwitch;
        }

        // ============================================
        // Settings
        // ============================================

        private void ApplySettings(CloakSettings newSettings)
        {
            settings = newSettings;
            if (!TrySave(newSettings))
                trayIcon.ShowBalloonTip(5000, "Settings not saved", "Move CursorCloaker to a folder you can write to, such as Documents.", ToolTipIcon.Warning);
            engine.UpdateSettings(newSettings);
            StartupRegistration.Apply(newSettings.StartWithWindows);
            trayMenu.Refresh(newSettings);
            exclusionsWindow?.Refresh(newSettings);
            UpdateTrayIcon();
        }

        /// <summary>Save next to the exe; false when the folder is read-only (e.g. Program Files).</summary>
        private bool TrySave(CloakSettings toSave)
        {
            try { settingsStore.Save(toSave); return true; }
            catch (System.IO.IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        private void ShowExclusions()
        {
            if (exclusionsWindow == null || exclusionsWindow.IsDisposed)
            {
                exclusionsWindow = new ExclusionsWindow(settings);
                exclusionsWindow.SettingsChanged += ApplySettings;
            }
            exclusionsWindow.Show();
            exclusionsWindow.Activate();
        }

        // ============================================
        // Tray icon
        // ============================================

        private void UpdateTrayIcon()
        {
            var previous = trayIcon.Icon;
            trayIcon.Icon = AppIcon.CreateTrayIcon(isPaused: !settings.Enabled);
            previous?.Dispose();
            trayIcon.Text = "CursorCloaker " + (settings.Enabled ? "is on" : "is paused");
        }

        private void OnTrayMouseUp(object sender, MouseEventArgs args)
        {
            // No main window, so a left click opens the same menu as a right click.
            if (args.Button != MouseButtons.Left) return;
            typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(trayIcon, null);
        }

        private void OnSessionSwitch(object sender, SessionSwitchEventArgs args)
        {
            if (args.Reason == SessionSwitchReason.SessionLock || args.Reason == SessionSwitchReason.SessionLogoff
                || args.Reason == SessionSwitchReason.ConsoleDisconnect || args.Reason == SessionSwitchReason.RemoteDisconnect)
            {
                engine.Uncloak();
            }
        }

        // "v0.1.0" from Directory.Build.props, via the informational version the SDK stamps.
        internal static string VersionText()
        {
            var version = typeof(TrayContext).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
            int metadata = version.IndexOf('+');
            return "v" + (metadata >= 0 ? version.Substring(0, metadata) : version);
        }

        protected override void ExitThreadCore()
        {
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            engine.Dispose();
            exclusionsWindow?.Close();
            trayIcon.Visible = false;
            trayIcon.Icon?.Dispose();
            trayIcon.Dispose();
            base.ExitThreadCore();
        }
    }
}
