using System;
using System.Collections.Generic;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.UI.Controls;
using Xunit;

namespace ConsoleMode.GamepadCompanion.Tests
{
    public sealed class ProfileSystemPhase2Tests
    {
        private static GamepadState MakeState(uint packet, GamepadButtons buttons = GamepadButtons.None, byte rt = 0)
        {
            return new GamepadState(true, packet, buttons, 0, rt, 0, 0, 0, 0);
        }

        [Fact]
        public void GameProfileEditorControl_LoadsGameAndInitializesFocusedSlot()
        {
            var editor = new GameProfileEditorControl();
            var game = new GameEntry
            {
                Id = "turtle-wow",
                Name = "Turtle WoW",
                Profile = GameProfilePresets.CreateWowClassic()
            };

            editor.LoadGameProfile(game, new AppSettings());

            Assert.Equal(ProfileEditorSlot.MouseSensitivity, editor.FocusedSlot);
            Assert.Equal(50, editor.WorkingProfile.ActivePage.MouseSensitivity);
            Assert.True(editor.WorkingProfile.UseGlobalDeadzone);
            Assert.True(editor.WorkingProfile.UseGlobalTriggerThreshold);
        }

        [Fact]
        public void GameProfileEditorControl_AdjustSensitivityAndDeadzoneSliders_ViaGamepad()
        {
            var editor = new GameProfileEditorControl();
            var settings = new AppSettings { StickDeadzonePercent = 24, TriggerThresholdPercent = 20 };
            var game = new GameEntry { Id = "test", Name = "Test Game", Profile = GameProfilePresets.CreateWowClassic() };

            editor.LoadGameProfile(game, settings);
            editor.ProcessGamepad(MakeState(1), 10);

            // 1. Ajusta Sensibilidade do Mouse para a direita (+2)
            editor.ProcessGamepad(MakeState(2, GamepadButtons.DPadRight), 50);
            editor.ProcessGamepad(MakeState(3), 70);
            Assert.Equal(52, editor.WorkingProfile.ActivePage.MouseSensitivity);

            // 2. Desce para DeadzoneSlider
            editor.ProcessGamepad(MakeState(4, GamepadButtons.DPadDown), 100);
            editor.ProcessGamepad(MakeState(5), 120);
            Assert.Equal(ProfileEditorSlot.DeadzoneSlider, editor.FocusedSlot);

            // Ao ajustar o slider de Deadzone com DPadLeft/Right, desmarca automaticamente UseGlobalDeadzone
            editor.ProcessGamepad(MakeState(6, GamepadButtons.DPadRight), 150);
            editor.ProcessGamepad(MakeState(7), 170);
            Assert.False(editor.WorkingProfile.UseGlobalDeadzone);
            Assert.Equal(26, editor.WorkingProfile.CustomDeadzonePercent);

            // 3. Pressionar A (ou navegar para o Toggle Global e apertar A) reativa o Global
            editor.ProcessGamepad(MakeState(8, GamepadButtons.A), 200);
            editor.ProcessGamepad(MakeState(9), 220);
            Assert.True(editor.WorkingProfile.UseGlobalDeadzone);
        }

