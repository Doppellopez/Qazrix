using NUnit.Framework;
using Qazrix.Simulation.Movement;

namespace Qazrix.Tests
{
    /// <summary>Animación de espera de la mujer del velo: respira, mira a los lados, cambia el peso y parpadea.</summary>
    public sealed class ContactIdleTests
    {
        private const float Step = 1f / 60f;

        [Test]
        public void EverythingStaysWithinItsLimits()
        {
            for (float t = 0f; t < 120f; t += Step)
            {
                ContactIdlePose p = ContactIdle.Sample(t);
                Assert.That(p.BreathExpand, Is.InRange(0f, ContactIdle.BreathExpandMax + 1e-6f));
                Assert.That(p.BreathLift, Is.InRange(0f, ContactIdle.BreathLiftMax + 1e-6f));
                Assert.That(System.Math.Abs(p.HeadYaw), Is.LessThanOrEqualTo(ContactIdle.HeadYawMax + 1e-4f));
                Assert.That(System.Math.Abs(p.HeadPitch), Is.LessThanOrEqualTo(ContactIdle.HeadPitchMax + 1e-4f));
                Assert.That(System.Math.Abs(p.HeadRoll), Is.LessThanOrEqualTo(ContactIdle.HeadRollMax + 1e-4f));
                Assert.That(System.Math.Abs(p.SwayX), Is.LessThanOrEqualTo(ContactIdle.SwayXMax + 1e-6f));
                Assert.That(System.Math.Abs(p.SwayRoll), Is.LessThanOrEqualTo(ContactIdle.SwayRollMax + 1e-4f));
                Assert.That(System.Math.Abs(p.LeftArm), Is.LessThanOrEqualTo(ContactIdle.ArmMax + 1e-4f));
                Assert.That(System.Math.Abs(p.RightArm), Is.LessThanOrEqualTo(ContactIdle.ArmMax + 1e-4f));
                Assert.That(System.Math.Abs(p.VeilSwing), Is.LessThanOrEqualTo(ContactIdle.VeilMax + 1e-4f));
                Assert.That(p.Blink, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void BreathingRepeatsEveryPeriod()
        {
            ContactIdlePose a = ContactIdle.Sample(1.3f), b = ContactIdle.Sample(1.3f + ContactIdle.BreathPeriod);
            Assert.AreEqual(a.BreathExpand, b.BreathExpand, 1e-4f);
            Assert.AreEqual(a.BreathLift, b.BreathLift, 1e-4f);
        }

        [Test]
        public void ItBreathesInAndOut()
        {
            float min = float.MaxValue, max = float.MinValue;
            for (float t = 0f; t < ContactIdle.BreathPeriod; t += Step)
            {
                float e = ContactIdle.Sample(t).BreathExpand;
                min = System.Math.Min(min, e);
                max = System.Math.Max(max, e);
            }
            Assert.Less(min, 0.001f);
            Assert.Greater(max, ContactIdle.BreathExpandMax * 0.99f);
        }

        [Test]
        public void TheHeadLooksBothWays()
        {
            float left = 0f, right = 0f;
            for (float t = 0f; t < ContactIdle.HeadPeriod; t += Step)
            {
                float yaw = ContactIdle.Sample(t).HeadYaw;
                left = System.Math.Min(left, yaw);
                right = System.Math.Max(right, yaw);
            }
            Assert.Less(left, -ContactIdle.HeadYawMax * 0.95f);
            Assert.Greater(right, ContactIdle.HeadYawMax * 0.95f);
        }

        [Test]
        public void ItBlinksBrieflyAndOnlyOnceInAWhile()
        {
            int closedFrames = 0, frames = 0;
            float peak = 0f;
            for (float t = 0f; t < ContactIdle.BlinkEvery * 10f; t += Step)
            {
                float b = ContactIdle.BlinkAt(t);
                peak = System.Math.Max(peak, b);
                if (b > 0.5f) closedFrames++;
                frames++;
            }
            Assert.Greater(peak, 0.99f, "los ojos llegan a cerrarse del todo");
            Assert.Less(closedFrames / (float)frames, 0.05f, "casi siempre los tiene abiertos");
            Assert.AreEqual(0f, ContactIdle.BlinkAt(ContactIdle.BlinkEvery * 0.5f), 1e-6f);
        }

        [Test]
        public void MotionIsSmooth_NoJumpsBetweenFrames()
        {
            ContactIdlePose previous = ContactIdle.Sample(0f);
            for (float t = Step; t < 60f; t += Step)
            {
                ContactIdlePose p = ContactIdle.Sample(t);
                Assert.Less(System.Math.Abs(p.HeadYaw - previous.HeadYaw), 1f, $"giro de cabeza a t={t:0.00}");
                Assert.Less(System.Math.Abs(p.VeilSwing - previous.VeilSwing), 0.2f, $"velo a t={t:0.00}");
                Assert.Less(System.Math.Abs(p.SwayX - previous.SwayX), 0.001f, $"balanceo a t={t:0.00}");
                previous = p;
            }
        }

        [Test]
        public void WithoutWindTheClothesAreAtRest()
        {
            ContactIdlePose p = ContactIdle.Sample(5f);
            Assert.AreEqual(0f, p.VeilRoll, 1e-6f);
            Assert.AreEqual(0f, p.DrapeSwing, 1e-6f);
            Assert.AreEqual(0f, p.SkirtPitch, 1e-6f);
            Assert.AreEqual(0f, p.SkirtRoll, 1e-6f);
        }

        [Test]
        public void WindInHerFaceLiftsTheVeil_WindAtHerBackPressesIt()
        {
            float noWind = ContactIdle.Sample(0f).VeilSwing;
            float headwind = ContactIdle.Sample(0f, -2f, 0f).VeilSwing;   // sopla hacia atrás: de cara
            float tailwind = ContactIdle.Sample(0f, 2f, 0f).VeilSwing;
            Assert.Greater(headwind, noWind + 4f, "de cara se levanta");
            Assert.Less(tailwind, noWind, "a favor se pega a la espalda");
            Assert.GreaterOrEqual(tailwind, noWind + ContactIdle.VeilPressMax - 1f, "pero no la atraviesa");
        }

        [Test]
        public void CrosswindTiltsTheVeilTowardsWhereItBlows()
        {
            Assert.Greater(ContactIdle.Sample(0f, 0f, 2f).VeilRoll, 3f, "hacia su derecha");
            Assert.Less(ContactIdle.Sample(0f, 0f, -2f).VeilRoll, -3f, "hacia su izquierda");
            Assert.Greater(ContactIdle.Sample(0f, 0f, 2f).SkirtRoll, 1f);
        }

        [Test]
        public void TailwindBlowsTheFaceGauzeAndTheHemForward()
        {
            ContactIdlePose p = ContactIdle.Sample(0f, 2f, 0f);
            Assert.Less(p.DrapeSwing, -3f, "la gasa se aparta hacia delante");
            Assert.Less(p.SkirtPitch, -1f, "el bajo se echa hacia delante");
        }

        [Test]
        public void LightWindKeepsEverythingWithinLimits()
        {
            for (float t = 0f; t < 60f; t += 0.05f)
            {
                ContactIdlePose p = ContactIdle.Sample(t, 1.6f * (float)System.Math.Sin(t), 1.6f * (float)System.Math.Cos(t));
                Assert.That(p.VeilSwing, Is.InRange(-ContactIdle.VeilMax - 2f, ContactIdle.VeilMax + ContactIdle.VeilLiftMax + 3f));
                Assert.That(System.Math.Abs(p.VeilRoll), Is.LessThanOrEqualTo(ContactIdle.VeilRollMax + 2f));
                Assert.That(System.Math.Abs(p.SkirtPitch), Is.LessThanOrEqualTo(ContactIdle.SkirtMax + 1e-4f));
                Assert.That(System.Math.Abs(p.SkirtRoll), Is.LessThanOrEqualTo(ContactIdle.SkirtMax + 2f));
            }
        }

        [Test]
        public void SamplingIsDeterministic()
        {
            Assert.AreEqual(ContactIdle.Sample(12.34f).HeadYaw, ContactIdle.Sample(12.34f).HeadYaw);
        }
    }
}
