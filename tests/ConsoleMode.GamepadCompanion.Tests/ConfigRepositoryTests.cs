using System.IO;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.Hardware;
using Xunit;

namespace ConsoleMode.GamepadCompanion.Tests
{
    public class ConfigRepositoryTests
    {
        [Fact]
        public void Load_NonExistentFile_CreatesDefaultAndSaves()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "companion_test_" + System.Guid.NewGuid().ToString("N") + ".ini");
            try
            {
                var repo = new ConfigRepository(tempFile);
                var settings = repo.Load();

                Assert.True(File.Exists(tempFile));
                Assert.Equal(50, settings.MouseSensitivity);
                Assert.Equal(24, settings.StickDeadzonePercent);
                Assert.Equal(20, settings.TriggerThresholdPercent);
                Assert.Equal(-1, settings.SelectedSlot);
                Assert.True(settings.MappingEnabled);
                Assert.Equal(string.Empty, settings.SteamGridDbApiKey);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void SaveAndLoad_PersistsCustomValues()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "companion_test_" + System.Guid.NewGuid().ToString("N") + ".ini");
            try
            {
                var repo = new ConfigRepository(tempFile);
                var toSave = new AppSettings
                {
                    SelectedSlot = 1,
                    MouseSensitivity = 75,
                    StickDeadzonePercent = 15,
                    TriggerThresholdPercent = 35,
                    MappingEnabled = false,
                    SteamGridDbApiKey = "test_api_key_12345"
                };

                repo.Save(toSave);

                var loaded = repo.Load();
                Assert.Equal(1, loaded.SelectedSlot);
                Assert.Equal(75, loaded.MouseSensitivity);
                Assert.Equal(15, loaded.StickDeadzonePercent);
                Assert.Equal(35, loaded.TriggerThresholdPercent);
                Assert.False(loaded.MappingEnabled);
                Assert.Equal("test_api_key_12345", loaded.SteamGridDbApiKey);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }
    }
}
