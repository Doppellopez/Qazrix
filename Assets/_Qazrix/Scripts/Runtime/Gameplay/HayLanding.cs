using UnityEngine;

namespace Qazrix.Gameplay
{
    /// <summary>
    /// Pajar donde termina un salto de fe: una zona de aterrizaje blanda. Solo guarda dónde cae el
    /// cuerpo y qué radio cuenta como «dentro»; el vuelo lo lleva <see cref="Qazrix.Prototype.PlayerFaithLeap"/>.
    /// </summary>
    public sealed class HayLanding : MonoBehaviour
    {
        [SerializeField] private float radius = 2f;
        [SerializeField] private float surfaceHeight = 0.45f; // altura del montón sobre el suelo

        public float Radius => radius;

        /// <summary>Punto donde acaban los pies: la cima del pajar.</summary>
        public Vector3 LandingPoint => transform.position + Vector3.up * surfaceHeight;

        public int LandingsDone { get; private set; }

        public void Configure(float landingRadius, float height)
        {
            radius = landingRadius;
            surfaceHeight = height;
        }

        /// <summary>¿Está ese punto sobre el pajar (en planta)?</summary>
        public bool Contains(Vector3 position)
        {
            Vector3 flat = position - transform.position;
            flat.y = 0f;
            return flat.magnitude <= radius;
        }

        public void RegisterLanding()
        {
            LandingsDone++;
        }
    }
}
