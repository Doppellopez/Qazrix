using System;

namespace Qazrix.Simulation.World
{
    /// <summary>
    /// Rejilla cuadrada de cotas centrada en el origen local (spec §5.6). Guarda las filas como
    /// llegan de un ráster GIS, de norte a sur, y ofrece el acceso de sur a norte que usa Unity.
    /// Tener este volteo en un único sitio probado evita el fallo clásico del mapa en espejo.
    /// </summary>
    public sealed class HeightGrid
    {
        private readonly float[] _northToSouth;

        /// <param name="northToSouthRows">Cotas en metros, fila a fila de norte a sur y de oeste a este.</param>
        /// <param name="samples">Muestras por lado (impar: hay una muestra justo en el origen).</param>
        /// <param name="cellSize">Metros entre muestras.</param>
        public HeightGrid(float[] northToSouthRows, int samples, double cellSize)
        {
            if (northToSouthRows == null) throw new ArgumentNullException(nameof(northToSouthRows));
            if (samples < 3 || samples % 2 == 0) throw new ArgumentException("El número de muestras debe ser impar y mayor que 2.");
            if (northToSouthRows.Length != samples * samples) throw new ArgumentException("El tamaño de los datos no cuadra con las muestras.");
            if (cellSize <= 0) throw new ArgumentException("El tamaño de celda debe ser positivo.");

            _northToSouth = northToSouthRows;
            Samples = samples;
            CellSize = cellSize;

            float min = float.MaxValue, max = float.MinValue;
            foreach (float h in northToSouthRows)
            {
                if (float.IsNaN(h)) throw new ArgumentException("Hay cotas sin dato (NaN).");
                if (h < min) min = h;
                if (h > max) max = h;
            }
            Min = min;
            Max = max;
        }

        public int Samples { get; }
        public double CellSize { get; }
        public float Min { get; }
        public float Max { get; }

        /// <summary>Distancia del origen al borde, en metros.</summary>
        public double HalfExtent => (Samples - 1) / 2 * CellSize;

        /// <summary>Cota de la muestra en la columna (oeste → este) y fila contada desde el sur.</summary>
        public float At(int column, int rowFromSouth)
        {
            if (column < 0 || column >= Samples || rowFromSouth < 0 || rowFromSouth >= Samples)
                throw new ArgumentOutOfRangeException();
            return _northToSouth[(Samples - 1 - rowFromSouth) * Samples + column];
        }

        public bool Contains(double localX, double localZ)
        {
            return Math.Abs(localX) <= HalfExtent && Math.Abs(localZ) <= HalfExtent;
        }

        /// <summary>Cota interpolada (bilineal) en un punto local: X = este, Z = norte.</summary>
        public double Sample(double localX, double localZ)
        {
            if (!Contains(localX, localZ)) throw new ArgumentOutOfRangeException(nameof(localX), "Punto fuera de la rejilla.");

            double fx = (localX + HalfExtent) / CellSize;
            double fz = (localZ + HalfExtent) / CellSize;
            int x0 = Math.Min((int)Math.Floor(fx), Samples - 2);
            int z0 = Math.Min((int)Math.Floor(fz), Samples - 2);
            double tx = fx - x0;
            double tz = fz - z0;

            double south = At(x0, z0) * (1 - tx) + At(x0 + 1, z0) * tx;
            double north = At(x0, z0 + 1) * (1 - tx) + At(x0 + 1, z0 + 1) * tx;
            return south * (1 - tz) + north * tz;
        }

        /// <summary>Índice de la muestra más cercana a una coordenada local, acotado a la rejilla.</summary>
        public int NearestIndex(double local)
        {
            int i = (int)Math.Round((local + HalfExtent) / CellSize);
            return Math.Max(0, Math.Min(Samples - 1, i));
        }
    }
}
