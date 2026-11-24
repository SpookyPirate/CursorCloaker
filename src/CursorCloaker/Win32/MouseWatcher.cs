/*
 * MouseWatcher - reports deliberate mouse use (movement past the jitter filter, clicks, wheel).
 *
 * A WH_MOUSE_LL hook on a dedicated thread with its own message loop. Windows silently removes a
 * low-level hook whose thread doesn't answer within LowLevelHooksTimeout, so the hook must never
 * share a thread with UI work. Injected events count (streaming clients deliver the mouse that
 * way); SetCursorPos, used for parking, never reaches the hook. See docs/DESIGN.md.
 *
 * The hook callback only reads the event and posts it to the UI thread; filtering happens there.
 */
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace CursorCloaker.Win32
{
    internal sealed class MouseWatcher : IDisposable
    {
        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEMOVE = 0x0200;
        private const uint WM_QUIT = 0x0012;

        private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr module, uint threadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string name);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern int GetMessage(out MSG message, IntPtr window, uint filterMin, uint filterMax);

        [DllImport("user32.dll")]
        private static extern bool PostThreadMessage(uint threadId, uint message, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG { public IntPtr Window; public uint Message; public IntPtr WParam, LParam; public uint Time; public int X, Y; }

        private readonly SynchronizationContext uiContext;
        private readonly Thread thread;
        private readonly ManualResetEventSlim started = new ManualResetEventSlim();
        private HookProc hookProc;   // field keeps the delegate alive while Windows holds a pointer to it
        private uint threadId;

        /// <summary>Raised on the UI thread for every mouse move, with its screen position.</summary>
        public event Action<int, int> Moved;
        /// <summary>Raised on the UI thread for clicks and wheel turns.</summary>
        public event Action Clicked;

        public MouseWatcher()
        {
            uiContext = SynchronizationContext.Current ?? new SynchronizationContext();
            thread = new Thread(Run) { IsBackground = true, Name = "CursorCloaker mouse hook" };
            thread.Start();
            started.Wait(TimeSpan.FromSeconds(5));
        }

        /// <summary>
        /// Only while armed are events forwarded to the UI thread. The engine arms the watcher
        /// while the cursor is cloaked, so a 1000 Hz mouse costs nothing the rest of the time.
        /// </summary>
        public bool IsArmed { get => isArmed; set => isArmed = value; }
        private volatile bool isArmed;

        /// <summary>True once the hook was installed.</summary>
        public bool IsHooked { get; private set; }

        private void Run()
        {
            threadId = GetCurrentThreadId();
            hookProc = OnHook;
            var hook = SetWindowsHookEx(WH_MOUSE_LL, hookProc, GetModuleHandle(null), 0);
            IsHooked = hook != IntPtr.Zero;
            started.Set();
            if (!IsHooked) return;

            // A low-level hook is called through this thread's message queue.
            while (GetMessage(out _, IntPtr.Zero, 0, 0) > 0) { }
            UnhookWindowsHookEx(hook);
        }

        private IntPtr OnHook(int code, IntPtr message, IntPtr data)
        {
            if (code >= 0 && isArmed)
            {
                int kind = message.ToInt32();
                if (kind == WM_MOUSEMOVE)
                {
                    int x = Marshal.ReadInt32(data), y = Marshal.ReadInt32(data, 4);
                    uiContext.Post(_ => Moved?.Invoke(x, y), null);
                }
                else
                {
                    // Button down/up, wheel, horizontal wheel, X buttons: any of them is deliberate.
                    uiContext.Post(_ => Clicked?.Invoke(), null);
                }
            }
            return CallNextHookEx(IntPtr.Zero, code, message, data);
        }

        public void Dispose()
        {
            if (threadId != 0) PostThreadMessage(threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            thread.Join(TimeSpan.FromSeconds(2));
            started.Dispose();
        }
    }
}
