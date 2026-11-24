/*
 * Program - entry point for the CursorCloaker tray app.
 *
 * Responsibilities here are limited to process-level concerns: one instance per user session,
 * restoring the system cursors on start and on any exit path, and running the tray context.
 */
using System;
using System.Threading;
using System.Windows.Forms;

using CursorCloaker.Win32;

namespace CursorCloaker
{
    internal static class Program
    {
        // Local\ scope: one instance per signed-in session, independent of other users.
        private const string SINGLE_INSTANCE_MUTEX = @"Local\CursorCloaker.SingleInstance";

        [STAThread]
        private static void Main()
        {
            using (var mutex = new Mutex(true, SINGLE_INSTANCE_MUTEX, out bool isFirstInstance))
            {
                if (!isFirstInstance) return;

                // A previous run that was killed may have left the blank cursor in place.
                SystemCursors.Restore();
                AppDomain.CurrentDomain.UnhandledException += (sender, args) => SystemCursors.Restore();
                AppDomain.CurrentDomain.ProcessExit += (sender, args) => SystemCursors.Restore();

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += (sender, args) => { SystemCursors.Restore(); Application.Exit(); };
                try
                {
                    Application.Run(new TrayContext());
                }
                finally
                {
                    SystemCursors.Restore();
                }
            }
        }
    }
}
