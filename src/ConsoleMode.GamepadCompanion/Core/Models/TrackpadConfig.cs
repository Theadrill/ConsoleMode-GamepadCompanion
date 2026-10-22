using System.Collections.Generic;

namespace ConsoleMode.GamepadCompanion.Core.Models
{
    /// <summary>
    /// Configuração de modo de operação, sensibilidade, inércia, hápticos e mapeamentos
    /// para um Trackpad do Steam Deck (Esquerdo ou Direito).
    /// Suporta Dupla Ação: disparo independente ao tocar levemente (Touch) e/ou ao afundar o clique mecânico (Press).
    /// </summary>
    public sealed class TrackpadConfig
    {
        /// <summary>Modo funcional ativo do trackpad.</summary>
        public TrackpadMode Mode { get; set; } = TrackpadMode.Disabled;

        /// <summary>Sensibilidade do cursor/scroll (1 a 100, padrão: 50).</summary>
        public int Sensitivity { get; set; } = 50;

        /// <summary>Atrito/Inércia da simulação de trackball (1 a 100, padrão: 50). Valores maiores param o giro mais rápido.</summary>
        public int Friction { get; set; } = 50;

        /// <summary>Intensidade do feedback háptico (Off, Low, Medium, High. Padrão: Medium).</summary>
        public TrackpadHapticIntensity HapticIntensity { get; set; } = TrackpadHapticIntensity.Medium;

        /// <summary>
        /// Se true, ativa rolagem natural estilo touch (deslizar p/ cima desce a página). Padrão de fábrica: true.
        /// </summary>
        public bool InvertScroll { get; set; } = true;

        /// <summary>Zona morta central percentual para descanso do polegar no modo DPad (padrão: 15%).</summary>
        public int DeadzonePercent { get; set; } = 15;

        /// <summary>
        /// Limiar de ativação de clique por pressão analógica (0 a 32767).
        /// Se 0, desativa o gatilho por pressão e utiliza exclusivamente o microswitch mecânico físico.
        /// Padrão: 11000 (~pressão suave a média com o polegar, sem exigir esmagamento mecânico).
        /// </summary>
        public int ClickPressureThreshold { get; set; } = 11000;

        /// <summary>Ações disparadas ao tocar a superfície capacitiva (Touch).</summary>
        public List<InputAction> TouchActions { get; set; } = new List<InputAction>();

        /// <summary>Ações disparadas ao afundar o clique físico mecânico (Press).</summary>
        public List<InputAction> PressActions { get; set; } = new List<InputAction>();

        /// <summary>Comando(s) disparado(s) para o quadrante Cima quando em modo DPad.</summary>
        public List<InputAction> Up { get; set; } = new List<InputAction>();

        /// <summary>Comando(s) disparado(s) para o quadrante Baixo quando em modo DPad.</summary>
        public List<InputAction> Down { get; set; } = new List<InputAction>();

        /// <summary>Comando(s) disparado(s) para o quadrante Esquerda quando em modo DPad.</summary>
        public List<InputAction> Left { get; set; } = new List<InputAction>();

        /// <summary>Comando(s) disparado(s) para o quadrante Direita quando em modo DPad.</summary>
        public List<InputAction> Right { get; set; } = new List<InputAction>();

        public TrackpadConfig Clone()
        {
            var clone = new TrackpadConfig
            {
                Mode = this.Mode,
                Sensitivity = this.Sensitivity,
                Friction = this.Friction,
                HapticIntensity = this.HapticIntensity,
                InvertScroll = this.InvertScroll,
                DeadzonePercent = this.DeadzonePercent,
                ClickPressureThreshold = this.ClickPressureThreshold,
                TouchActions = CloneList(this.TouchActions),
                PressActions = CloneList(this.PressActions),
                Up = CloneList(this.Up),
                Down = CloneList(this.Down),
                Left = CloneList(this.Left),
                Right = CloneList(this.Right)
            };
            return clone;
        }

        private static List<InputAction> CloneList(List<InputAction> list)
        {
            var res = new List<InputAction>();
            if (list == null) return res;
            foreach (var a in list)
            {
                if (a != null) res.Add(a.Clone());
            }
            return res;
        }
    }
}
