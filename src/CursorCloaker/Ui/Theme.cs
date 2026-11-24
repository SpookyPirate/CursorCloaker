/*
 * Theme - the design tokens for every CursorCloaker UI surface (dark, blue accent).
 *
 * All UI colours, type and spacing come from here; no other file defines a colour.
 * Follows the ui-design skill foundations:
 *   - Neutrals are one ramp tinted toward the accent hue (~222°), never pure black or grey.
 *   - Elevation is lighter, not darker: a menu sits one step above the page.
 *   - Interaction states are a white state layer over the surface (hover 8%), not new hues.
 *   - Hairlines are white at low alpha, composited onto the surface they sit on.
 *   - The accent is desaturated for dark grounds and spent only on what is active or selected.
 *
 * Contrast, computed against Surface (#222735): TextPrimary 12.2:1, TextSecondary 5.9:1,
 * Accent 5.2:1, all above WCAG 2.2 AA for text (4.5:1). TextDisabled is 2.6:1 on purpose:
 * WCAG exempts disabled controls, and it must read as clearly weaker than enabled text.
 */
using System;
using System.Drawing;

namespace CursorCloaker.Ui
{
    internal static class Theme
    {
        // ============================================
        // Neutral ramp (blue-tinted)
        // ============================================

        /// <summary>Page / window background.</summary>
        public static readonly Color Background = Color.FromArgb(0x15, 0x18, 0x21);
        /// <summary>Raised surface: menus, popovers, cards.</summary>
        public static readonly Color Surface = Color.FromArgb(0x22, 0x27, 0x35);

        // ============================================
        // Text
        // ============================================

        public static readonly Color TextPrimary = Color.FromArgb(0xE6, 0xE9, 0xEF);
        public static readonly Color TextSecondary = Color.FromArgb(0x9A, 0xA3, 0xB5);
        /// <summary>Disabled content: ~38% emphasis, deliberately below text contrast.</summary>
        public static readonly Color TextDisabled = Color.FromArgb(0x5E, 0x66, 0x78);

        // ============================================
        // Accent (blue)
        // ============================================

        public static readonly Color Accent = Color.FromArgb(0x5B, 0x9A, 0xF5);
        /// <summary>Accent one ramp step lighter (hover) and darker (pressed), for filled buttons.</summary>
        public static readonly Color AccentHover = Color.FromArgb(0x74, 0xAA, 0xF7);
        public static readonly Color AccentPressed = Color.FromArgb(0x4A, 0x88, 0xE6);
        /// <summary>Text on an accent fill. White on this blue is 2.8:1; near-black is 6.6:1 (5.3:1 pressed).</summary>
        public static readonly Color OnAccent = Color.FromArgb(0x0B, 0x12, 0x20);
        /// <summary>Accent at low alpha: the tint behind a selected/checked item's mark.</summary>
        public static readonly Color AccentSubtle = Color.FromArgb(0x2E, 0x5B, 0x9A, 0xF5);

        // ============================================
        // State layers and hairlines (white over the surface)
        // ============================================

        public static readonly Color HoverLayer = Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF);     // 8%
        public static readonly Color PressedLayer = Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF);   // 12%
        public static readonly Color Hairline = Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF);       // 10%
        /// <summary>Hairline lifted for a hovered control, so the edge brightens with the fill.</summary>
        public static readonly Color HairlineStrong = Color.FromArgb(0x29, 0xFF, 0xFF, 0xFF); // 16%

        /// <summary>Hairline composited onto Surface, for APIs that need an opaque colour (window border).</summary>
        public static readonly Color HairlineOnSurface = Composite(Hairline, Surface);

        // ============================================
        // Type and spacing (4px grid, in logical pixels; WinForms scales them for DPI)
        // ============================================

        public const string FONT_FAMILY = "Segoe UI";
        /// <summary>10pt ≈ 13px: one step above the Windows 9pt default, for couch-distance reading.</summary>
        public const float FONT_SIZE_POINTS = 10f;

        public const string FONT_FAMILY_SEMIBOLD = "Segoe UI Semibold";
        public const float TITLE_SIZE_POINTS = 15f;

        public const int MENU_PADDING = 4;          // inset so items never touch the rounded corners
        public const int MENU_ITEM_RADIUS = 4;      // menu radius 8 − inset 4: concentric corners

        // Windows (logical px at 96 DPI; scaled per monitor by ThemedForm.Scale)
        public const int WINDOW_PADDING = 24;
        public const int SPACE_SMALL = 8;
        public const int SPACE_MEDIUM = 16;
        public const int CONTROL_HEIGHT = 32;       // buttons and inputs share one height
        public const int CONTROL_RADIUS = 6;
        public const int CONTROL_PADDING_X = 14;    // ≈ 1.75 × the text's vertical padding
        public const int LIST_RADIUS = 8;
        public const int LIST_PADDING = 4;
        public const int LIST_ROW_HEIGHT = 52;
        public const int LIST_ROW_RADIUS = 4;       // list radius 8 − padding 4: concentric
        public const int LIST_ICON_SIZE = 32;
        public const int FOCUS_RING_WIDTH = 2;

        public static Font CreateFont(FontStyle style = FontStyle.Regular)
        {
            return new Font(FONT_FAMILY, FONT_SIZE_POINTS, style, GraphicsUnit.Point);
        }

        public static Font CreateSemiboldFont(float points = FONT_SIZE_POINTS)
        {
            return new Font(FONT_FAMILY_SEMIBOLD, points, FontStyle.Regular, GraphicsUnit.Point);
        }

        /// <summary>Secondary text one step smaller than body (9pt), for row details.</summary>
        public static Font CreateDetailFont()
        {
            return new Font(FONT_FAMILY, FONT_SIZE_POINTS - 1f, FontStyle.Regular, GraphicsUnit.Point);
        }

        public static System.Drawing.Drawing2D.GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            float diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
            if (diameter <= 0) { path.AddRectangle(bounds); return path; }
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static Color Composite(Color overlay, Color background)
        {
            float alpha = overlay.A / 255f;
            return Color.FromArgb(
                (int)(overlay.R * alpha + background.R * (1 - alpha) + 0.5f),
                (int)(overlay.G * alpha + background.G * (1 - alpha) + 0.5f),
                (int)(overlay.B * alpha + background.B * (1 - alpha) + 0.5f));
        }
    }
}
