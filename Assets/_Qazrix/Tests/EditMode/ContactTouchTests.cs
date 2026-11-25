using NUnit.Framework;
using Qazrix.Simulation.Gameplay;

namespace Qazrix.Tests
{
    /// <summary>Tocar a la mujer del velo: pegado a ella y a su altura.</summary>
    public sealed class ContactTouchTests
    {
        [Test]
        public void RightNextToHerTouches()
        {
            Assert.IsTrue(ContactTouch.IsTouching(0.5f, 0f, 0.4f));
        }

        [Test]
        public void AtTheEdgeOfReachStillTouches()
        {
            Assert.IsTrue(ContactTouch.IsTouching(ContactTouch.Reach, 0f, 0f));
        }

        [Test]
        public void FarAwayDoesNotTouch()
        {
            Assert.IsFalse(ContactTouch.IsTouching(3f, 0f, 0f));
            Assert.IsFalse(ContactTouch.IsTouching(0.9f, 0f, 0.9f)); // en diagonal, 1,27 m
        }

        [Test]
        public void OnARoofAboveHerDoesNotTouch()
        {
            Assert.IsFalse(ContactTouch.IsTouching(0.2f, 6f, 0.2f));
            Assert.IsFalse(ContactTouch.IsTouching(0.2f, -6f, 0.2f));
        }
    }
}
