using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using ConsoleMode.GamepadCompanion.Core.Interfaces;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.Engine;
using ConsoleMode.GamepadCompanion.Hardware;
using ConsoleMode.GamepadCompanion.Profiles;
using ConsoleMode.GamepadCompanion.UI;
using ConsoleMode.GamepadCompanion.UI.Controls;
using Xunit;

namespace ConsoleMode.GamepadCompanion.Tests
{
    public class DesktopProfilePhase2And3Tests
    {
        private sealed class RecordingInputSimulator : IInputSimulator
        {
            public HashSet<VirtualKey> HeldKeys { get; } = new HashSet<VirtualKey>();
            public HashSet<MouseButton> HeldMouseButtons { get; } = new HashSet<MouseButton>();

            public void KeyDown(VirtualKey key) => HeldKeys.Add(key);
            public void KeyUp(VirtualKey key) => HeldKeys.Remove(key);
            public void MouseButtonDown(MouseButton button) => HeldMouseButtons.Add(button);
            public void MouseButtonUp(MouseButton button) => HeldMouseButtons.Remove(button);
            public void MouseMoveRelative(int dx, int dy) { }
            public void MouseWheel(int delta) { }
        }

        private sealed class FakeWindowTracker : IWindowTracker
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

            public void SimulateFocusChange(bool isGame, bool isCompanion, IntPtr hwnd, GameEntry game = null)
            {
                IsGameFocused = isGame;
                IsCompanionFocused = isCompanion;
                ActiveWindowHandle = hwnd;
                ActiveGame = game;
                FocusChanged?.Invoke(isGame);
                ActiveWindowChanged?.Invoke(hwnd);
                ActiveGameChanged?.Invoke(game);
            }
        }

