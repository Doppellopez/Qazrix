using UnityEngine;

namespace Qazrix.Gameplay
{
    /// <summary>
    /// El personaje que espera en la X roja del mapa (la mujer del velo violeta). Está en la escena desactivado; el mapa
    /// lo coloca al elegir el punto del objetivo, apoyado en el suelo y mirando hacia el jugador.
    /// </summary>
    public sealed class ObjectiveContact : MonoBehaviour
    {
        /// <summary>Dónde se colocó (null mientras no ha aparecido).</summary>
        public Vector3? Placed { get; private set; }

        /// <summary>El jugador la ha tocado: la X del mapa ya no se enseña.</summary>
        public bool Touched { get; private set; }

        private PlayerInteractor _player;

        private void Awake()
        {
            // Espera con vida desde que aparece; se añade aquí para que valga también la escena ya guardada.
            if (GetComponent<ContactIdleAnimator>() == null) gameObject.AddComponent<ContactIdleAnimator>();
            if (GetComponent<KeyHandover>() == null) gameObject.AddComponent<KeyHandover>();
        }

        private void Update()
        {
            if (Touched || !Placed.HasValue) return;
            if (_player == null) _player = FindFirstObjectByType<PlayerInteractor>();
            if (_player == null) return;

            Vector3 d = _player.transform.position - transform.position;
            if (Qazrix.Simulation.Gameplay.ContactTouch.IsTouching(d.x, d.y, d.z))
            {
                Touched = true;
                GetComponent<KeyHandover>()?.Begin(_player); // al tocarla, Zahra le da la llave
            }
        }

        /// <summary>Lo pone en el suelo bajo <paramref name="world"/> (X y Z) mirando hacia <paramref name="lookAt"/> y lo activa.</summary>
        public void PlaceAt(Vector3 world, Vector3 lookAt)
        {
            Vector3 position = world;
            if (Physics.Raycast(new Vector3(world.x, world.y + 400f, world.z), Vector3.down, out RaycastHit hit, 900f, ~0, QueryTriggerInteraction.Ignore))
                position = hit.point;

            Vector3 toward = new Vector3(lookAt.x - position.x, 0f, lookAt.z - position.z);
            transform.SetPositionAndRotation(position, toward.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toward, Vector3.up) : transform.rotation);
            gameObject.SetActive(true);
            Placed = position;
        }
    }
}
