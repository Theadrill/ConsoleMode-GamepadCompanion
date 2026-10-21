using System;
using System.Collections.Generic;

namespace ConsoleMode.GamepadCompanion.Core.Models
{
    /// <summary>
    /// Fornece presets prontos e validados para o Companion:
    /// 1. WoW Classic (Octowow) — Smart Mouse Look F9 + WASD + Espaço/F9
    /// 2. RPG / MMO (Câmera Botão Direito) — Mantém Clique Direito ao girar câmera com o analógico direito
    /// 3. Padrão PC (WASD + Mouse) — Mapeamento de tiro/ação em primeira/terceira pessoa
    /// </summary>
    public static class GameProfilePresets
    {
        public const string PresetWowClassic = "WoW Classic (Octowow)";
        public const string PresetRpgRightClickCamera = "RPG / MMO (Câmera Botão Direito)";
        public const string PresetStandardPc = "Padrão PC (WASD + Mouse)";

        public static IEnumerable<string> PresetNames => new[]
        {
            PresetWowClassic,
            PresetRpgRightClickCamera,
            PresetStandardPc
        };

        public static GameProfile CreatePreset(string presetName)
        {
            if (string.Equals(presetName, PresetRpgRightClickCamera, StringComparison.OrdinalIgnoreCase))
            {
                return CreateRpgRightClickCamera();
            }
            if (string.Equals(presetName, PresetStandardPc, StringComparison.OrdinalIgnoreCase))
            {
                return CreateStandardPc();
            }
            return CreateWowClassic();
        }

        /// <summary>
        /// Layout Octowow original para ConsoleModeVanilla / Turtle WoW:
        /// - LeftStick: WASD + F9 (Smart Mouse Look) + L3 = Mouse Left
        /// - RightStick: MouseMove + R3 = Mouse Right
        /// - A: Espaço + F9
        /// - B: 3, X: 1, Y: 2
        /// - D-Pad: Cima (7), Baixo (8), Esquerda (9), Direita (0)
        /// - LB: Tab, RB: LeftControl
        /// - LT: LeftShift, RT: LeftAlt
        /// - Select: M, Start: F11
        /// </summary>
        public static GameProfile CreateWowClassic()
        {
            var profile = new GameProfile
            {
                Name = PresetWowClassic,
                UseGlobalDeadzone = true,
                CustomDeadzonePercent = 24,
                UseGlobalTriggerThreshold = true,
                CustomTriggerThresholdPercent = 20
            };

            var page = profile.ActivePage;
            page.MouseSensitivity = 50;

            // Analógico Esquerdo: WASD + F9 adicional + L3 Mouse L
            page.LeftStick.Mode = StickMode.DirectionalKeys;
            page.LeftStick.Up = new List<InputAction> { InputAction.FromKey(VirtualKey.W) };
            page.LeftStick.Down = new List<InputAction> { InputAction.FromKey(VirtualKey.S) };
            page.LeftStick.Left = new List<InputAction> { InputAction.FromKey(VirtualKey.A) };
            page.LeftStick.Right = new List<InputAction> { InputAction.FromKey(VirtualKey.D) };
            page.LeftStick.AdditionalActions = new List<InputAction> { InputAction.FromKey(VirtualKey.F9) };
            page.LeftStick.ClickActions = new List<InputAction> { InputAction.FromMouseButton(MouseButton.Left) };

            // Analógico Direito: MouseMove + R3 Mouse R
            page.RightStick.Mode = StickMode.MouseMove;
            page.RightStick.AdditionalActions = new List<InputAction>();
            page.RightStick.ClickActions = new List<InputAction> { InputAction.FromMouseButton(MouseButton.Right) };

            // Botões Faciais
            page.SetActions(ControllerButton.A, new[] { InputAction.FromKey(VirtualKey.Space), InputAction.FromKey(VirtualKey.F9) });
            page.SetActions(ControllerButton.B, new[] { InputAction.FromKey(VirtualKey.D3) });
            page.SetActions(ControllerButton.X, new[] { InputAction.FromKey(VirtualKey.D1) });
            page.SetActions(ControllerButton.Y, new[] { InputAction.FromKey(VirtualKey.D2) });

            // D-Pad
            page.SetActions(ControllerButton.DPadUp, new[] { InputAction.FromKey(VirtualKey.D7) });
            page.SetActions(ControllerButton.DPadDown, new[] { InputAction.FromKey(VirtualKey.D8) });
            page.SetActions(ControllerButton.DPadLeft, new[] { InputAction.FromKey(VirtualKey.D9) });
            page.SetActions(ControllerButton.DPadRight, new[] { InputAction.FromKey(VirtualKey.D0) });

            // Bumpers & Triggers
            page.SetActions(ControllerButton.LeftShoulder, new[] { InputAction.FromKey(VirtualKey.Tab) });
            page.SetActions(ControllerButton.RightShoulder, new[] { InputAction.FromKey(VirtualKey.LeftControl) });
            page.SetActions(ControllerButton.LeftTrigger, new[] { InputAction.FromKey(VirtualKey.LeftShift) });
            page.SetActions(ControllerButton.RightTrigger, new[] { InputAction.FromKey(VirtualKey.LeftAlt) });

            // Menus
            page.SetActions(ControllerButton.Back, new[] { InputAction.FromKey(VirtualKey.M) });
            page.SetActions(ControllerButton.Start, new[] { InputAction.FromKey(VirtualKey.F11) });

            // Clicks
            page.SetActions(ControllerButton.LeftStickClick, new[] { InputAction.FromMouseButton(MouseButton.Left) });
            page.SetActions(ControllerButton.RightStickClick, new[] { InputAction.FromMouseButton(MouseButton.Right) });

            return profile;
        }

