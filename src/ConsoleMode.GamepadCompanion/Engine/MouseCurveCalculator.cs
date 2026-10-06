using System;

namespace ConsoleMode.GamepadCompanion.Engine
{
    /// <summary>
    /// Matemática pura do analógico como mouse: deadzone circular + curva de potência.
    /// Sem dependência de hardware ou UI (100% testável).
    /// </summary>
    public static class MouseCurveCalculator
    {
        /// <summary>Velocidade máxima (px/s) com sensibilidade 50 e inclinação total.</summary>
        public const float BaseMaxSpeed = 1600f;

        /// <summary>Expoente da curva: leve = preciso, total = rápido.</summary>
        public const float CurvePower = 2.0f;

        /// <summary>Resultado em pixels (fracionário) para o intervalo informado.</summary>
        public static void Calculate(
            short rawX, short rawY, int sensitivity, float deltaSeconds, float deadzone,
            out float dx, out float dy)
        {
            dx = 0f;
            dy = 0f;

            float nx = Clamp(rawX / 32767f);
            float ny = Clamp(rawY / 32767f);
            float magnitude = (float)Math.Sqrt(nx * nx + ny * ny);
            if (magnitude <= deadzone || magnitude <= 0f) return;

            // Remapeia (deadzone..1) -> (0..1) para não haver "salto" ao sair da deadzone.
            float scaled = Math.Min(1f, (magnitude - deadzone) / (1f - deadzone));
            float curved = (float)Math.Pow(scaled, CurvePower);

            float speed = BaseMaxSpeed * (Math.Max(1, Math.Min(100, sensitivity)) / 50f) * curved;
            float distance = speed * deltaSeconds;

            dx = nx / magnitude * distance;
            dy = -ny / magnitude * distance; // stick para cima = cursor para cima (Y de tela é invertido)
        }

        private static float Clamp(float value)
        {
            return Math.Max(-1f, Math.Min(1f, value));
        }
    }
}
