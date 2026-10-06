using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.Core.Interfaces
{
    /// <summary>Contrato para perfis de mapeamento de controle.</summary>
    public interface IProfile
    {
        string Name { get; }
        void Update(GamepadState state, float deltaSeconds);
        void Reset();
    }
}
