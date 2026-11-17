using System;

namespace Qazrix.Simulation.World
{
    /// <summary>Resultado de cortar las esquinas: rejilla con las celdas quitadas y rellenadas, y celdas cortadas en diagonal.</summary>
    public sealed class ChamferResult
    {
        /// <summary>Cota de tejado por celda; NaN en las celdas quitadas del todo. Las celdas rellenadas en un rincón llevan cota.</summary>
        public float[] Roofs;

        /// <summary>
        /// Por celda: 0 = entera; 1..4 = cortada en diagonal, con el triángulo de esa esquina quitado
        /// (<see cref="CornerRounding.CutNorthWest"/> y siguientes). Solo se queda el otro triángulo.
        /// </summary>
        public byte[] Cuts;

        /// <summary>Celdas que caen del todo detrás de una diagonal.</summary>
        public int RemovedCells;

        /// <summary>Celdas de esquina saliente que la diagonal deja a medias (triángulo).</summary>
        public int CutCells;

        /// <summary>Celdas vacías que se rellenan a medias (triángulo) en un rincón entrante.</summary>
        public int FilledCells;
    }

    /// <summary>
    /// Corta todas las esquinas de los edificios de una rejilla de tejados con una línea diagonal (chaflán).
    /// <para>
    /// Esquinas salientes: con tamaño <c>n</c> la diagonal une dos puntos a <c>n</c> celdas de la esquina por cada
    /// lado; las celdas que caen del todo detrás se quitan (las de índice i + j ≤ n − 2 desde la esquina) y las que
    /// la diagonal parte por la mitad (i + j = n − 1) se quedan como triángulo. Si el edificio es demasiado pequeño
    /// para el tamaño pedido, se prueba con uno menor hasta 1, así que también se cortan las esquinas de las torres.
    /// </para>
    /// <para>
    /// Rincones entrantes: la celda vacía del rincón se rellena con un triángulo de una celda de lado, y la
    /// diagonal cierra el rincón. Solo se hace donde los dos muros siguen rectos al menos una celda más
    /// y las tres celdas que rodean el rincón son enteras y de cota parecida.
    /// </para>
    /// Solo cuenta como esquina un ángulo recto «de verdad»: los escalones sueltos de un borde irregular no se tocan.
    /// Sin dependencias de Unity.
    /// </summary>
    public static class CornerRounding
    {
        public const byte CutNorthWest = 1, CutNorthEast = 2, CutSouthEast = 3, CutSouthWest = 4;

        /// <summary>Tamaño máximo del corte, en celdas: más de esto se comería edificios pequeños.</summary>
        public const int MaxPasses = 6;

        /// <param name="roofs">Cota de tejado por celda, filas de norte a sur; NaN = sin edificio.</param>
        /// <param name="size">Celdas que se alejan de la esquina los extremos de la diagonal (0 = no cambia nada).</param>
        /// <param name="stepHeight">Diferencia de cota máxima entre las celdas que rodean un rincón para rellenarlo.</param>
        /// <returns>Resultado nuevo; la rejilla original no se modifica.</returns>
        public static ChamferResult Chamfer(float[] roofs, int columns, int rows, int size, float stepHeight = float.PositiveInfinity)
        {
            if (roofs == null) throw new ArgumentNullException(nameof(roofs));
            if (columns < 1 || rows < 1 || roofs.Length != columns * rows) throw new ArgumentException("La rejilla de tejados no cuadra con sus dimensiones.");

            var result = new ChamferResult { Roofs = (float[])roofs.Clone(), Cuts = new byte[roofs.Length] };
            int n = Math.Max(0, Math.Min(size, MaxPasses));
            if (n == 0) return result;

            bool Built(int x, int y) => BlockMesher.IsBuilt(roofs, columns, rows, x, y);

            // Se decide siempre sobre la rejilla original: el resultado no depende del orden de recorrido.
            var removed = new bool[roofs.Length];
            var cut = new byte[roofs.Length];

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    if (!Built(c, r)) continue;
                    for (int oy = -1; oy <= 1; oy += 2)
                    {
                        for (int ox = -1; ox <= 1; ox += 2)
                        {
                            // El mayor corte que cabe; los edificios pequeños (torres) se quedan con uno menor.
                            for (int s = n; s >= 1; s--)
                            {
                                if (!IsCorner(roofs, columns, rows, c, r, ox, oy, s)) continue;
                                byte corner = oy < 0 ? (ox < 0 ? CutNorthWest : CutNorthEast) : (ox > 0 ? CutSouthEast : CutSouthWest);
                                for (int j = 0; j < s; j++)
                                {
                                    for (int i = 0; i + j < s; i++)
                                    {
                                        int index = (r - oy * j) * columns + (c - ox * i);
                                        if (i + j <= s - 2) removed[index] = true;
                                        else if (cut[index] == 0) cut[index] = corner;
                                        else if (cut[index] != corner) removed[index] = true; // cortada por dos esquinas: no queda nada útil
                                    }
                                }
                                break;
                            }
                        }
                    }
                }
            }

            for (int index = 0; index < roofs.Length; index++)
            {
                if (removed[index])
                {
                    result.Roofs[index] = float.NaN;
                    result.RemovedCells++;
                }
                else if (cut[index] != 0)
                {
                    result.Cuts[index] = cut[index];
                    result.CutCells++;
                }
            }

