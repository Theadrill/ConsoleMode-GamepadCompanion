using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.UI.Navigation
{
    /// <summary>
    /// Gerenciador de foco e navegação espacial por gamepad para a interface.
    /// Trata D-Pad, botões A e B, auto-repeat progressivo e foco visual.
    /// </summary>
    public sealed class GamepadNavigationManager
    {
        private const int InitialRepeatDelayMs = 260;
        private const int ListRepeatIntervalMs = 160;

        private readonly List<INavigableControl> _controls = new List<INavigableControl>();
        private int _focusedIndex = -1;

        private bool _prevA;
        private bool _prevB;

        private NavigationDirection? _currentDirection;
        private long _dirStartTime;
        private long _lastRepeatTime;
        private int _repeatCount;

        public event Action<INavigableControl> FocusChanged;
        public event Action<INavigableControl, bool> EditingChanged;
        public event Action RequestRepaint;

        public IReadOnlyList<INavigableControl> Controls => _controls;

        public int FocusedIndex => _focusedIndex;

        public INavigableControl FocusedControl =>
            _focusedIndex >= 0 && _focusedIndex < _controls.Count ? _controls[_focusedIndex] : null;

        public bool IsEditingAny => FocusedControl?.IsEditing == true;

        public void RegisterControl(INavigableControl control)
        {
            if (control == null) return;
            control.StateChanged += () => OnControlStateChanged(control);
            _controls.Add(control);

            if (_focusedIndex == -1)
            {
                SetFocusedIndex(0);
            }
        }

        public void SetFocusedIndex(int index)
        {
            if (_controls.Count == 0) return;
            index = Math.Max(0, Math.Min(_controls.Count - 1, index));

            if (index == _focusedIndex) return;

            var oldControl = FocusedControl;
            _focusedIndex = index;
            var newControl = FocusedControl;

            oldControl?.OnFocusLost();
            newControl?.OnFocusGained();
            _isCurrentlyEditing = newControl?.IsEditing == true;

            FocusChanged?.Invoke(newControl);
            RequestRepaint?.Invoke();
        }

        public void ProcessGamepad(GamepadState state, long nowMs)
        {
            if (!state.IsConnected || _controls.Count == 0) return;

            // 1. Processa botão A (Confirmar / Editar / Clicar)
            bool aPressed = state.IsPressed(GamepadButtons.A);
            if (aPressed && !_prevA)
            {
                FocusedControl?.OnButtonAPressed();
                RequestRepaint?.Invoke();
            }
            _prevA = aPressed;

            // 2. Processa botão B (Cancelar / Restaurar valor)
            bool bPressed = state.IsPressed(GamepadButtons.B);
            if (bPressed && !_prevB)
            {
                FocusedControl?.OnButtonBPressed();
                RequestRepaint?.Invoke();
            }
            _prevB = bPressed;

            // 3. Resolve direção do D-Pad
            NavigationDirection? dir = null;
            if (state.IsPressed(GamepadButtons.DPadUp)) dir = NavigationDirection.Up;
            else if (state.IsPressed(GamepadButtons.DPadDown)) dir = NavigationDirection.Down;
            else if (state.IsPressed(GamepadButtons.DPadLeft)) dir = NavigationDirection.Left;
            else if (state.IsPressed(GamepadButtons.DPadRight)) dir = NavigationDirection.Right;

            // 4. Trata pulo inicial e auto-repeat com aceleração
            if (dir.HasValue)
            {
                if (_currentDirection != dir.Value)
                {
                    _currentDirection = dir.Value;
                    _dirStartTime = nowMs;
                    _lastRepeatTime = nowMs;
                    _repeatCount = 0;
                    HandleDirection(dir.Value, isRepeat: false, repeatCount: 0);
                    RequestRepaint?.Invoke();
                }
                else
                {
                    long elapsed = nowMs - _dirStartTime;
                    if (elapsed >= InitialRepeatDelayMs)
                    {
                        int interval = CalculateInterval();
                        if (nowMs - _lastRepeatTime >= interval)
                        {
                            _repeatCount++;
                            _lastRepeatTime = nowMs;
                            HandleDirection(dir.Value, isRepeat: true, repeatCount: _repeatCount);
                            RequestRepaint?.Invoke();
                        }
                    }
                }
            }
            else
            {
                _currentDirection = null;
                _repeatCount = 0;
            }
        }

        private int CalculateInterval()
        {
            if (FocusedControl?.IsEditing == true &&
                (_currentDirection == NavigationDirection.Left || _currentDirection == NavigationDirection.Right))
            {
                // Aceleração progressiva no slider: começa em 90ms e acelera até 20ms
                return Math.Max(20, 90 - (_repeatCount * 4));
            }

            return ListRepeatIntervalMs;
        }

        private void HandleDirection(NavigationDirection dir, bool isRepeat, int repeatCount)
        {
            var active = FocusedControl;
            if (active == null) return;

            if (active.IsEditing)
            {
                active.OnDirection(dir, isRepeat, repeatCount);
                return;
            }

            // Modo lista: navega entre os controles com Up / Down
            if (dir == NavigationDirection.Down)
            {
                if (_focusedIndex < _controls.Count - 1)
                {
                    SetFocusedIndex(_focusedIndex + 1);
                }
            }
            else if (dir == NavigationDirection.Up)
            {
                if (_focusedIndex > 0)
                {
                    SetFocusedIndex(_focusedIndex - 1);
                }
            }
        }

        private bool _isCurrentlyEditing;

        private void OnControlStateChanged(INavigableControl control)
        {
            if (control == FocusedControl && control.IsEditing != _isCurrentlyEditing)
            {
                _isCurrentlyEditing = control.IsEditing;
                EditingChanged?.Invoke(control, control.IsEditing);
            }
            RequestRepaint?.Invoke();
        }

        public void DrawFocusHighlight(Graphics g)
        {
            var active = FocusedControl;
            if (active == null || active.IsEditing) return;

            // Se for botão, o próprio botão já customiza seu contorno
            if (active is ButtonNavigable) return;

            var r = active.Bounds;
            if (r.Width <= 0 || r.Height <= 0) return;

            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Desenha retângulo com cantos arredondados destacando o bloco focado
            using (var pen = new Pen(Color.FromArgb(120, 190, 255), 2f))
            {
                DrawRoundedRectangle(g, pen, r, 6);
            }
        }

        private static void DrawRoundedRectangle(Graphics g, Pen pen, Rectangle r, int radius)
        {
            using (var path = new GraphicsPath())
            {
                int d = radius * 2;
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                g.DrawPath(pen, path);
            }
        }
    }
}
