using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using ConsoleMode.GamepadCompanion.Core.Interfaces;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.Engine;
using ConsoleMode.GamepadCompanion.Hardware;
using ConsoleMode.GamepadCompanion.UI.Controls;
using ConsoleMode.GamepadCompanion.UI.Navigation;

namespace ConsoleMode.GamepadCompanion.UI
{
    /// <summary>
    /// Janela principal de configurações: apresenta estado, visual debugger, catálogo de jogos (Fase 2)
    /// e navegação nativa por controle com foco couch gaming.
    /// </summary>
    internal sealed class SettingsForm : Form
    {
        private enum ContentViewMode
        {
            Debugger,
            GamesGrid,
            GameConfig,
            SteamGridDb
        }

        private readonly IGamepadService _gamepad;
        private readonly AppSettings _settings;
        private readonly ProfileManager _profileManager;
        private readonly ConfigRepository _configRepo;
        private readonly IWindowTracker _windowTracker;
        private readonly GameRepository _gameRepo;
        private readonly GameLauncher _gameLauncher;

        private readonly Timer _refreshTimer = new Timer { Interval = 16 };
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        private readonly ComboBox _slotCombo = new ComboBox();
        private readonly Label _statusLabel = new Label();
        private readonly Label _profileLabel = new Label();
        private readonly Button _toggleButton = new Button();
        private readonly Button _gamesButton = new Button();
        private readonly GamepadVisualDebugger _debugger = new GamepadVisualDebugger();
        private readonly GamesGridControl _gamesGrid = new GamesGridControl();
        private readonly GameConfigPanel _configPanel = new GameConfigPanel();
        private readonly SteamGridDbBrowserControl _steamGridBrowser = new SteamGridDbBrowserControl();
        private readonly Engine.Services.SteamGridDbService _steamGridService = new Engine.Services.SteamGridDbService();
        private readonly FocusOverlayPanel _focusOverlay = new FocusOverlayPanel();
        private readonly GamepadNavigationManager _navManager = new GamepadNavigationManager();

        private Panel _sidePanel;
        private string _slotSignature = string.Empty;
        private bool _updatingCombo;
        private bool _allowClose;
        private bool _lastStateY;
        private bool _isLaunching;
        private string _lastSteamGridSearchTerm;
        private ContentViewMode _viewMode = ContentViewMode.Debugger;

        public SettingsForm(
            IGamepadService gamepad,
            AppSettings settings,
            ProfileManager profileManager,
            ConfigRepository configRepo,
            IWindowTracker windowTracker = null)
        {
            _gamepad = gamepad;
            _settings = settings;
            _profileManager = profileManager;
            _configRepo = configRepo;
            _windowTracker = windowTracker;
            _gameRepo = new GameRepository();
            _gameLauncher = new GameLauncher(windowTracker);

            BuildLayout();
            RefreshSlotCombo(force: true);
            ApplyToggleVisual();
            RefreshGamesList();

            _refreshTimer.Tick += (s, e) => Refresh_Tick();
            _refreshTimer.Start();
        }

