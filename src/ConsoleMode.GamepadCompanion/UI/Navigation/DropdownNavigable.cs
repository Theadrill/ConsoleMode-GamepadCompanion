using System;
using System.Drawing;
using System.Windows.Forms;

namespace ConsoleMode.GamepadCompanion.UI.Navigation
{
    /// <summary>Controle navegável para ComboBox com suporte a seleção modal via gamepad.</summary>
    public sealed class DropdownNavigable : INavigableControl
    {
        private readonly ComboBox _comboBox;
        private readonly Label _titleLabel;
        private bool _isFocused;
        private bool _isEditing;
        private int _originalIndex;
        private int _tempIndex;

        public DropdownNavigable(string id, Label titleLabel, ComboBox comboBox)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            _titleLabel = titleLabel ?? throw new ArgumentNullException(nameof(titleLabel));
            _comboBox = comboBox ?? throw new ArgumentNullException(nameof(comboBox));
        }

        public string Id { get; }
        public ComboBox ComboBox => _comboBox;
        public Label TitleLabel => _titleLabel;

        public int TempIndex => _tempIndex;

        public Rectangle Bounds
        {
            get
            {
                int left = Math.Min(_titleLabel.Left, _comboBox.Left) - 4;
                int top = _titleLabel.Top - 2;
                int right = Math.Max(_titleLabel.Right, _comboBox.Right) + 4;
                int bottom = _comboBox.Bottom + 4;
                return new Rectangle(left, top, right - left, bottom - top);
            }
        }

        public bool IsFocused
        {
            get => _isFocused;
            set
            {
                if (_isFocused != value)
                {
                    _isFocused = value;
                    StateChanged?.Invoke();
                }
            }
        }

        public bool IsEditing => _isEditing;

        public event Action StateChanged;

        public void OnFocusGained()
        {
            IsFocused = true;
        }

        public void OnFocusLost()
        {
            if (_isEditing)
            {
                CancelEdit();
            }
            IsFocused = false;
        }

        public bool OnButtonAPressed()
        {
            if (!_isEditing)
            {
                _originalIndex = _comboBox.SelectedIndex;
                _tempIndex = _originalIndex;
                _isEditing = true;
                StateChanged?.Invoke();
                return true;
            }

            // Confirma a seleção
            if (_tempIndex >= 0 && _tempIndex < _comboBox.Items.Count)
            {
                _comboBox.SelectedIndex = _tempIndex;
            }
            _isEditing = false;
            StateChanged?.Invoke();
            return true;
        }

        public bool OnButtonBPressed()
        {
            if (_isEditing)
            {
                CancelEdit();
                return true;
            }
            return false;
        }

        public bool OnDirection(NavigationDirection direction, bool isRepeat, int repeatCount)
        {
            if (!_isEditing)
            {
                return false;
            }

            if (direction == NavigationDirection.Up)
            {
                if (_tempIndex > 0)
                {
                    _tempIndex--;
                    StateChanged?.Invoke();
                }
                return true;
            }

            if (direction == NavigationDirection.Down)
            {
                if (_tempIndex < _comboBox.Items.Count - 1)
                {
                    _tempIndex++;
                    StateChanged?.Invoke();
                }
                return true;
            }

            // Horizontal ignorado no modo de seleção
            return true;
        }

        private void CancelEdit()
        {
            _isEditing = false;
            _tempIndex = _originalIndex;
            StateChanged?.Invoke();
        }
    }
}
