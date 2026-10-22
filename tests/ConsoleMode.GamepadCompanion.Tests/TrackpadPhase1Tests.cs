using System;
using System.Collections.Generic;
using ConsoleMode.GamepadCompanion.Core.Interfaces;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.Engine;
using ConsoleMode.GamepadCompanion.Hardware;
using ConsoleMode.GamepadCompanion.Profiles;
using Xunit;

namespace ConsoleMode.GamepadCompanion.Tests
{
    public class TrackpadPhase1Tests
    {
        private sealed class TestInputSimulator : IInputSimulator
        {
            public enum EventType { KeyDown, KeyUp, MouseDown, MouseUp, MouseMove, MouseWheel }

            public struct InputEvent
            {
                public EventType Type;
                public VirtualKey Key;
                public MouseButton Button;
                public int Dx;
                public int Dy;
                public int WheelDelta;
            }

            public List<InputEvent> Events { get; } = new List<InputEvent>();

            public void KeyDown(VirtualKey key) => Events.Add(new InputEvent { Type = EventType.KeyDown, Key = key });
            public void KeyUp(VirtualKey key) => Events.Add(new InputEvent { Type = EventType.KeyUp, Key = key });
            public void MouseButtonDown(MouseButton button) => Events.Add(new InputEvent { Type = EventType.MouseDown, Button = button });
            public void MouseButtonUp(MouseButton button) => Events.Add(new InputEvent { Type = EventType.MouseUp, Button = button });
            public void MouseMoveRelative(int dx, int dy)
            {
                if (dx == 0 && dy == 0) return;
                Events.Add(new InputEvent { Type = EventType.MouseMove, Dx = dx, Dy = dy });
            }
            public void MouseWheel(int delta) => Events.Add(new InputEvent { Type = EventType.MouseWheel, WheelDelta = delta });
        }

        private sealed class TestHapticsProvider : ISteamDeckHaptics
        {
            public List<(bool isRightPad, TrackpadHapticIntensity intensity)> Pulses { get; } =
                new List<(bool, TrackpadHapticIntensity)>();

            public bool TriggerHaptic(bool isRightPad, TrackpadHapticIntensity intensity, bool isClick = false)
            {
                Pulses.Add((isRightPad, intensity));
                return true;
            }
        }

        #region 1. Modelo de Domínio & Presets

        [Fact]
        public void TrackpadConfig_DefaultValues_AreCorrect()
        {
            var config = new TrackpadConfig();

            Assert.Equal(TrackpadMode.Disabled, config.Mode);
            Assert.Equal(50, config.Sensitivity);
            Assert.Equal(50, config.Friction);
            Assert.Equal(TrackpadHapticIntensity.High, config.HapticIntensity);
            Assert.True(config.InvertScroll);
            Assert.Equal(15, config.DeadzonePercent);
            Assert.Equal(11000, config.ClickPressureThreshold);
            Assert.Empty(config.TouchActions);
            Assert.Empty(config.PressActions);
            Assert.Empty(config.Up);
            Assert.Empty(config.Down);
            Assert.Empty(config.Left);
            Assert.Empty(config.Right);
        }

        [Fact]
        public void TrackpadConfig_Clone_CreatesDeepIndependentCopy()
        {
            var original = new TrackpadConfig
            {
                Mode = TrackpadMode.DPad,
                Sensitivity = 75,
                Friction = 30,
                HapticIntensity = TrackpadHapticIntensity.High,
                InvertScroll = false,
                DeadzonePercent = 20,
                ClickPressureThreshold = 9500,
                TouchActions = new List<InputAction> { InputAction.FromKey(VirtualKey.Space) },
                PressActions = new List<InputAction> { InputAction.FromMouseButton(MouseButton.Left) },
                Up = new List<InputAction> { InputAction.FromKey(VirtualKey.W) },
                Down = new List<InputAction> { InputAction.FromKey(VirtualKey.S) },
                Left = new List<InputAction> { InputAction.FromKey(VirtualKey.A) },
                Right = new List<InputAction> { InputAction.FromKey(VirtualKey.D) }
            };

            var clone = original.Clone();

            Assert.NotSame(original, clone);
            Assert.Equal(original.Mode, clone.Mode);
            Assert.Equal(original.Sensitivity, clone.Sensitivity);
            Assert.Equal(original.Friction, clone.Friction);
            Assert.Equal(original.HapticIntensity, clone.HapticIntensity);
            Assert.Equal(original.InvertScroll, clone.InvertScroll);
            Assert.Equal(original.DeadzonePercent, clone.DeadzonePercent);
            Assert.Equal(original.ClickPressureThreshold, clone.ClickPressureThreshold);

            Assert.NotSame(original.TouchActions, clone.TouchActions);
            Assert.Equal(VirtualKey.Space, clone.TouchActions[0].Key);
            Assert.NotSame(original.PressActions, clone.PressActions);
            Assert.Equal(MouseButton.Left, clone.PressActions[0].MouseButton);

            // Mutating clone does not change original
            clone.Up.Clear();
            Assert.Single(original.Up);
        }

        [Fact]
        public void GameProfilePresets_DesktopDefault_ConfiguresDualTrackpads()
        {
            var profile = GameProfilePresets.CreateDesktopDefault();
            var page = profile.ActivePage;

            // Trackpad Direito: Mouse/Trackball com clique mecânico no Mouse Left
            Assert.Equal(TrackpadMode.MouseTrackball, page.RightTrackpad.Mode);
            Assert.Equal(50, page.RightTrackpad.Sensitivity);
            Assert.Equal(50, page.RightTrackpad.Friction);
            Assert.Equal(TrackpadHapticIntensity.High, page.RightTrackpad.HapticIntensity);
            Assert.Single(page.RightTrackpad.PressActions);
            Assert.Equal(MouseButton.Left, page.RightTrackpad.PressActions[0].MouseButton);

            // Trackpad Esquerdo: Scrollwheel vertical com rolagem natural invertida
            Assert.Equal(TrackpadMode.Scrollwheel, page.LeftTrackpad.Mode);
            Assert.Equal(50, page.LeftTrackpad.Sensitivity);
            Assert.True(page.LeftTrackpad.InvertScroll);
            Assert.Equal(TrackpadHapticIntensity.High, page.LeftTrackpad.HapticIntensity);
        }

