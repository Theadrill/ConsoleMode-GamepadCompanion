using System;
using System.Collections.Generic;
using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.Core.Interfaces
{
    /// <summary>Contrato para monitoramento da janela e jogo em foco no Windows.</summary>
    public interface IWindowTracker
    {
        bool IsGameFocused { get; }
        bool IsCompanionFocused { get; }
        string ActiveProcessName { get; }
        IntPtr ActiveWindowHandle { get; }
        GameEntry ActiveGame { get; }

        event Action<bool> FocusChanged;
        event Action<IntPtr> ActiveWindowChanged;
        event Action<GameEntry> ActiveGameChanged;

        void CheckActiveWindow();
        void RegisterGameExecutable(string executableName);
        void RegisterGame(GameEntry game);
        void SetGames(IEnumerable<GameEntry> games);
    }
}
