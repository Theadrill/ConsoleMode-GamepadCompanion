using System;
using ConsoleMode.GamepadCompanion.Core.Models;
using Microsoft.Win32;

namespace ConsoleMode.GamepadCompanion.Hardware
{
    /// <summary>
    /// Responsável por identificar se a aplicação está em execução em hardware Valve Steam Deck (LCD ou OLED).
    /// </summary>
    public static class SteamDeckDetector
    {
        private static SteamDeckInfo _cachedInfo;
        private static readonly object _syncRoot = new object();

        /// <summary>
        /// Obtém as informações do hardware atual diagnosticado no sistema.
        /// </summary>
        public static SteamDeckInfo Current
        {
            get
            {
                if (_cachedInfo == null)
                {
                    lock (_syncRoot)
                    {
                        if (_cachedInfo == null)
                        {
                            _cachedInfo = Detect();
                        }
                    }
                }
                return _cachedInfo;
            }
        }

        /// <summary>
        /// Indica se o hardware atual é um Valve Steam Deck.
        /// </summary>
        public static bool IsSteamDeck => Current.IsSteamDeck;

        /// <summary>
        /// Permite definir manualmente as informações de hardware (usado principalmente em testes unitários).
        /// </summary>
        public static void SetOverride(SteamDeckInfo info)
        {
            lock (_syncRoot)
            {
                _cachedInfo = info;
            }
        }

        /// <summary>
        /// Reseta o cache de detecção para forçar uma nova leitura.
        /// </summary>
        public static void ResetCache()
        {
            lock (_syncRoot)
            {
                _cachedInfo = null;
            }
        }

        /// <summary>
        /// Avalia a variante do Steam Deck com base nas strings de fabricante e produto.
        /// </summary>
        public static SteamDeckVariant DetectVariant(string manufacturer, string product)
        {
            if (string.IsNullOrEmpty(manufacturer) && string.IsNullOrEmpty(product))
            {
                return SteamDeckVariant.None;
            }

            string m = (manufacturer ?? string.Empty).Trim();
            string p = (product ?? string.Empty).Trim();

            bool isValve = m.IndexOf("Valve", StringComparison.OrdinalIgnoreCase) >= 0;

            if (p.IndexOf("Galileo", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return SteamDeckVariant.SteamDeckOled;
            }

            if (p.IndexOf("Jupiter", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return SteamDeckVariant.SteamDeckLcd;
            }

            if (isValve)
            {
                // Se o fabricante for Valve e o produto não especificar, assume variante LCD como padrão
                return SteamDeckVariant.SteamDeckLcd;
            }

            return SteamDeckVariant.None;
        }

        /// <summary>
        /// Executa o diagnóstico real lendo os dados do BIOS no Registro do Windows.
        /// </summary>
        public static SteamDeckInfo Detect()
        {
            string manufacturer = string.Empty;
            string product = string.Empty;
            string biosVersion = string.Empty;

            try
            {
                using (RegistryKey biosKey = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS"))
                {
                    if (biosKey != null)
                    {
                        manufacturer = biosKey.GetValue("SystemManufacturer") as string
                                       ?? biosKey.GetValue("BaseBoardManufacturer") as string
                                       ?? string.Empty;

                        product = biosKey.GetValue("SystemProductName") as string
                                  ?? biosKey.GetValue("BaseBoardProduct") as string
                                  ?? string.Empty;

                        biosVersion = biosKey.GetValue("BIOSVersion") as string ?? string.Empty;
                    }
                }
            }
            catch
            {
                // Fallback silencioso caso o registro esteja inacessível
            }

            SteamDeckVariant variant = DetectVariant(manufacturer, product);
            return new SteamDeckInfo(variant, manufacturer, product, biosVersion);
        }
    }
}
