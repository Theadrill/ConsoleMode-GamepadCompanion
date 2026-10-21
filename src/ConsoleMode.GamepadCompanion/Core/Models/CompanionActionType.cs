namespace ConsoleMode.GamepadCompanion.Core.Models
{
    /// <summary>
    /// Ações especiais de sistema e funções exclusivas do Companion.
    /// </summary>
    public enum CompanionActionType
    {
        None = 0,

        // Funções do Companion
        CenterCursor,
        ToggleClipCursor,

        // Funções de Janelas e Atalhos de Sistema
        AltTab,
        AltF4,
        AltEnter,
        ShowDesktop
    }
}
