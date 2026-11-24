/*
 * ThemedMenu - applies the dark Theme to a WinForms context menu and its submenus.
 *
 * WinForms draws menus in the light system style; this replaces every painted part with a
 * custom renderer driven by Theme tokens. On Windows 11 the menu window also gets DWM rounded
 * corners and a hairline border colour; on Windows 10, where DWM ignores those attributes, the
 * renderer draws the hairline itself.
 */
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CursorCloaker.Ui
{
    internal static class ThemedMenu
    {
        // Menus already themed. A submenu reports its parent's renderer, so the renderer can't
        // tell whether a submenu has had its own font, padding and window frame applied.
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ToolStripDropDownMenu, object> themed =
            new System.Runtime.CompilerServices.ConditionalWeakTable<ToolStripDropDownMenu, object>();

        /// <summary>Style a menu and every submenu beneath it (items added later are styled on open).</summary>
        public static void Apply(ToolStripDropDownMenu menu)
        {
            if (themed.TryGetValue(menu, out _)) return;
            themed.Add(menu, null);

            menu.Renderer = new DarkMenuRenderer();
            menu.Font = Theme.CreateFont();
            menu.BackColor = Theme.Surface;
            menu.ForeColor = Theme.TextPrimary;
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = true;
            menu.Padding = new Padding(Theme.MENU_PADDING);
            menu.DropShadowEnabled = true;

            menu.HandleCreated += (sender, args) => ApplyWindowFrame(menu);
            if (menu.IsHandleCreated) ApplyWindowFrame(menu);

            // Items can be added after Apply; style each one as the menu opens.
            menu.Opening += (sender, args) => StyleItems(menu);
            StyleItems(menu);
        }

        private static void StyleItems(ToolStripDropDownMenu menu)
        {
            foreach (ToolStripItem item in menu.Items)
            {
                // Vertical padding sets the row height (MENU_ITEM_HEIGHT at the 10pt font).
                if (!(item is ToolStripSeparator)) item.Padding = new Padding(0, 6, 0, 6);
                item.ForeColor = Theme.TextPrimary;

                if (item is ToolStripMenuItem menuItem && menuItem.HasDropDownItems && menuItem.DropDown is ToolStripDropDownMenu submenu)
                {
                    Apply(submenu);
                    StyleItems(submenu);
                }
            }
        }

        // ============================================
        // DWM frame (Windows 11)
        // ============================================

        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWCP_ROUND = 2;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        private static void ApplyWindowFrame(ToolStripDropDownMenu menu)
        {
            int corner = DWMWCP_ROUND;
            bool isRounded = DwmSetWindowAttribute(menu.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int)) == 0;

            var border = Theme.HairlineOnSurface;
            int colorRef = border.R | (border.G << 8) | (border.B << 16);
            DwmSetWindowAttribute(menu.Handle, DWMWA_BORDER_COLOR, ref colorRef, sizeof(int));

            if (menu.Renderer is DarkMenuRenderer renderer) renderer.DrawsOwnBorder = !isRounded;
        }

        // ============================================
        // Renderer
        // ============================================

        private sealed class DarkMenuRenderer : ToolStripRenderer
        {
            /// <summary>True where DWM cannot draw the frame (Windows 10).</summary>
            public bool DrawsOwnBorder { get; set; } = true;

            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                using (var brush = new SolidBrush(Theme.Surface)) e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                if (!DrawsOwnBorder) return;
                var bounds = new Rectangle(Point.Empty, e.ToolStrip.Size);
                using (var pen = new Pen(Theme.HairlineOnSurface))
                    e.Graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
            }

            protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
            {
                // The check column shares the menu surface; no separate gutter.
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                if (!e.Item.Enabled || !(e.Item.Selected || e.Item.Pressed)) return;

                var layer = e.Item.Pressed ? Theme.PressedLayer : Theme.HoverLayer;
                var bounds = new Rectangle(Point.Empty, e.Item.Size);
                FillRounded(e.Graphics, layer, bounds, Theme.MENU_ITEM_RADIUS);
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = !e.Item.Enabled ? Theme.TextDisabled
                    : e.Item is ToolStripLabel ? Theme.TextSecondary
                    : Theme.TextPrimary;
                base.OnRenderItemText(e);
            }

            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                int y = e.Item.Height / 2;
                using (var pen = new Pen(Theme.Hairline))
                    e.Graphics.DrawLine(pen, Theme.MENU_PADDING, y, e.Item.Width - Theme.MENU_PADDING, y);
            }

            protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
            {
                // Accent checkmark, no box: the mark alone carries the state, the label names it.
                var box = e.ImageRectangle;
                var graphics = e.Graphics;
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(e.Item.Enabled ? Theme.Accent : Theme.TextDisabled, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    float left = box.Left + box.Width * 0.18f, mid = box.Left + box.Width * 0.42f, right = box.Left + box.Width * 0.84f;
                    float top = box.Top + box.Height * 0.24f, middle = box.Top + box.Height * 0.54f, bottom = box.Top + box.Height * 0.78f;
                    graphics.DrawLines(pen, new[] { new PointF(left, middle), new PointF(mid, bottom), new PointF(right, top) });
                }
                graphics.SmoothingMode = SmoothingMode.None;
            }

            protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
            {
                e.ArrowColor = e.Item.Enabled ? Theme.TextSecondary : Theme.TextDisabled;
                base.OnRenderArrow(e);
            }

            private static void FillRounded(Graphics graphics, Color color, Rectangle bounds, int radius)
            {
                int diameter = radius * 2;
                using (var path = new GraphicsPath())
                using (var brush = new SolidBrush(color))
                {
                    path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
                    path.AddArc(bounds.Right - diameter - 1, bounds.Top, diameter, diameter, 270, 90);
                    path.AddArc(bounds.Right - diameter - 1, bounds.Bottom - diameter - 1, diameter, diameter, 0, 90);
                    path.AddArc(bounds.Left, bounds.Bottom - diameter - 1, diameter, diameter, 90, 90);
                    path.CloseFigure();
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.FillPath(brush, path);
                    graphics.SmoothingMode = SmoothingMode.None;
                }
            }
        }
    }
}