        [Fact]
        public void GameProfilePresets_StandardPcAndGaming_ConfiguresRightTrackpadAsMouse()
        {
            var wow = GameProfilePresets.CreateWowClassic();
            Assert.Equal(TrackpadMode.MouseTrackball, wow.ActivePage.RightTrackpad.Mode);
            Assert.Equal(TrackpadHapticIntensity.High, wow.ActivePage.RightTrackpad.HapticIntensity);

            var pc = GameProfilePresets.CreateStandardPc();
            Assert.Equal(TrackpadMode.MouseTrackball, pc.ActivePage.RightTrackpad.Mode);

            var rpg = GameProfilePresets.CreateRpgRightClickCamera();
            Assert.Equal(TrackpadMode.MouseTrackball, rpg.ActivePage.RightTrackpad.Mode);
        }

        #endregion

        #region 2. Serialização no ProfileSerializer

        [Fact]
        public void ProfileSerializer_SerializeAndDeserializeTrackpads_RoundtripsSuccessfully()
        {
            var ini = new IniFile();
            var profile = new GameProfile { Name = "TestTrackpadProfile" };
            var page = profile.ActivePage;

            page.LeftTrackpad.Mode = TrackpadMode.Scrollwheel;
            page.LeftTrackpad.Sensitivity = 65;
            page.LeftTrackpad.InvertScroll = true;
            page.LeftTrackpad.HapticIntensity = TrackpadHapticIntensity.Medium;

            page.RightTrackpad.Mode = TrackpadMode.MouseTrackball;
            page.RightTrackpad.Sensitivity = 70;
            page.RightTrackpad.Friction = 40;
            page.RightTrackpad.ClickPressureThreshold = 14500;
            page.RightTrackpad.HapticIntensity = TrackpadHapticIntensity.High;
            page.RightTrackpad.PressActions = new List<InputAction> { InputAction.FromMouseButton(MouseButton.Left) };

            ProfileSerializer.SerializeToIni(ini, "TestSection", profile);
            var loaded = ProfileSerializer.DeserializeFromIni(ini, "TestSection");

            Assert.NotNull(loaded);
            var loadedPage = loaded.ActivePage;

            Assert.Equal(TrackpadMode.Scrollwheel, loadedPage.LeftTrackpad.Mode);
            Assert.Equal(65, loadedPage.LeftTrackpad.Sensitivity);
            Assert.True(loadedPage.LeftTrackpad.InvertScroll);
            Assert.Equal(TrackpadHapticIntensity.Medium, loadedPage.LeftTrackpad.HapticIntensity);

            Assert.Equal(TrackpadMode.MouseTrackball, loadedPage.RightTrackpad.Mode);
            Assert.Equal(70, loadedPage.RightTrackpad.Sensitivity);
            Assert.Equal(40, loadedPage.RightTrackpad.Friction);
            Assert.Equal(14500, loadedPage.RightTrackpad.ClickPressureThreshold);
            Assert.Equal(TrackpadHapticIntensity.High, loadedPage.RightTrackpad.HapticIntensity);
            Assert.Single(loadedPage.RightTrackpad.PressActions);
            Assert.Equal(MouseButton.Left, loadedPage.RightTrackpad.PressActions[0].MouseButton);
        }

        #endregion

        #region 3. Motor Matemático - TrackpadProcessor

        [Fact]
        public void TrackpadProcessor_FirstTouch_ProducesNoDeltaToPreventJump()
        {
            var processor = new TrackpadProcessor(isRightPad: true);
            var config = new TrackpadConfig { Mode = TrackpadMode.MouseTrackball };

            var output = processor.Process(isTouch: true, isPress: false, rawX: 10000, rawY: 5000, config: config, dt: 0.008f);

            Assert.Equal(0f, output.MouseDx);
            Assert.Equal(0f, output.MouseDy);
        }

        [Fact]
        public void TrackpadProcessor_AntiJitter_FiltersMicroTremorBelowThreshold()
        {
            var processor = new TrackpadProcessor(isRightPad: true);
            var config = new TrackpadConfig { Mode = TrackpadMode.MouseTrackball };

            // Primeiro toque âncora em (1000, 1000)
            processor.Process(isTouch: true, isPress: false, rawX: 1000, rawY: 1000, config: config, dt: 0.008f);

            // Tremulação capacitiva de ~7.8 unidades (< 10f DeadzoneThreshold)
            var output = processor.Process(isTouch: true, isPress: false, rawX: 1006, rawY: 1005, config: config, dt: 0.008f);

            Assert.Equal(0f, output.MouseDx);
            Assert.Equal(0f, output.MouseDy);
        }

        [Fact]
        public void TrackpadProcessor_SoftKneeDeadband_StartsSmoothlyWithoutStepJump()
        {
            var processor = new TrackpadProcessor(isRightPad: true);
            var config = new TrackpadConfig { Mode = TrackpadMode.MouseTrackball, Sensitivity = 50 };

            // Primeiro toque
            processor.Process(isTouch: true, isPress: false, rawX: 1000, rawY: 1000, config: config, dt: 0.008f);

            // Delta levemente acima da zona morta configurada em TrackpadTuning
            short testDelta = (short)(TrackpadTuning.DeadzoneThreshold + 5);
            var output = processor.Process(isTouch: true, isPress: false, rawX: (short)(1000 + testDelta), rawY: 1000, config: config, dt: 0.008f);

            // Deve iniciar suavemente a partir de zero (sub-pixel minúsculo < 0.05px), sem salto abrupto
            Assert.True(output.MouseDx > 0f, "Deve registrar deslocamento suave acima do limiar");
            Assert.True(output.MouseDx < 0.05f, "Zona morta suave (soft-knee) não deve saltar o valor bruto do deadband");
        }

        [Fact]
        public void TrackpadProcessor_MouseMovement_ConvertsDeltasAndInvertsScreenY()
        {
            var processor = new TrackpadProcessor(isRightPad: true);
            var config = new TrackpadConfig { Mode = TrackpadMode.MouseTrackball, Sensitivity = 50 };

            processor.Process(isTouch: true, isPress: false, rawX: 0, rawY: 0, config: config, dt: 0.008f);

            // Mover para direita e para cima (+1000 X, +1000 Y no espaço do deck)
            var output = processor.Process(isTouch: true, isPress: false, rawX: 1000, rawY: 1000, config: config, dt: 0.008f);

            Assert.True(output.MouseDx > 0f, "Movimento para direita deve produzir mouseDx positivo");
            Assert.True(output.MouseDy < 0f, "Movimento para cima no deck deve produzir mouseDy negativo (cima na tela)");
        }

