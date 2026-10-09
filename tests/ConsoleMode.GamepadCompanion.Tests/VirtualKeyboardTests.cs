using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.UI.VirtualKeyboard;
using Xunit;

namespace ConsoleMode.GamepadCompanion.Tests
{
    public sealed class VirtualKeyboardTests
    {
        // -------------------------------------------------------------
        // Testes do Buffer de Texto (TextInputBuffer)
        // -------------------------------------------------------------
        [Fact]
        public void TextInputBuffer_InitialText_SetsTextAndCursorAtEnd()
        {
            var buffer = new TextInputBuffer("Warcraft");
            Assert.Equal("Warcraft", buffer.Text);
            Assert.Equal(8, buffer.Length);
            Assert.Equal(8, buffer.CursorPosition);
        }

        [Fact]
        public void TextInputBuffer_Insert_InsertsAtCursorPosition()
        {
            var buffer = new TextInputBuffer("WoW");
            buffer.Insert(" Classic");
            Assert.Equal("WoW Classic", buffer.Text);
            Assert.Equal(11, buffer.CursorPosition);

            // Move o cursor para o início e insere prefixo
            buffer.CursorPosition = 0;
            buffer.Insert("Turtle ");
            Assert.Equal("Turtle WoW Classic", buffer.Text);
            Assert.Equal(7, buffer.CursorPosition);
        }

        [Fact]
        public void TextInputBuffer_Backspace_RemovesCharacterBeforeCursor()
        {
            var buffer = new TextInputBuffer("Game");
            buffer.Backspace();
            Assert.Equal("Gam", buffer.Text);
            Assert.Equal(3, buffer.CursorPosition);

            // Backspace na posição zero não deve quebrar
            buffer.CursorPosition = 0;
            buffer.Backspace();
            Assert.Equal("Gam", buffer.Text);
            Assert.Equal(0, buffer.CursorPosition);
        }

        [Fact]
        public void TextInputBuffer_Delete_RemovesCharacterAfterCursor()
        {
            var buffer = new TextInputBuffer("Play");
            buffer.CursorPosition = 0;
            buffer.Delete(); // Remove 'P'
            Assert.Equal("lay", buffer.Text);
            Assert.Equal(0, buffer.CursorPosition);

            buffer.CursorPosition = buffer.Length;
            buffer.Delete(); // No final, não deve quebrar
            Assert.Equal("lay", buffer.Text);
        }

        [Fact]
        public void TextInputBuffer_MoveCursor_ClampsWithinTextBounds()
        {
            var buffer = new TextInputBuffer("Halo");
            buffer.MoveCursorLeft();
            Assert.Equal(3, buffer.CursorPosition);

            buffer.MoveCursorLeft();
            buffer.MoveCursorLeft();
            buffer.MoveCursorLeft();
            buffer.MoveCursorLeft(); // Excede início
            Assert.Equal(0, buffer.CursorPosition);

            buffer.MoveCursorRight();
            Assert.Equal(1, buffer.CursorPosition);

            buffer.CursorPosition = 999; // Clamping
            Assert.Equal(4, buffer.CursorPosition);

            buffer.CursorPosition = -5; // Clamping
            Assert.Equal(0, buffer.CursorPosition);
        }

        [Fact]
        public void TextInputBuffer_Clear_EmptiesTextAndResetsCursor()
        {
            var buffer = new TextInputBuffer("Some long text to clear");
            bool textChangedFired = false;
            buffer.TextChanged += () => textChangedFired = true;

            buffer.Clear();
            Assert.Equal(string.Empty, buffer.Text);
            Assert.Equal(0, buffer.Length);
            Assert.Equal(0, buffer.CursorPosition);
            Assert.True(textChangedFired);
        }

        // -------------------------------------------------------------
        // Testes do Layout de 5 Linhas (KeyboardLayoutProvider)
        // -------------------------------------------------------------
        [Fact]
        public void KeyboardLayoutProvider_CreatesExactlyFiveRows()
        {
            var layout = KeyboardLayoutProvider.CreateLayout();
            Assert.NotNull(layout);
            Assert.Equal(5, layout.Count);
        }

