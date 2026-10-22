using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.UI.Controls
{
    /// <summary>
    /// Identifica cada linha interativa no painel de configuração expandida do Trackpad.
    /// </summary>
    public enum TrackpadConfigField
    {
        Mode,
        Sensitivity,
        Friction,
        InvertScroll,
        DeadzonePercent,
        ClickPressureThreshold,
        HapticIntensity,
        DpadUp,
        DpadDown,
        DpadLeft,
        DpadRight,
        PressAction,
        TouchAction,
        BtnSave,
        BtnBack
    }

    /// <summary>
    /// Painel de Configuração Expandida do Trackpad (Fase 6 / Etapa 2).
    /// Interface Couch Gaming 100% Dark Mode OLED para personalização cirúrgica de modos,
    /// sensibilidade, inércia, hápticos LRA, rolagem natural e ações de toque/clique no Steam Deck.
    /// </summary>
    public sealed class TrackpadConfigControl : UserControl
    {
        private static readonly Color BgColor = Color.FromArgb(24, 26, 32);
        private static readonly Color HeaderBg = Color.FromArgb(30, 32, 40);
        private static readonly Color CardBg = Color.FromArgb(34, 37, 46);
        private static readonly Color InputBg = Color.FromArgb(20, 22, 28);
        private static readonly Color FocusBg = Color.FromArgb(35, 50, 78);
        private static readonly Color BorderIdle = Color.FromArgb(120, 126, 140);
        private static readonly Color BorderSubtle = Color.FromArgb(65, 70, 82);
        private static readonly Color BorderFocused = Color.FromArgb(120, 190, 255);
        private static readonly Color ActiveGreen = Color.FromArgb(120, 230, 150);
        private static readonly Color AlertAmber = Color.FromArgb(245, 158, 11);
        private static readonly Color ModeCyan = Color.FromArgb(80, 200, 255);
        private static readonly Color TextPrimary = Color.FromArgb(230, 232, 240);
        private static readonly Color TextSecondary = Color.FromArgb(160, 170, 185);

        private const float LogicalWidth = 620f;
        private const float LogicalHeight = 540f;

        private GameEntry _game;
        private bool _isRightPad;
        private TrackpadConfig _config = new TrackpadConfig();
        private readonly List<TrackpadConfigField> _visibleFields = new List<TrackpadConfigField>();
        private readonly Dictionary<TrackpadConfigField, RectangleF> _fieldBounds = new Dictionary<TrackpadConfigField, RectangleF>();

        private int _focusedIndex;

        // Estado de Debounce / Repetição do Gamepad
        private bool _lastUp;
        private bool _lastDown;
        private bool _lastLeft;
        private bool _lastRight;
        private bool _lastA;
        private bool _lastB;
        private bool _lastX;
        private bool _lastRT;
        private long _lastMoveTime;
        private int _repeatCount;
        private const long InitialRepeatDelayMs = 230;
        private const long RepeatIntervalMs = 75;

        public event Action<bool, TrackpadConfig> ConfigConfirmed;
        public event Action CancelRequested;
        public event Action<ProfileEditorSlot, string, List<InputAction>> SlotEditRequested;

        public TrackpadConfigControl()
        {
            BackColor = BgColor;
            DoubleBuffered = true;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
        }

        public bool IsRightPad => _isRightPad;
        public TrackpadConfig Config => _config;
        public TrackpadConfigField FocusedField => (_focusedIndex >= 0 && _focusedIndex < _visibleFields.Count)
            ? _visibleFields[_focusedIndex]
            : TrackpadConfigField.Mode;

        public void Open(GameEntry game, bool isRightPad, TrackpadConfig sourceConfig)
        {
            _game = game ?? new GameEntry { Name = Strings.DefaultGameName };
            _isRightPad = isRightPad;
            _config = sourceConfig?.Clone() ?? new TrackpadConfig();

            RebuildVisibleFields();
            _focusedIndex = 0;
            ResetInputState();
            Invalidate();
        }

        public void ResetInputState()
        {
            _lastA = true;
            _lastB = true;
            _lastX = true;
            _lastRT = true;
            _lastUp = false;
            _lastDown = false;
            _lastLeft = false;
            _lastRight = false;
            _repeatCount = 0;
            _lastMoveTime = 0;
            Invalidate();
        }

        public void SetSlotActions(ProfileEditorSlot slot, List<InputAction> actions)
        {
            var cloned = CloneActionList(actions);
            switch (slot)
            {
                case ProfileEditorSlot.TrackpadPress:
                    _config.PressActions = cloned;
                    break;
                case ProfileEditorSlot.TrackpadTouch:
                    _config.TouchActions = cloned;
                    break;
                case ProfileEditorSlot.TrackpadDpadUp:
                    _config.Up = cloned;
                    break;
                case ProfileEditorSlot.TrackpadDpadDown:
                    _config.Down = cloned;
                    break;
                case ProfileEditorSlot.TrackpadDpadLeft:
                    _config.Left = cloned;
                    break;
                case ProfileEditorSlot.TrackpadDpadRight:
                    _config.Right = cloned;
                    break;
            }
            Invalidate();
        }

        public void ProcessGamepad(GamepadState state, long nowMs)
        {
            if (!state.IsConnected) return;

            bool up = state.IsPressed(GamepadButtons.DPadUp) || state.LeftThumbY > 16000;
            bool down = state.IsPressed(GamepadButtons.DPadDown) || state.LeftThumbY < -16000;
            bool left = state.IsPressed(GamepadButtons.DPadLeft) || state.LeftThumbX < -16000;
            bool right = state.IsPressed(GamepadButtons.DPadRight) || state.LeftThumbX > 16000;

            bool btnA = state.IsPressed(GamepadButtons.A);
            bool btnB = state.IsPressed(GamepadButtons.B);
            bool btnX = state.IsPressed(GamepadButtons.X);
            bool triggerRT = state.RightTrigger > 120;

            // Gatilho RT salva imediatamente
            if (triggerRT && !_lastRT)
            {
                _lastRT = triggerRT;
                ConfirmAndClose();
                return;
            }
            _lastRT = triggerRT;

            // Botão B cancela e volta
            if (btnB && !_lastB)
            {
                _lastB = btnB;
                CancelRequested?.Invoke();
                return;
            }
            _lastB = btnB;

            // Botão X limpa a ação se estiver num slot
            if (btnX && !_lastX)
            {
                _lastX = btnX;
                ClearFocusedField();
                return;
            }
            _lastX = btnX;

            // Botão A aciona o campo focado
            if (btnA && !_lastA)
            {
                _lastA = btnA;
                ActivateFocusedField();
                return;
            }
            _lastA = btnA;

            // Navegação e ajuste com D-Pad / Analógico
            bool anyDir = up || down || left || right;
            if (anyDir)
            {
                bool isInitial = (up && !_lastUp) || (down && !_lastDown) || (left && !_lastLeft) || (right && !_lastRight);
                long delay = _repeatCount == 0 ? InitialRepeatDelayMs : RepeatIntervalMs;

                if (isInitial || (nowMs - _lastMoveTime >= delay))
                {
                    if (up && _focusedIndex > 0)
                    {
                        _focusedIndex--;
                        Invalidate();
                    }
                    else if (down && _focusedIndex < _visibleFields.Count - 1)
                    {
                        _focusedIndex++;
                        Invalidate();
                    }
                    else if (left)
                    {
                        AdjustFocusedField(-1);
                    }
                    else if (right)
                    {
                        AdjustFocusedField(1);
                    }

                    _lastMoveTime = nowMs;
                    _repeatCount = isInitial ? 0 : _repeatCount + 1;
                }
            }
            else
            {
                _repeatCount = 0;
            }

            _lastUp = up;
            _lastDown = down;
            _lastLeft = left;
            _lastRight = right;
        }

        public void ActivateFocusedField()
        {
            if (_focusedIndex < 0 || _focusedIndex >= _visibleFields.Count) return;
            var field = _visibleFields[_focusedIndex];

            switch (field)
            {
                case TrackpadConfigField.Mode:
                    CycleMode(1);
                    break;

                case TrackpadConfigField.InvertScroll:
                    _config.InvertScroll = !_config.InvertScroll;
                    Invalidate();
                    break;

                case TrackpadConfigField.HapticIntensity:
                    CycleHaptics(1);
                    break;

                case TrackpadConfigField.PressAction:
                    SlotEditRequested?.Invoke(ProfileEditorSlot.TrackpadPress, Strings.TrackpadPressActionLabel, _config.PressActions);
                    break;

                case TrackpadConfigField.TouchAction:
                    SlotEditRequested?.Invoke(ProfileEditorSlot.TrackpadTouch, Strings.TrackpadTouchActionLabel, _config.TouchActions);
                    break;

                case TrackpadConfigField.DpadUp:
                    SlotEditRequested?.Invoke(ProfileEditorSlot.TrackpadDpadUp, Strings.TrackpadDpadUpLabel, _config.Up);
                    break;

                case TrackpadConfigField.DpadDown:
                    SlotEditRequested?.Invoke(ProfileEditorSlot.TrackpadDpadDown, Strings.TrackpadDpadDownLabel, _config.Down);
                    break;

                case TrackpadConfigField.DpadLeft:
                    SlotEditRequested?.Invoke(ProfileEditorSlot.TrackpadDpadLeft, Strings.TrackpadDpadLeftLabel, _config.Left);
                    break;

                case TrackpadConfigField.DpadRight:
                    SlotEditRequested?.Invoke(ProfileEditorSlot.TrackpadDpadRight, Strings.TrackpadDpadRightLabel, _config.Right);
                    break;

                case TrackpadConfigField.BtnSave:
                    ConfirmAndClose();
                    break;

                case TrackpadConfigField.BtnBack:
                    CancelRequested?.Invoke();
                    break;
            }
        }

        public void AdjustFocusedField(int dir)
        {
            if (_focusedIndex < 0 || _focusedIndex >= _visibleFields.Count) return;
            var field = _visibleFields[_focusedIndex];

            switch (field)
            {
                case TrackpadConfigField.Mode:
                    CycleMode(dir);
                    break;

                case TrackpadConfigField.Sensitivity:
                    _config.Sensitivity = Math.Max(1, Math.Min(100, _config.Sensitivity + dir * 2));
                    Invalidate();
                    break;

                case TrackpadConfigField.Friction:
                    _config.Friction = Math.Max(1, Math.Min(100, _config.Friction + dir * 2));
                    Invalidate();
                    break;

                case TrackpadConfigField.InvertScroll:
                    _config.InvertScroll = !_config.InvertScroll;
                    Invalidate();
                    break;

                case TrackpadConfigField.DeadzonePercent:
                    _config.DeadzonePercent = Math.Max(5, Math.Min(40, _config.DeadzonePercent + dir * 2));
                    Invalidate();
                    break;

                case TrackpadConfigField.ClickPressureThreshold:
                    int newPress = _config.ClickPressureThreshold + dir * 500;
                    if (newPress < 0) newPress = 0;
                    if (newPress > 25000) newPress = 25000;
                    _config.ClickPressureThreshold = newPress;
                    Invalidate();
                    break;

                case TrackpadConfigField.HapticIntensity:
                    CycleHaptics(dir);
                    break;

                case TrackpadConfigField.BtnSave:
                case TrackpadConfigField.BtnBack:
                    _focusedIndex = (field == TrackpadConfigField.BtnSave) ? _visibleFields.IndexOf(TrackpadConfigField.BtnBack) : _visibleFields.IndexOf(TrackpadConfigField.BtnSave);
                    Invalidate();
                    break;
            }
        }

        public void ClearFocusedField()
        {
            if (_focusedIndex < 0 || _focusedIndex >= _visibleFields.Count) return;
            var field = _visibleFields[_focusedIndex];

            switch (field)
            {
                case TrackpadConfigField.PressAction:
                    _config.PressActions.Clear();
                    Invalidate();
                    break;

                case TrackpadConfigField.TouchAction:
                    _config.TouchActions.Clear();
                    Invalidate();
                    break;

                case TrackpadConfigField.DpadUp:
                    _config.Up.Clear();
                    Invalidate();
                    break;

                case TrackpadConfigField.DpadDown:
                    _config.Down.Clear();
                    Invalidate();
                    break;

                case TrackpadConfigField.DpadLeft:
                    _config.Left.Clear();
                    Invalidate();
                    break;

                case TrackpadConfigField.DpadRight:
                    _config.Right.Clear();
                    Invalidate();
                    break;
            }
        }

        private void CycleMode(int dir)
        {
            var modes = new[]
            {
                TrackpadMode.MouseTrackball,
                TrackpadMode.Scrollwheel,
                TrackpadMode.DPad,
                TrackpadMode.SingleButton,
                TrackpadMode.Disabled
            };

            int idx = Array.IndexOf(modes, _config.Mode);
            if (idx < 0) idx = 0;
            idx = (idx + dir + modes.Length) % modes.Length;
            _config.Mode = modes[idx];

            RebuildVisibleFields();
            if (_focusedIndex >= _visibleFields.Count) _focusedIndex = _visibleFields.Count - 1;
            Invalidate();
        }

        private void CycleHaptics(int dir)
        {
            var levels = new[]
            {
                TrackpadHapticIntensity.Off,
                TrackpadHapticIntensity.Low,
                TrackpadHapticIntensity.Medium,
                TrackpadHapticIntensity.High
            };

            int idx = Array.IndexOf(levels, _config.HapticIntensity);
            if (idx < 0) idx = 3;
            idx = (idx + dir + levels.Length) % levels.Length;
            _config.HapticIntensity = levels[idx];
            Invalidate();
        }

        public void ConfirmAndClose()
        {
            ConfigConfirmed?.Invoke(_isRightPad, _config.Clone());
        }

        private void RebuildVisibleFields()
        {
            _visibleFields.Clear();
            _visibleFields.Add(TrackpadConfigField.Mode);

            switch (_config.Mode)
            {
                case TrackpadMode.MouseTrackball:
                    _visibleFields.Add(TrackpadConfigField.Sensitivity);
                    _visibleFields.Add(TrackpadConfigField.Friction);
                    _visibleFields.Add(TrackpadConfigField.ClickPressureThreshold);
                    _visibleFields.Add(TrackpadConfigField.HapticIntensity);
                    _visibleFields.Add(TrackpadConfigField.PressAction);
                    _visibleFields.Add(TrackpadConfigField.TouchAction);
                    break;

                case TrackpadMode.Scrollwheel:
                    _visibleFields.Add(TrackpadConfigField.Sensitivity);
                    _visibleFields.Add(TrackpadConfigField.InvertScroll);
                    _visibleFields.Add(TrackpadConfigField.ClickPressureThreshold);
                    _visibleFields.Add(TrackpadConfigField.HapticIntensity);
                    _visibleFields.Add(TrackpadConfigField.PressAction);
                    _visibleFields.Add(TrackpadConfigField.TouchAction);
                    break;

                case TrackpadMode.DPad:
                    _visibleFields.Add(TrackpadConfigField.DeadzonePercent);
                    _visibleFields.Add(TrackpadConfigField.HapticIntensity);
                    _visibleFields.Add(TrackpadConfigField.DpadUp);
                    _visibleFields.Add(TrackpadConfigField.DpadDown);
                    _visibleFields.Add(TrackpadConfigField.DpadLeft);
                    _visibleFields.Add(TrackpadConfigField.DpadRight);
                    _visibleFields.Add(TrackpadConfigField.PressAction);
                    _visibleFields.Add(TrackpadConfigField.TouchAction);
                    break;

                case TrackpadMode.SingleButton:
                    _visibleFields.Add(TrackpadConfigField.ClickPressureThreshold);
                    _visibleFields.Add(TrackpadConfigField.HapticIntensity);
                    _visibleFields.Add(TrackpadConfigField.PressAction);
                    break;

                case TrackpadMode.Disabled:
                    break;
            }

            _visibleFields.Add(TrackpadConfigField.BtnSave);
            _visibleFields.Add(TrackpadConfigField.BtnBack);

            PopulateFieldBounds();
        }

        private void PopulateFieldBounds()
        {
            _fieldBounds.Clear();
            float topY = 50f;
            float rowHeight = 34f;
            float spacing = 5f;

            for (int i = 0; i < _visibleFields.Count; i++)
            {
                var field = _visibleFields[i];
                if (field == TrackpadConfigField.BtnSave)
                {
                    _fieldBounds[TrackpadConfigField.BtnSave] = new RectangleF(190f, 455f, 116f, 32f);
                }
                else if (field == TrackpadConfigField.BtnBack)
                {
                    _fieldBounds[TrackpadConfigField.BtnBack] = new RectangleF(314f, 455f, 116f, 32f);
                }
                else
                {
                    float y = topY + i * (rowHeight + spacing);
                    _fieldBounds[field] = new RectangleF(28f, y, 564f, rowHeight);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            GetTransformParams(out float scale, out float offsetX, out float offsetY);
            g.TranslateTransform(offsetX, offsetY);
            g.ScaleTransform(scale, scale);

            DrawHeader(g);
            DrawFields(g);
            DrawFooter(g);
        }

        private void DrawHeader(Graphics g)
        {
            using (var bg = new SolidBrush(HeaderBg))
            using (var linePen = new Pen(BorderSubtle, 1f))
            {
                g.FillRectangle(bg, 0, 0, LogicalWidth, 40);
                g.DrawLine(linePen, 0, 40, LogicalWidth, 40);
            }

            string padName = _isRightPad ? Strings.ProfileCardRightTrackpad : Strings.ProfileCardLeftTrackpad;
            string gameName = _game?.Name ?? Strings.DefaultGameName;
            string title = string.Format(Strings.TrackpadHeaderTitleFormat, padName, gameName);

            using (var font = new Font("Segoe UI", 10.2f, FontStyle.Bold))
            using (var brush = new SolidBrush(TextPrimary))
            {
                g.DrawString(title, font, brush, 18, 10);
            }

            // Badge do modo atual no canto superior direito
            string modeName = GetModeDisplayName(_config.Mode);
            Color modeColor = GetModeColor(_config.Mode);
            RectangleF badgeRect = new RectangleF(440, 8, 160, 24);

            using (var path = CreateRoundedRectanglePath(badgeRect, 4f))
            using (var badgeBg = new SolidBrush(Color.FromArgb(40, modeColor)))
            using (var badgePen = new Pen(modeColor, 1.2f))
            using (var badgeFont = new Font("Segoe UI", 7.8f, FontStyle.Bold))
            using (var badgeBrush = new SolidBrush(modeColor))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.FillPath(badgeBg, path);
                g.DrawPath(badgePen, path);
                g.DrawString(modeName, badgeFont, badgeBrush, badgeRect, sf);
            }
        }

        private void DrawFields(Graphics g)
        {
            for (int i = 0; i < _visibleFields.Count; i++)
            {
                var field = _visibleFields[i];
                if (!_fieldBounds.TryGetValue(field, out var rect)) continue;
                bool isFocused = i == _focusedIndex;

                if (field == TrackpadConfigField.BtnSave)
                {
                    DrawButton(g, rect, Strings.ProfileBtnSave, isFocused, ActiveGreen);
                }
                else if (field == TrackpadConfigField.BtnBack)
                {
                    DrawButton(g, rect, Strings.ProfileBtnBack, isFocused, BorderIdle);
                }
                else
                {
                    DrawFieldRow(g, field, rect, isFocused);
                }
            }
        }

        private void DrawFieldRow(Graphics g, TrackpadConfigField field, RectangleF rect, bool isFocused)
        {
            Color fill = isFocused ? FocusBg : CardBg;
            Color border = isFocused ? BorderFocused : BorderSubtle;
            float penWidth = isFocused ? 2f : 1f;

            using (var path = CreateRoundedRectanglePath(rect, 5f))
            using (var bgBrush = new SolidBrush(fill))
            using (var borderPen = new Pen(border, penWidth))
            {
                g.FillPath(bgBrush, path);
                g.DrawPath(borderPen, path);
            }

            string label = GetFieldLabel(field);
            using (var lblFont = new Font("Segoe UI", 8.2f, FontStyle.Bold))
            using (var lblBrush = new SolidBrush(isFocused ? TextPrimary : TextSecondary))
            {
                g.DrawString(label, lblFont, lblBrush, rect.X + 12, rect.Y + 9);
            }

            RectangleF valRect = new RectangleF(rect.X + 260, rect.Y + 4, rect.Width - 272, rect.Height - 8);
            DrawFieldValue(g, field, valRect, isFocused);
        }

        private void DrawFieldValue(Graphics g, TrackpadConfigField field, RectangleF rect, bool isFocused)
        {
            switch (field)
            {
                case TrackpadConfigField.Mode:
                    DrawSelector(g, rect, GetModeDisplayName(_config.Mode), isFocused, GetModeColor(_config.Mode));
                    break;

                case TrackpadConfigField.Sensitivity:
                    DrawSlider(g, rect, _config.Sensitivity, 1, 100, $"{_config.Sensitivity}%", isFocused);
                    break;

                case TrackpadConfigField.Friction:
                    DrawSlider(g, rect, _config.Friction, 1, 100, $"{_config.Friction}%", isFocused);
                    break;

                case TrackpadConfigField.InvertScroll:
                    string invText = _config.InvertScroll ? Strings.TrackpadYes : Strings.TrackpadNo;
                    Color invColor = _config.InvertScroll ? ActiveGreen : AlertAmber;
                    DrawSelector(g, rect, invText, isFocused, invColor);
                    break;

                case TrackpadConfigField.DeadzonePercent:
                    DrawSlider(g, rect, _config.DeadzonePercent, 5, 40, $"{_config.DeadzonePercent}%", isFocused);
                    break;

                case TrackpadConfigField.ClickPressureThreshold:
                    string pressText = _config.ClickPressureThreshold == 0
                        ? Strings.TrackpadPressureMechanicalOnly
                        : $"{_config.ClickPressureThreshold}";
                    DrawSlider(g, rect, _config.ClickPressureThreshold, 0, 25000, pressText, isFocused);
                    break;

                case TrackpadConfigField.HapticIntensity:
                    DrawSelector(g, rect, GetHapticsDisplayName(_config.HapticIntensity), isFocused, ModeCyan);
                    break;

                case TrackpadConfigField.PressAction:
                    DrawActionSlotPreview(g, rect, _config.PressActions, isFocused);
                    break;

                case TrackpadConfigField.TouchAction:
                    DrawActionSlotPreview(g, rect, _config.TouchActions, isFocused);
                    break;

                case TrackpadConfigField.DpadUp:
                    DrawActionSlotPreview(g, rect, _config.Up, isFocused);
                    break;

                case TrackpadConfigField.DpadDown:
                    DrawActionSlotPreview(g, rect, _config.Down, isFocused);
                    break;

                case TrackpadConfigField.DpadLeft:
                    DrawActionSlotPreview(g, rect, _config.Left, isFocused);
                    break;

                case TrackpadConfigField.DpadRight:
                    DrawActionSlotPreview(g, rect, _config.Right, isFocused);
                    break;
            }
        }

        private static void DrawSelector(Graphics g, RectangleF rect, string text, bool isFocused, Color accent)
        {
            using (var bg = new SolidBrush(InputBg))
            using (var pen = new Pen(isFocused ? BorderFocused : BorderIdle, 1f))
            using (var font = new Font("Segoe UI", 8.0f, FontStyle.Bold))
            using (var brush = new SolidBrush(accent))
            using (var arrowBrush = new SolidBrush(isFocused ? TextPrimary : TextSecondary))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.FillRectangle(bg, rect);
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);

                g.DrawString("◄", font, arrowBrush, rect.X + 12, rect.Y + rect.Height / 2 - 6);
                g.DrawString(text, font, brush, rect, sf);
                g.DrawString("►", font, arrowBrush, rect.Right - 22, rect.Y + rect.Height / 2 - 6);
            }
        }

        private static void DrawSlider(Graphics g, RectangleF rect, int val, int min, int max, string label, bool isFocused)
        {
            float barWidth = rect.Width - 90f;
            var barRect = new RectangleF(rect.X + 8, rect.Y + (rect.Height - 8) / 2, barWidth, 8);
            float ratio = Math.Max(0f, Math.Min(1f, (float)(val - min) / Math.Max(1, max - min)));

            using (var bg = new SolidBrush(InputBg))
            using (var pen = new Pen(BorderIdle, 1f))
            {
                g.FillRectangle(bg, barRect);
                g.DrawRectangle(pen, barRect.X, barRect.Y, barRect.Width, barRect.Height);
            }

            if (ratio > 0f)
            {
                var fillRect = new RectangleF(barRect.X, barRect.Y, barRect.Width * ratio, barRect.Height);
                using (var fillBrush = new SolidBrush(isFocused ? ActiveGreen : ModeCyan))
                {
                    g.FillRectangle(fillBrush, fillRect);
                }
            }

            var textRect = new RectangleF(rect.X + barWidth + 14, rect.Y, 70, rect.Height);
            using (var font = new Font("Segoe UI", 7.8f, FontStyle.Bold))
            using (var brush = new SolidBrush(isFocused ? TextPrimary : TextSecondary))
            using (var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(label, font, brush, textRect, sf);
            }
        }

        private static void DrawActionSlotPreview(Graphics g, RectangleF rect, List<InputAction> actions, bool isFocused)
        {
            bool hasActions = actions != null && actions.Count > 0;
            string desc = hasActions ? FormatActionList(actions) : Strings.ProfileActionNone;
            Color textColor = hasActions ? (isFocused ? ActiveGreen : ModeCyan) : TextSecondary;

            using (var bg = new SolidBrush(InputBg))
            using (var pen = new Pen(isFocused ? BorderFocused : (hasActions ? ActiveGreen : BorderIdle), 1f))
            using (var font = new Font("Segoe UI", 7.8f, FontStyle.Bold))
            using (var brush = new SolidBrush(textColor))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.FillRectangle(bg, rect);
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);

                string displayText = hasActions ? $"[ {desc} ]" : desc;
                g.DrawString(displayText, font, brush, rect, sf);
            }
        }

        private static void DrawButton(Graphics g, RectangleF rect, string text, bool isFocused, Color accent)
        {
            Color fill = isFocused ? FocusBg : CardBg;
            Color border = isFocused ? BorderFocused : accent;
            float penWidth = isFocused ? 2f : 1f;

            using (var path = CreateRoundedRectanglePath(rect, 4f))
            using (var bgBrush = new SolidBrush(fill))
            using (var borderPen = new Pen(border, penWidth))
            using (var font = new Font("Segoe UI", 8.2f, FontStyle.Bold))
            using (var textBrush = new SolidBrush(isFocused ? TextPrimary : accent))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.FillPath(bgBrush, path);
                g.DrawPath(borderPen, path);
                g.DrawString(text, font, textBrush, rect, sf);
            }
        }

        private static void DrawFooter(Graphics g)
        {
            using (var font = new Font("Segoe UI", 7.6f))
            using (var brush = new SolidBrush(TextSecondary))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(Strings.TrackpadFooterLegend, font, brush, new RectangleF(10, 508, LogicalWidth - 20, 20), sf);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            PointF pt = ClientToLogical(e.Location);

            for (int i = 0; i < _visibleFields.Count; i++)
            {
                var field = _visibleFields[i];
                if (_fieldBounds.TryGetValue(field, out var rect) && rect.Contains(pt))
                {
                    _focusedIndex = i;

                    if (e.Button == MouseButtons.Right)
                    {
                        ClearFocusedField();
                        return;
                    }

                    if (e.Button == MouseButtons.Left)
                    {
                        // Se clicou na metade esquerda ou direita de um slider/seletor
                        if (field == TrackpadConfigField.Mode ||
                            field == TrackpadConfigField.HapticIntensity ||
                            field == TrackpadConfigField.InvertScroll)
                        {
                            if (pt.X < rect.X + 350) AdjustFocusedField(-1);
                            else AdjustFocusedField(1);
                        }
                        else if (field == TrackpadConfigField.Sensitivity ||
                                 field == TrackpadConfigField.Friction ||
                                 field == TrackpadConfigField.DeadzonePercent ||
                                 field == TrackpadConfigField.ClickPressureThreshold)
                        {
                            UpdateSliderFromLogicalX(field, pt.X, rect);
                        }
                        else
                        {
                            ActivateFocusedField();
                        }
                    }
                    return;
                }
            }
        }

        private void UpdateSliderFromLogicalX(TrackpadConfigField field, float logicalX, RectangleF rect)
        {
            float barX = rect.X + 268;
            float barWidth = rect.Width - 362;
            float ratio = Math.Max(0f, Math.Min(1f, (logicalX - barX) / Math.Max(1f, barWidth)));

            switch (field)
            {
                case TrackpadConfigField.Sensitivity:
                    _config.Sensitivity = (int)Math.Round(1 + ratio * 99);
                    break;
                case TrackpadConfigField.Friction:
                    _config.Friction = (int)Math.Round(1 + ratio * 99);
                    break;
                case TrackpadConfigField.DeadzonePercent:
                    _config.DeadzonePercent = (int)Math.Round(5 + ratio * 35);
                    break;
                case TrackpadConfigField.ClickPressureThreshold:
                    _config.ClickPressureThreshold = (int)Math.Round(ratio * 50) * 500;
                    break;
            }
            Invalidate();
        }

        private static string GetFieldLabel(TrackpadConfigField field)
        {
            switch (field)
            {
                case TrackpadConfigField.Mode: return Strings.TrackpadModeLabel;
                case TrackpadConfigField.Sensitivity: return Strings.TrackpadSensitivityLabel;
                case TrackpadConfigField.Friction: return Strings.TrackpadFrictionLabel;
                case TrackpadConfigField.InvertScroll: return Strings.TrackpadInvertScrollLabel;
                case TrackpadConfigField.DeadzonePercent: return Strings.TrackpadDeadzonePercentLabel;
                case TrackpadConfigField.ClickPressureThreshold: return Strings.TrackpadClickPressureLabel;
                case TrackpadConfigField.HapticIntensity: return Strings.TrackpadHapticIntensityLabel;
                case TrackpadConfigField.PressAction: return Strings.TrackpadPressActionLabel;
                case TrackpadConfigField.TouchAction: return Strings.TrackpadTouchActionLabel;
                case TrackpadConfigField.DpadUp: return Strings.TrackpadDpadUpLabel;
                case TrackpadConfigField.DpadDown: return Strings.TrackpadDpadDownLabel;
                case TrackpadConfigField.DpadLeft: return Strings.TrackpadDpadLeftLabel;
                case TrackpadConfigField.DpadRight: return Strings.TrackpadDpadRightLabel;
                default: return field.ToString();
            }
        }

        private static string GetModeDisplayName(TrackpadMode mode)
        {
            switch (mode)
            {
                case TrackpadMode.MouseTrackball: return Strings.ProfileTrackpadModeMouse;
                case TrackpadMode.Scrollwheel: return Strings.ProfileTrackpadModeScroll;
                case TrackpadMode.DPad: return Strings.ProfileTrackpadModeDPad;
                case TrackpadMode.SingleButton: return Strings.ProfileTrackpadModeSingleButton;
                case TrackpadMode.Disabled: return Strings.ProfileTrackpadModeDisabled;
                default: return mode.ToString();
            }
        }

        private static Color GetModeColor(TrackpadMode mode)
        {
            switch (mode)
            {
                case TrackpadMode.MouseTrackball: return ModeCyan;
                case TrackpadMode.Scrollwheel: return AlertAmber;
                case TrackpadMode.DPad: return ActiveGreen;
                case TrackpadMode.SingleButton: return Color.FromArgb(180, 130, 255);
                case TrackpadMode.Disabled: return Color.FromArgb(140, 140, 140);
                default: return TextPrimary;
            }
        }

        private static string GetHapticsDisplayName(TrackpadHapticIntensity intensity)
        {
            switch (intensity)
            {
                case TrackpadHapticIntensity.Off: return Strings.TrackpadHapticOff;
                case TrackpadHapticIntensity.Low: return Strings.TrackpadHapticLow;
                case TrackpadHapticIntensity.Medium: return Strings.TrackpadHapticMedium;
                case TrackpadHapticIntensity.High: return Strings.TrackpadHapticHigh;
                default: return intensity.ToString();
            }
        }

        private static string FormatActionList(List<InputAction> actions)
        {
            if (actions == null || actions.Count == 0) return Strings.ProfileActionNone;
            var parts = new List<string>();
            foreach (var a in actions)
            {
                if (a == null) continue;
                if (a.Type == InputActionType.Key) parts.Add(a.Key.ToString());
                else if (a.Type == InputActionType.MouseButton) parts.Add(a.MouseButton.ToString());
                else if (a.Type == InputActionType.MouseWheel) parts.Add($"Scroll({a.WheelDelta})");
                else if (a.Type == InputActionType.SpecialAction) parts.Add(a.SpecialAction.ToString());
            }
            return parts.Count > 0 ? string.Join(" + ", parts) : Strings.ProfileActionNone;
        }

        private static List<InputAction> CloneActionList(List<InputAction> list)
        {
            var res = new List<InputAction>();
            if (list == null) return res;
            foreach (var a in list)
            {
                if (a != null) res.Add(a.Clone());
            }
            return res;
        }

        private static GraphicsPath CreateRoundedRectanglePath(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2f;
            if (rect.Width <= 0 || rect.Height <= 0) return path;

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private PointF ClientToLogical(Point pt)
        {
            GetTransformParams(out float scale, out float offsetX, out float offsetY);
            if (scale <= 0.001f) return PointF.Empty;
            return new PointF((pt.X - offsetX) / scale, (pt.Y - offsetY) / scale);
        }

        private void GetTransformParams(out float scale, out float offsetX, out float offsetY)
        {
            float clientW = Math.Max(1, ClientSize.Width);
            float clientH = Math.Max(1, ClientSize.Height);

            float scaleX = clientW / LogicalWidth;
            float scaleY = clientH / LogicalHeight;
            scale = Math.Min(scaleX, scaleY);

            offsetX = (clientW - (LogicalWidth * scale)) / 2f;
            offsetY = (clientH - (LogicalHeight * scale)) / 2f;
        }
    }
}