        [Fact]
        public void TrackpadProcessor_SpeedAcceleration_ProducesHigherOutputOnFastFlick()
        {
            var slowProc = new TrackpadProcessor(isRightPad: true);
            var fastProc = new TrackpadProcessor(isRightPad: true);
            var config = new TrackpadConfig { Mode = TrackpadMode.MouseTrackball, Sensitivity = 50 };

            slowProc.Process(isTouch: true, isPress: false, rawX: 0, rawY: 0, config: config, dt: 0.008f);
            fastProc.Process(isTouch: true, isPress: false, rawX: 0, rawY: 0, config: config, dt: 0.008f);

            // Deslocamento de 3000 em 50ms (lento) vs 3000 em 8ms (rápido flick)
            var slowOut = slowProc.Process(isTouch: true, isPress: false, rawX: 3000, rawY: 0, config: config, dt: 0.050f);
            var fastOut = fastProc.Process(isTouch: true, isPress: false, rawX: 3000, rawY: 0, config: config, dt: 0.008f);

            Assert.True(fastOut.MouseDx > slowOut.MouseDx, "Flick rápido deve aplicar aceleração maior que movimento lento");
        }

        [Fact]
        public void TrackpadProcessor_TrackballInertia_CoastsAfterFastReleaseAndDecays()
        {
            var processor = new TrackpadProcessor(isRightPad: true);
            var config = new TrackpadConfig { Mode = TrackpadMode.MouseTrackball, Sensitivity = 50, Friction = 50 };

            // Simula swipe rápido
            processor.Process(isTouch: true, isPress: false, rawX: 0, rawY: 0, config: config, dt: 0.008f);
            processor.Process(isTouch: true, isPress: false, rawX: 4000, rawY: 0, config: config, dt: 0.008f);

            // Solta o dedo rápido no ar
            var coast1 = processor.Process(isTouch: false, isPress: false, rawX: 4000, rawY: 0, config: config, dt: 0.016f);
            Assert.True(coast1.MouseDx > 0f, "Deve gerar inércia na mesma direção do flick");

            // Frame subsequente de inércia desacelerando
            var coast2 = processor.Process(isTouch: false, isPress: false, rawX: 4000, rawY: 0, config: config, dt: 0.016f);
            Assert.True(coast2.MouseDx > 0f, "Inércia continua no próximo frame");
            Assert.True(coast2.MouseDx < coast1.MouseDx, "Inércia deve decair suavemente pelo atrito");
        }

        [Fact]
        public void TrackpadProcessor_Trackball_StopOnTouchCancelsInertiaImmediately()
        {
            var processor = new TrackpadProcessor(isRightPad: true);
            var config = new TrackpadConfig { Mode = TrackpadMode.MouseTrackball, Sensitivity = 50, Friction = 50 };

            // Swipe rápido e soltura
            processor.Process(isTouch: true, isPress: false, rawX: 0, rawY: 0, config: config, dt: 0.008f);
            processor.Process(isTouch: true, isPress: false, rawX: 4000, rawY: 0, config: config, dt: 0.008f);
            processor.Process(isTouch: false, isPress: false, rawX: 4000, rawY: 0, config: config, dt: 0.016f);

            // Usuário encosta novamente na trackball
            var touchAgain = processor.Process(isTouch: true, isPress: false, rawX: 4000, rawY: 0, config: config, dt: 0.016f);
            Assert.Equal(0f, touchAgain.MouseDx);
            Assert.Equal(0f, touchAgain.MouseDy);

            // Ao soltar parado após tocar, não deve haver inércia residual
            var releaseStill = processor.Process(isTouch: false, isPress: false, rawX: 4000, rawY: 0, config: config, dt: 0.016f);
            Assert.Equal(0f, releaseStill.MouseDx);
            Assert.Equal(0f, releaseStill.MouseDy);
        }

        [Fact]
        public void TrackpadProcessor_Scrollwheel_NaturalScrollingEmitsInvertedWheelDelta()
        {
            var processor = new TrackpadProcessor(isRightPad: false);
            var config = new TrackpadConfig
            {
                Mode = TrackpadMode.Scrollwheel,
                InvertScroll = true, // Natural scrolling padrão de fábrica
                Sensitivity = 50
            };

            processor.Process(isTouch: true, isPress: false, rawX: 0, rawY: 0, config: config, dt: 0.008f);

            // Simula passada para cima ao longo de frames físicos realistas (< MaxPhysicalDeltaPerFrame)
            processor.Process(isTouch: true, isPress: false, rawX: 0, rawY: 2000, config: config, dt: 0.008f);
            var scrollUp = processor.Process(isTouch: true, isPress: false, rawX: 0, rawY: 5000, config: config, dt: 0.008f);

            Assert.True(scrollUp.WheelDelta < 0, "Rolagem natural: deslizar p/ cima deve rolar p/ baixo (WheelDelta < 0)");
            Assert.True(scrollUp.TriggerHaptic, "Deve disparar pulso háptico de catraca ao emitir notch");

            // Solta o dedo antes de inverter a direção (gesto humano natural)
            processor.Process(isTouch: false, isPress: false, rawX: 0, rawY: 5000, config: config, dt: 0.016f);

            // Simula passada para baixo ao longo de frames físicos
            processor.Process(isTouch: true, isPress: false, rawX: 0, rawY: 0, config: config, dt: 0.008f);
            processor.Process(isTouch: true, isPress: false, rawX: 0, rawY: -2000, config: config, dt: 0.008f);
            var scrollDown = processor.Process(isTouch: true, isPress: false, rawX: 0, rawY: -5000, config: config, dt: 0.008f);

            Assert.True(scrollDown.WheelDelta > 0, "Rolagem natural: deslizar p/ baixo deve rolar p/ cima (WheelDelta > 0)");
        }

        [Fact]
        public void TrackpadProcessor_Scrollwheel_NonInvertedScrollWorksWhenConfigured()
        {
            var processor = new TrackpadProcessor(isRightPad: false);
            var config = new TrackpadConfig
            {
                Mode = TrackpadMode.Scrollwheel,
                InvertScroll = false,
                Sensitivity = 50
            };

            processor.Process(isTouch: true, isPress: false, rawX: 0, rawY: 0, config: config, dt: 0.008f);

            // Simula passada para cima ao longo de frames (< MaxPhysicalDeltaPerFrame)
            processor.Process(isTouch: true, isPress: false, rawX: 0, rawY: 2000, config: config, dt: 0.008f);
            var scrollUp = processor.Process(isTouch: true, isPress: false, rawX: 0, rawY: 5000, config: config, dt: 0.008f);
            Assert.True(scrollUp.WheelDelta > 0);
        }

