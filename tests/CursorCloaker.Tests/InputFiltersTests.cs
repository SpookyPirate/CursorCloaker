/*
 * InputFiltersTests - controller activity, mouse jitter and fullscreen geometry thresholds.
 */
using CursorCloaker.Core;
using Xunit;

namespace CursorCloaker.Tests
{
    public class GamepadActivityTests
    {
        private static GamepadSample Idle => new GamepadSample();

        [Fact]
        public void NewButtonPressIsActivity()
        {
            Assert.True(GamepadActivity.IsActive(Idle, new GamepadSample { Buttons = 0x10 }));
        }

        [Fact]
        public void HeldOrReleasedButtonIsNotActivity()
        {
            var held = new GamepadSample { Buttons = 0x10 };

            Assert.False(GamepadActivity.IsActive(held, held));
            Assert.False(GamepadActivity.IsActive(held, Idle));
        }

        [Fact]
        public void SecondButtonWhileOneIsHeldIsActivity()
        {
            Assert.True(GamepadActivity.IsActive(new GamepadSample { Buttons = 0x1 }, new GamepadSample { Buttons = 0x3 }));
        }

        [Fact]
        public void StickWobbleInsideDeadZoneIsNotActivity()
        {
            Assert.False(GamepadActivity.IsActive(Idle, new GamepadSample { LeftX = 3000, LeftY = -4000 }));
        }

        [Fact]
        public void StickPushedOutOfDeadZoneIsActivity()
        {
            Assert.True(GamepadActivity.IsActive(Idle, new GamepadSample { RightX = 20000 }));
        }

        [Fact]
        public void DriftingStickRestingOutsideDeadZoneIsNotActivity()
        {
            var drift = new GamepadSample { LeftX = 9000 };

            Assert.False(GamepadActivity.IsActive(drift, new GamepadSample { LeftX = 9400, LeftY = 300 }));
        }

        [Fact]
        public void StickMovedWhileOutsideDeadZoneIsActivity()
        {
            Assert.True(GamepadActivity.IsActive(new GamepadSample { LeftX = 9000 }, new GamepadSample { LeftX = 20000 }));
        }

        [Fact]
        public void ExtremeAxesDoNotOverflow()
        {
            Assert.True(GamepadActivity.IsActive(new GamepadSample { LeftX = short.MinValue, LeftY = short.MinValue },
                                                 new GamepadSample { LeftX = short.MaxValue, LeftY = short.MaxValue }));
        }

        [Fact]
        public void TriggerPulledPastThresholdIsActivity()
        {
            Assert.True(GamepadActivity.IsActive(Idle, new GamepadSample { RightTrigger = 200 }));
            Assert.False(GamepadActivity.IsActive(Idle, new GamepadSample { RightTrigger = 20 }));
            Assert.False(GamepadActivity.IsActive(new GamepadSample { LeftTrigger = 200 }, new GamepadSample { LeftTrigger = 255 }));
        }

        [Theory]
        [InlineData(0, 0, 255, -32768)]
        [InlineData(255, 0, 255, 32767)]
        [InlineData(128, 0, 255, 128)]
        [InlineData(-5, 0, 255, -32768)]
        [InlineData(5, 5, 5, 0)]
        public void ScaleAxisMapsLogicalRange(int value, int minimum, int maximum, int expected)
        {
            Assert.Equal(expected, GamepadActivity.ScaleAxis(value, minimum, maximum));
        }
    }

    public class MouseMovementFilterTests
    {
        [Fact]
        public void SmallWobbleDoesNotCount()
        {
            var filter = new MouseMovementFilter();
            filter.Reset(100, 100);

            Assert.False(filter.OnMove(101, 100));
            Assert.False(filter.OnMove(100, 101));
            Assert.False(filter.OnMove(101, 101));
        }

        [Fact]
        public void DeliberateMovementCounts()
        {
            var filter = new MouseMovementFilter();
            filter.Reset(100, 100);

            Assert.True(filter.OnMove(110, 100));
        }

        [Fact]
        public void ResetStartsOver()
        {
            var filter = new MouseMovementFilter();
            filter.Reset(0, 0);
            filter.OnMove(5, 0);

            filter.Reset(5, 0);

            Assert.False(filter.OnMove(7, 0));
        }

        [Fact]
        public void FirstEventWithoutResetOnlySetsOrigin()
        {
            Assert.False(new MouseMovementFilter().OnMove(500, 500));
        }
    }

    public class ScreenGeometryTests
    {
        private static readonly ScreenRect Monitor = new ScreenRect(0, 0, 3840, 2160);

        [Fact]
        public void ExactFullscreenCovers()
        {
            Assert.True(ScreenGeometry.CoversMonitor(new ScreenRect(0, 0, 3840, 2160), Monitor));
        }

        [Fact]
        public void BorderlessOnePixelShortStillCovers()
        {
            Assert.True(ScreenGeometry.CoversMonitor(new ScreenRect(0, 0, 3839, 2159), Monitor));
        }

        [Fact]
        public void MaximisedWindowAboveTaskbarDoesNotCover()
        {
            Assert.False(ScreenGeometry.CoversMonitor(new ScreenRect(0, 0, 3840, 2088), Monitor));
        }

        [Fact]
        public void OversizedWindowCovers()
        {
            Assert.True(ScreenGeometry.CoversMonitor(new ScreenRect(-8, -8, 3848, 2168), Monitor));
        }

        [Fact]
        public void SecondMonitorCoordinatesWork()
        {
            var second = new ScreenRect(-1920, 0, 0, 1080);

            Assert.True(ScreenGeometry.CoversMonitor(new ScreenRect(-1920, 0, 0, 1080), second));
            ScreenGeometry.ParkPoint(second, ParkPosition.BottomRight, out int x, out int y);
            Assert.Equal(-1, x);
            Assert.Equal(1079, y);
        }
        [Theory]
        [InlineData("BottomRight", 3839, 2159)]
        [InlineData("BottomLeft", 0, 2159)]
        [InlineData("TopRight", 3839, 0)]
        [InlineData("TopLeft", 0, 0)]
        [InlineData("BottomEdge", 1919, 2159)]
        [InlineData("TopEdge", 1919, 0)]
        [InlineData("LeftEdge", 0, 1079)]
        [InlineData("RightEdge", 3839, 1079)]
        public void ParkPointLandsOnTheMonitorsOutermostPixels(string position, int expectedX, int expectedY)
        {
            // By name: the enum is internal, and a public test method can't take it as a parameter.
            ScreenGeometry.ParkPoint(Monitor, (ParkPosition)System.Enum.Parse(typeof(ParkPosition), position), out int x, out int y);

            Assert.Equal(expectedX, x);
            Assert.Equal(expectedY, y);
        }

        [Fact]
        public void EdgePositionsUseThatMonitorsOwnCentre()
        {
            ScreenGeometry.ParkPoint(new ScreenRect(-1920, 200, 0, 1280), ParkPosition.LeftEdge, out int x, out int y);

            Assert.Equal(-1920, x);
            Assert.Equal(739, y);
        }
    }
}