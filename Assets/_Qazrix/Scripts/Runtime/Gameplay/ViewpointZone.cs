using Qazrix.Prototype;
using UnityEngine;

namespace Qazrix.Gameplay
{
    /// <summary>
    /// Atalaya: al subir a su azotea el jugador se sincroniza. Desde entonces reaparece aquí, en lo
    /// alto, y no abajo (como un <see cref="CheckpointZone"/>), y se marca la atalaya como descubierta.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ViewpointZone : MonoBehaviour
    {
        [SerializeField] private string label = "atalaya";
        [SerializeField] private Transform respawnPoint;

        public string Label => label;

        public bool IsSynchronized { get; private set; }

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
            IsSynchronized = true;
        }
    }
}
