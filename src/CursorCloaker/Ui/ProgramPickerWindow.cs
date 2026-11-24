/*
 * ProgramPickerWindow - choose one program from the running programs or the open windows.
 *
 * The list loads off the UI thread (enumerating processes and extracting icons can take a
 * moment) and is filtered as you type. Arrow keys move through the list while focus stays in
 * the search field; Enter or double-click adds. Programs already excluded are shown, dimmed,
 * so the list matches what the user sees on screen, but can't be chosen.
 */
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

using CursorCloaker.Win32;

namespace CursorCloaker.Ui
{
    internal enum ProgramSource
    {
        RunningPrograms,
        OpenWindows,
    }

    internal sealed class ProgramPickerWindow : ThemedForm
    {
        private readonly Label titleLabel = new Label { AutoSize = true };
        private readonly SearchBox search = new SearchBox("Search");
        private readonly ProgramList list = new ProgramList { EmptyTitle = "Loading", EmptyBody = "" };
        private readonly ListCard listCard;
        private readonly ThemedButton cancelButton = new ThemedButton("Cancel", ButtonVariant.Secondary);
        private readonly ThemedButton addButton = new ThemedButton("Add", ButtonVariant.Primary);
        private List<ProgramEntry> allEntries = new List<ProgramEntry>();

        public ProgramPickerWindow(ProgramSource source, ICollection<string> excluded)
        {
            titleLabel.Text = source == ProgramSource.RunningPrograms ? "Choose running program" : "Choose open window";
            titleLabel.Font = Theme.CreateSemiboldFont(Theme.TITLE_SIZE_POINTS);
            titleLabel.ForeColor = Theme.TextPrimary;
            listCard = new ListCard(list);
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;

            addButton.Enabled = false;
            addButton.Click += (sender, args) => Choose();
            cancelButton.Click += (sender, args) => Close();
            AcceptButton = addButton;
            list.SelectedIndexChanged += (sender, args) => addButton.Enabled = list.SelectedEntry != null;
            list.DoubleClick += (sender, args) => Choose();
            search.TextChanged += (sender, args) => ApplyFilter();
            search.Input.KeyDown += OnSearchKeyDown;

            Controls.AddRange(new Control[] { titleLabel, search, listCard, cancelButton, addButton });
            LoadEntries(source, excluded.ToList());
        }

        /// <summary>The program the user chose, once the window closes with OK.</summary>
        public ProgramEntry Chosen { get; private set; }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ClientSize = new Size(Scale(520), Scale(560));
            MinimumSize = SizeFromClientSize(new Size(Scale(400), Scale(360)));
            LayoutControls();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            search.Focus();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (IsHandleCreated) LayoutControls();
        }

        private void LayoutControls()
        {
            int pad = Scale(Theme.WINDOW_PADDING), height = Scale(Theme.CONTROL_HEIGHT), width = ClientSize.Width - pad * 2;
            titleLabel.Location = new Point(pad, pad);
            search.SetBounds(pad, titleLabel.Bottom + Scale(Theme.SPACE_MEDIUM), width, height);

            int footerTop = ClientSize.Height - pad - height;
            addButton.SetBounds(ClientSize.Width - pad - Math.Max(addButton.PreferredWidth(Scale), Scale(80)), footerTop, Math.Max(addButton.PreferredWidth(Scale), Scale(80)), height);
            cancelButton.SetBounds(addButton.Left - Scale(Theme.SPACE_SMALL) - Math.Max(cancelButton.PreferredWidth(Scale), Scale(80)), footerTop, Math.Max(cancelButton.PreferredWidth(Scale), Scale(80)), height);

            int listTop = search.Bottom + Scale(Theme.SPACE_SMALL + 4);
            listCard.SetBounds(pad, listTop, width, footerTop - Scale(Theme.SPACE_MEDIUM + 4) - listTop);
        }

        private async void LoadEntries(ProgramSource source, List<string> excluded)
        {
            var entries = await Task.Run(() => source == ProgramSource.RunningPrograms
                ? ProgramCatalog.RunningPrograms(excluded)
                : ProgramCatalog.OpenWindows(excluded));
            if (IsDisposed) { foreach (var entry in entries) entry.Icon?.Dispose(); return; }

            allEntries = entries;
            list.EmptyTitle = "No matches";
            list.EmptyBody = "Try another search.";
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string query = search.Text.Trim();
            var visible = query.Length == 0 ? allEntries : allEntries.Where(entry =>
                Contains(entry.DisplayName, query) || Contains(entry.Detail, query) || Contains(entry.ProcessName, query)).ToList();

            list.BeginUpdate();
            list.Items.Clear();
            foreach (var entry in visible) list.Items.Add(entry);
            list.EndUpdate();
            addButton.Enabled = false;
            list.Invalidate();
        }

        private static bool Contains(string text, string query)
        {
            return text != null && text.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private void OnSearchKeyDown(object sender, KeyEventArgs args)
        {
            if (args.KeyCode != Keys.Down && args.KeyCode != Keys.Up) return;
            int step = args.KeyCode == Keys.Down ? 1 : -1;
            // Skip rows that can't be chosen.
            for (int index = list.SelectedIndex + step; index >= 0 && index < list.Items.Count; index += step)
            {
                if (((ProgramEntry)list.Items[index]).IsAlreadyExcluded) continue;
                list.SelectedIndex = index;
                break;
            }
            args.Handled = true;
        }

        private void Choose()
        {
            if (list.SelectedEntry == null) return;
            Chosen = list.SelectedEntry;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) foreach (var entry in allEntries) entry.Icon?.Dispose();
            base.Dispose(disposing);
        }
    }
}
