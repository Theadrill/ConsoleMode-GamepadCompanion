using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.UI.Controls
{
    /// <summary>
    /// Seletor Visual de Mapeamento em 3 Abas (Fase 4).
    /// Ocupa 100% da área de conteúdo (Dock = DockStyle.Fill) e oferece navegação nativa couch gaming:
    /// 1. [1. TECLADO] — Layout ANSI completo renderizado em GDI+ (Funções, Numérico, QWERTY, Modificadores e Setas).
    /// 2. [2. MOUSE] — Ilustração vetorial do mouse com cards para botões e scroll.
    /// 3. [3. SISTEMA & ESPECIAL] — Ações de áudio/mídia, atalhos de janelas/captura e funções do Companion.
    /// Suporta seleção direta com (A), montagem de combos com (X), limpeza com (Y) e cancelamento com (B).
    /// </summary>
    public sealed class InputVisualSelectorControl : UserControl
    {
        private static readonly Color BgColor = Color.FromArgb(24, 26, 32);
        private static readonly Color HeaderBg = Color.FromArgb(30, 32, 40);
        private static readonly Color CardBg = Color.FromArgb(34, 37, 46);
        private static readonly Color SubHeaderBg = Color.FromArgb(20, 22, 28);
        private static readonly Color KeyBg = Color.FromArgb(38, 42, 53);
        private static readonly Color FocusBg = Color.FromArgb(45, 68, 105);
        private static readonly Color ComboBg = Color.FromArgb(25, 60, 42);
        private static readonly Color BorderIdle = Color.FromArgb(120, 126, 140);
        private static readonly Color BorderSubtle = Color.FromArgb(65, 70, 82);
        private static readonly Color BorderFocused = Color.FromArgb(120, 190, 255);
        private static readonly Color ActiveGreen = Color.FromArgb(120, 230, 150);
        private static readonly Color TextPrimary = Color.FromArgb(230, 232, 240);
        private static readonly Color TextSecondary = Color.FromArgb(160, 170, 185);
        private static readonly Color AccentBlue = Color.FromArgb(100, 170, 245);

        private const float LogicalWidth = 620f;
        private const float LogicalHeight = 540f;

        private const long InitialRepeatDelayMs = 230;
        private const long RepeatIntervalMs = 80;

        public sealed class KeyItem
        {
            public VirtualKey Key { get; }
            public string Label { get; }
            public RectangleF Bounds { get; set; }
            public int Row { get; }
            public int Col { get; }

            public KeyItem(VirtualKey key, string label, int row, int col)
            {
                Key = key;
                Label = label;
                Row = row;
                Col = col;
            }
        }

        public sealed class SysItem
        {
            public string Title { get; }
            public InputAction Action { get; }
            public RectangleF Bounds { get; set; }

            public SysItem(string title, InputAction action)
            {
                Title = title;
                Action = action;
            }
        }

        private ProfileEditorSlot _targetSlot;
        private string _slotTitle = string.Empty;
        private string _gameName = string.Empty;
        private int _currentTab; // 0 = Teclado, 1 = Mouse, 2 = Sistema & Especial
        private readonly List<InputAction> _currentCombo = new List<InputAction>();

        // Tab 0: Teclado ANSI
        private readonly List<KeyItem>[] _keyboardRows = new List<KeyItem>[6];
        private int _kbRow = 3;
        private int _kbCol = 1; // Inicia na tecla 'A'

        // Tab 1: Mouse
        private readonly SysItem[] _mouseItems = new SysItem[6];
        private int _mouseIndex;

        // Tab 2: Sistema & Especial (3 Colunas)
        private readonly List<SysItem>[] _sysColumns = new List<SysItem>[3];
        private int _sysCol;
        private int _sysRow;

        // Limites de clique mouse
        private readonly RectangleF[] _tabBounds = new RectangleF[3];
        private RectangleF _btnSelectBounds;
        private RectangleF _btnConfirmBounds;
        private RectangleF _btnToggleBounds;
        private RectangleF _btnClearBounds;
        private RectangleF _btnCancelBounds;

        // Gamepad Debounce & Repetição
        private bool _lastUp;
        private bool _lastDown;
        private bool _lastLeft;
        private bool _lastRight;
        private bool _lastA;
        private bool _lastB;
        private bool _lastX;
        private bool _lastY;
        private bool _lastLB;
        private bool _lastRB;
        private bool _lastRT;
        private long _lastMoveTime;
        private int _repeatCount;

        public event Action<ProfileEditorSlot, List<InputAction>> BindingConfirmed;
        public event Action CancelRequested;

        public InputVisualSelectorControl()
        {
            BackColor = BgColor;
            DoubleBuffered = true;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable,
                true);

            TabStop = true;
            InitializeLayoutStructure();
            BuildLogicalBounds();
        }

        public ProfileEditorSlot TargetSlot => _targetSlot;
        public string SlotTitle => _slotTitle;
        public int CurrentTab => _currentTab;
        public IReadOnlyList<InputAction> CurrentCombo => _currentCombo;
        public int KeyboardRow => _kbRow;
        public int KeyboardCol => _kbCol;
        public int MouseIndex => _mouseIndex;
        public int SysColumn => _sysCol;
        public int SysRow => _sysRow;

        private void InitializeLayoutStructure()
        {
            // Inicializa Tab 0: Teclado ANSI
            for (int r = 0; r < 6; r++)
            {
                _keyboardRows[r] = new List<KeyItem>();
            }

            // Linha 0: Esc + F1..F12
            _keyboardRows[0].Add(new KeyItem(VirtualKey.Escape, "Esc", 0, 0));
            _keyboardRows[0].Add(new KeyItem(VirtualKey.F1, "F1", 0, 1));
            _keyboardRows[0].Add(new KeyItem(VirtualKey.F2, "F2", 0, 2));
            _keyboardRows[0].Add(new KeyItem(VirtualKey.F3, "F3", 0, 3));
            _keyboardRows[0].Add(new KeyItem(VirtualKey.F4, "F4", 0, 4));
            _keyboardRows[0].Add(new KeyItem(VirtualKey.F5, "F5", 0, 5));
            _keyboardRows[0].Add(new KeyItem(VirtualKey.F6, "F6", 0, 6));
            _keyboardRows[0].Add(new KeyItem(VirtualKey.F7, "F7", 0, 7));
            _keyboardRows[0].Add(new KeyItem(VirtualKey.F8, "F8", 0, 8));
            _keyboardRows[0].Add(new KeyItem(VirtualKey.F9, "F9", 0, 9));
            _keyboardRows[0].Add(new KeyItem(VirtualKey.F10, "F10", 0, 10));
            _keyboardRows[0].Add(new KeyItem(VirtualKey.F11, "F11", 0, 11));
            _keyboardRows[0].Add(new KeyItem(VirtualKey.F12, "F12", 0, 12));

            // Linha 1: ` 1 2 3 4 5 6 7 8 9 0 - = Backspace
            _keyboardRows[1].Add(new KeyItem(VirtualKey.OemTilde, "`", 1, 0));
            _keyboardRows[1].Add(new KeyItem(VirtualKey.D1, "1", 1, 1));
            _keyboardRows[1].Add(new KeyItem(VirtualKey.D2, "2", 1, 2));
            _keyboardRows[1].Add(new KeyItem(VirtualKey.D3, "3", 1, 3));
            _keyboardRows[1].Add(new KeyItem(VirtualKey.D4, "4", 1, 4));
            _keyboardRows[1].Add(new KeyItem(VirtualKey.D5, "5", 1, 5));
            _keyboardRows[1].Add(new KeyItem(VirtualKey.D6, "6", 1, 6));
            _keyboardRows[1].Add(new KeyItem(VirtualKey.D7, "7", 1, 7));
            _keyboardRows[1].Add(new KeyItem(VirtualKey.D8, "8", 1, 8));
            _keyboardRows[1].Add(new KeyItem(VirtualKey.D9, "9", 1, 9));
            _keyboardRows[1].Add(new KeyItem(VirtualKey.D0, "0", 1, 10));
            _keyboardRows[1].Add(new KeyItem(VirtualKey.OemMinus, "-", 1, 11));
            _keyboardRows[1].Add(new KeyItem(VirtualKey.OemPlus, "=", 1, 12));
            _keyboardRows[1].Add(new KeyItem(VirtualKey.Backspace, "⌫ Back", 1, 13));

            // Linha 2: Tab Q W E R T Y U I O P [ ] \
            _keyboardRows[2].Add(new KeyItem(VirtualKey.Tab, "Tab", 2, 0));
            _keyboardRows[2].Add(new KeyItem(VirtualKey.Q, "Q", 2, 1));
            _keyboardRows[2].Add(new KeyItem(VirtualKey.W, "W", 2, 2));
            _keyboardRows[2].Add(new KeyItem(VirtualKey.E, "E", 2, 3));
            _keyboardRows[2].Add(new KeyItem(VirtualKey.R, "R", 2, 4));
            _keyboardRows[2].Add(new KeyItem(VirtualKey.T, "T", 2, 5));
            _keyboardRows[2].Add(new KeyItem(VirtualKey.Y, "Y", 2, 6));
            _keyboardRows[2].Add(new KeyItem(VirtualKey.U, "U", 2, 7));
            _keyboardRows[2].Add(new KeyItem(VirtualKey.I, "I", 2, 8));
            _keyboardRows[2].Add(new KeyItem(VirtualKey.O, "O", 2, 9));
            _keyboardRows[2].Add(new KeyItem(VirtualKey.P, "P", 2, 10));
            _keyboardRows[2].Add(new KeyItem(VirtualKey.OemOpenBrackets, "[", 2, 11));
            _keyboardRows[2].Add(new KeyItem(VirtualKey.OemCloseBrackets, "]", 2, 12));
            _keyboardRows[2].Add(new KeyItem(VirtualKey.OemPipe, "\\", 2, 13));

            // Linha 3: Caps A S D F G H J K L ; ' Enter
            _keyboardRows[3].Add(new KeyItem(VirtualKey.CapsLock, "Caps", 3, 0));
            _keyboardRows[3].Add(new KeyItem(VirtualKey.A, "A", 3, 1));
            _keyboardRows[3].Add(new KeyItem(VirtualKey.S, "S", 3, 2));
            _keyboardRows[3].Add(new KeyItem(VirtualKey.D, "D", 3, 3));
            _keyboardRows[3].Add(new KeyItem(VirtualKey.F, "F", 3, 4));
            _keyboardRows[3].Add(new KeyItem(VirtualKey.G, "G", 3, 5));
            _keyboardRows[3].Add(new KeyItem(VirtualKey.H, "H", 3, 6));
            _keyboardRows[3].Add(new KeyItem(VirtualKey.J, "J", 3, 7));
            _keyboardRows[3].Add(new KeyItem(VirtualKey.K, "K", 3, 8));
            _keyboardRows[3].Add(new KeyItem(VirtualKey.L, "L", 3, 9));
            _keyboardRows[3].Add(new KeyItem(VirtualKey.OemSemicolon, ";", 3, 10));
            _keyboardRows[3].Add(new KeyItem(VirtualKey.OemQuotes, "'", 3, 11));
            _keyboardRows[3].Add(new KeyItem(VirtualKey.Enter, "Enter ↵", 3, 12));

            // Linha 4: LShift Z X C V B N M , . / RShift
            _keyboardRows[4].Add(new KeyItem(VirtualKey.LeftShift, "Shift ⇧", 4, 0));
            _keyboardRows[4].Add(new KeyItem(VirtualKey.Z, "Z", 4, 1));
            _keyboardRows[4].Add(new KeyItem(VirtualKey.X, "X", 4, 2));
            _keyboardRows[4].Add(new KeyItem(VirtualKey.C, "C", 4, 3));
            _keyboardRows[4].Add(new KeyItem(VirtualKey.V, "V", 4, 4));
            _keyboardRows[4].Add(new KeyItem(VirtualKey.B, "B", 4, 5));
            _keyboardRows[4].Add(new KeyItem(VirtualKey.N, "N", 4, 6));
            _keyboardRows[4].Add(new KeyItem(VirtualKey.M, "M", 4, 7));
            _keyboardRows[4].Add(new KeyItem(VirtualKey.OemComma, ",", 4, 8));
            _keyboardRows[4].Add(new KeyItem(VirtualKey.OemPeriod, ".", 4, 9));
            _keyboardRows[4].Add(new KeyItem(VirtualKey.OemQuestion, "/", 4, 10));
            _keyboardRows[4].Add(new KeyItem(VirtualKey.RightShift, "Shift ⇧", 4, 11));

            // Linha 5: LCtrl Win LAlt Espaço RAlt RCtrl ← ↑ ↓ →
            _keyboardRows[5].Add(new KeyItem(VirtualKey.LeftControl, "Ctrl", 5, 0));
            _keyboardRows[5].Add(new KeyItem(VirtualKey.LeftWindows, "Win", 5, 1));
            _keyboardRows[5].Add(new KeyItem(VirtualKey.LeftAlt, "Alt", 5, 2));
            _keyboardRows[5].Add(new KeyItem(VirtualKey.Space, "Espaço", 5, 3));
            _keyboardRows[5].Add(new KeyItem(VirtualKey.RightAlt, "Alt", 5, 4));
            _keyboardRows[5].Add(new KeyItem(VirtualKey.RightControl, "Ctrl", 5, 5));
            _keyboardRows[5].Add(new KeyItem(VirtualKey.Left, "←", 5, 6));
            _keyboardRows[5].Add(new KeyItem(VirtualKey.Up, "↑", 5, 7));
            _keyboardRows[5].Add(new KeyItem(VirtualKey.Down, "↓", 5, 8));
            _keyboardRows[5].Add(new KeyItem(VirtualKey.Right, "→", 5, 9));

            // Inicializa Tab 1: Mouse
            _mouseItems[0] = new SysItem(Strings.VisualSelectorMouseLeft, InputAction.FromMouseButton(MouseButton.Left));
            _mouseItems[1] = new SysItem(Strings.VisualSelectorMouseRight, InputAction.FromMouseButton(MouseButton.Right));
            _mouseItems[2] = new SysItem(Strings.VisualSelectorMouseMiddle, InputAction.FromMouseButton(MouseButton.Middle));
            _mouseItems[3] = new SysItem(Strings.VisualSelectorMouseWheelUp, InputAction.FromMouseWheel(120));
            _mouseItems[4] = new SysItem(Strings.VisualSelectorMouseWheelDown, InputAction.FromMouseWheel(-120));
            _mouseItems[5] = new SysItem(Strings.VisualSelectorMouseClear, null);

            // Inicializa Tab 2: Sistema & Especial (3 Colunas)
            for (int c = 0; c < 3; c++)
            {
                _sysColumns[c] = new List<SysItem>();
            }

            // Coluna 0: Áudio & Mídia
            _sysColumns[0].Add(new SysItem(Strings.VisualSelectorAudioVolUp, InputAction.FromKey(VirtualKey.VolumeUp)));
            _sysColumns[0].Add(new SysItem(Strings.VisualSelectorAudioVolDown, InputAction.FromKey(VirtualKey.VolumeDown)));
            _sysColumns[0].Add(new SysItem(Strings.VisualSelectorAudioMute, InputAction.FromKey(VirtualKey.VolumeMute)));
            _sysColumns[0].Add(new SysItem(Strings.VisualSelectorAudioPlayPause, InputAction.FromKey(VirtualKey.MediaPlayPause)));
            _sysColumns[0].Add(new SysItem(Strings.VisualSelectorAudioNext, InputAction.FromKey(VirtualKey.MediaNextTrack)));
            _sysColumns[0].Add(new SysItem(Strings.VisualSelectorAudioPrev, InputAction.FromKey(VirtualKey.MediaPrevTrack)));

            // Coluna 1: Janelas & Captura
            _sysColumns[1].Add(new SysItem(Strings.VisualSelectorWinPrintScreen, InputAction.FromKey(VirtualKey.PrintScreen)));
            _sysColumns[1].Add(new SysItem(Strings.VisualSelectorWinAltEnter, InputAction.FromSpecial(CompanionActionType.AltEnter)));
            _sysColumns[1].Add(new SysItem(Strings.VisualSelectorWinAltTab, InputAction.FromSpecial(CompanionActionType.AltTab)));
            _sysColumns[1].Add(new SysItem(Strings.VisualSelectorWinShowDesktop, InputAction.FromSpecial(CompanionActionType.ShowDesktop)));
            _sysColumns[1].Add(new SysItem(Strings.VisualSelectorWinAltF4, InputAction.FromSpecial(CompanionActionType.AltF4)));
            _sysColumns[1].Add(new SysItem(Strings.VisualSelectorWinKey, InputAction.FromKey(VirtualKey.LeftWindows)));

            // Coluna 2: Funções do Companion
            _sysColumns[2].Add(new SysItem(Strings.VisualSelectorCompCenterCursor, InputAction.FromSpecial(CompanionActionType.CenterCursor)));
            _sysColumns[2].Add(new SysItem(Strings.VisualSelectorCompToggleClip, InputAction.FromSpecial(CompanionActionType.ToggleClipCursor)));
        }

        private void BuildLogicalBounds()
        {
            // Abas no topo (Y: 0 a 40)
            float tabW = 180f;
            float tabGap = 12f;
            float totalTabsW = (tabW * 3f) + (tabGap * 2f);
            float startTabX = (LogicalWidth - totalTabsW) / 2f;
            for (int i = 0; i < 3; i++)
            {
                _tabBounds[i] = new RectangleF(startTabX + i * (tabW + tabGap), 6, tabW, 30);
            }

            // Teclado ANSI: Layout geométrico das 6 linhas (Y: 110 a 344)
            float kbStartX = 18f;
            float kbTotalW = LogicalWidth - 36f; // 584px
            float rowY = 114f;
            float rowH = 34f;
            float gap = 4f;

            // Linha 0: Esc (38) + 12 F-keys (38 cada) com pequenos espaçamentos
            float fKeyW = 38f;
            float curX = kbStartX;
            for (int c = 0; c < _keyboardRows[0].Count; c++)
            {
                float kw = (c == 0) ? 42f : fKeyW;
                _keyboardRows[0][c].Bounds = new RectangleF(curX, rowY, kw, rowH);
                curX += kw + gap;
                if (c == 0 || c == 4 || c == 8) curX += 4f; // Pequeno gap visual entre blocos de F-keys
            }

            // Linha 1: 14 teclas: 13 teclas de 37px + Backspace 70px
            rowY += rowH + gap;
            curX = kbStartX;
            float numKeyW = 36.8f;
            for (int c = 0; c < _keyboardRows[1].Count; c++)
            {
                float kw = (c == 13) ? (kbStartX + kbTotalW - curX) : numKeyW;
                _keyboardRows[1][c].Bounds = new RectangleF(curX, rowY, kw, rowH);
                curX += kw + gap;
            }

            // Linha 2: Tab (48px) + 10 letras (36.8px) + [ (36.8) + ] (36.8) + \ (resto)
            rowY += rowH + gap;
            curX = kbStartX;
            for (int c = 0; c < _keyboardRows[2].Count; c++)
            {
                float kw = (c == 0) ? 50f : (c == 13 ? (kbStartX + kbTotalW - curX) : numKeyW);
                _keyboardRows[2][c].Bounds = new RectangleF(curX, rowY, kw, rowH);
                curX += kw + gap;
            }

            // Linha 3: Caps (58px) + 9 letras (36.8px) + ; + ' + Enter (resto)
            rowY += rowH + gap;
            curX = kbStartX;
            for (int c = 0; c < _keyboardRows[3].Count; c++)
            {
                float kw = (c == 0) ? 60f : (c == 12 ? (kbStartX + kbTotalW - curX) : numKeyW);
                _keyboardRows[3][c].Bounds = new RectangleF(curX, rowY, kw, rowH);
                curX += kw + gap;
            }

            // Linha 4: Shift (72px) + 7 letras (36.8px) + , + . + / + Shift (resto)
            rowY += rowH + gap;
            curX = kbStartX;
            for (int c = 0; c < _keyboardRows[4].Count; c++)
            {
                float kw = (c == 0) ? 74f : (c == 11 ? (kbStartX + kbTotalW - curX) : numKeyW);
                _keyboardRows[4][c].Bounds = new RectangleF(curX, rowY, kw, rowH);
                curX += kw + gap;
            }

            // Linha 5: LCtrl (44) Win (36) LAlt (42) Space (184) RAlt (42) RCtrl (44) Setas ← ↑ ↓ → (36 cada)
            rowY += rowH + gap;
            curX = kbStartX;
            float[] r5Widths = new float[] { 46f, 38f, 44f, 178f, 44f, 46f, 38f, 38f, 38f, 38f };
            for (int c = 0; c < _keyboardRows[5].Count; c++)
            {
                float kw = (c < r5Widths.Length) ? r5Widths[c] : 38f;
                _keyboardRows[5][c].Bounds = new RectangleF(curX, rowY, kw, rowH);
                curX += kw + gap;
            }

            // Tab 1: Mouse (Cards à direita: X = 250 a 594, W = 344)
            float mCardX = 260f;
            float mCardW = 330f;
            float mCardY = 114f;
            float mCardH = 48f;
            float mCardGap = 10f;
            for (int i = 0; i < _mouseItems.Length; i++)
            {
                _mouseItems[i].Bounds = new RectangleF(mCardX, mCardY + i * (mCardH + mCardGap), mCardW, mCardH);
            }

            // Tab 2: Sistema (3 colunas, W = 184 cada)
            float sysColW = 184f;
            float sysGapX = 14f;
            float sysStartX = (LogicalWidth - ((sysColW * 3f) + (sysGapX * 2f))) / 2f;
            float sysStartY = 118f;
            float sysItemH = 50f;
            float sysItemGap = 8f;

            for (int c = 0; c < 3; c++)
            {
                float colX = sysStartX + c * (sysColW + sysGapX);
                for (int r = 0; r < _sysColumns[c].Count; r++)
                {
                    _sysColumns[c][r].Bounds = new RectangleF(colX, sysStartY + r * (sysItemH + sysItemGap), sysColW, sysItemH);
                }
            }

            // Botões do Rodapé (Y = 498, H = 34)
            _btnSelectBounds = new RectangleF(14, 498, 114, 34);
            _btnToggleBounds = new RectangleF(134, 498, 114, 34);
            _btnClearBounds = new RectangleF(254, 498, 100, 34);
            _btnConfirmBounds = new RectangleF(360, 498, 120, 34);
            _btnCancelBounds = new RectangleF(486, 498, 120, 34);
        }

        public void OpenForSlot(ProfileEditorSlot slot, string slotTitle, string gameName, List<InputAction> currentActions)
        {
            _targetSlot = slot;
            _slotTitle = slotTitle ?? string.Empty;
            _gameName = gameName ?? string.Empty;

            _currentCombo.Clear();
            if (currentActions != null)
            {
                foreach (var a in currentActions)
                {
                    if (a != null && a.Type != InputActionType.None)
                    {
                        _currentCombo.Add(a.Clone());
                    }
                }
            }

            // Define aba inicial com base no tipo de ação atual se houver
            if (_currentCombo.Count > 0)
            {
                var first = _currentCombo[0];
                if (first.Type == InputActionType.MouseButton || first.Type == InputActionType.MouseWheel)
                {
                    _currentTab = 1;
                }
                else if (first.Type == InputActionType.SpecialAction ||
                         first.Key == VirtualKey.VolumeUp || first.Key == VirtualKey.VolumeDown ||
                         first.Key == VirtualKey.VolumeMute || first.Key == VirtualKey.MediaPlayPause ||
                         first.Key == VirtualKey.MediaNextTrack || first.Key == VirtualKey.MediaPrevTrack ||
                         first.Key == VirtualKey.PrintScreen)
                {
                    _currentTab = 2;
                }
                else
                {
                    _currentTab = 0;
                }
            }
            else
            {
                _currentTab = 0;
            }

            ResetInputState();
            Invalidate();
        }

        public void SetTab(int tab)
        {
            _currentTab = Math.Max(0, Math.Min(2, tab));
            Invalidate();
        }

        public void ResetInputState()
        {
            _lastUp = false;
            _lastDown = false;
            _lastLeft = false;
            _lastRight = false;
            _lastA = true;
            _lastB = true;
            _lastX = false;
            _lastY = false;
            _lastLB = false;
            _lastRB = false;
            _lastRT = true;
            _repeatCount = 0;
            _lastMoveTime = 0;
        }

        public void ProcessGamepad(GamepadState state, long nowMs)
        {
            bool btnLB = state.IsPressed(GamepadButtons.LeftShoulder);
            bool btnRB = state.IsPressed(GamepadButtons.RightShoulder);
            bool btnA = state.IsPressed(GamepadButtons.A);
            bool btnB = state.IsPressed(GamepadButtons.B);
            bool btnX = state.IsPressed(GamepadButtons.X);
            bool btnY = state.IsPressed(GamepadButtons.Y);
            bool triggerRT = state.RightTrigger > 120;

            // Troca de Abas com LB e RB
            if (btnLB && !_lastLB)
            {
                _currentTab = (_currentTab + 2) % 3;
                Invalidate();
            }
            if (btnRB && !_lastRB)
            {
                _currentTab = (_currentTab + 1) % 3;
                Invalidate();
            }

            // Seleção de botão/tecla único com (A)
            if (btnA && !_lastA)
            {
                SelectFocusedItemAsSingle();
            }

            // Confirmação e Salvamento com [RT]
            if (triggerRT && !_lastRT)
            {
                ConfirmSelection();
            }

            // Cancelamento com (B)
            if (btnB && !_lastB)
            {
                Cancel();
            }

            // Adicionar/Remover no Combo com (X)
            if (btnX && !_lastX)
            {
                ToggleFocusedItemInCombo();
            }

            // Limpar Combo com (Y)
            if (btnY && !_lastY)
            {
                ClearCombo();
            }

            // Navegação direcional (D-Pad e Analógico Esquerdo)
            bool up = state.IsPressed(GamepadButtons.DPadUp) || state.LeftThumbY > 16000;
            bool down = state.IsPressed(GamepadButtons.DPadDown) || state.LeftThumbY < -16000;
            bool left = state.IsPressed(GamepadButtons.DPadLeft) || state.LeftThumbX < -16000;
            bool right = state.IsPressed(GamepadButtons.DPadRight) || state.LeftThumbX > 16000;

            bool isAnyDirPressed = up || down || left || right;
            bool isInitialDir = (up && !_lastUp) || (down && !_lastDown) || (left && !_lastLeft) || (right && !_lastRight);

            if (isAnyDirPressed)
            {
                long delay = (_repeatCount == 0) ? InitialRepeatDelayMs : RepeatIntervalMs;
                if (isInitialDir || (nowMs - _lastMoveTime >= delay))
                {
                    HandleDirectionalMove(up, down, left, right);
                    _lastMoveTime = nowMs;
                    _repeatCount = isInitialDir ? 0 : _repeatCount + 1;
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
            _lastA = btnA;
            _lastB = btnB;
            _lastX = btnX;
            _lastY = btnY;
            _lastLB = btnLB;
            _lastRB = btnRB;
            _lastRT = triggerRT;
        }

        private void HandleDirectionalMove(bool up, bool down, bool left, bool right)
        {
            if (_currentTab == 0)
            {
                // Tab 0: Teclado ANSI
                if (up && _kbRow > 0)
                {
                    MoveKeyboardRow(-1);
                }
                else if (down && _kbRow < 5)
                {
                    MoveKeyboardRow(1);
                }
                else if (left && _kbCol > 0)
                {
                    _kbCol--;
                    Invalidate();
                }
                else if (right && _kbCol < _keyboardRows[_kbRow].Count - 1)
                {
                    _kbCol++;
                    Invalidate();
                }
            }
            else if (_currentTab == 1)
            {
                // Tab 1: Mouse (Lista vertical de 6 itens)
                if (up && _mouseIndex > 0)
                {
                    _mouseIndex--;
                    Invalidate();
                }
                else if (down && _mouseIndex < _mouseItems.Length - 1)
                {
                    _mouseIndex++;
                    Invalidate();
                }
            }
            else if (_currentTab == 2)
            {
                // Tab 2: Sistema (3 Colunas)
                if (left && _sysCol > 0)
                {
                    _sysCol--;
                    _sysRow = Math.Min(_sysRow, _sysColumns[_sysCol].Count - 1);
                    Invalidate();
                }
                else if (right && _sysCol < 2)
                {
                    _sysCol++;
                    _sysRow = Math.Min(_sysRow, _sysColumns[_sysCol].Count - 1);
                    Invalidate();
                }
                else if (up && _sysRow > 0)
                {
                    _sysRow--;
                    Invalidate();
                }
                else if (down && _sysRow < _sysColumns[_sysCol].Count - 1)
                {
                    _sysRow++;
                    Invalidate();
                }
            }
        }

        private void MoveKeyboardRow(int rowDelta)
        {
            int oldRow = _kbRow;
            int targetRow = oldRow + rowDelta;
            if (targetRow < 0 || targetRow > 5) return;

            // Navegação espacial: seleciona a tecla na nova linha cujo centro X esteja mais próximo
            var currentKey = _keyboardRows[oldRow][_kbCol];
            float currentCenterX = currentKey.Bounds.X + (currentKey.Bounds.Width / 2f);

            int closestCol = 0;
            float closestDist = float.MaxValue;
            for (int c = 0; c < _keyboardRows[targetRow].Count; c++)
            {
                var candidate = _keyboardRows[targetRow][c];
                float candidateCenterX = candidate.Bounds.X + (candidate.Bounds.Width / 2f);
                float dist = Math.Abs(candidateCenterX - currentCenterX);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestCol = c;
                }
            }

            _kbRow = targetRow;
            _kbCol = closestCol;
            Invalidate();
        }

        public InputAction GetFocusedAction()
        {
            if (_currentTab == 0)
            {
                if (_kbRow >= 0 && _kbRow < _keyboardRows.Length &&
                    _kbCol >= 0 && _kbCol < _keyboardRows[_kbRow].Count)
                {
                    return InputAction.FromKey(_keyboardRows[_kbRow][_kbCol].Key);
                }
            }
            else if (_currentTab == 1)
            {
                if (_mouseIndex >= 0 && _mouseIndex < _mouseItems.Length)
                {
                    return _mouseItems[_mouseIndex].Action;
                }
            }
            else if (_currentTab == 2)
            {
                if (_sysCol >= 0 && _sysCol < 3 &&
                    _sysRow >= 0 && _sysRow < _sysColumns[_sysCol].Count)
                {
                    return _sysColumns[_sysCol][_sysRow].Action;
                }
            }
            return null;
        }

        public void SelectFocusedItemAsSingle()
        {
            var focused = GetFocusedAction();
            _currentCombo.Clear();
            if (focused != null && focused.Type != InputActionType.None)
            {
                _currentCombo.Add(focused.Clone());
            }
            Invalidate();
        }

        public void ToggleFocusedItemInCombo()
        {
            var action = GetFocusedAction();
            if (action == null)
            {
                // Item de limpar
                ClearCombo();
                return;
            }

            // Verifica se já está presente no combo
            int existingIndex = -1;
            for (int i = 0; i < _currentCombo.Count; i++)
            {
                if (_currentCombo[i].Equals(action))
                {
                    existingIndex = i;
                    break;
                }
            }

            if (existingIndex >= 0)
            {
                _currentCombo.RemoveAt(existingIndex);
            }
            else
            {
                _currentCombo.Add(action.Clone());
            }

            Invalidate();
        }

        public void ClearCombo()
        {
            _currentCombo.Clear();
            Invalidate();
        }

        public void ConfirmSelection()
        {
            if (_currentCombo.Count > 0)
            {
                BindingConfirmed?.Invoke(_targetSlot, new List<InputAction>(_currentCombo));
            }
            else
            {
                var focused = GetFocusedAction();
                var result = (focused != null) ? new List<InputAction> { focused } : new List<InputAction>();
                BindingConfirmed?.Invoke(_targetSlot, result);
            }
        }

        public void Cancel()
        {
            CancelRequested?.Invoke();
        }

        private bool IsActionInCurrentCombo(InputAction action)
        {
            if (action == null) return false;
            foreach (var a in _currentCombo)
            {
                if (a != null && a.Equals(action)) return true;
            }
            return false;
        }

        private bool IsKeyInCurrentCombo(VirtualKey key)
        {
            foreach (var a in _currentCombo)
            {
                if (a != null && a.Type == InputActionType.Key && a.Key == key)
                    return true;
            }
            return false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.Clear(BgColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            GetTransformParams(out float scale, out float offsetX, out float offsetY);
            g.TranslateTransform(offsetX, offsetY);
            g.ScaleTransform(scale, scale);

            DrawHeader(g);
            DrawSubHeader(g);

            if (_currentTab == 0)
            {
                DrawTabKeyboard(g);
            }
            else if (_currentTab == 1)
            {
                DrawTabMouse(g);
            }
            else
            {
                DrawTabSystem(g);
            }

            DrawFooter(g);
        }

        private void GetTransformParams(out float scale, out float offsetX, out float offsetY)
        {
            float w = ClientSize.Width;
            float h = ClientSize.Height;
            scale = Math.Min(w / LogicalWidth, h / LogicalHeight);
            offsetX = (w - LogicalWidth * scale) / 2f;
            offsetY = (h - LogicalHeight * scale) / 2f;
        }

        private PointF ClientToLogical(Point pt)
        {
            GetTransformParams(out float scale, out float offsetX, out float offsetY);
            if (scale <= 0.001f) return PointF.Empty;
            return new PointF((pt.X - offsetX) / scale, (pt.Y - offsetY) / scale);
        }

        private void DrawHeader(Graphics g)
        {
            using (var bgBrush = new SolidBrush(HeaderBg))
            using (var pen = new Pen(BorderSubtle, 1f))
            {
                g.FillRectangle(bgBrush, 0, 0, LogicalWidth, 42);
                g.DrawLine(pen, 0, 42, LogicalWidth, 42);
            }

            // Indicadores de ombro [LB] e [RB]
            using (var hintFont = new Font("Segoe UI", 8f, FontStyle.Bold))
            using (var hintBrush = new SolidBrush(AccentBlue))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString("[LB]", hintFont, hintBrush, new RectangleF(10, 6, 32, 30), sf);
                g.DrawString("[RB]", hintFont, hintBrush, new RectangleF(LogicalWidth - 42, 6, 32, 30), sf);
            }

            string[] tabNames = new[] { Strings.VisualSelectorTabKeyboard, Strings.VisualSelectorTabMouse, Strings.VisualSelectorTabSystem };

            using (var tabFont = new Font("Segoe UI", 9.2f, FontStyle.Bold))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                for (int i = 0; i < 3; i++)
                {
                    bool isActive = (i == _currentTab);
                    var rect = _tabBounds[i];

                    using (var tabBg = new SolidBrush(isActive ? CardBg : Color.FromArgb(20, 22, 28)))
                    using (var borderPen = new Pen(isActive ? BorderFocused : BorderSubtle, isActive ? 1.5f : 1f))
                    using (var textBrush = new SolidBrush(isActive ? TextPrimary : TextSecondary))
                    {
                        DrawRoundedRectangle(g, tabBg, borderPen, rect, 4f);
                        string dot = isActive ? "● " : "○ ";
                        g.DrawString(dot + tabNames[i], tabFont, textBrush, rect, sf);
                    }
                }
            }
        }

        private void DrawSubHeader(Graphics g)
        {
            using (var bg = new SolidBrush(SubHeaderBg))
            using (var pen = new Pen(BorderSubtle, 1f))
            {
                g.FillRectangle(bg, 0, 43, LogicalWidth, 44);
                g.DrawLine(pen, 0, 87, LogicalWidth, 87);
            }

            using (var titleFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (var comboFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (var titleBrush = new SolidBrush(TextPrimary))
            using (var sfLeft = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            using (var sfRight = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
            {
                string leftText = string.Format(Strings.VisualSelectorHeaderFormat, _slotTitle, _gameName);
                g.DrawString(leftText, titleFont, titleBrush, new RectangleF(18, 43, 310, 44), sfLeft);

                string comboText = (_currentCombo.Count > 0)
                    ? InputAction.FormatComboDisplay(_currentCombo)
                    : Strings.VisualSelectorComboEmpty;
                Color comboColor = (_currentCombo.Count > 0) ? ActiveGreen : TextSecondary;

                using (var comboBrush = new SolidBrush(comboColor))
                {
                    string rightText = string.Format(Strings.VisualSelectorComboLabel, comboText);
                    g.DrawString(rightText, comboFont, comboBrush, new RectangleF(330, 43, LogicalWidth - 348, 44), sfRight);
                }
            }
        }

        private void DrawTabKeyboard(Graphics g)
        {
            using (var keyFont = new Font("Segoe UI", 8.8f, FontStyle.Bold))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                for (int r = 0; r < 6; r++)
                {
                    for (int c = 0; c < _keyboardRows[r].Count; c++)
                    {
                        var keyItem = _keyboardRows[r][c];
                        bool isFocused = (r == _kbRow && c == _kbCol);
                        bool inCombo = IsKeyInCurrentCombo(keyItem.Key);

                        Color fill = inCombo ? (isFocused ? Color.FromArgb(40, 85, 60) : ComboBg)
                                             : (isFocused ? FocusBg : KeyBg);
                        Color border = inCombo ? ActiveGreen
                                               : (isFocused ? BorderFocused : BorderIdle);
                        float borderWidth = (isFocused || inCombo) ? 1.8f : 1f;

                        using (var brush = new SolidBrush(fill))
                        using (var pen = new Pen(border, borderWidth))
                        using (var textBrush = new SolidBrush(inCombo ? ActiveGreen : (isFocused ? Color.White : TextPrimary)))
                        {
                            DrawRoundedRectangle(g, brush, pen, keyItem.Bounds, 3.5f);
                            g.DrawString(keyItem.Label, keyFont, textBrush, keyItem.Bounds, sf);
                        }
                    }
                }
            }

            // Dica e Detalhes da Tecla Focada no Rodapé da Aba
            DrawFocusedKeyLegend(g);
        }

        private void DrawFocusedKeyLegend(Graphics g)
        {
            if (_kbRow < 0 || _kbRow >= _keyboardRows.Length || _kbCol < 0 || _kbCol >= _keyboardRows[_kbRow].Count) return;
            var focused = _keyboardRows[_kbRow][_kbCol];
            bool inCombo = IsKeyInCurrentCombo(focused.Key);

            var infoRect = new RectangleF(18, 356, LogicalWidth - 36, 128);
            using (var cardBrush = new SolidBrush(CardBg))
            using (var borderPen = new Pen(BorderSubtle, 1f))
            using (var fontTitle = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (var fontDesc = new Font("Segoe UI", 9f))
            using (var brushTitle = new SolidBrush(TextPrimary))
            using (var brushDesc = new SolidBrush(TextSecondary))
            using (var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near })
            {
                DrawRoundedRectangle(g, cardBrush, borderPen, infoRect, 6f);

                string title = $"TECLA FOCADA: [ {focused.Label} ]  (VK: {focused.Key})";
                g.DrawString(title, fontTitle, brushTitle, new PointF(infoRect.X + 16, infoRect.Y + 12));

                string status = inCombo ? "● Presente no Combo Atual" : "○ Não incluída no Combo";
                Color statColor = inCombo ? ActiveGreen : TextSecondary;
                using (var statBrush = new SolidBrush(statColor))
                using (var statFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
                {
                    g.DrawString(status, statFont, statBrush, new PointF(infoRect.X + 16, infoRect.Y + 36));
                }

                string desc = "Pressione (A) para selecionar imediatamente esta tecla como mapeamento único.\n" +
                              "Pressione (X) para Adicionar / Remover do Combo Montado no topo.\n" +
                              "Dica: Segure (X) em várias teclas para montar combos como [ Espaço + F9 ] sem teclado físico!";
                g.DrawString(desc, fontDesc, brushDesc, new RectangleF(infoRect.X + 16, infoRect.Y + 62, infoRect.Width - 32, 58), sf);
            }
        }

        private void DrawTabMouse(Graphics g)
        {
            // Ilustração do Mouse à Esquerda (X = 35 a 230, Y = 114 a 450)
            DrawMouseIllustration(g, new RectangleF(35, 114, 195, 340));

            // Lista de Cards de Ações de Mouse à Direita
            using (var font = new Font("Segoe UI", 9.8f, FontStyle.Bold))
            using (var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            {
                for (int i = 0; i < _mouseItems.Length; i++)
                {
                    var item = _mouseItems[i];
                    bool isFocused = (i == _mouseIndex);
                    bool inCombo = item.Action != null && IsActionInCurrentCombo(item.Action);

                    Color fill = inCombo ? (isFocused ? Color.FromArgb(40, 85, 60) : ComboBg)
                                         : (isFocused ? FocusBg : CardBg);
                    Color border = inCombo ? ActiveGreen
                                           : (isFocused ? BorderFocused : BorderSubtle);
                    float borderWidth = (isFocused || inCombo) ? 1.8f : 1f;

                    using (var brush = new SolidBrush(fill))
                    using (var pen = new Pen(border, borderWidth))
                    using (var textBrush = new SolidBrush(inCombo ? ActiveGreen : (isFocused ? Color.White : TextPrimary)))
                    {
                        DrawRoundedRectangle(g, brush, pen, item.Bounds, 5f);
                        g.DrawString("   " + item.Title, font, textBrush, item.Bounds, sf);
                    }
                }
            }
        }

        private void DrawMouseIllustration(Graphics g, RectangleF bounds)
        {
            using (var bgBrush = new SolidBrush(CardBg))
            using (var borderPen = new Pen(BorderSubtle, 1f))
            {
                DrawRoundedRectangle(g, bgBrush, borderPen, bounds, 8f);
            }

            // Desenho do corpo do mouse centralizado dentro dos bounds
            float mw = 120f;
            float mh = 200f;
            float mx = bounds.X + (bounds.Width - mw) / 2f;
            float my = bounds.Y + (bounds.Height - mh) / 2f;

            var bodyRect = new RectangleF(mx, my, mw, mh);

            // Determina destaques da ilustração baseado no foco ou seleção
            bool highlightLeft = (_mouseIndex == 0) || (_mouseItems[0].Action != null && IsActionInCurrentCombo(_mouseItems[0].Action));
            bool highlightRight = (_mouseIndex == 1) || (_mouseItems[1].Action != null && IsActionInCurrentCombo(_mouseItems[1].Action));
            bool highlightMiddle = (_mouseIndex == 2 || _mouseIndex == 3 || _mouseIndex == 4);

            // Corpo principal
            using (var bodyBrush = new SolidBrush(Color.FromArgb(28, 31, 40)))
            using (var bodyPen = new Pen(BorderIdle, 2f))
            {
                DrawRoundedRectangle(g, bodyBrush, bodyPen, bodyRect, 45f);
            }

            // Botão Esquerdo (Metade superior esquerda)
            var leftBtnRect = new RectangleF(mx + 4, my + 4, (mw / 2f) - 6, 75f);
            using (var lBrush = new SolidBrush(highlightLeft ? ActiveGreen : Color.FromArgb(42, 46, 58)))
            using (var lPen = new Pen(highlightLeft ? Color.White : BorderSubtle, 1.5f))
            {
                DrawRoundedRectangle(g, lBrush, lPen, leftBtnRect, 18f);
            }

            // Botão Direito (Metade superior direita)
            var rightBtnRect = new RectangleF(mx + (mw / 2f) + 2, my + 4, (mw / 2f) - 6, 75f);
            using (var rBrush = new SolidBrush(highlightRight ? ActiveGreen : Color.FromArgb(42, 46, 58)))
            using (var rPen = new Pen(highlightRight ? Color.White : BorderSubtle, 1.5f))
            {
                DrawRoundedRectangle(g, rBrush, rPen, rightBtnRect, 18f);
            }

            // Scroll Wheel no Meio
            var wheelRect = new RectangleF(mx + (mw / 2f) - 6, my + 24, 12f, 36f);
            using (var wBrush = new SolidBrush(highlightMiddle ? AccentBlue : Color.FromArgb(160, 170, 185)))
            using (var wPen = new Pen(Color.White, 1.2f))
            {
                DrawRoundedRectangle(g, wBrush, wPen, wheelRect, 5f);
            }

            // Rótulo da Ilustração
            using (var font = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (var brush = new SolidBrush(TextSecondary))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                var labelRect = new RectangleF(bounds.X, my + mh + 12, bounds.Width, 24);
                g.DrawString("MOUSE DO SISTEMA", font, brush, labelRect, sf);
            }
        }

        private void DrawTabSystem(Graphics g)
        {
            string[] sectionTitles = new[]
            {
                Strings.VisualSelectorSectionAudio,
                Strings.VisualSelectorSectionWindows,
                Strings.VisualSelectorSectionCompanion
            };

            using (var titleFont = new Font("Segoe UI", 9.2f, FontStyle.Bold))
            using (var itemFont = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (var sfTitle = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var sfItem = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            {
                for (int c = 0; c < 3; c++)
                {
                    // Título da Seção
                    float colX = _sysColumns[c].Count > 0 ? _sysColumns[c][0].Bounds.X : 0;
                    float colW = _sysColumns[c].Count > 0 ? _sysColumns[c][0].Bounds.Width : 180;
                    var titleRect = new RectangleF(colX, 96, colW, 20);
                    using (var titleBrush = new SolidBrush(AccentBlue))
                    {
                        g.DrawString(sectionTitles[c], titleFont, titleBrush, titleRect, sfTitle);
                    }

                    // Itens da Coluna
                    for (int r = 0; r < _sysColumns[c].Count; r++)
                    {
                        var item = _sysColumns[c][r];
                        bool isFocused = (c == _sysCol && r == _sysRow);
                        bool inCombo = item.Action != null && IsActionInCurrentCombo(item.Action);

                        Color fill = inCombo ? (isFocused ? Color.FromArgb(40, 85, 60) : ComboBg)
                                             : (isFocused ? FocusBg : CardBg);
                        Color border = inCombo ? ActiveGreen
                                               : (isFocused ? BorderFocused : BorderSubtle);
                        float borderWidth = (isFocused || inCombo) ? 1.8f : 1f;

                        using (var brush = new SolidBrush(fill))
                        using (var pen = new Pen(border, borderWidth))
                        using (var textBrush = new SolidBrush(inCombo ? ActiveGreen : (isFocused ? Color.White : TextPrimary)))
                        {
                            DrawRoundedRectangle(g, brush, pen, item.Bounds, 5f);
                            g.DrawString(" " + item.Title, itemFont, textBrush, item.Bounds, sfItem);
                        }
                    }
                }
            }

            // Explicação na coluna 2 (Funções do Companion)
            DrawCompanionFunctionsInfo(g);
        }

        private void DrawCompanionFunctionsInfo(Graphics g)
        {
            var infoRect = new RectangleF(_sysColumns[2][0].Bounds.X, 240, _sysColumns[2][0].Bounds.Width, 220);
            using (var bgBrush = new SolidBrush(CardBg))
            using (var borderPen = new Pen(BorderSubtle, 1f))
            using (var fontTitle = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (var fontDesc = new Font("Segoe UI", 8.4f))
            using (var brushTitle = new SolidBrush(TextPrimary))
            using (var brushDesc = new SolidBrush(TextSecondary))
            using (var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near })
            {
                DrawRoundedRectangle(g, bgBrush, borderPen, infoRect, 6f);

                g.DrawString("SOBRE AS FUNÇÕES", fontTitle, brushTitle, new PointF(infoRect.X + 10, infoRect.Y + 10));

                string desc = "🎯 Centralizar Cursor:\n" +
                              "Move instantaneamente o cursor ao centro da tela do jogo.\n\n" +
                              "🔒 Toggle Confinar Cursor:\n" +
                              "Ativa ou libera o travamento do mouse dentro da janela do jogo (ClipCursor).";
                g.DrawString(desc, fontDesc, brushDesc, new RectangleF(infoRect.X + 10, infoRect.Y + 34, infoRect.Width - 20, infoRect.Height - 44), sf);
            }
        }

        private void DrawFooter(Graphics g)
        {
            using (var bg = new SolidBrush(HeaderBg))
            using (var pen = new Pen(BorderSubtle, 1f))
            {
                g.FillRectangle(bg, 0, 492, LogicalWidth, 48);
                g.DrawLine(pen, 0, 492, LogicalWidth, 492);
            }

            // 5 Botões Clicáveis
            DrawFooterButton(g, _btnSelectBounds, Strings.VisualSelectorBtnSelect, AccentBlue);
            DrawFooterButton(g, _btnToggleBounds, Strings.VisualSelectorBtnToggle, AccentBlue);
            DrawFooterButton(g, _btnClearBounds, Strings.VisualSelectorBtnClear, TextSecondary);
            DrawFooterButton(g, _btnConfirmBounds, Strings.VisualSelectorBtnConfirm, ActiveGreen);
            DrawFooterButton(g, _btnCancelBounds, Strings.VisualSelectorBtnCancel, TextSecondary);
        }

        private void DrawFooterButton(Graphics g, RectangleF bounds, string text, Color accent)
        {
            using (var bg = new SolidBrush(CardBg))
            using (var pen = new Pen(BorderSubtle, 1f))
            using (var font = new Font("Segoe UI", 8.8f, FontStyle.Bold))
            using (var brush = new SolidBrush(accent))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                DrawRoundedRectangle(g, bg, pen, bounds, 4f);
                g.DrawString(text, font, brush, bounds, sf);
            }
        }

        private static void DrawRoundedRectangle(Graphics g, Brush fillBrush, Pen borderPen, RectangleF rect, float radius)
        {
            using (var path = new GraphicsPath())
            {
                float d = radius * 2f;
                path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                path.CloseFigure();

                if (fillBrush != null) g.FillPath(fillBrush, path);
                if (borderPen != null) g.DrawPath(borderPen, path);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            PointF pt = ClientToLogical(e.Location);

            if (e.Button == MouseButtons.Right)
            {
                Cancel();
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                // Cliques nas Abas
                for (int i = 0; i < 3; i++)
                {
                    if (_tabBounds[i].Contains(pt))
                    {
                        SetTab(i);
                        return;
                    }
                }

                // Cliques no Rodapé
                if (_btnSelectBounds.Contains(pt))
                {
                    SelectFocusedItemAsSingle();
                    return;
                }
                if (_btnConfirmBounds.Contains(pt))
                {
                    ConfirmSelection();
                    return;
                }
                if (_btnToggleBounds.Contains(pt))
                {
                    ToggleFocusedItemInCombo();
                    return;
                }
                if (_btnClearBounds.Contains(pt))
                {
                    ClearCombo();
                    return;
                }
                if (_btnCancelBounds.Contains(pt))
                {
                    Cancel();
                    return;
                }

                // Cliques no Conteúdo da Aba Ativa
                if (_currentTab == 0)
                {
                    for (int r = 0; r < 6; r++)
                    {
                        for (int c = 0; c < _keyboardRows[r].Count; c++)
                        {
                            if (_keyboardRows[r][c].Bounds.Contains(pt))
                            {
                                _kbRow = r;
                                _kbCol = c;
                                if ((ModifierKeys & Keys.Control) == Keys.Control)
                                {
                                    ToggleFocusedItemInCombo();
                                }
                                else
                                {
                                    SelectFocusedItemAsSingle();
                                }
                                return;
                            }
                        }
                    }
                }
                else if (_currentTab == 1)
                {
                    for (int i = 0; i < _mouseItems.Length; i++)
                    {
                        if (_mouseItems[i].Bounds.Contains(pt))
                        {
                            _mouseIndex = i;
                            if ((ModifierKeys & Keys.Control) == Keys.Control)
                            {
                                ToggleFocusedItemInCombo();
                            }
                            else
                            {
                                SelectFocusedItemAsSingle();
                            }
                            return;
                        }
                    }
                }
                else if (_currentTab == 2)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        for (int r = 0; r < _sysColumns[c].Count; r++)
                        {
                            if (_sysColumns[c][r].Bounds.Contains(pt))
                            {
                                _sysCol = c;
                                _sysRow = r;
                                if ((ModifierKeys & Keys.Control) == Keys.Control)
                                {
                                    ToggleFocusedItemInCombo();
                                }
                                else
                                {
                                    SelectFocusedItemAsSingle();
                                }
                                return;
                            }
                        }
                    }
                }
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left)
            {
                ConfirmSelection();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            PointF pt = ClientToLogical(e.Location);

            if (_currentTab == 0)
            {
                for (int r = 0; r < 6; r++)
                {
                    for (int c = 0; c < _keyboardRows[r].Count; c++)
                    {
                        if (_keyboardRows[r][c].Bounds.Contains(pt))
                        {
                            if (_kbRow != r || _kbCol != c)
                            {
                                _kbRow = r;
                                _kbCol = c;
                                Invalidate();
                            }
                            return;
                        }
                    }
                }
            }
            else if (_currentTab == 1)
            {
                for (int i = 0; i < _mouseItems.Length; i++)
                {
                    if (_mouseItems[i].Bounds.Contains(pt))
                    {
                        if (_mouseIndex != i)
                        {
                            _mouseIndex = i;
                            Invalidate();
                        }
                        return;
                    }
                }
            }
            else if (_currentTab == 2)
            {
                for (int c = 0; c < 3; c++)
                {
                    for (int r = 0; r < _sysColumns[c].Count; r++)
                    {
                        if (_sysColumns[c][r].Bounds.Contains(pt))
                        {
                            if (_sysCol != c || _sysRow != r)
                            {
                                _sysCol = c;
                                _sysRow = r;
                                Invalidate();
                            }
                            return;
                        }
                    }
                }
            }
        }
    }
}
