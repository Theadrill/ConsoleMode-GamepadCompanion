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
    public sealed class SteamDeckControllerService : IDisposable, ISteamDeckHaptics
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

        // Configurações de Remapeamento de Hardware para os 4 botões traseiros
        public SteamDeckHardwareRemap L4Remap { get; set; } = SteamDeckHardwareRemap.None;
        public SteamDeckHardwareRemap L5Remap { get; set; } = SteamDeckHardwareRemap.None;
        public SteamDeckHardwareRemap R4Remap { get; set; } = SteamDeckHardwareRemap.None;
        public SteamDeckHardwareRemap R5Remap { get; set; } = SteamDeckHardwareRemap.None;

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

                        // Aplica o remapeamento de hardware configurado
                        ApplyHardwareRemap(ref raw);

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

        public void ApplyHardwareRemap(ref SteamDeckRawState raw)
        {
            if (raw.IsL4 && L4Remap != SteamDeckHardwareRemap.None)
            {
                MapRemapTarget(ref raw, L4Remap);
            }

            if (raw.IsL5 && L5Remap != SteamDeckHardwareRemap.None)
            {
                MapRemapTarget(ref raw, L5Remap);
            }

            if (raw.IsR4 && R4Remap != SteamDeckHardwareRemap.None)
            {
                MapRemapTarget(ref raw, R4Remap);
            }

            if (raw.IsR5 && R5Remap != SteamDeckHardwareRemap.None)
            {
                MapRemapTarget(ref raw, R5Remap);
            }
        }

        private static void MapRemapTarget(ref SteamDeckRawState raw, SteamDeckHardwareRemap target)
        {
            switch (target)
            {
                case SteamDeckHardwareRemap.LeftShoulder:
                    raw.Buttons0 |= SDCButton0.BTN_L1;
                    break;
                case SteamDeckHardwareRemap.RightShoulder:
                    raw.Buttons0 |= SDCButton0.BTN_R1;
                    break;
                case SteamDeckHardwareRemap.A:
                    raw.Buttons0 |= SDCButton0.BTN_A;
                    break;
                case SteamDeckHardwareRemap.B:
                    raw.Buttons0 |= SDCButton0.BTN_B;
                    break;
                case SteamDeckHardwareRemap.X:
                    raw.Buttons0 |= SDCButton0.BTN_X;
                    break;
                case SteamDeckHardwareRemap.Y:
                    raw.Buttons0 |= SDCButton0.BTN_Y;
                    break;
                case SteamDeckHardwareRemap.LeftTrigger:
                    raw.Buttons0 |= SDCButton0.BTN_L2;
                    raw.LeftTrigger = 32767;
                    break;
                case SteamDeckHardwareRemap.RightTrigger:
                    raw.Buttons0 |= SDCButton0.BTN_R2;
                    raw.RightTrigger = 32767;
                    break;
                case SteamDeckHardwareRemap.LeftStickClick:
                    raw.Buttons1 |= SDCButton1.BTN_LSTICK_PRESS;
                    break;
                case SteamDeckHardwareRemap.RightStickClick:
                    raw.Buttons2 |= SDCButton2.BTN_RSTICK_PRESS;
                    break;
                case SteamDeckHardwareRemap.DPadUp:
                    raw.Buttons0 |= SDCButton0.BTN_DPAD_UP;
                    break;
                case SteamDeckHardwareRemap.DPadDown:
                    raw.Buttons0 |= SDCButton0.BTN_DPAD_DOWN;
                    break;
                case SteamDeckHardwareRemap.DPadLeft:
                    raw.Buttons0 |= SDCButton0.BTN_DPAD_LEFT;
                    break;
                case SteamDeckHardwareRemap.DPadRight:
                    raw.Buttons0 |= SDCButton0.BTN_DPAD_RIGHT;
                    break;
                case SteamDeckHardwareRemap.Start:
                    raw.Buttons0 |= SDCButton0.BTN_OPTIONS;
                    break;
                case SteamDeckHardwareRemap.Back:
                    raw.Buttons0 |= SDCButton0.BTN_MENU;
                    break;
                case SteamDeckHardwareRemap.Guide:
                    raw.Buttons0 |= SDCButton0.BTN_STEAM;
                    break;
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

        /// <summary>
        /// Emite um pulso háptico tátil para o trackpad com a intensidade informada.
        /// </summary>
        public bool TriggerHaptic(bool isRightPad, TrackpadHapticIntensity intensity)
        {
            if (intensity == TrackpadHapticIntensity.Off) return false;
            ushort amp = 400;
            ushort period = 300;
            switch (intensity)
            {
                case TrackpadHapticIntensity.Low:
                    amp = 600;
                    period = 300;
                    break;
                case TrackpadHapticIntensity.Medium:
                    amp = 1200;
                    period = 350;
                    break;
                case TrackpadHapticIntensity.High:
                    amp = 2000;
                    period = 400;
                    break;
            }
            return TriggerHapticRaw(isRightPad, amp, period, 1);
        }

        /// <summary>
        /// Envia pacote de feature report nativo da Valve (0x8f PT_FEEDBACK) para o hardware do Steam Deck.
        /// </summary>
        public bool TriggerHapticRaw(bool isRightPad, ushort amplitude, ushort period = 300, ushort count = 1)
        {
            lock (_syncRoot)
            {
                if (_device == null || !_isRunning) return false;

                try
                {
                    byte[] packet = new byte[9];
                    packet[0] = 0x8f; // PT_FEEDBACK
                    packet[1] = 0x07; // PL_FEEDBACK
                    packet[2] = (byte)(isRightPad ? 1 : 0);
                    packet[3] = (byte)(amplitude & 0xFF);
                    packet[4] = (byte)((amplitude >> 8) & 0xFF);
                    packet[5] = (byte)(period & 0xFF);
                    packet[6] = (byte)((period >> 8) & 0xFF);
                    packet[7] = (byte)(count & 0xFF);
                    packet[8] = (byte)((count >> 8) & 0xFF);

                    _device.RequestFeatureReport(packet);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public void Dispose()
        {
            Stop();
            _virtualX360.Dispose();
        }
    }
}
