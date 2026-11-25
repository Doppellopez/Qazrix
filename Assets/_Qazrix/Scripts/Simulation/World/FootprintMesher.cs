using System;
using System.Collections.Generic;

namespace Qazrix.Simulation.World
{
    /// <summary>Malla de un trozo de ciudad levantada sobre huellas poligonales: vértices, UV y triángulos de tejado y de muro.</summary>
    public sealed class FootprintChunk
    {
        public float[] Vertices;      // x, y, z seguidos
        public float[] Uvs;           // u, v seguidos
        public int[] RoofTriangles;
        public int[] WallTriangles;
        /// <summary>Paredes de los edificios con foto: una lista de triángulos por foto (vacía si no hay fotos).</summary>
        public int[][] PhotoWallTriangles = new int[0][];

        public int VertexCount => Vertices.Length / 3;
    }

    /// <summary>
    /// Levanta los edificios de <see cref="BuildingFootprints"/>: un tejado plano por polígono y un muro por cada lado
    /// del contorno, desde el tejado hasta el terreno (hundido un poco para no dejar huecos en pendiente). Los lados largos
    /// se parten en tramos para seguir el terreno. Sin dependencias de Unity; la cara vista es la de fuera (convenio de Unity).
    /// </summary>
    public static class FootprintMesher
    {
        /// <param name="groundAt">Cota del terreno en un punto (x, z).</param>
        /// <param name="sink">Cuánto baja el pie del muro bajo el terreno (m).</param>
        /// <param name="chunkMetres">Lado (m) de cada trozo de malla.</param>
        /// <param name="metersPerTile">Metros que cubre una repetición de la textura.</param>
        /// <param name="maxSegment">Largo máximo (m) de cada tramo de muro.</param>
        /// <param name="photoParts">Edificios cuyas paredes llevan una foto en vez del muro de piedra: parte → número de foto.</param>
        /// <param name="photoAspects">Ancho / alto de cada foto; la foto se repite a lo ancho sin deformarse.</param>
        /// <param name="doors">Puertas: en cada una se deja un hueco en el muro, con el dintel de piedra encima.</param>
        public static List<FootprintChunk> Build(List<BuildingPart> parts, Func<double, double, float> groundAt, float sink,
            double chunkMetres, float metersPerTile, double maxSegment = 3.0,
            IReadOnlyDictionary<BuildingPart, int> photoParts = null, IReadOnlyList<double> photoAspects = null,
            IReadOnlyList<DoorPlacement> doors = null)
        {
            int photoCount = photoAspects?.Count ?? 0;
            if (parts == null) throw new ArgumentNullException(nameof(parts));
            if (groundAt == null) throw new ArgumentNullException(nameof(groundAt));
            if (chunkMetres <= 0.0 || !(metersPerTile > 0f) || maxSegment <= 0.0) throw new ArgumentException("Tamaños no válidos.");

            var builders = new Dictionary<long, Builder>();
            foreach (BuildingPart part in parts)
            {
                long key = ((long)Math.Floor(part.Centre.X / chunkMetres) << 32) ^ ((long)Math.Floor(part.Centre.Z / chunkMetres) & 0xffffffffL);
                if (!builders.TryGetValue(key, out Builder b)) builders[key] = b = new Builder(photoCount);
                AddRoof(b, part, metersPerTile);
                int photo = -1;
                if (photoParts != null && photoParts.TryGetValue(part, out int found) && found >= 0 && found < photoCount) photo = found;
                AddWalls(b, part, groundAt, sink, metersPerTile, maxSegment, photo, photo >= 0 ? photoAspects[photo] : 0.0, doors);
            }

            var chunks = new List<FootprintChunk>();
            foreach (Builder b in builders.Values)
            {
                if (b.Roof.Count == 0) continue;
                chunks.Add(new FootprintChunk
                {
                    Vertices = b.Vertices.ToArray(),
                    Uvs = b.Uvs.ToArray(),
                    RoofTriangles = b.Roof.ToArray(),
                    WallTriangles = b.Wall.ToArray(),
                    PhotoWallTriangles = Array.ConvertAll(b.Photos, l => l.ToArray())
                });
            }
            return chunks;
        }

        private sealed class Builder
        {
            public readonly List<float> Vertices = new List<float>();
            public readonly List<float> Uvs = new List<float>();
            public readonly List<int> Roof = new List<int>();
            public readonly List<int> Wall = new List<int>();
            public readonly List<int>[] Photos;

            public Builder(int photoCount)
            {
                Photos = new List<int>[photoCount];
                for (int i = 0; i < photoCount; i++) Photos[i] = new List<int>();
            }

