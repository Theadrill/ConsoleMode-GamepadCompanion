using System.Collections.Generic;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.UI.Controls;
using Xunit;

namespace ConsoleMode.GamepadCompanion.Tests
{
    public sealed class ProfileSystemPhase4Tests
    {
        private static GamepadState MakeState(uint packet, GamepadButtons buttons = GamepadButtons.None, short thumbX = 0, short thumbY = 0, byte rightTrigger = 0)
        {
            return new GamepadState(true, packet, buttons, 0, rightTrigger, thumbX, thumbY, 0, 0);
        }

        [Fact]
        public void InputVisualSelectorControl_InitialState_DefaultsToKeyboardTabAndAKey()
        {
            var selector = new InputVisualSelectorControl();
            selector.OpenForSlot(ProfileEditorSlot.ButtonA, "BOTÃO (A)", "Turtle WoW", null);

            Assert.Equal(0, selector.CurrentTab);
            Assert.Equal(3, selector.KeyboardRow); // Linha 3 (Caps, A, S, D...)
            Assert.Equal(1, selector.KeyboardCol); // Coluna 1 ('A')
            Assert.Empty(selector.CurrentCombo);

            var focused = selector.GetFocusedAction();
            Assert.NotNull(focused);
            Assert.Equal(InputActionType.Key, focused.Type);
            Assert.Equal(VirtualKey.A, focused.Key);
        }

        [Fact]
        public void InputVisualSelectorControl_TabSwitching_LB_RB_NavigatesTabsCyclically()
        {
            var selector = new InputVisualSelectorControl();
            selector.OpenForSlot(ProfileEditorSlot.ButtonA, "BOTÃO (A)", "Turtle WoW", null);

            Assert.Equal(0, selector.CurrentTab);

            // Pressiona RB -> Aba 1 (Mouse)
            selector.ProcessGamepad(MakeState(1, GamepadButtons.RightShoulder), 10);
            Assert.Equal(1, selector.CurrentTab);

            // Solta e pressiona RB novamente -> Aba 2 (Sistema & Especial)
            selector.ProcessGamepad(MakeState(2), 20);
            selector.ProcessGamepad(MakeState(3, GamepadButtons.RightShoulder), 30);
            Assert.Equal(2, selector.CurrentTab);

            // Solta e pressiona RB novamente -> Cicla de volta para Aba 0 (Teclado)
            selector.ProcessGamepad(MakeState(4), 40);
            selector.ProcessGamepad(MakeState(5, GamepadButtons.RightShoulder), 50);
            Assert.Equal(0, selector.CurrentTab);

            // Solta e pressiona LB -> Cicla para trás (Aba 2)
            selector.ProcessGamepad(MakeState(6), 60);
            selector.ProcessGamepad(MakeState(7, GamepadButtons.LeftShoulder), 70);
            Assert.Equal(2, selector.CurrentTab);
        }

        [Fact]
        public void InputVisualSelectorControl_KeyboardSpatialNavigation_ClosestCenter()
        {
            var selector = new InputVisualSelectorControl();
            selector.OpenForSlot(ProfileEditorSlot.ButtonA, "BOTÃO (A)", "Turtle WoW", null);

            // Move para baixo até a linha 5 (LCtrl, Win, LAlt, Espaço...)
            // Linha 3 -> Linha 4
            selector.ProcessGamepad(MakeState(1, GamepadButtons.DPadDown), 10);
            Assert.Equal(4, selector.KeyboardRow);

            // Linha 4 -> Linha 5 (navegação espacial cai em LAlt, col 2)
            selector.ProcessGamepad(MakeState(2), 20);
            selector.ProcessGamepad(MakeState(3, GamepadButtons.DPadDown), 30);
            Assert.Equal(5, selector.KeyboardRow);

            // Move para a direita (col 2 -> col 3: Espaço)
            selector.ProcessGamepad(MakeState(4), 40);
            selector.ProcessGamepad(MakeState(5, GamepadButtons.DPadRight), 50); // Espaço (col 3)

            Assert.Equal(3, selector.KeyboardCol);
            var spaceKey = selector.GetFocusedAction();
            Assert.Equal(VirtualKey.Space, spaceKey.Key);

            // Navegação espacial: Ao subir da barra de espaço (linha 5),
            // a tecla com centro mais próximo na linha 4 deve ser B, N ou V
            selector.ProcessGamepad(MakeState(10), 100);
            selector.ProcessGamepad(MakeState(11, GamepadButtons.DPadUp), 110);

            Assert.Equal(4, selector.KeyboardRow);
            var keyAbove = selector.GetFocusedAction();
            Assert.True(keyAbove.Key == VirtualKey.B || keyAbove.Key == VirtualKey.N || keyAbove.Key == VirtualKey.V);
        }