        [Fact]
        public void KeyboardLayoutProvider_Row0_ContainsWindowsSymbolsAndBackspace()
        {
            var layout = KeyboardLayoutProvider.CreateLayout();
            var row0 = layout[0];

            // Deve conter os símbolos de path do Windows
            Assert.Contains(row0, k => k.PrimaryLabel == ":");
            Assert.Contains(row0, k => k.PrimaryLabel == "\\");
            Assert.Contains(row0, k => k.PrimaryLabel == "/");
            Assert.Contains(row0, k => k.PrimaryLabel == "_");
            Assert.Contains(row0, k => k.PrimaryLabel == "-");
            Assert.Contains(row0, k => k.PrimaryLabel == ".");

            // Última tecla deve ser Backspace
            var lastKey = row0[row0.Count - 1];
            Assert.Equal(VirtualKeyType.Backspace, lastKey.KeyType);
        }

        [Fact]
        public void KeyboardLayoutProvider_Row1_StartsWithTabAndContainsQWERTY()
        {
            var layout = KeyboardLayoutProvider.CreateLayout();
            var row1 = layout[1];

            Assert.Equal(VirtualKeyType.Tab, row1[0].KeyType);
            Assert.Equal("q", row1[1].PrimaryLabel);
            Assert.Equal("w", row1[2].PrimaryLabel);
            Assert.Equal("e", row1[3].PrimaryLabel);
            Assert.Equal("r", row1[4].PrimaryLabel);
            Assert.Equal("t", row1[5].PrimaryLabel);
            Assert.Equal("y", row1[6].PrimaryLabel);
        }

        [Fact]
        public void KeyboardLayoutProvider_Row2_StartsWithCapsAndEndsWithEnter()
        {
            var layout = KeyboardLayoutProvider.CreateLayout();
            var row2 = layout[2];

            Assert.Equal(VirtualKeyType.CapsLock, row2[0].KeyType);
            Assert.Equal("a", row2[1].PrimaryLabel);
            Assert.Equal("s", row2[2].PrimaryLabel);
            Assert.Equal("d", row2[3].PrimaryLabel);
            Assert.Equal(VirtualKeyType.Enter, row2[row2.Count - 1].KeyType);
        }

        [Fact]
        public void KeyboardLayoutProvider_Row3_StartsWithShift()
        {
            var layout = KeyboardLayoutProvider.CreateLayout();
            var row3 = layout[3];

            Assert.Equal(VirtualKeyType.Shift, row3[0].KeyType);
            Assert.Equal("z", row3[1].PrimaryLabel);
            Assert.Equal("x", row3[2].PrimaryLabel);
            Assert.Equal("c", row3[3].PrimaryLabel);
        }

        [Fact]
        public void KeyboardLayoutProvider_Row4_ContainsUtilitiesAndSpace()
        {
            var layout = KeyboardLayoutProvider.CreateLayout();
            var row4 = layout[4];

            Assert.Contains(row4, k => k.KeyType == VirtualKeyType.Copy);
            Assert.Contains(row4, k => k.KeyType == VirtualKeyType.Paste);
            Assert.Contains(row4, k => k.KeyType == VirtualKeyType.Space);
            Assert.Contains(row4, k => k.KeyType == VirtualKeyType.CursorLeft);
            Assert.Contains(row4, k => k.KeyType == VirtualKeyType.CursorRight);
            Assert.Contains(row4, k => k.KeyType == VirtualKeyType.ClearAll);
        }

        [Fact]
        public void KeyboardLayoutProvider_IndicesAreProperlyConfigured()
        {
            var layout = KeyboardLayoutProvider.CreateLayout();
            for (int r = 0; r < layout.Count; r++)
            {
                var row = layout[r];
                for (int c = 0; c < row.Count; c++)
                {
                    Assert.Equal(r, row[c].RowIndex);
                    Assert.Equal(c, row[c].ColIndex);
                    Assert.True(row[c].WidthWeight > 0);
                }
            }
        }

