using System;

namespace ConsoleMode.GamepadCompanion.Core.Models
{
    /// <summary>
    /// Representa um jogo ou aplicativo cadastrado na biblioteca do Companion.
    /// Suporta launchers/scripts intermediários e mapeia o executável principal para detecção de perfil.
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
                CoverImagePath = this.CoverImagePath
            };
        }
    }
}
