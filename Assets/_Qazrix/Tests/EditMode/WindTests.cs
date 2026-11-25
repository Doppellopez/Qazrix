using NUnit.Framework;
using Qazrix.Simulation.World;

namespace Qazrix.Tests
{
    /// <summary>Viento leve del oeste: sopla hacia el este, con ráfagas suaves que no lo paran.</summary>
    public sealed class WindTests
    {
        [Test]
        public void BlowsFromTheWestTowardsTheEast()
        {
            for (float t = 0f; t < 60f; t += 0.5f)
            {
                Wind.Velocity(t, out float x, out float z);
                Assert.Greater(x, 0f, $"hacia +X (este) a t={t}");
                Assert.AreEqual(0f, z, 1e-6f, "ni al norte ni al sur");
            }
        }

        [Test]
        public void ItIsAGentleBreezeThatNeverStopsNorGetsStrong()
        {
            float min = float.MaxValue, max = float.MinValue;
            for (float t = 0f; t < 120f; t += 0.05f)
            {
                float s = Wind.Speed(t);
                min = System.Math.Min(min, s);
                max = System.Math.Max(max, s);
            }
            Assert.Greater(min, 0.4f, "nunca se para del todo");
            Assert.Less(max, 3f, "es viento leve");
            Assert.GreaterOrEqual(min, Wind.MinSpeed - 1e-3f);
            Assert.LessOrEqual(max, Wind.MaxSpeed + 1e-3f);
            Assert.Greater(max - min, 0.5f, "tiene ráfagas");
        }

        [Test]
        public void GustsAreSmooth()
        {
            float previous = Wind.Speed(0f);
            for (float t = 1f / 60f; t < 60f; t += 1f / 60f)
            {
                float s = Wind.Speed(t);
                Assert.Less(System.Math.Abs(s - previous), 0.03f, $"a t={t:0.00}");
                previous = s;
            }
        }

        [Test]
        public void FacingEast_TheWindComesFromBehind()
        {
            Wind.RelativeToBody(3f, 90f, out float forward, out float right);
            Assert.AreEqual(Wind.Speed(3f), forward, 1e-4f);
            Assert.AreEqual(0f, right, 1e-4f);
        }

        [Test]
        public void FacingWest_TheWindBlowsInHisFace()
        {
            Wind.RelativeToBody(3f, 270f, out float forward, out float right);
            Assert.AreEqual(-Wind.Speed(3f), forward, 1e-4f);
            Assert.AreEqual(0f, right, 1e-4f);
        }

        [Test]
        public void FacingNorth_TheWindBlowsTowardsHisRight()
        {
            Wind.RelativeToBody(3f, 0f, out float forward, out float right);
            Assert.AreEqual(0f, forward, 1e-4f);
            Assert.AreEqual(Wind.Speed(3f), right, 1e-4f, "mirando al norte, el este queda a su derecha");
        }

        [Test]
        public void FacingSouth_TheWindBlowsTowardsHisLeft()
        {
            Wind.RelativeToBody(3f, 180f, out float forward, out float right);
            Assert.AreEqual(0f, forward, 1e-4f);
            Assert.AreEqual(-Wind.Speed(3f), right, 1e-4f);
        }

        [Test]
        public void TurningDoesNotChangeTheStrength()
        {
            for (float yaw = 0f; yaw < 360f; yaw += 15f)
            {
                Wind.RelativeToBody(7.7f, yaw, out float f, out float r);
                Assert.AreEqual(Wind.Speed(7.7f), (float)System.Math.Sqrt(f * f + r * r), 1e-4f, $"yaw {yaw}");
            }
        }
    }
}