        private sealed class DummyGamepadService : IGamepadService
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
            public void UpdateState(GamepadState state)
            {
                CurrentState = state;
                StateUpdated?.Invoke(state);
            }
        }

        [Fact]
        public void DesktopProfile_WhenSuspended_DoesNotSimulateAnyInput()
        {
            var mockSim = new RecordingInputSimulator();
            var settings = new AppSettings { DesktopModeEnabled = true, MappingEnabled = true };
            var desktop = new DesktopProfile(mockSim, settings);

            desktop.IsSuspended = true;

            var state = new GamepadState(true, 1, GamepadButtons.A, 0, 0, 0, 0, 255, 0);

            desktop.Update(state, 0.016f);

            Assert.Empty(mockSim.HeldKeys);
            Assert.Empty(mockSim.HeldMouseButtons);
        }

        [Fact]
        public void DesktopProfile_WhenResumed_SimulatesInputNormally()
        {
            var mockSim = new RecordingInputSimulator();
            var settings = new AppSettings { DesktopModeEnabled = true, MappingEnabled = true };
            var desktop = new DesktopProfile(mockSim, settings);

            desktop.IsSuspended = true;
            var state = new GamepadState(true, 1, GamepadButtons.A, 0, 0, 0, 0, 0, 0);
            desktop.Update(state, 0.016f);
            Assert.Empty(mockSim.HeldKeys);

            // Resumir emulação
            desktop.IsSuspended = false;
            desktop.Update(state, 0.016f);
            Assert.Contains(VirtualKey.Enter, mockSim.HeldKeys);
        }

        [Fact]
        public void DesktopProfile_WhenCompanionInForeground_AutomaticallySilencesInput()
        {
            var mockSim = new RecordingInputSimulator();
            var settings = new AppSettings { DesktopModeEnabled = true, MappingEnabled = true };
            var desktop = new DesktopProfile(mockSim, settings);

            try
            {
                // Simula detecção de que o Companion está em primeiro plano
                DesktopProfile.ForegroundCheckOverride = () => true;

                var state = new GamepadState(true, 1, GamepadButtons.DPadDown, 0, 0, 0, 0, 0, 0);
                desktop.Update(state, 0.016f);

                Assert.Empty(mockSim.HeldKeys);
                Assert.Empty(mockSim.HeldMouseButtons);

                // Quando Companion sai do primeiro plano
                DesktopProfile.ForegroundCheckOverride = () => false;
                desktop.Update(state, 0.016f);

                Assert.Contains(VirtualKey.Down, mockSim.HeldKeys);
            }
            finally
            {
                DesktopProfile.ForegroundCheckOverride = null;
            }
        }

        [Fact]
        public void ProfileManager_FocusTransitions_SuspendDesktopProfileWhenCompanionFocused()
        {
            var tracker = new FakeWindowTracker();
            var gamepad = new DummyGamepadService();
            var settings = new AppSettings { DesktopModeEnabled = true, MappingEnabled = true };
            var mockSim = new RecordingInputSimulator();

            var gamingProfile = new GamingProfile(mockSim, settings);
            var desktopProfile = new DesktopProfile(mockSim, settings);
            var engine = new ProfileEngine(gamepad, desktopProfile);

            using (var pm = new ProfileManager(engine, gamingProfile, desktopProfile, tracker, gamepad, settings))
            {
                // 1. Inicia sem foco em jogo e sem foco no companion (Desktop comum ativo)
                tracker.SimulateFocusChange(isGame: false, isCompanion: false, new IntPtr(100));
                Assert.False(pm.IsGameFocused);
                Assert.False(desktopProfile.IsSuspended);
                Assert.Equal("Desktop", pm.ActiveProfileName);

                // 2. Foco entra na janela do Companion (deve suspender o DesktopProfile)
                tracker.SimulateFocusChange(isGame: false, isCompanion: true, new IntPtr(200));
                Assert.False(pm.IsGameFocused);
                Assert.True(desktopProfile.IsSuspended);

                // 3. Foco entra em um jogo monitorado
                var game = new GameEntry { Id = "game-1", Name = "Test Game", Profile = GameProfilePresets.CreateStandardPc() };
                tracker.SimulateFocusChange(isGame: true, isCompanion: false, new IntPtr(300), game);
                Assert.True(pm.IsGameFocused);
                Assert.Equal("Test Game", pm.ActiveProfileName);

                // 4. Sai do jogo de volta para o Desktop
                tracker.SimulateFocusChange(isGame: false, isCompanion: false, new IntPtr(400));
                Assert.False(pm.IsGameFocused);
                Assert.False(desktopProfile.IsSuspended);
                Assert.Equal("Desktop", pm.ActiveProfileName);
            }
        }

        [Fact]
        public void ProfileManager_RefreshDesktopProfile_UpdatesInnerProfile()
        {
            var tracker = new FakeWindowTracker();
            var gamepad = new DummyGamepadService();
            var settings = new AppSettings { DesktopModeEnabled = true, MappingEnabled = true };
            var mockSim = new RecordingInputSimulator();

            var gamingProfile = new GamingProfile(mockSim, settings);
            var desktopProfile = new DesktopProfile(mockSim, settings);
            var engine = new ProfileEngine(gamepad, desktopProfile);

            using (var pm = new ProfileManager(engine, gamingProfile, desktopProfile, tracker, gamepad, settings))
            {
                var newProfile = GameProfilePresets.CreateDesktopDefault();
                newProfile.ActivePage.MouseSensitivity = 88;

                pm.RefreshDesktopProfile(newProfile);

                Assert.Equal(88, pm.DesktopProfile.ActiveProfile.ActivePage.MouseSensitivity);
            }
        }

        [Fact]
        public void SettingsForm_InstantiatesAndIntegratesDesktopNavigation()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "CM_Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                string iniPath = Path.Combine(tempDir, "config.ini");
                var configRepo = new ConfigRepository(iniPath);
                var settings = new AppSettings { DesktopModeEnabled = true, MappingEnabled = true };
                configRepo.Save(settings);

                var tracker = new FakeWindowTracker();
                var gamepad = new DummyGamepadService();
                var mockSim = new RecordingInputSimulator();
                var gamingProfile = new GamingProfile(mockSim, settings);
                var desktopProfile = new DesktopProfile(mockSim, settings);
                var engine = new ProfileEngine(gamepad, desktopProfile);

                Exception threadEx = null;
                var t = new Thread(() =>
                {
                    try
                    {
                        using (var pm = new ProfileManager(engine, gamingProfile, desktopProfile, tracker, gamepad, settings))
                        using (var form = new SettingsForm(gamepad, settings, pm, configRepo, tracker))
                        {
                            Assert.NotNull(form);
                        }
                    }
                    catch (Exception ex)
                    {
                        threadEx = ex;
                    }
                });
                t.SetApartmentState(ApartmentState.STA);
                t.Start();
                t.Join();

                if (threadEx != null)
                {
                    throw new AggregateException(threadEx);
                }
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
