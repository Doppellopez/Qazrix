using Qazrix.Simulation.AI;
using Qazrix.Simulation.Movement;
using UnityEngine;

namespace Qazrix.Prototype
{
    /// <summary>
    /// Parkour contextual del jugador: mide el obstáculo marcado que tiene delante, pregunta a
    /// <see cref="ParkourRules"/> si puede pasarlo y, si es así, lleva el cuerpo por la trayectoria de
    /// <see cref="ParkourPath"/>. El controlador del jugador decide cuándo intentarlo (tecla de salto).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerParkour : MonoBehaviour
    {
        [SerializeField] private ParkourLimits limits = new ParkourLimits();
        [SerializeField] private float reach = 0.9f;            // metros desde el borde del cuerpo
        [SerializeField] private float probeHeight = 0.25f;     // altura del rayo sobre los pies
        [SerializeField] private float landingMargin = 0.3f;    // hueco entre el obstáculo y el punto de llegada
        [SerializeField] private float minFacing = 0.6f;        // cuánto de frente hay que estar (coseno)
        [SerializeField] private LayerMask obstacleMask = ~0;

        private struct Plan
        {
            public ParkourAction Action;
            public Vector3 Start;
            public Vector3 End;
            public Vector3 Facing;
            public float ObstacleTop;
            public float Duration;
        }

        private CharacterController _controller;
        private Plan _plan;
        private float _progress;

        public bool IsActive { get; private set; }

        /// <summary>Acción posible ahora mismo con lo que hay delante (para la ayuda en pantalla).</summary>
        public ParkourAction Available { get; private set; }

        public ParkourAction LastAction { get; private set; }
        public int ActionsDone { get; private set; }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        /// <summary>Actualiza <see cref="Available"/>. Lo llama el controlador cuando el jugador puede actuar.</summary>
        public void Refresh(bool canAct)
        {
            Available = canAct && !IsActive ? Probe(out _) : ParkourAction.None;
        }

        /// <summary>Empieza la acción si hay un obstáculo marcado y practicable delante.</summary>
        public bool TryBegin()
        {
            if (IsActive || Probe(out Plan plan) == ParkourAction.None) return false;

            _plan = plan;
            _progress = 0f;
            IsActive = true;
            Available = ParkourAction.None;
            _controller.enabled = false; // durante la acción el cuerpo sigue la trayectoria, no la física
            return true;
        }

        /// <summary>Avanza la acción en curso. Devuelve true mientras siga activa.</summary>
        public bool Tick(float deltaTime)
        {
            if (!IsActive) return false;

            _progress += deltaTime / Mathf.Max(_plan.Duration, 0.05f);
            Float3 p = ParkourPath.Evaluate(_plan.Action, ToFloat3(_plan.Start), ToFloat3(_plan.End), _plan.ObstacleTop, _progress);
            transform.position = new Vector3(p.X, p.Y, p.Z);
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(_plan.Facing, Vector3.up), 720f * deltaTime);

            if (_progress < 1f) return true;

            transform.position = _plan.End;
            _controller.enabled = true;
            IsActive = false;
            LastAction = _plan.Action;
            ActionsDone++;
            return false;
        }

        private ParkourAction Probe(out Plan plan)
        {
            plan = default;
            Vector3 feet = transform.position;
            Vector3 forward = transform.forward;
            float radius = _controller.radius;

            Vector3 origin = feet + Vector3.up * probeHeight;
            if (!Physics.Raycast(origin, forward, out RaycastHit front, radius + reach, obstacleMask, QueryTriggerInteraction.Ignore))
                return ParkourAction.None;

            var marker = front.collider.GetComponentInParent<ParkourMarker>();
            if (marker == null) return ParkourAction.None;

            // Dirección hacia dentro del obstáculo, en horizontal.
            Vector3 into = -front.normal;
            into.y = 0f;
            if (into.sqrMagnitude < 0.01f) return ParkourAction.None; // la cara golpeada no es una pared
            into.Normalize();
            if (Vector3.Dot(into, forward) < minFacing) return ParkourAction.None; // hay que ir de frente

            float top = front.collider.bounds.max.y;
            float height = top - feet.y;

            // Grosor: rayo desde el otro lado hacia atrás contra el mismo colisionador.
            float depth = float.MaxValue;
            Vector3 farSide = front.point;
            var back = new Ray(front.point + into * 6f, -into);
            if (front.collider.Raycast(back, out RaycastHit rear, 6f))
            {
                depth = Vector3.Distance(front.point, rear.point);
                farSide = rear.point;
            }

            Vector3 end;
            if (marker.Kind == ParkourKind.Vault)
            {
                end = farSide + into * (radius + landingMargin);
                if (!Physics.Raycast(end + Vector3.up * 2f, Vector3.down, out RaycastHit ground, 6f, obstacleMask, QueryTriggerInteraction.Ignore))
                    return ParkourAction.None;
                end.y = ground.point.y;
            }
            else
            {
                end = front.point + into * (radius + landingMargin);
                end.y = top;
            }

            ParkourAction action = ParkourRules.Decide(marker.Kind, height, depth, HasRoomAt(end), limits);
            if (action == ParkourAction.None) return ParkourAction.None;

            plan = new Plan
            {
                Action = action,
                Start = feet,
                End = end,
                Facing = into,
                ObstacleTop = top,
                Duration = ParkourRules.Duration(action, height)
            };
            return action;
        }

        /// <summary>¿Cabe el cuerpo de pie en ese punto?</summary>
        private bool HasRoomAt(Vector3 feet)
        {
            float radius = _controller.radius * 0.9f;
            Vector3 bottom = feet + Vector3.up * (radius + 0.08f);
            Vector3 topSphere = feet + Vector3.up * (_controller.height - radius);
            return !Physics.CheckCapsule(bottom, topSphere, radius, obstacleMask, QueryTriggerInteraction.Ignore);
        }

        private static Float3 ToFloat3(Vector3 v) => new Float3(v.x, v.y, v.z);
    }
}
