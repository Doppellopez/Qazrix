using System;
using System.Collections.Generic;
using NUnit.Framework;
using Qazrix.Simulation.Movement;

namespace Qazrix.Tests
{
    /// <summary>Malla continua de la capa: cerrada, con las caras hacia fuera y sin huecos entre tramos.</summary>
    public sealed class CapeMeshLayoutTests
    {
        private static readonly CapeMeshLayout Layout = new CapeMeshLayout();

        [Test]
        public void Layout_HasTheExpectedVertexAndTriangleCounts()
        {
            Assert.AreEqual(2 * CapeMeshLayout.Rows * CapeMeshLayout.Columns, Layout.VertexCount);
            Assert.AreEqual(CapeMeshLayout.YokeRows + CapeSim.Segments * CapeMeshLayout.RowsPerSegment + 1, CapeMeshLayout.Rows);

            // Dos caras de (filas−1) × (columnas−1) celdas y cuatro cantos, con dos triángulos por celda.
            int faces = 2 * (CapeMeshLayout.Rows - 1) * (CapeMeshLayout.Columns - 1) * 2;
            int edges = 2 * (CapeMeshLayout.Rows - 1) * 2 + 2 * (CapeMeshLayout.Columns - 1) * 2;
            Assert.AreEqual(faces + edges, Layout.Triangles.Length / 3);
            Assert.Greater(Layout.Triangles.Length / 3, 1000, "es una tela con muchos polígonos, no unas pocas cajas");
        }

        [Test]
        public void Mesh_IsWatertight_EveryEdgeSharedByExactlyTwoTriangles()
        {
            var uses = new Dictionary<(int, int), int>();
            for (int t = 0; t < Layout.Triangles.Length; t += 3)
            {
                int a = Layout.Triangles[t], b = Layout.Triangles[t + 1], c = Layout.Triangles[t + 2];
                Count(uses, a, b); Count(uses, b, c); Count(uses, c, a);
            }
            foreach (var pair in uses)
                Assert.AreEqual(2, pair.Value, $"la arista {pair.Key} debería estar en dos triángulos: la tela está cerrada");
        }

        private static void Count(Dictionary<(int, int), int> uses, int a, int b)
        {
            var key = a < b ? (a, b) : (b, a);
            uses.TryGetValue(key, out int n);
            uses[key] = n + 1;
        }

        [Test]
        public void Faces_PointOutwards_OuterBackwards_InnerTowardsTheBody()
        {
            const int perLayer = CapeMeshLayout.Rows * CapeMeshLayout.Columns;
            int outerBackwards = 0, innerForwards = 0;
            for (int t = 0; t < Layout.Triangles.Length; t += 3)
            {
                int a = Layout.Triangles[t], b = Layout.Triangles[t + 1], c = Layout.Triangles[t + 2];
                Layout.Rest(a, out float ax, out float ay, out float az);
                Layout.Rest(b, out float bx, out float by, out float bz);
                Layout.Rest(c, out float cx, out float cy, out float cz);
                float ux = bx - ax, uy = by - ay, uz = bz - az, wx = cx - ax, wy = cy - ay, wz = cz - az;
                // Normal (u × w, convenio de Unity). Hacia fuera = hacia arriba o hacia atrás: ny − nz > 0.
                float ny = uz * wx - ux * wz, nz = ux * wy - uy * wx;
                float outward = ny - nz;

                int layer = a / perLayer;
                if (layer != b / perLayer || layer != c / perLayer) continue; // triángulo de un canto
                if (layer == 0 && outward > 0f) outerBackwards++;
                if (layer == 1 && outward < 0f) innerForwards++;
            }
            int perFace = (CapeMeshLayout.Rows - 1) * (CapeMeshLayout.Columns - 1) * 2;
            Assert.AreEqual(perFace, outerBackwards, "toda la cara exterior mira hacia fuera: hacia arriba sobre el hombro y hacia atrás en la espalda");
            Assert.AreEqual(perFace, innerForwards, "y toda la interior hacia el cuerpo");
        }

        [Test]
        public void Edges_PointOutwardsToo()
        {
            // Los cantos cierran la lámina: izquierdo (-x), derecho (+x), inferior (-y) y el superior, que mira hacia
            // donde sigue el perfil sobre el hombro (hacia delante, +z).
            const int perLayer = CapeMeshLayout.Rows * CapeMeshLayout.Columns;
            int left = 0, right = 0, bottom = 0, front = 0;
            for (int t = 0; t < Layout.Triangles.Length; t += 3)
            {
                int a = Layout.Triangles[t], b = Layout.Triangles[t + 1], c = Layout.Triangles[t + 2];
                if (a / perLayer == b / perLayer && a / perLayer == c / perLayer) continue; // cara
                Layout.Rest(a, out float ax, out float ay, out float az);
                Layout.Rest(b, out float bx, out float by, out float bz);
                Layout.Rest(c, out float cx, out float cy, out float cz);
                float ux = bx - ax, uy = by - ay, uz = bz - az, wx = cx - ax, wy = cy - ay, wz = cz - az;
                float nx = uy * wz - uz * wy, ny = uz * wx - ux * wz, nz = ux * wy - uy * wx;
                float absX = Math.Abs(nx), absY = Math.Abs(ny), absZ = Math.Abs(nz);
                if (absX >= absY && absX >= absZ) { if (nx < 0f) left++; else right++; }
                else if (absY >= absZ) { if (ny < 0f) bottom++; }
                else if (nz > 0f) front++;
            }
            Assert.AreEqual(2 * (CapeMeshLayout.Rows - 1), left, "canto izquierdo");
            Assert.AreEqual(2 * (CapeMeshLayout.Rows - 1), right, "canto derecho");
            Assert.AreEqual(2 * (CapeMeshLayout.Columns - 1), bottom, "borde de abajo");
            Assert.AreEqual(2 * (CapeMeshLayout.Columns - 1), front, "extremo sobre el hombro, hacia delante");
        }

