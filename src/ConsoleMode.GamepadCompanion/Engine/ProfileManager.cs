using System;
using ConsoleMode.GamepadCompanion.Core.Interfaces;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.Profiles;

namespace ConsoleMode.GamepadCompanion.Engine
{
    /// <summary>
    /// Gerencia os perfis (Gaming vs Desktop) baseado no foco da janela e no estado do botão Guide/Home.
    /// </summary>
    public sealed class ProfileManager : IDisposable
    {
        private readonly ProfileEngine _profileEngine;
        private readonly GamingProfile _gamingProfile;
        private readonly DesktopProfile _desktopProfile;
        private readonly IWindowTracker _windowTracker;
        private readonly IGamepadService _gamepad;

        private bool _lastGuideButton;

        public ProfileManager(
            ProfileEngine profileEngine,
            GamingProfile gamingProfile,
            DesktopProfile desktopProfile,
            IWindowTracker windowTracker,
            IGamepadService gamepad)
        {
            _profileEngine = profileEngine ?? throw new ArgumentNullException(nameof(profileEngine));
            _gamingProfile = gamingProfile ?? throw new ArgumentNullException(nameof(gamingProfile));
            _desktopProfile = desktopProfile ?? throw new ArgumentNullException(nameof(desktopProfile));
            _windowTracker = windowTracker ?? throw new ArgumentNullException(nameof(windowTracker));
            _gamepad = gamepad ?? throw new ArgumentNullException(nameof(gamepad));

            _windowTracker.FocusChanged += OnFocusChanged;
            _gamepad.StateUpdated += OnGamepadStateUpdated;

            // Define perfil inicial baseado no foco atual
            _windowTracker.CheckActiveWindow();
            UpdateActiveProfile(_windowTracker.IsGameFocused);
        }

        public event Action GuidePressed;

        public bool IsGameFocused => _windowTracker.IsGameFocused;
        public string ActiveProfileName => _profileEngine.CurrentProfile?.Name ?? string.Empty;

        private void OnFocusChanged(bool isGameFocused)
        {
            UpdateActiveProfile(isGameFocused);
        }

        private void UpdateActiveProfile(bool isGameFocused)
        {
            if (isGameFocused)
            {
                _profileEngine.CurrentProfile = _gamingProfile;
            }
            else
            {
                _profileEngine.CurrentProfile = _desktopProfile;
            }
        }

        private void OnGamepadStateUpdated(GamepadState state)
        {
            if (!state.IsConnected) return;

            bool guideNow = state.IsPressed(GamepadButtons.Guide);
            if (guideNow && !_lastGuideButton)
            {
                GuidePressed?.Invoke();
            }
            _lastGuideButton = guideNow;
        }

        public void Dispose()
        {
            _windowTracker.FocusChanged -= OnFocusChanged;
            _gamepad.StateUpdated -= OnGamepadStateUpdated;
        }
    }
}
