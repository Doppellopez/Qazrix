using NUnit.Framework;
using Qazrix.Simulation.Movement;

namespace Qazrix.Tests
{
    /// <summary>Animación de escalada: ciclo puro de brazos y piernas.</summary>
    public sealed class ClimbCycleTests
    {
        [Test]
        public void Phase_AdvancesWithDistance_UpOrDown_AndHoldsWhenStill()
        {
            Assert.AreEqual(0.3f, ClimbCycle.Advance(0.3f, 0f), 1e-6f, "colgado y quieto conserva la postura");
            float up = ClimbCycle.Advance(0f, ClimbCycle.ReachLength * 0.25f);
            float down = ClimbCycle.Advance(0f, -ClimbCycle.ReachLength * 0.25f);
            Assert.AreEqual(0.25f, up, 1e-5f);
            Assert.AreEqual(up, down, 1e-5f, "bajando también mueve las manos");
            Assert.AreEqual(0.1f, ClimbCycle.Advance(0.9f, ClimbCycle.ReachLength * 0.2f), 1e-5f, "la fase da la vuelta");
        }

        [Test]
        public void Arms_StayAboveTheShoulders_AndAlternate()
        {
            for (float phase = 0f; phase < 1f; phase += 0.05f)
            {
                float left = ClimbCycle.ArmAngle(phase, true), right = ClimbCycle.ArmAngle(phase, false);
                Assert.Greater(left, 110f, "los brazos van siempre por encima de los hombros");
                Assert.Greater(right, 110f);
                Assert.Less(left, 180f);
                Assert.AreEqual(2f * ClimbCycle.ArmCentre, left + right, 1e-3f, "cuando uno se estira el otro se recoge");
            }
            Assert.Greater(ClimbCycle.ArmAngle(0.25f, true), ClimbCycle.ArmAngle(0.25f, false) + 30f);
        }

        [Test]
        public void Legs_PushInDiagonal_WithTheOppositeArm()
        {
            // En el cuarto de ciclo el brazo izquierdo está arriba: sube la pierna derecha.
            Assert.Greater(ClimbCycle.LegAngle(0.25f, false), ClimbCycle.LegAngle(0.25f, true) + 30f);
            Assert.Greater(ClimbCycle.LegAngle(0.75f, true), ClimbCycle.LegAngle(0.75f, false) + 30f);
        }

        [Test]
        public void Mantle_EndsWithArmsDown_AndLegsStraight()
        {
            Assert.AreEqual(ClimbCycle.ArmCentre, ClimbCycle.MantleArmAngle(0f), 1e-4f);
            Assert.Less(ClimbCycle.MantleArmAngle(1f), 40f, "termina empujando hacia abajo");
            Assert.Greater(ClimbCycle.MantleLegAngle(0.5f, true), 50f, "una pierna sube al borde");
            Assert.AreEqual(0f, ClimbCycle.MantleLegAngle(1f, true), 1e-3f);
            Assert.AreEqual(0f, ClimbCycle.MantleLegAngle(1f, false), 1e-3f);
        }
    }
}
