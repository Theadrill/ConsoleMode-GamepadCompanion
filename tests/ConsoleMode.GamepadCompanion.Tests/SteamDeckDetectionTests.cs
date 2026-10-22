using System;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.Hardware;
using Xunit;

namespace ConsoleMode.GamepadCompanion.Tests
{
    public class SteamDeckDetectionTests
    {
        [Theory]
        [InlineData("Valve", "Jupiter", SteamDeckVariant.SteamDeckLcd)]
        [InlineData("VALVE", "jupiter", SteamDeckVariant.SteamDeckLcd)]
        [InlineData("Valve", "Galileo", SteamDeckVariant.SteamDeckOled)]
        [InlineData("Valve Corporation", "Galileo OLED", SteamDeckVariant.SteamDeckOled)]
        [InlineData("Valve", "Aerith", SteamDeckVariant.SteamDeckLcd)]
        [InlineData("Dell Inc.", "XPS 15 9500", SteamDeckVariant.None)]
        [InlineData("ASUSTeK COMPUTER INC.", "ROG Ally RC71L", SteamDeckVariant.None)]
        [InlineData("Lenovo", "Legion Go 8APU1", SteamDeckVariant.None)]
        [InlineData("", "", SteamDeckVariant.None)]
        [InlineData(null, null, SteamDeckVariant.None)]
        public void DetectVariant_ShouldIdentifyHardwareCorrectly(string manufacturer, string product, SteamDeckVariant expected)
        {
            var result = SteamDeckDetector.DetectVariant(manufacturer, product);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void SteamDeckInfo_ShouldReportIsSteamDeckProperly()
        {
            var lcdInfo = new SteamDeckInfo(SteamDeckVariant.SteamDeckLcd, "Valve", "Jupiter");
            Assert.True(lcdInfo.IsSteamDeck);
            Assert.Contains("Jupiter", lcdInfo.DisplayName);

            var oledInfo = new SteamDeckInfo(SteamDeckVariant.SteamDeckOled, "Valve", "Galileo");
            Assert.True(oledInfo.IsSteamDeck);
            Assert.Contains("Galileo", oledInfo.DisplayName);

            var pcInfo = SteamDeckInfo.CreateStandardPc("Microsoft Corporation", "Surface Pro");
            Assert.False(pcInfo.IsSteamDeck);
            Assert.Equal("Computador Padrão (PC)", pcInfo.DisplayName);
        }

        [Fact]
        public void SteamDeckDetector_OverrideAndReset_ShouldWorkProperly()
        {
            try
            {
                var custom = new SteamDeckInfo(SteamDeckVariant.SteamDeckOled, "Valve", "Galileo");
                SteamDeckDetector.SetOverride(custom);

                Assert.True(SteamDeckDetector.IsSteamDeck);
                Assert.Equal(SteamDeckVariant.SteamDeckOled, SteamDeckDetector.Current.Variant);

                SteamDeckDetector.ResetCache();
                // Após o reset, o detector lê os dados do sistema atual
                Assert.NotNull(SteamDeckDetector.Current);
            }
            finally
            {
                SteamDeckDetector.ResetCache();
            }
        }

        [Fact]
        public void ViGEmBusService_StatusDiagnostics_ShouldBeConsistent()
        {
            var service = ViGEmBusService.Instance;
            Assert.NotNull(service);

            var status = service.CheckStatus();
            Assert.True(status == ViGEmBusStatus.NotInstalled || status == ViGEmBusStatus.Active || status == ViGEmBusStatus.ServiceStopped);

            if (status == ViGEmBusStatus.Active)
            {
                Assert.True(service.IsDriverInstalledAndActive);
            }
            else
            {
                Assert.False(service.IsDriverInstalledAndActive);
            }
        }

        [Fact]
        public void ViGEmBusInstaller_ResolveInstallerPath_ShouldLocateOrReturnNull()
        {
            string path = ViGEmBusInstaller.ResolveInstallerPath();
            if (path != null)
            {
                Assert.True(System.IO.File.Exists(path));
                Assert.Contains("ViGEmBus_Setup.exe", path);
            }
        }

        [Fact]
        public void SteamDeckSetupControl_InstantiationAndRefresh_ShouldNotThrow()
        {
            using (var control = new UI.Controls.SteamDeckSetupControl())
            {
                control.Size = new System.Drawing.Size(1000, 600);
                control.RefreshState();

                Assert.True(control.Controls.Count > 0);
            }
        }

        [Fact]
        public void SteamDeckRawState_Parse_EmptyOrNullBuffer_ShouldReturnDefault()
        {
            var nullState = SteamDeckRawState.Parse(null);
            Assert.False(nullState.IsA);
            Assert.False(nullState.IsL4);

            var shortState = SteamDeckRawState.Parse(new byte[32]);
            Assert.False(shortState.IsA);
            Assert.False(shortState.IsR5);
        }

        [Fact]
        public void SteamDeckRawState_Parse_And_ToGamepadState_ShouldMapAccurately()
        {
            byte[] buffer = new byte[64];

            // 0x08 Buttons0: A, DPadUp, L1, L5
            ushort buttons0 = (ushort)(SDCButton0.BTN_A | SDCButton0.BTN_DPAD_UP | SDCButton0.BTN_L1 | SDCButton0.BTN_L5);
            Array.Copy(BitConverter.GetBytes(buttons0), 0, buffer, 8, 2);

            // 0x0A Buttons1: LStickPress (L3), R5
            buffer[10] = (byte)(SDCButton1.BTN_LSTICK_PRESS | SDCButton1.BTN_R5);

            // 0x0B Buttons2: RStickPress (R3)
            buffer[11] = (byte)SDCButton2.BTN_RSTICK_PRESS;

            // 0x0D Buttons4: L4, R4
            buffer[13] = (byte)(SDCButton4.BTN_L4 | SDCButton4.BTN_R4);

            // Gatilhos: LT = 16384 (50% = ~128), RT = 32767 (100% = ~255)
            Array.Copy(BitConverter.GetBytes((short)16384), 0, buffer, 0x2C, 2);
            Array.Copy(BitConverter.GetBytes((short)32767), 0, buffer, 0x2E, 2);

            // Analógicos: LeftX = 12000, LeftY = -15000
            Array.Copy(BitConverter.GetBytes((short)12000), 0, buffer, 0x30, 2);
            Array.Copy(BitConverter.GetBytes((short)-15000), 0, buffer, 0x32, 2);

            var raw = SteamDeckRawState.Parse(buffer);

            // Validação dos botões físicos
            Assert.True(raw.IsA);
            Assert.True(raw.IsDPadUp);
            Assert.True(raw.IsL1);
            Assert.True(raw.IsL3);
            Assert.True(raw.IsR3);
            Assert.True(raw.IsL4);
            Assert.True(raw.IsL5);
            Assert.True(raw.IsR4);
            Assert.True(raw.IsR5);
            Assert.False(raw.IsB);
            Assert.False(raw.IsX);

            // Conversão para GamepadState padrão
            var state = raw.ToGamepadState(10);
            Assert.True(state.IsConnected);
            Assert.Equal((uint)10, state.PacketNumber);
            Assert.True(state.IsPressed(GamepadButtons.A));
            Assert.True(state.IsPressed(GamepadButtons.DPadUp));
            Assert.True(state.IsPressed(GamepadButtons.LeftShoulder));
            Assert.True(state.IsPressed(GamepadButtons.LeftStick));
            Assert.True(state.IsPressed(GamepadButtons.RightStick));
            Assert.False(state.IsPressed(GamepadButtons.B));

            Assert.InRange(state.LeftTrigger, (byte)125, (byte)130);
            Assert.Equal((byte)255, state.RightTrigger);

            Assert.Equal((short)12000, state.LeftThumbX);
            Assert.Equal((short)-15000, state.LeftThumbY);
        }

        [Fact]
        public void SteamDeckManager_Stop_ShouldBeSafeWhenNotStarted()
        {
            SteamDeckManager.Stop();
            Assert.False(SteamDeckManager.IsActive);
        }
    }
}
