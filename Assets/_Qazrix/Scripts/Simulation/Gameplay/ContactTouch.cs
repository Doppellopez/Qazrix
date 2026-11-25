namespace Qazrix.Simulation.Gameplay
{
    /// <summary>
    /// Cuándo se considera que el jugador ha tocado a la mujer que espera en la X del mapa: está pegado a ella
    /// (el cuerpo de ella mide 0,3 m de radio y el del jugador algo más) y a su altura, no en un tejado encima.
    /// </summary>
    public static class ContactTouch
    {
        /// <summary>Distancia horizontal máxima entre los centros (m).</summary>
        public const float Reach = 1.2f;

        /// <summary>Diferencia de altura máxima entre los pies de los dos (m).</summary>
        public const float MaxHeightDifference = 2f;

        /// <param name="dx">Diferencia en X del jugador respecto a ella.</param>
        /// <param name="dy">Diferencia de altura.</param>
        /// <param name="dz">Diferencia en Z.</param>
        public static bool IsTouching(float dx, float dy, float dz)
        {
            return dx * dx + dz * dz <= Reach * Reach + 1e-4f &&System.Math.Abs(dy) <= MaxHeightDifference;
        }
    }
}