            public int Add(double x, double y, double z, double u, double v)
            {
                Vertices.Add((float)x); Vertices.Add((float)y); Vertices.Add((float)z);
                Uvs.Add((float)u); Uvs.Add((float)v);
                return Vertices.Count / 3 - 1;
            }

            /// <summary>Triángulo con la cara vista hacia (nx, ny, nz): si el orden dado mira hacia el otro lado, se invierte.</summary>
            public void Triangle(List<int> list, int a, int b, int c, double nx, double ny, double nz)
            {
                double ax = Vertices[a * 3], ay = Vertices[a * 3 + 1], az = Vertices[a * 3 + 2];
                double ux = Vertices[b * 3] - ax, uy = Vertices[b * 3 + 1] - ay, uz = Vertices[b * 3 + 2] - az;
                double vx = Vertices[c * 3] - ax, vy = Vertices[c * 3 + 1] - ay, vz = Vertices[c * 3 + 2] - az;
                double cx = uy * vz - uz * vy, cy = uz * vx - ux * vz, cz = ux * vy - uy * vx;
                if (cx * cx + cy * cy + cz * cz < 1e-18) return; // sin área
                if (cx * nx + cy * ny + cz * nz < 0) { int t = b; b = c; c = t; }
                list.Add(a); list.Add(b); list.Add(c);
            }
        }

        private static void AddRoof(Builder b, BuildingPart part, float metersPerTile)
        {
            int start = b.Vertices.Count / 3;
            for (int i = 0; i < part.RoofXZ.Length / 2; i++)
            {
                float x = part.RoofXZ[i * 2], z = part.RoofXZ[i * 2 + 1];
                b.Add(x, part.RoofHeight, z, x / metersPerTile, z / metersPerTile);
            }
            for (int t = 0; t + 2 < part.RoofTriangles.Length; t += 3)
                b.Triangle(b.Roof, start + part.RoofTriangles[t], start + part.RoofTriangles[t + 1], start + part.RoofTriangles[t + 2], 0, 1, 0);
        }

        private static void AddWalls(Builder b, BuildingPart part, Func<double, double, float> groundAt, float sink,
            float metersPerTile, double maxSegment, int photo, double photoAspect, IReadOnlyList<DoorPlacement> doors)
        {
            // Con foto: una repetición de la imagen cubre la altura del edificio y mide alto × proporción de ancho.
            List<int> walls = photo >= 0 ? b.Photos[photo] : b.Wall;
            double photoHeight = Math.Max(1.0, part.RoofHeight - groundAt(part.Centre.X, part.Centre.Z));
            double photoBase = part.RoofHeight - photoHeight;
            for (int loopIndex = 0; loopIndex < part.Loops.Count; loopIndex++)
            {
                Vec2[] loop = part.Loops[loopIndex];
                double along = 0.0;
                for (int i = 0; i < loop.Length; i++)
                {
                    Vec2 p = loop[i], q = loop[(i + 1) % loop.Length];
                    double dx = q.X - p.X, dz = q.Z - p.Z, length = Math.Sqrt(dx * dx + dz * dz);
                    if (length < 1e-9) continue;
                    // El edificio queda a la izquierda del sentido de recorrido: hacia fuera es la derecha.
                    double nx = dz / length, nz = -dx / length;

                    int segments = Math.Max(1, (int)Math.Ceiling(length / maxSegment));
                    // Puerta en este lado (solo en el contorno exterior y sin foto): se parte el muro en sus bordes y el tramo del
                    // hueco queda abierto hasta el dintel.
                    DoorPlacement door = null;
                    if (doors != null && loopIndex == 0 && photo < 0)
                        foreach (DoorPlacement d in doors)
                            if (d.Part == part && d.EdgeIndex == i) { door = d; break; }
                    var cuts = new List<double>();
                    for (int s = 0; s <= segments; s++) cuts.Add((double)s / segments);
                    if (door != null)
                    {
                        cuts.Add(door.AlongStart / length);
                        cuts.Add((door.AlongStart + door.Width) / length);
                        cuts.Sort();
                    }
                    for (int s = 0; s + 1 < cuts.Count; s++)
                    {
                        double t0 = cuts[s], t1 = cuts[s + 1];
                        if (t1 - t0 < 1e-9) continue;
                        double ax = p.X + dx * t0, az = p.Z + dz * t0, bx = p.X + dx * t1, bz = p.Z + dz * t1;
                        float top = part.RoofHeight;
                        float aBase = Math.Min(groundAt(ax, az) - sink, top), bBase = Math.Min(groundAt(bx, bz) - sink, top);
                        if (door != null)
                        {
                            double mid = (t0 + t1) * 0.5 * length;
                            if (mid > door.AlongStart && mid < door.AlongStart + door.Width)
                            {
                                // Sobre el hueco: solo el dintel, desde lo alto de la puerta hasta el tejado.
                                float lintel = (float)door.TopY;
                                if (lintel >= top - 0.05f) continue;
                                aBase = bBase = lintel;
                            }
                        }
                        double tile = photo >= 0 ? photoHeight * photoAspect : metersPerTile;
                        double u0 = (along + length * t0) / tile, u1 = (along + length * t1) / tile;
                        double vTop = photo >= 0 ? (top - photoBase) / photoHeight : top / metersPerTile;
                        double vA = photo >= 0 ? (aBase - photoBase) / photoHeight : aBase / metersPerTile;
                        double vB = photo >= 0 ? (bBase - photoBase) / photoHeight : bBase / metersPerTile;

                        int aT = b.Add(ax, top, az, u0, vTop), bT = b.Add(bx, top, bz, u1, vTop);
                        int aB = b.Add(ax, aBase, az, u0, vA), bB = b.Add(bx, bBase, bz, u1, vB);
                        b.Triangle(walls, bB, bT, aT, nx, 0, nz);
                        b.Triangle(walls, bB, aT, aB, nx, 0, nz);
                    }
                    along += length;
                }
            }
        }
    }

