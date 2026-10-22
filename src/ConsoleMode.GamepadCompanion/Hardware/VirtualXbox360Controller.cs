using System;
using ConsoleMode.GamepadCompanion.Core.Models;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace ConsoleMode.GamepadCompanion.Hardware
{
    /// <summary>
    /// Gerencia um gamepad virtual de Xbox 360 no Windows através do driver ViGEmBus.
    /// </summary>
    public sealed class VirtualXbox360Controller : IDisposable
    {
        private ViGEmClient _client;
        private IXbox360Controller _controller;
        private bool _isConnected;
        private readonly object _syncRoot = new object();

        public bool IsConnected => _isConnected;

        public bool Connect()
        {
            lock (_syncRoot)
            {
                if (_isConnected) return true;

                try
                {
                    _client = new ViGEmClient();
                    _controller = _client.CreateXbox360Controller();
                    _controller.AutoSubmitReport = false;
                    _controller.Connect();
                    _isConnected = true;
                    return true;
                }
                catch
                {
                    Disconnect();
                    return false;
                }
            }
        }

        public void Disconnect()
        {
            lock (_syncRoot)
            {
                if (_controller != null)
                {
                    try { _controller.Disconnect(); } catch { }
                    _controller = null;
                }

                if (_client != null)
                {
                    try { _client.Dispose(); } catch { }
                    _client = null;
                }

                _isConnected = false;
            }
        }

        public void Submit(ref SteamDeckRawState state)
        {
            lock (_syncRoot)
            {
                if (!_isConnected || _controller == null) return;

                try
                {
                    // Botões Faciais e Direcionais
                    _controller.SetButtonState(Xbox360Button.A, state.IsA);
                    _controller.SetButtonState(Xbox360Button.B, state.IsB);
                    _controller.SetButtonState(Xbox360Button.X, state.IsX);
                    _controller.SetButtonState(Xbox360Button.Y, state.IsY);

                    _controller.SetButtonState(Xbox360Button.Up, state.IsDPadUp);
                    _controller.SetButtonState(Xbox360Button.Down, state.IsDPadDown);
                    _controller.SetButtonState(Xbox360Button.Left, state.IsDPadLeft);
                    _controller.SetButtonState(Xbox360Button.Right, state.IsDPadRight);

                    // Bumpers e Cliques dos Sticks
                    _controller.SetButtonState(Xbox360Button.LeftShoulder, state.IsL1);
                    _controller.SetButtonState(Xbox360Button.RightShoulder, state.IsR1);
                    _controller.SetButtonState(Xbox360Button.LeftThumb, state.IsL3);
                    _controller.SetButtonState(Xbox360Button.RightThumb, state.IsR3);

                    // Menus e Guide
                    _controller.SetButtonState(Xbox360Button.Start, state.IsStart);
                    _controller.SetButtonState(Xbox360Button.Back, state.IsBack);
                    _controller.SetButtonState(Xbox360Button.Guide, state.IsGuide);

                    // Gatilhos Analógicos (0 a 255)
                    byte lt = (byte)Math.Min(255, Math.Max(0, state.LeftTrigger / 128));
                    byte rt = (byte)Math.Min(255, Math.Max(0, state.RightTrigger / 128));
                    _controller.SetSliderValue(Xbox360Slider.LeftTrigger, lt);
                    _controller.SetSliderValue(Xbox360Slider.RightTrigger, rt);

                    // Eixos Analógicos (-32768 a 32767)
                    _controller.SetAxisValue(Xbox360Axis.LeftThumbX, state.LeftThumbX);
                    _controller.SetAxisValue(Xbox360Axis.LeftThumbY, state.LeftThumbY);
                    _controller.SetAxisValue(Xbox360Axis.RightThumbX, state.RightThumbX);
                    _controller.SetAxisValue(Xbox360Axis.RightThumbY, state.RightThumbY);

                    _controller.SubmitReport();
                }
                catch
                {
                    // Falha silenciosa de transporte; reconecta no próximo ciclo se necessário
                }
            }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
