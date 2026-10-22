using System;
using System.Collections.Generic;

namespace ConsoleMode.GamepadCompanion.Core.Models
{
    /// <summary>
    /// Representa uma página ou camada de mapeamento dentro de um perfil.
    /// Contém sensibilidade própria de mouse, configurações dos dois analógicos, dois trackpads e mapeamento dos botões.
    /// </summary>
    public sealed class ProfilePage
    {
        public int PageIndex { get; set; } = 0;
        public string Name { get; set; } = "Página 1";

        /// <summary>Sensibilidade do mouse específica desta página (1-100).</summary>
        public int MouseSensitivity { get; set; } = 50;

        /// <summary>Configuração do analógico esquerdo.</summary>
        public StickConfig LeftStick { get; set; } = new StickConfig();

        /// <summary>Configuração do analógico direito.</summary>
        public StickConfig RightStick { get; set; } = new StickConfig { Mode = StickMode.MouseMove };

        /// <summary>Configuração do Trackpad esquerdo (Steam Deck / Handheld).</summary>
        public TrackpadConfig LeftTrackpad { get; set; } = new TrackpadConfig();

        /// <summary>Configuração do Trackpad direito (Steam Deck / Handheld).</summary>
        public TrackpadConfig RightTrackpad { get; set; } = new TrackpadConfig { Mode = TrackpadMode.MouseTrackball };

        /// <summary>Mapeamento de botões físicos do controle para lista de ações.</summary>
        public Dictionary<ControllerButton, List<InputAction>> ButtonMappings { get; set; } =
            new Dictionary<ControllerButton, List<InputAction>>();

        public List<InputAction> GetActions(ControllerButton button)
        {
            if (ButtonMappings.TryGetValue(button, out var list) && list != null)
            {
                return list;
            }
            return new List<InputAction>();
        }

        public void SetActions(ControllerButton button, IEnumerable<InputAction> actions)
        {
            var list = new List<InputAction>();
            if (actions != null)
            {
                foreach (var a in actions)
                {
                    if (a != null) list.Add(a.Clone());
                }
            }
            ButtonMappings[button] = list;
        }

        public void ClearActions(ControllerButton button)
        {
            ButtonMappings[button] = new List<InputAction>();
        }

        public ProfilePage Clone()
        {
            var clone = new ProfilePage
            {
                PageIndex = this.PageIndex,
                Name = this.Name,
                MouseSensitivity = this.MouseSensitivity,
                LeftStick = this.LeftStick?.Clone() ?? new StickConfig(),
                RightStick = this.RightStick?.Clone() ?? new StickConfig { Mode = StickMode.MouseMove },
                LeftTrackpad = this.LeftTrackpad?.Clone() ?? new TrackpadConfig(),
                RightTrackpad = this.RightTrackpad?.Clone() ?? new TrackpadConfig(),
                ButtonMappings = new Dictionary<ControllerButton, List<InputAction>>()
            };

            foreach (var kvp in this.ButtonMappings)
            {
                var actionList = new List<InputAction>();
                if (kvp.Value != null)
                {
                    foreach (var a in kvp.Value)
                    {
                        if (a != null) actionList.Add(a.Clone());
                    }
                }
                clone.ButtonMappings[kvp.Key] = actionList;
            }

            return clone;
        }
    }
}
