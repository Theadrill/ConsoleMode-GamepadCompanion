using System;
using ConsoleMode.GamepadCompanion.Core.Interfaces;
using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.Profiles
{
    /// <summary>
    /// Perfil Desktop: Mapeamento preparado para uso no Windows.
    /// Nesta fase inicial, permanece silencioso (sem envio acidental de comandos).
    /// </summary>
    public sealed class DesktopProfile : IProfile
    {
        public string Name => "Desktop";

        public void Update(GamepadState state, float deltaSeconds)
        {
            // Silencioso por padrão enquanto a tela de mapeamento customizado de desktop é preparada
        }

        public void Reset()
        {
        }
    }
}
