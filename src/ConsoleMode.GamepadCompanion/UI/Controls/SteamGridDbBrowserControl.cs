using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.Engine.Services;

namespace ConsoleMode.GamepadCompanion.UI.Controls
{
    public enum SteamGridBrowserMode
    {
        GameList,
        CoverGallery
    }

    /// <summary>
    /// Controle Couch Gaming para visualização e seleção em 2 etapas do SteamGridDB:
    /// Tela 1: Lista vertical instantânea dos jogos encontrados pelo autocomplete.
    /// Tela 2: Grade de cards com capas verticais 600x900 disponíveis para download.
    /// Suporta navegação espacial por Gamepad e clique único de Mouse.
    /// </summary>
    public sealed class SteamGridDbBrowserControl : UserControl
    {
        private static readonly Color BgColor = Color.FromArgb(24, 26, 32);
        private static readonly Color HeaderBg = Color.FromArgb(30, 32, 40);
        private static readonly Color CardBg = Color.FromArgb(34, 37, 46);
        private static readonly Color CardHoverBg = Color.FromArgb(45, 60, 88);
        private static readonly Color BorderColor = Color.FromArgb(60, 66, 80);
        private static readonly Color BorderFocused = Color.FromArgb(120, 190, 255);
        private static readonly Color TextPrimary = Color.FromArgb(235, 238, 245);
        private static readonly Color TextMuted = Color.FromArgb(150, 160, 175);
        private static readonly Color TagBg = Color.FromArgb(48, 54, 70);

        private readonly Label _titleLabel = new Label();
        private readonly Label _legendLabel = new Label();
        private readonly Panel _headerPanel = new Panel();
        private readonly Panel _contentPanel = new Panel();
        private string _statusMessage = null;
        private bool _isLoading;

        private readonly SteamGridDbService _service;
        private string _apiKey;
        private string _currentSearchTerm;
        private SteamGridBrowserMode _mode = SteamGridBrowserMode.GameList;

        // Dados
        private readonly List<SteamGridGame> _games = new List<SteamGridGame>();
        private readonly List<SteamGridAsset> _covers = new List<SteamGridAsset>();
        private readonly Dictionary<int, Image> _thumbnailCache = new Dictionary<int, Image>();
        private SteamGridGame _selectedGame;

        // Foco e Navegação
        private int _focusedGameIndex;
        private int _focusedCoverIndex;
        private const int CoverCardWidth = 140;
        private const int CoverCardHeight = 210;
        private const int CoverCardMargin = 16;

        // Debounce Gamepad
        private bool _lastUp;
        private bool _lastDown;
        private bool _lastLeft;
        private bool _lastRight;
        private bool _lastA;
        private bool _lastB;
        private bool _lastX;
        private bool _lastY;

        private readonly Button _btnChangeApiKey = new Button();

        public event Action<string> CoverSelected;
        public event Action BackToConfigRequested;
        public event Action NewSearchRequested;
        public event Action ChangeApiKeyRequested;

        public SteamGridDbBrowserControl(SteamGridDbService service = null)
        {
            _service = service ?? new SteamGridDbService();

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer,
                true);

            BackColor = BgColor;
            Visible = false;
            BuildLayout();
        }

        public SteamGridBrowserMode CurrentMode => _mode;
        public int FocusedGameIndex => _focusedGameIndex;
        public int FocusedCoverIndex => _focusedCoverIndex;
        public int ContentScrollY => _contentPanel.VerticalScroll.Value;

