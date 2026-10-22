using System;
using System.Diagnostics;
using System.Threading;
using ConsoleMode.GamepadCompanion.Core.Models;
using hidapi;

namespace ConsoleMode.GamepadCompanion.Hardware
{
    /// <summary>
    /// Serviço de captura direta USB HID do controle embutido do Steam Deck (Neptune)
    /// e despacho para o gamepad virtual de Xbox 360 no Windows.
    /// </summary>
    public sealed class SteamDeckControllerService : IDisposable
    {
        public const ushort VendorId = 0x28DE;
        public const ushort ProductId = 0x1205;
        private const int ReadTimeoutMs = 25;
        private const int LizardModeKeepaliveIntervalMs = 500;

        private HidDevice _device;
        private readonly VirtualXbox360Controller _virtualX360;
        private Thread _pollThread;
        private volatile bool _isRunning;
        private readonly object _syncRoot = new object();

        private DateTime _lastLizardUpdate = DateTime.MinValue;
        private SteamDeckRawState _lastRawState;
        private uint _packetCounter;

        public bool IsRunning => _isRunning;
        public SteamDeckRawState CurrentRawState => _lastRawState;

        public event Action<SteamDeckRawState> RawStateUpdated;

        public SteamDeckControllerService(VirtualXbox360Controller virtualX360 = null)
        {
            _virtualX360 = virtualX360 ?? new VirtualXbox360Controller();
        }

        public bool Start()
        {
            lock (_syncRoot)
            {
                if (_isRunning) return true;

                // Tenta abrir o dispositivo HID Neptune
                try
                {
                    _device = new HidDevice(VendorId, ProductId, 64);
                    if (!_device.OpenDevice())
                    {
                        using (_device) { }
                        _device = null;
                        return false;
                    }
                }
                catch
                {
                    using (_device) { }
                    _device = null;
                    return false;
                }

                // Desativa o Lizard Mode
                DisableLizardMode();

                // Conecta o Gamepad Virtual de Xbox 360 via ViGEmBus
                _virtualX360.Connect();

                _isRunning = true;
                _pollThread = new Thread(PollLoop)
                {
                    Name = "SteamDeckHidPollingThread",
                    IsBackground = true,
                    Priority = ThreadPriority.AboveNormal
                };
                _pollThread.Start();

                return true;
            }
        }

        public void Stop()
        {
            lock (_syncRoot)
            {
                if (!_isRunning) return;

                _isRunning = false;

                if (_pollThread != null && _pollThread.IsAlive)
                {
                    _pollThread.Join(500);
                    _pollThread = null;
                }

                // Restaura o Lizard Mode para o usuário não perder mouse e teclado no Windows
                EnableLizardMode();

                // Desconecta o controle virtual
                _virtualX360.Disconnect();

                if (_device != null)
                {
                    using (_device) { }
                    _device = null;
                }
            }
        }

        private void PollLoop()
        {
            var sw = Stopwatch.StartNew();

            while (_isRunning)
            {
                try
                {
                    if (_device == null || !_device.IsDeviceValid)
                    {
                        Thread.Sleep(250);
                        if (!_isRunning) break;
                        TryReopenDevice();
                        continue;
                    }

                    // Envia keepalive periódico para garantir que o Lizard Mode continue desligado
                    if ((DateTime.Now - _lastLizardUpdate).TotalMilliseconds > LizardModeKeepaliveIntervalMs)
                    {
                        DisableLizardMode();
                    }

                    byte[] data = _device.Read(ReadTimeoutMs);
                    if (data != null && data.Length >= 64)
                    {
                        _packetCounter++;
                        var raw = SteamDeckRawState.Parse(data);
                        _lastRawState = raw;

                        // Despacha para o controle virtual Xbox 360
                        _virtualX360.Submit(ref raw);

                        // Notifica ouvintes locais
                        RawStateUpdated?.Invoke(raw);
                    }
                }
                catch (HidDeviceInvalidException)
                {
                    TryReopenDevice();
                    Thread.Sleep(100);
                }
                catch (Exception)
                {
                    Thread.Sleep(20);
                }
            }
        }

        private void TryReopenDevice()
        {
            try
            {
                if (_device != null)
                {
                    using (_device) { }
                }

                _device = new HidDevice(VendorId, ProductId, 64);
                if (_device.OpenDevice())
                {
                    DisableLizardMode();
                }
                else
                {
                    using (_device) { }
                    _device = null;
                }
            }
            catch
            {
                using (_device) { }
                _device = null;
            }
        }

        private void DisableLizardMode()
        {
            if (_device == null) return;

            try
            {
                // Desativa emulação de mouse: { 0x87, 0x03, 0x08, 0x07 }
                _device.RequestFeatureReport(new byte[] { 0x87, 0x03, 0x08, 0x07 });
                // Desativa emulação de teclado/botões: { 0x81, 0x00 }
                _device.RequestFeatureReport(new byte[] { 0x81, 0x00 });

                _lastLizardUpdate = DateTime.Now;
            }
            catch { }
        }

        private void EnableLizardMode()
        {
            if (_device == null) return;

            try
            {
                // Reativa emulação de mouse: { 0x8e, 0x00 }
                _device.RequestFeatureReport(new byte[] { 0x8e, 0x00 });
                // Reativa emulação de botões/teclado: { 0x85, 0x00 }
                _device.RequestFeatureReport(new byte[] { 0x85, 0x00 });
            }
            catch { }
        }

        public void Dispose()
        {
            Stop();
            _virtualX360.Dispose();
        }
    }
}
