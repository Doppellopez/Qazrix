using System.Collections.Generic;
using NUnit.Framework;
using Qazrix.Simulation.World;

namespace Qazrix.Tests
{
    /// <summary>Pasadizo largo bajo la ciudad: tubo de suelo, paredes y techo que sigue una poligonal.</summary>
    public sealed class TunnelTests
    {
        private static List<Vec2> Straight(double length = 20) => new List<Vec2> { new Vec2(0, 0), new Vec2(0, length) };

        private static float[] FaceNormal(TunnelMesh m, int t)
        {
            int a = m.Triangles[t], b = m.Triangles[t + 1], c = m.Triangles[t + 2];
            float ux = m.Vertices[b * 3] - m.Vertices[a * 3], uy = m.Vertices[b * 3 + 1] - m.Vertices[a * 3 + 1], uz = m.Vertices[b * 3 + 2] - m.Vertices[a * 3 + 2];
            float vx = m.Vertices[c * 3] - m.Vertices[a * 3], vy = m.Vertices[c * 3 + 1] - m.Vertices[a * 3 + 1], vz = m.Vertices[c * 3 + 2] - m.Vertices[a * 3 + 2];
            return new[] { uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx }; // como Vector3.Cross: la cara vista de Unity
        }

        [Test]
        public void ASegmentHasFloorCeilingAndTwoWalls()
        {
            TunnelMesh m = TunnelMesher.Build(Straight(), -100);
            Assert.AreEqual(4 * 4, m.VertexCount, "cuatro caras de cuatro vértices");
            Assert.AreEqual(4 * 6, m.Triangles.Length);
            Assert.AreEqual(20f, m.Length, 1e-4f);
        }

        [Test]
        public void ItSpansFromTheFloorToTheCeilingAtTheGivenDepth()
        {
            TunnelMesh m = TunnelMesher.Build(Straight(), -100, 3.0, 2.8);
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < m.VertexCount; i++) { min = System.Math.Min(min, m.Vertices[i * 3 + 1]); max = System.Math.Max(max, m.Vertices[i * 3 + 1]); }
            Assert.AreEqual(-100f, min, 1e-4f);
            Assert.AreEqual(-97.2f, max, 1e-4f);
        }

        [Test]
        public void ItIsAsWideAsAsked()
        {
            TunnelMesh m = TunnelMesher.Build(Straight(), 0, 3.5);
            float minX = float.MaxValue, maxX = float.MinValue;
            for (int i = 0; i < m.VertexCount; i++) { minX = System.Math.Min(minX, m.Vertices[i * 3]); maxX = System.Math.Max(maxX, m.Vertices[i * 3]); }
            Assert.AreEqual(3.5f, maxX - minX, 1e-4f);
        }

        [Test]
        public void EveryVisibleFaceLooksInwards()
        {
            // Un tramo recto hacia el norte: suelo hacia arriba, techo hacia abajo y paredes hacia el eje.
            TunnelMesh m = TunnelMesher.Build(Straight(), 0, 3, 3);
            int up = 0, down = 0, towardsAxis = 0;
            for (int t = 0; t < m.Triangles.Length; t += 3)
            {
                float[] n = FaceNormal(m, t);
                int a = m.Triangles[t];
                float x = m.Vertices[a * 3];
                if (n[1] > 0.1f) up++;
                else if (n[1] < -0.1f) down++;
                else if (x * n[0] < 0f) towardsAxis++; // la pared a la izquierda (x<0) mira a +x y la de la derecha, a −x
                else Assert.Fail($"pared de x={x} mirando hacia fuera");
            }
            Assert.AreEqual(2, up, "suelo");
            Assert.AreEqual(2, down, "techo");
            Assert.AreEqual(4, towardsAxis, "dos paredes de dos triángulos");
        }

        [Test]
        public void TheColliderHasBothSidesOfEveryFace()
        {
            TunnelMesh m = TunnelMesher.Build(Straight(), 0);
            Assert.AreEqual(m.Triangles.Length * 2, m.ColliderTriangles.Length);
        }

        [Test]
        public void ABendIsMitredWithoutGaps()
        {
            // Giro de 90° a la izquierda: el borde interior se acorta y el exterior se alarga lo mismo que el semiancho.
            var path = new List<Vec2> { new Vec2(0, 0), new Vec2(0, 10), new Vec2(-10, 10) };
            TunnelMesh m = TunnelMesher.Build(path, 0, 2.0);
            Assert.AreEqual(8 * 4, m.VertexCount, "dos tramos");
            Assert.AreEqual(20f, m.Length, 1e-4f);
            // En el vértice de la curva las caras de los dos tramos comparten posición (sin hueco): el borde interior (izquierdo) en (−1, 9).
            bool found = false;
            for (int i = 0; i < m.VertexCount; i++)
                if (System.Math.Abs(m.Vertices[i * 3] - -1f) < 1e-3f && System.Math.Abs(m.Vertices[i * 3 + 2] - 9f) < 1e-3f) found = true;
            Assert.IsTrue(found, "el inglete interior queda en (−1, 9)");
            bool outer = false;
            for (int i = 0; i < m.VertexCount; i++)
                if (System.Math.Abs(m.Vertices[i * 3] - 1f) < 1e-3f && System.Math.Abs(m.Vertices[i * 3 + 2] - 11f) < 1e-3f) outer = true;
            Assert.IsTrue(outer, "el exterior queda en (1, 11)");
        }

        [Test]
        public void RepeatedPointsAreIgnored()
        {
            var path = new List<Vec2> { new Vec2(0, 0), new Vec2(0, 0), new Vec2(0, 10) };
            Assert.AreEqual(10f, TunnelMesher.Build(path, 0).Length, 1e-4f);
        }

        [Test]
        public void BadPathsAreRejected()
        {
            Assert.Throws<System.ArgumentNullException>(() => TunnelMesher.Build(null, 0));
            Assert.Throws<System.ArgumentException>(() => TunnelMesher.Build(new List<Vec2> { new Vec2(0, 0) }, 0));
            Assert.Throws<System.ArgumentException>(() => TunnelMesher.Build(new List<Vec2> { new Vec2(1, 1), new Vec2(1, 1) }, 0));
        }
    }
}
