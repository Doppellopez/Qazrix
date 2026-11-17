using System;
using System.Collections.Generic;

namespace Qazrix.Simulation.World
{
    /// <summary>
    /// Un matacán: balcón volado sobre una pared, justo bajo la cornisa, con ménsulas debajo y un pretil bajo. Se define
    /// por el centro de su tramo de pared, la dirección de la pared y la normal hacia fuera.
    /// </summary>
    public sealed class Machicolation
    {
        public double X, Z;                       // centro, sobre la pared
        public float NormalX, NormalZ;            // normal horizontal hacia fuera (unitaria)
        public float TangentX, TangentZ;          // dirección de la pared (unitaria)
        public float RoofHeight;                  // cota del tejado del edificio
        public double Length;                     // largo de la losa a lo largo de la pared (m)
        public bool Tower;                        // está en una torre
        public int PartIndex;                     // edificio al que pertenece
        public int EdgeIndex;                     // lado de su contorno exterior en el que va
    }

    /// <summary>Medidas y reparto de los matacanes. Valores de prototipo, sin base documental.</summary>
    public sealed class MachicolationRules
    {
        /// <summary>Largo de la losa (m) y cuánto vuela sobre la pared (m).</summary>
        public double SlabLength = 3.5, Overhang = 0.7;
        /// <summary>La cara de arriba de la losa queda tanto por debajo del tejado (m); grosor de la losa (m).</summary>
        public double DropBelowRoof = 0.9, SlabThickness = 0.25;
        /// <summary>Altura (m) y grosor (m) del pretil.</summary>
        public double ParapetHeight = 0.4, ParapetThickness = 0.15;
        /// <summary>Almenas sobre el pretil frontal: cuántas, alto (m) y ancho (m).</summary>
        public int Merlons = 4;
        public double MerlonHeight = 0.3, MerlonWidth = 0.35;
        /// <summary>Cuántas ménsulas lleva debajo.</summary>
        public int Corbels = 4;
        /// <summary>Distancia mínima (m) entre dos matacanes de edificios altos y entre uno de estos y una torre.</summary>
        public double MinSeparation = 6.0;
        /// <summary>De los doce puntos de prueba hacia fuera de un lado, cuántos deben caer fuera de todo edificio para que dé a la calle.</summary>
        public int MinOpen = 6;
        /// <summary>Altura (m) sobre el terreno desde la que un edificio cuenta como alto.</summary>
        public float TallMinRise = 9f;
        /// <summary>Lado recto mínimo (m) para poner un matacán en una torre y en un edificio alto.</summary>
        public double MinEdgeTower = 4.0, MinEdgeTall = 4.0;
        /// <summary>Margen (m) entre el extremo de la losa y la esquina del lado.</summary>
        public double EdgeMargin = 0.8;
        /// <summary>Tope de matacanes en toda la ciudad (son solo visuales y sin colisión).</summary>
        public int MaxCount = 300;
        /// <summary>Distancias (m) a las que se mira hacia fuera de cada lado para saber si da a la calle o a otro edificio.</summary>
        public double[] OpennessProbes = { 1.5, 3.0, 5.0, 8.0 };
    }

