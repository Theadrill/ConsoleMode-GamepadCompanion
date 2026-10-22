using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ConsoleMode.GamepadCompanion.Core.Interfaces;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.Hardware;

namespace ConsoleMode.GamepadCompanion.UI.Controls
{
    /// <summary>
    /// Painel de onboarding e setup assistido para o Valve Steam Deck na área de conteúdo.
    /// Permite a instalação silenciosa do driver de gamepad virtual ViGEmBus com 1 clique e navegação por controle.
    /// </summary>
    public sealed class SteamDeckSetupControl : UserControl
    {
        private static readonly Color BgColor = Color.FromArgb(24, 26, 32);
        private static readonly Color CardBg = Color.FromArgb(34, 37, 46);
        private static readonly Color BorderColor = Color.FromArgb(120, 190, 255);
        private static readonly Color TextPrimary = Color.FromArgb(235, 238, 245);
        private static readonly Color TextSecondary = Color.FromArgb(160, 170, 185);
        private static readonly Color TextAmber = Color.FromArgb(245, 158, 11);
        private static readonly Color TextGreen = Color.FromArgb(120, 230, 150);

        private readonly Panel _cardPanel = new Panel();
        private readonly Label _headerLabel = new Label();
        private readonly Label _deviceInfoLabel = new Label();
        private readonly Label _descriptionLabel = new Label();
        private readonly Label _statusBadgeLabel = new Label();
        private readonly Label _progressLabel = new Label();
        private readonly Button _btnInstall = new Button();
        private readonly Button _btnSkip = new Button();
        private readonly Label _footerHintsLabel = new Label();

        private bool _isInstalling;
        private int _focusedButtonIndex = 0; // 0 = Install, 1 = Skip

        // Gamepad debouncing
        private bool _lastUp;
        private bool _lastDown;
        private bool _lastA;
        private bool _lastB;
        private bool _lastRt;

        public event EventHandler SetupCompleted;
        public event EventHandler SetupSkipped;

        public SteamDeckSetupControl()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer,
                true);

            BackColor = BgColor;
            Dock = DockStyle.Fill;

