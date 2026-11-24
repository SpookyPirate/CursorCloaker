/*
 * ThemedButton - owner-drawn button in the dark theme, with every state drawn the same way.
 *
 * Primary: accent fill (one per window). Secondary: surface fill with a hairline.
 * States: hover and pressed shift the fill one step (accent ramp, or a white state layer on the
 * surface); keyboard focus draws a 2px accent ring outside the shape; disabled drops the label
 * to TextDisabled and the fill to the plain surface.
 */
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CursorCloaker.Ui
{
    internal enum ButtonVariant
    {
        Primary,
        Secondary,
    }

    internal sealed class ThemedButton : Button
    {
        private bool isHovered;
        private bool isPressed;

        public ThemedButton(string text, ButtonVariant variant)
        {
            Text = text;
            Variant = variant;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            Font = Theme.CreateSemiboldFont();
            Cursor = Cursors.Hand;
        }

        public ButtonVariant Variant { get; }

        /// <summary>Width that fits the label with the theme's horizontal padding.</summary>
        public int PreferredWidth(Func<int, int> scale)
        {
            int text = TextRenderer.MeasureText(Text, Font).Width;
            return text + scale(Theme.CONTROL_PADDING_X) * 2;
        }

        protected override void OnMouseEnter(EventArgs e) { isHovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { isHovered = false; isPressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { isPressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { isPressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var parentBrush = new SolidBrush(Parent?.BackColor ?? Theme.Background)) graphics.FillRectangle(parentBrush, ClientRectangle);

            float ring = Theme.FOCUS_RING_WIDTH * DeviceDpi / 96f;
            float radius = Theme.CONTROL_RADIUS * DeviceDpi / 96f;
            // The focus ring sits outside the fill, so the fill is inset by the ring's width.
            var fill = new RectangleF(ring, ring, Width - ring * 2 - 1, Height - ring * 2 - 1);

            using (var path = Theme.RoundedRectangle(fill, radius))
            {
                using (var brush = new SolidBrush(FillColor())) graphics.FillPath(brush, path);
                if (Variant == ButtonVariant.Secondary)
                {
                    using (var border = new Pen(Enabled && isHovered ? Theme.HairlineStrong : Theme.Hairline)) graphics.DrawPath(border, path);
                    if (Enabled && (isHovered || isPressed))
                        using (var layer = new SolidBrush(isPressed ? Theme.PressedLayer : Theme.HoverLayer)) graphics.FillPath(layer, path);
                }
            }

            if (Focused && ShowFocusCues && Enabled)
            {
                var ringBounds = new RectangleF(ring / 2, ring / 2, Width - ring - 1, Height - ring - 1);
                using (var path = Theme.RoundedRectangle(ringBounds, radius + ring / 2))
                using (var pen = new Pen(Theme.Accent, ring)) graphics.DrawPath(pen, path);
            }

            var textColor = !Enabled ? Theme.TextDisabled : Variant == ButtonVariant.Primary ? Theme.OnAccent : Theme.TextPrimary;
            TextRenderer.DrawText(graphics, Text, Font, Rectangle.Round(fill), textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        private Color FillColor()
        {
            if (Variant == ButtonVariant.Secondary || !Enabled) return Theme.Surface;
            return isPressed ? Theme.AccentPressed : isHovered ? Theme.AccentHover : Theme.Accent;
        }
    }
}
