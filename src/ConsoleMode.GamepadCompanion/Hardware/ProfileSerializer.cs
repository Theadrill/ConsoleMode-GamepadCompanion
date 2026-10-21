using System;
using System.Collections.Generic;
using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.Hardware
{
    /// <summary>
    /// Serializa e desserializa instâncias de GameProfile em seções do arquivo games.ini.
    /// Suporta configurações de hardware (Deadzone, Gatilhos), analógicos e combos por botão.
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

            // Se não contém nenhuma chave de perfil salva na seção, retorna nulo para carregamento de preset
            if (string.IsNullOrEmpty(lStickModeStr) && string.IsNullOrEmpty(rStickModeStr))
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
    }
}
