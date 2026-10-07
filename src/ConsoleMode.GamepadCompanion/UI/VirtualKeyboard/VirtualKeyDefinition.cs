using System.Drawing;

namespace ConsoleMode.GamepadCompanion.UI.VirtualKeyboard
{
    /// <summary>
    /// Definição de tecla do Teclado Virtual (rótulos primário, shift, símbolos e posição).
    /// </summary>
    public sealed class VirtualKeyDefinition
    {
        public string KeyId { get; set; }
        public string PrimaryLabel { get; set; }
        public string ShiftLabel { get; set; }
        public string SymbolsLabel { get; set; }
        public VirtualKeyType KeyType { get; set; } = VirtualKeyType.Character;
        public float WidthWeight { get; set; } = 1.0f;
        public int RowIndex { get; set; }
        public int ColIndex { get; set; }
        public Rectangle Bounds { get; set; }

        public string GetDisplayLabel(bool isCaps, bool isSymbols)
        {
            if (isSymbols && !string.IsNullOrEmpty(SymbolsLabel))
                return SymbolsLabel;

            if (isCaps && !string.IsNullOrEmpty(ShiftLabel))
                return ShiftLabel;

            return PrimaryLabel;
        }

        public string GetInsertText(bool isCaps, bool isSymbols)
        {
            switch (KeyType)
            {
                case VirtualKeyType.Character:
                    return GetDisplayLabel(isCaps, isSymbols);
                case VirtualKeyType.Space:
                    return " ";
                case VirtualKeyType.Tab:
                    return "    ";
                default:
                    return string.Empty;
            }
        }
    }
}
