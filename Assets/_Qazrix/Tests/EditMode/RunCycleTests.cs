using NUnit.Framework;
using Qazrix.Simulation.Movement;

namespace Qazrix.Tests
{
    /// <summary>Lo que distingue correr de andar: zancada, rodillas, codos, inclinación y rebote.</summary>
    public sealed class RunCycleTests
    {
        [Test]
        public void Blend_IsZeroWalking_OneRunning_AndSmoothBetween()
        {
            Assert.AreEqual(0f, RunCycle.Blend(2.2f), 1e-5f, "andando (2,2 m/s) no se corre");
            Assert.AreEqual(1f, RunCycle.Blend(5.5f), 1e-5f, "a 5,5 m/s se corre del todo");
            float mid = RunCycle.Blend((RunCycle.StartSpeed + RunCycle.FullSpeed) * 0.5f);
            Assert.AreEqual(0.5f, mid, 1e-4f);
            Assert.Greater(RunCycle.Blend(3.8f), RunCycle.Blend(3.4f), "crece con la velocidad");
        }

        [Test]
        public void Stride_IsLongerRunning_AndUnchangedWalking()
        {
            Assert.AreEqual(WalkCycle.StrideLength, RunCycle.StrideLength(2.2f), 1e-5f);
            Assert.AreEqual(RunCycle.RunStrideLength, RunCycle.StrideLength(5.5f), 1e-5f);
            Assert.Greater(RunCycle.StrideLength(5.5f), WalkCycle.StrideLength);
            // El ritmo de pasos debe seguir siendo humano: entre 1 y 2 ciclos por segundo (2 a 4 pasos por segundo).
            float cyclesPerSecond = 5.5f / RunCycle.StrideLength(5.5f);
            Assert.That(cyclesPerSecond, Is.InRange(1f, 2.2f));
        }

        [Test]
        public void Knee_BendsMostWhenTheLegSwingsForwardUnderTheBody()
        {
            // La pierna izquierda cruza vertical yendo hacia delante en fase 0 (el seno sube).
            float swinging = RunCycle.KneeAngle(0f, 5.5f, true);
            float planted = RunCycle.KneeAngle(0.5f, 5.5f, true);
            Assert.AreEqual(RunCycle.MaxKnee, swinging, 1e-3f);
            Assert.Less(planted, 15f, "apoyada, la rodilla casi se estira");
            Assert.Greater(swinging, planted + 50f);
            // La otra pierna va a contrafase.
            Assert.AreEqual(planted, RunCycle.KneeAngle(0f, 5.5f, false), 1e-3f);
            Assert.AreEqual(0f, RunCycle.KneeAngle(0f, 0f, true), "parado no hay rodilla doblada");
        }

        [Test]
        public void Knee_NeverBendsTheWrongWay()
        {
            for (float p = 0f; p < 1f; p += 0.01f)
                foreach (float speed in new[] { 0.5f, 2.2f, 4f, 5.5f })
                {
                    Assert.GreaterOrEqual(RunCycle.KneeAngle(p, speed, true), 0f);
                    Assert.LessOrEqual(RunCycle.KneeAngle(p, speed, true), RunCycle.MaxKnee + 1e-3f);
                }
        }

        [Test]
        public void Elbow_IsAlmostStraightWalking_AndRightAngleRunning()
        {
            Assert.Less(RunCycle.ElbowAngle(2.2f), 15f);
            Assert.AreEqual(RunCycle.RunElbow, RunCycle.ElbowAngle(5.5f), 1e-4f);
            Assert.AreEqual(0f, RunCycle.ElbowAngle(0f));
        }

        [Test]
        public void Body_LeansAndBobsOnlyWhenRunning()
        {
            Assert.AreEqual(0f, RunCycle.Lean(2.2f), 1e-5f);
            Assert.AreEqual(RunCycle.RunLean, RunCycle.Lean(5.5f), 1e-4f);
            Assert.AreEqual(0f, RunCycle.BodyLift(0.25f, 2.2f), 1e-6f, "andando no rebota");
            // Dos botes por ciclo: el cuerpo está más bajo con las piernas juntas y más alto con ellas abiertas.
            Assert.AreEqual(0f, RunCycle.BodyLift(0f, 5.5f), 1e-5f);
            Assert.AreEqual(RunCycle.RunBob, RunCycle.BodyLift(0.25f, 5.5f), 1e-5f);
            Assert.AreEqual(0f, RunCycle.BodyLift(0.5f, 5.5f), 1e-5f);
            Assert.AreEqual(RunCycle.RunBob, RunCycle.BodyLift(0.75f, 5.5f), 1e-5f);
        }

        [Test]
        public void RunningSwingsMoreThanWalking()
        {
            Assert.AreEqual(1f, RunCycle.SwingScale(2.2f), 1e-5f);
            Assert.AreEqual(1f + RunCycle.ExtraSwing, RunCycle.SwingScale(5.5f), 1e-5f);
        }
    }
}