        [Fact]
        public void InputVisualSelectorControl_SingleKeySelection_SelectsWithA_AndConfirmsWithRT()
        {
            var selector = new InputVisualSelectorControl();
            selector.OpenForSlot(ProfileEditorSlot.ButtonA, "BOTÃO (A)", "Turtle WoW", null);

            List<InputAction> result = null;
            selector.BindingConfirmed += (slot, actions) => result = actions;

            // Focado na tecla 'A' com combo vazio -> prime e pressiona (A) no controle para selecionar
            selector.ProcessGamepad(MakeState(1), 0);
            selector.ProcessGamepad(MakeState(2, GamepadButtons.A), 10);

            // A tecla 'A' foi colocada no CurrentCombo, mas ainda NÃO confirmou
            Assert.Null(result);
            Assert.Single(selector.CurrentCombo);
            Assert.Equal(VirtualKey.A, selector.CurrentCombo[0].Key);

            // Agora pressiona [RT] para salvar e confirmar
            selector.ProcessGamepad(MakeState(3), 20);
            selector.ProcessGamepad(MakeState(4, rightTrigger: 255), 30);

            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(VirtualKey.A, result[0].Key);
        }

        [Fact]
        public void InputVisualSelectorControl_SwitchSelectionWithA_AndConfirmWithRT_ReplacesPreviousSelection()
        {
            // Exemplo exato do usuário:
            // "tipo entro no picker, ja tem a tecla 2 selecionada, vou na tecla F e aperto A, desmarca 2 e marca F, dai eu dou RT para confirmar"
            var selector = new InputVisualSelectorControl();
            selector.OpenForSlot(
                ProfileEditorSlot.ButtonX,
                "BOTÃO (X)",
                "Turtle WoW",
                new List<InputAction> { InputAction.FromKey(VirtualKey.D2) });

            Assert.Single(selector.CurrentCombo);
            Assert.Equal(VirtualKey.D2, selector.CurrentCombo[0].Key);

            List<InputAction> result = null;
            selector.BindingConfirmed += (slot, actions) => result = actions;

            // Navega até a tecla 'F':
            // Na aba 0 (Teclado), linha 3: CapsLock (0), A (1), S (2), D (3), F (4)
            // Prime inicial do controle
            selector.ProcessGamepad(MakeState(1), 0);

            // Move da coluna 1 ('A') para a direita até coluna 4 ('F')
            selector.ProcessGamepad(MakeState(2, GamepadButtons.DPadRight), 10); // 'S'
            selector.ProcessGamepad(MakeState(3), 20);
            selector.ProcessGamepad(MakeState(4, GamepadButtons.DPadRight), 30); // 'D'
            selector.ProcessGamepad(MakeState(5), 40);
            selector.ProcessGamepad(MakeState(6, GamepadButtons.DPadRight), 50); // 'F'
            selector.ProcessGamepad(MakeState(7), 60);

            Assert.Equal(3, selector.KeyboardRow);
            Assert.Equal(4, selector.KeyboardCol);
            var focused = selector.GetFocusedAction();
            Assert.NotNull(focused);
            Assert.Equal(VirtualKey.F, focused.Key);

            // Pressiona (A): deve DESMARCAR a tecla 2 e MARCAR a tecla F
            selector.ProcessGamepad(MakeState(8, GamepadButtons.A), 70);

            Assert.Null(result); // Ainda não confirmou
            Assert.Single(selector.CurrentCombo);
            Assert.Equal(VirtualKey.F, selector.CurrentCombo[0].Key);

            // Pressiona [RT] para salvar e confirmar
            selector.ProcessGamepad(MakeState(9), 80);
            selector.ProcessGamepad(MakeState(10, rightTrigger: 255), 90);

            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(VirtualKey.F, result[0].Key);
        }

