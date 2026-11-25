using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Qazrix.Simulation.World;

namespace Qazrix.Tests
{
    /// <summary>Murallas infranqueables: barrera invisible sobre su tejado para que no se salven ni se escalen.</summary>
    public sealed class WallBarrierTests
    {
        private static BuildingPart Wall(float roof = 12f)
        {
            var part = new BuildingPart { RoofHeight = roof };
            part.Loops.Add(new[] { new Vec2(0, 0), new Vec2(30, 0), new Vec2(30, 2), new Vec2(0, 2) });
            return part;
        }

        [Test]
        public void EachSideGetsFourVerticesAndTwoFaces()
        {
            WallBarrierMesh mesh = WallBarrier.Build(new[] { Wall() });
            Assert.AreEqual(4 * 4, mesh.VertexCount);
            Assert.AreEqual(4 * 12, mesh.Triangles.Length, "dos caras de dos triángulos por lado");
        }

        [Test]
        public void ItRisesFromTheRoofEdgeUpwards()
        {
            WallBarrierMesh mesh = WallBarrier.Build(new[] { Wall(12f) });
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < mesh.VertexCount; i++)
            {
                float y = mesh.Vertices[i * 3 + 1];
                min = System.Math.Min(min, y);
                max = System.Math.Max(max, y);
            }
            Assert.AreEqual(12f, min, 1e-4f, "empieza en el tejado de la muralla");
            Assert.AreEqual(12f + WallBarrier.Height, max, 1e-4f);
            Assert.GreaterOrEqual(WallBarrier.Height, 30f, "demasiado alto para saltarlo");
        }

        [Test]
        public void ItCollidesFromBothSides()
        {
            WallBarrierMesh mesh = WallBarrier.Build(new[] { Wall() });
            // Para cada triángulo hay otro con los mismos vértices en sentido contrario.
            var seen = new HashSet<string>();
            for (int t = 0; t < mesh.Triangles.Length; t += 3)
            {
                int a = mesh.Triangles[t], b = mesh.Triangles[t + 1], c = mesh.Triangles[t + 2];
                seen.Add($"{a},{b},{c}");
            }
            for (int t = 0; t < mesh.Triangles.Length; t += 3)
            {
                int a = mesh.Triangles[t], b = mesh.Triangles[t + 1], c = mesh.Triangles[t + 2];
                Assert.IsTrue(seen.Contains($"{a},{c},{b}"), $"triángulo {t / 3} sin su cara contraria");
            }
        }

        [Test]
        public void CoversEveryWallAndEveryInnerCourtyard()
        {
            BuildingPart withCourtyard = Wall();
            withCourtyard.Loops.Add(new[] { new Vec2(10, 0.5), new Vec2(10, 1.5), new Vec2(12, 1.5), new Vec2(12, 0.5) });
            WallBarrierMesh mesh = WallBarrier.Build(new[] { withCourtyard, Wall(20f) });
            Assert.AreEqual((4 + 4 + 4) * 4, mesh.VertexCount, "contorno exterior, patio y la segunda muralla");
        }

        [Test]
        public void NoWallsMeansAnEmptyMesh()
        {
            WallBarrierMesh mesh = WallBarrier.Build(Enumerable.Empty<BuildingPart>());
            Assert.AreEqual(0, mesh.VertexCount);
            Assert.AreEqual(0, mesh.Triangles.Length);
        }

        [Test]
        public void BadArgumentsAreRejected()
        {
            Assert.Throws<System.ArgumentNullException>(() => WallBarrier.Build(null));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => WallBarrier.Build(new[] { Wall() }, 0f));
        }
    }
}