        private void BuildLayout()
        {
            // Cabeçalho
            _headerPanel.Dock = DockStyle.Top;
            _headerPanel.Height = 56;
            _headerPanel.BackColor = HeaderBg;
            _headerPanel.Padding = new Padding(20, 8, 20, 8);

            _titleLabel.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            _titleLabel.ForeColor = TextPrimary;
            _titleLabel.AutoSize = true;
            _titleLabel.Location = new Point(20, 10);

            _legendLabel.Font = new Font("Segoe UI", 9f);
            _legendLabel.ForeColor = TextMuted;
            _legendLabel.AutoSize = true;
            _legendLabel.Location = new Point(20, 34);

            _btnChangeApiKey.Text = Strings.BtnChangeApiKey;
            _btnChangeApiKey.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            _btnChangeApiKey.FlatStyle = FlatStyle.Flat;
            _btnChangeApiKey.FlatAppearance.BorderColor = BorderColor;
            _btnChangeApiKey.BackColor = Color.FromArgb(44, 48, 62);
            _btnChangeApiKey.ForeColor = TextPrimary;
            _btnChangeApiKey.Cursor = Cursors.Hand;
            _btnChangeApiKey.SetBounds(Math.Max(20, _headerPanel.Width - 180), 12, 160, 32);
            _btnChangeApiKey.Click += (s, e) => ChangeApiKeyRequested?.Invoke();

            _headerPanel.Resize += (s, e) =>
            {
                _btnChangeApiKey.SetBounds(Math.Max(20, _headerPanel.ClientSize.Width - 180), 12, 160, 32);
            };

            _headerPanel.Controls.Add(_titleLabel);
            _headerPanel.Controls.Add(_legendLabel);
            _headerPanel.Controls.Add(_btnChangeApiKey);

            // Painel de Conteúdo
            _contentPanel.Dock = DockStyle.Fill;
            _contentPanel.AutoScroll = true;
            _contentPanel.BackColor = BgColor;
            _contentPanel.Paint += (s, e) => OnContentPaint(e);
            _contentPanel.MouseDown += (s, e) => OnContentMouseDown(e);
            _contentPanel.MouseMove += (s, e) => OnContentMouseMove(e);
            _contentPanel.Resize += (s, e) =>
            {
                UpdateScrollSize();
                _contentPanel.Invalidate();
            };
            _contentPanel.Scroll += (s, e) => _contentPanel.Invalidate();
            _contentPanel.MouseWheel += (s, e) => _contentPanel.Invalidate();

            Controls.Add(_contentPanel);
            Controls.Add(_headerPanel);
        }

        private void RunOnUi(Action action)
        {
            if (InvokeRequired && IsHandleCreated)
            {
                BeginInvoke(action);
            }
            else
            {
                action();
            }
        }

        public async Task StartSearchAsync(string term, string apiKey)
        {
            _currentSearchTerm = term?.Trim() ?? string.Empty;
            _apiKey = apiKey;
            _mode = SteamGridBrowserMode.GameList;
            _focusedGameIndex = 0;
            _games.Clear();
            _covers.Clear();
            ClearThumbnailCache();
            _contentPanel.AutoScrollPosition = new Point(0, 0);

            _isLoading = true;
            ShowStatusMessage("Buscando jogos no SteamGridDB...");
            UpdateScrollSize();
            UpdateHeader();

            var results = await _service.SearchGamesAsync(_currentSearchTerm, _apiKey).ConfigureAwait(false);

            RunOnUi(() =>
            {
                _isLoading = false;
                _games.Clear();
                if (results != null)
                {
                    _games.AddRange(results);
                }

                if (_games.Count == 0)
                {
                    ShowStatusMessage(Strings.SteamGridNoGamesFound);
                }
                else
                {
                    HideStatusMessage();
                }

                UpdateScrollSize();
                UpdateHeader();
            });
        }