        /// <summary>
        /// Preset RPG / MMO com travamento de câmera por Botão Direito:
        /// Ao movimentar o analógico direito para girar a câmera, o Botão Direito do Mouse é mantido pressionado
        /// automaticamente enquanto o stick estiver em movimento, soltando ao parar.
        /// </summary>
        public static GameProfile CreateRpgRightClickCamera()
        {
            var profile = new GameProfile
            {
                Name = PresetRpgRightClickCamera,
                UseGlobalDeadzone = true,
                CustomDeadzonePercent = 24,
                UseGlobalTriggerThreshold = true,
                CustomTriggerThresholdPercent = 20
            };

            var page = profile.ActivePage;
            page.MouseSensitivity = 50;

            page.LeftStick.Mode = StickMode.DirectionalKeys;
            page.LeftStick.Up = new List<InputAction> { InputAction.FromKey(VirtualKey.W) };
            page.LeftStick.Down = new List<InputAction> { InputAction.FromKey(VirtualKey.S) };
            page.LeftStick.Left = new List<InputAction> { InputAction.FromKey(VirtualKey.A) };
            page.LeftStick.Right = new List<InputAction> { InputAction.FromKey(VirtualKey.D) };
            page.LeftStick.AdditionalActions = new List<InputAction>();
            page.LeftStick.ClickActions = new List<InputAction> { InputAction.FromMouseButton(MouseButton.Left) };

            // Analógico Direito segura Clique Direito ao se mover para girar câmera
            page.RightStick.Mode = StickMode.MouseMove;
            page.RightStick.AdditionalActions = new List<InputAction> { InputAction.FromMouseButton(MouseButton.Right) };
            page.RightStick.ClickActions = new List<InputAction> { InputAction.FromMouseButton(MouseButton.Right) };

            page.SetActions(ControllerButton.A, new[] { InputAction.FromKey(VirtualKey.Space) });
            page.SetActions(ControllerButton.B, new[] { InputAction.FromKey(VirtualKey.D3) });
            page.SetActions(ControllerButton.X, new[] { InputAction.FromKey(VirtualKey.D1) });
            page.SetActions(ControllerButton.Y, new[] { InputAction.FromKey(VirtualKey.D2) });

            page.SetActions(ControllerButton.DPadUp, new[] { InputAction.FromKey(VirtualKey.D1) });
            page.SetActions(ControllerButton.DPadDown, new[] { InputAction.FromKey(VirtualKey.D2) });
            page.SetActions(ControllerButton.DPadLeft, new[] { InputAction.FromKey(VirtualKey.D3) });
            page.SetActions(ControllerButton.DPadRight, new[] { InputAction.FromKey(VirtualKey.D4) });

            page.SetActions(ControllerButton.LeftShoulder, new[] { InputAction.FromKey(VirtualKey.Tab) });
            page.SetActions(ControllerButton.RightShoulder, new[] { InputAction.FromKey(VirtualKey.LeftControl) });
            page.SetActions(ControllerButton.LeftTrigger, new[] { InputAction.FromKey(VirtualKey.LeftShift) });
            page.SetActions(ControllerButton.RightTrigger, new[] { InputAction.FromKey(VirtualKey.LeftAlt) });

            page.SetActions(ControllerButton.Back, new[] { InputAction.FromKey(VirtualKey.M) });
            page.SetActions(ControllerButton.Start, new[] { InputAction.FromKey(VirtualKey.Escape) });

            page.SetActions(ControllerButton.LeftStickClick, new[] { InputAction.FromMouseButton(MouseButton.Left) });
            page.SetActions(ControllerButton.RightStickClick, new[] { InputAction.FromMouseButton(MouseButton.Right) });

            return profile;
        }

