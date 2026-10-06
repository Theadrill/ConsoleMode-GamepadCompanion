using System;
using System.Windows.Forms;
using ConsoleMode.GamepadCompanion.Core.Interfaces;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.Engine;
using ConsoleMode.GamepadCompanion.Hardware;
using ConsoleMode.GamepadCompanion.UI;

namespace ConsoleMode.GamepadCompanion
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (var singleInstanceGuard = new SingleInstanceGuard())
            {
                if (!singleInstanceGuard.EnsureSingleInstance())
                {
                    return;
                }

                var configRepo = new ConfigRepository();
                var settings = configRepo.Load();

                var input = new InputSimulator();
                var gamingProfile = new Profiles.GamingProfile(input, settings);
                var desktopProfile = new Profiles.DesktopProfile();

                var windowTracker = new WindowTracker();

                using (IGamepadService gamepad = new GamepadService())
                {
                    gamepad.SelectedSlot = settings.SelectedSlot;
                    gamepad.Start();

                    using (var profileEngine = new ProfileEngine(gamepad, desktopProfile))
                    using (var profileManager = new ProfileManager(profileEngine, gamingProfile, desktopProfile, windowTracker, gamepad))
                    using (var trayContext = new TrayAppContext(gamepad, settings, profileManager, configRepo, windowTracker))
                    {
                        Application.Run(trayContext);
                    }
                }
            }
        }
    }
}
