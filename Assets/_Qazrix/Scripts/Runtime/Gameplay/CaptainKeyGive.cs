using Qazrix.Input;
using Qazrix.Prototype;
using Qazrix.Simulation.Gameplay;
using Qazrix.Simulation.Movement;
using UnityEngine;

namespace Qazrix.Gameplay
{
    /// <summary>
    /// El capitán de la guarnición de San Jorge recibe la llave: cuando el protagonista, con la llave encima, lo toca, se la saca de
    /// la camisa y se la da, y el capitán la toma con la mano y se la guarda en el cinto (<see cref="KeyGiveCycle"/>). La llave sale del
    /// inventario al llegar a la mano del capitán. Una sola vez.
    /// </summary>
    public sealed class CaptainKeyGive : MonoBehaviour
    {
        private Transform _arm, _hand;
        private Material _gold;
        private Quaternion _armRest;

        private PlayerInteractor _player;
        private CharacterWalkAnimator _hero;
        private QazrixInput _input;
        private Transform _key;
        private float _seconds = -1f;

        /// <summary>Ya se ha empezado a dar la llave (o está dada).</summary>
        public bool Started { get; private set; }

        /// <summary>La llave ya está en manos del capitán.</summary>
        public bool Delivered { get; private set; }

        public float Seconds => _seconds;

        public void Configure(Transform arm, Transform hand, Material gold)
        {
            _arm = arm;
            _hand = hand;
            _gold = gold;
            _armRest = arm.localRotation;
        }

        private void Update()
        {
            if (!Started) { WatchForTouch(); return; }
            if (_seconds < 0f) return;
            _seconds += Time.deltaTime;

            // Brazo del capitán: la mano abierta hacia el protagonista (giro negativo en X = hacia delante).
            _arm.localRotation = _armRest * Quaternion.Euler(-KeyGiveCycle.CaptainArm(_seconds), 0f, 0f);

            if (KeyGiveCycle.KeyVisible(_seconds))
            {
                if (_key == null) _key = KeyProp.Build(_gold, _hero.RightHand.position);
                // La llave sale de la camisa en la mano del protagonista, pasa a la mano del capitán y este la lleva hasta que se la guarda.
                Vector3 from = _hero.RightHand.position, to = _hand.position;
                _key.position = Vector3.Lerp(from, to, KeyGiveCycle.KeyBlend(_seconds));
                _key.rotation = Quaternion.LookRotation(_player.transform.forward + Vector3.up * 0.3f, Vector3.up);
            }
            else if (_key != null)
            {
                Destroy(_key.gameObject); // guardada en el cinto
                _key = null;
            }

            if (!Delivered && KeyGiveCycle.HasHandedOver(_seconds))
            {
                Delivered = true;
                _player.Inventory.Remove(KeyHandover.KeyItemId); // ya no la lleva él
                OrchardGarrison garrison = GetComponentInParent<OrchardGarrison>(); // el capitán cuelga de su guarnición
                if (garrison != null) garrison.RaiseSpears(); // los soldados alzan las lanzas al descompás
            }

            if (KeyGiveCycle.IsFinished(_seconds))
            {
                _arm.localRotation = _armRest;
                _seconds = -1f;
                if (_input != null) _input.GameplayBlocked = false;
                EndingSequence.Trigger(); // después: fundido en negro y el pergamino del final
            }
        }

        private void WatchForTouch()
        {
            if (_player == null) _player = FindFirstObjectByType<PlayerInteractor>();
            if (_player == null || _player.Inventory.CountOf(KeyHandover.KeyItemId) <= 0) return; // sin llave no hay nada que dar

            Vector3 d = _player.transform.position - transform.position;
            if (!ContactTouch.IsTouching(d.x, d.y, d.z)) return;

            _hero = _player.GetComponentInChildren<CharacterWalkAnimator>();
            if (_hero == null || _hero.RightHand == null) return;
            Started = true;
            _seconds = 0f;

            // Se miran el uno al otro; el juego queda parado mientras dura el gesto.
            Vector3 flat = new Vector3(d.x, 0f, d.z);
            if (flat.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
                _player.transform.rotation = Quaternion.LookRotation(-flat.normalized, Vector3.up);
            }
            _input = FindFirstObjectByType<QazrixInput>();
            if (_input != null) _input.GameplayBlocked = true;
            _hero.PlayKeyGive();
        }
    }
}
