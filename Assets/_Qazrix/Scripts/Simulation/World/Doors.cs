using System;

namespace Qazrix.Simulation.World
{
    /// <summary>Dónde va una puerta en la pared de un edificio: el hueco que se abre en el muro y la hoja que lo cierra.</summary>
    public sealed class DoorPlacement
    {
        public BuildingPart Part;           // edificio al que pertenece
        public int EdgeIndex;               // lado del contorno exterior donde está
        public double AlongStart;           // distancia desde el principio del lado hasta el principio del hueco (m)
        public double Width, Height;        // tamaño del hueco (m)
        public double X, Z;                 // centro del hueco en el suelo, sobre la pared
        public double NormalX, NormalZ;     // hacia fuera del edificio
        public double TangentX, TangentZ;   // a lo largo de la pared, en el sentido del contorno
        public double GroundY;              // cota del suelo en el umbral
        public double TopY => GroundY + Height;
    }

    /// <summary>
    /// Elige dónde abrir una puerta en un edificio: la pared más cercana al punto indicado que sea lo bastante larga y dé a un
    /// espacio libre, con la puerta lo más cerca posible de ese punto sin acercarse a las esquinas. Sin dependencias de Unity.
    /// </summary>
    public static class DoorPlanner
    {
        public const double DefaultWidth = 2.2, DefaultHeight = 2.8;
        public const double CornerMargin = 0.8;   // m de pared a cada lado de la puerta
        public const double FreeDepth = 3.0;      // m libres delante, para poder llegar a ella

        public static DoorPlacement Place(FootprintIndex index, double x, double z, Func<double, double, float> ground,
            double width = DefaultWidth, double height = DefaultHeight)
        {
            if (index == null) throw new ArgumentNullException(nameof(index));
            if (ground == null) throw new ArgumentNullException(nameof(ground));
            if (width <= 0.0 || height <= 0.0) throw new ArgumentOutOfRangeException(nameof(width));

            BuildingPart part = index.At(x, z);
            if (part == null) return null;
            Vec2[] loop = part.Loops[0];

            double bestDistance = double.MaxValue, bestLength = 0.0, bestAlong = 0.0;
            int best = -1;
            for (int i = 0; i < loop.Length; i++)
            {
                Vec2 a = loop[i], b = loop[(i + 1) % loop.Length];
                double dx = b.X - a.X, dz = b.Z - a.Z, length = Math.Sqrt(dx * dx + dz * dz);
                if (length < width + 2.0 * CornerMargin) continue;
                // Contorno antihorario: el edificio queda a la izquierda, fuera es a la derecha.
                double nx = dz / length, nz = -dx / length;
                double mx = (a.X + b.X) * 0.5, mz = (a.Z + b.Z) * 0.5;
                if (index.Contains(mx + nx * 1.0, mz + nz * 1.0) || index.Contains(mx + nx * FreeDepth, mz + nz * FreeDepth)) continue;

                double along = Math.Max(0.0, Math.Min(length, ((x - a.X) * dx + (z - a.Z) * dz) / length));
                double fx = a.X + dx / length * along - x, fz = a.Z + dz / length * along - z;
                double distance = fx * fx + fz * fz;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                bestLength = length;
                bestAlong = along;
                best = i;
            }
            if (best < 0) return null;

            Vec2 p = loop[best], q = loop[(best + 1) % loop.Length];
            double ex = (q.X - p.X) / bestLength, ez = (q.Z - p.Z) / bestLength;
            double centre = Math.Max(CornerMargin + width * 0.5, Math.Min(bestLength - CornerMargin - width * 0.5, bestAlong));
            double cx = p.X + ex * centre, cz = p.Z + ez * centre;
            return new DoorPlacement
            {
                Part = part,
                EdgeIndex = best,
                AlongStart = centre - width * 0.5,
                Width = width,
                Height = height,
                X = cx, Z = cz,
                NormalX = ez, NormalZ = -ex,
                TangentX = ex, TangentZ = ez,
                GroundY = ground(cx, cz)
            };
        }
    }
}
