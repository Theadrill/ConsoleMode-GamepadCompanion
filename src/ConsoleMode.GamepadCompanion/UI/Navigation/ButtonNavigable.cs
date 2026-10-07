using System;
using System.Drawing;
using System.Windows.Forms;

namespace ConsoleMode.GamepadCompanion.UI.Navigation
{
    /// <summary>Controle navegável para botões comuns e toggles (ação direta com A, sem overlay).</summary>
    public sealed class ButtonNavigable : INavigableControl
    {
        private readonly Button _button;
        private readonly Color _normalBorderColor;
        private readonly int _normalBorderSize;
        private bool _isFocused;

        public ButtonNavigable(string id, Button button)
        {
            _button = button ?? throw new ArgumentNullException(nameof(button));
            Id = id ?? throw new ArgumentNullException(nameof(id));
            _normalBorderColor = _button.FlatAppearance.BorderColor;
            _normalBorderSize = _button.FlatAppearance.BorderSize;
        }

        public string Id { get; }

        public Rectangle Bounds => _button.Bounds;

        public bool IsFocused
        {
            get => _isFocused;
            set
            {
                if (_isFocused != value)
                {
                    _isFocused = value;
                    UpdateVisual();
                    StateChanged?.Invoke();
                }
            }
        }

        public bool IsEditing => false;

        public event Action StateChanged;

        public void OnFocusGained()
        {
            IsFocused = true;
        }

        public void OnFocusLost()
        {
            IsFocused = false;
        }

        public bool OnButtonAPressed()
        {
            _button.PerformClick();
            return true;
        }

        public bool OnButtonBPressed() => false;

        public bool OnDirection(NavigationDirection direction, bool isRepeat, int repeatCount) => false;

        private void UpdateVisual()
        {
            if (_isFocused)
            {
                _button.FlatAppearance.BorderColor = Color.FromArgb(120, 190, 255);
                _button.FlatAppearance.BorderSize = 2;
            }
            else
            {
                _button.FlatAppearance.BorderColor = _normalBorderColor;
                _button.FlatAppearance.BorderSize = _normalBorderSize;
            }
            _button.Invalidate();
        }
    }
}
