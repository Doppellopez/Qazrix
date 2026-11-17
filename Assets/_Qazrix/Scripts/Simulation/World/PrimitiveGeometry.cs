using System;
using System.Collections.Generic;

namespace Qazrix.Simulation.World
{
    /// <summary>Malla en arreglos simples, sin tipos de Unity: posiciones, normales, UV y triángulos.</summary>
    public sealed class MeshData
    {
        public readonly List<float> Vertices = new List<float>();  // x, y, z
        public readonly List<float> Normals = new List<float>();   // x, y, z
        public readonly List<float> Uvs = new List<float>();       // u, v
        public readonly List<int> Triangles = new List<int>();

        public int VertexCount => Vertices.Count / 3;
        public int TriangleCount => Triangles.Count / 3;

        internal int AddVertex(double x, double y, double z, double nx, double ny, double nz, double u, double v)
        {
            Vertices.Add((float)x); Vertices.Add((float)y); Vertices.Add((float)z);
            Normals.Add((float)nx); Normals.Add((float)ny); Normals.Add((float)nz);
            Uvs.Add((float)u); Uvs.Add((float)v);
            return VertexCount - 1;
        }

        /// <summary>
        /// Añade un triángulo orientado hacia fuera (la cara frontal de Unity es la que mira hacia donde apunta
        /// el producto vectorial de sus lados). Descarta los de área cero de los polos y ajusta el orden si hace falta.
        /// </summary>
        internal void AddTriangle(int a, int b, int c)
        {
            double ax = Vertices[a * 3], ay = Vertices[a * 3 + 1], az = Vertices[a * 3 + 2];
            double ux = Vertices[b * 3] - ax, uy = Vertices[b * 3 + 1] - ay, uz = Vertices[b * 3 + 2] - az;
            double vx = Vertices[c * 3] - ax, vy = Vertices[c * 3 + 1] - ay, vz = Vertices[c * 3 + 2] - az;
            double cx = uy * vz - uz * vy, cy = uz * vx - ux * vz, cz = ux * vy - uy * vx;
            if (cx * cx + cy * cy + cz * cz < 1e-18) return; // triángulo degenerado (polo)

            double nx = Normals[a * 3] + Normals[b * 3] + Normals[c * 3];
            double ny = Normals[a * 3 + 1] + Normals[b * 3 + 1] + Normals[c * 3 + 1];
            double nz = Normals[a * 3 + 2] + Normals[b * 3 + 2] + Normals[c * 3 + 2];
            if (cx * nx + cy * ny + cz * nz < 0) { int t = b; b = c; c = t; }
            Triangles.Add(a); Triangles.Add(b); Triangles.Add(c);
        }
    }

    /// <summary>
    /// Primitivas con el mismo tamaño que las de Unity (esfera de radio 0,5; cilindro y cápsula de 2 de alto y
    /// radio 0,5; cubo de lado 1) pero con muchos más triángulos y con las aristas redondeadas, para siluetas
    /// suaves (ART-11: diez veces los polígonos; ART-13: cajas y cilindros redondeados). Lógica pura, sin Unity.
    /// </summary>
    public static class PrimitiveGeometry
    {
        // Subdivisiones por defecto. Las pruebas exigen al menos 10 veces los triángulos de la primitiva de Unity.
        public const int SphereLongitudes = 96, SphereLatitudes = 44;
        public const int CylinderSides = 64, CylinderLatitudes = 24;
        public const int CapsuleLongitudes = 128, CapsuleHemisphereRows = 26;
        public const int BoxLongitudes = 48, BoxLatitudes = 24;

        /// <summary>
        /// Redondez de cajas y cilindros (exponente de la superelipse): 1 = esfera, 0 = arista viva. Con 0,5 no queda ninguna esquina viva: las
        /// aristas quedan suavizadas sin perder la forma general.
        /// </summary>
        public const double Roundness = 0.5;

        private const double Radius = 0.5;

