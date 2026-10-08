using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.Engine.Services;

namespace ConsoleMode.GamepadCompanion.UI.Controls
{
    /// <summary>
    /// Modal Couch Gaming reutilizável para inserção, validação em tempo real e alteração da API Key do SteamGridDB.
    /// Suporta navegação espacial por Gamepad (D-Pad), clique de mouse, abertura do navegador e botão Colar.
    /// </summary>
    public sealed class SteamGridApiKeyDialogControl : UserControl
    {
        private static readonly Color CardBg = Color.FromArgb(30, 33, 42);
        private static readonly Color HeaderBg = Color.FromArgb(24, 26, 32);
        private static readonly Color BorderNormal = Color.FromArgb(60, 66, 82);
        private static readonly Color BorderFocused = Color.FromArgb(120, 190, 255);
        private static readonly Color BorderValid = Color.FromArgb(60, 210, 120);
        private static readonly Color BorderInvalid = Color.FromArgb(235, 75, 75);
        private static readonly Color TextPrimary = Color.FromArgb(235, 238, 245);
        private static readonly Color TextMuted = Color.FromArgb(150, 160, 175);
        private static readonly Color InputBg = Color.FromArgb(18, 20, 26);

        private readonly Label _titleLabel = new Label();
        private readonly Label _stepsLabel = new Label();
        private readonly Button _btnOpenWebsite = new Button();
        private readonly TextBox _txtApiKey = new TextBox();
        private readonly Button _btnPaste = new Button();
        private readonly Button _btnKeyboard = new Button();
        private readonly Label _statusLabel = new Label();
        private readonly Button _btnConfirm = new Button();
        private readonly Button _btnCancel = new Button();

        private readonly SteamGridDbService _service;
        private bool _isChecking;
        private bool _isValidated;
        private int _focusedIndex = 1; // Foco inicial no botão [Colar]

        // Gamepad repeat & debounce
        private bool _lastUp;
        private bool _lastDown;
        private bool _lastLeft;
        private bool _lastRight;
        private bool _lastA;
        private bool _lastB;

        public event Action<string> KeyConfirmed;
        public event Action Cancelled;
        public event Action<string, Action<string>> RequestVirtualKeyboard;

        public SteamGridApiKeyDialogControl(SteamGridDbService service = null)
        {
            _service = service ?? new SteamGridDbService();

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer,
                true);

            BackColor = CardBg;
            Size = new Size(580, 380);

            BuildLayout();
        }

