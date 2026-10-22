namespace ConsoleMode.GamepadCompanion.Core.Models
{
    /// <summary>
    /// Variantes conhecidas de hardware do Valve Steam Deck.
    /// </summary>
    public enum SteamDeckVariant
    {
        None = 0,
        SteamDeckLcd = 1,
        SteamDeckOled = 2
    }

    /// <summary>
    /// Estado do driver de gamepad virtual ViGEmBus no sistema operacional.
    /// </summary>
    public enum ViGEmBusStatus
    {
        NotInstalled = 0,
        Active = 1,
        ServiceStopped = 2,
        AccessDenied = 3,
        Error = 4
    }

    /// <summary>
    /// Informações diagnosticadas do hardware em execução.
    /// </summary>
    public sealed class SteamDeckInfo
    {
        public SteamDeckVariant Variant { get; }
        public string Manufacturer { get; }
        public string Product { get; }
        public string BiosVersion { get; }

        public bool IsSteamDeck => Variant != SteamDeckVariant.None;

        public string DisplayName
        {
            get
            {
                switch (Variant)
                {
                    case SteamDeckVariant.SteamDeckLcd:
                        return "Steam Deck (LCD - Jupiter)";
                    case SteamDeckVariant.SteamDeckOled:
                        return "Steam Deck (OLED - Galileo)";
                    default:
                        return "Computador Padrão (PC)";
                }
            }
        }

        public SteamDeckInfo(SteamDeckVariant variant, string manufacturer, string product, string biosVersion = null)
        {
            Variant = variant;
            Manufacturer = manufacturer ?? string.Empty;
            Product = product ?? string.Empty;
            BiosVersion = biosVersion ?? string.Empty;
        }

        public static SteamDeckInfo CreateStandardPc(string manufacturer = null, string product = null)
        {
            return new SteamDeckInfo(SteamDeckVariant.None, manufacturer, product);
        }
    }
}
