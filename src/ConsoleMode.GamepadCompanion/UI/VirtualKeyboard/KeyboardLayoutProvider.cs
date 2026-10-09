using System.Collections.Generic;

namespace ConsoleMode.GamepadCompanion.UI.VirtualKeyboard
{
    /// <summary>
    /// Fornecedor de layout completo de 5 linhas estilo Couch Gaming para o Teclado Virtual.
    /// Inclui linha de símbolos rápidos do Windows (:, \, /, _, -, .), QWERTY e utilitários.
    /// </summary>
    public static class KeyboardLayoutProvider
    {
        public static List<List<VirtualKeyDefinition>> CreateLayout()
        {
            var rows = new List<List<VirtualKeyDefinition>>(5);

            // -------------------------------------------------------------
            // Linha 0: Símbolos Windows & Toggle & Backspace
            // -------------------------------------------------------------
            var r0 = new List<VirtualKeyDefinition>
            {
                new VirtualKeyDefinition { KeyId = "colon", PrimaryLabel = ":", ShiftLabel = "1", SymbolsLabel = "!", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "backslash", PrimaryLabel = "\\", ShiftLabel = "2", SymbolsLabel = "@", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "slash", PrimaryLabel = "/", ShiftLabel = "3", SymbolsLabel = "#", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "underscore", PrimaryLabel = "_", ShiftLabel = "4", SymbolsLabel = "$", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "dash", PrimaryLabel = "-", ShiftLabel = "5", SymbolsLabel = "%", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "dot", PrimaryLabel = ".", ShiftLabel = "6", SymbolsLabel = "^", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "lparen", PrimaryLabel = "(", ShiftLabel = "7", SymbolsLabel = "&", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "rparen", PrimaryLabel = ")", ShiftLabel = "8", SymbolsLabel = "*", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "quote", PrimaryLabel = "\"", ShiftLabel = "9", SymbolsLabel = "(", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "at", PrimaryLabel = "@", ShiftLabel = "0", SymbolsLabel = ")", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "toggle_sym", PrimaryLabel = "#+=", ShiftLabel = "#+=", SymbolsLabel = "ABC", KeyType = VirtualKeyType.ToggleSymbols, WidthWeight = 1.3f },
                new VirtualKeyDefinition { KeyId = "backspace", PrimaryLabel = "⌫", ShiftLabel = "⌫", SymbolsLabel = "⌫", KeyType = VirtualKeyType.Backspace, WidthWeight = 1.5f }
            };
            SetRowAndColIndices(r0, 0);
            rows.Add(r0);

            // -------------------------------------------------------------
            // Linha 1: Tab + QWERTY superior + colchetes
            // -------------------------------------------------------------
            var r1 = new List<VirtualKeyDefinition>
            {
                new VirtualKeyDefinition { KeyId = "tab", PrimaryLabel = "Tab", ShiftLabel = "Tab", SymbolsLabel = "Tab", KeyType = VirtualKeyType.Tab, WidthWeight = 1.2f },
                new VirtualKeyDefinition { KeyId = "q", PrimaryLabel = "q", ShiftLabel = "Q", SymbolsLabel = "1", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "w", PrimaryLabel = "w", ShiftLabel = "W", SymbolsLabel = "2", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "e", PrimaryLabel = "e", ShiftLabel = "E", SymbolsLabel = "3", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "r", PrimaryLabel = "r", ShiftLabel = "R", SymbolsLabel = "4", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "t", PrimaryLabel = "t", ShiftLabel = "T", SymbolsLabel = "5", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "y", PrimaryLabel = "y", ShiftLabel = "Y", SymbolsLabel = "6", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "u", PrimaryLabel = "u", ShiftLabel = "U", SymbolsLabel = "7", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "i", PrimaryLabel = "i", ShiftLabel = "I", SymbolsLabel = "8", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "o", PrimaryLabel = "o", ShiftLabel = "O", SymbolsLabel = "9", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "p", PrimaryLabel = "p", ShiftLabel = "P", SymbolsLabel = "0", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "lbracket", PrimaryLabel = "[", ShiftLabel = "{", SymbolsLabel = "<", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "rbracket", PrimaryLabel = "]", ShiftLabel = "}", SymbolsLabel = ">", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "pipe", PrimaryLabel = "|", ShiftLabel = "~", SymbolsLabel = "=", WidthWeight = 1.0f }
            };
            SetRowAndColIndices(r1, 1);
            rows.Add(r1);

            // -------------------------------------------------------------
            // Linha 2: Caps + ASDFGH + Enter
            // -------------------------------------------------------------
            var r2 = new List<VirtualKeyDefinition>
            {
                new VirtualKeyDefinition { KeyId = "caps", PrimaryLabel = "Caps", ShiftLabel = "CAPS", SymbolsLabel = "Caps", KeyType = VirtualKeyType.CapsLock, WidthWeight = 1.4f },
                new VirtualKeyDefinition { KeyId = "a", PrimaryLabel = "a", ShiftLabel = "A", SymbolsLabel = "+", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "s", PrimaryLabel = "s", ShiftLabel = "S", SymbolsLabel = "-", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "d", PrimaryLabel = "d", ShiftLabel = "D", SymbolsLabel = "*", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "f", PrimaryLabel = "f", ShiftLabel = "F", SymbolsLabel = "/", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "g", PrimaryLabel = "g", ShiftLabel = "G", SymbolsLabel = "=", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "h", PrimaryLabel = "h", ShiftLabel = "H", SymbolsLabel = "%", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "j", PrimaryLabel = "j", ShiftLabel = "J", SymbolsLabel = "&", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "k", PrimaryLabel = "k", ShiftLabel = "K", SymbolsLabel = "|", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "l", PrimaryLabel = "l", ShiftLabel = "L", SymbolsLabel = "~", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "semicolon", PrimaryLabel = ";", ShiftLabel = ":", SymbolsLabel = "^", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "squote", PrimaryLabel = "'", ShiftLabel = "\"", SymbolsLabel = "`", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "enter", PrimaryLabel = "↵ Enter", ShiftLabel = "↵ Enter", SymbolsLabel = "↵ Enter", KeyType = VirtualKeyType.Enter, WidthWeight = 1.8f }
            };
            SetRowAndColIndices(r2, 2);
            rows.Add(r2);

            // -------------------------------------------------------------
            // Linha 3: Shift + ZXCVBN + Pontuação
            // -------------------------------------------------------------
            var r3 = new List<VirtualKeyDefinition>
            {
                new VirtualKeyDefinition { KeyId = "shift", PrimaryLabel = "Shift", ShiftLabel = "SHIFT", SymbolsLabel = "Shift", KeyType = VirtualKeyType.Shift, WidthWeight = 1.5f },
                new VirtualKeyDefinition { KeyId = "z", PrimaryLabel = "z", ShiftLabel = "Z", SymbolsLabel = "!", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "x", PrimaryLabel = "x", ShiftLabel = "X", SymbolsLabel = "@", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "c", PrimaryLabel = "c", ShiftLabel = "C", SymbolsLabel = "#", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "v", PrimaryLabel = "v", ShiftLabel = "V", SymbolsLabel = "$", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "b", PrimaryLabel = "b", ShiftLabel = "B", SymbolsLabel = "_", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "n", PrimaryLabel = "n", ShiftLabel = "N", SymbolsLabel = "-", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "m", PrimaryLabel = "m", ShiftLabel = "M", SymbolsLabel = "+", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "comma", PrimaryLabel = ",", ShiftLabel = "<", SymbolsLabel = ",", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "period", PrimaryLabel = ".", ShiftLabel = ">", SymbolsLabel = ".", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "fslash", PrimaryLabel = "/", ShiftLabel = "?", SymbolsLabel = "/", WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "question", PrimaryLabel = "?", ShiftLabel = "!", SymbolsLabel = "?", WidthWeight = 1.0f }
            };
            SetRowAndColIndices(r3, 3);
            rows.Add(r3);

            // -------------------------------------------------------------
            // Linha 4: Utilitários & Navegação & Espaço
            // -------------------------------------------------------------
            var r4 = new List<VirtualKeyDefinition>
            {
                new VirtualKeyDefinition { KeyId = "copy", PrimaryLabel = "Copiar", ShiftLabel = "Copiar", SymbolsLabel = "Copiar", KeyType = VirtualKeyType.Copy, WidthWeight = 1.3f },
                new VirtualKeyDefinition { KeyId = "space", PrimaryLabel = "Espaço", ShiftLabel = "Espaço", SymbolsLabel = "Espaço", KeyType = VirtualKeyType.Space, WidthWeight = 4.8f },
                new VirtualKeyDefinition { KeyId = "paste", PrimaryLabel = "Colar", ShiftLabel = "Colar", SymbolsLabel = "Colar", KeyType = VirtualKeyType.Paste, WidthWeight = 1.3f },
                new VirtualKeyDefinition { KeyId = "left", PrimaryLabel = "←", ShiftLabel = "←", SymbolsLabel = "←", KeyType = VirtualKeyType.CursorLeft, WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "right", PrimaryLabel = "→", ShiftLabel = "→", SymbolsLabel = "→", KeyType = VirtualKeyType.CursorRight, WidthWeight = 1.0f },
                new VirtualKeyDefinition { KeyId = "clear", PrimaryLabel = "Limpar [LT]", ShiftLabel = "Limpar [LT]", SymbolsLabel = "Limpar [LT]", KeyType = VirtualKeyType.ClearAll, WidthWeight = 1.6f }
            };
            SetRowAndColIndices(r4, 4);
            rows.Add(r4);

            return rows;
        }

        private static void SetRowAndColIndices(List<VirtualKeyDefinition> row, int rowIndex)
        {
            for (int col = 0; col < row.Count; col++)
            {
                row[col].RowIndex = rowIndex;
                row[col].ColIndex = col;
            }
        }
    }
}
