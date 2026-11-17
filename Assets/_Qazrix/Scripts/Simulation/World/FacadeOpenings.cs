using System;
using System.Collections.Generic;

namespace Qazrix.Simulation.World
{
    public enum OpeningKind
    {
        /// <summary>Ventana geminada (ajimez) de casa o palacio.</summary>
        Window,
        /// <summary>Saetera: ranura estrecha de torre.</summary>
        ArrowSlit
    }

    /// <summary>Hueco dibujado sobre una fachada: centro, normal horizontal hacia fuera (unitaria; en diagonal si la pared lo es) y tipo.</summary>
    public struct FacadeOpening
    {
        public float X, Y, Z;
        public float NormalX, NormalZ;
        public OpeningKind Kind;
    }

    /// <summary>Medidas de reparto de huecos. Valores de prototipo, sin base documental.</summary>
    public sealed class FacadeOpeningRules
    {
        /// <summary>Un hueco cada tantas celdas a lo largo de la fachada.</summary>
        public int Spacing = 3;
        /// <summary>Altura del centro de la primera fila sobre el pie del muro, y separación entre filas (m).</summary>
        public float WindowFirst = 2.4f, WindowEvery = 3f;
        public float SlitFirst = 3f, SlitEvery = 4f;
        /// <summary>Alto del recuadro de cada tipo (m).</summary>
        public float WindowHeight = 1.6f, SlitHeight = 1.5f;
        /// <summary>Muro macizo que debe quedar entre el hueco y la cornisa (m).</summary>
        public float TopMargin = 0.6f;
        /// <summary>Es torre el volumen que se alza al menos esto sobre el suelo (m)...</summary>
        public float TowerMinHeight = 13f;
        /// <summary>...y cuya planta no pasa de esto (m²).</summary>
        public float TowerMaxArea = 200f;
    }

    /// <summary>
    /// Reparte ventanas y saeteras por las fachadas de la rejilla de tejados que usa
    /// <see cref="BlockMesher"/>: ventanas en las casas, saeteras en las torres. Sin dependencias de Unity.
    /// </summary>
    public static class FacadeOpenings
    {
        /// <summary>
        /// Marca las celdas que pertenecen a una torre: volúmenes de cota uniforme, altos y de planta pequeña.
        /// </summary>
        /// <param name="ground">Cota del terreno en cada esquina: (columns+1) × (rows+1), norte a sur.</param>
        public static bool[] Towers(float[] roofs, int columns, int rows, float cellSize, float[] ground, FacadeOpeningRules rules)
        {
            Check(roofs, columns, rows, cellSize, ground);
            if (rules == null) throw new ArgumentNullException(nameof(rules));

            var towers = new bool[roofs.Length];
            var seen = new bool[roofs.Length];
            var queue = new Queue<int>();
            var component = new List<int>();
            int stride = columns + 1;

            for (int start = 0; start < roofs.Length; start++)
            {
                if (seen[start] || float.IsNaN(roofs[start])) continue;
                component.Clear();
                queue.Enqueue(start);
                seen[start] = true;
                double groundSum = 0;
                while (queue.Count > 0)
                {
                    int cell = queue.Dequeue();
                    component.Add(cell);
                    int c = cell % columns, r = cell / columns;
                    groundSum += 0.25 * (ground[r * stride + c] + ground[r * stride + c + 1]
                        + ground[(r + 1) * stride + c] + ground[(r + 1) * stride + c + 1]);
                    Visit(c + 1, r); Visit(c - 1, r); Visit(c, r + 1); Visit(c, r - 1);

                    void Visit(int nc, int nr)
                    {
                        if (!BlockMesher.IsBuilt(roofs, columns, rows, nc, nr)) return;
                        int next = nr * columns + nc;
                        if (seen[next] || Math.Abs(roofs[next] - roofs[cell]) > 0.01f) return;
                        seen[next] = true;
                        queue.Enqueue(next);
                    }
                }

                float area = component.Count * cellSize * cellSize;
                float rise = roofs[start] - (float)(groundSum / component.Count);
                if (area <= rules.TowerMaxArea && rise >= rules.TowerMinHeight)
                    foreach (int cell in component) towers[cell] = true;
            }
            return towers;
        }

