using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.UI.Navigation;

namespace ConsoleMode.GamepadCompanion.UI.Controls
{
    /// <summary>
    /// Tipos de diálogo modal Couch Gaming suportados.
    /// </summary>
    public enum MessageDialogType
    {
        Information,
        Warning,
        Error,
        Confirmation
    }

    /// <summary>
    /// Painel de overlay escurecido para edição modal de sliders e dropdown via gamepad.
    /// Renderiza a interface escurecida e destaca o elemento ativo no estilo Couch Gaming.
    /// </summary>
    public sealed class FocusOverlayPanel : Control
    {
        private Bitmap _snapshot;
        private INavigableControl _activeControl;

        private bool _isExitDialog;
        private int _exitOptionIndex;
        private Action _onCloseApp;
        private Action _onMinimizeToTray;
        private Action _onCancelExit;
        private Rectangle _exitOption0Rect;
        private Rectangle _exitOption1Rect;

        private bool _prevExitA;
        private bool _prevExitB;
        private bool _prevExitUp;
        private bool _prevExitDown;

        // Diálogos modais customizados Couch Gaming
        private bool _isMessageDialog;
        private MessageDialogType _messageType;
        private string _messageTitle;
        private string _messageText;
        private Action _onMessageOk;
        private Action _onMessageCancel;
        private int _messageOptionIndex;
        private Rectangle _msgBtn0Rect;
        private Rectangle _msgBtn1Rect;

        private bool _prevMsgA;
        private bool _prevMsgB;
        private bool _prevMsgLeft;
        private bool _prevMsgRight;

        // Teclado Virtual Couch Gaming
        private bool _isVirtualKeyboard;
        private VirtualKeyboard.VirtualKeyboardControl _keyboardControl;
        private Action<string> _onKeyboardConfirm;
        private Action _onKeyboardCancel;
        private Action<string> _currentLiveChangeHandler;
        private Rectangle _keyboardCardRect;

        // Diálogo de API Key do SteamGridDB
        private bool _isSteamGridApiKeyDialog;
        private SteamGridApiKeyDialogControl _apiKeyDialogControl;
        private Engine.Services.SteamGridDbService _steamGridService;
        private Action<string> _onApiKeyConfirm;
        private Action _onApiKeyCancel;

