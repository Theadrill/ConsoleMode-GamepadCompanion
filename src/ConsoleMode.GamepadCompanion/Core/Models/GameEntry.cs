using System;

namespace ConsoleMode.GamepadCompanion.Core.Models
{
    /// <summary>
    /// Representa um jogo ou aplicativo cadastrado na biblioteca do Companion.
    /// Suporta launchers/scripts intermediários, mapeia o executável principal para detecção de perfil,
    /// toggle de input do Companion e perfil de controles dedicado.
    /// </summary>
    public sealed class GameEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public string LauncherName { get; set; } = string.Empty;
        public string MainExecutable { get; set; } = string.Empty;
        public string TargetPath { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string CoverImagePath { get; set; } = string.Empty;
        public string SteamGridDbSearchTerm { get; set; } = string.Empty;

        /// <summary>
        /// Se true, o Companion intercepta o controle e emula teclado/mouse.
        /// Se false, o Companion fica silencioso neste jogo (para jogos com suporte nativo a gamepad).
        /// </summary>
        public bool EnableCompanionInput { get; set; } = true;

        /// <summary>Perfil de mapeamento de controles específico deste jogo.</summary>
        public GameProfile Profile { get; set; }

        public GameEntry Clone()
        {
            return new GameEntry
            {
                Id = this.Id,
                Name = this.Name,
                LauncherName = this.LauncherName,
                MainExecutable = this.MainExecutable,
                TargetPath = this.TargetPath,
                WorkingDirectory = this.WorkingDirectory,
                Arguments = this.Arguments,
                CoverImagePath = this.CoverImagePath,
                SteamGridDbSearchTerm = this.SteamGridDbSearchTerm,
                EnableCompanionInput = this.EnableCompanionInput,
                Profile = this.Profile?.Clone()
            };
        }
    }
}
