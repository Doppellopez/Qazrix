using Qazrix.Prototype;
using Qazrix.Simulation.AI;
using UnityEngine;
using UnityEngine.AI;

namespace Qazrix.AI
{
    /// <summary>
    /// Guardia del prototipo (spec §6.2). Aquí solo hay sentidos y movimiento: qué hacer con lo que
    /// percibe lo decide <see cref="AlertStateMachine"/>, y cuánto ve lo calcula <see cref="VisionCone"/>.
    /// Se desplaza con un CharacterController siguiendo el camino que calcula la malla de navegación
    /// (D38); si no hay malla o no encuentra camino, va en línea recta. Todo el movimiento pasa por
    /// <see cref="MoveTo"/>.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class GuardController : MonoBehaviour
    {
        [Header("Referencias (las asigna PrototypeSceneBuilder)")]
        [SerializeField] private StealthTarget target;
        [SerializeField] private Transform[] waypoints = new Transform[0];
        [SerializeField] private Renderer indicator;

        [Header("Sentidos")]
        [SerializeField] private VisionConfig vision = new VisionConfig();
        [SerializeField] private AlertConfig alert = new AlertConfig();
        [SerializeField] private float eyeHeight = 1.6f;
        [SerializeField] private LayerMask sightMask = ~0;

        [Header("Movimiento")]
        [SerializeField] private float walkSpeed = 1.6f;
        [SerializeField] private float investigateSpeed = 2.6f;
        [SerializeField] private float runSpeed = 4.2f;
        [SerializeField] private float turnSpeedDegrees = 240f;
        [SerializeField] private float arriveDistance = 0.5f;
        [SerializeField] private float catchDistance = 0.85f; // alcance de contacto en horizontal: los dos cuerpos casi se tocan
        [SerializeField] private float bodyHeight = 1.9f;     // altura del guardia: con la del jugador decide si de verdad le toca
        [SerializeField] private float lookAroundDegreesPerSecond = 60f;

        [Header("Navegación")]
        [SerializeField] private float repathSeconds = 0.4f;
        [SerializeField] private float navSampleDistance = 2f;
        [SerializeField] private float cornerReach = 0.3f;

        private CharacterController _controller;
        private CharacterWalkAnimator _animator;
        private AlertStateMachine _machine;
        private MaterialPropertyBlock _block;
        private int _waypointIndex;
        private Vector3 _post;
        private Quaternion _postRotation;
        private Vector3 _lastKnown;

        private NavMeshPath _path;
        private Vector3[] _corners = new Vector3[0];
        private Vector3 _pathDestination;
        private float _pathTime = -1000f;
        private int _corner;
        private bool _hasPath;

        /// <summary>Si en el último movimiento seguía un camino de la malla de navegación (false = línea recta).</summary>
        public bool IsFollowingPath => _hasPath;

        public AlertState State => _machine != null ? _machine.State : AlertState.Patrol;
        public float Awareness => _machine != null ? _machine.Awareness : 0f;

        /// <summary>Cuánto ve al objetivo en este fotograma (0..1). Para depuración y pruebas.</summary>
        public float LastVisibility { get; private set; }

        /// <summary>Si ha oído al objetivo en este fotograma.</summary>
        public bool LastHeard { get; private set; }

        public Vector3 LastKnownPosition => _lastKnown;

        public void Configure(StealthTarget stealthTarget, Transform[] route, Renderer stateIndicator)
        {
            target = stealthTarget;
            waypoints = route ?? new Transform[0];
            indicator = stateIndicator;
        }

        /// <summary>Convierte al guardia en centinela: se queda en ese punto mirando en esa dirección.</summary>
        public void SetPost(Vector3 position, Quaternion rotation)
        {
            waypoints = new Transform[0];
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _controller.enabled = true;
            _post = position;
            _postRotation = rotation;
            _lastKnown = position;
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _machine = new AlertStateMachine(alert);
            _machine.StateChanged += OnStateChanged;
            _block = new MaterialPropertyBlock();
            _post = transform.position;
            _postRotation = transform.rotation;
            _lastKnown = transform.position;
        }

        private void Update()
        {
            if (target == null) return;

            Sense(out float visibility, out bool heard);
            _machine.Tick(Time.deltaTime, new GuardPercept(visibility, heard, IsAtPost()));
            Act(visibility);
            UpdateIndicator();

            // Persiguiendo, lleva el brazo y la cimitarra en alto; en cualquier otro estado, abajo.
            if (_animator == null) _animator = GetComponentInChildren<CharacterWalkAnimator>();
            if (_animator != null) _animator.WeaponRaised = State == AlertState.Alert;
        }

        private void Sense(out float visibility, out bool heard)
        {
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 aim = target.AimPoint;

            // La geometría tapa la vista: el guardia nunca «ve a través» de una pared.
            bool lineOfSight = !Physics.Linecast(eye, aim, out RaycastHit hit, sightMask, QueryTriggerInteraction.Ignore)
                               || hit.transform.IsChildOf(target.transform);

            visibility = VisionCone.Visibility(ToFloat3(eye), ToFloat3(transform.forward), ToFloat3(aim),
                lineOfSight, target.Exposure, vision);
            heard = StealthRules.Hears(Vector3.Distance(transform.position, target.transform.position), target.NoiseRadius);

            if (visibility > 0f || heard) _lastKnown = target.transform.position;
            LastVisibility = visibility;
            LastHeard = heard;
        }

        private void Act(float visibility)
        {
            switch (_machine.State)
            {
                case AlertState.Patrol:
                    Patrol();
                    break;

                case AlertState.Suspicious:
                    Face(_lastKnown);
                    break;

                case AlertState.Investigate:
                case AlertState.Search:
                    if (MoveTo(_lastKnown, investigateSpeed))
                        transform.Rotate(0f, lookAroundDegreesPerSecond * Time.deltaTime, 0f); // mira alrededor
                    break;

                case AlertState.Alert:
                    MoveTo(_lastKnown, runSpeed);
                    // Solo si le toca: pasar por encima o por debajo del guardia (un tejado, otro piso) no le captura.
                    if (visibility > 0f && StealthRules.Touches(FlatDistance(transform.position, target.transform.position),
                            transform.position.y, bodyHeight, target.transform.position.y, target.BodyHeight, catchDistance))
                    {
                        target.Caught();
                        _machine.GiveUp();
                    }
                    break;

                case AlertState.Return:
                    MoveTo(HasRoute ? waypoints[_waypointIndex].position : _post, walkSpeed);
                    break;
            }
        }

        private bool HasRoute => waypoints != null && waypoints.Length > 0;

        private void Patrol()
        {
            if (!HasRoute)
            {
                if (MoveTo(_post, walkSpeed))
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, _postRotation, turnSpeedDegrees * Time.deltaTime);
                return;
            }

            if (MoveTo(waypoints[_waypointIndex].position, walkSpeed))
                _waypointIndex = (_waypointIndex + 1) % waypoints.Length;
        }

