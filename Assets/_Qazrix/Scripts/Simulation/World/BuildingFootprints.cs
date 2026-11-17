using System;
using System.Collections.Generic;

namespace Qazrix.Simulation.World
{
    /// <summary>Punto en planta: X hacia el este, Z hacia el norte (metros locales).</summary>
    public struct Vec2
    {
        public double X, Z;

        public Vec2(double x, double z)
        {
            X = x;
            Z = z;
        }
    }

    /// <summary>Medidas del paso de rejilla a polígonos. Valores de prototipo.</summary>
    public sealed class FootprintOptions
    {
        /// <summary>Desviación máxima (m) al simplificar un contorno: lo que se aparte menos que esto de una recta se enderezará.</summary>
        public double Tolerance = 1.5;

        /// <summary>Lado más corto (m) que se acepta: los menores se funden con su vecino si la pared no se desvía más que la tolerancia.</summary>
        public double MinEdge = 2.0;

        /// <summary>Corte en diagonal de cada esquina (m, medido sobre cada lado). 0 = esquinas vivas.</summary>
        public double ChamferSize = 2.0;

        /// <summary>Ángulo mínimo de giro (grados) para que un vértice cuente como esquina y se corte.</summary>
        public double MinCornerAngle = 25.0;

        /// <summary>Corte más pequeño (m) que merece la pena hacer.</summary>
        public double MinChamfer = 0.6;

        /// <summary>Solo se corta una esquina si sus dos lados miden al menos esto (m): así no quedan trocitos de pared entre dos cortes seguidos.</summary>
        public double MinCornerEdge = 4.0;

        /// <summary>Salto de cota (m) a partir del cual dos celdas vecinas son edificios distintos.</summary>
        public float Step = 0.5f;
    }

    /// <summary>
    /// Un edificio (o una parte de él) como polígono en planta con tejado plano: un contorno exterior y, si los hay,
    /// agujeros (patios). Los lados son rectos y pueden ir en cualquier dirección.
    /// </summary>
    public sealed class BuildingPart
    {
        /// <summary>Cota del tejado (m), igual en todo el edificio.</summary>
        public float RoofHeight;

        /// <summary>Superficie total del volumen al que pertenece (m²), contando todas sus partes.</summary>
        public double VolumeArea;

        /// <summary>Superficie de esta parte (m²), descontando los patios.</summary>
        public double Area;

        /// <summary>Contornos: el primero es el exterior (antihorario: el edificio queda a la izquierda); el resto, agujeros (horarios).</summary>
        public List<Vec2[]> Loops = new List<Vec2[]>();

        /// <summary>Triángulos del tejado: pares (x, z) y trío de índices a ese arreglo de puntos.</summary>
        public float[] RoofXZ;
        public int[] RoofTriangles;

        /// <summary>Centro aproximado del contorno exterior.</summary>
        public Vec2 Centre;

        /// <summary>Cuántos vértices tenía el contorno exterior en la rejilla antes de simplificarlo.</summary>
        public int RawVertexCount;

        /// <summary>El contorno se ha simplificado (true) o se ha dejado como salía de la rejilla (false).</summary>
        public bool Simplified;

        /// <summary>El tejado se ha tenido que rellenar por celdas porque el polígono no se pudo triangular.</summary>
        public bool CellRoof;
    }

