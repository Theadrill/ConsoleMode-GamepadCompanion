using System;
using System.IO;
using System.Collections.Generic;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.Hardware;
using ConsoleMode.GamepadCompanion.UI.Controls;
using Xunit;

namespace ConsoleMode.GamepadCompanion.Tests
{
    public class SteamDeckVisualAndHardwareRemapTests
    {
        [Fact]
        public void ControllerVisualModeResolver_WhenNotSteamDeck_ShouldReturnStandardGamepad()
        {
            try
            {
                SteamDeckDetector.SetOverride(SteamDeckInfo.CreateStandardPc("Dell Inc.", "XPS"));
                var mode = ControllerVisualModeResolver.Resolve(0);
                Assert.Equal(ControllerVisualMode.StandardGamepad, mode);
            }
            finally
            {
                SteamDeckDetector.ResetCache();
            }
        }

        [Fact]
        public void ControllerVisualModeResolver_WhenSteamDeckAndExternalSlot_ShouldReturnStandardGamepad()
        {
            try
            {
                SteamDeckDetector.SetOverride(new SteamDeckInfo(SteamDeckVariant.SteamDeckLcd, "Valve", "Jupiter"));
                // Quando o usuário seleciona slot externo (slot 1, 2, 3)
                var mode = ControllerVisualModeResolver.Resolve(1);
                Assert.Equal(ControllerVisualMode.StandardGamepad, mode);

                var modeSlot2 = ControllerVisualModeResolver.Resolve(2);
                Assert.Equal(ControllerVisualMode.StandardGamepad, modeSlot2);
            }
            finally
            {
                SteamDeckDetector.ResetCache();
            }
        }

        [Fact]
        public void ApplyHardwareRemap_L4ToLeftShoulder_ShouldSetL1Button()
        {
            using (var service = new SteamDeckControllerService())
            {
                service.L4Remap = SteamDeckHardwareRemap.LeftShoulder;

                var raw = new SteamDeckRawState
                {
                    Buttons4 = SDCButton4.BTN_L4 // L4 físico pressionado
                };

                Assert.True(raw.IsL4);
                Assert.False(raw.IsL1);

                service.ApplyHardwareRemap(ref raw);

                // Agora L1 deve estar ativo no nível de driver virtual!
                Assert.True(raw.IsL1);
                Assert.True((raw.Buttons0 & SDCButton0.BTN_L1) != 0);
            }
        }

        [Fact]
        public void ApplyHardwareRemap_L5ToRightShoulder_ShouldSetR1Button()
        {
            using (var service = new SteamDeckControllerService())
            {
                service.L5Remap = SteamDeckHardwareRemap.RightShoulder;

                var raw = new SteamDeckRawState
                {
                    Buttons0 = SDCButton0.BTN_L5 // L5 físico pressionado
                };

                Assert.True(raw.IsL5);
                Assert.False(raw.IsR1);

                service.ApplyHardwareRemap(ref raw);

                Assert.True(raw.IsR1);
                Assert.True((raw.Buttons0 & SDCButton0.BTN_R1) != 0);
            }
        }

        [Fact]
        public void ApplyHardwareRemap_R4ToA_And_R5ToB()
        {
            using (var service = new SteamDeckControllerService())
            {
                service.R4Remap = SteamDeckHardwareRemap.A;
                service.R5Remap = SteamDeckHardwareRemap.B;

                var raw = new SteamDeckRawState
                {
                    Buttons4 = SDCButton4.BTN_R4,
                    Buttons1 = SDCButton1.BTN_R5
                };

                Assert.True(raw.IsR4);
                Assert.True(raw.IsR5);
                Assert.False(raw.IsA);
                Assert.False(raw.IsB);

                service.ApplyHardwareRemap(ref raw);

                Assert.True(raw.IsA);
                Assert.True(raw.IsB);
            }
        }

        [Fact]
        public void SteamDeckRawState_SteamAndQuickAccess_ShouldBeIndependent()
        {
            var rawSteam = new SteamDeckRawState
            {
                Buttons0 = SDCButton0.BTN_STEAM
            };
            Assert.True(rawSteam.IsSteam);
            Assert.True(rawSteam.IsGuide);
            Assert.False(rawSteam.IsQuickAccess);

            var rawQuickAccess = new SteamDeckRawState
            {
                Buttons5 = SDCButton5.BTN_QUICK_ACCESS
            };
            Assert.False(rawQuickAccess.IsSteam);
            Assert.False(rawQuickAccess.IsGuide);
            Assert.True(rawQuickAccess.IsQuickAccess);
        }

