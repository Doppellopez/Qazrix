using System.Linq;
using NUnit.Framework;
using Qazrix.Simulation.Movement;

namespace Qazrix.Tests
{
    /// <summary>Los soldados alzan las lanzas al descompás al recibir el capitán la llave.</summary>
    public sealed class SpearSaluteTests
    {
        [Test]
        public void SpearsStartDownAndEndDown()
        {
            for (int i = 0; i < 50; i++)
            {
                var s = SpearSalute.ScheduleFor(i);
                Assert.AreEqual(0f, SpearSalute.Weight(0f, s), 1e-6f, $"soldado {i} al empezar");
                Assert.AreEqual(0f, SpearSalute.Weight(s.End + 0.1f, s), 1e-6f, $"soldado {i} al final");
                Assert.AreEqual(1f, SpearSalute.Weight(s.Delay + s.Rise + s.Hold * 0.5f, s), 1e-6f, $"soldado {i} arriba del todo");
            }
        }

        [Test]
        public void EverySoldierStartsAtAnotherMoment_OutOfStep()
        {
            var delays = Enumerable.Range(0, 50).Select(i => SpearSalute.ScheduleFor(i).Delay).ToArray();
            Assert.Greater(delays.Distinct().Count(), 40, "casi todos empiezan en un momento distinto");
            Assert.Greater(delays.Max() - delays.Min(), 1.0f, "repartidos en más de un segundo");
            Assert.LessOrEqual(delays.Max(), SpearSalute.MaxDelay + 1e-4f);
            Assert.GreaterOrEqual(delays.Min(), 0f);
        }

        [Test]
        public void TheyRaiseTheSpearAtDifferentSpeedsAndHoldItForDifferentTimes()
        {
            var rises = Enumerable.Range(0, 50).Select(i => SpearSalute.ScheduleFor(i).Rise).ToArray();
            var holds = Enumerable.Range(0, 50).Select(i => SpearSalute.ScheduleFor(i).Hold).ToArray();
            Assert.Greater(rises.Max() - rises.Min(), 0.15f);
            Assert.Greater(holds.Max() - holds.Min(), 1.5f);
            Assert.GreaterOrEqual(holds.Min(), 2.5f - 1e-4f, "se mantienen alzadas un buen rato");
        }

        [Test]
        public void AtAnyMomentSomeAreUpSomeAreRisingAndSomeHaveNotStarted()
        {
            float t = 0.8f;
            var weights = Enumerable.Range(0, 50).Select(i => SpearSalute.Weight(t, SpearSalute.ScheduleFor(i))).ToArray();
            Assert.GreaterOrEqual(weights.Count(w => w >= 0.99f), 3, "algunos ya la tienen arriba");
            Assert.GreaterOrEqual(weights.Count(w => w > 0.01f && w < 0.99f), 2, "otros la están subiendo");
            Assert.GreaterOrEqual(weights.Count(w => w <= 0.0001f), 3, "otros ni han empezado");
        }

        [Test]
        public void EverybodyHasItUpTogetherForAWhile()
        {
            float t = SpearSalute.MaxDelay + 0.6f + 0.2f;
            for (int i = 0; i < 50; i++)
                Assert.AreEqual(1f, SpearSalute.Weight(t, SpearSalute.ScheduleFor(i)), 1e-6f, $"soldado {i} a los {t:0.0} s");
        }

        [Test]
        public void TheMotionIsSmoothAndTheSpearRises()
        {
            var s = SpearSalute.ScheduleFor(7);
            float previous = 0f;
            for (float t = 0f; t < s.End + 0.2f; t += 1f / 60f)
            {
                float w = SpearSalute.Weight(t, s);
                Assert.Less(System.Math.Abs(w - previous), 0.1f, $"a t={t:0.00}");
                previous = w;
            }
            Assert.AreEqual(SpearSalute.MaxLift, SpearSalute.Lift(1f), 1e-6f);
            Assert.AreEqual(SpearSalute.MaxArmDegrees, SpearSalute.ArmDegrees(1f), 1e-6f);
            Assert.AreEqual(0f, SpearSalute.Lift(0f), 1e-6f);
        }

        [Test]
        public void TheScheduleIsTheSameEveryTime()
        {
            Assert.AreEqual(SpearSalute.ScheduleFor(12).Delay, SpearSalute.ScheduleFor(12).Delay);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => SpearSalute.ScheduleFor(-1));
        }
    }
}
