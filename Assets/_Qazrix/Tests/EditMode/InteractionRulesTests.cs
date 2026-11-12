using NUnit.Framework;
using Qazrix.Simulation.Gameplay;

namespace Qazrix.Tests
{
    /// <summary>Elección del objeto con el que se interactúa (spec §6.4).</summary>
    public sealed class InteractionRulesTests
    {
        private static int Pick(float[] distances, float[] facings, bool[] available)
        {
            return InteractionRules.Pick(distances, facings, available, distances.Length);
        }

        [Test]
        public void NothingNearby_PicksNothing()
        {
            Assert.AreEqual(-1, InteractionRules.Pick(new float[4], new float[4], new bool[4], 0));
            Assert.AreEqual(-1, Pick(new[] { InteractionRules.Range + 0.1f }, new[] { 1f }, new[] { true }), "fuera de alcance");
        }

        [Test]
        public void NearestInFront_Wins()
        {
            Assert.AreEqual(1, Pick(new[] { 1.5f, 0.9f, 1.2f }, new[] { 1f, 0.8f, 1f }, new[] { true, true, true }));
        }

        [Test]
        public void BehindThePlayer_IsIgnored_UnlessTouching()
        {
            Assert.AreEqual(-1, Pick(new[] { 1.2f }, new[] { -0.9f }, new[] { true }), "a la espalda no se activa");
            Assert.AreEqual(0, Pick(new[] { 0.4f }, new[] { -0.9f }, new[] { true }), "pegado a él, la mirada no importa");
        }

        [Test]
        public void UnavailableObjects_AreSkipped()
        {
            // El más cercano ya está recogido: se elige el siguiente.
            Assert.AreEqual(1, Pick(new[] { 0.5f, 1.4f }, new[] { 1f, 1f }, new[] { false, true }));
        }

        [Test]
        public void OnlyTheFirstCountEntries_AreConsidered()
        {
            // Los arrays son búferes reutilizados: lo que queda más allá de «count» es basura vieja.
            Assert.AreEqual(-1, InteractionRules.Pick(new[] { 5f, 0.5f }, new[] { 1f, 1f }, new[] { true, true }, 1));
        }
    }
}
