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
        private bool _isCompanionActive;
        private readonly uint _companionPid;
        private string _lastProcessName = string.Empty;
        private IntPtr _activeHwnd = IntPtr.Zero;
        private GameEntry _activeGame;

        private GameEntry _lastLaunchedGame;
        private GameEntry _lastSelectedGame;

        private readonly object _lock = new object();
        private readonly List<GameEntry> _registeredGames = new List<GameEntry>();
        private readonly Dictionary<string, GameEntry> _exeToGameMap =
            new Dictionary<string, GameEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, GameEntry> _dirToGameMap =
            new Dictionary<string, GameEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _customGameExecutables =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public WindowTracker()
        {
            try
            {
                _companionPid = (uint)Process.GetCurrentProcess().Id;
            }
            catch
            {
                _companionPid = 0;
            }
        }

        public bool IsGameFocused => _isGameActive;

        public bool IsCompanionFocused => _isCompanionActive;

        public string ActiveProcessName => _lastProcessName;

        public IntPtr ActiveWindowHandle => _activeHwnd;

        public GameEntry ActiveGame => _activeGame;

        public GameEntry LastLaunchedGame => _lastLaunchedGame;

        public GameEntry LastSelectedGame => _lastSelectedGame;

        public event Action<bool> FocusChanged;

        public event Action<IntPtr> ActiveWindowChanged;

        public event Action<GameEntry> ActiveGameChanged;

        public void NotifyGameLaunched(GameEntry game)
        {
            if (game == null) return;
            lock (_lock)
            {
                _lastLaunchedGame = game;
                RegisterGameInternal(game);
            }
        }

        public void NotifyGameSelected(GameEntry game)
        {
            if (game == null) return;
            lock (_lock)
            {
                _lastSelectedGame = game;
                RegisterGameInternal(game);
            }
        }

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
            if (!IsSystemExecutable(game.TargetPath))
            {
                MapExecutable(game.TargetPath, game);
            }
            MapExecutable(game.LauncherName, game);

            MapDirectory(game.WorkingDirectory, game);
            if (!string.IsNullOrWhiteSpace(game.TargetPath))
            {
                try
                {
                    string targetDir = Path.GetDirectoryName(game.TargetPath.Trim().Trim('"', '\''));
                    if (!IsSystemDirectory(targetDir))
                    {
                        MapDirectory(targetDir, game);
                    }
                }
                catch { }
            }
        }

        private void MapDirectory(string rawDir, GameEntry game)
        {
            if (string.IsNullOrWhiteSpace(rawDir) || game == null) return;
            string clean = rawDir.Trim().Trim('"', '\'').TrimEnd('\\', '/');
            if (!string.IsNullOrEmpty(clean) && !IsSystemDirectory(clean))
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

            if (IsSystemExecutable(cleanName) || IsSystemExecutable(fileName))
            {
                return;
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
            RegisterGameExecutable(executableName, null);
        }

        public void RegisterGameExecutable(string executableName, GameEntry game)
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

            if (IsSystemExecutable(cleanName) || IsSystemExecutable(fileName))
            {
                return;
            }

            lock (_lock)
            {
                if (!string.IsNullOrEmpty(cleanName))
                {
                    _customGameExecutables.Add(cleanName);
                    if (game != null)
                    {
                        _exeToGameMap[cleanName] = game;
                    }
                }
                if (!string.IsNullOrEmpty(fileName))
                {
                    _customGameExecutables.Add(fileName);
                    if (game != null)
                    {
                        _exeToGameMap[fileName] = game;
                    }
                }
            }
        }

        public void CheckActiveWindow()
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
            {
                _isCompanionActive = false;
                UpdateFocus(false, string.Empty, IntPtr.Zero, null);
                return;
            }

            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0)
            {
                _isCompanionActive = false;
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

            EvaluateActiveWindow(hwnd, pid, procName, procFullPath);
        }

        internal void EvaluateActiveWindow(IntPtr hwnd, uint pid, string procName, string procFullPath)
        {
            _isCompanionActive = (_companionPid != 0 && pid == _companionPid);

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
                        if (!string.IsNullOrEmpty(procDir) && !IsSystemDirectory(procDir))
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

                // 2. Se o processo em foco for cliente de World of Warcraft (WoW.exe, VanillaFixes, etc.)
                if (!isGameFocused && !string.IsNullOrEmpty(procName) && IsDefaultWowProcess(procName))
                {
                    isGameFocused = true;

                    // 2a. Se um jogo WoW foi lançado pelo Companion, prioridade máxima para ele!
                    if (_lastLaunchedGame != null && IsWowGame(_lastLaunchedGame))
                    {
                        matchedGame = _lastLaunchedGame;
                    }
                    // 2b. Continuidade: se o jogo ativo já é um WoW cadastrado, mantenha-o!
                    else if (_activeGame != null && IsWowGame(_activeGame))
                    {
                        matchedGame = _activeGame;
                    }
                    // 2c. Se um jogo WoW foi selecionado/editado no Companion
                    else if (_lastSelectedGame != null && IsWowGame(_lastSelectedGame))
                    {
                        matchedGame = _lastSelectedGame;
                    }
                    // 2d. Busca nos jogos cadastrados pelo executável ou preset WoW
                    else
                    {
                        matchedGame = _registeredGames.Find(g =>
                            (!string.IsNullOrEmpty(g.MainExecutable) && string.Equals(Path.GetFileNameWithoutExtension(g.MainExecutable), procName, StringComparison.OrdinalIgnoreCase)) ||
                            (!string.IsNullOrEmpty(g.LauncherName) && string.Equals(Path.GetFileNameWithoutExtension(g.LauncherName), procName, StringComparison.OrdinalIgnoreCase)))
                            ?? _registeredGames.Find(IsWowGame);
                    }
                }

                // 3. Match pelo executável (jogos gerais)
                if (!isGameFocused && !string.IsNullOrEmpty(procName))
                {
                    if (_lastLaunchedGame != null && MatchesGameExecutable(_lastLaunchedGame, procName))
                    {
                        matchedGame = _lastLaunchedGame;
                        isGameFocused = true;
                    }
                    else if (_exeToGameMap.TryGetValue(procName, out var game) ||
                             _exeToGameMap.TryGetValue(procName + ".exe", out game))
                    {
                        matchedGame = game;
                        isGameFocused = true;
                    }
                    else if (_customGameExecutables.Contains(procName) ||
                             _customGameExecutables.Contains(procName + ".exe"))
                    {
                        isGameFocused = true;
                        matchedGame = _lastLaunchedGame ?? _lastSelectedGame ?? _activeGame;
                    }
                }
            }

            UpdateFocus(isGameFocused, procName, hwnd, matchedGame);
        }

        private static bool IsDefaultWowProcess(string procName)
        {
            if (string.IsNullOrEmpty(procName)) return false;
            return string.Equals(procName, "WoW", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(procName, "turtle-wow", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(procName, "VanillaFixes", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(procName, "WoWClassic", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(procName, "WoW-64", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsWowGame(GameEntry game)
        {
            if (game == null) return false;
            if (string.Equals(game.Id, "turtle-wow", StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.IsNullOrEmpty(game.MainExecutable) &&
                (game.MainExecutable.IndexOf("wow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 game.MainExecutable.IndexOf("vanilla", StringComparison.OrdinalIgnoreCase) >= 0))
                return true;
            if (!string.IsNullOrEmpty(game.TargetPath) &&
                (game.TargetPath.IndexOf("wow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 game.TargetPath.IndexOf("vanilla", StringComparison.OrdinalIgnoreCase) >= 0))
                return true;
            if (!string.IsNullOrEmpty(game.LauncherName) &&
                (game.LauncherName.IndexOf("wow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 game.LauncherName.IndexOf("vanilla", StringComparison.OrdinalIgnoreCase) >= 0))
                return true;
            if (!string.IsNullOrEmpty(game.WorkingDirectory) &&
                (game.WorkingDirectory.IndexOf("wow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 game.WorkingDirectory.IndexOf("vanilla", StringComparison.OrdinalIgnoreCase) >= 0))
                return true;
            if (!string.IsNullOrEmpty(game.Name) &&
                game.Name.IndexOf("wow", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return false;
        }

        private static bool MatchesGameExecutable(GameEntry game, string procName)
        {
            if (game == null || string.IsNullOrEmpty(procName)) return false;
            return MatchesExe(game.MainExecutable, procName) ||
                   MatchesExe(game.LauncherName, procName) ||
                   (!IsSystemExecutable(game.TargetPath) && MatchesExe(game.TargetPath, procName));
        }

        private static bool MatchesExe(string pathOrName, string procName)
        {
            if (string.IsNullOrWhiteSpace(pathOrName)) return false;
            string clean = pathOrName.Trim().Trim('"', '\'');
            string name = clean;
            try
            {
                name = Path.GetFileNameWithoutExtension(clean);
            }
            catch { }
            return string.Equals(name, procName, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(clean, procName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSystemDirectory(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir)) return false;
            string clean = dir.Trim().Trim('"', '\'').TrimEnd('\\', '/');

            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\', '/');
            string sysDir = Environment.SystemDirectory.TrimEnd('\\', '/');

            if (!string.IsNullOrEmpty(winDir) && string.Equals(clean, winDir, StringComparison.OrdinalIgnoreCase))
                return true;
            if (!string.IsNullOrEmpty(sysDir) && string.Equals(clean, sysDir, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrEmpty(winDir))
            {
                string sys32 = Path.Combine(winDir, "System32");
                string sysWow64 = Path.Combine(winDir, "SysWOW64");
                if (string.Equals(clean, sys32, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(clean, sysWow64, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsSystemExecutable(string exe)
        {
            if (string.IsNullOrWhiteSpace(exe)) return false;
            string clean = exe.Trim().Trim('"', '\'');
            string name = clean;
            try
            {
                name = Path.GetFileNameWithoutExtension(clean);
            }
            catch { }

            return string.Equals(name, "cmd", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "powershell", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "pwsh", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "wt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "explorer", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "conhost", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "rundll32", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "cscript", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "wscript", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetProcessFullPath(uint pid)
        {
            IntPtr hProc = IntPtr.Zero;
            try
            {
                hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
                if (hProc != IntPtr.Zero)
                {
                    var sb = new System.Text.StringBuilder(1024);
                    uint size = (uint)sb.Capacity;
                    if (QueryFullProcessImageName(hProc, 0, sb, ref size))
                    {
                        return sb.ToString();
                    }
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

            try
            {
                using (var proc = Process.GetProcessById((int)pid))
                {
                    return proc.MainModule?.FileName ?? string.Empty;
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private void UpdateFocus(bool focused, string procName, IntPtr hwnd, GameEntry matchedGame)
        {
            _lastProcessName = procName;
            bool focusStateChanged = _isGameActive != focused;
            bool hwndChanged = _activeHwnd != hwnd;
            bool gameChanged = !ReferenceEquals(_activeGame, matchedGame) || (_activeGame?.Id != matchedGame?.Id);

            _isGameActive = focused;
            _activeHwnd = hwnd;
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
