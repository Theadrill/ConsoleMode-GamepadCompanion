using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.UI.Controls
{
    /// <summary>
    /// Tela de Escolha do Modo de Edição & Captura Física de Combos (Fase 3).
    /// Ocupa 100% da área de conteúdo (Dock = DockStyle.Fill) e oferece:
    /// 1. [🎙️ Capturar Input Físico] — Acumula teclas simultâneas, cliques de mouse ou scroll na ordem exata e aplica ao soltar.
    /// 2. [🖮 Escolher Botão / Botões] — Abre o seletor visual de 3 abas (Fase 4).
    /// 3. [🗑️ Limpar / Desativar Entrada] — Limpa o slot imediatamente.
    /// </summary>
    public sealed class InputBindingCaptureControl : UserControl
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
        private static readonly Color AlertRed = Color.FromArgb(220, 95, 95);
        private static readonly Color TextPrimary = Color.FromArgb(230, 232, 240);
        private static readonly Color TextSecondary = Color.FromArgb(160, 170, 185);

        private const float LogicalWidth = 620f;
        private const float LogicalHeight = 540f;

        private const int VK_LSHIFT = 0xA0;
        private const int VK_RSHIFT = 0xA1;
        private const int VK_LCONTROL = 0xA2;
        private const int VK_RCONTROL = 0xA3;
        private const int VK_LMENU = 0xA4;
        private const int VK_RMENU = 0xA5;

        // Low-Level Keyboard Hook (WH_KEYBOARD_LL) para sequestro temporário de atalhos do Windows (Win+R, Alt+Tab, etc.)
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private const uint LLKHF_INJECTED = 0x00000010;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private IntPtr _keyboardHookHandle = IntPtr.Zero;
        private readonly LowLevelKeyboardProc _keyboardHookDelegate;

        private ProfileEditorSlot _targetSlot;
        private string _slotTitle = string.Empty;
        private string _gameName = string.Empty;
        private readonly List<InputAction> _currentActions = new List<InputAction>();

        private int _focusedOptionIndex; // 0 = Captura Física, 1 = Escolher Botão, 2 = Limpar
        private readonly RectangleF[] _optionBounds = new RectangleF[3];

        private bool _isListening;
        private RectangleF _listeningBoxBounds;
        private RectangleF _cancelListeningBtnBounds;

        // Estado acumulador de Captura Física de Combos
        private readonly List<InputAction> _capturedCombo = new List<InputAction>();
        private readonly HashSet<VirtualKey> _currentlyHeldKeys = new HashSet<VirtualKey>();
        private readonly HashSet<MouseButton> _currentlyHeldMouseButtons = new HashSet<MouseButton>();

        // Debounce / Repetição do Gamepad
        private bool _lastUp;
        private bool _lastDown;
        private bool _lastA;
        private bool _lastB;
        private long _lastMoveTime;
        private int _repeatCount;
        private const long InitialRepeatDelayMs = 230;
        private const long RepeatIntervalMs = 80;

        public event Action<ProfileEditorSlot, List<InputAction>> BindingConfirmed;
        public event Action<ProfileEditorSlot, string, List<InputAction>> OpenVisualSelectorRequested;
        public event Action CancelRequested;

        public InputBindingCaptureControl()
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
            _keyboardHookDelegate = KeyboardHookCallback;
            BuildLogicalBounds();
        }

        public ProfileEditorSlot TargetSlot => _targetSlot;
        public string SlotTitle => _slotTitle;
        public int FocusedOptionIndex => _focusedOptionIndex;
        public bool IsListening => _isListening;
        public IReadOnlyList<InputAction> CapturedCombo => _capturedCombo;

        private void BuildLogicalBounds()
        {
            float boxW = 420f;
            float boxX = (LogicalWidth - boxW) / 2f;

            _optionBounds[0] = new RectangleF(boxX, 140f, boxW, 74f);
            _optionBounds[1] = new RectangleF(boxX, 232f, boxW, 74f);
            _optionBounds[2] = new RectangleF(boxX, 324f, boxW, 64f);

            _listeningBoxBounds = new RectangleF(70f, 130f, 480f, 290f);
            _cancelListeningBtnBounds = new RectangleF(230f, 366f, 160f, 36f);
        }

        public void OpenForSlot(ProfileEditorSlot slot, string slotTitle, string gameName, IEnumerable<InputAction> existingActions)
        {
            _targetSlot = slot;
            _slotTitle = slotTitle ?? slot.ToString();
            _gameName = string.IsNullOrWhiteSpace(gameName) ? Strings.DefaultGameName : gameName;

            _currentActions.Clear();
            if (existingActions != null)
            {
                foreach (var a in existingActions)
                {
                    if (a != null) _currentActions.Add(a.Clone());
                }
            }

            _focusedOptionIndex = 0;
            StopListening();
            ResetInputState();
            Invalidate();
        }

        public void ResetInputState()
        {
            _lastA = true;
            _lastB = true;
            _lastUp = false;
            _lastDown = false;
            _repeatCount = 0;
            _lastMoveTime = 0;
            Invalidate();
        }

        public void StartListening()
        {
            _isListening = true;
            _capturedCombo.Clear();
            _currentlyHeldKeys.Clear();
            _currentlyHeldMouseButtons.Clear();
            InstallKeyboardHook();
            if (IsHandleCreated)
            {
                Focus();
            }
            Invalidate();
        }

        public void StopListening()
        {
            UninstallKeyboardHook();
            _isListening = false;
            _capturedCombo.Clear();
            _currentlyHeldKeys.Clear();
            _currentlyHeldMouseButtons.Clear();
            Invalidate();
        }

        private void InstallKeyboardHook()
        {
            if (_keyboardHookHandle != IntPtr.Zero) return;
            try
            {
                using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
                using (var curModule = curProcess.MainModule)
                {
                    IntPtr hMod = curModule != null ? GetModuleHandle(curModule.ModuleName) : IntPtr.Zero;
                    _keyboardHookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardHookDelegate, hMod, 0);
                }
            }
            catch
            {
                _keyboardHookHandle = IntPtr.Zero;
            }
        }

        private void UninstallKeyboardHook()
        {
            if (_keyboardHookHandle == IntPtr.Zero) return;
            try
            {
                UnhookWindowsHookEx(_keyboardHookHandle);
            }
            catch
            {
            }
            finally
            {
                _keyboardHookHandle = IntPtr.Zero;
            }
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _isListening)
            {
                int msg = wParam.ToInt32();
                bool isKeyDown = (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN);
                bool isKeyUp = (msg == WM_KEYUP || msg == WM_SYSKEYUP);

                if (isKeyDown || isKeyUp)
                {
                    var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

                    // Ignora eventos injetados sinteticamente pelo próprio SendInput
                    if ((kb.flags & LLKHF_INJECTED) == 0)
                    {
                        VirtualKey vk = MapRawVkToVirtualKey((int)kb.vkCode);
                        if (vk != VirtualKey.None)
                        {
                            if (isKeyDown)
                            {
                                RegisterPhysicalVirtualKeyDown(vk);
                            }
                            else
                            {
                                RegisterPhysicalVirtualKeyUp(vk);
                            }

                            // Retorna 1 (não-zero) para engolir a tecla antes que o Windows processe Win+R, Alt+Tab, Menu Iniciar, etc.
                            return (IntPtr)1;
                        }
                    }
                }
            }

            return CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
        }

        private static VirtualKey MapRawVkToVirtualKey(int rawVk)
        {
            if (Enum.IsDefined(typeof(VirtualKey), (ushort)rawVk))
            {
                var vk = (VirtualKey)(ushort)rawVk;
                if (vk != VirtualKey.None) return vk;
            }

            return MapKeysToVirtualKey((Keys)rawVk);
        }

        public void ProcessGamepad(GamepadState state, long nowMs)
        {
            if (!state.IsConnected) return;

            bool up = state.IsPressed(GamepadButtons.DPadUp) || state.LeftThumbY > 16000;
            bool down = state.IsPressed(GamepadButtons.DPadDown) || state.LeftThumbY < -16000;
            bool btnA = state.IsPressed(GamepadButtons.A);
            bool btnB = state.IsPressed(GamepadButtons.B);

            // Se estiver aguardando captura física, B cancela a escuta e volta às 3 opções
            if (_isListening)
            {
                if (btnB && !_lastB)
                {
                    _lastB = btnB;
                    StopListening();
                    return;
                }
                _lastB = btnB;
                _lastA = btnA;
                _lastUp = up;
                _lastDown = down;
                return;
            }

            if (btnB && !_lastB)
            {
                _lastB = btnB;
                CancelRequested?.Invoke();
                return;
            }
            _lastB = btnB;

            if (btnA && !_lastA)
            {
                _lastA = btnA;
                ExecuteFocusedOption();
                return;
            }
            _lastA = btnA;

            if (up || down)
            {
                bool isInitial = (up && !_lastUp) || (down && !_lastDown);
                long delay = _repeatCount == 0 ? InitialRepeatDelayMs : RepeatIntervalMs;
                if (isInitial || (nowMs - _lastMoveTime >= delay))
                {
                    if (up && _focusedOptionIndex > 0)
                    {
                        _focusedOptionIndex--;
                        Invalidate();
                    }
                    else if (down && _focusedOptionIndex < 2)
                    {
                        _focusedOptionIndex++;
                        Invalidate();
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
        }

        public void ExecuteFocusedOption()
        {
            switch (_focusedOptionIndex)
            {
                case 0:
                    StartListening();
                    break;
                case 1:
                    OpenVisualSelectorRequested?.Invoke(_targetSlot, _slotTitle, CloneList(_currentActions));
                    break;
                case 2:
                    BindingConfirmed?.Invoke(_targetSlot, new List<InputAction>());
                    break;
            }
        }

        // =========================================================================
        // CAPTURA FÍSICA DE TECLAS, COMBOS E MOUSE
        // =========================================================================

        public bool HandlePhysicalKeyDown(Keys keyCode)
        {
            if (!_isListening) return false;

            VirtualKey vk = MapKeysToVirtualKey(keyCode);
            if (vk == VirtualKey.None) return false;

            return RegisterPhysicalVirtualKeyDown(vk);
        }

        public bool RegisterPhysicalVirtualKeyDown(VirtualKey vk)
        {
            if (!_isListening || vk == VirtualKey.None) return false;

            _currentlyHeldKeys.Add(vk);
            var action = InputAction.FromKey(vk);
            if (!_capturedCombo.Contains(action))
            {
                _capturedCombo.Add(action);
            }
            Invalidate();
            return true;
        }

        public bool HandlePhysicalKeyUp(Keys keyCode)
        {
            if (!_isListening) return false;

            VirtualKey vk = MapKeysToVirtualKey(keyCode);
            if (vk != VirtualKey.None)
            {
                // Se for Shift/Control/Menu genérico, remove ambos L e R se não estiverem mais pressionados
                if (keyCode == Keys.ShiftKey)
                {
                    if (!IsVkDown(VK_LSHIFT)) _currentlyHeldKeys.Remove(VirtualKey.LeftShift);
                    if (!IsVkDown(VK_RSHIFT)) _currentlyHeldKeys.Remove(VirtualKey.RightShift);
                }
                else if (keyCode == Keys.ControlKey)
                {
                    if (!IsVkDown(VK_LCONTROL)) _currentlyHeldKeys.Remove(VirtualKey.LeftControl);
                    if (!IsVkDown(VK_RCONTROL)) _currentlyHeldKeys.Remove(VirtualKey.RightControl);
                }
                else if (keyCode == Keys.Menu)
                {
                    if (!IsVkDown(VK_LMENU)) _currentlyHeldKeys.Remove(VirtualKey.LeftAlt);
                    if (!IsVkDown(VK_RMENU)) _currentlyHeldKeys.Remove(VirtualKey.RightAlt);
                }
                else
                {
                    _currentlyHeldKeys.Remove(vk);
                }
            }

            CheckAndFinalizePhysicalCombo();
            return true;
        }

        public bool RegisterPhysicalVirtualKeyUp(VirtualKey vk)
        {
            if (!_isListening || vk == VirtualKey.None) return false;
            _currentlyHeldKeys.Remove(vk);
            CheckAndFinalizePhysicalCombo();
            return true;
        }

        public bool RegisterPhysicalMouseDown(MouseButton button)
        {
            if (!_isListening) return false;

            _currentlyHeldMouseButtons.Add(button);
            var action = InputAction.FromMouseButton(button);
            if (!_capturedCombo.Contains(action))
            {
                _capturedCombo.Add(action);
            }
            Invalidate();
            return true;
        }

        public bool RegisterPhysicalMouseUp(MouseButton button)
        {
            if (!_isListening) return false;

            _currentlyHeldMouseButtons.Remove(button);
            CheckAndFinalizePhysicalCombo();
            return true;
        }

        public bool RegisterPhysicalMouseWheel(int delta)
        {
            if (!_isListening || delta == 0) return false;

            var action = InputAction.FromMouseWheel(delta > 0 ? 120 : -120);
            if (!_capturedCombo.Contains(action))
            {
                _capturedCombo.Add(action);
            }

            if (_currentlyHeldKeys.Count == 0 && _currentlyHeldMouseButtons.Count == 0)
            {
                FinalizeCapture();
            }
            else
            {
                Invalidate();
            }
            return true;
        }

        private void CheckAndFinalizePhysicalCombo()
        {
            if (!_isListening) return;

            if (_capturedCombo.Count > 0 &&
                _currentlyHeldKeys.Count == 0 &&
                _currentlyHeldMouseButtons.Count == 0)
            {
                FinalizeCapture();
            }
            else
            {
                Invalidate();
            }
        }

        private void FinalizeCapture()
        {
            UninstallKeyboardHook();
            var result = CloneList(_capturedCombo);
            _isListening = false;
            _capturedCombo.Clear();
            _currentlyHeldKeys.Clear();
            _currentlyHeldMouseButtons.Clear();
            Invalidate();

            BindingConfirmed?.Invoke(_targetSlot, result);
        }

        private static bool IsVkDown(int vk)
        {
            try
            {
                return (GetAsyncKeyState(vk) & 0x8000) != 0;
            }
            catch
            {
                return false;
            }
        }

        public static VirtualKey MapKeysToVirtualKey(Keys keyCode)
        {
            Keys clean = keyCode & Keys.KeyCode;

            switch (clean)
            {
                case Keys.ShiftKey:
                    if (IsVkDown(VK_RSHIFT) && !IsVkDown(VK_LSHIFT)) return VirtualKey.RightShift;
                    return VirtualKey.LeftShift;

                case Keys.ControlKey:
                    if (IsVkDown(VK_RCONTROL) && !IsVkDown(VK_LCONTROL)) return VirtualKey.RightControl;
                    return VirtualKey.LeftControl;

                case Keys.Menu:
                    if (IsVkDown(VK_RMENU) && !IsVkDown(VK_LMENU)) return VirtualKey.RightAlt;
                    return VirtualKey.LeftAlt;

                case Keys.LShiftKey: return VirtualKey.LeftShift;
                case Keys.RShiftKey: return VirtualKey.RightShift;
                case Keys.LControlKey: return VirtualKey.LeftControl;
                case Keys.RControlKey: return VirtualKey.RightControl;
                case Keys.LMenu: return VirtualKey.LeftAlt;
                case Keys.RMenu: return VirtualKey.RightAlt;

                case Keys.LWin: return VirtualKey.LeftWindows;
                case Keys.RWin: return VirtualKey.RightWindows;
                case Keys.Apps: return VirtualKey.Apps;

                case Keys.Back: return VirtualKey.Backspace;
                case Keys.Tab: return VirtualKey.Tab;
                case Keys.Return: return VirtualKey.Enter;
                case Keys.Escape: return VirtualKey.Escape;
                case Keys.Space: return VirtualKey.Space;
                case Keys.Capital: return VirtualKey.CapsLock;
                case Keys.NumLock: return VirtualKey.NumLock;
                case Keys.Scroll: return VirtualKey.ScrollLock;

                case Keys.Prior: return VirtualKey.PageUp;
                case Keys.Next: return VirtualKey.PageDown;
                case Keys.End: return VirtualKey.End;
                case Keys.Home: return VirtualKey.Home;
                case Keys.Left: return VirtualKey.Left;
                case Keys.Up: return VirtualKey.Up;
                case Keys.Right: return VirtualKey.Right;
                case Keys.Down: return VirtualKey.Down;
                case Keys.PrintScreen: return VirtualKey.PrintScreen;
                case Keys.Insert: return VirtualKey.Insert;
                case Keys.Delete: return VirtualKey.Delete;

                case Keys.Oemtilde: return VirtualKey.OemTilde;
                case Keys.OemMinus: return VirtualKey.OemMinus;
                case Keys.Oemplus: return VirtualKey.OemPlus;
                case Keys.OemOpenBrackets: return VirtualKey.OemOpenBrackets;
                case Keys.OemCloseBrackets: return VirtualKey.OemCloseBrackets;
                case Keys.OemPipe:
                case Keys.OemBackslash: return VirtualKey.OemPipe;
                case Keys.OemSemicolon: return VirtualKey.OemSemicolon;
                case Keys.OemQuotes: return VirtualKey.OemQuotes;
                case Keys.Oemcomma: return VirtualKey.OemComma;
                case Keys.OemPeriod: return VirtualKey.OemPeriod;
                case Keys.OemQuestion: return VirtualKey.OemQuestion;

                case Keys.VolumeMute: return VirtualKey.VolumeMute;
                case Keys.VolumeDown: return VirtualKey.VolumeDown;
                case Keys.VolumeUp: return VirtualKey.VolumeUp;
                case Keys.MediaNextTrack: return VirtualKey.MediaNextTrack;
                case Keys.MediaPreviousTrack: return VirtualKey.MediaPrevTrack;
                case Keys.MediaStop: return VirtualKey.MediaStop;
                case Keys.MediaPlayPause: return VirtualKey.MediaPlayPause;
            }

            int val = (int)clean;
            if (Enum.IsDefined(typeof(VirtualKey), (ushort)val))
            {
                return (VirtualKey)(ushort)val;
            }

            return VirtualKey.None;
        }

        private static List<InputAction> CloneList(IEnumerable<InputAction> list)
        {
            var res = new List<InputAction>();
            if (list == null) return res;
            foreach (var a in list)
            {
                if (a != null) res.Add(a.Clone());
            }
            return res;
        }

        // =========================================================================
        // RENDERIZAÇÃO GDI+
        // =========================================================================

        private void GetTransformParams(out float scale, out float offsetX, out float offsetY)
        {
            float w = Math.Max(1, ClientSize.Width);
            float h = Math.Max(1, ClientSize.Height);
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

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BgColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            GetTransformParams(out float scale, out float offsetX, out float offsetY);
            g.TranslateTransform(offsetX, offsetY);
            g.ScaleTransform(scale, scale);

            DrawHeader(g);
            DrawCurrentMappingBadge(g);

            if (_isListening)
            {
                DrawListeningModal(g);
            }
            else
            {
                DrawOptionsSection(g);
            }

            DrawFooter(g);
        }

        private void DrawHeader(Graphics g)
        {
            using (var bg = new SolidBrush(HeaderBg))
            using (var pen = new Pen(BorderSubtle, 1f))
            using (var font = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (var brush = new SolidBrush(TextPrimary))
            {
                g.FillRectangle(bg, 0, 0, LogicalWidth, 42);
                g.DrawLine(pen, 0, 42, LogicalWidth, 42);
                string title = string.Format(Strings.BindingHeaderTitle, _slotTitle, _gameName);
                g.DrawString(title, font, brush, 16, 11);
            }
        }

        private void DrawCurrentMappingBadge(Graphics g)
        {
            string currentText = InputAction.FormatComboDisplay(_currentActions);
            string fullText = string.Format(Strings.BindingCurrentMapping, currentText);

            var badgeRect = new RectangleF(100, 66, 420, 44);
            using (var bg = new SolidBrush(InputBg))
            using (var pen = new Pen(BorderSubtle, 1.5f))
            using (var font = new Font("Segoe UI", 11f, FontStyle.Bold))
            using (var brush = new SolidBrush(ActiveGreen))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.FillRectangle(bg, badgeRect);
                g.DrawRectangle(pen, badgeRect.X, badgeRect.Y, badgeRect.Width, badgeRect.Height);
                g.DrawString(fullText, font, brush, badgeRect, sf);
            }
        }

        private void DrawOptionsSection(Graphics g)
        {
            DrawOptionButton(
                g,
                _optionBounds[0],
                Strings.BindingOptionCaptureTitle,
                Strings.BindingOptionCaptureDesc,
                _focusedOptionIndex == 0,
                BorderFocused);

            DrawOptionButton(
                g,
                _optionBounds[1],
                Strings.BindingOptionVisualTitle,
                Strings.BindingOptionVisualDesc,
                _focusedOptionIndex == 1,
                ActiveGreen);

            DrawOptionButton(
                g,
                _optionBounds[2],
                Strings.BindingOptionClearTitle,
                Strings.BindingOptionClearDesc,
                _focusedOptionIndex == 2,
                AlertRed);

            var tipRect = new RectangleF(80, 412, 460, 56);
            using (var font = new Font("Segoe UI", 8.8f, FontStyle.Italic))
            using (var brush = new SolidBrush(TextSecondary))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(Strings.BindingCaptureTip, font, brush, tipRect, sf);
            }
        }

        private static void DrawOptionButton(
            Graphics g,
            RectangleF rect,
            string title,
            string subtitle,
            bool isFocused,
            Color accentColor)
        {
            using (var bg = new SolidBrush(isFocused ? FocusBg : CardBg))
            using (var pen = new Pen(isFocused ? BorderFocused : BorderSubtle, isFocused ? 2.5f : 1.2f))
            {
                g.FillRectangle(bg, rect);
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
            }

            if (isFocused)
            {
                using (var barBrush = new SolidBrush(accentColor))
                {
                    g.FillRectangle(barBrush, rect.X, rect.Y, 6f, rect.Height);
                }
            }

            using (var titleFont = new Font("Segoe UI", 11f, FontStyle.Bold))
            using (var subFont = new Font("Segoe UI", 8.8f))
            using (var titleBrush = new SolidBrush(isFocused ? Color.White : TextPrimary))
            using (var subBrush = new SolidBrush(isFocused ? BorderFocused : TextSecondary))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                var topRect = new RectangleF(rect.X, rect.Y + 8, rect.Width, 28);
                var botRect = new RectangleF(rect.X, rect.Y + 36, rect.Width, 24);
                g.DrawString(title, titleFont, titleBrush, topRect, sf);
                g.DrawString(subtitle, subFont, subBrush, botRect, sf);
            }
        }

        private void DrawListeningModal(Graphics g)
        {
            using (var bg = new SolidBrush(CardBg))
            using (var pen = new Pen(AlertAmber, 2.5f))
            {
                g.FillRectangle(bg, _listeningBoxBounds);
                g.DrawRectangle(pen, _listeningBoxBounds.X, _listeningBoxBounds.Y, _listeningBoxBounds.Width, _listeningBoxBounds.Height);
            }

            using (var titleFont = new Font("Segoe UI", 12.5f, FontStyle.Bold))
            using (var promptFont = new Font("Segoe UI", 9.2f))
            using (var titleBrush = new SolidBrush(AlertAmber))
            using (var promptBrush = new SolidBrush(TextPrimary))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(
                    Strings.BindingListeningTitle,
                    titleFont,
                    titleBrush,
                    new RectangleF(_listeningBoxBounds.X, _listeningBoxBounds.Y + 18, _listeningBoxBounds.Width, 32),
                    sf);

                g.DrawString(
                    Strings.BindingListeningPrompt,
                    promptFont,
                    promptBrush,
                    new RectangleF(_listeningBoxBounds.X + 20, _listeningBoxBounds.Y + 56, _listeningBoxBounds.Width - 40, 48),
                    sf);
            }

            // Caixa de exibição em tempo real do combo sendo segurado
            var comboBox = new RectangleF(_listeningBoxBounds.X + 36, _listeningBoxBounds.Y + 120, _listeningBoxBounds.Width - 72, 64);
            bool hasCaptured = _capturedCombo.Count > 0;
            string comboDisplay = hasCaptured
                ? "[ " + InputAction.FormatComboDisplay(_capturedCombo) + " ]"
                : Strings.BindingListeningWaiting;

            using (var boxBg = new SolidBrush(InputBg))
            using (var boxPen = new Pen(hasCaptured ? ActiveGreen : BorderFocused, 2f))
            using (var comboFont = new Font("Segoe UI", 13f, FontStyle.Bold))
            using (var comboBrush = new SolidBrush(hasCaptured ? ActiveGreen : TextSecondary))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.FillRectangle(boxBg, comboBox);
                g.DrawRectangle(boxPen, comboBox.X, comboBox.Y, comboBox.Width, comboBox.Height);
                g.DrawString(comboDisplay, comboFont, comboBrush, comboBox, sf);
            }

            if (hasCaptured)
            {
                using (var hintFont = new Font("Segoe UI", 8.8f, FontStyle.Bold))
                using (var hintBrush = new SolidBrush(ActiveGreen))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(
                        Strings.BindingListeningReleaseHint,
                        hintFont,
                        hintBrush,
                        new RectangleF(_listeningBoxBounds.X, _listeningBoxBounds.Y + 194, _listeningBoxBounds.Width, 26),
                        sf);
                }
            }

            // Botão Cancelar Escuta
            using (var btnBg = new SolidBrush(FocusBg))
            using (var btnPen = new Pen(BorderFocused, 1.5f))
            using (var btnFont = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (var btnBrush = new SolidBrush(TextPrimary))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.FillRectangle(btnBg, _cancelListeningBtnBounds);
                g.DrawRectangle(btnPen, _cancelListeningBtnBounds.X, _cancelListeningBtnBounds.Y, _cancelListeningBtnBounds.Width, _cancelListeningBtnBounds.Height);
                g.DrawString(Strings.BindingFooterListening, btnFont, btnBrush, _cancelListeningBtnBounds, sf);
            }
        }

        private void DrawFooter(Graphics g)
        {
            using (var bg = new SolidBrush(HeaderBg))
            using (var pen = new Pen(BorderSubtle, 1f))
            using (var font = new Font("Segoe UI", 8.8f, FontStyle.Bold))
            using (var brush = new SolidBrush(TextSecondary))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.FillRectangle(bg, 0, 500, LogicalWidth, 40);
                g.DrawLine(pen, 0, 500, LogicalWidth, 500);
                string footer = _isListening ? Strings.BindingFooterListening : Strings.BindingFooterChoice;
                g.DrawString(footer, font, brush, new RectangleF(0, 500, LogicalWidth, 40), sf);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isListening) return;

            PointF pt = ClientToLogical(e.Location);
            for (int i = 0; i < _optionBounds.Length; i++)
            {
                if (_optionBounds[i].Contains(pt) && _focusedOptionIndex != i)
                {
                    _focusedOptionIndex = i;
                    Invalidate();
                    break;
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            PointF pt = ClientToLogical(e.Location);

            if (_isListening)
            {
                if (e.Button == MouseButtons.Left && _cancelListeningBtnBounds.Contains(pt))
                {
                    StopListening();
                    return;
                }

                if (e.Button == MouseButtons.Left) RegisterPhysicalMouseDown(MouseButton.Left);
                else if (e.Button == MouseButtons.Right) RegisterPhysicalMouseDown(MouseButton.Right);
                else if (e.Button == MouseButtons.Middle) RegisterPhysicalMouseDown(MouseButton.Middle);
                return;
            }

            if (e.Button == MouseButtons.Right)
            {
                CancelRequested?.Invoke();
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                for (int i = 0; i < _optionBounds.Length; i++)
                {
                    if (_optionBounds[i].Contains(pt))
                    {
                        _focusedOptionIndex = i;
                        ExecuteFocusedOption();
                        return;
                    }
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!_isListening) return;

            if (e.Button == MouseButtons.Left) RegisterPhysicalMouseUp(MouseButton.Left);
            else if (e.Button == MouseButtons.Right) RegisterPhysicalMouseUp(MouseButton.Right);
            else if (e.Button == MouseButtons.Middle) RegisterPhysicalMouseUp(MouseButton.Middle);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!Visible && _isListening)
            {
                StopListening();
            }
        }

        protected override void Dispose(bool disposing)
        {
            UninstallKeyboardHook();
            base.Dispose(disposing);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_isListening && e.Delta != 0)
            {
                RegisterPhysicalMouseWheel(e.Delta);
            }
        }
    }
}
