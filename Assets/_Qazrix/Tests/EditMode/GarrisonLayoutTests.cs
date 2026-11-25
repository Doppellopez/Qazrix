using System;
using System.Linq;
using NUnit.Framework;
using Qazrix.Simulation.World;

namespace Qazrix.Tests
{
    /// <summary>Un capitán y 50 soldados esperan a la salida del pasadizo, delante de quien sale y mirando hacia él.</summary>
    public sealed class GarrisonLayoutTests
    {
        private static double Dist(double ax, double az, double bx, double bz) => Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));

        [Test]
        public void ThereIsOneCaptainAndFiftySoldiers()
        {
            var slots = GarrisonLayout.Compute(100, 200, 0);
            Assert.AreEqual(51, slots.Count);
            Assert.AreEqual(1, slots.Count(s => s.IsCaptain));
            Assert.AreEqual(50, slots.Count(s => !s.IsCaptain));
        }

        [Test]
        public void TheCaptainStandsInFrontOfTheSoldiers()
        {
            var slots = GarrisonLayout.Compute(0, 0, 0);
            var captain = slots.Single(s => s.IsCaptain);
            Assert.AreEqual(GarrisonLayout.CaptainRadius, Dist(0, 0, captain.X, captain.Z), 1e-6, "a 4 m de la salida");
            Assert.AreEqual(0.0, captain.X, 1e-6, "justo delante");
            Assert.Greater(captain.Z, 0.0);
            foreach (var s in slots.Where(s => !s.IsCaptain))
                Assert.Greater(Dist(0, 0, s.X, s.Z), GarrisonLayout.CaptainRadius, "los soldados detrás del capitán");
        }

        [Test]
        public void EverybodyIsInFrontOfWhoComesOut()
        {
            foreach (double yaw in new[] { 0.0, 90.0, 215.0 })
            {
                var slots = GarrisonLayout.Compute(10, 20, yaw);
                double fx = Math.Sin(yaw * Math.PI / 180.0), fz = Math.Cos(yaw * Math.PI / 180.0);
                foreach (var s in slots)
                    Assert.Greater((s.X - 10) * fx + (s.Z - 20) * fz, 0.0, $"delante con yaw {yaw}");
            }
        }

        [Test]
        public void EverybodyLooksTowardsTheExit()
        {
            var slots = GarrisonLayout.Compute(5, 5, 40);
            foreach (var s in slots)
            {
                double look = s.YawDegrees * Math.PI / 180.0;
                double toExitX = 5 - s.X, toExitZ = 5 - s.Z, len = Dist(5, 5, s.X, s.Z);
                double dot = (Math.Sin(look) * toExitX + Math.Cos(look) * toExitZ) / len;
                Assert.AreEqual(1.0, dot, 1e-6, "mirando a la salida");
            }
        }

        [Test]
        public void NoOneStandsOnTopOfAnother()
        {
            var slots = GarrisonLayout.Compute(0, 0, 0);
            for (int i = 0; i < slots.Count; i++)
                for (int j = i + 1; j < slots.Count; j++)
                    Assert.GreaterOrEqual(Dist(slots[i].X, slots[i].Z, slots[j].X, slots[j].Z), 1.2, $"puestos {i} y {j} demasiado juntos");
        }

        [Test]
        public void SoldiersAreSpreadOverThreeRowsInProportionToTheirLength()
        {
            var slots = GarrisonLayout.Compute(0, 0, 0);
            int[] rows = new int[GarrisonLayout.Rows + 1];
            foreach (var s in slots) rows[s.Row]++;
            Assert.AreEqual(1, rows[0]);
            Assert.Less(rows[1], rows[2], "las filas de fuera son más largas y llevan más gente");
            Assert.Less(rows[2], rows[3]);
            Assert.AreEqual(50, rows[1] + rows[2] + rows[3]);
        }

        [Test]
        public void TheCountCanBeChanged()
        {
            Assert.AreEqual(1, GarrisonLayout.Compute(0, 0, 0, 0).Count, "solo el capitán");
            Assert.AreEqual(11, GarrisonLayout.Compute(0, 0, 0, 10).Count);
            Assert.Throws<ArgumentOutOfRangeException>(() => GarrisonLayout.Compute(0, 0, 0, -1));
        }
    }
}
