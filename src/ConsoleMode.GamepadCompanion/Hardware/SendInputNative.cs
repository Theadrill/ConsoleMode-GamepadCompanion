using System;
using System.Runtime.InteropServices;

namespace ConsoleMode.GamepadCompanion.Hardware
{
    /// <summary>P/Invoke isolado de SendInput (user32). Sem regra de negócio.</summary>
    internal static class SendInputNative
    {
        public const uint InputMouse = 0;
        public const uint InputKeyboard = 1;

        public const uint MouseEventMove = 0x0001;
        public const uint MouseEventLeftDown = 0x0002;
        public const uint MouseEventLeftUp = 0x0004;
        public const uint MouseEventRightDown = 0x0008;
        public const uint MouseEventRightUp = 0x0010;
        public const uint MouseEventMiddleDown = 0x0020;
        public const uint MouseEventMiddleUp = 0x0040;
        public const uint MouseEventWheel = 0x0800;

        public const uint KeyEventExtendedKey = 0x0001;
        public const uint KeyEventKeyUp = 0x0002;
        public const uint KeyEventScanCode = 0x0008;

        private const uint MapVkToVsc = 0;
        private const uint MapVkToVscEx = 4;

        [StructLayout(LayoutKind.Sequential)]
        public struct MouseInput
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KeyboardInput
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HardwareInput
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)] public MouseInput mi;
            [FieldOffset(0)] public KeyboardInput ki;
            [FieldOffset(0)] public HardwareInput hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Input
        {
            public uint type;
            public InputUnion u;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        public static uint Send(Input input)
        {
            return SendInput(1, new[] { input }, Marshal.SizeOf(typeof(Input)));
        }

        public static ushort ScanCodeFor(ushort virtualKey)
        {
            uint mapped = MapVirtualKey(virtualKey, MapVkToVscEx);
            if (mapped == 0)
            {
                mapped = MapVirtualKey(virtualKey, MapVkToVsc);
            }
            return (ushort)(mapped & 0xFF);
        }

        public static bool IsExtendedKey(ushort virtualKey)
        {
            uint mappedEx = MapVirtualKey(virtualKey, MapVkToVscEx);
            if ((mappedEx & 0xFF00) == 0xE000 || (mappedEx & 0xFF00) == 0xE100)
            {
                return true;
            }

            switch (virtualKey)
            {
                case 0x5B: // VK_LWIN
                case 0x5C: // VK_RWIN
                case 0x5D: // VK_APPS
                case 0xA3: // VK_RCONTROL
                case 0xA5: // VK_RMENU (Right Alt)
                case 0x21: // VK_PRIOR (PageUp)
                case 0x22: // VK_NEXT (PageDown)
                case 0x23: // VK_END
                case 0x24: // VK_HOME
                case 0x25: // VK_LEFT
                case 0x26: // VK_UP
                case 0x27: // VK_RIGHT
                case 0x28: // VK_DOWN
                case 0x2C: // VK_SNAPSHOT (PrintScreen)
                case 0x2D: // VK_INSERT
                case 0x2E: // VK_DELETE
                case 0x90: // VK_NUMLOCK
                case 0xAD: // VK_VOLUME_MUTE
                case 0xAE: // VK_VOLUME_DOWN
                case 0xAF: // VK_VOLUME_UP
                case 0xB0: // VK_MEDIA_NEXT_TRACK
                case 0xB1: // VK_MEDIA_PREV_TRACK
                case 0xB2: // VK_MEDIA_STOP
                case 0xB3: // VK_MEDIA_PLAY_PAUSE
                    return true;
                default:
                    return false;
            }
        }
    }
}
