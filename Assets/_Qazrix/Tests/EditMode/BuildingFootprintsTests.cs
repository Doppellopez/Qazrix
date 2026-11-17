using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Qazrix.Simulation.World;

namespace Qazrix.Tests
{
    /// <summary>Edificios como polígonos de lados rectos (también en diagonal) y esquinas cortadas, a partir de la rejilla del LiDAR.</summary>
    public sealed class BuildingFootprintsTests
    {
        /// <summary>Dibujo de la rejilla: «#» = tejado a 10 m, «+» = tejado a 14 m, «.» = vacío. La primera línea es el norte.</summary>
        private static float[] Grid(out int columns, out int rows, params string[] lines)
        {
            rows = lines.Length;
            columns = lines[0].Length;
            var roofs = new float[columns * rows];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < columns; c++)
                    roofs[r * columns + c] = lines[r][c] == '#' ? 10f : (lines[r][c] == '+' ? 14f : float.NaN);
            return roofs;
        }

        private static List<BuildingPart> Extract(string[] lines, double chamfer = 2.0, double tolerance = 1.0)
        {
            float[] roofs = Grid(out int columns, out int rows, lines);
            return BuildingFootprints.Extract(roofs, columns, rows, 1f, 0f, rows,
                new FootprintOptions { ChamferSize = chamfer, Tolerance = tolerance });
        }

        private static string[] Block(int columns, int rows)
        {
            return Enumerable.Range(0, rows).Select(_ => new string('#', columns)).ToArray();
        }

        private static double RoofArea(BuildingPart part)
        {
            double sum = 0.0;
            for (int t = 0; t + 2 < part.RoofTriangles.Length; t += 3)
            {
                int a = part.RoofTriangles[t] * 2, b = part.RoofTriangles[t + 1] * 2, c = part.RoofTriangles[t + 2] * 2;
                double cross = (part.RoofXZ[b] - part.RoofXZ[a]) * (part.RoofXZ[c + 1] - part.RoofXZ[a + 1])
                    - (part.RoofXZ[b + 1] - part.RoofXZ[a + 1]) * (part.RoofXZ[c] - part.RoofXZ[a]);
                Assert.Greater(cross, 0.0, "los triángulos del tejado salen antihorarios y con área");
                sum += 0.5 * cross;
            }
            return sum;
        }

        [Test]
        public void Rectangle_BecomesOnePolygon_WithItsFourCornersCutDiagonally()
        {
            List<BuildingPart> parts = Extract(Block(12, 8));
            Assert.AreEqual(1, parts.Count);
            BuildingPart part = parts[0];
            Assert.AreEqual(1, part.Loops.Count);
            Assert.AreEqual(8, part.Loops[0].Length, "cuatro lados rectos y cuatro diagonales");
            Assert.Greater(BuildingFootprints.SignedArea(part.Loops[0]), 0.0, "antihorario: el edificio queda a la izquierda");
            Assert.AreEqual(96.0 - 4 * 2.0, part.Area, 0.01, "cada esquina pierde un triángulo de 2 m × 2 m / 2");
            Assert.AreEqual(part.Area, RoofArea(part), 1e-3, "el tejado cubre justo la planta");
            Assert.AreEqual(10f, part.RoofHeight);
            Assert.IsTrue(part.Simplified);
            Assert.IsFalse(part.CellRoof);
        }

        [Test]
        public void WithoutChamfer_TheRectangleKeepsFourSharpCorners()
        {
            BuildingPart part = Extract(Block(12, 8), chamfer: 0.0)[0];
            Assert.AreEqual(4, part.Loops[0].Length);
            Assert.AreEqual(96.0, part.Area, 1e-6);
        }

        [Test]
        public void Chamfers_AreDiagonal_AtFortyFiveDegrees_AndOnlyAtCorners()
        {
            BuildingPart part = Extract(Block(12, 8))[0];
            Vec2[] loop = part.Loops[0];
            int diagonal = 0, straight = 0;
            for (int i = 0; i < loop.Length; i++)
            {
                Vec2 a = loop[i], b = loop[(i + 1) % loop.Length];
                double dx = b.X - a.X, dz = b.Z - a.Z;
                bool axis = Math.Abs(dx) < 1e-9 || Math.Abs(dz) < 1e-9;
                if (axis) straight++;
                else
                {
                    diagonal++;
                    Assert.AreEqual(Math.Abs(dx), Math.Abs(dz), 1e-9, "la diagonal de una esquina recta va a 45°");
                }
            }
            Assert.AreEqual(4, diagonal);
            Assert.AreEqual(4, straight, "los lados quedan enteros: un solo segmento recto por pared");
        }

