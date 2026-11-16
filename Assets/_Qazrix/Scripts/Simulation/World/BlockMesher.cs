using System;
using System.Collections.Generic;

namespace Qazrix.Simulation.World
{
    /// <summary>Malla de un trozo de ciudad: vértices (x, y, z seguidos) y triángulos de tejado y de muro.</summary>
    public sealed class BlockMeshChunk
    {
        public float[] Vertices;
        public int[] RoofTriangles;
        public int[] WallTriangles;

        public int VertexCount => Vertices.Length / 3;
    }

    /// <summary>
    /// Levanta la volumetría de los edificios (spec §5.3, capa B) a partir de una rejilla de cotas de
    /// tejado: una superficie continua sobre las celdas construidas y un muro hasta el suelo en cada
    /// borde con una celda vacía. Sin dependencias de Unity; el orden de los vértices sigue el convenio
    /// de Unity (cara vista en sentido horario).
    /// </summary>
    public static class BlockMesher
    {
        /// <param name="roofs">Cota de tejado por celda, filas de norte a sur; NaN = sin edificio.</param>
        /// <param name="westX">X local del borde oeste de la rejilla.</param>
        /// <param name="northZ">Z local del borde norte de la rejilla.</param>
        /// <param name="baseHeights">Cota del pie de muro en cada esquina: (columns+1) × (rows+1), norte a sur.</param>
        /// <param name="chunkCells">Lado, en celdas, de cada trozo de malla.</param>
        /// <param name="stepHeight">Salto de cota (m) a partir del cual dos celdas vecinas son volúmenes
        /// distintos: cada una conserva su tejado y entre ambas se levanta un muro. Por defecto no hay
        /// escalones y toda la superficie es continua.</param>
        public static List<BlockMeshChunk> Build(float[] roofs, int columns, int rows, float cellSize,
            float westX, float northZ, float[] baseHeights, int chunkCells, float stepHeight = float.PositiveInfinity)
        {
            if (!(stepHeight > 0f)) throw new ArgumentException("El salto entre volúmenes debe ser positivo.");
            if (roofs == null) throw new ArgumentNullException(nameof(roofs));
            if (baseHeights == null) throw new ArgumentNullException(nameof(baseHeights));
            if (columns < 1 || rows < 1 || roofs.Length != columns * rows) throw new ArgumentException("La rejilla de tejados no cuadra con sus dimensiones.");
            if (baseHeights.Length != (columns + 1) * (rows + 1)) throw new ArgumentException("Las cotas de base deben darse por esquina.");
            if (cellSize <= 0f || chunkCells < 1) throw new ArgumentException("Tamaños no válidos.");

            var chunks = new List<BlockMeshChunk>();

            for (int r0 = 0; r0 < rows; r0 += chunkCells)
            {
                for (int c0 = 0; c0 < columns; c0 += chunkCells)
                {
                    BlockMeshChunk chunk = BuildChunk(roofs, stepHeight, baseHeights, columns, rows, cellSize, westX, northZ,
                        c0, r0, Math.Min(c0 + chunkCells, columns), Math.Min(r0 + chunkCells, rows));
                    if (chunk != null) chunks.Add(chunk);
                }
            }
            return chunks;
        }

        /// <summary>
        /// Coordenadas de textura (u, v seguidos) a escala real: una repetición cada
        /// <paramref name="metersPerTile"/> metros. En los muros u corre a lo largo de la fachada y v es
        /// la altura; en los tejados se proyecta en planta. Los muros no comparten vértices con el tejado.
        /// </summary>
        public static float[] Uvs(BlockMeshChunk chunk, float metersPerTile)
        {
            if (chunk == null) throw new ArgumentNullException(nameof(chunk));
            if (!(metersPerTile > 0f)) throw new ArgumentException("El tamaño de la repetición debe ser positivo.");

            float[] v = chunk.Vertices;
            var uvs = new float[chunk.VertexCount * 2];
            for (int n = 0; n < chunk.VertexCount; n++)
            {
                uvs[n * 2] = v[n * 3] / metersPerTile;
                uvs[n * 2 + 1] = v[n * 3 + 2] / metersPerTile;
            }

            int[] walls = chunk.WallTriangles;
            for (int t = 0; t + 2 < walls.Length; t += 3)
            {
                int a = walls[t], b = walls[t + 1], c = walls[t + 2];
                // Fachada norte-sur (x constante): u sigue a z; si no, sigue a x.
                bool alongZ = v[a * 3] == v[b * 3] && v[b * 3] == v[c * 3];
                for (int k = 0; k < 3; k++)
                {
                    int n = walls[t + k];
                    uvs[n * 2] = (alongZ ? v[n * 3 + 2] : v[n * 3]) / metersPerTile;
                    uvs[n * 2 + 1] = v[n * 3 + 1] / metersPerTile;
                }
            }
            return uvs;
        }

        public static bool IsBuilt(float[] roofs, int columns, int rows, int column, int row)
        {
            return column >= 0 && column < columns && row >= 0 && row < rows && !float.IsNaN(roofs[row * columns + column]);
        }

