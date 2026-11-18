using System;

namespace Qazrix.Simulation.Gameplay
{
    /// <summary>Elige un punto al azar en una corona alrededor de un centro, que cumpla una condición. Sin dependencias de Unity.</summary>
    public static class RandomSpot
    {
        /// <summary>
        /// Prueba hasta <paramref name="attempts"/> puntos de la corona entre <paramref name="minRadius"/> y
        /// <paramref name="maxRadius"/> (reparto uniforme por superficie) y devuelve el primero que acepta
        /// <paramref name="accept"/>. <paramref name="random01"/> da números en [0, 1).
        /// </summary>
        public static bool TryPick(Func<float> random01, float centerX, float centerZ, float minRadius, float maxRadius,
            Func<float, float, bool> accept, int attempts, out float x, out float z)
        {
            x = centerX;
            z = centerZ;
            if (random01 == null || accept == null || minRadius < 0f || maxRadius <= minRadius) return false;

            for (int i = 0; i < attempts; i++)
            {
                float angle = random01() * 2f * (float)Math.PI;
                float radius = (float)Math.Sqrt(minRadius * minRadius + random01() * (maxRadius * maxRadius - minRadius * minRadius));
                float px = centerX + (float)Math.Cos(angle) * radius;
                float pz = centerZ + (float)Math.Sin(angle) * radius;
                if (!accept(px, pz)) continue;
                x = px;
                z = pz;
                return true;
            }
            return false;
        }
    }
}