        // -------------------------------------------------------------
        // Testes de Definição de Tecla (VirtualKeyDefinition)
        // -------------------------------------------------------------
        [Fact]
        public void VirtualKeyDefinition_LabelsRespectCapsAndSymbols()
        {
            var keyA = new VirtualKeyDefinition
            {
                PrimaryLabel = "a",
                ShiftLabel = "A",
                SymbolsLabel = "+"
            };

            Assert.Equal("a", keyA.GetDisplayLabel(isCaps: false, isSymbols: false));
            Assert.Equal("A", keyA.GetDisplayLabel(isCaps: true, isSymbols: false));
            Assert.Equal("+", keyA.GetDisplayLabel(isCaps: false, isSymbols: true));
            Assert.Equal("+", keyA.GetDisplayLabel(isCaps: true, isSymbols: true));
        }

        // -------------------------------------------------------------
        // Testes do Componente Visual (VirtualKeyboardControl)
        // -------------------------------------------------------------
        [Fact]
        public void VirtualKeyboardControl_InitialFocus_StartsOnHomeRow()
        {
            var kb = new VirtualKeyboardControl("Test");
            Assert.Equal(2, kb.FocusedRow);
            Assert.Equal(1, kb.FocusedCol);
            Assert.Equal("Test", kb.Buffer.Text);
            Assert.False(kb.IsCaps);
            Assert.False(kb.IsSymbols);
        }

        [Fact]
        public void VirtualKeyboardControl_ExecuteKey_InsertsCharactersAndTriggersPulse()
        {
            var kb = new VirtualKeyboardControl();
            var keyA = new VirtualKeyDefinition { KeyType = VirtualKeyType.Character, PrimaryLabel = "a" };

            kb.ExecuteKey(keyA);
            Assert.Equal("a", kb.Buffer.Text);

            var keyB = new VirtualKeyDefinition { KeyType = VirtualKeyType.Character, PrimaryLabel = "b" };
            kb.ExecuteKey(keyB);
            Assert.Equal("ab", kb.Buffer.Text);

            var keyBack = new VirtualKeyDefinition { KeyType = VirtualKeyType.Backspace };
            kb.ExecuteKey(keyBack);
            Assert.Equal("a", kb.Buffer.Text);
        }

        [Fact]
        public void VirtualKeyboardControl_Gamepad_LT_ClearsBuffer()
        {
            var kb = new VirtualKeyboardControl("ClearMe");
            var state = new GamepadState(true, 1, GamepadButtons.None, leftTrigger: 150, rightTrigger: 0, leftThumbX: 0, leftThumbY: 0, rightThumbX: 0, rightThumbY: 0);

            kb.ProcessGamepad(state, 100);
            Assert.Equal(string.Empty, kb.Buffer.Text);
        }

        [Fact]
        public void VirtualKeyboardControl_Gamepad_LB_RB_TogglesCaps()
        {
            var kb = new VirtualKeyboardControl();
            Assert.False(kb.IsCaps);

            var stateLb = new GamepadState(true, 1, GamepadButtons.LeftShoulder, leftTrigger: 0, rightTrigger: 0, leftThumbX: 0, leftThumbY: 0, rightThumbX: 0, rightThumbY: 0);

            kb.ProcessGamepad(stateLb, 100);
            Assert.True(kb.IsCaps);

            var stateRb = new GamepadState(true, 2, GamepadButtons.RightShoulder, leftTrigger: 0, rightTrigger: 0, leftThumbX: 0, leftThumbY: 0, rightThumbX: 0, rightThumbY: 0);

            // Solta primeiro para testar pulso do RB
            kb.ProcessGamepad(new GamepadState(true, 3, GamepadButtons.None, 0, 0, 0, 0, 0, 0), 150);
            kb.ProcessGamepad(stateRb, 200);
            Assert.False(kb.IsCaps);
        }

