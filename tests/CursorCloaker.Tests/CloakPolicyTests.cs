/*
 * CloakPolicyTests - every cloak/uncloak rule from docs/DESIGN.md.
 */
using CursorCloaker.Core;
using Xunit;

namespace CursorCloaker.Tests
{
    public class CloakPolicyTests
    {
        private static readonly ForegroundInfo FullscreenGame = new ForegroundInfo("game.exe", isFullscreen: true, isShell: false);
        private static readonly ForegroundInfo WindowedApp = new ForegroundInfo("notepad", isFullscreen: false, isShell: false);
        private static readonly ForegroundInfo Desktop = new ForegroundInfo("explorer", isFullscreen: true, isShell: true);

        private static CloakPolicy PolicyOver(ForegroundInfo foreground, CloakSettings settings = null)
        {
            var policy = new CloakPolicy(settings ?? new CloakSettings());
            policy.OnForegroundChanged(foreground);
            return policy;
        }

        [Fact]
        public void ControllerInputCloaksOverFullscreenWindow()
        {
            var policy = PolicyOver(FullscreenGame);

            Assert.Equal(CloakAction.Cloak, policy.OnControllerInput(0));
            Assert.True(policy.IsCloaked);
        }

        [Fact]
        public void RepeatedControllerInputDoesNotCloakTwice()
        {
            var policy = PolicyOver(FullscreenGame);
            policy.OnControllerInput(0);

            Assert.Equal(CloakAction.None, policy.OnControllerInput(10));
        }

        [Fact]
        public void FullscreenOnlyIgnoresWindowedApps()
        {
            Assert.Equal(CloakAction.None, PolicyOver(WindowedApp).OnControllerInput(0));
        }

        [Fact]
        public void AllProgramsScopeCloaksOverWindowedApps()
        {
            var policy = PolicyOver(WindowedApp, new CloakSettings { Scope = CloakScope.AllPrograms });

            Assert.Equal(CloakAction.Cloak, policy.OnControllerInput(0));
        }

        [Fact]
        public void DesktopIsNeverInScopeEvenForAllPrograms()
        {
            var policy = PolicyOver(Desktop, new CloakSettings { Scope = CloakScope.AllPrograms });

            Assert.Equal(CloakAction.None, policy.OnControllerInput(0));
        }

        [Fact]
        public void ExcludedProgramIsNeverCloakedOver()
        {
            var settings = new CloakSettings();
            settings.ExcludedProcesses.Add("game");

            Assert.Equal(CloakAction.None, PolicyOver(FullscreenGame, settings).OnControllerInput(0));
        }

        [Fact]
        public void DisabledNeverCloaks()
        {
            Assert.Equal(CloakAction.None, PolicyOver(FullscreenGame, new CloakSettings { Enabled = false }).OnControllerInput(0));
        }

        [Fact]
        public void MouseUncloaksAndStaysUncloakedUntilNextControllerInput()
        {
            var policy = PolicyOver(FullscreenGame);
            policy.OnControllerInput(0);

            Assert.Equal(CloakAction.Uncloak, policy.OnMouseInput());
            // However long the mouse then sits still, nothing re-cloaks on its own.
            Assert.Equal(CloakAction.None, policy.OnTick(60000));
            Assert.False(policy.IsCloaked);
            // The controller cloaks again.
            Assert.Equal(CloakAction.Cloak, policy.OnControllerInput(60001));
        }

        [Fact]
        public void MouseWhileUncloakedDoesNothing()
        {
            Assert.Equal(CloakAction.None, PolicyOver(FullscreenGame).OnMouseInput());
        }

        [Fact]
        public void LeavingScopeUncloaks()
        {
            var policy = PolicyOver(FullscreenGame);
            policy.OnControllerInput(0);

            Assert.Equal(CloakAction.Uncloak, policy.OnForegroundChanged(WindowedApp));
        }

        [Fact]
        public void ReturningToScopeDoesNotCloakWithoutControllerInput()
        {
            var policy = PolicyOver(FullscreenGame);
            policy.OnControllerInput(0);
            policy.OnForegroundChanged(WindowedApp);

            Assert.Equal(CloakAction.None, policy.OnForegroundChanged(FullscreenGame));
            Assert.False(policy.IsCloaked);
        }

        [Fact]
        public void SwitchingBetweenFullscreenWindowsKeepsCloak()
        {
            var policy = PolicyOver(FullscreenGame);
            policy.OnControllerInput(0);

            Assert.Equal(CloakAction.None, policy.OnForegroundChanged(new ForegroundInfo("steamwebhelper", true, false)));
            Assert.True(policy.IsCloaked);
        }

        [Fact]
        public void DisablingUncloaks()
        {
            var policy = PolicyOver(FullscreenGame);
            policy.OnControllerInput(0);

            Assert.Equal(CloakAction.Uncloak, policy.OnSettingsChanged(new CloakSettings { Enabled = false }));
        }

        [Fact]
        public void ExcludingTheCurrentProgramUncloaks()
        {
            var policy = PolicyOver(FullscreenGame);
            policy.OnControllerInput(0);
            var settings = new CloakSettings();
            settings.ExcludedProcesses.Add("GAME.EXE");

            Assert.Equal(CloakAction.Uncloak, policy.OnSettingsChanged(CloakSettings.Parse(settings.Serialize())));
        }

        [Fact]
        public void DelayedCloakFiresOnTickAfterDelay()
        {
            var policy = PolicyOver(FullscreenGame, new CloakSettings { HideDelayMilliseconds = 1000 });

            Assert.Equal(CloakAction.None, policy.OnControllerInput(100));
            Assert.True(policy.IsCloakPending);
            Assert.Equal(CloakAction.None, policy.OnTick(1099));
            // Further input during the delay doesn't push the deadline back.
            policy.OnControllerInput(900);
            Assert.Equal(CloakAction.Cloak, policy.OnTick(1100));
            Assert.False(policy.IsCloakPending);
        }

        [Fact]
        public void MouseCancelsPendingCloak()
        {
            var policy = PolicyOver(FullscreenGame, new CloakSettings { HideDelayMilliseconds = 1000 });
            policy.OnControllerInput(0);

            policy.OnMouseInput();

            Assert.Equal(CloakAction.None, policy.OnTick(5000));
        }

        [Fact]
        public void LeavingScopeCancelsPendingCloak()
        {
            var policy = PolicyOver(FullscreenGame, new CloakSettings { HideDelayMilliseconds = 1000 });
            policy.OnControllerInput(0);
            policy.OnForegroundChanged(WindowedApp);
            policy.OnForegroundChanged(FullscreenGame);

            Assert.Equal(CloakAction.None, policy.OnTick(5000));
        }

        [Fact]
        public void ResetUncloaks()
        {
            var policy = PolicyOver(FullscreenGame);
            policy.OnControllerInput(0);

            Assert.Equal(CloakAction.Uncloak, policy.Reset());
            Assert.Equal(CloakAction.None, policy.Reset());
        }

        [Fact]
        public void NoForegroundReportedMeansNothingCloaks()
        {
            var policy = new CloakPolicy(new CloakSettings { Scope = CloakScope.AllPrograms });

            Assert.Equal(CloakAction.None, policy.OnControllerInput(0));
        }
    }
}