    /// <summary>
    /// Reparte ventanas y saeteras por las paredes de las huellas poligonales: un hueco cada tres metros a lo largo de cada
    /// lado, en cualquier dirección (también en diagonal), con la normal hacia fuera. Ventanas en las casas y saeteras en las
    /// torres. Sin dependencias de Unity.
    /// </summary>
    public static class FootprintOpenings
    {
        /// <param name="reserved">Matacanes: los huecos que chocarían con ellos no se ponen.</param>
        public static List<FacadeOpening> Place(List<BuildingPart> parts, Func<double, double, float> groundAt,
            FacadeOpeningRules rules, double spacingMetres = 3.0, IList<Machicolation> reserved = null,
            MachicolationRules machicolationRules = null)
        {
            if (parts == null) throw new ArgumentNullException(nameof(parts));
            if (groundAt == null) throw new ArgumentNullException(nameof(groundAt));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (spacingMetres <= 0.0) throw new ArgumentException("La separación entre huecos debe ser positiva.");

            var openings = new List<FacadeOpening>();
            if (reserved != null && machicolationRules == null) machicolationRules = new MachicolationRules();
            foreach (BuildingPart part in parts)
            {
                float top = part.RoofHeight;
                float rise = top - groundAt(part.Centre.X, part.Centre.Z);
                bool tower = part.VolumeArea <= rules.TowerMaxArea && rise >= rules.TowerMinHeight;
                float first = tower ? rules.SlitFirst : rules.WindowFirst;
                float every = tower ? rules.SlitEvery : rules.WindowEvery;
                float halfHeight = 0.5f * (tower ? rules.SlitHeight : rules.WindowHeight);

                foreach (Vec2[] loop in part.Loops)
                {
                    for (int i = 0; i < loop.Length; i++)
                    {
                        Vec2 p = loop[i], q = loop[(i + 1) % loop.Length];
                        double dx = q.X - p.X, dz = q.Z - p.Z, length = Math.Sqrt(dx * dx + dz * dz);
                        int columns = (int)Math.Floor(length / spacingMetres);
                        if (columns < 1) continue; // lados cortos (los chaflanes de esquina) no llevan hueco
                        float nx = (float)(dz / length), nz = (float)(-dx / length);

                        for (int k = 0; k < columns; k++)
                        {
                            double t = (k + 0.5) / columns;
                            double x = p.X + dx * t, z = p.Z + dz * t;
                            float bottom = groundAt(x, z);
                            for (float y = bottom + first; y + halfHeight + rules.TopMargin <= top; y += every)
                            {
                                var opening = new FacadeOpening
                                {
                                    X = (float)x, Y = y, Z = (float)z, NormalX = nx, NormalZ = nz,
                                    Kind = tower ? OpeningKind.ArrowSlit : OpeningKind.Window
                                };
                                if (IsReserved(reserved, opening, halfHeight, machicolationRules)) continue; // ahí va un matacán
                                openings.Add(opening);
                            }
                        }
                    }
                }
            }
            return openings;
        }

        private static bool IsReserved(IList<Machicolation> reserved, FacadeOpening opening, float halfHeight, MachicolationRules rules)
        {
            if (reserved == null) return false;
            for (int i = 0; i < reserved.Count; i++)
                if (MachicolationPlanner.BlocksOpening(reserved[i], opening, halfHeight, rules)) return true;
            return false;
        }
    }
}
