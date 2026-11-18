using System;
using System.Collections.Generic;

namespace Qazrix.Simulation.World
{
    /// <summary>
    /// Caja de colisión de una pieza de matacán, orientada con la pared: <c>Along</c> sobre la pared, <c>Out</c> hacia fuera
    /// (la normal), <c>Y</c> en vertical. Las medidas son los lados completos, no las mitades.
    /// </summary>
    public struct MachicolationBox
    {
        public string Name;
        public double CentreAlong, CentreOut, CentreY;
        public double SizeAlong, SizeOut, SizeY;
    }

    /// <summary>
    /// Qué cajas de colisión lleva un matacán para poder posarse dentro: la losa (el suelo) y el pretil de tres lados.
    /// Las ménsulas y las almenas no llevan: no hacen falta para estar de pie y así no se atasca nadie en ellas.
    /// Lógica pura, sin Unity; las medidas salen de las mismas reglas que la malla (<see cref="MachicolationMesher"/>).
    /// </summary>
    public static class MachicolationColliders
    {
        /// <summary>La losa entra un poco en la pared, igual que en la malla.</summary>
        public const double Embed = 0.05;

        public static List<MachicolationBox> Build(Machicolation m, MachicolationRules rules)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            if (rules == null) throw new ArgumentNullException(nameof(rules));

            double top = m.RoofHeight - rules.DropBelowRoof;
            double bottom = top - rules.SlabThickness;
            double half = m.Length * 0.5;
            double pt = rules.ParapetThickness, pTop = top + rules.ParapetHeight;

            var boxes = new List<MachicolationBox>(4)
            {
                Make("Losa", -half, half, -Embed, rules.Overhang, bottom, top),
                Make("Pretil_frontal", -half, half, rules.Overhang - pt, rules.Overhang, top, pTop),
                Make("Pretil_izquierdo", -half, -half + pt, -Embed, rules.Overhang - pt, top, pTop),
                Make("Pretil_derecho", half - pt, half, -Embed, rules.Overhang - pt, top, pTop),
            };
            return boxes;
        }

        /// <summary>Cota (m) de la cara de arriba de la losa: donde se pisa.</summary>
        public static double FloorHeight(Machicolation m, MachicolationRules rules) => m.RoofHeight - rules.DropBelowRoof;

        /// <summary>Punto del suelo del matacán (mundo, x y z) a una distancia <paramref name="out"/> de la pared y <paramref name="along"/> del centro.</summary>
        public static void WorldPoint(Machicolation m, double along, double @out, out double x, out double z)
        {
            x = m.X + m.TangentX * along + m.NormalX * @out;
            z = m.Z + m.TangentZ * along + m.NormalZ * @out;
        }

        private static MachicolationBox Make(string name, double a0, double a1, double n0, double n1, double y0, double y1) =>
            new MachicolationBox
            {
                Name = name,
                CentreAlong = (a0 + a1) * 0.5, CentreOut = (n0 + n1) * 0.5, CentreY = (y0 + y1) * 0.5,
                SizeAlong = a1 - a0, SizeOut = n1 - n0, SizeY = y1 - y0,
            };
    }
}