        private void BuildLayout()
        {
            // Título
            _titleLabel.Text = Strings.SteamGridModalTitle;
            _titleLabel.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            _titleLabel.ForeColor = TextPrimary;
            _titleLabel.SetBounds(24, 18, 532, 28);

            // Passos resumidos
            _stepsLabel.Text = $"{Strings.SteamGridStep1}\n{Strings.SteamGridStep2}\n{Strings.SteamGridStep3}";
            _stepsLabel.Font = new Font("Segoe UI", 9f);
            _stepsLabel.ForeColor = TextMuted;
            _stepsLabel.SetBounds(24, 52, 532, 60);

            // Botão Abrir Site
            _btnOpenWebsite.Text = $"🌐 {Strings.BtnGetApiKey}";
            _btnOpenWebsite.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _btnOpenWebsite.FlatStyle = FlatStyle.Flat;
            _btnOpenWebsite.FlatAppearance.BorderColor = Color.FromArgb(80, 120, 180);
            _btnOpenWebsite.BackColor = Color.FromArgb(35, 65, 110);
            _btnOpenWebsite.ForeColor = Color.White;
            _btnOpenWebsite.Cursor = Cursors.Hand;
            _btnOpenWebsite.SetBounds(24, 122, 280, 32);
            _btnOpenWebsite.Click += (s, e) => OpenWebsite();

            // Campo de texto para API Key
            _txtApiKey.Font = new Font("Consolas", 10f);
            _txtApiKey.BackColor = InputBg;
            _txtApiKey.ForeColor = TextPrimary;
            _txtApiKey.BorderStyle = BorderStyle.FixedSingle;
            _txtApiKey.SetBounds(24, 170, 350, 28);
            _txtApiKey.TextChanged += async (s, e) => await ValidateKeyLiveAsync(_txtApiKey.Text);

            // Botão Colar
            _btnPaste.Text = $"📋 {Strings.BtnPaste}";
            _btnPaste.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _btnPaste.FlatStyle = FlatStyle.Flat;
            _btnPaste.FlatAppearance.BorderColor = BorderNormal;
            _btnPaste.BackColor = Color.FromArgb(44, 48, 62);
            _btnPaste.ForeColor = TextPrimary;
            _btnPaste.Cursor = Cursors.Hand;
            _btnPaste.SetBounds(382, 169, 84, 28);
            _btnPaste.Click += (s, e) => PasteFromClipboard();

            // Botão Teclado Virtual
            _btnKeyboard.Text = "⌨ Digitar";
            _btnKeyboard.Font = new Font("Segoe UI", 9f);
            _btnKeyboard.FlatStyle = FlatStyle.Flat;
            _btnKeyboard.FlatAppearance.BorderColor = BorderNormal;
            _btnKeyboard.BackColor = Color.FromArgb(44, 48, 62);
            _btnKeyboard.ForeColor = TextPrimary;
            _btnKeyboard.Cursor = Cursors.Hand;
            _btnKeyboard.SetBounds(472, 169, 84, 28);
            _btnKeyboard.Click += (s, e) => OpenVirtualKeyboard();

            // Label de Status (Amarelo / Verde / Vermelho)
            _statusLabel.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _statusLabel.ForeColor = TextMuted;
            _statusLabel.SetBounds(24, 210, 532, 42);
            _statusLabel.Text = string.Empty;

            // Botões de Ação Inferiores
            _btnConfirm.Text = Strings.NavHintConfirm;
            _btnConfirm.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _btnConfirm.FlatStyle = FlatStyle.Flat;
            _btnConfirm.FlatAppearance.BorderSize = 0;
            _btnConfirm.BackColor = Color.FromArgb(46, 125, 80);
            _btnConfirm.ForeColor = Color.White;
            _btnConfirm.Cursor = Cursors.Hand;
            _btnConfirm.Enabled = false;
            _btnConfirm.SetBounds(140, 290, 140, 36);
            _btnConfirm.Click += (s, e) => Confirm();

            _btnCancel.Text = Strings.NavHintCancel;
            _btnCancel.Font = new Font("Segoe UI", 9.5f);
            _btnCancel.FlatStyle = FlatStyle.Flat;
            _btnCancel.FlatAppearance.BorderColor = BorderNormal;
            _btnCancel.BackColor = Color.FromArgb(52, 56, 66);
            _btnCancel.ForeColor = TextPrimary;
            _btnCancel.Cursor = Cursors.Hand;
            _btnCancel.SetBounds(300, 290, 140, 36);
            _btnCancel.Click += (s, e) => Cancel();

            Controls.Add(_titleLabel);
            Controls.Add(_stepsLabel);
            Controls.Add(_btnOpenWebsite);
            Controls.Add(_txtApiKey);
            Controls.Add(_btnPaste);
            Controls.Add(_btnKeyboard);
            Controls.Add(_statusLabel);
            Controls.Add(_btnConfirm);
            Controls.Add(_btnCancel);

            UpdateFocusVisuals();
        }

        public void SetInitialKey(string existingKey)
        {
            _isValidated = false;
            _btnConfirm.Enabled = false;
            _txtApiKey.Text = existingKey ?? string.Empty;

            if (string.IsNullOrWhiteSpace(existingKey))
            {
                _statusLabel.Text = string.Empty;
                _statusLabel.ForeColor = TextMuted;
                _focusedIndex = 1; // Foco em [Colar]
                UpdateFocusVisuals();
            }
        }

        private void OpenWebsite()
        {
            try
            {
                Process.Start("https://www.steamgriddb.com/profile/preferences/api");
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Não foi possível abrir o navegador: {ex.Message}";
                _statusLabel.ForeColor = BorderInvalid;
            }
        }

