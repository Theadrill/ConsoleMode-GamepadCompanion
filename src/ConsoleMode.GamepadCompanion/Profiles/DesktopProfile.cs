using System;
using ConsoleMode.GamepadCompanion.Core.Interfaces;
using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.Profiles
{
    /// <summary>
    /// Perfil Desktop: Mapeamento para navegação no Windows via Gamepad.
    /// Ativo quando nenhuma janela de jogo monitorada estiver em primeiro plano.
    /// Suporta emulação completa via GamingProfile interno respeitando o toggle DesktopModeEnabled.
    /// </summary>
    public sealed class DesktopProfile : IProfile
    {
        private readonly GamingProfile _innerProfile;
        private readonly AppSettings _settings;

        public event Action CenterCursorRequested;
        public event Action ToggleClipCursorRequested;
        public event Action ScreenClipRequested;
        public event Action AltTabRequested;
        public event Action ShowDesktopRequested;

        public DesktopProfile(IInputSimulator input = null, AppSettings settings = null, GameProfile initialProfile = null)
        {
            _settings = settings ?? new AppSettings();
            var sim = input ?? new MockInputSimulator();
            _innerProfile = new GamingProfile(sim, _settings, initialProfile ?? GameProfilePresets.CreateDesktopDefault());

            _innerProfile.CenterCursorRequested += () => CenterCursorRequested?.Invoke();
            _innerProfile.ToggleClipCursorRequested += () => ToggleClipCursorRequested?.Invoke();
            _innerProfile.ScreenClipRequested += () => ScreenClipRequested?.Invoke();
            _innerProfile.AltTabRequested += () => AltTabRequested?.Invoke();
            _innerProfile.ShowDesktopRequested += () => ShowDesktopRequested?.Invoke();
        }

        public string Name => "Desktop";

        public bool Enabled
        {
            get => _settings.DesktopModeEnabled;
            set => _settings.DesktopModeEnabled = value;
        }

        public GameProfile ActiveProfile
        {
            get => _innerProfile.ActiveProfile;
            set => _innerProfile.ActiveProfile = value;
        }

        public void Update(GamepadState state, float deltaSeconds)
        {
            if (!_settings.DesktopModeEnabled)
            {
                _innerProfile.Reset();
                return;
            }

            _innerProfile.Update(state, deltaSeconds);
        }

        public void Reset()
        {
            _innerProfile.Reset();
        }

        private sealed class MockInputSimulator : IInputSimulator
        {
            public void KeyDown(VirtualKey key) { }
            public void KeyUp(VirtualKey key) { }
            public void MouseButtonDown(MouseButton button) { }
            public void MouseButtonUp(MouseButton button) { }
            public void MouseMoveRelative(int dx, int dy) { }
            public void MouseWheel(int delta) { }
        }
    }
}
