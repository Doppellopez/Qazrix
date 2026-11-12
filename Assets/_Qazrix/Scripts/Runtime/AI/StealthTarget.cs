using UnityEngine;

namespace Qazrix.AI
{
    /// <summary>
    /// Lo que un guardia necesita saber de quien vigila. El jugador lo implementa; el guardia no conoce
    /// al controlador concreto, solo este contrato.
    /// </summary>
    public abstract class StealthTarget : MonoBehaviour
    {
        /// <summary>Punto del cuerpo al que mira el guardia para comprobar la línea de visión.</summary>
        public abstract Vector3 AimPoint { get; }

        /// <summary>Exposición entre 0 y 1 (agachado se ve menos).</summary>
        public abstract float Exposure { get; }

        /// <summary>Radio en metros en el que se le oye ahora mismo; 0 si no hace ruido.</summary>
        public abstract float NoiseRadius { get; }

        /// <summary>Un guardia le ha alcanzado.</summary>
        public abstract void Caught();
    }
}
