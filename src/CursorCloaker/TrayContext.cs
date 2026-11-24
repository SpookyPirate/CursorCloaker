/*
 * TrayContext - the notification-area icon and its menu; the app has no main window.
 *
 * Loads settings at startup. Cloaking itself is not wired in yet: see docs/DESIGN.md for the
 * planned input, foreground and cloaking components that will hang off this context.
 */
using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

using CursorCloaker.Core;
using CursorCloaker.Ui;

namespace CursorCloaker
{
    internal sealed class TrayContext : ApplicationContext
    {
        private readonly NotifyIcon trayIcon;
        private readonly SettingsStore settingsStore;
        private readonly CloakSettings settings;

        public TrayContext()
        {
            settingsStore = new SettingsStore(AppContext.BaseDirectory);
            settings = settingsStore.Load();

            trayIcon = new NotifyIcon
            {
                // TODO: Replace with a dedicated CursorCloaker icon resource.
                Icon = SystemIcons.Application,
                Text = "CursorCloaker " + VersionText(),
                ContextMenuStrip = TrayMenu.Create(VersionText(), ExitThread),
                Visible = true,
            };
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
            trayIcon.Visible = false;
            trayIcon.Dispose();
            base.ExitThreadCore();
        }
    }
}
