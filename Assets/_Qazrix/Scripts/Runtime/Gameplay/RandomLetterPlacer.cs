using Qazrix.Simulation.Gameplay;
using Qazrix.World;
using UnityEngine;
using UnityEngine.AI;

namespace Qazrix.Gameplay
{
    /// <summary>
    /// Al empezar a jugar mueve una carta a un punto al azar de la zona exterior de la plaza: una corona
    /// alrededor del punto de salida del jugador, libre de edificios y alcanzable a pie por la malla de
    /// navegación. Cada ejecución sale en un sitio distinto. Si no encuentra ninguno, la carta se queda donde está.
    /// </summary>
    public sealed class RandomLetterPlacer : MonoBehaviour
    {
        [SerializeField] private LetterPickup letter;
        [SerializeField] private CityBlocks city;
        [SerializeField] private Transform center;
        [SerializeField] private float minRadius = 16f;
        [SerializeField] private float maxRadius = 55f;
        [SerializeField] private float clearRadius = 1.5f;
        [SerializeField] private float heightAboveGround = 0.3f;

        private NavMeshPath _path; // se crea en Start: Unity no deja hacerlo en un inicializador de campo

        /// <summary>Dónde se colocó la carta en esta ejecución; null si no se movió.</summary>
        public Vector3? Placed { get; private set; }

        public void Configure(LetterPickup target, CityBlocks buildings, Transform origin)
        {
            letter = target;
            city = buildings;
            center = origin;
        }

        private void Start()
        {
            if (letter == null || center == null) return;
            _path = new NavMeshPath();
            Vector3 origin = center.position;
            Vector3 spot = default;

            bool found = RandomSpot.TryPick(() => Random.value, origin.x, origin.z, minRadius, maxRadius, (x, z) =>
            {
                if (city != null && !city.IsClear(x, z, clearRadius)) return false;
                if (!NavMesh.SamplePosition(new Vector3(x, origin.y, z), out NavMeshHit hit, 6f, NavMesh.AllAreas)) return false;
                if (Mathf.Abs(hit.position.x - x) > 1f || Mathf.Abs(hit.position.z - z) > 1f) return false;
                if (!NavMesh.CalculatePath(origin, hit.position, NavMesh.AllAreas, _path) || _path.status != NavMeshPathStatus.PathComplete) return false;
                spot = hit.position;
                return true;
            }, 200, out _, out _);

            if (!found)
            {
                Debug.LogWarning("[Qazrix] La carta al azar no encontró sitio en la zona exterior de la plaza: se queda donde está.", this);
                return;
            }

            letter.transform.position = spot + Vector3.up * heightAboveGround;
            Placed = letter.transform.position;
            Debug.Log($"[Qazrix] Carta al azar en ({spot.x:0.0}, {spot.z:0.0}), a {Vector3.Distance(new Vector3(origin.x, 0f, origin.z), new Vector3(spot.x, 0f, spot.z)):0} m del punto de salida");
        }
    }
}
