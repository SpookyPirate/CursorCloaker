/*
 * RawGamepadReader - detects controller use on HID gamepads and joysticks through Raw Input.
 *
 * Covers pads XInput can't see: Switch Pro, DualShock/DualSense, and pads in DInput mode.
 * A message-only window registers for HID usage page 0x01, usages 0x04 (joystick) and 0x05
 * (gamepad), with RIDEV_INPUTSINK so reports arrive while other programs have focus.
 *
 * Reports are decoded with the device's own HID descriptor (hid.dll HidP_* functions) into a
 * GamepadSample: buttons from usage page 0x09, X/Y, Rx/Ry and Z/Rz as three axis pairs, and the
 * hat switch as extra button bits. Comparing decoded values (not raw bytes) is what keeps
 * analog noise and vendor data such as motion sensors from looking like use.
 *
 * Limitation: vendor-specific report modes that the descriptor doesn't describe (e.g. a Switch
 * Pro Controller switched into its full 0x30 mode by Steam) decode to nothing and are ignored.
 */
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

using CursorCloaker.Core;

namespace CursorCloaker.Win32
{
    internal sealed class RawGamepadReader : NativeWindow, IDisposable
    {
        private const int WM_INPUT = 0x00FF;
        private const int WM_INPUT_DEVICE_CHANGE = 0x00FE;
        private const int GIDC_REMOVAL = 2;
        private const uint RIDEV_INPUTSINK = 0x00000100;
        private const uint RIDEV_DEVNOTIFY = 0x00002000;
        private const uint RIDEV_REMOVE = 0x00000001;
        private const uint RID_INPUT = 0x10000003;
        private const uint RIDI_PREPARSEDDATA = 0x20000005;
        private const uint RIM_TYPEHID = 2;
        private const ushort USAGE_PAGE_GENERIC = 0x01;
        private const ushort USAGE_PAGE_BUTTON = 0x09;
        private const ushort USAGE_JOYSTICK = 0x04;
        private const ushort USAGE_GAMEPAD = 0x05;
        private const ushort USAGE_X = 0x30, USAGE_Y = 0x31, USAGE_Z = 0x32, USAGE_RX = 0x33, USAGE_RY = 0x34, USAGE_RZ = 0x35, USAGE_HAT = 0x39;
        private const int HIDP_STATUS_SUCCESS = 0x00110000;
        private const int HIDP_INPUT = 0;
        // Hat directions become button bits from here up, clear of real buttons (usages 1-48).
        private const int HAT_BIT_OFFSET = 48;

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE { public ushort UsagePage; public ushort Usage; public uint Flags; public IntPtr Target; }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER { public uint Type; public uint Size; public IntPtr Device; public IntPtr WParam; }

        [StructLayout(LayoutKind.Sequential)]
        private struct HIDP_CAPS
        {
            public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices;
            public ushort NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devices, uint count, uint size);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputData(IntPtr rawInput, uint command, IntPtr data, ref uint size, uint headerSize);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputDeviceInfo(IntPtr device, uint command, IntPtr data, ref uint size);

        [DllImport("hid.dll")]
        private static extern int HidP_GetCaps(IntPtr preparsed, out HIDP_CAPS caps);

        [DllImport("hid.dll")]
        private static extern int HidP_GetValueCaps(int reportType, IntPtr valueCaps, ref ushort length, IntPtr preparsed);

        [DllImport("hid.dll")]
        private static extern uint HidP_MaxUsageListLength(int reportType, ushort usagePage, IntPtr preparsed);

        [DllImport("hid.dll")]
        private static extern int HidP_GetUsages(int reportType, ushort usagePage, ushort linkCollection, [Out] ushort[] usages, ref uint length, IntPtr preparsed, byte[] report, uint reportLength);

        [DllImport("hid.dll")]
        private static extern int HidP_GetUsageValue(int reportType, ushort usagePage, ushort linkCollection, ushort usage, out uint value, IntPtr preparsed, byte[] report, uint reportLength);

        /// <summary>Per-device decoder state, built from the device's HID descriptor on first report.</summary>
        private sealed class Device : IDisposable
        {
            public IntPtr Preparsed;
            public uint MaxButtons;
            public readonly Dictionary<ushort, AxisRange> Axes = new Dictionary<ushort, AxisRange>();
            public GamepadSample? Previous;

            public void Dispose()
            {
                if (Preparsed != IntPtr.Zero) Marshal.FreeHGlobal(Preparsed);
                Preparsed = IntPtr.Zero;
            }
        }

