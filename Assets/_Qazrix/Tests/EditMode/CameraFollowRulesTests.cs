using NUnit.Framework;
using Qazrix.Simulation.Movement;

namespace Qazrix.Tests
{
    /// <summary>Cuándo la cámara se coloca detrás de donde mira el personaje.</summary>
    public sealed class CameraFollowRulesTests
    {
        private const float Hold = 1.5f;

        private static bool Follow(float forward = 0f, float tankTurn = 0f, float tankForward = 0f,
            float sinceManual = 99f, bool enabled = true, bool blocked = false)
        {
            return CameraFollowRules.ShouldFollow(enabled, blocked, sinceManual, Hold, forward, tankTurn, tankForward);
        }

        [Test]
        public void Standing_DoesNotMoveTheCamera()
        {
            Assert.IsFalse(Follow());
        }

        [Test]
        public void WASD_Advancing_Follows_ButSidewaysAndBackwardsDoNot()
        {
            Assert.IsTrue(Follow(forward: 1f), "avanzando, la cámara se coloca detrás");
            Assert.IsTrue(Follow(forward: 0.5f));
            Assert.IsFalse(Follow(forward: 0f), "solo de lado: sin componente hacia delante, no gira tras el personaje");
            Assert.IsFalse(Follow(forward: -1f), "hacia atrás tampoco");
            Assert.IsFalse(Follow(forward: CameraFollowRules.Deadzone * 0.5f), "una palanca casi centrada no cuenta");
        }

        [Test]
        public void Arrows_AlwaysFollow_WhenTurningAdvancingOrRetreating()
        {
            Assert.IsTrue(Follow(tankTurn: 1f), "girando con las flechas");
            Assert.IsTrue(Follow(tankTurn: -1f));
            Assert.IsTrue(Follow(tankForward: 1f), "avanzando");
            Assert.IsTrue(Follow(tankForward: -1f), "y retrocediendo");
        }

        [Test]
        public void ManualLook_IsRespectedForAWhile()
        {
            Assert.IsFalse(Follow(forward: 1f, sinceManual: 0.2f), "acaba de mover la cámara a mano");
            Assert.IsFalse(Follow(tankTurn: 1f, sinceManual: Hold - 0.01f));
            Assert.IsTrue(Follow(forward: 1f, sinceManual: Hold + 0.01f), "pasado el tiempo, vuelve a seguir");
        }

        [Test]
        public void TurnAround_StartsOnlyWhenBackIsPressed_NotWhileItIsHeld()
        {
            Assert.IsTrue(CameraFollowRules.ShouldStartTurnAround(true, false, 99f, Hold, -1f, false, false), "al pulsar atrás");
            Assert.IsFalse(CameraFollowRules.ShouldStartTurnAround(true, false, 99f, Hold, -1f, true, false), "si ya estaba pulsado, no repite");
            Assert.IsFalse(CameraFollowRules.ShouldStartTurnAround(true, false, 99f, Hold, -1f, false, true), "ni mientras da la vuelta");
            Assert.IsFalse(CameraFollowRules.ShouldStartTurnAround(true, false, 99f, Hold, 0f, false, false), "sin pulsar nada");
            Assert.IsFalse(CameraFollowRules.ShouldStartTurnAround(true, false, 99f, Hold, 1f, false, false), "hacia delante tampoco");
            Assert.IsFalse(CameraFollowRules.ShouldStartTurnAround(true, false, 99f, Hold, -CameraFollowRules.Deadzone * 0.5f, false, false), "zona muerta");
        }

        [Test]
        public void TurnAround_IsOff_WhenDisabled_Blocked_OrRightAfterManualLook()
        {
            Assert.IsFalse(CameraFollowRules.ShouldStartTurnAround(false, false, 99f, Hold, -1f, false, false));
            Assert.IsFalse(CameraFollowRules.ShouldStartTurnAround(true, true, 99f, Hold, -1f, false, false));
            Assert.IsFalse(CameraFollowRules.ShouldStartTurnAround(true, false, 0.3f, Hold, -1f, false, false), "acaba de mover la cámara a mano");
            Assert.IsTrue(CameraFollowRules.ShouldStartTurnAround(true, false, Hold + 0.01f, Hold, -1f, false, false));
        }

        [Test]
        public void MovementYaw_KeepsTheOldHeading_WhileTheTurnIsLatched()
        {
            Assert.AreEqual(30f, CameraFollowRules.MovementYaw(30f, 200f, false), "sin latch, la de la cámara");
            Assert.AreEqual(200f, CameraFollowRules.MovementYaw(30f, 200f, true), "con latch, la de antes de la vuelta");
        }

        [Test]
        public void TurnAroundStep_ReachesExactly180_AtAConstantSlowRate_WithoutOvershooting()
        {
            float yaw = 10f;
            const float target = 190f;
            float elapsed = 0f;
            while (yaw != target && elapsed < 10f)
            {
                float next = CameraFollowRules.TurnAroundStep(yaw, target, 90f, 1f / 60f);
                Assert.LessOrEqual(next, target, "sin pasarse");
                Assert.GreaterOrEqual(next, yaw, "siempre hacia el destino");
                yaw = next;
                elapsed += 1f / 60f;
            }
            Assert.AreEqual(target, yaw, 0f, "acaba justo en el destino y se queda");
            Assert.AreEqual(2f, elapsed, 0.05f, "180° a 90°/s tardan unos 2 s: despacio");
            Assert.AreEqual(target, CameraFollowRules.TurnAroundStep(yaw, target, 90f, 1f), "ya en el destino, no se mueve");
            Assert.AreEqual(50f, CameraFollowRules.TurnAroundStep(100f, 0f, 50f, 1f), 1e-5f, "también hacia ángulos menores");
        }

        [Test]
        public void Disabled_OrBlockedByAPanel_NeverFollows()
        {
            Assert.IsFalse(Follow(forward: 1f, enabled: false));
            Assert.IsFalse(Follow(tankTurn: 1f, blocked: true));
        }
    }
}
