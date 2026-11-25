using System;
using System.Collections.Generic;

namespace Qazrix.Simulation.World
{
    /// <summary>Malla de un pasadizo: vértices (x, y, z seguidos), UV, triángulos visibles (cara hacia dentro) y los del collider (a doble cara).</summary>
    public sealed class TunnelMesh
    {
        public float[] Vertices;
        public float[] Uvs;
        public int[] Triangles;          // se ven desde dentro del pasadizo
        public int[] ColliderTriangles;  // las mismas caras por los dos lados: no se atraviesa desde ningún sentido

        public int VertexCount => Vertices.Length / 3;
        public float Length;             // largo del eje (m)
    }

    /// <summary>
    /// Pasadizo recto por tramos: un tubo rectangular de suelo, dos paredes y techo que sigue una poligonal en planta, a una cota
    /// de suelo constante. En las curvas las paredes se unen en inglete, así que no quedan cuñas ni huecos. Sin dependencias de Unity.
    /// </summary>
    public static class TunnelMesher
    {
        public const double DefaultWidth = 3.0, DefaultHeight = 3.0;
        private const double MaxMiter = 3.0;   // el inglete no se alarga más que esto en una curva muy cerrada

        public static TunnelMesh Build(IReadOnlyList<Vec2> path, double floorY, double width = DefaultWidth, double height = DefaultHeight,
            double uvMetres = 3.0)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (path.Count < 2) throw new ArgumentException("Un pasadizo necesita al menos dos puntos.", nameof(path));
            if (width <= 0.0 || height <= 0.0 || uvMetres <= 0.0) throw new ArgumentOutOfRangeException(nameof(width));

            // Sin puntos repetidos seguidos.
            var points = new List<Vec2> { path[0] };
            for (int i = 1; i < path.Count; i++)
                if (Distance(points[points.Count - 1], path[i]) > 1e-6) points.Add(path[i]);
            if (points.Count < 2) throw new ArgumentException("Los puntos del pasadizo coinciden.", nameof(path));

            int n = points.Count;
            var dirs = new Vec2[n - 1];
            for (int i = 0; i < n - 1; i++)
            {
                double dx = points[i + 1].X - points[i].X, dz = points[i + 1].Z - points[i].Z, len = Math.Sqrt(dx * dx + dz * dz);
                dirs[i] = new Vec2(dx / len, dz / len);
            }

            // Bordes izquierdo y derecho (mirando hacia delante), con inglete en los puntos intermedios.
            var left = new Vec2[n];
            var right = new Vec2[n];
            double half = width * 0.5;
            for (int i = 0; i < n; i++)
            {
                Vec2 d0 = dirs[Math.Max(0, i - 1)], d1 = dirs[Math.Min(n - 2, i)];
                // Normal izquierda (en x este, z norte): (−dz, dx).
                Vec2 n0 = new Vec2(-d0.Z, d0.X), n1 = new Vec2(-d1.Z, d1.X);
                double bx = n0.X + n1.X, bz = n0.Z + n1.Z, bl = Math.Sqrt(bx * bx + bz * bz);
                Vec2 bis = bl < 1e-9 ? n1 : new Vec2(bx / bl, bz / bl);
                double cos = Math.Max(1.0 / MaxMiter, bis.X * n1.X + bis.Z * n1.Z);
                double offset = half / cos;
                left[i] = new Vec2(points[i].X + bis.X * offset, points[i].Z + bis.Z * offset);
                right[i] = new Vec2(points[i].X - bis.X * offset, points[i].Z - bis.Z * offset);
            }

            var vertices = new List<float>();
            var uvs = new List<float>();
            var tris = new List<int>();
            double along = 0.0;
            for (int i = 0; i < n - 1; i++)
            {
                double segment = Distance(points[i], points[i + 1]);
                double u0 = along / uvMetres, u1 = (along + segment) / uvMetres;
                double top = floorY + height;
                // Suelo, techo, pared izquierda y pared derecha, con la cara visible hacia dentro.
                AddQuad(vertices, uvs, tris, left[i], right[i], left[i + 1], right[i + 1], floorY, floorY, u0, u1, 0.0, width / uvMetres);
                AddQuad(vertices, uvs, tris, right[i], left[i], right[i + 1], left[i + 1], top, top, u0, u1, 0.0, width / uvMetres);
                AddWall(vertices, uvs, tris, left[i], left[i + 1], floorY, top, u0, u1, height / uvMetres, inwardIsRight: true);
                AddWall(vertices, uvs, tris, right[i], right[i + 1], floorY, top, u0, u1, height / uvMetres, inwardIsRight: false);
                along += segment;
            }

            var collider = new List<int>(tris.Count * 2);
            for (int t = 0; t < tris.Count; t += 3)
            {
                collider.Add(tris[t]); collider.Add(tris[t + 1]); collider.Add(tris[t + 2]);
                collider.Add(tris[t]); collider.Add(tris[t + 2]); collider.Add(tris[t + 1]);
            }
            return new TunnelMesh
            {
                Vertices = vertices.ToArray(),
                Uvs = uvs.ToArray(),
                Triangles = tris.ToArray(),
                ColliderTriangles = collider.ToArray(),
                Length = (float)along
            };
        }

        private static void AddQuad(List<float> v, List<float> uv, List<int> t, Vec2 a0, Vec2 b0, Vec2 a1, Vec2 b1,
            double y0, double y1, double u0, double u1, double vA, double vB)
        {
            int i = v.Count / 3;
            Add(v, uv, a0.X, y0, a0.Z, u0, vA); Add(v, uv, b0.X, y0, b0.Z, u0, vB);
            Add(v, uv, a1.X, y1, a1.Z, u1, vA); Add(v, uv, b1.X, y1, b1.Z, u1, vB);
            // Suelo y techo se llaman con los bordes intercambiados (izquierda/derecha), así el mismo orden de vértices
            // da la cara hacia arriba en el suelo y hacia abajo en el techo (convenio de Unity, como FootprintMesher).
            t.AddRange(new[] { i, i + 2, i + 1, i + 1, i + 2, i + 3 });
        }

        private static void AddWall(List<float> v, List<float> uv, List<int> t, Vec2 a, Vec2 b, double y0, double y1,
            double u0, double u1, double vTop, bool inwardIsRight)
        {
            int i = v.Count / 3;
            Add(v, uv, a.X, y0, a.Z, u0, 0.0); Add(v, uv, b.X, y0, b.Z, u1, 0.0);
            Add(v, uv, a.X, y1, a.Z, u0, vTop); Add(v, uv, b.X, y1, b.Z, u1, vTop);
            // Pared izquierda: se ve desde su derecha (dentro). Pared derecha: desde su izquierda.
            if (inwardIsRight) t.AddRange(new[] { i, i + 2, i + 1, i + 1, i + 2, i + 3 });
            else t.AddRange(new[] { i, i + 1, i + 2, i + 1, i + 3, i + 2 });
        }

        private static void Add(List<float> v, List<float> uv, double x, double y, double z, double u, double w)
        {
            v.Add((float)x); v.Add((float)y); v.Add((float)z);
            uv.Add((float)u); uv.Add((float)w);
        }

        private static double Distance(Vec2 a, Vec2 b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
    }
}
