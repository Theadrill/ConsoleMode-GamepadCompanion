using System;
using System.IO;
using System.Text;
using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.Hardware
{
    /// <summary>
    /// Rastreamento opt-in de baixo custo para diagnosticar crosstalk de haptics.
    /// Ative definindo a variável de ambiente COMPANION_HAPTIC_TRACE=1 antes de iniciar o app.
    /// O log é gravado em %TEMP%\ConsoleMode-Haptics.log.
    /// </summary>
    internal static class HapticTrace
    {
        private static readonly bool Enabled =
            string.Equals(Environment.GetEnvironmentVariable("COMPANION_HAPTIC_TRACE"), "1", StringComparison.OrdinalIgnoreCase);

        private static readonly object Sync = new object();
        private static readonly string LogPath =
            Path.Combine(Path.GetTempPath(), "ConsoleMode-Haptics.log");

        private static bool _lastLeftTouch;
        private static bool _lastRightTouch;
        private static bool _hasTouchBaseline;

        public static void Touch(SteamDeckRawState raw)
        {
            if (!Enabled) return;

            bool left = raw.IsLPadTouch;
            bool right = raw.IsRPadTouch;

            if (_hasTouchBaseline && left == _lastLeftTouch && right == _lastRightTouch)
                return;

            _hasTouchBaseline = true;
            _lastLeftTouch = left;
            _lastRightTouch = right;

            Write(string.Format(
                "TOUCH L={0} R={1} B1=0x{2:X2} LPress={3} RPress={4}",
                left ? 1 : 0,
                right ? 1 : 0,
                (byte)raw.Buttons1,
                raw.IsLPadPress ? 1 : 0,
                raw.IsRPadPress ? 1 : 0));
        }

        public static void Haptic(bool isRightPad, string intensity, bool isClick, byte position, byte cmd, sbyte dBgain, int result)
        {
            if (!Enabled) return;

            Write(string.Format(
                "HAPTIC pad={0} int={1} click={2} pos=0x{3:X2} cmd={4} gain={5} result={6}",
                isRightPad ? "R" : "L",
                intensity,
                isClick ? 1 : 0,
                position,
                cmd,
                dBgain,
                result));
        }

        public static void Info(string message)
        {
            if (!Enabled) return;
            Write(message);
        }

        private static void Write(string message)
        {
            try
            {
                lock (Sync)
                {
                    File.AppendAllText(
                        LogPath,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message + Environment.NewLine,
                        Encoding.UTF8);
                }
            }
            catch
            {
                // Diagnóstico nunca deve derrubar o app.
            }
        }
    }
}