        /// <summary>
        /// Preset Padrão PC (WASD + Mouse livre):
        /// - Gatilhos: RT = Clique Esquerdo (Ataque/Tiro), LT = Clique Direito (Mira)
        /// - Bumpers: LB = Q, RB = G
        /// - A: Espaço (Pulo), B: C (Agachar), X: E (Interagir), Y: F (Alternar)
        /// - L3: Shift (Correr), R3: C (Agachar)
        /// - D-Pad: 1, 2, 3, 4
        /// </summary>
        public static GameProfile CreateStandardPc()
        {
            var profile = new GameProfile
            {
                Name = PresetStandardPc,
                UseGlobalDeadzone = true,
                CustomDeadzonePercent = 24,
                UseGlobalTriggerThreshold = true,
                CustomTriggerThresholdPercent = 20
            };

            var page = profile.ActivePage;
            page.MouseSensitivity = 50;

            page.LeftStick.Mode = StickMode.DirectionalKeys;
            page.LeftStick.Up = new List<InputAction> { InputAction.FromKey(VirtualKey.W) };
            page.LeftStick.Down = new List<InputAction> { InputAction.FromKey(VirtualKey.S) };
            page.LeftStick.Left = new List<InputAction> { InputAction.FromKey(VirtualKey.A) };
            page.LeftStick.Right = new List<InputAction> { InputAction.FromKey(VirtualKey.D) };
            page.LeftStick.AdditionalActions = new List<InputAction>();
            page.LeftStick.ClickActions = new List<InputAction> { InputAction.FromKey(VirtualKey.LeftShift) };

            page.RightStick.Mode = StickMode.MouseMove;
            page.RightStick.AdditionalActions = new List<InputAction>();
            page.RightStick.ClickActions = new List<InputAction> { InputAction.FromKey(VirtualKey.C) };

            page.SetActions(ControllerButton.A, new[] { InputAction.FromKey(VirtualKey.Space) });
            page.SetActions(ControllerButton.B, new[] { InputAction.FromKey(VirtualKey.C) });
            page.SetActions(ControllerButton.X, new[] { InputAction.FromKey(VirtualKey.E) });
            page.SetActions(ControllerButton.Y, new[] { InputAction.FromKey(VirtualKey.F) });

            page.SetActions(ControllerButton.DPadUp, new[] { InputAction.FromKey(VirtualKey.D1) });
            page.SetActions(ControllerButton.DPadRight, new[] { InputAction.FromKey(VirtualKey.D2) });
            page.SetActions(ControllerButton.DPadDown, new[] { InputAction.FromKey(VirtualKey.D3) });
            page.SetActions(ControllerButton.DPadLeft, new[] { InputAction.FromKey(VirtualKey.D4) });

            page.SetActions(ControllerButton.LeftShoulder, new[] { InputAction.FromKey(VirtualKey.Q) });
            page.SetActions(ControllerButton.RightShoulder, new[] { InputAction.FromKey(VirtualKey.G) });
            page.SetActions(ControllerButton.LeftTrigger, new[] { InputAction.FromMouseButton(MouseButton.Right) });
            page.SetActions(ControllerButton.RightTrigger, new[] { InputAction.FromMouseButton(MouseButton.Left) });

            page.SetActions(ControllerButton.Back, new[] { InputAction.FromKey(VirtualKey.Tab) });
            page.SetActions(ControllerButton.Start, new[] { InputAction.FromKey(VirtualKey.Escape) });

            page.SetActions(ControllerButton.LeftStickClick, new[] { InputAction.FromKey(VirtualKey.LeftShift) });
            page.SetActions(ControllerButton.RightStickClick, new[] { InputAction.FromKey(VirtualKey.C) });

            return profile;
        }
    }
}
