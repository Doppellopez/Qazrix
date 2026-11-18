using Qazrix.Input;
using Qazrix.Prototype;
using Qazrix.Simulation.Gameplay;
using Qazrix.Simulation.Movement;
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

        /// <summary>Lo que lleva el jugador. Empieza vacío.</summary>
        public Inventory Inventory { get; } = new Inventory();

        public void AddItem()
        {
            ItemsCollected++;
            Inventory.Add("carta", "Carta");
        }

        /// <summary>Guarda una carta en el inventario, con el punto del mapa que señale.</summary>
        public void AddLetter(LetterPickup letter)
        {
            ItemsCollected++;
            Vector3 mark = letter.MarkPoint;
            Inventory.Add("carta", "Carta", 1, letter.HasMark, mark.x, mark.z);
        }

        // Recoger una carta: agacharse (animación), cogerla a mitad del gesto y levantarse.
        private const float AutoPickupDistance = 0.9f;
        private const float AutoPickupStillSeconds = 0.2f;
        private const float StillSpeed = 0.25f;

        private LetterPickup _pickupLetter;
        private float _pickupSeconds;
        private bool _pickupTaken;
        private float _stillSeconds;
        private PrototypePlayerController _body;
        private CharacterWalkAnimator _animator;

        /// <summary>¿Se está agachando a recoger algo?</summary>
        public bool IsPickingUp => _pickupLetter != null;

        /// <summary>Empieza a agacharse a recoger la carta. El juego queda parado hasta que se levanta.</summary>
        public void BeginPickup(LetterPickup letter)
        {
            if (_pickupLetter != null || letter == null || letter.Collected || letter.PickingUp) return;
            _pickupLetter = letter;
            letter.PickingUp = true;
            _pickupSeconds = 0f;
            _pickupTaken = false;
            Current = null;

            // Mira hacia la carta para agacharse hacia ella.
            Vector3 to = letter.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.04f) transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);

            if (_animator == null) _animator = GetComponentInChildren<CharacterWalkAnimator>();
            if (_animator != null) _animator.PlayPickup();
            if (inputReader != null) inputReader.GameplayBlocked = true; // sin moverse mientras se agacha
        }

        private void TickPickup()
        {
            _pickupSeconds += Time.deltaTime;
            if (!_pickupTaken && PickupCycle.HasCollected(_pickupSeconds)) TakeLetter();
            if (!PickupCycle.IsFinished(_pickupSeconds)) return;

            if (!_pickupTaken) TakeLetter();
            _pickupLetter = null;
            if (inputReader != null) inputReader.GameplayBlocked = false;
        }

        private void TakeLetter()
        {
            _pickupTaken = true;
            _pickupLetter.Collect(this);
        }

        /// <summary>Parado encima de una carta durante un momento: se agacha a recogerla sin pulsar nada.</summary>
        private void TryAutoPickup()
        {
            if (_body == null) _body = GetComponent<PrototypePlayerController>();
            var letter = Current as LetterPickup;
            bool still = _body != null && _body.IsGrounded && !_body.IsCrouching && !_body.IsDead && _body.HorizontalSpeed < StillSpeed;
            if (letter == null || !still)
            {
                _stillSeconds = 0f;
                return;
            }

            Vector3 flat = letter.transform.position - transform.position;
            flat.y = 0f;
            bool onTop = flat.magnitude <= AutoPickupDistance && Mathf.Abs(letter.transform.position.y - transform.position.y) < 1.6f;
            _stillSeconds = onTop ? _stillSeconds + Time.deltaTime : 0f;
            if (_stillSeconds >= AutoPickupStillSeconds)
            {
                _stillSeconds = 0f;
                BeginPickup(letter);
            }
        }

        private void Update()
        {
            if (_pickupLetter != null)
            {
                TickPickup();
                return;
            }

            Current = FindBest();
            if (Current != null && inputReader != null && inputReader.Read().InteractPressed)
            {
                Interactable target = Current;
                Current = null;
                target.Interact(this);
                return;
            }
            TryAutoPickup();
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
