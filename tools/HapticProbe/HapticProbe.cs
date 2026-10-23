using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using hidapi;

namespace HapticProbe
{
    internal static class Program
    {
        private const ushort Vid = 0x28DE;
        private const ushort Pid = 0x1205;

        [DllImport("hidapi.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int hid_send_feature_report(IntPtr device, byte[] data, UIntPtr length);

        private static IntPtr _handle;
        private static readonly string ResultsPath =
            Path.Combine(Path.GetTempPath(), "HapticProbe-results.txt");

        private static void Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("=== Haptic Probe - Steam Deck (0x28DE:0x1205) ===");
            Console.WriteLine("Feche completamente o Companion antes de continuar.");
            Console.WriteLine("Resultados serão gravados em: " + ResultsPath);
            Console.WriteLine();

            var device = new HidDevice(Vid, Pid, 64);
            if (!device.OpenDevice())
            {
                Console.WriteLine("Falha ao abrir o dispositivo HID. O Companion ainda está aberto?");
                Console.WriteLine("Pressione qualquer tecla para sair.");
                Console.ReadKey(true);
                return;
            }

            var field = typeof(HidDevice).GetField("_deviceHandle", BindingFlags.Instance | BindingFlags.NonPublic);
            _handle = field == null ? IntPtr.Zero : (IntPtr)field.GetValue(device);
            if (_handle == IntPtr.Zero)
            {
                Console.WriteLine("Handle HID nativo indisponível.");
                Console.ReadKey(true);
                return;
            }

            File.WriteAllText(ResultsPath, "Haptic Probe Results" + Environment.NewLine, Encoding.UTF8);

            bool running = true;
            while (running)
            {
                Console.Clear();
                Console.WriteLine("Escolha o teste:");
                Console.WriteLine("  1 - 13 bytes, position 0x00");
                Console.WriteLine("  2 - 13 bytes, position 0x01");
                Console.WriteLine("  3 - 19 bytes, side 0x01");
                Console.WriteLine("  4 - 19 bytes, side 0x02");
                Console.WriteLine("  5 - 19 bytes, side 0x03");
                Console.WriteLine("  6 - 0x8F, pad 0x00");
                Console.WriteLine("  7 - 0x8F, pad 0x01");
                Console.WriteLine("  0 - Sair");
                Console.Write("Opção: ");

                var key = Console.ReadLine();
                switch (key)
                {
                    case "1": RunTest("13-byte position=0x00", () => Send13(0x00)); break;
                    case "2": RunTest("13-byte position=0x01", () => Send13(0x01)); break;
                    case "3": RunTest("19-byte side=0x01", () => Send19(0x01)); break;
                    case "4": RunTest("19-byte side=0x02", () => Send19(0x02)); break;
                    case "5": RunTest("19-byte side=0x03", () => Send19(0x03)); break;
                    case "6": RunTest("0x8F pad=0x00", () => Send8F(0x00)); break;
                    case "7": RunTest("0x8F pad=0x01", () => Send8F(0x01)); break;
                    case "0": running = false; break;
                }
            }

            device.Dispose();
        }

        private static void RunTest(string label, Action send)
        {
            Console.Clear();
            Console.WriteLine("TESTE: " + label);
            Console.WriteLine("Prepare o Steam Deck. Os pulsos começam em 3 segundos...");
            for (int i = 3; i >= 1; i--)
            {
                Console.WriteLine("  " + i + "...");
                Thread.Sleep(1000);
            }

            Console.WriteLine("Enviando 12 pulsos...");
            for (int i = 0; i < 12; i++)
            {
                send();
                Thread.Sleep(200);
            }

            Console.WriteLine();
            Console.WriteLine("Qual trackpad vibrou?");
            Console.WriteLine("  L = esquerdo");
            Console.WriteLine("  R = direito");
            Console.WriteLine("  B = ambos");
            Console.WriteLine("  N = nenhum");
            Console.Write("Resposta: ");
            string answer = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(answer)) answer = "?";

            string line = label + " => " + answer.Trim().ToUpperInvariant() + Environment.NewLine;
            File.AppendAllText(ResultsPath, line, Encoding.UTF8);
            Console.WriteLine("Registrado: " + line.Trim());
            Console.WriteLine("Pressione Enter para voltar ao menu.");
            Console.ReadLine();
        }

        private static void Send13(byte position)
        {
            int ts = Environment.TickCount;
            var packet = new byte[65];
            packet[0] = 0x00;
            packet[1] = 0xEA;
            packet[2] = 0x0D;
            packet[3] = position;
            packet[4] = 0x01; // tick
            packet[5] = 0x00;
            packet[6] = 0x03; // +8 dB
            packet[7] = 0x04;
            packet[8] = (byte)(ts & 0xFF);
            packet[9] = (byte)((ts >> 8) & 0xFF);
            packet[10] = (byte)((ts >> 16) & 0xFF);
            packet[11] = (byte)((ts >> 24) & 0xFF);
            packet[12] = (byte)(ts & 0xFF);
            packet[13] = (byte)((ts >> 8) & 0xFF);
            packet[14] = (byte)((ts >> 16) & 0xFF);
            packet[15] = (byte)((ts >> 24) & 0xFF);
            Send(packet);
        }

        private static void Send19(byte side)
        {
            var packet = new byte[65];
            packet[0] = 0x00;
            packet[1] = 0xEA;
            packet[2] = 0x13;
            packet[3] = side;
            packet[4] = 0x01; // tick
            packet[5] = 0x00;
            packet[6] = 0x03; // +8 dB
            Send(packet);
        }

        private static void Send8F(byte pad)
        {
            var packet = new byte[65];
            packet[0] = 0x00;
            packet[1] = 0x8F;
            packet[2] = 0x08;
            packet[3] = pad;
            packet[4] = 0x90; // duration 0x0190
            packet[5] = 0x01;
            packet[6] = 0x00; // interval 0
            packet[7] = 0x00;
            packet[8] = 0x01; // count 1
            packet[9] = 0x00;
            packet[10] = 0x03; // gain
            Send(packet);
        }

        private static void Send(byte[] packet)
        {
            int result = hid_send_feature_report(_handle, packet, (UIntPtr)packet.Length);
            Console.WriteLine(string.Format(
                "  type=0x{0:X2} len=0x{1:X2} side/pos=0x{2:X2} -> {3}",
                packet[1], packet[2], packet[3], result));
        }
    }
}
