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

            Assert.Equal(ProfileEditorSlot.DeadzoneSlider, editor.FocusedSlot);
            Assert.Equal(20, editor.WorkingProfile.ActivePage.MouseSensitivity);
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

            // 1. Inicia focado em DeadzoneSlider. Sem apertar [A], DPadRight NAVEGA espacialmente e NAO altera valor
            editor.ProcessGamepad(MakeState(2, GamepadButtons.DPadRight), 50);
            editor.ProcessGamepad(MakeState(3), 70);
            Assert.False(editor.IsEditingSlider);
            Assert.True(editor.WorkingProfile.UseGlobalDeadzone);
            Assert.Equal(ProfileEditorSlot.LeftStickMode, editor.FocusedSlot);

            // 2. Retorna para DeadzoneSlider e aperta [A] para entrar no modo de edição do slider
            editor.SetFocusedSlot(ProfileEditorSlot.DeadzoneSlider);
            editor.ProcessGamepad(MakeState(4, GamepadButtons.A), 100);
            editor.ProcessGamepad(MakeState(5), 120);
            Assert.True(editor.IsEditingSlider);

            // Agora sim, DPadRight incrementa (+2) e desmarca o global
            editor.ProcessGamepad(MakeState(6, GamepadButtons.DPadRight), 150);
            editor.ProcessGamepad(MakeState(7), 170);
            Assert.False(editor.WorkingProfile.UseGlobalDeadzone);
            Assert.Equal(26, editor.WorkingProfile.CustomDeadzonePercent);

            // [B] cancela e restaura o valor anterior (Global ativado)
            editor.ProcessGamepad(MakeState(8, GamepadButtons.B), 200);
            editor.ProcessGamepad(MakeState(9), 220);
            Assert.False(editor.IsEditingSlider);
            Assert.True(editor.WorkingProfile.UseGlobalDeadzone);

            // Entra novamente com [A], incrementa e confirma com [A]
            editor.ProcessGamepad(MakeState(10, GamepadButtons.A), 250);
            editor.ProcessGamepad(MakeState(11), 270);
            Assert.True(editor.IsEditingSlider);
            editor.ProcessGamepad(MakeState(12, GamepadButtons.DPadRight), 300);
            editor.ProcessGamepad(MakeState(13), 320);
            Assert.Equal(26, editor.WorkingProfile.CustomDeadzonePercent);
            editor.ProcessGamepad(MakeState(14, GamepadButtons.A), 350);
            editor.ProcessGamepad(MakeState(15), 370);
            Assert.False(editor.IsEditingSlider);
            Assert.False(editor.WorkingProfile.UseGlobalDeadzone);
            Assert.Equal(26, editor.WorkingProfile.CustomDeadzonePercent);

            // 3. Foca no slot de Sensibilidade do Mouse (no Stick Direito quando em MouseMove)
            editor.SetFocusedSlot(ProfileEditorSlot.MouseSensitivity);
            Assert.Equal(20, editor.WorkingProfile.ActivePage.MouseSensitivity);

            // Sem [A], DPadRight não altera sensibilidade
            editor.ProcessGamepad(MakeState(16, GamepadButtons.DPadRight), 400);
            editor.ProcessGamepad(MakeState(17), 420);
            Assert.False(editor.IsEditingSlider);
            Assert.Equal(20, editor.WorkingProfile.ActivePage.MouseSensitivity);

            // Aperta [A] para editar sensibilidade
            editor.SetFocusedSlot(ProfileEditorSlot.MouseSensitivity);
            editor.ProcessGamepad(MakeState(18, GamepadButtons.A), 450);
            editor.ProcessGamepad(MakeState(19), 470);
            Assert.True(editor.IsEditingSlider);

            // Ajusta para a direita (+2)
            editor.ProcessGamepad(MakeState(20, GamepadButtons.DPadRight), 500);
            editor.ProcessGamepad(MakeState(21), 520);
            Assert.Equal(22, editor.WorkingProfile.ActivePage.MouseSensitivity);

            // [B] cancela e volta para 20
            editor.ProcessGamepad(MakeState(22, GamepadButtons.B), 550);
            editor.ProcessGamepad(MakeState(23), 570);
            Assert.False(editor.IsEditingSlider);
            Assert.Equal(20, editor.WorkingProfile.ActivePage.MouseSensitivity);

            // Aperta [A], ajusta para 22 e confirma com [A]
            editor.ProcessGamepad(MakeState(24, GamepadButtons.A), 600);
            editor.ProcessGamepad(MakeState(25), 620);
            editor.ProcessGamepad(MakeState(26, GamepadButtons.DPadRight), 650);
            editor.ProcessGamepad(MakeState(27), 670);
            Assert.Equal(22, editor.WorkingProfile.ActivePage.MouseSensitivity);
            editor.ProcessGamepad(MakeState(28, GamepadButtons.A), 700);
            editor.ProcessGamepad(MakeState(29), 720);
            Assert.False(editor.IsEditingSlider);
            Assert.Equal(22, editor.WorkingProfile.ActivePage.MouseSensitivity);
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

        [Fact]
        public void GameProfileEditorControl_MouseSensitivity_VisibleOnlyWhenRightStickInMouseMove()
        {
            var editor = new GameProfileEditorControl();
            var game = new GameEntry { Id = "wow", Name = "Turtle WoW", Profile = GameProfilePresets.CreateWowClassic() };
            editor.LoadGameProfile(game, new AppSettings());

            // WowClassic: RightStick.Mode é StickMode.MouseMove
            Assert.Equal(StickMode.MouseMove, editor.WorkingProfile.ActivePage.RightStick.Mode);
            editor.SetFocusedSlot(ProfileEditorSlot.MouseSensitivity);
            Assert.Equal(ProfileEditorSlot.MouseSensitivity, editor.FocusedSlot);

            // Ao mudar o modo para DirectionalKeys
            editor.WorkingProfile.ActivePage.RightStick.Mode = StickMode.DirectionalKeys;
            editor.SetFocusedSlot(ProfileEditorSlot.RightStickMode);
            Assert.Equal(ProfileEditorSlot.RightStickMode, editor.FocusedSlot);
        }

        [Fact]
        public void GameProfileEditorControl_RightStickCard_BoundsAndSpacingDoNotOverlap()
        {
            var editor = new GameProfileEditorControl();
            var game = new GameEntry { Id = "wow", Name = "Turtle WoW", Profile = GameProfilePresets.CreateWowClassic() };
            editor.LoadGameProfile(game, new AppSettings());

            // Garante que o layout calculou bounds
            editor.SetFocusedSlot(ProfileEditorSlot.RightStickMode);

            // Testa o layout padrão Classic Gamepad
            var modeRect = editor.GetSlotBounds(ProfileEditorSlot.RightStickMode);
            var sensRect = editor.GetSlotBounds(ProfileEditorSlot.MouseSensitivity);
            var addRect = editor.GetSlotBounds(ProfileEditorSlot.RightStickAdditional);
            var clickRect = editor.GetSlotBounds(ProfileEditorSlot.RightStickClick);

            // RightStickCard está em Y=240, Título "STICK DIREITO" é desenhado em Y=244..257
            Assert.True(modeRect.Top >= 259f, "RightStickMode deve iniciar em Y >= 259 para não colidir com o título do card");
            Assert.True(sensRect.Top >= modeRect.Bottom + 10f, "Sensibilidade deve ter respiro suficiente para o label de título");
            Assert.True(addRect.Top >= sensRect.Bottom + 5f, "Comando Adicional não pode colidir com o slider de Sensibilidade");
            Assert.True(clickRect.Top >= addRect.Bottom + 2f, "Clique R3 não pode colidir com Comando Adicional");

            // Testa modo Handheld Steam Deck
            editor.VisualMode = ControllerVisualMode.HandheldSteamDeck;
            editor.SetFocusedSlot(ProfileEditorSlot.RightStickMode);

            var deckModeRect = editor.GetSlotBounds(ProfileEditorSlot.RightStickMode);
            var deckSensRect = editor.GetSlotBounds(ProfileEditorSlot.MouseSensitivity);
            var deckAddRect = editor.GetSlotBounds(ProfileEditorSlot.RightStickAdditional);
            var deckClickRect = editor.GetSlotBounds(ProfileEditorSlot.RightStickClick);

            Assert.True(deckSensRect.Top >= deckModeRect.Bottom + 10f, "Handheld: Sensibilidade deve ter respiro do modo");
            Assert.True(deckAddRect.Top >= deckSensRect.Bottom + 5f, "Handheld: Comando Adicional não pode colidir com o slider");
            Assert.True(deckClickRect.Top >= deckAddRect.Bottom + 2f, "Handheld: Clique R3 não pode colidir com Comando Adicional");
        }
    }
}
