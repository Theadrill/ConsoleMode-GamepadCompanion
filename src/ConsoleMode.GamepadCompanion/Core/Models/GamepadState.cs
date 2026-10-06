namespace ConsoleMode.GamepadCompanion.Core.Models
{
    /// <summary>Snapshot imutável do estado do controle, desacoplado do XInput.</summary>
    public readonly struct GamepadState
    {
        public GamepadState(
            bool isConnected,
            uint packetNumber,
            GamepadButtons buttons,
            byte leftTrigger,
            byte rightTrigger,
            short leftThumbX,
            short leftThumbY,
            short rightThumbX,
            short rightThumbY)
        {
            IsConnected = isConnected;
            PacketNumber = packetNumber;
            Buttons = buttons;
            LeftTrigger = leftTrigger;
            RightTrigger = rightTrigger;
            LeftThumbX = leftThumbX;
            LeftThumbY = leftThumbY;
            RightThumbX = rightThumbX;
            RightThumbY = rightThumbY;
        }

        public bool IsConnected { get; }
        public uint PacketNumber { get; }
        public GamepadButtons Buttons { get; }
        public byte LeftTrigger { get; }
        public byte RightTrigger { get; }
        public short LeftThumbX { get; }
        public short LeftThumbY { get; }
        public short RightThumbX { get; }
        public short RightThumbY { get; }

        public static GamepadState Disconnected => new GamepadState(false, 0, GamepadButtons.None, 0, 0, 0, 0, 0, 0);

        public bool IsPressed(GamepadButtons button) => (Buttons & button) == button;
    }
}
