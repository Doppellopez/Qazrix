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

        /// <summary>Altura del cuerpo en metros (con los pies en el origen): sirve para saber si un guardia le toca de verdad.</summary>
        public virtual float BodyHeight => 1.8f;

        /// <summary>Un guardia le ha alcanzado.</summary>
        public abstract void Caught();

        /// <summary>Un guardia le ha alcanzado desde ese punto del mundo (de ahí sabe hacia dónde caer). Por defecto, igual que <see cref="Caught()"/>.</summary>
        public virtual void Caught(Vector3 attackerPosition) => Caught();

        /// <summary>Si ya ha caído y está tumbado o reapareciendo: no se le vuelve a alcanzar ni a ver.</summary>
        public virtual bool IsDown => false;
    }
}
