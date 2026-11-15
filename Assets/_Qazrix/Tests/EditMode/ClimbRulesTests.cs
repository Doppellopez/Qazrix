using NUnit.Framework;
using Qazrix.Simulation.AI;
using Qazrix.Simulation.Movement;

namespace Qazrix.Tests
{
    /// <summary>Trepada libre por fachadas: reglas puras (spec §6.1).</summary>
    public sealed class ClimbRulesTests
    {
        private static readonly ClimbLimits Limits = new ClimbLimits();

        [Test]
        public void Grab_NeedsRunningStraightAtAClimbableWall()
        {
            Assert.IsTrue(ClimbRules.CanGrab(true, true, false, true, 0.9f, 0f, Limits));
            Assert.IsFalse(ClimbRules.CanGrab(false, true, false, true, 0.9f, 0f, Limits), "andando no trepa");
            Assert.IsFalse(ClimbRules.CanGrab(true, false, false, true, 0.9f, 0f, Limits), "parado no trepa");
            Assert.IsFalse(ClimbRules.CanGrab(true, true, true, true, 0.9f, 0f, Limits), "agachado no trepa");
            Assert.IsFalse(ClimbRules.CanGrab(true, true, false, false, 0.9f, 0f, Limits), "la superficie debe ser trepable");
            Assert.IsFalse(ClimbRules.CanGrab(true, true, false, true, 0.3f, 0f, Limits), "de refilón no se agarra");
            Assert.IsFalse(ClimbRules.CanGrab(true, true, false, true, 0.9f, 0.8f, Limits), "una rampa no es una pared");
        }

        [Test]
        public void VerticalSpeed_FollowsTheStick_WithADeadZone()
        {
            Assert.AreEqual(Limits.UpSpeed, ClimbRules.VerticalSpeed(1f, Limits), 1e-5f);
            Assert.AreEqual(-Limits.DownSpeed, ClimbRules.VerticalSpeed(-1f, Limits), 1e-5f);
            Assert.AreEqual(0f, ClimbRules.VerticalSpeed(0.1f, Limits), "sin empujar se queda colgado");
            Assert.AreEqual(Limits.UpSpeed, ClimbRules.VerticalSpeed(5f, Limits), 1e-5f, "la entrada se limita a 1");
        }

        [Test]
        public void Mantle_RisesFirst_ThenStepsOntoTheRoof()
        {
            var start = new Float3(0f, 8.9f, 0f);
            var end = new Float3(0f, 10f, 0.8f);

            Float3 halfway = ClimbRules.Mantle(start, end, 0.3f);
            Assert.AreEqual(0f, halfway.Z, 1e-5f, "mientras sube no avanza: no atraviesa la esquina");
            Assert.Greater(halfway.Y, start.Y);

            Float3 edge = ClimbRules.Mantle(start, end, 0.6f);
            Assert.AreEqual(10f, edge.Y, 1e-4f);
            Assert.AreEqual(0f, edge.Z, 1e-4f);

            Float3 done = ClimbRules.Mantle(start, end, 1.5f);
            Assert.AreEqual(10f, done.Y, 1e-5f);
            Assert.AreEqual(0.8f, done.Z, 1e-5f);
        }
    }
}