            BuildLayout();
        }

        private void BuildLayout()
        {
            _cardPanel.BackColor = CardBg;
            _cardPanel.Size = new Size(680, 420);
            _cardPanel.Paint += OnCardPaint;

            _headerLabel.Text = "📟 STEAM DECK DETECTADO";
            _headerLabel.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
            _headerLabel.ForeColor = TextPrimary;
            _headerLabel.SetBounds(24, 24, 632, 34);

            _deviceInfoLabel.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            _deviceInfoLabel.ForeColor = BorderColor;
            _deviceInfoLabel.SetBounds(24, 62, 632, 24);

            _descriptionLabel.Text = "Para que os controles embutidos do Steam Deck funcionem com máxima compatibilidade tanto no Companion quanto em jogos com suporte nativo a controle, o Windows requer o driver de Gamepad Virtual (ViGEmBus).";
            _descriptionLabel.Font = new Font("Segoe UI", 10f);
            _descriptionLabel.ForeColor = TextSecondary;
            _descriptionLabel.SetBounds(24, 96, 632, 64);

            _statusBadgeLabel.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _statusBadgeLabel.SetBounds(24, 168, 632, 30);

            _progressLabel.Font = new Font("Segoe UI", 9.5f, FontStyle.Italic);
            _progressLabel.ForeColor = TextSecondary;
            _progressLabel.SetBounds(24, 204, 632, 24);
            _progressLabel.Text = "Clique abaixo para instalar em segundo plano (requer confirmação única no UAC do Windows).";

            // Botão Primário: Instalar
            _btnInstall.Text = "⚡ INSTALAR DRIVER AUTOMATICAMENTE (A)";
            _btnInstall.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _btnInstall.FlatStyle = FlatStyle.Flat;
            _btnInstall.FlatAppearance.BorderSize = 2;
            _btnInstall.FlatAppearance.BorderColor = BorderColor;
            _btnInstall.BackColor = Color.FromArgb(37, 99, 235);
            _btnInstall.ForeColor = Color.White;
            _btnInstall.Cursor = Cursors.Hand;
            _btnInstall.SetBounds(24, 240, 632, 48);
            _btnInstall.Click += async (s, e) => await StartInstallationAsync();

            // Botão Secundário: Continuar sem driver
            _btnSkip.Text = "CONTINUAR SEM DRIVER (B)";
            _btnSkip.Font = new Font("Segoe UI", 9.5f);
            _btnSkip.FlatStyle = FlatStyle.Flat;
            _btnSkip.FlatAppearance.BorderColor = Color.FromArgb(80, 86, 100);
            _btnSkip.BackColor = Color.FromArgb(44, 48, 60);
            _btnSkip.ForeColor = TextSecondary;
            _btnSkip.Cursor = Cursors.Hand;
            _btnSkip.SetBounds(24, 302, 632, 38);
            _btnSkip.Click += (s, e) => SetupSkipped?.Invoke(this, EventArgs.Empty);

            // Dicas de rodapé
            _footerHintsLabel.Text = "[A] Confirmar / Executar   [B] Pular / Continuar   [▲ / ▼] Navegar";
            _footerHintsLabel.Font = new Font("Segoe UI", 9f);
            _footerHintsLabel.ForeColor = Color.FromArgb(120, 130, 145);
            _footerHintsLabel.TextAlign = ContentAlignment.MiddleCenter;
            _footerHintsLabel.SetBounds(24, 356, 632, 24);

            _cardPanel.Controls.Add(_headerLabel);
            _cardPanel.Controls.Add(_deviceInfoLabel);
            _cardPanel.Controls.Add(_descriptionLabel);
            _cardPanel.Controls.Add(_statusBadgeLabel);
            _cardPanel.Controls.Add(_progressLabel);
            _cardPanel.Controls.Add(_btnInstall);
            _cardPanel.Controls.Add(_btnSkip);
            _cardPanel.Controls.Add(_footerHintsLabel);

            Controls.Add(_cardPanel);
            UpdateFocusVisual();
        }

        public void RefreshState()
        {
            var info = SteamDeckDetector.Current;
            _deviceInfoLabel.Text = $"Hardware: {info.DisplayName}";

            bool isInstalled = ViGEmBusService.Instance.IsDriverInstalledAndActive;
            if (isInstalled)
            {
                _statusBadgeLabel.Text = "Status do Driver:  ✅ INSTALADO E ATIVO";
                _statusBadgeLabel.ForeColor = TextGreen;
                _btnInstall.Text = "✅ DRIVER JÁ INSTALADO (AVANÇAR)";
                _btnInstall.BackColor = Color.FromArgb(20, 120, 60);
            }
            else
            {
                _statusBadgeLabel.Text = "Status do Driver:  ⚠️ NÃO INSTALADO NO WINDOWS";
                _statusBadgeLabel.ForeColor = TextAmber;
                _btnInstall.Text = "⚡ INSTALAR DRIVER AUTOMATICAMENTE (A)";
                _btnInstall.BackColor = Color.FromArgb(37, 99, 235);
            }

            CenterCard();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            CenterCard();
        }

        private void CenterCard()
        {
            if (_cardPanel != null && Width > 0 && Height > 0)
            {
                int x = Math.Max(16, (Width - _cardPanel.Width) / 2);
                int y = Math.Max(16, (Height - _cardPanel.Height) / 2);
                _cardPanel.Location = new Point(x, y);
            }
        }

        private void OnCardPaint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(BorderColor, 2f))
            {
                var rect = new Rectangle(1, 1, _cardPanel.Width - 3, _cardPanel.Height - 3);
                e.Graphics.DrawRectangle(pen, rect);
            }
        }

        private async System.Threading.Tasks.Task StartInstallationAsync()
        {
            if (_isInstalling) return;

            if (ViGEmBusService.Instance.IsDriverInstalledAndActive)
            {
                SetupCompleted?.Invoke(this, EventArgs.Empty);
                return;
            }

            _isInstalling = true;
            _btnInstall.Enabled = false;
            _btnSkip.Enabled = false;
            _btnInstall.BackColor = Color.FromArgb(50, 55, 68);

            var progress = new Progress<string>(msg =>
            {
                _progressLabel.Text = msg;
                _progressLabel.ForeColor = BorderColor;
            });

            InstallResult result = await ViGEmBusInstaller.InstallAsync(progress);

            _isInstalling = false;
            _btnInstall.Enabled = true;
            _btnSkip.Enabled = true;

            if (result.Success)
            {
                _statusBadgeLabel.Text = "Status do Driver:  ✅ INSTALADO E ATIVO COM SUCESSO!";
                _statusBadgeLabel.ForeColor = TextGreen;
                _progressLabel.Text = "Driver pronto para uso imediato. Carregando Companion...";
                _progressLabel.ForeColor = TextGreen;

                SteamDeckManager.TryStart();
                await System.Threading.Tasks.Task.Delay(1200);
                SetupCompleted?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                _progressLabel.Text = result.Message;
                _progressLabel.ForeColor = Color.FromArgb(235, 75, 75);
                _btnInstall.BackColor = Color.FromArgb(37, 99, 235);
            }
        }

        public void ProcessGamepadInput(GamepadState state)
        {
            if (_isInstalling || !Visible) return;

            bool up = state.IsPressed(GamepadButtons.DPadUp);
            bool down = state.IsPressed(GamepadButtons.DPadDown);
            bool a = state.IsPressed(GamepadButtons.A);
            bool b = state.IsPressed(GamepadButtons.B);
            bool rt = state.RightTrigger > 100;

            if (down && !_lastDown)
            {
                _focusedButtonIndex = Math.Min(1, _focusedButtonIndex + 1);
                UpdateFocusVisual();
            }
            else if (up && !_lastUp)
            {
                _focusedButtonIndex = Math.Max(0, _focusedButtonIndex - 1);
                UpdateFocusVisual();
            }

            if ((a && !_lastA) || (rt && !_lastRt))
            {
                if (_focusedButtonIndex == 0)
                {
                    _btnInstall.PerformClick();
                }
                else
                {
                    _btnSkip.PerformClick();
                }
            }
            else if (b && !_lastB)
            {
                _btnSkip.PerformClick();
            }

            _lastUp = up;
            _lastDown = down;
            _lastA = a;
            _lastB = b;
            _lastRt = rt;
        }

        private void UpdateFocusVisual()
        {
            if (_focusedButtonIndex == 0)
            {
                _btnInstall.FlatAppearance.BorderColor = Color.White;
                _btnInstall.FlatAppearance.BorderSize = 2;
                _btnSkip.FlatAppearance.BorderColor = Color.FromArgb(80, 86, 100);
            }
            else
            {
                _btnInstall.FlatAppearance.BorderColor = BorderColor;
                _btnInstall.FlatAppearance.BorderSize = 1;
                _btnSkip.FlatAppearance.BorderColor = Color.White;
            }
        }
    }
}
