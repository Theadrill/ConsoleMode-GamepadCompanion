using System;
using System.Diagnostics;
using ConsoleMode.GamepadCompanion.Core;
using ConsoleMode.GamepadCompanion.Core.Interfaces;
using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.Engine
{
    /// <summary>
    /// Converte o analógico direito em movimento de mouse e o R3 em botão direito.
    /// Acumula frações de pixel para manter movimentos lentos suaves.
    /// </summary>
    public sealed class MouseEmulationEngine : IDisposable
    {
        private readonly IGamepadService _gamepad;
        private readonly IInputSimulator _input;
        private readonly AppSettings _settings;
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        private long _lastTicks;
        private float _remainderX;
        private float _remainderY;
        private bool _rightButtonHeld;

        public MouseEmulationEngine(IGamepadService gamepad, IInputSimulator input, AppSettings settings)
        {
            _gamepad = gamepad;
            _input = input;
            _settings = settings;
            _lastTicks = _clock.ElapsedTicks;
            _gamepad.StateUpdated += OnStateUpdated;
        }

        private void OnStateUpdated(GamepadState state)
        {
            long now = _clock.ElapsedTicks;
            float dt = (now - _lastTicks) / (float)Stopwatch.Frequency;
            _lastTicks = now;
            dt = Math.Min(dt, 0.05f); // evita salto após travamentos

            if (!state.IsConnected || !_settings.MappingEnabled)
            {
                ReleaseAll();
                return;
            }

            UpdateRightButton(state.IsPressed(GamepadButtons.RightStick));
            UpdateMouseMove(state, dt);
        }

        private void UpdateMouseMove(GamepadState state, float dt)
        {
            MouseCurveCalculator.Calculate(
                state.RightThumbX, state.RightThumbY, _settings.MouseSensitivity, dt,
                GamepadDefaults.StickDeadzone, out float dx, out float dy);

            _remainderX += dx;
            _remainderY += dy;
            int moveX = (int)_remainderX;
            int moveY = (int)_remainderY;
            _remainderX -= moveX;
            _remainderY -= moveY;

            if (dx == 0f && dy == 0f)
            {
                _remainderX = 0f;
                _remainderY = 0f;
            }

            _input.MouseMoveRelative(moveX, moveY);
        }

        private void UpdateRightButton(bool pressed)
        {
            if (pressed && !_rightButtonHeld)
            {
                _input.MouseButtonDown(MouseButton.Right);
                _rightButtonHeld = true;
            }
            else if (!pressed && _rightButtonHeld)
            {
                _input.MouseButtonUp(MouseButton.Right);
                _rightButtonHeld = false;
            }
        }

        private void ReleaseAll()
        {
            UpdateRightButton(false);
            _remainderX = 0f;
            _remainderY = 0f;
        }

        public void Dispose()
        {
            _gamepad.StateUpdated -= OnStateUpdated;
            ReleaseAll();
        }
    }
}
