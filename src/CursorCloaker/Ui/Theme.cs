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
        /// <summary>Accent at low alpha: the tint behind a selected/checked item's mark.</summary>
        public static readonly Color AccentSubtle = Color.FromArgb(0x2E, 0x5B, 0x9A, 0xF5);

        // ============================================
        // State layers and hairlines (white over the surface)
        // ============================================

        public static readonly Color HoverLayer = Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF);     // 8%
        public static readonly Color PressedLayer = Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF);   // 12%
        public static readonly Color Hairline = Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF);       // 10%

        /// <summary>Hairline composited onto Surface, for APIs that need an opaque colour (window border).</summary>
        public static readonly Color HairlineOnSurface = Composite(Hairline, Surface);

        // ============================================
        // Type and spacing (4px grid, in logical pixels; WinForms scales them for DPI)
        // ============================================

        public const string FONT_FAMILY = "Segoe UI";
        /// <summary>10pt ≈ 13px: one step above the Windows 9pt default, for couch-distance reading.</summary>
        public const float FONT_SIZE_POINTS = 10f;

        public const int MENU_PADDING = 4;          // inset so items never touch the rounded corners
        public const int MENU_ITEM_RADIUS = 4;      // menu radius 8 − inset 4: concentric corners

        public static Font CreateFont(FontStyle style = FontStyle.Regular)
        {
            return new Font(FONT_FAMILY, FONT_SIZE_POINTS, style, GraphicsUnit.Point);
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
