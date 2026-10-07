using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.Engine.Search;

namespace ConsoleMode.GamepadCompanion.UI.Controls
{
    /// <summary>
    /// Componente de Grade de Capas de Jogos com navegação espacial 2D por D-Pad,
    /// rolagem suave automática, Barra de Pesquisa Fuzzy e estado de resultado vazio.
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
        private readonly SearchBarControl _searchBar = new SearchBarControl();

        private readonly Panel _emptyStatePanel = new Panel();
        private readonly Panel _emptyIconPanel = new Panel();
        private readonly Label _emptyTitleLabel = new Label();
        private readonly Label _emptyDescLabel = new Label();

        private readonly List<GameEntry> _allGames = new List<GameEntry>();
        private readonly List<GameCoverCard> _cards = new List<GameCoverCard>();
        private string _currentQuery = string.Empty;
        private int _focusedIndex = 0;
        private bool _isSearchFocused;

        // Controle de transição e supressão de disparo contínuo entre busca e grade
        private bool _suppressDownUntilRelease;
        private bool _suppressUpUntilRelease;

        // Controle de debounce / repetição do gamepad
        private bool _lastDpadUp;
        private bool _lastDpadDown;
        private bool _lastDpadLeft;
        private bool _lastDpadRight;
        private bool _lastBtnA;
        private bool _lastBtnB;
        private bool _lastBtnX;
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
        public bool IsSearchFocused => _isSearchFocused || _searchBar.IsSearchFocused;
        public string SearchText => _searchBar.SearchText;

        public void ResetInputState()
        {
            _lastBtnA = true;
            _lastBtnB = true;
            _lastBtnX = true;
            _lastBtnY = true;
            _lastDpadUp = false;
            _lastDpadDown = false;
            _lastDpadLeft = false;
            _lastDpadRight = false;
            _suppressDownUntilRelease = false;
            _suppressUpUntilRelease = false;
            _repeatCount = 0;
            _lastMoveTime = 0;
        }

        private void BuildLayout()
        {
            // Painel de Cabeçalho (Título + Atalhos + Barra de Pesquisa)
            _headerPanel.Dock = DockStyle.Top;
            _headerPanel.Height = 88;
            _headerPanel.BackColor = HeaderBg;

            _titleLabel.Text = Strings.GamesLibraryTitle;
            _titleLabel.Font = new Font("Segoe UI", 11.5f, FontStyle.Bold);
            _titleLabel.ForeColor = TextPrimary;
            _titleLabel.AutoSize = true;
            _titleLabel.Location = new Point(16, 12);

            _hintsLabel.Text = $"{Strings.ActionLaunch}   {Strings.ActionConfigure}   {Strings.ActionSearch}   {Strings.NavHintBack}";
            _hintsLabel.Font = new Font("Segoe UI", 8.5f);
            _hintsLabel.ForeColor = TextSecondary;
            _hintsLabel.AutoSize = true;
            _hintsLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _hintsLabel.Location = new Point(Math.Max(200, Width - 360), 14);

            _searchBar.Location = new Point(16, 42);
            _searchBar.Width = Width - 32;
            _searchBar.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;

            _searchBar.SearchTextChanged += text =>
            {
                _currentQuery = text ?? string.Empty;
                _focusedIndex = 0;
                ApplyFilter();
            };

            _searchBar.ClearRequested += () =>
            {
                _currentQuery = string.Empty;
                _focusedIndex = 0;
                ApplyFilter();
            };

            _headerPanel.Controls.Add(_titleLabel);
            _headerPanel.Controls.Add(_hintsLabel);
            _headerPanel.Controls.Add(_searchBar);

            // Container de Capas
            _cardsContainer.Dock = DockStyle.Fill;
            _cardsContainer.AutoScroll = true;
            _cardsContainer.BackColor = BgColor;
            _cardsContainer.Padding = new Padding(16);
            _cardsContainer.WrapContents = true;

            BuildEmptyState();

            Controls.Add(_cardsContainer);
            Controls.Add(_headerPanel);
        }

