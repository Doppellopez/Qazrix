using System;

namespace Qazrix.Simulation.World
{
    /// <summary>Punto en metros locales del juego: X = este, Y = altura, Z = norte (D12).</summary>
    public readonly struct LocalPoint
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Z;

        public LocalPoint(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    /// <summary>
    /// Origen local del mundo (spec §5.6). La conversión es una traslación pura desde coordenadas
    /// proyectadas en metros (ETRS89 / UTM 29N, EPSG:25829) y cota ortométrica; no reproyecta.
    /// Se trabaja en double porque un float no distingue metros en valores como 4 372 732.
    /// </summary>
    public readonly struct GeoOrigin
    {
        public readonly double Easting;
        public readonly double Northing;
        public readonly double Height;

        public GeoOrigin(double easting, double northing, double height)
        {
            if (double.IsNaN(easting) || double.IsNaN(northing) || double.IsNaN(height))
                throw new ArgumentException("El origen no puede contener NaN.");
            Easting = easting;
            Northing = northing;
            Height = height;
        }

        public LocalPoint ToLocal(double easting, double northing, double height)
        {
            return new LocalPoint(easting - Easting, height - Height, northing - Northing);
        }

        public void ToSource(LocalPoint local, out double easting, out double northing, out double height)
        {
            easting = Easting + local.X;
            northing = Northing + local.Z;
            height = Height + local.Y;
        }

        /// <summary>Error horizontal en metros entre un punto convertido y su valor esperado (puntos de control).</summary>
        public static double HorizontalError(LocalPoint a, LocalPoint b)
        {
            double dx = a.X - b.X;
            double dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
