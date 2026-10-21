using System.Collections.Generic;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.UI.Controls;
using Xunit;

namespace ConsoleMode.GamepadCompanion.Tests
{
    public sealed class ProfileSystemPhase3Tests
    {
        private static GamepadState MakeState(uint packet, GamepadButtons buttons = GamepadButtons.None)
        {
            return new GamepadState(true, packet, buttons, 0, 0, 0, 0, 0, 0);
        }

        [Fact]
        public void InputBindingCaptureControl_SingleKeyCapture_RecordsOnKeyRelease()
        {
            var capture = new InputBindingCaptureControl();
            capture.OpenForSlot(
                ProfileEditorSlot.ButtonA,
                "BOTÃO (A)",
                "Turtle WoW",
                new[] { InputAction.FromKey(VirtualKey.Space) });

            List<InputAction> confirmedActions = null;
            capture.BindingConfirmed += (slot, actions) => confirmedActions = actions;

            // Seleciona a opção 0 ([🎙️ Capturar Input Físico]) com o botão A
            capture.ProcessGamepad(MakeState(1), 10);
            capture.ProcessGamepad(MakeState(2, GamepadButtons.A), 30);
            capture.ProcessGamepad(MakeState(3), 50);

            Assert.True(capture.IsListening);
            Assert.Empty(capture.CapturedCombo);

            // Pressiona F9 no teclado físico
            capture.RegisterPhysicalVirtualKeyDown(VirtualKey.F9);
            Assert.True(capture.IsListening);
            Assert.Single(capture.CapturedCombo);
            Assert.Null(confirmedActions);

            // Ao soltar F9, grava automaticamente e encerra o modo de escuta
            capture.RegisterPhysicalVirtualKeyUp(VirtualKey.F9);
            Assert.False(capture.IsListening);
            Assert.NotNull(confirmedActions);
            Assert.Single(confirmedActions);
            Assert.Equal(VirtualKey.F9, confirmedActions[0].Key);
        }

        [Fact]
        public void InputBindingCaptureControl_ThreeKeyCombo_AccumulatesInOrder_AndFinalizesWhenAllReleased()
        {
            var capture = new InputBindingCaptureControl();
            capture.OpenForSlot(ProfileEditorSlot.ButtonA, "BOTÃO (A)", "Turtle WoW", null);

            List<InputAction> confirmedActions = null;
            capture.BindingConfirmed += (slot, actions) => confirmedActions = actions;

            capture.StartListening();
            Assert.True(capture.IsListening);

            // 1. Segura LeftControl
            capture.RegisterPhysicalVirtualKeyDown(VirtualKey.LeftControl);
            Assert.Single(capture.CapturedCombo);

            // 2. Segura LeftShift (com LeftControl ainda pressionado)
            capture.RegisterPhysicalVirtualKeyDown(VirtualKey.LeftShift);
            Assert.Equal(2, capture.CapturedCombo.Count);

            // 3. Segura S (com LeftControl e LeftShift ainda pressionados)
            capture.RegisterPhysicalVirtualKeyDown(VirtualKey.S);
            Assert.Equal(3, capture.CapturedCombo.Count);
            Assert.Null(confirmedActions);

            // 4. Solta S e LeftShift — ainda falta soltar LeftControl, então NÃO deve finalizar ainda
            capture.RegisterPhysicalVirtualKeyUp(VirtualKey.S);
            capture.RegisterPhysicalVirtualKeyUp(VirtualKey.LeftShift);
            Assert.True(capture.IsListening);
            Assert.Null(confirmedActions);

            // 5. Solta LeftControl — todas as teclas soltas, finaliza e grava o combo completo!
            capture.RegisterPhysicalVirtualKeyUp(VirtualKey.LeftControl);
            Assert.False(capture.IsListening);
            Assert.NotNull(confirmedActions);
            Assert.Equal(3, confirmedActions.Count);
            Assert.Equal(VirtualKey.LeftControl, confirmedActions[0].Key);
            Assert.Equal(VirtualKey.LeftShift, confirmedActions[1].Key);
            Assert.Equal(VirtualKey.S, confirmedActions[2].Key);
            Assert.Equal("L-Ctrl + L-Shift + S", InputAction.FormatComboDisplay(confirmedActions));
        }

