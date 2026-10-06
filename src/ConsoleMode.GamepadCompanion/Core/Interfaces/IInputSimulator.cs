using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.Core.Interfaces
{
    /// <summary>Contrato para emitir teclado e mouse no sistema operacional.</summary>
    public interface IInputSimulator
    {
        void KeyDown(VirtualKey key);
        void KeyUp(VirtualKey key);
        void MouseMoveRelative(int dx, int dy);
        void MouseButtonDown(MouseButton button);
        void MouseButtonUp(MouseButton button);
    }
}
