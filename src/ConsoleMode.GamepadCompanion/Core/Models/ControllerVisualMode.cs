namespace ConsoleMode.GamepadCompanion.Core.Models
{
    /// <summary>
    /// Define o estilo visual de representação do controle na UI (Visual Debugger e Editor de Perfis).
    /// </summary>
    public enum ControllerVisualMode
    {
        /// <summary>Controle convencional padrão (Xbox / XInput clássico).</summary>
        StandardGamepad,

        /// <summary>Console portátil estilo Steam Deck (chassis com tela central, dual trackpads e botões traseiros L4/L5/R5/R4).</summary>
        HandheldSteamDeck
    }
}
