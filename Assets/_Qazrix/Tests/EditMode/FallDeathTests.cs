using NUnit.Framework;
using Qazrix.Simulation.Movement;

namespace Qazrix.Tests
{
    /// <summary>Morir al caer desde 15 m o más, con su propia animación.</summary>
    public sealed class FallDeathTests
    {
        [Test]
        public void Drop_IsFatal_FromFifteenMetres()
        {
            Assert.AreEqual(15f, FallTracker.FatalDrop);
            Assert.IsFalse(FallTracker.IsFatal(14.99f));
            Assert.IsTrue(FallTracker.IsFatal(15f));
            Assert.IsTrue(FallTracker.IsFatal(40f));
        }

        [Test]
        public void Tracker_MeasuresFromTheHighestPoint_AtTheMomentOfLanding()
        {
            var t = new FallTracker();
            t.Reset(0f);
            Assert.AreEqual(0f, t.Step(true, 0f), "en el suelo no hay caída");
            Assert.AreEqual(0f, t.Step(false, 1.2f));
            Assert.AreEqual(0f, t.Step(false, 1.5f), "subiendo no es una caída");
            Assert.AreEqual(0f, t.Step(false, 0.5f));
            Assert.AreEqual(1.5f, t.Step(true, 0f), 1e-5f, "al aterrizar da lo caído desde el punto más alto");
            Assert.AreEqual(0f, t.Step(true, 0f), "y solo una vez");
        }

        [Test]
        public void Tracker_CountsTheWholeDrop_AfterClimbingHigh()
        {
            var t = new FallTracker();
            t.Reset(0f);
            t.Step(false, 20f); // suelta una pared a 20 m
            t.Step(false, 10f);
            float drop = t.Step(true, 0f);
            Assert.AreEqual(20f, drop, 1e-5f);
            Assert.IsTrue(FallTracker.IsFatal(drop));
        }

        [Test]
        public void Tracker_LandingOnAHigherRoof_IsNotADrop()
        {
            var t = new FallTracker();
            t.Reset(0f);
            t.Step(false, 3f);
            Assert.AreEqual(0f, t.Step(true, 5f), "subirse a un tejado no es caer");
        }

        [Test]
        public void Tracker_ResetForgetsTheOldHeight()
        {
            var t = new FallTracker();
            t.Step(false, 50f);
            t.Reset(2f);
            Assert.AreEqual(2f, t.Peak);
            Assert.AreEqual(0f, t.Step(true, 2f));
        }

        [Test]
        public void FallDeath_ToppleFaceDown_AndEndsLying()
        {
            Assert.AreEqual(0f, FallDeathCycle.BodyPitch(0f), 1e-5f);
            Assert.AreEqual(FallDeathCycle.CrumplePitch, FallDeathCycle.BodyPitch(FallDeathCycle.CrumpleSeconds), 1e-3f);
            Assert.AreEqual(FallDeathCycle.LyingPitch, FallDeathCycle.BodyPitch(FallDeathCycle.CrumpleSeconds + FallDeathCycle.ToppleSeconds), 1e-3f);
            Assert.AreEqual(FallDeathCycle.LyingPitch, FallDeathCycle.BodyPitch(FallDeathCycle.TotalSeconds), 1e-3f);
            float last = -1f;
            for (float t = 0f; t <= FallDeathCycle.TotalSeconds; t += 0.01f)
            {
                float p = FallDeathCycle.BodyPitch(t);
                Assert.GreaterOrEqual(p, last - 1e-4f);
                Assert.LessOrEqual(p, FallDeathCycle.LyingPitch + 1e-4f);
                last = p;
            }
        }

        [Test]
        public void FallDeath_BouncesOffTheGround_ThenSettles()
        {
            float settled = FallDeathCycle.LyingLift;
            float during = FallDeathCycle.BodyLift(FallDeathCycle.CrumpleSeconds + FallDeathCycle.ToppleSeconds + FallDeathCycle.BounceSeconds * 0.5f);
            Assert.Greater(during, settled + 0.05f, "rebota un poco");
            Assert.AreEqual(settled, FallDeathCycle.BodyLift(FallDeathCycle.TotalSeconds), 1e-4f, "y se queda quieto");
        }

        [Test]
        public void FallDeath_ThrowsArmsForwardAndBucklesTheKnees()
        {
            Assert.Greater(FallDeathCycle.ArmAngle(FallDeathCycle.CrumpleSeconds, true), 150f, "brazos lanzados hacia delante");
            Assert.AreEqual(70f, FallDeathCycle.KneeAngle(FallDeathCycle.CrumpleSeconds), 1e-3f, "las rodillas ceden al aterrizar");
            Assert.AreEqual(20f, FallDeathCycle.KneeAngle(FallDeathCycle.TotalSeconds), 1e-3f);
        }

        [Test]
        public void TheTwoDeaths_LookDifferent_AndFallLastsLonger()
        {
            DeathPoseSample sword = DeathPoses.Sample(DeathKind.Sword, 1.0f);
            DeathPoseSample fall = DeathPoses.Sample(DeathKind.Fall, 1.0f);
            Assert.Greater(fall.LeftArm, sword.LeftArm + 50f, "de bruces los brazos van hacia delante, de espaldas quedan abiertos");
            Assert.AreNotEqual(sword.LeftKnee, fall.LeftKnee);
            Assert.Greater(DeathPoses.TotalSeconds(DeathKind.Fall), DeathPoses.TotalSeconds(DeathKind.Sword));
            Assert.IsFalse(DeathPoses.IsFinished(DeathKind.Fall, DeathCycle.TotalSeconds), "la caída sigue cuando el tajo ya acabó");
            Assert.IsTrue(DeathPoses.IsFinished(DeathKind.Sword, DeathCycle.TotalSeconds));
        }
    }
}
