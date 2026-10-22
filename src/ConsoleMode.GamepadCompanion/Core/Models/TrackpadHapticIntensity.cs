namespace ConsoleMode.GamepadCompanion.Core.Models
{
    /// <summary>
    /// Intensidade dos pulsos hápticos do atuador linear do Trackpad do Steam Deck.
    /// </summary>
    public enum TrackpadHapticIntensity
    {
        /// <summary>Feedback tátil desligado.</summary>
        Off,

        /// <summary>Feedback suave padrão (Low) - micro-pulsos discretos.</summary>
        Low,

        /// <summary>Feedback médio (Medium).</summary>
        Medium,

        /// <summary>Feedback forte e nítido (High).</summary>
        High
    }
}
