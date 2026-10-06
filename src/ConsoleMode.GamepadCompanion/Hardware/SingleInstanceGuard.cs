using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using ConsoleMode.GamepadCompanion.UI;

namespace ConsoleMode.GamepadCompanion.Hardware
{
    /// <summary>
    /// Garante que apenas uma instância do aplicativo esteja em execução.
    /// Se uma instância já estiver aberta, solicita confirmação para encerrá-la
    /// com máxima letalidade (kill/TerminateProcess/taskkill) para destravar qualquer freeze do sistema.
    /// </summary>
    public sealed class SingleInstanceGuard : IDisposable
    {
        public const string DefaultMutexName = @"Local\ConsoleMode_GamepadCompanion_SingleInstance";

        private readonly string _mutexName;
        private readonly Func<bool> _promptCallback;
        private readonly Func<int, Process[]> _otherInstancesProvider;
        private readonly Action<Process> _killAction;

        private Mutex _mutex;
        private bool _hasHandle;

        public SingleInstanceGuard()
            : this(
                DefaultMutexName,
                ShowDefaultPrompt,
                GetRunningOtherInstances,
                KillProcessLethal)
        {
        }

        public SingleInstanceGuard(
            string mutexName,
            Func<bool> promptCallback,
            Func<int, Process[]> otherInstancesProvider,
            Action<Process> killAction)
        {
            _mutexName = mutexName ?? throw new ArgumentNullException(nameof(mutexName));
            _promptCallback = promptCallback ?? throw new ArgumentNullException(nameof(promptCallback));
            _otherInstancesProvider = otherInstancesProvider ?? throw new ArgumentNullException(nameof(otherInstancesProvider));
            _killAction = killAction ?? throw new ArgumentNullException(nameof(killAction));
        }

        private static bool ShowDefaultPrompt()
        {
            DialogResult res = MessageBox.Show(
                Strings.AlreadyRunningPrompt,
                Strings.AlreadyRunningTitle,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            return res == DialogResult.Yes;
        }

        public bool EnsureSingleInstance()
        {
            int currentPid = Process.GetCurrentProcess().Id;
            var runningOthers = _otherInstancesProvider(currentPid);

            bool createdNew;
            try
            {
                _mutex = new Mutex(true, _mutexName, out createdNew);
                _hasHandle = createdNew;
            }
            catch
            {
                _hasHandle = false;
                createdNew = false;
            }

            // Se o mutex já existia ou existem outros processos em execução
            if (!createdNew || (runningOthers != null && runningOthers.Length > 0))
            {
                bool userWantsToKill = _promptCallback();

                if (!userWantsToKill)
                {
                    // Usuário optou por NÃO finalizar a instância existente: aborta inicialização desta
                    return false;
                }

                // Usuário confirmou que travou ou quer forçar reinício: elimina instâncias existentes
                if (runningOthers != null)
                {
                    foreach (var process in runningOthers)
                    {
                        try
                        {
                            _killAction(process);
                        }
                        catch
                        {
                            // Ignora se o processo já tiver encerrado
                        }
                    }
                }

                // Libera o mutex anterior se tiver adquirido parcialmente
                if (_hasHandle && _mutex != null)
                {
                    try { _mutex.ReleaseMutex(); } catch { }
                    _mutex.Dispose();
                    _mutex = null;
                    _hasHandle = false;
                }

                // Aguarda um curto intervalo para o sistema operacional liberar recursos e recria o mutex
                Thread.Sleep(300);
                try
                {
                    _mutex = new Mutex(true, _mutexName, out _hasHandle);
                }
                catch
                {
                    _hasHandle = true;
                }
            }

            return true;
        }

        public static Process[] GetRunningOtherInstances(int currentPid)
        {
            var result = new List<Process>();
            try
            {
                string procName = Process.GetCurrentProcess().ProcessName;
                var all = Process.GetProcessesByName(procName);
                foreach (var p in all)
                {
                    if (p.Id != currentPid)
                    {
                        result.Add(p);
                    }
                }
            }
            catch
            {
                // Fallback defensivo
            }

            return result.ToArray();
        }

        public static void KillProcessLethal(Process process)
        {
            if (process == null) return;

            try
            {
                int pid = process.Id;

                // 1. Terminate padrão via .NET Process.Kill
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                }
                catch { }

                // 2. Aguarda até 800ms
                if (process.WaitForExit(800))
                {
                    return;
                }

                // 3. Chamada direta à Win32 API TerminateProcess com código de saída forçado
                IntPtr hProcess = OpenProcess(0x0001 /* PROCESS_TERMINATE */, false, (uint)pid);
                if (hProcess != IntPtr.Zero)
                {
                    try
                    {
                        TerminateProcess(hProcess, 1);
                    }
                    finally
                    {
                        CloseHandle(hProcess);
                    }
                }

                // 4. Se ainda estiver preso (ex: driver preso em kernel mode ou crash grave), comando letal via taskkill /F
                if (!process.HasExited)
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "taskkill.exe",
                        Arguments = $"/F /PID {pid}",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    using (var killer = Process.Start(psi))
                    {
                        killer?.WaitForExit(1000);
                    }
                }
            }
            catch
            {
                // Processo já terminou ou acesso negado
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        public void Dispose()
        {
            if (_hasHandle && _mutex != null)
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                catch { }
            }
            _mutex?.Dispose();
            _mutex = null;
        }
    }
}
