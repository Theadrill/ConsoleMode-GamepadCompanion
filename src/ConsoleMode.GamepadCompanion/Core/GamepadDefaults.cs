namespace ConsoleMode.GamepadCompanion.Core
{
    /// <summary>Constantes compartilhadas de comportamento do controle.</summary>
    public static class GamepadDefaults
    {
        /// <summary>Deadzone circular dos analógicos (fração do curso, 0-1).</summary>
        public const float StickDeadzone = 0.24f;

        /// <summary>Limiar (0-255) a partir do qual um gatilho conta como pressionado.</summary>
        public const byte TriggerThreshold = 50;
    }
}