        private void BuildEmptyState()
        {
            _emptyStatePanel.Size = new Size(380, 150);
            _emptyStatePanel.BackColor = Color.Transparent;
            _emptyStatePanel.Visible = false;

            _emptyIconPanel.Size = new Size(380, 44);
            _emptyIconPanel.BackColor = Color.Transparent;
            _emptyIconPanel.Location = new Point(0, 8);
            _emptyIconPanel.Paint += (s, pe) =>
            {
                var g = pe.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int cx = _emptyIconPanel.Width / 2;
                int cy = 20;
                int r = 12;
                using (var pen = new Pen(Color.FromArgb(90, 96, 110), 2.5f))
                {
                    g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
                    g.DrawLine(pen, cx + 8, cy + 8, cx + 18, cy + 18);
                }
            };

            _emptyTitleLabel.Text = Strings.NoGamesFound;
            _emptyTitleLabel.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            _emptyTitleLabel.ForeColor = TextPrimary;
            _emptyTitleLabel.AutoSize = false;
            _emptyTitleLabel.Size = new Size(380, 26);
            _emptyTitleLabel.TextAlign = ContentAlignment.MiddleCenter;
            _emptyTitleLabel.Location = new Point(0, 58);

            _emptyDescLabel.Text = Strings.PressBToClearSearch;
            _emptyDescLabel.Font = new Font("Segoe UI", 9.5f);
            _emptyDescLabel.ForeColor = TextSecondary;
            _emptyDescLabel.AutoSize = false;
            _emptyDescLabel.Size = new Size(380, 22);
            _emptyDescLabel.TextAlign = ContentAlignment.MiddleCenter;
            _emptyDescLabel.Location = new Point(0, 88);

            _emptyStatePanel.Controls.Add(_emptyIconPanel);
            _emptyStatePanel.Controls.Add(_emptyTitleLabel);
            _emptyStatePanel.Controls.Add(_emptyDescLabel);
        }

        private void PositionEmptyState()
        {
            if (_emptyStatePanel != null && _cardsContainer != null)
            {
                int x = Math.Max(0, (_cardsContainer.ClientSize.Width - _emptyStatePanel.Width) / 2);
                int y = Math.Max(40, (_cardsContainer.ClientSize.Height - _emptyStatePanel.Height) / 3);
                _emptyStatePanel.Location = new Point(x, y);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_hintsLabel != null)
            {
                _hintsLabel.Location = new Point(Math.Max(200, Width - _hintsLabel.Width - 16), 14);
            }
            PositionEmptyState();
        }

        public void LoadGames(IEnumerable<GameEntry> games)
        {
            _allGames.Clear();
            if (games != null)
            {
                _allGames.AddRange(games);
            }
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            _cardsContainer.SuspendLayout();
            try
            {
                foreach (var card in _cards)
                {
                    card.Dispose();
                }
                _cards.Clear();
                _cardsContainer.Controls.Clear();

                IEnumerable<GameEntry> matches;
                if (string.IsNullOrWhiteSpace(_currentQuery))
                {
                    matches = _allGames;
                }
                else
                {
                    matches = FuzzySearchEngine.Filter(_allGames, _currentQuery);
                }

                foreach (var game in matches)
                {
                    var card = new GameCoverCard(game);
                    card.Margin = new Padding(10);
                    card.Click += (s, e) => OnCardClicked(card);
                    card.DoubleClick += (s, e) => OnCardDoubleClicked(card);
                    _cards.Add(card);
                    _cardsContainer.Controls.Add(card);
                }

                // Slot de adição [+] exibido apenas quando não há busca ativa
                if (string.IsNullOrWhiteSpace(_currentQuery))
                {
                    var addCard = new GameCoverCard(null);
                    addCard.Margin = new Padding(10);
                    addCard.Click += (s, e) => OnCardClicked(addCard);
                    _cards.Add(addCard);
                    _cardsContainer.Controls.Add(addCard);
                }

                bool isEmpty = _cards.Count == 0;
                _emptyStatePanel.Visible = isEmpty;
                if (isEmpty)
                {
                    _cardsContainer.Controls.Add(_emptyStatePanel);
                    PositionEmptyState();
                }

                if (_cards.Count > 0)
                {
                    _focusedIndex = Math.Max(0, Math.Min(_cards.Count - 1, _focusedIndex));
                    if (!_isSearchFocused)
                    {
                        UpdateCardFocus();
                    }
                    else
                    {
                        ClearCardsFocus();
                    }
                }
            }
            finally
            {
                _cardsContainer.ResumeLayout(true);
            }
        }

