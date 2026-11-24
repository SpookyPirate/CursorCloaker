/*
 * ExclusionsWindow - lists the programs CursorCloaker never cloaks over, and adds or removes them.
 *
 * Add offers three sources (browse for an exe, pick a running program, pick an open window), all
 * producing a process name. Every change is reported at once through SettingsChanged; TrayContext
 * saves and applies it, then calls Refresh, so this window never holds unsaved state.
 */
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

using CursorCloaker.Core;
using CursorCloaker.Win32;

namespace CursorCloaker.Ui
{
    internal sealed class ExclusionsWindow : ThemedForm
    {
        private readonly Label titleLabel = new Label { Text = "Excluded programs", AutoSize = true };
        private readonly Label subtitleLabel = new Label { Text = "The cursor is never cloaked over these.", AutoSize = true };
        private readonly ThemedButton addButton = new ThemedButton("Add program", ButtonVariant.Primary);
        private readonly ThemedButton removeButton = new ThemedButton("Remove", ButtonVariant.Secondary);
        private readonly ContextMenuStrip addMenu = new ContextMenuStrip();
        private readonly ProgramList list = new ProgramList { EmptyTitle = "No excluded programs", EmptyBody = "Add one to keep its cursor visible." };
        private readonly ListCard listCard;
        private CloakSettings settings;

        /// <summary>Raised with an edited copy of the settings after every add or remove.</summary>
        public event Action<CloakSettings> SettingsChanged;

        public ExclusionsWindow(CloakSettings settings)
        {
            this.settings = settings;
            listCard = new ListCard(list);

            titleLabel.Font = Theme.CreateSemiboldFont(Theme.TITLE_SIZE_POINTS);
            titleLabel.ForeColor = Theme.TextPrimary;
            subtitleLabel.ForeColor = Theme.TextSecondary;

            addMenu.Items.Add("Browse for program…", null, (sender, args) => BrowseForProgram());
            addMenu.Items.Add("Choose running program…", null, (sender, args) => PickFrom(ProgramSource.RunningPrograms));
            addMenu.Items.Add("Choose open window…", null, (sender, args) => PickFrom(ProgramSource.OpenWindows));
            ThemedMenu.Apply(addMenu);
            // Action menu: no item is ever checked, so no check column.
            addMenu.ShowCheckMargin = false;

            addButton.Click += (sender, args) => addMenu.Show(addButton, new Point(0, addButton.Height + Scale(4)));
            removeButton.Click += (sender, args) => RemoveSelected();
            list.SelectedIndexChanged += (sender, args) => removeButton.Enabled = list.SelectedEntry != null;
            list.KeyDown += (sender, args) => { if (args.KeyCode == Keys.Delete) { RemoveSelected(); args.Handled = true; } };

            Controls.AddRange(new Control[] { titleLabel, subtitleLabel, addButton, removeButton, listCard });
            Refresh(settings);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ClientSize = new Size(Scale(520), Scale(480));
            MinimumSize = SizeFromClientSize(new Size(Scale(400), Scale(320)));
            LayoutControls();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (IsHandleCreated) LayoutControls();
        }

        private void LayoutControls()
        {
            int pad = Scale(Theme.WINDOW_PADDING), height = Scale(Theme.CONTROL_HEIGHT);
            titleLabel.Location = new Point(pad, pad);
            subtitleLabel.Location = new Point(pad, titleLabel.Bottom + Scale(4));

            int toolbarTop = subtitleLabel.Bottom + Scale(Theme.SPACE_MEDIUM);
            addButton.SetBounds(pad, toolbarTop, addButton.PreferredWidth(Scale), height);
            removeButton.SetBounds(addButton.Right + Scale(Theme.SPACE_SMALL), toolbarTop, removeButton.PreferredWidth(Scale), height);

            int listTop = toolbarTop + height + Scale(Theme.SPACE_MEDIUM);
            listCard.SetBounds(pad, listTop, ClientSize.Width - pad * 2, ClientSize.Height - listTop - pad);
        }

        /// <summary>Show the given settings' exclusion list.</summary>
        public void Refresh(CloakSettings current)
        {
            settings = current;
            string selected = list.SelectedEntry?.ProcessName;
            list.BeginUpdate();
            list.Items.Clear();
            foreach (var entry in ProgramCatalog.ForExclusions(current.ExcludedProcesses)) list.Items.Add(entry);
            list.EndUpdate();
            list.SelectedIndex = selected == null ? -1 : IndexOf(selected);
            removeButton.Enabled = list.SelectedEntry != null;
            list.Invalidate();
        }

        private int IndexOf(string processName)
        {
            for (int i = 0; i < list.Items.Count; i++)
                if (((ProgramEntry)list.Items[i]).ProcessName == processName) return i;
            return -1;
        }

        // ============================================
        // Changes
        // ============================================

        private void BrowseForProgram()
        {
            using (var dialog = new OpenFileDialog { Title = "Browse for program", Filter = "Programs (*.exe)|*.exe", CheckFileExists = true })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK) Add(ProgramCatalog.FromExecutable(dialog.FileName).ProcessName);
            }
        }

        private void PickFrom(ProgramSource source)
        {
            using (var picker = new ProgramPickerWindow(source, settings.ExcludedProcesses))
            {
                if (picker.ShowDialog(this) == DialogResult.OK && picker.Chosen != null) Add(picker.Chosen.ProcessName);
            }
        }

        private void Add(string processName)
        {
            string name = CloakSettings.NormalizeProcessName(processName);
            if (name.Length > 0 && !settings.ExcludedProcesses.Contains(name))
            {
                var copy = settings.Clone();
                copy.ExcludedProcesses.Add(name);
                SettingsChanged?.Invoke(copy);
            }
            list.SelectedIndex = IndexOf(name);
            list.Focus();
        }

        private void RemoveSelected()
        {
            var entry = list.SelectedEntry;
            if (entry == null) return;
            int index = list.SelectedIndex;
            var copy = settings.Clone();
            copy.ExcludedProcesses.Remove(entry.ProcessName);
            SettingsChanged?.Invoke(copy);
            // Keep the keyboard flow going: select the row that took the removed one's place.
            if (list.Items.Count > 0) list.SelectedIndex = Math.Min(index, list.Items.Count - 1);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                addMenu.Dispose();
                foreach (var entry in list.Items.OfType<ProgramEntry>()) entry.Icon?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