        public async Task SelectGameAsync(SteamGridGame game)
        {
            if (game == null) return;

            _selectedGame = game;
            _mode = SteamGridBrowserMode.CoverGallery;
            _focusedCoverIndex = 0;
            _covers.Clear();
            ClearThumbnailCache();
            _contentPanel.AutoScrollPosition = new Point(0, 0);

            _isLoading = true;
            ShowStatusMessage($"Buscando capas verticais para '{game.name}'...");
            UpdateScrollSize();
            UpdateHeader();

            var assets = await _service.GetGameGridsAsync(game.id, _apiKey).ConfigureAwait(false);

            RunOnUi(() =>
            {
                _isLoading = false;
                _covers.Clear();
                if (assets != null)
                {
                    _covers.AddRange(assets);
                }

                if (_covers.Count == 0)
                {
                    ShowStatusMessage(Strings.SteamGridNoCoversFound);
                }
                else
                {
                    HideStatusMessage();
                    _ = LoadThumbnailsAsync();
                }

                UpdateScrollSize();
                UpdateHeader();
            });
        }

        private async Task LoadThumbnailsAsync()
        {
            foreach (var cover in _covers)
            {
                if (string.IsNullOrWhiteSpace(cover.thumb) || _thumbnailCache.ContainsKey(cover.id))
                    continue;

                try
                {
                    var bytes = await _service.GetByteArrayAsync(cover.thumb).ConfigureAwait(false);
                    if (bytes != null && bytes.Length > 0)
                    {
                        using (var ms = new MemoryStream(bytes))
                        {
                            var img = Image.FromStream(ms);
                            var copy = new Bitmap(img);
                            _thumbnailCache[cover.id] = copy;
                        }

                        if (_contentPanel.IsHandleCreated)
                        {
                            _contentPanel.BeginInvoke((Action)(() => _contentPanel.Invalidate()));
                        }
                    }
                }
                catch
                {
                    // Se falhar o thumb, continua sem travar
                }
            }
        }

