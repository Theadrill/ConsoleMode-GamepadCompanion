using System;
using System.Drawing;
using System.Windows.Forms;
using ConsoleMode.GamepadCompanion.Core.Interfaces;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.UI.Controls;

namespace ConsoleMode.GamepadCompanion.UI
{
    /// <summary>Janela de configurações: apenas apresenta estado e repassa ações do usuário.</summary>
    internal sealed class SettingsForm : Form
    {
        private readonly IGamepadService _gamepad;
        private readonly AppSettings _settings;
        private readonly Timer _refreshTimer = new Timer { Interval = 16 };

        private readonly ComboBox _slotCombo = new ComboBox();
        private readonly Label _statusLabel = new Label();
        private readonly Button _toggleButton = new Button();
        private readonly GamepadVisualDebugger _debugger = new GamepadVisualDebugger();

        private string _slotSignature = string.Empty;
        private bool _updatingCombo;

        public SettingsForm(IGamepadService gamepad, AppSettings settings)
        {
            _gamepad = gamepad;
            _settings = settings;

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

            var side = new Panel { Dock = DockStyle.Left, Width = 260, Padding = new Padding(16) };

            var slotTitle = MakeTitle(Strings.ActiveController, 16);
            _slotCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _slotCombo.SetBounds(16, 42, 228, 28);
            _slotCombo.SelectedIndexChanged += (s, e) => OnSlotChanged();

            _statusLabel.SetBounds(16, 78, 228, 22);
            _statusLabel.ForeColor = Color.FromArgb(150, 220, 160);

            _toggleButton.SetBounds(16, 116, 228, 40);
            _toggleButton.FlatStyle = FlatStyle.Flat;
            _toggleButton.FlatAppearance.BorderSize = 0;
            _toggleButton.Click += (s, e) =>
            {
                _settings.MappingEnabled = !_settings.MappingEnabled;
                ApplyToggleVisual();
            };

            var exit = new Button { Text = Strings.Exit, FlatStyle = FlatStyle.Flat };
            exit.SetBounds(16, 436, 228, 36);
            exit.FlatAppearance.BorderColor = Color.FromArgb(120, 126, 140);
            exit.Click += (s, e) => Close();

            side.Controls.AddRange(new Control[] { slotTitle, _slotCombo, _statusLabel, _toggleButton, exit });
            AddSlider(side, Strings.MouseSensitivity, 176, 1, 100, _settings.MouseSensitivity,
                v => _settings.MouseSensitivity = v);
            AddSlider(side, Strings.StickDeadzone, 244, 5, 50, _settings.StickDeadzonePercent,
                v => _settings.StickDeadzonePercent = v);
            AddSlider(side, Strings.TriggerThreshold, 312, 5, 90, _settings.TriggerThresholdPercent,
                v => _settings.TriggerThresholdPercent = v);

            _debugger.Dock = DockStyle.Fill;

            Controls.Add(_debugger);
            Controls.Add(side);
        }

        private static void AddSlider(
            Panel parent, string title, int top, int min, int max, int value, Action<int> onChanged)
        {
            var valueLabel = new Label { AutoSize = false, Text = value.ToString() };
            valueLabel.SetBounds(204, top + 32, 44, 24);

            var track = new TrackBar { Minimum = min, Maximum = max, TickFrequency = Math.Max(1, (max - min) / 10) };
            track.SetBounds(10, top + 26, 190, 45);
            track.Value = Math.Max(min, Math.Min(max, value));
            track.ValueChanged += (s, e) =>
            {
                onChanged(track.Value);
                valueLabel.Text = track.Value.ToString();
            };

            parent.Controls.Add(MakeTitle(title, top));
            parent.Controls.Add(track);
            parent.Controls.Add(valueLabel);
        }

        private static Label MakeTitle(string text, int top)
        {
            var label = new Label { Text = text, AutoSize = false, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            label.SetBounds(16, top, 228, 22);
            return label;
        }

        private void Refresh_Tick()
        {
            GamepadState state = _gamepad.CurrentState;
            _debugger.StickDeadzone = _settings.StickDeadzone;
            _debugger.TriggerThreshold = _settings.TriggerThreshold;
            _debugger.SetState(state);
            RefreshSlotCombo(force: false);

            int active = _gamepad.ActiveSlot;
            _statusLabel.Text = active >= 0 ? string.Format(Strings.StatusFormat, active + 1) : Strings.StatusNone;
            _statusLabel.ForeColor = active >= 0 ? Color.FromArgb(150, 220, 160) : Color.FromArgb(255, 190, 90);
        }

        private void RefreshSlotCombo(bool force)
        {
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
        }

        private void ApplyToggleVisual()
        {
            bool on = _settings.MappingEnabled;
            _toggleButton.Text = on ? Strings.MappingOn : Strings.MappingOff;
            _toggleButton.BackColor = on ? Color.FromArgb(46, 125, 80) : Color.FromArgb(150, 80, 60);
            _toggleButton.ForeColor = Color.White;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
            base.OnFormClosed(e);
        }
    }
}
