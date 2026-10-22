using System;
using ConsoleMode.GamepadCompanion.Core.Models;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Exceptions;

namespace ConsoleMode.GamepadCompanion.Hardware
{
    /// <summary>
    /// Contrato desacoplado para checagem e diagnóstico do driver ViGEmBus.
    /// </summary>
    public interface IViGEmBusService
    {
        ViGEmBusStatus CheckStatus();
        bool IsDriverInstalledAndActive { get; }
    }

    /// <summary>
    /// Serviço de verificação do driver de kernel ViGEmBus (Virtual Gamepad Emulation Bus).
    /// </summary>
    public class ViGEmBusService : IViGEmBusService
    {
        private static IViGEmBusService _instance;
        private static readonly object _syncRoot = new object();

        public static IViGEmBusService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_syncRoot)
                    {
                        if (_instance == null)
                        {
                            _instance = new ViGEmBusService();
                        }
                    }
                }
                return _instance;
            }
            set
            {
                lock (_syncRoot)
                {
                    _instance = value;
                }
            }
        }

        public bool IsDriverInstalledAndActive => CheckStatus() == ViGEmBusStatus.Active;

        /// <summary>
        /// Realiza o teste de inicialização do canal de comunicação do ViGEmBus com o kernel.
        /// </summary>
        public virtual ViGEmBusStatus CheckStatus()
        {
            try
            {
                using (var client = new ViGEmClient())
                {
                    return ViGEmBusStatus.Active;
                }
            }
            catch (VigemBusNotFoundException)
            {
                return ViGEmBusStatus.NotInstalled;
            }
            catch (VigemBusAccessFailedException)
            {
                return ViGEmBusStatus.AccessDenied;
            }
            catch (VigemBusVersionMismatchException)
            {
                return ViGEmBusStatus.Error;
            }
            catch (Exception)
            {
                return ViGEmBusStatus.Error;
            }
        }
    }
}
