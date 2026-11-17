using Qazrix.Gameplay;
using Qazrix.Simulation.AI;
using Qazrix.Simulation.Movement;
using UnityEngine;

namespace Qazrix.Prototype
{
    /// <summary>
    /// Salto de fe: desde el borde de una atalaya (<see cref="FaithLeapZone"/>) el jugador se lanza al
    /// vacío y cae sobre un pajar (<see cref="HayLanding"/>). El vuelo es cinemático hacia un destino
    /// fijo: <see cref="LeapRules"/> calcula la duración y la trayectoria, aquí se comprueba que el arco
    /// esté libre y se mueve el cuerpo. El controlador del jugador decide cuándo intentarlo.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerFaithLeap : MonoBehaviour
    {
        [SerializeField] private LeapLimits limits = new LeapLimits();
        [SerializeField] private LayerMask obstacleMask = ~0;
        [SerializeField] private int arcSamples = 16;

        private CharacterController _controller;
        private FaithLeapZone _zone;
        private HayLanding _target;
        private Float3 _start, _end;
        private float _duration, _elapsed;

        public bool IsActive { get; private set; }

        /// <summary>Hay una zona de salto bajo los pies y no se está saltando ya.</summary>
        public bool Available => !IsActive && _zone != null && _zone.Target != null;

        public int LeapsDone { get; private set; }

        /// <summary>El último salto terminó sobre un pajar.</summary>
        public bool LastLeapSafe { get; private set; }

        /// <summary>Avance del vuelo, de 0 a 1.</summary>
        public float Progress => IsActive && _duration > 0f ? Mathf.Clamp01(_elapsed / _duration) : 0f;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        public void Enter(FaithLeapZone zone)
        {
            _zone = zone;
        }

        public void Exit(FaithLeapZone zone)
        {
            if (_zone == zone) _zone = null;
        }

        /// <summary>Empieza el salto si hay zona, el desnivel y el alcance lo permiten y el arco está libre.</summary>
        public bool TryBegin()
        {
            if (!Available) return false;

            Vector3 from = transform.position;
            Vector3 to = _zone.Target.LandingPoint;
            Vector3 flat = to - from;
            flat.y = 0f;
            float drop = from.y - to.y;
            if (!LeapRules.CanLeap(drop, flat.magnitude, limits)) return false;

            float duration = LeapRules.FlightTime(drop, limits);
            var start = new Float3(from.x, from.y, from.z);
            var end = new Float3(to.x, to.y, to.z);

            // El propio controlador no cuenta como obstáculo del arco.
            _controller.enabled = false;
            if (!IsArcClear(start, end, duration))
            {
                _controller.enabled = true;
                return false;
            }

            _target = _zone.Target;
            _start = start;
            _end = end;
            _duration = duration;
            _elapsed = 0f;
            IsActive = true;
            transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
            return true;
        }

        /// <summary>Avanza el vuelo. Devuelve false cuando ha terminado.</summary>
        public bool Tick(float deltaTime)
        {
            if (!IsActive) return false;

            _elapsed += deltaTime;
            Float3 p = LeapRules.Position(_start, _end, _duration, _elapsed, limits);
            transform.position = new Vector3(p.X, p.Y, p.Z);
            if (_elapsed < _duration) return true;

            LeapsDone++;
            LastLeapSafe = _target != null && _target.Contains(transform.position);
            if (_target != null) _target.RegisterLanding();
            End();
            return false;
        }

        /// <summary>Corta el salto sin más (reaparición, captura).</summary>
        public void Cancel()
        {
            if (IsActive) End();
        }

        private void End()
        {
            IsActive = false;
            _controller.enabled = true;
        }

        private bool IsArcClear(Float3 start, Float3 end, float duration)
        {
            float radius = _controller.radius * 0.8f;
            float height = _controller.height;
            for (int i = 1; i <= arcSamples; i++)
            {
                Float3 p = LeapRules.Position(start, end, duration, duration * i / arcSamples, limits);
                // El último punto es la cima del pajar: se comprueba algo por encima del suelo.
                var feet = new Vector3(p.X, p.Y + (i == arcSamples ? 0.2f : 0f), p.Z);
                Vector3 bottom = feet + Vector3.up * (radius + 0.1f);
                Vector3 top = feet + Vector3.up * (height - radius);
                if (Physics.CheckCapsule(bottom, top, radius, obstacleMask, QueryTriggerInteraction.Ignore)) return false;
            }
            return true;
        }
    }
}
