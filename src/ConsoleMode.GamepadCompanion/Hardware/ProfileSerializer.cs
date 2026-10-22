using System;
using System.Collections.Generic;
using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.Hardware
{
    /// <summary>
    /// Serializa e desserializa instâncias de GameProfile em seções do arquivo games.ini e config.ini.
    /// Suporta configurações de hardware (Deadzone, Gatilhos), analógicos, trackpads e combos por botão.
    /// </summary>
    public static class ProfileSerializer
    {
        private const string KeyPrefix = "Profile_";

        public static void SerializeToIni(IniFile ini, string section, GameProfile profile)
        {
            if (ini == null || string.IsNullOrWhiteSpace(section) || profile == null) return;

            ini.SetValue(section, KeyPrefix + "Name", profile.Name ?? string.Empty);
            ini.SetValue(section, KeyPrefix + "UseGlobalDeadzone", profile.UseGlobalDeadzone.ToString());
            ini.SetValue(section, KeyPrefix + "CustomDeadzone", profile.CustomDeadzonePercent.ToString());
            ini.SetValue(section, KeyPrefix + "UseGlobalTrigger", profile.UseGlobalTriggerThreshold.ToString());
            ini.SetValue(section, KeyPrefix + "CustomTrigger", profile.CustomTriggerThresholdPercent.ToString());

            var page = profile.ActivePage;
            if (page != null)
            {
                ini.SetValue(section, KeyPrefix + "Sens", page.MouseSensitivity.ToString());

                // Analógico Esquerdo
                ini.SetValue(section, KeyPrefix + "LStick_Mode", page.LeftStick.Mode.ToString());
                ini.SetValue(section, KeyPrefix + "LStick_Up", InputAction.SerializeCombo(page.LeftStick.Up));
                ini.SetValue(section, KeyPrefix + "LStick_Down", InputAction.SerializeCombo(page.LeftStick.Down));
                ini.SetValue(section, KeyPrefix + "LStick_Left", InputAction.SerializeCombo(page.LeftStick.Left));
                ini.SetValue(section, KeyPrefix + "LStick_Right", InputAction.SerializeCombo(page.LeftStick.Right));
                ini.SetValue(section, KeyPrefix + "LStick_Additional", InputAction.SerializeCombo(page.LeftStick.AdditionalActions));
                ini.SetValue(section, KeyPrefix + "LStick_Click", InputAction.SerializeCombo(page.LeftStick.ClickActions));

                // Analógico Direito
                ini.SetValue(section, KeyPrefix + "RStick_Mode", page.RightStick.Mode.ToString());
                ini.SetValue(section, KeyPrefix + "RStick_Up", InputAction.SerializeCombo(page.RightStick.Up));
                ini.SetValue(section, KeyPrefix + "RStick_Down", InputAction.SerializeCombo(page.RightStick.Down));
                ini.SetValue(section, KeyPrefix + "RStick_Left", InputAction.SerializeCombo(page.RightStick.Left));
                ini.SetValue(section, KeyPrefix + "RStick_Right", InputAction.SerializeCombo(page.RightStick.Right));
                ini.SetValue(section, KeyPrefix + "RStick_Additional", InputAction.SerializeCombo(page.RightStick.AdditionalActions));
                ini.SetValue(section, KeyPrefix + "RStick_Click", InputAction.SerializeCombo(page.RightStick.ClickActions));

                // Trackpads Steam Deck
                SerializeTrackpad(ini, section, KeyPrefix + "LPad_", page.LeftTrackpad);
                SerializeTrackpad(ini, section, KeyPrefix + "RPad_", page.RightTrackpad);

                // Botões
                foreach (ControllerButton btn in (ControllerButton[])Enum.GetValues(typeof(ControllerButton)))
                {
                    var actions = page.GetActions(btn);
                    ini.SetValue(section, KeyPrefix + "Btn_" + btn, InputAction.SerializeCombo(actions));
                }
            }
        }

        public static GameProfile DeserializeFromIni(IniFile ini, string section)
        {
            if (ini == null || string.IsNullOrWhiteSpace(section)) return null;

            string lStickModeStr = ini.GetValue(section, KeyPrefix + "LStick_Mode");
            string rStickModeStr = ini.GetValue(section, KeyPrefix + "RStick_Mode");
            string lPadModeStr = ini.GetValue(section, KeyPrefix + "LPad_Mode");
            string rPadModeStr = ini.GetValue(section, KeyPrefix + "RPad_Mode");

            // Se não contém nenhuma chave de perfil salva na seção, retorna nulo para carregamento de preset
            if (string.IsNullOrEmpty(lStickModeStr) && string.IsNullOrEmpty(rStickModeStr) &&
                string.IsNullOrEmpty(lPadModeStr) && string.IsNullOrEmpty(rPadModeStr))
            {
                return null;
            }

            var profile = new GameProfile
            {
                Name = ini.GetValue(section, KeyPrefix + "Name", string.Empty),
                UseGlobalDeadzone = ini.GetBool(section, KeyPrefix + "UseGlobalDeadzone", true),
                CustomDeadzonePercent = ini.GetInt(section, KeyPrefix + "CustomDeadzone", 24),
                UseGlobalTriggerThreshold = ini.GetBool(section, KeyPrefix + "UseGlobalTrigger", true),
                CustomTriggerThresholdPercent = ini.GetInt(section, KeyPrefix + "CustomTrigger", 20)
            };

            var page = profile.ActivePage;
            page.MouseSensitivity = ini.GetInt(section, KeyPrefix + "Sens", 50);

            // Analógico Esquerdo
            if (Enum.TryParse(lStickModeStr, true, out StickMode lMode))
            {
                page.LeftStick.Mode = lMode;
            }
            page.LeftStick.Up = InputAction.DeserializeCombo(ini.GetValue(section, KeyPrefix + "LStick_Up"));
            page.LeftStick.Down = InputAction.DeserializeCombo(ini.GetValue(section, KeyPrefix + "LStick_Down"));
            page.LeftStick.Left = InputAction.DeserializeCombo(ini.GetValue(section, KeyPrefix + "LStick_Left"));
            page.LeftStick.Right = InputAction.DeserializeCombo(ini.GetValue(section, KeyPrefix + "LStick_Right"));
            page.LeftStick.AdditionalActions = InputAction.DeserializeCombo(ini.GetValue(section, KeyPrefix + "LStick_Additional"));
            page.LeftStick.ClickActions = InputAction.DeserializeCombo(ini.GetValue(section, KeyPrefix + "LStick_Click"));

            // Analógico Direito
            if (Enum.TryParse(rStickModeStr, true, out StickMode rMode))
            {
                page.RightStick.Mode = rMode;
            }
            page.RightStick.Up = InputAction.DeserializeCombo(ini.GetValue(section, KeyPrefix + "RStick_Up"));
            page.RightStick.Down = InputAction.DeserializeCombo(ini.GetValue(section, KeyPrefix + "RStick_Down"));
            page.RightStick.Left = InputAction.DeserializeCombo(ini.GetValue(section, KeyPrefix + "RStick_Left"));
            page.RightStick.Right = InputAction.DeserializeCombo(ini.GetValue(section, KeyPrefix + "RStick_Right"));
            page.RightStick.AdditionalActions = InputAction.DeserializeCombo(ini.GetValue(section, KeyPrefix + "RStick_Additional"));
            page.RightStick.ClickActions = InputAction.DeserializeCombo(ini.GetValue(section, KeyPrefix + "RStick_Click"));

            // Trackpads
            if (!string.IsNullOrEmpty(lPadModeStr))
            {
                page.LeftTrackpad = DeserializeTrackpad(ini, section, KeyPrefix + "LPad_");
            }
            else if (string.Equals(section, "DesktopProfile", StringComparison.OrdinalIgnoreCase))
            {
                // Fallback para preset de fábrica do Desktop se a chave ainda não existia no INI
                page.LeftTrackpad = new TrackpadConfig
                {
                    Mode = TrackpadMode.Scrollwheel,
                    Sensitivity = 50,
                    InvertScroll = true,
                    HapticIntensity = TrackpadHapticIntensity.Medium
                };
            }

            if (!string.IsNullOrEmpty(rPadModeStr))
            {
                page.RightTrackpad = DeserializeTrackpad(ini, section, KeyPrefix + "RPad_");
            }
            else if (string.Equals(section, "DesktopProfile", StringComparison.OrdinalIgnoreCase))
            {
                // Fallback para preset de fábrica do Desktop se a chave ainda não existia no INI
                page.RightTrackpad = new TrackpadConfig
                {
                    Mode = TrackpadMode.MouseTrackball,
                    Sensitivity = 50,
                    Friction = 50,
                    HapticIntensity = TrackpadHapticIntensity.Medium,
                    PressActions = new List<InputAction> { InputAction.FromMouseButton(MouseButton.Left) }
                };
            }
            else
            {
                // Preset padrão para jogos: trackpad direito como mouse/mira suave
                page.RightTrackpad = new TrackpadConfig
                {
                    Mode = TrackpadMode.MouseTrackball,
                    Sensitivity = 50,
                    Friction = 50,
                    HapticIntensity = TrackpadHapticIntensity.Medium
                };
            }

            // Botões
            foreach (ControllerButton btn in (ControllerButton[])Enum.GetValues(typeof(ControllerButton)))
            {
                string comboStr = ini.GetValue(section, KeyPrefix + "Btn_" + btn);
                if (!string.IsNullOrEmpty(comboStr))
                {
                    page.SetActions(btn, InputAction.DeserializeCombo(comboStr));
                }
            }

            return profile;
        }

        private static void SerializeTrackpad(IniFile ini, string section, string prefix, TrackpadConfig config)
        {
            if (config == null) return;
            ini.SetValue(section, prefix + "Mode", config.Mode.ToString());
            ini.SetValue(section, prefix + "Sens", config.Sensitivity.ToString());
            ini.SetValue(section, prefix + "Friction", config.Friction.ToString());
            ini.SetValue(section, prefix + "Haptics", config.HapticIntensity.ToString());
            ini.SetValue(section, prefix + "InvertScroll", config.InvertScroll.ToString());
            ini.SetValue(section, prefix + "Deadzone", config.DeadzonePercent.ToString());
            ini.SetValue(section, prefix + "ClickPressure", config.ClickPressureThreshold.ToString());
            ini.SetValue(section, prefix + "Touch", InputAction.SerializeCombo(config.TouchActions));
            ini.SetValue(section, prefix + "Press", InputAction.SerializeCombo(config.PressActions));
            ini.SetValue(section, prefix + "Up", InputAction.SerializeCombo(config.Up));
            ini.SetValue(section, prefix + "Down", InputAction.SerializeCombo(config.Down));
            ini.SetValue(section, prefix + "Left", InputAction.SerializeCombo(config.Left));
            ini.SetValue(section, prefix + "Right", InputAction.SerializeCombo(config.Right));
        }

        private static TrackpadConfig DeserializeTrackpad(IniFile ini, string section, string prefix)
        {
            var config = new TrackpadConfig();
            string modeStr = ini.GetValue(section, prefix + "Mode");
            if (!string.IsNullOrEmpty(modeStr) && Enum.TryParse(modeStr, true, out TrackpadMode mode))
            {
                config.Mode = mode;
            }

            config.Sensitivity = ini.GetInt(section, prefix + "Sens", 50);
            config.Friction = ini.GetInt(section, prefix + "Friction", 50);

            string hapticsStr = ini.GetValue(section, prefix + "Haptics");
            if (!string.IsNullOrEmpty(hapticsStr) && Enum.TryParse(hapticsStr, true, out TrackpadHapticIntensity haptics))
            {
                config.HapticIntensity = haptics;
            }

            config.InvertScroll = ini.GetBool(section, prefix + "InvertScroll", true);
            config.DeadzonePercent = ini.GetInt(section, prefix + "Deadzone", 15);
            config.ClickPressureThreshold = ini.GetInt(section, prefix + "ClickPressure", 11000);

            config.TouchActions = InputAction.DeserializeCombo(ini.GetValue(section, prefix + "Touch"));
            config.PressActions = InputAction.DeserializeCombo(ini.GetValue(section, prefix + "Press"));
            config.Up = InputAction.DeserializeCombo(ini.GetValue(section, prefix + "Up"));
            config.Down = InputAction.DeserializeCombo(ini.GetValue(section, prefix + "Down"));
            config.Left = InputAction.DeserializeCombo(ini.GetValue(section, prefix + "Left"));
            config.Right = InputAction.DeserializeCombo(ini.GetValue(section, prefix + "Right"));

            return config;
        }
    }
}
