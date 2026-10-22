using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using ConsoleMode.GamepadCompanion.Core.Interfaces;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.Engine;
using ConsoleMode.GamepadCompanion.Hardware;
using ConsoleMode.GamepadCompanion.Profiles;
using ConsoleMode.GamepadCompanion.UI.Controls;
using Xunit;

namespace ConsoleMode.GamepadCompanion.Tests
{
    public sealed class ProfileSystemPhase1Tests
    {
        private sealed class RecordingInputSimulator : IInputSimulator
        {
            public enum EventType
            {
                KeyDown,
                KeyUp,
                MouseDown,
                MouseUp,
                MouseMove,
                MouseWheel
            }

            public struct InputRecord
            {
                public EventType Type;
                public VirtualKey Key;
                public MouseButton Button;
                public int Dx;
                public int Dy;
                public int Delta;
            }

            public List<InputRecord> Events { get; } = new List<InputRecord>();
            public HashSet<VirtualKey> HeldKeys { get; } = new HashSet<VirtualKey>();
            public HashSet<MouseButton> HeldMouseButtons { get; } = new HashSet<MouseButton>();

            public void KeyDown(VirtualKey key)
            {
                Events.Add(new InputRecord { Type = EventType.KeyDown, Key = key });
                HeldKeys.Add(key);
            }

            public void KeyUp(VirtualKey key)
            {
                Events.Add(new InputRecord { Type = EventType.KeyUp, Key = key });
                HeldKeys.Remove(key);
            }

            public void MouseButtonDown(MouseButton button)
            {
                Events.Add(new InputRecord { Type = EventType.MouseDown, Button = button });
                HeldMouseButtons.Add(button);
            }

            public void MouseButtonUp(MouseButton button)
            {
                Events.Add(new InputRecord { Type = EventType.MouseUp, Button = button });
                HeldMouseButtons.Remove(button);
            }

            public void MouseMoveRelative(int dx, int dy)
            {
                Events.Add(new InputRecord { Type = EventType.MouseMove, Dx = dx, Dy = dy });
            }

            public void MouseWheel(int delta)
            {
                Events.Add(new InputRecord { Type = EventType.MouseWheel, Delta = delta });
            }
        }

        private sealed class MockWindowTracker : IWindowTracker
        {
            public bool IsGameFocused { get; set; }
            public bool IsCompanionFocused { get; set; }
            public string ActiveProcessName { get; set; } = string.Empty;
            public IntPtr ActiveWindowHandle { get; set; } = IntPtr.Zero;
            public GameEntry ActiveGame { get; set; }

            public event Action<bool> FocusChanged;
            public event Action<IntPtr> ActiveWindowChanged;
            public event Action<GameEntry> ActiveGameChanged;

            public void CheckActiveWindow() { }
            public void RegisterGameExecutable(string executableName) { }
            public void RegisterGame(GameEntry game) { }
            public void SetGames(IEnumerable<GameEntry> games) { }

            public void TriggerWindowChange(IntPtr hwnd, bool isGame, GameEntry game)
            {
                ActiveWindowHandle = hwnd;
                IsGameFocused = isGame;
                ActiveGame = game;
                FocusChanged?.Invoke(isGame);
                ActiveWindowChanged?.Invoke(hwnd);
                ActiveGameChanged?.Invoke(game);
            }
        }

        private sealed class TestGamepadService : IGamepadService
        {
            public int SelectedSlot { get; set; } = 0;
            public int ActiveSlot => 0;
            public GamepadState CurrentState { get; set; }
            public event Action<GamepadState> StateUpdated;
            public event Action<int, bool> ConnectionChanged { add { } remove { } }
            public bool IsSlotConnected(int slot) => slot == 0;
            public void Start() { }
            public void Stop() { }
            public void Dispose() { }

            public void EmitState(GamepadState state)
            {
                CurrentState = state;
                StateUpdated?.Invoke(state);
            }
        }

        // =========================================================================
        // 1. MODELOS, COMBOS E SERIALIZAÇÃO NO GAMES.INI
        // =========================================================================

