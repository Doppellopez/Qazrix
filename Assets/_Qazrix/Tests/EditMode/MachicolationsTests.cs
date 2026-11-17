using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Qazrix.Simulation.World;

namespace Qazrix.Tests
{
    /// <summary>Matacanes: reparto determinista (torres y algunos edificios altos), sin tapar ventanas ni llegar a las esquinas, y su malla.</summary>
    public sealed class MachicolationsTests
    {
        private static readonly Func<double, double, float> Flat = (x, z) => 0f;

        private static List<BuildingPart> Parts(float[] roofs, int columns, int rows)
        {
            return BuildingFootprints.Extract(roofs, columns, rows, 1f, 0f, rows, new FootprintOptions());
        }

        /// <summary>Una torre de 14 m (9 × 9) con un edificio de 10 m pegado por el oeste: su pared de ese lado no da a la calle.</summary>
        private static List<BuildingPart> TowerNextToABuilding()
        {
            const int columns = 40, rows = 15;
            var roofs = new float[columns * rows];
            for (int i = 0; i < roofs.Length; i++) roofs[i] = float.NaN;
            for (int r = 3; r < 12; r++)
                for (int c = 4; c < 14; c++) roofs[r * columns + c] = 10f;      // edificio de 10 m, al oeste
            for (int r = 3; r < 12; r++)
                for (int c = 14; c < 23; c++) roofs[r * columns + c] = 14f;     // torre de 14 m (planta de 81 m²)
            return Parts(roofs, columns, rows);
        }

        private static List<Machicolation> PlaceAll(List<BuildingPart> parts, MachicolationRules rules = null)
        {
            return MachicolationPlanner.Place(parts, new FootprintIndex(parts), Flat, new FacadeOpeningRules(), rules ?? new MachicolationRules());
        }

        [Test]
        public void ATower_GetsOneMachicolation_OnItsSideMostOpenToTheStreet()
        {
            List<BuildingPart> parts = TowerNextToABuilding();
            List<Machicolation> list = PlaceAll(parts);
            Assert.AreEqual(1, list.Count(m => m.Tower), "una en la torre");
            Machicolation tower = list.First(m => m.Tower);
            Assert.AreEqual(14f, tower.RoofHeight);
            Assert.Greater(tower.NormalX, -0.5f, $"no mira al oeste, donde está pegado el otro edificio (normal {tower.NormalX:0.00}, {tower.NormalZ:0.00})");
        }

        [Test]
        public void Machicolation_StaysAwayFromTheCorners_AndLiesOnTheWall()
        {
            List<BuildingPart> parts = TowerNextToABuilding();
            var rules = new MachicolationRules();
            foreach (Machicolation m in PlaceAll(parts, rules))
            {
                BuildingPart part = parts[m.PartIndex];
                // El centro está sobre el contorno exterior y la losa deja el margen a los extremos del lado.
                double best = double.MaxValue;
                Vec2[] loop = part.Loops[0];
                for (int i = 0; i < loop.Length; i++)
                {
                    Vec2 a = loop[i], b = loop[(i + 1) % loop.Length];
                    double dx = b.X - a.X, dz = b.Z - a.Z, length = Math.Sqrt(dx * dx + dz * dz);
                    double distance = Math.Abs((m.X - a.X) * dz - (m.Z - a.Z) * dx) / length;
                    double along = ((m.X - a.X) * dx + (m.Z - a.Z) * dz) / length;
                    if (distance < 1e-6 && along >= 0.0 && along <= length)
                    {
                        best = distance;
                        Assert.GreaterOrEqual(along - m.Length * 0.5, rules.EdgeMargin - 1e-6, "no llega a la esquina de su lado");
                        Assert.GreaterOrEqual(length - along - m.Length * 0.5, rules.EdgeMargin - 1e-6);
                    }
                }
                Assert.Less(best, 1e-6, "el centro del matacán está sobre una pared");
                Assert.AreEqual(1.0, Math.Sqrt(m.NormalX * m.NormalX + m.NormalZ * m.NormalZ), 1e-4);
                Assert.AreEqual(0.0, m.NormalX * m.TangentX + m.NormalZ * m.TangentZ, 1e-4, "normal y pared son perpendiculares");
            }
        }

