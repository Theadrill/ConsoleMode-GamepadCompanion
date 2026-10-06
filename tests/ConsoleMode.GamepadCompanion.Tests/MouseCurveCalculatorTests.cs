using ConsoleMode.GamepadCompanion.Core;
using ConsoleMode.GamepadCompanion.Engine;
using Xunit;

namespace ConsoleMode.GamepadCompanion.Tests
{
    public class MouseCurveCalculatorTests
    {
        private const float Dz = GamepadDefaults.StickDeadzone;
        private const float Dt = 0.01f;

        private static void Calc(short x, short y, int sens, out float dx, out float dy)
        {
            MouseCurveCalculator.Calculate(x, y, sens, Dt, Dz, out dx, out dy);
        }

        [Fact]
        public void CenteredStick_ProducesNoMovement()
        {
            Calc(0, 0, 50, out float dx, out float dy);
            Assert.Equal(0f, dx);
            Assert.Equal(0f, dy);
        }

        [Fact]
        public void InsideDeadzone_ProducesNoMovement()
        {
            short small = (short)(32767 * (Dz - 0.02f));
            Calc(small, 0, 50, out float dx, out float dy);
            Assert.Equal(0f, dx);
            Assert.Equal(0f, dy);
        }

        [Fact]
        public void DiagonalInsideDeadzone_UsesCircularNotSquareZone()
        {
            // Cada eixo (0.18) é menor que a deadzone, mas a magnitude (~0.25) passa dela.
            short axis = (short)(32767 * 0.18f);
            Calc(axis, axis, 50, out float dx, out _);
            Assert.True(dx > 0f);
        }

        [Fact]
        public void FullDeflection_MatchesMaxSpeed()
        {
            Calc(32767, 0, 50, out float dx, out float dy);
            Assert.Equal(MouseCurveCalculator.BaseMaxSpeed * Dt, dx, 3);
            Assert.Equal(0f, dy, 3);
        }

        [Fact]
        public void StickUp_MovesCursorUp()
        {
            Calc(0, 32767, 50, out _, out float dy);
            Assert.True(dy < 0f);
        }

        [Fact]
        public void StickLeft_MovesCursorLeft()
        {
            Calc(-32768, 0, 50, out float dx, out _);
            Assert.True(dx < 0f);
        }

        [Fact]
        public void HalfDeflection_IsSlowerThanLinearHalf()
        {
            Calc(32767, 0, 50, out float full, out _);
            short half = (short)(32767 * (Dz + (1f - Dz) * 0.5f));
            Calc(half, 0, 50, out float mid, out _);
            Assert.True(mid < full * 0.5f);
            Assert.True(mid > 0f);
        }

        [Fact]
        public void Speed_IncreasesMonotonicallyWithDeflection()
        {
            float previous = 0f;
            for (int raw = 8000; raw <= 32767; raw += 1000)
            {
                Calc((short)raw, 0, 50, out float dx, out _);
                Assert.True(dx >= previous);
                previous = dx;
            }
        }

        [Fact]
        public void Sensitivity_ScalesSpeedLinearly()
        {
            Calc(32767, 0, 25, out float slow, out _);
            Calc(32767, 0, 50, out float normal, out _);
            Calc(32767, 0, 100, out float fast, out _);
            Assert.Equal(normal * 0.5f, slow, 3);
            Assert.Equal(normal * 2f, fast, 3);
        }

        [Fact]
        public void LeavingDeadzone_StartsSmoothlyFromZero()
        {
            short justOutside = (short)(32767 * (Dz + 0.005f));
            Calc(justOutside, 0, 50, out float dx, out _);
            Assert.InRange(dx, 0f, 0.05f);
        }
    }
}
