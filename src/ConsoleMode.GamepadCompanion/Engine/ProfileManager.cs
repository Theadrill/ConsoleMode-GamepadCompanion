using System;
using ConsoleMode.GamepadCompanion.Core.Interfaces;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.Hardware;
using ConsoleMode.GamepadCompanion.Profiles;

namespace ConsoleMode.GamepadCompanion.Engine
{
    /// <summary>
    /// Gerencia os perfis (Gaming dinâmico vs Desktop) baseado no foco da janela e no estado do botão Guide/Home.
    /// Suporta Toggle de Input por Jogo (EnableCompanionInput) e controle de confinamento de cursor (ClipCursor).
    /// </summary>
    public sealed class ProfileManager : IDisposable
    {
        private readonly ProfileEngine _profileEngine;
        private readonly GamingProfile _gamingProfile;
        private readonly DesktopProfile _desktopProfile;
        private readonly IWindowTracker _windowTracker;
        private readonly IGamepadService _gamepad;
        private readonly AppSettings _settings;

        private bool _lastGuideButton;
        private bool _cursorClipToggledOff;

        public ProfileManager(
            ProfileEngine profileEngine,
            GamingProfile gamingProfile,
            DesktopProfile desktopProfile,
            IWindowTracker windowTracker,
            IGamepadService gamepad,
            AppSettings settings = null)
        {
            _profileEngine = profileEngine ?? throw new ArgumentNullException(nameof(profileEngine));
            _gamingProfile = gamingProfile ?? throw new ArgumentNullException(nameof(gamingProfile));
            _desktopProfile = desktopProfile ?? throw new ArgumentNullException(nameof(desktopProfile));
            _windowTracker = windowTracker ?? throw new ArgumentNullException(nameof(windowTracker));
            _gamepad = gamepad ?? throw new ArgumentNullException(nameof(gamepad));
            _settings = settings;

            _windowTracker.FocusChanged += OnFocusChanged;
            _windowTracker.ActiveWindowChanged += OnActiveWindowChanged;
            _windowTracker.ActiveGameChanged += OnActiveGameChanged;
            _gamepad.StateUpdated += OnGamepadStateUpdated;

            _gamingProfile.CenterCursorRequested += OnCenterCursorRequested;
            _gamingProfile.ToggleClipCursorRequested += OnToggleClipCursorRequested;

            // Define perfil inicial baseado no foco atual
            _windowTracker.CheckActiveWindow();
            UpdateActiveProfile(_windowTracker.IsGameFocused);
        }

        public event Action GuidePressed;

        public bool IsGameFocused => _windowTracker.IsGameFocused;

        public bool IsCompanionInputActive => _profileEngine.CurrentProfile == _gamingProfile;

        public GameEntry ActiveGame => _windowTracker.ActiveGame;

        public string ActiveProfileName
        {
            get
            {
                if (!_windowTracker.IsGameFocused)
                {
                    return _desktopProfile.Name;
                }

                var game = _windowTracker.ActiveGame;
                if (game != null && !game.EnableCompanionInput)
                {
                    return $"{game.Name} (Desativado)";
                }

                if (game != null && !string.IsNullOrWhiteSpace(game.Name))
                {
                    return game.Name;
                }

                return _gamingProfile.Name;
            }
        }

        private void OnFocusChanged(bool isGameFocused)
        {
            UpdateActiveProfile(isGameFocused);
        }

        private void OnActiveGameChanged(GameEntry activeGame)
        {
            if (_windowTracker.IsGameFocused)
            {
                UpdateActiveProfile(true);
            }
        }

        private void OnActiveWindowChanged(IntPtr activeHwnd)
        {
            // Sempre que a janela alvo muda, reseta o perfil atual para evitar teclas presas
            _profileEngine.CurrentProfile?.Reset();

            if (activeHwnd != IntPtr.Zero && _windowTracker.IsGameFocused)
            {
                var game = _windowTracker.ActiveGame;
                bool shouldClip = (game == null || game.EnableCompanionInput) &&
                                  (_settings == null || _settings.ClipCursorToGameWindow) &&
                                  !_cursorClipToggledOff;

                if (shouldClip)
                {
                    WindowNative.ClipCursorToWindow(activeHwnd);
                }
                else
                {
                    WindowNative.ReleaseCursorClip();
                }
            }
            else
            {
                WindowNative.ReleaseCursorClip();
            }
        }

        private void UpdateActiveProfile(bool isGameFocused)
        {
            if (isGameFocused)
            {
                var game = _windowTracker.ActiveGame;
                if (game != null && !game.EnableCompanionInput)
                {
                    // Companion input desativado para este jogo: jogo lê controle nativo diretamente
                    _profileEngine.CurrentProfile = _desktopProfile;
                    WindowNative.ReleaseCursorClip();
                    return;
                }

                if (game?.Profile != null)
                {
                    _gamingProfile.ActiveProfile = game.Profile;
                }
                else
                {
                    _gamingProfile.ActiveProfile = GameProfilePresets.CreateWowClassic();
                }

                _profileEngine.CurrentProfile = _gamingProfile;

                bool shouldClip = (_settings == null || _settings.ClipCursorToGameWindow) && !_cursorClipToggledOff;
                if (shouldClip)
                {
                    WindowNative.ClipCursorToWindow(_windowTracker.ActiveWindowHandle);
                }
                else
                {
                    WindowNative.ReleaseCursorClip();
                }
            }
            else
            {
                _profileEngine.CurrentProfile = _desktopProfile;
                WindowNative.ReleaseCursorClip();
            }
        }

        private void OnCenterCursorRequested()
        {
            if (_windowTracker.IsGameFocused && _windowTracker.ActiveWindowHandle != IntPtr.Zero)
            {
                WindowNative.CenterCursorInWindow(_windowTracker.ActiveWindowHandle);
            }
        }

        private void OnToggleClipCursorRequested()
        {
            _cursorClipToggledOff = !_cursorClipToggledOff;
            if (_cursorClipToggledOff)
            {
                WindowNative.ReleaseCursorClip();
            }
            else if (_windowTracker.IsGameFocused && _windowTracker.ActiveWindowHandle != IntPtr.Zero)
            {
                WindowNative.ClipCursorToWindow(_windowTracker.ActiveWindowHandle);
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
            _windowTracker.ActiveWindowChanged -= OnActiveWindowChanged;
            _windowTracker.ActiveGameChanged -= OnActiveGameChanged;
            _gamepad.StateUpdated -= OnGamepadStateUpdated;

            _gamingProfile.CenterCursorRequested -= OnCenterCursorRequested;
            _gamingProfile.ToggleClipCursorRequested -= OnToggleClipCursorRequested;

            WindowNative.ReleaseCursorClip();
        }
    }
}
