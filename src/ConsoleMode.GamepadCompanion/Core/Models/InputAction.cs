using System;
using System.Collections.Generic;
using System.Text;

namespace ConsoleMode.GamepadCompanion.Core.Models
{
    /// <summary>Tipo de ação executada por uma entrada mapeada.</summary>
    public enum InputActionType
    {
        None = 0,
        Key,
        MouseButton,
        MouseWheel,
        SpecialAction
    }

    /// <summary>
    /// Representa uma ação atômica ou componente de combo mapeada em uma entrada de controle.
    /// Suporta Teclado (VirtualKey), Botões de Mouse, Scroll e Ações Especiais do Companion.
    /// </summary>
    public sealed class InputAction : IEquatable<InputAction>
    {
        public InputActionType Type { get; set; } = InputActionType.None;
        public VirtualKey Key { get; set; } = VirtualKey.None;
        public MouseButton MouseButton { get; set; } = MouseButton.Left;
        public int WheelDelta { get; set; }
        public CompanionActionType SpecialAction { get; set; } = CompanionActionType.None;

        public static InputAction FromKey(VirtualKey key)
        {
            return new InputAction
            {
                Type = InputActionType.Key,
                Key = key
            };
        }

        public static InputAction FromMouseButton(MouseButton button)
        {
            return new InputAction
            {
                Type = InputActionType.MouseButton,
                MouseButton = button
            };
        }

        public static InputAction FromMouseWheel(int delta)
        {
            return new InputAction
            {
                Type = InputActionType.MouseWheel,
                WheelDelta = delta
            };
        }

        public static InputAction FromSpecial(CompanionActionType special)
        {
            return new InputAction
            {
                Type = InputActionType.SpecialAction,
                SpecialAction = special
            };
        }

        public string GetDisplayText()
        {
            switch (Type)
            {
                case InputActionType.Key:
                    return FormatKeyDisplay(Key);
                case InputActionType.MouseButton:
                    switch (MouseButton)
                    {
                        case MouseButton.Left: return "Mouse L";
                        case MouseButton.Right: return "Mouse R";
                        case MouseButton.Middle: return "Mouse M";
                        default: return MouseButton.ToString();
                    }
                case InputActionType.MouseWheel:
                    return WheelDelta > 0 ? "Wheel ▲" : "Wheel ▼";
                case InputActionType.SpecialAction:
                    switch (SpecialAction)
                    {
                        case CompanionActionType.CenterCursor: return "Centralizar Cursor";
                        case CompanionActionType.ToggleClipCursor: return "Travar/Destravar Cursor";
                        case CompanionActionType.AltTab: return "Alt+Tab";
                        case CompanionActionType.AltF4: return "Alt+F4";
                        case CompanionActionType.AltEnter: return "Tela Cheia";
                        case CompanionActionType.ShowDesktop: return "Desktop (Win+D)";
                        default: return SpecialAction.ToString();
                    }
                default:
                    return "Nenhum";
            }
        }

        private static string FormatKeyDisplay(VirtualKey key)
        {
            switch (key)
            {
                case VirtualKey.Space: return "Espaço";
                case VirtualKey.LeftShift: return "L-Shift";
                case VirtualKey.RightShift: return "R-Shift";
                case VirtualKey.LeftControl: return "L-Ctrl";
                case VirtualKey.RightControl: return "R-Ctrl";
                case VirtualKey.LeftAlt: return "L-Alt";
                case VirtualKey.RightAlt: return "R-Alt";
                case VirtualKey.LeftWindows: return "Win";
                case VirtualKey.RightWindows: return "Win";
                case VirtualKey.PrintScreen: return "PrintScreen";
                case VirtualKey.CapsLock: return "Caps";
                case VirtualKey.D0: return "0";
                case VirtualKey.D1: return "1";
                case VirtualKey.D2: return "2";
                case VirtualKey.D3: return "3";
                case VirtualKey.D4: return "4";
                case VirtualKey.D5: return "5";
                case VirtualKey.D6: return "6";
                case VirtualKey.D7: return "7";
                case VirtualKey.D8: return "8";
                case VirtualKey.D9: return "9";
                case VirtualKey.OemTilde: return "`";
                case VirtualKey.OemMinus: return "-";
                case VirtualKey.OemPlus: return "=";
                case VirtualKey.OemOpenBrackets: return "[";
                case VirtualKey.OemCloseBrackets: return "]";
                case VirtualKey.OemPipe: return "\\";
                case VirtualKey.OemSemicolon: return ";";
                case VirtualKey.OemQuotes: return "'";
                case VirtualKey.OemComma: return ",";
                case VirtualKey.OemPeriod: return ".";
                case VirtualKey.OemQuestion: return "/";
                default: return key.ToString();
            }
        }