        private bool IsAtPost()
        {
            Vector3 post = HasRoute ? waypoints[_waypointIndex].position : _post;
            return FlatDistance(transform.position, post) <= arriveDistance;
        }

        private void OnStateChanged(AlertState previous, AlertState next)
        {
            // Al rendirse vuelve al punto de ruta más cercano, no al que tocaba antes de la alarma.
            if (next != AlertState.Return || !HasRoute) return;
            float best = float.MaxValue;
            for (int i = 0; i < waypoints.Length; i++)
            {
                float d = FlatDistance(transform.position, waypoints[i].position);
                if (d < best) { best = d; _waypointIndex = i; }
            }
        }

        /// <summary>
        /// Avanza hacia el destino rodeando obstáculos. Devuelve true cuando ha llegado, o cuando ha
        /// llegado al punto alcanzable más cercano (por ejemplo, si el destino está sobre una plataforma).
        /// </summary>
        private bool MoveTo(Vector3 destination, float speed)
        {
            if (FlatDistance(transform.position, destination) <= arriveDistance)
            {
                _controller.SimpleMove(Vector3.zero); // sigue aplicando la gravedad
                return true;
            }

            Vector3 step = NextStep(destination, out bool endOfPath);
            if (endOfPath)
            {
                _controller.SimpleMove(Vector3.zero);
                return true;
            }

            Vector3 flat = step - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 1e-6f)
            {
                _controller.SimpleMove(Vector3.zero);
                return false;
            }