        [Fact]
        public void InputAction_FromKey_And_DisplayText()
        {
            var actionSpace = InputAction.FromKey(VirtualKey.Space);
            Assert.Equal(InputActionType.Key, actionSpace.Type);
            Assert.Equal("Espaço", actionSpace.GetDisplayText());

            var actionF9 = InputAction.FromKey(VirtualKey.F9);
            Assert.Equal("F9", actionF9.GetDisplayText());

            var actionMouseR = InputAction.FromMouseButton(MouseButton.Right);
            Assert.Equal(InputActionType.MouseButton, actionMouseR.Type);
            Assert.Equal("Mouse R", actionMouseR.GetDisplayText());
        }

        [Fact]
        public void InputAction_Serialize_And_Deserialize_Combo()
        {
            var combo = new List<InputAction>
            {
                InputAction.FromKey(VirtualKey.LeftControl),
                InputAction.FromKey(VirtualKey.LeftShift),
                InputAction.FromKey(VirtualKey.S)
            };

            string serialized = InputAction.SerializeCombo(combo);
            Assert.Equal("Key:LeftControl+Key:LeftShift+Key:S", serialized);

            var deserialized = InputAction.DeserializeCombo(serialized);
            Assert.Equal(3, deserialized.Count);
            Assert.Equal(VirtualKey.LeftControl, deserialized[0].Key);
            Assert.Equal(VirtualKey.LeftShift, deserialized[1].Key);
            Assert.Equal(VirtualKey.S, deserialized[2].Key);

            Assert.Equal("L-Ctrl + L-Shift + S", InputAction.FormatComboDisplay(deserialized));
        }

