using System;

namespace Qazrix.Simulation.Movement
{
    /// <summary>
    /// Ciclo de andar de un personaje bípedo: fase que avanza con la velocidad y ángulos de piernas y
    /// brazos en función de esa fase. Determinista y sin dependencias de Unity.
    /// </summary>
    public static class WalkCycle
    {
        /// <summary>Metros que recorre el cuerpo en un ciclo completo (dos pasos).</summary>
        public const float StrideLength = 1.5f;

        /// <summary>Balanceo máximo de la pierna, en grados, al correr.</summary>
        public const float MaxLegSwing = 38f;

        /// <summary>Velocidad (m/s) a la que el balanceo llega a su máximo.</summary>
        public const float FullSwingSpeed = 5f;

        /// <summary>Por debajo de esta velocidad el personaje se considera quieto.</summary>
        public const float StillSpeed = 0.1f;

        /// <summary>Avanza la fase (en vueltas, 0..1) según lo andado en este paso de tiempo.</summary>
        public static float Advance(float phase, float speed, float deltaSeconds)
        {
            if (speed < StillSpeed || deltaSeconds <= 0f) return phase;
            phase += speed * deltaSeconds / RunCycle.StrideLength(speed); // más largo al correr
            return phase - (float)Math.Floor(phase);
        }

        /// <summary>Amplitud del balanceo en grados para una velocidad dada: andar poco, correr mucho.</summary>
        public static float Amplitude(float speed)
        {
            if (speed < StillSpeed) return 0f;
            float t = Math.Min(speed / FullSwingSpeed, 1f);
            return MaxLegSwing * (0.45f + 0.55f * t);
        }

        /// <summary>Ángulo de la pierna izquierda (positivo = hacia delante). La derecha es el opuesto.</summary>
        public static float LegAngle(float phase, float speed)
        {
            return (float)Math.Sin(phase * 2.0 * Math.PI) * Amplitude(speed);
        }

        /// <summary>Ángulo del brazo izquierdo: contrario a su pierna y algo menor.</summary>
        public static float ArmAngle(float phase, float speed)
        {
            return -LegAngle(phase, speed) * 0.7f;
        }
    }
}