    /// <summary>
    /// Pasa la rejilla de tejados (1 m por celda, bordes dentados del LiDAR) a edificios con huella poligonal: agrupa
    /// las celdas en volúmenes, recorre su contorno, lo endereza (cada pared pasa a ser un solo segmento recto, también
    /// en diagonal), corta en diagonal las esquinas y triangula el tejado. Sin dependencias de Unity.
    /// </summary>
    public static class BuildingFootprints
    {
        /// <param name="roofs">Cota de tejado por celda, filas de norte a sur; NaN = sin edificio.</param>
        public static List<BuildingPart> Extract(float[] roofs, int columns, int rows, float cellSize, float westX, float northZ,
            FootprintOptions options = null)
        {
            if (roofs == null) throw new ArgumentNullException(nameof(roofs));
            if (columns < 1 || rows < 1 || roofs.Length != columns * rows) throw new ArgumentException("La rejilla de tejados no cuadra con sus dimensiones.");
            if (cellSize <= 0f) throw new ArgumentException("Tamaño de celda no válido.");
            options = options ?? new FootprintOptions();

            int[] label = Label(roofs, columns, rows, options.Step, out int labelCount);
            var cellsOf = new List<int>[labelCount];
            for (int i = 0; i < labelCount; i++) cellsOf[i] = new List<int>();
            for (int cell = 0; cell < label.Length; cell++) if (label[cell] >= 0) cellsOf[label[cell]].Add(cell);

            var parts = new List<BuildingPart>();
            for (int id = 0; id < labelCount; id++)
            {
                List<int> cells = cellsOf[id];
                float height = Median(roofs, cells);
                double volumeArea = cells.Count * (double)cellSize * cellSize;

                List<Vec2[]> rawLoops = TraceLoops(label, id, columns, rows, cells, cellSize, westX, northZ);
                var outers = new List<Vec2[]>();
                var holes = new List<Vec2[]>();
                foreach (Vec2[] loop in rawLoops)
                {
                    if (SignedArea(loop) > 0) outers.Add(loop);
                    else holes.Add(loop);
                }

                foreach (Vec2[] outer in outers)
                {
                    var part = new BuildingPart { RoofHeight = height, VolumeArea = volumeArea, RawVertexCount = outer.Length };
                    var own = new List<Vec2[]> { outer };
                    foreach (Vec2[] hole in holes)
                        if (outers.Count == 1 || ContainsAny(outer, hole)) own.Add(hole);
                    Finish(part, own, options, cells, columns, cellSize, westX, northZ, id, label);
                    parts.Add(part);
                }
            }
            return parts;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Volúmenes y contornos de la rejilla
        // ---------------------------------------------------------------------------------------------------------

        private static int[] Label(float[] roofs, int columns, int rows, float step, out int count)
        {
            var label = new int[roofs.Length];
            for (int i = 0; i < label.Length; i++) label[i] = -1;
            int labels = 0;
            var queue = new Queue<int>();
            for (int start = 0; start < roofs.Length; start++)
            {
                if (label[start] >= 0 || float.IsNaN(roofs[start])) continue;
                int current = labels++;
                label[start] = current;
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    int cell = queue.Dequeue();
                    int c = cell % columns, r = cell / columns;
                    Visit(c + 1, r); Visit(c - 1, r); Visit(c, r + 1); Visit(c, r - 1);

                    void Visit(int nc, int nr)
                    {
                        if (nc < 0 || nc >= columns || nr < 0 || nr >= rows) return;
                        int next = nr * columns + nc;
                        if (label[next] >= 0 || float.IsNaN(roofs[next])) return;
                        if (Math.Abs(roofs[next] - roofs[cell]) > step) return;
                        label[next] = current;
                        queue.Enqueue(next);
                    }
                }
            }
            count = labels;
            return label;
        }

        private static float Median(float[] roofs, List<int> cells)
        {
            var heights = new float[cells.Count];
            for (int i = 0; i < heights.Length; i++) heights[i] = roofs[cells[i]];
            Array.Sort(heights);
            return heights[heights.Length / 2];
        }

        /// <summary>Contornos del volumen sobre la rejilla: bordes de celda con el edificio a la izquierda, unidos en bucles.</summary>
        private static List<Vec2[]> TraceLoops(int[] label, int id, int columns, int rows, List<int> cells,
            float cellSize, float westX, float northZ)
        {
            int stride = columns + 1;
            var outgoing = new Dictionary<int, List<int>>();

            void Edge(int fromI, int fromJ, int toI, int toJ)
            {
                int from = fromJ * stride + fromI;
                if (!outgoing.TryGetValue(from, out List<int> list)) outgoing[from] = list = new List<int>(2);
                list.Add(toJ * stride + toI);
            }

            bool Same(int c, int r) => c >= 0 && c < columns && r >= 0 && r < rows && label[r * columns + c] == id;

            foreach (int cell in cells)
            {
                int c = cell % columns, r = cell / columns;
                // Sentido antihorario visto desde arriba (x al este, z al norte): SO → SE → NE → NO.
                if (!Same(c, r + 1)) Edge(c, r + 1, c + 1, r + 1);   // lado sur, hacia el este
                if (!Same(c + 1, r)) Edge(c + 1, r + 1, c + 1, r);   // lado este, hacia el norte
                if (!Same(c, r - 1)) Edge(c + 1, r, c, r);           // lado norte, hacia el oeste
                if (!Same(c - 1, r)) Edge(c, r, c, r + 1);           // lado oeste, hacia el sur
            }

            var loops = new List<Vec2[]>();
            var keys = new List<int>(outgoing.Keys);
            keys.Sort();
            foreach (int startKey in keys)
            {
                while (outgoing[startKey].Count > 0)
                {
                    var chain = new List<int> { startKey };
                    int current = startKey;
                    int previous = -1;
                    while (true)
                    {
                        List<int> options = outgoing[current];
                        if (options.Count == 0) break;
                        int pick = 0;
                        if (previous >= 0 && options.Count > 1) pick = MostLeft(options, previous, current, stride);
                        int next = options[pick];
                        options.RemoveAt(pick);
                        previous = current;
                        current = next;
                        if (current == startKey) break;
                        chain.Add(current);
                    }

                    // Quita los vértices en línea recta (la rejilla tiene uno por celda en cada lado).
                    var points = new List<Vec2>();
                    for (int i = 0; i < chain.Count; i++)
                    {
                        int pi = chain[(i + chain.Count - 1) % chain.Count], ci = chain[i], ni = chain[(i + 1) % chain.Count];
                        if (IsStraight(pi, ci, ni, stride)) continue;
                        points.Add(new Vec2(westX + (ci % stride) * cellSize, northZ - (ci / stride) * cellSize));
                    }
                    if (points.Count >= 3) loops.Add(points.ToArray());
                }
            }
            return loops;
        }

