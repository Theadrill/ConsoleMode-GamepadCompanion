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

        public const uint KeyEventKeyUp = 0x0002;
        public const uint KeyEventScanCode = 0x0008;

        private const uint MapVkToVsc = 0;

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
            return (ushort)MapVirtualKey(virtualKey, MapVkToVsc);
        }
    }
}
