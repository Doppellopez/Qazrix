using Qazrix.Prototype;
using UnityEngine;

namespace Qazrix.Gameplay
{
    /// <summary>
    /// Un extremo del pasadizo: una trampilla o una escalera. Con la tecla de interactuar se hace un fundido a negro y el jugador
    /// aparece en el otro extremo, mirando hacia donde sigue el camino.
    /// </summary>
    public sealed class PassageStairs : Interactable
    {
        private Vector3 _destination;
        private Quaternion _facing = Quaternion.identity;
        private PassageStairs _other;

        /// <summary>Hacia dónde lleva (para pruebas).</summary>
        public Vector3 Destination => _destination;

        public void Configure(string prompt, Vector3 destination, Quaternion facing)
        {
            SetPrompt(prompt);
            _destination = destination;
            _facing = facing;
        }

        public override bool CanInteract => base.CanInteract && !ScreenFade.Instance.Busy;

        public override void Interact(PlayerInteractor actor)
        {
            PrototypePlayerController body = actor != null ? actor.GetComponent<PrototypePlayerController>() : null;
            if (body == null) return;
            ScreenFade.Run(() => body.TeleportTo(_destination, _facing));
        }
    }
}