        private void PasteFromClipboard()
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText()?.Trim();
                    if (!string.IsNullOrEmpty(text))
                    {
                        _txtApiKey.Text = text;
                    }
                }
            }
            catch
            {
                // Fallback silencioso de área de transferência
            }
        }

        private void OpenVirtualKeyboard()
        {
            RequestVirtualKeyboard?.Invoke(_txtApiKey.Text, newText =>
            {
                _txtApiKey.Text = newText?.Trim() ?? string.Empty;
            });
        }

        public async Task ValidateKeyLiveAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                _isValidated = false;
                _btnConfirm.Enabled = false;
                _statusLabel.Text = string.Empty;
                _txtApiKey.Invalidate();
                return;
            }

            _isChecking = true;
            _statusLabel.Text = Strings.SteamGridCheckingKey;
            _statusLabel.ForeColor = Color.FromArgb(240, 200, 70); // Amarelo
            _btnConfirm.Enabled = false;

            bool ok = await _service.ValidateApiKeyAsync(key).ConfigureAwait(false);

            if (IsDisposed) return;

            Action update = () =>
            {
                _isChecking = false;
                _isValidated = ok;

                if (ok)
                {
                    _statusLabel.Text = Strings.SteamGridKeyValid;
                    _statusLabel.ForeColor = BorderValid; // Verde
                    _btnConfirm.Enabled = true;
                    _focusedIndex = 3; // Move o foco para [Confirmar]
                    UpdateFocusVisuals();
                }
                else
                {
                    _statusLabel.Text = Strings.SteamGridKeyInvalid;
                    _statusLabel.ForeColor = BorderInvalid; // Vermelho
                    _btnConfirm.Enabled = false;
                }

                _txtApiKey.Invalidate();
            };

            if (InvokeRequired && IsHandleCreated)
            {
                BeginInvoke(update);
            }
            else
            {
                update();
            }
        }

        private void Confirm()
        {
            if (_isValidated && !string.IsNullOrWhiteSpace(_txtApiKey.Text))
            {
                KeyConfirmed?.Invoke(_txtApiKey.Text.Trim());
            }
        }

        private void Cancel()
        {
            Cancelled?.Invoke();
        }

        public void ProcessGamepad(GamepadState state, long nowMs)
        {
            if (!state.IsConnected || _isChecking) return;

            // Navegação Up / Down / Left / Right
            bool up = state.IsPressed(GamepadButtons.DPadUp);
            bool down = state.IsPressed(GamepadButtons.DPadDown);
            bool left = state.IsPressed(GamepadButtons.DPadLeft);
            bool right = state.IsPressed(GamepadButtons.DPadRight);

            if (up && !_lastUp)
            {
                if (_focusedIndex == 3 || _focusedIndex == 4) _focusedIndex = 1;
                else if (_focusedIndex == 1 || _focusedIndex == 2) _focusedIndex = 0;
                UpdateFocusVisuals();
            }
            _lastUp = up;

            if (down && !_lastDown)
            {
                if (_focusedIndex == 0) _focusedIndex = 1;
                else if (_focusedIndex == 1 || _focusedIndex == 2) _focusedIndex = _isValidated ? 3 : 4;
                UpdateFocusVisuals();
            }
            _lastDown = down;

            if (left && !_lastLeft)
            {
                if (_focusedIndex == 2) _focusedIndex = 1;
                else if (_focusedIndex == 4 && _isValidated) _focusedIndex = 3;
                UpdateFocusVisuals();
            }
            _lastLeft = left;

            if (right && !_lastRight)
            {
                if (_focusedIndex == 1) _focusedIndex = 2;
                else if (_focusedIndex == 3) _focusedIndex = 4;
                UpdateFocusVisuals();
            }
            _lastRight = right;

            // Botão A executa a ação focada
            bool a = state.IsPressed(GamepadButtons.A);
            if (a && !_lastA)
            {
                ExecuteFocused();
            }
            _lastA = a;

            // Botão B cancela
            bool b = state.IsPressed(GamepadButtons.B);
            if (b && !_lastB)
            {
                Cancel();
            }
            _lastB = b;
        }

        private void ExecuteFocused()
        {
            switch (_focusedIndex)
            {
                case 0:
                    OpenWebsite();
                    break;
                case 1:
                    PasteFromClipboard();
                    break;
                case 2:
                    OpenVirtualKeyboard();
                    break;
                case 3:
                    Confirm();
                    break;
                case 4:
                    Cancel();
                    break;
            }
        }

        private void UpdateFocusVisuals()
        {
            HighlightButton(_btnOpenWebsite, _focusedIndex == 0);
            HighlightButton(_btnPaste, _focusedIndex == 1);
            HighlightButton(_btnKeyboard, _focusedIndex == 2);
            HighlightButton(_btnConfirm, _focusedIndex == 3);
            HighlightButton(_btnCancel, _focusedIndex == 4);
        }

        private static void HighlightButton(Button btn, bool focused)
        {
            if (focused)
            {
                btn.FlatAppearance.BorderSize = 2;
                btn.FlatAppearance.BorderColor = BorderFocused;
            }
            else
            {
                btn.FlatAppearance.BorderSize = 1;
                btn.FlatAppearance.BorderColor = BorderNormal;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Borda do card principal
            Color cardBorder = _isValidated ? BorderValid : (_statusLabel.ForeColor == BorderInvalid ? BorderInvalid : BorderNormal);
            using (var pen = new Pen(cardBorder, 2))
            {
                g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
            }
        }
    }
}