        /// <summary>Entre varias aristas que salen de un vértice, la que gira más a la izquierda: así los edificios que se tocan solo en una esquina quedan en bucles separados.</summary>
        private static int MostLeft(List<int> options, int previous, int current, int stride)
        {
            double inX = (current % stride) - (previous % stride), inZ = -((current / stride) - (previous / stride));
            int best = 0;
            double bestScore = double.MinValue;
            for (int i = 0; i < options.Count; i++)
            {
                double outX = (options[i] % stride) - (current % stride), outZ = -((options[i] / stride) - (current / stride));
                double cross = inX * outZ - inZ * outX, dot = inX * outX + inZ * outZ;
                double score = cross > 0 ? 3 : (cross < 0 ? 1 : (dot > 0 ? 2 : 0));
                if (score > bestScore) { bestScore = score; best = i; }
            }
            return best;
        }

        private static bool IsStraight(int previous, int current, int next, int stride)
        {
            int ax = (current % stride) - (previous % stride), az = (current / stride) - (previous / stride);
            int bx = (next % stride) - (current % stride), bz = (next / stride) - (current / stride);
            return ax * bz - az * bx == 0 && ax * bx + az * bz > 0;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Simplificar, cortar esquinas y triangular
        // ---------------------------------------------------------------------------------------------------------

        private static void Finish(BuildingPart part, List<Vec2[]> raw, FootprintOptions o, List<int> cells, int columns,
            float cellSize, float westX, float northZ, int id, int[] label)
        {
            List<Vec2[]> chosen = raw;
            part.Simplified = false;

            // El contorno de la rejilla es siempre válido; los simplificados se aceptan solo si siguen siendo polígonos simples.
            foreach (double tolerance in new[] { o.Tolerance, o.Tolerance * 0.5, o.Tolerance * 0.25 })
            {
                if (tolerance <= 0.0) break;
                var candidate = new List<Vec2[]>();
                foreach (Vec2[] loop in raw)
                {
                    // Se endereza, se funden los lados cortos con sus vecinos y se vuelve a enderezar.
                    Vec2[] straight = SimplifyClosed(loop, tolerance);
                    straight = MergeShortEdges(straight, o.MinEdge, tolerance);
                    candidate.Add(SimplifyClosed(straight, tolerance));
                }
                if (!IsSimple(candidate)) continue;
                chosen = candidate;
                part.Simplified = true;
                break;
            }

            // Esquinas cortadas en diagonal; si el corte hace que el contorno se cruce, se prueba con la mitad y después sin corte.
            for (double size = o.ChamferSize; size >= o.MinChamfer; size *= 0.5)
            {
                List<Vec2[]> cut = Chamfer(chosen, size, o);
                if (!IsSimple(cut)) continue;
                chosen = cut;
                break;
            }

            part.Loops = chosen;
            if (!TriangulateLoops(chosen, out part.RoofXZ, out part.RoofTriangles))
            {
                // Último recurso: el contorno tal cual sale de la rejilla y el tejado por celdas.
                part.Loops = raw;
                part.Simplified = false;
                if (!TriangulateLoops(raw, out part.RoofXZ, out part.RoofTriangles))
                {
                    CellRoof(cells, columns, cellSize, westX, northZ, id, label, out part.RoofXZ, out part.RoofTriangles);
                    part.CellRoof = true;
                }
            }

            double area = 0.0;
            for (int i = 0; i < part.Loops.Count; i++) area += SignedArea(part.Loops[i]);
            part.Area = area;
            double sx = 0.0, sz = 0.0;
            foreach (Vec2 p in part.Loops[0]) { sx += p.X; sz += p.Z; }
            part.Centre = new Vec2(sx / part.Loops[0].Length, sz / part.Loops[0].Length);
        }

        /// <summary>Douglas–Peucker sobre un contorno cerrado.</summary>
        public static Vec2[] SimplifyClosed(Vec2[] points, double tolerance)
        {
            int n = points.Length;
            if (n <= 4) return points;

            int far = 0;
            double best = -1.0;
            for (int i = 1; i < n; i++)
            {
                double d = Dist2(points[0], points[i]);
                if (d > best) { best = d; far = i; }
            }

            var keep = new bool[n];
            keep[0] = keep[far] = true;
            Reduce(points, 0, far, tolerance, keep);
            // Segundo tramo: de far hasta el punto 0 pasando por el final del arreglo.
            var second = new List<Vec2>();
            for (int i = far; i < n; i++) second.Add(points[i]);
            second.Add(points[0]);
            var secondKeep = new bool[second.Count];
            secondKeep[0] = secondKeep[second.Count - 1] = true;
            Reduce(second.ToArray(), 0, second.Count - 1, tolerance, secondKeep);
            for (int i = 1; i < second.Count - 1; i++) if (secondKeep[i]) keep[far + i] = true;

            var result = new List<Vec2>();
            for (int i = 0; i < n; i++) if (keep[i]) result.Add(points[i]);
            return result.Count >= 3 ? result.ToArray() : points;
        }

        /// <summary>
        /// Funde los lados más cortos que <paramref name="minEdge"/>: quita el extremo que menos se aparta de la recta que
        /// forman sus vecinos, mientras esa desviación no pase de <paramref name="maxDeviation"/>. Así desaparecen
        /// las muescas y los peldaños pequeños de una pared, pero no las esquinas de verdad.
        /// </summary>
        public static Vec2[] MergeShortEdges(Vec2[] loop, double minEdge, double maxDeviation)
        {
            if (minEdge <= 0.0 || loop.Length <= 4) return loop;
            var pts = new List<Vec2>(loop);
            for (int pass = 0; pass < 200 && pts.Count > 4; pass++)
            {
                bool removed = false;
                int n = pts.Count;
                // Los lados más cortos primero.
                var order = new List<int>();
                for (int i = 0; i < n; i++) if (Math.Sqrt(Dist2(pts[i], pts[(i + 1) % n])) < minEdge) order.Add(i);
                order.Sort((a, b) => Dist2(pts[a], pts[(a + 1) % n]).CompareTo(Dist2(pts[b], pts[(b + 1) % n])));

                foreach (int i in order)
                {
                    int a = i, b = (i + 1) % n, before = (i + n - 1) % n, after = (i + 2) % n;
                    double deviationA = DistanceToSegment(pts[a], pts[before], pts[b]);
                    double deviationB = DistanceToSegment(pts[b], pts[a], pts[after]);
                    int drop = deviationA <= deviationB ? a : b;
                    if (Math.Min(deviationA, deviationB) > maxDeviation) continue;
                    pts.RemoveAt(drop);
                    removed = true;
                    break;
                }
                if (!removed) break;
            }
            return pts.ToArray();
        }

        private static void Reduce(Vec2[] points, int first, int last, double tolerance, bool[] keep)
        {
            if (last <= first + 1) return;
            double worst = -1.0;
            int index = -1;
            for (int i = first + 1; i < last; i++)
            {
                double d = DistanceToSegment(points[i], points[first], points[last]);
                if (d > worst) { worst = d; index = i; }
            }
            if (worst <= tolerance) return;
            keep[index] = true;
            Reduce(points, first, index, tolerance, keep);
            Reduce(points, index, last, tolerance, keep);
        }

        /// <summary>
        /// Corta en diagonal las esquinas de los contornos: cada vértice que gira lo bastante se sustituye por dos puntos
        /// sobre sus lados, a <paramref name="size"/> metros (o menos, si el lado es corto).
        /// </summary>
        public static List<Vec2[]> Chamfer(List<Vec2[]> loops, double size, FootprintOptions o)
        {
            var result = new List<Vec2[]>();
            double cosLimit = Math.Cos(o.MinCornerAngle * Math.PI / 180.0);
            foreach (Vec2[] loop in loops)
            {
                int n = loop.Length;
                var points = new List<Vec2>();
                for (int i = 0; i < n; i++)
                {
                    Vec2 p = loop[(i + n - 1) % n], v = loop[i], q = loop[(i + 1) % n];
                    double e1x = v.X - p.X, e1z = v.Z - p.Z, e2x = q.X - v.X, e2z = q.Z - v.Z;
                    double l1 = Math.Sqrt(e1x * e1x + e1z * e1z), l2 = Math.Sqrt(e2x * e2x + e2z * e2z);
                    if (l1 < 1e-9 || l2 < 1e-9) { points.Add(v); continue; }
                    double cos = (e1x * e2x + e1z * e2z) / (l1 * l2);
                    double d = Math.Min(size, Math.Min(0.3 * l1, 0.3 * l2));
                    if (cos > cosLimit || d < o.MinChamfer || l1 < o.MinCornerEdge || l2 < o.MinCornerEdge)
                    {
                        points.Add(v); // casi recto o demasiado pequeño para cortar
                        continue;
                    }
                    points.Add(new Vec2(v.X - e1x / l1 * d, v.Z - e1z / l1 * d));
                    points.Add(new Vec2(v.X + e2x / l2 * d, v.Z + e2z / l2 * d));
                }
                result.Add(points.ToArray());
            }
            return result;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Geometría básica
        // ---------------------------------------------------------------------------------------------------------

        public static double SignedArea(Vec2[] loop)
        {
            double sum = 0.0;
            for (int i = 0; i < loop.Length; i++)
            {
                Vec2 a = loop[i], b = loop[(i + 1) % loop.Length];
                sum += a.X * b.Z - b.X * a.Z;
            }
            return sum * 0.5;
        }

        private static double Dist2(Vec2 a, Vec2 b)
        {
            double dx = a.X - b.X, dz = a.Z - b.Z;
            return dx * dx + dz * dz;
        }

        private static double DistanceToSegment(Vec2 p, Vec2 a, Vec2 b)
        {
            double dx = b.X - a.X, dz = b.Z - a.Z;
            double len2 = dx * dx + dz * dz;
            if (len2 < 1e-18) return Math.Sqrt(Dist2(p, a));
            double t = Math.Max(0.0, Math.Min(1.0, ((p.X - a.X) * dx + (p.Z - a.Z) * dz) / len2));
            return Math.Sqrt(Dist2(p, new Vec2(a.X + t * dx, a.Z + t * dz)));
        }

        /// <summary>¿Está el punto dentro del contorno? (par-impar).</summary>
        public static bool Inside(Vec2[] loop, Vec2 p)
        {
            bool inside = false;
            for (int i = 0, j = loop.Length - 1; i < loop.Length; j = i++)
            {
                Vec2 a = loop[i], b = loop[j];
                if ((a.Z > p.Z) != (b.Z > p.Z) && p.X < (b.X - a.X) * (p.Z - a.Z) / (b.Z - a.Z) + a.X) inside = !inside;
            }
            return inside;
        }

        private static bool ContainsAny(Vec2[] outer, Vec2[] hole)
        {
            foreach (Vec2 p in hole) if (Inside(outer, p)) return true;
            return false;
        }

        /// <summary>¿Es un conjunto de contornos simple? Ningún lado cruza o toca a otro, salvo en un vértice común.</summary>
        public static bool IsSimple(List<Vec2[]> loops)
        {
            var edges = new List<Vec2[]>();
            var owner = new List<int>();
            var position = new List<int>();
            for (int l = 0; l < loops.Count; l++)
            {
                Vec2[] loop = loops[l];
                if (loop.Length < 3) return false;
                for (int i = 0; i < loop.Length; i++)
                {
                    edges.Add(new[] { loop[i], loop[(i + 1) % loop.Length] });
                    owner.Add(l);
                    position.Add(i);
                }
            }

            for (int i = 0; i < edges.Count; i++)
            {
                for (int j = i + 1; j < edges.Count; j++)
                {
                    bool adjacent = owner[i] == owner[j]
                        && (position[j] == position[i] + 1 || (position[i] == 0 && position[j] == loops[owner[i]].Length - 1));
                    if (SegmentsClash(edges[i][0], edges[i][1], edges[j][0], edges[j][1], adjacent)) return false;
                }
            }
            return true;
        }

        private const double Eps = 1e-9;

        /// <summary>Dos lados chocan si se cruzan, o si uno toca al otro fuera de un extremo compartido.</summary>
        private static bool SegmentsClash(Vec2 a, Vec2 b, Vec2 c, Vec2 d, bool adjacent)
        {
            bool shareAC = Near(a, c), shareAD = Near(a, d), shareBC = Near(b, c), shareBD = Near(b, d);
            if (adjacent || shareAC || shareAD || shareBC || shareBD)
            {
                // Con un extremo común solo es un fallo si además se solapan en línea (uno entra en el otro).
                Vec2 s = shareAC || shareAD ? a : b;
                Vec2 u = shareAC || shareAD ? b : a;
                Vec2 v = shareAC || shareBC ? d : c;
                double cross = (u.X - s.X) * (v.Z - s.Z) - (u.Z - s.Z) * (v.X - s.X);
                double dot = (u.X - s.X) * (v.X - s.X) + (u.Z - s.Z) * (v.Z - s.Z);
                if (adjacent && !(shareAC || shareAD || shareBC || shareBD)) return false;
                return Math.Abs(cross) < Eps && dot > 0.0;
            }

            double d1 = Cross(c, d, a), d2 = Cross(c, d, b), d3 = Cross(a, b, c), d4 = Cross(a, b, d);
            if (((d1 > Eps && d2 < -Eps) || (d1 < -Eps && d2 > Eps)) && ((d3 > Eps && d4 < -Eps) || (d3 < -Eps && d4 > Eps))) return true;
            if (Math.Abs(d1) <= Eps && OnSegment(c, d, a)) return true;
            if (Math.Abs(d2) <= Eps && OnSegment(c, d, b)) return true;
            if (Math.Abs(d3) <= Eps && OnSegment(a, b, c)) return true;
            if (Math.Abs(d4) <= Eps && OnSegment(a, b, d)) return true;
            return false;
        }

        private static double Cross(Vec2 a, Vec2 b, Vec2 p) => (b.X - a.X) * (p.Z - a.Z) - (b.Z - a.Z) * (p.X - a.X);

        private static bool OnSegment(Vec2 a, Vec2 b, Vec2 p)
        {
            return p.X >= Math.Min(a.X, b.X) - Eps && p.X <= Math.Max(a.X, b.X) + Eps
                && p.Z >= Math.Min(a.Z, b.Z) - Eps && p.Z <= Math.Max(a.Z, b.Z) + Eps;
        }

        private static bool Near(Vec2 a, Vec2 b) => Math.Abs(a.X - b.X) < 1e-7 && Math.Abs(a.Z - b.Z) < 1e-7;

        // ---------------------------------------------------------------------------------------------------------
        // Triangulación del tejado: agujeros unidos al contorno por un puente y recorte de orejas
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Triangula un polígono con agujeros. Los triángulos salen antihorarios. Devuelve false si no lo consigue.</summary>
        public static bool TriangulateLoops(List<Vec2[]> loops, out float[] xz, out int[] triangles)
        {
            xz = null;
            triangles = null;
            var polygon = new List<Vec2>(loops[0]);

            // Los agujeros se unen de derecha a izquierda para que cada puente no cruce a los que quedan por unir.
            var holes = new List<Vec2[]>();
            for (int i = 1; i < loops.Count; i++) holes.Add(loops[i]);
            holes.Sort((a, b) => MaxX(b).CompareTo(MaxX(a)));

            for (int h = 0; h < holes.Count; h++)
            {
                var pending = new List<Vec2[]>();
                for (int k = h + 1; k < holes.Count; k++) pending.Add(holes[k]);
                if (!Bridge(polygon, holes[h], pending)) return false;
            }

            var index = new List<int>();
            for (int i = 0; i < polygon.Count; i++) index.Add(i);
            var tris = new List<int>();

            int safety = polygon.Count * polygon.Count + 16;
            while (index.Count > 3 && safety-- > 0)
            {
                bool clipped = false;
                for (int k = 0; k < index.Count && !clipped; k++)
                {
                    int ia = index[(k + index.Count - 1) % index.Count], ib = index[k], ic = index[(k + 1) % index.Count];
                    Vec2 a = polygon[ia], b = polygon[ib], c = polygon[ic];
                    if (Cross(a, b, c) <= Eps) continue; // cóncavo o degenerado

                    bool ear = true;
                    for (int m = 0; m < index.Count && ear; m++)
                    {
                        int j = index[m];
                        if (j == ia || j == ib || j == ic) continue;
                        Vec2 p = polygon[j];
                        if (Near(p, a) || Near(p, b) || Near(p, c)) continue; // vértice repetido por un puente
                        if (InTriangle(p, a, b, c)) ear = false;
                    }
                    if (!ear) continue;

                    tris.Add(ia); tris.Add(ib); tris.Add(ic);
                    index.RemoveAt(k);
                    clipped = true;
                }

                if (!clipped)
                {
                    // Sin orejas: se quita un vértice degenerado (en línea con sus vecinos), si lo hay.
                    int remove = -1;
                    for (int k = 0; k < index.Count; k++)
                    {
                        Vec2 a = polygon[index[(k + index.Count - 1) % index.Count]], b = polygon[index[k]], c = polygon[index[(k + 1) % index.Count]];
                        if (Math.Abs(Cross(a, b, c)) <= Eps) { remove = k; break; }
                    }
                    if (remove < 0) return false;
                    index.RemoveAt(remove);
                }
            }
            if (index.Count == 3)
            {
                Vec2 a = polygon[index[0]], b = polygon[index[1]], c = polygon[index[2]];
                if (Cross(a, b, c) > Eps) { tris.Add(index[0]); tris.Add(index[1]); tris.Add(index[2]); }
            }

            // El área triangulada debe ser la del polígono: si no, algo ha ido mal y se descarta.
            double expected = 0.0;
            for (int i = 0; i < loops.Count; i++) expected += SignedArea(loops[i]);
            double got = 0.0;
            for (int t = 0; t + 2 < tris.Count; t += 3) got += 0.5 * Cross(polygon[tris[t]], polygon[tris[t + 1]], polygon[tris[t + 2]]);
            if (tris.Count == 0 || Math.Abs(got - expected) > 1e-4 * Math.Max(1.0, Math.Abs(expected))) return false;

            xz = new float[polygon.Count * 2];
            for (int i = 0; i < polygon.Count; i++) { xz[i * 2] = (float)polygon[i].X; xz[i * 2 + 1] = (float)polygon[i].Z; }
            triangles = tris.ToArray();
            return true;
        }

        private static double MaxX(Vec2[] loop)
        {
            double max = double.MinValue;
            foreach (Vec2 p in loop) if (p.X > max) max = p.X;
            return max;
        }

        private static bool InTriangle(Vec2 p, Vec2 a, Vec2 b, Vec2 c)
        {
            return Cross(a, b, p) >= -Eps && Cross(b, c, p) >= -Eps && Cross(c, a, p) >= -Eps;
        }

        /// <summary>Une un agujero al polígono con un puente (un par de vértices que se ven) duplicando los dos extremos.</summary>
        private static bool Bridge(List<Vec2> polygon, Vec2[] hole, List<Vec2[]> pending)
        {
            var pairs = new List<(double distance, int hole, int outer)>();
            for (int h = 0; h < hole.Length; h++)
                for (int o = 0; o < polygon.Count; o++)
                    pairs.Add((Dist2(hole[h], polygon[o]), h, o));
            pairs.Sort((x, y) => x.distance.CompareTo(y.distance));

            foreach (var pair in pairs)
            {
                Vec2 from = hole[pair.hole], to = polygon[pair.outer];
                if (!BridgeIsClear(from, to, polygon, hole, pending)) continue;

                var merged = new List<Vec2>();
                for (int i = 0; i <= pair.outer; i++) merged.Add(polygon[i]);
                for (int k = 0; k <= hole.Length; k++) merged.Add(hole[(pair.hole + k) % hole.Length]);
                for (int i = pair.outer; i < polygon.Count; i++) merged.Add(polygon[i]);
                polygon.Clear();
                polygon.AddRange(merged);
                return true;
            }
            return false;
        }

        private static bool BridgeIsClear(Vec2 from, Vec2 to, List<Vec2> polygon, Vec2[] hole, List<Vec2[]> pending)
        {
            Vec2 mid = new Vec2((from.X + to.X) * 0.5, (from.Z + to.Z) * 0.5);
            if (!Inside(polygon.ToArray(), mid) || Inside(hole, mid)) return false;
            foreach (Vec2[] other in pending) if (Inside(other, mid)) return false;

            if (CrossesLoop(from, to, polygon.ToArray())) return false;
            if (CrossesLoop(from, to, hole)) return false;
            foreach (Vec2[] other in pending) if (CrossesLoop(from, to, other)) return false;
            return true;
        }

        private static bool CrossesLoop(Vec2 from, Vec2 to, Vec2[] loop)
        {
            for (int i = 0; i < loop.Length; i++)
            {
                Vec2 c = loop[i], d = loop[(i + 1) % loop.Length];
                // Los lados que parten de un extremo del puente no cuentan.
                if (Near(c, from) || Near(c, to) || Near(d, from) || Near(d, to))
                {
                    // Salvo que el lado vaya por encima del puente (misma recta).
                    continue;
                }
                if (SegmentsClash(from, to, c, d, false)) return true;
            }
            return false;
        }

        /// <summary>Tejado por celdas (dos triángulos por celda) para el caso de que ni el contorno de la rejilla se pueda triangular.</summary>
        private static void CellRoof(List<int> cells, int columns, float cellSize, float westX, float northZ, int id, int[] label,
            out float[] xz, out int[] triangles)
        {
            var points = new float[cells.Count * 8];
            var tris = new int[cells.Count * 6];
            for (int n = 0; n < cells.Count; n++)
            {
                int c = cells[n] % columns, r = cells[n] / columns;
                float x0 = westX + c * cellSize, x1 = x0 + cellSize, z0 = northZ - r * cellSize, z1 = z0 - cellSize;
                int p = n * 4;
                points[p * 2] = x0; points[p * 2 + 1] = z1;           // SO
                points[p * 2 + 2] = x1; points[p * 2 + 3] = z1;       // SE
                points[p * 2 + 4] = x1; points[p * 2 + 5] = z0;       // NE
                points[p * 2 + 6] = x0; points[p * 2 + 7] = z0;       // NO
                tris[n * 6] = p; tris[n * 6 + 1] = p + 1; tris[n * 6 + 2] = p + 2;
                tris[n * 6 + 3] = p; tris[n * 6 + 4] = p + 2; tris[n * 6 + 5] = p + 3;
            }
            xz = points;
            triangles = tris;
        }
    }

    /// <summary>
    /// Consulta rápida de «¿hay edificio en este punto?» sobre los polígonos: una rejilla de cubos con las partes
    /// cuyo rectángulo envolvente los toca. Sin dependencias de Unity.
    /// </summary>
    public sealed class FootprintIndex
    {
        private readonly List<BuildingPart> _parts;
        private readonly Dictionary<long, List<int>> _buckets = new Dictionary<long, List<int>>();
        private readonly double _bucket;

        public FootprintIndex(List<BuildingPart> parts, double bucketMetres = 16.0)
        {
            _parts = parts ?? throw new ArgumentNullException(nameof(parts));
            _bucket = bucketMetres;
            for (int n = 0; n < parts.Count; n++)
            {
                double minX = double.MaxValue, maxX = double.MinValue, minZ = double.MaxValue, maxZ = double.MinValue;
                foreach (Vec2 p in parts[n].Loops[0])
                {
                    if (p.X < minX) minX = p.X;
                    if (p.X > maxX) maxX = p.X;
                    if (p.Z < minZ) minZ = p.Z;
                    if (p.Z > maxZ) maxZ = p.Z;
                }
                for (long bx = Bucket(minX); bx <= Bucket(maxX); bx++)
                {
                    for (long bz = Bucket(minZ); bz <= Bucket(maxZ); bz++)
                    {
                        long key = (bx << 32) ^ (bz & 0xffffffffL);
                        if (!_buckets.TryGetValue(key, out List<int> list)) _buckets[key] = list = new List<int>();
                        list.Add(n);
                    }
                }
            }
        }

        private long Bucket(double v) => (long)Math.Floor(v / _bucket);

        /// <summary>Parte de edificio que cubre el punto, o null.</summary>
        public BuildingPart At(double x, double z)
        {
            long key = (Bucket(x) << 32) ^ (Bucket(z) & 0xffffffffL);
            if (!_buckets.TryGetValue(key, out List<int> list)) return null;
            var p = new Vec2(x, z);
            foreach (int n in list)
            {
                BuildingPart part = _parts[n];
                if (!BuildingFootprints.Inside(part.Loops[0], p)) continue;
                bool inHole = false;
                for (int h = 1; h < part.Loops.Count && !inHole; h++) inHole = BuildingFootprints.Inside(part.Loops[h], p);
                if (!inHole) return part;
            }
            return null;
        }

        public bool Contains(double x, double z) => At(x, z) != null;
    }
}
