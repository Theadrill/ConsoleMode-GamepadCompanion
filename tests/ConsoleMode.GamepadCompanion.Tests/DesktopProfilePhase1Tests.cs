using System;
using System.IO;
using System.Collections.Generic;
using ConsoleMode.GamepadCompanion.Core.Interfaces;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.Hardware;
using ConsoleMode.GamepadCompanion.Profiles;
using Xunit;

namespace ConsoleMode.GamepadCompanion.Tests
{
    public class DesktopProfilePhase1Tests
    {
        [Fact]
        public void GameProfilePresets_CreateDesktopDefault_ReturnsValidConfiguredProfile()
        {
            var profile = GameProfilePresets.CreateDesktopDefault();

            Assert.NotNull(profile);
            Assert.Equal(GameProfilePresets.PresetDesktopDefault, profile.Name);
            Assert.True(profile.UseGlobalDeadzone);
            Assert.True(profile.UseGlobalTriggerThreshold);

            var page = profile.ActivePage;
            Assert.NotNull(page);
            Assert.Equal(20, page.MouseSensitivity);

            // Analógicos
            Assert.Equal(StickMode.MouseMove, page.LeftStick.Mode);
            Assert.Equal(StickMode.Disabled, page.RightStick.Mode);

            // Botões Faciais
            Assert.Equal(VirtualKey.Enter, page.GetActions(ControllerButton.A)[0].Key);
            Assert.Equal(VirtualKey.Escape, page.GetActions(ControllerButton.B)[0].Key);
            Assert.Equal(VirtualKey.Space, page.GetActions(ControllerButton.X)[0].Key);
            Assert.Equal(VirtualKey.Backspace, page.GetActions(ControllerButton.Y)[0].Key);

            // Gatilhos e Bumpers
            Assert.Equal(MouseButton.Left, page.GetActions(ControllerButton.RightTrigger)[0].MouseButton);
            Assert.Equal(MouseButton.Right, page.GetActions(ControllerButton.LeftTrigger)[0].MouseButton);
            Assert.Equal(VirtualKey.BrowserBack, page.GetActions(ControllerButton.LeftShoulder)[0].Key);
            Assert.Equal(VirtualKey.BrowserForward, page.GetActions(ControllerButton.RightShoulder)[0].Key);

            // Menus & Cliques
            Assert.Equal(CompanionActionType.AltTab, page.GetActions(ControllerButton.Back)[0].SpecialAction);
            Assert.Equal(VirtualKey.LeftWindows, page.GetActions(ControllerButton.Start)[0].Key);
            Assert.Equal(MouseButton.Middle, page.GetActions(ControllerButton.LeftStickClick)[0].MouseButton);
            Assert.Equal(CompanionActionType.ShowDesktop, page.GetActions(ControllerButton.RightStickClick)[0].SpecialAction);

            // Traseiros Handheld / Steam Deck
            Assert.Equal(VirtualKey.VolumeDown, page.GetActions(ControllerButton.L4)[0].Key);
            Assert.Equal(VirtualKey.VolumeUp, page.GetActions(ControllerButton.R4)[0].Key);
            Assert.Equal(VirtualKey.MediaPlayPause, page.GetActions(ControllerButton.L5)[0].Key);
            Assert.Equal(VirtualKey.PrintScreen, page.GetActions(ControllerButton.R5)[0].Key);
        }

        [Fact]
        public void DesktopProfile_WhenEnabled_SimulatesInputs()
        {
            DesktopProfile.ForegroundCheckOverride = () => false;
            try
            {
                var mockSim = new RecordingInputSimulator();
                var settings = new AppSettings { DesktopModeEnabled = true, MappingEnabled = true };
                var desktop = new DesktopProfile(mockSim, settings);

                var state = new GamepadState(true, 1, GamepadButtons.A, 0, 0, 0, 0, 0, 0);

                desktop.Update(state, 0.016f);

                Assert.Contains(VirtualKey.Enter, mockSim.HeldKeys);

                // Solta o botão
                desktop.Update(new GamepadState(true, 2, GamepadButtons.None, 0, 0, 0, 0, 0, 0), 0.016f);
                Assert.DoesNotContain(VirtualKey.Enter, mockSim.HeldKeys);
            }
            finally
            {
                DesktopProfile.ForegroundCheckOverride = null;
            }
        }

        [Fact]
        public void DesktopProfile_WhenDisabled_RemainsSilent()
        {
            var mockSim = new RecordingInputSimulator();
            var settings = new AppSettings { DesktopModeEnabled = false, MappingEnabled = true };
            var desktop = new DesktopProfile(mockSim, settings);

            var state = new GamepadState(true, 1, GamepadButtons.A, 0, 0, 0, 0, 0, 0);

            desktop.Update(state, 0.016f);

            Assert.Empty(mockSim.HeldKeys);
            Assert.Empty(mockSim.HeldMouseButtons);
        }

        [Fact]
        public void ConfigRepository_ShouldPersistDesktopModeEnabled()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "test_config_desktop_" + Guid.NewGuid() + ".ini");
            try
            {
                var repo = new ConfigRepository(tempFile);
                var settings = new AppSettings
                {
                    DesktopModeEnabled = false
                };

                repo.Save(settings);

                var loaded = repo.Load();
                Assert.False(loaded.DesktopModeEnabled);

                // Reativa e salva
                loaded.DesktopModeEnabled = true;
                repo.Save(loaded);

                var reloaded = repo.Load();
                Assert.True(reloaded.DesktopModeEnabled);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void ConfigRepository_ShouldPersistAndLoadDesktopProfile()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "test_config_desk_prof_" + Guid.NewGuid() + ".ini");
            try
            {
                var repo = new ConfigRepository(tempFile);

                // Carrega padrão quando não existe
                var initial = repo.LoadDesktopProfile();
                Assert.NotNull(initial);
                Assert.Equal(GameProfilePresets.PresetDesktopDefault, initial.Name);

                // Modifica o perfil desktop (ex: altera botão Y para F1)
                initial.ActivePage.SetActions(ControllerButton.Y, new[] { InputAction.FromKey(VirtualKey.F1) });
                repo.SaveDesktopProfile(initial);

                // Recarrega e valida persistência
                var loaded = repo.LoadDesktopProfile();
                Assert.NotNull(loaded);
                Assert.Equal(VirtualKey.F1, loaded.ActivePage.GetActions(ControllerButton.Y)[0].Key);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

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
    }
}
