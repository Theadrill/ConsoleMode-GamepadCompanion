using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.UI.Controls
{
    /// <summary>
    /// Painel de formulário estilo Steam Shortcut para cadastro e edição dos 7 campos de um jogo.
    /// Inclui seletores de arquivos/pastas para TargetPath, WorkingDirectory e CoverImagePath.
    /// </summary>
    internal sealed class GameConfigPanel : UserControl
    {
        private static readonly Color BgColor = Color.FromArgb(24, 26, 32);
        private static readonly Color HeaderBg = Color.FromArgb(30, 32, 40);
        private static readonly Color CardBg = Color.FromArgb(34, 37, 46);
        private static readonly Color TextPrimary = Color.FromArgb(230, 232, 240);
        private static readonly Color TextSecondary = Color.FromArgb(160, 170, 185);
        private static readonly Color BorderColor = Color.FromArgb(120, 126, 140);
        private static readonly Color InputBg = Color.FromArgb(20, 22, 28);

        private readonly Label _titleLabel = new Label();
        private readonly Panel _contentPanel = new Panel();

        private readonly TextBox _txtName = new TextBox();
        private readonly TextBox _txtLauncher = new TextBox();
        private readonly TextBox _txtExecutable = new TextBox();
        private readonly TextBox _txtTargetPath = new TextBox();
        private readonly TextBox _txtWorkingDir = new TextBox();
        private readonly TextBox _txtArguments = new TextBox();
        private readonly TextBox _txtCoverPath = new TextBox();

        private readonly Button _btnSave = new Button();
        private readonly Button _btnCancel = new Button();
        private readonly Button _btnDelete = new Button();

        private GameEntry _currentGame;
        private bool _isNewGame;

        public event Action<GameEntry> SaveRequested;
        public event Action<GameEntry> DeleteRequested;
        public event Action CancelRequested;

        public GameConfigPanel()
        {
            BackColor = BgColor;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            BuildLayout();
        }

        private void BuildLayout()
        {
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = HeaderBg,
                Padding = new Padding(16, 8, 16, 8)
            };

            _titleLabel.Font = new Font("Segoe UI", 11.5f, FontStyle.Bold);
            _titleLabel.ForeColor = TextPrimary;
            _titleLabel.AutoSize = true;
            _titleLabel.Location = new Point(16, 11);
            header.Controls.Add(_titleLabel);

            _contentPanel.Dock = DockStyle.Fill;
            _contentPanel.AutoScroll = true;
            _contentPanel.Padding = new Padding(20);

            int currentY = 16;
            currentY = AddField(_contentPanel, Strings.GameConfigName, _txtName, currentY);
            currentY = AddField(_contentPanel, Strings.GameConfigLauncher, _txtLauncher, currentY);
            currentY = AddField(_contentPanel, Strings.GameConfigExecutable, _txtExecutable, currentY);
            currentY = AddFieldWithBrowse(_contentPanel, Strings.GameConfigTargetPath, _txtTargetPath, currentY, BrowseTarget);
            currentY = AddFieldWithBrowse(_contentPanel, Strings.GameConfigWorkingDir, _txtWorkingDir, currentY, BrowseFolder);
            currentY = AddField(_contentPanel, Strings.GameConfigArguments, _txtArguments, currentY);
            currentY = AddFieldWithBrowse(_contentPanel, Strings.GameConfigCoverImage, _txtCoverPath, currentY, BrowseImage);

            // Painel de botões de ação na base
            var actionsPanel = new Panel
            {
                Location = new Point(20, currentY + 10),
                Size = new Size(500, 48)
            };

            _btnSave.Text = Strings.BtnSave;
            _btnSave.SetBounds(0, 4, 130, 36);
            _btnSave.FlatStyle = FlatStyle.Flat;
            _btnSave.BackColor = Color.FromArgb(46, 125, 80);
            _btnSave.ForeColor = Color.White;
            _btnSave.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.Click += (s, e) => OnSaveClicked();

            _btnCancel.Text = Strings.BtnCancel;
            _btnCancel.SetBounds(142, 4, 130, 36);
            _btnCancel.FlatStyle = FlatStyle.Flat;
            _btnCancel.BackColor = Color.FromArgb(52, 56, 66);
            _btnCancel.ForeColor = TextPrimary;
            _btnCancel.Font = new Font("Segoe UI", 9.5f);
            _btnCancel.FlatAppearance.BorderColor = BorderColor;
            _btnCancel.Click += (s, e) => CancelRequested?.Invoke();

            _btnDelete.Text = Strings.BtnDelete;
            _btnDelete.SetBounds(284, 4, 130, 36);
            _btnDelete.FlatStyle = FlatStyle.Flat;
            _btnDelete.BackColor = Color.FromArgb(140, 50, 50);
            _btnDelete.ForeColor = Color.White;
            _btnDelete.Font = new Font("Segoe UI", 9.5f);
            _btnDelete.FlatAppearance.BorderSize = 0;
            _btnDelete.Click += (s, e) => OnDeleteClicked();

            actionsPanel.Controls.Add(_btnSave);
            actionsPanel.Controls.Add(_btnCancel);
            actionsPanel.Controls.Add(_btnDelete);

            _contentPanel.Controls.Add(actionsPanel);

            Controls.Add(_contentPanel);
            Controls.Add(header);
        }

        private int AddField(Panel parent, string labelText, TextBox textBox, int top)
        {
            var lbl = new Label
            {
                Text = labelText,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = TextSecondary,
                AutoSize = true,
                Location = new Point(20, top)
            };

            textBox.SetBounds(20, top + 20, 480, 26);
            StyleTextBox(textBox);

            parent.Controls.Add(lbl);
            parent.Controls.Add(textBox);

            return top + 56;
        }

        private int AddFieldWithBrowse(Panel parent, string labelText, TextBox textBox, int top, Action onBrowse)
        {
            var lbl = new Label
            {
                Text = labelText,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = TextSecondary,
                AutoSize = true,
                Location = new Point(20, top)
            };

            textBox.SetBounds(20, top + 20, 380, 26);
            StyleTextBox(textBox);

            var btn = new Button
            {
                Text = Strings.BtnBrowse,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f),
                BackColor = CardBg,
                ForeColor = TextPrimary
            };
            btn.FlatAppearance.BorderColor = BorderColor;
            btn.SetBounds(408, top + 19, 92, 28);
            btn.Click += (s, e) => onBrowse();

            parent.Controls.Add(lbl);
            parent.Controls.Add(textBox);
            parent.Controls.Add(btn);

            return top + 56;
        }

        private static void StyleTextBox(TextBox tb)
        {
            tb.BackColor = InputBg;
            tb.ForeColor = TextPrimary;
            tb.BorderStyle = BorderStyle.FixedSingle;
            tb.Font = new Font("Segoe UI", 9.5f);
        }

        public void EditGame(GameEntry game)
        {
            _currentGame = game != null ? game.Clone() : new GameEntry();
            _isNewGame = (game == null);

            _titleLabel.Text = _isNewGame ? Strings.GameConfigNewTitle : Strings.GameConfigTitle;
            _btnDelete.Visible = !_isNewGame;

            _txtName.Text = _currentGame.Name;
            _txtLauncher.Text = _currentGame.LauncherName;
            _txtExecutable.Text = _currentGame.MainExecutable;
            _txtTargetPath.Text = _currentGame.TargetPath;
            _txtWorkingDir.Text = _currentGame.WorkingDirectory;
            _txtArguments.Text = _currentGame.Arguments;
            _txtCoverPath.Text = _currentGame.CoverImagePath;

            _txtName.Focus();
        }

        private void BrowseTarget()
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "Selecionar Executável ou Atalho do Jogo";
                ofd.Filter = "Executáveis e Atalhos (*.exe;*.lnk;*.bat;*.cmd)|*.exe;*.lnk;*.bat;*.cmd|Todos os Arquivos (*.*)|*.*";
                if (ofd.ShowDialog(FindForm()) == DialogResult.OK)
                {
                    _txtTargetPath.Text = ofd.FileName;

                    string fileName = Path.GetFileName(ofd.FileName);
                    string nameNoExt = Path.GetFileNameWithoutExtension(ofd.FileName);
                    string dir = Path.GetDirectoryName(ofd.FileName);

                    if (string.IsNullOrWhiteSpace(_txtName.Text))
                        _txtName.Text = nameNoExt;

                    if (string.IsNullOrWhiteSpace(_txtExecutable.Text))
                        _txtExecutable.Text = fileName;

                    if (string.IsNullOrWhiteSpace(_txtWorkingDir.Text))
                        _txtWorkingDir.Text = dir;
                }
            }
        }

        private void BrowseFolder()
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Selecionar Diretório de Trabalho do Jogo";
                if (!string.IsNullOrWhiteSpace(_txtWorkingDir.Text) && Directory.Exists(_txtWorkingDir.Text))
                {
                    fbd.SelectedPath = _txtWorkingDir.Text;
                }
                if (fbd.ShowDialog(FindForm()) == DialogResult.OK)
                {
                    _txtWorkingDir.Text = fbd.SelectedPath;
                }
            }
        }

        private void BrowseImage()
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "Selecionar Capa do Jogo";
                ofd.Filter = "Imagens (*.png;*.jpg;*.jpeg;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.webp|Todos os Arquivos (*.*)|*.*";
                if (ofd.ShowDialog(FindForm()) == DialogResult.OK)
                {
                    _txtCoverPath.Text = ofd.FileName;
                }
            }
        }

        private void OnSaveClicked()
        {
            string name = _txtName.Text.Trim();
            string target = _txtTargetPath.Text.Trim();
            string exe = _txtExecutable.Text.Trim();

            if (string.IsNullOrWhiteSpace(name) || (string.IsNullOrWhiteSpace(target) && string.IsNullOrWhiteSpace(exe)))
            {
                MessageBox.Show(
                    FindForm(),
                    Strings.ValidationErrorPrompt,
                    Strings.ValidationErrorTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            _currentGame.Name = name;
            _currentGame.LauncherName = _txtLauncher.Text.Trim();
            _currentGame.MainExecutable = string.IsNullOrWhiteSpace(exe) ? Path.GetFileName(target) : exe;
            _currentGame.TargetPath = string.IsNullOrWhiteSpace(target) ? exe : target;
            _currentGame.WorkingDirectory = _txtWorkingDir.Text.Trim();
            _currentGame.Arguments = _txtArguments.Text.Trim();
            _currentGame.CoverImagePath = _txtCoverPath.Text.Trim();

            SaveRequested?.Invoke(_currentGame);
        }

        private void OnDeleteClicked()
        {
            if (_currentGame == null || _isNewGame) return;

            var result = MessageBox.Show(
                FindForm(),
                string.Format(Strings.ConfirmDeletePrompt, _currentGame.Name),
                Strings.ConfirmDeleteTitle,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                DeleteRequested?.Invoke(_currentGame);
            }
        }

        private bool _lastBtnB;

        public void ResetInputState()
        {
            _lastBtnB = true;
        }

        public void ProcessGamepad(GamepadState state)
        {
            bool btnB = state.IsPressed(GamepadButtons.B);
            if (btnB && !_lastBtnB)
            {
                CancelRequested?.Invoke();
            }
            _lastBtnB = btnB;
        }
    }
}
