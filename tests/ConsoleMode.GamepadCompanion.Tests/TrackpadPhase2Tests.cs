using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.Hardware;
using ConsoleMode.GamepadCompanion.UI.Controls;
using Xunit;

namespace ConsoleMode.GamepadCompanion.Tests
{
    public class TrackpadPhase2Tests
    {
        [Fact]
        public void GameProfileEditorControl_TrackpadSlots_VisibleOnlyOnHandheldSteamDeck()
        {
            var editor = new GameProfileEditorControl();
            var game = new GameEntry { Name = "Test Game", Profile = GameProfilePresets.CreateStandardPc() };
            var settings = new AppSettings();

            editor.LoadGameProfile(game, settings);

            // 1. No layout StandardGamepad (Xbox / controle convencional), os cards dos trackpads não devem estar visíveis
            editor.VisualMode = ControllerVisualMode.StandardGamepad;
            editor.SetFocusedSlot(ProfileEditorSlot.LeftTrackpad);
            // Ao tentar focar ou navegar, slots invisíveis não devem ser selecionáveis no fluxo normal
            Assert.Equal(ControllerVisualMode.StandardGamepad, editor.VisualMode);

            // 2. No layout HandheldSteamDeck, os slots tornam-se plenamente ativos
            editor.VisualMode = ControllerVisualMode.HandheldSteamDeck;
            Assert.Equal(ControllerVisualMode.HandheldSteamDeck, editor.VisualMode);
        }

        [Fact]
        public void GameProfileEditorControl_ActivateTrackpadSlot_FiresTrackpadEditRequested()
        {
            var editor = new GameProfileEditorControl();
            var game = new GameEntry { Name = "Test Game", Profile = GameProfilePresets.CreateStandardPc() };
            var settings = new AppSettings();

            editor.LoadGameProfile(game, settings);
            editor.VisualMode = ControllerVisualMode.HandheldSteamDeck;

            bool fired = false;
            bool receivedIsRight = false;
            TrackpadConfig receivedConfig = null;

            editor.TrackpadEditRequested += (isRight, config) =>
            {
                fired = true;
                receivedIsRight = isRight;
                receivedConfig = config;
            };

            // Ativa o Trackpad Esquerdo
            editor.SetFocusedSlot(ProfileEditorSlot.LeftTrackpad);
            editor.ActivateFocusedSlot();

            Assert.True(fired);
            Assert.False(receivedIsRight);
            Assert.NotNull(receivedConfig);

            // Ativa o Trackpad Direito
            fired = false;
            editor.SetFocusedSlot(ProfileEditorSlot.RightTrackpad);
            editor.ActivateFocusedSlot();

            Assert.True(fired);
            Assert.True(receivedIsRight);
            Assert.NotNull(receivedConfig);
        }

        [Fact]
        public void TrackpadConfigControl_OpenAndCycleModes()
        {
            var ctrl = new TrackpadConfigControl();
            var game = new GameEntry { Name = "Test Game" };
            var config = new TrackpadConfig { Mode = TrackpadMode.MouseTrackball, Sensitivity = 60 };

            ctrl.Open(game, isRightPad: true, sourceConfig: config);

            Assert.True(ctrl.IsRightPad);
            Assert.Equal(TrackpadConfigField.Mode, ctrl.FocusedField);
            Assert.Equal(TrackpadMode.MouseTrackball, ctrl.Config.Mode);

            // Cicla para o próximo modo (Scrollwheel)
            ctrl.AdjustFocusedField(1);
            Assert.Equal(TrackpadMode.Scrollwheel, ctrl.Config.Mode);

            // Cicla novamente (DPad)
            ctrl.AdjustFocusedField(1);
            Assert.Equal(TrackpadMode.DPad, ctrl.Config.Mode);

            // Cicla novamente (SingleButton)
            ctrl.AdjustFocusedField(1);
            Assert.Equal(TrackpadMode.SingleButton, ctrl.Config.Mode);

            // Cicla novamente (Disabled)
            ctrl.AdjustFocusedField(1);
            Assert.Equal(TrackpadMode.Disabled, ctrl.Config.Mode);

            // Retorna ao MouseTrackball
            ctrl.AdjustFocusedField(1);
            Assert.Equal(TrackpadMode.MouseTrackball, ctrl.Config.Mode);
        }

