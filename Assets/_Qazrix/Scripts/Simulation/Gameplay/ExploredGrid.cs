using System.Collections.Generic;

namespace Qazrix.Simulation.Gameplay
{
    /// <summary>
    /// Lo que el jugador ya ha visto del mundo, en celdas cuadradas (rejilla dispersa: solo guarda las vistas).
    /// Sin dependencias de Unity.
    /// </summary>
    public sealed class ExploredGrid
    {
        private readonly HashSet<long> _seen = new HashSet<long>();
        private readonly float _cell;

        public ExploredGrid(float cellSize = 2f)
        {
            _cell = cellSize > 0f ? cellSize : 2f;
        }

        public int CellCount => _seen.Count;

        private static long Key(int cx, int cz)
        {
            return ((long)cx << 32) ^ (uint)cz;
        }

        private int Cell(float v)
        {
            return (int)System.Math.Floor(v / _cell);
        }

        /// <summary>Marca como vistas las celdas cuyo centro está a menos de <paramref name="radius"/> metros del punto.</summary>
        public void Reveal(float x, float z, float radius)
        {
            int minX = Cell(x - radius), maxX = Cell(x + radius);
            int minZ = Cell(z - radius), maxZ = Cell(z + radius);
            float r2 = radius * radius;
            for (int cx = minX; cx <= maxX; cx++)
            {
                for (int cz = minZ; cz <= maxZ; cz++)
                {
                    float dx = (cx + 0.5f) * _cell - x, dz = (cz + 0.5f) * _cell - z;
                    if (dx * dx + dz * dz <= r2) _seen.Add(Key(cx, cz));
                }
            }
        }

        public bool IsRevealed(float x, float z)
        {
            return _seen.Contains(Key(Cell(x), Cell(z)));
        }

        public void Clear()
        {
            _seen.Clear();
        }
    }
}
