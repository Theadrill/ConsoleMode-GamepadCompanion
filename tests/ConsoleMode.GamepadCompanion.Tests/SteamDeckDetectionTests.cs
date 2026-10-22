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
            // No estado atual da máquina de teste (driver desinstalado), o status deve ser NotInstalled
            // ou retornar um status conhecido sem disparar exceção não tratada
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
    }
}