        [Fact]
        public void TrackpadProcessor_Scrollwheel_MaxOneNotchPerFrameAndRespectsCooldown()
        {
            var processor = new TrackpadProcessor(isRightPad: false);
            var config = new TrackpadConfig
            {
                Mode = TrackpadMode.Scrollwheel,
                Sensitivity = 50,
                InvertScroll = true
            };

            // Toque inicial
            processor.Process(isTouch: true, isPress: false, rawX: 0, rawY: 0, config: config, dt: 0.008f);

            // Frame 1: acumula antes de atingir ScrollStep (4000f)
            processor.Process(isTouch: true, isPress: false, rawX: 0, rawY: 2500, config: config, dt: 0.008f);
            // Frame 2: ultrapassa ScrollStep (2500 + 3000 = 5500 >= 4000)
            var out1 = processor.Process(isTouch: true, isPress: false, rawX: 0, rawY: 5500, config: config, dt: 0.008f);

            // Deve emitir EXATAMENTE 1 notch (-120), nunca múltiplos notches em rajada
            Assert.Equal(-120, out1.WheelDelta);

            // Frame imediato seguinte (apenas 8ms após, enquanto cooldown de 80ms ainda está ativo):
            var outImmediate = processor.Process(isTouch: true, isPress: false, rawX: 0, rawY: 10500, config: config, dt: 0.008f);
            Assert.Equal(0, outImmediate.WheelDelta);

            // Após o cooldown expirar (avança 85ms no total e acumula outro deslocamento completo de ScrollStep):
            processor.Process(isTouch: true, isPress: false, rawX: 0, rawY: 13000, config: config, dt: 0.045f);
            var outAfterCooldown = processor.Process(isTouch: true, isPress: false, rawX: 0, rawY: 18000, config: config, dt: 0.045f);
            Assert.Equal(-120, outAfterCooldown.WheelDelta);
        }

        [Fact]
        public void TrackpadProcessor_Scrollwheel_CapacitiveLeap_IsDiscarded()
        {
            var processor = new TrackpadProcessor(isRightPad: false);
            var config = new TrackpadConfig
            {
                Mode = TrackpadMode.Scrollwheel,
                Sensitivity = 50
            };

            // Frame 1: contato inicial
            processor.Process(isTouch: true, isPress: false, rawX: 0, rawY: 0, config: config, dt: 0.008f);

            // Frame 2: salto anômalo impossível de 15.000 unidades em 8ms
            var leapOut = processor.Process(isTouch: true, isPress: false, rawX: 0, rawY: 15000, config: config, dt: 0.008f);

            // Deve ser rejeitado (0 notches)
            Assert.Equal(0, leapOut.WheelDelta);
        }

        [Fact]
        public void TrackpadProcessor_DPad_CentralDeadzoneFiltersRestingThumb()
        {
            var processor = new TrackpadProcessor(isRightPad: false);
            var config = new TrackpadConfig
            {
                Mode = TrackpadMode.DPad,
                DeadzonePercent = 15,
                Up = new List<InputAction> { InputAction.FromKey(VirtualKey.W) },
                Down = new List<InputAction> { InputAction.FromKey(VirtualKey.S) },
                Left = new List<InputAction> { InputAction.FromKey(VirtualKey.A) },
                Right = new List<InputAction> { InputAction.FromKey(VirtualKey.D) }
            };

            // Toque próximo ao centro (distância radial ~ 4% < 15%)
            var deadCenter = processor.Process(isTouch: true, isPress: false, rawX: 1000, rawY: 1000, config: config, dt: 0.008f);
            Assert.Empty(deadCenter.Actions);
        }

        [Fact]
        public void TrackpadProcessor_DPad_QuadrantsClassifyCorrectly()
        {
            var processor = new TrackpadProcessor(isRightPad: false);
            var config = new TrackpadConfig
            {
                Mode = TrackpadMode.DPad,
                DeadzonePercent = 15,
                Up = new List<InputAction> { InputAction.FromKey(VirtualKey.Up) },
                Down = new List<InputAction> { InputAction.FromKey(VirtualKey.Down) },
                Left = new List<InputAction> { InputAction.FromKey(VirtualKey.Left) },
                Right = new List<InputAction> { InputAction.FromKey(VirtualKey.Right) }
            };

            // Cima
            var upOut = processor.Process(isTouch: true, isPress: false, rawX: 0, rawY: 25000, config: config, dt: 0.008f);
            Assert.Equal(VirtualKey.Up, upOut.Actions[0].Key);
            Assert.True(upOut.TriggerHaptic, "Transição para novo quadrante deve disparar háptico");

            // Direita
            var rightOut = processor.Process(isTouch: true, isPress: false, rawX: 25000, rawY: 0, config: config, dt: 0.008f);
            Assert.Equal(VirtualKey.Right, rightOut.Actions[0].Key);

            // Baixo
            var downOut = processor.Process(isTouch: true, isPress: false, rawX: 0, rawY: -25000, config: config, dt: 0.008f);
            Assert.Equal(VirtualKey.Down, downOut.Actions[0].Key);

            // Esquerda
            var leftOut = processor.Process(isTouch: true, isPress: false, rawX: -25000, rawY: 0, config: config, dt: 0.008f);
            Assert.Equal(VirtualKey.Left, leftOut.Actions[0].Key);
        }

        [Fact]
        public void TrackpadProcessor_DualAction_MechanicalPressFiresPressActionsInAnyMode()
        {
            var processor = new TrackpadProcessor(isRightPad: true);
            var config = new TrackpadConfig
            {
                Mode = TrackpadMode.MouseTrackball,
                PressActions = new List<InputAction> { InputAction.FromMouseButton(MouseButton.Left) }
            };

            // Toque suave sem clique mecânico: sem ações de botão
            var touchOnly = processor.Process(isTouch: true, isPress: false, rawX: 1000, rawY: 1000, config: config, dt: 0.008f);
            Assert.Empty(touchOnly.Actions);

            // Afundar clique mecânico: dispara PressActions
            var pressOut = processor.Process(isTouch: true, isPress: true, rawX: 1000, rawY: 1000, config: config, dt: 0.008f);
            Assert.Single(pressOut.Actions);
            Assert.Equal(MouseButton.Left, pressOut.Actions[0].MouseButton);
        }