    /// <summary>
    /// Reparto determinista de los matacanes: uno en cada torre, en su lado más abierto a la calle, y después en los
    /// edificios altos, como mucho uno por lado recto largo que dé a la calle, repartidos por toda la ciudad, sin que dos
    /// queden a menos de <see cref="MachicolationRules.MinSeparation"/> metros. Las torres tienen prioridad en el tope.
    /// Sin azar. Sin dependencias de Unity.
    /// </summary>
    public static class MachicolationPlanner
    {
        public static List<Machicolation> Place(List<BuildingPart> parts, FootprintIndex index, Func<double, double, float> groundAt,
            FacadeOpeningRules towerRules, MachicolationRules rules)
        {
            if (parts == null) throw new ArgumentNullException(nameof(parts));
            if (index == null) throw new ArgumentNullException(nameof(index));
            if (groundAt == null) throw new ArgumentNullException(nameof(groundAt));
            if (towerRules == null) throw new ArgumentNullException(nameof(towerRules));
            if (rules == null) throw new ArgumentNullException(nameof(rules));

            var towers = new List<Machicolation>();
            var tallCandidates = new List<Machicolation>();
            for (int n = 0; n < parts.Count; n++)
            {
                BuildingPart part = parts[n];
                float rise = part.RoofHeight - groundAt(part.Centre.X, part.Centre.Z);
                bool tower = part.VolumeArea <= towerRules.TowerMaxArea && rise >= towerRules.TowerMinHeight;
                bool tall = !tower && rise >= rules.TallMinRise;
                if (!tower && !tall) continue;

                Vec2[] loop = part.Loops[0];
                if (tower)
                {
                    // Un solo matacán por torre, en su lado más abierto a la calle (y, a igualdad, el más largo).
                    int best = -1;
                    double bestScore = double.MinValue;
                    for (int i = 0; i < loop.Length; i++)
                    {
                        if (!EdgeFits(loop, i, rules.MinEdgeTower, rules, out _, out _)) continue;
                        double score = Openness(loop, i, index, rules) * 1000.0 + EdgeLength(loop, i);
                        if (score > bestScore) { bestScore = score; best = i; }
                    }
                    Machicolation m = best >= 0 ? At(part, n, loop, best, rules) : null;
                    if (m != null) { m.Tower = true; towers.Add(m); }
                    continue;
                }

                // Edificio alto: cada lado recto largo que da a la calle es un candidato.
                for (int i = 0; i < loop.Length; i++)
                {
                    if (!EdgeFits(loop, i, rules.MinEdgeTall, rules, out _, out _)) continue;
                    if (Openness(loop, i, index, rules) < rules.MinOpen) continue;
                    Machicolation m = At(part, n, loop, i, rules);
                    if (m != null) tallCandidates.Add(m);
                }
            }

            var result = new List<Machicolation>();
            var grid = new SeparationGrid(rules.MinSeparation);
            foreach (Machicolation m in towers)
            {
                if (result.Count >= rules.MaxCount) break;
                result.Add(m);
                grid.Add(m);
            }

            // Edificios altos: orden fijo (oeste a este, sur a norte, lado a lado); primero repartidos a intervalos regulares
            // por toda la lista y después, si queda cupo, se rellena con el resto en orden.
            tallCandidates.Sort((a, b) =>
            {
                if (a.X != b.X) return a.X.CompareTo(b.X);
                if (a.Z != b.Z) return a.Z.CompareTo(b.Z);
                return a.EdgeIndex.CompareTo(b.EdgeIndex);
            });
            int want = rules.MaxCount - result.Count;
            if (want > 0 && tallCandidates.Count > 0)
            {
                var taken = new bool[tallCandidates.Count];
                int added = 0;
                int step = Math.Max(1, (int)Math.Ceiling(tallCandidates.Count / (double)want));
                for (int k = step / 2; k < tallCandidates.Count && added < want; k += step)
                    if (grid.TryAdd(tallCandidates[k])) { result.Add(tallCandidates[k]); taken[k] = true; added++; }
                for (int k = 0; k < tallCandidates.Count && added < want; k++)
                    if (!taken[k] && grid.TryAdd(tallCandidates[k])) { result.Add(tallCandidates[k]); added++; }
            }
            return result;
        }

