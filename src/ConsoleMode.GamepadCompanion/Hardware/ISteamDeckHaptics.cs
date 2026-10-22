using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.Hardware
{
    /// <summary>
    /// Contrato desacoplado para emissão de feedback háptico nos atuadores lineares do Steam Deck.
    /// Permite injeção de dependência e mock 100% testável sem hardware USB.
    /// </summary>
    public interface ISteamDeckHaptics
    {
        /// <summary>
        /// Dispara um pulso háptico no trackpad especificado com a intensidade configurada.
        /// </summary>
        /// <param name="isRightPad">true para o Trackpad Direito; false para o Trackpad Esquerdo.</param>
        /// <param name="intensity">Nível de intensidade (Off, Low, Medium, High).</param>
        /// <returns>true se o relatório foi enviado ao hardware com sucesso; false caso contrário.</returns>
        bool TriggerHaptic(bool isRightPad, TrackpadHapticIntensity intensity);
    }
}