        [Fact]
        public void TrackpadProcessor_AnalogPressureClick_TriggersPressActionAndHapticWithoutMechanicalBottoming()
        {
            var processor = new TrackpadProcessor(isRightPad: true);
            var config = new TrackpadConfig
            {
                Mode = TrackpadMode.MouseTrackball,
                ClickPressureThreshold = 11000,
                HapticIntensity = TrackpadHapticIntensity.Low,
                PressActions = new List<InputAction> { InputAction.FromMouseButton(MouseButton.Left) }
            };

            // 1. Toque leve de navegação (pressão 4000 < 11000): sem clique
            var lightTouch = processor.Process(isTouch: true, isPress: false, rawX: 1000, rawY: 1000, rawPressure: 4000, config: config, dt: 0.008f);
            Assert.Empty(lightTouch.Actions);

            // 2. Aperto firme com o polegar (pressão 12500 >= 11000, chave mecânica NÃO afundada): DISPARA CLIQUE E HÁPTICO
            var pressureClick = processor.Process(isTouch: true, isPress: false, rawX: 1000, rawY: 1000, rawPressure: 12500, config: config, dt: 0.008f);
            Assert.Single(pressureClick.Actions);
            Assert.Equal(MouseButton.Left, pressureClick.Actions[0].MouseButton);
            Assert.True(pressureClick.TriggerHaptic, "Borda de subida do clique por pressão deve disparar pulso háptico tátil");

            // 3. Mantendo o polegar pressionado (pressão 10500, acima da histerese 9500): mantém o clique sem re-disparar pulso
            var sustainedPress = processor.Process(isTouch: true, isPress: false, rawX: 1000, rawY: 1000, rawPressure: 10500, config: config, dt: 0.008f);
            Assert.Single(sustainedPress.Actions);
            Assert.False(sustainedPress.TriggerHaptic, "Clique sustentado não deve ficar vibrando repetidamente");

            // 4. Aliviando a pressão (pressão 7000 < histerese 9500): solta o clique
            var releasePress = processor.Process(isTouch: true, isPress: false, rawX: 1000, rawY: 1000, rawPressure: 7000, config: config, dt: 0.008f);
            Assert.Empty(releasePress.Actions);
        }

        [Fact]
        public void TrackpadProcessor_ClickStabilization_DampensMovementDuringPressureClick()
        {
            var normalProc = new TrackpadProcessor(isRightPad: true);
            var clickingProc = new TrackpadProcessor(isRightPad: true);
            var config = new TrackpadConfig { Mode = TrackpadMode.MouseTrackball, Sensitivity = 50, ClickPressureThreshold = 11000 };

            // Frame 1: toque de âncora
            normalProc.Process(isTouch: true, isPress: false, rawX: 1000, rawY: 1000, rawPressure: 3000, config: config, dt: 0.008f);
            clickingProc.Process(isTouch: true, isPress: false, rawX: 1000, rawY: 1000, rawPressure: 3000, config: config, dt: 0.008f);

            // Frame 2: deslocamento de 80 unidades no polegar (deformação capacitiva de aperto).
            // Normal: toque leve sem aperto (pressão 3000)
            var normalOut = normalProc.Process(isTouch: true, isPress: false, rawX: 1080, rawY: 1000, rawPressure: 3000, config: config, dt: 0.008f);

            // Clicando: aperto firme (pressão 12000 >= limiar 11000). A âncora de clique trava em zero absoluto!
            var clickingOut = clickingProc.Process(isTouch: true, isPress: false, rawX: 1080, rawY: 1000, rawPressure: 12000, config: config, dt: 0.008f);

            Assert.True(normalOut.MouseDx > 0f);
            Assert.Equal(0f, clickingOut.MouseDx); // Travado pela âncora de clique (0 px)

            // Ao soltar o polegar após o clique, NÃO deve haver inércia residual ("estilingada")
            var releaseCoast = clickingProc.Process(isTouch: false, isPress: false, rawX: 1080, rawY: 1000, rawPressure: 0, config: config, dt: 0.016f);
            Assert.Equal(0f, releaseCoast.MouseDx);
            Assert.Equal(0f, releaseCoast.MouseDy);
        }

        [Fact]
        public void TrackpadProcessor_DoubleClickAnchor_KeepsZeroDisplacementAcrossTapAndReleaseWindow()
        {
            var proc = new TrackpadProcessor(isRightPad: true);
            var config = new TrackpadConfig
            {
                Mode = TrackpadMode.MouseTrackball,
                Sensitivity = 50,
                ClickPressureThreshold = 11000,
                PressActions = new List<InputAction> { InputAction.FromMouseButton(MouseButton.Left) }
            };

            // 1. Toque inicial em (1000, 1000)
            proc.Process(isTouch: true, isPress: false, rawX: 1000, rawY: 1000, rawPressure: 3000, config: config, dt: 0.008f);

            // 2. Primeiro Clique afundado: (1030, 1000), pressão 12000 -> arma âncora de duplo clique (350ms)
            var click1 = proc.Process(isTouch: true, isPress: false, rawX: 1030, rawY: 1000, rawPressure: 12000, config: config, dt: 0.008f);
            Assert.Equal(0f, click1.MouseDx);
            Assert.Single(click1.Actions); // Clique esquerdo disparado

            // 3. Alívio rápido do polegar entre os dois cliques (100ms depois)
            var releaseBetween = proc.Process(isTouch: false, isPress: false, rawX: 1030, rawY: 1000, rawPressure: 0, config: config, dt: 0.100f);
            Assert.Equal(0f, releaseBetween.MouseDx);
            Assert.Empty(releaseBetween.Actions);

            // 4. Segundo toque em posição ligeiramente deslocada pelo tremor (1050, 1000) - âncora ainda ativa!
            proc.Process(isTouch: true, isPress: false, rawX: 1050, rawY: 1000, rawPressure: 4000, config: config, dt: 0.008f);

            // 5. Segundo Clique afundado (1060, 1000), pressão 12500:
            var click2 = proc.Process(isTouch: true, isPress: false, rawX: 1060, rawY: 1000, rawPressure: 12500, config: config, dt: 0.008f);
            Assert.Equal(0f, click2.MouseDx); // Zero absoluto de deslocamento: garante duplo clique perfeito no Windows!
            Assert.Single(click2.Actions);
        }

