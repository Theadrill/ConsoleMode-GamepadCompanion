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
            settings.DesktopModeEnabled = ini.GetBool("Settings", "DesktopModeEnabled", true);
            settings.ClipCursorToGameWindow = ini.GetBool("Settings", "ClipCursorToGameWindow", true);
            settings.SteamGridDbApiKey = ini.GetValue("SteamGridDB", "ApiKey") ?? string.Empty;

            settings.L4HardwareRemap = ParseRemap(ini.GetValue("SteamDeck", "L4Remap"));
            settings.L5HardwareRemap = ParseRemap(ini.GetValue("SteamDeck", "L5Remap"));
            settings.R4HardwareRemap = ParseRemap(ini.GetValue("SteamDeck", "R4Remap"));
            settings.R5HardwareRemap = ParseRemap(ini.GetValue("SteamDeck", "R5Remap"));

            return settings;
        }

        private static SteamDeckHardwareRemap ParseRemap(string val)
        {
            if (!string.IsNullOrEmpty(val) && Enum.TryParse(val, true, out SteamDeckHardwareRemap remap))
            {
                return remap;
            }
            return SteamDeckHardwareRemap.None;
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
            ini.SetValue("Settings", "DesktopModeEnabled", settings.DesktopModeEnabled.ToString());
            ini.SetValue("Settings", "ClipCursorToGameWindow", settings.ClipCursorToGameWindow.ToString());
            ini.SetValue("SteamGridDB", "ApiKey", settings.SteamGridDbApiKey ?? string.Empty);

            ini.SetValue("SteamDeck", "L4Remap", settings.L4HardwareRemap.ToString());
            ini.SetValue("SteamDeck", "L5Remap", settings.L5HardwareRemap.ToString());
            ini.SetValue("SteamDeck", "R4Remap", settings.R4HardwareRemap.ToString());
            ini.SetValue("SteamDeck", "R5Remap", settings.R5HardwareRemap.ToString());

            ini.Save(_filePath);
        }

        public GameProfile LoadDesktopProfile()
        {
            if (!File.Exists(_filePath))
            {
                var def = GameProfilePresets.CreateDesktopDefault();
                SaveDesktopProfile(def);
                return def;
            }

            var ini = new IniFile();
            ini.Load(_filePath);
            var profile = ProfileSerializer.DeserializeFromIni(ini, "DesktopProfile");
            if (profile == null)
            {
                profile = GameProfilePresets.CreateDesktopDefault();
                SaveDesktopProfile(profile);
                return profile;
            }

            // Auto-migração: se o config.ini antigo não possuía trackpads configurados no arquivo,
            // persiste imediatamente os presets ergonômicos de fábrica no arquivo.
            bool hasLPad = !string.IsNullOrEmpty(ini.GetValue("DesktopProfile", "Profile_LPad_Mode"));
            bool hasRPad = !string.IsNullOrEmpty(ini.GetValue("DesktopProfile", "Profile_RPad_Mode"));
            if (!hasLPad || !hasRPad)
            {
                SaveDesktopProfile(profile);
            }

            return profile;
        }

        public void SaveDesktopProfile(GameProfile profile)
        {
            if (profile == null) return;
            var ini = new IniFile();
            if (File.Exists(_filePath)) ini.Load(_filePath);
            ProfileSerializer.SerializeToIni(ini, "DesktopProfile", profile);
            ini.Save(_filePath);
        }
    }
}
