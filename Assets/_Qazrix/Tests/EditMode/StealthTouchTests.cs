using NUnit.Framework;
using Qazrix.Simulation.AI;

namespace Qazrix.Tests
{
    /// <summary>El guardia solo captura si le toca de verdad: cerca en horizontal y a la misma altura.</summary>
    public sealed class StealthTouchTests
    {
        private const float GuardHeight = 1.9f, PlayerHeight = 1.8f, Reach = 0.85f;

        [Test]
        public void SideBySide_OnTheSameFloor_IsATouch()
        {
            Assert.IsTrue(StealthRules.Touches(0.5f, 0f, GuardHeight, 0f, PlayerHeight, Reach));
            Assert.IsTrue(StealthRules.Touches(Reach, 0f, GuardHeight, 0f, PlayerHeight, Reach), "justo en el límite del alcance");
        }

        [Test]
        public void TooFarAway_IsNotATouch()
        {
            Assert.IsFalse(StealthRules.Touches(Reach + 0.01f, 0f, GuardHeight, 0f, PlayerHeight, Reach));
            Assert.IsFalse(StealthRules.Touches(3f, 0f, GuardHeight, 0f, PlayerHeight, Reach));
        }

        [Test]
        public void PassingOverTheGuard_IsNotATouch()
        {
            // Los pies del jugador por encima de la cabeza del guardia: pasa por encima (un tejado, un salto muy alto).
            Assert.IsFalse(StealthRules.Touches(0f, 0f, GuardHeight, 2.5f, PlayerHeight, Reach), "justo encima, a 2,5 m");
            Assert.IsFalse(StealthRules.Touches(0.2f, 0f, GuardHeight, GuardHeight, PlayerHeight, Reach), "con los pies sobre la cabeza");
        }

        [Test]
        public void BeingUnderTheGuard_IsNotATouch()
        {
            // El guardia pisa un tejado por encima del jugador: la cabeza del jugador queda bajo los pies del guardia.
            Assert.IsFalse(StealthRules.Touches(0f, 4f, GuardHeight, 0f, PlayerHeight, Reach), "el guardia está 4 m más arriba");
            Assert.IsFalse(StealthRules.Touches(0f, PlayerHeight, GuardHeight, 0f, PlayerHeight, Reach), "con los pies del guardia en la cabeza del jugador");
        }

        [Test]
        public void PartialOverlapInHeight_IsATouch()
        {
            // Un jugador que salta y todavía tiene los pies a 1 m (junto a un guardia de 1,9 m): se tocan.
            Assert.IsTrue(StealthRules.Touches(0.3f, 0f, GuardHeight, 1f, PlayerHeight, Reach));
            // Un guardia en un escalón de 1 m junto al jugador: también.
            Assert.IsTrue(StealthRules.Touches(0.3f, 1f, GuardHeight, 0f, PlayerHeight, Reach));
        }

        [Test]
        public void CrouchedPlayer_HasASmallerBody_ButStillTouchesOnTheSameFloor()
        {
            Assert.IsTrue(StealthRules.Touches(0.4f, 0f, GuardHeight, 0f, 1.0f, Reach));
        }
    }
}