        [Fact]
        public void InputVisualSelectorControl_ComboBuildingWithX_AccumulatesAndConfirmsWithRT()
        {
            var selector = new InputVisualSelectorControl();
            selector.OpenForSlot(ProfileEditorSlot.ButtonA, "BOTÃO (A)", "Turtle WoW", null);

            List<InputAction> result = null;
            selector.BindingConfirmed += (slot, actions) => result = actions;

            // 1. Marca 'A' com (X)
            selector.ProcessGamepad(MakeState(1, GamepadButtons.X), 10);
            selector.ProcessGamepad(MakeState(2), 20);

            Assert.Single(selector.CurrentCombo);
            Assert.Equal(VirtualKey.A, selector.CurrentCombo[0].Key);

            // 2. Move para a direita (tecla 'S')
            selector.ProcessGamepad(MakeState(3, GamepadButtons.DPadRight), 30);
            selector.ProcessGamepad(MakeState(4), 40);

            var keyS = selector.GetFocusedAction();
            Assert.Equal(VirtualKey.S, keyS.Key);

            // 3. Marca 'S' com (X)
            selector.ProcessGamepad(MakeState(5, GamepadButtons.X), 50);
            selector.ProcessGamepad(MakeState(6), 60);

            Assert.Equal(2, selector.CurrentCombo.Count);
            Assert.Equal(VirtualKey.A, selector.CurrentCombo[0].Key);
            Assert.Equal(VirtualKey.S, selector.CurrentCombo[1].Key);

            // 4. Confirma o combo com [RT]
            selector.ProcessGamepad(MakeState(7), 65);
            selector.ProcessGamepad(MakeState(8, rightTrigger: 255), 70);

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal(VirtualKey.A, result[0].Key);
            Assert.Equal(VirtualKey.S, result[1].Key);
        }

        [Fact]
        public void InputVisualSelectorControl_ToggleKeyAgainWithX_RemovesFromCombo()
        {
            var selector = new InputVisualSelectorControl();
            selector.OpenForSlot(ProfileEditorSlot.ButtonA, "BOTÃO (A)", "Turtle WoW", null);

            // Adiciona 'A' ao combo
            selector.ToggleFocusedItemInCombo();
            Assert.Single(selector.CurrentCombo);

            // Alterna novamente a mesma tecla 'A' -> remove do combo
            selector.ToggleFocusedItemInCombo();
            Assert.Empty(selector.CurrentCombo);
        }

        [Fact]
        public void InputVisualSelectorControl_ClearComboWithY_EmptiesCurrentCombo()
        {
            var selector = new InputVisualSelectorControl();
            selector.OpenForSlot(
                ProfileEditorSlot.ButtonA,
                "BOTÃO (A)",
                "Turtle WoW",
                new List<InputAction> { InputAction.FromKey(VirtualKey.LeftControl), InputAction.FromKey(VirtualKey.Space) });

            Assert.Equal(2, selector.CurrentCombo.Count);

            // Pressiona (Y) no controle
            selector.ProcessGamepad(MakeState(1, GamepadButtons.Y), 10);

            Assert.Empty(selector.CurrentCombo);
        }

