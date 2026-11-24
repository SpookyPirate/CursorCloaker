/*
 * CloakEngine - connects the input watchers to CloakPolicy and applies its decisions.
 *
 * Everything runs on the UI thread: a ~60 Hz timer polls XInput and fires delayed cloaks, Raw
 * Input arrives as window messages, and the mouse hook (on its own thread) posts here. Policy
 * decisions are applied by SystemCursors (hide / restore / park).
 *
 * Foreground is re-read every few ticks, and immediately before acting on controller input, so
 * a decision is never made against a stale window.
 */
using System;
using System.Diagnostics;
using System.Windows.Forms;

using CursorCloaker.Core;
using CursorCloaker.Win32;

namespace CursorCloaker
{
    internal sealed class CloakEngine : IDisposable
    {
        // 16 ms ≈ 60 Hz: controller response within a frame, negligible CPU.
        private const int TICK_MILLISECONDS = 16;
        // Foreground is re-read every 15 ticks (≈ 250 ms) to notice windows turning fullscreen.
        private const int FOREGROUND_POLL_TICKS = 15;

        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Timer timer = new Timer { Interval = TICK_MILLISECONDS };
        private readonly XInputPoller xinput = new XInputPoller();
        private readonly RawGamepadReader rawGamepads = new RawGamepadReader();
        private readonly MouseWatcher mouse = new MouseWatcher();
        private readonly ForegroundWatcher foregroundWatcher = new ForegroundWatcher();
        private readonly MouseMovementFilter mouseFilter = new MouseMovementFilter();
        private readonly CloakPolicy policy;

        private CloakSettings settings;
        private ForegroundInfo foreground = ForegroundInfo.None;
        private int ticksUntilForegroundPoll;

        public CloakEngine(CloakSettings settings)
        {
            this.settings = settings;
            policy = new CloakPolicy(settings);

            xinput.Activity += OnControllerActivity;
            rawGamepads.Activity += OnControllerActivity;
            mouse.Moved += OnMouseMoved;
            mouse.Clicked += OnMouseUsed;
            timer.Tick += OnTick;

            RefreshForeground();
            timer.Start();
        }

        /// <summary>True when the mouse hook and Raw Input registered (diagnostics).</summary>
        public bool IsMouseHooked => mouse.IsHooked;
        public bool IsRawInputRegistered => rawGamepads.IsRegistered;
        public bool IsCloaked => policy.IsCloaked;

        public void UpdateSettings(CloakSettings newSettings)
        {
            // A mode change while cloaked would leave the old effect half-applied; start clean.
            if (policy.IsCloaked && newSettings.Mode != settings.Mode) Apply(policy.Reset());
            settings = newSettings;
            Apply(policy.OnSettingsChanged(newSettings));
        }

        /// <summary>Uncloak now (session locked, app exiting). Controller input can cloak again later.</summary>
        public void Uncloak()
        {
            Apply(policy.Reset());
        }

        // ============================================
        // Input
        // ============================================

        private void OnControllerActivity()
        {
            RefreshForeground();
            bool wasPending = policy.IsCloakPending;
            Apply(policy.OnControllerInput(clock.ElapsedMilliseconds));
            // A newly scheduled delayed cloak can be cancelled by the mouse: measure from here.
            if (policy.IsCloakPending && !wasPending) ResetMouseFilterHere();
        }

        private void OnMouseMoved(int x, int y)
        {
            if (mouseFilter.OnMove(x, y)) OnMouseUsed();
        }

        private void OnMouseUsed()
        {
            Apply(policy.OnMouseInput());
        }

        private void OnTick(object sender, EventArgs args)
        {
            xinput.Poll();
            Apply(policy.OnTick(clock.ElapsedMilliseconds));
            if (--ticksUntilForegroundPoll <= 0) RefreshForeground();
        }

        private void RefreshForeground()
        {
            ticksUntilForegroundPoll = FOREGROUND_POLL_TICKS;
            var current = foregroundWatcher.Read();
            if (current.ProcessName == foreground.ProcessName && current.IsFullscreen == foreground.IsFullscreen && current.IsShell == foreground.IsShell) return;
            foreground = current;
            Apply(policy.OnForegroundChanged(current));
        }

        // ============================================
        // Effects
        // ============================================

        private void Apply(CloakAction action)
        {
            if (action == CloakAction.Cloak) CloakCursor();
            else if (action == CloakAction.Uncloak) UncloakCursor();
            mouse.IsArmed = policy.IsCloaked || policy.IsCloakPending;
        }

        private void CloakCursor()
        {
            if (settings.Mode != CloakMode.Park) SystemCursors.Hide();
            if (settings.Mode != CloakMode.Hide)
            {
                var parked = SystemCursors.Park();
                mouseFilter.Reset(parked.X, parked.Y);
            }
            else
            {
                ResetMouseFilterHere();
            }
        }

        private static void UncloakCursor()
        {
            if (SystemCursors.IsHidden) SystemCursors.Restore();
        }

        private void ResetMouseFilterHere()
        {
            var position = SystemCursors.GetPosition();
            mouseFilter.Reset(position.X, position.Y);
        }

        public void Dispose()
        {
            timer.Stop();
            timer.Dispose();
            Uncloak();
            mouse.Dispose();
            rawGamepads.Dispose();
            SystemCursors.Restore();
        }
    }
}
