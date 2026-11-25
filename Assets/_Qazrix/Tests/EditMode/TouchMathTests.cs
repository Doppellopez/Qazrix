using NUnit.Framework;
using Qazrix.Simulation.Input;

namespace Qazrix.Tests.EditMode
{
    public class TouchMathTests
    {
        [Test]
        public void Stick_PushedUp_IsForward()
        {
            TouchMath.Stick(100f, 300f, 100f, 240f, 60f, out float x, out float y);
            Assert.AreEqual(0f, x, 1e-4f);
            Assert.AreEqual(1f, y, 1e-4f); // 60 px hacia arriba = recorrido completo
        }

        [Test]
        public void Stick_PushedRight_IsRight_AndIsClampedToOne()
        {
            TouchMath.Stick(100f, 300f, 400f, 300f, 60f, out float x, out float y);
            Assert.AreEqual(1f, x, 1e-4f);
            Assert.AreEqual(0f, y, 1e-4f);
        }

        [Test]
        public void Stick_InsideTheDeadzone_IsZero()
        {
            TouchMath.Stick(100f, 300f, 103f, 298f, 60f, out float x, out float y);
            Assert.AreEqual(0f, x);
            Assert.AreEqual(0f, y);
        }

        [Test]
        public void Stick_Diagonal_NeverExceedsOne()
        {
            TouchMath.Stick(0f, 0f, 500f, -500f, 60f, out float x, out float y);
            Assert.LessOrEqual(x * x + y * y, 1.0001f);
            Assert.Greater(x, 0f);
            Assert.Greater(y, 0f);
        }

        [Test]
        public void InCircle_HitsInsideAndMissesOutside()
        {
            Assert.IsTrue(TouchMath.InCircle(105f, 100f, 100f, 100f, 10f));
            Assert.IsFalse(TouchMath.InCircle(120f, 100f, 100f, 100f, 10f));
        }

        [Test]
        public void PinchZoom_SpreadingZoomsIn_PinchingZoomsOut()
        {
            Assert.AreEqual(1f, TouchMath.PinchZoom(200f, 300f, 100f), 1e-4f);
            Assert.AreEqual(-0.5f, TouchMath.PinchZoom(300f, 250f, 100f), 1e-4f);
            Assert.AreEqual(0f, TouchMath.PinchZoom(300f, 250f, 0f));
        }
    }
}