        [Fact]
        public void InputVisualSelectorControl_CancelWithB_FiresCancelRequested()
        {
            var selector = new InputVisualSelectorControl();
            selector.OpenForSlot(ProfileEditorSlot.ButtonA, "BOTÃO (A)", "Turtle WoW", null);

            bool cancelled = false;
            selector.CancelRequested += () => cancelled = true;

            // Pressiona (B) no controle após priming
            selector.ProcessGamepad(MakeState(1), 0);
            selector.ProcessGamepad(MakeState(2, GamepadButtons.B), 10);

            Assert.True(cancelled);
        }

        [Fact]
        public void InputVisualSelectorControl_MouseTab_SelectsAndConfirmsMouseButton()
        {
            var selector = new InputVisualSelectorControl();
            selector.OpenForSlot(ProfileEditorSlot.RightStickAdditional, "STICK DIREITO", "Turtle WoW", null);

            List<InputAction> result = null;
            selector.BindingConfirmed += (slot, actions) => result = actions;

            // Muda para a Aba 1 (Mouse)
            selector.SetTab(1);
            Assert.Equal(1, selector.CurrentTab);

            // Move para baixo (item 1 = Clique Direito)
            selector.ProcessGamepad(MakeState(1, GamepadButtons.DPadDown), 10);
            Assert.Equal(1, selector.MouseIndex);

            var focused = selector.GetFocusedAction();
            Assert.NotNull(focused);
            Assert.Equal(InputActionType.MouseButton, focused.Type);
            Assert.Equal(MouseButton.Right, focused.MouseButton);

            // Seleciona com (A)
            selector.ProcessGamepad(MakeState(2), 20);
            selector.ProcessGamepad(MakeState(3, GamepadButtons.A), 30);
            Assert.Single(selector.CurrentCombo);
            Assert.Equal(MouseButton.Right, selector.CurrentCombo[0].MouseButton);

            // Confirma com [RT]
            selector.ProcessGamepad(MakeState(4), 40);
            selector.ProcessGamepad(MakeState(5, rightTrigger: 255), 50);

            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(InputActionType.MouseButton, result[0].Type);
            Assert.Equal(MouseButton.Right, result[0].MouseButton);
        }

        [Fact]
        public void InputVisualSelectorControl_SystemTab_AudioAndWindowsAndCompanionActions()
        {
            var selector = new InputVisualSelectorControl();
            selector.OpenForSlot(ProfileEditorSlot.ButtonStart, "START", "Turtle WoW", null);

            selector.SetTab(2);
            Assert.Equal(2, selector.CurrentTab);
            Assert.Equal(0, selector.SysColumn); // Áudio & Mídia
            Assert.Equal(0, selector.SysRow);    // Volume +

            // Confirma que o item padrão da coluna 0 é Volume +
            var actionAudio = selector.GetFocusedAction();
            Assert.Equal(VirtualKey.VolumeUp, actionAudio.Key);

            // Move para Coluna 1 (Janelas & Captura)
            selector.ProcessGamepad(MakeState(1, GamepadButtons.DPadRight), 10);
            Assert.Equal(1, selector.SysColumn);

            // Item 0 da coluna 1 é PrintScreen
            var actionWin0 = selector.GetFocusedAction();
            Assert.Equal(VirtualKey.PrintScreen, actionWin0.Key);

            // Move para baixo na coluna 1 (Item 1 = Alt+Enter, Item 2 = Alt+Tab, Item 3 = ShowDesktop)
            selector.ProcessGamepad(MakeState(2), 20);
            selector.ProcessGamepad(MakeState(3, GamepadButtons.DPadDown), 30);
            selector.ProcessGamepad(MakeState(4), 40);
            selector.ProcessGamepad(MakeState(5, GamepadButtons.DPadDown), 50);

            Assert.Equal(2, selector.SysRow);
            var actionAltTab = selector.GetFocusedAction();
            Assert.Equal(InputActionType.SpecialAction, actionAltTab.Type);
            Assert.Equal(CompanionActionType.AltTab, actionAltTab.SpecialAction);

            // Move para Coluna 2 (Funções do Companion)
            selector.ProcessGamepad(MakeState(6), 60);
            selector.ProcessGamepad(MakeState(7, GamepadButtons.DPadRight), 70);

            Assert.Equal(2, selector.SysColumn);
            // Item 1 na Coluna 2 é ToggleClipCursor (já que estávamos na linha >= 1)
            var actionCompanion = selector.GetFocusedAction();
            Assert.Equal(InputActionType.SpecialAction, actionCompanion.Type);
            Assert.True(actionCompanion.SpecialAction == CompanionActionType.CenterCursor ||
                        actionCompanion.SpecialAction == CompanionActionType.ToggleClipCursor);
        }

