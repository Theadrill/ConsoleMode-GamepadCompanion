using System;
using System.Collections.Generic;
using ConsoleMode.GamepadCompanion.Core.Interfaces;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.Engine;

namespace ConsoleMode.GamepadCompanion.Profiles
{
    /// <summary>
    /// Perfil Gaming 100% idêntico ao layout Octowow do Steam Input para ConsoleModeVanilla:
    /// - Botões Faciais: A (Espaço + F9), B (3), X (1), Y (2)
    /// - D-Pad: Cima (7), Baixo (8), Esquerda (9), Direita (0)
    /// - Bumpers: LB (Tab), RB (Ctrl)
    /// - Gatilhos Digitais: LT (Shift), RT (Alt)
    /// - Menus: Select (M), Start (F11)
    /// - Stick Esquerdo: W, A, S, D + Smart Mouse Look (F9)
    /// - Stick Direito: Movimento relativo do mouse
    /// - R3: Clique do botão direito do mouse
    /// </summary>
    public sealed class GamingProfile : IProfile
    {
        private readonly IInputSimulator _input;
        private readonly AppSettings _settings;
        private readonly SmartMouseLookHandler _smartMouseLook;

        private readonly HashSet<VirtualKey> _activeKeys = new HashSet<VirtualKey>();
        private bool _l3Held;
        private bool _r3Held;
        private float _remainderX;
        private float _remainderY;

        public GamingProfile(IInputSimulator input, AppSettings settings)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _smartMouseLook = new SmartMouseLookHandler(_input);
        }

        public string Name => "Gaming";

        public void Update(GamepadState state, float deltaSeconds)
        {
            if (!state.IsConnected || !_settings.MappingEnabled)
            {
                Reset();
                return;
            }

            // 1. Gatilhos analógicos com comportamento digital imediato (LT = Shift, RT = Alt)
            byte triggerThreshold = _settings.TriggerThreshold;
            bool ltPressed = state.LeftTrigger >= triggerThreshold;
            bool rtPressed = state.RightTrigger >= triggerThreshold;

            SetKeyState(VirtualKey.LeftShift, ltPressed);
            SetKeyState(VirtualKey.LeftAlt, rtPressed);

            // 2. Bumpers (LB = Tab, RB = Ctrl)
            SetKeyState(VirtualKey.Tab, state.IsPressed(GamepadButtons.LeftShoulder));
            SetKeyState(VirtualKey.LeftControl, state.IsPressed(GamepadButtons.RightShoulder));

            // 3. Menus (Select = M, Start = F11)
            SetKeyState(VirtualKey.M, state.IsPressed(GamepadButtons.Back));
            SetKeyState(VirtualKey.F11, state.IsPressed(GamepadButtons.Start));

            // 4. D-Pad (Cima = 7, Baixo = 8, Esquerda = 9, Direita = 0)
            SetKeyState(VirtualKey.D7, state.IsPressed(GamepadButtons.DPadUp));
            SetKeyState(VirtualKey.D8, state.IsPressed(GamepadButtons.DPadDown));
            SetKeyState(VirtualKey.D9, state.IsPressed(GamepadButtons.DPadLeft));
            SetKeyState(VirtualKey.D0, state.IsPressed(GamepadButtons.DPadRight));

            // 5. Botões Faciais (B = 3, X = 1, Y = 2, A = Espaço)
            SetKeyState(VirtualKey.D3, state.IsPressed(GamepadButtons.B));
            SetKeyState(VirtualKey.D1, state.IsPressed(GamepadButtons.X));
            SetKeyState(VirtualKey.D2, state.IsPressed(GamepadButtons.Y));

            bool aPressed = state.IsPressed(GamepadButtons.A);
            SetKeyState(VirtualKey.Space, aPressed);

            // 6. Analógico Esquerdo -> WASD com threshold de deadzone circular
            float deadzone = _settings.StickDeadzone;
            float leftNormX = state.LeftThumbX / 32767f;
            float leftNormY = state.LeftThumbY / 32767f;
            float leftMagnitude = (float)Math.Sqrt(leftNormX * leftNormX + leftNormY * leftNormY);
            bool leftActive = leftMagnitude > deadzone;

            bool moveW = false;
            bool moveS = false;
            bool moveA = false;
            bool moveD = false;

            if (leftActive)
            {
                // Limiar direcional de 0.35 para cada eixo quando fora da deadzone
                if (leftNormY > 0.35f) moveW = true;
                if (leftNormY < -0.35f) moveS = true;
                if (leftNormX < -0.35f) moveA = true;
                if (leftNormX > 0.35f) moveD = true;
            }

            SetKeyState(VirtualKey.W, moveW);
            SetKeyState(VirtualKey.S, moveS);
            SetKeyState(VirtualKey.A, moveA);
            SetKeyState(VirtualKey.D, moveD);

            // 7. Smart Mouse Look Companion (F9 sincronizado com WASD e Espaço/A)
            _smartMouseLook.Update(leftActive, aPressed);

            // 8. L3 -> Clique do botão esquerdo do mouse
            bool l3Pressed = state.IsPressed(GamepadButtons.LeftStick);
            if (l3Pressed && !_l3Held)
            {
                _input.MouseButtonDown(MouseButton.Left);
                _l3Held = true;
            }
            else if (!l3Pressed && _l3Held)
            {
                _input.MouseButtonUp(MouseButton.Left);
                _l3Held = false;
            }

            // 9. R3 -> Clique do botão direito do mouse
            bool r3Pressed = state.IsPressed(GamepadButtons.RightStick);
            if (r3Pressed && !_r3Held)
            {
                _input.MouseButtonDown(MouseButton.Right);
                _r3Held = true;
            }
            else if (!r3Pressed && _r3Held)
            {
                _input.MouseButtonUp(MouseButton.Right);
                _r3Held = false;
            }

            // 10. Analógico Direito -> Emulação de mouse com curva exponencial suave
            MouseCurveCalculator.Calculate(
                state.RightThumbX, state.RightThumbY, _settings.MouseSensitivity, deltaSeconds,
                deadzone, out float dx, out float dy);

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

        private void SetKeyState(VirtualKey key, bool pressed)
        {
            if (pressed && !_activeKeys.Contains(key))
            {
                _input.KeyDown(key);
                _activeKeys.Add(key);
            }
            else if (!pressed && _activeKeys.Contains(key))
            {
                _input.KeyUp(key);
                _activeKeys.Remove(key);
            }
        }

        public void Reset()
        {
            foreach (var key in _activeKeys)
            {
                _input.KeyUp(key);
            }
            _activeKeys.Clear();

            _smartMouseLook.Reset();

            if (_l3Held)
            {
                _input.MouseButtonUp(MouseButton.Left);
                _l3Held = false;
            }

            if (_r3Held)
            {
                _input.MouseButtonUp(MouseButton.Right);
                _r3Held = false;
            }

            _remainderX = 0f;
            _remainderY = 0f;
        }
    }
}