        public static MeshData Sphere(int longitudes = SphereLongitudes, int latitudes = SphereLatitudes)
        {
            if (longitudes < 3 || latitudes < 2) throw new ArgumentOutOfRangeException(nameof(longitudes));
            var mesh = new MeshData();
            AddBand(mesh, longitudes, latitudes + 1, (i, j) =>
            {
                double theta = Math.PI * j / latitudes, phi = 2.0 * Math.PI * i / longitudes;
                return Ring(theta, phi, 0.0, (double)i / longitudes, 1.0 - (double)j / latitudes);
            });
            return mesh;
        }

        /// <summary>Cápsula de 2 de alto: cilindro de 1 y dos hemisferios de radio 0,5 centrados en y = ±0,5.</summary>
        public static MeshData Capsule(int longitudes = CapsuleLongitudes, int hemisphereRows = CapsuleHemisphereRows)
        {
            if (longitudes < 3 || hemisphereRows < 1) throw new ArgumentOutOfRangeException(nameof(longitudes));
            var mesh = new MeshData();
            int rings = 2 * (hemisphereRows + 1);
            AddBand(mesh, longitudes, rings, (i, j) =>
            {
                double phi = 2.0 * Math.PI * i / longitudes;
                bool upper = j <= hemisphereRows;
                int k = upper ? j : j - (hemisphereRows + 1);
                double theta = (Math.PI * 0.5) * k / hemisphereRows;     // 0..90° en cada hemisferio
                if (!upper) theta += Math.PI * 0.5;
                double centreY = upper ? 0.5 : -0.5;
                return Ring(theta, phi, centreY, (double)i / longitudes, 1.0 - (double)j / (rings - 1));
            });
            return mesh;
        }

        /// <summary>
        /// Cilindro de radio 0,5 y 2 de alto con las aristas de las tapas redondeadas (superelipse de exponente
        /// <paramref name="roundness"/>): con 1 sería una esfera y con valores pequeños se acerca al cilindro recto.
        /// Tiene el mismo tamaño que el cilindro de Unity.
        /// </summary>
        public static MeshData Cylinder(int sides = CylinderSides, int latitudes = CylinderLatitudes, double roundness = Roundness)
        {
            if (sides < 3 || latitudes < 2 || roundness <= 0.0 || roundness > 1.0) throw new ArgumentOutOfRangeException(nameof(sides));
            var mesh = new MeshData();
            double p = 2.0 / roundness;
            AddBand(mesh, sides, latitudes + 1, (i, j) =>
            {
                double theta = Math.PI * j / latitudes, phi = 2.0 * Math.PI * i / sides;
                double st = Math.Sin(theta), ct = Math.Cos(theta), c = Math.Cos(phi), s = Math.Sin(phi);
                double rho = Radius * Math.Pow(Math.Abs(st), roundness);
                double y = Math.Sign(ct) * Math.Pow(Math.Abs(ct), roundness);
                // Normal = gradiente de (ρ/0,5)^p + |y|^p, que es 1 en la superficie.
                double gr = Math.Pow(rho / Radius, p - 1.0) / Radius, gy = Math.Sign(y) * Math.Pow(Math.Abs(y), p - 1.0);
                double nx = gr * c, nz = gr * s, ny = gy, len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (len < 1e-12) { nx = 0.0; ny = Math.Sign(ct) == 0 ? 1.0 : Math.Sign(ct); nz = 0.0; len = 1.0; }
                nx /= len; ny /= len; nz /= len;
                // Textura: alrededor y a lo alto en el lateral; plana en las tapas.
                bool cap = Math.Abs(ny) > Math.Sqrt(nx * nx + nz * nz);
                double u = cap ? 0.5 + rho * c : (double)i / sides, v = cap ? 0.5 + rho * s : (y + 1.0) * 0.5;
                return new[] { rho * c, y, rho * s, nx, ny, nz, u, v };
            });
            return mesh;
        }

