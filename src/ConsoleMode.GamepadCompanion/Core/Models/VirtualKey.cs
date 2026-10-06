namespace ConsoleMode.GamepadCompanion.Core.Models
{
    /// <summary>Teclas virtuais usadas pelos perfis (valores VK_ do Windows).</summary>
    public enum VirtualKey : ushort
    {
        Tab = 0x09,
        Space = 0x20,
        D0 = 0x30, D1 = 0x31, D2 = 0x32, D3 = 0x33, D4 = 0x34,
        D5 = 0x35, D6 = 0x36, D7 = 0x37, D8 = 0x38, D9 = 0x39,
        A = 0x41, D = 0x44, M = 0x4D, S = 0x53, W = 0x57,
        F9 = 0x78,
        F11 = 0x7A,
        LeftShift = 0xA0,
        LeftControl = 0xA2,
        LeftAlt = 0xA4
    }
}