        [Fact]
        public void InputVisualSelectorControl_PreFillsFromExistingActions_AndSetsAppropriateInitialTab()
        {
            var selector = new InputVisualSelectorControl();

            // 1. Ação de Mouse abre na aba 1
            selector.OpenForSlot(
                ProfileEditorSlot.RightStickAdditional,
                "STICK DIREITO",
                "Turtle WoW",
                new List<InputAction> { InputAction.FromMouseButton(MouseButton.Right) });

            Assert.Equal(1, selector.CurrentTab);
            Assert.Single(selector.CurrentCombo);
            Assert.Equal(MouseButton.Right, selector.CurrentCombo[0].MouseButton);

            // 2. Ação Especial abre na aba 2
            selector.OpenForSlot(
                ProfileEditorSlot.ButtonBack,
                "SELECT",
                "Turtle WoW",
                new List<InputAction> { InputAction.FromSpecial(CompanionActionType.CenterCursor) });

            Assert.Equal(2, selector.CurrentTab);
            Assert.Single(selector.CurrentCombo);
            Assert.Equal(CompanionActionType.CenterCursor, selector.CurrentCombo[0].SpecialAction);

            // 3. Tecla normal abre na aba 0
            selector.OpenForSlot(
                ProfileEditorSlot.ButtonA,
                "BOTÃO (A)",
                "Turtle WoW",
                new List<InputAction> { InputAction.FromKey(VirtualKey.F9) });

            Assert.Equal(0, selector.CurrentTab);
            Assert.Single(selector.CurrentCombo);
            Assert.Equal(VirtualKey.F9, selector.CurrentCombo[0].Key);
        }

        [Fact]
        public void InputBindingCaptureControl_Option1_FiresOpenVisualSelectorRequested()
        {
            var capture = new InputBindingCaptureControl();
            capture.OpenForSlot(ProfileEditorSlot.ButtonA, "BOTÃO (A)", "Turtle WoW", new[] { InputAction.FromKey(VirtualKey.F9) });

            bool opened = false;
            ProfileEditorSlot receivedSlot = ProfileEditorSlot.ButtonLT;
            List<InputAction> receivedActions = null;

            capture.OpenVisualSelectorRequested += (slot, title, actions) =>
            {
                opened = true;
                receivedSlot = slot;
                receivedActions = actions;
            };

            // Navega para baixo para focar na opção 1 ([🖮 Escolher Botão / Botões])
            capture.ProcessGamepad(MakeState(1, GamepadButtons.DPadDown), 10);
            Assert.Equal(1, capture.FocusedOptionIndex);

            // Aciona com o botão A
            capture.ProcessGamepad(MakeState(2), 20);
            capture.ProcessGamepad(MakeState(3, GamepadButtons.A), 30);

            Assert.True(opened);
            Assert.Equal(ProfileEditorSlot.ButtonA, receivedSlot);
            Assert.NotNull(receivedActions);
            Assert.Single(receivedActions);
            Assert.Equal(VirtualKey.F9, receivedActions[0].Key);
        }
    }
}
