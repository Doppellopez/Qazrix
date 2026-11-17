using System;
using NUnit.Framework;
using Qazrix.Simulation.World;
using UnityEngine;

namespace Qazrix.Tests
{
    /// <summary>Mallas de alta resolución de los personajes (ART-11): mismo tamaño que las primitivas de Unity, 10 veces los triángulos.</summary>
    public sealed class PrimitiveGeometryTests
    {
        private static MeshData Build(PrimitiveType type)
        {
            switch (type)
            {
                case PrimitiveType.Sphere: return PrimitiveGeometry.Sphere();
                case PrimitiveType.Capsule: return PrimitiveGeometry.Capsule();
                case PrimitiveType.Cylinder: return PrimitiveGeometry.Cylinder();
                default: return PrimitiveGeometry.Box();
            }
        }

        /// <summary>Malla de la primitiva que usa <c>CreatePrimitive</c> (es la que sustituimos).</summary>
        private static Mesh Reference(PrimitiveType type)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            try { return go.GetComponent<MeshFilter>().sharedMesh; }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        private static readonly PrimitiveType[] Types =
            { PrimitiveType.Sphere, PrimitiveType.Capsule, PrimitiveType.Cylinder, PrimitiveType.Cube };

        [TestCaseSource(nameof(Types))]
        public void HasAtLeastTenTimesTheTrianglesOfUnitysPrimitive(PrimitiveType type)
        {
            Mesh original = Reference(type);
            Assert.IsNotNull(original, $"no se encontró la primitiva {type} de Unity");
            int originalTriangles = original.triangles.Length / 3;
            MeshData ours = Build(type);
            Assert.GreaterOrEqual(ours.TriangleCount, originalTriangles * 10,
                $"{type}: {ours.TriangleCount} triángulos frente a {originalTriangles} de Unity");
            Debug.Log($"[Qazrix] {type}: {originalTriangles} → {ours.TriangleCount} triángulos ({ours.TriangleCount / (float)originalTriangles:0.0}×)");
        }

        [TestCaseSource(nameof(Types))]
        public void HasTheSameSizeAsUnitysPrimitive(PrimitiveType type)
        {
            Mesh original = Reference(type);
            MeshData ours = Build(type);
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), max = -min;
            for (int i = 0; i < ours.VertexCount; i++)
            {
                var p = new Vector3(ours.Vertices[i * 3], ours.Vertices[i * 3 + 1], ours.Vertices[i * 3 + 2]);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            Assert.AreEqual(original.bounds.min.x, min.x, 0.002f, $"{type} x mín");
            Assert.AreEqual(original.bounds.max.x, max.x, 0.002f, $"{type} x máx");
            Assert.AreEqual(original.bounds.min.y, min.y, 0.002f, $"{type} y mín");
            Assert.AreEqual(original.bounds.max.y, max.y, 0.002f, $"{type} y máx");
            Assert.AreEqual(original.bounds.min.z, min.z, 0.002f, $"{type} z mín");
            Assert.AreEqual(original.bounds.max.z, max.z, 0.002f, $"{type} z máx");
        }

        [TestCaseSource(nameof(Types))]
        public void EveryTriangleFacesOutward_WithUnitNormals_AndValidUvs(PrimitiveType type)
        {
            MeshData m = Build(type);
            Assert.AreEqual(m.VertexCount * 3, m.Normals.Count);
            Assert.AreEqual(m.VertexCount * 2, m.Uvs.Count);
            for (int i = 0; i < m.VertexCount; i++)
            {
                float length = Mathf.Sqrt(m.Normals[i * 3] * m.Normals[i * 3] + m.Normals[i * 3 + 1] * m.Normals[i * 3 + 1] + m.Normals[i * 3 + 2] * m.Normals[i * 3 + 2]);
                Assert.AreEqual(1f, length, 1e-3f, $"{type}: normal {i} no es unitaria");
                Assert.That(m.Uvs[i * 2], Is.InRange(-0.001f, 1.001f), $"{type}: u fuera de rango");
                Assert.That(m.Uvs[i * 2 + 1], Is.InRange(-0.001f, 1.001f), $"{type}: v fuera de rango");
            }

            int inverted = 0;
            for (int t = 0; t < m.TriangleCount; t++)
            {
                int a = m.Triangles[t * 3], b = m.Triangles[t * 3 + 1], c = m.Triangles[t * 3 + 2];
                Assert.That(a, Is.InRange(0, m.VertexCount - 1));
                Assert.That(b, Is.InRange(0, m.VertexCount - 1));
                Assert.That(c, Is.InRange(0, m.VertexCount - 1));
                Vector3 pa = P(m, a), pb = P(m, b), pc = P(m, c);
                Vector3 normal = Vector3.Cross(pb - pa, pc - pa);
                Assert.Greater(normal.sqrMagnitude, 0f, $"{type}: triángulo {t} sin área");
                Vector3 average = N(m, a) + N(m, b) + N(m, c);
                if (Vector3.Dot(normal, average) <= 0f) inverted++;
            }
            Assert.AreEqual(0, inverted, $"{type}: triángulos con la cara hacia dentro");
        }

