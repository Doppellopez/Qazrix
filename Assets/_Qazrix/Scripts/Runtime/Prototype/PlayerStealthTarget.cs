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

        public override float BodyHeight => Player.CurrentHeight;

        public override float Exposure => Player.IsDead ? 0f : StealthRules.Exposure(Player.IsCrouching);

        public override float NoiseRadius => Player.IsDead ? 0f : StealthRules.NoiseRadius(
            Player.HorizontalSpeed, Player.IsCrouching, Player.IsRunning,
            Time.time - Player.LastLandingTime < LandingNoiseSeconds);

        public override bool IsDown => Player.IsDead;

        public override void Caught() => Caught(transform.position - transform.forward);

        public override void Caught(Vector3 attackerPosition)
        {
            if (Player.IsDead) return; // ya está en el suelo
            TimesCaught++;
            // Cae a cuchilladas y, tras quedar tumbado un momento, reaparece en el punto de salida (checkpoint único).
            Player.Die(attackerPosition);
        }
    }
}
