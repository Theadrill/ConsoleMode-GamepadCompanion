using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.Hardware;

namespace ConsoleMode.GamepadCompanion.UI.Controls
{
    /// <summary>
    /// Componente de Grade de Capas de Jogos com navegação espacial 2D por D-Pad,
    /// rolagem suave automática e suporte a múltiplos jogos + slot de adição [+].
    /// </summary>
    internal sealed class GamesGridControl : UserControl
    {
        private static readonly Color BgColor = Color.FromArgb(24, 26, 32);
        private static readonly Color HeaderBg = Color.FromArgb(30, 32, 40);
        private static readonly Color TextPrimary = Color.FromArgb(230, 232, 240);
        private static readonly Color TextSecondary = Color.FromArgb(160, 170, 185);

        private readonly Panel _headerPanel = new Panel();
        private readonly FlowLayoutPanel _cardsContainer = new FlowLayoutPanel();
        private readonly Label _titleLabel = new Label();
        private readonly Label _hintsLabel = new Label();

        private readonly List<GameCoverCard> _cards = new List<GameCoverCard>();
        private int _focusedIndex = 0;

        // Controle de debounce / repetição do gamepad
        private bool _lastDpadUp;
        private bool _lastDpadDown;
        private bool _lastDpadLeft;
        private bool _lastDpadRight;
        private bool _lastBtnA;
        private bool _lastBtnB;
        private bool _lastBtnY;
        private long _lastMoveTime;
        private int _repeatCount;

        public event Action<GameEntry> LaunchRequested;
        public event Action<GameEntry> ConfigureRequested;
        public event Action AddRequested;
        public event Action BackRequested;

        public GamesGridControl()
        {
            BackColor = BgColor;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            BuildLayout();
        }

        public int FocusedIndex => _focusedIndex;
        public int TotalCards => _cards.Count;

        private void BuildLayout()
        {
            _headerPanel.Dock = DockStyle.Top;
            _headerPanel.Height = 44;
            _headerPanel.BackColor = HeaderBg;
            _headerPanel.Padding = new Padding(16, 8, 16, 8);

            _titleLabel.Text = Strings.GamesLibraryTitle;
            _titleLabel.Font = new Font("Segoe UI", 11.5f, FontStyle.Bold);
            _titleLabel.ForeColor = TextPrimary;
            _titleLabel.AutoSize = true;
            _titleLabel.Location = new Point(16, 11);

            _hintsLabel.Text = $"{Strings.ActionLaunch}    {Strings.ActionConfigure}    {Strings.NavHintBack}";
            _hintsLabel.Font = new Font("Segoe UI", 8.5f);
            _hintsLabel.ForeColor = TextSecondary;
            _hintsLabel.AutoSize = true;
            _hintsLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _hintsLabel.Location = new Point(Width - 280, 14);

            _headerPanel.Controls.Add(_titleLabel);
            _headerPanel.Controls.Add(_hintsLabel);

            _cardsContainer.Dock = DockStyle.Fill;
            _cardsContainer.AutoScroll = true;
            _cardsContainer.BackColor = BgColor;
            _cardsContainer.Padding = new Padding(16);
            _cardsContainer.WrapContents = true;

            Controls.Add(_cardsContainer);
            Controls.Add(_headerPanel);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_hintsLabel != null)
            {
                _hintsLabel.Location = new Point(Math.Max(200, Width - _hintsLabel.Width - 16), 14);
            }
        }

        public void LoadGames(IEnumerable<GameEntry> games)
        {
            _cardsContainer.SuspendLayout();
            try
            {
                // Limpa cards anteriores
                foreach (var card in _cards)
                {
                    card.Dispose();
                }
                _cards.Clear();
                _cardsContainer.Controls.Clear();

                if (games != null)
                {
                    foreach (var game in games)
                    {
                        var card = new GameCoverCard(game);
                        card.Margin = new Padding(10);
                        card.Click += (s, e) => OnCardClicked(card);
                        card.DoubleClick += (s, e) => OnCardDoubleClicked(card);
                        _cards.Add(card);
                        _cardsContainer.Controls.Add(card);
                    }
                }

                // Slot final [+] Adicionar Novo Jogo
                var addCard = new GameCoverCard(null);
                addCard.Margin = new Padding(10);
                addCard.Click += (s, e) => OnCardClicked(addCard);
                _cards.Add(addCard);
                _cardsContainer.Controls.Add(addCard);

                // Garante que o foco fique dentro dos limites
                if (_focusedIndex >= _cards.Count)
                {
                    _focusedIndex = Math.Max(0, _cards.Count - 1);
                }
                UpdateCardFocus();
            }
            finally
            {
                _cardsContainer.ResumeLayout(true);
            }
        }

