using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleMode.GamepadCompanion.Core.Interfaces;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.Engine;
using ConsoleMode.GamepadCompanion.Hardware;

namespace ConsoleMode.GamepadCompanion.Profiles
{
    /// <summary>
    /// Perfil Gaming dinâmico baseado em GameProfile:
    /// - Executa qualquer perfil customizado por jogo com união de estados por frame.
    /// - Garante que comandos compartilhados (ex: F9 entre Stick e Espaço, ou Mouse Right entre Stick e R3)
    ///   nunca sejam soltos enquanto qualquer entrada ativa ainda precisar deles.
    /// - Garante que MouseButtonDown ocorra estritamente ANTES de MouseMoveRelative.
    /// </summary>
    public sealed class GamingProfile : IProfile
    {
        private readonly IInputSimulator _input;
        private readonly AppSettings _settings;
        private readonly object _syncLock = new object();

        private GameProfile _activeProfile;
        private readonly List<VirtualKey> _currentlyHeldKeys = new List<VirtualKey>();
        private readonly List<MouseButton> _currentlyHeldMouseButtons = new List<MouseButton>();

        private float _remainderX;
        private float _remainderY;

        private bool _wasScreenClipActive;
        private bool _wasAltTabActive;
        private bool _wasShowDesktopActive;
        private readonly HashSet<CompanionActionType> _currentlyHeldSpecial = new HashSet<CompanionActionType>();

        private readonly TrackpadProcessor _leftTrackpadProcessor = new TrackpadProcessor(isRightPad: false);
        private readonly TrackpadProcessor _rightTrackpadProcessor = new TrackpadProcessor(isRightPad: true);
        private readonly ISteamDeckHaptics _hapticsProvider;

        public event Action<bool, TrackpadHapticIntensity> HapticPulseRequested;

        public event Action CenterCursorRequested;
        public event Action ToggleClipCursorRequested;
        public event Action ScreenClipRequested;
        public event Action AltTabRequested;
        public event Action ShowDesktopRequested;

        public GamingProfile(IInputSimulator input, AppSettings settings, GameProfile initialProfile = null, ISteamDeckHaptics haptics = null)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _activeProfile = initialProfile ?? GameProfilePresets.CreateWowClassic();
            _hapticsProvider = haptics;
        }

        public string Name => "Gaming";