        [Fact]
        public void GameProfileEditorControl_PresetOverlayMenu_SwitchesBetweenWowClassicAndRpgRightClick()
        {
            var editor = new GameProfileEditorControl();
            var game = new GameEntry { Id = "test", Name = "Test Game", Profile = GameProfilePresets.CreateWowClassic() };
            var otherGame = new GameEntry
            {
                Id = "other-fps",
                Name = "Half-Life",
                MainExecutable = "hl.exe",
                Profile = GameProfilePresets.CreateStandardPc()
            };

            editor.LoadGameProfile(game, new AppSettings(), new[] { game, otherGame });
            editor.SetFocusedSlot(ProfileEditorSlot.PresetSelector);
            editor.ProcessGamepad(MakeState(1), 10);

            // Abre o menu de Presets / Copiar Jogo com botão A
            editor.ProcessGamepad(MakeState(2, GamepadButtons.A), 30);
            editor.ProcessGamepad(MakeState(3), 50);
            Assert.True(editor.IsPresetMenuOpen);
            Assert.Equal(5, editor.PresetMenuCount); // 4 presets + 1 jogo para copiar

            // Desce para o 2º item: RPG / MMO (Câmera Botão Direito)
            editor.ProcessGamepad(MakeState(4, GamepadButtons.DPadDown), 80);
            editor.ProcessGamepad(MakeState(5), 100);
            Assert.Equal(1, editor.PresetMenuFocusedIndex);

            // Confirma com A
            editor.ProcessGamepad(MakeState(6, GamepadButtons.A), 130);
            editor.ProcessGamepad(MakeState(7), 150);
            Assert.False(editor.IsPresetMenuOpen);

            // Verifica que o perfil foi trocado para RPG (RightStick tem Mouse:Right como comando adicional)
            var rStickAdd = editor.GetSlotActions(ProfileEditorSlot.RightStickAdditional);
            Assert.Single(rStickAdd);
            Assert.Equal(InputActionType.MouseButton, rStickAdd[0].Type);
            Assert.Equal(MouseButton.Right, rStickAdd[0].MouseButton);
            Assert.Empty(editor.GetSlotActions(ProfileEditorSlot.LeftStickAdditional));

            // Reabre menu e copia do jogo "Half-Life" (índice 4)
            editor.ProcessGamepad(MakeState(8, GamepadButtons.A), 180);
            editor.ProcessGamepad(MakeState(9), 200);
            for (int i = 0; i < 4; i++)
            {
                editor.ProcessGamepad(MakeState((uint)(10 + i * 2), GamepadButtons.DPadDown), 220 + i * 40);
                editor.ProcessGamepad(MakeState((uint)(11 + i * 2)), 240 + i * 40);
            }
            Assert.Equal(4, editor.PresetMenuFocusedIndex);

            editor.ProcessGamepad(MakeState(20, GamepadButtons.A), 400);
            editor.ProcessGamepad(MakeState(21), 420);

            // No Standard PC (Half-Life), Botão X é VirtualKey.E
            var btnXActions = editor.GetSlotActions(ProfileEditorSlot.ButtonX);
            Assert.Single(btnXActions);
            Assert.Equal(VirtualKey.E, btnXActions[0].Key);
        }

        [Fact]
        public void GameProfileEditorControl_CyclesStickMode_AndClearsSlotWithButtonX_AndSavesWithRT()
        {
            var editor = new GameProfileEditorControl();
            var game = new GameEntry { Id = "wow", Name = "Turtle WoW", Profile = GameProfilePresets.CreateWowClassic() };
            editor.LoadGameProfile(game, new AppSettings());
            editor.ProcessGamepad(MakeState(1), 10);

            // 1. Alterna Modo do Stick Esquerdo com A: DirectionalKeys -> MouseMove -> Disabled -> DirectionalKeys
            editor.SetFocusedSlot(ProfileEditorSlot.LeftStickMode);
            Assert.Equal(StickMode.DirectionalKeys, editor.WorkingProfile.ActivePage.LeftStick.Mode);

            editor.ProcessGamepad(MakeState(2, GamepadButtons.A), 40);
            editor.ProcessGamepad(MakeState(3), 60);
            Assert.Equal(StickMode.MouseMove, editor.WorkingProfile.ActivePage.LeftStick.Mode);

            editor.ProcessGamepad(MakeState(4, GamepadButtons.A), 80);
            editor.ProcessGamepad(MakeState(5), 100);
            Assert.Equal(StickMode.Disabled, editor.WorkingProfile.ActivePage.LeftStick.Mode);

            editor.ProcessGamepad(MakeState(6, GamepadButtons.A), 120);
            editor.ProcessGamepad(MakeState(7), 140);
            Assert.Equal(StickMode.DirectionalKeys, editor.WorkingProfile.ActivePage.LeftStick.Mode);

            // 2. Foca no Botão A (que tem Espaço + F9) e limpa com o botão X do controle
            editor.SetFocusedSlot(ProfileEditorSlot.ButtonA);
            Assert.Equal(2, editor.GetSlotActions(ProfileEditorSlot.ButtonA).Count);

            editor.ProcessGamepad(MakeState(8, GamepadButtons.X), 170);
            editor.ProcessGamepad(MakeState(9), 190);
            Assert.Empty(editor.GetSlotActions(ProfileEditorSlot.ButtonA));

            // 3. Salva com RT e verifica evento SaveRequested
            GameProfile savedProfile = null;
            editor.SaveRequested += (g, prof) => savedProfile = prof;

            editor.ProcessGamepad(MakeState(10, GamepadButtons.None, rt: 250), 220);

            Assert.NotNull(savedProfile);
            Assert.Empty(savedProfile.ActivePage.GetActions(ControllerButton.A));
            Assert.Empty(game.Profile.ActivePage.GetActions(ControllerButton.A));
        }
    }
}