        private void OnCardClicked(GameCoverCard card)
        {
            _isSearchFocused = false;
            _searchBar.BlurInput();
            Focus();
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

        private void ClearCardsFocus()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                _cards[i].IsFocusedCard = false;
            }
        }

        private int GetColumnsCount()
        {
            if (_cards.Count == 0) return 1;
            int availableWidth = Math.Max(180, _cardsContainer.ClientSize.Width - 32);
            int cols = availableWidth / 180;
            return Math.Max(1, cols);
        }

        public void FocusSearch()
        {
            _isSearchFocused = true;
            _searchBar.FocusInput();
            ClearCardsFocus();
        }

        public void ClearSearch()
        {
            _searchBar.ClearSearch();
        }

        public void ProcessGamepad(GamepadState state, long currentTimeMs)
        {
            if (!state.IsConnected) return;

            bool dpadUp = state.IsPressed(GamepadButtons.DPadUp);
            bool dpadDown = state.IsPressed(GamepadButtons.DPadDown);
            bool dpadLeft = state.IsPressed(GamepadButtons.DPadLeft);
            bool dpadRight = state.IsPressed(GamepadButtons.DPadRight);
            bool btnA = state.IsPressed(GamepadButtons.A);
            bool btnB = state.IsPressed(GamepadButtons.B);
            bool btnX = state.IsPressed(GamepadButtons.X);
            bool btnY = state.IsPressed(GamepadButtons.Y);

            // Botão X: foca/alterna para a barra de pesquisa
            if (btnX && !_lastBtnX)
            {
                FocusSearch();
                _lastBtnX = btnX;
                return;
            }
            _lastBtnX = btnX;

            // Se o foco estiver na barra de pesquisa:
            if (_isSearchFocused)
            {
                if (_suppressUpUntilRelease)
                {
                    if (dpadUp) dpadUp = false;
                    else _suppressUpUntilRelease = false;
                }

                // D-Pad Down sai da busca e retorna aos cards da grade
                if (dpadDown && !_lastDpadDown && _cards.Count > 0)
                {
                    _isSearchFocused = false;
                    _searchBar.BlurInput();
                    Focus();
                    _focusedIndex = 0;
                    UpdateCardFocus();

                    _suppressDownUntilRelease = true;
                    _lastMoveTime = currentTimeMs;
                    _repeatCount = 0;
                }

                // Botão B limpa a busca ou fecha o foco
                if (btnB && !_lastBtnB)
                {
                    if (!string.IsNullOrEmpty(_searchBar.SearchText))
                    {
                        _searchBar.ClearSearch();
                    }
                    else
                    {
                        _isSearchFocused = false;
                        _searchBar.BlurInput();
                        Focus();
                        if (_cards.Count > 0)
                        {
                            _focusedIndex = 0;
                            UpdateCardFocus();
                        }
                    }
                }

                _lastDpadUp = dpadUp;
                _lastDpadDown = dpadDown;
                _lastDpadLeft = dpadLeft;
                _lastDpadRight = dpadRight;
                _lastBtnA = btnA;
                _lastBtnB = btnB;
                _lastBtnY = btnY;
                return;
            }

            // Modo grade vazia (nenhum resultado encontrado na busca)
            if (_cards.Count == 0)
            {
                if (btnB && !_lastBtnB)
                {
                    if (!string.IsNullOrEmpty(_searchBar.SearchText))
                    {
                        _searchBar.ClearSearch();
                    }
                    else
                    {
                        BackRequested?.Invoke();
                    }
                }
                _lastBtnB = btnB;
                return;
            }

            // Supressão de D-Pad Down que originou a transição da busca para a grade
            if (_suppressDownUntilRelease)
            {
                if (dpadDown)
                {
                    dpadDown = false;
                }
                else
                {
                    _suppressDownUntilRelease = false;
                }
            }

            // D-Pad Up a partir da linha de topo move o foco para a barra de busca
            int cols = GetColumnsCount();
            if (dpadUp && !_lastDpadUp && _focusedIndex < cols)
            {
                FocusSearch();
                _suppressUpUntilRelease = true;
                _lastDpadUp = dpadUp;
                return;
            }

            // Navegação direcional entre os cards
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

            // Botão B (Voltar ou Limpar busca)
            if (btnB && !_lastBtnB)
            {
                if (!string.IsNullOrEmpty(_searchBar.SearchText))
                {
                    _searchBar.ClearSearch();
                }
                else
                {
                    BackRequested?.Invoke();
                }
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