        [Fact]
        public void GameProfile_Serialization_RoundTrip_Through_Ini()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"test_profile_ini_{Guid.NewGuid():N}.ini");
            try
            {
                var profile = new GameProfile
                {
                    Name = "Custom Test Profile",
                    UseGlobalDeadzone = false,
                    CustomDeadzonePercent = 18,
                    UseGlobalTriggerThreshold = false,
                    CustomTriggerThresholdPercent = 35
                };

                var page = profile.ActivePage;
                page.MouseSensitivity = 70;
                page.LeftStick.Mode = StickMode.DirectionalKeys;
                page.LeftStick.Up = new List<InputAction> { InputAction.FromKey(VirtualKey.W) };
                page.LeftStick.AdditionalActions = new List<InputAction> { InputAction.FromKey(VirtualKey.F9) };
                page.RightStick.Mode = StickMode.MouseMove;
                page.RightStick.AdditionalActions = new List<InputAction> { InputAction.FromMouseButton(MouseButton.Right) };

                page.SetActions(ControllerButton.A, new[] { InputAction.FromKey(VirtualKey.Space), InputAction.FromKey(VirtualKey.F9) });
                page.SetActions(ControllerButton.B, new[] { InputAction.FromKey(VirtualKey.D3) });
                page.SetActions(ControllerButton.Back, new[] { InputAction.FromSpecial(CompanionActionType.CenterCursor) });

                var ini = new IniFile();
                ProfileSerializer.SerializeToIni(ini, "Game_custom1", profile);
                ini.Save(tempFile);

                var loadedIni = new IniFile();
                loadedIni.Load(tempFile);
                var loadedProfile = ProfileSerializer.DeserializeFromIni(loadedIni, "Game_custom1");

                Assert.NotNull(loadedProfile);
                Assert.Equal("Custom Test Profile", loadedProfile.Name);
                Assert.False(loadedProfile.UseGlobalDeadzone);
                Assert.Equal(18, loadedProfile.CustomDeadzonePercent);
                Assert.False(loadedProfile.UseGlobalTriggerThreshold);
                Assert.Equal(35, loadedProfile.CustomTriggerThresholdPercent);

                var loadedPage = loadedProfile.ActivePage;
                Assert.Equal(70, loadedPage.MouseSensitivity);
                Assert.Equal(StickMode.DirectionalKeys, loadedPage.LeftStick.Mode);
                Assert.Single(loadedPage.LeftStick.Up);
                Assert.Equal(VirtualKey.W, loadedPage.LeftStick.Up[0].Key);
                Assert.Single(loadedPage.LeftStick.AdditionalActions);
                Assert.Equal(VirtualKey.F9, loadedPage.LeftStick.AdditionalActions[0].Key);

                Assert.Equal(StickMode.MouseMove, loadedPage.RightStick.Mode);
                Assert.Single(loadedPage.RightStick.AdditionalActions);
                Assert.Equal(MouseButton.Right, loadedPage.RightStick.AdditionalActions[0].MouseButton);

                var aActions = loadedPage.GetActions(ControllerButton.A);
                Assert.Equal(2, aActions.Count);
                Assert.Equal(VirtualKey.Space, aActions[0].Key);
                Assert.Equal(VirtualKey.F9, aActions[1].Key);

                var backActions = loadedPage.GetActions(ControllerButton.Back);
                Assert.Single(backActions);
                Assert.Equal(CompanionActionType.CenterCursor, backActions[0].SpecialAction);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void GameRepository_LoadsDefaultTurtleWoW_WithWowClassicPreset()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"test_repo_wow_{Guid.NewGuid():N}.ini");
            try
            {
                var repo = new GameRepository(tempFile);
                var games = repo.GetAll();

                Assert.Single(games);
                var turtle = games[0];
                Assert.Equal("turtle-wow", turtle.Id);
                Assert.True(turtle.EnableCompanionInput);
                Assert.NotNull(turtle.Profile);
                Assert.Equal(GameProfilePresets.PresetWowClassic, turtle.Profile.Name);

                // Valida que o botão A contém Espaço + F9
                var aActions = turtle.Profile.ActivePage.GetActions(ControllerButton.A);
                Assert.Equal(2, aActions.Count);
                Assert.Equal(VirtualKey.Space, aActions[0].Key);
                Assert.Equal(VirtualKey.F9, aActions[1].Key);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void GameRepository_Persists_EnableCompanionInput_Flag()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"test_repo_toggle_{Guid.NewGuid():N}.ini");
            try
            {
                var repo = new GameRepository(tempFile);
                var game = new GameEntry
                {
                    Id = "game-disabled-input",
                    Name = "Native Gamepad Game",
                    MainExecutable = "Game.exe",
                    TargetPath = @"C:\Games\Game.exe",
                    EnableCompanionInput = false
                };

                repo.Save(game);

                var retrieved = repo.GetById("game-disabled-input");
                Assert.NotNull(retrieved);
                Assert.False(retrieved.EnableCompanionInput);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        // =========================================================================
        // 2. MOTOR DINÂMICO & UNIÃO DE ESTADOS POR FRAME (GamingProfile)
        // =========================================================================

        [Fact]
        public void GamingProfile_FrameStateUnion_F9_HeldUntilBothStickAndA_AreReleased()
        {
            var sim = new RecordingInputSimulator();
            var settings = new AppSettings { StickDeadzonePercent = 20, TriggerThresholdPercent = 20 };
            var profile = GameProfilePresets.CreateWowClassic();
            var gamingProfile = new GamingProfile(sim, settings, profile);

            // Frame 1: Analógico Esquerdo move para cima (Y = 20000, > deadzone de 20%)
            // Deve acionar W e F9
            var stateMoving = new GamepadState(
                isConnected: true,
                packetNumber: 1,
                buttons: GamepadButtons.None,
                leftTrigger: 0,
                rightTrigger: 0,
                leftThumbX: 0,
                leftThumbY: 20000,
                rightThumbX: 0,
                rightThumbY: 0
            );

            gamingProfile.Update(stateMoving, 0.016f);

            Assert.Contains(VirtualKey.W, sim.HeldKeys);
            Assert.Contains(VirtualKey.F9, sim.HeldKeys);

            int f9DownCount = sim.Events.FindAll(e => e.Type == RecordingInputSimulator.EventType.KeyDown && e.Key == VirtualKey.F9).Count;
            Assert.Equal(1, f9DownCount);

            // Frame 2: Enquanto ainda move o stick, aperta o Botão A (Espaço + F9)
            // F9 já está acionado, não deve duplicar KeyDown nem soltar!
            var stateMovingAndA = new GamepadState(
                isConnected: true,
                packetNumber: 2,
                buttons: GamepadButtons.A,
                leftTrigger: 0,
                rightTrigger: 0,
                leftThumbX: 0,
                leftThumbY: 20000,
                rightThumbX: 0,
                rightThumbY: 0
            );

            gamingProfile.Update(stateMovingAndA, 0.016f);

            Assert.Contains(VirtualKey.W, sim.HeldKeys);
            Assert.Contains(VirtualKey.Space, sim.HeldKeys);
            Assert.Contains(VirtualKey.F9, sim.HeldKeys);

            f9DownCount = sim.Events.FindAll(e => e.Type == RecordingInputSimulator.EventType.KeyDown && e.Key == VirtualKey.F9).Count;
            Assert.Equal(1, f9DownCount);
            Assert.Empty(sim.Events.FindAll(e => e.Type == RecordingInputSimulator.EventType.KeyUp && e.Key == VirtualKey.F9));

            // Frame 3: Solta o Botão A, mas o Stick continua se movendo!
            // Espaço deve soltar, MAS F9 deve continuar pressionado (união de estados por frame)!
            gamingProfile.Update(stateMoving, 0.016f);

            Assert.Contains(VirtualKey.W, sim.HeldKeys);
            Assert.DoesNotContain(VirtualKey.Space, sim.HeldKeys);
            Assert.Contains(VirtualKey.F9, sim.HeldKeys);
            Assert.Empty(sim.Events.FindAll(e => e.Type == RecordingInputSimulator.EventType.KeyUp && e.Key == VirtualKey.F9));

            // Frame 4: Agora o stick retorna ao neutro
            // F9 deve FINALMENTE ser solto com KeyUp!
            var stateNeutral = new GamepadState(true, 4, GamepadButtons.None, 0, 0, 0, 0, 0, 0);
            gamingProfile.Update(stateNeutral, 0.016f);

            Assert.DoesNotContain(VirtualKey.W, sim.HeldKeys);
            Assert.DoesNotContain(VirtualKey.F9, sim.HeldKeys);
            Assert.Single(sim.Events.FindAll(e => e.Type == RecordingInputSimulator.EventType.KeyUp && e.Key == VirtualKey.F9));
        }

        [Fact]
        public void GamingProfile_FrameStateUnion_RightClick_HeldUntilBothR3AndRightStick_AreReleased()
        {
            var sim = new RecordingInputSimulator();
            var settings = new AppSettings { StickDeadzonePercent = 20 };
            var profile = GameProfilePresets.CreateRpgRightClickCamera();
            var gamingProfile = new GamingProfile(sim, settings, profile);

            // Frame 1: R-Stick move para a direita (X = 18000, > deadzone)
            // No preset RPG, RightStick ativa AdditionalActions = Mouse:Right
            var stateMovingRStick = new GamepadState(
                isConnected: true,
                packetNumber: 1,
                buttons: GamepadButtons.None,
                leftTrigger: 0,
                rightTrigger: 0,
                leftThumbX: 0,
                leftThumbY: 0,
                rightThumbX: 18000,
                rightThumbY: 0
            );

            gamingProfile.Update(stateMovingRStick, 0.016f);

            Assert.Contains(MouseButton.Right, sim.HeldMouseButtons);
            int mouseDownCount = sim.Events.FindAll(e => e.Type == RecordingInputSimulator.EventType.MouseDown && e.Button == MouseButton.Right).Count;
            Assert.Equal(1, mouseDownCount);

            // Frame 2: Aperta R3 enquanto stick ainda se move
            var stateMovingAndR3 = new GamepadState(
                isConnected: true,
                packetNumber: 2,
                buttons: GamepadButtons.RightStick,
                leftTrigger: 0,
                rightTrigger: 0,
                leftThumbX: 0,
                leftThumbY: 0,
                rightThumbX: 18000,
                rightThumbY: 0
            );

            gamingProfile.Update(stateMovingAndR3, 0.016f);
            Assert.Contains(MouseButton.Right, sim.HeldMouseButtons);
            Assert.Empty(sim.Events.FindAll(e => e.Type == RecordingInputSimulator.EventType.MouseUp && e.Button == MouseButton.Right));

            // Frame 3: Solta R3, stick ainda se move
            gamingProfile.Update(stateMovingRStick, 0.016f);
            Assert.Contains(MouseButton.Right, sim.HeldMouseButtons);
            Assert.Empty(sim.Events.FindAll(e => e.Type == RecordingInputSimulator.EventType.MouseUp && e.Button == MouseButton.Right));

            // Frame 4: Stick para
            var stateNeutral = new GamepadState(true, 4, GamepadButtons.None, 0, 0, 0, 0, 0, 0);
            gamingProfile.Update(stateNeutral, 0.016f);
            Assert.DoesNotContain(MouseButton.Right, sim.HeldMouseButtons);
            Assert.Single(sim.Events.FindAll(e => e.Type == RecordingInputSimulator.EventType.MouseUp && e.Button == MouseButton.Right));
        }

        [Fact]
        public void GamingProfile_MouseDown_StrictlyPrecedes_MouseMoveRelative()
        {
            var sim = new RecordingInputSimulator();
            var settings = new AppSettings { StickDeadzonePercent = 20 };
            var profile = GameProfilePresets.CreateRpgRightClickCamera();
            var gamingProfile = new GamingProfile(sim, settings, profile);

            var stateMovingRStick = new GamepadState(
                isConnected: true,
                packetNumber: 1,
                buttons: GamepadButtons.None,
                leftTrigger: 0,
                rightTrigger: 0,
                leftThumbX: 0,
                leftThumbY: 0,
                rightThumbX: 20000,
                rightThumbY: 0
            );

            gamingProfile.Update(stateMovingRStick, 0.016f);

            int mouseDownIndex = sim.Events.FindIndex(e => e.Type == RecordingInputSimulator.EventType.MouseDown && e.Button == MouseButton.Right);
            int mouseMoveIndex = sim.Events.FindIndex(e => e.Type == RecordingInputSimulator.EventType.MouseMove);

            Assert.True(mouseDownIndex >= 0);
            Assert.True(mouseMoveIndex >= 0);
            Assert.True(mouseDownIndex < mouseMoveIndex, "REGRA ARQUITETURAL: MouseDown DEVE ocorrer ANTES de MouseMoveRelative");
        }

        // =========================================================================
        // 3. TOGGLE DE INPUT NO JOGO & PROFILE MANAGER
        // =========================================================================

        [Fact]
        public void ProfileManager_WhenGameHas_EnableCompanionInputFalse_RemainsSilentAndAllowsGuide()
        {
            var sim = new RecordingInputSimulator();
            var settings = new AppSettings { ClipCursorToGameWindow = false };
            var gamingProfile = new GamingProfile(sim, settings);
            var desktopProfile = new DesktopProfile();
            var tracker = new MockWindowTracker();
            var gamepad = new TestGamepadService();

            var gameWithInputDisabled = new GameEntry
            {
                Id = "native-pad-game",
                Name = "Game With Native Controller",
                MainExecutable = "Game.exe",
                EnableCompanionInput = false
            };

            bool guidePressed = false;

            using (var profileEngine = new ProfileEngine(gamepad, desktopProfile))
            using (var profileManager = new ProfileManager(profileEngine, gamingProfile, desktopProfile, tracker, gamepad, settings))
            {
                profileManager.GuidePressed += () => guidePressed = true;

                // Foca na janela do jogo com input desativado
                tracker.TriggerWindowChange(new IntPtr(5000), true, gameWithInputDisabled);

                Assert.False(profileManager.IsCompanionInputActive);
                Assert.Equal("Game With Native Controller (Desativado)", profileManager.ActiveProfileName);

                // Emite comandos de gameplay (ex: Botão A + analógico)
                var stateAction = new GamepadState(true, 1, GamepadButtons.A, 0, 0, 20000, 20000, 0, 0);
                gamepad.EmitState(stateAction);

                // Deve permanecer estritamente silencioso
                Assert.Empty(sim.HeldKeys);
                Assert.Empty(sim.Events);

                // Botão Guide no controle DEVE continuar funcionando mesmo com o input desativado!
                var stateGuide = new GamepadState(true, 2, GamepadButtons.Guide, 0, 0, 0, 0, 0, 0);
                gamepad.EmitState(stateGuide);

                Assert.True(guidePressed);
            }
        }

        [Fact]
        public void ProfileManager_WhenGameHas_EnableCompanionInputTrue_ActivatesProfile()
        {
            var sim = new RecordingInputSimulator();
            var settings = new AppSettings { ClipCursorToGameWindow = false };
            var gamingProfile = new GamingProfile(sim, settings);
            var desktopProfile = new DesktopProfile();
            var tracker = new MockWindowTracker();
            var gamepad = new TestGamepadService();

            var customProfile = GameProfilePresets.CreateStandardPc();
            var gameWithInputActive = new GameEntry
            {
                Id = "custom-fps",
                Name = "Custom FPS Game",
                MainExecutable = "FPS.exe",
                EnableCompanionInput = true,
                Profile = customProfile
            };

            using (var profileEngine = new ProfileEngine(gamepad, desktopProfile))
            using (var profileManager = new ProfileManager(profileEngine, gamingProfile, desktopProfile, tracker, gamepad, settings))
            {
                tracker.TriggerWindowChange(new IntPtr(7000), true, gameWithInputActive);

                Assert.True(profileManager.IsCompanionInputActive);
                Assert.Equal("Custom FPS Game", profileManager.ActiveProfileName);

                // No Standard PC, Botão X emite VirtualKey.E
                var stateBtnX = new GamepadState(true, 1, GamepadButtons.X, 0, 0, 0, 0, 0, 0);
                gamepad.EmitState(stateBtnX);

                Assert.Contains(VirtualKey.E, sim.HeldKeys);
            }
        }

        // =========================================================================
        // 4. UI DO GAMECONFIGPANEL: TOGGLE DE INPUT & NAVEGAÇÃO
        // =========================================================================

        [Fact]
        public void GameConfigPanel_InputToggle_TogglesAndSavesCorrectly()
        {
            var panel = new GameConfigPanel();
            var game = new GameEntry
            {
                Id = "test-game",
                Name = "Test Game",
                MainExecutable = "Test.exe",
                TargetPath = @"C:\Games\Test.exe",
                EnableCompanionInput = true
            };

            panel.EditGame(game);

            GameEntry savedGame = null;
            panel.SaveRequested += g => savedGame = g;

            // Simula clique no botão Save (RT)
            var stateRT = new GamepadState(true, 1, GamepadButtons.None, 0, 255, 0, 0, 0, 0);
            panel.ProcessGamepad(stateRT, 100);

            Assert.NotNull(savedGame);
            Assert.True(savedGame.EnableCompanionInput);
        }

        [Fact]
        public void GameConfigPanel_ButtonA_OnInputToggle_FlipsState_BothGamepadAndClick()
        {
            var panel = new GameConfigPanel();
            var game = new GameEntry
            {
                Id = "test-game",
                Name = "Test Game",
                MainExecutable = "Test.exe",
                TargetPath = @"C:\Games\Test.exe",
                EnableCompanionInput = true
            };

            panel.EditGame(game);

            // Reseta input e simula navegação para baixo até a linha do Toggle (linha 7)
            panel.ResetInputState();
            long time = 100;

            // Navega para baixo 7 vezes
            for (int i = 0; i < 7; i++)
            {
                var stateDown = new GamepadState(true, (uint)(i + 1), GamepadButtons.DPadDown, 0, 0, 0, 0, 0, 0);
                panel.ProcessGamepad(stateDown, time);
                time += 50;
                var stateNeutral = new GamepadState(true, (uint)(i + 10), GamepadButtons.None, 0, 0, 0, 0, 0, 0);
                panel.ProcessGamepad(stateNeutral, time);
                time += 50;
            }

            // Pressiona o botão A do controle sobre o Toggle
            var stateA = new GamepadState(true, 99, GamepadButtons.A, 0, 0, 0, 0, 0, 0);
            panel.ProcessGamepad(stateA, time);
            time += 50;
            var stateReleaseA = new GamepadState(true, 100, GamepadButtons.None, 0, 0, 0, 0, 0, 0);
            panel.ProcessGamepad(stateReleaseA, time);

            // Verifica se o estado mudou para false
            GameEntry savedGame1 = null;
            panel.SaveRequested += g => savedGame1 = g;
            var stateRT = new GamepadState(true, 101, GamepadButtons.None, 0, 255, 0, 0, 0, 0);
            panel.ProcessGamepad(stateRT, time + 50);

            Assert.NotNull(savedGame1);
            Assert.False(savedGame1.EnableCompanionInput);

            // Pressiona o botão A novamente para voltar para true
            time += 100;
            panel.ProcessGamepad(stateA, time);
            time += 50;
            panel.ProcessGamepad(stateReleaseA, time);

            GameEntry savedGame2 = null;
            panel.SaveRequested += g => savedGame2 = g;
            panel.ProcessGamepad(stateRT, time + 50);

            Assert.NotNull(savedGame2);
            Assert.True(savedGame2.EnableCompanionInput);
        }
    }
}