        [Fact]
        public void TrackpadProcessor_DoubleClickAnchor_BreaksWhenDraggingBeyondThreshold()
        {
            var proc = new TrackpadProcessor(isRightPad: true);
            var config = new TrackpadConfig { Mode = TrackpadMode.MouseTrackball, Sensitivity = 50, ClickPressureThreshold = 11000 };

            // 1. Âncora inicial
            proc.Process(isTouch: true, isPress: false, rawX: 1000, rawY: 1000, rawPressure: 3000, config: config, dt: 0.008f);

            // 2. Clique afundado -> arma âncora
            proc.Process(isTouch: true, isPress: false, rawX: 1000, rawY: 1000, rawPressure: 12000, config: config, dt: 0.008f);

            // 3. Movimento inicial abaixo do limiar de quebra: travado em zero
            short underBreakDelta = (short)(TrackpadTuning.DoubleClickBreakDistance * 0.4f);
            var locked = proc.Process(isTouch: true, isPress: false, rawX: (short)(1000 + underBreakDelta), rawY: 1000, rawPressure: 12000, config: config, dt: 0.008f);
            Assert.Equal(0f, locked.MouseDx);

            // 4. Usuário continua arrastando firme além do limiar: quebra a âncora para Click & Drag!
            short overBreakDelta = (short)(TrackpadTuning.DoubleClickBreakDistance + 50f);
            var dragging = proc.Process(isTouch: true, isPress: false, rawX: (short)(1000 + overBreakDelta), rawY: 1000, rawPressure: 12000, config: config, dt: 0.008f);
            Assert.True(dragging.MouseDx > 0f, "Deve quebrar a âncora e arrastar quando passar do limiar de quebra");
        }

        [Fact]
        public void TrackpadProcessor_TrackballInertia_RequiresMinimumStrokeDistance()
        {
            var processor = new TrackpadProcessor(isRightPad: true);
            var config = new TrackpadConfig { Mode = TrackpadMode.MouseTrackball, Sensitivity = 50, Friction = 50 };

            // Toque inicial
            processor.Process(isTouch: true, isPress: false, rawX: 0, rawY: 0, config: config, dt: 0.008f);

            // Movimento rápido em distância curta (stroke de 200 unidades < 700f MinStrokeDistanceForInertia)
            processor.Process(isTouch: true, isPress: false, rawX: 200, rawY: 0, config: config, dt: 0.004f);

            // Solta o dedo: mesmo tendo alta velocidade instantânea (200 / 0.004 = 50.000 u/s),
            // a inércia NÃO deve ser armada porque não houve trajeto de varredura (stroke) contínuo!
            var coast = processor.Process(isTouch: false, isPress: false, rawX: 200, rawY: 0, config: config, dt: 0.016f);
            Assert.Equal(0f, coast.MouseDx);
            Assert.Equal(0f, coast.MouseDy);
        }

        [Fact]
        public void TrackpadProcessor_CapacitiveLeap_IsDiscardedWhenDeltaExceedsThreshold()
        {
            var processor = new TrackpadProcessor(isRightPad: true);
            var config = new TrackpadConfig { Mode = TrackpadMode.MouseTrackball, Sensitivity = 50 };

            // Frame 1: Toque inicial em (1000, 1000)
            processor.Process(isTouch: true, isPress: false, rawX: 1000, rawY: 1000, config: config, dt: 0.008f);

            // Frame 2: Salto capacitivo anômalo impossível (teletransporte de +8000 unidades)
            var leapOut = processor.Process(isTouch: true, isPress: false, rawX: 9000, rawY: 1000, config: config, dt: 0.008f);

            // O salto deve ser rejeitado (0 px de deslocamento emitido para o cursor)
            Assert.Equal(0f, leapOut.MouseDx);
            Assert.Equal(0f, leapOut.MouseDy);
        }

        [Fact]
        public void TrackpadProcessor_ShortTap_NeverArmsInertiaEvenWithHighInstantSpeed()
        {
            var processor = new TrackpadProcessor(isRightPad: true);
            var config = new TrackpadConfig { Mode = TrackpadMode.MouseTrackball, Sensitivity = 50, Friction = 50 };

            // Toquinho com deformação de polpa (stroke de 800 unidades < MinStrokeDistanceForInertia = 1600f)
            // Frame 1: contato inicial
            processor.Process(isTouch: true, isPress: false, rawX: 1000, rawY: 1000, config: config, dt: 0.008f);
            // Frame 2: movimento rápido da polpa
            processor.Process(isTouch: true, isPress: false, rawX: 1800, rawY: 1000, config: config, dt: 0.008f);

            // Frame 3: soltura imediata (toquinho)
            var coast = processor.Process(isTouch: false, isPress: false, rawX: 1800, rawY: 1000, config: config, dt: 0.016f);

            // A inércia (estilingue) NÃO deve ser armada por causa do trajeto insuficiente para um flick intencional
            Assert.Equal(0f, coast.MouseDx);
            Assert.Equal(0f, coast.MouseDy);
        }

        [Fact]
        public void TrackpadProcessor_StopInDeadzone_CancelsInertiaBeforeRelease()
        {
            var processor = new TrackpadProcessor(isRightPad: true);
            var config = new TrackpadConfig { Mode = TrackpadMode.MouseTrackball, Sensitivity = 50, Friction = 50 };

            // Swipe longo e rápido (acumula stroke e velocidade)
            processor.Process(isTouch: true, isPress: false, rawX: 0, rawY: 0, config: config, dt: 0.008f);
            processor.Process(isTouch: true, isPress: false, rawX: 3000, rawY: 0, config: config, dt: 0.008f);

            // Usuário para o polegar sobre o alvo (deadzone <= 10f) antes de soltar
            processor.Process(isTouch: true, isPress: false, rawX: 3002, rawY: 0, config: config, dt: 0.008f);

            // Solta o dedo: NÃO deve haver inércia pois o movimento parou antes da soltura
            var coast = processor.Process(isTouch: false, isPress: false, rawX: 3002, rawY: 0, config: config, dt: 0.016f);
            Assert.Equal(0f, coast.MouseDx);
            Assert.Equal(0f, coast.MouseDy);
        }

        #endregion

        #region 4. Integração com GamingProfile e DesktopProfile

