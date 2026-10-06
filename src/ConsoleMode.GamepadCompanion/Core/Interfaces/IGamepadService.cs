using System;
using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.Core.Interfaces
{
    /// <summary>Contrato para leitura contínua de um controle (slot 0-3).</summary>
    public interface IGamepadService : IDisposable
    {
        /// <summary>Slot ativo (0 a 3). Valor negativo = auto (primeiro conectado).</summary>
        int SelectedSlot { get; set; }

        /// <summary>Slot efetivamente em uso, ou -1 se nenhum conectado.</summary>
        int ActiveSlot { get; }

        /// <summary>Último estado lido do controle ativo.</summary>
        GamepadState CurrentState { get; }

        /// <summary>Disparado a cada leitura nova (thread de polling).</summary>
        event Action<GamepadState> StateUpdated;

        /// <summary>Disparado quando o slot ativo conecta/desconecta.</summary>
        event Action<int, bool> ConnectionChanged;

        /// <summary>Lista os slots atualmente conectados.</summary>
        bool IsSlotConnected(int slot);

        void Start();
        void Stop();
    }
}
