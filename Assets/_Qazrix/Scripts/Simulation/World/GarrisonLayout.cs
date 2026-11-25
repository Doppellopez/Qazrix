using System;
using System.Collections.Generic;

namespace Qazrix.Simulation.World
{
    /// <summary>Un puesto de la formación: posición (metros, x este, z norte) y hacia dónde mira (grados, giro en Y: 0 = norte, 90 = este).</summary>
    public struct GarrisonSlot
    {
        public double X, Z;
        public double YawDegrees;
        public int Row;          // 0 = el capitán, 1.. = filas de soldados de dentro afuera
        public bool IsCaptain => Row == 0;
    }

    /// <summary>
    /// Formación de la guarnición que espera a la salida del pasadizo: el capitán al frente y los soldados en arcos concéntricos delante
    /// de quien sale, todos mirando hacia la salida. Reparte los soldados por filas en proporción a lo largo de cada arco, para que la
    /// separación entre vecinos sea parecida en todas. Sin dependencias de Unity.
    /// </summary>
    public static class GarrisonLayout
    {
        public const int DefaultSoldiers = 50;
        public const double CaptainRadius = 4.0;
        public const double FirstRow = 7.0, RowGap = 2.0;
        public const int Rows = 3;
        public const double HalfArcDegrees = 80.0;   // cada arco abarca 160° delante de quien sale

        /// <param name="exitX">Salida del pasadizo.</param>
        /// <param name="facingYaw">Hacia dónde mira quien sale (grados, giro en Y de Unity).</param>
        public static List<GarrisonSlot> Compute(double exitX, double exitZ, double facingYaw, int soldiers = DefaultSoldiers)
        {
            if (soldiers < 0) throw new ArgumentOutOfRangeException(nameof(soldiers));
            var slots = new List<GarrisonSlot>(soldiers + 1);
            slots.Add(Slot(exitX, exitZ, facingYaw, 0.0, CaptainRadius, 0));

            double arc = 2.0 * HalfArcDegrees * Math.PI / 180.0, total = 0.0;
            for (int r = 0; r < Rows; r++) total += arc * (FirstRow + r * RowGap);

            int placed = 0;
            for (int r = 0; r < Rows; r++)
            {
                double radius = FirstRow + r * RowGap;
                int count = r == Rows - 1 ? soldiers - placed : (int)Math.Round(soldiers * arc * radius / total);
                count = Math.Max(0, Math.Min(count, soldiers - placed));
                for (int k = 0; k < count; k++)
                {
                    double t = count == 1 ? 0.5 : (double)k / (count - 1);
                    double angle = -HalfArcDegrees + 2.0 * HalfArcDegrees * t;
                    slots.Add(Slot(exitX, exitZ, facingYaw, angle, radius, r + 1));
                }
                placed += count;
            }
            return slots;
        }

        private static GarrisonSlot Slot(double exitX, double exitZ, double facingYaw, double angleDegrees, double radius, int row)
        {
            // La dirección hacia delante de la salida y el puesto, girado angleDegrees a un lado.
            double yaw = (facingYaw + angleDegrees) * Math.PI / 180.0;
            double x = exitX + Math.Sin(yaw) * radius, z = exitZ + Math.Cos(yaw) * radius;
            // Mira hacia la salida: el sentido contrario al que va del centro al puesto.
            double look = Math.Atan2(exitX - x, exitZ - z) * 180.0 / Math.PI;
            return new GarrisonSlot { X = x, Z = z, YawDegrees = look, Row = row };
        }
    }
}
