using System;
using System.Diagnostics;
using System.Reflection;
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
        private const int LizardModeKeepaliveIntervalMs = 1000;

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

                    // Envia keepalive periódico leve a cada 1 segundo (1000ms) para manter o Lizard Mode desativado
                    // e alimentar o hardware watchdog do firmware Neptune sem engasgar o loop de leitura
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
                // 1. Desativa emulação de teclado/botões de firmware do Steam Deck:
                byte[] clearMappings = new byte[65];
                clearMappings[0] = 0x00;
                clearMappings[1] = 0x81; // ID_CLEAR_DIGITAL_MAPPINGS
                SteamDeckHidNative.SendFeatureReport(_device, clearMappings);

                // 2. Desativa mouse de hardware e configura modos oficiais Valve Neptune:
                // 3 registros x 3 bytes = 9 bytes de payload (0x09)
                byte[] setSettings = new byte[65];
                setSettings[0] = 0x00;
                setSettings[1] = 0x87; // ID_SET_SETTINGS_VALUES
                setSettings[2] = 9;    // 3 registros x 3 bytes
                
                // Reg 0: 0x18 (SETTING_DECK_RPAD_MARGIN) = 0
                setSettings[3] = 0x18;
                setSettings[4] = 0x00;
                setSettings[5] = 0x00;

                // Reg 1: 0x07 (SETTING_DECK_LPAD_MODE) = 7 (TRACKPAD_NONE -> Desativa mouse/scroll de firmware do pad esquerdo)
                setSettings[6] = 0x07;
                setSettings[7] = 0x07;
                setSettings[8] = 0x00;

                // Reg 2: 0x08 (SETTING_DECK_RPAD_MODE) = 7 (TRACKPAD_NONE -> Desativa mouse de firmware do pad direito)
                setSettings[9] = 0x08;
                setSettings[10] = 0x07;
                setSettings[11] = 0x00;

                // NOTA CRÍTICA: NÃO definir SETTING_DECK_LPAD_CLICK_PRESSURE (0x34) nem RPAD (0x35) para 0xFFFF!
                // Colocar 0xFFFF desativa os switches mecânicos físicos dos trackpads no firmware do Neptune,
                // impedindo que o hardware reporte BTN_LPAD_PRESS e que os atuadores LRA recebam comandos de vibração.

                SteamDeckHidNative.SendFeatureReport(_device, setSettings);
                _lastLizardUpdate = DateTime.Now;
            }
            catch { }
        }

        private void EnableLizardMode()
        {
            if (_device == null) return;

            try
            {
                byte[] loadDefaults = new byte[65];
                loadDefaults[0] = 0x00;
                loadDefaults[1] = 0x8E; // ID_LOAD_DEFAULT_SETTINGS
                SteamDeckHidNative.SendFeatureReport(_device, loadDefaults);

                byte[] defaultMappings = new byte[65];
                defaultMappings[0] = 0x00;
                defaultMappings[1] = 0x85; // ID_SET_DEFAULT_DIGITAL_MAPPINGS
                SteamDeckHidNative.SendFeatureReport(_device, defaultMappings);
            }
            catch { }
        }

        private static readonly Random _hapticRandom = new Random();

        /// <summary>
        /// Emite um pulso háptico tátil nos atuadores LRA do Steam Deck (Neptune)
        /// utilizando a estrutura nativa SDCHapticPacket2 do Steam Deck (0xEA).
        /// Referência técnica local: C:\PROJETOS\steam-deck-tools\SteamController\Devices\SteamControllerHaptic.cs
        /// </summary>
        public bool TriggerHaptic(bool isRightPad, TrackpadHapticIntensity intensity, bool isClick = false)
        {
            if (intensity == TrackpadHapticIntensity.Off) return false;
            if (_device == null || !_isRunning) return false;

            // No protocolo oficial do Steam Deck / Valve (SDCHapticPacket2 / Neptune):
            // Por razões legadas da Valve (conforme hid-steam.c e steam-deck-tools):
            // 0x01 aciona o Trackpad Esquerdo (Left)
            // 0x00 aciona o Trackpad Direito (Right)
            byte position = (byte)(isRightPad ? 0x00 : 0x01);

            byte style; // 1 = Weak (Ticks de rotação/scroll), 2 = Strong (Clique mecânico de press down/up)
            sbyte intensityVal; // -7..5 correspondente a -2dB..10dB

            if (isClick)
            {
                style = 2; // Strong
                switch (intensity)
                {
                    case TrackpadHapticIntensity.Low:
                        intensityVal = -3; // 2dB
                        break;
                    case TrackpadHapticIntensity.Medium:
                        intensityVal = 0;  // 5dB
                        break;
                    case TrackpadHapticIntensity.High:
                    default:
                        intensityVal = 3;  // 8dB
                        break;
                }
            }
            else
            {
                style = 1; // Weak
                switch (intensity)
                {
                    case TrackpadHapticIntensity.Low:
                        intensityVal = -7; // -2dB (textura super suave e sedosa)
                        break;
                    case TrackpadHapticIntensity.Medium:
                        intensityVal = -5; // 0dB
                        break;
                    case TrackpadHapticIntensity.High:
                    default:
                        intensityVal = -2; // 3dB
                        break;
                }
            }

            int ts;
            lock (_hapticRandom)
            {
                ts = _hapticRandom.Next();
            }

            // Protocolo nativo Neptune SDCHapticPacket2 (tamanho exato 13 bytes / 0x0D):
            // byte 0: 0x00 (Report ID para HIDAPI Windows)
            // byte 1: 0xEA (packet_type = 0xEA)
            // byte 2: 0x0D (len = 13 bytes)
            // byte 3: position (0x01 = Left, 0x00 = Right no hardware Neptune / Valve)
            // byte 4: style (1 = Weak, 2 = Strong)
            // byte 5: unsure2 = 0x00
            // byte 6: intensity (-7..5)
            // byte 7: unsure3 = 0x04
            // byte 8..11: tsA (timestamp aleatório para o hardware diferenciar disparos consecutivos)
            // byte 12..15: tsB (mesmo timestamp aleatório)
            byte[] packetEA = new byte[65];
            packetEA[0] = 0x00;
            packetEA[1] = 0xEA;
            packetEA[2] = 0x0D;
            packetEA[3] = position;
            packetEA[4] = style;
            packetEA[5] = 0x00;
            packetEA[6] = (byte)intensityVal;
            packetEA[7] = 0x04;
            packetEA[8] = (byte)(ts & 0xFF);
            packetEA[9] = (byte)((ts >> 8) & 0xFF);
            packetEA[10] = (byte)((ts >> 16) & 0xFF);
            packetEA[11] = (byte)((ts >> 24) & 0xFF);
            packetEA[12] = (byte)(ts & 0xFF);
            packetEA[13] = (byte)((ts >> 8) & 0xFF);
            packetEA[14] = (byte)((ts >> 16) & 0xFF);
            packetEA[15] = (byte)((ts >> 24) & 0xFF);

            int resEA = SteamDeckHidNative.SendFeatureReport(_device, packetEA);
            return resEA > 0;
        }

        public bool TriggerHapticNative(bool isRightPad, byte style, sbyte intensity)
        {
            var hapticIntensity = intensity switch
            {
                <= 0 => TrackpadHapticIntensity.Low,
                <= 2 => TrackpadHapticIntensity.Medium,
                _ => TrackpadHapticIntensity.High
            };
            return TriggerHaptic(isRightPad, hapticIntensity, isClick: true);
        }

        public bool TriggerHapticRaw(bool isRightPad, ushort amplitude, ushort period = 300, ushort count = 1)
        {
            if (_device == null || !_isRunning) return false;
            byte[] packet8F = new byte[65];
            packet8F[0] = 0x00;
            packet8F[1] = 0x8F;
            packet8F[2] = 0x07;
            packet8F[3] = (byte)(isRightPad ? 0x00 : 0x01); // 0x01 = Left Pad, 0x00 = Right Pad (convenção legada da Valve / Neptune)
            packet8F[4] = (byte)(amplitude & 0xFF);
            packet8F[5] = (byte)((amplitude >> 8) & 0xFF);
            packet8F[6] = (byte)(period & 0xFF);
            packet8F[7] = (byte)((period >> 8) & 0xFF);
            packet8F[8] = (byte)(count & 0xFF);
            packet8F[9] = (byte)((count >> 8) & 0xFF);
            int res = SteamDeckHidNative.SendFeatureReport(_device, packet8F);
            return res > 0;
        }

        public void Dispose()
        {
            Stop();
            _virtualX360.Dispose();
        }
    }

    /// <summary>
    /// Fornece envio direto de feature reports via P/Invoke de alta performance usando o delegate interno do hidapi.
    /// Evita o bloqueio e as exceções geradas pelo método RequestFeatureReport do wrapper gerenciado.
    /// </summary>
    internal static class SteamDeckHidNative
    {
        private static readonly FieldInfo DeviceHandleField =
            typeof(HidDevice).GetField("_deviceHandle", BindingFlags.Instance | BindingFlags.NonPublic);

        private delegate int HidSendFeatureReportDelegate(IntPtr device, byte[] data, uint length);
        private static readonly HidSendFeatureReportDelegate _sendFeatureReport;

        static SteamDeckHidNative()
        {
            try
            {
                var nativeType = typeof(HidDevice).Assembly.GetType("hidapi.Native.HidApiNative");
                if (nativeType != null)
                {
                    var method = nativeType.GetMethod("hid_send_feature_report", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (method != null)
                    {
                        _sendFeatureReport = (HidSendFeatureReportDelegate)Delegate.CreateDelegate(typeof(HidSendFeatureReportDelegate), method);
                    }
                }
            }
            catch { }
        }

        public static int SendFeatureReport(HidDevice device, byte[] data)
        {
            if (device == null || !device.IsDeviceValid || _sendFeatureReport == null || data == null) return -1;
            try
            {
                IntPtr handle = (IntPtr)DeviceHandleField.GetValue(device);
                if (handle == IntPtr.Zero) return -1;
                return _sendFeatureReport(handle, data, (uint)data.Length);
            }
            catch
            {
                return -1;
            }
        }
    }
}
