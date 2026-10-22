using System;
using System.Diagnostics;
using ConsoleMode.GamepadCompanion.Core.Interfaces;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.Hardware;

namespace ConsoleMode.GamepadCompanion.Profiles
{
    /// <summary>
    /// Perfil Desktop: Mapeamento para navegação no Windows via Gamepad.
    /// Ativo quando nenhuma janela de jogo monitorada estiver em primeiro plano.
    /// Suporta emulação completa via GamingProfile interno respeitando o toggle DesktopModeEnabled.
    /// Suspende automaticamente a emulação quando o próprio Companion estiver em primeiro plano.
    /// </summary>
    public sealed class DesktopProfile : IProfile
    {
        private static readonly uint CurrentPid = GetCurrentProcessId();

        public static Func<bool> ForegroundCheckOverride { get; set; }

        private readonly GamingProfile _innerProfile;
        private readonly GamingProfile _foregroundAppNavProfile;
        private readonly AppSettings _settings;

        public event Action CenterCursorRequested;
        public event Action ToggleClipCursorRequested;
        public event Action ScreenClipRequested;
        public event Action AltTabRequested;
        public event Action ShowDesktopRequested;
        public event Action<bool, TrackpadHapticIntensity> HapticPulseRequested;

        public DesktopProfile(IInputSimulator input = null, AppSettings settings = null, GameProfile initialProfile = null, ISteamDeckHaptics haptics = null)
        {
            _settings = settings ?? new AppSettings();
            var sim = input ?? new MockInputSimulator();
            _innerProfile = new GamingProfile(sim, _settings, initialProfile ?? GameProfilePresets.CreateDesktopDefault(), haptics);
            _foregroundAppNavProfile = new GamingProfile(sim, _settings, GameProfilePresets.CreateDesktopDefault(), haptics);

            _innerProfile.CenterCursorRequested += () => CenterCursorRequested?.Invoke();
            _innerProfile.ToggleClipCursorRequested += () => ToggleClipCursorRequested?.Invoke();
            _innerProfile.ScreenClipRequested += () => ScreenClipRequested?.Invoke();
            _innerProfile.AltTabRequested += () => AltTabRequested?.Invoke();
            _innerProfile.ShowDesktopRequested += () => ShowDesktopRequested?.Invoke();
            _innerProfile.HapticPulseRequested += (right, intensity) => HapticPulseRequested?.Invoke(right, intensity);

            _foregroundAppNavProfile.HapticPulseRequested += (right, intensity) => HapticPulseRequested?.Invoke(right, intensity);
        }

        public string Name => "Desktop";

        public bool Enabled
        {
            get => _settings.DesktopModeEnabled;
            set => _settings.DesktopModeEnabled = value;
        }

        public bool IsSuspended { get; set; }

        public GameProfile ActiveProfile
        {
            get => _innerProfile.ActiveProfile;
            set => _innerProfile.ActiveProfile = value;
        }

        public void Update(GamepadState state, float deltaSeconds)
        {
            SteamDeckRawState deckRaw = default;
            if (SteamDeckManager.IsActive && SteamDeckManager.ActiveService != null)
            {
                deckRaw = SteamDeckManager.ActiveService.CurrentRawState;
            }
            Update(state, deckRaw, deltaSeconds);
        }

        public void Update(GamepadState state, SteamDeckRawState deckRaw, float deltaSeconds)
        {
            if (!_settings.DesktopModeEnabled || IsSuspended)
            {
                _innerProfile.Reset();
                _foregroundAppNavProfile.Reset();
                return;
            }

            if (IsCompanionInForeground())
            {
                // Quando o Companion está em primeiro plano, botões do controle e analógicos
                // são preservados para a navegação interna da UI via GamepadNavigationManager,
                // mas os trackpads continuam operando normalmente como Mouse + Scroll para permitir
                // apontar, clicar e rolar livremente pelo app com os trackpads.
                _innerProfile.Reset();

                var emptyGamepadState = new GamepadState(true, state.PacketNumber, GamepadButtons.None, 0, 0, 0, 0, 0, 0);
                _foregroundAppNavProfile.Update(emptyGamepadState, deckRaw, deltaSeconds);
                return;
            }

            _foregroundAppNavProfile.Reset();
            _innerProfile.Update(state, deckRaw, deltaSeconds);
        }

        private static uint GetCurrentProcessId()
        {
            try { return (uint)Process.GetCurrentProcess().Id; }
            catch { return 0; }
        }

        private static bool IsCompanionInForeground()
        {
            if (ForegroundCheckOverride != null)
            {
                return ForegroundCheckOverride();
            }

            if (CurrentPid == 0) return false;
            try
            {
                IntPtr fg = WindowNative.GetForegroundWindow();
                if (fg == IntPtr.Zero) return false;
                WindowNative.GetWindowThreadProcessId(fg, out uint fgPid);
                return fgPid == CurrentPid;
            }
            catch
            {
                return false;
            }
        }

        public void Reset()
        {
            _innerProfile.Reset();
            _foregroundAppNavProfile.Reset();
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
