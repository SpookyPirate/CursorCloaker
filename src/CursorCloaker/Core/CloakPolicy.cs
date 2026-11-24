/*
 * CloakPolicy - decides when the cursor is cloaked and uncloaked.
 *
 * Pure state machine: callers report what happened (controller input, real mouse input, a new
 * foreground window, a timer tick, new settings) and get back what to do. No Win32 here, so
 * every rule in docs/DESIGN.md is covered by unit tests.
 *
 * Rules:
 *   - Controller input cloaks, after HideDelayMs, if enabled and the foreground is in scope.
 *   - Real mouse input uncloaks and cancels a pending cloak. Only the next controller input
 *     cloaks again ("the mouse wins until the controller is used again").
 *   - Leaving scope (foreground change, exclusion, disabling) uncloaks.
 */
namespace CursorCloaker.Core
{
    internal enum CloakAction
    {
        None,
        Cloak,
        Uncloak,
    }

    /// <summary>The foreground window, as far as cloaking decisions need it.</summary>
    internal sealed class ForegroundInfo
    {
        public static readonly ForegroundInfo None = new ForegroundInfo("", false, true);

        public ForegroundInfo(string processName, bool isFullscreen, bool isShell)
        {
            ProcessName = CloakSettings.NormalizeProcessName(processName);
            IsFullscreen = isFullscreen;
            IsShell = isShell;
        }

        /// <summary>Lower-case, without ".exe".</summary>
        public string ProcessName { get; }
        /// <summary>The window covers its whole monitor (fullscreen or borderless).</summary>
        public bool IsFullscreen { get; }
        /// <summary>Desktop, taskbar, no window, or CursorCloaker itself: never cloaked over.</summary>
        public bool IsShell { get; }
    }

    internal sealed class CloakPolicy
    {
        private CloakSettings settings;
        private ForegroundInfo foreground = ForegroundInfo.None;
        // Time (ms) at which a delayed cloak fires; null when none is pending.
        private long? pendingCloakAt;

        public CloakPolicy(CloakSettings settings)
        {
            this.settings = settings;
        }

        public bool IsCloaked { get; private set; }
        public bool IsCloakPending => pendingCloakAt.HasValue;

        public static bool IsInScope(CloakSettings settings, ForegroundInfo foreground)
        {
            if (!settings.Enabled || foreground.IsShell) return false;
            if (settings.ExcludedProcesses.Contains(foreground.ProcessName)) return false;
            return settings.Scope == CloakScope.AllPrograms || foreground.IsFullscreen;
        }

        public CloakAction OnControllerInput(long nowMilliseconds)
        {
            if (IsCloaked || !IsInScope(settings, foreground)) return CloakAction.None;
            if (settings.HideDelayMilliseconds <= 0) return SetCloaked(true);

            // Repeated input during the delay keeps the original deadline, so holding a stick
            // still cloaks on time.
            if (!pendingCloakAt.HasValue) pendingCloakAt = nowMilliseconds + settings.HideDelayMilliseconds;
            return CloakAction.None;
        }

        public CloakAction OnMouseInput()
        {
            pendingCloakAt = null;
            return IsCloaked ? SetCloaked(false) : CloakAction.None;
        }

        public CloakAction OnForegroundChanged(ForegroundInfo newForeground)
        {
            foreground = newForeground ?? ForegroundInfo.None;
            return ReevaluateScope();
        }

        public CloakAction OnSettingsChanged(CloakSettings newSettings)
        {
            settings = newSettings;
            return ReevaluateScope();
        }

        /// <summary>Fires a delayed cloak once its time has come.</summary>
        public CloakAction OnTick(long nowMilliseconds)
        {
            if (!pendingCloakAt.HasValue || nowMilliseconds < pendingCloakAt.Value) return CloakAction.None;
            pendingCloakAt = null;
            return !IsCloaked && IsInScope(settings, foreground) ? SetCloaked(true) : CloakAction.None;
        }

        /// <summary>Forces the uncloaked state, e.g. when the session locks or the app exits.</summary>
        public CloakAction Reset()
        {
            pendingCloakAt = null;
            return IsCloaked ? SetCloaked(false) : CloakAction.None;
        }

        private CloakAction ReevaluateScope()
        {
            if (IsInScope(settings, foreground)) return CloakAction.None;
            pendingCloakAt = null;
            return IsCloaked ? SetCloaked(false) : CloakAction.None;
        }

        private CloakAction SetCloaked(bool cloaked)
        {
            IsCloaked = cloaked;
            return cloaked ? CloakAction.Cloak : CloakAction.Uncloak;
        }
    }
}
