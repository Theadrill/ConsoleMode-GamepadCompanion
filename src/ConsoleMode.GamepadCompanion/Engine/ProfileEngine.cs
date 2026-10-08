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

        private readonly object _profileLock = new object();

        public ProfileEngine(IGamepadService gamepad, IProfile initialProfile)
        {
            _gamepad = gamepad ?? throw new ArgumentNullException(nameof(gamepad));
            _currentProfile = initialProfile ?? throw new ArgumentNullException(nameof(initialProfile));
            _lastTicks = _clock.ElapsedTicks;
            _gamepad.StateUpdated += OnStateUpdated;
        }

        public IProfile CurrentProfile
        {
            get
            {
                lock (_profileLock)
                {
                    return _currentProfile;
                }
            }
            set
            {
                IProfile oldProfile;
                lock (_profileLock)
                {
                    if (_currentProfile == value) return;
                    oldProfile = _currentProfile;
                    _currentProfile = value;
                }
                oldProfile?.Reset();
            }
        }

        private void OnStateUpdated(GamepadState state)
        {
            long now = _clock.ElapsedTicks;
            float dt = (now - _lastTicks) / (float)Stopwatch.Frequency;
            _lastTicks = now;
            dt = Math.Min(dt, 0.05f); // Evita saltos de tempo após travamento ou suspensão

            IProfile profile;
            lock (_profileLock)
            {
                profile = _currentProfile;
            }
            profile?.Update(state, dt);
        }

        public void Dispose()
        {
            _gamepad.StateUpdated -= OnStateUpdated;
            IProfile profile;
            lock (_profileLock)
            {
                profile = _currentProfile;
                _currentProfile = null;
            }
            profile?.Reset();
        }
    }
}
