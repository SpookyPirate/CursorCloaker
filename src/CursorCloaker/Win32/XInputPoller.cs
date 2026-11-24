/*
 * XInputPoller - detects controller use on XInput pads (Xbox, 8BitDo in X mode, Steam's virtual pads).
 *
 * XInput has no events, so Poll() is called from a UI-thread timer (about 60 Hz). Each slot's
 * first reading after connecting is only a baseline, so plugging in a pad, or a stick already
 * resting off-centre, isn't mistaken for use.
 */
using System;
using System.Runtime.InteropServices;

using CursorCloaker.Core;

namespace CursorCloaker.Win32
{
    internal sealed class XInputPoller
    {
        private const int SLOT_COUNT = 4;
        private const uint ERROR_SUCCESS = 0;
        // After a slot reports "not connected", skip it for a while: probing an empty slot is the
        // expensive XInput call (documented guidance).
        private const int EMPTY_SLOT_RETRY_POLLS = 60;

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_STATE
        {
            public uint PacketNumber;
            public ushort Buttons;
            public byte LeftTrigger, RightTrigger;
            public short ThumbLX, ThumbLY, ThumbRX, ThumbRY;
        }

        [DllImport("xinput1_4.dll")]
        private static extern uint XInputGetState(uint userIndex, out XINPUT_STATE state);

        private readonly GamepadSample?[] previous = new GamepadSample?[SLOT_COUNT];
        private readonly int[] skipPolls = new int[SLOT_COUNT];
        private bool isAvailable = true;

        /// <summary>Raised (on the polling thread) when any pad is used.</summary>
        public event Action Activity;

        public void Poll()
        {
            if (!isAvailable) return;
            bool active = false;
            for (uint slot = 0; slot < SLOT_COUNT; slot++)
            {
                if (skipPolls[slot] > 0) { skipPolls[slot]--; continue; }
                active |= PollSlot(slot);
            }
            if (active) Activity?.Invoke();
        }

        private bool PollSlot(uint slot)
        {
            XINPUT_STATE state;
            try
            {
                if (XInputGetState(slot, out state) != ERROR_SUCCESS)
                {
                    previous[slot] = null;
                    skipPolls[slot] = EMPTY_SLOT_RETRY_POLLS;
                    return false;
                }
            }
            catch (DllNotFoundException)
            {
                // xinput1_4 ships with Windows 8+; without it, Raw Input still covers HID pads.
                isAvailable = false;
                return false;
            }

            var current = new GamepadSample
            {
                Buttons = state.Buttons,
                LeftTrigger = state.LeftTrigger,
                RightTrigger = state.RightTrigger,
                LeftX = state.ThumbLX, LeftY = state.ThumbLY,
                RightX = state.ThumbRX, RightY = state.ThumbRY,
            };
            bool active = previous[slot].HasValue && GamepadActivity.IsActive(previous[slot].Value, current);
            previous[slot] = current;
            return active;
        }
    }
}