        [Fact]
        public void VirtualKeyboardControl_Gamepad_RT_TriggersConfirmed()
        {
            var kb = new VirtualKeyboardControl("SaveText");
            string confirmedResult = null;
            kb.Confirmed += text => confirmedResult = text;

            var stateRt = new GamepadState(true, 1, GamepadButtons.None, leftTrigger: 0, rightTrigger: 150, leftThumbX: 0, leftThumbY: 0, rightThumbX: 0, rightThumbY: 0);
            kb.ProcessGamepad(stateRt, 100);

            Assert.Equal("SaveText", confirmedResult);
        }

        [Fact]
        public void VirtualKeyboardControl_Gamepad_B_TriggersCancelled()
        {
            var kb = new VirtualKeyboardControl("AbortText");
            bool cancelledFired = false;
            kb.Cancelled += () => cancelledFired = true;

            // Libera B primeiro caso estivesse preso
            kb.ProcessGamepad(new GamepadState(true, 1, GamepadButtons.None, 0, 0, 0, 0, 0, 0), 100);

            var stateB = new GamepadState(true, 2, GamepadButtons.B, 0, 0, 0, 0, 0, 0);
            kb.ProcessGamepad(stateB, 150);

            Assert.True(cancelledFired);
        }

        [Fact]
        public void VirtualKeyboardControl_Gamepad_ButtonX_BackspacesSingleCharacter()
        {
            var kb = new VirtualKeyboardControl("Texto");

            // Libera X caso tenha vindo acionado
            kb.ProcessGamepad(new GamepadState(true, 1, GamepadButtons.None, 0, 0, 0, 0, 0, 0), 100);

            // Pressiona X
            var stateX = new GamepadState(true, 2, GamepadButtons.X, 0, 0, 0, 0, 0, 0);
            kb.ProcessGamepad(stateX, 150);

            Assert.Equal("Text", kb.Buffer.Text);
        }

        [Fact]
        public void VirtualKeyboardControl_Gamepad_ButtonX_AutoRepeatsWhenHeld()
        {
            var kb = new VirtualKeyboardControl("12345");

            // Libera X primeiro
            kb.ProcessGamepad(new GamepadState(true, 1, GamepadButtons.None, 0, 0, 0, 0, 0, 0), 100);

            // Primeiro toque no tempo 150ms: apaga '5' (fica '1234')
            var stateX = new GamepadState(true, 2, GamepadButtons.X, 0, 0, 0, 0, 0, 0);
            kb.ProcessGamepad(stateX, 150);
            Assert.Equal("1234", kb.Buffer.Text);

            // Continua segurando antes do tempo de delay (200ms - decorrido 50ms): NÃO apaga ainda
            kb.ProcessGamepad(stateX, 200);
            Assert.Equal("1234", kb.Buffer.Text);

            // Atinge o delay inicial (150 + 280 = 430ms): apaga '4' (fica '123')
            kb.ProcessGamepad(stateX, 435);
            Assert.Equal("123", kb.Buffer.Text);

            // Próximo intervalo de repetição (435 + 70 = 505ms): apaga '3' (fica '12')
            kb.ProcessGamepad(stateX, 510);
            Assert.Equal("12", kb.Buffer.Text);
        }

        [Fact]
        public void VirtualKeyboardControl_CursorNavigation_InsertsInMiddle()
        {
            var kb = new VirtualKeyboardControl("AB");
            // Cursor começa no final (posição 2)
            Assert.Equal(2, kb.Buffer.CursorPosition);

            // Executa tecla CursorLeft
            var leftKey = new VirtualKeyDefinition { KeyType = VirtualKeyType.CursorLeft };
            kb.ExecuteKey(leftKey);
            Assert.Equal(1, kb.Buffer.CursorPosition);

            // Digita caractere 'X' no meio
            var charKey = new VirtualKeyDefinition { KeyType = VirtualKeyType.Character, PrimaryLabel = "X" };
            kb.ExecuteKey(charKey);
            Assert.Equal("AXB", kb.Buffer.Text);
            Assert.Equal(2, kb.Buffer.CursorPosition);
        }