        [Test]
        public void LShape_CutsItsReentrantCornerToo()
        {
            var lines = new[]
            {
                "#####.....",
                "#####.....",
                "#####.....",
                "#####.....",
                "##########",
                "##########",
                "##########",
                "##########",
                "##########",
                "##########",
            };
            BuildingPart part = Extract(lines)[0];
            Assert.AreEqual(12, part.Loops[0].Length, "seis esquinas, cada una cortada");
            // Area del bloque (5×4 + 10×6 = 80). Cada corte es de 2 m como mucho y el 30 % del lado más corto: 2; 1,8; 1,5;
            // 1,2 y 1,5 m en las cinco esquinas salientes (1/2 de su cuadrado cada una) y 1,2 m en la entrante, que añade área.
            double cuts = 0.5 * (2.0 * 2.0 + 1.8 * 1.8 + 1.5 * 1.5 + 1.2 * 1.2 + 1.5 * 1.5);
            Assert.AreEqual(80.0 - cuts + 0.5 * 1.2 * 1.2, part.Area, 0.05);
            Assert.AreEqual(part.Area, RoofArea(part), 1e-3);
        }

        [Test]
        public void Courtyard_BecomesAHole_AndTheRoofAvoidsIt()
        {
            var lines = Block(14, 14);
            for (int r = 5; r < 9; r++) lines[r] = lines[r].Substring(0, 5) + "...." + lines[r].Substring(9);
            BuildingPart part = Extract(lines)[0];
            Assert.AreEqual(2, part.Loops.Count, "contorno exterior y un patio");
            Assert.Less(BuildingFootprints.SignedArea(part.Loops[1]), 0.0, "el patio va en sentido horario");
            Assert.IsFalse(part.CellRoof, "el tejado se triangula alrededor del patio");
            Assert.AreEqual(part.Area, RoofArea(part), 1e-3, "y no lo cubre");
            Assert.Less(part.Area, 196.0 - 16.0 + 0.01);
        }

        [Test]
        public void JaggedWall_BecomesOneStraightWall_WithNoNotchesInTheMiddle()
        {
            // 20 × 6, con la pared sur en dientes de sierra de 1 m de fondo.
            var lines = Block(20, 6);
            var teeth = lines[5].ToCharArray();
            for (int c = 1; c < 20; c += 2) teeth[c] = '.';
            lines[5] = new string(teeth);
            BuildingPart part = Extract(lines, tolerance: 1.2)[0];
            Assert.LessOrEqual(part.Loops[0].Length, 12, $"la pared dentada debe quedar en un solo lado recto (quedaron {part.Loops[0].Length} vértices)");
            Assert.IsTrue(part.Simplified);
        }