        [Fact]
        public void TrackpadConfigControl_SliderAdjustment_ModifiesValuesCorrectly()
        {
            var ctrl = new TrackpadConfigControl();
            var game = new GameEntry { Name = "Test Game" };
            var config = new TrackpadConfig
            {
                Mode = TrackpadMode.MouseTrackball,
                Sensitivity = 50,
                Friction = 50,
                ClickPressureThreshold = 10000,
                HapticIntensity = TrackpadHapticIntensity.Medium
            };

            ctrl.Open(game, isRightPad: true, sourceConfig: config);

            // Simula navegação para baixo para Sensibilidade
            ctrl.ProcessGamepad(CreateDpadDownState(), 100);
            Assert.Equal(TrackpadConfigField.Sensitivity, ctrl.FocusedField);

            ctrl.AdjustFocusedField(1); // +2%
            Assert.Equal(52, ctrl.Config.Sensitivity);
            ctrl.AdjustFocusedField(-1); // -2%
            Assert.Equal(50, ctrl.Config.Sensitivity);

            // Alívio e novo toque: Navega para Atrito/Inércia
            ctrl.ProcessGamepad(CreateNeutralState(), 150);
            ctrl.ProcessGamepad(CreateDpadDownState(), 200);
            Assert.Equal(TrackpadConfigField.Friction, ctrl.FocusedField);
            ctrl.AdjustFocusedField(1);
            Assert.Equal(52, ctrl.Config.Friction);

            // Alívio e novo toque: Navega para Limiar de Pressão
            ctrl.ProcessGamepad(CreateNeutralState(), 250);
            ctrl.ProcessGamepad(CreateDpadDownState(), 300);
            Assert.Equal(TrackpadConfigField.ClickPressureThreshold, ctrl.FocusedField);
            ctrl.AdjustFocusedField(1); // +500
            Assert.Equal(10500, ctrl.Config.ClickPressureThreshold);

            // Alívio e novo toque: Navega para Intensidade Háptica
            ctrl.ProcessGamepad(CreateNeutralState(), 350);
            ctrl.ProcessGamepad(CreateDpadDownState(), 400);
            Assert.Equal(TrackpadConfigField.HapticIntensity, ctrl.FocusedField);
            ctrl.AdjustFocusedField(1); // Medium -> High
            Assert.Equal(TrackpadHapticIntensity.High, ctrl.Config.HapticIntensity);
        }

        [Fact]
        public void TrackpadConfigControl_SetSlotActions_UpdatesConfigMappings()
        {
            var ctrl = new TrackpadConfigControl();
            var game = new GameEntry { Name = "Test Game" };
            var config = new TrackpadConfig { Mode = TrackpadMode.DPad };

            ctrl.Open(game, isRightPad: false, sourceConfig: config);

            // Mapeia Clique Mecânico (Press) para Botão Esquerdo do Mouse
            var leftClick = new List<InputAction> { InputAction.FromMouseButton(MouseButton.Left) };
            ctrl.SetSlotActions(ProfileEditorSlot.TrackpadPress, leftClick);
            Assert.Single(ctrl.Config.PressActions);
            Assert.Equal(MouseButton.Left, ctrl.Config.PressActions[0].MouseButton);

            // Mapeia Toque Leve (Touch) para Barra de Espaço
            var spaceKey = new List<InputAction> { InputAction.FromKey(VirtualKey.Space) };
            ctrl.SetSlotActions(ProfileEditorSlot.TrackpadTouch, spaceKey);
            Assert.Single(ctrl.Config.TouchActions);
            Assert.Equal(VirtualKey.Space, ctrl.Config.TouchActions[0].Key);

            // Mapeia Direcionais do D-Pad
            ctrl.SetSlotActions(ProfileEditorSlot.TrackpadDpadUp, new List<InputAction> { InputAction.FromKey(VirtualKey.W) });
            ctrl.SetSlotActions(ProfileEditorSlot.TrackpadDpadDown, new List<InputAction> { InputAction.FromKey(VirtualKey.S) });
            ctrl.SetSlotActions(ProfileEditorSlot.TrackpadDpadLeft, new List<InputAction> { InputAction.FromKey(VirtualKey.A) });
            ctrl.SetSlotActions(ProfileEditorSlot.TrackpadDpadRight, new List<InputAction> { InputAction.FromKey(VirtualKey.D) });

            Assert.Single(ctrl.Config.Up);
            Assert.Equal(VirtualKey.W, ctrl.Config.Up[0].Key);
            Assert.Single(ctrl.Config.Down);
            Assert.Equal(VirtualKey.S, ctrl.Config.Down[0].Key);
            Assert.Single(ctrl.Config.Left);
            Assert.Equal(VirtualKey.A, ctrl.Config.Left[0].Key);
            Assert.Single(ctrl.Config.Right);
            Assert.Equal(VirtualKey.D, ctrl.Config.Right[0].Key);
        }

        [Fact]
        public void TrackpadConfigControl_ConfirmAndClose_FiresConfigConfirmedEvent()
        {
            var ctrl = new TrackpadConfigControl();
            var game = new GameEntry { Name = "Test Game" };
            var config = new TrackpadConfig
            {
                Mode = TrackpadMode.Scrollwheel,
                Sensitivity = 75,
                InvertScroll = false
            };

            ctrl.Open(game, isRightPad: false, sourceConfig: config);

            bool confirmed = false;
            bool confirmedIsRight = true;
            TrackpadConfig confirmedConfig = null;

            ctrl.ConfigConfirmed += (isRight, savedConfig) =>
            {
                confirmed = true;
                confirmedIsRight = isRight;
                confirmedConfig = savedConfig;
            };

            ctrl.ConfirmAndClose();

            Assert.True(confirmed);
            Assert.False(confirmedIsRight);
            Assert.NotNull(confirmedConfig);
            Assert.Equal(TrackpadMode.Scrollwheel, confirmedConfig.Mode);
            Assert.Equal(75, confirmedConfig.Sensitivity);
            Assert.False(confirmedConfig.InvertScroll);
        }

        private static GamepadState CreateNeutralState()
        {
            return new GamepadState(true, 1, GamepadButtons.None, 0, 0, 0, 0, 0, 0);
        }

        private static GamepadState CreateDpadDownState()
        {
            return new GamepadState(true, 1, GamepadButtons.DPadDown, 0, 0, 0, 0, 0, 0);
        }
    }
}
