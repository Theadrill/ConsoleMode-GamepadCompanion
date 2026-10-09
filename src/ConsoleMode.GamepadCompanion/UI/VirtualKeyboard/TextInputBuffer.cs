using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ConsoleMode.GamepadCompanion.UI.VirtualKeyboard
{
    /// <summary>
    /// Buffer de edição de texto com suporte a cursor, inserção, deleção e clipboard do Windows.
    /// </summary>
    public sealed class TextInputBuffer
    {
        private string _text = string.Empty;
        private int _cursorPosition;

        public event Action TextChanged;
        public event Action CursorChanged;

        public TextInputBuffer(string initialText = "")
        {
            SetText(initialText);
        }

        public string Text => _text;

        public int Length => _text.Length;

        public int CursorPosition
        {
            get => _cursorPosition;
            set
            {
                int clamped = Math.Max(0, Math.Min(_text.Length, value));
                if (_cursorPosition != clamped)
                {
                    _cursorPosition = clamped;
                    CursorChanged?.Invoke();
                }
            }
        }

        public void SetText(string text)
        {
            _text = text ?? string.Empty;
            _cursorPosition = _text.Length;
            TextChanged?.Invoke();
            CursorChanged?.Invoke();
        }

        public void Insert(string input)
        {
            if (string.IsNullOrEmpty(input)) return;

            _text = _text.Insert(_cursorPosition, input);
            _cursorPosition += input.Length;
            TextChanged?.Invoke();
            CursorChanged?.Invoke();
        }

        public void Insert(char c)
        {
            Insert(c.ToString());
        }

        public void Backspace()
        {
            if (_cursorPosition > 0 && _text.Length > 0)
            {
                _text = _text.Remove(_cursorPosition - 1, 1);
                _cursorPosition--;
                TextChanged?.Invoke();
                CursorChanged?.Invoke();
            }
        }

        public void Delete()
        {
            if (_cursorPosition < _text.Length)
            {
                _text = _text.Remove(_cursorPosition, 1);
                TextChanged?.Invoke();
                CursorChanged?.Invoke();
            }
        }

        public void Clear()
        {
            if (_text.Length > 0)
            {
                _text = string.Empty;
                _cursorPosition = 0;
                TextChanged?.Invoke();
                CursorChanged?.Invoke();
            }
        }

        public void MoveCursorLeft()
        {
            if (_cursorPosition > 0)
            {
                _cursorPosition--;
                CursorChanged?.Invoke();
            }
        }

        public void MoveCursorRight()
        {
            if (_cursorPosition < _text.Length)
            {
                _cursorPosition++;
                CursorChanged?.Invoke();
            }
        }

        public void MoveCursorHome()
        {
            CursorPosition = 0;
        }

        public void MoveCursorEnd()
        {
            CursorPosition = _text.Length;
        }

        public bool CopyToClipboard()
        {
            if (string.IsNullOrEmpty(_text)) return false;

            try
            {
                Clipboard.SetText(_text);
                return true;
            }
            catch (ExternalException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        public bool PasteFromClipboard()
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    string clip = Clipboard.GetText();
                    if (!string.IsNullOrEmpty(clip))
                    {
                        Insert(clip);
                        return true;
                    }
                }
            }
            catch
            {
                // Fallback gracioso caso clipboard esteja bloqueado por outro processo
            }
            return false;
        }
    }
}
