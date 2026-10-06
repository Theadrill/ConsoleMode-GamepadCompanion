using System;
using System.Runtime.InteropServices;

namespace ConsoleMode.GamepadCompanion.Hardware
{
    /// <summary>P/Invoke isolado do XInput. Nenhuma regra de negócio aqui.</summary>
    internal static class XInputNative
    {
        public const uint ErrorSuccess = 0;
        public const int MaxControllers = 4;

        [StructLayout(LayoutKind.Sequential)]
        public struct XInputGamepad
        {
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct XInputState
        {
            public uint dwPacketNumber;
            public XInputGamepad Gamepad;
        }

        // Ordinal 100 = XInputGetStateEx (expõe o botão Guide). Não documentado, porém estável.
        [DllImport("xinput1_4.dll", EntryPoint = "#100")]
        private static extern uint XInputGetStateEx14(uint dwUserIndex, out XInputState pState);

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern uint XInputGetState14(uint dwUserIndex, out XInputState pState);

        private static bool _exUnavailable;

        /// <summary>Lê o estado; usa a versão Ex (com Guide) e cai para a padrão se indisponível.</summary>
        public static uint GetState(uint userIndex, out XInputState state)
        {
            if (!_exUnavailable)
            {
                try
                {
                    return XInputGetStateEx14(userIndex, out state);
                }
                catch (EntryPointNotFoundException)
                {
                    _exUnavailable = true;
                }
            }

            return XInputGetState14(userIndex, out state);
        }
    }
}
