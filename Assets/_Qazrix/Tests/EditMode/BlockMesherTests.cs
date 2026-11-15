using System.Collections.Generic;
using NUnit.Framework;
using Qazrix.Simulation.World;

namespace Qazrix.Tests
{
    /// <summary>Volumetría de edificios a partir de la rejilla de tejados (spec §5.3, capa B).</summary>
    public sealed class BlockMesherTests
    {
        private const float N = float.NaN;

        private static float[] Bases(int columns, int rows, float value = 0f)
        {
            var b = new float[(columns + 1) * (rows + 1)];
            for (int i = 0; i < b.Length; i++) b[i] = value;
            return b;
        }

        /// <summary>Normal de un triángulo con el convenio de Unity (cara vista en sentido horario).</summary>
        private static (float x, float y, float z) Normal(BlockMeshChunk chunk, int[] triangles, int t)
        {
            float[] v = chunk.Vertices;
            int a = triangles[t * 3] * 3, b = triangles[t * 3 + 1] * 3, c = triangles[t * 3 + 2] * 3;
            float ux = v[b] - v[a], uy = v[b + 1] - v[a + 1], uz = v[b + 2] - v[a + 2];
            float wx = v[c] - v[a], wy = v[c + 1] - v[a + 1], wz = v[c + 2] - v[a + 2];
            return (uy * wz - uz * wy, uz * wx - ux * wz, ux * wy - uy * wx);
        }

        [Test]
        public void SingleCell_IsABox_RoofUp_WallsOutwards()
        {
            // Una celda de 1 m con tejado a 5 m, en x de 10 a 11 y z de 19 a 20.
            List<BlockMeshChunk> chunks = BlockMesher.Build(new[] { 5f }, 1, 1, 1f, 10f, 20f, Bases(1, 1), 16);
            Assert.AreEqual(1, chunks.Count);
            BlockMeshChunk box = chunks[0];

            Assert.AreEqual(2, box.RoofTriangles.Length / 3, "un cuadrado de tejado");
            Assert.AreEqual(8, box.WallTriangles.Length / 3, "cuatro muros");

            for (int t = 0; t < 2; t++)
                Assert.Greater(Normal(box, box.RoofTriangles, t).y, 0f, "el tejado mira hacia arriba");

            // Cada muro mira hacia fuera: su normal apunta lejos del centro de la celda (10,5; 19,5).
            for (int t = 0; t < 8; t++)
            {
                (float nx, float ny, float nz) = Normal(box, box.WallTriangles, t);
                int a = box.WallTriangles[t * 3] * 3;
                float outX = box.Vertices[a] - 10.5f, outZ = box.Vertices[a + 2] - 19.5f;
                Assert.AreEqual(0f, ny, 1e-5f, "los muros son verticales");
                Assert.Greater(nx * outX + nz * outZ, 0f, $"el triángulo de muro {t} mira hacia dentro");
            }
        }

        [Test]
        public void Walls_ReachDownToTheBase()
        {
            BlockMeshChunk box = BlockMesher.Build(new[] { 5f }, 1, 1, 1f, 0f, 0f, Bases(1, 1, -1.5f), 16)[0];
            float min = float.MaxValue, max = float.MinValue;
            for (int v = 1; v < box.Vertices.Length; v += 3)
            {
                if (box.Vertices[v] < min) min = box.Vertices[v];
                if (box.Vertices[v] > max) max = box.Vertices[v];
            }
            Assert.AreEqual(-1.5f, min, 1e-5f, "el pie del muro baja hasta la cota de base");
            Assert.AreEqual(5f, max, 1e-5f);
        }

        [Test]
        public void AdjacentCells_ShareNoWallBetweenThem()
        {
            // Dos celdas juntas (oeste-este): perímetro de 6 lados, no 8.
            BlockMeshChunk block = BlockMesher.Build(new[] { 5f, 5f }, 2, 1, 1f, 0f, 0f, Bases(2, 1), 16)[0];
            Assert.AreEqual(4, block.RoofTriangles.Length / 3);
            Assert.AreEqual(12, block.WallTriangles.Length / 3);
        }

        [Test]
        public void Street_BetweenTwoBuildings_StaysOpen()
        {
            // Edificio, calle, edificio: en la celda del medio no hay tejado y cada lado tiene su muro.
            List<BlockMeshChunk> chunks = BlockMesher.Build(new[] { 6f, N, 6f }, 3, 1, 1f, 0f, 0f, Bases(3, 1), 16);
            BlockMeshChunk block = chunks[0];
            Assert.AreEqual(4, block.RoofTriangles.Length / 3, "solo dos celdas con tejado");
            Assert.AreEqual(16, block.WallTriangles.Length / 3, "cada edificio conserva sus cuatro muros");

            // Ningún vértice de tejado queda dentro de la calle (x entre 1 y 2, sin contar los bordes).
            for (int t = 0; t < block.RoofTriangles.Length; t++)
            {
                float x = block.Vertices[block.RoofTriangles[t] * 3];
                Assert.IsFalse(x > 1.001f && x < 1.999f);
            }
        }