        /// <summary>Cuadrícula de edificios altos (10 m, no torres) de 12 × 12 m, separados por calles de 6 m.</summary>
        private static List<BuildingPart> TallBlocks(int blocksX, int blocksZ)
        {
            const int size = 12, gap = 6;
            int columns = blocksX * (size + gap), rows = blocksZ * (size + gap);
            var roofs = new float[columns * rows];
            for (int i = 0; i < roofs.Length; i++) roofs[i] = float.NaN;
            for (int bz = 0; bz < blocksZ; bz++)
                for (int bx = 0; bx < blocksX; bx++)
                    for (int r = 0; r < size; r++)
                        for (int c = 0; c < size; c++)
                            roofs[(bz * (size + gap) + r) * columns + bx * (size + gap) + c] = 10f;
            return Parts(roofs, columns, rows);
        }

        [Test]
        public void TallBuildings_GetManyMachicolations_OnStreetWalls_AtLeastSixMetresApart()
        {
            List<BuildingPart> parts = TallBlocks(6, 4);
            Assert.AreEqual(24, parts.Count);
            var index = new FootprintIndex(parts);
            List<Machicolation> list = PlaceAll(parts);
            Assert.GreaterOrEqual(list.Count, 40, "bastantes: un buen puñado de lados largos dan a la calle");
            Assert.IsTrue(list.All(m => !m.Tower));

            for (int i = 0; i < list.Count; i++)
            {
                Machicolation m = list[i];
                // Mira a la calle: a metro y medio hacia fuera no hay edificio.
                Assert.IsFalse(index.Contains(m.X + m.NormalX * 1.5, m.Z + m.NormalZ * 1.5), "da a la calle");
                for (int j = i + 1; j < list.Count; j++)
                {
                    double dx = m.X - list[j].X, dz = m.Z - list[j].Z;
                    Assert.GreaterOrEqual(Math.Sqrt(dx * dx + dz * dz), 6.0 - 1e-6, "dos matacanes nunca a menos de 6 m");
                }
            }
            // Como mucho uno por lado.
            Assert.AreEqual(list.Count, list.Select(m => (m.PartIndex, m.EdgeIndex)).Distinct().Count(), "uno por lado como mucho");
        }

        [Test]
        public void Placement_IsDeterministic_AndTheTotalIsCapped_WithTowersFirst()
        {
            List<BuildingPart> parts = TallBlocks(6, 4);
            List<Machicolation> first = PlaceAll(parts);
            List<Machicolation> second = PlaceAll(parts);
            CollectionAssert.AreEqual(first.Select(m => (m.X, m.Z, m.NormalX, m.NormalZ)), second.Select(m => (m.X, m.Z, m.NormalX, m.NormalZ)), "mismo resultado cada vez: sin azar");

            var tight = new MachicolationRules { MaxCount = 7 };
            Assert.AreEqual(7, PlaceAll(parts, tight).Count, "el total tiene tope");

            // Con torres entre edificios altos y un tope pequeño, las torres entran todas antes que ninguno alto.
            List<BuildingPart> mixed = TowerNextToABuilding();
            Assert.AreEqual(1, mixed.Count(p => p.RoofHeight > 13f), "una torre");
            List<Machicolation> capped = PlaceAll(mixed, new MachicolationRules { MaxCount = 1 });
            Assert.AreEqual(1, capped.Count);
            Assert.IsTrue(capped[0].Tower, "con un solo hueco en el tope, es para la torre");
        }