        private void OnCardClicked(GameCoverCard card)
        {
            int idx = _cards.IndexOf(card);
            if (idx >= 0)
            {
                SetFocusedIndex(idx);
            }
        }

        private void OnCardDoubleClicked(GameCoverCard card)
        {
            if (card.IsAddSlot)
            {
                AddRequested?.Invoke();
            }
            else
            {
                LaunchRequested?.Invoke(card.Game);
            }
        }

        public void SetFocusedIndex(int index)
        {
            if (_cards.Count == 0) return;
            _focusedIndex = Math.Max(0, Math.Min(_cards.Count - 1, index));
            UpdateCardFocus();
        }

        private void UpdateCardFocus()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                _cards[i].IsFocusedCard = (i == _focusedIndex);
            }

            if (_focusedIndex >= 0 && _focusedIndex < _cards.Count)
            {
                _cardsContainer.ScrollControlIntoView(_cards[_focusedIndex]);
            }
        }

        private int GetColumnsCount()
        {
            if (_cards.Count == 0) return 1;
            // Largura do card (160) + margem horizontal (20) = 180
            int availableWidth = Math.Max(180, _cardsContainer.ClientSize.Width - 32);
            int cols = availableWidth / 180;
            return Math.Max(1, cols);
        }

        public void ProcessGamepad(GamepadState state, long currentTimeMs)
        {
            if (_cards.Count == 0) return;

            bool dpadUp = state.IsPressed(GamepadButtons.DPadUp);
            bool dpadDown = state.IsPressed(GamepadButtons.DPadDown);
            bool dpadLeft = state.IsPressed(GamepadButtons.DPadLeft);
            bool dpadRight = state.IsPressed(GamepadButtons.DPadRight);
            bool btnA = state.IsPressed(GamepadButtons.A);
            bool btnB = state.IsPressed(GamepadButtons.B);
            bool btnY = state.IsPressed(GamepadButtons.Y);

            // Navegação direcional
            bool isMoving = dpadUp || dpadDown || dpadLeft || dpadRight;
            if (isMoving)
            {
                bool isInitial = (!_lastDpadUp && dpadUp) ||
                                 (!_lastDpadDown && dpadDown) ||
                                 (!_lastDpadLeft && dpadLeft) ||
                                 (!_lastDpadRight && dpadRight);

                int interval = _repeatCount == 0 ? 250 : (_repeatCount < 4 ? 140 : 80);

                if (isInitial || (currentTimeMs - _lastMoveTime >= interval))
                {
                    int cols = GetColumnsCount();
                    if (dpadLeft) MoveFocus(-1);
                    else if (dpadRight) MoveFocus(1);
                    else if (dpadUp) MoveFocus(-cols);
                    else if (dpadDown) MoveFocus(cols);

                    _lastMoveTime = currentTimeMs;
                    _repeatCount = isInitial ? 0 : _repeatCount + 1;
                }
            }
            else
            {
                _repeatCount = 0;
            }

            // Botão A (Confirmar / Iniciar / Adicionar)
            if (btnA && !_lastBtnA)
            {
                TriggerPrimaryAction();
            }

            // Botão Y (Configurar jogo focado)
            if (btnY && !_lastBtnY)
            {
                TriggerConfigureAction();
            }

            // Botão B (Voltar)
            if (btnB && !_lastBtnB)
            {
                BackRequested?.Invoke();
            }

            _lastDpadUp = dpadUp;
            _lastDpadDown = dpadDown;
            _lastDpadLeft = dpadLeft;
            _lastDpadRight = dpadRight;
            _lastBtnA = btnA;
            _lastBtnB = btnB;
            _lastBtnY = btnY;
        }

        private void MoveFocus(int delta)
        {
            if (_cards.Count == 0) return;
            int newIdx = Math.Max(0, Math.Min(_cards.Count - 1, _focusedIndex + delta));
            if (newIdx != _focusedIndex)
            {
                _focusedIndex = newIdx;
                UpdateCardFocus();
            }
        }

        private void TriggerPrimaryAction()
        {
            if (_focusedIndex < 0 || _focusedIndex >= _cards.Count) return;

            var card = _cards[_focusedIndex];
            if (card.IsAddSlot)
            {
                AddRequested?.Invoke();
            }
            else
            {
                LaunchRequested?.Invoke(card.Game);
            }
        }

        private void TriggerConfigureAction()
        {
            if (_focusedIndex < 0 || _focusedIndex >= _cards.Count) return;

            var card = _cards[_focusedIndex];
            if (!card.IsAddSlot)
            {
                ConfigureRequested?.Invoke(card.Game);
            }
        }
    }
}
