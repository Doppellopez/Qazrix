using NUnit.Framework;
using Qazrix.Simulation.Movement;

namespace Qazrix.Tests
{
    /// <summary>Zahra le da la llave: ella la ofrece, él la toma, se la lleva al pecho y la guarda en la camisa.</summary>
    public sealed class KeyHandoverCycleTests
    {
        private const float Step = 1f / 60f;

        [Test]
        public void BothArmsStartAndEndAtRest()
        {
            Assert.AreEqual(0f, KeyHandoverCycle.ZahraArm(0f), 1e-4f);
            Assert.AreEqual(0f, KeyHandoverCycle.HeroArm(0f), 1e-4f);
            Assert.AreEqual(0f, KeyHandoverCycle.HeroElbow(0f), 1e-4f);
            float end = KeyHandoverCycle.TotalSeconds;
            Assert.AreEqual(0f, KeyHandoverCycle.ZahraArm(end), 1e-4f);
            Assert.AreEqual(0f, KeyHandoverCycle.HeroArm(end), 1e-4f);
            Assert.AreEqual(0f, KeyHandoverCycle.HeroElbow(end), 1e-4f);
        }

        [Test]
        public void ZahraOffersTheKeyBeforeTheHeroReachesForIt()
        {
            Assert.AreEqual(1f, KeyHandoverCycle.ZahraOffer(KeyHandoverCycle.OfferUpUntil), 1e-4f, "ya tiene el brazo extendido");
            Assert.Less(KeyHandoverCycle.HeroArm(KeyHandoverCycle.ReachFrom), 1f, "él aún no ha movido el brazo");
            Assert.Greater(KeyHandoverCycle.ZahraOffer(KeyHandoverCycle.ReachFrom), 0.9f, "cuando él empieza a alargar la mano, ella ya ofrece");
        }

        [Test]
        public void HeroReachesOutThenBringsTheHandToHisChest()
        {
            float reachArm = KeyHandoverCycle.HeroArm(KeyHandoverCycle.ReachUntil);
            Assert.AreEqual(KeyHandoverCycle.ReachArmDegrees, reachArm, 0.5f, "la mano alargada hacia la llave");
            Assert.AreEqual(KeyHandoverCycle.ReachElbowDegrees, KeyHandoverCycle.HeroElbow(KeyHandoverCycle.ReachUntil), 0.5f);

            float chestElbow = KeyHandoverCycle.HeroElbow(KeyHandoverCycle.ChestUntil);
            Assert.AreEqual(KeyHandoverCycle.ChestElbowDegrees, chestElbow, 0.5f, "el codo se dobla para llevar la mano al pecho");
            Assert.AreEqual(KeyHandoverCycle.ChestArmDegrees, KeyHandoverCycle.HeroArm(KeyHandoverCycle.ChestUntil), 0.5f);
        }

        [Test]
        public void TheKeyTravelsFromHerHandToHisAndIsInHisBeforeHeMovesItToHisChest()
        {
            Assert.AreEqual(0f, KeyHandoverCycle.KeyBlend(KeyHandoverCycle.TravelFrom - 0.01f), 1e-4f, "en la mano de ella");
            Assert.AreEqual(1f, KeyHandoverCycle.KeyBlend(KeyHandoverCycle.TravelFrom + KeyHandoverCycle.TravelSeconds), 1e-4f, "en la de él");
            Assert.LessOrEqual(KeyHandoverCycle.TravelFrom + KeyHandoverCycle.TravelSeconds, KeyHandoverCycle.ChestFrom + 1e-4f,
                "la lleva él cuando empieza a llevársela al pecho");
            float mid = KeyHandoverCycle.KeyBlend(KeyHandoverCycle.TravelFrom + KeyHandoverCycle.TravelSeconds * 0.5f);
            Assert.That(mid, Is.InRange(0.4f, 0.6f));
        }

        [Test]
        public void TheKeyVanishesInsideTheShirtOnlyAfterReachingTheChest()
        {
            Assert.IsTrue(KeyHandoverCycle.KeyVisible(KeyHandoverCycle.ChestUntil));
            Assert.Greater(KeyHandoverCycle.StashAt, KeyHandoverCycle.ChestUntil, "primero llega al pecho");
            Assert.IsFalse(KeyHandoverCycle.KeyVisible(KeyHandoverCycle.StashAt));
            Assert.IsTrue(KeyHandoverCycle.HasStashed(KeyHandoverCycle.StashAt));
            Assert.IsFalse(KeyHandoverCycle.HasStashed(KeyHandoverCycle.StashAt - 0.01f));
        }

        [Test]
        public void ZahraKeepsHerArmOutUntilTheKeyHasLeftHerHand()
        {
            float keyLeft = KeyHandoverCycle.TravelFrom + KeyHandoverCycle.TravelSeconds;
            Assert.AreEqual(1f, KeyHandoverCycle.ZahraOffer(keyLeft), 1e-4f);
            Assert.Less(KeyHandoverCycle.ZahraOffer(KeyHandoverCycle.OfferDownUntil), 1e-4f + 0.001f);
        }

        [Test]
        public void MotionIsSmooth_NoJumpsBetweenFrames()
        {
            float prevZ = 0f, prevArm = 0f, prevElbow = 0f, prevKey = 0f;
            for (float t = Step; t <= KeyHandoverCycle.TotalSeconds; t += Step)
            {
                float z = KeyHandoverCycle.ZahraArm(t), arm = KeyHandoverCycle.HeroArm(t), elbow = KeyHandoverCycle.HeroElbow(t), key = KeyHandoverCycle.KeyBlend(t);
                // Pendiente máxima de un suavizado: 1,5 × el ángulo total / el tiempo de la subida, por fotograma (unos 3,1° a 60 fps).
                Assert.Less(System.Math.Abs(z - prevZ), 4f, $"brazo de Zahra a t={t:0.00}");
                Assert.Less(System.Math.Abs(arm - prevArm), 4f, $"brazo del protagonista a t={t:0.00}");
                Assert.Less(System.Math.Abs(elbow - prevElbow), 6f, $"codo a t={t:0.00}"); // 1,5 × 115° / 0,5 s / 60 ≈ 5,75° por fotograma
                Assert.Less(System.Math.Abs(key - prevKey), 0.1f, $"llave a t={t:0.00}");
                prevZ = z; prevArm = arm; prevElbow = elbow; prevKey = key;
            }
        }

        [Test]
        public void ItEndsOnlyAfterEverythingIsBackAtRest()
        {
            Assert.IsFalse(KeyHandoverCycle.IsFinished(KeyHandoverCycle.StashAt));
            Assert.Greater(KeyHandoverCycle.TotalSeconds, KeyHandoverCycle.ReleaseUntil);
            Assert.IsTrue(KeyHandoverCycle.IsFinished(KeyHandoverCycle.TotalSeconds));
        }
    }
}
