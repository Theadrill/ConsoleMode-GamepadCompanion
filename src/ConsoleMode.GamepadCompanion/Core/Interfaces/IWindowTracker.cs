using System;

namespace ConsoleMode.GamepadCompanion.Core.Interfaces
{
    /// <summary>Contrato para monitoramento da janela em foco no Windows.</summary>
    public interface IWindowTracker
    {
        bool IsGameFocused { get; }
        string ActiveProcessName { get; }
        IntPtr ActiveWindowHandle { get; }
        event Action<bool> FocusChanged;
        event Action<IntPtr> ActiveWindowChanged;
        void CheckActiveWindow();
        void RegisterGameExecutable(string executableName);
    }
}
