/*
 * ThemedForm - base for every CursorCloaker window: dark ground, theme font, dark title bar.
 *
 * Layout is done in code in logical pixels (96 DPI) and converted with Scale(), so windows are
 * sized correctly on every monitor; the process is per-monitor DPI aware (app.manifest).
 */
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CursorCloaker.Ui
{
    internal class ThemedForm : Form
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_TEXT_COLOR = 36;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        public ThemedForm()
        {
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Theme.Background;
            ForeColor = Theme.TextPrimary;
            Font = Theme.CreateFont();
            Icon = AppIcon.Create(new Size(32, 32));
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            // The heading inside each window names it; the title bar names the app (Alt-Tab, taskbar).
            Text = "CursorCloaker";
        }

        /// <summary>Logical (96 DPI) pixels to physical pixels for this window's monitor.</summary>
        protected int Scale(int logicalPixels)
        {
            return (int)Math.Round(logicalPixels * DeviceDpi / 96f);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Dark title bar on Windows 10 2004+ and 11; caption colours on Windows 11 only.
            int enabled = 1;
            DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref enabled, sizeof(int));
            int caption = ToColorRef(Theme.Background), text = ToColorRef(Theme.TextPrimary);
            DwmSetWindowAttribute(Handle, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
            DwmSetWindowAttribute(Handle, DWMWA_TEXT_COLOR, ref text, sizeof(int));
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { Close(); e.Handled = true; return; }
            base.OnKeyDown(e);
        }

        private static int ToColorRef(Color color)
        {
            return color.R | (color.G << 8) | (color.B << 16);
        }
    }
}