        /// <summary>
        /// Huecos de todas las fachadas: en cada cara de celda que da a la calle o a un tejado más bajo,
        /// una columna de huecos cada <see cref="FacadeOpeningRules.Spacing"/> celdas.
        /// </summary>
        /// <param name="stepHeight">El mismo salto entre volúmenes que se pasa a <see cref="BlockMesher.Build"/>.</param>
        public static List<FacadeOpening> Place(float[] roofs, int columns, int rows, float cellSize,
            float westX, float northZ, float[] ground, float stepHeight, FacadeOpeningRules rules)
        {
            bool[] towers = Towers(roofs, columns, rows, cellSize, ground, rules);
            if (rules.Spacing < 1) throw new ArgumentException("La separación entre huecos debe ser de una celda o más.");

            var openings = new List<FacadeOpening>();
            int stride = columns + 1;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    if (!BlockMesher.IsBuilt(roofs, columns, rows, c, r)) continue;
                    bool tower = towers[r * columns + c];
                    float x0 = westX + c * cellSize, z0 = northZ - r * cellSize, half = cellSize * 0.5f;

                    // Norte y sur corren a lo largo de las columnas; este y oeste, de las filas.
                    Face(c, c, r - 1, c, r, c + 1, r, x0 + half, z0, 0, 1);
                    Face(c, c, r + 1, c, r + 1, c + 1, r + 1, x0 + half, z0 - cellSize, 0, -1);
                    Face(r, c + 1, r, c + 1, r, c + 1, r + 1, x0 + cellSize, z0 - half, 1, 0);
                    Face(r, c - 1, r, c, r, c, r + 1, x0, z0 - half, -1, 0);

                    void Face(int along, int nc, int nr, int ai, int aj, int bi, int bj, float x, float z, int nx, int nz)
                    {
                        if (along % rules.Spacing != rules.Spacing / 2) return;
                        float top = roofs[r * columns + c];
                        float bottom;
                        if (!BlockMesher.IsBuilt(roofs, columns, rows, nc, nr))
                            bottom = Math.Max(ground[aj * stride + ai], ground[bj * stride + bi]);
                        else if (top - roofs[nr * columns + nc] > stepHeight)
                            bottom = roofs[nr * columns + nc];
                        else
                            return; // medianera o mismo volumen: no hay fachada

                        float first = tower ? rules.SlitFirst : rules.WindowFirst;
                        float every = tower ? rules.SlitEvery : rules.WindowEvery;
                        float halfHeight = 0.5f * (tower ? rules.SlitHeight : rules.WindowHeight);
                        for (float y = bottom + first; y + halfHeight + rules.TopMargin <= top; y += every)
                        {
                            openings.Add(new FacadeOpening
                            {
                                X = x, Y = y, Z = z, NormalX = nx, NormalZ = nz,
                                Kind = tower ? OpeningKind.ArrowSlit : OpeningKind.Window
                            });
                        }
                    }
                }
            }
            return openings;
        }

        private static void Check(float[] roofs, int columns, int rows, float cellSize, float[] ground)
        {
            if (roofs == null) throw new ArgumentNullException(nameof(roofs));
            if (ground == null) throw new ArgumentNullException(nameof(ground));
            if (columns < 1 || rows < 1 || roofs.Length != columns * rows) throw new ArgumentException("La rejilla de tejados no cuadra con sus dimensiones.");
            if (ground.Length != (columns + 1) * (rows + 1)) throw new ArgumentException("Las cotas del terreno deben darse por esquina.");
            if (cellSize <= 0f) throw new ArgumentException("Tamaño de celda no válido.");
        }
    }
}
