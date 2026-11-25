/*
 * TrayMenu - the tray icon's context menu.
 *
 * Shows the current settings and reports the user's changes through SettingsChanged. It never
 * saves or applies anything itself; TrayContext owns that. Kept free of the tray icon so
 * scripts/preview-menu.ps1 can build and screenshot the real menu.
 */
using System;
using System.Linq;
using System.Windows.Forms;

using CursorCloaker.Core;

namespace CursorCloaker.Ui
{
    internal sealed class TrayMenu
    {
        private readonly ToolStripMenuItem enabledItem = new ToolStripMenuItem("Enabled");
        private readonly ToolStripMenuItem modeItem = new ToolStripMenuItem("Cloak style");
        private readonly ToolStripMenuItem parkItem = new ToolStripMenuItem("Move to");
        private readonly ToolStripMenuItem scopeItem = new ToolStripMenuItem("Cloak over");
        private readonly ToolStripMenuItem startupItem = new ToolStripMenuItem("Start with Windows");
        private CloakSettings settings;

        /// <summary>Raised with an edited copy of the settings after any menu change.</summary>
        public event Action<CloakSettings> SettingsChanged;
        public event Action ExclusionsRequested;
        public event Action ExitRequested;

        public TrayMenu(string versionText, CloakSettings settings)
        {
            this.settings = settings;
            Menu = new ContextMenuStrip();
            Menu.Items.Add(new ToolStripLabel("CursorCloaker " + versionText));
            Menu.Items.Add(new ToolStripSeparator());

            enabledItem.Click += (sender, args) => Change(copy => copy.Enabled = !copy.Enabled);
            Menu.Items.Add(enabledItem);

            AddChoice(modeItem, "Hide", CloakMode.Hide, (copy, value) => copy.Mode = value);
            AddChoice(modeItem, "Move away", CloakMode.Park, (copy, value) => copy.Mode = value);
            AddChoice(modeItem, "Hide and move away", CloakMode.HideAndPark, (copy, value) => copy.Mode = value);
            Menu.Items.Add(modeItem);

            // Corners first (the usual choice), then the middle of each edge.
            AddChoice(parkItem, "Bottom-right corner", ParkPosition.BottomRight, (copy, value) => copy.ParkPosition = value);
            AddChoice(parkItem, "Bottom-left corner", ParkPosition.BottomLeft, (copy, value) => copy.ParkPosition = value);
            AddChoice(parkItem, "Top-right corner", ParkPosition.TopRight, (copy, value) => copy.ParkPosition = value);
            AddChoice(parkItem, "Top-left corner", ParkPosition.TopLeft, (copy, value) => copy.ParkPosition = value);
            parkItem.DropDownItems.Add(new ToolStripSeparator());
            AddChoice(parkItem, "Bottom edge", ParkPosition.BottomEdge, (copy, value) => copy.ParkPosition = value);
            AddChoice(parkItem, "Top edge", ParkPosition.TopEdge, (copy, value) => copy.ParkPosition = value);
            AddChoice(parkItem, "Left edge", ParkPosition.LeftEdge, (copy, value) => copy.ParkPosition = value);
            AddChoice(parkItem, "Right edge", ParkPosition.RightEdge, (copy, value) => copy.ParkPosition = value);
            Menu.Items.Add(parkItem);

            AddChoice(scopeItem, "Fullscreen programs", CloakScope.FullscreenOnly, (copy, value) => copy.Scope = value);
            AddChoice(scopeItem, "All programs", CloakScope.AllPrograms, (copy, value) => copy.Scope = value);
            Menu.Items.Add(scopeItem);

            Menu.Items.Add("Excluded programs…", null, (sender, args) => ExclusionsRequested?.Invoke());
            Menu.Items.Add(new ToolStripSeparator());

            startupItem.Click += (sender, args) => Change(copy => copy.StartWithWindows = !copy.StartWithWindows);
            Menu.Items.Add(startupItem);
            Menu.Items.Add(new ToolStripSeparator());
            Menu.Items.Add("Exit", null, (sender, args) => ExitRequested?.Invoke());

            ThemedMenu.Apply(Menu);
            Refresh(settings);
        }

        public ContextMenuStrip Menu { get; }

        /// <summary>Show the given settings as the current state (checks and radio marks).</summary>
        public void Refresh(CloakSettings current)
        {
            settings = current;
            enabledItem.Checked = current.Enabled;
            startupItem.Checked = current.StartWithWindows;
            foreach (ToolStripMenuItem choice in modeItem.DropDownItems) choice.Checked = Equals(choice.Tag, current.Mode);
            foreach (var choice in parkItem.DropDownItems.OfType<ToolStripMenuItem>()) choice.Checked = Equals(choice.Tag, current.ParkPosition);
            // Plain Hide never moves the cursor, so where it would move to doesn't apply.
            parkItem.Enabled = current.Mode != CloakMode.Hide;
            foreach (ToolStripMenuItem choice in scopeItem.DropDownItems) choice.Checked = Equals(choice.Tag, current.Scope);
        }

        private void AddChoice<T>(ToolStripMenuItem parent, string text, T value, Action<CloakSettings, T> write)
        {
            var choice = new ToolStripMenuItem(text) { Tag = value };
            choice.Click += (sender, args) => Change(copy => write(copy, value));
            parent.DropDownItems.Add(choice);
        }

        private void Change(Action<CloakSettings> edit)
        {
            // Edit a copy so listeners can compare old and new settings.
            var copy = settings.Clone();
            edit(copy);
            SettingsChanged?.Invoke(copy);
        }
    }
}
