/*
 * TrayMenu - builds the tray icon's context menu.
 *
 * Kept separate from TrayContext so scripts/preview-menu.ps1 can build and screenshot the real
 * menu without a tray icon.
 */
using System;
using System.Windows.Forms;

namespace CursorCloaker.Ui
{
    internal static class TrayMenu
    {
        public static ContextMenuStrip Create(string versionText, Action onExit)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add(new ToolStripLabel("CursorCloaker " + versionText));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (sender, args) => onExit());
            ThemedMenu.Apply(menu);
            return menu;
        }
    }
}
