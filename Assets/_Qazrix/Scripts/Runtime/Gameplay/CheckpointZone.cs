using Qazrix.Prototype;
using UnityEngine;

namespace Qazrix.Gameplay
{
    /// <summary>
    /// Zona que, al entrar el jugador, pasa a ser su punto de reaparición (spec §6.5). Desde entonces,
    /// caer fuera del mundo o ser alcanzado por un guardia le devuelve aquí y no al inicio.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CheckpointZone : MonoBehaviour
    {
        [SerializeField] private string label = "Checkpoint";
        [SerializeField] private Transform respawnPoint;

        public string Label => label;

        public void Configure(string name, Transform point)
        {
            label = name;
            respawnPoint = point;
        }

        private void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponent<PrototypePlayerController>();
            if (player == null) return;

            Transform point = respawnPoint != null ? respawnPoint : transform;
            player.SetCheckpoint(point.position, point.rotation, label);
        }
    }
}
