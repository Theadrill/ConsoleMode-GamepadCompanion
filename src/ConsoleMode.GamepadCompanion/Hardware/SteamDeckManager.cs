using System;
using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.Hardware
{
    /// <summary>
    /// Ponto central de gerenciamento do ciclo de vida da emulação de controle do Steam Deck.
    /// </summary>
    public static class SteamDeckManager
    {
        private static SteamDeckControllerService _service;
        private static readonly object _syncRoot = new object();

        public static SteamDeckControllerService ActiveService => _service;
        public static bool IsActive => _service != null && _service.IsRunning;

        /// <summary>
        /// Inicia o serviço de captura HID e despacho para o gamepad virtual Xbox 360 se estiver em um Steam Deck com o driver ViGEm ativo.
        /// </summary>
        public static bool TryStart(AppSettings settings = null)
        {
            lock (_syncRoot)
            {
                if (_service != null && _service.IsRunning)
                {
                    if (settings != null) ApplySettings(settings);
                    return true;
                }

                if (!SteamDeckDetector.IsSteamDeck) return false;

                if (!ViGEmBusService.Instance.IsDriverInstalledAndActive) return false;

                try
                {
                    _service = new SteamDeckControllerService();
                    if (settings != null)
                    {
                        _service.L4Remap = settings.L4HardwareRemap;
                        _service.L5Remap = settings.L5HardwareRemap;
                        _service.R4Remap = settings.R4HardwareRemap;
                        _service.R5Remap = settings.R5HardwareRemap;
                    }

                    if (_service.Start())
                    {
                        return true;
                    }

                    _service.Dispose();
                    _service = null;
                    return false;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SteamDeckManager] Falha ao iniciar: {ex}");
                    if (_service != null)
                    {
                        try { _service.Dispose(); } catch { }
                        _service = null;
                    }
                    return false;
                }
            }
        }

        /// <summary>
        /// Atualiza as configurações de hardware (como remapeamento L4/L5/R4/R5) no serviço em execução.
        /// </summary>
        public static void ApplySettings(AppSettings settings)
        {
            lock (_syncRoot)
            {
                if (_service != null && settings != null)
                {
                    _service.L4Remap = settings.L4HardwareRemap;
                    _service.L5Remap = settings.L5HardwareRemap;
                    _service.R4Remap = settings.R4HardwareRemap;
                    _service.R5Remap = settings.R5HardwareRemap;
                }
            }
        }

        /// <summary>
        /// Encerra o serviço, desconecta o controle virtual e restaura o Lizard Mode (teclado/mouse) no Steam Deck.
        /// </summary>
        public static void Stop()
        {
            lock (_syncRoot)
            {
                if (_service != null)
                {
                    try { _service.Dispose(); } catch { }
                    _service = null;
                }
            }
        }
    }
}
