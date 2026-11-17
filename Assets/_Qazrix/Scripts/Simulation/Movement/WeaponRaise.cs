using System;

namespace Qazrix.Simulation.Movement
{
    /// <summary>
    /// Brazo del arma en alto (el guardia que persigue): el brazo sube hacia delante, la hoja apunta hacia
    /// arriba y un poco hacia delante, y todo se balancea levemente como al blandirla. El peso va de 0 (arma
    /// abajo, en reposo) a 1 (arma en alto). Sin dependencias de Unity. Ángulos en grados, positivo = hacia delante.
    /// </summary>
    public static class WeaponRaise
    {
        /// <summary>Brazo levantado: 145° hacia delante (la mano queda por encima del hombro).</summary>
        public const float ArmUp = 145f;

        /// <summary>Inclinación de la hoja con el arma en alto, respecto a la vertical: 25° hacia delante.</summary>
        public const float BladeForward = 25f;

        /// <summary>Hoja con el brazo caído: apunta hacia delante y algo hacia abajo (100° desde la vertical hacia abajo).</summary>
        public const float BladeLowered = 100f;

        /// <summary>Balanceo del brazo en alto: amplitud (°) y ritmo (ciclos por segundo).</summary>
        public const float Brandish = 7f, BrandishRate = 2.2f;

        /// <summary>Cuánto cambia el peso por segundo (levantar o bajar tarda unos 0,2 s).</summary>
        public const float BlendPerSecond = 5f;

        /// <summary>Acerca el peso a 1 (arma en alto) o a 0 (abajo) a ritmo constante.</summary>
        public static float Blend(float current, bool raised, float deltaTime)
        {
            float target = raised ? 1f : 0f;
            float step = BlendPerSecond * Math.Max(0f, deltaTime);
            if (current < target) return Math.Min(target, current + step);
            return Math.Max(target, current - step);
        }

        /// <summary>Ángulo del brazo: el de andar con peso 0 y el del brazo en alto, con su balanceo, con peso 1.</summary>
        public static float ArmAngle(float walkAngle, float weight, float seconds)
        {
            float w = Clamp01(weight);
            float up = ArmUp + (float)Math.Sin(seconds * BrandishRate * 2.0 * Math.PI) * Brandish * w;
            return walkAngle + (up - walkAngle) * w;
        }

        /// <summary>
        /// Giro local del puño del arma. Como cuelga del brazo, hereda su giro: para que la hoja quede donde se
        /// quiere (entre <see cref="BladeLowered"/> y <see cref="BladeForward"/>) hay que sumarle el ángulo del brazo.
        /// </summary>
        public static float GripAngle(float weight, float armAngle)
        {
            float w = Clamp01(weight);
            return BladeLowered + (BladeForward - BladeLowered) * w + armAngle;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
