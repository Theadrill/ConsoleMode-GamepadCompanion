using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using ConsoleMode.GamepadCompanion.Core.Interfaces;

namespace ConsoleMode.GamepadCompanion.Hardware
{
    /// <summary>
    /// Monitora a janela em primeiro plano do Windows via Win32 API.
    /// Detecta se o jogo World of Warcraft (WoW.exe ou turtle-wow.exe) está em foco.
    /// </summary>
    public sealed class WindowTracker : IWindowTracker
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private bool _isWowActive;
        private string _lastProcessName = string.Empty;

        private readonly System.Collections.Generic.HashSet<string> _customGameExecutables =
            new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public bool IsGameFocused => _isWowActive;

        public string ActiveProcessName => _lastProcessName;

        public event Action<bool> FocusChanged;

        public void RegisterGameExecutable(string executableName)
        {
            if (string.IsNullOrWhiteSpace(executableName)) return;
            string clean = executableName.Trim().Trim('"', '\'');
            string cleanName = string.Empty;
            string fileName = string.Empty;
            try
            {
                cleanName = Path.GetFileNameWithoutExtension(clean);
                fileName = Path.GetFileName(clean);
            }
            catch
            {
                int lastSlash = Math.Max(clean.LastIndexOf('\\'), clean.LastIndexOf('/'));
                fileName = lastSlash >= 0 ? clean.Substring(lastSlash + 1) : clean;
                int dot = fileName.LastIndexOf('.');
                cleanName = dot > 0 ? fileName.Substring(0, dot) : fileName;
            }

            lock (_customGameExecutables)
            {
                if (!string.IsNullOrEmpty(cleanName))
                {
                    _customGameExecutables.Add(cleanName);
                }
                if (!string.IsNullOrEmpty(fileName))
                {
                    _customGameExecutables.Add(fileName);
                }
            }
        }

        public void CheckActiveWindow()
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
            {
                UpdateFocus(false, string.Empty);
                return;
            }

            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0)
            {
                UpdateFocus(false, string.Empty);
                return;
            }

            string procName = string.Empty;
            try
            {
                using (var proc = Process.GetProcessById((int)pid))
                {
                    procName = proc.ProcessName;
                }
            }
            catch
            {
                // Processo pode ter finalizado ou acesso restrito
            }

            bool wowFocused = string.Equals(procName, "turtle-wow", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(procName, "WoW", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(procName, "VanillaFixes", StringComparison.OrdinalIgnoreCase);

            if (!wowFocused && !string.IsNullOrEmpty(procName))
            {
                lock (_customGameExecutables)
                {
                    wowFocused = _customGameExecutables.Contains(procName) ||
                                 _customGameExecutables.Contains(procName + ".exe");
                }
            }

            UpdateFocus(wowFocused, procName);
        }

        private void UpdateFocus(bool focused, string procName)
        {
            _lastProcessName = procName;
            if (_isWowActive != focused)
            {
                _isWowActive = focused;
                FocusChanged?.Invoke(focused);
            }
        }
    }
}