        private void BuildLayout()
        {
            Text = Strings.WindowTitle;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(880, 540);
            MinimumSize = new Size(760, 550);
            BackColor = Color.FromArgb(30, 32, 40);
            ForeColor = Color.FromArgb(230, 232, 240);
            Font = new Font("Segoe UI", 9.5f);

            _sidePanel = new Panel { Dock = DockStyle.Left, Width = 260, Padding = new Padding(16) };
            _sidePanel.Paint += (s, e) => _navManager.DrawFocusHighlight(e.Graphics);

            _navManager.RequestRepaint += () => _sidePanel.Invalidate();
            _navManager.EditingChanged += (ctrl, editing) => OnNavEditingChanged(ctrl, editing);

            int currentY = 16;

            var slotTitle = MakeTitle(Strings.ActiveController, currentY);
            currentY = slotTitle.Bottom + 6;

            _slotCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _slotCombo.SetBounds(16, currentY, 228, 28);
            _slotCombo.SelectedIndexChanged += (s, e) => OnSlotChanged();
            currentY = _slotCombo.Bottom + 14;

            _statusLabel.SetBounds(16, currentY, 228, 18);
            _statusLabel.ForeColor = Color.FromArgb(150, 220, 160);
            currentY = _statusLabel.Bottom + 4;

            _profileLabel.SetBounds(16, currentY, 228, 18);
            _profileLabel.ForeColor = Color.FromArgb(120, 190, 255);
            currentY = _profileLabel.Bottom + 10;

            _toggleButton.SetBounds(16, currentY, 228, 38);
            _toggleButton.FlatStyle = FlatStyle.Flat;
            _toggleButton.FlatAppearance.BorderSize = 1;
            _toggleButton.FlatAppearance.BorderColor = Color.FromArgb(120, 126, 140);
            _toggleButton.Click += (s, e) =>
            {
                _settings.MappingEnabled = !_settings.MappingEnabled;
                _configRepo.Save(_settings);
                ApplyToggleVisual();
            };
            currentY = _toggleButton.Bottom + 14;

            _sidePanel.Controls.AddRange(new Control[] { slotTitle, _slotCombo, _statusLabel, _profileLabel, _toggleButton });

            // Registro sequencial dos controles para navegação D-Pad (topo -> base)
            var slotNav = new DropdownNavigable("SlotCombo", slotTitle, _slotCombo);
            var toggleNav = new ButtonNavigable("ToggleButton", _toggleButton);

            var sensNav = AddSlider(_sidePanel, "SensSlider", Strings.MouseSensitivity, ref currentY, 1, 100, _settings.MouseSensitivity,
                v => { _settings.MouseSensitivity = v; _configRepo.Save(_settings); });
            var deadNav = AddSlider(_sidePanel, "DeadzoneSlider", Strings.StickDeadzone, ref currentY, 5, 50, _settings.StickDeadzonePercent,
                v => { _settings.StickDeadzonePercent = v; _configRepo.Save(_settings); });
            var trigNav = AddSlider(_sidePanel, "TriggerSlider", Strings.TriggerThreshold, ref currentY, 5, 90, _settings.TriggerThresholdPercent,
                v => { _settings.TriggerThresholdPercent = v; _configRepo.Save(_settings); });

            currentY += 8; // Margem de respiro entre o último slider e os botões de ação

            // Botão GAMES (Biblioteca de Jogos) - Novo na Fase 2
            _gamesButton.Text = Strings.BtnGames;
            _gamesButton.SetBounds(16, currentY, 228, 36);
            _gamesButton.FlatStyle = FlatStyle.Flat;
            _gamesButton.FlatAppearance.BorderColor = Color.FromArgb(120, 126, 140);
            _gamesButton.BackColor = Color.FromArgb(40, 44, 56);
            _gamesButton.ForeColor = Color.FromArgb(230, 232, 240);
            _gamesButton.Click += (s, e) =>
            {
                if (_viewMode == ContentViewMode.Debugger)
                {
                    ShowGamesLibrary();
                }
                else
                {
                    ShowDebugger();
                }
            };
            currentY = _gamesButton.Bottom + 10;

            var exit = new Button { Text = Strings.Exit, FlatStyle = FlatStyle.Flat };
            exit.SetBounds(16, currentY, 228, 36);
            exit.FlatAppearance.BorderColor = Color.FromArgb(120, 126, 140);
            exit.Click += (s, e) =>
            {
                _focusOverlay.ShowExitDialog(
                    this,
                    onCloseApp: () =>
                    {
                        _allowClose = true;
                        Application.Exit();
                    },
                    onMinimizeToTray: () =>
                    {
                        Hide();
                    });
            };

            _sidePanel.Controls.Add(_gamesButton);
            _sidePanel.Controls.Add(exit);

            var gamesNav = new ButtonNavigable("GamesButton", _gamesButton);
            var exitNav = new ButtonNavigable("ExitButton", exit);

            _navManager.RegisterControl(slotNav);
            _navManager.RegisterControl(toggleNav);
            _navManager.RegisterControl(sensNav);
            _navManager.RegisterControl(deadNav);
            _navManager.RegisterControl(trigNav);
            _navManager.RegisterControl(gamesNav);
            _navManager.RegisterControl(exitNav);

            // Foco inicial definido no botão GAMES (conforme regra e plano da Fase 2)
            _navManager.SetFocus(gamesNav);

            // Views da área de conteúdo à direita
            _debugger.Dock = DockStyle.Fill;
            _gamesGrid.Dock = DockStyle.Fill;
            _gamesGrid.Visible = false;
            _configPanel.Dock = DockStyle.Fill;
            _configPanel.Visible = false;
            _steamGridBrowser.Dock = DockStyle.Fill;
            _steamGridBrowser.Visible = false;

            // Eventos do Grid de Jogos
            _gamesGrid.LaunchRequested += OnLaunchGame;
            _gamesGrid.ConfigureRequested += OnConfigureGame;
            _gamesGrid.AddRequested += OnAddGame;
            _gamesGrid.BackRequested += ShowDebugger;
            _gamesGrid.RequestSearchVirtualKeyboard += () =>
            {
                _focusOverlay.ShowVirtualKeyboard(
                    this,
                    Strings.VirtualKeyboardSearchTitle,
                    _gamesGrid.SearchQuery,
                    text =>
                    {
                        _gamesGrid.SetSearchQuery(text);
                        _gamesGrid.FocusSearch();
                        _gamesGrid.ResetInputState();
                    },
                    onCancel: () =>
                    {
                        _gamesGrid.FocusSearch();
                        _gamesGrid.ResetInputState();
                    },
                    onLiveTextChange: text =>
                    {
                        _gamesGrid.SetSearchQuery(text);
                        _focusOverlay.RefreshLiveBackground(this);
                    }
                );
            };

            // Eventos do Painel de Configuração
            _configPanel.SaveRequested += OnSaveGame;
            _configPanel.DeleteRequested += OnDeleteGame;
            _configPanel.CancelRequested += ShowGamesLibrary;
            _configPanel.RequestVirtualKeyboard += (title, initialText, onConfirmed) =>
            {
                _focusOverlay.ShowVirtualKeyboard(
                    this,
                    title,
                    initialText,
                    text =>
                    {
                        onConfirmed(text);
                        _configPanel.ResetInputState();
                    },
                    onCancel: () =>
                    {
                        _configPanel.ResetInputState();
                    }
                );
            };
            _configPanel.ValidationFailed += (title, message) =>
            {
                _focusOverlay.ShowMessage(this, title, message, MessageDialogType.Warning, () =>
                {
                    _configPanel.ResetInputState();
                });
            };
            _configPanel.SteamGridDbRequested += OnSteamGridDbRequested;

            _steamGridBrowser.CoverSelected += OnSteamGridCoverSelected;
            _steamGridBrowser.BackToConfigRequested += ShowGameConfigFromSteamGrid;
            _steamGridBrowser.NewSearchRequested += () => PromptSteamGridSearch(_lastSteamGridSearchTerm);
            _steamGridBrowser.ChangeApiKeyRequested += () => ShowSteamGridApiKeyModal(_lastSteamGridSearchTerm);

            Controls.Add(_focusOverlay);
            Controls.Add(_steamGridBrowser);
            Controls.Add(_configPanel);
            Controls.Add(_gamesGrid);
            Controls.Add(_debugger);
            Controls.Add(_sidePanel);
        }

