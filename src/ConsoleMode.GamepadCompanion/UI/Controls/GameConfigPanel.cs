using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.UI.Controls
{
    /// <summary>
    /// Painel de formulário estilo Steam Shortcut para cadastro e edição dos 7 campos de um jogo.
    /// Inclui navegação espacial completa por Gamepad (D-Pad/Thumbstick) e integração com Teclado Virtual.
    /// </summary>
    internal sealed class GameConfigPanel : UserControl
    {
        private static readonly Color BgColor = Color.FromArgb(24, 26, 32);
        private static readonly Color HeaderBg = Color.FromArgb(30, 32, 40);
        private static readonly Color CardBg = Color.FromArgb(34, 37, 46);
        private static readonly Color TextPrimary = Color.FromArgb(230, 232, 240);
        private static readonly Color TextSecondary = Color.FromArgb(160, 170, 185);
        private static readonly Color BorderColor = Color.FromArgb(120, 126, 140);
        private static readonly Color BorderFocused = Color.FromArgb(120, 190, 255);
        private static readonly Color InputBg = Color.FromArgb(20, 22, 28);
        private static readonly Color InputFocusedBg = Color.FromArgb(35, 50, 78);

        private readonly Label _titleLabel = new Label();
        private readonly Panel _contentPanel = new Panel();

        private readonly TextBox _txtName = new TextBox();
        private readonly TextBox _txtLauncher = new TextBox();
        private readonly TextBox _txtExecutable = new TextBox();
        private readonly TextBox _txtTargetPath = new TextBox();
        private readonly TextBox _txtWorkingDir = new TextBox();
        private readonly TextBox _txtArguments = new TextBox();
        private readonly TextBox _txtCoverPath = new TextBox();

        private readonly Button _btnBrowseTarget = new Button();
        private readonly Button _btnBrowseWorkingDir = new Button();
        private readonly Button _btnBrowseCover = new Button();

        private readonly Button _btnSave = new Button();
        private readonly Button _btnCancel = new Button();
        private readonly Button _btnDelete = new Button();

        private readonly List<List<Control>> _navGrid = new List<List<Control>>();
        private int _navRow;
        private int _navCol;

        private GameEntry _currentGame;
        private bool _isNewGame;

        // Repeat e debounce de Gamepad
        private bool _lastDpadUp;
        private bool _lastDpadDown;
        private bool _lastDpadLeft;
        private bool _lastDpadRight;
        private bool _lastBtnA;
        private bool _lastBtnB;
        private long _lastMoveTime;
        private int _repeatCount;
        private const long InitialRepeatDelayMs = 250;
        private const long RepeatIntervalMs = 90;

        public event Action<GameEntry> SaveRequested;
        public event Action<GameEntry> DeleteRequested;
        public event Action CancelRequested;
        public event Action<string, string> ValidationFailed;
        public event Action<string, string, Action<string>> RequestVirtualKeyboard;

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
            currentY = AddFieldWithBrowse(_contentPanel, Strings.GameConfigTargetPath, _txtTargetPath, _btnBrowseTarget, currentY, BrowseTarget);
            currentY = AddFieldWithBrowse(_contentPanel, Strings.GameConfigWorkingDir, _txtWorkingDir, _btnBrowseWorkingDir, currentY, BrowseFolder);
            currentY = AddField(_contentPanel, Strings.GameConfigArguments, _txtArguments, currentY);
            currentY = AddFieldWithBrowse(_contentPanel, Strings.GameConfigCoverImage, _txtCoverPath, _btnBrowseCover, currentY, BrowseImage);

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

            BuildNavGrid();
        }

        private void BuildNavGrid()
        {
            _navGrid.Clear();
            _navGrid.Add(new List<Control> { _txtName });
            _navGrid.Add(new List<Control> { _txtLauncher });
            _navGrid.Add(new List<Control> { _txtExecutable });
            _navGrid.Add(new List<Control> { _txtTargetPath, _btnBrowseTarget });
            _navGrid.Add(new List<Control> { _txtWorkingDir, _btnBrowseWorkingDir });
            _navGrid.Add(new List<Control> { _txtArguments });
            _navGrid.Add(new List<Control> { _txtCoverPath, _btnBrowseCover });

            var actions = new List<Control> { _btnSave, _btnCancel };
            if (!_isNewGame)
            {
                actions.Add(_btnDelete);
            }
            _navGrid.Add(actions);
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

        private int AddFieldWithBrowse(Panel parent, string labelText, TextBox textBox, Button btn, int top, Action onBrowse)
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

            btn.Text = Strings.BtnBrowse;
            btn.FlatStyle = FlatStyle.Flat;
            btn.Font = new Font("Segoe UI", 8.5f);
            btn.BackColor = CardBg;
            btn.ForeColor = TextPrimary;
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

            BuildNavGrid();
            _navRow = 0;
            _navCol = 0;
            UpdateFocusVisual();
        }

        private void UpdateFocusVisual()
        {
            Control focusedCtrl = GetCurrentControl();

            for (int r = 0; r < _navGrid.Count; r++)
            {
                var row = _navGrid[r];
                for (int c = 0; c < row.Count; c++)
                {
                    var ctrl = row[c];
                    bool isTarget = (ctrl == focusedCtrl);

                    if (ctrl is TextBox tb)
                    {
                        tb.BackColor = isTarget ? InputFocusedBg : InputBg;
                        if (isTarget)
                        {
                            tb.Focus();
                            tb.SelectAll();
                            _contentPanel.ScrollControlIntoView(tb);
                        }
                    }
                    else if (ctrl is Button btn)
                    {
                        if (isTarget)
                        {
                            btn.FlatAppearance.BorderSize = 2;
                            btn.FlatAppearance.BorderColor = BorderFocused;
                            btn.Focus();
                            _contentPanel.ScrollControlIntoView(btn);
                        }
                        else
                        {
                            btn.FlatAppearance.BorderSize = (btn == _btnCancel || btn.Text == Strings.BtnBrowse) ? 1 : 0;
                            btn.FlatAppearance.BorderColor = BorderColor;
                        }
                    }
                }
            }
        }

        private Control GetCurrentControl()
        {
            if (_navRow >= 0 && _navRow < _navGrid.Count)
            {
                var row = _navGrid[_navRow];
                if (_navCol >= 0 && _navCol < row.Count)
                {
                    return row[_navCol];
                }
            }
            return null;
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
                ValidationFailed?.Invoke(Strings.ValidationErrorTitle, Strings.ValidationErrorPrompt);
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
            DeleteRequested?.Invoke(_currentGame);
        }

        public void ResetInputState()
        {
            _lastBtnA = true;
            _lastBtnB = true;
            _lastDpadUp = false;
            _lastDpadDown = false;
            _lastDpadLeft = false;
            _lastDpadRight = false;
            _repeatCount = 0;
            _lastMoveTime = 0;
            UpdateFocusVisual();
        }

        public void ProcessGamepad(GamepadState state, long currentTimeMs = 0)
        {
            if (!state.IsConnected) return;

            bool dpadUp = state.IsPressed(GamepadButtons.DPadUp) || state.LeftThumbY > 16000;
            bool dpadDown = state.IsPressed(GamepadButtons.DPadDown) || state.LeftThumbY < -16000;
            bool dpadLeft = state.IsPressed(GamepadButtons.DPadLeft) || state.LeftThumbX < -16000;
            bool dpadRight = state.IsPressed(GamepadButtons.DPadRight) || state.LeftThumbX > 16000;

            bool btnA = state.IsPressed(GamepadButtons.A);
            bool btnB = state.IsPressed(GamepadButtons.B);

            // Botão B sempre cancela
            if (btnB && !_lastBtnB)
            {
                CancelRequested?.Invoke();
                return;
            }
            _lastBtnB = btnB;

            // Botão A aciona o elemento focado
            if (btnA && !_lastBtnA)
            {
                ExecuteFocusedControl();
                _lastBtnA = btnA;
                return;
            }
            _lastBtnA = btnA;

            // Navegação Direcional D-Pad / Thumbstick com Repeat
            bool isMoving = dpadUp || dpadDown || dpadLeft || dpadRight;
            if (!isMoving)
            {
                _lastDpadUp = false;
                _lastDpadDown = false;
                _lastDpadLeft = false;
                _lastDpadRight = false;
                _repeatCount = 0;
                return;
            }

            bool isFirstPress = (dpadUp && !_lastDpadUp) ||
                                (dpadDown && !_lastDpadDown) ||
                                (dpadLeft && !_lastDpadLeft) ||
                                (dpadRight && !_lastDpadRight);

            bool shouldStep = false;
            if (isFirstPress)
            {
                shouldStep = true;
                _lastMoveTime = currentTimeMs;
                _repeatCount = 0;
            }
            else
            {
                long elapsed = currentTimeMs - _lastMoveTime;
                long required = _repeatCount == 0 ? InitialRepeatDelayMs : RepeatIntervalMs;
                if (elapsed >= required)
                {
                    shouldStep = true;
                    _lastMoveTime = currentTimeMs;
                    _repeatCount++;
                }
            }

            if (shouldStep)
            {
                if (dpadUp && _navRow > 0)
                {
                    _navRow--;
                    _navCol = Math.Min(_navCol, _navGrid[_navRow].Count - 1);
                    UpdateFocusVisual();
                }
                else if (dpadDown && _navRow < _navGrid.Count - 1)
                {
                    _navRow++;
                    _navCol = Math.Min(_navCol, _navGrid[_navRow].Count - 1);
                    UpdateFocusVisual();
                }
                else if (dpadLeft && _navCol > 0)
                {
                    _navCol--;
                    UpdateFocusVisual();
                }
                else if (dpadRight && _navCol < _navGrid[_navRow].Count - 1)
                {
                    _navCol++;
                    UpdateFocusVisual();
                }
            }

            _lastDpadUp = dpadUp;
            _lastDpadDown = dpadDown;
            _lastDpadLeft = dpadLeft;
            _lastDpadRight = dpadRight;
        }

        private void ExecuteFocusedControl()
        {
            var ctrl = GetCurrentControl();
            if (ctrl == null) return;

            if (ctrl is TextBox tb)
            {
                string title = GetFieldTitle(tb);
                RequestVirtualKeyboard?.Invoke(title, tb.Text, newText =>
                {
                    tb.Text = newText;
                    tb.Focus();
                    tb.SelectAll();
                });
            }
            else if (ctrl == _btnBrowseTarget)
            {
                BrowseTarget();
            }
            else if (ctrl == _btnBrowseWorkingDir)
            {
                BrowseFolder();
            }
            else if (ctrl == _btnBrowseCover)
            {
                BrowseImage();
            }
            else if (ctrl == _btnSave)
            {
                OnSaveClicked();
            }
            else if (ctrl == _btnCancel)
            {
                CancelRequested?.Invoke();
            }
            else if (ctrl == _btnDelete)
            {
                OnDeleteClicked();
            }
        }

        private string GetFieldTitle(TextBox tb)
        {
            if (tb == _txtName) return Strings.GameConfigName;
            if (tb == _txtLauncher) return Strings.GameConfigLauncher;
            if (tb == _txtExecutable) return Strings.GameConfigExecutable;
            if (tb == _txtTargetPath) return Strings.GameConfigTargetPath;
            if (tb == _txtWorkingDir) return Strings.GameConfigWorkingDir;
            if (tb == _txtArguments) return Strings.GameConfigArguments;
            if (tb == _txtCoverPath) return Strings.GameConfigCoverImage;
            return Strings.VirtualKeyboardDefaultTitle;
        }
    }
}
