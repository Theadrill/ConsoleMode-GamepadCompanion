using System.Collections.Generic;

namespace ConsoleMode.GamepadCompanion.Core.Models
{
    /// <summary>
    /// Configuração de mapeamento e modo de operação para uma alavanca analógica (Stick).
    /// </summary>
    public sealed class StickConfig
    {
        public StickMode Mode { get; set; } = StickMode.DirectionalKeys;

        /// <summary>Comando(s) disparado(s) para cima quando em modo DirectionalKeys.</summary>
        public List<InputAction> Up { get; set; } = new List<InputAction>();

        /// <summary>Comando(s) disparado(s) para baixo quando em modo DirectionalKeys.</summary>
        public List<InputAction> Down { get; set; } = new List<InputAction>();

        /// <summary>Comando(s) disparado(s) para a esquerda quando em modo DirectionalKeys.</summary>
        public List<InputAction> Left { get; set; } = new List<InputAction>();

        /// <summary>Comando(s) disparado(s) para a direita quando em modo DirectionalKeys.</summary>
        public List<InputAction> Right { get; set; } = new List<InputAction>();

        /// <summary>
        /// Comandos adicionais disparados automaticamente enquanto a alavanca estiver ativa fora da deadzone
        /// (ex: F9 no analógico esquerdo para Smart Mouse Look, ou Clique Direito no analógico direito para câmera MMO).
        /// </summary>
        public List<InputAction> AdditionalActions { get; set; } = new List<InputAction>();

        /// <summary>Comandos disparados ao pressionar o clique da alavanca (L3 ou R3).</summary>
        public List<InputAction> ClickActions { get; set; } = new List<InputAction>();

        public StickConfig Clone()
        {
            var clone = new StickConfig
            {
                Mode = this.Mode,
                Up = CloneList(this.Up),
                Down = CloneList(this.Down),
                Left = CloneList(this.Left),
                Right = CloneList(this.Right),
                AdditionalActions = CloneList(this.AdditionalActions),
                ClickActions = CloneList(this.ClickActions)
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
