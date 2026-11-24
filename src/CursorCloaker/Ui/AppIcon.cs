/*
 * AppIcon - loads the app icon at the size a given surface needs.
 *
 * The .ico holds 16-256 px frames. Asking for an exact size picks the matching frame instead of
 * letting Windows scale the 32 px one, which blurs in the tray at 125%+ scaling.
 *
 * The paused variant keeps the tile solid and dims its contents: the bright controller and cursor
 * drop to a muted grey. Lowering opacity instead made the dark tile vanish into a dark taskbar,
 * which reads as "missing", not "paused".
 */
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CursorCloaker.Ui
{
    internal static class AppIcon
    {
        private const string RESOURCE_NAME = "CursorCloaker.ico";
        private const int SM_CXSMICON = 49;
        // Paused icon brightness (RGB multiplier, alpha unchanged): the white artwork becomes a mid grey.
        private const float PAUSED_BRIGHTNESS = 0.6f;

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr icon);

        /// <summary>The tray (small icon) size for the system DPI: 16 at 100%, 24 at 150%, 32 at 200%.</summary>
        public static Icon CreateTrayIcon(bool isPaused)
        {
            int size = GetSystemMetrics(SM_CXSMICON);
            var icon = Create(new Size(size, size));
            if (!isPaused) return icon;
            using (icon) return Dim(icon);
        }

        public static Icon Create(Size size)
        {
            using (var stream = typeof(AppIcon).Assembly.GetManifestResourceStream(RESOURCE_NAME))
            {
                return stream == null ? (Icon)SystemIcons.Application.Clone() : new Icon(stream, size);
            }
        }

        private static Icon Dim(Icon source)
        {
            using (var original = source.ToBitmap())
            using (var dimmed = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(dimmed))
                using (var attributes = new ImageAttributes())
                {
                    // Greyscale (luma weights) scaled down; alpha untouched so the tile stays solid.
                    float r = 0.299f * PAUSED_BRIGHTNESS, g = 0.587f * PAUSED_BRIGHTNESS, b = 0.114f * PAUSED_BRIGHTNESS;
                    attributes.SetColorMatrix(new ColorMatrix(new[]
                    {
                        new[] { r, r, r, 0f, 0f },
                        new[] { g, g, g, 0f, 0f },
                        new[] { b, b, b, 0f, 0f },
                        new[] { 0f, 0f, 0f, 1f, 0f },
                        new[] { 0f, 0f, 0f, 0f, 1f },
                    }));
                    graphics.DrawImage(original, new Rectangle(0, 0, original.Width, original.Height),
                        0, 0, original.Width, original.Height, GraphicsUnit.Pixel, attributes);
                }

                // FromHandle doesn't own the handle; Clone makes an owned copy, then the original goes.
                var handle = dimmed.GetHicon();
                try { return (Icon)Icon.FromHandle(handle).Clone(); }
                finally { DestroyIcon(handle); }
            }
        }
    }
}