        [Test]
        public void SphereVerticesAreOnTheSurface_AndCapsuleIsTwoHemispheresAndACylinder()
        {
            MeshData sphere = PrimitiveGeometry.Sphere();
            for (int i = 0; i < sphere.VertexCount; i++)
                Assert.AreEqual(0.5f, P(sphere, i).magnitude, 1e-4f, "los vértices de la esfera están a radio 0,5");

            MeshData capsule = PrimitiveGeometry.Capsule();
            for (int i = 0; i < capsule.VertexCount; i++)
            {
                Vector3 p = P(capsule, i);
                float axisY = Mathf.Clamp(p.y, -0.5f, 0.5f);
                float distance = new Vector3(p.x, p.y - axisY, p.z).magnitude;
                Assert.AreEqual(0.5f, distance, 1e-4f, "los vértices de la cápsula están a 0,5 de su eje");
            }
        }

        [Test]
        public void Box_HasRoundedEdgesAndCorners_ButKeepsItsExtremes()
        {
            MeshData box = PrimitiveGeometry.Box();
            var corner = new Vector3(0.5f, 0.5f, 0.5f);
            float nearestToCorner = float.MaxValue, nearestToEdge = float.MaxValue;
            for (int i = 0; i < box.VertexCount; i++)
            {
                Vector3 p = new Vector3(Mathf.Abs(P(box, i).x), Mathf.Abs(P(box, i).y), Mathf.Abs(P(box, i).z));
                nearestToCorner = Mathf.Min(nearestToCorner, Vector3.Distance(p, corner));
                nearestToEdge = Mathf.Min(nearestToEdge, Vector2.Distance(new Vector2(p.x, p.y), new Vector2(0.5f, 0.5f)));
                // Convexo: la normal siempre mira hacia fuera desde el centro.
                Assert.Greater(Vector3.Dot(N(box, i), P(box, i)), 0f, "normal hacia dentro en el vértice " + i);
            }
            Assert.Greater(nearestToCorner, 0.05f, "ningún vértice llega a la esquina viva");
            Assert.Greater(nearestToEdge, 0.02f, "ni a la arista viva");
        }

        [Test]
        public void Cylinder_HasRoundedCapEdges_ButKeepsItsExtremes()
        {
            MeshData cylinder = PrimitiveGeometry.Cylinder();
            float maxRadius = 0f, maxHeight = 0f;
            for (int i = 0; i < cylinder.VertexCount; i++)
            {
                Vector3 p = P(cylinder, i);
                float radius = new Vector2(p.x, p.z).magnitude;
                maxRadius = Mathf.Max(maxRadius, radius);
                maxHeight = Mathf.Max(maxHeight, Mathf.Abs(p.y));
                Assert.IsFalse(radius > 0.49f && Mathf.Abs(p.y) > 0.97f, "ningún vértice en la arista viva de la tapa");
            }
            Assert.AreEqual(0.5f, maxRadius, 0.002f, "llega al radio completo");
            Assert.AreEqual(1f, maxHeight, 0.002f, "y a la altura completa");
        }

        [Test]
        public void InvalidSizes_AreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PrimitiveGeometry.Sphere(2, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => PrimitiveGeometry.Cylinder(8, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => PrimitiveGeometry.Capsule(8, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => PrimitiveGeometry.Box(0));
        }

        private static Vector3 P(MeshData m, int i) => new Vector3(m.Vertices[i * 3], m.Vertices[i * 3 + 1], m.Vertices[i * 3 + 2]);

        private static Vector3 N(MeshData m, int i) => new Vector3(m.Normals[i * 3], m.Normals[i * 3 + 1], m.Normals[i * 3 + 2]);
    }
}
