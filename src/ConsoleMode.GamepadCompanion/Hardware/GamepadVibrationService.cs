using System;
using System.Threading.Tasks;

namespace ConsoleMode.GamepadCompanion.Hardware
{
    /// <summary>
    /// Serviço de feedback háptico sutil via XInput para digitação no Teclado Virtual.
    /// </summary>
    public sealed class GamepadVibrationService
    {
        private static readonly Lazy<GamepadVibrationService> _instance =
            new Lazy<GamepadVibrationService>(() => new GamepadVibrationService());

        public static GamepadVibrationService Instance => _instance.Value;

        public void Vibrate(int slot, ushort leftSpeed, ushort rightSpeed, int durationMs)
        {
            if (slot < 0) slot = 0;
            if (slot > 3) return;

            Task.Run(async () =>
            {
                try
                {
                    var on = new XInputNative.XInputVibration
                    {
                        wLeftMotorSpeed = leftSpeed,
                        wRightMotorSpeed = rightSpeed
                    };
                    XInputNative.SetState((uint)slot, ref on);

                    await Task.Delay(durationMs).ConfigureAwait(false);

                    var off = new XInputNative.XInputVibration
                    {
                        wLeftMotorSpeed = 0,
                        wRightMotorSpeed = 0
                    };
                    XInputNative.SetState((uint)slot, ref off);
                }
                catch
                {
                    // Fallback silencioso em ambientes sem controle físico/XInput
                }
            });
        }

        public void Pulse(int slot = 0, int durationMs = 35)
        {
            // Vibração tátil sutil: motor de alta frequência (right motor) a 20%
            Vibrate(slot, 0, 14000, durationMs);
        }
    }
}
