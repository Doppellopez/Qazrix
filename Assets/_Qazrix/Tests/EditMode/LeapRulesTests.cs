using System;
using NUnit.Framework;
using Qazrix.Simulation.AI;
using Qazrix.Simulation.Movement;

namespace Qazrix.Tests
{
    /// <summary>Salto de fe: reglas puras de alcance y trayectoria.</summary>
    public sealed class LeapRulesTests
    {
        private readonly LeapLimits _limits = new LeapLimits();

        [Test]
        public void CanLeap_NeedsEnoughDrop_AndReachableDistance()
        {
            Assert.IsTrue(LeapRules.CanLeap(21f, 10f, _limits));
            Assert.IsFalse(LeapRules.CanLeap(3f, 10f, _limits), "un escalón no es un salto de fe");
            Assert.IsFalse(LeapRules.CanLeap(21f, 30f, _limits), "demasiado lejos");
            Assert.IsFalse(LeapRules.CanLeap(21f, 0.1f, _limits), "sin distancia no hay salto");
        }

        [Test]
        public void FlightTime_GrowsWithDrop_AndMatchesTheBallisticFormula()
        {
            float shortFall = LeapRules.FlightTime(8f, _limits);
            float longFall = LeapRules.FlightTime(21f, _limits);
            Assert.Greater(longFall, shortFall);

            // Con el tiempo calculado, la altura ballística baja exactamente el desnivel.
            float y = _limits.LaunchUp * longFall + 0.5f * _limits.Gravity * longFall * longFall;
            Assert.AreEqual(-21f, y, 1e-3f);
        }

        [Test]
        public void Position_StartsAtTheEdge_AndEndsExactlyAtTheTarget()
        {
            var start = new Float3(10f, 30f, 5f);
            var end = new Float3(20f, 9f, 5f);
            float duration = LeapRules.FlightTime(21f, _limits);

            Float3 first = LeapRules.Position(start, end, duration, 0f, _limits);
            Assert.AreEqual(start.X, first.X, 1e-5f);
            Assert.AreEqual(start.Y, first.Y, 1e-5f);

            Float3 last = LeapRules.Position(start, end, duration, duration, _limits);
            Assert.AreEqual(end.X, last.X, 1e-4f);
            Assert.AreEqual(end.Y, last.Y, 1e-4f);
            Assert.AreEqual(end.Z, last.Z, 1e-4f);

            Float3 beyond = LeapRules.Position(start, end, duration, duration * 3f, _limits);
            Assert.AreEqual(end.Y, beyond.Y, 1e-4f, "pasado el tiempo se queda en el destino");
        }

        [Test]
        public void Position_RisesFirst_ThenFalls_AndAdvancesSteadily()
        {
            var start = new Float3(0f, 30f, 0f);
            var end = new Float3(12f, 9f, 0f);
            float duration = LeapRules.FlightTime(21f, _limits);

            Float3 early = LeapRules.Position(start, end, duration, 0.1f, _limits);
            Assert.Greater(early.Y, start.Y, "al saltar del borde sube un poco antes de caer");

            Float3 mid = LeapRules.Position(start, end, duration, duration * 0.5f, _limits);
            Assert.AreEqual(6f, mid.X, 1e-3f, "avance horizontal uniforme");
            Assert.Less(mid.Y, start.Y);
        }

        [Test]
        public void Rules_RejectNullLimits()
        {
            Assert.Throws<ArgumentNullException>(() => LeapRules.CanLeap(10f, 5f, null));
            Assert.Throws<ArgumentNullException>(() => LeapRules.FlightTime(10f, null));
        }

        [Test]
        public void DivePose_ThrowsTheArmsUp()
        {
            Assert.Greater(LeapRules.DiveArm, 90f, "brazos por encima de los hombros");
        }
    }
}