        private void RefreshGamesList()
        {
            var games = _gameRepo.GetAll();
            _gamesGrid.LoadGames(games);
        }

        private void ShowDebugger()
        {
            _viewMode = ContentViewMode.Debugger;
            _debugger.Visible = true;
            _gamesGrid.Visible = false;
            _configPanel.Visible = false;
            _steamGridBrowser.Visible = false;
            _debugger.BringToFront();
            _focusOverlay.BringToFront();
            _lastStateY = true;
            _navManager.ResetInputState();
        }

        private void ShowGamesLibrary()
        {
            _viewMode = ContentViewMode.GamesGrid;
            RefreshGamesList();
            _gamesGrid.ResetInputState();
            _debugger.Visible = false;
            _gamesGrid.Visible = true;
            _configPanel.Visible = false;
            _steamGridBrowser.Visible = false;
            _gamesGrid.BringToFront();
            _focusOverlay.BringToFront();
            _lastStateY = true;
        }

        private void ShowGameConfig(GameEntry game)
        {
            _viewMode = ContentViewMode.GameConfig;
            _configPanel.ResetInputState();
            _debugger.Visible = false;
            _gamesGrid.Visible = false;
            _steamGridBrowser.Visible = false;
            _configPanel.Visible = true;
            _configPanel.EditGame(game);
            _configPanel.BringToFront();
            _focusOverlay.BringToFront();
        }

