using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using ConsoleMode.GamepadCompanion.Core.Interfaces;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.UI.Controls;
using ConsoleMode.GamepadCompanion.UI.Navigation;

namespace ConsoleMode.GamepadCompanion.UI
{
    /// <summary>Janela de configurações: apresenta estado, visual debugger e navegação nativa por controle.</summary>
    internal sealed class SettingsForm : Form
    {
        private readonly IGamepadService _gamepad;
        private readonly AppSettings _settings;
        private readonly Engine.ProfileManager _profileManager;
        private readonly Hardware.ConfigRepository _configRepo;
        private readonly Timer _refreshTimer = new Timer { Interval = 16 };
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        private readonly ComboBox _slotCombo = new ComboBox();
        private readonly Label _statusLabel = new Label();
        private readonly Label _profileLabel = new Label();
        private readonly Button _toggleButton = new Button();
        private readonly GamepadVisualDebugger _debugger = new GamepadVisualDebugger();
        private readonly FocusOverlayPanel _focusOverlay = new FocusOverlayPanel();
        private readonly GamepadNavigationManager _navManager = new GamepadNavigationManager();

        private Panel _sidePanel;
        private string _slotSignature = string.Empty;
        private bool _updatingCombo;

        public SettingsForm(
            IGamepadService gamepad,
            AppSettings settings,
            Engine.ProfileManager profileManager,
            Hardware.ConfigRepository configRepo)
        {
            _gamepad = gamepad;
            _settings = settings;
            _profileManager = profileManager;
            _configRepo = configRepo;

            BuildLayout();
            RefreshSlotCombo(force: true);
            ApplyToggleVisual();

            _refreshTimer.Tick += (s, e) => Refresh_Tick();
            _refreshTimer.Start();
        }

        private void BuildLayout()
        {
            Text = Strings.WindowTitle;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(820, 490);
            MinimumSize = new Size(700, 530);
            BackColor = Color.FromArgb(30, 32, 40);
            ForeColor = Color.FromArgb(230, 232, 240);
            Font = new Font("Segoe UI", 9.5f);

            _sidePanel = new Panel { Dock = DockStyle.Left, Width = 260, Padding = new Padding(16) };
            _sidePanel.Paint += (s, e) => _navManager.DrawFocusHighlight(e.Graphics);

            _navManager.RequestRepaint += () => _sidePanel.Invalidate();
            _navManager.EditingChanged += (ctrl, editing) => OnNavEditingChanged(ctrl, editing);

            var slotTitle = MakeTitle(Strings.ActiveController, 16);
            _slotCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _slotCombo.SetBounds(16, 42, 228, 28);
            _slotCombo.SelectedIndexChanged += (s, e) => OnSlotChanged();

            _statusLabel.SetBounds(16, 74, 228, 20);
            _statusLabel.ForeColor = Color.FromArgb(150, 220, 160);

            _profileLabel.SetBounds(16, 96, 228, 20);
            _profileLabel.ForeColor = Color.FromArgb(120, 190, 255);

            _toggleButton.SetBounds(16, 122, 228, 38);
            _toggleButton.FlatStyle = FlatStyle.Flat;
            _toggleButton.FlatAppearance.BorderSize = 1;
            _toggleButton.FlatAppearance.BorderColor = Color.FromArgb(120, 126, 140);
            _toggleButton.Click += (s, e) =>
            {
                _settings.MappingEnabled = !_settings.MappingEnabled;
                _configRepo.Save(_settings);
                ApplyToggleVisual();
            };

            var exit = new Button { Text = Strings.Exit, FlatStyle = FlatStyle.Flat };
            exit.SetBounds(16, 432, 228, 36);
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

            _sidePanel.Controls.AddRange(new Control[] { slotTitle, _slotCombo, _statusLabel, _profileLabel, _toggleButton, exit });

            // Registro sequencial dos controles para navegação D-Pad (topo -> base)
            var slotNav = new DropdownNavigable("SlotCombo", slotTitle, _slotCombo);
            var toggleNav = new ButtonNavigable("ToggleButton", _toggleButton);
            var sensNav = AddSlider(_sidePanel, "SensSlider", Strings.MouseSensitivity, 172, 1, 100, _settings.MouseSensitivity,
                v => { _settings.MouseSensitivity = v; _configRepo.Save(_settings); });
            var deadNav = AddSlider(_sidePanel, "DeadzoneSlider", Strings.StickDeadzone, 252, 5, 50, _settings.StickDeadzonePercent,
                v => { _settings.StickDeadzonePercent = v; _configRepo.Save(_settings); });
            var trigNav = AddSlider(_sidePanel, "TriggerSlider", Strings.TriggerThreshold, 332, 5, 90, _settings.TriggerThresholdPercent,
                v => { _settings.TriggerThresholdPercent = v; _configRepo.Save(_settings); });
            var exitNav = new ButtonNavigable("ExitButton", exit);

            _navManager.RegisterControl(slotNav);
            _navManager.RegisterControl(toggleNav);
            _navManager.RegisterControl(sensNav);
            _navManager.RegisterControl(deadNav);
            _navManager.RegisterControl(trigNav);
            _navManager.RegisterControl(exitNav);

            _debugger.Dock = DockStyle.Fill;

            Controls.Add(_focusOverlay);
            Controls.Add(_debugger);
            Controls.Add(_sidePanel);
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

        private bool _allowClose;

        public void ForceClose()
        {
            _allowClose = true;
            Close();
        }

        private static SliderNavigable AddSlider(
            Panel parent, string id, string title, int top, int min, int max, int value, Action<int> onChanged)
        {
            var titleLabel = MakeTitle(title, top);
            var valueLabel = new Label { AutoSize = false, Text = value.ToString() };
            valueLabel.SetBounds(204, top + 22, 44, 20);

            var track = new TrackBar { Minimum = min, Maximum = max, TickFrequency = Math.Max(1, (max - min) / 10) };
            track.SetBounds(10, top + 18, 190, 32);
            track.Value = Math.Max(min, Math.Min(max, value));
            track.ValueChanged += (s, e) =>
            {
                onChanged(track.Value);
                valueLabel.Text = track.Value.ToString();
            };

            parent.Controls.Add(titleLabel);
            parent.Controls.Add(track);
            parent.Controls.Add(valueLabel);

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

            _navManager.ProcessGamepad(state, _stopwatch.ElapsedMilliseconds);

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
            base.OnFormClosed(e);
        }
    }
}
