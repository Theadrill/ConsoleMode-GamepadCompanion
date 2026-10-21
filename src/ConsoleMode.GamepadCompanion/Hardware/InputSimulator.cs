using System;
using ConsoleMode.GamepadCompanion.Core.Interfaces;
using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.Hardware
{
    /// <summary>
    /// Emite teclado/mouse via SendInput padrão do Windows (sem injeção de DLL, sem leitura de memória).
    /// Teclas são enviadas por scan code para serem reconhecidas por jogos.
    /// </summary>
    public sealed class InputSimulator : IInputSimulator
    {
        public void KeyDown(VirtualKey key) => SendKey(key, keyUp: false);

        public void KeyUp(VirtualKey key) => SendKey(key, keyUp: true);

        public void MouseMoveRelative(int dx, int dy)
        {
            if (dx == 0 && dy == 0) return;
            SendMouse(dx, dy, 0, SendInputNative.MouseEventMove);
        }

        public void MouseButtonDown(MouseButton button)
        {
            uint flag;
            switch (button)
            {
                case MouseButton.Left:
                    flag = SendInputNative.MouseEventLeftDown;
                    break;
                case MouseButton.Right:
                    flag = SendInputNative.MouseEventRightDown;
                    break;
                case MouseButton.Middle:
                    flag = SendInputNative.MouseEventMiddleDown;
                    break;
                default:
                    return;
            }
            SendMouse(0, 0, 0, flag);
        }

        public void MouseButtonUp(MouseButton button)
        {
            uint flag;
            switch (button)
            {
                case MouseButton.Left:
                    flag = SendInputNative.MouseEventLeftUp;
                    break;
                case MouseButton.Right:
                    flag = SendInputNative.MouseEventRightUp;
                    break;
                case MouseButton.Middle:
                    flag = SendInputNative.MouseEventMiddleUp;
                    break;
                default:
                    return;
            }
            SendMouse(0, 0, 0, flag);
        }

        public void MouseWheel(int delta)
        {
            if (delta == 0) return;
            SendMouse(0, 0, (uint)delta, SendInputNative.MouseEventWheel);
        }

        private static void SendKey(VirtualKey key, bool keyUp)
        {
            ushort vk = (ushort)key;
            if (vk == 0) return;

            ushort scan = SendInputNative.ScanCodeFor(vk);
            bool isExtended = SendInputNative.IsExtendedKey(vk);

            uint flags = keyUp ? SendInputNative.KeyEventKeyUp : 0u;
            if (isExtended)
            {
                flags |= SendInputNative.KeyEventExtendedKey;
            }

            // A documentação oficial da Microsoft especifica que se KEYEVENTF_SCANCODE for usado, o Windows ignora wVk!
            // Atalhos da Shell do Windows (Win+Shift+S, Win+D, Win+R, Volume, PrintScreen) exigem wVk sem KEYEVENTF_SCANCODE.
            // Já teclas normais de gameplay DirectInput funcionam perfeitamente preenchendo ambos wVk e wScan sem KEYEVENTF_SCANCODE,
            // ou usando KEYEVENTF_SCANCODE apenas quando não for tecla do Windows/Mídia.
            if (!IsWindowsShellOrMediaKey(key) && scan != 0)
            {
                // Mantém wVk preenchido e wScan preenchido sem KEYEVENTF_SCANCODE para que tanto a Shell do Windows
                // (Win+Shift+S, Alt+Tab, etc.) quanto os jogos recebam o VirtualKey e o ScanCode simultaneamente!
            }

            var input = new SendInputNative.Input { type = SendInputNative.InputKeyboard };
            input.u.ki = new SendInputNative.KeyboardInput
            {
                wVk = vk,
                wScan = scan,
                dwFlags = flags,
                time = 0,
                dwExtraInfo = IntPtr.Zero
            };
            SendInputNative.Send(input);
        }

        private static bool IsWindowsShellOrMediaKey(VirtualKey key)
        {
            switch (key)
            {
                case VirtualKey.LeftWindows:
                case VirtualKey.RightWindows:
                case VirtualKey.Apps:
                case VirtualKey.PrintScreen:
                case VirtualKey.VolumeMute:
                case VirtualKey.VolumeDown:
                case VirtualKey.VolumeUp:
                case VirtualKey.MediaNextTrack:
                case VirtualKey.MediaPrevTrack:
                case VirtualKey.MediaStop:
                case VirtualKey.MediaPlayPause:
                    return true;
                default:
                    return false;
            }
        }

        private static void SendMouse(int dx, int dy, uint mouseData, uint flags)
        {
            var input = new SendInputNative.Input { type = SendInputNative.InputMouse };
            input.u.mi = new SendInputNative.MouseInput
            {
                dx = dx,
                dy = dy,
                mouseData = mouseData,
                dwFlags = flags,
                time = 0,
                dwExtraInfo = IntPtr.Zero
            };
            SendInputNative.Send(input);
        }
    }
}
