using System;
using System.Diagnostics;
using ConsoleMode.GamepadCompanion.Core.Interfaces;
using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.Engine
{
    /// <summary>
    /// Gerencia a execução do perfil ativo e o cálculo de tempo delta.
    /// Desacoplado da UI e do SO.
    /// </summary>
    public sealed class ProfileEngine : IDisposable
    {
        private readonly IGamepadService _gamepad;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private long _lastTicks;
        private IProfile _currentProfile;

        public ProfileEngine(IGamepadService gamepad, IProfile initialProfile)
        {
            _gamepad = gamepad ?? throw new ArgumentNullException(nameof(gamepad));
            _currentProfile = initialProfile ?? throw new ArgumentNullException(nameof(initialProfile));
            _lastTicks = _clock.ElapsedTicks;
            _gamepad.StateUpdated += OnStateUpdated;
        }

        public IProfile CurrentProfile
        {
            get => _currentProfile;
            set
            {
                if (_currentProfile == value) return;
                _currentProfile?.Reset();
                _currentProfile = value;
            }
        }

        private void OnStateUpdated(GamepadState state)
        {
            long now = _clock.ElapsedTicks;
            float dt = (now - _lastTicks) / (float)Stopwatch.Frequency;
            _lastTicks = now;
            dt = Math.Min(dt, 0.05f); // Evita saltos de tempo após travamento ou suspensão

            _currentProfile?.Update(state, dt);
        }

        public void Dispose()
        {
            _gamepad.StateUpdated -= OnStateUpdated;
            _currentProfile?.Reset();
        }
    }
}
