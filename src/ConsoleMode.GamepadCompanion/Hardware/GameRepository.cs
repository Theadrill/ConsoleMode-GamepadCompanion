using System;
using System.Collections.Generic;
using System.IO;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.UI;

namespace ConsoleMode.GamepadCompanion.Hardware
{
    /// <summary>
    /// Gerencia a persistência da lista de jogos no arquivo games.ini.
    /// Realiza pré-cadastro padrão (Turtle WoW) caso o arquivo ainda não exista.
    /// </summary>
    public sealed class GameRepository
    {
        private const string SectionPrefix = "Game_";
        private readonly string _filePath;

        public GameRepository(string filePath = null)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                _filePath = Path.Combine(appDir, "games.ini");
            }
            else
            {
                _filePath = filePath;
            }
        }

        public string FilePath => _filePath;

        public List<GameEntry> GetAll()
        {
            if (!File.Exists(_filePath))
            {
                var defaults = CreateDefaultGames();
                SaveAll(defaults);
                return defaults;
            }

            var ini = new IniFile();
            ini.Load(_filePath);

            var games = new List<GameEntry>();
            foreach (string section in ini.GetSections())
            {
                if (!section.StartsWith(SectionPrefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                string id = ini.GetValue(section, "Id");
                string name = ini.GetValue(section, "Name");
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                    continue;

                games.Add(new GameEntry
                {
                    Id = id,
                    Name = name,
                    LauncherName = ini.GetValue(section, "LauncherName"),
                    MainExecutable = ini.GetValue(section, "MainExecutable"),
                    TargetPath = ini.GetValue(section, "TargetPath"),
                    WorkingDirectory = ini.GetValue(section, "WorkingDirectory"),
                    Arguments = ini.GetValue(section, "Arguments"),
                    CoverImagePath = ini.GetValue(section, "CoverImagePath"),
                    SteamGridDbSearchTerm = ini.GetValue(section, "SteamGridDbSearchTerm")
                });
            }

            if (games.Count == 0)
            {
                var defaults = CreateDefaultGames();
                SaveAll(defaults);
                return defaults;
            }

            return games;
        }

        public GameEntry GetById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            var list = GetAll();
            return list.Find(g => string.Equals(g.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public void Save(GameEntry game)
        {
            if (game == null || string.IsNullOrWhiteSpace(game.Id)) return;

            var ini = new IniFile();
            if (File.Exists(_filePath)) ini.Load(_filePath);

            string section = SectionPrefix + game.Id;
            ini.SetValue(section, "Id", game.Id);
            ini.SetValue(section, "Name", game.Name ?? string.Empty);
            ini.SetValue(section, "LauncherName", game.LauncherName ?? string.Empty);
            ini.SetValue(section, "MainExecutable", game.MainExecutable ?? string.Empty);
            ini.SetValue(section, "TargetPath", game.TargetPath ?? string.Empty);
            ini.SetValue(section, "WorkingDirectory", game.WorkingDirectory ?? string.Empty);
            ini.SetValue(section, "Arguments", game.Arguments ?? string.Empty);
            ini.SetValue(section, "CoverImagePath", game.CoverImagePath ?? string.Empty);
            ini.SetValue(section, "SteamGridDbSearchTerm", game.SteamGridDbSearchTerm ?? string.Empty);

            ini.Save(_filePath);
        }

        public void UpdateSearchTerm(string id, string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            var ini = new IniFile();
            if (!File.Exists(_filePath)) return;
            ini.Load(_filePath);

            string section = SectionPrefix + id;
            if (string.IsNullOrWhiteSpace(ini.GetValue(section, "Id"))) return;

            ini.SetValue(section, "SteamGridDbSearchTerm", searchTerm ?? string.Empty);
            ini.Save(_filePath);
        }

        public bool Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;

            var ini = new IniFile();
            if (!File.Exists(_filePath)) return false;
            ini.Load(_filePath);

            string section = SectionPrefix + id;
            bool removed = ini.RemoveSection(section);
            if (removed)
            {
                ini.Save(_filePath);
            }
            return removed;
        }

        public void SaveAll(IEnumerable<GameEntry> games)
        {
            if (games == null) return;

            var ini = new IniFile();
            foreach (var g in games)
            {
                if (g == null || string.IsNullOrWhiteSpace(g.Id)) continue;
                string section = SectionPrefix + g.Id;
                ini.SetValue(section, "Id", g.Id);
                ini.SetValue(section, "Name", g.Name ?? string.Empty);
                ini.SetValue(section, "LauncherName", g.LauncherName ?? string.Empty);
                ini.SetValue(section, "MainExecutable", g.MainExecutable ?? string.Empty);
                ini.SetValue(section, "TargetPath", g.TargetPath ?? string.Empty);
                ini.SetValue(section, "WorkingDirectory", g.WorkingDirectory ?? string.Empty);
                ini.SetValue(section, "Arguments", g.Arguments ?? string.Empty);
                ini.SetValue(section, "CoverImagePath", g.CoverImagePath ?? string.Empty);
                ini.SetValue(section, "SteamGridDbSearchTerm", g.SteamGridDbSearchTerm ?? string.Empty);
            }
            ini.Save(_filePath);
        }

        private static List<GameEntry> CreateDefaultGames()
        {
            return new List<GameEntry>
            {
                new GameEntry
                {
                    Id = "turtle-wow",
                    Name = Strings.DefaultGameName,
                    LauncherName = string.Empty,
                    MainExecutable = Strings.DefaultGameExecutable,
                    TargetPath = Strings.DefaultGameTargetPath,
                    WorkingDirectory = string.Empty,
                    Arguments = string.Empty,
                    CoverImagePath = string.Empty
                }
            };
        }
    }
}
