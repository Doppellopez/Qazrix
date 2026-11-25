using System;
using System.Collections.Generic;

namespace Qazrix.Simulation.World
{
    /// <summary>Malla de la barrera invisible sobre las murallas: vértices (x, y, z seguidos) y triángulos.</summary>
    public sealed class WallBarrierMesh
    {
        public float[] Vertices;
        public int[] Triangles;

        public int VertexCount => Vertices.Length / 3;
    }

    /// <summary>
    /// Las murallas son infranqueables y no se pueden escalar: además de sus paredes sólidas, sobre cada una se levanta una
    /// barrera invisible desde el borde del tejado hacia arriba, para que nadie la salve saltando desde un tejado cercano ni se
    /// suba a su azotea. Las caras son de doble sentido (se ve y se choca desde los dos lados). Sin dependencias de Unity.
    /// </summary>
    public static class WallBarrier
    {
        /// <summary>Alto de la barrera sobre el tejado (m): más de lo que se puede saltar o trepar.</summary>
        public const float Height = 40f;

        public static WallBarrierMesh Build(IEnumerable<BuildingPart> walls, float height = Height)
        {
            if (walls == null) throw new ArgumentNullException(nameof(walls));
            if (height <= 0f) throw new ArgumentOutOfRangeException(nameof(height));

            var vertices = new List<float>();
            var triangles = new List<int>();
            foreach (BuildingPart part in walls)
            {
                // Todos los contornos: el exterior y los patios (nadie debe colarse por un patio de la muralla).
                foreach (Vec2[] loop in part.Loops)
                {
                    for (int i = 0; i < loop.Length; i++)
                    {
                        Vec2 a = loop[i], b = loop[(i + 1) % loop.Length];
                        if ((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z) < 1e-9) continue;
                        int v = vertices.Count / 3;
                        float bottom = part.RoofHeight, top = part.RoofHeight + height;
                        Add(vertices, a.X, bottom, a.Z); Add(vertices, b.X, bottom, b.Z);
                        Add(vertices, a.X, top, a.Z); Add(vertices, b.X, top, b.Z);
                        // Dos caras por cada lado: así sirve desde dentro y desde fuera.
                        triangles.AddRange(new[] { v, v + 2, v + 1, v + 1, v + 2, v + 3 });
                        triangles.AddRange(new[] { v, v + 1, v + 2, v + 1, v + 3, v + 2 });
                    }
                }
            }
            return new WallBarrierMesh { Vertices = vertices.ToArray(), Triangles = triangles.ToArray() };
        }

        private static void Add(List<float> list, double x, double y, double z)
        {
            list.Add((float)x); list.Add((float)y); list.Add((float)z);
        }
    }
}
