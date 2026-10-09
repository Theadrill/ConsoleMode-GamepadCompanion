using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.Hardware;

namespace ConsoleMode.GamepadCompanion.UI.VirtualKeyboard
{
    /// <summary>
    /// Controle visual completo de Teclado Virtual Couch Gaming de 5 linhas.
    /// Suporta navegação espacial 2D por Gamepad (D-Pad/Thumbstick), mouse, feedback tátil háptico e animação de press-down.
    /// </summary>
    public sealed class VirtualKeyboardControl : UserControl
    {
        private static readonly Color BgColor = Color.FromArgb(24, 26, 32);
        private static readonly Color CardBg = Color.FromArgb(32, 35, 44);
        private static readonly Color InputBoxBg = Color.FromArgb(18, 20, 25);
        private static readonly Color InputBoxBorder = Color.FromArgb(60, 65, 80);
        private static readonly Color TextColor = Color.FromArgb(235, 238, 245);
        private static readonly Color MutedText = Color.FromArgb(150, 160, 175);
        private static readonly Color KeyNormalBg = Color.FromArgb(38, 42, 54);
        private static readonly Color KeyUtilityBg = Color.FromArgb(30, 33, 42);
        private static readonly Color KeyBorder = Color.FromArgb(56, 62, 78);
        private static readonly Color KeyFocusBorder = Color.FromArgb(120, 190, 255);
        private static readonly Color KeyFocusBg = Color.FromArgb(45, 75, 115);
        private static readonly Color KeyPressedBg = Color.FromArgb(70, 140, 230);
        private static readonly Color ActiveIndicatorColor = Color.FromArgb(80, 220, 140);

        private readonly TextInputBuffer _buffer;
        private readonly List<List<VirtualKeyDefinition>> _rows;
        private readonly Timer _cursorBlinkTimer;
        private bool _cursorVisible = true;

        private int _focusedRow = 2; // Inicia na linha home ASDFGH
        private int _focusedCol = 1; // Tecla 'a'

        private bool _isCaps;
        private bool _isShift;
        private bool _isSymbols;

        private VirtualKeyDefinition _lastPressedKey;
        private long _lastPressTick;

        // Controle de auto-repeat do Gamepad
        private bool _lastDpadUp;
        private bool _lastDpadDown;
        private bool _lastDpadLeft;
        private bool _lastDpadRight;
        private bool _lastBtnA;
        private bool _lastBtnB;
        private bool _lastBtnX;
        private bool _lastBtnY;
        private bool _lastBtnLB;
        private bool _lastBtnRB;
        private bool _lastTriggerLT;
        private bool _lastTriggerRT;

        private long _lastMoveTime;
        private int _repeatCount;
        private const long InitialRepeatDelayMs = 240;
        private const long RepeatIntervalMs = 80;

        private long _lastBtnXTime;
        private int _btnXRepeatCount;
        private const long BtnXInitialRepeatDelayMs = 280;
        private const long BtnXRepeatIntervalMs = 70;

        public event Action<string> BufferTextChanged;
        public event Action<string> Confirmed;
        public event Action Cancelled;

        public string Title { get; set; } = Strings.VirtualKeyboardDefaultTitle;

        public TextInputBuffer Buffer => _buffer;
        public bool IsCaps => _isCaps;
        public bool IsShift => _isShift;
        public bool IsSymbols => _isSymbols;
        public int FocusedRow => _focusedRow;
        public int FocusedCol => _focusedCol;
        public bool CursorVisible => _cursorVisible;

        public VirtualKeyboardControl() : this(string.Empty)
        {
        }

        public VirtualKeyboardControl(string initialText)
        {
            DoubleBuffered = true;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.Selectable,
                true);

            BackColor = BgColor;
            Font = new Font("Segoe UI", 9.5f);

            _cursorBlinkTimer = new Timer { Interval = 500 };
            _cursorBlinkTimer.Tick += OnBlinkTimerTick;

            _buffer = new TextInputBuffer(initialText);
            _buffer.TextChanged += () =>
            {
                ResetCursorBlink();
                BufferTextChanged?.Invoke(_buffer.Text);
                Invalidate();
            };
            _buffer.CursorChanged += () =>
            {
                ResetCursorBlink();
                Invalidate();
            };

            _rows = KeyboardLayoutProvider.CreateLayout();
        }

