using System;
using System.Drawing;
using System.Windows.Forms;
using ConsoleMode.GamepadCompanion.Core.Interfaces;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.Engine;
using ConsoleMode.GamepadCompanion.Hardware;

namespace ConsoleMode.GamepadCompanion.UI
{
    /// <summary>
    /// Gerencia o ciclo de vida do aplicativo no System Tray (área de notificação).
    /// Mantém o ícone na bandeja e controla a exibição da janela de configurações e overlay.
    /// </summary>
    public sealed class TrayAppContext : ApplicationContext
    {
        private readonly NotifyIcon _trayIcon;
        private readonly SettingsForm _settingsForm;
        private readonly ProfileManager _profileManager;
        private readonly Timer _windowCheckTimer = new Timer { Interval = 150 };

        public TrayAppContext(
            IGamepadService gamepad,
            AppSettings settings,
            ProfileManager profileManager,
            ConfigRepository configRepo,
            IWindowTracker windowTracker)
        {
            _profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));

            _settingsForm = new SettingsForm(gamepad, settings, profileManager, configRepo, windowTracker);

            // Menu de contexto rápido da bandeja
            var contextMenu = new ContextMenuStrip();
            var openItem = new ToolStripMenuItem(Strings.OpenSettings, null, (s, e) => _settingsForm.BringToForeground());
            var exitItem = new ToolStripMenuItem(Strings.Exit, null, (s, e) => ExitApplication());
            contextMenu.Items.Add(openItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(exitItem);

            _trayIcon = new NotifyIcon
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application,
                Text = Strings.WindowTitle,
                ContextMenuStrip = contextMenu,
                Visible = true
            };

            // Clique esquerdo ou direito na bandeja abre a janela de configurações
            _trayIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ShowOrToggleWindow();
                }
            };

            // Botão Guide/Home no controle faz o Toggle estilo Steam Overlay
            _profileManager.GuidePressed += OnGuidePressed;

            // Timer leve para verificar janela em foco do Windows
            _windowCheckTimer.Tick += (s, e) => windowTracker.CheckActiveWindow();
            _windowCheckTimer.Start();

            // Abre a janela inicialmente para o usuário ver
            _settingsForm.BringToForeground();
        }

        private void OnGuidePressed()
        {
            if (_settingsForm.InvokeRequired)
            {
                _settingsForm.BeginInvoke(new Action(ShowOrToggleWindow));
            }
            else
            {
                ShowOrToggleWindow();
            }
        }

        public void ShowOrToggleWindow()
        {
            if (_settingsForm.Visible && _settingsForm.IsAppForeground())
            {
                _settingsForm.Hide();
            }
            else
            {
                _settingsForm.BringToForeground();
            }
        }

        private void ExitApplication()
        {
            _windowCheckTimer.Stop();
            _windowCheckTimer.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _settingsForm.ForceClose();
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _profileManager.GuidePressed -= OnGuidePressed;
                _windowCheckTimer.Stop();
                _windowCheckTimer.Dispose();
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _settingsForm.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