        [Fact]
        public void GamingProfile_WithSteamDeckRawState_ProcessesMouseMovementAndHaptics()
        {
            var sim = new TestInputSimulator();
            var haptics = new TestHapticsProvider();
            var settings = new AppSettings { MappingEnabled = true };
            var profile = GameProfilePresets.CreateDesktopDefault();

            var gamingProfile = new GamingProfile(sim, settings, profile, haptics);

            var deckRaw1 = new SteamDeckRawState
            {
                Buttons1 = SDCButton1.BTN_RPAD_TOUCH,
                RPadX = 0,
                RPadY = 0
            };
            var gamepadState = new GamepadState(true, 1, GamepadButtons.None, 0, 0, 0, 0, 0, 0);

            // Frame 1: inicialização de toque
            gamingProfile.Update(gamepadState, deckRaw1, 0.008f);

            // Frame 2: movimento significativo no trackpad direito (+2000 X, +2000 Y)
            var deckRaw2 = new SteamDeckRawState
            {
                Buttons1 = SDCButton1.BTN_RPAD_TOUCH,
                RPadX = 2000,
                RPadY = 2000
            };
            gamingProfile.Update(gamepadState, deckRaw2, 0.008f);

            // Verifica se o simulador recebeu movimento de mouse
            var mouseMoves = sim.Events.FindAll(e => e.Type == TestInputSimulator.EventType.MouseMove);
            Assert.NotEmpty(mouseMoves);
            Assert.True(mouseMoves[0].Dx > 0, "Dx deve ser positivo");
            Assert.True(mouseMoves[0].Dy < 0, "Dy deve ser negativo (movimento para cima na tela)");
        }

        [Fact]
        public void GamingProfile_SlowSubpixelAccumulation_PreservesRemainderAcrossStationaryFrames()
        {
            var sim = new TestInputSimulator();
            var settings = new AppSettings { MappingEnabled = true };
            var profile = GameProfilePresets.CreateDesktopDefault();
            var gamingProfile = new GamingProfile(sim, settings, profile);
            var gamepadState = new GamepadState(true, 1, GamepadButtons.None, 0, 0, 0, 0, 0, 0);

            // Toque inicial
            gamingProfile.Update(gamepadState, new SteamDeckRawState { Buttons1 = SDCButton1.BTN_RPAD_TOUCH, RPadX = 0, RPadY = 0 }, 0.008f);

            // Movimento lento que gera sub-pixel fracionário (< 1 pixel, sem emissão imediata de MouseMove)
            gamingProfile.Update(gamepadState, new SteamDeckRawState { Buttons1 = SDCButton1.BTN_RPAD_TOUCH, RPadX = 80, RPadY = 0 }, 0.008f);

            // Frame estacionário (delta = 0): NÃO deve limpar o acumulador sub-pixel!
            gamingProfile.Update(gamepadState, new SteamDeckRawState { Buttons1 = SDCButton1.BTN_RPAD_TOUCH, RPadX = 80, RPadY = 0 }, 0.008f);

            // Segundo micro-movimento que, somado ao acumulador residual preservado, cruza >= 1 pixel
            gamingProfile.Update(gamepadState, new SteamDeckRawState { Buttons1 = SDCButton1.BTN_RPAD_TOUCH, RPadX = 450, RPadY = 0 }, 0.008f);

            var mouseMoves = sim.Events.FindAll(e => e.Type == TestInputSimulator.EventType.MouseMove);
            Assert.NotEmpty(mouseMoves);
            Assert.True(mouseMoves[0].Dx >= 1, "Acumulador de subpixel preservado deve emitir pelo menos 1 pixel");
        }

        [Fact]
        public void DesktopProfile_LeftTrackpad_ScrollsAndEmitsHapticPulses()
        {
            var sim = new TestInputSimulator();
            var haptics = new TestHapticsProvider();
            var settings = new AppSettings { MappingEnabled = true, DesktopModeEnabled = true };
            DesktopProfile.ForegroundCheckOverride = () => false; // Companion em background

            var desktopProfile = new DesktopProfile(sim, settings, GameProfilePresets.CreateDesktopDefault(), haptics);

            var gamepadState = new GamepadState(true, 1, GamepadButtons.None, 0, 0, 0, 0, 0, 0);

            // Frame 1: toque no trackpad esquerdo
            var deckRaw1 = new SteamDeckRawState
            {
                Buttons1 = SDCButton1.BTN_LPAD_TOUCH,
                LPadX = 0,
                LPadY = 0
            };
            desktopProfile.Update(gamepadState, deckRaw1, 0.008f);

            // Frame 2 e 3: swipe vertical contínuo no trackpad esquerdo (< MaxPhysicalDeltaPerFrame)
            var deckRaw2 = new SteamDeckRawState
            {
                Buttons1 = SDCButton1.BTN_LPAD_TOUCH,
                LPadX = 0,
                LPadY = 4500
            };
            desktopProfile.Update(gamepadState, deckRaw2, 0.008f);

            var deckRaw3 = new SteamDeckRawState
            {
                Buttons1 = SDCButton1.BTN_LPAD_TOUCH,
                LPadX = 0,
                LPadY = 9000
            };
            desktopProfile.Update(gamepadState, deckRaw3, 0.008f);

            // Verifica emissão de MouseWheel
            var wheels = sim.Events.FindAll(e => e.Type == TestInputSimulator.EventType.MouseWheel);
            Assert.NotEmpty(wheels);
            Assert.True(wheels[0].WheelDelta < 0, "Rolagem natural deve emitir WheelDelta negativo ao deslizar p/ cima");

            // Verifica emissão de pulso háptico
            Assert.NotEmpty(haptics.Pulses);
            Assert.False(haptics.Pulses[0].isRightPad, "Haptic deve ser disparado no trackpad esquerdo");
            Assert.Equal(TrackpadHapticIntensity.High, haptics.Pulses[0].intensity);

            DesktopProfile.ForegroundCheckOverride = null;
        }