        public FocusOverlayPanel()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.Selectable,
                true);
            Visible = false;
        }

        public bool IsActive => _isExitDialog || _isMessageDialog || _isVirtualKeyboard || _isSteamGridApiKeyDialog || (_activeControl != null && _activeControl.IsEditing);

        public bool IsExitDialogOpen => _isExitDialog;

        public bool IsMessageDialogOpen => _isMessageDialog;

        public bool IsVirtualKeyboardOpen => _isVirtualKeyboard;

        public bool IsSteamGridApiKeyDialogOpen => _isSteamGridApiKeyDialog;

        public void ShowOverlay(INavigableControl control, Form parent)
        {
            if (control == null || parent == null) return;

            // Se o overlay já está ativo para este controle, apenas redesenha sem recapturar tela
            if (Visible && _activeControl == control && !_isExitDialog)
            {
                Invalidate();
                return;
            }

            if (_activeControl != null)
            {
                _activeControl.StateChanged -= OnActiveControlStateChanged;
            }

            _activeControl = control;
            _activeControl.StateChanged += OnActiveControlStateChanged;
            _isExitDialog = false;

            Bounds = new Rectangle(0, 0, parent.ClientSize.Width, parent.ClientSize.Height);

            // Tira snapshot limpo apenas da área cliente do formulário
            _snapshot?.Dispose();
            _snapshot = CaptureClientArea(parent);

            BringToFront();
            Visible = true;
            Invalidate();
        }

        public void ShowExitDialog(Form parent, Action onCloseApp, Action onMinimizeToTray, Action onCancel = null)
        {
            if (parent == null) return;

            if (Visible && _isExitDialog)
            {
                Invalidate();
                return;
            }

            if (_activeControl != null)
            {
                _activeControl.StateChanged -= OnActiveControlStateChanged;
                _activeControl = null;
            }

            _isExitDialog = true;
            _exitOptionIndex = 0; // Padrão: Minimizar para a barra de tarefas
            _onCloseApp = onCloseApp;
            _onMinimizeToTray = onMinimizeToTray;
            _onCancelExit = onCancel;

            _prevExitA = true; // Evita disparo acidental se botão A ainda estiver pressionado do clique
            _prevExitB = false;
            _prevExitUp = false;
            _prevExitDown = false;

            Bounds = new Rectangle(0, 0, parent.ClientSize.Width, parent.ClientSize.Height);

            _snapshot?.Dispose();
            _snapshot = CaptureClientArea(parent);

            BringToFront();
            Visible = true;
            Invalidate();
        }

        private static Bitmap CaptureClientArea(Form parent)
        {
            int w = Math.Max(1, parent.ClientSize.Width);
            int h = Math.Max(1, parent.ClientSize.Height);
            var bmp = new Bitmap(w, h);

            try
            {
                if (parent.IsHandleCreated && parent.Visible)
                {
                    Point origin = parent.PointToScreen(Point.Empty);
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(origin.X, origin.Y, 0, 0, new Size(w, h), CopyPixelOperation.SourceCopy);
                    }
                    return bmp;
                }
            }
            catch
            {
                // Fallback seguro caso esteja em execução headless/teste
            }

            using (var g = Graphics.FromImage(bmp))
            using (var brush = new SolidBrush(Color.FromArgb(30, 32, 40)))
            {
                g.FillRectangle(brush, 0, 0, w, h);
            }
            return bmp;
        }

        public void CloseExitDialog()
        {
            _isExitDialog = false;
            _onCloseApp = null;
            _onMinimizeToTray = null;
            _onCancelExit = null;

            Visible = false;
            _snapshot?.Dispose();
            _snapshot = null;
            Parent?.Invalidate(true);
        }

        public void ProcessExitGamepad(GamepadState state, long nowMs)
        {
            if (!state.IsConnected || !_isExitDialog) return;

            // Botão A confirma a opção selecionada
            bool aPressed = state.IsPressed(GamepadButtons.A);
            if (aPressed && !_prevExitA)
            {
                if (_exitOptionIndex == 0)
                {
                    var act = _onMinimizeToTray;
                    CloseExitDialog();
                    act?.Invoke();
                }
                else
                {
                    var act = _onCloseApp;
                    CloseExitDialog();
                    act?.Invoke();
                }
                return;
            }
            _prevExitA = aPressed;

            // Botão B cancela e retorna para a tela principal
            bool bPressed = state.IsPressed(GamepadButtons.B);
            if (bPressed && !_prevExitB)
            {
                var cancel = _onCancelExit;
                CloseExitDialog();
                cancel?.Invoke();
                return;
            }
            _prevExitB = bPressed;

            // D-Pad Up e Down alternam entre as 2 opções
            bool up = state.IsPressed(GamepadButtons.DPadUp);
            if (up && !_prevExitUp)
            {
                _exitOptionIndex = _exitOptionIndex == 0 ? 1 : 0;
                Invalidate();
            }
            _prevExitUp = up;

            bool down = state.IsPressed(GamepadButtons.DPadDown);
            if (down && !_prevExitDown)
            {
                _exitOptionIndex = _exitOptionIndex == 0 ? 1 : 0;
                Invalidate();
            }
            _prevExitDown = down;
        }

        public void ShowMessage(Form parent, string title, string message, MessageDialogType type = MessageDialogType.Information, Action onOk = null)
        {
            if (parent == null) return;

            if (Visible && _isMessageDialog)
            {
                Invalidate();
                return;
            }

            if (_activeControl != null)
            {
                _activeControl.StateChanged -= OnActiveControlStateChanged;
                _activeControl = null;
            }

            _isExitDialog = false;
            _isMessageDialog = true;
            _messageType = type;
            _messageTitle = title ?? string.Empty;
            _messageText = message ?? string.Empty;
            _onMessageOk = onOk;
            _onMessageCancel = null;
            _messageOptionIndex = 0;

            _prevMsgA = true;
            _prevMsgB = true;
            _prevMsgLeft = false;
            _prevMsgRight = false;

            Bounds = new Rectangle(0, 0, parent.ClientSize.Width, parent.ClientSize.Height);

            _snapshot?.Dispose();
            _snapshot = CaptureClientArea(parent);

            BringToFront();
            Visible = true;
            Invalidate();
        }

        public void ShowConfirmation(Form parent, string title, string message, Action onConfirm, Action onCancel = null)
        {
            if (parent == null) return;

            if (Visible && _isMessageDialog)
            {
                Invalidate();
                return;
            }

            if (_activeControl != null)
            {
                _activeControl.StateChanged -= OnActiveControlStateChanged;
                _activeControl = null;
            }

            _isExitDialog = false;
            _isMessageDialog = true;
            _messageType = MessageDialogType.Confirmation;
            _messageTitle = title ?? string.Empty;
            _messageText = message ?? string.Empty;
            _onMessageOk = onConfirm;
            _onMessageCancel = onCancel;
            _messageOptionIndex = 0;

            _prevMsgA = true;
            _prevMsgB = true;
            _prevMsgLeft = false;
            _prevMsgRight = false;

            Bounds = new Rectangle(0, 0, parent.ClientSize.Width, parent.ClientSize.Height);

            _snapshot?.Dispose();
            _snapshot = CaptureClientArea(parent);

            BringToFront();
            Visible = true;
            Invalidate();
        }

        public void CloseMessageDialog()
        {
            _isMessageDialog = false;
            _onMessageOk = null;
            _onMessageCancel = null;
            Visible = false;
            _snapshot?.Dispose();
            _snapshot = null;
            Parent?.Invalidate(true);
        }

        public void ProcessMessageGamepad(GamepadState state, long nowMs)
        {
            if (!state.IsConnected || !_isMessageDialog) return;

            bool aPressed = state.IsPressed(GamepadButtons.A);
            bool bPressed = state.IsPressed(GamepadButtons.B);
            bool leftPressed = state.IsPressed(GamepadButtons.DPadLeft) || state.LeftThumbX < -16000;
            bool rightPressed = state.IsPressed(GamepadButtons.DPadRight) || state.LeftThumbX > 16000;

            if (_messageType != MessageDialogType.Confirmation)
            {
                // Qualquer botão A ou B fecha e aciona onOk
                if ((aPressed && !_prevMsgA) || (bPressed && !_prevMsgB))
                {
                    var ok = _onMessageOk;
                    CloseMessageDialog();
                    ok?.Invoke();
                    return;
                }
            }
            else
            {
                // Alternar seleção entre 0 (Confirmar) e 1 (Cancelar)
                if (leftPressed && !_prevMsgLeft)
                {
                    _messageOptionIndex = 0;
                    Invalidate();
                }
                _prevMsgLeft = leftPressed;

                if (rightPressed && !_prevMsgRight)
                {
                    _messageOptionIndex = 1;
                    Invalidate();
                }
                _prevMsgRight = rightPressed;

                // Botão A aciona a opção selecionada
                if (aPressed && !_prevMsgA)
                {
                    if (_messageOptionIndex == 0)
                    {
                        var ok = _onMessageOk;
                        CloseMessageDialog();
                        ok?.Invoke();
                    }
                    else
                    {
                        var cancel = _onMessageCancel;
                        CloseMessageDialog();
                        cancel?.Invoke();
                    }
                    return;
                }

                // Botão B sempre cancela
                if (bPressed && !_prevMsgB)
                {
                    var cancel = _onMessageCancel;
                    CloseMessageDialog();
                    cancel?.Invoke();
                    return;
                }
            }

            _prevMsgA = aPressed;
            _prevMsgB = bPressed;
        }

        public void ShowVirtualKeyboard(Form parent, string title, string initialText, Action<string> onConfirm, Action onCancel = null, Action<string> onLiveTextChange = null)
        {
            if (parent == null) return;

            if (Visible && _isVirtualKeyboard)
            {
                Invalidate();
                return;
            }

            if (_activeControl != null)
            {
                _activeControl.StateChanged -= OnActiveControlStateChanged;
                _activeControl = null;
            }

            _isExitDialog = false;
            _isMessageDialog = false;
            _isVirtualKeyboard = true;

            _onKeyboardConfirm = onConfirm;
            _onKeyboardCancel = onCancel;
            _currentLiveChangeHandler = onLiveTextChange;

            Bounds = new Rectangle(0, 0, parent.ClientSize.Width, parent.ClientSize.Height);

            if (_keyboardControl == null)
            {
                _keyboardControl = new VirtualKeyboard.VirtualKeyboardControl();
                Controls.Add(_keyboardControl);
            }

            UpdateKeyboardBounds(parent.ClientSize);
            _keyboardControl.SetInitialText(initialText, title);

            // Re-inscreve eventos
            _keyboardControl.BufferTextChanged -= OnKeyboardLiveChanged;
            _keyboardControl.Confirmed -= OnKeyboardConfirmed;
            _keyboardControl.Cancelled -= OnKeyboardCancelled;

            _keyboardControl.BufferTextChanged += OnKeyboardLiveChanged;
            _keyboardControl.Confirmed += OnKeyboardConfirmed;
            _keyboardControl.Cancelled += OnKeyboardCancelled;

            _keyboardControl.Visible = true;
            _keyboardControl.BringToFront();

            _snapshot?.Dispose();
            _snapshot = null;
            RefreshLiveBackground(parent);

            BringToFront();
            Visible = true;
            Invalidate();
        }

        private void UpdateKeyboardBounds(Size parentSize)
        {
            if (_keyboardControl == null) return;

            int cardW = Math.Min(800, parentSize.Width - 32);
            int cardH = Math.Min(340, parentSize.Height - 30);
            int cardX = (parentSize.Width - cardW) / 2;
            int cardY = Math.Max(12, parentSize.Height - cardH - 14);

            _keyboardCardRect = new Rectangle(cardX, cardY, cardW, cardH);
            _keyboardControl.SetBounds(cardX, cardY, cardW, cardH);
        }

        public void RefreshLiveBackground(Form parent)
        {
            if (parent == null) return;
            int w = Math.Max(1, parent.ClientSize.Width);
            int h = Math.Max(1, parent.ClientSize.Height);

            try
            {
                var bmp = new Bitmap(w, h);
                using (var g = Graphics.FromImage(bmp))
                {
                    using (var bgBrush = new SolidBrush(parent.BackColor))
                    {
                        g.FillRectangle(bgBrush, 0, 0, w, h);
                    }

                    for (int i = parent.Controls.Count - 1; i >= 0; i--)
                    {
                        var ctrl = parent.Controls[i];
                        if (ctrl != this && ctrl.Visible && ctrl.Width > 0 && ctrl.Height > 0)
                        {
                            using (var ctrlBmp = new Bitmap(ctrl.Width, ctrl.Height))
                            {
                                ctrl.DrawToBitmap(ctrlBmp, new Rectangle(0, 0, ctrl.Width, ctrl.Height));
                                g.DrawImageUnscaled(ctrlBmp, ctrl.Location.X, ctrl.Location.Y);
                            }
                        }
                    }
                }

                _snapshot?.Dispose();
                _snapshot = bmp;
                Invalidate();
            }
            catch
            {
                // Fallback seguro se controle estiver indisponível
            }
        }

        private void OnKeyboardLiveChanged(string text)
        {
            _currentLiveChangeHandler?.Invoke(text);
        }

        private void OnKeyboardConfirmed(string text)
        {
            var conf = _onKeyboardConfirm;
            CloseVirtualKeyboard();
            conf?.Invoke(text);
        }

        private void OnKeyboardCancelled()
        {
            var canc = _onKeyboardCancel;
            CloseVirtualKeyboard();
            canc?.Invoke();
        }

        public void CloseVirtualKeyboard()
        {
            _isVirtualKeyboard = false;
            if (_keyboardControl != null)
            {
                _keyboardControl.Visible = false;
                _keyboardControl.BufferTextChanged -= OnKeyboardLiveChanged;
                _keyboardControl.Confirmed -= OnKeyboardConfirmed;
                _keyboardControl.Cancelled -= OnKeyboardCancelled;
            }
            _currentLiveChangeHandler = null;
            _onKeyboardConfirm = null;
            _onKeyboardCancel = null;

            Visible = false;
            _snapshot?.Dispose();
            _snapshot = null;
            Parent?.Invalidate(true);
        }

        public void ProcessVirtualKeyboardGamepad(GamepadState state, long nowMs)
        {
            if (!state.IsConnected || !_isVirtualKeyboard || _keyboardControl == null) return;
            _keyboardControl.ProcessGamepad(state, nowMs);
        }

        public void ShowSteamGridApiKeyDialog(
            Form parent,
            string initialKey,
            Action<string> onConfirmed,
            Action onCancel = null,
            Engine.Services.SteamGridDbService service = null)
        {
            if (parent == null) return;

            _isExitDialog = false;
            _isMessageDialog = false;
            _isVirtualKeyboard = false;
            _isSteamGridApiKeyDialog = true;
            _onApiKeyConfirm = onConfirmed;
            _onApiKeyCancel = onCancel;

            Bounds = new Rectangle(0, 0, parent.ClientSize.Width, parent.ClientSize.Height);

            if (service != null)
            {
                _steamGridService = service;
            }

            if (_apiKeyDialogControl == null)
            {
                _apiKeyDialogControl = new SteamGridApiKeyDialogControl(_steamGridService);
                _apiKeyDialogControl.Visible = false;
                Controls.Add(_apiKeyDialogControl);
            }

            UpdateApiKeyDialogBounds(parent.ClientSize);
            _apiKeyDialogControl.SetInitialKey(initialKey);

            _apiKeyDialogControl.KeyConfirmed -= OnApiKeyConfirmed;
            _apiKeyDialogControl.Cancelled -= OnApiKeyCancelled;
            _apiKeyDialogControl.RequestVirtualKeyboard -= OnApiKeyRequestVirtualKeyboard;

            _apiKeyDialogControl.KeyConfirmed += OnApiKeyConfirmed;
            _apiKeyDialogControl.Cancelled += OnApiKeyCancelled;
            _apiKeyDialogControl.RequestVirtualKeyboard += OnApiKeyRequestVirtualKeyboard;

            _apiKeyDialogControl.Visible = true;
            _apiKeyDialogControl.BringToFront();

            _snapshot?.Dispose();
            _snapshot = null;
            RefreshLiveBackground(parent);

            BringToFront();
            Visible = true;
            Invalidate();
        }

        private void UpdateApiKeyDialogBounds(Size parentSize)
        {
            if (_apiKeyDialogControl == null) return;

            int cardW = Math.Min(580, parentSize.Width - 32);
            int cardH = Math.Min(380, parentSize.Height - 32);
            int cardX = (parentSize.Width - cardW) / 2;
            int cardY = (parentSize.Height - cardH) / 2;

            _apiKeyDialogControl.SetBounds(cardX, cardY, cardW, cardH);
        }

        private void OnApiKeyConfirmed(string key)
        {
            var conf = _onApiKeyConfirm;
            CloseSteamGridApiKeyDialog();
            conf?.Invoke(key);
        }

        private void OnApiKeyCancelled()
        {
            var canc = _onApiKeyCancel;
            CloseSteamGridApiKeyDialog();
            canc?.Invoke();
        }

        private void OnApiKeyRequestVirtualKeyboard(string currentText, Action<string> onDone)
        {
            if (Parent is Form parentForm)
            {
                if (_apiKeyDialogControl != null)
                {
                    _apiKeyDialogControl.Visible = false;
                }

                ShowVirtualKeyboard(
                    parentForm,
                    "Chave de API do SteamGridDB",
                    currentText,
                    newKey =>
                    {
                        onDone(newKey);
                        ShowSteamGridApiKeyDialog(parentForm, newKey, _onApiKeyConfirm, _onApiKeyCancel, _steamGridService);
                    },
                    onCancel: () =>
                    {
                        ShowSteamGridApiKeyDialog(parentForm, currentText, _onApiKeyConfirm, _onApiKeyCancel, _steamGridService);
                    }
                );
            }
        }

        public void CloseSteamGridApiKeyDialog()
        {
            _isSteamGridApiKeyDialog = false;
            if (_apiKeyDialogControl != null)
            {
                _apiKeyDialogControl.Visible = false;
                _apiKeyDialogControl.KeyConfirmed -= OnApiKeyConfirmed;
                _apiKeyDialogControl.Cancelled -= OnApiKeyCancelled;
                _apiKeyDialogControl.RequestVirtualKeyboard -= OnApiKeyRequestVirtualKeyboard;
            }
            _onApiKeyConfirm = null;
            _onApiKeyCancel = null;

            Visible = false;
            _snapshot?.Dispose();
            _snapshot = null;
            Parent?.Invalidate(true);
        }

        public void ProcessSteamGridApiKeyGamepad(GamepadState state, long nowMs)
        {
            if (!state.IsConnected || !_isSteamGridApiKeyDialog || _apiKeyDialogControl == null) return;
            _apiKeyDialogControl.ProcessGamepad(state, nowMs);
        }

        public void HideOverlay()
        {
            if (_activeControl != null)
            {
                _activeControl.StateChanged -= OnActiveControlStateChanged;
                _activeControl = null;
            }

            if (_isVirtualKeyboard)
            {
                CloseVirtualKeyboard();
            }

            if (_isSteamGridApiKeyDialog)
            {
                CloseSteamGridApiKeyDialog();
            }

            _isExitDialog = false;
            _isMessageDialog = false;
            _onMessageOk = null;
            _onMessageCancel = null;
            Visible = false;
            _snapshot?.Dispose();
            _snapshot = null;
            Parent?.Invalidate(true);
        }

        private void OnActiveControlStateChanged()
        {
            if (_activeControl == null || !_activeControl.IsEditing)
            {
                HideOverlay();
                return;
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 1. Desenha o snapshot de fundo capturado
            if (_snapshot != null)
            {
                g.DrawImageUnscaled(_snapshot, 0, 0);
            }
            else
            {
                using (var bgBrush = new SolidBrush(Color.FromArgb(30, 32, 40)))
                {
                    g.FillRectangle(bgBrush, ClientRectangle);
                }
            }

            // 2. Aplica a camada de escurecimento semi-transparente (mais suave quando o teclado está ativo para permitir visualização dos resultados)
            int dimAlpha = _isVirtualKeyboard ? 130 : 190;
            using (var dimBrush = new SolidBrush(Color.FromArgb(dimAlpha, 15, 17, 22)))
            {
                g.FillRectangle(dimBrush, ClientRectangle);
            }

            // 3. Renderiza o card do controle ativo em destaque
            if (_isExitDialog)
            {
                DrawExitDialogCard(g);
            }
            else if (_isMessageDialog)
            {
                DrawMessageDialogCard(g);
            }
            else if (_isVirtualKeyboard)
            {
                // Sombra suave e moldura de contorno atrás do teclado
                using (var cardBorderPen = new Pen(Color.FromArgb(60, 68, 85), 1.5f))
                {
                    DrawRoundedRectangle(g, cardBorderPen, _keyboardCardRect, 8);
                }
            }
            else if (_activeControl is SliderNavigable slider)
            {
                DrawSliderCard(g, slider);
            }
            else if (_activeControl is DropdownNavigable dropdown)
            {
                DrawDropdownCard(g, dropdown);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_isVirtualKeyboard && Parent != null)
            {
                UpdateKeyboardBounds(Parent.ClientSize);
            }
            if (_isSteamGridApiKeyDialog && Parent != null)
            {
                UpdateApiKeyDialogBounds(Parent.ClientSize);
            }
        }

        private void DrawSliderCard(Graphics g, SliderNavigable slider)
        {
            int cardW = 440;
            int cardH = 180;
            int cardX = (ClientSize.Width - cardW) / 2;
            int cardY = (ClientSize.Height - cardH) / 2;
            var cardRect = new Rectangle(cardX, cardY, cardW, cardH);

            // Card background & borda
            using (var cardBrush = new SolidBrush(Color.FromArgb(34, 37, 46)))
            using (var borderPen = new Pen(Color.FromArgb(120, 190, 255), 2f))
            {
                FillRoundedRectangle(g, cardBrush, cardRect, 8);
                DrawRoundedRectangle(g, borderPen, cardRect, 8);
            }

            // Título
            using (var titleFont = new Font("Segoe UI", 11.5f, FontStyle.Bold))
            using (var textBrush = new SolidBrush(Color.FromArgb(230, 232, 240)))
            {
                g.DrawString(slider.Title, titleFont, textBrush, cardX + 24, cardY + 20);
            }

            // Valor em destaque
            string valueDisplay = slider.Value.ToString();
            if (slider.Title.Contains("%"))
            {
                valueDisplay += "%";
            }

            using (var valueFont = new Font("Segoe UI", 16f, FontStyle.Bold))
            using (var valueBrush = new SolidBrush(Color.FromArgb(120, 190, 255)))
            {
                var valSize = g.MeasureString(valueDisplay, valueFont);
                g.DrawString(valueDisplay, valueFont, valueBrush, cardX + cardW - 24 - valSize.Width, cardY + 16);
            }

            // Barra gráfica de progresso
            int barX = cardX + 24;
            int barY = cardY + 68;
            int barW = cardW - 48;
            int barH = 14;
            var trackRect = new Rectangle(barX, barY, barW, barH);

            using (var trackBrush = new SolidBrush(Color.FromArgb(52, 56, 66)))
            {
                FillRoundedRectangle(g, trackBrush, trackRect, 7);
            }

            int min = slider.Minimum;
            int max = slider.Maximum;
            int val = slider.Value;
            float ratio = max > min ? (float)(val - min) / (max - min) : 0f;
            ratio = Math.Max(0f, Math.Min(1f, ratio));
            int fillW = Math.Max(barH, (int)(barW * ratio));
            var fillRect = new Rectangle(barX, barY, fillW, barH);

            using (var fillBrush = new SolidBrush(Color.FromArgb(90, 170, 255)))
            {
                FillRoundedRectangle(g, fillBrush, fillRect, 7);
            }

            // Indicador / Thumb
            int thumbX = barX + (int)(barW * ratio);
            int thumbY = barY + (barH / 2);
            int thumbR = 10;
            var thumbRect = new Rectangle(thumbX - thumbR, thumbY - thumbR, thumbR * 2, thumbR * 2);

            using (var thumbBrush = new SolidBrush(Color.White))
            using (var thumbBorder = new Pen(Color.FromArgb(120, 190, 255), 2f))
            {
                g.FillEllipse(thumbBrush, thumbRect);
                g.DrawEllipse(thumbBorder, thumbRect);
            }

            // Valores Min / Max nos cantos da barra
            using (var minMaxFont = new Font("Segoe UI", 8.5f))
            using (var minMaxBrush = new SolidBrush(Color.FromArgb(160, 170, 185)))
            {
                g.DrawString(min.ToString(), minMaxFont, minMaxBrush, barX, barY + barH + 4);
                string maxStr = max.ToString();
                var maxSize = g.MeasureString(maxStr, minMaxFont);
                g.DrawString(maxStr, minMaxFont, minMaxBrush, barX + barW - maxSize.Width, barY + barH + 4);
            }

            // Legenda de atalhos
            int hintY = cardY + cardH - 34;
            DrawButtonBadge(g, "[◄ / ►]", Color.FromArgb(120, 190, 255), cardX + 24, hintY);
            DrawText(g, "Ajustar", Color.FromArgb(200, 205, 215), cardX + 78, hintY);

            DrawButtonBadge(g, "[A]", Color.FromArgb(120, 230, 150), cardX + 170, hintY);
            DrawText(g, "Confirmar", Color.FromArgb(200, 205, 215), cardX + 198, hintY);

            DrawButtonBadge(g, "[B]", Color.FromArgb(255, 120, 120), cardX + 300, hintY);
            DrawText(g, "Cancelar", Color.FromArgb(200, 205, 215), cardX + 328, hintY);
        }

        private void DrawDropdownCard(Graphics g, DropdownNavigable dropdown)
        {
            var combo = dropdown.ComboBox;
            int count = Math.Max(1, combo.Items.Count);
            int itemH = 36;
            int headerH = 50;
            int footerH = 46;
            int cardW = 380;
            int cardH = headerH + (count * itemH) + footerH;
            int cardX = (ClientSize.Width - cardW) / 2;
            int cardY = Math.Max(20, (ClientSize.Height - cardH) / 2);
            var cardRect = new Rectangle(cardX, cardY, cardW, cardH);

            // Card background & borda
            using (var cardBrush = new SolidBrush(Color.FromArgb(34, 37, 46)))
            using (var borderPen = new Pen(Color.FromArgb(120, 190, 255), 2f))
            {
                FillRoundedRectangle(g, cardBrush, cardRect, 8);
                DrawRoundedRectangle(g, borderPen, cardRect, 8);
            }

            // Título
            using (var titleFont = new Font("Segoe UI", 11.5f, FontStyle.Bold))
            using (var textBrush = new SolidBrush(Color.FromArgb(230, 232, 240)))
            {
                g.DrawString(Strings.SelectActiveController, titleFont, textBrush, cardX + 20, cardY + 16);
            }

            // Linhas dos itens
            int listY = cardY + headerH;
            for (int i = 0; i < count; i++)
            {
                int rowY = listY + (i * itemH);
                var rowRect = new Rectangle(cardX + 16, rowY, cardW - 32, itemH - 4);
                bool isSelected = i == dropdown.TempIndex;

                Color rowBg = isSelected ? Color.FromArgb(46, 75, 115) : Color.FromArgb(24, 26, 32);
                Color rowBorder = isSelected ? Color.FromArgb(120, 190, 255) : Color.FromArgb(52, 56, 66);

                using (var rowBrush = new SolidBrush(rowBg))
                using (var rowPen = new Pen(rowBorder, isSelected ? 1.5f : 1f))
                {
                    FillRoundedRectangle(g, rowBrush, rowRect, 6);
                    DrawRoundedRectangle(g, rowPen, rowRect, 6);
                }

                string itemText = i < combo.Items.Count ? combo.Items[i]?.ToString() ?? string.Empty : string.Empty;

                // Indicador de status (bolinha colorida)
                Color statusColor = Color.FromArgb(120, 126, 140);
                if (itemText.Contains(Strings.Connected)) statusColor = Color.FromArgb(120, 230, 150);
                else if (itemText.Contains(Strings.Automatic)) statusColor = Color.FromArgb(120, 190, 255);

                using (var dotBrush = new SolidBrush(statusColor))
                {
                    g.FillEllipse(dotBrush, rowRect.X + 12, rowRect.Y + (rowRect.Height / 2) - 4, 8, 8);
                }

                using (var itemFont = new Font("Segoe UI", 9.5f, isSelected ? FontStyle.Bold : FontStyle.Regular))
                using (var itemBrush = new SolidBrush(isSelected ? Color.White : Color.FromArgb(220, 225, 235)))
                {
                    g.DrawString(itemText, itemFont, itemBrush, rowRect.X + 28, rowRect.Y + 6);
                }
            }

            // Legenda no rodapé
            int hintY = cardY + cardH - 32;
            DrawButtonBadge(g, "[▲ / ▼]", Color.FromArgb(120, 190, 255), cardX + 20, hintY);
            DrawText(g, "Navegar", Color.FromArgb(200, 205, 215), cardX + 74, hintY);

            DrawButtonBadge(g, "[A]", Color.FromArgb(120, 230, 150), cardX + 160, hintY);
            DrawText(g, "Confirmar", Color.FromArgb(200, 205, 215), cardX + 188, hintY);

            DrawButtonBadge(g, "[B]", Color.FromArgb(255, 120, 120), cardX + 276, hintY);
            DrawText(g, "Cancelar", Color.FromArgb(200, 205, 215), cardX + 304, hintY);
        }

        private void DrawExitDialogCard(Graphics g)
        {
            int cardW = 460;
            int cardH = 250;
            int cardX = (ClientSize.Width - cardW) / 2;
            int cardY = (ClientSize.Height - cardH) / 2;
            var cardRect = new Rectangle(cardX, cardY, cardW, cardH);

            // Background do card & borda
            using (var cardBrush = new SolidBrush(Color.FromArgb(34, 37, 46)))
            using (var borderPen = new Pen(Color.FromArgb(120, 190, 255), 2f))
            {
                FillRoundedRectangle(g, cardBrush, cardRect, 8);
                DrawRoundedRectangle(g, borderPen, cardRect, 8);
            }

            // Título
            using (var titleFont = new Font("Segoe UI", 12f, FontStyle.Bold))
            using (var titleBrush = new SolidBrush(Color.FromArgb(230, 232, 240)))
            {
                g.DrawString(Strings.ExitDialogTitle, titleFont, titleBrush, cardX + 24, cardY + 18);
            }

            // Subtítulo
            using (var subFont = new Font("Segoe UI", 9f))
            using (var subBrush = new SolidBrush(Color.FromArgb(160, 170, 185)))
            {
                g.DrawString(Strings.ExitDialogMessage, subFont, subBrush, cardX + 24, cardY + 42);
            }

            // Opção 0: Minimizar para a Barra de Tarefas
            _exitOption0Rect = new Rectangle(cardX + 20, cardY + 68, cardW - 40, 52);
            bool sel0 = _exitOptionIndex == 0;
            Color bg0 = sel0 ? Color.FromArgb(46, 75, 115) : Color.FromArgb(24, 26, 32);
            Color border0 = sel0 ? Color.FromArgb(120, 190, 255) : Color.FromArgb(52, 56, 66);

            using (var bBrush = new SolidBrush(bg0))
            using (var bPen = new Pen(border0, sel0 ? 1.5f : 1f))
            {
                FillRoundedRectangle(g, bBrush, _exitOption0Rect, 6);
                DrawRoundedRectangle(g, bPen, _exitOption0Rect, 6);
            }

            using (var optFont = new Font("Segoe UI", 10f, sel0 ? FontStyle.Bold : FontStyle.Regular))
            using (var optBrush = new SolidBrush(sel0 ? Color.White : Color.FromArgb(220, 225, 235)))
            {
                g.DrawString(Strings.ExitActionMinimize, optFont, optBrush, _exitOption0Rect.X + 16, _exitOption0Rect.Y + 8);
            }
            using (var descFont = new Font("Segoe UI", 8.2f))
            using (var descBrush = new SolidBrush(Color.FromArgb(160, 170, 185)))
            {
                g.DrawString(Strings.ExitActionMinimizeDesc, descFont, descBrush, _exitOption0Rect.X + 16, _exitOption0Rect.Y + 28);
            }

            // Opção 1: Fechar o Aplicativo
            _exitOption1Rect = new Rectangle(cardX + 20, cardY + 128, cardW - 40, 52);
            bool sel1 = _exitOptionIndex == 1;
            Color bg1 = sel1 ? Color.FromArgb(115, 45, 45) : Color.FromArgb(24, 26, 32);
            Color border1 = sel1 ? Color.FromArgb(255, 120, 120) : Color.FromArgb(52, 56, 66);

            using (var bBrush = new SolidBrush(bg1))
            using (var bPen = new Pen(border1, sel1 ? 1.5f : 1f))
            {
                FillRoundedRectangle(g, bBrush, _exitOption1Rect, 6);
                DrawRoundedRectangle(g, bPen, _exitOption1Rect, 6);
            }

            using (var optFont = new Font("Segoe UI", 10f, sel1 ? FontStyle.Bold : FontStyle.Regular))
            using (var optBrush = new SolidBrush(sel1 ? Color.White : Color.FromArgb(220, 225, 235)))
            {
                g.DrawString(Strings.ExitActionClose, optFont, optBrush, _exitOption1Rect.X + 16, _exitOption1Rect.Y + 8);
            }
            using (var descFont = new Font("Segoe UI", 8.2f))
            using (var descBrush = new SolidBrush(Color.FromArgb(160, 170, 185)))
            {
                g.DrawString(Strings.ExitActionCloseDesc, descFont, descBrush, _exitOption1Rect.X + 16, _exitOption1Rect.Y + 28);
            }

            // Legenda de atalhos
            int hintY = cardY + cardH - 30;
            DrawButtonBadge(g, "[▲ / ▼]", Color.FromArgb(120, 190, 255), cardX + 24, hintY);
            DrawText(g, "Selecionar", Color.FromArgb(200, 205, 215), cardX + 76, hintY);

            DrawButtonBadge(g, "[A]", Color.FromArgb(120, 230, 150), cardX + 170, hintY);
            DrawText(g, "Confirmar", Color.FromArgb(200, 205, 215), cardX + 196, hintY);

            DrawButtonBadge(g, "[B]", Color.FromArgb(255, 120, 120), cardX + 290, hintY);
            DrawText(g, "Voltar", Color.FromArgb(200, 205, 215), cardX + 316, hintY);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_isExitDialog)
            {
                if (_exitOption0Rect.Contains(e.Location))
                {
                    var act = _onMinimizeToTray;
                    CloseExitDialog();
                    act?.Invoke();
                }
                else if (_exitOption1Rect.Contains(e.Location))
                {
                    var act = _onCloseApp;
                    CloseExitDialog();
                    act?.Invoke();
                }
                else
                {
                    var cancel = _onCancelExit;
                    CloseExitDialog();
                    cancel?.Invoke();
                }
            }
            else if (_isMessageDialog)
            {
                if (_messageType != MessageDialogType.Confirmation)
                {
                    var ok = _onMessageOk;
                    CloseMessageDialog();
                    ok?.Invoke();
                }
                else
                {
                    if (_msgBtn0Rect.Contains(e.Location))
                    {
                        var ok = _onMessageOk;
                        CloseMessageDialog();
                        ok?.Invoke();
                    }
                    else if (_msgBtn1Rect.Contains(e.Location))
                    {
                        var cancel = _onMessageCancel;
                        CloseMessageDialog();
                        cancel?.Invoke();
                    }
                    else
                    {
                        var cancel = _onMessageCancel;
                        CloseMessageDialog();
                        cancel?.Invoke();
                    }
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isExitDialog)
            {
                if (_exitOption0Rect.Contains(e.Location) && _exitOptionIndex != 0)
                {
                    _exitOptionIndex = 0;
                    Invalidate();
                }
                else if (_exitOption1Rect.Contains(e.Location) && _exitOptionIndex != 1)
                {
                    _exitOptionIndex = 1;
                    Invalidate();
                }
            }
            else if (_isMessageDialog && _messageType == MessageDialogType.Confirmation)
            {
                if (_msgBtn0Rect.Contains(e.Location) && _messageOptionIndex != 0)
                {
                    _messageOptionIndex = 0;
                    Invalidate();
                }
                else if (_msgBtn1Rect.Contains(e.Location) && _messageOptionIndex != 1)
                {
                    _messageOptionIndex = 1;
                    Invalidate();
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (_isExitDialog)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    var cancel = _onCancelExit;
                    CloseExitDialog();
                    cancel?.Invoke();
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
                {
                    if (_exitOptionIndex == 0)
                    {
                        var act = _onMinimizeToTray;
                        CloseExitDialog();
                        act?.Invoke();
                    }
                    else
                    {
                        var act = _onCloseApp;
                        CloseExitDialog();
                        act?.Invoke();
                    }
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
                {
                    _exitOptionIndex = _exitOptionIndex == 0 ? 1 : 0;
                    Invalidate();
                    e.Handled = true;
                }
            }
            else if (_isMessageDialog)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    if (_messageType == MessageDialogType.Confirmation)
                    {
                        var cancel = _onMessageCancel;
                        CloseMessageDialog();
                        cancel?.Invoke();
                    }
                    else
                    {
                        var ok = _onMessageOk;
                        CloseMessageDialog();
                        ok?.Invoke();
                    }
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
                {
                    if (_messageType == MessageDialogType.Confirmation && _messageOptionIndex == 1)
                    {
                        var cancel = _onMessageCancel;
                        CloseMessageDialog();
                        cancel?.Invoke();
                    }
                    else
                    {
                        var ok = _onMessageOk;
                        CloseMessageDialog();
                        ok?.Invoke();
                    }
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right)
                {
                    if (_messageType == MessageDialogType.Confirmation)
                    {
                        _messageOptionIndex = _messageOptionIndex == 0 ? 1 : 0;
                        Invalidate();
                        e.Handled = true;
                    }
                }
            }
        }

        private void DrawMessageDialogCard(Graphics g)
        {
            int cardW = 500;
            int maxTextW = cardW - 56;

            Size textSize;
            using (var bodyFont = new Font("Segoe UI", 10f))
            {
                textSize = TextRenderer.MeasureText(_messageText, bodyFont, new Size(maxTextW, 0), TextFormatFlags.WordBreak);
            }

            int textH = Math.Max(38, textSize.Height);
            int cardH = 72 + textH + 28 + 44 + 20;
            if (cardH < 220) cardH = 220;

            int cardX = (ClientSize.Width - cardW) / 2;
            int cardY = (ClientSize.Height - cardH) / 2;
            var cardRect = new Rectangle(cardX, cardY, cardW, cardH);

            Color borderColor;
            Color iconBg;
            string iconSymbol;

            switch (_messageType)
            {
                case MessageDialogType.Error:
                    borderColor = Color.FromArgb(240, 90, 90);
                    iconBg = Color.FromArgb(180, 50, 50);
                    iconSymbol = "✕";
                    break;
                case MessageDialogType.Warning:
                    borderColor = Color.FromArgb(245, 185, 65);
                    iconBg = Color.FromArgb(170, 120, 30);
                    iconSymbol = "!";
                    break;
                case MessageDialogType.Confirmation:
                    borderColor = Color.FromArgb(120, 190, 255);
                    iconBg = Color.FromArgb(46, 75, 115);
                    iconSymbol = "?";
                    break;
                case MessageDialogType.Information:
                default:
                    borderColor = Color.FromArgb(120, 190, 255);
                    iconBg = Color.FromArgb(46, 75, 115);
                    iconSymbol = "i";
                    break;
            }

            // Card background & borda
            using (var cardBrush = new SolidBrush(Color.FromArgb(34, 37, 46)))
            using (var borderPen = new Pen(borderColor, 2f))
            {
                FillRoundedRectangle(g, cardBrush, cardRect, 8);
                DrawRoundedRectangle(g, borderPen, cardRect, 8);
            }

            // Ícone circular de status
            int iconSize = 28;
            var iconRect = new Rectangle(cardX + 24, cardY + 20, iconSize, iconSize);
            using (var iconBrush = new SolidBrush(iconBg))
            {
                g.FillEllipse(iconBrush, iconRect);
            }
            using (var symFont = new Font("Segoe UI", 11f, FontStyle.Bold))
            using (var symBrush = new SolidBrush(Color.White))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(iconSymbol, symFont, symBrush, iconRect, sf);
            }

            // Título
            using (var titleFont = new Font("Segoe UI", 12f, FontStyle.Bold))
            using (var titleBrush = new SolidBrush(Color.FromArgb(240, 242, 250)))
            {
                g.DrawString(_messageTitle, titleFont, titleBrush, cardX + 62, cardY + 23);
            }

            // Linha divisória sutil
            using (var sepPen = new Pen(Color.FromArgb(50, 55, 68), 1f))
            {
                g.DrawLine(sepPen, cardX + 24, cardY + 58, cardX + cardW - 24, cardY + 58);
            }

            // Mensagem de corpo
            var textRect = new Rectangle(cardX + 26, cardY + 70, maxTextW, textH);
            using (var bodyFont = new Font("Segoe UI", 9.8f))
            {
                TextRenderer.DrawText(
                    g,
                    _messageText,
                    bodyFont,
                    textRect,
                    Color.FromArgb(215, 222, 235),
                    TextFormatFlags.WordBreak | TextFormatFlags.Left);
            }

            // Botões de ação
            int btnY = cardY + cardH - 52;
            int btnH = 36;

            if (_messageType != MessageDialogType.Confirmation)
            {
                int btnW = 140;
                int btnX = cardX + (cardW - btnW) / 2;
                _msgBtn0Rect = new Rectangle(btnX, btnY, btnW, btnH);
                _msgBtn1Rect = Rectangle.Empty;

                using (var btnBrush = new SolidBrush(Color.FromArgb(46, 75, 115)))
                using (var btnPen = new Pen(Color.FromArgb(120, 190, 255), 1.5f))
                {
                    FillRoundedRectangle(g, btnBrush, _msgBtn0Rect, 6);
                    DrawRoundedRectangle(g, btnPen, _msgBtn0Rect, 6);
                }

                using (var font = new Font("Segoe UI", 9.5f, FontStyle.Bold))
                using (var brush = new SolidBrush(Color.White))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString("[A] " + Strings.DialogOk, font, brush, _msgBtn0Rect, sf);
                }
            }
            else
            {
                int btnW = 140;
                int spacing = 16;
                int totalW = btnW * 2 + spacing;
                int startX = cardX + (cardW - totalW) / 2;

                _msgBtn0Rect = new Rectangle(startX, btnY, btnW, btnH);
                _msgBtn1Rect = new Rectangle(startX + btnW + spacing, btnY, btnW, btnH);

                bool sel0 = _messageOptionIndex == 0;
                bool sel1 = _messageOptionIndex == 1;

                Color bg0 = sel0 ? Color.FromArgb(46, 75, 115) : Color.FromArgb(24, 26, 32);
                Color border0 = sel0 ? Color.FromArgb(120, 190, 255) : Color.FromArgb(52, 56, 66);
                using (var b0Brush = new SolidBrush(bg0))
                using (var b0Pen = new Pen(border0, sel0 ? 1.5f : 1f))
                {
                    FillRoundedRectangle(g, b0Brush, _msgBtn0Rect, 6);
                    DrawRoundedRectangle(g, b0Pen, _msgBtn0Rect, 6);
                }

                Color bg1 = sel1 ? Color.FromArgb(115, 45, 45) : Color.FromArgb(24, 26, 32);
                Color border1 = sel1 ? Color.FromArgb(255, 120, 120) : Color.FromArgb(52, 56, 66);
                using (var b1Brush = new SolidBrush(bg1))
                using (var b1Pen = new Pen(border1, sel1 ? 1.5f : 1f))
                {
                    FillRoundedRectangle(g, b1Brush, _msgBtn1Rect, 6);
                    DrawRoundedRectangle(g, b1Pen, _msgBtn1Rect, 6);
                }

                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    using (var f0 = new Font("Segoe UI", 9.5f, sel0 ? FontStyle.Bold : FontStyle.Regular))
                    using (var br0 = new SolidBrush(sel0 ? Color.White : Color.FromArgb(200, 205, 215)))
                    {
                        string t0 = sel0 ? "[A] " + Strings.DialogConfirm : Strings.DialogConfirm;
                        g.DrawString(t0, f0, br0, _msgBtn0Rect, sf);
                    }

                    using (var f1 = new Font("Segoe UI", 9.5f, sel1 ? FontStyle.Bold : FontStyle.Regular))
                    using (var br1 = new SolidBrush(sel1 ? Color.White : Color.FromArgb(200, 205, 215)))
                    {
                        string t1 = sel1 ? "[A] " + Strings.DialogCancel : "[B] " + Strings.DialogCancel;
                        g.DrawString(t1, f1, br1, _msgBtn1Rect, sf);
                    }
                }
            }
        }

        private static void DrawButtonBadge(Graphics g, string text, Color color, int x, int y)
        {
            using (var font = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (var brush = new SolidBrush(color))
            {
                g.DrawString(text, font, brush, x, y);
            }
        }

        private static void DrawText(Graphics g, string text, Color color, int x, int y)
        {
            using (var font = new Font("Segoe UI", 9f))
            using (var brush = new SolidBrush(color))
            {
                g.DrawString(text, font, brush, x, y);
            }
        }

        private static void FillRoundedRectangle(Graphics g, Brush brush, Rectangle r, int radius)
        {
            using (var path = CreateRoundedRectanglePath(r, radius))
            {
                g.FillPath(brush, path);
            }
        }

        private static void DrawRoundedRectangle(Graphics g, Pen pen, Rectangle r, int radius)
        {
            using (var path = CreateRoundedRectanglePath(r, radius))
            {
                g.DrawPath(pen, path);
            }
        }

        private static GraphicsPath CreateRoundedRectanglePath(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _snapshot?.Dispose();
                _snapshot = null;
            }
            base.Dispose(disposing);
        }
    }
}
