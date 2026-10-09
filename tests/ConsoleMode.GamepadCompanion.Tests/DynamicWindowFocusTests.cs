using System;
using ConsoleMode.GamepadCompanion.Core.Interfaces;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.Engine;
using ConsoleMode.GamepadCompanion.Hardware;
using ConsoleMode.GamepadCompanion.Profiles;
using Xunit;

namespace ConsoleMode.GamepadCompanion.Tests
{
    public class DynamicWindowFocusTests
    {
        private sealed class FakeWindowTracker : IWindowTracker
        {
            public bool IsGameFocused { get; set; }
            public string ActiveProcessName { get; set; } = string.Empty;
            public IntPtr ActiveWindowHandle { get; set; } = IntPtr.Zero;

            public event Action<bool> FocusChanged;
            public event Action<IntPtr> ActiveWindowChanged;

            public void CheckActiveWindow() { }
            public void RegisterGameExecutable(string executableName) { }

            public void SimulateWindowChange(IntPtr newHwnd, bool isGame)
            {
                ActiveWindowHandle = newHwnd;
                IsGameFocused = isGame;
                FocusChanged?.Invoke(isGame);
                ActiveWindowChanged?.Invoke(newHwnd);
            }
        }

        private sealed class DummyGamepadService : IGamepadService
        {
            public int SelectedSlot { get; set; } = 0;
            public int ActiveSlot => -1;
            public GamepadState CurrentState => default;
            public event Action<GamepadState> StateUpdated { add { } remove { } }
            public event Action<int, bool> ConnectionChanged { add { } remove { } }
            public bool IsSlotConnected(int slot) => false;
            public void Start() { }
            public void Stop() { }
            public void Dispose() { }
        }

        private sealed class SpyProfile : IProfile
        {
            public string Name => "SpyProfile";
            public int ResetCount { get; private set; }

            public void Update(GamepadState state, float deltaSeconds) { }

            public void Reset()
            {
                ResetCount++;
            }
        }

        private sealed class DummyInputSimulator : IInputSimulator
        {
            public void KeyDown(VirtualKey key) { }
            public void KeyUp(VirtualKey key) { }
            public void MouseMoveRelative(int dx, int dy) { }
            public void MouseButtonDown(MouseButton button) { }
            public void MouseButtonUp(MouseButton button) { }
        }

        [Fact]
        public void WindowTracker_InitialActiveWindowHandle_IsZero()
        {
            var tracker = new WindowTracker();
            Assert.Equal(IntPtr.Zero, tracker.ActiveWindowHandle);
        }

        [Fact]
        public void ProfileManager_WhenSwitchingBetweenTwoGameWindows_ResetsProfile()
        {
            var tracker = new FakeWindowTracker();
            var gamepad = new DummyGamepadService();
            var settings = new AppSettings { ClipCursorToGameWindow = false };

            var dummyInput = new DummyInputSimulator();
            var gamingProfile = new GamingProfile(dummyInput, settings);
            var desktopProfile = new DesktopProfile();

            using (var profileEngine = new ProfileEngine(gamepad, desktopProfile))
            using (var profileManager = new ProfileManager(profileEngine, gamingProfile, desktopProfile, tracker, gamepad, settings))
            {
                // Janela 1 do WoW ativa
                tracker.SimulateWindowChange(new IntPtr(1001), true);
                Assert.Equal("Gaming", profileManager.ActiveProfileName);

                // Alterna para Janela 2 do WoW (outro HWND, mesmo jogo)
                tracker.SimulateWindowChange(new IntPtr(2002), true);
                Assert.Equal("Gaming", profileManager.ActiveProfileName);

                // Alterna para Desktop
                tracker.SimulateWindowChange(IntPtr.Zero, false);
                Assert.Equal("Desktop", profileManager.ActiveProfileName);
            }
        }
    }
}
