using System;

namespace ConsoleMode.GamepadCompanion.Core.Interfaces
{
    /// <summary>Contrato para monitoramento da janela em foco no Windows.</summary>
    public interface IWindowTracker
    {
        bool IsGameFocused { get; }
        string ActiveProcessName { get; }
        event Action<bool> FocusChanged;
        void CheckActiveWindow();
        void RegisterGameExecutable(string executableName);
    }
}