        private void OnSteamGridDbRequested(string gameName)
        {
            if (string.IsNullOrWhiteSpace(_settings.SteamGridDbApiKey))
            {
                ShowSteamGridApiKeyModal(gameName);
            }
            else
            {
                PromptSteamGridSearch(gameName);
            }
        }

        private void ShowSteamGridApiKeyModal(string gameName)
        {
            _focusOverlay.ShowSteamGridApiKeyDialog(
                this,
                _settings.SteamGridDbApiKey,
                onConfirmed: validKey =>
                {
                    _settings.SteamGridDbApiKey = validKey;
                    _configRepo.Save(_settings);
                    if (_viewMode == ContentViewMode.SteamGridDb)
                    {
                        _steamGridBrowser.ResetInputState();
                        _ = _steamGridBrowser.StartSearchAsync(gameName, validKey);
                    }
                    else
                    {
                        _configPanel.ResetInputState();
                        PromptSteamGridSearch(gameName);
                    }
                },
                onCancel: () =>
                {
                    if (_viewMode == ContentViewMode.SteamGridDb)
                    {
                        _steamGridBrowser.ResetInputState();
                    }
                    else
                    {
                        _configPanel.ResetInputState();
                    }
                },
                service: _steamGridService
            );
        }

        private void PromptSteamGridSearch(string initialTerm)
        {
            _lastSteamGridSearchTerm = string.IsNullOrWhiteSpace(initialTerm) ? "Turtle WoW" : initialTerm;
            _focusOverlay.ShowVirtualKeyboard(
                this,
                Strings.SteamGridSearchTitle,
                _lastSteamGridSearchTerm,
                onConfirm: searchQuery =>
                {
                    _lastSteamGridSearchTerm = searchQuery;
                    _configPanel.ResetInputState();
                    ShowSteamGridBrowser(searchQuery);
                },
                onCancel: () =>
                {
                    _configPanel.ResetInputState();
                }
            );
        }

        private void ShowSteamGridBrowser(string query)
        {
            _viewMode = ContentViewMode.SteamGridDb;
            _debugger.Visible = false;
            _gamesGrid.Visible = false;
            _configPanel.Visible = false;
            _steamGridBrowser.Visible = true;
            _steamGridBrowser.BringToFront();
            _focusOverlay.BringToFront();
            _steamGridBrowser.ResetInputState();
            _ = _steamGridBrowser.StartSearchAsync(query, _settings.SteamGridDbApiKey);
        }

        private void ShowGameConfigFromSteamGrid()
        {
            _viewMode = ContentViewMode.GameConfig;
            _steamGridBrowser.Visible = false;
            _configPanel.Visible = true;
            _configPanel.BringToFront();
            _focusOverlay.BringToFront();
            _configPanel.ResetInputState();
        }

        private void OnSteamGridCoverSelected(string localCoverPath)
        {
            _configPanel.SetCoverPath(localCoverPath);
            ShowGameConfigFromSteamGrid();
        }

