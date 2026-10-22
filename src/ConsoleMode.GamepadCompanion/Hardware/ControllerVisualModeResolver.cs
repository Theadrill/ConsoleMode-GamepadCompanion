using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.Hardware
{
    /// <summary>
    /// Determina dinamicamente o modo de representação visual do controle
    /// com base na presença do hardware Steam Deck, status do serviço e slot ativo.
    /// </summary>
    public static class ControllerVisualModeResolver
    {
        public static ControllerVisualMode Resolve(int selectedSlot = -1)
        {
            if (SteamDeckDetector.IsSteamDeck && SteamDeckManager.IsActive && selectedSlot <= 0)
            {
                return ControllerVisualMode.HandheldSteamDeck;
            }

            return ControllerVisualMode.StandardGamepad;
        }
    }
}
