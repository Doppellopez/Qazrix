using UnityEngine;

namespace Qazrix.Gameplay
{
    /// <summary>Objeto que se recoge una sola vez (la carta o el sello de la misión de demo, spec §4.2).</summary>
    public sealed class LetterPickup : Interactable
    {
        public bool Collected { get; private set; }

        public override bool CanInteract => base.CanInteract && !Collected;

        public override void Interact(PlayerInteractor actor)
        {
            if (Collected) return;
            Collected = true;
            actor.AddItem();

            // Desaparece de la escena pero el objeto sigue existiendo: su estado se podrá guardar.
            foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = false;
            foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        }
    }
}
