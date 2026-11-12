using System;

namespace Qazrix.Simulation.Gameplay
{
    /// <summary>
    /// Elige con qué objeto interactúa el jugador (spec §6.4): el más cercano que esté a su alcance y
    /// más o menos delante. Regla única para puertas, cartas, palancas y conversaciones.
    /// </summary>
    public static class InteractionRules
    {
        /// <summary>Alcance en metros desde el centro del jugador.</summary>
        public const float Range = 1.8f;

        /// <summary>Coseno mínimo del ángulo con la mirada: 0,2 ≈ hasta 78° a cada lado.</summary>
        public const float MinFacing = 0.2f;

        /// <summary>Tan cerca como esto, la dirección de la mirada ya no importa.</summary>
        public const float TouchDistance = 0.6f;

        /// <param name="distances">Distancia horizontal a cada candidato.</param>
        /// <param name="facings">Coseno entre la mirada del jugador y la dirección a cada candidato.</param>
        /// <param name="available">Si el candidato admite interacción ahora.</param>
        /// <returns>Índice del elegido, o -1 si no hay ninguno.</returns>
        public static int Pick(float[] distances, float[] facings, bool[] available, int count)
        {
            if (distances == null || facings == null || available == null) throw new ArgumentNullException();
            if (count > distances.Length || count > facings.Length || count > available.Length)
                throw new ArgumentException("Hay menos datos que candidatos.");

            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (!available[i] || distances[i] > Range) continue;
                if (distances[i] > TouchDistance && facings[i] < MinFacing) continue;
                if (distances[i] < bestDistance)
                {
                    bestDistance = distances[i];
                    best = i;
                }
            }
            return best;
        }
    }
}
