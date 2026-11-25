using Qazrix.Input;
using Qazrix.Prototype;
using Qazrix.Simulation.Movement;
using UnityEngine;

namespace Qazrix.Gameplay
{
    /// <summary>
    /// Zahra le da una llave al protagonista cuando él la toca: ella extiende el brazo con la llave, él la toma, se la lleva al
    /// pecho y se la guarda en la camisa (<see cref="KeyHandoverCycle"/>). Al guardarla entra en el inventario. Una sola vez.
    /// </summary>
    public sealed class KeyHandover : MonoBehaviour
    {
        public const string KeyItemId = "llave";

        private ContactIdleAnimator _zahra;
        private PlayerInteractor _player;
        private CharacterWalkAnimator _hero;
        private QazrixInput _input;
        private Transform _key;
        private float _seconds = -1f;

        /// <summary>Ya se ha entregado la llave (o está en ello).</summary>
        public bool Started { get; private set; }

        /// <summary>El protagonista ya la tiene guardada en la camisa.</summary>
        public bool Delivered { get; private set; }

        /// <summary>Segundos desde que empezó la entrega; -1 si no está en marcha.</summary>
        public float Seconds => _seconds;

        /// <summary>Empieza la entrega (una sola vez). Los dos se miran, ella ofrece y él recibe; el juego se para mientras dura.</summary>
        public void Begin(PlayerInteractor player)
        {
            if (Started || player == null) return;
            _zahra = GetComponent<ContactIdleAnimator>();
            _hero = player.GetComponentInChildren<CharacterWalkAnimator>();
            if (_zahra == null || _hero == null || _zahra.RightHand == null || _hero.RightHand == null) return;
            Started = true;
            _player = player;
            _seconds = 0f;

            // Se miran el uno al otro.
            Vector3 toHero = player.transform.position - transform.position;
            toHero.y = 0f;
            if (toHero.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(toHero.normalized, Vector3.up);
                player.transform.rotation = Quaternion.LookRotation(-toHero.normalized, Vector3.up);
            }

            _input = FindFirstObjectByType<QazrixInput>();
            if (_input != null) _input.GameplayBlocked = true;
            _hero.PlayKeyHandover();
            _key = BuildKey(_zahra.RightHand);
        }

        private void Update()
        {
            if (_seconds < 0f) return;
            _seconds += Time.deltaTime;

            _zahra.OfferWeight = KeyHandoverCycle.ZahraOffer(_seconds);

            if (_key != null)
            {
                if (KeyHandoverCycle.KeyVisible(_seconds))
                {
                    // La llave pasa de la mano de ella a la de él, y después sigue a la mano de él hasta el pecho.
                    Vector3 from = _zahra.RightHand.position, to = _hero.RightHand.position;
                    _key.position = Vector3.Lerp(from, to, KeyHandoverCycle.KeyBlend(_seconds));
                    Quaternion hold = Quaternion.LookRotation(transform.forward + Vector3.up * 0.5f, Vector3.up); // la hoja apunta hacia delante y arriba
                    _key.rotation = hold;
                }
                else
                {
                    Destroy(_key.gameObject);
                    _key = null;
                }
            }

            if (!Delivered && KeyHandoverCycle.HasStashed(_seconds))
            {
                Delivered = true; // guardada en la camisa
                // La llave señala el antiguo alcázar: aparece una X en el mapa y, desde el inventario, la llave abre el mapa ahí.
                Vector2 alcazar = MapPoints.OldAlcazar;
                _player.Inventory.Add(KeyItemId, "Llave", 1, true, alcazar.x, alcazar.y);
                MapPanel map = FindFirstObjectByType<MapPanel>();
                if (map != null) map.AddMark(new Vector3(alcazar.x, 0f, alcazar.y));
            }

            if (KeyHandoverCycle.IsFinished(_seconds))
            {
                _zahra.OfferWeight = 0f;
                _seconds = -1f;
                if (_input != null) _input.GameplayBlocked = false;
            }
        }

        /// <summary>
        /// Llave de unos 12 cm hecha con primitivas, del dorado del ribete de Zahra: anilla, vástago y dos dientes. Sin colisión.
        /// La hoja apunta a +Z de la llave.
        /// </summary>
        private Transform BuildKey(Transform hand)
        {
            Material gold = null;
            foreach (MeshRenderer r in GetComponentsInChildren<MeshRenderer>(true))
                if (r.name == "Ribete") { gold = r.sharedMaterial; break; }

            return KeyProp.Build(gold, hand.position);
        }
    }
}