        /// <summary>
        /// Caja de lado 1 con las aristas y las esquinas redondeadas (superelipsoide de exponente
        /// <paramref name="roundness"/>): con 1 sería una esfera y con valores pequeños se acerca al cubo recto.
        /// Tiene el mismo tamaño que el cubo de Unity. Las caras son planas en el centro y se curvan hacia los bordes.
        /// </summary>
        public static MeshData Box(int longitudes = BoxLongitudes, int latitudes = BoxLatitudes, double roundness = Roundness)
        {
            if (longitudes < 4 || latitudes < 2 || roundness <= 0.0 || roundness > 1.0) throw new ArgumentOutOfRangeException(nameof(longitudes));
            var mesh = new MeshData();
            double p = 2.0 / roundness;
            AddBand(mesh, longitudes, latitudes + 1, (i, j) =>
            {
                double theta = Math.PI * j / latitudes, phi = 2.0 * Math.PI * i / longitudes;
                double st = Math.Sin(theta);
                double dx = st * Math.Cos(phi), dy = Math.Cos(theta), dz = st * Math.Sin(phi);
                double x = Radius * Math.Sign(dx) * Math.Pow(Math.Abs(dx), roundness);
                double y = Radius * Math.Sign(dy) * Math.Pow(Math.Abs(dy), roundness);
                double z = Radius * Math.Sign(dz) * Math.Pow(Math.Abs(dz), roundness);
                // Normal = gradiente de |x/0,5|^p + |y/0,5|^p + |z/0,5|^p.
                double nx = Math.Sign(x) * Math.Pow(Math.Abs(x) / Radius, p - 1.0);
                double ny = Math.Sign(y) * Math.Pow(Math.Abs(y) / Radius, p - 1.0);
                double nz = Math.Sign(z) * Math.Pow(Math.Abs(z) / Radius, p - 1.0);
                double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (len < 1e-12) { nx = 0.0; ny = 1.0; nz = 0.0; len = 1.0; }
                nx /= len; ny /= len; nz /= len;
                // Textura: proyección plana por la cara a la que más mira el punto.
                double ax = Math.Abs(nx), ay = Math.Abs(ny), az = Math.Abs(nz);
                double u, v;
                if (ax >= ay && ax >= az) { u = z + 0.5; v = y + 0.5; }
                else if (ay >= az) { u = x + 0.5; v = z + 0.5; }
                else { u = x + 0.5; v = y + 0.5; }
                return new[] { x, y, z, nx, ny, nz, u, v };
            });
            return mesh;
        }

        /// <summary>Punto de un anillo esférico: θ desde el polo +Y, con el centro desplazado en Y.</summary>
        private static double[] Ring(double theta, double phi, double centreY, double u, double v)
        {
            double st = Math.Sin(theta), ct = Math.Cos(theta), c = Math.Cos(phi), s = Math.Sin(phi);
            double nx = st * c, ny = ct, nz = st * s;
            return new[] { Radius * nx, centreY + Radius * ny, Radius * nz, nx, ny, nz, u, v };
        }

        /// <summary>Rejilla de anillos (columnas con costura duplicada) unida en cuadrados de dos triángulos.</summary>
        private static void AddBand(MeshData mesh, int columns, int rows, Func<int, int, double[]> point)
        {
            int start = mesh.VertexCount;
            for (int j = 0; j < rows; j++)
            {
                for (int i = 0; i <= columns; i++)
                {
                    double[] p = point(i, j);
                    mesh.AddVertex(p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7]);
                }
            }
            for (int j = 0; j < rows - 1; j++)
            {
                for (int i = 0; i < columns; i++)
                {
                    int a = start + j * (columns + 1) + i, b = a + 1, c = a + columns + 1, d = c + 1;
                    mesh.AddTriangle(a, b, c);
                    mesh.AddTriangle(b, d, c);
                }
            }
        }
    }
}
