using NUnit.Framework;
using Qazrix.Simulation.Gameplay;

namespace Qazrix.Tests
{
    /// <summary>Fin de partida: el juego se oscurece, sale el mensaje y se vuelve a empezar.</summary>
    public sealed class GameOverScreenTests
    {
        [Test]
        public void StartsBrightAndEndsDark()
        {
            Assert.AreEqual(0f, GameOverScreen.Darkness(0f), 1e-6f);
            Assert.AreEqual(GameOverScreen.MaxDarkness, GameOverScreen.Darkness(GameOverScreen.FadeSeconds), 1e-6f);
            Assert.AreEqual(GameOverScreen.MaxDarkness, GameOverScreen.Darkness(100f), 1e-6f, "se queda oscuro");
        }

        [Test]
        public void DarknessOnlyGrows()
        {
            float previous = 0f;
            for (float t = 0f; t <= GameOverScreen.TotalSeconds; t += 0.02f)
            {
                float d = GameOverScreen.Darkness(t);
                Assert.GreaterOrEqual(d, previous - 1e-6f, $"a t={t:0.00}");
                Assert.LessOrEqual(d, 1f);
                previous = d;
            }
        }

        [Test]
        public void TheMessageAppearsAfterTheScreenStartsGoingDark()
        {
            Assert.AreEqual(0f, GameOverScreen.TextAlpha(0f), 1e-6f);
            Assert.AreEqual(0f, GameOverScreen.TextAlpha(GameOverScreen.TextStart), 1e-6f);
            Assert.Greater(GameOverScreen.Darkness(GameOverScreen.TextStart), 0.2f, "ya está bastante oscuro cuando sale el texto");
            Assert.AreEqual(1f, GameOverScreen.TextAlpha(GameOverScreen.TextStart + GameOverScreen.TextFadeSeconds), 1e-6f);
            Assert.AreEqual(1f, GameOverScreen.TextAlpha(100f), 1e-6f);
        }

        [Test]
        public void ItRestartsOnlyAfterTheMessageHasBeenShownForAWhile()
        {
            Assert.IsFalse(GameOverScreen.IsFinished(0f));
            Assert.IsFalse(GameOverScreen.IsFinished(GameOverScreen.TextStart + GameOverScreen.TextFadeSeconds));
            Assert.IsTrue(GameOverScreen.IsFinished(GameOverScreen.TotalSeconds));
            Assert.GreaterOrEqual(GameOverScreen.HoldSeconds, 3f, "da tiempo a leerlo");
        }

        [Test]
        public void TheMessageIsTheOneGiven()
        {
            Assert.AreEqual("Vidas acabadas, los musulmanes han ganado!", GameOverScreen.Message);
        }
    }
}
