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
    }
}