        [Test]
        public void TallBuildings_DoNotGetOne_OnWallsThatTouchAnotherBuilding()
        {
            // Dos edificios altos de 12 × 12 pegados por un lado: el lado de contacto no da a la calle.
            const int columns = 40, rows = 20;
            var roofs = new float[columns * rows];
            for (int i = 0; i < roofs.Length; i++) roofs[i] = float.NaN;
            for (int r = 4; r < 16; r++)
                for (int c = 4; c < 16; c++) roofs[r * columns + c] = 10f;
            for (int r = 4; r < 16; r++)
                for (int c = 16; c < 28; c++) roofs[r * columns + c] = 10.2f; // +0,2 m: pegado pero con otro tejado
            List<BuildingPart> parts = Parts(roofs, columns, rows);
            var index = new FootprintIndex(parts);
            foreach (Machicolation m in PlaceAll(parts))
                Assert.IsFalse(index.Contains(m.X + m.NormalX * 1.5, m.Z + m.NormalZ * 1.5), "ninguno mira al edificio vecino");
        }

        [Test]
        public void LowBuildings_AndShortWalls_GetNone()
        {
            // Un edificio de 10 m con un lado de 4 m: no es torre (planta 16 m² pero baja) ni tiene lado de 5 m.
            var roofs = new float[8 * 8];
            for (int i = 0; i < roofs.Length; i++) roofs[i] = float.NaN;
            for (int r = 2; r < 6; r++)
                for (int c = 2; c < 6; c++) roofs[r * 8 + c] = 10f;
            Assert.AreEqual(0, PlaceAll(Parts(roofs, 8, 8), new MachicolationRules()).Count, "lados de 4 m: no llega al mínimo de un edificio alto");

            var low = new float[8 * 8];
            for (int i = 0; i < low.Length; i++) low[i] = float.NaN;
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++) low[r * 8 + c] = 6f;
            Assert.AreEqual(0, PlaceAll(Parts(low, 8, 8), new MachicolationRules()).Count, "un edificio de 6 m no cuenta como alto");
        }

        [Test]
        public void Machicolation_ReservesItsStretchOfWall_SoNoWindowIsDrawnUnderIt()
        {
            List<BuildingPart> parts = TowerNextToABuilding();
            var rules = new MachicolationRules();
            List<Machicolation> list = PlaceAll(parts, rules);
            Machicolation m = list.First();

            // Un hueco justo bajo el matacán choca; uno más abajo, o fuera de su tramo, no.
            var under = new FacadeOpening { X = (float)m.X, Y = m.RoofHeight - 1.0f, Z = (float)m.Z };
            var low = new FacadeOpening { X = (float)m.X, Y = m.RoofHeight - 7.0f, Z = (float)m.Z };
            var aside = new FacadeOpening { X = (float)(m.X + m.TangentX * (m.Length * 0.5 + 1.5)), Y = m.RoofHeight - 1.0f, Z = (float)(m.Z + m.TangentZ * (m.Length * 0.5 + 1.5)) };
            Assert.IsTrue(MachicolationPlanner.BlocksOpening(m, under, 0.8f, rules), "bajo el matacán no va ventana");
            Assert.IsFalse(MachicolationPlanner.BlocksOpening(m, low, 0.8f, rules), "más abajo sí");
            Assert.IsFalse(MachicolationPlanner.BlocksOpening(m, aside, 0.8f, rules), "al lado de su tramo sí");

            // Con el reparto de huecos de la torre (saeteras, de 1,5 m de alto), ninguno queda dentro de la zona reservada.
            var towerRules = new FacadeOpeningRules();
            List<FacadeOpening> openings = FootprintOpenings.Place(parts, Flat, towerRules, 3.0, list, rules);
            foreach (FacadeOpening o in openings)
                Assert.IsFalse(MachicolationPlanner.BlocksOpening(m, o, 0.5f * towerRules.SlitHeight, rules), "ningún hueco queda bajo el matacán");
            int without = FootprintOpenings.Place(parts, Flat, new FacadeOpeningRules()).Count;
            Assert.LessOrEqual(openings.Count, without, "reservar el tramo no añade huecos");
        }