        public void SetInitialText(string text, string title = null)
        {
            if (!string.IsNullOrEmpty(title))
            {
                Title = title;
            }
            _buffer.SetText(text ?? string.Empty);
            _isCaps = false;
            _isShift = false;
            _isSymbols = false;
            _focusedRow = 2;
            _focusedCol = 1;
            ResetGamepadState();
            ResetCursorBlink();
            Invalidate();
        }

        public void ResetGamepadState()
        {
            _lastDpadUp = false;
            _lastDpadDown = false;
            _lastDpadLeft = false;
            _lastDpadRight = false;
            _lastBtnA = true; // Previne clique residual do acionador
            _lastBtnB = true;
            _lastBtnX = true; // Previne apagar imediatamente se o teclado foi aberto com o botão X
            _lastBtnY = true; // Previne inserir espaço se o teclado foi aberto com o botão Y
            _lastBtnLB = false;
            _lastBtnRB = false;
            _lastTriggerLT = false;
            _lastTriggerRT = false;
            _repeatCount = 0;
            _lastMoveTime = 0;
            _btnXRepeatCount = 0;
            _lastBtnXTime = 0;
        }

        private void OnBlinkTimerTick(object sender, EventArgs e)
        {
            if (!Visible || Disposing || IsDisposed)
            {
                _cursorBlinkTimer.Stop();
                return;
            }

            _cursorVisible = !_cursorVisible;
            InvalidateInputArea();
        }

        public void ResetCursorBlink()
        {
            _cursorVisible = true;
            if (Visible && !Disposing && !IsDisposed)
            {
                _cursorBlinkTimer.Stop();
                _cursorBlinkTimer.Start();
            }
            InvalidateInputArea();
        }

        internal void ToggleCursorBlinkForTesting()
        {
            _cursorVisible = !_cursorVisible;
        }