            Vector3 direction = flat.normalized;
            Quaternion look = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeedDegrees * Time.deltaTime);
            _controller.SimpleMove(direction * speed);
            return false;
        }

        /// <summary>
        /// Siguiente punto hacia el que andar. Recalcula el camino cada poco o si el destino se mueve.
        /// Sin malla de navegación o sin camino, devuelve el propio destino (línea recta).
        /// </summary>
        private Vector3 NextStep(Vector3 destination, out bool endOfPath)
        {
            endOfPath = false;
            bool stale = Time.time - _pathTime > repathSeconds || (destination - _pathDestination).sqrMagnitude > 0.25f;
            if (stale)
            {
                if (_path == null) _path = new NavMeshPath();
                _pathTime = Time.time;
                _pathDestination = destination;
                _corner = 1;
                _hasPath = NavMesh.SamplePosition(transform.position, out NavMeshHit from, navSampleDistance, NavMesh.AllAreas)
                           && NavMesh.SamplePosition(destination, out NavMeshHit to, navSampleDistance, NavMesh.AllAreas)
                           && NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, _path)
                           && _path.corners.Length >= 2;
                if (_hasPath) _corners = _path.corners;
            }

            if (!_hasPath) return destination;

            while (_corner < _corners.Length - 1 && FlatDistance(transform.position, _corners[_corner]) <= cornerReach) _corner++;
            if (_corner == _corners.Length - 1 && FlatDistance(transform.position, _corners[_corner]) <= cornerReach) endOfPath = true;
            return _corners[_corner];
        }

        private void Face(Vector3 point)
        {
            Vector3 flat = point - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f)
            {
                Quaternion look = Quaternion.LookRotation(flat.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeedDegrees * Time.deltaTime);
            }
            _controller.SimpleMove(Vector3.zero);
        }

        /// <summary>Señal de descubrimiento gradual: cambia de color con el estado y crece con la sospecha.</summary>
        private void UpdateIndicator()
        {
            if (indicator == null) return;
            Color color = StateColor(_machine.State);
            _block.SetColor("_BaseColor", color);
            _block.SetColor("_Color", color);
            indicator.SetPropertyBlock(_block);
            float size = 0.2f + 0.35f * _machine.Awareness;
            indicator.transform.localScale = new Vector3(size, size, size);
        }

        public static Color StateColor(AlertState state)
        {
            switch (state)
            {
                case AlertState.Suspicious: return new Color(1f, 0.85f, 0.1f);
                case AlertState.Investigate: return new Color(1f, 0.55f, 0.1f);
                case AlertState.Alert: return new Color(0.9f, 0.1f, 0.1f);
                case AlertState.Search: return new Color(0.8f, 0.2f, 0.8f);
                case AlertState.Return: return new Color(0.3f, 0.5f, 1f);
                default: return new Color(0.3f, 0.8f, 0.3f);
            }
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static Float3 ToFloat3(Vector3 v) => new Float3(v.x, v.y, v.z);

        private void OnDrawGizmosSelected()
        {
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Gizmos.color = StateColor(State);
            Vector3 left = Quaternion.Euler(0f, -vision.HalfAngleDegrees, 0f) * transform.forward * vision.Range;
            Vector3 right = Quaternion.Euler(0f, vision.HalfAngleDegrees, 0f) * transform.forward * vision.Range;
            Gizmos.DrawLine(eye, eye + left);
            Gizmos.DrawLine(eye, eye + right);
            Gizmos.DrawLine(eye + left, eye + right);
            Gizmos.DrawWireSphere(transform.position, vision.ProximityRadius);
            if (Application.isPlaying) Gizmos.DrawWireSphere(_lastKnown, 0.3f);
        }
    }
}
