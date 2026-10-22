using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.UI.Controls
{
    /// <summary>
    /// Identifica cada slot ou controle interativo navegável na tela do Editor Visual de Perfil do Jogo.
    /// </summary>
    public enum ProfileEditorSlot
    {
        PresetSelector,
        MouseSensitivity,
        DeadzoneSlider,
        DeadzoneGlobalToggle,
        TriggerSlider,
        TriggerGlobalToggle,

        // Topo Esquerda / Direita
        ButtonLT,
        ButtonRT,
        ButtonLB,
        ButtonRB,
        ButtonBack,
        ButtonStart,

        // Card Stick Esquerdo
        LeftStickMode,
        LeftStickUp,
        LeftStickDown,
        LeftStickLeft,
        LeftStickRight,
        LeftStickAdditional,
        LeftStickClick,

        // Card D-Pad
        DPadUp,
        DPadDown,
        DPadLeft,
        DPadRight,

        // Card Botões Faciais
        ButtonY,
        ButtonB,
        ButtonX,
        ButtonA,

        // Card Stick Direito
        RightStickMode,
        RightStickUp,
        RightStickDown,
        RightStickLeft,
        RightStickRight,
        RightStickAdditional,
        RightStickClick,

        // Botões Traseiros (Handheld / Steam Deck)
        ButtonL4,
        ButtonL5,
        ButtonR5,
        ButtonR4,

        // Trackpads Handheld (Steam Deck)
        LeftTrackpad,
        RightTrackpad,

        // Sub-slots para Edição de Ações do Trackpad
        TrackpadTouch,
        TrackpadPress,
        TrackpadDpadUp,
        TrackpadDpadDown,
        TrackpadDpadLeft,
        TrackpadDpadRight,

        // Ações de Rodapé
        FooterSave,
        FooterBack
    }

    /// <summary>
    /// Tela do Editor Visual de Perfil do Jogo estilo Steam Input (Fase 2).
    /// Ocupa 100% da área de conteúdo (Dock = DockStyle.Fill) com renderização GDI+ de alto contraste,
    /// sliders de Sensibilidade, Deadzone (Global/Individual) e Gatilhos (Global/Individual),
    /// seletor integrado de Presets/Copiar Jogo, diagrama central do controle e cards interativos.
    /// </summary>
    public sealed class GameProfileEditorControl : UserControl
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

        private GameEntry _game;
        private GameProfile _workingProfile = GameProfilePresets.CreateWowClassic();
        private AppSettings _settings = new AppSettings();
        private readonly List<GameEntry> _otherGames = new List<GameEntry>();

        private ProfileEditorSlot _focusedSlot = ProfileEditorSlot.MouseSensitivity;
        private readonly Dictionary<ProfileEditorSlot, RectangleF> _slotBounds = new Dictionary<ProfileEditorSlot, RectangleF>();

        // Menu Overlay de Presets / Copiar Jogo
        private bool _isPresetMenuOpen;
        private int _presetMenuFocusedIndex;
        private readonly List<PresetMenuItem> _presetMenuItems = new List<PresetMenuItem>();
        private readonly List<RectangleF> _presetMenuItemBounds = new List<RectangleF>();
        private RectangleF _presetMenuBoxBounds;

        // Arraste de Sliders com Mouse
        private ProfileEditorSlot? _draggingSlider;

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

        public event Action<GameEntry, GameProfile> SaveRequested;
        public event Action CancelRequested;
        public event Action<ProfileEditorSlot, string, List<InputAction>> SlotEditRequested;
        public event Action<bool, TrackpadConfig> TrackpadEditRequested;

        private sealed class PresetMenuItem
        {
            public string Title { get; set; }
            public string Subtitle { get; set; }
            public bool IsPreset { get; set; }
            public string PresetName { get; set; }
            public GameEntry SourceGame { get; set; }
        }

        public GameProfileEditorControl()
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

        public GameEntry CurrentGame => _game;
        public GameProfile WorkingProfile => _workingProfile;
        private ControllerVisualMode _visualMode = ControllerVisualMode.StandardGamepad;
        public ControllerVisualMode VisualMode
        {
            get => _visualMode;
            set
            {
                if (_visualMode == value) return;
                _visualMode = value;
                _slotBounds.Clear();
                EnsureLogicalBoundsPopulated();
                Invalidate();
            }
        }

        public ProfileEditorSlot FocusedSlot => _focusedSlot;
        public bool IsPresetMenuOpen => _isPresetMenuOpen;
        public int PresetMenuFocusedIndex => _presetMenuFocusedIndex;
        public int PresetMenuCount => _presetMenuItems.Count;

        public void LoadGameProfile(GameEntry game, AppSettings settings, IEnumerable<GameEntry> allGames = null)
        {
            _game = game ?? new GameEntry { Name = Strings.DefaultGameName };
            _settings = settings ?? new AppSettings();
            _workingProfile = (_game.Profile != null)
                ? _game.Profile.Clone()
                : GameProfilePresets.CreateWowClassic();

            SyncClickActionsToMappings(_workingProfile.ActivePage);
            VisualMode = Hardware.ControllerVisualModeResolver.Resolve(_settings.SelectedSlot);
            _slotBounds.Clear();
            EnsureLogicalBoundsPopulated();

            _otherGames.Clear();
            if (allGames != null)
            {
                foreach (var g in allGames)
                {
                    if (g != null && !string.Equals(g.Id, _game.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        _otherGames.Add(g);
                    }
                }
            }

            RebuildPresetMenuItems();
            _isPresetMenuOpen = false;
            _presetMenuFocusedIndex = 0;
            _focusedSlot = ProfileEditorSlot.MouseSensitivity;
            ResetInputState();
            Invalidate();
        }

        public void SetFocusedSlot(ProfileEditorSlot slot)
        {
            _focusedSlot = slot;
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
            _draggingSlider = null;
            Invalidate();
        }

        private void RebuildPresetMenuItems()
        {
            _presetMenuItems.Clear();
            _presetMenuItems.Add(new PresetMenuItem
            {
                Title = Strings.PresetWowClassicName,
                Subtitle = "WASD + F9 Smart Mouse Look + Espaço/F9",
                IsPreset = true,
                PresetName = GameProfilePresets.PresetWowClassic
            });
            _presetMenuItems.Add(new PresetMenuItem
            {
                Title = Strings.PresetRpgName,
                Subtitle = "Stick Direito segura Clique Direito ao girar câmera",
                IsPreset = true,
                PresetName = GameProfilePresets.PresetRpgRightClickCamera
            });
            _presetMenuItems.Add(new PresetMenuItem
            {
                Title = Strings.PresetStandardPcName,
                Subtitle = "WASD + Mouse Livre + RT Ataque / LT Mira",
                IsPreset = true,
                PresetName = GameProfilePresets.PresetStandardPc
            });
            _presetMenuItems.Add(new PresetMenuItem
            {
                Title = Strings.DesktopModeTitle,
                Subtitle = "Mouse no Analógico Esquerdo + Cliques RT/LT + Atalhos Windows",
                IsPreset = true,
                PresetName = GameProfilePresets.PresetDesktopDefault
            });

            foreach (var other in _otherGames)
            {
                if (other.Profile != null)
                {
                    _presetMenuItems.Add(new PresetMenuItem
                    {
                        Title = string.Format(Strings.ProfileMenuCopyFromFormat, other.Name),
                        Subtitle = other.MainExecutable ?? string.Empty,
                        IsPreset = false,
                        SourceGame = other
                    });
                }
            }
        }

        public void ApplyPresetByName(string presetName)
        {
            var preset = GameProfilePresets.CreatePreset(presetName);
            _workingProfile = preset;
            SyncClickActionsToMappings(_workingProfile.ActivePage);
            _visualMode = Hardware.ControllerVisualModeResolver.Resolve(_settings.SelectedSlot);
            Invalidate();
        }

        public void CopyProfileFromGame(GameEntry sourceGame)
        {
            if (sourceGame?.Profile == null) return;
            _workingProfile = sourceGame.Profile.Clone();
            SyncClickActionsToMappings(_workingProfile.ActivePage);
            _visualMode = Hardware.ControllerVisualModeResolver.Resolve(_settings.SelectedSlot);
            Invalidate();
        }

        private static void SyncClickActionsToMappings(ProfilePage page)
        {
            if (page == null) return;

            if (page.LeftStick != null)
            {
                var l3Map = page.GetActions(ControllerButton.LeftStickClick);
                if (l3Map.Count > 0 && (page.LeftStick.ClickActions == null || page.LeftStick.ClickActions.Count == 0))
                {
                    page.LeftStick.ClickActions = CloneActionList(l3Map);
                }
                else if (page.LeftStick.ClickActions != null && page.LeftStick.ClickActions.Count > 0)
                {
                    page.SetActions(ControllerButton.LeftStickClick, page.LeftStick.ClickActions);
                }
            }

            if (page.RightStick != null)
            {
                var r3Map = page.GetActions(ControllerButton.RightStickClick);
                if (r3Map.Count > 0 && (page.RightStick.ClickActions == null || page.RightStick.ClickActions.Count == 0))
                {
                    page.RightStick.ClickActions = CloneActionList(r3Map);
                }
                else if (page.RightStick.ClickActions != null && page.RightStick.ClickActions.Count > 0)
                {
                    page.SetActions(ControllerButton.RightStickClick, page.RightStick.ClickActions);
                }
            }
        }

        private static List<InputAction> CloneActionList(IEnumerable<InputAction> list)
        {
            var res = new List<InputAction>();
            if (list == null) return res;
            foreach (var item in list)
            {
                if (item != null) res.Add(item.Clone());
            }
            return res;
        }

        public List<InputAction> GetSlotActions(ProfileEditorSlot slot)
        {
            var page = _workingProfile.ActivePage;
            switch (slot)
            {
                case ProfileEditorSlot.ButtonLT: return CloneActionList(page.GetActions(ControllerButton.LeftTrigger));
                case ProfileEditorSlot.ButtonRT: return CloneActionList(page.GetActions(ControllerButton.RightTrigger));
                case ProfileEditorSlot.ButtonLB: return CloneActionList(page.GetActions(ControllerButton.LeftShoulder));
                case ProfileEditorSlot.ButtonRB: return CloneActionList(page.GetActions(ControllerButton.RightShoulder));
                case ProfileEditorSlot.ButtonBack: return CloneActionList(page.GetActions(ControllerButton.Back));
                case ProfileEditorSlot.ButtonStart: return CloneActionList(page.GetActions(ControllerButton.Start));
                case ProfileEditorSlot.ButtonY: return CloneActionList(page.GetActions(ControllerButton.Y));
                case ProfileEditorSlot.ButtonB: return CloneActionList(page.GetActions(ControllerButton.B));
                case ProfileEditorSlot.ButtonX: return CloneActionList(page.GetActions(ControllerButton.X));
                case ProfileEditorSlot.ButtonA: return CloneActionList(page.GetActions(ControllerButton.A));
                case ProfileEditorSlot.DPadUp: return CloneActionList(page.GetActions(ControllerButton.DPadUp));
                case ProfileEditorSlot.DPadDown: return CloneActionList(page.GetActions(ControllerButton.DPadDown));
                case ProfileEditorSlot.DPadLeft: return CloneActionList(page.GetActions(ControllerButton.DPadLeft));
                case ProfileEditorSlot.DPadRight: return CloneActionList(page.GetActions(ControllerButton.DPadRight));
                case ProfileEditorSlot.LeftStickUp: return CloneActionList(page.LeftStick.Up);
                case ProfileEditorSlot.LeftStickDown: return CloneActionList(page.LeftStick.Down);
                case ProfileEditorSlot.LeftStickLeft: return CloneActionList(page.LeftStick.Left);
                case ProfileEditorSlot.LeftStickRight: return CloneActionList(page.LeftStick.Right);
                case ProfileEditorSlot.LeftStickAdditional: return CloneActionList(page.LeftStick.AdditionalActions);
                case ProfileEditorSlot.LeftStickClick: return CloneActionList(page.GetActions(ControllerButton.LeftStickClick));
                case ProfileEditorSlot.RightStickUp: return CloneActionList(page.RightStick.Up);
                case ProfileEditorSlot.RightStickDown: return CloneActionList(page.RightStick.Down);
                case ProfileEditorSlot.RightStickLeft: return CloneActionList(page.RightStick.Left);
                case ProfileEditorSlot.RightStickRight: return CloneActionList(page.RightStick.Right);
                case ProfileEditorSlot.RightStickAdditional: return CloneActionList(page.RightStick.AdditionalActions);
                case ProfileEditorSlot.RightStickClick: return CloneActionList(page.GetActions(ControllerButton.RightStickClick));
                case ProfileEditorSlot.ButtonL4: return CloneActionList(page.GetActions(ControllerButton.L4));
                case ProfileEditorSlot.ButtonL5: return CloneActionList(page.GetActions(ControllerButton.L5));
                case ProfileEditorSlot.ButtonR5: return CloneActionList(page.GetActions(ControllerButton.R5));
                case ProfileEditorSlot.ButtonR4: return CloneActionList(page.GetActions(ControllerButton.R4));
                default: return new List<InputAction>();
            }
        }

        public void SetSlotActions(ProfileEditorSlot slot, IEnumerable<InputAction> actions)
        {
            var page = _workingProfile.ActivePage;
            var cloned = CloneActionList(actions);
            switch (slot)
            {
                case ProfileEditorSlot.ButtonLT: page.SetActions(ControllerButton.LeftTrigger, cloned); break;
                case ProfileEditorSlot.ButtonRT: page.SetActions(ControllerButton.RightTrigger, cloned); break;
                case ProfileEditorSlot.ButtonLB: page.SetActions(ControllerButton.LeftShoulder, cloned); break;
                case ProfileEditorSlot.ButtonRB: page.SetActions(ControllerButton.RightShoulder, cloned); break;
                case ProfileEditorSlot.ButtonBack: page.SetActions(ControllerButton.Back, cloned); break;
                case ProfileEditorSlot.ButtonStart: page.SetActions(ControllerButton.Start, cloned); break;
                case ProfileEditorSlot.ButtonY: page.SetActions(ControllerButton.Y, cloned); break;
                case ProfileEditorSlot.ButtonB: page.SetActions(ControllerButton.B, cloned); break;
                case ProfileEditorSlot.ButtonX: page.SetActions(ControllerButton.X, cloned); break;
                case ProfileEditorSlot.ButtonA: page.SetActions(ControllerButton.A, cloned); break;
                case ProfileEditorSlot.DPadUp: page.SetActions(ControllerButton.DPadUp, cloned); break;
                case ProfileEditorSlot.DPadDown: page.SetActions(ControllerButton.DPadDown, cloned); break;
                case ProfileEditorSlot.DPadLeft: page.SetActions(ControllerButton.DPadLeft, cloned); break;
                case ProfileEditorSlot.DPadRight: page.SetActions(ControllerButton.DPadRight, cloned); break;
                case ProfileEditorSlot.LeftStickUp: page.LeftStick.Up = cloned; break;
                case ProfileEditorSlot.LeftStickDown: page.LeftStick.Down = cloned; break;
                case ProfileEditorSlot.LeftStickLeft: page.LeftStick.Left = cloned; break;
                case ProfileEditorSlot.LeftStickRight: page.LeftStick.Right = cloned; break;
                case ProfileEditorSlot.LeftStickAdditional: page.LeftStick.AdditionalActions = cloned; break;
                case ProfileEditorSlot.LeftStickClick:
                    page.LeftStick.ClickActions = CloneActionList(cloned);
                    page.SetActions(ControllerButton.LeftStickClick, cloned);
                    break;
                case ProfileEditorSlot.RightStickUp: page.RightStick.Up = cloned; break;
                case ProfileEditorSlot.RightStickDown: page.RightStick.Down = cloned; break;
                case ProfileEditorSlot.RightStickLeft: page.RightStick.Left = cloned; break;
                case ProfileEditorSlot.RightStickRight: page.RightStick.Right = cloned; break;
                case ProfileEditorSlot.RightStickAdditional: page.RightStick.AdditionalActions = cloned; break;
                case ProfileEditorSlot.RightStickClick:
                    page.RightStick.ClickActions = CloneActionList(cloned);
                    page.SetActions(ControllerButton.RightStickClick, cloned);
                    break;
                case ProfileEditorSlot.ButtonL4: page.SetActions(ControllerButton.L4, cloned); break;
                case ProfileEditorSlot.ButtonL5: page.SetActions(ControllerButton.L5, cloned); break;
                case ProfileEditorSlot.ButtonR5: page.SetActions(ControllerButton.R5, cloned); break;
                case ProfileEditorSlot.ButtonR4: page.SetActions(ControllerButton.R4, cloned); break;
            }
            Invalidate();
        }

        public void ClearSlot(ProfileEditorSlot slot)
        {
            SetSlotActions(slot, Array.Empty<InputAction>());
        }

        public static string GetSlotDisplayTitle(ProfileEditorSlot slot)
        {
            switch (slot)
            {
                case ProfileEditorSlot.ButtonLT: return "GATILHO ESQUERDO (LT)";
                case ProfileEditorSlot.ButtonRT: return "GATILHO DIREITO (RT)";
                case ProfileEditorSlot.ButtonLB: return "BUMPER ESQUERDO (LB)";
                case ProfileEditorSlot.ButtonRB: return "BUMPER DIREITO (RB)";
                case ProfileEditorSlot.ButtonBack: return "SELECT / VIEW";
                case ProfileEditorSlot.ButtonStart: return "START / MENU";
                case ProfileEditorSlot.ButtonY: return "BOTÃO (Y)";
                case ProfileEditorSlot.ButtonB: return "BOTÃO (B)";
                case ProfileEditorSlot.ButtonX: return "BOTÃO (X)";
                case ProfileEditorSlot.ButtonA: return "BOTÃO (A)";
                case ProfileEditorSlot.DPadUp: return "D-PAD CIMA";
                case ProfileEditorSlot.DPadDown: return "D-PAD BAIXO";
                case ProfileEditorSlot.DPadLeft: return "D-PAD ESQUERDA";
                case ProfileEditorSlot.DPadRight: return "D-PAD DIREITA";
                case ProfileEditorSlot.LeftStickUp: return "STICK ESQUERDO (CIMA)";
                case ProfileEditorSlot.LeftStickDown: return "STICK ESQUERDO (BAIXO)";
                case ProfileEditorSlot.LeftStickLeft: return "STICK ESQUERDO (ESQUERDA)";
                case ProfileEditorSlot.LeftStickRight: return "STICK ESQUERDO (DIREITA)";
                case ProfileEditorSlot.LeftStickAdditional: return "STICK ESQUERDO (COMANDO ADICIONAL)";
                case ProfileEditorSlot.LeftStickClick: return "CLIQUE DO STICK ESQUERDO (L3)";
                case ProfileEditorSlot.RightStickUp: return "STICK DIREITO (CIMA)";
                case ProfileEditorSlot.RightStickDown: return "STICK DIREITO (BAIXO)";
                case ProfileEditorSlot.RightStickLeft: return "STICK DIREITO (ESQUERDA)";
                case ProfileEditorSlot.RightStickRight: return "STICK DIREITO (DIREITA)";
                case ProfileEditorSlot.RightStickAdditional: return "STICK DIREITO (COMANDO ADICIONAL)";
                case ProfileEditorSlot.RightStickClick: return "CLIQUE DO STICK DIREITO (R3)";
                case ProfileEditorSlot.ButtonL4: return "BOTÃO TRASEIRO (L4)";
                case ProfileEditorSlot.ButtonL5: return "BOTÃO TRASEIRO (L5)";
                case ProfileEditorSlot.ButtonR5: return "BOTÃO TRASEIRO (R5)";
                case ProfileEditorSlot.ButtonR4: return "BOTÃO TRASEIRO (R4)";
                case ProfileEditorSlot.LeftTrackpad: return Strings.ProfileCardLeftTrackpad;
                case ProfileEditorSlot.RightTrackpad: return Strings.ProfileCardRightTrackpad;
                case ProfileEditorSlot.TrackpadTouch: return "TRACKPAD - TOQUE LEVE (TOUCH)";
                case ProfileEditorSlot.TrackpadPress: return "TRACKPAD - CLIQUE MECÂNICO (PRESS)";
                case ProfileEditorSlot.TrackpadDpadUp: return "TRACKPAD D-PAD (CIMA)";
                case ProfileEditorSlot.TrackpadDpadDown: return "TRACKPAD D-PAD (BAIXO)";
                case ProfileEditorSlot.TrackpadDpadLeft: return "TRACKPAD D-PAD (ESQUERDA)";
                case ProfileEditorSlot.TrackpadDpadRight: return "TRACKPAD D-PAD (DIREITA)";
                default: return slot.ToString();
            }
        }

        public static bool IsActionSlot(ProfileEditorSlot slot)
        {
            switch (slot)
            {
                case ProfileEditorSlot.ButtonLT:
                case ProfileEditorSlot.ButtonRT:
                case ProfileEditorSlot.ButtonLB:
                case ProfileEditorSlot.ButtonRB:
                case ProfileEditorSlot.ButtonBack:
                case ProfileEditorSlot.ButtonStart:
                case ProfileEditorSlot.ButtonY:
                case ProfileEditorSlot.ButtonB:
                case ProfileEditorSlot.ButtonX:
                case ProfileEditorSlot.ButtonA:
                case ProfileEditorSlot.DPadUp:
                case ProfileEditorSlot.DPadDown:
                case ProfileEditorSlot.DPadLeft:
                case ProfileEditorSlot.DPadRight:
                case ProfileEditorSlot.LeftStickUp:
                case ProfileEditorSlot.LeftStickDown:
                case ProfileEditorSlot.LeftStickLeft:
                case ProfileEditorSlot.LeftStickRight:
                case ProfileEditorSlot.LeftStickAdditional:
                case ProfileEditorSlot.LeftStickClick:
                case ProfileEditorSlot.RightStickUp:
                case ProfileEditorSlot.RightStickDown:
                case ProfileEditorSlot.RightStickLeft:
                case ProfileEditorSlot.RightStickRight:
                case ProfileEditorSlot.RightStickAdditional:
                case ProfileEditorSlot.RightStickClick:
                case ProfileEditorSlot.ButtonL4:
                case ProfileEditorSlot.ButtonL5:
                case ProfileEditorSlot.ButtonR5:
                case ProfileEditorSlot.ButtonR4:
                case ProfileEditorSlot.TrackpadTouch:
                case ProfileEditorSlot.TrackpadPress:
                case ProfileEditorSlot.TrackpadDpadUp:
                case ProfileEditorSlot.TrackpadDpadDown:
                case ProfileEditorSlot.TrackpadDpadLeft:
                case ProfileEditorSlot.TrackpadDpadRight:
                    return true;
                default:
                    return false;
            }
        }

        private bool IsSlotVisible(ProfileEditorSlot slot)
        {
            var page = _workingProfile.ActivePage;
            if (slot == ProfileEditorSlot.LeftStickUp ||
                slot == ProfileEditorSlot.LeftStickDown ||
                slot == ProfileEditorSlot.LeftStickLeft ||
                slot == ProfileEditorSlot.LeftStickRight)
            {
                return page.LeftStick.Mode == StickMode.DirectionalKeys;
            }

            if (slot == ProfileEditorSlot.RightStickUp ||
                slot == ProfileEditorSlot.RightStickDown ||
                slot == ProfileEditorSlot.RightStickLeft ||
                slot == ProfileEditorSlot.RightStickRight)
            {
                return page.RightStick.Mode == StickMode.DirectionalKeys;
            }

            if (slot == ProfileEditorSlot.ButtonL4 ||
                slot == ProfileEditorSlot.ButtonL5 ||
                slot == ProfileEditorSlot.ButtonR5 ||
                slot == ProfileEditorSlot.ButtonR4 ||
                slot == ProfileEditorSlot.LeftTrackpad ||
                slot == ProfileEditorSlot.RightTrackpad)
            {
                return _visualMode == ControllerVisualMode.HandheldSteamDeck;
            }

            return true;
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

            // Se o menu de Presets / Copiar Jogo estiver aberto
            if (_isPresetMenuOpen)
            {
                if (btnB && !_lastB)
                {
                    _isPresetMenuOpen = false;
                    _lastB = btnB;
                    Invalidate();
                    return;
                }
                _lastB = btnB;

                if (btnA && !_lastA)
                {
                    ExecuteSelectedPresetMenuItem();
                    _lastA = btnA;
                    return;
                }
                _lastA = btnA;

                if (up || down)
                {
                    bool isInitial = (up && !_lastUp) || (down && !_lastDown);
                    long delay = _repeatCount == 0 ? InitialRepeatDelayMs : RepeatIntervalMs;
                    if (isInitial || (nowMs - _lastMoveTime >= delay))
                    {
                        if (up && _presetMenuFocusedIndex > 0)
                        {
                            _presetMenuFocusedIndex--;
                            Invalidate();
                        }
                        else if (down && _presetMenuFocusedIndex < _presetMenuItems.Count - 1)
                        {
                            _presetMenuFocusedIndex++;
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
                _lastLeft = left;
                _lastRight = right;
                _lastX = btnX;
                _lastRT = triggerRT;
                return;
            }

            // Gatilho RT salva imediatamente o perfil e volta para GameConfigPanel
            if (triggerRT && !_lastRT)
            {
                _lastRT = triggerRT;
                ConfirmAndSave();
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

            // Botão X limpa entrada focada (ou alterna Global se estiver num slider)
            if (btnX && !_lastX)
            {
                _lastX = btnX;
                if (IsActionSlot(_focusedSlot))
                {
                    ClearSlot(_focusedSlot);
                }
                else if (_focusedSlot == ProfileEditorSlot.DeadzoneSlider || _focusedSlot == ProfileEditorSlot.DeadzoneGlobalToggle)
                {
                    ToggleDeadzoneGlobal();
                }
                else if (_focusedSlot == ProfileEditorSlot.TriggerSlider || _focusedSlot == ProfileEditorSlot.TriggerGlobalToggle)
                {
                    ToggleTriggerGlobal();
                }
                return;
            }
            _lastX = btnX;

            // Botão A aciona / alterna o slot focado
            if (btnA && !_lastA)
            {
                _lastA = btnA;
                ActivateFocusedSlot();
                return;
            }
            _lastA = btnA;

            // Navegação ou ajuste de slider com D-Pad
            bool anyDir = up || down || left || right;
            if (anyDir)
            {
                bool isInitial = (up && !_lastUp) || (down && !_lastDown) || (left && !_lastLeft) || (right && !_lastRight);
                long delay = _repeatCount == 0 ? InitialRepeatDelayMs : RepeatIntervalMs;

                if (isInitial || (nowMs - _lastMoveTime >= delay))
                {
                    bool handledSlider = false;
                    if ((left || right) && IsSliderSlot(_focusedSlot))
                    {
                        int delta = right ? 2 : -2;
                        handledSlider = AdjustFocusedSlider(delta);
                    }

                    if (!handledSlider)
                    {
                        if (up) NavigateSpatial(0, -1);
                        else if (down) NavigateSpatial(0, 1);
                        else if (left) NavigateSpatial(-1, 0);
                        else if (right) NavigateSpatial(1, 0);
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

        private static bool IsSliderSlot(ProfileEditorSlot slot)
        {
            return slot == ProfileEditorSlot.MouseSensitivity ||
                   slot == ProfileEditorSlot.DeadzoneSlider ||
                   slot == ProfileEditorSlot.TriggerSlider;
        }

        private bool AdjustFocusedSlider(int delta)
        {
            var page = _workingProfile.ActivePage;
            switch (_focusedSlot)
            {
                case ProfileEditorSlot.MouseSensitivity:
                    page.MouseSensitivity = Math.Max(1, Math.Min(100, page.MouseSensitivity + delta));
                    Invalidate();
                    return true;

                case ProfileEditorSlot.DeadzoneSlider:
                    if (_workingProfile.UseGlobalDeadzone)
                    {
                        _workingProfile.UseGlobalDeadzone = false;
                        _workingProfile.CustomDeadzonePercent = _settings.StickDeadzonePercent;
                    }
                    _workingProfile.CustomDeadzonePercent = Math.Max(5, Math.Min(50, _workingProfile.CustomDeadzonePercent + delta));
                    Invalidate();
                    return true;

                case ProfileEditorSlot.TriggerSlider:
                    if (_workingProfile.UseGlobalTriggerThreshold)
                    {
                        _workingProfile.UseGlobalTriggerThreshold = false;
                        _workingProfile.CustomTriggerThresholdPercent = _settings.TriggerThresholdPercent;
                    }
                    _workingProfile.CustomTriggerThresholdPercent = Math.Max(5, Math.Min(90, _workingProfile.CustomTriggerThresholdPercent + delta));
                    Invalidate();
                    return true;

                default:
                    return false;
            }
        }

        public void ActivateFocusedSlot()
        {
            switch (_focusedSlot)
            {
                case ProfileEditorSlot.PresetSelector:
                    RebuildPresetMenuItems();
                    _isPresetMenuOpen = !_isPresetMenuOpen;
                    _presetMenuFocusedIndex = 0;
                    Invalidate();
                    break;

                case ProfileEditorSlot.DeadzoneGlobalToggle:
                case ProfileEditorSlot.DeadzoneSlider:
                    ToggleDeadzoneGlobal();
                    break;

                case ProfileEditorSlot.TriggerGlobalToggle:
                case ProfileEditorSlot.TriggerSlider:
                    ToggleTriggerGlobal();
                    break;

                case ProfileEditorSlot.LeftStickMode:
                    CycleStickMode(_workingProfile.ActivePage.LeftStick);
                    break;

                case ProfileEditorSlot.RightStickMode:
                    CycleStickMode(_workingProfile.ActivePage.RightStick);
                    break;

                case ProfileEditorSlot.LeftTrackpad:
                    TrackpadEditRequested?.Invoke(false, _workingProfile.ActivePage.LeftTrackpad);
                    break;

                case ProfileEditorSlot.RightTrackpad:
                    TrackpadEditRequested?.Invoke(true, _workingProfile.ActivePage.RightTrackpad);
                    break;

                case ProfileEditorSlot.FooterSave:
                    ConfirmAndSave();
                    break;

                case ProfileEditorSlot.FooterBack:
                    CancelRequested?.Invoke();
                    break;

                default:
                    if (IsActionSlot(_focusedSlot))
                    {
                        SlotEditRequested?.Invoke(_focusedSlot, GetSlotDisplayTitle(_focusedSlot), GetSlotActions(_focusedSlot));
                    }
                    break;
            }
        }

        private void ToggleDeadzoneGlobal()
        {
            _workingProfile.UseGlobalDeadzone = !_workingProfile.UseGlobalDeadzone;
            if (!_workingProfile.UseGlobalDeadzone && _workingProfile.CustomDeadzonePercent <= 0)
            {
                _workingProfile.CustomDeadzonePercent = _settings.StickDeadzonePercent;
            }
            Invalidate();
        }

        private void ToggleTriggerGlobal()
        {
            _workingProfile.UseGlobalTriggerThreshold = !_workingProfile.UseGlobalTriggerThreshold;
            if (!_workingProfile.UseGlobalTriggerThreshold && _workingProfile.CustomTriggerThresholdPercent <= 0)
            {
                _workingProfile.CustomTriggerThresholdPercent = _settings.TriggerThresholdPercent;
            }
            Invalidate();
        }

        private void CycleStickMode(StickConfig stick)
        {
            if (stick == null) return;
            switch (stick.Mode)
            {
                case StickMode.DirectionalKeys:
                    stick.Mode = StickMode.MouseMove;
                    break;
                case StickMode.MouseMove:
                    stick.Mode = StickMode.Disabled;
                    break;
                default:
                    stick.Mode = StickMode.DirectionalKeys;
                    if (stick.Up.Count == 0 && stick.Down.Count == 0 && stick.Left.Count == 0 && stick.Right.Count == 0)
                    {
                        stick.Up = new List<InputAction> { InputAction.FromKey(VirtualKey.W) };
                        stick.Down = new List<InputAction> { InputAction.FromKey(VirtualKey.S) };
                        stick.Left = new List<InputAction> { InputAction.FromKey(VirtualKey.A) };
                        stick.Right = new List<InputAction> { InputAction.FromKey(VirtualKey.D) };
                    }
                    break;
            }
            Invalidate();
        }

        private void ExecuteSelectedPresetMenuItem()
        {
            if (_presetMenuFocusedIndex >= 0 && _presetMenuFocusedIndex < _presetMenuItems.Count)
            {
                var item = _presetMenuItems[_presetMenuFocusedIndex];
                if (item.IsPreset)
                {
                    ApplyPresetByName(item.PresetName);
                }
                else if (item.SourceGame != null)
                {
                    CopyProfileFromGame(item.SourceGame);
                }
            }
            _isPresetMenuOpen = false;
            Invalidate();
        }

        public void ConfirmAndSave()
        {
            if (_game != null)
            {
                _game.Profile = _workingProfile.Clone();
            }
            SaveRequested?.Invoke(_game, _workingProfile.Clone());
        }

        private void NavigateSpatial(int dirX, int dirY)
        {
            EnsureLogicalBoundsPopulated();
            if (!_slotBounds.TryGetValue(_focusedSlot, out var curRect))
            {
                _focusedSlot = ProfileEditorSlot.MouseSensitivity;
                Invalidate();
                return;
            }

            // Regras diretas explícitas para os sliders e toggles [Global] no topo
            if (dirX > 0)
            {
                if (_focusedSlot == ProfileEditorSlot.DeadzoneSlider)
                {
                    _focusedSlot = ProfileEditorSlot.DeadzoneGlobalToggle;
                    Invalidate();
                    return;
                }
                if (_focusedSlot == ProfileEditorSlot.TriggerSlider)
                {
                    _focusedSlot = ProfileEditorSlot.TriggerGlobalToggle;
                    Invalidate();
                    return;
                }
            }
            else if (dirX < 0)
            {
                if (_focusedSlot == ProfileEditorSlot.DeadzoneGlobalToggle)
                {
                    _focusedSlot = ProfileEditorSlot.DeadzoneSlider;
                    Invalidate();
                    return;
                }
                if (_focusedSlot == ProfileEditorSlot.TriggerGlobalToggle)
                {
                    _focusedSlot = ProfileEditorSlot.TriggerSlider;
                    Invalidate();
                    return;
                }
            }

            float curCx = curRect.X + curRect.Width / 2f;
            float curCy = curRect.Y + curRect.Height / 2f;

            ProfileEditorSlot bestSlot = _focusedSlot;
            float bestScore = float.MaxValue;

            foreach (var kvp in _slotBounds)
            {
                var candidate = kvp.Key;
                if (candidate == _focusedSlot || !IsSlotVisible(candidate)) continue;

                var rect = kvp.Value;
                float cx = rect.X + rect.Width / 2f;
                float cy = rect.Y + rect.Height / 2f;

                float dx = cx - curCx;
                float dy = cy - curCy;

                if (dirY < 0 && dy >= -4f) continue;
                if (dirY > 0 && dy <= 4f) continue;
                if (dirX < 0 && dx >= -4f) continue;
                if (dirX > 0 && dx <= 4f) continue;

                float primaryDist = dirY != 0 ? Math.Abs(dy) : Math.Abs(dx);
                float secondaryDist = dirY != 0 ? Math.Abs(dx) : Math.Abs(dy);

                // Penaliza candidatos muito desalinhados no eixo ortogonal
                float score = primaryDist + secondaryDist * 1.65f;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestSlot = candidate;
                }
            }

            if (bestSlot != _focusedSlot)
            {
                _focusedSlot = bestSlot;
                Invalidate();
            }
        }

        private void EnsureLogicalBoundsPopulated()
        {
            if (_slotBounds.Count > 0) return;

            _slotBounds[ProfileEditorSlot.PresetSelector] = new RectangleF(392, 6, 216, 28);
            _slotBounds[ProfileEditorSlot.MouseSensitivity] = new RectangleF(195, 45, 250, 18);
            _slotBounds[ProfileEditorSlot.DeadzoneSlider] = new RectangleF(195, 69, 250, 18);
            _slotBounds[ProfileEditorSlot.DeadzoneGlobalToggle] = new RectangleF(502, 67, 96, 21);
            _slotBounds[ProfileEditorSlot.TriggerSlider] = new RectangleF(195, 93, 250, 18);
            _slotBounds[ProfileEditorSlot.TriggerGlobalToggle] = new RectangleF(502, 91, 96, 21);

            _slotBounds[ProfileEditorSlot.ButtonLT] = new RectangleF(12, 124, 178, 25);
            _slotBounds[ProfileEditorSlot.ButtonLB] = new RectangleF(12, 153, 178, 25);
            _slotBounds[ProfileEditorSlot.ButtonBack] = new RectangleF(12, 182, 178, 25);

            _slotBounds[ProfileEditorSlot.ButtonRT] = new RectangleF(430, 124, 178, 25);
            _slotBounds[ProfileEditorSlot.ButtonRB] = new RectangleF(430, 153, 178, 25);
            _slotBounds[ProfileEditorSlot.ButtonStart] = new RectangleF(430, 182, 178, 25);

            if (_visualMode == ControllerVisualMode.HandheldSteamDeck)
            {
                // Layout otimizado para Handheld Steam Deck:
                // Coluna Esquerda: Left Stick (Y=214..334), D-Pad (Y=338..422), Left Trackpad Placeholder (Y=428..492)
                _slotBounds[ProfileEditorSlot.LeftStickMode] = new RectangleF(20, 230, 162, 17);
                _slotBounds[ProfileEditorSlot.LeftStickUp] = new RectangleF(20, 249, 78, 17);
                _slotBounds[ProfileEditorSlot.LeftStickDown] = new RectangleF(104, 249, 78, 17);
                _slotBounds[ProfileEditorSlot.LeftStickLeft] = new RectangleF(20, 268, 78, 17);
                _slotBounds[ProfileEditorSlot.LeftStickRight] = new RectangleF(104, 268, 78, 17);
                _slotBounds[ProfileEditorSlot.LeftStickAdditional] = new RectangleF(20, 287, 162, 17);
                _slotBounds[ProfileEditorSlot.LeftStickClick] = new RectangleF(20, 306, 162, 18);

                // D-Pad
                _slotBounds[ProfileEditorSlot.DPadUp] = new RectangleF(20, 356, 78, 20);
                _slotBounds[ProfileEditorSlot.DPadDown] = new RectangleF(104, 356, 78, 20);
                _slotBounds[ProfileEditorSlot.DPadLeft] = new RectangleF(20, 380, 78, 20);
                _slotBounds[ProfileEditorSlot.DPadRight] = new RectangleF(104, 380, 78, 20);

                // Coluna Direita: Face Buttons (Y=214..304), Right Stick (Y=308..422), Right Trackpad Placeholder (Y=428..492)
                _slotBounds[ProfileEditorSlot.ButtonY] = new RectangleF(438, 230, 162, 16);
                _slotBounds[ProfileEditorSlot.ButtonB] = new RectangleF(438, 248, 162, 16);
                _slotBounds[ProfileEditorSlot.ButtonX] = new RectangleF(438, 266, 162, 16);
                _slotBounds[ProfileEditorSlot.ButtonA] = new RectangleF(438, 284, 162, 16);

                _slotBounds[ProfileEditorSlot.RightStickMode] = new RectangleF(438, 324, 162, 17);
                _slotBounds[ProfileEditorSlot.RightStickUp] = new RectangleF(438, 343, 78, 17);
                _slotBounds[ProfileEditorSlot.RightStickDown] = new RectangleF(522, 343, 78, 17);
                _slotBounds[ProfileEditorSlot.RightStickLeft] = new RectangleF(438, 362, 78, 17);
                _slotBounds[ProfileEditorSlot.RightStickRight] = new RectangleF(522, 362, 78, 17);
                _slotBounds[ProfileEditorSlot.RightStickAdditional] = new RectangleF(438, 381, 162, 17);
                _slotBounds[ProfileEditorSlot.RightStickClick] = new RectangleF(438, 400, 162, 18);

                // Botões Traseiros Handheld (L4, L5, R5, R4) em linha horizontal centralizada
                _slotBounds[ProfileEditorSlot.ButtonL4] = new RectangleF(196, 380, 52, 36);
                _slotBounds[ProfileEditorSlot.ButtonL5] = new RectangleF(252, 380, 52, 36);
                _slotBounds[ProfileEditorSlot.ButtonR5] = new RectangleF(316, 380, 52, 36);
                _slotBounds[ProfileEditorSlot.ButtonR4] = new RectangleF(372, 380, 52, 36);

                // Cards Interativos dos Trackpads (Fase 6 / Etapa 2)
                _slotBounds[ProfileEditorSlot.LeftTrackpad] = new RectangleF(12, 428, 178, 64);
                _slotBounds[ProfileEditorSlot.RightTrackpad] = new RectangleF(430, 428, 178, 64);

                // Botões de Rodapé Centralizados (Salvar e Voltar)
                _slotBounds[ProfileEditorSlot.FooterSave] = new RectangleF(204, 436, 112, 32);
                _slotBounds[ProfileEditorSlot.FooterBack] = new RectangleF(322, 436, 94, 32);
            }
            else
            {
                // Layout Padrão Gamepad Clássico
                // Stick Esquerdo
                _slotBounds[ProfileEditorSlot.LeftStickMode] = new RectangleF(20, 236, 162, 22);
                _slotBounds[ProfileEditorSlot.LeftStickUp] = new RectangleF(20, 262, 78, 22);
                _slotBounds[ProfileEditorSlot.LeftStickDown] = new RectangleF(104, 262, 78, 22);
                _slotBounds[ProfileEditorSlot.LeftStickLeft] = new RectangleF(20, 288, 78, 22);
                _slotBounds[ProfileEditorSlot.LeftStickRight] = new RectangleF(104, 288, 78, 22);
                _slotBounds[ProfileEditorSlot.LeftStickAdditional] = new RectangleF(20, 314, 162, 22);
                _slotBounds[ProfileEditorSlot.LeftStickClick] = new RectangleF(20, 340, 162, 22);

                // D-Pad
                _slotBounds[ProfileEditorSlot.DPadUp] = new RectangleF(20, 396, 78, 24);
                _slotBounds[ProfileEditorSlot.DPadDown] = new RectangleF(104, 396, 78, 24);
                _slotBounds[ProfileEditorSlot.DPadLeft] = new RectangleF(20, 424, 78, 24);
                _slotBounds[ProfileEditorSlot.DPadRight] = new RectangleF(104, 424, 78, 24);

                // Botões Faciais
                _slotBounds[ProfileEditorSlot.ButtonY] = new RectangleF(438, 236, 162, 23);
                _slotBounds[ProfileEditorSlot.ButtonB] = new RectangleF(438, 263, 162, 23);
                _slotBounds[ProfileEditorSlot.ButtonX] = new RectangleF(438, 290, 162, 23);
                _slotBounds[ProfileEditorSlot.ButtonA] = new RectangleF(438, 317, 162, 23);

                // Stick Direito
                _slotBounds[ProfileEditorSlot.RightStickMode] = new RectangleF(438, 372, 162, 22);
                _slotBounds[ProfileEditorSlot.RightStickUp] = new RectangleF(438, 396, 78, 20);
                _slotBounds[ProfileEditorSlot.RightStickDown] = new RectangleF(522, 396, 78, 20);
                _slotBounds[ProfileEditorSlot.RightStickLeft] = new RectangleF(438, 418, 78, 20);
                _slotBounds[ProfileEditorSlot.RightStickRight] = new RectangleF(522, 418, 78, 20);
                _slotBounds[ProfileEditorSlot.RightStickAdditional] = new RectangleF(438, 440, 162, 22);
                _slotBounds[ProfileEditorSlot.RightStickClick] = new RectangleF(438, 464, 162, 22);

                // Rear buttons preenchidos defensivamente
                _slotBounds[ProfileEditorSlot.ButtonL4] = new RectangleF(196, 380, 52, 36);
                _slotBounds[ProfileEditorSlot.ButtonL5] = new RectangleF(252, 380, 52, 36);
                _slotBounds[ProfileEditorSlot.ButtonR5] = new RectangleF(316, 380, 52, 36);
                _slotBounds[ProfileEditorSlot.ButtonR4] = new RectangleF(372, 380, 52, 36);

                // Botões de Rodapé
                _slotBounds[ProfileEditorSlot.FooterSave] = new RectangleF(204, 456, 112, 30);
                _slotBounds[ProfileEditorSlot.FooterBack] = new RectangleF(322, 456, 94, 30);
            }
        }

        private PointF ClientToLogical(Point pt)
        {
            GetTransformParams(out float scale, out float offsetX, out float offsetY);
            if (scale <= 0.001f) return PointF.Empty;
            return new PointF((pt.X - offsetX) / scale, (pt.Y - offsetY) / scale);
        }

        private void GetTransformParams(out float scale, out float offsetX, out float offsetY)
        {
            float w = Math.Max(1, ClientSize.Width);
            float h = Math.Max(1, ClientSize.Height);
            scale = Math.Min(w / LogicalWidth, h / LogicalHeight);
            offsetX = (w - LogicalWidth * scale) / 2f;
            offsetY = (h - LogicalHeight * scale) / 2f;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            EnsureLogicalBoundsPopulated();

            var g = e.Graphics;
            g.Clear(BgColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            GetTransformParams(out float scale, out float offsetX, out float offsetY);
            g.TranslateTransform(offsetX, offsetY);
            g.ScaleTransform(scale, scale);

            DrawHeader(g);
            DrawSlidersSection(g);
            DrawControllerCenterDiagram(g);
            if (_visualMode == ControllerVisualMode.HandheldSteamDeck)
            {
                DrawRearButtonsRow(g);
            }
            DrawTopShoulderSlots(g);
            DrawLeftStickCard(g);
            DrawDPadCard(g);
            DrawFaceButtonsCard(g);
            DrawRightStickCard(g);
            if (_visualMode == ControllerVisualMode.HandheldSteamDeck)
            {
                DrawTrackpadPlaceholderCards(g);
            }
            DrawCenterActionButtons(g);
            DrawFooterLegend(g);

            if (_isPresetMenuOpen)
            {
                DrawPresetOverlayMenu(g);
            }
        }

        private void DrawHeader(Graphics g)
        {
            using (var bg = new SolidBrush(HeaderBg))
            using (var linePen = new Pen(BorderSubtle, 1f))
            {
                g.FillRectangle(bg, 0, 0, LogicalWidth, 38);
                g.DrawLine(linePen, 0, 38, LogicalWidth, 38);
            }

            string gameName = _game?.Name ?? Strings.DefaultGameName;
            string title = string.Format(Strings.ProfileEditorTitle, gameName);
            using (var font = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (var brush = new SolidBrush(TextPrimary))
            {
                g.DrawString(title, font, brush, 14, 9);
            }

            // Botão [Importar Preset / Copiar Jogo ▼]
            var btnRect = _slotBounds[ProfileEditorSlot.PresetSelector];
            bool focused = (_focusedSlot == ProfileEditorSlot.PresetSelector) || _isPresetMenuOpen;
            DrawInteractiveSlotBox(g, btnRect, focused, Color.FromArgb(32, 75, 120));

            using (var font = new Font("Segoe UI", 8.2f, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.White))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(Strings.ProfileImportPresetOrCopy, font, brush, btnRect, sf);
            }
        }

        private void DrawSlidersSection(Graphics g)
        {
            using (var bg = new SolidBrush(Color.FromArgb(27, 30, 38)))
            using (var linePen = new Pen(BorderSubtle, 1f))
            {
                g.FillRectangle(bg, 0, 39, LogicalWidth, 76);
                g.DrawLine(linePen, 0, 115, LogicalWidth, 115);
            }

            var page = _workingProfile.ActivePage;

            // 1. Sensibilidade Mouse (Página 1)
            DrawSliderRow(
                g,
                Strings.ProfileSensitivityLabel,
                _slotBounds[ProfileEditorSlot.MouseSensitivity],
                page.MouseSensitivity,
                1,
                100,
                page.MouseSensitivity.ToString(),
                _focusedSlot == ProfileEditorSlot.MouseSensitivity,
                isGlobalLocked: false);

            // 2. Deadzone Analógicos + [X] Global
            int effectiveDeadzone = _workingProfile.UseGlobalDeadzone
                ? _settings.StickDeadzonePercent
                : _workingProfile.CustomDeadzonePercent;

            DrawSliderRow(
                g,
                Strings.ProfileDeadzoneLabel,
                _slotBounds[ProfileEditorSlot.DeadzoneSlider],
                effectiveDeadzone,
                5,
                50,
                effectiveDeadzone + "%",
                _focusedSlot == ProfileEditorSlot.DeadzoneSlider,
                isGlobalLocked: _workingProfile.UseGlobalDeadzone);

            DrawGlobalCheckbox(
                g,
                _slotBounds[ProfileEditorSlot.DeadzoneGlobalToggle],
                _workingProfile.UseGlobalDeadzone,
                _focusedSlot == ProfileEditorSlot.DeadzoneGlobalToggle);

            // 3. Limiar de Gatilhos (LT/RT) + [X] Global
            int effectiveTrigger = _workingProfile.UseGlobalTriggerThreshold
                ? _settings.TriggerThresholdPercent
                : _workingProfile.CustomTriggerThresholdPercent;

            DrawSliderRow(
                g,
                Strings.ProfileTriggerThresholdLabel,
                _slotBounds[ProfileEditorSlot.TriggerSlider],
                effectiveTrigger,
                5,
                90,
                effectiveTrigger + "%",
                _focusedSlot == ProfileEditorSlot.TriggerSlider,
                isGlobalLocked: _workingProfile.UseGlobalTriggerThreshold);

            DrawGlobalCheckbox(
                g,
                _slotBounds[ProfileEditorSlot.TriggerGlobalToggle],
                _workingProfile.UseGlobalTriggerThreshold,
                _focusedSlot == ProfileEditorSlot.TriggerGlobalToggle);
        }

        private static void DrawSliderRow(
            Graphics g,
            string label,
            RectangleF trackRect,
            int value,
            int min,
            int max,
            string valueText,
            bool isFocused,
            bool isGlobalLocked)
        {
            using (var lblFont = new Font("Segoe UI", 8.2f, isFocused ? FontStyle.Bold : FontStyle.Regular))
            using (var lblBrush = new SolidBrush(isFocused ? BorderFocused : TextSecondary))
            {
                g.DrawString(label, lblFont, lblBrush, 14, trackRect.Y + 1);
            }

            if (isFocused)
            {
                var highlightRect = new RectangleF(trackRect.X - 6, trackRect.Y - 2, trackRect.Width + 54, trackRect.Height + 4);
                using (var bg = new SolidBrush(FocusBg))
                using (var pen = new Pen(BorderFocused, 1.5f))
                {
                    g.FillRectangle(bg, highlightRect);
                    g.DrawRectangle(pen, highlightRect.X, highlightRect.Y, highlightRect.Width, highlightRect.Height);
                }
            }

            float barY = trackRect.Y + trackRect.Height / 2f - 3f;
            float barH = 6f;
            float ratio = Math.Max(0f, Math.Min(1f, (float)(value - min) / Math.Max(1, max - min)));
            float fillW = trackRect.Width * ratio;

            Color fillColor = isGlobalLocked
                ? Color.FromArgb(95, 130, 175)
                : (isFocused ? ActiveGreen : BorderFocused);

            using (var backBrush = new SolidBrush(InputBg))
            using (var fillBrush = new SolidBrush(fillColor))
            using (var borderPen = new Pen(BorderSubtle, 1f))
            {
                g.FillRectangle(backBrush, trackRect.X, barY, trackRect.Width, barH);
                if (fillW > 0)
                {
                    g.FillRectangle(fillBrush, trackRect.X, barY, fillW, barH);
                }
                g.DrawRectangle(borderPen, trackRect.X, barY, trackRect.Width, barH);
            }

            float knobX = trackRect.X + fillW;
            float knobY = trackRect.Y + trackRect.Height / 2f;
            using (var knobBrush = new SolidBrush(isFocused ? Color.White : TextPrimary))
            using (var knobPen = new Pen(fillColor, 2f))
            {
                g.FillEllipse(knobBrush, knobX - 6, knobY - 6, 12, 12);
                g.DrawEllipse(knobPen, knobX - 6, knobY - 6, 12, 12);
            }

            using (var valFont = new Font("Segoe UI", 8.2f, FontStyle.Bold))
            using (var valBrush = new SolidBrush(TextPrimary))
            {
                g.DrawString(valueText, valFont, valBrush, trackRect.Right + 8, trackRect.Y + 1);
            }
        }

        private static void DrawGlobalCheckbox(Graphics g, RectangleF rect, bool isChecked, bool isFocused)
        {
            DrawInteractiveSlotBox(g, rect, isFocused, isChecked ? Color.FromArgb(32, 62, 48) : InputBg);

            var boxRect = new RectangleF(rect.X + 6, rect.Y + 4, 13, 13);
            using (var boxBrush = new SolidBrush(isChecked ? ActiveGreen : InputBg))
            using (var boxPen = new Pen(isChecked ? ActiveGreen : BorderIdle, 1.2f))
            {
                g.FillRectangle(boxBrush, boxRect);
                g.DrawRectangle(boxPen, boxRect.X, boxRect.Y, boxRect.Width, boxRect.Height);
            }

            if (isChecked)
            {
                using (var checkPen = new Pen(BgColor, 2f))
                {
                    g.DrawLine(checkPen, boxRect.X + 2, boxRect.Y + 6, boxRect.X + 5, boxRect.Y + 10);
                    g.DrawLine(checkPen, boxRect.X + 5, boxRect.Y + 10, boxRect.X + 11, boxRect.Y + 3);
                }
            }

            using (var font = new Font("Segoe UI", 8f, FontStyle.Bold))
            using (var brush = new SolidBrush(isChecked ? ActiveGreen : TextSecondary))
            {
                g.DrawString(Strings.ProfileGlobalToggle, font, brush, rect.X + 24, rect.Y + 3);
            }
        }

        private void DrawTopShoulderSlots(Graphics g)
        {
            var page = _workingProfile.ActivePage;

            DrawLabeledMappingRow(
                g,
                _slotBounds[ProfileEditorSlot.ButtonLT],
                "LT:",
                InputAction.FormatComboDisplay(page.GetActions(ControllerButton.LeftTrigger)),
                _focusedSlot == ProfileEditorSlot.ButtonLT);

            DrawLabeledMappingRow(
                g,
                _slotBounds[ProfileEditorSlot.ButtonLB],
                "LB:",
                InputAction.FormatComboDisplay(page.GetActions(ControllerButton.LeftShoulder)),
                _focusedSlot == ProfileEditorSlot.ButtonLB);

            DrawLabeledMappingRow(
                g,
                _slotBounds[ProfileEditorSlot.ButtonBack],
                "Select:",
                InputAction.FormatComboDisplay(page.GetActions(ControllerButton.Back)),
                _focusedSlot == ProfileEditorSlot.ButtonBack);

            DrawLabeledMappingRow(
                g,
                _slotBounds[ProfileEditorSlot.ButtonRT],
                "RT:",
                InputAction.FormatComboDisplay(page.GetActions(ControllerButton.RightTrigger)),
                _focusedSlot == ProfileEditorSlot.ButtonRT);

            DrawLabeledMappingRow(
                g,
                _slotBounds[ProfileEditorSlot.ButtonRB],
                "RB:",
                InputAction.FormatComboDisplay(page.GetActions(ControllerButton.RightShoulder)),
                _focusedSlot == ProfileEditorSlot.ButtonRB);

            DrawLabeledMappingRow(
                g,
                _slotBounds[ProfileEditorSlot.ButtonStart],
                "Start:",
                InputAction.FormatComboDisplay(page.GetActions(ControllerButton.Start)),
                _focusedSlot == ProfileEditorSlot.ButtonStart);
        }

        private void DrawLeftStickCard(Graphics g)
        {
            var cardRect = _visualMode == ControllerVisualMode.HandheldSteamDeck
                ? new RectangleF(12, 214, 178, 120)
                : new RectangleF(12, 214, 178, 154);
            DrawCardFrame(g, cardRect, Strings.ProfileCardLeftStick);

            var stick = _workingProfile.ActivePage.LeftStick;

            // Modo
            DrawLabeledMappingRow(
                g,
                _slotBounds[ProfileEditorSlot.LeftStickMode],
                Strings.ProfileStickModeLabel,
                FormatStickMode(stick.Mode),
                _focusedSlot == ProfileEditorSlot.LeftStickMode,
                isModeBadge: true);

            if (stick.Mode == StickMode.DirectionalKeys)
            {
                DrawCompactMappingBox(
                    g,
                    _slotBounds[ProfileEditorSlot.LeftStickUp],
                    Strings.ProfileDirUp,
                    InputAction.FormatComboDisplay(stick.Up),
                    _focusedSlot == ProfileEditorSlot.LeftStickUp);

                DrawCompactMappingBox(
                    g,
                    _slotBounds[ProfileEditorSlot.LeftStickDown],
                    Strings.ProfileDirDown,
                    InputAction.FormatComboDisplay(stick.Down),
                    _focusedSlot == ProfileEditorSlot.LeftStickDown);

                DrawCompactMappingBox(
                    g,
                    _slotBounds[ProfileEditorSlot.LeftStickLeft],
                    Strings.ProfileDirLeft,
                    InputAction.FormatComboDisplay(stick.Left),
                    _focusedSlot == ProfileEditorSlot.LeftStickLeft);

                DrawCompactMappingBox(
                    g,
                    _slotBounds[ProfileEditorSlot.LeftStickRight],
                    Strings.ProfileDirRight,
                    InputAction.FormatComboDisplay(stick.Right),
                    _focusedSlot == ProfileEditorSlot.LeftStickRight);
            }
            else
            {
                var infoRect = _visualMode == ControllerVisualMode.HandheldSteamDeck
                    ? new RectangleF(20, 249, 162, 36)
                    : new RectangleF(20, 264, 162, 44);
                using (var font = new Font("Segoe UI", 7.8f, FontStyle.Italic))
                using (var brush = new SolidBrush(TextSecondary))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    string desc = stick.Mode == StickMode.MouseMove
                        ? "Controla o cursor / câmera com curva suave"
                        : "Eixo analógico desativado";
                    g.DrawString(desc, font, brush, infoRect, sf);
                }
            }

            // Comando Adicional (ex: F9)
            DrawLabeledMappingRow(
                g,
                _slotBounds[ProfileEditorSlot.LeftStickAdditional],
                Strings.ProfileAdditionalAction,
                InputAction.FormatComboDisplay(stick.AdditionalActions),
                _focusedSlot == ProfileEditorSlot.LeftStickAdditional,
                highlightNonEmpty: true);

            // Clique L3
            DrawLabeledMappingRow(
                g,
                _slotBounds[ProfileEditorSlot.LeftStickClick],
                Strings.ProfileClickL3,
                InputAction.FormatComboDisplay(_workingProfile.ActivePage.GetActions(ControllerButton.LeftStickClick)),
                _focusedSlot == ProfileEditorSlot.LeftStickClick);
        }

        private void DrawDPadCard(Graphics g)
        {
            var cardRect = _visualMode == ControllerVisualMode.HandheldSteamDeck
                ? new RectangleF(12, 338, 178, 84)
                : new RectangleF(12, 374, 178, 82);
            DrawCardFrame(g, cardRect, Strings.ProfileCardDPad);

            var page = _workingProfile.ActivePage;

            DrawCompactMappingBox(
                g,
                _slotBounds[ProfileEditorSlot.DPadUp],
                Strings.ProfileDirUp,
                InputAction.FormatComboDisplay(page.GetActions(ControllerButton.DPadUp)),
                _focusedSlot == ProfileEditorSlot.DPadUp);

            DrawCompactMappingBox(
                g,
                _slotBounds[ProfileEditorSlot.DPadDown],
                Strings.ProfileDirDown,
                InputAction.FormatComboDisplay(page.GetActions(ControllerButton.DPadDown)),
                _focusedSlot == ProfileEditorSlot.DPadDown);

            DrawCompactMappingBox(
                g,
                _slotBounds[ProfileEditorSlot.DPadLeft],
                Strings.ProfileDirLeft,
                InputAction.FormatComboDisplay(page.GetActions(ControllerButton.DPadLeft)),
                _focusedSlot == ProfileEditorSlot.DPadLeft);

            DrawCompactMappingBox(
                g,
                _slotBounds[ProfileEditorSlot.DPadRight],
                Strings.ProfileDirRight,
                InputAction.FormatComboDisplay(page.GetActions(ControllerButton.DPadRight)),
                _focusedSlot == ProfileEditorSlot.DPadRight);
        }

        private void DrawFaceButtonsCard(Graphics g)
        {
            var cardRect = _visualMode == ControllerVisualMode.HandheldSteamDeck
                ? new RectangleF(430, 214, 178, 90)
                : new RectangleF(430, 214, 178, 132);
            DrawCardFrame(g, cardRect, Strings.ProfileCardFaceButtons);

            var page = _workingProfile.ActivePage;

            DrawLabeledMappingRow(
                g,
                _slotBounds[ProfileEditorSlot.ButtonY],
                "(Y):",
                InputAction.FormatComboDisplay(page.GetActions(ControllerButton.Y)),
                _focusedSlot == ProfileEditorSlot.ButtonY);

            DrawLabeledMappingRow(
                g,
                _slotBounds[ProfileEditorSlot.ButtonB],
                "(B):",
                InputAction.FormatComboDisplay(page.GetActions(ControllerButton.B)),
                _focusedSlot == ProfileEditorSlot.ButtonB);

            DrawLabeledMappingRow(
                g,
                _slotBounds[ProfileEditorSlot.ButtonX],
                "(X):",
                InputAction.FormatComboDisplay(page.GetActions(ControllerButton.X)),
                _focusedSlot == ProfileEditorSlot.ButtonX);

            DrawLabeledMappingRow(
                g,
                _slotBounds[ProfileEditorSlot.ButtonA],
                "(A):",
                InputAction.FormatComboDisplay(page.GetActions(ControllerButton.A)),
                _focusedSlot == ProfileEditorSlot.ButtonA,
                highlightNonEmpty: true);
        }

        private void DrawRightStickCard(Graphics g)
        {
            var cardRect = _visualMode == ControllerVisualMode.HandheldSteamDeck
                ? new RectangleF(430, 308, 178, 114)
                : new RectangleF(430, 350, 178, 142);
            DrawCardFrame(g, cardRect, Strings.ProfileCardRightStick);

            var stick = _workingProfile.ActivePage.RightStick;

            DrawLabeledMappingRow(
                g,
                _slotBounds[ProfileEditorSlot.RightStickMode],
                Strings.ProfileStickModeLabel,
                FormatStickMode(stick.Mode),
                _focusedSlot == ProfileEditorSlot.RightStickMode,
                isModeBadge: true);

            if (stick.Mode == StickMode.DirectionalKeys)
            {
                DrawCompactMappingBox(
                    g,
                    _slotBounds[ProfileEditorSlot.RightStickUp],
                    Strings.ProfileDirUp,
                    InputAction.FormatComboDisplay(stick.Up),
                    _focusedSlot == ProfileEditorSlot.RightStickUp);

                DrawCompactMappingBox(
                    g,
                    _slotBounds[ProfileEditorSlot.RightStickDown],
                    Strings.ProfileDirDown,
                    InputAction.FormatComboDisplay(stick.Down),
                    _focusedSlot == ProfileEditorSlot.RightStickDown);

                DrawCompactMappingBox(
                    g,
                    _slotBounds[ProfileEditorSlot.RightStickLeft],
                    Strings.ProfileDirLeft,
                    InputAction.FormatComboDisplay(stick.Left),
                    _focusedSlot == ProfileEditorSlot.RightStickLeft);

                DrawCompactMappingBox(
                    g,
                    _slotBounds[ProfileEditorSlot.RightStickRight],
                    Strings.ProfileDirRight,
                    InputAction.FormatComboDisplay(stick.Right),
                    _focusedSlot == ProfileEditorSlot.RightStickRight);
            }
            else
            {
                var infoRect = _visualMode == ControllerVisualMode.HandheldSteamDeck
                    ? new RectangleF(438, 343, 162, 36)
                    : new RectangleF(438, 396, 162, 40);
                using (var font = new Font("Segoe UI", 7.8f, FontStyle.Italic))
                using (var brush = new SolidBrush(TextSecondary))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    string desc = stick.Mode == StickMode.MouseMove
                        ? "Emulação de Mouse Exponencial"
                        : "Eixo analógico desativado";
                    g.DrawString(desc, font, brush, infoRect, sf);
                }
            }

            // Comando Adicional (ex: Clique Direito câmera MMO)
            DrawLabeledMappingRow(
                g,
                _slotBounds[ProfileEditorSlot.RightStickAdditional],
                Strings.ProfileAdditionalAction,
                InputAction.FormatComboDisplay(stick.AdditionalActions),
                _focusedSlot == ProfileEditorSlot.RightStickAdditional,
                highlightNonEmpty: true);

            // Clique R3
            DrawLabeledMappingRow(
                g,
                _slotBounds[ProfileEditorSlot.RightStickClick],
                Strings.ProfileClickR3,
                InputAction.FormatComboDisplay(_workingProfile.ActivePage.GetActions(ControllerButton.RightStickClick)),
                _focusedSlot == ProfileEditorSlot.RightStickClick);
        }

        private void DrawControllerCenterDiagram(Graphics g)
        {
            if (_visualMode == ControllerVisualMode.HandheldSteamDeck)
            {
                DrawHandheldCenterDiagram(g);
                return;
            }
            // Desenha um diagrama estilizado do controle no centro (entre X=196 e X=424, Y=140 a Y=430)
            var state = g.Save();
            float targetCenterX = 310f;
            float targetCenterY = 285f;
            float diagScale = 0.43f;

            g.TranslateTransform(targetCenterX - (520f * diagScale) / 2f, targetCenterY - (330f * diagScale) / 2f);
            g.ScaleTransform(diagScale, diagScale);

            // Corpo do controle
            using (var path = new GraphicsPath())
            {
                path.AddArc(50, 70, 120, 120, 180, 90);
                path.AddArc(350, 70, 120, 120, 270, 90);
                path.AddArc(400, 160, 100, 140, 0, 70);
                path.AddArc(20, 160, 100, 140, 110, 70);
                path.CloseFigure();
                using (var brush = new SolidBrush(CardBg))
                using (var pen = new Pen(BorderIdle, 3f))
                {
                    g.FillPath(brush, path);
                    g.DrawPath(pen, path);
                }
            }

            // Gatilhos & Bumpers
            DrawDiagramBox(g, new RectangleF(90, 8, 90, 26), "LT", _focusedSlot == ProfileEditorSlot.ButtonLT);
            DrawDiagramBox(g, new RectangleF(340, 8, 90, 26), "RT", _focusedSlot == ProfileEditorSlot.ButtonRT);
            DrawDiagramBox(g, new RectangleF(90, 42, 90, 22), "LB", _focusedSlot == ProfileEditorSlot.ButtonLB);
            DrawDiagramBox(g, new RectangleF(340, 42, 90, 22), "RB", _focusedSlot == ProfileEditorSlot.ButtonRB);

            // Sticks
            bool lStickFocused = _focusedSlot == ProfileEditorSlot.LeftStickMode ||
                                 _focusedSlot == ProfileEditorSlot.LeftStickUp ||
                                 _focusedSlot == ProfileEditorSlot.LeftStickDown ||
                                 _focusedSlot == ProfileEditorSlot.LeftStickLeft ||
                                 _focusedSlot == ProfileEditorSlot.LeftStickRight ||
                                 _focusedSlot == ProfileEditorSlot.LeftStickAdditional ||
                                 _focusedSlot == ProfileEditorSlot.LeftStickClick;

            bool rStickFocused = _focusedSlot == ProfileEditorSlot.RightStickMode ||
                                 _focusedSlot == ProfileEditorSlot.RightStickUp ||
                                 _focusedSlot == ProfileEditorSlot.RightStickDown ||
                                 _focusedSlot == ProfileEditorSlot.RightStickLeft ||
                                 _focusedSlot == ProfileEditorSlot.RightStickRight ||
                                 _focusedSlot == ProfileEditorSlot.RightStickAdditional ||
                                 _focusedSlot == ProfileEditorSlot.RightStickClick;

            DrawDiagramStick(g, 140, 125, "L", lStickFocused, _focusedSlot == ProfileEditorSlot.LeftStickClick);
            DrawDiagramStick(g, 330, 215, "R", rStickFocused, _focusedSlot == ProfileEditorSlot.RightStickClick);

            // D-Pad
            const float s = 22f;
            float dcx = 210f, dcy = 215f;
            DrawDiagramBox(g, new RectangleF(dcx - s / 2, dcy - s * 1.5f, s, s), "▲", _focusedSlot == ProfileEditorSlot.DPadUp);
            DrawDiagramBox(g, new RectangleF(dcx - s / 2, dcy + s * 0.5f, s, s), "▼", _focusedSlot == ProfileEditorSlot.DPadDown);
            DrawDiagramBox(g, new RectangleF(dcx - s * 1.5f, dcy - s / 2, s, s), "◄", _focusedSlot == ProfileEditorSlot.DPadLeft);
            DrawDiagramBox(g, new RectangleF(dcx + s * 0.5f, dcy - s / 2, s, s), "►", _focusedSlot == ProfileEditorSlot.DPadRight);

            // Botões Faciais
            float fcx = 390f, fcy = 125f;
            DrawDiagramCircle(g, fcx, fcy + 32, "A", _focusedSlot == ProfileEditorSlot.ButtonA, Color.FromArgb(90, 200, 90));
            DrawDiagramCircle(g, fcx + 32, fcy, "B", _focusedSlot == ProfileEditorSlot.ButtonB, Color.FromArgb(230, 80, 70));
            DrawDiagramCircle(g, fcx - 32, fcy, "X", _focusedSlot == ProfileEditorSlot.ButtonX, Color.FromArgb(70, 140, 240));
            DrawDiagramCircle(g, fcx, fcy - 32, "Y", _focusedSlot == ProfileEditorSlot.ButtonY, Color.FromArgb(240, 200, 60));

            // Botões Centrais
            DrawDiagramCircle(g, 262, 80, "G", false, Color.FromArgb(160, 120, 255));
            DrawDiagramBox(g, new RectangleF(216, 122, 38, 20), "Sel", _focusedSlot == ProfileEditorSlot.ButtonBack);
            DrawDiagramBox(g, new RectangleF(268, 122, 38, 20), "Sta", _focusedSlot == ProfileEditorSlot.ButtonStart);

            g.Restore(state);
        }

        private static void DrawDiagramBox(Graphics g, RectangleF rect, string text, bool highlighted)
        {
            using (var brush = new SolidBrush(highlighted ? BorderFocused : Color.FromArgb(52, 56, 66)))
            using (var pen = new Pen(highlighted ? Color.White : BorderIdle, 2f))
            using (var font = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (var textBrush = new SolidBrush(highlighted ? BgColor : TextPrimary))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.FillRectangle(brush, rect);
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                g.DrawString(text, font, textBrush, rect, sf);
            }
        }

        private static void DrawDiagramCircle(Graphics g, float cx, float cy, string text, bool highlighted, Color accent)
        {
            const float r = 16f;
            var rect = new RectangleF(cx - r, cy - r, r * 2, r * 2);
            using (var brush = new SolidBrush(highlighted ? accent : Color.FromArgb(52, 56, 66)))
            using (var pen = new Pen(highlighted ? Color.White : accent, 2.5f))
            using (var font = new Font("Segoe UI", 11f, FontStyle.Bold))
            using (var textBrush = new SolidBrush(highlighted ? BgColor : TextPrimary))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.FillEllipse(brush, rect);
                g.DrawEllipse(pen, rect);
                g.DrawString(text, font, textBrush, rect, sf);
            }
        }

        private static void DrawDiagramStick(Graphics g, float cx, float cy, string label, bool stickHighlighted, bool clickHighlighted)
        {
            const float outerR = 36f;
            const float innerR = 18f;
            using (var outerBrush = new SolidBrush(stickHighlighted ? FocusBg : Color.FromArgb(44, 48, 58)))
            using (var outerPen = new Pen(stickHighlighted ? BorderFocused : BorderIdle, stickHighlighted ? 3.5f : 2f))
            using (var innerBrush = new SolidBrush(clickHighlighted ? AlertAmber : (stickHighlighted ? BorderFocused : TextPrimary)))
            using (var font = new Font("Segoe UI", 11f, FontStyle.Bold))
            using (var textBrush = new SolidBrush(BgColor))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.FillEllipse(outerBrush, cx - outerR, cy - outerR, outerR * 2, outerR * 2);
                g.DrawEllipse(outerPen, cx - outerR, cy - outerR, outerR * 2, outerR * 2);

                var innerRect = new RectangleF(cx - innerR, cy - innerR, innerR * 2, innerR * 2);
                g.FillEllipse(innerBrush, innerRect);
                g.DrawString(label, font, textBrush, innerRect, sf);
            }
        }

        private void DrawCenterActionButtons(Graphics g)
        {
            var saveRect = _slotBounds[ProfileEditorSlot.FooterSave];
            bool saveFocused = _focusedSlot == ProfileEditorSlot.FooterSave;
            DrawInteractiveSlotBox(g, saveRect, saveFocused, Color.FromArgb(46, 125, 80));
            using (var font = new Font("Segoe UI", 8.2f, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.White))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(Strings.ProfileBtnSave, font, brush, saveRect, sf);
            }

            var backRect = _slotBounds[ProfileEditorSlot.FooterBack];
            bool backFocused = _focusedSlot == ProfileEditorSlot.FooterBack;
            DrawInteractiveSlotBox(g, backRect, backFocused, CardBg);
            using (var font = new Font("Segoe UI", 8.2f, FontStyle.Bold))
            using (var brush = new SolidBrush(TextPrimary))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(Strings.ProfileBtnBack, font, brush, backRect, sf);
            }
        }

        private static void DrawFooterLegend(Graphics g)
        {
            using (var bg = new SolidBrush(HeaderBg))
            using (var linePen = new Pen(BorderSubtle, 1f))
            using (var font = new Font("Segoe UI", 8.2f, FontStyle.Bold))
            using (var brush = new SolidBrush(TextSecondary))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.FillRectangle(bg, 0, 502, LogicalWidth, 38);
                g.DrawLine(linePen, 0, 502, LogicalWidth, 502);
                g.DrawString(Strings.ProfileFooterLegend, font, brush, new RectangleF(0, 502, LogicalWidth, 38), sf);
            }
        }

        private void DrawPresetOverlayMenu(Graphics g)
        {
            using (var veil = new SolidBrush(Color.FromArgb(185, 14, 16, 22)))
            {
                g.FillRectangle(veil, 0, 0, LogicalWidth, LogicalHeight);
            }

            int count = _presetMenuItems.Count;
            float itemH = 44f;
            float boxW = 420f;
            float boxH = Math.Min(440f, 76f + count * (itemH + 6f));
            float boxX = (LogicalWidth - boxW) / 2f;
            float boxY = (LogicalHeight - boxH) / 2f;

            _presetMenuBoxBounds = new RectangleF(boxX, boxY, boxW, boxH);
            _presetMenuItemBounds.Clear();

            using (var bg = new SolidBrush(HeaderBg))
            using (var pen = new Pen(BorderFocused, 2f))
            {
                g.FillRectangle(bg, _presetMenuBoxBounds);
                g.DrawRectangle(pen, boxX, boxY, boxW, boxH);
            }

            using (var titleFont = new Font("Segoe UI", 10f, FontStyle.Bold))
            using (var titleBrush = new SolidBrush(TextPrimary))
            {
                g.DrawString(Strings.ProfileMenuSectionPresets, titleFont, titleBrush, boxX + 16, boxY + 12);
            }

            float curY = boxY + 40f;
            for (int i = 0; i < count; i++)
            {
                var item = _presetMenuItems[i];
                var itemRect = new RectangleF(boxX + 14f, curY, boxW - 28f, itemH);
                _presetMenuItemBounds.Add(itemRect);

                bool isFocused = (i == _presetMenuFocusedIndex);
                using (var itemBg = new SolidBrush(isFocused ? FocusBg : CardBg))
                using (var itemPen = new Pen(isFocused ? BorderFocused : BorderSubtle, isFocused ? 2f : 1f))
                {
                    g.FillRectangle(itemBg, itemRect);
                    g.DrawRectangle(itemPen, itemRect.X, itemRect.Y, itemRect.Width, itemRect.Height);
                }

                using (var mainFont = new Font("Segoe UI", 9f, FontStyle.Bold))
                using (var subFont = new Font("Segoe UI", 7.6f))
                using (var mainBrush = new SolidBrush(isFocused ? Color.White : TextPrimary))
                using (var subBrush = new SolidBrush(isFocused ? BorderFocused : TextSecondary))
                {
                    g.DrawString(item.Title, mainFont, mainBrush, itemRect.X + 10, itemRect.Y + 5);
                    g.DrawString(item.Subtitle, subFont, subBrush, itemRect.X + 10, itemRect.Y + 23);
                }

                curY += itemH + 6f;
                if (curY + itemH > boxY + boxH - 28f) break;
            }

            using (var hintFont = new Font("Segoe UI", 8f, FontStyle.Bold))
            using (var hintBrush = new SolidBrush(TextSecondary))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(
                    Strings.ProfileMenuCloseHint,
                    hintFont,
                    hintBrush,
                    new RectangleF(boxX, boxY + boxH - 26f, boxW, 22f),
                    sf);
            }
        }

        private void DrawHandheldCenterDiagram(Graphics g)
        {
            var state = g.Save();
            float targetCenterX = 310f;
            float targetCenterY = 245f;
            float diagScale = 0.34f;

            g.TranslateTransform(targetCenterX - (620f * diagScale) / 2f, targetCenterY - (346f * diagScale) / 2f);
            g.ScaleTransform(diagScale, diagScale);

            // Carcaça do Handheld
            var rect = new RectangleF(24, 28, 572, 258);
            using (var path = CreateRoundedRectanglePath(rect, 22f))
            using (var brush = new SolidBrush(CardBg))
            using (var pen = new Pen(BorderIdle, 3f))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }

            // Tela Central
            var screenRect = new RectangleF(186, 44, 248, 192);
            using (var path = CreateRoundedRectanglePath(screenRect, 8f))
            using (var brush = new SolidBrush(Color.FromArgb(16, 18, 23)))
            using (var pen = new Pen(BorderSubtle, 2f))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }

            using (var font = new Font("Segoe UI", 12f, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.FromArgb(90, 96, 115)))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString("STEAM DECK", font, brush, screenRect, sf);
            }

            // Gatilhos e Bumpers
            DrawDiagramBox(g, new RectangleF(55, 6, 85, 20), "LT", _focusedSlot == ProfileEditorSlot.ButtonLT);
            DrawDiagramBox(g, new RectangleF(480, 6, 85, 20), "RT", _focusedSlot == ProfileEditorSlot.ButtonRT);
            DrawDiagramBox(g, new RectangleF(55, 30, 85, 18), "LB", _focusedSlot == ProfileEditorSlot.ButtonLB);
            DrawDiagramBox(g, new RectangleF(480, 30, 85, 18), "RB", _focusedSlot == ProfileEditorSlot.ButtonRB);

            // Sticks
            bool lStickFocused = _focusedSlot == ProfileEditorSlot.LeftStickMode ||
                                 _focusedSlot == ProfileEditorSlot.LeftStickUp ||
                                 _focusedSlot == ProfileEditorSlot.LeftStickDown ||
                                 _focusedSlot == ProfileEditorSlot.LeftStickLeft ||
                                 _focusedSlot == ProfileEditorSlot.LeftStickRight ||
                                 _focusedSlot == ProfileEditorSlot.LeftStickAdditional ||
                                 _focusedSlot == ProfileEditorSlot.LeftStickClick;

            bool rStickFocused = _focusedSlot == ProfileEditorSlot.RightStickMode ||
                                 _focusedSlot == ProfileEditorSlot.RightStickUp ||
                                 _focusedSlot == ProfileEditorSlot.RightStickDown ||
                                 _focusedSlot == ProfileEditorSlot.RightStickLeft ||
                                 _focusedSlot == ProfileEditorSlot.RightStickRight ||
                                 _focusedSlot == ProfileEditorSlot.RightStickAdditional ||
                                 _focusedSlot == ProfileEditorSlot.RightStickClick;

            DrawDiagramStick(g, 100, 92, "L", lStickFocused, _focusedSlot == ProfileEditorSlot.LeftStickClick);
            DrawDiagramStick(g, 520, 168, "R", rStickFocused, _focusedSlot == ProfileEditorSlot.RightStickClick);

            // D-Pad
            const float s = 20f;
            float dcx = 100f, dcy = 168f;
            DrawDiagramBox(g, new RectangleF(dcx - s / 2, dcy - s * 1.5f, s, s), "▲", _focusedSlot == ProfileEditorSlot.DPadUp);
            DrawDiagramBox(g, new RectangleF(dcx - s / 2, dcy + s * 0.5f, s, s), "▼", _focusedSlot == ProfileEditorSlot.DPadDown);
            DrawDiagramBox(g, new RectangleF(dcx - s * 1.5f, dcy - s / 2, s, s), "◄", _focusedSlot == ProfileEditorSlot.DPadLeft);
            DrawDiagramBox(g, new RectangleF(dcx + s * 0.5f, dcy - s / 2, s, s), "►", _focusedSlot == ProfileEditorSlot.DPadRight);

            // Botões Faciais
            float fcx = 520f, fcy = 92f;
            DrawDiagramCircle(g, fcx, fcy + 28, "A", _focusedSlot == ProfileEditorSlot.ButtonA, Color.FromArgb(90, 200, 90));
            DrawDiagramCircle(g, fcx + 28, fcy, "B", _focusedSlot == ProfileEditorSlot.ButtonB, Color.FromArgb(230, 80, 70));
            DrawDiagramCircle(g, fcx - 28, fcy, "X", _focusedSlot == ProfileEditorSlot.ButtonX, Color.FromArgb(70, 140, 240));
            DrawDiagramCircle(g, fcx, fcy - 28, "Y", _focusedSlot == ProfileEditorSlot.ButtonY, Color.FromArgb(240, 200, 60));

            // Trackpads
            DrawDiagramTrackpad(g, new RectangleF(72, 212, 56, 56), "L-PAD");
            DrawDiagramTrackpad(g, new RectangleF(492, 212, 56, 56), "R-PAD");

            // Botões Centrais
            DrawDiagramBox(g, new RectangleF(150, 72, 26, 18), "Sel", _focusedSlot == ProfileEditorSlot.ButtonBack);
            DrawDiagramBox(g, new RectangleF(444, 72, 26, 18), "Sta", _focusedSlot == ProfileEditorSlot.ButtonStart);

            g.Restore(state);
        }

        private static void DrawDiagramTrackpad(Graphics g, RectangleF rect, string label)
        {
            using (var path = CreateRoundedRectanglePath(rect, 6f))
            using (var brush = new SolidBrush(Color.FromArgb(44, 48, 58)))
            using (var pen = new Pen(BorderIdle, 2f))
            using (var font = new Font("Segoe UI", 8f, FontStyle.Bold))
            using (var textBrush = new SolidBrush(TextSecondary))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
                g.DrawString(label, font, textBrush, rect, sf);
            }
        }

        private void DrawTrackpadPlaceholderCards(Graphics g)
        {
            var page = _workingProfile.ActivePage;
            var leftRect = new RectangleF(12, 428, 178, 64);
            var rightRect = new RectangleF(430, 428, 178, 64);

            DrawTrackpadInteractiveCard(g, leftRect, Strings.ProfileCardLeftTrackpad, page.LeftTrackpad, _focusedSlot == ProfileEditorSlot.LeftTrackpad);
            DrawTrackpadInteractiveCard(g, rightRect, Strings.ProfileCardRightTrackpad, page.RightTrackpad, _focusedSlot == ProfileEditorSlot.RightTrackpad);
        }

        private static void DrawTrackpadInteractiveCard(Graphics g, RectangleF rect, string title, TrackpadConfig config, bool isFocused)
        {
            Color fill = isFocused ? FocusBg : CardBg;
            Color border = isFocused ? BorderFocused : BorderSubtle;
            float penWidth = isFocused ? 2f : 1f;

            using (var bg = new SolidBrush(fill))
            using (var pen = new Pen(border, penWidth))
            using (var headerFont = new Font("Segoe UI", 7.8f, FontStyle.Bold))
            using (var headerBrush = new SolidBrush(isFocused ? TextPrimary : BorderFocused))
            {
                g.FillRectangle(bg, rect);
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                g.DrawString(title, headerFont, headerBrush, rect.X + 8, rect.Y + 4);
            }

            TrackpadMode mode = config?.Mode ?? TrackpadMode.Disabled;
            string modeName = GetTrackpadModeName(mode);
            Color modeColor = GetTrackpadModeColor(mode);

            // Badge do Modo no topo direito do card
            var badgeRect = new RectangleF(rect.Right - 90, rect.Y + 4, 82, 14);
            using (var path = CreateRoundedRectanglePath(badgeRect, 3f))
            using (var badgeBg = new SolidBrush(Color.FromArgb(40, modeColor)))
            using (var badgePen = new Pen(modeColor, 1f))
            using (var badgeFont = new Font("Segoe UI", 6.8f, FontStyle.Bold))
            using (var badgeBrush = new SolidBrush(modeColor))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.FillPath(badgeBg, path);
                g.DrawPath(badgePen, path);
                g.DrawString(modeName, badgeFont, badgeBrush, badgeRect, sf);
            }

            // Resumo dinâmico dos parâmetros
            string summary = GetTrackpadSummary(config);
            using (var sumFont = new Font("Segoe UI", 7.2f))
            using (var sumBrush = new SolidBrush(TextSecondary))
            {
                g.DrawString(summary, sumFont, sumBrush, rect.X + 8, rect.Y + 22);
            }

            // Dica de Ação no rodapé do card
            using (var hintFont = new Font("Segoe UI", 7.0f, FontStyle.Bold))
            using (var hintBrush = new SolidBrush(isFocused ? ActiveGreen : TextSecondary))
            {
                g.DrawString(Strings.ProfileTrackpadConfigureHint, hintFont, hintBrush, rect.X + 8, rect.Y + 44);
            }
        }

        private static string GetTrackpadModeName(TrackpadMode mode)
        {
            switch (mode)
            {
                case TrackpadMode.MouseTrackball: return "Mouse / Ball";
                case TrackpadMode.Scrollwheel: return "Scrollwheel";
                case TrackpadMode.DPad: return "D-Pad";
                case TrackpadMode.SingleButton: return "Botão Único";
                case TrackpadMode.Disabled: return "Desativado";
                default: return mode.ToString();
            }
        }

        private static Color GetTrackpadModeColor(TrackpadMode mode)
        {
            switch (mode)
            {
                case TrackpadMode.MouseTrackball: return Color.FromArgb(80, 200, 255);
                case TrackpadMode.Scrollwheel: return AlertAmber;
                case TrackpadMode.DPad: return ActiveGreen;
                case TrackpadMode.SingleButton: return Color.FromArgb(180, 130, 255);
                case TrackpadMode.Disabled: return Color.FromArgb(140, 140, 140);
                default: return TextPrimary;
            }
        }

        private static string GetTrackpadSummary(TrackpadConfig config)
        {
            if (config == null || config.Mode == TrackpadMode.Disabled)
                return "Desativado neste perfil";

            switch (config.Mode)
            {
                case TrackpadMode.MouseTrackball:
                    return $"Sens: {config.Sensitivity}% • Inércia: {config.Friction}%";
                case TrackpadMode.Scrollwheel:
                    string roll = config.InvertScroll ? "Rolagem Natural" : "Padrão";
                    return $"Sens: {config.Sensitivity}% • {roll}";
                case TrackpadMode.DPad:
                    return $"Zona Morta: {config.DeadzonePercent}%";
                case TrackpadMode.SingleButton:
                    string act = config.PressActions.Count > 0 ? "Ação Mapeada" : "Sem Ação";
                    return $"Botão • {act}";
                default:
                    return "Ativo";
            }
        }

        private void DrawRearButtonsRow(Graphics g)
        {
            var page = _workingProfile.ActivePage;

            using (var titleFont = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (var titleBrush = new SolidBrush(TextSecondary))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(Strings.ProfileRearButtonsTitle, titleFont, titleBrush, new RectangleF(196, 360, 228, 16), sf);
            }

            DrawRearButtonSlot(g, ProfileEditorSlot.ButtonL4, "L4", _settings.L4HardwareRemap, page.GetActions(ControllerButton.L4));
            DrawRearButtonSlot(g, ProfileEditorSlot.ButtonL5, "L5", _settings.L5HardwareRemap, page.GetActions(ControllerButton.L5));
            DrawRearButtonSlot(g, ProfileEditorSlot.ButtonR5, "R5", _settings.R5HardwareRemap, page.GetActions(ControllerButton.R5));
            DrawRearButtonSlot(g, ProfileEditorSlot.ButtonR4, "R4", _settings.R4HardwareRemap, page.GetActions(ControllerButton.R4));
        }

        private void DrawRearButtonSlot(Graphics g, ProfileEditorSlot slot, string label, SteamDeckHardwareRemap hwRemap, List<InputAction> actions)
        {
            if (!_slotBounds.TryGetValue(slot, out var rect)) return;
            bool isFocused = _focusedSlot == slot;
            bool hasActions = actions != null && actions.Count > 0;
            bool hasHwRemap = hwRemap != SteamDeckHardwareRemap.None;

            Color bgColor = isFocused ? FocusBg : (hasActions || hasHwRemap ? Color.FromArgb(30, 48, 40) : CardBg);
            DrawInteractiveSlotBox(g, rect, isFocused, bgColor);

            using (var titleFont = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (var titleBrush = new SolidBrush(isFocused ? BorderFocused : TextSecondary))
            {
                g.DrawString(label, titleFont, titleBrush, rect.X + 4, rect.Y + 2);
            }

            string displayVal;
            Color valColor;
            if (hasHwRemap)
            {
                displayVal = "HW:" + FormatHardwareRemapShort(hwRemap);
                valColor = AlertAmber;
            }
            else if (hasActions)
            {
                displayVal = InputAction.FormatComboDisplay(actions);
                valColor = ActiveGreen;
            }
            else
            {
                displayVal = "-";
                valColor = TextSecondary;
            }

            var textRect = new RectangleF(rect.X + 2, rect.Y + 14, rect.Width - 4, 14);
            using (var valFont = new Font("Segoe UI", 7.2f, FontStyle.Bold))
            using (var valBrush = new SolidBrush(valColor))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                g.DrawString(displayVal, valFont, valBrush, textRect, sf);
            }
        }

        private static string FormatHardwareRemapShort(SteamDeckHardwareRemap remap)
        {
            switch (remap)
            {
                case SteamDeckHardwareRemap.LeftShoulder: return "LB";
                case SteamDeckHardwareRemap.RightShoulder: return "RB";
                case SteamDeckHardwareRemap.LeftTrigger: return "LT";
                case SteamDeckHardwareRemap.RightTrigger: return "RT";
                case SteamDeckHardwareRemap.LeftStickClick: return "L3";
                case SteamDeckHardwareRemap.RightStickClick: return "R3";
                default: return remap.ToString();
            }
        }

        private static GraphicsPath CreateRoundedRectanglePath(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2f;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static void DrawCardFrame(Graphics g, RectangleF rect, string title)
        {
            using (var bg = new SolidBrush(CardBg))
            using (var pen = new Pen(BorderSubtle, 1f))
            using (var headerFont = new Font("Segoe UI", 7.8f, FontStyle.Bold))
            using (var headerBrush = new SolidBrush(BorderFocused))
            {
                g.FillRectangle(bg, rect);
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                g.DrawString(title, headerFont, headerBrush, rect.X + 8, rect.Y + 4);
            }
        }

        private static void DrawLabeledMappingRow(
            Graphics g,
            RectangleF rect,
            string label,
            string valueText,
            bool isFocused,
            bool isModeBadge = false,
            bool highlightNonEmpty = false)
        {
            Color fill = isFocused
                ? FocusBg
                : (isModeBadge ? Color.FromArgb(30, 48, 72) : InputBg);

            DrawInteractiveSlotBox(g, rect, isFocused, fill);

            using (var lblFont = new Font("Segoe UI", 7.8f, FontStyle.Bold))
            using (var lblBrush = new SolidBrush(TextSecondary))
            {
                g.DrawString(label, lblFont, lblBrush, rect.X + 6, rect.Y + 4);
            }

            bool isNone = string.Equals(valueText, Strings.ProfileActionNone, StringComparison.OrdinalIgnoreCase);
            Color valColor = isNone
                ? Color.FromArgb(110, 118, 132)
                : (isModeBadge ? BorderFocused : (highlightNonEmpty ? ActiveGreen : TextPrimary));

            var valRect = new RectangleF(rect.X + 58, rect.Y + 2, rect.Width - 62, rect.Height - 4);
            using (var valFont = new Font("Segoe UI", 8f, FontStyle.Bold))
            using (var valBrush = new SolidBrush(valColor))
            using (var sf = new StringFormat
            {
                Alignment = StringAlignment.Far,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            })
            {
                g.DrawString(valueText, valFont, valBrush, valRect, sf);
            }
        }

        private static void DrawCompactMappingBox(
            Graphics g,
            RectangleF rect,
            string label,
            string valueText,
            bool isFocused)
        {
            DrawInteractiveSlotBox(g, rect, isFocused, isFocused ? FocusBg : InputBg);

            using (var lblFont = new Font("Segoe UI", 7.2f, FontStyle.Bold))
            using (var lblBrush = new SolidBrush(TextSecondary))
            {
                g.DrawString(label, lblFont, lblBrush, rect.X + 4, rect.Y + 4);
            }

            bool isNone = string.Equals(valueText, Strings.ProfileActionNone, StringComparison.OrdinalIgnoreCase);
            var valRect = new RectangleF(rect.X + 34, rect.Y + 2, rect.Width - 38, rect.Height - 4);
            using (var valFont = new Font("Segoe UI", 7.8f, FontStyle.Bold))
            using (var valBrush = new SolidBrush(isNone ? Color.FromArgb(110, 118, 132) : TextPrimary))
            using (var sf = new StringFormat
            {
                Alignment = StringAlignment.Far,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            })
            {
                g.DrawString(valueText, valFont, valBrush, valRect, sf);
            }
        }

        private static void DrawInteractiveSlotBox(Graphics g, RectangleF rect, bool isFocused, Color bgColor)
        {
            using (var bg = new SolidBrush(isFocused ? FocusBg : bgColor))
            using (var pen = new Pen(isFocused ? BorderFocused : BorderSubtle, isFocused ? 2f : 1f))
            {
                g.FillRectangle(bg, rect);
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
            }
        }

        private static string FormatStickMode(StickMode mode)
        {
            switch (mode)
            {
                case StickMode.DirectionalKeys: return Strings.ProfileStickModeDirectional;
                case StickMode.MouseMove: return Strings.ProfileStickModeMouse;
                default: return Strings.ProfileStickModeDisabled;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            EnsureLogicalBoundsPopulated();
            PointF pt = ClientToLogical(e.Location);

            if (_isPresetMenuOpen)
            {
                for (int i = 0; i < _presetMenuItemBounds.Count; i++)
                {
                    if (_presetMenuItemBounds[i].Contains(pt) && _presetMenuFocusedIndex != i)
                    {
                        _presetMenuFocusedIndex = i;
                        Invalidate();
                        break;
                    }
                }
                return;
            }

            if (_draggingSlider.HasValue && e.Button == MouseButtons.Left)
            {
                UpdateSliderFromLogicalX(_draggingSlider.Value, pt.X);
                return;
            }

            foreach (var kvp in _slotBounds)
            {
                if (!IsSlotVisible(kvp.Key)) continue;
                if (kvp.Value.Contains(pt))
                {
                    if (_focusedSlot != kvp.Key)
                    {
                        _focusedSlot = kvp.Key;
                        Invalidate();
                    }
                    break;
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            EnsureLogicalBoundsPopulated();
            PointF pt = ClientToLogical(e.Location);

            if (_isPresetMenuOpen)
            {
                if (e.Button == MouseButtons.Right)
                {
                    _isPresetMenuOpen = false;
                    Invalidate();
                    return;
                }

                for (int i = 0; i < _presetMenuItemBounds.Count; i++)
                {
                    if (_presetMenuItemBounds[i].Contains(pt))
                    {
                        _presetMenuFocusedIndex = i;
                        ExecuteSelectedPresetMenuItem();
                        return;
                    }
                }

                if (!_presetMenuBoxBounds.Contains(pt))
                {
                    _isPresetMenuOpen = false;
                    Invalidate();
                }
                return;
            }

            foreach (var kvp in _slotBounds)
            {
                if (!IsSlotVisible(kvp.Key)) continue;
                if (kvp.Value.Contains(pt))
                {
                    _focusedSlot = kvp.Key;

                    if (e.Button == MouseButtons.Right && IsActionSlot(_focusedSlot))
                    {
                        ClearSlot(_focusedSlot);
                        return;
                    }

                    if (e.Button == MouseButtons.Left)
                    {
                        if (IsSliderSlot(_focusedSlot))
                        {
                            _draggingSlider = _focusedSlot;
                            UpdateSliderFromLogicalX(_focusedSlot, pt.X);
                        }
                        else
                        {
                            ActivateFocusedSlot();
                        }
                    }
                    return;
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _draggingSlider = null;
        }

        private void UpdateSliderFromLogicalX(ProfileEditorSlot sliderSlot, float logicalX)
        {
            if (!_slotBounds.TryGetValue(sliderSlot, out var rect)) return;
            float ratio = Math.Max(0f, Math.Min(1f, (logicalX - rect.X) / Math.Max(1f, rect.Width)));

            switch (sliderSlot)
            {
                case ProfileEditorSlot.MouseSensitivity:
                    _workingProfile.ActivePage.MouseSensitivity = (int)Math.Round(1 + ratio * 99);
                    break;

                case ProfileEditorSlot.DeadzoneSlider:
                    _workingProfile.UseGlobalDeadzone = false;
                    _workingProfile.CustomDeadzonePercent = (int)Math.Round(5 + ratio * 45);
                    break;

                case ProfileEditorSlot.TriggerSlider:
                    _workingProfile.UseGlobalTriggerThreshold = false;
                    _workingProfile.CustomTriggerThresholdPercent = (int)Math.Round(5 + ratio * 85);
                    break;
            }

            Invalidate();
        }
    }
}
