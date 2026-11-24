/*
 * AppIcon - loads the app icon at the size a given surface needs.
 *
 * The .ico holds 16-256 px frames. Asking for an exact size picks the matching hand-built frame
 * instead of letting Windows scale the 32 px one, which blurs in the tray at 125%+ scaling.
 */
using System.Drawing;
using System.Runtime.InteropServices;

namespace CursorCloaker.Ui
{
    internal static class AppIcon
    {
        private const string RESOURCE_NAME = "CursorCloaker.ico";
        private const int SM_CXSMICON = 49;

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        /// <summary>The tray (small icon) size for the system DPI: 16 at 100%, 24 at 150%, 32 at 200%.</summary>
        public static Icon CreateTrayIcon()
        {
            int size = GetSystemMetrics(SM_CXSMICON);
            return Create(new Size(size, size));
        }

        public static Icon Create(Size size)
        {
            using (var stream = typeof(AppIcon).Assembly.GetManifestResourceStream(RESOURCE_NAME))
            {
                return stream == null ? SystemIcons.Application : new Icon(stream, size);
            }
        }
    }
}