        [Fact]
        public void VirtualKeyboardControl_ToggleSymbols_ChangesLabels()
        {
            var kb = new VirtualKeyboardControl();
            Assert.False(kb.IsSymbols);

            var toggleKey = new VirtualKeyDefinition { KeyType = VirtualKeyType.ToggleSymbols };
            kb.ExecuteKey(toggleKey);
            Assert.True(kb.IsSymbols);

            kb.ExecuteKey(toggleKey);
            Assert.False(kb.IsSymbols);
        }

        // -------------------------------------------------------------
        // Testes de Navegação Gamepad no GameConfigPanel
        // -------------------------------------------------------------
        [Fact]
        public void GameConfigPanel_Gamepad_Navigation_And_VirtualKeyboardTrigger()
        {
            var panel = new ConsoleMode.GamepadCompanion.UI.Controls.GameConfigPanel();
            var game = new GameEntry
            {
                Id = "test-1",
                Name = "Portal 2",
                MainExecutable = "portal2.exe"
            };
            panel.EditGame(game);
            panel.ResetInputState();

            string requestedTitle = null;
            string requestedText = null;
            Action<string> confirmCallback = null;

            panel.RequestVirtualKeyboard += (title, text, onConfirm) =>
            {
                requestedTitle = title;
                requestedText = text;
                confirmCallback = onConfirm;
            };

            // 1. Pressionar A no primeiro campo focado (Nome) deve abrir o teclado virtual com "Portal 2"
            var stateNeutral = new GamepadState(true, 1, GamepadButtons.None, 0, 0, 0, 0, 0, 0);
            var stateA = new GamepadState(true, 2, GamepadButtons.A, 0, 0, 0, 0, 0, 0);

            panel.ProcessGamepad(stateNeutral, 100);
            panel.ProcessGamepad(stateA, 120);

            Assert.NotNull(requestedTitle);
            Assert.Equal("Portal 2", requestedText);
            Assert.NotNull(confirmCallback);

            // Executa o callback simulando o teclado digitando e confirmando novo nome
            confirmCallback("Portal 2 Reloaded");

            // 2. Agora navega para baixo com D-Pad Down e pressiona B para cancelar
            bool cancelFired = false;
            panel.CancelRequested += () => cancelFired = true;

            var stateDown = new GamepadState(true, 3, GamepadButtons.DPadDown, 0, 0, 0, 0, 0, 0);
            panel.ProcessGamepad(stateNeutral, 200);
            panel.ProcessGamepad(stateDown, 220);

            var stateB = new GamepadState(true, 4, GamepadButtons.B, 0, 0, 0, 0, 0, 0);
            panel.ProcessGamepad(stateNeutral, 300);
            panel.ProcessGamepad(stateB, 320);

            Assert.True(cancelFired);
        }

        [Fact]
        public void FocusOverlayPanel_RefreshLiveBackground_CompositesControlsCleanly()
        {
            var form = new Form { Width = 600, Height = 400 };
            var overlay = new ConsoleMode.GamepadCompanion.UI.Controls.FocusOverlayPanel();
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Red };
            form.Controls.Add(overlay);
            form.Controls.Add(panel);

            overlay.ShowVirtualKeyboard(form, "Busca", "test", text => { });
            Assert.True(overlay.IsVirtualKeyboardOpen);

            overlay.RefreshLiveBackground(form);

            overlay.CloseVirtualKeyboard();
            Assert.False(overlay.IsVirtualKeyboardOpen);
        }