        public GameProfile ActiveProfile
        {
            get
            {
                lock (_syncLock)
                {
                    return _activeProfile;
                }
            }
            set
            {
                lock (_syncLock)
                {
                    if (ReferenceEquals(_activeProfile, value)) return;
                    ResetInternal();
                    _activeProfile = value ?? GameProfilePresets.CreateWowClassic();
                }
            }
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
            if (!state.IsConnected || !_settings.MappingEnabled)
            {
                Reset();
                return;
            }

            GameProfile profile;
            lock (_syncLock)
            {
                profile = _activeProfile;
            }

            if (profile == null) return;
            var page = profile.ActivePage;
            if (page == null) return;

            // 1. Configurações de hardware (Deadzone e Limiar de Gatilhos)
            float deadzone = profile.UseGlobalDeadzone ? _settings.StickDeadzone : (profile.CustomDeadzonePercent / 100f);
            byte triggerThreshold = profile.UseGlobalTriggerThreshold ? _settings.TriggerThreshold : (byte)(profile.CustomTriggerThresholdPercent * 255 / 100);
            int sensitivity = page.MouseSensitivity > 0 ? page.MouseSensitivity : _settings.MouseSensitivity;

            // 2. Acumuladores de estado para união de estados no frame atual (preservando a ordem exata dos combos!)
            var frameKeys = new List<VirtualKey>();
            var frameMouseButtons = new List<MouseButton>();
            int frameWheelDelta = 0;
            float frameDx = 0f;
            float frameDy = 0f;
            var frameSpecial = new List<CompanionActionType>();

            void AccumulateActions(IEnumerable<InputAction> actions)
            {
                if (actions == null) return;
                foreach (var action in actions)
                {
                    if (action == null) continue;
                    switch (action.Type)
                    {
                        case InputActionType.Key:
                            if (action.Key != VirtualKey.None && !frameKeys.Contains(action.Key))
                            {
                                frameKeys.Add(action.Key);
                            }
                            break;
                        case InputActionType.MouseButton:
                            if (!frameMouseButtons.Contains(action.MouseButton))
                            {
                                frameMouseButtons.Add(action.MouseButton);
                            }
                            break;
                        case InputActionType.MouseWheel:
                            frameWheelDelta += action.WheelDelta;
                            break;
                        case InputActionType.SpecialAction:
                            if (action.SpecialAction != CompanionActionType.None)
                            {
                                frameSpecial.Add(action.SpecialAction);
                            }
                            break;
                    }
                }
            }

            // 3. Avaliação dos 14 botões físicos
            if (state.IsPressed(GamepadButtons.A)) AccumulateActions(page.GetActions(ControllerButton.A));
            if (state.IsPressed(GamepadButtons.B)) AccumulateActions(page.GetActions(ControllerButton.B));
            if (state.IsPressed(GamepadButtons.X)) AccumulateActions(page.GetActions(ControllerButton.X));
            if (state.IsPressed(GamepadButtons.Y)) AccumulateActions(page.GetActions(ControllerButton.Y));

            if (state.IsPressed(GamepadButtons.DPadUp)) AccumulateActions(page.GetActions(ControllerButton.DPadUp));
            if (state.IsPressed(GamepadButtons.DPadDown)) AccumulateActions(page.GetActions(ControllerButton.DPadDown));
            if (state.IsPressed(GamepadButtons.DPadLeft)) AccumulateActions(page.GetActions(ControllerButton.DPadLeft));
            if (state.IsPressed(GamepadButtons.DPadRight)) AccumulateActions(page.GetActions(ControllerButton.DPadRight));

            if (state.IsPressed(GamepadButtons.LeftShoulder)) AccumulateActions(page.GetActions(ControllerButton.LeftShoulder));
            if (state.IsPressed(GamepadButtons.RightShoulder)) AccumulateActions(page.GetActions(ControllerButton.RightShoulder));

            if (state.LeftTrigger >= triggerThreshold) AccumulateActions(page.GetActions(ControllerButton.LeftTrigger));
            if (state.RightTrigger >= triggerThreshold) AccumulateActions(page.GetActions(ControllerButton.RightTrigger));

            if (state.IsPressed(GamepadButtons.Back)) AccumulateActions(page.GetActions(ControllerButton.Back));
            if (state.IsPressed(GamepadButtons.Start)) AccumulateActions(page.GetActions(ControllerButton.Start));

            if (state.IsPressed(GamepadButtons.LeftStick))
            {
                AccumulateActions(page.GetActions(ControllerButton.LeftStickClick));
                if (page.LeftStick?.ClickActions != null) AccumulateActions(page.LeftStick.ClickActions);
            }

            if (state.IsPressed(GamepadButtons.RightStick))
            {
                AccumulateActions(page.GetActions(ControllerButton.RightStickClick));
                if (page.RightStick?.ClickActions != null) AccumulateActions(page.RightStick.ClickActions);
            }

            // 3.1. Avaliação dos Botões Traseiros (Steam Deck / Handheld)
            if (deckRaw.IsL4) AccumulateActions(page.GetActions(ControllerButton.L4));
            if (deckRaw.IsL5) AccumulateActions(page.GetActions(ControllerButton.L5));
            if (deckRaw.IsR4) AccumulateActions(page.GetActions(ControllerButton.R4));
            if (deckRaw.IsR5) AccumulateActions(page.GetActions(ControllerButton.R5));

            // 3.2. Avaliação dos Trackpads do Steam Deck (Esquerdo e Direito)
            if (page.LeftTrackpad != null && page.LeftTrackpad.Mode != TrackpadMode.Disabled)
            {
                var lpadOut = _leftTrackpadProcessor.Process(
                    deckRaw.IsLPadTouch,
                    deckRaw.IsLPadPress,
                    deckRaw.LPadX,
                    deckRaw.LPadY,
                    page.LeftTrackpad,
                    deltaSeconds);

                frameDx += lpadOut.MouseDx;
                frameDy += lpadOut.MouseDy;
                frameWheelDelta += lpadOut.WheelDelta;
                AccumulateActions(lpadOut.Actions);

                if (lpadOut.TriggerHaptic && lpadOut.HapticIntensity != TrackpadHapticIntensity.Off)
                {
                    DispatchHaptic(isRightPad: false, lpadOut.HapticIntensity);
                }
            }

            if (page.RightTrackpad != null && page.RightTrackpad.Mode != TrackpadMode.Disabled)
            {
                var rpadOut = _rightTrackpadProcessor.Process(
                    deckRaw.IsRPadTouch,
                    deckRaw.IsRPadPress,
                    deckRaw.RPadX,
                    deckRaw.RPadY,
                    page.RightTrackpad,
                    deltaSeconds);

                frameDx += rpadOut.MouseDx;
                frameDy += rpadOut.MouseDy;
                frameWheelDelta += rpadOut.WheelDelta;
                AccumulateActions(rpadOut.Actions);

                if (rpadOut.TriggerHaptic && rpadOut.HapticIntensity != TrackpadHapticIntensity.Off)
                {
                    DispatchHaptic(isRightPad: true, rpadOut.HapticIntensity);
                }
            }

            // 4. Avaliação do Analógico Esquerdo
            if (page.LeftStick != null && page.LeftStick.Mode != StickMode.Disabled)
            {
                float leftNormX = state.LeftThumbX / 32767f;
                float leftNormY = state.LeftThumbY / 32767f;
                float leftMagnitude = (float)Math.Sqrt(leftNormX * leftNormX + leftNormY * leftNormY);
                bool leftActive = leftMagnitude > deadzone;

                if (page.LeftStick.Mode == StickMode.DirectionalKeys)
                {
                    if (leftActive)
                    {
                        if (leftNormY > 0.35f) AccumulateActions(page.LeftStick.Up);
                        if (leftNormY < -0.35f) AccumulateActions(page.LeftStick.Down);
                        if (leftNormX < -0.35f) AccumulateActions(page.LeftStick.Left);
                        if (leftNormX > 0.35f) AccumulateActions(page.LeftStick.Right);

                        AccumulateActions(page.LeftStick.AdditionalActions);
                    }
                }
                else if (page.LeftStick.Mode == StickMode.MouseMove)
                {
                    MouseCurveCalculator.Calculate(
                        state.LeftThumbX, state.LeftThumbY, sensitivity, deltaSeconds,
                        deadzone, out float ldx, out float ldy);
                    frameDx += ldx;
                    frameDy += ldy;

                    if (leftActive)
                    {
                        AccumulateActions(page.LeftStick.AdditionalActions);
                    }
                }
            }

            // 5. Avaliação do Analógico Direito
            if (page.RightStick != null && page.RightStick.Mode != StickMode.Disabled)
            {
                float rightNormX = state.RightThumbX / 32767f;
                float rightNormY = state.RightThumbY / 32767f;
                float rightMagnitude = (float)Math.Sqrt(rightNormX * rightNormX + rightNormY * rightNormY);
                bool rightActive = rightMagnitude > deadzone;

                if (page.RightStick.Mode == StickMode.MouseMove)
                {
                    MouseCurveCalculator.Calculate(
                        state.RightThumbX, state.RightThumbY, sensitivity, deltaSeconds,
                        deadzone, out float rdx, out float rdy);
                    frameDx += rdx;
                    frameDy += rdy;

                    if (rightActive)
                    {
                        AccumulateActions(page.RightStick.AdditionalActions);
                    }
                }
                else if (page.RightStick.Mode == StickMode.DirectionalKeys)
                {
                    if (rightActive)
                    {
                        if (rightNormY > 0.35f) AccumulateActions(page.RightStick.Up);
                        if (rightNormY < -0.35f) AccumulateActions(page.RightStick.Down);
                        if (rightNormX < -0.35f) AccumulateActions(page.RightStick.Left);
                        if (rightNormX > 0.35f) AccumulateActions(page.RightStick.Right);

                        AccumulateActions(page.RightStick.AdditionalActions);
                    }
                }
            }

            // 6. Aplicação da União de Estados (Diff por frame preservando ordem de combo)
            List<VirtualKey> keysToRelease;
            List<VirtualKey> keysToPress;
            List<MouseButton> mouseToRelease;
            List<MouseButton> mouseToPress;

            lock (_syncLock)
            {
                // Solta na ordem inversa (tecla final antes dos modificadores)
                keysToRelease = _currentlyHeldKeys.Where(k => !frameKeys.Contains(k)).Reverse().ToList();
                // Pressiona na ordem exata do combo (modificadores antes da tecla final)
                keysToPress = frameKeys.Where(k => !_currentlyHeldKeys.Contains(k)).ToList();

                foreach (var k in keysToRelease) _currentlyHeldKeys.Remove(k);
                foreach (var k in keysToPress) _currentlyHeldKeys.Add(k);

                mouseToRelease = _currentlyHeldMouseButtons.Where(b => !frameMouseButtons.Contains(b)).Reverse().ToList();
                mouseToPress = frameMouseButtons.Where(b => !_currentlyHeldMouseButtons.Contains(b)).ToList();

                foreach (var b in mouseToRelease) _currentlyHeldMouseButtons.Remove(b);
                foreach (var b in mouseToPress) _currentlyHeldMouseButtons.Add(b);
            }

            // 6.1 Solta teclas que não são mais requisitadas por nenhuma entrada ativa
            foreach (var k in keysToRelease)
            {
                _input.KeyUp(k);
            }

            // 6.2 Pressiona novas teclas requisitadas
            foreach (var k in keysToPress)
            {
                _input.KeyDown(k);
            }

            // 6.3 Solta botões de mouse não mais requisitados
            foreach (var b in mouseToRelease)
            {
                _input.MouseButtonUp(b);
            }

            // 6.4 REGRA ARQUITETURAL: MouseButtonDown ocorre estritamente ANTES de MouseMoveRelative
            foreach (var b in mouseToPress)
            {
                _input.MouseButtonDown(b);
            }

            // 6.5 Scroll de Mouse
            if (frameWheelDelta != 0)
            {
                _input.MouseWheel(frameWheelDelta);
            }

            // 6.6 Movimentação Relativa do Mouse com Acumulação Contínua de Subpixel
            _remainderX += frameDx;
            _remainderY += frameDy;
            int moveX = (int)_remainderX;
            int moveY = (int)_remainderY;
            _remainderX -= moveX;
            _remainderY -= moveY;

            if (moveX != 0 || moveY != 0)
            {
                _input.MouseMoveRelative(moveX, moveY);
            }

            // 6.7 Detecção de Atalhos de Sistema do Windows (Win+Shift+S, Alt+Tab, Win+D) e Ações Especiais
            bool hasWin = frameKeys.Contains(VirtualKey.LeftWindows) || frameKeys.Contains(VirtualKey.RightWindows);
            bool hasShift = frameKeys.Contains(VirtualKey.LeftShift) || frameKeys.Contains(VirtualKey.RightShift);
            bool hasAlt = frameKeys.Contains(VirtualKey.LeftAlt) || frameKeys.Contains(VirtualKey.RightAlt);

            bool isScreenClipCombo = hasWin && hasShift && frameKeys.Contains(VirtualKey.S) && frameKeys.Count == 3;
            if (isScreenClipCombo && !_wasScreenClipActive)
            {
                ScreenClipRequested?.Invoke();
            }
            _wasScreenClipActive = isScreenClipCombo;

            bool isAltTabCombo = (hasAlt && frameKeys.Contains(VirtualKey.Tab) && frameKeys.Count == 2) ||
                                 frameSpecial.Contains(CompanionActionType.AltTab);
            if (isAltTabCombo && !_wasAltTabActive)
            {
                AltTabRequested?.Invoke();
            }
            _wasAltTabActive = isAltTabCombo;

            bool isShowDesktopCombo = (hasWin && frameKeys.Contains(VirtualKey.D) && frameKeys.Count == 2) ||
                                      frameSpecial.Contains(CompanionActionType.ShowDesktop);
            if (isShowDesktopCombo && !_wasShowDesktopActive)
            {
                ShowDesktopRequested?.Invoke();
            }
            _wasShowDesktopActive = isShowDesktopCombo;

            if (frameSpecial.Count > 0)
            {
                foreach (var sp in frameSpecial)
                {
                    if (!_currentlyHeldSpecial.Contains(sp))
                    {
                        _currentlyHeldSpecial.Add(sp);
                        if (sp == CompanionActionType.CenterCursor)
                        {
                            CenterCursorRequested?.Invoke();
                        }
                        else if (sp == CompanionActionType.ToggleClipCursor)
                        {
                            ToggleClipCursorRequested?.Invoke();
                        }
                        else if (sp == CompanionActionType.AltF4)
                        {
                            _input.KeyDown(VirtualKey.LeftAlt);
                            _input.KeyDown(VirtualKey.F4);
                            _input.KeyUp(VirtualKey.F4);
                            _input.KeyUp(VirtualKey.LeftAlt);
                        }
                        else if (sp == CompanionActionType.AltEnter)
                        {
                            _input.KeyDown(VirtualKey.LeftAlt);
                            _input.KeyDown(VirtualKey.Enter);
                            _input.KeyUp(VirtualKey.Enter);
                            _input.KeyUp(VirtualKey.LeftAlt);
                        }
                    }
                }
            }
            _currentlyHeldSpecial.RemoveWhere(sp => !frameSpecial.Contains(sp));
        }