        [Fact]
        public void GameProfileEditor_RearButtonSlots_VisibilityAndMapping()
        {
            var editor = new GameProfileEditorControl();
            var profile = new GameProfile();
            var entry = new GameEntry { Id = "test-game", Name = "Test Game", Profile = profile };
            var settings = new AppSettings();

            editor.LoadGameProfile(entry, settings);

            // No modo padrão (StandardGamepad), slots traseiros não são visíveis
            editor.VisualMode = ControllerVisualMode.StandardGamepad;
            Assert.False(editor.GetSlotActions(ProfileEditorSlot.ButtonL4).Count > 0);

            // Muda para modo HandheldSteamDeck
            editor.VisualMode = ControllerVisualMode.HandheldSteamDeck;

            // Mapeia L4 para tecla Espaço e F9
            var actions = new List<InputAction>
            {
                InputAction.FromKey(VirtualKey.Space),
                InputAction.FromKey(VirtualKey.F9)
            };
            editor.SetSlotActions(ProfileEditorSlot.ButtonL4, actions);

            var retrieved = editor.GetSlotActions(ProfileEditorSlot.ButtonL4);
            Assert.Equal(2, retrieved.Count);
            Assert.Equal(VirtualKey.Space, retrieved[0].Key);
            Assert.Equal(VirtualKey.F9, retrieved[1].Key);

            // Mapeia R4 para Clique Direito do Mouse
            var mouseActions = new List<InputAction>
            {
                InputAction.FromMouseButton(MouseButton.Right)
            };
            editor.SetSlotActions(ProfileEditorSlot.ButtonR4, mouseActions);

            var retrievedR4 = editor.GetSlotActions(ProfileEditorSlot.ButtonR4);
            Assert.Single(retrievedR4);
            Assert.Equal(MouseButton.Right, retrievedR4[0].MouseButton);

            // Limpa o slot
            editor.ClearSlot(ProfileEditorSlot.ButtonL4);
            Assert.Empty(editor.GetSlotActions(ProfileEditorSlot.ButtonL4));
        }

        [Fact]
        public void ConfigRepository_ShouldPersistSteamDeckHardwareRemaps()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "test_config_" + Guid.NewGuid() + ".ini");
            try
            {
                var repo = new ConfigRepository(tempFile);
                var settings = new AppSettings
                {
                    L4HardwareRemap = SteamDeckHardwareRemap.LeftShoulder,
                    L5HardwareRemap = SteamDeckHardwareRemap.RightShoulder,
                    R4HardwareRemap = SteamDeckHardwareRemap.LeftStickClick,
                    R5HardwareRemap = SteamDeckHardwareRemap.RightStickClick
                };

                repo.Save(settings);

                var loaded = repo.Load();
                Assert.Equal(SteamDeckHardwareRemap.LeftShoulder, loaded.L4HardwareRemap);
                Assert.Equal(SteamDeckHardwareRemap.RightShoulder, loaded.L5HardwareRemap);
                Assert.Equal(SteamDeckHardwareRemap.LeftStickClick, loaded.R4HardwareRemap);
                Assert.Equal(SteamDeckHardwareRemap.RightStickClick, loaded.R5HardwareRemap);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void GameProfileEditorControl_WhenInHandheldMode_ShouldRenderWithoutKeyNotFoundException()
        {
            using (var editor = new GameProfileEditorControl())
            {
                var profile = new GameProfile();
                var entry = new GameEntry { Id = "test-game", Name = "Test Game", Profile = profile };
                var settings = new AppSettings();

                editor.Size = new System.Drawing.Size(800, 600);
                editor.LoadGameProfile(entry, settings);
                editor.VisualMode = ControllerVisualMode.HandheldSteamDeck;

                using (var bmp = new System.Drawing.Bitmap(800, 600))
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                {
                    var pe = new System.Windows.Forms.PaintEventArgs(g, new System.Drawing.Rectangle(0, 0, 800, 600));
                    var onPaint = typeof(GameProfileEditorControl).GetMethod("OnPaint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    Assert.NotNull(onPaint);
                    var exception = Record.Exception(() => onPaint.Invoke(editor, new object[] { pe }));
                    Assert.Null(exception);
                }
            }
        }

        [Fact]
        public void GameProfileEditorControl_WhenInStandardMode_ShouldRenderWithoutException()
        {
            using (var editor = new GameProfileEditorControl())
            {
                var profile = new GameProfile();
                var entry = new GameEntry { Id = "test-game", Name = "Test Game", Profile = profile };
                var settings = new AppSettings();

                editor.Size = new System.Drawing.Size(800, 600);
                editor.LoadGameProfile(entry, settings);
                editor.VisualMode = ControllerVisualMode.StandardGamepad;

                using (var bmp = new System.Drawing.Bitmap(800, 600))
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                {
                    var pe = new System.Windows.Forms.PaintEventArgs(g, new System.Drawing.Rectangle(0, 0, 800, 600));
                    var onPaint = typeof(GameProfileEditorControl).GetMethod("OnPaint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    Assert.NotNull(onPaint);
                    var exception = Record.Exception(() => onPaint.Invoke(editor, new object[] { pe }));
                    Assert.Null(exception);
                }
            }
        }
    }
}
