using System;
using System.Collections.Generic;

namespace Qazrix.Simulation.World
{
    /// <summary>Una gárgola puesta en una pared: dónde, hacia dónde mira y a qué altura queda su arranque.</summary>
    public sealed class GargoyleSpot
    {
        public int CatalogId;
        public bool Eroded;
        public double X, Z;                   // sobre la pared
        public float NormalX, NormalZ;        // normal horizontal hacia fuera (unitaria)
        public float Height;                  // cota del arranque
        public int PartIndex, EdgeIndex;      // edificio y lado de su contorno exterior
        public string Group;
    }

    /// <summary>Gárgolas que van juntas en torno a un punto del mapa.</summary>
    public sealed class GargoyleGroup
    {
        public string Name;
        public double X, Z;                   // punto de referencia, en metros locales
        public double Radius = 60.0;          // hasta dónde se buscan paredes
        /// <summary>true: repartidas alrededor (para rodear un edificio); false: las paredes más cercanas que miren al punto.</summary>
        public bool Spread;
        /// <summary>Si no es negativo, solo valen las paredes de ese edificio.</summary>
        public int OnlyPart = -1;
        /// <summary>Altura mínima y máxima (m) del tejado sobre la calle.</summary>
        public float MinRise = 4.5f, MaxRise = 16f;
        public int[] Ids = Array.Empty<int>();
        public int[] ErodedIds = Array.Empty<int>(); // cuáles de Ids van en su variante erosionada
    }

    /// <summary>Medidas del reparto. Valores de prototipo, sin base documental.</summary>
    public sealed class GargoyleRules
    {
        /// <summary>Lado recto mínimo (m), margen a las esquinas (m) y paso entre huecos de un mismo lado (m).</summary>
        public double MinEdge = 1.6, EdgeMargin = 0.7, SlotSpacing = 2.0;
        /// <summary>El arranque queda tanto por debajo del tejado (m): la figura asoma sobre la cornisa.</summary>
        public float DropBelowRoof = 0.45f;
        /// <summary>Distancias (m) hacia fuera de la pared que deben estar libres de edificios.</summary>
        public double[] ClearProbes = { 0.5, 1.2, 2.2 };
        /// <summary>Distancia mínima (m) entre dos gárgolas y a un matacán.</summary>
        public double MinSeparation = 3.5, MachicolationClearance = 3.0;
        /// <summary>Metros de penalización a las paredes que no miran al punto de referencia.</summary>
        public double TurnedAwayPenalty = 25.0;
    }

    /// <summary>
    /// Reparte las gárgolas por las paredes de los edificios: bajo la cornisa, sobre un lado que dé a un espacio libre,
    /// lejos de los matacanes y separadas entre sí. Sin azar y sin dependencias de Unity.
    /// </summary>
    public static class GargoylePlanner
    {
        public static List<GargoyleSpot> Place(List<BuildingPart> parts, FootprintIndex index, Func<double, double, float> groundAt,
            IReadOnlyList<GargoyleGroup> groups, ICollection<int> excludedParts = null, IReadOnlyList<Machicolation> machicolations = null,
            GargoyleRules rules = null)
        {
            if (parts == null) throw new ArgumentNullException(nameof(parts));
            if (index == null) throw new ArgumentNullException(nameof(index));
            if (groundAt == null) throw new ArgumentNullException(nameof(groundAt));
            if (groups == null) throw new ArgumentNullException(nameof(groups));
            rules = rules ?? new GargoyleRules();

            var placed = new List<GargoyleSpot>();
            foreach (GargoyleGroup group in groups)
            {
                List<GargoyleSpot> free = Candidates(parts, index, groundAt, group, excludedParts, machicolations, rules);
                foreach (int id in group.Ids)
                {
                    GargoyleSpot best = null;
                    double bestScore = double.MaxValue;
                    foreach (GargoyleSpot c in free)
                    {
                        if (TooClose(c, placed, rules.MinSeparation)) continue;
                        double score = Score(c, group, placed, rules);
                        if (score < bestScore) { bestScore = score; best = c; }
                    }
                    if (best == null) break; // no quedan paredes: el grupo se queda incompleto
                    free.Remove(best);
                    best.CatalogId = id;
                    best.Eroded = Array.IndexOf(group.ErodedIds, id) >= 0;
                    placed.Add(best);
                }
            }
            return placed;
        }

