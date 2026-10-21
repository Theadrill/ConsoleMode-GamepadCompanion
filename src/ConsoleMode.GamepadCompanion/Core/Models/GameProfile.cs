using System.Collections.Generic;

namespace ConsoleMode.GamepadCompanion.Core.Models
{
    /// <summary>
    /// Representa o perfil completo de controles associado a um jogo.
    /// Contém preferências de hardware (Deadzone e Limiar de Gatilhos) e páginas de mapeamento.
    /// </summary>
    public sealed class GameProfile
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>Se true, utiliza a deadzone global definida no aplicativo (AppSettings); se false, utiliza CustomDeadzonePercent.</summary>
        public bool UseGlobalDeadzone { get; set; } = true;

        /// <summary>Deadzone customizada dos analógicos deste jogo em % do curso (5-50).</summary>
        public int CustomDeadzonePercent { get; set; } = 24;

        /// <summary>Se true, utiliza o limiar de gatilhos global definido no aplicativo; se false, utiliza CustomTriggerThresholdPercent.</summary>
        public bool UseGlobalTriggerThreshold { get; set; } = true;

        /// <summary>Limiar customizado dos gatilhos LT/RT deste jogo em % do curso (5-90).</summary>
        public int CustomTriggerThresholdPercent { get; set; } = 20;

        /// <summary>Páginas/camadas de mapeamento do perfil.</summary>
        public List<ProfilePage> Pages { get; set; } = new List<ProfilePage>();

        /// <summary>Retorna a página ativa principal (Página 1).</summary>
        public ProfilePage ActivePage
        {
            get
            {
                if (Pages == null) Pages = new List<ProfilePage>();
                if (Pages.Count == 0)
                {
                    Pages.Add(new ProfilePage { PageIndex = 0, Name = "Página 1" });
                }
                return Pages[0];
            }
        }

        public GameProfile Clone()
        {
            var clone = new GameProfile
            {
                Name = this.Name,
                UseGlobalDeadzone = this.UseGlobalDeadzone,
                CustomDeadzonePercent = this.CustomDeadzonePercent,
                UseGlobalTriggerThreshold = this.UseGlobalTriggerThreshold,
                CustomTriggerThresholdPercent = this.CustomTriggerThresholdPercent,
                Pages = new List<ProfilePage>()
            };

            if (this.Pages != null)
            {
                foreach (var page in this.Pages)
                {
                    if (page != null) clone.Pages.Add(page.Clone());
                }
            }

            return clone;
        }
    }
}