            FillReentrantCorners(roofs, columns, rows, stepHeight, result);
            return result;
        }

        /// <summary>
        /// Rellena con un triángulo la celda vacía de cada rincón entrante recto. Se mira la rejilla original y se
        /// descartan los rincones donde alguna de las tres celdas que lo rodean ha sido tocada por un corte.
        /// </summary>
        private static void FillReentrantCorners(float[] roofs, int columns, int rows, float stepHeight, ChamferResult result)
        {
            bool Built(int x, int y) => BlockMesher.IsBuilt(roofs, columns, rows, x, y);
            bool Whole(int x, int y) => Built(x, y) && !float.IsNaN(result.Roofs[y * columns + x]) && result.Cuts[y * columns + x] == 0;

            var fillHeight = new float[roofs.Length];
            var fillCut = new byte[roofs.Length];
            var clash = new bool[roofs.Length];

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    if (Built(c, r)) continue;
                    for (int dy = -1; dy <= 1; dy += 2)
                    {
                        for (int dx = -1; dx <= 1; dx += 2)
                        {
                            // El rincón está en la esquina de la celda vacía que mira hacia (dx, dy): el edificio la
                            // rodea por la celda de al lado, la de arriba/abajo y la diagonal.
                            if (!Whole(c + dx, r) || !Whole(c, r + dy) || !Whole(c + dx, r + dy)) continue;

                            // Los dos muros que forman el rincón siguen rectos una celda más allá de la vacía.
                            if (!Whole(c + dx, r - dy) || Built(c, r - dy)) continue;
                            if (!Whole(c - dx, r + dy) || Built(c - dx, r)) continue;

                            float a = result.Roofs[r * columns + c + dx], b = result.Roofs[(r + dy) * columns + c],
                                d = result.Roofs[(r + dy) * columns + c + dx];
                            float lowest = Math.Min(a, Math.Min(b, d)), highest = Math.Max(a, Math.Max(b, d));
                            if (highest - lowest > stepHeight) continue;

                            // El rincón es la esquina (dx, dy) de la celda vacía: se queda el triángulo que la contiene
                            // y se quita la esquina contraria.
                            byte inner = dy < 0 ? (dx < 0 ? CutNorthWest : CutNorthEast) : (dx > 0 ? CutSouthEast : CutSouthWest);
                            byte opposite = Opposite(inner);

                            int index = r * columns + c;
                            if (fillCut[index] != 0) { clash[index] = true; continue; }
                            fillCut[index] = opposite;
                            fillHeight[index] = (a + b + d) / 3f;
                        }
                    }
                }
            }

            for (int index = 0; index < roofs.Length; index++)
            {
                if (fillCut[index] == 0 || clash[index]) continue; // dos rincones en la misma celda: se deja sin rellenar
                result.Roofs[index] = fillHeight[index];
                result.Cuts[index] = fillCut[index];
                result.FilledCells++;
            }
        }

        /// <summary>Esquina contraria de una celda: noroeste ↔ sureste, noreste ↔ suroeste.</summary>
        private static byte Opposite(byte corner)
        {
            switch (corner)
            {
                case CutNorthWest: return CutSouthEast;
                case CutNorthEast: return CutSouthWest;
                case CutSouthEast: return CutNorthWest;
                default: return CutNorthEast;
            }
        }

        /// <summary>
        /// ¿Es la celda (c, r) una esquina saliente de ángulo recto cuyos lados vacíos miran hacia (ox, oy)?
        /// Los lados rectos corren hacia el interior (−ox, −oy).
        /// </summary>
        private static bool IsCorner(float[] roofs, int columns, int rows, int c, int r, int ox, int oy, int size)
        {
            bool Built(int x, int y) => BlockMesher.IsBuilt(roofs, columns, rows, x, y);

            // Los dos lados que dan al exterior están vacíos.
            if (Built(c + ox, r) || Built(c, r + oy)) return false;

            // Los contornos sacados del LiDAR son irregulares: exigir lados rectos largos dejaría casi todas
            // las esquinas sin cortar. Basta con que la celda de al lado, en cada sentido, siga el borde.
            int run = Math.Max(1, size - 1);
            for (int i = 1; i <= run; i++)
            {
                // Lado recto horizontal: la celda existe y sigue teniendo vacío el lado exterior vertical.
                if (!Built(c - ox * i, r) || Built(c - ox * i, r + oy)) return false;
                // Lado recto vertical.
                if (!Built(c, r - oy * i) || Built(c + ox, r - oy * i)) return false;
            }

            // El edificio mide al menos 2 × size celdas en cada sentido: así los cortes de dos esquinas del mismo lado
            // no se pisan. Si no cabe, se prueba con un corte menor.
            for (int i = size + 1; i < 2 * size; i++)
                if (!Built(c - ox * i, r) || !Built(c, r - oy * i)) return false;

            // Bloque macizo detrás de la esquina: el corte no puede dejar el edificio colgando de un hilo.
            for (int j = 1; j <= size; j++)
                for (int i = 1; i <= size; i++)
                    if (!Built(c - ox * i, r - oy * j)) return false;
            return true;
        }
    }
}