        private static double EdgeLength(Vec2[] loop, int i)
        {
            Vec2 a = loop[i], b = loop[(i + 1) % loop.Length];
            return Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z));
        }

        /// <summary>¿Cabe un matacán en este lado? Mide al menos <paramref name="minEdge"/> y deja el margen a las esquinas.</summary>
        private static bool EdgeFits(Vec2[] loop, int i, double minEdge, MachicolationRules rules, out double slab, out double length)
        {
            length = EdgeLength(loop, i);
            slab = Math.Min(rules.SlabLength, length - 2.0 * rules.EdgeMargin);
            return length >= minEdge && slab >= 1.0;
        }

        /// <summary>Cuántos de los doce puntos de prueba, hacia fuera de la pared, caen fuera de todo edificio.</summary>
        private static int Openness(Vec2[] loop, int i, FootprintIndex index, MachicolationRules rules)
        {
            Vec2 p = loop[i], q = loop[(i + 1) % loop.Length];
            double dx = q.X - p.X, dz = q.Z - p.Z, length = Math.Sqrt(dx * dx + dz * dz);
            double nx = dz / length, nz = -dx / length;
            int open = 0;
            foreach (double t in new[] { 0.25, 0.5, 0.75 })
            {
                double mx = p.X + dx * t, mz = p.Z + dz * t;
                foreach (double d in rules.OpennessProbes)
                    if (!index.Contains(mx + nx * d, mz + nz * d)) open++;
            }
            return open;
        }

        /// <summary>Matacán centrado en el lado <paramref name="edge"/> del contorno, o null si no cabe.</summary>
        private static Machicolation At(BuildingPart part, int partIndex, Vec2[] loop, int edge, MachicolationRules rules)
        {
            if (!EdgeFits(loop, edge, 0.0, rules, out double slab, out double length)) return null;
            Vec2 a = loop[edge], b = loop[(edge + 1) % loop.Length];
            double ex = b.X - a.X, ez = b.Z - a.Z;
            return new Machicolation
            {
                X = a.X + ex * 0.5, Z = a.Z + ez * 0.5,
                NormalX = (float)(ez / length), NormalZ = (float)(-ex / length),
                TangentX = (float)(ex / length), TangentZ = (float)(ez / length),
                RoofHeight = part.RoofHeight, Length = slab, PartIndex = partIndex, EdgeIndex = edge
            };
        }

        /// <summary>Cubos de una rejilla para saber si ya hay otro matacán a menos de la separación mínima.</summary>
        private sealed class SeparationGrid
        {
            private readonly double _cell;
            private readonly Dictionary<long, List<Machicolation>> _cells = new Dictionary<long, List<Machicolation>>();

            public SeparationGrid(double separation) { _cell = Math.Max(1e-3, separation); }

            private static long Key(long x, long z) => (x << 32) ^ (z & 0xffffffffL);

            public void Add(Machicolation m)
            {
                long key = Key((long)Math.Floor(m.X / _cell), (long)Math.Floor(m.Z / _cell));
                if (!_cells.TryGetValue(key, out List<Machicolation> list)) _cells[key] = list = new List<Machicolation>();
                list.Add(m);
            }

            public bool TryAdd(Machicolation m)
            {
                long cx = (long)Math.Floor(m.X / _cell), cz = (long)Math.Floor(m.Z / _cell);
                for (long dx = -1; dx <= 1; dx++)
                    for (long dz = -1; dz <= 1; dz++)
                        if (_cells.TryGetValue(Key(cx + dx, cz + dz), out List<Machicolation> list))
                            foreach (Machicolation other in list)
                                if ((other.X - m.X) * (other.X - m.X) + (other.Z - m.Z) * (other.Z - m.Z) < _cell * _cell) return false;
                Add(m);
                return true;
            }
        }

        /// <summary>
        /// ¿Choca un hueco de fachada con un matacán? Sí si está sobre su tramo de pared (con medio metro de holgura a cada lado)
        /// y tan arriba que cae en la franja de las ménsulas y la losa.
        /// </summary>
        public static bool BlocksOpening(Machicolation m, FacadeOpening o, float openingHalfHeight, MachicolationRules rules)
        {
            double rx = o.X - m.X, rz = o.Z - m.Z;
            double along = rx * m.TangentX + rz * m.TangentZ, across = rx * m.NormalX + rz * m.NormalZ;
            if (Math.Abs(along) > m.Length * 0.5 + 0.5 || Math.Abs(across) > 0.5) return false;
            double reservedFloor = m.RoofHeight - rules.DropBelowRoof - rules.SlabThickness - 0.5 - 0.6; // ménsulas y margen
            return o.Y + openingHalfHeight > reservedFloor;
        }
    }

    /// <summary>Malla de los matacanes: la piedra (losa, ménsulas y pretil) y, aparte, los recuadros oscuros del suelo.</summary>
    public sealed class MachicolationMesh
    {
        public float[] Vertices, Uvs;                 // x, y, z seguidos; u, v seguidos
        public int[] Triangles;
        public float[] HoleVertices, HoleUvs;         // recuadros oscuros sobre la losa (mitad de la textura de huecos con la saetera)
        public int[] HoleTriangles;
    }

    public static class MachicolationMesher
    {
        public static MachicolationMesh Build(List<Machicolation> machicolations, MachicolationRules rules, float metersPerTile)
        {
            if (machicolations == null) throw new ArgumentNullException(nameof(machicolations));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (!(metersPerTile > 0f)) throw new ArgumentException("El tamaño de la repetición debe ser positivo.");

            var stone = new Sink();
            var holes = new Sink();
            foreach (Machicolation m in machicolations)
            {
                double top = m.RoofHeight - rules.DropBelowRoof;      // cara de arriba de la losa
                double bottom = top - rules.SlabThickness;
                double half = m.Length * 0.5;
                const double embed = 0.05;                            // la losa entra un poco en la pared

                // Losa.
                Box(stone, m, -half, half, -embed, rules.Overhang, bottom, top, metersPerTile);

                // Ménsulas: dos escalones, el de arriba más profundo.
                int count = Math.Max(1, rules.Corbels);
                for (int k = 0; k < count; k++)
                {
                    double s = -half + (k + 0.5) * m.Length / count, w = 0.12;
                    Box(stone, m, s - w, s + w, -embed, rules.Overhang * 0.8, bottom - 0.25, bottom, metersPerTile);
                    Box(stone, m, s - w * 0.8, s + w * 0.8, -embed, rules.Overhang * 0.45, bottom - 0.5, bottom - 0.25, metersPerTile);
                }

                // Pretil: tres lados sobre el borde de la losa.
                double pt = rules.ParapetThickness, pTop = top + rules.ParapetHeight;
                Box(stone, m, -half, half, rules.Overhang - pt, rules.Overhang, top, pTop, metersPerTile);   // frontal
                Box(stone, m, -half, -half + pt, -embed, rules.Overhang - pt, top, pTop, metersPerTile);     // lateral izquierdo
                Box(stone, m, half - pt, half, -embed, rules.Overhang - pt, top, pTop, metersPerTile);       // lateral derecho

                // Almenas sobre el pretil frontal.
                int merlons = Math.Max(0, rules.Merlons);
                for (int k = 0; k < merlons; k++)
                {
                    double s = -half + (k + 0.5) * m.Length / merlons, w = rules.MerlonWidth * 0.5;
                    Box(stone, m, s - w, s + w, rules.Overhang - pt, rules.Overhang, pTop, pTop + rules.MerlonHeight, metersPerTile);
                }

                // Huecos del suelo: tres recuadros oscuros en la cara de arriba de la losa.
                for (int k = 0; k < 3; k++)
                {
                    double s = -half * 0.6 + k * half * 0.6;
                    Quad(holes, m, s - 0.18, s + 0.18, 0.12, 0.38, top + 0.01);
                }
            }

            return new MachicolationMesh
            {
                Vertices = stone.Vertices.ToArray(), Uvs = stone.Uvs.ToArray(), Triangles = stone.Triangles.ToArray(),
                HoleVertices = holes.Vertices.ToArray(), HoleUvs = holes.Uvs.ToArray(), HoleTriangles = holes.Triangles.ToArray()
            };
        }

        private sealed class Sink
        {
            public readonly List<float> Vertices = new List<float>();
            public readonly List<float> Uvs = new List<float>();
            public readonly List<int> Triangles = new List<int>();

            public int Add(double x, double y, double z, double u, double v)
            {
                Vertices.Add((float)x); Vertices.Add((float)y); Vertices.Add((float)z);
                Uvs.Add((float)u); Uvs.Add((float)v);
                return Vertices.Count / 3 - 1;
            }

            /// <summary>Cuadrilátero a b c d con la cara vista hacia (nx, ny, nz); el orden se ajusta solo.</summary>
            public void Face(int a, int b, int c, int d, double nx, double ny, double nz)
            {
                Triangle(a, b, c, nx, ny, nz);
                Triangle(a, c, d, nx, ny, nz);
            }

            private void Triangle(int a, int b, int c, double nx, double ny, double nz)
            {
                double ax = Vertices[a * 3], ay = Vertices[a * 3 + 1], az = Vertices[a * 3 + 2];
                double ux = Vertices[b * 3] - ax, uy = Vertices[b * 3 + 1] - ay, uz = Vertices[b * 3 + 2] - az;
                double vx = Vertices[c * 3] - ax, vy = Vertices[c * 3 + 1] - ay, vz = Vertices[c * 3 + 2] - az;
                double cx = uy * vz - uz * vy, cy = uz * vx - ux * vz, cz = ux * vy - uy * vx;
                if (cx * cx + cy * cy + cz * cz < 1e-18) return;
                if (cx * nx + cy * ny + cz * nz < 0) { int t = b; b = c; c = t; }
                Triangles.Add(a); Triangles.Add(b); Triangles.Add(c);
            }
        }

        /// <summary>Punto en el mundo: s a lo largo de la pared desde el centro del tramo, d hacia fuera desde la pared.</summary>
        private static void At(Machicolation m, double s, double d, out double x, out double z)
        {
            x = m.X + m.TangentX * s + m.NormalX * d;
            z = m.Z + m.TangentZ * s + m.NormalZ * d;
        }

        /// <summary>Caja con cinco caras visibles (con fondo): s0..s1 a lo largo, d0..d1 hacia fuera, y0..y1 en altura.</summary>
        private static void Box(Sink b, Machicolation m, double s0, double s1, double d0, double d1, double y0, double y1, float tile)
        {
            At(m, s0, d0, out double x00, out double z00); At(m, s1, d0, out double x10, out double z10);
            At(m, s1, d1, out double x11, out double z11); At(m, s0, d1, out double x01, out double z01);

            int V(double x, double y, double z, double u, double v) => b.Add(x, y, z, u, v);
            // Arriba y abajo: textura plana en planta.
            Face4(b, V(x00, y1, z00, x00 / tile, z00 / tile), V(x10, y1, z10, x10 / tile, z10 / tile), V(x11, y1, z11, x11 / tile, z11 / tile), V(x01, y1, z01, x01 / tile, z01 / tile), 0, 1, 0);
            Face4(b, V(x00, y0, z00, x00 / tile, z00 / tile), V(x10, y0, z10, x10 / tile, z10 / tile), V(x11, y0, z11, x11 / tile, z11 / tile), V(x01, y0, z01, x01 / tile, z01 / tile), 0, -1, 0);

            // Cuatro lados: u a lo largo de la cara, v la altura.
            Side(b, x00, z00, x10, z10, y0, y1, -m.NormalX, -m.NormalZ, tile);   // hacia la pared
            Side(b, x11, z11, x01, z01, y0, y1, m.NormalX, m.NormalZ, tile);     // hacia fuera
            Side(b, x01, z01, x00, z00, y0, y1, -m.TangentX, -m.TangentZ, tile); // izquierda
            Side(b, x10, z10, x11, z11, y0, y1, m.TangentX, m.TangentZ, tile);   // derecha
        }

        private static void Face4(Sink b, int a, int c, int d, int e, double nx, double ny, double nz) => b.Face(a, c, d, e, nx, ny, nz);

        private static void Side(Sink b, double xa, double za, double xb, double zb, double y0, double y1, double nx, double nz, float tile)
        {
            double length = Math.Sqrt((xb - xa) * (xb - xa) + (zb - za) * (zb - za));
            if (length < 1e-9) return;
            int aB = b.Add(xa, y0, za, 0.0, y0 / tile), bB = b.Add(xb, y0, zb, length / tile, y0 / tile);
            int bT = b.Add(xb, y1, zb, length / tile, y1 / tile), aT = b.Add(xa, y1, za, 0.0, y1 / tile);
            b.Face(aB, bB, bT, aT, nx, 0.0, nz);
        }

        /// <summary>Recuadro horizontal mirando hacia arriba, con la mitad derecha de la textura de huecos (la de la saetera).</summary>
        private static void Quad(Sink b, Machicolation m, double s0, double s1, double d0, double d1, double y)
        {
            At(m, s0, d0, out double x00, out double z00); At(m, s1, d0, out double x10, out double z10);
            At(m, s1, d1, out double x11, out double z11); At(m, s0, d1, out double x01, out double z01);
            int a = b.Add(x00, y, z00, 0.5, 0.0), c = b.Add(x10, y, z10, 1.0, 0.0), d = b.Add(x11, y, z11, 1.0, 1.0), e = b.Add(x01, y, z01, 0.5, 1.0);
            b.Face(a, c, d, e, 0, 1, 0);
        }
    }
}