        private async Task SelectCoverAsync(SteamGridAsset cover)
        {
            if (cover == null || string.IsNullOrWhiteSpace(cover.url)) return;

            _isLoading = true;
            ShowStatusMessage(Strings.SteamGridDownloading);

            try
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string coversDir = Path.Combine(appDir, "covers");
                if (!Directory.Exists(coversDir))
                {
                    Directory.CreateDirectory(coversDir);
                }

                string ext = cover.mime == "image/jpeg" ? ".jpg" : ".png";
                string destFile = Path.Combine(coversDir, $"steamgrid_{_selectedGame.id}_{cover.id}{ext}");

                string savedPath = await _service.DownloadCoverAsync(cover.url, destFile).ConfigureAwait(false);

                RunOnUi(() =>
                {
                    _isLoading = false;
                    CoverSelected?.Invoke(savedPath);
                });
            }
            catch (Exception ex)
            {
                RunOnUi(() =>
                {
                    _isLoading = false;
                    ShowStatusMessage($"Erro ao baixar capa: {ex.Message}\nPressione B para voltar.");
                });
            }
        }

        private void UpdateScrollSize()
        {
            if (_mode == SteamGridBrowserMode.GameList)
            {
                int totalH = 16 + _games.Count * (56 + 8) + 16;
                _contentPanel.AutoScrollMinSize = new Size(0, totalH);
            }
            else
            {
                int columns = Math.Max(1, (_contentPanel.ClientSize.Width - 40) / (CoverCardWidth + CoverCardMargin));
                int rows = (_covers.Count + columns - 1) / columns;
                int totalH = 16 + rows * (CoverCardHeight + CoverCardMargin) + 16;
                _contentPanel.AutoScrollMinSize = new Size(0, totalH);
            }
        }

        private void UpdateHeader()
        {
            if (_mode == SteamGridBrowserMode.GameList)
            {
                _titleLabel.Text = string.Format(Strings.SteamGridResultsTitle, _currentSearchTerm);
                if (_games.Count == 0)
                {
                    _legendLabel.Text = "[X] Nova Pesquisa    [Y] Trocar Chave    [B] Voltar";
                }
                else
                {
                    _legendLabel.Text = Strings.SteamGridLegendGames;
                }
            }
            else
            {
                _titleLabel.Text = string.Format(Strings.SteamGridCoversTitle, _selectedGame?.name ?? string.Empty);
                if (_covers.Count == 0)
                {
                    _legendLabel.Text = "[B] Voltar aos Jogos";
                }
                else
                {
                    _legendLabel.Text = Strings.SteamGridLegendCovers;
                }
            }
        }

        private void ShowStatusMessage(string message)
        {
            _statusMessage = message;
            _contentPanel.Invalidate();
        }

        private void HideStatusMessage()
        {
            _statusMessage = null;
            _contentPanel.Invalidate();
        }

        private void ClearThumbnailCache()
        {
            foreach (var kvp in _thumbnailCache)
            {
                kvp.Value?.Dispose();
            }
            _thumbnailCache.Clear();
        }

        private void OnContentPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (!string.IsNullOrEmpty(_statusMessage))
            {
                using (var brush = new SolidBrush(TextMuted))
                using (var font = new Font("Segoe UI", 11f))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(_statusMessage, font, brush, _contentPanel.ClientRectangle, sf);
                }
                return;
            }

            if (_mode == SteamGridBrowserMode.GameList)
            {
                DrawGameList(g);
            }
            else
            {
                DrawCoverGallery(g);
            }
        }

        private void DrawGameList(Graphics g)
        {
            int startY = 16 - _contentPanel.VerticalScroll.Value;
            int itemHeight = 56;
            int itemWidth = Math.Max(400, _contentPanel.ClientSize.Width - 40);

            for (int i = 0; i < _games.Count; i++)
            {
                var game = _games[i];
                int itemY = startY + i * (itemHeight + 8);

                var itemRect = new Rectangle(20, itemY, itemWidth, itemHeight);
                bool isFocused = i == _focusedGameIndex;

                using (var brush = new SolidBrush(isFocused ? CardHoverBg : CardBg))
                {
                    g.FillRectangle(brush, itemRect);
                }

                using (var pen = new Pen(isFocused ? BorderFocused : BorderColor, isFocused ? 2 : 1))
                {
                    g.DrawRectangle(pen, itemRect);
                }

                // Número
                string numText = $"{i + 1}.";
                using (var font = new Font("Segoe UI", 10f, FontStyle.Bold))
                using (var brush = new SolidBrush(isFocused ? BorderFocused : TextMuted))
                {
                    g.DrawString(numText, font, brush, itemRect.X + 14, itemRect.Y + 18);
                }

                // Nome do Jogo
                using (var font = new Font("Segoe UI", 10.5f, FontStyle.Bold))
                using (var brush = new SolidBrush(TextPrimary))
                {
                    g.DrawString(game.name, font, brush, itemRect.X + 44, itemRect.Y + 16);
                }

                // Tags de plataforma
                if (game.types != null && game.types.Length > 0)
                {
                    string tags = string.Join(" • ", game.types).ToUpperInvariant();
                    using (var font = new Font("Segoe UI", 8f))
                    using (var brush = new SolidBrush(TextMuted))
                    {
                        var size = g.MeasureString(tags, font);
                        g.DrawString(tags, font, brush, itemRect.Right - size.Width - 16, itemRect.Y + 19);
                    }
                }
            }
        }

        private void DrawCoverGallery(Graphics g)
        {
            int startX = 20 - _contentPanel.HorizontalScroll.Value;
            int startY = 16 - _contentPanel.VerticalScroll.Value;

            int columns = Math.Max(1, (_contentPanel.ClientSize.Width - 40) / (CoverCardWidth + CoverCardMargin));

            for (int i = 0; i < _covers.Count; i++)
            {
                var cover = _covers[i];
                int col = i % columns;
                int row = i / columns;

                int x = startX + col * (CoverCardWidth + CoverCardMargin);
                int y = startY + row * (CoverCardHeight + CoverCardMargin);

                var cardRect = new Rectangle(x, y, CoverCardWidth, CoverCardHeight);
                bool isFocused = i == _focusedCoverIndex;

                // Fundo da capa
                using (var brush = new SolidBrush(CardBg))
                {
                    g.FillRectangle(brush, cardRect);
                }

                // Renderiza miniatura se já carregada
                if (_thumbnailCache.TryGetValue(cover.id, out var thumbImg) && thumbImg != null)
                {
                    g.DrawImage(thumbImg, cardRect);
                }
                else
                {
                    // Placeholder visual
                    using (var font = new Font("Segoe UI", 8.5f))
                    using (var brush = new SolidBrush(TextMuted))
                    {
                        string placeholder = "Carregando...";
                        var size = g.MeasureString(placeholder, font);
                        g.DrawString(placeholder, font, brush, cardRect.X + (cardRect.Width - size.Width) / 2, cardRect.Y + (cardRect.Height - size.Height) / 2);
                    }
                }

                // Borda e Destaque
                using (var pen = new Pen(isFocused ? BorderFocused : BorderColor, isFocused ? 3 : 1))
                {
                    g.DrawRectangle(pen, cardRect);
                }

                // Badge no rodapé se focado: [A] APLICAR
                if (isFocused)
                {
                    var badgeRect = new Rectangle(cardRect.X, cardRect.Bottom - 26, cardRect.Width, 26);
                    using (var brush = new SolidBrush(Color.FromArgb(220, 32, 90, 160)))
                    {
                        g.FillRectangle(brush, badgeRect);
                    }

                    using (var font = new Font("Segoe UI", 8.5f, FontStyle.Bold))
                    using (var brush = new SolidBrush(Color.White))
                    {
                        string badge = Strings.DialogConfirm;
                        var size = g.MeasureString(badge, font);
                        g.DrawString(badge, font, brush, badgeRect.X + (badgeRect.Width - size.Width) / 2, badgeRect.Y + 4);
                    }
                }
            }
        }

        private void OnContentMouseMove(MouseEventArgs e)
        {
            if (!string.IsNullOrEmpty(_statusMessage)) return;

            if (_mode == SteamGridBrowserMode.GameList)
            {
                int startY = 16 - _contentPanel.VerticalScroll.Value;
                int itemHeight = 56;
                int clickedIndex = (e.Y - startY) / (itemHeight + 8);
                if (clickedIndex >= 0 && clickedIndex < _games.Count && clickedIndex != _focusedGameIndex)
                {
                    _focusedGameIndex = clickedIndex;
                    _contentPanel.Invalidate();
                }
            }
            else
            {
                int startX = 20 - _contentPanel.HorizontalScroll.Value;
                int startY = 16 - _contentPanel.VerticalScroll.Value;
                int columns = Math.Max(1, (_contentPanel.ClientSize.Width - 40) / (CoverCardWidth + CoverCardMargin));

                int col = (e.X - startX) / (CoverCardWidth + CoverCardMargin);
                int row = (e.Y - startY) / (CoverCardHeight + CoverCardMargin);

                if (col >= 0 && col < columns && row >= 0)
                {
                    int index = row * columns + col;
                    if (index >= 0 && index < _covers.Count && index != _focusedCoverIndex)
                    {
                        _focusedCoverIndex = index;
                        _contentPanel.Invalidate();
                    }
                }
            }
        }

        private void OnContentMouseDown(MouseEventArgs e)
        {
            if (!string.IsNullOrEmpty(_statusMessage) || e.Button != MouseButtons.Left) return;

            if (_mode == SteamGridBrowserMode.GameList)
            {
                int startY = 16 - _contentPanel.VerticalScroll.Value;
                int itemHeight = 56;
                int clickedIndex = (e.Y - startY) / (itemHeight + 8);
                if (clickedIndex >= 0 && clickedIndex < _games.Count)
                {
                    _focusedGameIndex = clickedIndex;
                    _ = SelectGameAsync(_games[clickedIndex]);
                }
            }
            else
            {
                int startX = 20 - _contentPanel.HorizontalScroll.Value;
                int startY = 16 - _contentPanel.VerticalScroll.Value;
                int columns = Math.Max(1, (_contentPanel.ClientSize.Width - 40) / (CoverCardWidth + CoverCardMargin));

                int col = (e.X - startX) / (CoverCardWidth + CoverCardMargin);
                int row = (e.Y - startY) / (CoverCardHeight + CoverCardMargin);

                if (col >= 0 && col < columns && row >= 0)
                {
                    int index = row * columns + col;
                    if (index >= 0 && index < _covers.Count)
                    {
                        _focusedCoverIndex = index;
                        _ = SelectCoverAsync(_covers[index]);
                    }
                }
            }
        }

        public void ProcessGamepad(GamepadState state, long nowMs)
        {
            if (!state.IsConnected) return;

            bool up = state.IsPressed(GamepadButtons.DPadUp);
            bool down = state.IsPressed(GamepadButtons.DPadDown);
            bool left = state.IsPressed(GamepadButtons.DPadLeft);
            bool right = state.IsPressed(GamepadButtons.DPadRight);

            if (_mode == SteamGridBrowserMode.GameList)
            {
                if (!_isLoading && _games.Count > 0)
                {
                    if (up && !_lastUp)
                    {
                        if (_focusedGameIndex > 0)
                        {
                            _focusedGameIndex--;
                            EnsureVisibleGame(_focusedGameIndex);
                            _contentPanel.Invalidate();
                        }
                    }

                    if (down && !_lastDown)
                    {
                        if (_focusedGameIndex < _games.Count - 1)
                        {
                            _focusedGameIndex++;
                            EnsureVisibleGame(_focusedGameIndex);
                            _contentPanel.Invalidate();
                        }
                    }

                    // Botão A seleciona o jogo
                    bool a = state.IsPressed(GamepadButtons.A);
                    if (a && !_lastA && _focusedGameIndex >= 0 && _focusedGameIndex < _games.Count)
                    {
                        _ = SelectGameAsync(_games[_focusedGameIndex]);
                    }
                    _lastA = a;
                }
                else
                {
                    _lastA = state.IsPressed(GamepadButtons.A);
                }
                _lastUp = up;
                _lastDown = down;

                // Botão X pede nova busca
                bool x = state.IsPressed(GamepadButtons.X);
                if (x && !_lastX)
                {
                    NewSearchRequested?.Invoke();
                }
                _lastX = x;

                // Botão Y troca chave API
                bool y = state.IsPressed(GamepadButtons.Y);
                if (y && !_lastY)
                {
                    ChangeApiKeyRequested?.Invoke();
                }
                _lastY = y;

                // Botão B volta para a configuração
                bool b = state.IsPressed(GamepadButtons.B);
                if (b && !_lastB)
                {
                    BackToConfigRequested?.Invoke();
                }
                _lastB = b;
            }
            else // CoverGallery
            {
                int columns = Math.Max(1, (_contentPanel.ClientSize.Width - 40) / (CoverCardWidth + CoverCardMargin));

                if (!_isLoading && _covers.Count > 0)
                {
                    if (left && !_lastLeft)
                    {
                        if (_focusedCoverIndex > 0)
                        {
                            _focusedCoverIndex--;
                            EnsureVisibleCover(_focusedCoverIndex);
                            _contentPanel.Invalidate();
                        }
                    }

                    if (right && !_lastRight)
                    {
                        if (_focusedCoverIndex < _covers.Count - 1)
                        {
                            _focusedCoverIndex++;
                            EnsureVisibleCover(_focusedCoverIndex);
                            _contentPanel.Invalidate();
                        }
                    }

                    if (up && !_lastUp)
                    {
                        if (_focusedCoverIndex - columns >= 0)
                        {
                            _focusedCoverIndex -= columns;
                            EnsureVisibleCover(_focusedCoverIndex);
                            _contentPanel.Invalidate();
                        }
                    }

                    if (down && !_lastDown)
                    {
                        if (_focusedCoverIndex + columns < _covers.Count)
                        {
                            _focusedCoverIndex += columns;
                            EnsureVisibleCover(_focusedCoverIndex);
                            _contentPanel.Invalidate();
                        }
                        else if (_focusedCoverIndex < _covers.Count - 1)
                        {
                            _focusedCoverIndex = _covers.Count - 1;
                            EnsureVisibleCover(_focusedCoverIndex);
                            _contentPanel.Invalidate();
                        }
                    }

                    // Botão A seleciona a capa
                    bool a = state.IsPressed(GamepadButtons.A);
                    if (a && !_lastA && _focusedCoverIndex >= 0 && _focusedCoverIndex < _covers.Count)
                    {
                        _ = SelectCoverAsync(_covers[_focusedCoverIndex]);
                    }
                    _lastA = a;
                }
                else
                {
                    _lastA = state.IsPressed(GamepadButtons.A);
                }
                _lastLeft = left;
                _lastRight = right;
                _lastUp = up;
                _lastDown = down;

                // Botão B volta para a Tela 1 (Lista de Jogos)
                bool b = state.IsPressed(GamepadButtons.B);
                if (b && !_lastB)
                {
                    _mode = SteamGridBrowserMode.GameList;
                    _isLoading = false;
                    if (_games.Count == 0)
                    {
                        ShowStatusMessage(Strings.SteamGridNoGamesFound);
                    }
                    else
                    {
                        HideStatusMessage();
                    }
                    UpdateScrollSize();
                    UpdateHeader();
                    _contentPanel.AutoScrollPosition = new Point(0, 0);
                    _contentPanel.Invalidate();
                }
                _lastB = b;
            }
        }

        private void EnsureVisibleGame(int index)
        {
            if (_games.Count == 0 || index < 0 || index >= _games.Count) return;

            int itemHeight = 56;
            int itemY = 16 + index * (itemHeight + 8);

            int currentScroll = _contentPanel.VerticalScroll.Value;
            int viewportHeight = Math.Max(1, _contentPanel.ClientSize.Height);

            if (itemY < currentScroll)
            {
                int targetY = Math.Max(0, itemY - 16);
                _contentPanel.AutoScrollPosition = new Point(0, targetY);
            }
            else if (itemY + itemHeight > currentScroll + viewportHeight)
            {
                int targetY = itemY + itemHeight - viewportHeight + 16;
                _contentPanel.AutoScrollPosition = new Point(0, Math.Max(0, targetY));
            }
        }

        private void EnsureVisibleCover(int index)
        {
            if (_covers.Count == 0 || index < 0 || index >= _covers.Count) return;

            int columns = Math.Max(1, (_contentPanel.ClientSize.Width - 40) / (CoverCardWidth + CoverCardMargin));
            int row = index / columns;
            int cardY = 16 + row * (CoverCardHeight + CoverCardMargin);
            int cardHeight = CoverCardHeight;

            int currentScroll = _contentPanel.VerticalScroll.Value;
            int viewportHeight = Math.Max(1, _contentPanel.ClientSize.Height);

            if (cardY < currentScroll)
            {
                int targetY = Math.Max(0, cardY - 16);
                _contentPanel.AutoScrollPosition = new Point(0, targetY);
            }
            else if (cardY + cardHeight > currentScroll + viewportHeight)
            {
                int targetY = cardY + cardHeight - viewportHeight + 16;
                _contentPanel.AutoScrollPosition = new Point(0, Math.Max(0, targetY));
            }
        }

        public void ResetInputState()
        {
            _lastUp = false;
            _lastDown = false;
            _lastLeft = false;
            _lastRight = false;
            _lastA = false;
            _lastB = false;
            _lastX = false;
            _lastY = false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ClearThumbnailCache();
            }
            base.Dispose(disposing);
        }
    }
}