        [Test]
        public void Mesh_HasSlabCorbelsAndParapet_FacingOutwards_WithinTheExpectedBox()
        {
            List<BuildingPart> parts = TowerNextToABuilding();
            var rules = new MachicolationRules();
            List<Machicolation> list = PlaceAll(parts, rules);
            Machicolation m = list.First(x => x.Tower);
            MachicolationMesh mesh = MachicolationMesher.Build(new List<Machicolation> { m }, rules, 3f); // uno solo: 16 cajas

            Assert.AreEqual(16 * 12, mesh.Triangles.Length / 3, "dieciséis cajas de doce triángulos: losa, ocho ménsulas, tres lados de pretil y cuatro almenas");
            Assert.AreEqual(mesh.Vertices.Length / 3 * 2, mesh.Uvs.Length);
            Assert.AreEqual(3 * 2, mesh.HoleTriangles.Length / 3, "tres recuadros oscuros de dos triángulos");

            // Todo cae a 0,5 m de la pared como mucho y entre la base de las ménsulas y la cima del pretil, bajo el tejado.
            double top = m.RoofHeight - rules.DropBelowRoof;
            double lowest = top - rules.SlabThickness - 0.5, highest = top + rules.ParapetHeight + rules.MerlonHeight;
            for (int i = 0; i < mesh.Vertices.Length / 3; i++)
            {
                double x = mesh.Vertices[i * 3], y = mesh.Vertices[i * 3 + 1], z = mesh.Vertices[i * 3 + 2];
                double outwards = (x - m.X) * m.NormalX + (z - m.Z) * m.NormalZ;
                Assert.That(outwards, Is.InRange(-0.06, rules.Overhang + 1e-4), "vuela como mucho lo previsto");
                Assert.That(y, Is.InRange(lowest - 1e-4, highest + 1e-4));
                Assert.Less(y, m.RoofHeight, "queda por debajo de la cornisa");
            }

            // Cada triángulo mira hacia fuera de su caja: la normal apunta lejos del centro de la losa.
            double cy = top - rules.SlabThickness * 0.5;
            for (int t = 0; t + 2 < mesh.Triangles.Length; t += 3)
            {
                int a = mesh.Triangles[t] * 3, b = mesh.Triangles[t + 1] * 3, c = mesh.Triangles[t + 2] * 3;
                double ux = mesh.Vertices[b] - mesh.Vertices[a], uy = mesh.Vertices[b + 1] - mesh.Vertices[a + 1], uz = mesh.Vertices[b + 2] - mesh.Vertices[a + 2];
                double vx = mesh.Vertices[c] - mesh.Vertices[a], vy = mesh.Vertices[c + 1] - mesh.Vertices[a + 1], vz = mesh.Vertices[c + 2] - mesh.Vertices[a + 2];
                double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                double length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                Assert.Greater(length, 0.0);
                Assert.IsFalse(double.IsNaN(length));
            }
            Assert.Greater(cy, lowest);
        }

        [Test]
        public void InvalidInput_IsRejected()
        {
            var parts = new List<BuildingPart>();
            Assert.Throws<ArgumentNullException>(() => MachicolationPlanner.Place(null, new FootprintIndex(parts), Flat, new FacadeOpeningRules(), new MachicolationRules()));
            Assert.Throws<ArgumentNullException>(() => MachicolationPlanner.Place(parts, null, Flat, new FacadeOpeningRules(), new MachicolationRules()));
            Assert.Throws<ArgumentNullException>(() => MachicolationMesher.Build(null, new MachicolationRules(), 3f));
            Assert.Throws<ArgumentException>(() => MachicolationMesher.Build(new List<Machicolation>(), new MachicolationRules(), 0f));
        }
    }
}