        private void OnLaunchGame(GameEntry game)
        {
            if (game == null || _isLaunching) return;

            _isLaunching = true;
            try
            {
                _gameLauncher.Launch(game);
            }
            catch (Exception ex)
            {
                _focusOverlay.ShowMessage(
                    this,
                    Strings.LaunchErrorTitle,
                    string.Format(Strings.LaunchErrorPrompt, ex.Message),
                    MessageDialogType.Error,
                    onOk: () =>
                    {
                        _gamesGrid.ResetInputState();
                    });
            }
            finally
            {
                _isLaunching = false;
                _gamesGrid.ResetInputState();
            }
        }

        private void OnConfigureGame(GameEntry game)
        {
            ShowGameConfig(game);
        }

        private void OnAddGame()
        {
            ShowGameConfig(null);
        }

        private void OnSaveGame(GameEntry game)
        {
            if (game == null) return;
            _gameRepo.Save(game);
            ShowGamesLibrary();
        }

        private void OnDeleteGame(GameEntry game)
        {
            if (game == null) return;

            _focusOverlay.ShowConfirmation(
                this,
                Strings.ConfirmDeleteTitle,
                string.Format(Strings.ConfirmDeletePrompt, game.Name),
                onConfirm: () =>
                {
                    _gameRepo.Delete(game.Id);
                    ShowGamesLibrary();
                },
                onCancel: () =>
                {
                    _configPanel.ResetInputState();
                });
        }

        private void OnNavEditingChanged(INavigableControl control, bool isEditing)
        {
            if (isEditing)
            {
                _focusOverlay.ShowOverlay(control, this);
            }
            else
            {
                _focusOverlay.HideOverlay();
            }
        }

        public void ForceClose()
        {
            _allowClose = true;
            Close();
        }

        private static SliderNavigable AddSlider(
            Panel parent, string id, string title, ref int currentY, int min, int max, int value, Action<int> onChanged)
        {
            var titleLabel = MakeTitle(title, currentY);
            var valueLabel = new Label { AutoSize = false, Text = value.ToString() };
            valueLabel.SetBounds(204, currentY + 22, 44, 20);

            var track = new TrackBar { Minimum = min, Maximum = max, TickFrequency = Math.Max(1, (max - min) / 10) };
            track.SetBounds(10, currentY + 18, 190, 32);
            track.Value = Math.Max(min, Math.Min(max, value));
            track.ValueChanged += (s, e) =>
            {
                onChanged(track.Value);
                valueLabel.Text = track.Value.ToString();
            };

            parent.Controls.Add(titleLabel);
            parent.Controls.Add(track);
            parent.Controls.Add(valueLabel);

            currentY = Math.Max(track.Bottom, valueLabel.Bottom) + 12;

            return new SliderNavigable(id, title, titleLabel, track, valueLabel, onChanged);
        }

