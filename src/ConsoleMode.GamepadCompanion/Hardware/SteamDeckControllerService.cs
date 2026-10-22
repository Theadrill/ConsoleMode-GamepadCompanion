using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.Engine;
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

        private HidDevice _device;
        private readonly VirtualXbox360Controller _virtualX360;
        private Thread _pollThread;
        private volatile bool _isRunning;
        private readonly object _syncRoot = new object();

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

                    byte[] data = _device.Read(ReadTimeoutMs);
                    if (data != null && data.Length >= 64)
                    {
                        _packetCounter++;

                        // Keepalive periódico (a cada ~1 segundo): garante que o firmware Neptune
                        // nunca reverta silenciosamente para o Lizard Mode por watchdog interno
                        if (_packetCounter % 250 == 0)
                        {
                            DisableLizardMode();
                        }

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
                // Protocolo oficial Valve Neptune / Linux hid-steam:
                // ID_SET_SETTINGS_VALUES (0x87), payload de 9 bytes (3 configurações de 3 bytes cada: reg, valLo, valHi):
                // 1. SETTING_LEFT_TRACKPAD_MODE (0x07) = TRACKPAD_NONE (0x0007) -> Desativa mouse/scroll de hardware no trackpad esquerdo
                // 2. SETTING_RIGHT_TRACKPAD_MODE (0x08) = TRACKPAD_NONE (0x0007) -> Desativa mouse de hardware no trackpad direito
                // 3. SETTING_STEAM_WATCHDOG_ENABLE (0x46) = 0x0000 -> Desativa o watchdog interno do firmware que força volta ao Lizard Mode
                _device.RequestFeatureReport(new byte[] {
                    0x87, 0x09,
                    0x07, 0x07, 0x00,
                    0x08, 0x07, 0x00,
                    0x46, 0x00, 0x00
                });

                // ID_CLEAR_DIGITAL_MAPPINGS (0x81): Desativa emulação de teclado/botões do firmware
                _device.RequestFeatureReport(new byte[] { 0x81, 0x00 });
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

        [StructLayout(LayoutKind.Sequential)]
        private struct SDCHapticPacket2
        {
            public byte packet_type; // 0xea
            public byte len;         // 0xd
            public byte position;    // 0 = Left, 1 = Right
            public byte style;       // 1 = Weak, 2 = Strong
            public byte unsure2;     // 0x0
            public sbyte intensity;  // -7..5 => -2dB..10dB (5 = Max, +10dB)
            public byte unsure3;     // 0x4
            public int tsA;
            public int tsB;
        }

        private static int _hapticTsSeed = 1000;
        private Task _lastLeftHapticTask;
        private Task _lastRightHapticTask;

        /// <summary>
        /// Emite um pulso háptico tátil para o trackpad com a intensidade informada.
        /// Utiliza o protocolo nativo 0xEA da Valve para acionamento direto dos LRAs do Steam Deck,
        /// parametrizado pelos valores de calibração fina de TrackpadTuning.
        /// </summary>
        public bool TriggerHaptic(bool isRightPad, TrackpadHapticIntensity intensity)
        {
            if (intensity == TrackpadHapticIntensity.Off) return false;

            byte style = TrackpadTuning.HapticStyleHigh;
            sbyte intensityVal = TrackpadTuning.HapticIntensityHigh;

            switch (intensity)
            {
                case TrackpadHapticIntensity.Low:
                    style = TrackpadTuning.HapticStyleLow;
                    intensityVal = TrackpadTuning.HapticIntensityLow;
                    break;
                case TrackpadHapticIntensity.Medium:
                    style = TrackpadTuning.HapticStyleMedium;
                    intensityVal = TrackpadTuning.HapticIntensityMedium;
                    break;
                case TrackpadHapticIntensity.High:
                    style = TrackpadTuning.HapticStyleHigh;
                    intensityVal = TrackpadTuning.HapticIntensityHigh;
                    break;
            }

            return TriggerHapticNative(isRightPad, style, intensityVal);
        }

        /// <summary>
        /// Envia o relatório de feature nativo do Steam Deck (0xEA) para os atuadores LRA lineares dos trackpads.
        /// </summary>
        public bool TriggerHapticNative(bool isRightPad, byte style, sbyte intensity)
        {
            lock (_syncRoot)
            {
                if (_device == null || !_isRunning) return false;

                try
                {
                    ref Task lastTask = ref (isRightPad ? ref _lastRightHapticTask : ref _lastLeftHapticTask);
                    if (lastTask != null && !lastTask.IsCompleted)
                    {
                        // Evita congelamento se a requisição USB anterior para este pad ainda estiver em trânsito
                        return false;
                    }

                    int ts = Interlocked.Increment(ref _hapticTsSeed);
                    var haptic = new SDCHapticPacket2
                    {
                        packet_type = 0xea,
                        len = 0x0d,
                        position = (byte)(isRightPad ? 1 : 0),
                        style = style,
                        unsure2 = 0x00,
                        intensity = intensity,
                        unsure3 = 0x04,
                        tsA = ts,
                        tsB = ts
                    };

                    byte[] bytes = new byte[Marshal.SizeOf<SDCHapticPacket2>()];
                    var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                    try
                    {
                        Marshal.StructureToPtr(haptic, handle.AddrOfPinnedObject(), false);
                        lastTask = _device.RequestFeatureReportAsync(bytes);
                    }
                    finally
                    {
                        handle.Free();
                    }

                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Envia pacote de feature report legado da Valve (0x8f PT_FEEDBACK) como compatibilidade de rumble.
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
