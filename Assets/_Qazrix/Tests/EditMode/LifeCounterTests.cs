using NUnit.Framework;
using Qazrix.Simulation.Gameplay;

namespace Qazrix.Tests
{
    /// <summary>Tres vidas al empezar: la que se juega y dos extra.</summary>
    public sealed class LifeCounterTests
    {
        [Test]
        public void StartsWithThreeLives_OnePlayedAndTwoExtra()
        {
            var lives = new LifeCounter();
            Assert.AreEqual(3, lives.Lives);
            Assert.AreEqual(2, lives.ExtraLives);
            Assert.IsFalse(lives.IsGameOver);
        }

        [Test]
        public void EachDeathSpendsOneLife()
        {
            var lives = new LifeCounter();
            lives.Lose();
            Assert.AreEqual(2, lives.Lives);
            Assert.AreEqual(1, lives.ExtraLives);
            lives.Lose();
            Assert.AreEqual(1, lives.Lives);
            Assert.AreEqual(0, lives.ExtraLives, "la última vida es la que se juega: no queda ninguna de reserva");
            Assert.IsFalse(lives.IsGameOver);
        }

        [Test]
        public void TheThirdDeathEndsTheGame()
        {
            var lives = new LifeCounter();
            lives.Lose(); lives.Lose(); lives.Lose();
            Assert.AreEqual(0, lives.Lives);
            Assert.IsTrue(lives.IsGameOver);
        }

        [Test]
        public void LivesNeverGoBelowZero()
        {
            var lives = new LifeCounter();
            for (int i = 0; i < 10; i++) lives.Lose();
            Assert.AreEqual(0, lives.Lives);
            Assert.AreEqual(0, lives.ExtraLives);
        }

        [Test]
        public void ResetGivesBackTheStartingLives()
        {
            var lives = new LifeCounter();
            lives.Lose(); lives.Lose(); lives.Lose();
            lives.Reset();
            Assert.AreEqual(LifeCounter.StartLives, lives.Lives);
            Assert.IsFalse(lives.IsGameOver);
        }
    }
}
