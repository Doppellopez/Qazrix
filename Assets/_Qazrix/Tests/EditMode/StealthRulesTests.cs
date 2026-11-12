using NUnit.Framework;
using Qazrix.Simulation.AI;

namespace Qazrix.Tests
{
    /// <summary>Casos de detección de spec §6.2: frontal, lateral, tras obstáculo, a cubierto y por sonido.</summary>
    public sealed class StealthRulesTests
    {
        private static readonly VisionConfig Config = new VisionConfig();
        private static readonly Float3 Eye = new Float3(0f, 1.6f, 0f);
        private static readonly Float3 North = new Float3(0f, 0f, 1f);

        private static float See(float x, float z, bool lineOfSight = true, float exposure = 1f)
        {
            return VisionCone.Visibility(Eye, North, new Float3(x, 1.6f, z), lineOfSight, exposure, Config);
        }

        [Test]
        public void Frontal_IsSeen_AndFadesWithDistance()
        {
            float near = See(0f, 3f);
            float far = See(0f, 13f);
            Assert.Greater(near, 0.8f);
            Assert.Greater(far, 0f);
            Assert.Less(far, near, "más lejos se ve peor");
        }

        [Test]
        public void BeyondRange_IsNotSeen()
        {
            Assert.AreEqual(0f, See(0f, Config.Range + 0.5f));
        }

        [Test]
        public void Lateral_OutsideTheCone_IsNotSeen()
        {
            Assert.AreEqual(0f, See(6f, 0f), "a 90° a la derecha");
            Assert.AreEqual(0f, See(0f, -6f), "a la espalda");
        }

        [Test]
        public void EdgeOfTheCone_IsSeenWorseThanTheCentre()
        {
            // Mismo alcance (6 m), uno de frente y otro a unos 45° (dentro del cono de 50°).
            float centre = See(0f, 6f);
            float edge = See(4.243f, 4.243f);
            Assert.Greater(edge, 0f);
            Assert.Less(edge, centre);
        }

        [Test]
        public void BehindAnObstacle_IsNeverSeen()
        {
            Assert.AreEqual(0f, See(0f, 3f, lineOfSight: false), "ni de frente y cerca");
            Assert.AreEqual(0f, See(0f, 0.5f, lineOfSight: false), "ni pegado, si hay pared en medio");
        }

        [Test]
        public void Crouching_HalvesExposure()
        {
            Assert.AreEqual(1f, StealthRules.Exposure(crouching: false));
            Assert.AreEqual(StealthRules.CrouchExposure, StealthRules.Exposure(crouching: true));

            float standing = See(0f, 6f, exposure: StealthRules.Exposure(false));
            float crouching = See(0f, 6f, exposure: StealthRules.Exposure(true));
            Assert.AreEqual(standing * StealthRules.CrouchExposure, crouching, 1e-5f);
        }

        [Test]
        public void VeryClose_IsNoticedEvenFromBehind()
        {
            Assert.Greater(See(0f, -1f), 0f, "a un metro por la espalda lo nota");
            Assert.AreEqual(0f, See(0f, -2f), "a dos metros por la espalda, no");
        }

        [Test]
        public void HeightDifference_DoesNotCloseTheCone()
        {
            float sameLevel = VisionCone.Visibility(Eye, North, new Float3(0f, 1.6f, 6f), true, 1f, Config);
            float above = VisionCone.Visibility(Eye, North, new Float3(0f, 4.3f, 6f), true, 1f, Config);
            Assert.Greater(above, 0f, "alguien en una plataforma de frente sigue dentro del cono");
            Assert.LessOrEqual(above, sameLevel);
        }

        [Test]
        public void Noise_DependsOnWhatThePlayerDoes()
        {
            Assert.AreEqual(0f, StealthRules.NoiseRadius(0f, false, false, false), "quieto");
            Assert.AreEqual(0f, StealthRules.NoiseRadius(1.3f, true, false, false), "andando agachado");
            Assert.AreEqual(StealthRules.WalkNoiseRadius, StealthRules.NoiseRadius(2.2f, false, false, false), "andando");
            Assert.AreEqual(StealthRules.RunNoiseRadius, StealthRules.NoiseRadius(5.5f, false, true, false), "corriendo");
            Assert.AreEqual(StealthRules.LandingNoiseRadius, StealthRules.NoiseRadius(0f, true, false, true), "caer suena aunque vaya agachado");
        }

        [Test]
        public void Hearing_NeedsNoiseWithinRadius()
        {
            Assert.IsTrue(StealthRules.Hears(7.9f, StealthRules.RunNoiseRadius));
            Assert.IsFalse(StealthRules.Hears(8.1f, StealthRules.RunNoiseRadius));
            Assert.IsFalse(StealthRules.Hears(0.1f, 0f), "sin ruido no se oye nada, ni al lado");
        }

        [Test]
        public void GiveUp_SendsAnAlertedGuardBackToItsPost()
        {
            var machine = new AlertStateMachine();
            for (int i = 0; i < 4; i++) machine.Tick(0.25f, new GuardPercept(1f));
            Assert.AreEqual(AlertState.Alert, machine.State);

            machine.GiveUp();

            Assert.AreEqual(AlertState.Return, machine.State);
            Assert.AreEqual(0f, machine.Awareness);
        }
    }
}
