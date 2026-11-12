using UnityEngine;

namespace Qazrix.Gameplay
{
    /// <summary>
    /// Base común de todo lo que el jugador puede activar con la tecla de interactuar (spec §6.4).
    /// Necesita un colisionador (puede ser de tipo disparador) para que el jugador lo encuentre.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public abstract class Interactable : MonoBehaviour
    {
        [SerializeField] private string prompt = "Interactuar";

        /// <summary>Texto que se muestra al jugador, por ejemplo «Recoger la carta».</summary>
        public string Prompt => prompt;

        public virtual bool CanInteract => isActiveAndEnabled;

        public void SetPrompt(string text)
        {
            prompt = text;
        }

        public abstract void Interact(PlayerInteractor actor);
    }
}