        private void InvalidateInputArea()
        {
            if (_inputBoxRect.Width > 0 && _inputBoxRect.Height > 0)
            {
                var rect = _inputBoxRect;
                rect.Inflate(2, 2);
                Invalidate(rect);
            }
            else
            {
                Invalidate();
            }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible)
            {
                _cursorVisible = true;
                _cursorBlinkTimer.Start();
                InvalidateInputArea();
            }
            else
            {
                _cursorBlinkTimer.Stop();
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (Visible)
            {
                _cursorVisible = true;
                _cursorBlinkTimer.Start();
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            _cursorBlinkTimer.Stop();
            base.OnHandleDestroyed(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cursorBlinkTimer.Stop();
                _cursorBlinkTimer.Tick -= OnBlinkTimerTick;
                _cursorBlinkTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RecalculateLayout();
            Invalidate();
        }

        private Rectangle _inputBoxRect;
        private Rectangle _keyboardAreaRect;

        private void RecalculateLayout()
        {
            if (Width < 200 || Height < 200) return;

            int padding = 14;
            int headerH = 30;
            int inputH = 42;
            int footerH = 34;

            _inputBoxRect = new Rectangle(padding, padding + headerH, Width - (padding * 2), inputH);

            int kbTop = _inputBoxRect.Bottom + 12;
            int kbHeight = Height - kbTop - footerH - padding;
            if (kbHeight < 100) kbHeight = 100;
            _keyboardAreaRect = new Rectangle(padding, kbTop, Width - (padding * 2), kbHeight);

            int rowGap = 5;
            int keyGap = 5;
            int totalRowGaps = rowGap * (_rows.Count - 1);
            int rowHeight = Math.Max(26, (_keyboardAreaRect.Height - totalRowGaps) / _rows.Count);

            for (int r = 0; r < _rows.Count; r++)
            {
                var row = _rows[r];
                int rowY = _keyboardAreaRect.Y + (r * (rowHeight + rowGap));

                float totalWeight = 0f;
                foreach (var k in row) totalWeight += k.WidthWeight;

                int totalKeyGaps = keyGap * (row.Count - 1);
                int availableRowWidth = _keyboardAreaRect.Width - totalKeyGaps;

                int curX = _keyboardAreaRect.X;
                for (int c = 0; c < row.Count; c++)
                {
                    var key = row[c];
                    int keyW = (int)Math.Round((key.WidthWeight / totalWeight) * availableRowWidth);

                    // Ajusta o último elemento da linha para fechar perfeitamente na largura
                    if (c == row.Count - 1)
                    {
                        keyW = (_keyboardAreaRect.Right) - curX;
                    }

                    key.Bounds = new Rectangle(curX, rowY, Math.Max(10, keyW), rowHeight);
                    curX += keyW + keyGap;
                }
            }
        }

        public void ProcessGamepad(GamepadState state, long currentTimeMs)
        {
            if (!state.IsConnected) return;

            bool dpadUp = state.IsPressed(GamepadButtons.DPadUp) || state.LeftThumbY > 16000;
            bool dpadDown = state.IsPressed(GamepadButtons.DPadDown) || state.LeftThumbY < -16000;
            bool dpadLeft = state.IsPressed(GamepadButtons.DPadLeft) || state.LeftThumbX < -16000;
            bool dpadRight = state.IsPressed(GamepadButtons.DPadRight) || state.LeftThumbX > 16000;

            bool btnA = state.IsPressed(GamepadButtons.A);
            bool btnB = state.IsPressed(GamepadButtons.B);
            bool btnX = state.IsPressed(GamepadButtons.X);
            bool btnY = state.IsPressed(GamepadButtons.Y);
            bool btnLB = state.IsPressed(GamepadButtons.LeftShoulder);
            bool btnRB = state.IsPressed(GamepadButtons.RightShoulder);
            bool triggerLT = state.LeftTrigger > 120;
            bool triggerRT = state.RightTrigger > 120;

            // 1. Ações físicas diretas (Triggers e Bumpers)
            if (triggerLT && !_lastTriggerLT)
            {
                _buffer.Clear();
                GamepadVibrationService.Instance.Pulse(0, 45);
                Invalidate();
            }
            _lastTriggerLT = triggerLT;

            if (triggerRT && !_lastTriggerRT)
            {
                Confirmed?.Invoke(_buffer.Text);
                return;
            }
            _lastTriggerRT = triggerRT;

            if ((btnLB && !_lastBtnLB) || (btnRB && !_lastBtnRB))
            {
                _isCaps = !_isCaps;
                GamepadVibrationService.Instance.Pulse(0, 20);
                Invalidate();
            }
            _lastBtnLB = btnLB;
            _lastBtnRB = btnRB;

            // 2. Botão B cancela / fecha
            if (btnB && !_lastBtnB)
            {
                Cancelled?.Invoke();
                return;
            }
            _lastBtnB = btnB;

            // 3. Botão X apaga (Backspace) com auto-repeat ao segurar
            if (btnX)
            {
                if (!_lastBtnX)
                {
                    _buffer.Backspace();
                    GamepadVibrationService.Instance.Pulse(0, 25);
                    _lastBtnXTime = currentTimeMs;
                    _btnXRepeatCount = 0;
                    Invalidate();
                }
                else
                {
                    long elapsed = currentTimeMs - _lastBtnXTime;
                    long required = _btnXRepeatCount == 0 ? BtnXInitialRepeatDelayMs : BtnXRepeatIntervalMs;
                    if (elapsed >= required)
                    {
                        _buffer.Backspace();
                        GamepadVibrationService.Instance.Pulse(0, 20);
                        _lastBtnXTime = currentTimeMs;
                        _btnXRepeatCount++;
                        Invalidate();
                    }
                }
            }
            else
            {
                _btnXRepeatCount = 0;
                _lastBtnXTime = 0;
            }
            _lastBtnX = btnX;

            // 3.5. Botão Y insere Espaço
            if (btnY && !_lastBtnY)
            {
                _buffer.Insert(" ");
                GamepadVibrationService.Instance.Pulse(0, 20);
                ResetCursorBlink();
                Invalidate();
            }
            _lastBtnY = btnY;

            // 4. Botão A digita a tecla focada
            if (btnA && !_lastBtnA)
            {
                ExecuteFocusedKey();
            }
            _lastBtnA = btnA;

            // 4. Navegação direcional 2D com auto-repeat
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
                if (dpadUp) MoveFocusUp();
                else if (dpadDown) MoveFocusDown();
                else if (dpadLeft) MoveFocusLeft();
                else if (dpadRight) MoveFocusRight();
            }

            _lastDpadUp = dpadUp;
            _lastDpadDown = dpadDown;
            _lastDpadLeft = dpadLeft;
            _lastDpadRight = dpadRight;
        }

        private void MoveFocusLeft()
        {
            if (_focusedRow < 0 || _focusedRow >= _rows.Count) return;
            var curRow = _rows[_focusedRow];
            _focusedCol = (_focusedCol - 1 + curRow.Count) % curRow.Count;
            Invalidate();
        }

        private void MoveFocusRight()
        {
            if (_focusedRow < 0 || _focusedRow >= _rows.Count) return;
            var curRow = _rows[_focusedRow];
            _focusedCol = (_focusedCol + 1) % curRow.Count;
            Invalidate();
        }

        private void MoveFocusUp()
        {
            if (_focusedRow <= 0) return;
            int curCenterX = GetCurrentKeyCenterX();
            _focusedRow--;
            _focusedCol = FindClosestColInRow(_focusedRow, curCenterX);
            Invalidate();
        }

        private void MoveFocusDown()
        {
            if (_focusedRow >= _rows.Count - 1) return;
            int curCenterX = GetCurrentKeyCenterX();
            _focusedRow++;
            _focusedCol = FindClosestColInRow(_focusedRow, curCenterX);
            Invalidate();
        }

        private int GetCurrentKeyCenterX()
        {
            if (_focusedRow >= 0 && _focusedRow < _rows.Count &&
                _focusedCol >= 0 && _focusedCol < _rows[_focusedRow].Count)
            {
                var rect = _rows[_focusedRow][_focusedCol].Bounds;
                return rect.Left + (rect.Width / 2);
            }
            return Width / 2;
        }

        private int FindClosestColInRow(int rowIdx, int targetCenterX)
        {
            var row = _rows[rowIdx];
            int bestCol = 0;
            int bestDist = int.MaxValue;

            for (int i = 0; i < row.Count; i++)
            {
                var r = row[i].Bounds;
                int center = r.Left + (r.Width / 2);
                int dist = Math.Abs(center - targetCenterX);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestCol = i;
                }
            }

            return bestCol;
        }