        /// <summary>Menor es mejor.</summary>
        private static double Score(GargoyleSpot c, GargoyleGroup group, List<GargoyleSpot> placed, GargoyleRules rules)
        {
            double toAnchor = Distance(c.X, c.Z, group.X, group.Z);
            if (group.Spread)
            {
                // Lo más lejos posible de las ya puestas del mismo grupo; la primera, la más cercana al punto.
                double nearest = double.MaxValue;
                foreach (GargoyleSpot other in placed)
                    if (other.Group == group.Name) nearest = Math.Min(nearest, Distance(c.X, c.Z, other.X, other.Z));
                return nearest == double.MaxValue ? toAnchor : -nearest;
            }
            bool facing = (group.X - c.X) * c.NormalX + (group.Z - c.Z) * c.NormalZ > 0.0;
            return toAnchor + (facing ? 0.0 : rules.TurnedAwayPenalty);
        }

        private static List<GargoyleSpot> Candidates(List<BuildingPart> parts, FootprintIndex index, Func<double, double, float> groundAt,
            GargoyleGroup group, ICollection<int> excluded, IReadOnlyList<Machicolation> machicolations, GargoyleRules rules)
        {
            var result = new List<GargoyleSpot>();
            for (int n = 0; n < parts.Count; n++)
            {
                if (excluded != null && excluded.Contains(n)) continue;
                if (group.OnlyPart >= 0 && n != group.OnlyPart) continue;
                BuildingPart part = parts[n];
                Vec2[] loop = part.Loops[0];
                for (int i = 0; i < loop.Length; i++)
                {
                    Vec2 a = loop[i], b = loop[(i + 1) % loop.Length];
                    double ex = b.X - a.X, ez = b.Z - a.Z, length = Math.Sqrt(ex * ex + ez * ez);
                    if (length < rules.MinEdge) continue;
                    double tx = ex / length, tz = ez / length;
                    double nx = tz, nz = -tx; // contorno antihorario: la calle queda a la derecha

                    double usable = Math.Max(0.0, length - 2.0 * rules.EdgeMargin);
                    int slots = (int)Math.Floor(usable / rules.SlotSpacing) + 1;
                    double first = length * 0.5 - (slots - 1) * rules.SlotSpacing * 0.5;
                    for (int k = 0; k < slots; k++)
                    {
                        double s = first + k * rules.SlotSpacing;
                        double x = a.X + tx * s, z = a.Z + tz * s;
                        if (Distance(x, z, group.X, group.Z) > group.Radius) continue;
                        float rise = part.RoofHeight - groundAt(x + nx, z + nz);
                        if (rise < group.MinRise || rise > group.MaxRise) continue;
                        if (!IsClear(index, x, z, nx, nz, tx, tz, rules)) continue;
                        float height = part.RoofHeight - rules.DropBelowRoof;
                        if (NearMachicolation(machicolations, x, z, part.RoofHeight, rules)) continue;
                        result.Add(new GargoyleSpot
                        {
                            X = x, Z = z, NormalX = (float)nx, NormalZ = (float)nz, Height = height,
                            PartIndex = n, EdgeIndex = i, Group = group.Name
                        });
                    }
                }
            }
            return result;
        }

        private static bool IsClear(FootprintIndex index, double x, double z, double nx, double nz, double tx, double tz, GargoyleRules rules)
        {
            foreach (double d in rules.ClearProbes)
                foreach (double side in new[] { -0.4, 0.0, 0.4 })
                    if (index.At(x + nx * d + tx * side, z + nz * d + tz * side) != null) return false;
            return true;
        }

        private static bool NearMachicolation(IReadOnlyList<Machicolation> machicolations, double x, double z, float roof, GargoyleRules rules)
        {
            if (machicolations == null) return false;
            foreach (Machicolation m in machicolations)
                if (Math.Abs(m.RoofHeight - roof) < 3f && Distance(x, z, m.X, m.Z) < rules.MachicolationClearance) return true;
            return false;
        }

        private static bool TooClose(GargoyleSpot c, List<GargoyleSpot> placed, double separation)
        {
            foreach (GargoyleSpot other in placed)
                if (Distance(c.X, c.Z, other.X, other.Z) < separation) return true;
            return false;
        }

        private static double Distance(double ax, double az, double bx, double bz) =>
            Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));
    }
}
