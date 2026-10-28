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

        [DllImport("psapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint GetProcessImageFileName(IntPtr hProcess, System.Text.StringBuilder lpImageFileName, uint nSize);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint QueryDosDevice(string lpDeviceName, System.Text.StringBuilder lpTargetPath, int ucchMax);

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

            // Garante que referências em cache apontem para a instância mais recente do jogo/perfil
            if (_activeGame != null && string.Equals(_activeGame.Id, game.Id, StringComparison.OrdinalIgnoreCase))
            {
                _activeGame = game;
            }
            if (_lastLaunchedGame != null && string.Equals(_lastLaunchedGame.Id, game.Id, StringComparison.OrdinalIgnoreCase))
            {
                _lastLaunchedGame = game;
            }
            if (_lastSelectedGame != null && string.Equals(_lastSelectedGame.Id, game.Id, StringComparison.OrdinalIgnoreCase))
            {
                _lastSelectedGame = game;
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

            if (_isCompanionActive)
            {
                UpdateFocus(false, procName, hwnd, null);
                return;
            }

            lock (_lock)
            {
                // Prioridade 1: Match por Caminho Completo do Executável ou Diretório (Universal e Sem Ambiguidade)
                if (!string.IsNullOrEmpty(procFullPath))
                {
                    string procDir = string.Empty;
                    try
                    {
                        procDir = Path.GetDirectoryName(procFullPath)?.TrimEnd('\\', '/');
                    }
                    catch { }

                    // 1a. Match exato pelo executável (TargetPath ou MainExecutable)
                    foreach (var g in _registeredGames)
                    {
                        if (g == null) continue;
                        if (!string.IsNullOrWhiteSpace(g.TargetPath) && string.Equals(g.TargetPath.Trim().Trim('"', '\''), procFullPath, StringComparison.OrdinalIgnoreCase))
                        {
                            matchedGame = g;
                            isGameFocused = true;
                            break;
                        }
                        if (!string.IsNullOrWhiteSpace(g.MainExecutable) && string.Equals(g.MainExecutable.Trim().Trim('"', '\''), procFullPath, StringComparison.OrdinalIgnoreCase))
                        {
                            matchedGame = g;
                            isGameFocused = true;
                            break;
                        }
                    }

                    // 1b. Match pelo diretório de execução (WorkingDirectory ou pasta do TargetPath)
                    if (!isGameFocused && !string.IsNullOrEmpty(procDir) && !IsSystemDirectory(procDir))
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
                                if (procDir.StartsWith(kvp.Key + "\\", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(procDir, kvp.Key, StringComparison.OrdinalIgnoreCase))
                                {
                                    matchedGame = kvp.Value;
                                    isGameFocused = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                // Prioridade 2: Match pelo Nome do Executável
                if (!isGameFocused && !string.IsNullOrEmpty(procName))
                {
                    // 2a. Se foi lançado recentemente pelo Companion e o executável confere
                    if (_lastLaunchedGame != null && MatchesGameExecutable(_lastLaunchedGame, procName))
                    {
                        matchedGame = _lastLaunchedGame;
                        isGameFocused = true;
                    }
                    // 2b. Mapeamento direto de executável registrado no repositório de jogos
                    else if (_exeToGameMap.TryGetValue(procName, out var game) ||
                             _exeToGameMap.TryGetValue(procName + ".exe", out game))
                    {
                        matchedGame = game;
                        isGameFocused = true;
                    }
                }
            }

            UpdateFocus(isGameFocused, procName, hwnd, matchedGame);
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
            if (pid == 0) return string.Empty;

            // Tenta permissões comuns de leitura de processos: 0x0400 (QueryInformation), 0x1000 (QueryLimitedInformation)
            uint[] accessRights = new uint[] { 0x0400, 0x1000, 0x1400 };
            foreach (uint access in accessRights)
            {
                IntPtr hProc = IntPtr.Zero;
                try
                {
                    hProc = OpenProcess(access, false, pid);
                    if (hProc != IntPtr.Zero)
                    {
                        var sb = new System.Text.StringBuilder(1024);
                        uint size = (uint)sb.Capacity;
                        if (QueryFullProcessImageName(hProc, 0, sb, ref size))
                        {
                            return sb.ToString();
                        }

                        sb.Clear();
                        if (GetProcessImageFileName(hProc, sb, (uint)sb.Capacity) > 0)
                        {
                            string devPath = sb.ToString();
                            string dosPath = ConvertDevicePathToDosPath(devPath);
                            if (!string.IsNullOrEmpty(dosPath))
                            {
                                return dosPath;
                            }
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

        private static string ConvertDevicePathToDosPath(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath)) return string.Empty;
            try
            {
                foreach (string drive in Environment.GetLogicalDrives())
                {
                    string driveLetter = drive.TrimEnd('\\', '/');
                    var sb = new System.Text.StringBuilder(260);
                    if (QueryDosDevice(driveLetter, sb, sb.Capacity) != 0)
                    {
                        string dev = sb.ToString();
                        if (devicePath.StartsWith(dev, StringComparison.OrdinalIgnoreCase))
                        {
                            return driveLetter + devicePath.Substring(dev.Length);
                        }
                    }
                }
            }
            catch
            {
            }
            return devicePath;
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