        public void ExecuteFocusedKey()
        {
            if (_focusedRow >= 0 && _focusedRow < _rows.Count &&
                _focusedCol >= 0 && _focusedCol < _rows[_focusedRow].Count)
            {
                ExecuteKey(_rows[_focusedRow][_focusedCol]);
            }
        }

        public void ExecuteKey(VirtualKeyDefinition key)
        {
            if (key == null) return;

            _lastPressedKey = key;
            _lastPressTick = Environment.TickCount;
            GamepadVibrationService.Instance.Pulse(0, 30);

            switch (key.KeyType)
            {
                case VirtualKeyType.Character:
                    string text = key.GetInsertText(_isCaps || _isShift, _isSymbols);
                    _buffer.Insert(text);
                    if (_isShift)
                    {
                        _isShift = false;
                    }
                    break;

                case VirtualKeyType.Space:
                    _buffer.Insert(" ");
                    break;

                case VirtualKeyType.Backspace:
                    _buffer.Backspace();
                    break;

                case VirtualKeyType.Tab:
                    _buffer.Insert("    ");
                    break;

                case VirtualKeyType.CapsLock:
                    _isCaps = !_isCaps;
                    break;

                case VirtualKeyType.Shift:
                    _isShift = !_isShift;
                    break;

                case VirtualKeyType.ToggleSymbols:
                    _isSymbols = !_isSymbols;
                    break;

                case VirtualKeyType.CursorLeft:
                    _buffer.MoveCursorLeft();
                    ResetCursorBlink();
                    break;

                case VirtualKeyType.CursorRight:
                    _buffer.MoveCursorRight();
                    ResetCursorBlink();
                    break;

                case VirtualKeyType.Copy:
                    _buffer.CopyToClipboard();
                    break;

                case VirtualKeyType.Paste:
                    _buffer.PasteFromClipboard();
                    break;

                case VirtualKeyType.ClearAll:
                    _buffer.Clear();
                    break;

                case VirtualKeyType.Enter:
                    Confirmed?.Invoke(_buffer.Text);
                    return;
            }

            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            for (int r = 0; r < _rows.Count; r++)
            {
                var row = _rows[r];
                for (int c = 0; c < row.Count; c++)
                {
                    if (row[c].Bounds.Contains(e.Location))
                    {
                        _focusedRow = r;
                        _focusedCol = c;
                        ExecuteKey(row[c]);
                        return;
                    }
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            if (_keyboardAreaRect.Width <= 0)
            {
                RecalculateLayout();
            }

            // 1. Título e contagem no cabeçalho
            using (var titleFont = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (var titleBrush = new SolidBrush(TextColor))
            {
                g.DrawString(Title, titleFont, titleBrush, _inputBoxRect.X, 10);
            }

            string charCount = $"{_buffer.Length} carac.";
            using (var countFont = new Font("Segoe UI", 8.5f))
            using (var countBrush = new SolidBrush(MutedText))
            {
                var sz = g.MeasureString(charCount, countFont);
                g.DrawString(charCount, countFont, countBrush, _inputBoxRect.Right - sz.Width, 12);
            }

            // 2. Caixa de Texto com Pré-visualização do Buffer e Cursor Piscante
            DrawInputPreview(g);

            // 3. Grid de Teclas de 5 Linhas
            DrawKeyboardGrid(g);

            // 4. Rodapé com Atalhos do Gamepad Couch Gaming
            DrawGamepadHints(g);
        }

        private void DrawInputPreview(Graphics g)
        {
            using (var bgBrush = new SolidBrush(InputBoxBg))
            using (var borderPen = new Pen(InputBoxBorder, 1.2f))
            {
                FillRoundedRectangle(g, bgBrush, _inputBoxRect, 6);
                DrawRoundedRectangle(g, borderPen, _inputBoxRect, 6);
            }

            // Renderiza texto e cursor
            string text = _buffer.Text;
            int cursorPos = _buffer.CursorPosition;
            int textX = _inputBoxRect.X + 12;
            int textY = _inputBoxRect.Y + ((_inputBoxRect.Height - 22) / 2);

            using (var textFont = new Font("Segoe UI", 11f, FontStyle.Regular))
            {
                if (string.IsNullOrEmpty(text))
                {
                    TextRenderer.DrawText(g, "...", textFont, new Point(textX, textY), Color.FromArgb(90, 95, 110), TextFormatFlags.NoPadding);
                }
                else
                {
                    TextRenderer.DrawText(g, text, textFont, new Point(textX, textY), TextColor, TextFormatFlags.NoPadding);
                }

                // Cursor piscante perfeitamente alinhado com o texto
                if (_cursorVisible)
                {
                    int cursorOffset = 0;
                    if (!string.IsNullOrEmpty(text) && cursorPos > 0)
                    {
                        string sub = text.Substring(0, Math.Min(text.Length, cursorPos));
                        var sz = TextRenderer.MeasureText(g, sub, textFont, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
                        cursorOffset = sz.Width + 1;
                    }

                    int cursorX = textX + cursorOffset;

                    using (var cursorPen = new Pen(KeyFocusBorder, 2f))
                    {
                        g.DrawLine(cursorPen, cursorX, textY + 2, cursorX, textY + 20);
                    }
                }
            }
        }

        private void DrawKeyboardGrid(Graphics g)
        {
            long now = Environment.TickCount;
            bool isPressActive = (now - _lastPressTick) < 120;

            for (int r = 0; r < _rows.Count; r++)
            {
                var row = _rows[r];
                for (int c = 0; c < row.Count; c++)
                {
                    var key = row[c];
                    bool isFocused = (r == _focusedRow && c == _focusedCol);
                    bool isPressed = isPressActive && (key == _lastPressedKey);

                    DrawSingleKey(g, key, isFocused, isPressed);
                }
            }
        }

        private void DrawSingleKey(Graphics g, VirtualKeyDefinition key, bool isFocused, bool isPressed)
        {
            var bounds = key.Bounds;
            if (isPressed)
            {
                // Animação de press-down: desloca 1px para baixo e 1px para a direita
                bounds = new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width - 1, bounds.Height - 1);
            }

            Color bg;
            if (isPressed)
            {
                bg = KeyPressedBg;
            }
            else if (isFocused)
            {
                bg = KeyFocusBg;
            }
            else if (key.KeyType != VirtualKeyType.Character)
            {
                bg = KeyUtilityBg;
            }
            else
            {
                bg = KeyNormalBg;
            }

            Color border = isFocused ? KeyFocusBorder : KeyBorder;
            float penWidth = isFocused ? 2.2f : 1f;

            using (var brush = new SolidBrush(bg))
            using (var pen = new Pen(border, penWidth))
            {
                FillRoundedRectangle(g, brush, bounds, 5);
                DrawRoundedRectangle(g, pen, bounds, 5);
            }

            // Indicador de Caps/Shift ativo
            if ((key.KeyType == VirtualKeyType.CapsLock && _isCaps) ||
                (key.KeyType == VirtualKeyType.Shift && _isShift) ||
                (key.KeyType == VirtualKeyType.ToggleSymbols && _isSymbols))
            {
                using (var indBrush = new SolidBrush(ActiveIndicatorColor))
                {
                    g.FillEllipse(indBrush, bounds.Right - 9, bounds.Y + 5, 4, 4);
                }
            }

            // Rótulo da tecla
            string label = key.GetDisplayLabel(_isCaps || _isShift, _isSymbols);
            Color labelColor = isFocused ? Color.White : TextColor;

            Font keyFont;
            if (key.KeyType == VirtualKeyType.Space || key.KeyType == VirtualKeyType.ClearAll ||
                key.KeyType == VirtualKeyType.Copy || key.KeyType == VirtualKeyType.Paste)
            {
                keyFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            }
            else if (label.Length > 1)
            {
                keyFont = new Font("Segoe UI", 9f, FontStyle.Bold);
            }
            else
            {
                keyFont = new Font("Segoe UI", 10.5f, FontStyle.Regular);
            }

            using (keyFont)
            using (var textBrush = new SolidBrush(labelColor))
            {
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(label, keyFont, textBrush, bounds, sf);
            }
        }

        private void DrawGamepadHints(Graphics g)
        {
            int footerY = Height - 30;
            string hints = $"{Strings.KbHintType}   {Strings.KbHintSpace}   {Strings.KbHintBackspace}   {Strings.KbHintCancel}   {Strings.KbHintConfirm}   {Strings.KbHintCaps}   {Strings.KbHintClear}   {Strings.KbHintCursor}";

            using (var font = new Font("Segoe UI", 8.5f, FontStyle.Regular))
            using (var brush = new SolidBrush(MutedText))
            {
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                var footerRect = new Rectangle(0, footerY, Width, 24);
                g.DrawString(hints, font, brush, footerRect, sf);
            }
        }

        public bool HandlePhysicalKeyPress(char keyChar)
        {
            if (!char.IsControl(keyChar))
            {
                _buffer.Insert(keyChar.ToString());
                ResetCursorBlink();
                Invalidate();
                return true;
            }
            return false;
        }

        public bool HandlePhysicalKeyDown(KeyEventArgs e)
        {
            if (e == null) return false;

            if (e.Control && e.KeyCode == Keys.V)
            {
                _buffer.PasteFromClipboard();
                ResetCursorBlink();
                Invalidate();
                e.Handled = true;
                return true;
            }
            if (e.Control && e.KeyCode == Keys.C)
            {
                _buffer.CopyToClipboard();
                e.Handled = true;
                return true;
            }
            if (e.KeyCode == Keys.Back)
            {
                _buffer.Backspace();
                ResetCursorBlink();
                Invalidate();
                e.Handled = true;
                return true;
            }
            if (e.KeyCode == Keys.Delete)
            {
                _buffer.Delete();
                ResetCursorBlink();
                Invalidate();
                e.Handled = true;
                return true;
            }
            if (e.KeyCode == Keys.Left)
            {
                _buffer.MoveCursorLeft();
                ResetCursorBlink();
                Invalidate();
                e.Handled = true;
                return true;
            }
            if (e.KeyCode == Keys.Right)
            {
                _buffer.MoveCursorRight();
                ResetCursorBlink();
                Invalidate();
                e.Handled = true;
                return true;
            }
            if (e.KeyCode == Keys.Home)
            {
                _buffer.MoveCursorHome();
                ResetCursorBlink();
                Invalidate();
                e.Handled = true;
                return true;
            }
            if (e.KeyCode == Keys.End)
            {
                _buffer.MoveCursorEnd();
                ResetCursorBlink();
                Invalidate();
                e.Handled = true;
                return true;
            }
            if (e.KeyCode == Keys.Enter)
            {
                Confirmed?.Invoke(_buffer.Text);
                e.Handled = true;
                return true;
            }
            if (e.KeyCode == Keys.Escape)
            {
                Cancelled?.Invoke();
                e.Handled = true;
                return true;
            }
            if (e.KeyCode == Keys.Space)
            {
                _buffer.Insert(" ");
                ResetCursorBlink();
                Invalidate();
                e.Handled = true;
                return true;
            }
            return false;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Left:
                case Keys.Right:
                case Keys.Up:
                case Keys.Down:
                case Keys.Tab:
                case Keys.Enter:
                case Keys.Escape:
                case Keys.Home:
                case Keys.End:
                    return true;
                default:
                    return base.IsInputKey(keyData);
            }
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            if (HandlePhysicalKeyPress(e.KeyChar))
            {
                e.Handled = true;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            HandlePhysicalKeyDown(e);
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
    }
}
