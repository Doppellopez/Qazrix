using NUnit.Framework;
using Qazrix.Simulation.Movement;

namespace Qazrix.Tests
{
    /// <summary>Animación de salto y caída: posturas puras según la velocidad vertical.</summary>
    public sealed class AirCycleTests
    {
        [Test]
        public void Knees_BendWhenJumping_RiseApexAndFall()
        {
            Assert.AreEqual(AirCycle.RiseLeadKnee, AirCycle.KneeAngle(AirCycle.FullSpeed, 0f, true), 1e-4f);
            Assert.AreEqual(AirCycle.RiseTrailKnee, AirCycle.KneeAngle(AirCycle.FullSpeed, 0f, false), 1e-4f);
            Assert.AreEqual(AirCycle.ApexLeadKnee, AirCycle.KneeAngle(0f, 0f, true), 1e-4f);
            Assert.Greater(AirCycle.KneeAngle(0f, 0f, false), 30f, "en lo alto las piernas van recogidas");
            Assert.Greater(AirCycle.KneeAngle(-AirCycle.FullSpeed, 0f, true), 10f, "al caer siguen flexionadas");
            Assert.Less(AirCycle.KneeAngle(-AirCycle.FullSpeed, 0f, true), AirCycle.KneeAngle(0f, 0f, true), "al caer se estiran algo");
        }

        [Test]
        public void Landing_BendsKnees_ThenReleases()
        {
            Assert.AreEqual(AirCycle.LandingKnee, AirCycle.LandingKneeAngle(0f), 1e-4f);
            Assert.AreEqual(0f, AirCycle.LandingKneeAngle(AirCycle.LandingSeconds), 1e-4f);
        }

        [Test]
        public void Blend_FollowsVerticalSpeed_AndIsClamped()
        {
            Assert.AreEqual(0f, AirCycle.Blend(0f), 1e-6f);
            Assert.AreEqual(0.5f, AirCycle.Blend(AirCycle.FullSpeed * 0.5f), 1e-6f);
            Assert.AreEqual(1f, AirCycle.Blend(50f), 1e-6f);
            Assert.AreEqual(-1f, AirCycle.Blend(-50f), 1e-6f);
        }

        [Test]
        public void Rising_ThrowsBothArmsUp_AndLeadsWithOneKnee()
        {
            float left = AirCycle.ArmAngle(AirCycle.FullSpeed, 0.1f, true);
            float right = AirCycle.ArmAngle(AirCycle.FullSpeed, 0.1f, false);
            Assert.AreEqual(AirCycle.RiseArm, left, 1e-4f);
            Assert.AreEqual(left, right, 1e-4f, "al subir los brazos van juntos");
            Assert.Greater(left, 90f, "por encima de los hombros");

            Assert.Greater(AirCycle.LegAngle(AirCycle.FullSpeed, 0.1f, true), 40f, "una rodilla delante");
            Assert.Less(AirCycle.LegAngle(AirCycle.FullSpeed, 0.1f, false), -20f, "la otra pierna atrás");
        }

        [Test]
        public void Apex_IsATuck_BetweenRiseAndFall()
        {
            Assert.AreEqual(AirCycle.ApexArm, AirCycle.ArmAngle(0f, 0.3f, true), 1e-4f);
            Assert.AreEqual(AirCycle.ApexArm, AirCycle.ArmAngle(0f, 0.3f, false), 1e-4f, "en lo alto no hay braceo");
            Assert.AreEqual(AirCycle.ApexLeadLeg, AirCycle.LegAngle(0f, 0.3f, true), 1e-4f);
            Assert.AreEqual(AirCycle.ApexTrailLeg, AirCycle.LegAngle(0f, 0.3f, false), 1e-4f);
        }

        [Test]
        public void Falling_RaisesTheArms_Flails_AndBringsBothLegsForward()
        {
            float widestGap = 0f;
            for (float t = 0f; t < 1f; t += 0.02f)
            {
                float left = AirCycle.ArmAngle(-20f, t, true), right = AirCycle.ArmAngle(-20f, t, false);
                Assert.Greater(left, 120f, "cayendo, los brazos van arriba");
                Assert.Greater(right, 120f);
                Assert.Less(left, 180f);
                Assert.AreEqual(2f * AirCycle.FallArm, left + right, 1e-3f, "bracean alternándose");
                widestGap = System.Math.Max(widestGap, System.Math.Abs(left - right));

                Assert.Greater(AirCycle.LegAngle(-20f, t, true), 0f, "las piernas buscan el suelo por delante");
                Assert.Greater(AirCycle.LegAngle(-20f, t, false), 0f);
            }
            Assert.Greater(widestGap, 30f, "el braceo se nota");
        }

        [Test]
        public void Landing_StartsStrong_AndFadesOut()
        {
            Assert.IsTrue(AirCycle.IsLanding(0f));
            Assert.IsFalse(AirCycle.IsLanding(AirCycle.LandingSeconds));
            Assert.IsFalse(AirCycle.IsLanding(-1f));

            Assert.AreEqual(AirCycle.LandingArm, AirCycle.LandingArmAngle(0f), 1e-4f);
            Assert.AreEqual(AirCycle.LandingLeg, AirCycle.LandingLegAngle(0f, true), 1e-4f);
            Assert.AreEqual(-AirCycle.LandingLeg, AirCycle.LandingLegAngle(0f, false), 1e-4f, "zancada: una delante y otra atrás");
            Assert.Less(AirCycle.LandingArmAngle(AirCycle.LandingSeconds * 0.5f), AirCycle.LandingArm);
            Assert.AreEqual(0f, AirCycle.LandingArmAngle(AirCycle.LandingSeconds), 1e-6f);
            Assert.AreEqual(0f, AirCycle.LandingLegAngle(1f, true), 1e-6f);
        }
    }
}
