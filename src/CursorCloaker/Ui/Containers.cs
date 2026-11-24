/*
 * Containers - small themed building blocks for windows.
 *
 * ListCard: a rounded Surface card on the window background that hosts a ProgramList, inset by
 * the list padding so rows never touch the rounded corners.
 * SearchBox: a bordered text field with a native placeholder (cue banner); the border turns
 * accent with a soft ring while focused.
 */
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CursorCloaker.Ui
{
    internal sealed class ListCard : Panel
    {
        public ListCard(Control content)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Background;
            content.Dock = DockStyle.Fill;
            Controls.Add(content);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int inset = (int)Math.Round(Theme.LIST_PADDING * DeviceDpi / 96f);
            Padding = new Padding(inset);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var ground = new SolidBrush(Theme.Background)) e.Graphics.FillRectangle(ground, ClientRectangle);
            float radius = Theme.LIST_RADIUS * DeviceDpi / 96f;
            using (var path = Theme.RoundedRectangle(new RectangleF(0, 0, Width - 1, Height - 1), radius))
            {
                using (var brush = new SolidBrush(Theme.Surface)) e.Graphics.FillPath(brush, path);
                using (var pen = new Pen(Theme.Hairline)) e.Graphics.DrawPath(pen, path);
            }
        }
    }

    internal sealed class SearchBox : Panel
    {
        private readonly TextBox input = new TextBox();
        private readonly string placeholder;

        public SearchBox(string placeholder)
        {
            this.placeholder = placeholder;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Background;

            input.BorderStyle = BorderStyle.None;
            input.BackColor = Theme.Surface;
            input.ForeColor = Theme.TextPrimary;
            input.Font = Theme.CreateFont();
            input.TextChanged += (sender, args) => { Invalidate(); TextChanged?.Invoke(this, EventArgs.Empty); };
            input.GotFocus += (sender, args) => Invalidate();
            input.LostFocus += (sender, args) => Invalidate();
            // Native placeholder (cue banner), kept visible while focused and empty.
            input.HandleCreated += (sender, args) => SendMessage(input.Handle, EM_SETCUEBANNER, new IntPtr(1), this.placeholder);
            Controls.Add(input);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, string lParam);
        private const int EM_SETCUEBANNER = 0x1501;

        public new event EventHandler TextChanged;

        public override string Text { get => input.Text; set => input.Text = value; }

        /// <summary>Lets the owner forward arrow keys to a list while focus stays in the field.</summary>
        public TextBox Input => input;

        public new void Focus() => input.Focus();

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int paddingX = (int)Math.Round(12 * DeviceDpi / 96f);
            input.SetBounds(paddingX, (Height - input.PreferredHeight) / 2, Width - paddingX * 2, input.PreferredHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var ground = new SolidBrush(Theme.Background)) graphics.FillRectangle(ground, ClientRectangle);

            float ring = Theme.FOCUS_RING_WIDTH * DeviceDpi / 96f, radius = Theme.CONTROL_RADIUS * DeviceDpi / 96f;
            var field = new RectangleF(ring, ring, Width - ring * 2 - 1, Height - ring * 2 - 1);
            bool isFocused = input.Focused;
            using (var path = Theme.RoundedRectangle(field, radius))
            {
                using (var brush = new SolidBrush(Theme.Surface)) graphics.FillPath(brush, path);
                using (var pen = new Pen(isFocused ? Theme.Accent : Theme.HairlineStrong)) graphics.DrawPath(pen, path);
            }
            if (isFocused)
            {
                using (var path = Theme.RoundedRectangle(new RectangleF(ring / 2, ring / 2, Width - ring - 1, Height - ring - 1), radius + ring / 2))
                using (var pen = new Pen(Theme.AccentSubtle, ring)) graphics.DrawPath(pen, path);
            }
        }
    }
}
