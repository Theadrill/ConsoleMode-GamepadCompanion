using System;

namespace ConsoleMode.GamepadCompanion.Core.Models
{
    [Flags]
    public enum SDCButton0 : ushort
    {
        BTN_L5 = 0b1000000000000000,
        BTN_OPTIONS = 0b0100000000000000, // Start
        BTN_STEAM = 0b0010000000000000,   // Guide
        BTN_MENU = 0b0001000000000000,    // Back
        BTN_DPAD_DOWN = 0b0000100000000000,
        BTN_DPAD_LEFT = 0b0000010000000000,
        BTN_DPAD_RIGHT = 0b0000001000000000,
        BTN_DPAD_UP = 0b0000000100000000,
        BTN_A = 0b0000000010000000,
        BTN_X = 0b0000000001000000,
        BTN_B = 0b0000000000100000,
        BTN_Y = 0b0000000000010000,
        BTN_L1 = 0b0000000000001000,
        BTN_R1 = 0b0000000000000100,
        BTN_L2 = 0b0000000000000010,
        BTN_R2 = 0b0000000000000001
    }

    [Flags]
    public enum SDCButton1 : byte
    {
        BTN_LSTICK_PRESS = 0b01000000,
        BTN_RPAD_TOUCH = 0b00010000,
        BTN_LPAD_TOUCH = 0b00001000,
        BTN_RPAD_PRESS = 0b00000100,
        BTN_LPAD_PRESS = 0b00000010,
        BTN_R5 = 0b00000001
    }

    [Flags]
    public enum SDCButton2 : byte
    {
        BTN_RSTICK_PRESS = 0b00000100
    }

    [Flags]
    public enum SDCButton4 : byte
    {
        BTN_LSTICK_TOUCH = 0b01000000,
        BTN_RSTICK_TOUCH = 0b10000000,
        BTN_R4 = 0b00000100,
        BTN_L4 = 0b00000010
    }

    [Flags]
    public enum SDCButton5 : byte
    {
        BTN_QUICK_ACCESS = 0b00000100 // '...'
    }

    /// <summary>
    /// Estado físico bruto decodificado do pacote USB HID de 64 bytes do Steam Deck.
    /// </summary>
    public struct SteamDeckRawState
    {
        public SDCButton0 Buttons0;
        public SDCButton1 Buttons1;
        public SDCButton2 Buttons2;
        public SDCButton4 Buttons4;
        public SDCButton5 Buttons5;

        public short LeftThumbX;
        public short LeftThumbY;
        public short RightThumbX;
        public short RightThumbY;
        public short LeftTrigger;
        public short RightTrigger;
        public short LPadX;
        public short LPadY;
        public short RPadX;
        public short RPadY;
        public short LPadPressure;
        public short RPadPressure;

        public bool IsA => (Buttons0 & SDCButton0.BTN_A) != 0;
        public bool IsB => (Buttons0 & SDCButton0.BTN_B) != 0;
        public bool IsX => (Buttons0 & SDCButton0.BTN_X) != 0;
        public bool IsY => (Buttons0 & SDCButton0.BTN_Y) != 0;
        public bool IsDPadUp => (Buttons0 & SDCButton0.BTN_DPAD_UP) != 0;
        public bool IsDPadDown => (Buttons0 & SDCButton0.BTN_DPAD_DOWN) != 0;
        public bool IsDPadLeft => (Buttons0 & SDCButton0.BTN_DPAD_LEFT) != 0;
        public bool IsDPadRight => (Buttons0 & SDCButton0.BTN_DPAD_RIGHT) != 0;
        public bool IsL1 => (Buttons0 & SDCButton0.BTN_L1) != 0;
        public bool IsR1 => (Buttons0 & SDCButton0.BTN_R1) != 0;
        public bool IsL2 => (Buttons0 & SDCButton0.BTN_L2) != 0;
        public bool IsR2 => (Buttons0 & SDCButton0.BTN_R2) != 0;
        public bool IsStart => (Buttons0 & SDCButton0.BTN_OPTIONS) != 0;
        public bool IsBack => (Buttons0 & SDCButton0.BTN_MENU) != 0;
        public bool IsSteam => (Buttons0 & SDCButton0.BTN_STEAM) != 0;
        public bool IsQuickAccess => (Buttons5 & SDCButton5.BTN_QUICK_ACCESS) != 0;
        public bool IsGuide => (Buttons0 & SDCButton0.BTN_STEAM) != 0;

        public bool IsL3 => (Buttons1 & SDCButton1.BTN_LSTICK_PRESS) != 0;
        public bool IsR3 => (Buttons2 & SDCButton2.BTN_RSTICK_PRESS) != 0;

