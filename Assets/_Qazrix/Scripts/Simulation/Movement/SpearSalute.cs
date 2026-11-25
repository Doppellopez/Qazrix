using System;

namespace Qazrix.Simulation.Movement
{
    /// <summary>
    /// Los soldados alzan las lanzas al recibir la llave el capitán, pero al descompás: cada uno empieza en un momento distinto, la sube
    /// a su ritmo y la mantiene un rato distinto, así no es un movimiento de revista sino una ola desigual. El reparto de cada soldado
    /// sale de su número (determinista, sin azar). Sin dependencias de Unity.
    /// </summary>
    public static class SpearSalute
    {
        public const float MaxLift = 0.45f;          // m que sube la lanza
        public const float MaxArmDegrees = 50f;      // giro del brazo hacia delante y arriba
        public const float MaxDelay = 1.4f;          // el último soldado empieza hasta 1,4 s después
        public const float LowerSeconds = 0.7f;

        public struct Schedule
        {
            public float Delay, Rise, Hold;
            public float End => Delay + Rise + Hold + LowerSeconds;
        }

        /// <summary>Cuándo empieza, cuánto tarda en subirla y cuánto la mantiene el soldado número <paramref name="index"/>.</summary>
        public static Schedule ScheduleFor(int index)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            return new Schedule
            {
                Delay = MaxDelay * Unit(index, 1),
                Rise = 0.35f + 0.25f * Unit(index, 2),
                Hold = 2.5f + 2.5f * Unit(index, 3)
            };
        }

        /// <summary>Altura de la lanza de 0 (abajo) a 1 (arriba del todo), a los <paramref name="seconds"/> de la entrega de la llave.</summary>
        public static float Weight(float seconds, Schedule s)
        {
            float t = seconds - s.Delay;
            if (t <= 0f) return 0f;
            if (t < s.Rise) return Smooth(t / s.Rise);
            t -= s.Rise;
            if (t <= s.Hold) return 1f;
            t -= s.Hold;
            return t >= LowerSeconds ? 0f : Smooth(1f - t / LowerSeconds);
        }

        public static float Lift(float weight) => MaxLift * weight;

        public static float ArmDegrees(float weight) => MaxArmDegrees * weight;

        /// <summary>Número entre 0 y 1 que depende solo del soldado y de cuál de sus tres tiempos se pide.</summary>
        private static float Unit(int index, int salt)
        {
            uint h = (uint)(index + 1) * 2654435761u ^ (uint)salt * 40503u;
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13; h *= 3266489917u; h ^= h >> 16;
            return (h & 0xFFFF) / 65535f;
        }

        private static float Smooth(float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return t * t * (3f - 2f * t);
        }
    }
}
