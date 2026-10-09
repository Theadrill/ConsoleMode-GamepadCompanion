using System;
using System.IO;
using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.Hardware
{
    /// <summary>
    /// Gerencia a persistência das configurações do aplicativo no arquivo config.ini.
    /// </summary>
    public sealed class ConfigRepository
    {
        private readonly string _filePath;

        public ConfigRepository(string filePath = null)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                _filePath = Path.Combine(appDir, "config.ini");
            }
            else
            {
                _filePath = filePath;
            }
        }

        public string FilePath => _filePath;

        public AppSettings Load()
        {
            var settings = new AppSettings();
            if (!File.Exists(_filePath))
            {
                Save(settings);
                return settings;
            }

            var ini = new IniFile();
            ini.Load(_filePath);

            settings.SelectedSlot = ini.GetInt("Settings", "SelectedSlot", -1);
            settings.MouseSensitivity = ini.GetInt("Settings", "MouseSensitivity", 50);
            settings.StickDeadzonePercent = ini.GetInt("Settings", "StickDeadzonePercent", 24);
            settings.TriggerThresholdPercent = ini.GetInt("Settings", "TriggerThresholdPercent", 20);
            settings.MappingEnabled = ini.GetBool("Settings", "MappingEnabled", true);
            settings.ClipCursorToGameWindow = ini.GetBool("Settings", "ClipCursorToGameWindow", true);
            settings.SteamGridDbApiKey = ini.GetValue("SteamGridDB", "ApiKey") ?? string.Empty;

            return settings;
        }

        public void Save(AppSettings settings)
        {
            if (settings == null) return;

            var ini = new IniFile();
            if (File.Exists(_filePath)) ini.Load(_filePath);

            ini.SetValue("Settings", "SelectedSlot", settings.SelectedSlot.ToString());
            ini.SetValue("Settings", "MouseSensitivity", settings.MouseSensitivity.ToString());
            ini.SetValue("Settings", "StickDeadzonePercent", settings.StickDeadzonePercent.ToString());
            ini.SetValue("Settings", "TriggerThresholdPercent", settings.TriggerThresholdPercent.ToString());
            ini.SetValue("Settings", "MappingEnabled", settings.MappingEnabled.ToString());
            ini.SetValue("Settings", "ClipCursorToGameWindow", settings.ClipCursorToGameWindow.ToString());
            ini.SetValue("SteamGridDB", "ApiKey", settings.SteamGridDbApiKey ?? string.Empty);

            ini.Save(_filePath);
        }
    }
}
