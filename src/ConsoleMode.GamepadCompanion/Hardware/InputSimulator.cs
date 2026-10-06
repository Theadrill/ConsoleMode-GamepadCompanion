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
            SendMouse(dx, dy, SendInputNative.MouseEventMove);
        }

        public void MouseButtonDown(MouseButton button)
        {
            SendMouse(0, 0, button == MouseButton.Left
                ? SendInputNative.MouseEventLeftDown
                : SendInputNative.MouseEventRightDown);
        }

        public void MouseButtonUp(MouseButton button)
        {
            SendMouse(0, 0, button == MouseButton.Left
                ? SendInputNative.MouseEventLeftUp
                : SendInputNative.MouseEventRightUp);
        }

        private static void SendKey(VirtualKey key, bool keyUp)
        {
            ushort vk = (ushort)key;
            uint flags = SendInputNative.KeyEventScanCode | (keyUp ? SendInputNative.KeyEventKeyUp : 0u);

            var input = new SendInputNative.Input { type = SendInputNative.InputKeyboard };
            input.u.ki = new SendInputNative.KeyboardInput
            {
                wVk = 0,
                wScan = SendInputNative.ScanCodeFor(vk),
                dwFlags = flags,
                time = 0,
                dwExtraInfo = IntPtr.Zero
            };
            SendInputNative.Send(input);
        }

        private static void SendMouse(int dx, int dy, uint flags)
        {
            var input = new SendInputNative.Input { type = SendInputNative.InputMouse };
            input.u.mi = new SendInputNative.MouseInput
            {
                dx = dx,
                dy = dy,
                mouseData = 0,
                dwFlags = flags,
                time = 0,
                dwExtraInfo = IntPtr.Zero
            };
            SendInputNative.Send(input);
        }
    }
}
