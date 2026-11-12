using Qazrix.AI;
using Qazrix.Simulation.AI;
using UnityEngine;

namespace Qazrix.Prototype
{
    /// <summary>Traduce el estado del jugador del prototipo a lo que perciben los guardias.</summary>
    [RequireComponent(typeof(PrototypePlayerController))]
    public sealed class PlayerStealthTarget : StealthTarget
    {
        /// <summary>Segundos durante los que el golpe de una caída sigue contando como ruido.</summary>
        private const float LandingNoiseSeconds = 0.3f;

        private PrototypePlayerController _player;

        /// <summary>Veces que un guardia le ha alcanzado en esta sesión.</summary>
        public int TimesCaught { get; private set; }

        private PrototypePlayerController Player => _player != null ? _player : (_player = GetComponent<PrototypePlayerController>());

        public override Vector3 AimPoint => transform.position + Vector3.up * (Player.CurrentHeight * 0.6f);

        public override float Exposure => StealthRules.Exposure(Player.IsCrouching);

        public override float NoiseRadius => StealthRules.NoiseRadius(
            Player.HorizontalSpeed, Player.IsCrouching, Player.IsRunning,
            Time.time - Player.LastLandingTime < LandingNoiseSeconds);

        public override void Caught()
        {
            TimesCaught++;
            Player.Respawn(); // por ahora ser alcanzado devuelve al punto de salida (checkpoint único)
        }
    }
}
