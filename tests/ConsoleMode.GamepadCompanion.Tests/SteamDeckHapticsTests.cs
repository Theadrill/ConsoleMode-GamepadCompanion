using ConsoleMode.GamepadCompanion.Hardware;
using Xunit;

namespace ConsoleMode.GamepadCompanion.Tests
{
    /// <summary>
    /// Valida o contrato do feature report nativo de haptics do Steam Deck (0xEA).
    /// O layout foi confirmado por captura USB dos pacotes que o Steam envia:
    /// EA 0D position style pad intensity 00 00 D9 39 48 65 FB 7F 00 00.
    /// O byte 0x07 é padding do struct SDCHapticPacket2 de 16 bytes.
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
            Assert.Equal(0x0D, packet[2]); // SDCHapticPacket2 = 13 bytes de payload
            Assert.Equal(SteamDeckControllerService.TriggerHapticPayloadLength, packet[2]);
            Assert.Equal(65, packet.Length);
        }

        [Theory]
        [InlineData(false, 0x00, 0x02)] // Trackpad esquerdo
        [InlineData(true, 0x01, 0x01)]  // Trackpad direito
        public void BuildTriggerHapticPacket_AddressesOnlyTheRequestedActuator(
            bool isRightPad,
            byte expectedPosition,
            byte expectedNativeSelector)
        {
            byte[] packet = SteamDeckControllerService.BuildTriggerHapticPacket(
                isRightPad: isRightPad,
                isClick: true,
                dBgain: 0);

            Assert.Equal(expectedPosition, packet[3]);
            Assert.Equal(expectedNativeSelector, packet[5]);
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
        public void BuildTriggerHapticPacket_UsesNativePaddingAndFixedTimestampLayout()
        {
            byte[] packet = SteamDeckControllerService.BuildTriggerHapticPacket(
                isRightPad: true,
                isClick: false,
                dBgain: -7);

            Assert.Equal(unchecked((byte)-7), packet[6]);
            Assert.Equal(SteamDeckControllerService.NativeHapticUnsure3, packet[7]);
            Assert.Equal(SteamDeckControllerService.NativeHapticPadding, packet[8]);

            Assert.Equal(0xD9, packet[9]);
            Assert.Equal(0x39, packet[10]);
            Assert.Equal(0x48, packet[11]);
            Assert.Equal(0x65, packet[12]);
            Assert.Equal(0xFB, packet[13]);
            Assert.Equal(0x7F, packet[14]);
            Assert.Equal(0x00, packet[15]);
            Assert.Equal(0x00, packet[16]);

            for (int i = 17; i < packet.Length; i++)
            {
                Assert.Equal(0x00, packet[i]);
            }
        }

        [Fact]
        public void BuildTriggerHapticPacket_MatchesCapturedSteamRightMovementPacket()
        {
            byte[] packet = SteamDeckControllerService.BuildTriggerHapticPacket(
                isRightPad: true,
                isClick: false,
                dBgain: 0);

            byte[] expected =
            {
                0x00, 0xEA, 0x0D, 0x01, 0x01, 0x01, 0x00, 0x00, 0x00,
                0xD9, 0x39, 0x48, 0x65, 0xFB, 0x7F, 0x00, 0x00
            };

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], packet[i]);
            }
        }

        [Fact]
        public void BuildTriggerHapticPacket_IsStableForConsecutivePulses()
        {
            byte[] first = SteamDeckControllerService.BuildTriggerHapticPacket(true, false, 0);
            byte[] second = SteamDeckControllerService.BuildTriggerHapticPacket(true, false, 0);

            Assert.Equal(first, second);
        }
    }
}