        public string Serialize()
        {
            switch (Type)
            {
                case InputActionType.Key:
                    return "Key:" + Key;
                case InputActionType.MouseButton:
                    return "Mouse:" + MouseButton;
                case InputActionType.MouseWheel:
                    return "Wheel:" + WheelDelta;
                case InputActionType.SpecialAction:
                    return "Special:" + SpecialAction;
                default:
                    return string.Empty;
            }
        }

        public static InputAction Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string clean = text.Trim();

            if (clean.StartsWith("Key:", StringComparison.OrdinalIgnoreCase))
            {
                string keyPart = clean.Substring(4).Trim();
                if (Enum.TryParse(keyPart, true, out VirtualKey key))
                {
                    return FromKey(key);
                }
            }
            else if (clean.StartsWith("Mouse:", StringComparison.OrdinalIgnoreCase))
            {
                string btnPart = clean.Substring(6).Trim();
                if (Enum.TryParse(btnPart, true, out MouseButton btn))
                {
                    return FromMouseButton(btn);
                }
                if (string.Equals(btnPart, "L", StringComparison.OrdinalIgnoreCase)) return FromMouseButton(MouseButton.Left);
                if (string.Equals(btnPart, "R", StringComparison.OrdinalIgnoreCase)) return FromMouseButton(MouseButton.Right);
                if (string.Equals(btnPart, "M", StringComparison.OrdinalIgnoreCase)) return FromMouseButton(MouseButton.Middle);
            }
            else if (clean.StartsWith("Wheel:", StringComparison.OrdinalIgnoreCase))
            {
                string deltaPart = clean.Substring(6).Trim();
                if (int.TryParse(deltaPart, out int delta))
                {
                    return FromMouseWheel(delta);
                }
            }
            else if (clean.StartsWith("Special:", StringComparison.OrdinalIgnoreCase))
            {
                string specialPart = clean.Substring(8).Trim();
                if (Enum.TryParse(specialPart, true, out CompanionActionType special))
                {
                    return FromSpecial(special);
                }
            }
            else
            {
                // Fallback de compatibilidade: tenta parsear direto como VirtualKey, MouseButton ou Special
                if (Enum.TryParse(clean, true, out VirtualKey vk))
                {
                    return FromKey(vk);
                }
                if (Enum.TryParse(clean, true, out MouseButton mb))
                {
                    return FromMouseButton(mb);
                }
                if (Enum.TryParse(clean, true, out CompanionActionType sp))
                {
                    return FromSpecial(sp);
                }
            }

            return null;
        }

        public static string SerializeCombo(IEnumerable<InputAction> actions)
        {
            if (actions == null) return string.Empty;
            var sb = new StringBuilder();
            foreach (var a in actions)
            {
                if (a == null || a.Type == InputActionType.None) continue;
                string s = a.Serialize();
                if (string.IsNullOrEmpty(s)) continue;

                if (sb.Length > 0) sb.Append("+");
                sb.Append(s);
            }
            return sb.ToString();
        }

        public static List<InputAction> DeserializeCombo(string text)
        {
            var list = new List<InputAction>();
            if (string.IsNullOrWhiteSpace(text)) return list;

            string[] parts = text.Split(new[] { '+', ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                var action = Parse(part);
                if (action != null)
                {
                    list.Add(action);
                }
            }
            return list;
        }

        public static string FormatComboDisplay(IEnumerable<InputAction> actions)
        {
            if (actions == null) return "Nenhum";
            var sb = new StringBuilder();
            foreach (var a in actions)
            {
                if (a == null || a.Type == InputActionType.None) continue;
                if (sb.Length > 0) sb.Append(" + ");
                sb.Append(a.GetDisplayText());
            }
            return sb.Length > 0 ? sb.ToString() : "Nenhum";
        }

        public InputAction Clone()
        {
            return new InputAction
            {
                Type = this.Type,
                Key = this.Key,
                MouseButton = this.MouseButton,
                WheelDelta = this.WheelDelta,
                SpecialAction = this.SpecialAction
            };
        }

        public bool Equals(InputAction other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Type == other.Type &&
                   Key == other.Key &&
                   MouseButton == other.MouseButton &&
                   WheelDelta == other.WheelDelta &&
                   SpecialAction == other.SpecialAction;
        }

        public override bool Equals(object obj) => Equals(obj as InputAction);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Type;
                hash = (hash * 397) ^ (int)Key;
                hash = (hash * 397) ^ (int)MouseButton;
                hash = (hash * 397) ^ WheelDelta;
                hash = (hash * 397) ^ (int)SpecialAction;
                return hash;
            }
        }

        public override string ToString() => GetDisplayText();
    }
}
