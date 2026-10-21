namespace ConsoleMode.GamepadCompanion.Core.Models
{
    /// <summary>
    /// Identifica os 14 botões e gatilhos físicos mapeáveis de um controle de jogo padrão.
    /// </summary>
    public enum ControllerButton
    {
        // Botões Faciais (ABXY)
        A,
        B,
        X,
        Y,

        // D-Pad
        DPadUp,
        DPadDown,
        DPadLeft,
        DPadRight,

        // Bumpers (Ombro)
        LeftShoulder,  // LB
        RightShoulder, // RB

        // Gatilhos Digitais
        LeftTrigger,   // LT
        RightTrigger,  // RT

        // Menus
        Back,          // Select / View
        Start,         // Start / Menu

        // Cliques dos Analógicos
        LeftStickClick,  // L3
        RightStickClick  // R3
    }
}
