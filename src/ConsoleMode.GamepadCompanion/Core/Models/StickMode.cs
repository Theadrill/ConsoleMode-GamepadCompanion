namespace ConsoleMode.GamepadCompanion.Core.Models
{
    /// <summary>
    /// Modo de operação de uma alavanca analógica (Stick).
    /// </summary>
    public enum StickMode
    {
        /// <summary>Emite teclas direcionais (ex: WASD ou setas) com base na inclinação.</summary>
        DirectionalKeys,

        /// <summary>Emula movimentação de cursor ou rotação de câmera de mouse.</summary>
        MouseMove,

        /// <summary>Desativado (não emite comandos direcionais).</summary>
        Disabled
    }
}