        [Test]
        public void ASmallJogInAWall_IsMergedIntoOneLongWall()
        {
            // 20 × 6 con la pared sur desplazada 1 m a la mitad: un peldaño de 1 m que no debe quedar como lado propio.
            var lines = Block(20, 6);
            lines[5] = new string('#', 10) + new string('.', 10);
            BuildingPart part = Extract(lines, chamfer: 0.0, tolerance: 1.5)[0];
            Assert.AreEqual(4, part.Loops[0].Length, $"cuatro lados rectos, sin peldaño (quedaron {part.Loops[0].Length} vértices)");
            double shortest = double.MaxValue;
            Vec2[] loop = part.Loops[0];
            for (int i = 0; i < loop.Length; i++)
            {
                Vec2 a = loop[i], b = loop[(i + 1) % loop.Length];
                shortest = Math.Min(shortest, Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z)));
            }
            Assert.Greater(shortest, 4.9, "ningún lado queda corto");
        }

        [Test]
        public void MergeShortEdges_KeepsRealCorners()
        {
            // Un cuadrado de 10 m no tiene lados cortos: no cambia. Un lado de 1 m en una pared recta sí se funde.
            var square = new[] { new Vec2(0, 0), new Vec2(10, 0), new Vec2(10, 10), new Vec2(0, 10) };
            Assert.AreEqual(4, BuildingFootprints.MergeShortEdges(square, 2.0, 1.5).Length);
            var jog = new[] { new Vec2(0, 0), new Vec2(10, 0), new Vec2(10, 1), new Vec2(20, 1), new Vec2(20, 10), new Vec2(0, 10) };
            Assert.AreEqual(5, BuildingFootprints.MergeShortEdges(jog, 2.0, 1.5).Length);
            // Con una desviación máxima menor que el peldaño, no se toca.
            Assert.AreEqual(6, BuildingFootprints.MergeShortEdges(jog, 2.0, 0.2).Length);
        }

        [Test]
        public void ShortWalls_KeepTheirCornersSharp_SoNoSliversAppearBetweenCuts()
        {
            // Un bloque de 3 × 3 m: sus lados son menores que MinCornerEdge y no se cortan.
            BuildingPart small = Extract(Block(3, 3))[0];
            Assert.AreEqual(4, small.Loops[0].Length);

            // En un edificio grande todos los lados que quedan entre dos cortes miden más de 1 m.
            Vec2[] loop = Extract(Block(14, 9))[0].Loops[0];
            for (int i = 0; i < loop.Length; i++)
            {
                Vec2 a = loop[i], b = loop[(i + 1) % loop.Length];
                Assert.Greater(Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z)), 1.0, $"lado {i} demasiado corto");
            }
        }

        [Test]
        public void StaircaseDiagonal_BecomesAStraightDiagonalWall()
        {
            // Triángulo rectángulo de 12 × 12 cuya hipotenusa es una escalera de peldaños de 1 m.
            var lines = new string[12];
            for (int r = 0; r < 12; r++) lines[r] = new string('#', 12 - r) + new string('.', r);
            BuildingPart part = Extract(lines)[0];
            Assert.LessOrEqual(part.Loops[0].Length, 10, $"tres esquinas cortadas, no una escalera (quedaron {part.Loops[0].Length} vértices)");

            bool found = false;
            Vec2[] loop = part.Loops[0];
            for (int i = 0; i < loop.Length; i++)
            {
                Vec2 a = loop[i], b = loop[(i + 1) % loop.Length];
                double dx = b.X - a.X, dz = b.Z - a.Z, length = Math.Sqrt(dx * dx + dz * dz);
                double angle = Math.Abs(Math.Atan2(dz, dx) * 180.0 / Math.PI) % 90.0;
                if (length > 10.0 && Math.Abs(angle - 45.0) < 3.0) found = true;
            }
            Assert.IsTrue(found, "la hipotenusa es un único muro en diagonal de más de 10 m");
        }

        [Test]
        public void BlocksTouchingOnlyAtACorner_AreSeparateBuildings()
        {
            var lines = new[]
            {
                "####....",
                "####....",
                "####....",
                "####....",
                "....####",
                "....####",
                "....####",
                "....####",
            };
            List<BuildingPart> parts = Extract(lines, chamfer: 0.0);
            Assert.AreEqual(2, parts.Count);
            foreach (BuildingPart part in parts)
            {
                Assert.AreEqual(4, part.Loops[0].Length);
                Assert.AreEqual(16.0, part.Area, 1e-6);
                Assert.IsTrue(BuildingFootprints.IsSimple(part.Loops));
            }
        }

        [Test]
        public void DifferentRoofHeights_AreDifferentBuildings()
        {
            var lines = new[] { "####++++", "####++++", "####++++" };
            List<BuildingPart> parts = Extract(lines, chamfer: 0.0);
            Assert.AreEqual(2, parts.Count);
            CollectionAssert.AreEquivalent(new[] { 10f, 14f }, parts.Select(p => p.RoofHeight));
        }

        [Test]
        public void ARandomishCity_GivesOnlySimplePolygons_ThatTriangulateExactly()
        {
            // Manchas deterministas con bordes irregulares, de varios tamaños, con patios y salientes.
            const int size = 90;
            var roofs = new float[size * size];
            uint seed = 12345u;
            float Next() { seed = seed * 1664525u + 1013904223u; return (seed >> 8) / (float)(1 << 24); }
            for (int i = 0; i < roofs.Length; i++) roofs[i] = float.NaN;
            for (int b = 0; b < 40; b++)
            {
                int w = 4 + (int)(Next() * 14), h = 4 + (int)(Next() * 14);
                int x = (int)(Next() * (size - w - 1)), y = (int)(Next() * (size - h - 1));
                float height = Next() > 0.5f ? 10f : 14f;
                for (int r = y; r < y + h; r++)
                    for (int c = x; c < x + w; c++)
                    {
                        bool edge = r == y || c == x || r == y + h - 1 || c == x + w - 1;
                        if (edge && Next() < 0.25f) continue; // borde dentado
                        roofs[r * size + c] = height;
                    }
                if (Next() > 0.6f && w > 8 && h > 8) // patio
                    for (int r = y + 3; r < y + h - 3; r++)
                        for (int c = x + 3; c < x + w - 3; c++)
                            roofs[r * size + c] = float.NaN;
            }

            List<BuildingPart> parts = BuildingFootprints.Extract(roofs, size, size, 1f, 0f, size, new FootprintOptions());
            Assert.Greater(parts.Count, 10);
            double polygonArea = 0.0;
            int cellRoofs = 0;
            foreach (BuildingPart part in parts)
            {
                Assert.IsTrue(BuildingFootprints.IsSimple(part.Loops), "los contornos no se cruzan ni se tocan");
                Assert.Greater(BuildingFootprints.SignedArea(part.Loops[0]), 0.0, "el contorno exterior es antihorario");
                for (int h = 1; h < part.Loops.Count; h++) Assert.Less(BuildingFootprints.SignedArea(part.Loops[h]), 0.0, "los patios son horarios");
                if (part.CellRoof) { cellRoofs++; continue; }
                Assert.AreEqual(part.Area, RoofArea(part), 1e-3 * Math.Max(1.0, part.Area), "el tejado cubre justo la planta");
                polygonArea += part.Area;
            }
            Assert.LessOrEqual(cellRoofs, parts.Count / 20, "casi ningún edificio necesita el relleno por celdas");

            int built = roofs.Count(h => !float.IsNaN(h));
            Assert.That(polygonArea, Is.InRange(built * 0.8, built * 1.05), "enderezar y cortar esquinas no se come ni inventa edificios");
        }

        [Test]
        public void Mesher_RaisesWallsFacingOutwards_AndRoofsFacingUp()
        {
            BuildingPart part = Extract(Block(12, 8))[0];
            List<FootprintChunk> chunks = FootprintMesher.Build(new List<BuildingPart> { part }, (x, z) => 0f, 1f, 48.0, 3f);
            Assert.AreEqual(1, chunks.Count);
            FootprintChunk chunk = chunks[0];

            for (int t = 0; t + 2 < chunk.RoofTriangles.Length; t += 3)
            {
                float[] n = Normal(chunk, chunk.RoofTriangles, t);
                Assert.Greater(n[1], 0.99f, "el tejado mira hacia arriba");
            }

            Assert.Greater(chunk.WallTriangles.Length, 0);
            float centreX = (float)part.Centre.X, centreZ = (float)part.Centre.Z;
            for (int t = 0; t + 2 < chunk.WallTriangles.Length; t += 3)
            {
                float[] n = Normal(chunk, chunk.WallTriangles, t);
                Assert.AreEqual(0f, n[1], 1e-3f, "los muros son verticales");
                float mx = 0f, mz = 0f;
                for (int k = 0; k < 3; k++)
                {
                    mx += chunk.Vertices[chunk.WallTriangles[t + k] * 3] / 3f;
                    mz += chunk.Vertices[chunk.WallTriangles[t + k] * 3 + 2] / 3f;
                }
                Assert.Greater((mx - centreX) * n[0] + (mz - centreZ) * n[2], 0f, "los muros miran hacia fuera del edificio");
            }

            // El pie de muro baja bajo el terreno (cota 0, hundido 1 m).
            float lowest = float.MaxValue, highest = float.MinValue;
            for (int i = 0; i < chunk.VertexCount; i++)
            {
                lowest = Math.Min(lowest, chunk.Vertices[i * 3 + 1]);
                highest = Math.Max(highest, chunk.Vertices[i * 3 + 1]);
            }
            Assert.AreEqual(-1f, lowest, 1e-4f);
            Assert.AreEqual(10f, highest, 1e-4f);
            Assert.AreEqual(chunk.VertexCount * 2, chunk.Uvs.Length);
        }

        private static float[] Normal(FootprintChunk chunk, int[] triangles, int t)
        {
            int a = triangles[t] * 3, b = triangles[t + 1] * 3, c = triangles[t + 2] * 3;
            float ux = chunk.Vertices[b] - chunk.Vertices[a], uy = chunk.Vertices[b + 1] - chunk.Vertices[a + 1], uz = chunk.Vertices[b + 2] - chunk.Vertices[a + 2];
            float vx = chunk.Vertices[c] - chunk.Vertices[a], vy = chunk.Vertices[c + 1] - chunk.Vertices[a + 1], vz = chunk.Vertices[c + 2] - chunk.Vertices[a + 2];
            float nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
            float length = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
            return new[] { nx / length, ny / length, nz / length };
        }

        [Test]
        public void LongWalls_AreSplitIntoSegments_SoTheyFollowTheGround()
        {
            BuildingPart part = Extract(Block(30, 6), chamfer: 0.0)[0];
            // Terreno en rampa de oeste a este: el pie del muro sube con él.
            List<FootprintChunk> chunks = FootprintMesher.Build(new List<BuildingPart> { part }, (x, z) => (float)x * 0.1f, 0f, 100.0, 3f, 3.0);
            FootprintChunk chunk = chunks[0];
            int wallQuads = chunk.WallTriangles.Length / 6;
            Assert.GreaterOrEqual(wallQuads, 2 * (30 / 3) + 2 * 2, "los lados largos se parten en tramos de 3 m como mucho");
            bool followsRamp = false;
            for (int i = 0; i < chunk.VertexCount; i++)
                if (chunk.Vertices[i * 3] > 29f && chunk.Vertices[i * 3 + 1] < 9.9f && chunk.Vertices[i * 3 + 1] > 2.8f) followsRamp = true;
            Assert.IsTrue(followsRamp, "en el extremo alto de la rampa el pie del muro está más alto");
        }

        [Test]
        public void Openings_FollowDiagonalWalls_WithUnitNormalsPointingOutwards()
        {
            // Esquinas cortadas a 4 m: las diagonales miden casi 5,7 m y admiten un hueco.
            BuildingPart part = Extract(Block(20, 14), chamfer: 4.0)[0];
            var parts = new List<BuildingPart> { part };
            List<FacadeOpening> openings = FootprintOpenings.Place(parts, (x, z) => 0f, new FacadeOpeningRules());
            Assert.Greater(openings.Count, 8);

            bool diagonal = false;
            foreach (FacadeOpening o in openings)
            {
                Assert.AreEqual(1f, Math.Sqrt(o.NormalX * o.NormalX + o.NormalZ * o.NormalZ), 1e-4, "normal unitaria");
                Assert.Greater((o.X - part.Centre.X) * o.NormalX + (o.Z - part.Centre.Z) * o.NormalZ, 0.0, "el hueco mira hacia fuera");
                if (Math.Abs(o.NormalX) > 0.5f && Math.Abs(o.NormalZ) > 0.5f) diagonal = true;
            }
            Assert.IsTrue(diagonal, "hay huecos sobre las paredes en diagonal");
        }

        [Test]
        public void ShortChamferWalls_DoNotGetOpenings()
        {
            BuildingPart part = Extract(Block(20, 14), chamfer: 2.0)[0];
            foreach (FacadeOpening o in FootprintOpenings.Place(new List<BuildingPart> { part }, (x, z) => 0f, new FacadeOpeningRules()))
                Assert.IsFalse(Math.Abs(o.NormalX) > 0.5f && Math.Abs(o.NormalZ) > 0.5f, "una diagonal de 2,8 m no lleva hueco");
        }

        [Test]
        public void Index_FindsTheBuilding_ButNotTheCourtyardNorTheStreet()
        {
            var lines = Block(14, 14);
            for (int r = 5; r < 9; r++) lines[r] = lines[r].Substring(0, 5) + "...." + lines[r].Substring(9);
            var parts = Extract(lines);
            var index = new FootprintIndex(parts);
            Assert.IsTrue(index.Contains(2.0, 7.0), "dentro del edificio");
            Assert.IsFalse(index.Contains(7.0, 7.0), "en el patio");
            Assert.IsFalse(index.Contains(30.0, 30.0), "en la calle");
            Assert.IsFalse(index.Contains(0.3, 0.3), "en la esquina cortada en diagonal");
            Assert.IsTrue(index.Contains(1.5, 1.5), "justo tras el corte de la esquina");
            Assert.AreEqual(10f, index.At(2.0, 7.0).RoofHeight);
        }

        [Test]
        public void InvalidInput_IsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => BuildingFootprints.Extract(null, 1, 1, 1f, 0f, 0f));
            Assert.Throws<ArgumentException>(() => BuildingFootprints.Extract(new float[3], 2, 2, 1f, 0f, 0f));
            Assert.Throws<ArgumentException>(() => BuildingFootprints.Extract(new float[4], 2, 2, 0f, 0f, 0f));
            Assert.Throws<ArgumentNullException>(() => FootprintMesher.Build(null, (x, z) => 0f, 1f, 10.0, 3f));
            Assert.Throws<ArgumentException>(() => FootprintMesher.Build(new List<BuildingPart>(), (x, z) => 0f, 1f, 0.0, 3f));
        }
    }
}
