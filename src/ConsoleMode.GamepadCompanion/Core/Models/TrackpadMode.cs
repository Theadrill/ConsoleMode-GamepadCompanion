namespace ConsoleMode.GamepadCompanion.Core.Models
{
    /// <summary>
    /// Modos funcionais de operação para um Trackpad do Steam Deck.
    /// </summary>
    public enum TrackpadMode
    {
        /// <summary>Trackpad desativado (sem processamento ou emulação).</summary>
        Disabled,

        /// <summary>Cursor do mouse com emulação de trackball física (aceleração, anti-jitter e inércia).</summary>
        MouseTrackball,

        /// <summary>Roda de rolagem contínua vertical/horizontal (com suporte a rolagem natural).</summary>
        Scrollwheel,

        /// <summary>4 botões direcionais angulares com deadzone central de descanso.</summary>
        DPad,

        /// <summary>Superfície atua como um botão mecânico extra com ações independentes de Toque e Clique.</summary>
        SingleButton
    }
}
