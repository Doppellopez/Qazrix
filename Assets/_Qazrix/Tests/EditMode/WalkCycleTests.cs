using NUnit.Framework;
using Qazrix.Simulation.Movement;

namespace Qazrix.Tests
{
    /// <summary>Ciclo de andar de los personajes.</summary>
    public sealed class WalkCycleTests
    {
        [Test]
        public void StandingStill_KeepsThePhase_AndTheLimbsAtRest()
        {
            Assert.AreEqual(0.3f, WalkCycle.Advance(0.3f, 0f, 1f));
            Assert.AreEqual(0f, WalkCycle.Amplitude(0f));
            Assert.AreEqual(0f, WalkCycle.LegAngle(0.25f, 0f));
        }

        [Test]
        public void Phase_AdvancesWithDistance_AndWraps()
        {
            // Un ciclo completo son 1,5 m: a 3 m/s durante 0,25 s se recorre medio ciclo.
            Assert.AreEqual(0.5f, WalkCycle.Advance(0f, 3f, 0.25f), 1e-5f);
            float wrapped = WalkCycle.Advance(0.9f, 3f, 0.25f);
            Assert.AreEqual(0.4f, wrapped, 1e-5f, "la fase vuelve a empezar al pasar de 1");
        }

        [Test]
        public void Legs_SwingForwardAndBack_Symmetrically()
        {
            float forward = WalkCycle.LegAngle(0.25f, 2.2f);
            float back = WalkCycle.LegAngle(0.75f, 2.2f);
            Assert.Greater(forward, 0f);
            Assert.AreEqual(-forward, back, 1e-4f);
            Assert.AreEqual(0f, WalkCycle.LegAngle(0f, 2.2f), 1e-4f, "al cruzarse, la pierna está vertical");
        }

        [Test]
        public void Arms_MoveOppositeToTheirLeg_AndLess()
        {
            float leg = WalkCycle.LegAngle(0.25f, 2.2f);
            float arm = WalkCycle.ArmAngle(0.25f, 2.2f);
            Assert.Less(arm, 0f);
            Assert.Less(System.Math.Abs(arm), System.Math.Abs(leg));
        }

        [Test]
        public void Running_SwingsMoreThanWalking_UpToAMaximum()
        {
            float walking = WalkCycle.Amplitude(2.2f);
            float running = WalkCycle.Amplitude(5.5f);
            Assert.Greater(running, walking);
            Assert.AreEqual(WalkCycle.MaxLegSwing, running, 1e-4f);
            Assert.AreEqual(WalkCycle.MaxLegSwing, WalkCycle.Amplitude(50f), 1e-4f, "no crece sin límite");
        }
    }
}