        private static Label MakeTitle(string text, int top)
        {
            var label = new Label { Text = text, AutoSize = false, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            label.SetBounds(16, top, 228, 18);
            return label;
        }

        private void Refresh_Tick()
        {
            GamepadState state = _gamepad.CurrentState;
            _debugger.StickDeadzone = _settings.StickDeadzone;
            _debugger.TriggerThreshold = _settings.TriggerThreshold;
            _debugger.SetState(state);
            RefreshSlotCombo(force: false);

            if (_focusOverlay.IsExitDialogOpen)
            {
                _focusOverlay.ProcessExitGamepad(state, _stopwatch.ElapsedMilliseconds);
                return;
            }

            if (_focusOverlay.IsMessageDialogOpen)
            {
                _focusOverlay.ProcessMessageGamepad(state, _stopwatch.ElapsedMilliseconds);
                return;
            }

            if (_focusOverlay.IsSteamGridApiKeyDialogOpen)
            {
                _focusOverlay.ProcessSteamGridApiKeyGamepad(state, _stopwatch.ElapsedMilliseconds);
                return;
            }

            if (_focusOverlay.IsVirtualKeyboardOpen)
            {
                _focusOverlay.ProcessVirtualKeyboardGamepad(state, _stopwatch.ElapsedMilliseconds);
                return;
            }

            // Processamento de acordo com o modo atual de exibição
            if (_viewMode == ContentViewMode.GameConfig)
            {
                _configPanel.ProcessGamepad(state, _stopwatch.ElapsedMilliseconds);
            }
            else if (_viewMode == ContentViewMode.GamesGrid)
            {
                _gamesGrid.ProcessGamepad(state, _stopwatch.ElapsedMilliseconds);
            }
            else if (_viewMode == ContentViewMode.SteamGridDb)
            {
                _steamGridBrowser.ProcessGamepad(state, _stopwatch.ElapsedMilliseconds);
            }
            else
            {
                // Modo Debugger: Atalho Y alterna para a Biblioteca de Jogos
                bool btnY = state.IsPressed(GamepadButtons.Y);
                if (btnY && !_lastStateY)
                {
                    ShowGamesLibrary();
                }
                _lastStateY = btnY;

                _navManager.ProcessGamepad(state, _stopwatch.ElapsedMilliseconds);
            }

            int active = _gamepad.ActiveSlot;
            _statusLabel.Text = active >= 0 ? string.Format(Strings.StatusFormat, active + 1) : Strings.StatusNone;
            _statusLabel.ForeColor = active >= 0 ? Color.FromArgb(150, 220, 160) : Color.FromArgb(255, 190, 90);

            if (_profileManager != null)
            {
                string prof = _profileManager.IsGameFocused ? Strings.ProfileGaming : Strings.ProfileDesktop;
                _profileLabel.Text = string.Format(Strings.ProfileFormat, prof);
                _profileLabel.ForeColor = _profileManager.IsGameFocused ? Color.FromArgb(120, 230, 150) : Color.FromArgb(160, 170, 185);
            }
        }

        private void RefreshSlotCombo(bool force)
        {
            if (!force && _navManager.IsEditingAny) return;

            string signature = string.Concat(
                _gamepad.IsSlotConnected(0) ? "1" : "0",
                _gamepad.IsSlotConnected(1) ? "1" : "0",
                _gamepad.IsSlotConnected(2) ? "1" : "0",
                _gamepad.IsSlotConnected(3) ? "1" : "0");
            if (!force && signature == _slotSignature) return;
            _slotSignature = signature;

            _updatingCombo = true;
            try
            {
                _slotCombo.Items.Clear();
                _slotCombo.Items.Add(Strings.Automatic);
                for (int i = 0; i < 4; i++)
                {
                    string state = _gamepad.IsSlotConnected(i) ? Strings.Connected : Strings.Disconnected;
                    _slotCombo.Items.Add(string.Format(Strings.PlayerFormat, i + 1, state));
                }
                _slotCombo.SelectedIndex = _settings.SelectedSlot + 1;
            }
            finally
            {
                _updatingCombo = false;
            }
        }

        private void OnSlotChanged()
        {
            if (_updatingCombo || _slotCombo.SelectedIndex < 0) return;
            _settings.SelectedSlot = _slotCombo.SelectedIndex - 1;
            _gamepad.SelectedSlot = _settings.SelectedSlot;
            _configRepo.Save(_settings);
        }

        private void ApplyToggleVisual()
        {
            bool on = _settings.MappingEnabled;
            _toggleButton.Text = on ? Strings.DisableCompanion : Strings.EnableCompanion;
            _toggleButton.BackColor = on ? Color.FromArgb(46, 125, 80) : Color.FromArgb(150, 80, 60);
            _toggleButton.ForeColor = Color.White;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_focusOverlay != null && _focusOverlay.Visible)
            {
                _focusOverlay.Bounds = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
                _focusOverlay.Invalidate();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_allowClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
            _steamGridService?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