        [Fact]
        public void VirtualKeyboardControl_CursorBlink_InitialStateAndInputReset()
        {
            using var kb = new VirtualKeyboardControl("Test");
            Assert.True(kb.CursorVisible);

            // Simula tick do timer desligando o cursor
            kb.ToggleCursorBlinkForTesting();
            Assert.False(kb.CursorVisible);

            // Reset manual liga o cursor imediatamente
            kb.ResetCursorBlink();
            Assert.True(kb.CursorVisible);

            // Desliga de novo
            kb.ToggleCursorBlinkForTesting();
            Assert.False(kb.CursorVisible);

            // Mover cursor para esquerda com tecla deve acionar reset automático para visível
            var leftKey = new VirtualKeyDefinition { KeyType = VirtualKeyType.CursorLeft };
            kb.ExecuteKey(leftKey);
            Assert.True(kb.CursorVisible);

            // Desliga de novo
            kb.ToggleCursorBlinkForTesting();
            Assert.False(kb.CursorVisible);

            // Digitar caractere deve ligar o cursor imediatamente
            var charKey = new VirtualKeyDefinition { KeyType = VirtualKeyType.Character, PrimaryLabel = "X" };
            kb.ExecuteKey(charKey);
            Assert.True(kb.CursorVisible);

            // Desliga de novo
            kb.ToggleCursorBlinkForTesting();
            Assert.False(kb.CursorVisible);

            // Backspace deve ligar o cursor imediatamente
            var backKey = new VirtualKeyDefinition { KeyType = VirtualKeyType.Backspace };
            kb.ExecuteKey(backKey);
            Assert.True(kb.CursorVisible);
        }

        [Fact]
        public void VirtualKeyboardControl_GamepadY_InsertsSpace()
        {
            using var kb = new VirtualKeyboardControl("Hello");
            kb.ResetGamepadState();

            // Libera estado de abertura (neutro)
            var stateNeutral = new GamepadState(true, 1, GamepadButtons.None, 0, 0, 0, 0, 0, 0);
            kb.ProcessGamepad(stateNeutral, 50);

            // Pressionar Y no gamepad deve inserir espaço
            var stateY = new GamepadState(true, 2, GamepadButtons.Y, 0, 0, 0, 0, 0, 0);
            kb.ProcessGamepad(stateY, 100);

            Assert.Equal("Hello ", kb.Buffer.Text);

            // Soltar Y e pressionar novamente insere outro espaço
            kb.ProcessGamepad(stateNeutral, 150);

            var stateY2 = new GamepadState(true, 3, GamepadButtons.Y, 0, 0, 0, 0, 0, 0);
            kb.ProcessGamepad(stateY2, 200);

            Assert.Equal("Hello  ", kb.Buffer.Text);
        }

        [Fact]
        public void VirtualKeyboardControl_PhysicalKeyboard_TypingAndControlsWork()
        {
            using var kb = new VirtualKeyboardControl("Steam");

            // 1. Digitação de caracteres físicos via HandlePhysicalKeyPress
            kb.HandlePhysicalKeyPress(' ');
            kb.HandlePhysicalKeyPress('D');
            kb.HandlePhysicalKeyPress('B');
            Assert.Equal("Steam DB", kb.Buffer.Text);

            // 2. Backspace via HandlePhysicalKeyDown
            kb.HandlePhysicalKeyDown(new KeyEventArgs(Keys.Back));
            Assert.Equal("Steam D", kb.Buffer.Text);

            // 3. Espaço via KeyDown
            kb.HandlePhysicalKeyDown(new KeyEventArgs(Keys.Space));
            Assert.Equal("Steam D ", kb.Buffer.Text);

            // 4. Navegação do cursor
            kb.HandlePhysicalKeyDown(new KeyEventArgs(Keys.Home));
            Assert.Equal(0, kb.Buffer.CursorPosition);

            kb.HandlePhysicalKeyPress('X');
            Assert.Equal("XSteam D ", kb.Buffer.Text);

            // 5. Delete (apaga o caractere à frente)
            kb.HandlePhysicalKeyDown(new KeyEventArgs(Keys.Home));
            kb.HandlePhysicalKeyDown(new KeyEventArgs(Keys.Delete));
            Assert.Equal("Steam D ", kb.Buffer.Text);

            // 6. Enter para confirmar
            string confirmedText = null;
            kb.Confirmed += text => confirmedText = text;
            kb.HandlePhysicalKeyDown(new KeyEventArgs(Keys.Enter));
            Assert.Equal("Steam D ", confirmedText);

            // 7. Escape para cancelar
            bool cancelled = false;
            kb.Cancelled += () => cancelled = true;
            kb.HandlePhysicalKeyDown(new KeyEventArgs(Keys.Escape));
            Assert.True(cancelled);
        }
    }
}
