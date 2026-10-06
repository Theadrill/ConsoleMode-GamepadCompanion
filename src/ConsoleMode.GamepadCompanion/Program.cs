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

            var settings = new AppSettings();
            var input = new InputSimulator();
            var gamingProfile = new Profiles.GamingProfile(input, settings);

            using (IGamepadService gamepad = new GamepadService())
            using (new ProfileEngine(gamepad, gamingProfile))
            {
                gamepad.SelectedSlot = settings.SelectedSlot;
                gamepad.Start();
                Application.Run(new SettingsForm(gamepad, settings));
            }
        }
    }
}
