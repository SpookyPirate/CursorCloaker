/*
 * ProgramList - owner-drawn list of programs (icon, name, detail) in the dark theme.
 *
 * Used by the exclusions window and the program pickers. Rows: 32px icon, name in primary text,
 * detail (exe name or window title) in secondary text. Hover and selection are a state layer
 * and an accent tint inside a rounded row inset from the list edge. Entries already excluded
 * are drawn dimmed with an "Excluded" tag and can't be selected. An empty list draws its own
 * empty state (title and one line).
 */
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

using CursorCloaker.Win32;

namespace CursorCloaker.Ui
{
    internal sealed class ProgramList : ListBox
    {
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr window, string subApplicationName, string subIdList);

        private readonly Font nameFont = Theme.CreateFont();
        private readonly Font detailFont = Theme.CreateDetailFont();
        private readonly Font emptyTitleFont = Theme.CreateSemiboldFont();
        private int hoveredIndex = -1;

        public ProgramList()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            BorderStyle = BorderStyle.None;
            BackColor = Theme.Surface;
            ForeColor = Theme.TextPrimary;
            IntegralHeight = false;
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        }

        public string EmptyTitle { get; set; } = "";
        public string EmptyBody { get; set; } = "";

        public ProgramEntry SelectedEntry => SelectedItem as ProgramEntry;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ItemHeight = Math.Min(255, Scale(Theme.LIST_ROW_HEIGHT));
            // Dark scrollbar (Windows 10 1809+ / 11).
            SetWindowTheme(Handle, "DarkMode_Explorer", null);
        }

        private int Scale(int logicalPixels)
        {
            return (int)Math.Round(logicalPixels * DeviceDpi / 96f);
        }

        // ============================================
        // Selection rules
        // ============================================

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            // Already-excluded rows are information, not choices.
            if (SelectedEntry != null && SelectedEntry.IsAlreadyExcluded) { SelectedIndex = -1; return; }
            base.OnSelectedIndexChanged(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int index = IndexFromPoint(e.Location);
            if (index != hoveredIndex) { hoveredIndex = index; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hoveredIndex = -1;
            Invalidate();
            base.OnMouseLeave(e);
        }

        // ============================================
        // Painting
        // ============================================

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            var graphics = e.Graphics;
            using (var ground = new SolidBrush(Theme.Surface)) graphics.FillRectangle(ground, e.Bounds);
            if (e.Index < 0 || e.Index >= Items.Count || !(Items[e.Index] is ProgramEntry entry)) return;

            int pad = Scale(Theme.LIST_PADDING);
            var row = new Rectangle(e.Bounds.X + pad, e.Bounds.Y + pad / 2, e.Bounds.Width - pad * 2, e.Bounds.Height - pad);
            bool isSelected = (e.State & DrawItemState.Selected) != 0 && !entry.IsAlreadyExcluded;
            bool isHovered = e.Index == hoveredIndex && !entry.IsAlreadyExcluded;

            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (isSelected || isHovered)
            {
                using (var path = Theme.RoundedRectangle(row, Scale(Theme.LIST_ROW_RADIUS)))
                using (var brush = new SolidBrush(isSelected ? Theme.AccentSubtle : Theme.HoverLayer))
                    graphics.FillPath(brush, path);
            }
            if (isSelected && Focused && (e.State & DrawItemState.Focus) != 0)
            {
                using (var path = Theme.RoundedRectangle(new RectangleF(row.X + 0.5f, row.Y + 0.5f, row.Width - 1, row.Height - 1), Scale(Theme.LIST_ROW_RADIUS)))
                using (var pen = new Pen(Theme.Accent, 1)) graphics.DrawPath(pen, path);
            }

            DrawEntry(graphics, entry, row);
        }

        private void DrawEntry(Graphics graphics, ProgramEntry entry, Rectangle row)
        {
            int iconSize = Scale(Theme.LIST_ICON_SIZE), gap = Scale(Theme.SPACE_SMALL + 4), inset = Scale(Theme.SPACE_SMALL + 4);
            var iconBounds = new Rectangle(row.X + inset, row.Y + (row.Height - iconSize) / 2, iconSize, iconSize);
            var image = entry.Icon;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            if (image != null) DrawIcon(graphics, image, iconBounds, entry.IsAlreadyExcluded);
            else
            {
                using (var placeholder = AppIcon.Create(new Size(iconSize, iconSize)))
                using (var bitmap = placeholder.ToBitmap()) DrawIcon(graphics, bitmap, iconBounds, true);
            }

            int textLeft = iconBounds.Right + gap;
            string tag = entry.IsAlreadyExcluded ? "Excluded" : null;
            int tagWidth = tag == null ? 0 : TextRenderer.MeasureText(tag, detailFont).Width + Scale(Theme.SPACE_MEDIUM);
            var textBounds = new Rectangle(textLeft, row.Y, row.Right - textLeft - inset - tagWidth, row.Height);

            int nameHeight = nameFont.Height, detailHeight = detailFont.Height, lineGap = Scale(2);
            int top = row.Y + (row.Height - nameHeight - detailHeight - lineGap) / 2;
            var flags = TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(graphics, entry.DisplayName, nameFont, new Rectangle(textBounds.X, top, textBounds.Width, nameHeight),
                entry.IsAlreadyExcluded ? Theme.TextDisabled : Theme.TextPrimary, flags);
            TextRenderer.DrawText(graphics, entry.Detail, detailFont, new Rectangle(textBounds.X, top + nameHeight + lineGap, textBounds.Width, detailHeight),
                entry.IsAlreadyExcluded ? Theme.TextDisabled : Theme.TextSecondary, flags);

            if (tag != null)
                TextRenderer.DrawText(graphics, tag, detailFont, new Rectangle(row.Right - inset - tagWidth, row.Y, tagWidth, row.Height),
                    Theme.TextSecondary, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        private static void DrawIcon(Graphics graphics, Image image, Rectangle bounds, bool isDimmed)
        {
            if (!isDimmed) { graphics.DrawImage(image, bounds); return; }
            using (var attributes = new System.Drawing.Imaging.ImageAttributes())
            {
                attributes.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix { Matrix33 = 0.45f });
                graphics.DrawImage(image, bounds, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
            }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            // WM_PAINT: an empty list draws its empty state over the blank ground.
            if (m.Msg == 0x000F && Items.Count == 0) DrawEmptyState();
        }

        private void DrawEmptyState()
        {
            using (var graphics = CreateGraphics())
            {
                using (var ground = new SolidBrush(Theme.Surface)) graphics.FillRectangle(ground, ClientRectangle);
                int titleHeight = emptyTitleFont.Height, bodyHeight = nameFont.Height, gap = Scale(4);
                int top = (ClientSize.Height - titleHeight - gap - bodyHeight) / 2;
                var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
                TextRenderer.DrawText(graphics, EmptyTitle, emptyTitleFont, new Rectangle(0, top, ClientSize.Width, titleHeight), Theme.TextPrimary, flags);
                TextRenderer.DrawText(graphics, EmptyBody, nameFont, new Rectangle(0, top + titleHeight + gap, ClientSize.Width, bodyHeight), Theme.TextSecondary, flags);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                nameFont.Dispose();
                detailFont.Dispose();
                emptyTitleFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
