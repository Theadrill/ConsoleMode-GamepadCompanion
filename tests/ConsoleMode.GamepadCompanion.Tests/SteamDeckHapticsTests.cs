using ConsoleMode.GamepadCompanion.Hardware;
using Xunit;

namespace ConsoleMode.GamepadCompanion.Tests
{
    /// <summary>
    /// Valida o contrato do feature report nativo de haptics do Steam Deck (0xEA).
    /// O firmware Neptune lê a estrutura legada de 13 bytes (SDCHapticPacket2):
    /// position 0x00 = Esquerdo, 0x01 = Direito, 0x02 = Ambos, 0x03 = Nenhum.
    /// Enviar 0x02 para o pad direito aciona os dois LRAs e causa crosstalk.
    /// </summary>
    public class SteamDeckHapticsTests
    {
        [Fact]
        public void BuildTriggerHapticPacket_UsesDeckNativeHeaderAndLegacyPayloadLength()
        {
            byte[] packet = SteamDeckControllerService.BuildTriggerHapticPacket(
                isRightPad: true,
                isClick: false,
                dBgain: -2);

            Assert.Equal(0x00, packet[0]); // Report ID HIDAPI
            Assert.Equal(0xEA, packet[1]); // ID_TRIGGER_HAPTIC_CMD (Deck/Neptune)
            Assert.Equal(0x0D, packet[2]); // SDCHapticPacket2 = 13 bytes
            Assert.Equal(SteamDeckControllerService.TriggerHapticPayloadLength, packet[2]);
            Assert.Equal(65, packet.Length);
        }

        [Theory]
        [InlineData(false, 0x00)] // Trackpad esquerdo
        [InlineData(true, 0x01)]  // Trackpad direito
        public void BuildTriggerHapticPacket_AddressesOnlyTheRequestedActuator(bool isRightPad, byte expectedPosition)
        {
            byte[] packet = SteamDeckControllerService.BuildTriggerHapticPacket(
                isRightPad: isRightPad,
                isClick: true,
                dBgain: 0);

            Assert.Equal(expectedPosition, packet[3]);
            Assert.NotEqual(0x02, packet[3]); // 0x02 aciona ambos os LRAs
            Assert.NotEqual(0x03, packet[3]); // 0x03 não aciona nenhum
        }

        [Theory]
        [InlineData(false, 0x01)] // tick / rotação / scroll
        [InlineData(true, 0x02)]  // click / press down-up
        public void BuildTriggerHapticPacket_MapsTickAndClickCommands(bool isClick, byte expectedCommand)
        {
            byte[] packet = SteamDeckControllerService.BuildTriggerHapticPacket(
                isRightPad: false,
                isClick: isClick,
                dBgain: 0);

            Assert.Equal(expectedCommand, packet[4]);
        }

        [Fact]
        public void BuildTriggerHapticPacket_UsesExpectedFixedFieldsAndDuplicateTimestamps()
        {
            byte[] packet = SteamDeckControllerService.BuildTriggerHapticPacket(
                isRightPad: true,
                isClick: false,
                dBgain: -7);

            Assert.Equal(unchecked((byte)-7), packet[6]);
            Assert.Equal(0x04, packet[7]);

            // tsA e tsB devem ser idênticos no SDCHapticPacket2.
            for (int i = 0; i < 4; i++)
            {
                Assert.Equal(packet[8 + i], packet[12 + i]);
            }
        }

        [Fact]
        public void BuildTriggerHapticPacket_ChangesTimestampBetweenConsecutivePulses()
        {
            byte[] first = SteamDeckControllerService.BuildTriggerHapticPacket(true, false, 0);
            byte[] second = SteamDeckControllerService.BuildTriggerHapticPacket(true, false, 0);

            bool different = false;
            for (int i = 8; i <= 11; i++)
            {
                if (first[i] != second[i])
                {
                    different = true;
                    break;
                }
            }

            Assert.True(different);
        }
    }
}
