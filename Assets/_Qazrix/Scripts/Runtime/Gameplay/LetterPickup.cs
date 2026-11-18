using UnityEngine;

namespace Qazrix.Gameplay
{
    /// <summary>
    /// Objeto que se recoge una sola vez (la carta o el sello de la misión de demo, spec §4.2). Se recoge con la
    /// tecla de interactuar o parándose encima: el jugador se agacha a cogerla y, a mitad del gesto, desaparece
    /// de la escena y pasa al inventario.
    /// </summary>
    public sealed class LetterPickup : Interactable
    {
        [SerializeField] private bool hasMark;
        [SerializeField] private Vector3 markPoint;

        /// <summary>¿Señala un punto del mundo? Al recogerla, ese punto se podrá ver en el mapa desde el inventario.</summary>
        public bool HasMark => hasMark;

        /// <summary>El punto que señala, en metros locales (solo X y Z importan en el mapa).</summary>
        public Vector3 MarkPoint => markPoint;

        public void SetMark(Vector3 point)
        {
            hasMark = true;
            markPoint = point;
        }

        public bool Collected { get; private set; }

        /// <summary>El jugador se está agachando a cogerla ahora mismo.</summary>
        public bool PickingUp { get; internal set; }

        public override bool CanInteract => base.CanInteract && !Collected && !PickingUp;

        public override void Interact(PlayerInteractor actor)
        {
            if (Collected || PickingUp) return;
            actor.BeginPickup(this);
        }

        /// <summary>La mano llega a la carta: desaparece de la escena y va al inventario de quien la coge.</summary>
        internal void Collect(PlayerInteractor actor)
        {
            if (Collected) return;
            Collected = true;
            PickingUp = false;
            actor.AddLetter(this);

            // Desaparece de la escena pero el objeto sigue existiendo: su estado se podrá guardar.
            foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = false;
            foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        }
    }
}
