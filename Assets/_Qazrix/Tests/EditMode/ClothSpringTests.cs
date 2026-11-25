using NUnit.Framework;
using Qazrix.Simulation.Movement;

namespace Qazrix.Tests
{
    /// <summary>Tela con inercia: sigue al viento con retraso y se queda atrás cuando el cuerpo acelera.</summary>
    public sealed class ClothSpringTests
    {
        private static void Run(ClothSpring spring, float seconds, float target)
        {
            for (float t = 0f; t < seconds; t += 1f / 60f) spring.Step(1f / 60f, target);
        }

        [Test]
        public void ConvergesToTheTarget()
        {
            var spring = new ClothSpring(40f, 8f);
            Run(spring, 4f, 5f);
            Assert.AreEqual(5f, spring.Value, 0.01f);
        }

        [Test]
        public void LagsBehindASuddenChange()
        {
            var spring = new ClothSpring(40f, 8f);
            Run(spring, 0.05f, 10f);
            Assert.Greater(spring.Value, 0f);
            Assert.Less(spring.Value, 5f, "no salta de golpe al objetivo");
        }

        [Test]
        public void LightDampingMakesItOvershoot()
        {
            var bouncy = new ClothSpring(60f, 2f);
            float peak = 0f;
            for (float t = 0f; t < 3f; t += 1f / 60f)
            {
                bouncy.Step(1f / 60f, 10f);
                peak = System.Math.Max(peak, bouncy.Value);
            }
            Assert.Greater(peak, 10.5f, "rebota pasándose un poco");
        }

        [Test]
        public void StaysStableWithALongPause()
        {
            var spring = new ClothSpring(40f, 8f);
            spring.Step(10f, 5f); // una pausa larga no debe dispararla
            Assert.That(spring.Value, Is.InRange(-1f, 11f));
            Assert.IsFalse(float.IsNaN(spring.Value));
        }

        [Test]
        public void ResetSetsItAtRest()
        {
            var spring = new ClothSpring(40f, 8f, 3f);
            Run(spring, 1f, 9f);
            spring.Reset(0f);
            Run(spring, 0.001f, 0f);
            Assert.AreEqual(0f, spring.Value, 1e-3f);
        }

        [Test]
        public void AcceleratingForwardLeavesTheClothBehind()
        {
            ClothInertia.Offsets(10f, 0f, out float lift, out float roll, out float drape, out float skirt, out _);
            Assert.Greater(lift, 3f, "el velo se levanta, queda atrás");
            Assert.AreEqual(0f, roll, 1e-6f);
            Assert.Greater(drape, 1f);
            Assert.Greater(skirt, 1f);
        }

        [Test]
        public void BrakingPushesTheClothForward_ButOnlyALittle()
        {
            ClothInertia.Offsets(-10f, 0f, out float lift, out _, out float drape, out _, out _);
            Assert.Less(lift, 0f);
            Assert.GreaterOrEqual(lift, -4f, "sin atravesar la espalda");
            Assert.Less(drape, 0f);
        }

        [Test]
        public void MovingRightLeavesTheClothToTheLeft()
        {
            ClothInertia.Offsets(0f, 10f, out _, out float roll, out _, out _, out float skirtRoll);
            Assert.Less(roll, -3f);
            Assert.Less(skirtRoll, -1f);
        }

        [Test]
        public void TeleportJoltsAreIgnored()
        {
            ClothInertia.Offsets(5000f, 5000f, out float lift, out float roll, out float drape, out float skirtPitch, out float skirtRoll);
            Assert.LessOrEqual(lift, 14f);
            Assert.GreaterOrEqual(roll, -14f);
            Assert.LessOrEqual(drape, 8f);
            Assert.LessOrEqual(skirtPitch, 6f);
            Assert.GreaterOrEqual(skirtRoll, -6f);
        }
    }
}
