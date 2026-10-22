namespace ConsoleMode.GamepadCompanion.Core.Models
{
    /// <summary>
    /// Identifica os botões padrão de Gamepad para os quais os botões traseiros físicos (L4, L5, R4, R5)
    /// podem ser remapeados diretamente em nível de hardware / driver virtual (ViGEm).
    /// </summary>
    public enum SteamDeckHardwareRemap
    {
        None = 0,
        LeftShoulder = 1,   // LB / L1
        RightShoulder = 2,  // RB / R1
        LeftTrigger = 3,    // LT / L2
        RightTrigger = 4,   // RT / R2
        LeftStickClick = 5, // L3
        RightStickClick = 6,// R3
        A = 7,
        B = 8,
        X = 9,
        Y = 10,
        DPadUp = 11,
        DPadDown = 12,
        DPadLeft = 13,
        DPadRight = 14,
        Start = 15,
        Back = 16,
        Guide = 17
    }
}