        public void Reset()
        {
            lock (_syncLock)
            {
                ResetInternal();
            }
        }

        private void ResetInternal()
        {
            var keys = _currentlyHeldKeys.ToArray();
            _currentlyHeldKeys.Clear();

            var buttons = _currentlyHeldMouseButtons.ToArray();
            _currentlyHeldMouseButtons.Clear();

            foreach (var k in keys)
            {
                _input.KeyUp(k);
            }

            foreach (var b in buttons)
            {
                _input.MouseButtonUp(b);
            }

            _remainderX = 0f;
            _remainderY = 0f;
            _wasScreenClipActive = false;
            _wasAltTabActive = false;
            _wasShowDesktopActive = false;
            _currentlyHeldSpecial.Clear();

            _leftTrackpadProcessor.Reset();
            _rightTrackpadProcessor.Reset();
        }

        private void DispatchHaptic(bool isRightPad, TrackpadHapticIntensity intensity)
        {
            try
            {
                if (_hapticsProvider != null)
                {
                    _hapticsProvider.TriggerHaptic(isRightPad, intensity);
                }
                else
                {
                    SteamDeckManager.TriggerHaptic(isRightPad, intensity);
                }
            }
            catch { }

            HapticPulseRequested?.Invoke(isRightPad, intensity);
        }
    }
}
