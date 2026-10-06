using System;
using System.Threading;
using ConsoleMode.GamepadCompanion.Core.Interfaces;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.Hardware;

namespace ConsoleMode.GamepadCompanion
{
    /// <summary>Fase 1: diagnóstico em console da leitura do XInput.</summary>
    internal static class Program
    {
        private static int Main()
        {
            Console.WriteLine("ConsoleMode-GamepadCompanion - Fase 1 (diagnostico XInput)");
            Console.WriteLine("Ctrl+C para sair.\n");

            using (IGamepadService gamepad = new GamepadService())
            {
                gamepad.ConnectionChanged += (slot, connected) =>
                    Console.WriteLine($"[slot {slot}] {(connected ? "CONECTADO" : "DESCONECTADO")}");

                gamepad.Start();

                GamepadState last = GamepadState.Disconnected;
                while (true)
                {
                    GamepadState s = gamepad.CurrentState;
                    if (s.IsConnected && s.PacketNumber != last.PacketNumber)
                    {
                        Console.Write(
                            $"\rBtn={s.Buttons,-40} LT={s.LeftTrigger,3} RT={s.RightTrigger,3} " +
                            $"L=({s.LeftThumbX,6},{s.LeftThumbY,6}) R=({s.RightThumbX,6},{s.RightThumbY,6})   ");
                    }
                    last = s;
                    Thread.Sleep(16);
                }
            }
        }
    }
}