        [Test]
        public void Rows_HangFromTheirSegment_AndRunTopToBottom()
        {
            for (int row = 0; row < CapeMeshLayout.YokeRows; row++)
                Assert.AreEqual(-1, Layout.RowSegment[row], "las filas del canesú siguen al cuerpo, no a un tramo");
            Assert.AreEqual(0, Layout.RowSegment[CapeMeshLayout.YokeRows], "el cuerpo empieza en el primer tramo");
            Assert.AreEqual(CapeSim.Segments - 1, Layout.RowSegment[CapeMeshLayout.Rows - 1], "la última fila cuelga del último tramo");
            for (int row = CapeMeshLayout.YokeRows + 1; row < CapeMeshLayout.Rows; row++)
                Assert.GreaterOrEqual(Layout.RowSegment[row], Layout.RowSegment[row - 1], "los tramos se recorren de arriba abajo");

            // En reposo recto la capa cuelga del punto de colgado y mide su largo.
            int hang = CapeMeshLayout.YokeRows;
            Layout.Rest(CapeMeshLayout.Index(0, hang, 0), out _, out float top, out _);
            Layout.Rest(CapeMeshLayout.Index(0, CapeMeshLayout.Rows - 1, 0), out _, out float bottom, out _);
            Assert.AreEqual(0f, top, CapeMeshLayout.Thickness, "el borde de arriba del primer tramo está en el punto de colgado");
            Assert.AreEqual(-CapeSim.CapeLength, bottom, CapeMeshLayout.Thickness);
        }

        [Test]
        public void Yoke_RisesBehindTheShoulder_AndCrossesTheCrestAboveIt()
        {
            // El canesú sale del punto de colgado, sube y pasa por encima de los hombros (cresta a y = 1,51 m).
            float crest = float.MinValue, crestZ = 0f;
            for (int column = 0; column < CapeMeshLayout.Columns; column++)
            {
                Layout.Rest(CapeMeshLayout.Index(0, 0, column), out _, out float y, out float z);
                if (y > crest) { crest = y; crestZ = z; }
            }
            float crestModelY = CapeMeshLayout.PivotY + crest;
            Assert.Greater(crestModelY, 1.50f, "la tela pasa por encima de los hombros (la caja de los hombros llega a 1,49 m)");
            Assert.Less(crestModelY, 1.56f, "pero pegada a ellos");
            float crestModelZ = CapeMeshLayout.PivotZ + crestZ;
            Assert.Greater(crestModelZ, -0.12f, "la cresta está sobre los hombros, no detrás de la espalda");

            // Los extremos del canesú cubren el ancho de los hombros.
            Layout.Rest(CapeMeshLayout.Index(0, 0, 0), out float x0, out _, out _);
            Layout.Rest(CapeMeshLayout.Index(0, 0, CapeMeshLayout.Columns - 1), out float x1, out _, out _);
            Assert.AreEqual(2f * CapeMeshLayout.YokeHalfWidth, x1 - x0, 1e-4f);
        }

        [Test]
        public void Width_TapersFromTopToBottom_AndTextureCoordinatesCoverTheCape()
        {
            int hang = CapeMeshLayout.YokeRows;
            Layout.Rest(CapeMeshLayout.Index(0, hang, 0), out float x0, out _, out _);
            Layout.Rest(CapeMeshLayout.Index(0, hang, CapeMeshLayout.Columns - 1), out float x1, out _, out _);
            Assert.AreEqual(CapeMeshLayout.WidthTop, x1 - x0, 1e-4f);

            Layout.Rest(CapeMeshLayout.Index(0, CapeMeshLayout.Rows - 1, 0), out float left, out _, out _);
            Layout.Rest(CapeMeshLayout.Index(0, CapeMeshLayout.Rows - 1, CapeMeshLayout.Columns - 1), out float right, out _, out _);
            Assert.AreEqual(CapeMeshLayout.WidthBottom, right - left, 1e-4f, "se ensancha hacia el borde de abajo");

            float maxU = 0f, maxV = 0f;
            for (int v = 0; v < Layout.VertexCount; v++)
            {
                maxU = Math.Max(maxU, Layout.Uvs[v * 2]);
                maxV = Math.Max(maxV, Layout.Uvs[v * 2 + 1]);
            }
            Assert.AreEqual(1f, maxU, 1e-5f);
            Assert.Greater(maxV, 1f);
        }

        [Test]
        public void Pleats_AreSmallEnoughNotToPokeThroughTheBody()
        {
            // En la espalda la cara interior se queda a menos de 2,3 cm del eje del tramo (grosor + pliegue).
            float maxZ = float.MinValue;
            for (int v = 0; v < Layout.VertexCount; v++)
            {
                int row = (v % (CapeMeshLayout.Rows * CapeMeshLayout.Columns)) / CapeMeshLayout.Columns;
                if (row < CapeMeshLayout.YokeRows) continue; // el canesú es el perfil sobre el hombro
                Layout.Rest(v, out _, out _, out float z);
                maxZ = Math.Max(maxZ, z);
            }
            Assert.LessOrEqual(maxZ, 0.5f * CapeMeshLayout.Thickness + CapeMeshLayout.PleatDepth + 1e-5f);
        }
    }
}
