using NUnit.Framework;
using Qazrix.Simulation.Movement;

namespace Qazrix.Tests
{
    public sealed class PickupCycleTests
    {
        [Test]
        public void Weight_StartsAtZero_ReachesOneAtTheBottom_AndReturnsToZero()
        {
            Assert.AreEqual(0f, PickupCycle.Weight(0f), 1e-6f);
            Assert.AreEqual(1f, PickupCycle.Weight(0.5f), 1e-6f, "abajo del todo a mitad del gesto");
            Assert.AreEqual(0f, PickupCycle.Weight(PickupCycle.Duration), 1e-6f);
            Assert.AreEqual(0f, PickupCycle.Weight(PickupCycle.Duration + 1f), 1e-6f);
            Assert.AreEqual(0f, PickupCycle.Weight(-1f), 1e-6f);
        }

        [Test]
        public void Weight_RisesThenFalls_Monotonically()
        {
            float previous = 0f;
            for (float t = 0f; t <= 0.35f; t += 0.01f)
            {
                float w = PickupCycle.Weight(t);
                Assert.GreaterOrEqual(w, previous - 1e-6f);
                previous = w;
            }
            previous = 1f;
            for (float t = 0.7f; t <= PickupCycle.Duration; t += 0.01f)
            {
                float w = PickupCycle.Weight(t);
                Assert.LessOrEqual(w, previous + 1e-6f);
                previous = w;
            }
        }

        [Test]
        public void ObjectIsTaken_WhileTheBodyIsStillLow()
        {
            Assert.IsFalse(PickupCycle.HasCollected(0.2f));
            Assert.IsTrue(PickupCycle.HasCollected(PickupCycle.CollectAt));
            Assert.Greater(PickupCycle.Weight(PickupCycle.CollectAt), 0.99f, "se coge con el cuerpo abajo");
            Assert.IsFalse(PickupCycle.IsFinished(PickupCycle.CollectAt));
            Assert.IsTrue(PickupCycle.IsFinished(PickupCycle.Duration));
        }

        [Test]
        public void Pose_BendsKneesAndHips_LowersAndLeansTheBody()
        {
            Assert.AreEqual(PickupCycle.KneeDegrees, PickupCycle.KneeAngle(1f), 1e-4f);
            Assert.Greater(PickupCycle.LegAngle(1f), 30f, "el muslo va hacia delante");
            Assert.Less(PickupCycle.ArmAngle(1f, true), PickupCycle.ArmAngle(1f, false), "el brazo que recoge baja más que el que equilibra");
            Assert.Greater(PickupCycle.Lean(1f), 10f);
            Assert.Less(PickupCycle.Drop(1f), -0.2f);
            Assert.AreEqual(0f, PickupCycle.KneeAngle(0f), 1e-6f);
            Assert.AreEqual(0f, PickupCycle.Drop(0f), 1e-6f);
        }
    }
}
