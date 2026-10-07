using System;
using System.Drawing;
using System.Windows.Forms;

namespace ConsoleMode.GamepadCompanion.UI.Navigation
{
    /// <summary>Controle navegável para sliders numéricos com suporte a modo de edição, aceleração e cancelamento.</summary>
    public sealed class SliderNavigable : INavigableControl
    {
        private readonly Label _titleLabel;
        private readonly TrackBar _trackBar;
        private readonly Label _valueLabel;
        private readonly Action<int> _onChanged;
        private bool _isFocused;
        private bool _isEditing;
        private int _originalValue;

        public SliderNavigable(
            string id,
            string title,
            Label titleLabel,
            TrackBar trackBar,
            Label valueLabel,
            Action<int> onChanged)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Title = title ?? string.Empty;
            _titleLabel = titleLabel ?? throw new ArgumentNullException(nameof(titleLabel));
            _trackBar = trackBar ?? throw new ArgumentNullException(nameof(trackBar));
            _valueLabel = valueLabel ?? throw new ArgumentNullException(nameof(valueLabel));
            _onChanged = onChanged;
        }

        public string Id { get; }
        public string Title { get; }
        public TrackBar TrackBar => _trackBar;
        public Label ValueLabel => _valueLabel;
        public Label TitleLabel => _titleLabel;

        public int Value => _trackBar.Value;
        public int Minimum => _trackBar.Minimum;
        public int Maximum => _trackBar.Maximum;

        public Rectangle Bounds
        {
            get
            {
                int left = Math.Min(_titleLabel.Left, Math.Min(_trackBar.Left, _valueLabel.Left)) - 2;
                int top = _titleLabel.Top - 2;
                int right = Math.Max(_titleLabel.Right, Math.Max(_trackBar.Right, _valueLabel.Right)) + 2;
                int bottom = Math.Max(_trackBar.Bottom, _valueLabel.Bottom) + 2;
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
                // Entra no modo de edição
                _originalValue = _trackBar.Value;
                _isEditing = true;
                StateChanged?.Invoke();
                return true;
            }

            // Confirma o novo valor e sai do modo de edição
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
                // No modo lista, direções horizontais não agem no slider
                return false;
            }

            if (direction == NavigationDirection.Left || direction == NavigationDirection.Right)
            {
                int step = CalculateStep(repeatCount);
                int delta = direction == NavigationDirection.Right ? step : -step;
                int target = Math.Max(_trackBar.Minimum, Math.Min(_trackBar.Maximum, _trackBar.Value + delta));

                if (target != _trackBar.Value)
                {
                    _trackBar.Value = target;
                    _valueLabel.Text = target.ToString();
                    _onChanged?.Invoke(target);
                    StateChanged?.Invoke();
                }
                return true;
            }

            // D-Pad vertical consumido sem ação no modo de edição
            return true;
        }

        private int CalculateStep(int repeatCount)
        {
            int range = _trackBar.Maximum - _trackBar.Minimum;
            if (repeatCount > 25 && range >= 50) return 5;
            if (repeatCount > 10 && range >= 20) return 2;
            return 1;
        }

        private void CancelEdit()
        {
            _isEditing = false;
            if (_trackBar.Value != _originalValue)
            {
                _trackBar.Value = _originalValue;
                _valueLabel.Text = _originalValue.ToString();
                _onChanged?.Invoke(_originalValue);
            }
            StateChanged?.Invoke();
        }
    }
}
