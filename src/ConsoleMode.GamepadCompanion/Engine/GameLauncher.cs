using System;
using System.Diagnostics;
using System.IO;
using ConsoleMode.GamepadCompanion.Core.Interfaces;
using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.Engine
{
    /// <summary>
    /// Responsável por inicializar processos de jogos/launchers e cadastrar executáveis
    /// no IWindowTracker para garantir a transição automática do perfil de controle.
    /// </summary>
    public sealed class GameLauncher
    {
        private readonly IWindowTracker _windowTracker;

        public GameLauncher(IWindowTracker windowTracker)
        {
            _windowTracker = windowTracker;
        }

        /// <summary>
        /// Inicia o jogo a partir do GameEntry fornecido e registra o executável no WindowTracker.
        /// </summary>
        /// <param name="game">Informações do jogo cadastrado.</param>
        /// <returns>Instância do Processo iniciado.</returns>
        public Process Launch(GameEntry game)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));

            string target = game.TargetPath?.Trim().Trim('"', '\'') ?? string.Empty;
            if (string.IsNullOrWhiteSpace(target))
                throw new ArgumentException("O caminho de destino (TargetPath) não pode ser vazio.", nameof(game));

            // Registra os executáveis no WindowTracker para garantir detecção automática de foco
            if (_windowTracker != null)
            {
                if (!string.IsNullOrWhiteSpace(target))
                {
                    _windowTracker.RegisterGameExecutable(target);
                }

                if (!string.IsNullOrWhiteSpace(game.MainExecutable))
                {
                    _windowTracker.RegisterGameExecutable(game.MainExecutable.Trim().Trim('"', '\''));
                }

                if (!string.IsNullOrWhiteSpace(game.LauncherName))
                {
                    _windowTracker.RegisterGameExecutable(game.LauncherName.Trim().Trim('"', '\''));
                }
            }

            string workingDir = game.WorkingDirectory?.Trim().Trim('"', '\'');
            if (string.IsNullOrWhiteSpace(workingDir))
            {
                try
                {
                    string dir = Path.GetDirectoryName(target);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        workingDir = dir;
                    }
                }
                catch
                {
                    // Se o TargetPath contiver caracteres especiais ou for relativo, ignora
                }
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = target,
                Arguments = game.Arguments ?? string.Empty,
                WorkingDirectory = workingDir ?? string.Empty,
                UseShellExecute = true
            };

            var process = Process.Start(startInfo);
            if (process != null && _windowTracker != null)
            {
                try
                {
                    _windowTracker.RegisterGameExecutable(process.ProcessName);
                }
                catch
                {
                }
            }
            return process;
        }
    }
}
