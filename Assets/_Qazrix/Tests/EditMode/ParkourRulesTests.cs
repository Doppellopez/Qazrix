using NUnit.Framework;
using Qazrix.Simulation.AI;
using Qazrix.Simulation.Movement;

namespace Qazrix.Tests
{
    /// <summary>Parkour contextual (spec §6.1): solo dentro de rangos medidos y con trayectoria predecible.</summary>
    public sealed class ParkourRulesTests
    {
        private static readonly ParkourLimits Limits = new ParkourLimits();

        [Test]
        public void Vault_AllowedOnLowNarrowObstacles()
        {
            Assert.AreEqual(ParkourAction.Vault, ParkourRules.Decide(ParkourKind.Vault, 0.5f, 0.4f, true, Limits));
            Assert.AreEqual(ParkourAction.Vault, ParkourRules.Decide(ParkourKind.Vault, Limits.MaxVaultHeight, Limits.MaxVaultDepth, true, Limits));
        }

        [Test]
        public void Vault_RefusedWhenTooHighOrTooDeep()
        {
            Assert.AreEqual(ParkourAction.None, ParkourRules.Decide(ParkourKind.Vault, 1.5f, 0.4f, true, Limits), "demasiado alto");
            Assert.AreEqual(ParkourAction.None, ParkourRules.Decide(ParkourKind.Vault, 0.5f, 3f, true, Limits), "demasiado ancho para pasarlo de un salto");
        }

        [Test]
        public void Climb_AllowedUpToTheLimit()
        {
            Assert.AreEqual(ParkourAction.Climb, ParkourRules.Decide(ParkourKind.Climb, 1.0f, 4f, true, Limits), "el grosor no importa al trepar");
            Assert.AreEqual(ParkourAction.Climb, ParkourRules.Decide(ParkourKind.Climb, Limits.MaxClimbHeight, 4f, true, Limits));
            Assert.AreEqual(ParkourAction.None, ParkourRules.Decide(ParkourKind.Climb, 2.7f, 4f, true, Limits), "el mirador (2,7 m) queda fuera de alcance");
        }

        [Test]
        public void NothingHappens_WithoutRoomToLand_OrOnATinyStep()
        {
            Assert.AreEqual(ParkourAction.None, ParkourRules.Decide(ParkourKind.Vault, 0.5f, 0.4f, false, Limits), "sin sitio al otro lado");
            Assert.AreEqual(ParkourAction.None, ParkourRules.Decide(ParkourKind.Climb, 1.0f, 4f, false, Limits), "sin sitio arriba");
            Assert.AreEqual(ParkourAction.None, ParkourRules.Decide(ParkourKind.Climb, 0.2f, 4f, true, Limits), "un escalón se sube andando");
        }

        [Test]
        public void MarkerKind_IsNeverUpgraded()
        {
            // Un obstáculo marcado solo para saltar no se puede trepar aunque la altura lo permitiera.
            Assert.AreEqual(ParkourAction.None, ParkourRules.Decide(ParkourKind.Vault, 2.0f, 0.4f, true, Limits));
        }

        [Test]
        public void Duration_GrowsWithHeight()
        {
            Assert.Greater(ParkourRules.Duration(ParkourAction.Climb, 2f), ParkourRules.Duration(ParkourAction.Climb, 1f));
            Assert.Greater(ParkourRules.Duration(ParkourAction.Vault, 0.5f), 0.3f);
            Assert.AreEqual(0f, ParkourRules.Duration(ParkourAction.None, 1f));
        }

        [Test]
        public void VaultPath_StartsAndEndsExactly_AndClearsTheObstacle()
        {
            var start = new Float3(0f, 0f, 4.3f);
            var end = new Float3(0f, 0f, 5.8f);
            const float top = 0.5f;

            Float3 a = ParkourPath.Evaluate(ParkourAction.Vault, start, end, top, 0f);
            Float3 mid = ParkourPath.Evaluate(ParkourAction.Vault, start, end, top, 0.5f);
            Float3 b = ParkourPath.Evaluate(ParkourAction.Vault, start, end, top, 1f);

            Assert.AreEqual(4.3f, a.Z, 1e-5f);
            Assert.AreEqual(0f, a.Y, 1e-5f);
            Assert.AreEqual(5.8f, b.Z, 1e-5f);
            Assert.AreEqual(0f, b.Y, 1e-5f);
            Assert.AreEqual(top + ParkourPath.VaultClearance, mid.Y, 1e-5f, "en el centro pasa por encima con margen");
            Assert.AreEqual(5.05f, mid.Z, 1e-5f);
        }

        [Test]
        public void ClimbPath_RisesFirst_ThenMovesOntoTheLedge()
        {
            var start = new Float3(-6f, 0f, 6.7f);
            var end = new Float3(-6f, 1f, 7.6f);

            Float3 rising = ParkourPath.Evaluate(ParkourAction.Climb, start, end, 1f, ParkourPath.ClimbRiseShare * 0.5f);
            Float3 atEdge = ParkourPath.Evaluate(ParkourAction.Climb, start, end, 1f, ParkourPath.ClimbRiseShare);
            Float3 done = ParkourPath.Evaluate(ParkourAction.Climb, start, end, 1f, 1f);

            Assert.AreEqual(6.7f, rising.Z, 1e-5f, "mientras sube no atraviesa la pared");
            Assert.Greater(rising.Y, 0f);
            Assert.Less(rising.Y, 1f);
            Assert.AreEqual(1f, atEdge.Y, 1e-5f, "ya está a la altura de la repisa antes de avanzar");
            Assert.AreEqual(6.7f, atEdge.Z, 1e-5f);
            Assert.AreEqual(7.6f, done.Z, 1e-5f);
            Assert.AreEqual(1f, done.Y, 1e-5f);
        }

        [Test]
        public void Path_ClampsTime()
        {
            var start = new Float3(0f, 0f, 0f);
            var end = new Float3(0f, 0f, 2f);
            Assert.AreEqual(0f, ParkourPath.Evaluate(ParkourAction.Vault, start, end, 0.5f, -1f).Z);
            Assert.AreEqual(2f, ParkourPath.Evaluate(ParkourAction.Vault, start, end, 0.5f, 5f).Z);
        }
    }
}
