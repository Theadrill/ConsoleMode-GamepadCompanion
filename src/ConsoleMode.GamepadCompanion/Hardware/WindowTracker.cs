using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using ConsoleMode.GamepadCompanion.Core.Interfaces;
using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.Hardware
{
    /// <summary>
    /// Monitora a janela em primeiro plano do Windows via Win32 API.
    /// Identifica qual jogo cadastrado (GameEntry) está ativo em foco no momento.
    /// </summary>
    public sealed class WindowTracker : IWindowTracker
    {
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, System.Text.StringBuilder lpExeName, ref uint lpdwSize);

        private bool _isGameActive;
        private string _lastProcessName = string.Empty;
        private IntPtr _activeHwnd = IntPtr.Zero;
        private GameEntry _activeGame;

        private readonly object _lock = new object();
        private readonly List<GameEntry> _registeredGames = new List<GameEntry>();
        private readonly Dictionary<string, GameEntry> _exeToGameMap =
            new Dictionary<string, GameEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, GameEntry> _dirToGameMap =
            new Dictionary<string, GameEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _customGameExecutables =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public bool IsGameFocused => _isGameActive;

        public string ActiveProcessName => _lastProcessName;

        public IntPtr ActiveWindowHandle => _activeHwnd;

        public GameEntry ActiveGame => _activeGame;

        public event Action<bool> FocusChanged;

        public event Action<IntPtr> ActiveWindowChanged;

        public event Action<GameEntry> ActiveGameChanged;

        public void SetGames(IEnumerable<GameEntry> games)
        {
            if (games == null) return;
            lock (_lock)
            {
                _registeredGames.Clear();
                _exeToGameMap.Clear();
                _dirToGameMap.Clear();
                foreach (var g in games)
                {
                    if (g != null)
                    {
                        RegisterGameInternal(g);
                    }
                }
            }
        }

        public void RegisterGame(GameEntry game)
        {
            if (game == null) return;
            lock (_lock)
            {
                RegisterGameInternal(game);
            }
        }

        private void RegisterGameInternal(GameEntry game)
        {
            int existingIdx = _registeredGames.FindIndex(x => string.Equals(x.Id, game.Id, StringComparison.OrdinalIgnoreCase));
            if (existingIdx >= 0)
            {
                _registeredGames[existingIdx] = game;
            }
            else
            {
                _registeredGames.Add(game);
            }

            MapExecutable(game.MainExecutable, game);
            MapExecutable(game.TargetPath, game);
            MapExecutable(game.LauncherName, game);
            MapDirectory(game.WorkingDirectory, game);
            if (!string.IsNullOrWhiteSpace(game.TargetPath))
            {
                try
                {
                    string targetDir = Path.GetDirectoryName(game.TargetPath.Trim().Trim('"', '\''));
                    MapDirectory(targetDir, game);
                }
                catch { }
            }
        }

        private void MapDirectory(string rawDir, GameEntry game)
        {
            if (string.IsNullOrWhiteSpace(rawDir) || game == null) return;
            string clean = rawDir.Trim().Trim('"', '\'').TrimEnd('\\', '/');
            if (!string.IsNullOrEmpty(clean))
            {
                _dirToGameMap[clean] = game;
            }
        }

        private void MapExecutable(string rawPathOrName, GameEntry game)
        {
            if (string.IsNullOrWhiteSpace(rawPathOrName)) return;
            string clean = rawPathOrName.Trim().Trim('"', '\'');
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

            if (!string.IsNullOrEmpty(cleanName))
            {
                _exeToGameMap[cleanName] = game;
                _customGameExecutables.Add(cleanName);
            }
            if (!string.IsNullOrEmpty(fileName))
            {
                _exeToGameMap[fileName] = game;
                _customGameExecutables.Add(fileName);
            }
        }

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

            lock (_lock)
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
                UpdateFocus(false, string.Empty, IntPtr.Zero, null);
                return;
            }

            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0)
            {
                UpdateFocus(false, string.Empty, IntPtr.Zero, null);
                return;
            }

            string procName = string.Empty;
            string procFullPath = GetProcessFullPath(pid);
            if (!string.IsNullOrEmpty(procFullPath))
            {
                try
                {
                    procName = Path.GetFileNameWithoutExtension(procFullPath);
                }
                catch { }
            }

            if (string.IsNullOrEmpty(procName))
            {
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
            }

            GameEntry matchedGame = null;
            bool isGameFocused = false;

            lock (_lock)
            {
                // 1. Prioridade Máxima: Match pelo diretório onde o executável em foco está rodando!
                if (!string.IsNullOrEmpty(procFullPath))
                {
                    try
                    {
                        string procDir = Path.GetDirectoryName(procFullPath)?.TrimEnd('\\', '/');
                        if (!string.IsNullOrEmpty(procDir))
                        {
                            if (_dirToGameMap.TryGetValue(procDir, out var dirGame))
                            {
                                matchedGame = dirGame;
                                isGameFocused = true;
                            }
                            else
                            {
                                foreach (var kvp in _dirToGameMap)
                                {
                                    if (procDir.StartsWith(kvp.Key + "\\", StringComparison.OrdinalIgnoreCase))
                                    {
                                        matchedGame = kvp.Value;
                                        isGameFocused = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }

                // 2. Match pelo nome do executável
                if (!isGameFocused && !string.IsNullOrEmpty(procName))
                {
                    if (_exeToGameMap.TryGetValue(procName, out var game) ||
                        _exeToGameMap.TryGetValue(procName + ".exe", out game))
                    {
                        matchedGame = game;
                        isGameFocused = true;
                    }
                    else if (_customGameExecutables.Contains(procName) ||
                             _customGameExecutables.Contains(procName + ".exe"))
                    {
                        isGameFocused = true;
                    }
                }

                // 3. Fallback para clientes WoW Classic / VanillaFixes (ex: VanillaFixes.exe que spawna WoW.exe na mesma pasta)
                if (!isGameFocused && !string.IsNullOrEmpty(procName))
                {
                    bool isDefaultWow = string.Equals(procName, "turtle-wow", StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(procName, "WoW", StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(procName, "VanillaFixes", StringComparison.OrdinalIgnoreCase);

                    if (isDefaultWow)
                    {
                        isGameFocused = true;
                        matchedGame = _registeredGames.Find(g => string.Equals(g.Id, "turtle-wow", StringComparison.OrdinalIgnoreCase))
                                      ?? _registeredGames.Find(g => g.MainExecutable != null &&
                                          (g.MainExecutable.IndexOf("wow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                           g.MainExecutable.IndexOf("vanilla", StringComparison.OrdinalIgnoreCase) >= 0));
                    }
                }
            }

            UpdateFocus(isGameFocused, procName, hwnd, matchedGame);
        }

        private static string GetProcessFullPath(uint pid)
        {
            IntPtr hProc = IntPtr.Zero;
            try
            {
                hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
                if (hProc == IntPtr.Zero) return string.Empty;

                var sb = new System.Text.StringBuilder(1024);
                uint size = (uint)sb.Capacity;
                if (QueryFullProcessImageName(hProc, 0, sb, ref size))
                {
                    return sb.ToString();
                }
            }
            catch
            {
            }
            finally
            {
                if (hProc != IntPtr.Zero)
                {
                    CloseHandle(hProc);
                }
            }
            return string.Empty;
        }

        private void UpdateFocus(bool focused, string procName, IntPtr hwnd, GameEntry matchedGame)
        {
            _lastProcessName = procName;
            bool focusStateChanged = _isGameActive != focused;
            IntPtr targetHwnd = focused ? hwnd : IntPtr.Zero;
            bool hwndChanged = _activeHwnd != targetHwnd;
            bool gameChanged = !ReferenceEquals(_activeGame, matchedGame) || (_activeGame?.Id != matchedGame?.Id);

            _isGameActive = focused;
            _activeHwnd = targetHwnd;
            _activeGame = matchedGame;

            if (focusStateChanged)
            {
                FocusChanged?.Invoke(focused);
            }

            if (hwndChanged)
            {
                ActiveWindowChanged?.Invoke(_activeHwnd);
            }

            if (gameChanged)
            {
                ActiveGameChanged?.Invoke(_activeGame);
            }
        }
    }
}
