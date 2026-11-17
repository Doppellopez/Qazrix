using Qazrix.Prototype;
using UnityEngine;

namespace Qazrix.Gameplay
{
    /// <summary>
    /// Zona en el borde de una atalaya desde la que se puede hacer el salto de fe hacia un pajar.
    /// Mientras el jugador está dentro, su <see cref="PlayerFaithLeap"/> sabe adónde saltar.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class FaithLeapZone : MonoBehaviour
    {
        [SerializeField] private HayLanding target;

        public HayLanding Target => target;

        public void Configure(HayLanding hay)
        {
            target = hay;
        }

        private void OnTriggerEnter(Collider other)
        {
            var leap = other.GetComponent<PlayerFaithLeap>();
            if (leap != null) leap.Enter(this);
        }

        private void OnTriggerExit(Collider other)
        {
            var leap = other.GetComponent<PlayerFaithLeap>();
            if (leap != null) leap.Exit(this);
        }
    }
}
