using System;
using System.Drawing;

namespace ConsoleMode.GamepadCompanion.UI.Navigation
{
    /// <summary>Contrato para elementos navegáveis via gamepad na interface.</summary>
    public interface INavigableControl
    {
        /// <summary>Identificador legível do controle.</summary>
        string Id { get; }

        /// <summary>Área ocupada pelo controle dentro de seu painel pai.</summary>
        Rectangle Bounds { get; }

        /// <summary>Indica se o elemento está com o foco de navegação.</summary>
        bool IsFocused { get; set; }

        /// <summary>Indica se o elemento está em modo de edição (com overlay escurecido).</summary>
        bool IsEditing { get; }

        /// <summary>Notifica quando o estado de foco ou edição mudar.</summary>
        event Action StateChanged;

        /// <summary>Chamado quando o foco entra no elemento.</summary>
        void OnFocusGained();

        /// <summary>Chamado quando o foco sai do elemento.</summary>
        void OnFocusLost();

        /// <summary>Chamado quando o botão A é pressionado.</summary>
        bool OnButtonAPressed();

        /// <summary>Chamado quando o botão B é pressionado.</summary>
        bool OnButtonBPressed();

        /// <summary>Chamado quando o direcional D-Pad é acionado.</summary>
        bool OnDirection(NavigationDirection direction, bool isRepeat, int repeatCount);
    }
}