        private struct AxisRange { public int Minimum, Maximum; }

        private readonly Dictionary<IntPtr, Device> devices = new Dictionary<IntPtr, Device>();

        /// <summary>Raised on the UI thread when any HID pad is used.</summary>
        public event Action Activity;

        public RawGamepadReader()
        {
            // HWND_MESSAGE (-3): a message-only window, never shown, receives WM_INPUT.
            CreateHandle(new CreateParams { Parent = new IntPtr(-3), Caption = "CursorCloaker raw input" });
            IsRegistered = Register(RIDEV_INPUTSINK | RIDEV_DEVNOTIFY, Handle);
        }

        /// <summary>True when Windows accepted the Raw Input registration.</summary>
        public bool IsRegistered { get; }

        private static bool Register(uint flags, IntPtr target)
        {
            var requests = new[]
            {
                new RAWINPUTDEVICE { UsagePage = USAGE_PAGE_GENERIC, Usage = USAGE_JOYSTICK, Flags = flags, Target = target },
                new RAWINPUTDEVICE { UsagePage = USAGE_PAGE_GENERIC, Usage = USAGE_GAMEPAD, Flags = flags, Target = target },
            };
            return RegisterRawInputDevices(requests, (uint)requests.Length, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == WM_INPUT) OnInput(message.LParam);
            else if (message.Msg == WM_INPUT_DEVICE_CHANGE && message.WParam.ToInt32() == GIDC_REMOVAL) Forget(message.LParam);
            base.WndProc(ref message);
        }

        // ============================================
        // Report handling
        // ============================================

        private void OnInput(IntPtr rawInput)
        {
            uint headerSize = (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER));
            uint size = 0;
            GetRawInputData(rawInput, RID_INPUT, IntPtr.Zero, ref size, headerSize);
            if (size == 0) return;

            var buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                if (GetRawInputData(rawInput, RID_INPUT, buffer, ref size, headerSize) != size) return;
                var header = (RAWINPUTHEADER)Marshal.PtrToStructure(buffer, typeof(RAWINPUTHEADER));
                if (header.Type != RIM_TYPEHID) return;

                var device = GetDevice(header.Device);
                if (device == null) return;

                // RAWHID: report size, report count, then the reports back to back.
                var hid = buffer + (int)headerSize;
                int reportSize = Marshal.ReadInt32(hid), reportCount = Marshal.ReadInt32(hid, 4);
                if (reportSize <= 0 || reportCount <= 0) return;

                bool active = false;
                var report = new byte[reportSize];
                for (int i = 0; i < reportCount; i++)
                {
                    Marshal.Copy(hid + 8 + i * reportSize, report, 0, reportSize);
                    active |= Decode(device, report);
                }
                if (active) Activity?.Invoke();
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        /// <summary>Decode one report; true when it shows use compared with the last report.</summary>
        private static bool Decode(Device device, byte[] report)
        {
            var sample = new GamepadSample();
            bool decodedAnything = DecodeButtons(device, report, ref sample);
            decodedAnything |= DecodeAxes(device, report, ref sample);
            if (!decodedAnything) return false;   // a report this descriptor doesn't describe

            bool active = device.Previous.HasValue && GamepadActivity.IsActive(device.Previous.Value, sample);
            device.Previous = sample;
            return active;
        }

        private static bool DecodeButtons(Device device, byte[] report, ref GamepadSample sample)
        {
            if (device.MaxButtons == 0) return false;
            var usages = new ushort[device.MaxButtons];
            uint count = device.MaxButtons;
            if (HidP_GetUsages(HIDP_INPUT, USAGE_PAGE_BUTTON, 0, usages, ref count, device.Preparsed, report, (uint)report.Length) != HIDP_STATUS_SUCCESS) return false;
            for (int i = 0; i < count; i++)
            {
                if (usages[i] >= 1 && usages[i] <= HAT_BIT_OFFSET) sample.Buttons |= 1UL << (usages[i] - 1);
            }
            return true;
        }

