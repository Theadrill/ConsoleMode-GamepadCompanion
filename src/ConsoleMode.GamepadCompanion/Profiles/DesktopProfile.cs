using System;
using System.Collections.Generic;
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
    /// Quando o próprio Companion estiver em primeiro plano, botões do controle (D-Pad, A, B, X, Y)
    /// e gatilhos/analógicos são silenciados para preservar a navegação nativa e fixa da UI,
    /// enquanto os trackpads continuam operando continuamente no mesmo motor para apontar, clicar
    /// e rolar com o mouse sem troca de perfil nem resets espúrios no meio de cliques.
    /// </summary>
    public sealed class DesktopProfile : IProfile
    {
        private static readonly uint CurrentPid = GetCurrentProcessId();

        public static Func<bool> ForegroundCheckOverride { get; set; }

        private readonly GamingProfile _innerProfile;
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

            _innerProfile.CenterCursorRequested += () => CenterCursorRequested?.Invoke();
            _innerProfile.ToggleClipCursorRequested += () => ToggleClipCursorRequested?.Invoke();
            _innerProfile.ScreenClipRequested += () => ScreenClipRequested?.Invoke();
            _innerProfile.AltTabRequested += () => AltTabRequested?.Invoke();
            _innerProfile.ShowDesktopRequested += () => ShowDesktopRequested?.Invoke();
            _innerProfile.HapticPulseRequested += (right, intensity) => HapticPulseRequested?.Invoke(right, intensity);
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
            if (!_settings.DesktopModeEnabled)
            {
                _innerProfile.Reset();
                return;
            }

            // Quando a janela do Companion está em foco (ou quando IsSuspended for sinalizado):
            // - Botões do controle (A/B/X/Y/D-Pad/Gatilhos/Sticks) e botões traseiros (L4-R5) são silenciados
            //   para não emitir teclas de teclado/mouse via SendInput (preservando a navegação nativa e fixa da UI).
            // - Os trackpads continuam rodando no mesmo motor contínuo para apontar, clicar e rolar
            //   com o mouse sem troca de perfil e SEM disparar Reset() no meio de cliques entre janelas!
            bool isCompanionInFg = IsCompanionInForeground() || IsSuspended;
            if (isCompanionInFg)
            {
                var navGamepadState = new GamepadState(
                    state.IsConnected,
                    state.PacketNumber,
                    GamepadButtons.None,
                    0, 0, // Triggers zerados
                    0, 0, // LeftStick zerado
                    0, 0  // RightStick zerado
                );

                var trackpadOnlyDeckRaw = new SteamDeckRawState
                {
                    Buttons0 = 0,
                    Buttons1 = deckRaw.Buttons1 & (SDCButton1.BTN_LPAD_TOUCH | SDCButton1.BTN_LPAD_PRESS | SDCButton1.BTN_RPAD_TOUCH | SDCButton1.BTN_RPAD_PRESS),
                    Buttons2 = 0,
                    LPadX = deckRaw.LPadX,
                    LPadY = deckRaw.LPadY,
                    LPadPressure = deckRaw.LPadPressure,
                    RPadX = deckRaw.RPadX,
                    RPadY = deckRaw.RPadY,
                    RPadPressure = deckRaw.RPadPressure
                };

                _innerProfile.Update(navGamepadState, trackpadOnlyDeckRaw, deltaSeconds, useCompanionNavTrackpads: true);
                return;
            }

            _innerProfile.Update(state, deckRaw, deltaSeconds, useCompanionNavTrackpads: false);
        }

        private static uint GetCurrentProcessId()
        {
            try { return (uint)Process.GetCurrentProcess().Id; }
            catch { return 0; }
        }

        internal static bool IsCompanionWindowInForeground() => IsCompanionInForeground();

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
