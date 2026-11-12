using Qazrix.Input;
using Qazrix.Simulation.Gameplay;
using UnityEngine;

namespace Qazrix.Gameplay
{
    /// <summary>
    /// Busca el objeto interactuable más adecuado junto al jugador y lo activa con la tecla de
    /// interactuar. La elección la hace <see cref="InteractionRules"/>; aquí solo se mide.
    /// </summary>
    public sealed class PlayerInteractor : MonoBehaviour
    {
        private const int MaxCandidates = 16;

        [SerializeField] private QazrixInput inputReader;
        [SerializeField] private float chestHeight = 0.9f;

        private readonly Collider[] _hits = new Collider[MaxCandidates];
        private readonly Interactable[] _candidates = new Interactable[MaxCandidates];
        private readonly float[] _distances = new float[MaxCandidates];
        private readonly float[] _facings = new float[MaxCandidates];
        private readonly bool[] _available = new bool[MaxCandidates];

        /// <summary>Objeto que se activaría ahora mismo; null si no hay ninguno a mano.</summary>
        public Interactable Current { get; private set; }

        /// <summary>Objetos recogidos en esta sesión (cartas, sellos…).</summary>
        public int ItemsCollected { get; private set; }

        public void Configure(QazrixInput reader)
        {
            inputReader = reader;
        }

        public void AddItem()
        {
            ItemsCollected++;
        }

        private void Update()
        {
            Current = FindBest();
            if (Current != null && inputReader != null && inputReader.Read().InteractPressed)
            {
                Interactable target = Current;
                Current = null;
                target.Interact(this);
            }
        }

        private Interactable FindBest()
        {
            Vector3 chest = transform.position + Vector3.up * chestHeight;
            int found = Physics.OverlapSphereNonAlloc(chest, InteractionRules.Range + 1f, _hits, ~0, QueryTriggerInteraction.Collide);

            int count = 0;
            for (int i = 0; i < found && count < MaxCandidates; i++)
            {
                var candidate = _hits[i].GetComponentInParent<Interactable>();
                if (candidate == null || System.Array.IndexOf(_candidates, candidate, 0, count) >= 0) continue;

                Vector3 to = _hits[i].ClosestPoint(chest) - chest;
                to.y = 0f;
                _candidates[count] = candidate;
                _distances[count] = to.magnitude;
                _facings[count] = to.sqrMagnitude > 1e-6f ? Vector3.Dot(transform.forward, to.normalized) : 1f;
                _available[count] = candidate.CanInteract;
                count++;
            }

            int best = InteractionRules.Pick(_distances, _facings, _available, count);
            return best >= 0 ? _candidates[best] : null;
        }
    }
}
