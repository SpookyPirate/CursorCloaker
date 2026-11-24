/*
 * InputFilters - turn raw controller, mouse and window readings into "did the user act?".
 *
 * Pure functions so the thresholds are tested. They exist because raw readings are noisy:
 * analog sticks drift and jitter, optical mice report sub-pixel wobble on a vibrating desk,
 * and borderless games are sometimes a pixel short of their monitor.
 */
using System;

namespace CursorCloaker.Core
{
    /// <summary>One reading of a controller, normalised so XInput and HID pads compare the same way.</summary>
    internal struct GamepadSample
    {
        /// <summary>Pressed buttons (one bit each; the d-pad counts as buttons).</summary>
        public ulong Buttons;
        /// <summary>Axes scaled to -32768..32767; triggers and unused axes may stay 0.</summary>
        public short LeftX, LeftY, RightX, RightY;
        /// <summary>A third axis pair some HID pads report (Z/Rz: a right stick or analog triggers).</summary>
        public short ExtraX, ExtraY;
        /// <summary>Triggers scaled to 0..255.</summary>
        public byte LeftTrigger, RightTrigger;
    }

    internal static class GamepadActivity
    {
        // XInput's documented left-stick dead zone; used for every stick so pads behave alike.
        public const int STICK_DEAD_ZONE = 7849;
        // A stick resting outside the dead zone (drift) must move this much more to count.
        public const int STICK_MOVE_THRESHOLD = 2500;
        // XInput's documented trigger threshold.
        public const int TRIGGER_THRESHOLD = 30;

        /// <summary>
        /// True when the user did something between two readings: a newly pressed button, a stick
        /// pushed out of its dead zone or moved while out of it, or a trigger pulled past threshold.
        /// Releases don't count; letting go isn't "using the controller".
        /// </summary>
        public static bool IsActive(GamepadSample previous, GamepadSample current)
        {
            if ((current.Buttons & ~previous.Buttons) != 0) return true;
            if (StickActive(previous.LeftX, previous.LeftY, current.LeftX, current.LeftY)) return true;
            if (StickActive(previous.RightX, previous.RightY, current.RightX, current.RightY)) return true;
            if (StickActive(previous.ExtraX, previous.ExtraY, current.ExtraX, current.ExtraY)) return true;
            return TriggerPulled(previous.LeftTrigger, current.LeftTrigger) || TriggerPulled(previous.RightTrigger, current.RightTrigger);
        }

        private static bool StickActive(short previousX, short previousY, short currentX, short currentY)
        {
            if (!IsOutsideDeadZone(currentX, currentY)) return false;
            if (!IsOutsideDeadZone(previousX, previousY)) return true;
            long dx = currentX - previousX, dy = currentY - previousY;
            return dx * dx + dy * dy >= (long)STICK_MOVE_THRESHOLD * STICK_MOVE_THRESHOLD;
        }

        private static bool IsOutsideDeadZone(short x, short y)
        {
            return (long)x * x + (long)y * y > (long)STICK_DEAD_ZONE * STICK_DEAD_ZONE;
        }

        private static bool TriggerPulled(byte previous, byte current)
        {
            return current > TRIGGER_THRESHOLD && previous <= TRIGGER_THRESHOLD;
        }

        /// <summary>Scale a HID axis value from its logical range to -32768..32767.</summary>
        public static short ScaleAxis(int value, int logicalMinimum, int logicalMaximum)
        {
            if (logicalMaximum <= logicalMinimum) return 0;
            double unit = (Math.Max(logicalMinimum, Math.Min(logicalMaximum, value)) - (double)logicalMinimum) / (logicalMaximum - (double)logicalMinimum);
            return (short)Math.Round(-32768 + unit * 65535);
        }
    }

    /// <summary>Accumulates real mouse movement so sensor wobble doesn't uncloak the cursor.</summary>
    internal sealed class MouseMovementFilter
    {
        // Total distance (px) that counts as deliberate movement. Small enough to feel instant.
        public const int MOVE_THRESHOLD_PIXELS = 6;

        private int lastX, lastY;
        private bool hasLast;
        private double travelled;

        /// <summary>Report a pointer position; true once movement since the last Reset adds up.</summary>
        public bool OnMove(int x, int y)
        {
            if (hasLast)
            {
                double dx = x - lastX, dy = y - lastY;
                travelled += Math.Sqrt(dx * dx + dy * dy);
            }
            lastX = x; lastY = y; hasLast = true;
            return travelled >= MOVE_THRESHOLD_PIXELS;
        }

        /// <summary>Start counting from zero at a known position (after cloaking or parking).</summary>
        public void Reset(int x, int y)
        {
            lastX = x; lastY = y; hasLast = true;
            travelled = 0;
        }
    }

    /// <summary>A rectangle in physical screen pixels (left/top inclusive, right/bottom exclusive).</summary>
    internal struct ScreenRect
    {
        public int Left, Top, Right, Bottom;

        public ScreenRect(int left, int top, int right, int bottom)
        {
            Left = left; Top = top; Right = right; Bottom = bottom;
        }
    }

    internal static class ScreenGeometry
    {
        // Big Picture and some borderless games end a pixel or two short of the monitor edge.
        public const int FULLSCREEN_TOLERANCE_PIXELS = 2;

        /// <summary>True when the window covers the whole monitor (fullscreen or borderless).</summary>
        public static bool CoversMonitor(ScreenRect window, ScreenRect monitor)
        {
            return window.Left <= monitor.Left + FULLSCREEN_TOLERANCE_PIXELS
                && window.Top <= monitor.Top + FULLSCREEN_TOLERANCE_PIXELS
                && window.Right >= monitor.Right - FULLSCREEN_TOLERANCE_PIXELS
                && window.Bottom >= monitor.Bottom - FULLSCREEN_TOLERANCE_PIXELS;
        }

        /// <summary>Where a parked cursor goes: the monitor's bottom-right pixel.</summary>
        public static void ParkPoint(ScreenRect monitor, out int x, out int y)
        {
            x = monitor.Right - 1;
            y = monitor.Bottom - 1;
        }
    }
}