        /// <summary>
        /// Cota del tejado de la celda (c, r) en la esquina (i, j): media de las celdas construidas que
        /// tocan esa esquina y cuya cota no se aleja de la suya más de <paramref name="step"/>.
        /// </summary>
        private static float CornerTop(float[] roofs, int columns, int rows, float step, int c, int r, int i, int j)
        {
            float own = roofs[r * columns + c];
            float sum = 0f;
            int n = 0;
            for (int dr = -1; dr <= 0; dr++)
            {
                for (int dc = -1; dc <= 0; dc++)
                {
                    if (!IsBuilt(roofs, columns, rows, i + dc, j + dr)) continue;
                    float h = roofs[(j + dr) * columns + (i + dc)];
                    if (Math.Abs(h - own) > step) continue;
                    sum += h;
                    n++;
                }
            }
            return sum / n; // n >= 1: la propia celda siempre cuenta
        }

        private static BlockMeshChunk BuildChunk(float[] roofs, float step, float[] bases, int columns, int rows,
            float cell, float westX, float northZ, int c0, int r0, int c1, int r1)
        {
            int width = c1 - c0;
            // Los vértices de tejado se comparten entre celdas cuando coinciden en esquina y en cota.
            var roofIndex = new Dictionary<long, int>();

            var vertices = new List<float>();
            var roofTriangles = new List<int>();
            var wallTriangles = new List<int>();

            int stride = columns + 1;

            float Top(int c, int r, int i, int j) => CornerTop(roofs, columns, rows, step, c, r, i, j);

            int RoofVertex(int c, int r, int i, int j)
            {
                float y = Top(c, r, i, j);
                long key = ((long)((j - r0) * (width + 1) + (i - c0)) << 32) | (uint)BitConverter.SingleToInt32Bits(y);
                if (roofIndex.TryGetValue(key, out int found)) return found;
                int index = vertices.Count / 3;
                vertices.Add(westX + i * cell);
                vertices.Add(y);
                vertices.Add(northZ - j * cell);
                roofIndex[key] = index;
                return index;
            }

            int Vertex(int i, int j, float y)
            {
                int index = vertices.Count / 3;
                vertices.Add(westX + i * cell);
                vertices.Add(y);
                vertices.Add(northZ - j * cell);
                return index;
            }

            // Muro entre las esquinas A y B (en ese orden, la normal queda hacia fuera: dirección A→B × arriba).
            // (c, r) es la celda dueña del muro y (nc, nr) la vecina de ese lado: si está vacía el muro
            // baja hasta el suelo; si es un volumen más bajo, hasta su tejado; si no, no hay muro.
            void Wall(int c, int r, int nc, int nr, int ai, int aj, int bi, int bj)
            {
                bool open = !IsBuilt(roofs, columns, rows, nc, nr);
                if (!open && !(roofs[r * columns + c] - roofs[nr * columns + nc] > step)) return;
                float aTop = Top(c, r, ai, aj), bTop = Top(c, r, bi, bj);
                float aBase = Math.Min(open ? bases[aj * stride + ai] : Top(nc, nr, ai, aj), aTop);
                float bBase = Math.Min(open ? bases[bj * stride + bi] : Top(nc, nr, bi, bj), bTop);
                int aT = Vertex(ai, aj, aTop), bT = Vertex(bi, bj, bTop);
                int aB = Vertex(ai, aj, aBase), bB = Vertex(bi, bj, bBase);
                wallTriangles.Add(bB); wallTriangles.Add(bT); wallTriangles.Add(aT);
                wallTriangles.Add(bB); wallTriangles.Add(aT); wallTriangles.Add(aB);
            }

            for (int r = r0; r < r1; r++)
            {
                for (int c = c0; c < c1; c++)
                {
                    if (!IsBuilt(roofs, columns, rows, c, r)) continue;

                    int nw = RoofVertex(c, r, c, r), ne = RoofVertex(c, r, c + 1, r);
                    int sw = RoofVertex(c, r, c, r + 1), se = RoofVertex(c, r, c + 1, r + 1);
                    roofTriangles.Add(sw); roofTriangles.Add(nw); roofTriangles.Add(ne);
                    roofTriangles.Add(sw); roofTriangles.Add(ne); roofTriangles.Add(se);

                    Wall(c, r, c, r - 1, c, r, c + 1, r);         // norte: NO → NE
                    Wall(c, r, c + 1, r, c + 1, r, c + 1, r + 1); // este:  NE → SE
                    Wall(c, r, c, r + 1, c + 1, r + 1, c, r + 1); // sur:   SE → SO
                    Wall(c, r, c - 1, r, c, r + 1, c, r);         // oeste: SO → NO
                }
            }

            if (roofTriangles.Count == 0) return null;
            return new BlockMeshChunk
            {
                Vertices = vertices.ToArray(),
                RoofTriangles = roofTriangles.ToArray(),
                WallTriangles = wallTriangles.ToArray()
            };
        }
    }
}