        // Botões Traseiros
        public bool IsL4 => (Buttons4 & SDCButton4.BTN_L4) != 0;
        public bool IsL5 => (Buttons0 & SDCButton0.BTN_L5) != 0;
        public bool IsR4 => (Buttons4 & SDCButton4.BTN_R4) != 0;
        public bool IsR5 => (Buttons1 & SDCButton1.BTN_R5) != 0;

        // Touchpads
        public bool IsLPadTouch => (Buttons1 & SDCButton1.BTN_LPAD_TOUCH) != 0;
        public bool IsRPadTouch => (Buttons1 & SDCButton1.BTN_RPAD_TOUCH) != 0;
        public bool IsLPadPress => (Buttons1 & SDCButton1.BTN_LPAD_PRESS) != 0;
        public bool IsRPadPress => (Buttons1 & SDCButton1.BTN_RPAD_PRESS) != 0;

        /// <summary>
        /// Decodifica o pacote bruto de 64 bytes lido da interface HID da Valve.
        /// </summary>
        public static SteamDeckRawState Parse(byte[] buffer)
        {
            var state = new SteamDeckRawState();
            if (buffer == null || buffer.Length < 64)
            {
                return state;
            }

            // Offsets oficiais conforme firmware Neptune / steam-deck-tools
            // 0x08 - Buttons0 (ushort)
            state.Buttons0 = (SDCButton0)BitConverter.ToUInt16(buffer, 8);
            // 0x0A - Buttons1 (byte)
            state.Buttons1 = (SDCButton1)buffer[10];
            // 0x0B - Buttons2 (byte)
            state.Buttons2 = (SDCButton2)buffer[11];
            // 0x0D - Buttons4 (byte)
            state.Buttons4 = (SDCButton4)buffer[13];
            // 0x0E - Buttons5 (byte)
            state.Buttons5 = (SDCButton5)buffer[14];

            // Analógicos e Gatilhos
            state.LPadX = BitConverter.ToInt16(buffer, 0x10);
            state.LPadY = BitConverter.ToInt16(buffer, 0x12);
            state.RPadX = BitConverter.ToInt16(buffer, 0x14);
            state.RPadY = BitConverter.ToInt16(buffer, 0x16);

            state.LeftTrigger = BitConverter.ToInt16(buffer, 0x2C);
            state.RightTrigger = BitConverter.ToInt16(buffer, 0x2E);

            state.LeftThumbX = BitConverter.ToInt16(buffer, 0x30);
            state.LeftThumbY = BitConverter.ToInt16(buffer, 0x32);
            state.RightThumbX = BitConverter.ToInt16(buffer, 0x34);
            state.RightThumbY = BitConverter.ToInt16(buffer, 0x36);

            state.LPadPressure = BitConverter.ToInt16(buffer, 0x38);
            state.RPadPressure = BitConverter.ToInt16(buffer, 0x3A);

            return state;
        }

        /// <summary>
        /// Converte o estado bruto do Steam Deck para um snapshot GamepadState compatível com o Companion.
        /// </summary>
        public GamepadState ToGamepadState(uint packetNumber)
        {
            var buttons = GamepadButtons.None;

            if (IsDPadUp) buttons |= GamepadButtons.DPadUp;
            if (IsDPadDown) buttons |= GamepadButtons.DPadDown;
            if (IsDPadLeft) buttons |= GamepadButtons.DPadLeft;
            if (IsDPadRight) buttons |= GamepadButtons.DPadRight;
            if (IsA) buttons |= GamepadButtons.A;
            if (IsB) buttons |= GamepadButtons.B;
            if (IsX) buttons |= GamepadButtons.X;
            if (IsY) buttons |= GamepadButtons.Y;
            if (IsL1) buttons |= GamepadButtons.LeftShoulder;
            if (IsR1) buttons |= GamepadButtons.RightShoulder;
            if (IsStart) buttons |= GamepadButtons.Start;
            if (IsBack) buttons |= GamepadButtons.Back;
            if (IsGuide) buttons |= GamepadButtons.Guide;
            if (IsL3) buttons |= GamepadButtons.LeftStick;
            if (IsR3) buttons |= GamepadButtons.RightStick;

            byte lt = (byte)Math.Min(255, Math.Max(0, LeftTrigger / 128));
            byte rt = (byte)Math.Min(255, Math.Max(0, RightTrigger / 128));

            return new GamepadState(
                true,
                packetNumber,
                buttons,
                lt,
                rt,
                LeftThumbX,
                LeftThumbY,
                RightThumbX,
                RightThumbY
            );
        }
    }
}
