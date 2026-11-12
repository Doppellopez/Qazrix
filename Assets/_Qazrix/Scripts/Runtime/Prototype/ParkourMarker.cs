using Qazrix.Simulation.Movement;
using UnityEngine;

namespace Qazrix.Prototype
{
    /// <summary>
    /// Marca un obstáculo como apto para parkour y dice qué se puede hacer en él (spec §6.1: «componentes
    /// de parkour colocados»). Sin esta marca no hay salto ni trepada: no existe la escalada universal.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class ParkourMarker : MonoBehaviour
    {
        [SerializeField] private ParkourKind kind = ParkourKind.Vault;

        public ParkourKind Kind => kind;

        public void Configure(ParkourKind allowed)
        {
            kind = allowed;
        }
    }
}
