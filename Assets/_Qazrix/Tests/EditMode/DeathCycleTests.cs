using NUnit.Framework;
using Qazrix.Simulation.Movement;

namespace Qazrix.Tests
{
    /// <summary>Muerte del jugador por la cimitarra y tajo del guardia.</summary>
    public sealed class DeathCycleTests
    {
        [Test]
        public void StartsStanding_AndEndsLyingFlat()
        {
            Assert.AreEqual(0f, DeathCycle.BodyPitch(0f), 1e-4f);
            Assert.AreEqual(DeathCycle.LyingPitch, DeathCycle.BodyPitch(DeathCycle.HitSeconds + DeathCycle.FallSeconds), 1e-3f);
            Assert.AreEqual(DeathCycle.LyingPitch, DeathCycle.BodyPitch(DeathCycle.TotalSeconds), 1e-3f, "queda tumbado hasta reaparecer");
        }

        [Test]
        public void Pitch_NeverGoesBackUp_AndNeverOvershoots()
        {
            float last = -1f;
            for (float t = 0f; t <= DeathCycle.TotalSeconds + 1f; t += 0.01f)
            {
                float pitch = DeathCycle.BodyPitch(t);
                Assert.GreaterOrEqual(pitch, last - 1e-4f, $"en t={t:0.00} no se incorpora");
                Assert.LessOrEqual(pitch, DeathCycle.LyingPitch + 1e-4f);
                last = pitch;
            }
        }

        [Test]
        public void Hit_ArchesTheBodyBeforeTheFall()
        {
            Assert.AreEqual(DeathCycle.StaggerPitch, DeathCycle.BodyPitch(DeathCycle.HitSeconds), 1e-3f);
            Assert.Greater(DeathCycle.BodyPitch(DeathCycle.HitSeconds * 0.5f), 0f);
        }

        [Test]
        public void Fall_AcceleratesLikeABody()
        {
            float early = DeathCycle.BodyPitch(DeathCycle.HitSeconds + DeathCycle.FallSeconds * 0.25f) - DeathCycle.StaggerPitch;
            float late = DeathCycle.BodyPitch(DeathCycle.HitSeconds + DeathCycle.FallSeconds) - DeathCycle.BodyPitch(DeathCycle.HitSeconds + DeathCycle.FallSeconds * 0.75f);
            Assert.Greater(late, early * 3f, "los últimos instantes caen más que los primeros");
        }

        [Test]
        public void Body_RisesTheThicknessOfTheTorso_WhenLyingDown()
        {
            Assert.AreEqual(0f, DeathCycle.BodyLift(0f), 1e-5f);
            Assert.AreEqual(DeathCycle.LyingLift, DeathCycle.BodyLift(DeathCycle.TotalSeconds), 1e-4f);
        }

        [Test]
        public void Limbs_GoLimp_AndTheKneesOnlyBendTheRightWay()
        {
            Assert.AreEqual(0f, DeathCycle.KneeAngle(0f, true), 1e-5f);
            for (float t = 0f; t <= DeathCycle.TotalSeconds; t += 0.05f)
            {
                Assert.GreaterOrEqual(DeathCycle.KneeAngle(t, true), 0f);
                Assert.GreaterOrEqual(DeathCycle.KneeAngle(t, false), 0f);
                Assert.GreaterOrEqual(DeathCycle.ElbowAngle(t), 0f);
            }
            Assert.Greater(DeathCycle.ArmAngle(0.4f, true), 100f, "los brazos salen disparados con el golpe");
            Assert.AreEqual(60f, DeathCycle.ArmAngle(DeathCycle.TotalSeconds, true), 1e-3f, "y quedan abiertos en el suelo");
        }

        [Test]
        public void Finishes_AfterTheTotalTime()
        {
            Assert.IsFalse(DeathCycle.IsFinished(DeathCycle.TotalSeconds - 0.01f));
            Assert.IsTrue(DeathCycle.IsFinished(DeathCycle.TotalSeconds));
        }

        [Test]
        public void Strike_SwingsTheArmDownFast_AndLowersTheBlade()
        {
            Assert.AreEqual(WeaponRaise.ArmUp, StrikeCycle.ArmAngle(0f), 1e-3f, "empieza con el brazo en alto");
            Assert.AreEqual(StrikeCycle.LowArm, StrikeCycle.ArmAngle(StrikeCycle.HitAt), 1e-3f, "llega abajo al golpear");
            Assert.AreEqual(1f, StrikeCycle.RaiseWeight(0f));
            Assert.AreEqual(1f, StrikeCycle.RaiseWeight(StrikeCycle.HitAt));
            Assert.AreEqual(0f, StrikeCycle.RaiseWeight(StrikeCycle.Seconds), 1e-5f);
            Assert.IsTrue(StrikeCycle.IsFinished(StrikeCycle.Seconds));
            float last = 999f;
            for (float t = 0f; t <= StrikeCycle.HitAt; t += 0.005f)
            {
                float arm = StrikeCycle.ArmAngle(t);
                Assert.LessOrEqual(arm, last + 1e-4f, "el brazo solo baja");
                last = arm;
            }
        }
    }
}
