using System.Linq;
using NUnit.Framework;
using Qazrix.Simulation.World;

namespace Qazrix.Tests
{
    /// <summary>Cajas de colisión del matacán: suelo y pretil, para poder posarse dentro.</summary>
    public sealed class MachicolationCollidersTests
    {
        private static Machicolation Sample() => new Machicolation
        {
            X = 10, Z = 20, NormalX = 0, NormalZ = 1, TangentX = 1, TangentZ = 0, RoofHeight = 15f, Length = 3.5,
        };

        [Test]
        public void HasAFloorAndThreeParapetSides()
        {
            var boxes = MachicolationColliders.Build(Sample(), new MachicolationRules());
            CollectionAssert.AreEquivalent(new[] { "Losa", "Pretil_frontal", "Pretil_izquierdo", "Pretil_derecho" }, boxes.Select(b => b.Name));
        }

        [Test]
        public void Floor_TopMatchesTheMeshAndTheRules()
        {
            var rules = new MachicolationRules();
            Machicolation m = Sample();
            MachicolationBox slab = MachicolationColliders.Build(m, rules).First(b => b.Name == "Losa");
            Assert.AreEqual(MachicolationColliders.FloorHeight(m, rules), slab.CentreY + slab.SizeY * 0.5, 1e-9, "la cara de arriba es donde se pisa");
            Assert.AreEqual(m.RoofHeight - rules.DropBelowRoof, MachicolationColliders.FloorHeight(m, rules), 1e-9);
            Assert.AreEqual(rules.SlabThickness, slab.SizeY, 1e-9);
            Assert.AreEqual(m.Length, slab.SizeAlong, 1e-9);
            Assert.AreEqual(rules.Overhang, slab.CentreOut + slab.SizeOut * 0.5, 1e-9, "llega hasta donde vuela");
        }

        [Test]
        public void Parapet_RisesFromTheFloor_AndLeavesRoomInside()
        {
            var rules = new MachicolationRules();
            var boxes = MachicolationColliders.Build(Sample(), rules);
            double floor = MachicolationColliders.FloorHeight(Sample(), rules);
            foreach (MachicolationBox b in boxes.Where(b => b.Name.StartsWith("Pretil")))
            {
                Assert.AreEqual(floor, b.CentreY - b.SizeY * 0.5, 1e-9, $"{b.Name} arranca en el suelo");
                Assert.AreEqual(rules.ParapetHeight, b.SizeY, 1e-9);
            }
            // Entre el pretil frontal y la pared cabe una persona (un cuerpo de unos 60 cm).
            MachicolationBox front = boxes.First(b => b.Name == "Pretil_frontal");
            double inside = (front.CentreOut - front.SizeOut * 0.5) - (-MachicolationColliders.Embed);
            Assert.GreaterOrEqual(inside, 0.8, "hay sitio dentro para posarse");
        }

        [Test]
        public void WorldPoint_FollowsTheWallDirections()
        {
            Machicolation m = Sample();
            MachicolationColliders.WorldPoint(m, 1.0, 0.5, out double x, out double z);
            Assert.AreEqual(11.0, x, 1e-9);
            Assert.AreEqual(20.5, z, 1e-9);
        }
    }
}