        [Fact]
        public void InputBindingCaptureControl_RightMouseButtonCapture_RecordsOnMouseUp()
        {
            var capture = new InputBindingCaptureControl();
            capture.OpenForSlot(
                ProfileEditorSlot.RightStickAdditional,
                "STICK DIREITO (COMANDO ADICIONAL)",
                "Turtle WoW",
                null);

            List<InputAction> confirmedActions = null;
            capture.BindingConfirmed += (slot, actions) => confirmedActions = actions;

            capture.StartListening();

            // Pressiona Botão Direito do Mouse
            capture.RegisterPhysicalMouseDown(MouseButton.Right);
            Assert.True(capture.IsListening);
            Assert.Single(capture.CapturedCombo);
            Assert.Null(confirmedActions);

            // Solta Botão Direito do Mouse
            capture.RegisterPhysicalMouseUp(MouseButton.Right);
            Assert.False(capture.IsListening);
            Assert.NotNull(confirmedActions);
            Assert.Single(confirmedActions);
            Assert.Equal(InputActionType.MouseButton, confirmedActions[0].Type);
            Assert.Equal(MouseButton.Right, confirmedActions[0].MouseButton);
        }

        [Fact]
        public void InputBindingCaptureControl_NavigationAndClearOption_ClearsSlotImmediately()
        {
            var capture = new InputBindingCaptureControl();
            capture.OpenForSlot(
                ProfileEditorSlot.ButtonY,
                "BOTÃO (Y)",
                "Turtle WoW",
                new[] { InputAction.FromKey(VirtualKey.D2) });

            List<InputAction> confirmedActions = null;
            capture.BindingConfirmed += (slot, actions) => confirmedActions = actions;

            capture.ProcessGamepad(MakeState(1), 10);

            // Desce 2 vezes até a opção 2 ([🗑️ Limpar / Desativar Entrada])
            capture.ProcessGamepad(MakeState(2, GamepadButtons.DPadDown), 40);
            capture.ProcessGamepad(MakeState(3), 60);
            capture.ProcessGamepad(MakeState(4, GamepadButtons.DPadDown), 90);
            capture.ProcessGamepad(MakeState(5), 110);

            Assert.Equal(2, capture.FocusedOptionIndex);

            // Pressiona A para limpar
            capture.ProcessGamepad(MakeState(6, GamepadButtons.A), 140);
            capture.ProcessGamepad(MakeState(7), 160);

            Assert.NotNull(confirmedActions);
            Assert.Empty(confirmedActions);
        }
        [Fact]
        public void InputBindingCaptureControl_WinPlusRCombo_CapturesBothKeysProperly()
        {
            var capture = new InputBindingCaptureControl();
            capture.OpenForSlot(ProfileEditorSlot.ButtonBack, "SELECT / VIEW", "Turtle WoW", null);

            List<InputAction> confirmedActions = null;
            capture.BindingConfirmed += (slot, actions) => confirmedActions = actions;

            capture.StartListening();
            Assert.True(capture.IsListening);

            // Segura LeftWindows (0x5B) e depois R (0x52)
            capture.RegisterPhysicalVirtualKeyDown(VirtualKey.LeftWindows);
            capture.RegisterPhysicalVirtualKeyDown(VirtualKey.R);
            Assert.Equal(2, capture.CapturedCombo.Count);
            Assert.Null(confirmedActions);

            // Solta R e depois LeftWindows
            capture.RegisterPhysicalVirtualKeyUp(VirtualKey.R);
            capture.RegisterPhysicalVirtualKeyUp(VirtualKey.LeftWindows);

            Assert.False(capture.IsListening);
            Assert.NotNull(confirmedActions);
            Assert.Equal(2, confirmedActions.Count);
            Assert.Equal(VirtualKey.LeftWindows, confirmedActions[0].Key);
            Assert.Equal(VirtualKey.R, confirmedActions[1].Key);
            Assert.Equal("Win + R", InputAction.FormatComboDisplay(confirmedActions));
        }
    }
}