        private static bool DecodeAxes(Device device, byte[] report, ref GamepadSample sample)
        {
            bool decoded = false;
            foreach (var axis in device.Axes)
            {
                if (HidP_GetUsageValue(HIDP_INPUT, USAGE_PAGE_GENERIC, 0, axis.Key, out uint raw, device.Preparsed, report, (uint)report.Length) != HIDP_STATUS_SUCCESS) continue;
                decoded = true;
                int value = (int)raw;
                if (axis.Key == USAGE_HAT)
                {
                    // In range = a direction (each one its own bit); out of range = centred.
                    if (value >= axis.Value.Minimum && value <= axis.Value.Maximum)
                        sample.Buttons |= 1UL << (HAT_BIT_OFFSET + Math.Min(15, value - axis.Value.Minimum));
                    continue;
                }
                short scaled = GamepadActivity.ScaleAxis(value, axis.Value.Minimum, axis.Value.Maximum);
                switch (axis.Key)
                {
                    case USAGE_X: sample.LeftX = scaled; break;
                    case USAGE_Y: sample.LeftY = scaled; break;
                    case USAGE_RX: sample.RightX = scaled; break;
                    case USAGE_RY: sample.RightY = scaled; break;
                    case USAGE_Z: sample.ExtraX = scaled; break;
                    case USAGE_RZ: sample.ExtraY = scaled; break;
                }
            }
            return decoded;
        }

        // ============================================
        // Device descriptors
        // ============================================

        private Device GetDevice(IntPtr handle)
        {
            if (devices.TryGetValue(handle, out var known)) return known;
            var device = LoadDevice(handle);
            devices[handle] = device;   // null is cached too: an unusable device isn't retried per report
            return device;
        }

        private static Device LoadDevice(IntPtr handle)
        {
            uint size = 0;
            GetRawInputDeviceInfo(handle, RIDI_PREPARSEDDATA, IntPtr.Zero, ref size);
            if (size == 0) return null;

            var device = new Device { Preparsed = Marshal.AllocHGlobal((int)size) };
            if (GetRawInputDeviceInfo(handle, RIDI_PREPARSEDDATA, device.Preparsed, ref size) == unchecked((uint)-1)
                || HidP_GetCaps(device.Preparsed, out var caps) != HIDP_STATUS_SUCCESS)
            {
                device.Dispose();
                return null;
            }

            device.MaxButtons = HidP_MaxUsageListLength(HIDP_INPUT, USAGE_PAGE_BUTTON, device.Preparsed);
            LoadAxes(device, caps.NumberInputValueCaps);
            return device;
        }

        // HIDP_VALUE_CAPS is 72 bytes; read the few fields needed by offset rather than mirroring
        // its unions in a struct.
        private const int VALUE_CAPS_SIZE = 72;
        private const int OFFSET_USAGE_PAGE = 0, OFFSET_IS_RANGE = 12, OFFSET_BIT_SIZE = 18;
        private const int OFFSET_LOGICAL_MIN = 40, OFFSET_LOGICAL_MAX = 44, OFFSET_USAGE = 56;

        private static void LoadAxes(Device device, ushort valueCapsCount)
        {
            if (valueCapsCount == 0) return;
            ushort length = valueCapsCount;
            var buffer = Marshal.AllocHGlobal(VALUE_CAPS_SIZE * valueCapsCount);
            try
            {
                if (HidP_GetValueCaps(HIDP_INPUT, buffer, ref length, device.Preparsed) != HIDP_STATUS_SUCCESS) return;
                for (int i = 0; i < length; i++)
                {
                    var caps = buffer + i * VALUE_CAPS_SIZE;
                    if ((ushort)Marshal.ReadInt16(caps, OFFSET_USAGE_PAGE) != USAGE_PAGE_GENERIC) continue;
                    if (Marshal.ReadByte(caps, OFFSET_IS_RANGE) != 0) continue;
                    ushort usage = (ushort)Marshal.ReadInt16(caps, OFFSET_USAGE);
                    if (usage < USAGE_X || (usage > USAGE_RZ && usage != USAGE_HAT)) continue;

                    int minimum = Marshal.ReadInt32(caps, OFFSET_LOGICAL_MIN), maximum = Marshal.ReadInt32(caps, OFFSET_LOGICAL_MAX);
                    // Descriptors sometimes declare an unsigned range in too few bits (e.g. 0..255 as 0..-1).
                    if (maximum < minimum)
                    {
                        int bits = Marshal.ReadInt16(caps, OFFSET_BIT_SIZE);
                        maximum = bits >= 31 ? int.MaxValue : (1 << bits) - 1;
                    }
                    device.Axes[usage] = new AxisRange { Minimum = minimum, Maximum = maximum };
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private void Forget(IntPtr handle)
        {
            if (devices.TryGetValue(handle, out var device)) device?.Dispose();
            devices.Remove(handle);
        }

        public void Dispose()
        {
            if (IsRegistered) Register(RIDEV_REMOVE, IntPtr.Zero);
            foreach (var device in devices.Values) device?.Dispose();
            devices.Clear();
            DestroyHandle();
        }
    }
}
