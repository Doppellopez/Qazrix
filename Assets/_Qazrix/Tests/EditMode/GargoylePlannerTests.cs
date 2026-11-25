using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Qazrix.Simulation.World;

namespace Qazrix.Tests
{
    /// <summary>Reparto de las gárgolas por las paredes: bajo la cornisa, mirando a la calle y separadas.</summary>
    public sealed class GargoylePlannerTests
    {
        /// <summary>Edificio rectangular con el contorno antihorario.</summary>
        private static BuildingPart House(double west, double south, double east, double north, float roof) => new BuildingPart
        {
            RoofHeight = roof,
            Loops = new List<Vec2[]>
            {
                new[] { new Vec2(west, south), new Vec2(east, south), new Vec2(east, north), new Vec2(west, north) }
            }
        };

        private static readonly Func<double, double, float> Flat = (x, z) => 0f;

        private static List<GargoyleSpot> Place(List<BuildingPart> parts, GargoyleGroup group,
            ICollection<int> excluded = null, IReadOnlyList<Machicolation> machicolations = null) =>
            GargoylePlanner.Place(parts, new FootprintIndex(parts), Flat, new[] { group }, excluded, machicolations);

        private static GargoyleGroup Group(double x, double z, params int[] ids) =>
            new GargoyleGroup { Name = "g", X = x, Z = z, Ids = ids };

        [Test]
        public void SitsJustBelowTheRoofFacingOutwards()
        {
            var parts = new List<BuildingPart> { House(0, 0, 10, 10, 8f) };

            GargoyleSpot spot = Place(parts, Group(5, -20, 3)).Single();

            Assert.AreEqual(3, spot.CatalogId);
            Assert.AreEqual(0.0, spot.Z, 1e-6, "en la pared sur, la que mira al punto");
            Assert.AreEqual(-1f, spot.NormalZ, 1e-4f, "hacia la calle");
            Assert.AreEqual(8f - new GargoyleRules().DropBelowRoof, spot.Height, 1e-4f);
        }

        [Test]
        public void KeepsThemApartAndMarksTheErodedOnes()
        {
            var parts = new List<BuildingPart> { House(0, 0, 12, 12, 8f) };
            GargoyleGroup group = Group(6, -20, 1, 2, 3, 4);
            group.ErodedIds = new[] { 2 };

            List<GargoyleSpot> spots = Place(parts, group);

            Assert.AreEqual(4, spots.Count);
            CollectionAssert.AreEqual(new[] { false, true, false, false }, spots.Select(s => s.Eroded).ToArray());
            foreach (GargoyleSpot a in spots)
                foreach (GargoyleSpot b in spots.Where(s => s != a))
                    Assert.GreaterOrEqual(Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Z - b.Z, 2)), 3.5 - 1e-6);
        }

        [Test]
        public void NeverFacesAnAdjacentBuilding()
        {
            // Una calle de 1 m entre las dos casas: la pared norte de la primera no sirve.
            var parts = new List<BuildingPart> { House(0, 0, 10, 10, 8f), House(0, 11, 10, 21, 8f) };

            List<GargoyleSpot> spots = Place(parts, Group(5, 10.5, 1, 2, 3, 4, 5, 6));

            Assert.IsNotEmpty(spots);
            Assert.IsFalse(spots.Any(s => Math.Abs(s.Z - 10.0) < 0.1 || Math.Abs(s.Z - 11.0) < 0.1));
        }

        [Test]
        public void SkipsLowRoofsExcludedBuildingsAndMachicolations()
        {
            var low = new List<BuildingPart> { House(0, 0, 10, 10, 3f) };
            Assert.IsEmpty(Place(low, Group(5, -5, 1)), "tejado a 3 m");

            var house = new List<BuildingPart> { House(0, 0, 4, 4, 8f) };
            Assert.IsEmpty(Place(house, Group(2, -5, 1), new HashSet<int> { 0 }), "edificio excluido");

            var machicolation = new[] { new Machicolation { X = 2, Z = 0, RoofHeight = 8f } };
            GargoyleSpot spot = Place(house, Group(2, -5, 1), null, machicolation).Single();
            Assert.Greater(Math.Abs(spot.Z), 1.0, "se va a otra pared");
        }

        [Test]
        public void CanBeTiedToASingleBuilding()
        {
            var parts = new List<BuildingPart> { House(0, 0, 10, 10, 8f), House(20, 0, 30, 10, 8f) };
            GargoyleGroup group = Group(5, -5, 1, 2);
            group.OnlyPart = 1;

            List<GargoyleSpot> spots = Place(parts, group);

            Assert.AreEqual(2, spots.Count);
            Assert.IsTrue(spots.All(s => s.PartIndex == 1));
        }

        [Test]
        public void SpreadGroupSurroundsTheBuilding()
        {
            var parts = new List<BuildingPart> { House(0, 0, 20, 20, 12f) };
            GargoyleGroup group = Group(10, 10, 1, 2, 3, 4);
            group.Spread = true;

            List<GargoyleSpot> spots = Place(parts, group);

            Assert.AreEqual(4, spots.Select(s => s.EdgeIndex).Distinct().Count(), "una por fachada");
        }
    }
}
