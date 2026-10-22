using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.Hardware
{
    /// <summary>
    /// Gerencia a obtenção e execução da instalação silenciosa do driver de kernel ViGEmBus.
    /// </summary>
    public static class ViGEmBusInstaller
    {
        public const string DefaultDownloadUrl = "https://github.com/nefarius/ViGEmBus/releases/download/v1.22.0/ViGEmBus_1.22.0_x64_x86_arm64.exe";
        private const string InstallerFilename = "ViGEmBus_Setup.exe";

        /// <summary>
        /// Localiza o executável do instalador localmente ou realiza download se necessário,
        /// e executa a instalação silenciosa em segundo plano solicitando elevação (UAC).
        /// </summary>
        public static async Task<InstallResult> InstallAsync(IProgress<string> progress = null)
        {
            try
            {
                string installerPath = ResolveInstallerPath();

                if (string.IsNullOrEmpty(installerPath) || !File.Exists(installerPath))
                {
                    progress?.Report("Baixando driver ViGEmBus da release oficial...");
                    installerPath = await DownloadInstallerAsync(progress).ConfigureAwait(false);
                }

                if (string.IsNullOrEmpty(installerPath) || !File.Exists(installerPath))
                {
                    return new InstallResult(false, "Não foi possível localizar ou baixar o instalador do ViGEmBus.");
                }

                progress?.Report("Aguardando confirmação de permissão de Administrador (UAC)...");

                var psi = new ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = "/quiet /norestart",
                    Verb = "runas",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process proc;
                try
                {
                    proc = Process.Start(psi);
                }
                catch (Win32Exception wEx) when (wEx.NativeErrorCode == 1223) // ERROR_CANCELLED (UAC recusado)
                {
                    return new InstallResult(false, "A instalação foi cancelada (permissão de Administrador recusada no UAC).");
                }

                if (proc == null)
                {
                    return new InstallResult(false, "Falha ao iniciar o processo do instalador.");
                }

                progress?.Report("Instalando o driver no Windows em segundo plano...");
                await Task.Run(() => proc.WaitForExit()).ConfigureAwait(false);

                // Aguarda 1 segundo para o Windows registrar o driver PnP e serviço
                await Task.Delay(1000).ConfigureAwait(false);

                progress?.Report("Verificando ativação do driver ViGEmBus...");
                ViGEmBusStatus status = ViGEmBusService.Instance.CheckStatus();

                if (status == ViGEmBusStatus.Active)
                {
                    return new InstallResult(true, "Driver ViGEmBus instalado e ativado com sucesso!");
                }

                return new InstallResult(false, $"O instalador foi concluído, mas o status do driver reportou: {status}.");
            }
            catch (Exception ex)
            {
                return new InstallResult(false, $"Erro inesperado durante a instalação: {ex.Message}");
            }
        }

        public static string ResolveInstallerPath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // 1. Subpasta redist/
            string p1 = Path.Combine(baseDir, "redist", InstallerFilename);
            if (File.Exists(p1)) return p1;

            // 2. Diretório raiz da aplicação
            string p2 = Path.Combine(baseDir, InstallerFilename);
            if (File.Exists(p2)) return p2;

            // 3. Pasta temporária previamente baixada
            string p3 = Path.Combine(Path.GetTempPath(), InstallerFilename);
            if (File.Exists(p3)) return p3;

            return null;
        }

        private static async Task<string> DownloadInstallerAsync(IProgress<string> progress)
        {
            string targetPath = Path.Combine(Path.GetTempPath(), InstallerFilename);

            using (var wc = new WebClient())
            {
                // Configura TLS 1.2+ para GitHub
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                await wc.DownloadFileTaskAsync(new Uri(DefaultDownloadUrl), targetPath).ConfigureAwait(false);
            }

            return targetPath;
        }
    }

    public sealed class InstallResult
    {
        public bool Success { get; }
        public string Message { get; }

        public InstallResult(bool success, string message)
        {
            Success = success;
            Message = message ?? string.Empty;
        }
    }
}