        [Test]
        public void Step_BetweenTwoBuildings_KeepsEachRoofFlat_AndAddsAWall()
        {
            // Dos edificios pegados a 4 m y 8 m con salto de 0,5 m: cada tejado en su cota y un muro
            // entre ambos que va de 8 m a 4 m (no hasta el suelo).
            BlockMeshChunk block = BlockMesher.Build(new[] { 4f, 8f }, 2, 1, 1f, 0f, 0f, Bases(2, 1), 16, 0.5f)[0];
            Assert.AreEqual(4, block.RoofTriangles.Length / 3);
            Assert.AreEqual(14, block.WallTriangles.Length / 3, "seis muros exteriores y el escalón");

            foreach (int index in block.RoofTriangles)
            {
                float x = block.Vertices[index * 3], y = block.Vertices[index * 3 + 1];
                if (x < 0.999f) Assert.AreEqual(4f, y, 1e-5f);
                if (x > 1.001f) Assert.AreEqual(8f, y, 1e-5f);
                Assert.IsTrue(System.Math.Abs(y - 4f) < 1e-5f || System.Math.Abs(y - 8f) < 1e-5f, "ningún tejado a media altura");
            }

            // El escalón está en x = 1: sus vértices van de 4 m a 8 m y mira hacia el edificio bajo (oeste).
            int stepTriangles = 0;
            for (int t = 0; t < block.WallTriangles.Length / 3; t++)
            {
                bool onStep = true;
                float low = float.MaxValue;
                for (int k = 0; k < 3; k++)
                {
                    int v = block.WallTriangles[t * 3 + k] * 3;
                    onStep &= System.Math.Abs(block.Vertices[v] - 1f) < 1e-5f;
                    low = System.Math.Min(low, block.Vertices[v + 1]);
                }
                if (!onStep) continue;
                stepTriangles++;
                Assert.AreEqual(4f, low, 1e-5f, "el escalón baja hasta el tejado vecino, no hasta el suelo");
                Assert.Less(Normal(block, block.WallTriangles, t).x, 0f, "el escalón mira hacia el edificio bajo");
            }
            Assert.AreEqual(2, stepTriangles);
        }

        [Test]
        public void SmallDifferences_BelowTheStep_StayOneSmoothRoof()
        {
            BlockMeshChunk block = BlockMesher.Build(new[] { 4f, 4.2f }, 2, 1, 1f, 0f, 0f, Bases(2, 1), 16, 0.5f)[0];
            Assert.AreEqual(12, block.WallTriangles.Length / 3, "sin escalón entre ambas");
        }

        [Test]
        public void RoofHeights_AreSmoothedAtSharedCorners()
        {
            // Dos celdas a 4 m y 8 m: la arista común queda a 6 m, sin escalón ni hueco.
            BlockMeshChunk block = BlockMesher.Build(new[] { 4f, 8f }, 2, 1, 1f, 0f, 0f, Bases(2, 1), 16)[0];
            var heightsAtSharedEdge = new List<float>();
            foreach (int index in block.RoofTriangles)
                if (System.Math.Abs(block.Vertices[index * 3] - 1f) < 1e-5f)
                    heightsAtSharedEdge.Add(block.Vertices[index * 3 + 1]);
            Assert.IsNotEmpty(heightsAtSharedEdge);
            foreach (float h in heightsAtSharedEdge) Assert.AreEqual(6f, h, 1e-5f);
        }

        [Test]
        public void Grid_IsSplitIntoChunks_AndEmptyOnesAreSkipped()
        {
            // 4 × 2 celdas, trozos de 2: cuatro trozos posibles, pero uno no tiene edificio.
            var roofs = new[]
            {
                5f, 5f, N, N,
                5f, 5f, 5f, 5f
            };
            List<BlockMeshChunk> chunks = BlockMesher.Build(roofs, 4, 2, 1f, 0f, 0f, Bases(4, 2), 2);
            Assert.AreEqual(1 + 1, chunks.Count, "trozo oeste completo y trozo este solo con la fila sur");

            roofs = new[] { N, N, N, N, N, N, N, N };
            Assert.IsEmpty(BlockMesher.Build(roofs, 4, 2, 1f, 0f, 0f, Bases(4, 2), 2));
        }

        [Test]
        public void Rows_RunNorthToSouth()
        {
            // 1 columna × 2 filas, solo la primera fila (norte) construida, con borde norte en z = 100.
            BlockMeshChunk block = BlockMesher.Build(new[] { 5f, N }, 1, 2, 1f, 0f, 100f, Bases(1, 2), 16)[0];
            foreach (int index in block.RoofTriangles)
                Assert.GreaterOrEqual(block.Vertices[index * 3 + 2], 99f - 1e-5f, "el edificio está en la fila norte (z de 99 a 100)");
        }

        [Test]
        public void BadInput_IsRejected()
        {
            Assert.Throws<System.ArgumentException>(() => BlockMesher.Build(new float[3], 2, 2, 1f, 0f, 0f, Bases(2, 2), 16));
            Assert.Throws<System.ArgumentException>(() => BlockMesher.Build(new float[4], 2, 2, 1f, 0f, 0f, new float[4], 16));
        }
    }
}
