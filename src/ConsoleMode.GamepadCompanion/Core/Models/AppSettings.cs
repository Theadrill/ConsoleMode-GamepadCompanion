namespace ConsoleMode.GamepadCompanion.Core.Models
{
    /// <summary>Configurações em memória do Companion (persistência entra em fase futura).</summary>
    public sealed class AppSettings
    {
        /// <summary>Slot XInput (0-3) ou -1 para automático.</summary>
        public int SelectedSlot { get; set; } = -1;

        /// <summary>Sensibilidade do mouse no analógico direito (1-100).</summary>
        public int MouseSensitivity { get; set; } = 50;

        /// <summary>Mapeamento ativo (false = pausado).</summary>
        public bool MappingEnabled { get; set; } = true;

        /// <summary>Deadzone dos analógicos em % do curso (5-50).</summary>
        public int StickDeadzonePercent { get; set; } = 24;

        /// <summary>Limiar dos gatilhos LT/RT em % do curso (5-90).</summary>
        public int TriggerThresholdPercent { get; set; } = 20;

        /// <summary>Deadzone dos analógicos como fração (0-1).</summary>
        public float StickDeadzone => StickDeadzonePercent / 100f;

        /// <summary>Limiar dos gatilhos no valor bruto do XInput (0-255).</summary>
        public byte TriggerThreshold => (byte)(TriggerThresholdPercent * 255 / 100);
    }
}
