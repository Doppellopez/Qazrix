using System;
using NUnit.Framework;
using Qazrix.Simulation.Gameplay;

namespace Qazrix.Tests
{
    public sealed class RandomSpotTests
    {
        [Test]
        public void Picks_APointInsideTheRing()
        {
            var rng = new Random(7);
            for (int i = 0; i < 200; i++)
            {
                Assert.IsTrue(RandomSpot.TryPick(() => (float)rng.NextDouble(), 10f, -5f, 16f, 55f, (x, z) => true, 1, out float px, out float pz));
                float d = (float)Math.Sqrt((px - 10f) * (px - 10f) + (pz + 5f) * (pz + 5f));
                Assert.GreaterOrEqual(d, 16f - 1e-3f);
                Assert.LessOrEqual(d, 55f + 1e-3f);
            }
        }

        [Test]
        public void SkipsRejectedPoints_AndReturnsAnAcceptedOne()
        {
            var rng = new Random(3);
            // Solo se acepta el este (x > centro): la elección tiene que cumplirlo.
            Assert.IsTrue(RandomSpot.TryPick(() => (float)rng.NextDouble(), 0f, 0f, 16f, 55f, (x, z) => x > 0f, 100, out float px, out _));
            Assert.Greater(px, 0f);
        }

        [Test]
        public void ReturnsFalse_WhenNothingIsAccepted_OrTheRingIsInvalid()
        {
            var rng = new Random(1);
            Assert.IsFalse(RandomSpot.TryPick(() => (float)rng.NextDouble(), 0f, 0f, 16f, 55f, (x, z) => false, 50, out _, out _));
            Assert.IsFalse(RandomSpot.TryPick(() => (float)rng.NextDouble(), 0f, 0f, 55f, 16f, (x, z) => true, 50, out _, out _));
        }

        [Test]
        public void DifferentRandomSources_GiveDifferentPoints()
        {
            var a = new Random(1);
            var b = new Random(2);
            RandomSpot.TryPick(() => (float)a.NextDouble(), 0f, 0f, 16f, 55f, (x, z) => true, 1, out float ax, out float az);
            RandomSpot.TryPick(() => (float)b.NextDouble(), 0f, 0f, 16f, 55f, (x, z) => true, 1, out float bx, out float bz);
            Assert.IsTrue(ax != bx || az != bz);
        }
    }
}