        [Fact]
        public void DesktopProfile_RightTrackpadPress_FiresLeftClick()
        {
            var sim = new TestInputSimulator();
            var settings = new AppSettings { MappingEnabled = true, DesktopModeEnabled = true };
            DesktopProfile.ForegroundCheckOverride = () => false;

            var desktopProfile = new DesktopProfile(sim, settings, GameProfilePresets.CreateDesktopDefault());
            var gamepadState = new GamepadState(true, 1, GamepadButtons.None, 0, 0, 0, 0, 0, 0);

            // Clique mecânico no trackpad direito
            var deckRaw = new SteamDeckRawState
            {
                Buttons1 = SDCButton1.BTN_RPAD_TOUCH | SDCButton1.BTN_RPAD_PRESS,
                RPadX = 500,
                RPadY = 500
            };

            desktopProfile.Update(gamepadState, deckRaw, 0.008f);

            var clicks = sim.Events.FindAll(e => e.Type == TestInputSimulator.EventType.MouseDown && e.Button == MouseButton.Left);
            Assert.NotEmpty(clicks);

            DesktopProfile.ForegroundCheckOverride = null;
        }

        [Fact]
        public void DesktopProfile_RightTrackpadAnalogPressure_FiresLeftClickWithoutMechanicalPress()
        {
            var sim = new TestInputSimulator();
            var haptics = new TestHapticsProvider();
            var settings = new AppSettings { MappingEnabled = true, DesktopModeEnabled = true };
            DesktopProfile.ForegroundCheckOverride = () => false;

            var desktopProfile = new DesktopProfile(sim, settings, GameProfilePresets.CreateDesktopDefault(), haptics);
            var gamepadState = new GamepadState(true, 1, GamepadButtons.None, 0, 0, 0, 0, 0, 0);

            // Toque com pressão analógica acima de 11000, mas SEM afundar chave mecânica física (Buttons1 tem só BTN_RPAD_TOUCH)
            var deckRaw = new SteamDeckRawState
            {
                Buttons1 = SDCButton1.BTN_RPAD_TOUCH,
                RPadX = 500,
                RPadY = 500,
                RPadPressure = 13000
            };

            desktopProfile.Update(gamepadState, deckRaw, 0.008f);

            var clicks = sim.Events.FindAll(e => e.Type == TestInputSimulator.EventType.MouseDown && e.Button == MouseButton.Left);
            Assert.NotEmpty(clicks);

            // Verifica que o pulso háptico tátil de clique foi despachado
            Assert.NotEmpty(haptics.Pulses);
            Assert.True(haptics.Pulses[0].isRightPad, "Pulso háptico de clique deve ser no trackpad direito");

            DesktopProfile.ForegroundCheckOverride = null;
        }

        [Fact]
        public void DesktopProfile_WhenCompanionInForeground_OperatesTrackpadNavigationAndMutesGamepadButtons()
        {
            var sim = new TestInputSimulator();
            var haptics = new TestHapticsProvider();
            var settings = new AppSettings { MappingEnabled = true, DesktopModeEnabled = true };
            DesktopProfile.ForegroundCheckOverride = () => true; // Companion em foreground

            var desktopProfile = new DesktopProfile(sim, settings, GameProfilePresets.CreateDesktopDefault(), haptics);

            // Controle com Botão A pressionado E movimento no trackpad direito (+2000 X, +2000 Y)
            var gamepadState = new GamepadState(true, 1, GamepadButtons.A, 0, 0, 0, 0, 0, 0);

            // Frame 1: toque
            desktopProfile.Update(gamepadState, new SteamDeckRawState
            {
                Buttons1 = SDCButton1.BTN_RPAD_TOUCH,
                RPadX = 0,
                RPadY = 0
            }, 0.008f);

            // Frame 2: movimento no trackpad
            desktopProfile.Update(gamepadState, new SteamDeckRawState
            {
                Buttons1 = SDCButton1.BTN_RPAD_TOUCH,
                RPadX = 2000,
                RPadY = 2000
            }, 0.008f);

            // 1. Assegura que o Botão A do gamepad NÃO gerou tecla de teclado (Enter), pois os botões
            // pertencem à navegação nativa por controle da UI (GamepadNavigationManager)
            var keyEvents = sim.Events.FindAll(e => e.Type == TestInputSimulator.EventType.KeyDown && e.Key == VirtualKey.Enter);
            Assert.Empty(keyEvents);

            // 2. Assegura que o Trackpad continuou operando livremente o cursor do mouse na UI do Companion!
            var mouseMoves = sim.Events.FindAll(e => e.Type == TestInputSimulator.EventType.MouseMove);
            Assert.NotEmpty(mouseMoves);
            Assert.True(mouseMoves[0].Dx > 0);
            Assert.True(mouseMoves[0].Dy < 0);

            DesktopProfile.ForegroundCheckOverride = null;
        }

        [Fact]
        public void ConfigRepository_LoadDesktopProfile_WhenOldIniLacksTrackpads_AutoMigratesAndEnablesThem()
        {
            string tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "test_old_config_" + Guid.NewGuid() + ".ini");
            try
            {
                // Cria um config.ini legado que não tem Profile_LPad ou Profile_RPad
                var ini = new IniFile();
                ini.SetValue("Settings", "DesktopModeEnabled", "True");
                ini.SetValue("DesktopProfile", "Profile_Name", "Modo Desktop (Navegação Windows)");
                ini.SetValue("DesktopProfile", "Profile_LStick_Mode", "MouseMove");
                ini.SetValue("DesktopProfile", "Profile_RStick_Mode", "MouseMove");
                ini.Save(tempFile);

                var repo = new ConfigRepository(tempFile);
                var loaded = repo.LoadDesktopProfile();

                // Deve auto-migrar e ativar o trackpad direito como mouse e esquerdo como scroll
                Assert.NotNull(loaded);
                Assert.Equal(TrackpadMode.Scrollwheel, loaded.ActivePage.LeftTrackpad.Mode);
                Assert.Equal(TrackpadMode.MouseTrackball, loaded.ActivePage.RightTrackpad.Mode);
                Assert.Single(loaded.ActivePage.RightTrackpad.PressActions);
                Assert.Equal(MouseButton.Left, loaded.ActivePage.RightTrackpad.PressActions[0].MouseButton);

                // E deve ter persistido no arquivo!
                var iniAfter = new IniFile();
                iniAfter.Load(tempFile);
                Assert.Equal("Scrollwheel", iniAfter.GetValue("DesktopProfile", "Profile_LPad_Mode"));
                Assert.Equal("MouseTrackball", iniAfter.GetValue("DesktopProfile", "Profile_RPad_Mode"));
                Assert.Equal("Mouse:Left", iniAfter.GetValue("DesktopProfile", "Profile_RPad_Press"));
            }
            finally
            {
                if (System.IO.File.Exists(tempFile))
                {
                    try { System.IO.File.Delete(tempFile); } catch { }
                }
            }
        }

        #endregion
    }
}
