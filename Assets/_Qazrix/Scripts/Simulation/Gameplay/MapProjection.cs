namespace Qazrix.Simulation.Gameplay
{
    /// <summary>
    /// Proyección del mapa cenital: norte (+Z) arriba, este (+X) a la derecha, centrado en un punto del
    /// mundo. Sin dependencias de Unity para poder probarla.
    /// </summary>
    public static class MapProjection
    {
        /// <summary>
        /// Posición en el mapa (píxeles desde la esquina superior izquierda) de un punto a (dx, dz) metros del centro.
        /// <paramref name="halfExtent"/> son los metros que hay del centro al borde del mapa.
        /// </summary>
        public static void WorldToMap(float dx, float dz, float halfExtent, float mapSize, out float x, out float y)
        {
            float scale = mapSize * 0.5f / halfExtent;
            x = mapSize * 0.5f + dx * scale;
            y = mapSize * 0.5f - dz * scale; // el norte queda arriba: y de pantalla crece hacia abajo
        }

        /// <summary>¿Cae el punto dentro del mapa?</summary>
        public static bool Contains(float dx, float dz, float halfExtent)
        {
            return dx >= -halfExtent && dx <= halfExtent && dz >= -halfExtent && dz <= halfExtent;
        }
    }
}
