using NUnit.Framework;
using Qazrix.Simulation.Gameplay;
using Qazrix.Simulation.Movement;

namespace Qazrix.Tests
{
    /// <summary>El protagonista le da la llave al capitán: la saca de la camisa, él la toma y se la guarda.</summary>
    public sealed class KeyGiveCycleTests
    {
        private const float Step = 1f / 60f;

        [Test]
        public void BothArmsStartAndEndAtRest()
        {
            Assert.AreEqual(0f, KeyGiveCycle.HeroArm(0f), 1e-4f);
            Assert.AreEqual(0f, KeyGiveCycle.HeroElbow(0f), 1e-4f);
            Assert.AreEqual(0f, KeyGiveCycle.CaptainArm(0f), 1e-4f);
            float end = KeyGiveCycle.TotalSeconds;
            Assert.AreEqual(0f, KeyGiveCycle.HeroArm(end), 1e-4f);
            Assert.AreEqual(0f, KeyGiveCycle.HeroElbow(end), 1e-4f);
            Assert.AreEqual(0f, KeyGiveCycle.CaptainArm(end), 1e-4f);
        }

        [Test]
        public void HeroDrawsTheKeyFromHisChestFirstThenReachesOut()
        {
            Assert.AreEqual(KeyHandoverCycle.ChestElbowDegrees, KeyGiveCycle.HeroElbow(KeyGiveCycle.ChestUntil), 0.5f, "el codo muy doblado en el pecho");
            Assert.AreEqual(KeyHandoverCycle.ReachArmDegrees, KeyGiveCycle.HeroArm(KeyGiveCycle.ReachUntil), 0.5f, "después alarga el brazo");
            Assert.AreEqual(KeyHandoverCycle.ReachElbowDegrees, KeyGiveCycle.HeroElbow(KeyGiveCycle.ReachUntil), 0.5f);
            Assert.AreEqual(KeyGiveCycle.ChestUntil, KeyGiveCycle.DrawAt, 1e-6f, "la llave sale justo al llegar al pecho");
        }

        [Test]
        public void TheKeyAppearsOnlyAfterItIsDrawnAndVanishesWhenTheCaptainStashesIt()
        {
            Assert.IsFalse(KeyGiveCycle.KeyVisible(0.1f), "aún está en la camisa");
            Assert.IsTrue(KeyGiveCycle.KeyVisible(KeyGiveCycle.DrawAt + 0.01f));
            Assert.IsTrue(KeyGiveCycle.KeyVisible(KeyGiveCycle.StashAt - 0.01f));
            Assert.IsFalse(KeyGiveCycle.KeyVisible(KeyGiveCycle.StashAt), "guardada en el cinto");
        }

        [Test]
        public void TheCaptainHoldsOutHisHandBeforeTheKeyTravelsAndKeepsItUntilItArrives()
        {
            Assert.AreEqual(1f, KeyGiveCycle.CaptainOffer(KeyGiveCycle.TravelFrom), 1e-4f, "con la mano ya extendida cuando sale la llave");
            Assert.AreEqual(1f, KeyGiveCycle.CaptainOffer(KeyGiveCycle.TravelFrom + KeyGiveCycle.TravelSeconds), 1e-4f);
            Assert.AreEqual(0f, KeyGiveCycle.KeyBlend(KeyGiveCycle.TravelFrom), 1e-4f, "sale de la mano de él");
            Assert.AreEqual(1f, KeyGiveCycle.KeyBlend(KeyGiveCycle.TravelFrom + KeyGiveCycle.TravelSeconds), 1e-4f, "llega a la del capitán");
        }

        [Test]
        public void TheKeyLeavesTheHeroOnlyWhenItReachesTheCaptain()
        {
            Assert.IsFalse(KeyGiveCycle.HasHandedOver(KeyGiveCycle.TravelFrom + 0.01f));
            Assert.IsTrue(KeyGiveCycle.HasHandedOver(KeyGiveCycle.TravelFrom + KeyGiveCycle.TravelSeconds));
            Assert.Less(KeyGiveCycle.TravelFrom + KeyGiveCycle.TravelSeconds, KeyGiveCycle.StashAt, "primero la toma y luego se la guarda");
        }

        [Test]
        public void MotionIsSmooth_NoJumpsBetweenFrames()
        {
            float prevArm = 0f, prevElbow = 0f, prevCaptain = 0f, prevKey = 0f;
            for (float t = Step; t <= KeyGiveCycle.TotalSeconds; t += Step)
            {
                float arm = KeyGiveCycle.HeroArm(t), elbow = KeyGiveCycle.HeroElbow(t), cap = KeyGiveCycle.CaptainArm(t), key = KeyGiveCycle.KeyBlend(t);
                Assert.Less(System.Math.Abs(arm - prevArm), 4f, $"brazo a t={t:0.00}");
                Assert.Less(System.Math.Abs(elbow - prevElbow), 8f, $"codo a t={t:0.00}"); // pendiente máxima: 1,5 × 140° / 0,5 s / 60 ≈ 7° por fotograma
                Assert.Less(System.Math.Abs(cap - prevCaptain), 4f, $"capitán a t={t:0.00}");
                Assert.Less(System.Math.Abs(key - prevKey), 0.1f, $"llave a t={t:0.00}");
                prevArm = arm; prevElbow = elbow; prevCaptain = cap; prevKey = key;
            }
        }

        [Test]
        public void ItEndsOnlyAfterEverythingIsBackAtRest()
        {
            Assert.IsFalse(KeyGiveCycle.IsFinished(KeyGiveCycle.StashAt));
            Assert.Greater(KeyGiveCycle.TotalSeconds, KeyGiveCycle.ReleaseUntil);
            Assert.IsTrue(KeyGiveCycle.IsFinished(KeyGiveCycle.TotalSeconds));
        }

        [Test]
        public void InventoryRemoveGivesTheKeyAway()
        {
            var inventory = new Inventory();
            inventory.Add("llave", "Llave", 1, true, 10f, 20f);
            inventory.Add("carta", "Carta");
            Assert.IsTrue(inventory.Remove("llave"));
            Assert.AreEqual(0, inventory.CountOf("llave"));
            Assert.AreEqual(1, inventory.CountOf("carta"), "lo demás se queda");
            Assert.IsFalse(inventory.Remove("llave"), "ya no la tiene");
        }

        [Test]
        public void InventoryRemoveKeepsTheRestOfAStack()
        {
            var inventory = new Inventory();
            inventory.Add("sello", "Sello", 3);
            Assert.IsFalse(inventory.Remove("sello", 5), "no hay tantos: no quita nada");
            Assert.AreEqual(3, inventory.CountOf("sello"));
            Assert.IsTrue(inventory.Remove("sello", 2));
            Assert.AreEqual(1, inventory.CountOf("sello"));
            Assert.IsFalse(inventory.Remove("", 1));
            Assert.IsFalse(inventory.Remove("sello", 0));
        }
    }
}
