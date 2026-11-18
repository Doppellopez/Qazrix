using Qazrix.Input;
using Qazrix.Simulation.Movement;
using UnityEngine;

namespace Qazrix.Prototype
{
    /// <summary>
    /// Controlador en tercera persona del prototipo (spec §6.1): andar, correr, acelerar y frenar,
    /// girar hacia el movimiento, saltar, caer y agacharse. Se mueve relativo a la cámara.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PrototypePlayerController : MonoBehaviour
    {
        [Header("Referencias (las asigna PrototypeSceneBuilder)")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Transform visual;
        [SerializeField] private QazrixInput inputReader;

        [Header("Velocidad (m/s)")]
        [SerializeField] private float walkSpeed = 2.2f;
        [SerializeField] private float runSpeed = 5.5f;
        [SerializeField] private float crouchSpeed = 1.3f;
        [SerializeField] private float acceleration = 18f;
        [SerializeField] private float deceleration = 24f;
        [SerializeField] private float airControl = 0.35f;
        [SerializeField] private float turnSpeedDegrees = 720f;
        [SerializeField] private float tankTurnDegrees = 100f; // °/s al girar con las flechas izquierda/derecha

        [Header("Salto")]
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -22f;
        [SerializeField] private float coyoteSeconds = 0.12f;
        [SerializeField] private float jumpBufferSeconds = 0.12f;
        [SerializeField] private float hardLandingSpeed = -4f; // m/s; caer más rápido que esto hace ruido

        [Header("Altura (m)")]
        [SerializeField] private float standHeight = 1.8f;
        [SerializeField] private float crouchHeight = 1.0f;
        [SerializeField] private LayerMask obstacleMask = ~0;

        [Header("Seguridad")]
        [SerializeField] private float respawnFallDistance = 150f; // metros por debajo del punto de salida

        private CharacterController _controller;
        private PlayerParkour _parkour;
        private PlayerWallClimb _climb;
        private PrototypeOrbitCamera _orbit;
        private PlayerFaithLeap _leap;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private float _sinceGrounded;
        private float _sinceJumpPressed = 999f;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;

        public bool IsGrounded { get; private set; }
        public bool IsCrouching { get; private set; }
        public float HorizontalSpeed => _horizontalVelocity.magnitude;

        /// <summary>Corre de verdad: tecla pulsada, de pie, en el suelo y moviéndose.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>Instante (Time.time) de la última caída con golpe; -1000 si aún no ha caído.</summary>
        public float LastLandingTime { get; private set; } = -1000f;

        public float CurrentHeight => IsCrouching ? crouchHeight : standHeight;

        /// <summary>Un guardia le ha matado y está cayendo, tumbado o a punto de reaparecer: no responde a los mandos.</summary>
        public bool IsDead { get; private set; }

        /// <summary>Segundos desde el golpe mortal; -1 si está vivo.</summary>
        public float DeathSeconds => IsDead ? _deathSeconds : -1f;

        /// <summary>Hacia dónde cae al morir (horizontal, unitaria, en el mundo): la que va del guardia al jugador.</summary>
        public Vector3 DeathDirection => _deathDirection;

        private float _deathSeconds;
        private Vector3 _deathDirection = Vector3.back;
        private readonly FallTracker _fall = new FallTracker();

        /// <summary>Cómo ha muerto (de un tajo o de una caída); solo vale mientras <see cref="IsDead"/>.</summary>
        public DeathKind DeathKind { get; private set; }

        /// <summary>Veces que ha muerto por caer desde 15 m o más en esta sesión.</summary>
        public int FallDeaths { get; private set; }

        /// <summary>Altura de la última caída (m) al aterrizar; 0 si aún no ha caído de ninguna altura.</summary>
        public float LastFallDrop { get; private set; }

        /// <summary>
        /// Un guardia le alcanza desde <paramref name="attackerPosition"/>: cae hacia el lado contrario, queda tumbado un
        /// momento (<see cref="DeathCycle"/>) y reaparece en el punto de salida. Sin mandos mientras tanto.
        /// </summary>
        public void Die(Vector3 attackerPosition)
        {
            if (IsDead) return;
            if (_climb != null) _climb.Cancel(); // suelta la pared y la física vuelve a mandar
            if (_leap != null) _leap.Cancel();
            Vector3 away = transform.position - attackerPosition;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = -transform.forward;
            _deathDirection = away.normalized;
            _deathSeconds = 0f;
            DeathKind = DeathKind.Sword;
            IsDead = true;
            IsRunning = false;
            _horizontalVelocity = Vector3.zero;
        }

        /// <summary>Se mata al aterrizar tras una caída desde 15 m o más: cae de bruces, hacia donde miraba.</summary>
        private void DieFalling()
        {
            if (IsDead) return;
            FallDeaths++;
            Vector3 forward = transform.forward;
            forward.y = 0f;
            _deathDirection = forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
            _deathSeconds = 0f;
            DeathKind = DeathKind.Fall;
            IsDead = true;
            IsRunning = false;
            _horizontalVelocity = Vector3.zero;
        }

        /// <summary>Un fotograma de muerte: sin mandos, solo la gravedad hasta que reaparece.</summary>
        private void UpdateDeath(float dt)
        {
            _deathSeconds += dt;
            _horizontalVelocity = Vector3.zero;
            if (_controller.enabled)
            {
                if (IsGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
                _verticalVelocity += gravity * dt;
                _controller.Move(Vector3.up * (_verticalVelocity * dt));
                IsGrounded = _controller.isGrounded;
            }
            if (DeathPoses.IsFinished(DeathKind, _deathSeconds)) Respawn();
        }

        /// <summary>Acción de parkour posible ahora mismo con la tecla de salto (None si no hay ninguna).</summary>
        public ParkourAction AvailableParkour => _parkour != null ? _parkour.Available : ParkourAction.None;

        public bool IsInParkour => _parkour != null && _parkour.IsActive;

        /// <summary>Está agarrado a una fachada, trepando o encaramándose al tejado.</summary>
        public bool IsClimbing => _climb != null && _climb.IsActive;

        /// <summary>Está en el borde de una atalaya y puede lanzarse con la tecla de salto.</summary>
        public bool FaithLeapAvailable => _leap != null && _leap.Available;

        /// <summary>Está en pleno salto de fe.</summary>
        public bool IsLeaping => _leap != null && _leap.IsActive;

        /// <summary>Nombre del último checkpoint alcanzado; vacío si aún reaparece en el inicio.</summary>
        public string CheckpointLabel { get; private set; } = string.Empty;

        /// <summary>Fija dónde reaparece el jugador al caer o ser alcanzado.</summary>
        public void SetCheckpoint(Vector3 position, Quaternion rotation, string label)
        {
            _spawnPosition = position;
            _spawnRotation = rotation;
            CheckpointLabel = label ?? string.Empty;
        }

        public void Configure(Transform cameraRig, Transform visualRoot, QazrixInput reader)
        {
            inputReader = reader;
            var interactor = GetComponent<Qazrix.Gameplay.PlayerInteractor>();
            if (interactor != null) interactor.Configure(reader);
            cameraTransform = cameraRig;
            visual = visualRoot;
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _parkour = GetComponent<PlayerParkour>(); // opcional: sin él solo hay salto normal
            _climb = GetComponent<PlayerWallClimb>();  // opcional: sin él no trepa fachadas
            _leap = GetComponent<PlayerFaithLeap>();   // opcional: sin él no hay salto de fe
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
            ApplyHeight(standHeight);
        }

        private void Update()
        {
            InputFrame input = inputReader != null ? inputReader.Read() : default;
            float dt = Time.deltaTime;

            if (IsDead)
            {
                UpdateDeath(dt);
                return;
            }

            if (_parkour != null)
            {
                // Durante un salto de obstáculo o una trepada el cuerpo sigue su trayectoria: sin control.
                if (_parkour.IsActive)
                {
                    if (!_parkour.Tick(dt)) FinishParkour();
                    return;
                }

                bool canAct = IsGrounded && !IsCrouching;
                _parkour.Refresh(canAct);
                // La tecla de salto es contextual: ante un obstáculo marcado lo salta o lo trepa.
                if (canAct && input.JumpPressed && _parkour.TryBegin())
                {
                    _horizontalVelocity = Vector3.zero;
                    _verticalVelocity = 0f;
                    _sinceJumpPressed = 999f;
                    IsRunning = false;
                    return;
                }
            }

            if (_leap != null)
            {
                // En pleno salto de fe el cuerpo sigue su trayectoria: sin control.
                if (_leap.IsActive)
                {
                    if (!_leap.Tick(dt)) FinishParkour();
                    return;
                }

                // En el borde de una atalaya, la tecla de salto lanza al vacío.
                if (IsGrounded && !IsCrouching && input.JumpPressed && _leap.TryBegin())
                {
                    _horizontalVelocity = Vector3.zero;
                    _verticalVelocity = 0f;
                    _sinceJumpPressed = 999f;
                    IsRunning = false;
                    IsGrounded = false;
                    return;
                }
            }

            if (_climb != null)
            {
                // Trepando, la palanca sube, baja o desplaza de lado; agacharse suelta la pared.
                if (_climb.IsActive)
                {
                    _climb.Tick(dt, input.Move, input.Crouch, input.JumpPressed);
                    if (!_climb.IsActive) FinishClimb();
                    return;
                }

                // Corriendo contra una fachada se agarra a ella, también en el aire tras un salto.
                if (_climb.TryBegin(Wished(input), input.Run, IsCrouching || input.Crouch))
                {
                    _horizontalVelocity = Vector3.zero;
                    _verticalVelocity = 0f;
                    IsGrounded = false;
                    IsRunning = false;
                    return;
                }
            }

            UpdateCrouch(input.Crouch);
            UpdateHorizontal(input, dt);
            UpdateVertical(input, dt);

            Vector3 motion = (_horizontalVelocity + Vector3.up * _verticalVelocity) * dt;
            CollisionFlags flags = _controller.Move(motion);

            bool wasGrounded = IsGrounded;
            float fallSpeed = _verticalVelocity;
            IsGrounded = _controller.isGrounded;
            if (!wasGrounded && IsGrounded && fallSpeed < hardLandingSpeed) LastLandingTime = Time.time;
            // Caer desde 15 m o más mata. La altura se mide desde el punto más alto en el aire: un salto de fe a un pajar
            // (que no pasa por aquí) o un salto de obstáculo no cuentan.
            float drop = _fall.Step(IsGrounded, transform.position.y);
            if (drop > 0f) LastFallDrop = drop; // se queda con la última caída aunque después se ande por el suelo
            if (FallTracker.IsFatal(drop)) DieFalling();
            IsRunning = input.Run && !IsCrouching && IsGrounded && HorizontalSpeed > walkSpeed + 0.2f;
            if ((flags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f) _verticalVelocity = 0f;

            // Caer fuera del mundo no debe dejar al jugador perdido (spec §6.1).
            if (transform.position.y < _spawnPosition.y - respawnFallDistance) Respawn();
        }

        private void FinishClimb()
        {
            _horizontalVelocity = Vector3.zero;
            _sinceJumpPressed = 999f;
            if (_climb.EndedStanding)
            {
                FinishParkour();
                return;
            }
            if (_climb.EndedWithBackJump)
            {
                // Salto hacia atrás: sale despedido de la pared con la velocidad que fijan las reglas.
                Vector3 launch = _climb.BackJumpVelocity;
                _horizontalVelocity = new Vector3(launch.x, 0f, launch.z);
                _verticalVelocity = launch.y;
                _sinceGrounded = 999f;      // ya no vale el margen de salto del suelo
                _sinceJumpPressed = 999f;   // y la pulsación no se reutiliza para otro salto
                IsGrounded = false;
                return;
            }
            // Se ha soltado: cae desde donde estaba.
            _verticalVelocity = 0f;
            _sinceGrounded = 999f;
            IsGrounded = false;
        }

        private void FinishParkour()
        {
            _verticalVelocity = -2f;
            _sinceGrounded = 0f;
            IsGrounded = true;
        }

        private void UpdateCrouch(bool wantsCrouch)
        {
            if (wantsCrouch)
            {
                if (!IsCrouching) { IsCrouching = true; ApplyHeight(crouchHeight); }
                return;
            }

            // Solo se levanta si hay sitio encima: no se puede poner de pie dentro del túnel.
            if (IsCrouching && HasHeadroom())
            {
                IsCrouching = false;
                ApplyHeight(standHeight);
            }
        }

        /// <summary>Dirección horizontal pedida con la palanca, relativa a la cámara.</summary>
        private Vector3 Wished(InputFrame input)
        {
            if (input.Move.sqrMagnitude <= 0.0001f)
            {
                // Flechas: arriba/abajo avanzan o retroceden en la dirección en que mira el personaje.
                if (Mathf.Abs(input.Tank.y) <= 0.0001f) return Vector3.zero;
                Vector3 forward = transform.forward;
                forward.y = 0f;
                return forward.normalized * input.Tank.y;
            }
            // Con la cámara orbital, la orientación de WASD es la suya salvo durante su media vuelta al pulsar «atrás»
            // (entonces es la de antes de empezarla: el personaje sigue yendo hacia el mismo sitio).
            if (_orbit == null && cameraTransform != null) _orbit = cameraTransform.GetComponent<PrototypeOrbitCamera>();
            float yaw = _orbit != null ? _orbit.MovementYaw
                : cameraTransform != null ? cameraTransform.eulerAngles.y : transform.eulerAngles.y;
            return Quaternion.Euler(0f, yaw, 0f) * new Vector3(input.Move.x, 0f, input.Move.y);
        }

        private void UpdateHorizontal(InputFrame input, float dt)
        {
            Vector3 wished = Wished(input);

            // Flechas izquierda/derecha giran al personaje sobre sí mismo, también en movimiento.
            bool tankDrive = input.Move.sqrMagnitude <= 0.0001f && Mathf.Abs(input.Tank.y) > 0.0001f;
            if (Mathf.Abs(input.Tank.x) > 0.0001f)
                transform.Rotate(0f, input.Tank.x * tankTurnDegrees * dt, 0f, Space.World);
            if (tankDrive) wished = Wished(input); // con el giro ya aplicado

            float topSpeed = IsCrouching ? crouchSpeed : (input.Run ? runSpeed : walkSpeed);
            Vector3 target = wished * topSpeed;

            float rate = target.sqrMagnitude > _horizontalVelocity.sqrMagnitude ? acceleration : deceleration;
            if (!IsGrounded) rate *= airControl;
            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, target, rate * dt);

            if (!tankDrive && wished.sqrMagnitude > 0.0001f)
            {
                Quaternion look = Quaternion.LookRotation(wished, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeedDegrees * dt);
            }
        }

        private void UpdateVertical(InputFrame input, float dt)
        {
            _sinceGrounded = IsGrounded ? 0f : _sinceGrounded + dt;
            _sinceJumpPressed = input.JumpPressed ? 0f : _sinceJumpPressed + dt;

            if (IsGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f; // lo mantiene pegado al suelo

            bool canJump = _sinceGrounded <= coyoteSeconds && !IsCrouching;
            if (canJump && _sinceJumpPressed <= jumpBufferSeconds)
            {
                _verticalVelocity = Mathf.Sqrt(2f * jumpHeight * -gravity);
                _sinceJumpPressed = 999f;
                _sinceGrounded = 999f;
            }

            _verticalVelocity += gravity * dt;
        }

        private bool HasHeadroom()
        {
            float radius = _controller.radius * 0.95f;
            Vector3 feet = transform.position + Vector3.up * (radius + 0.05f);
            float distance = standHeight - 2f * radius - 0.05f;
            return !Physics.SphereCast(feet, radius, Vector3.up, out _, distance, obstacleMask, QueryTriggerInteraction.Ignore);
        }

        private void ApplyHeight(float height)
        {
            _controller.height = height;
            _controller.center = new Vector3(0f, height * 0.5f, 0f);
            if (visual != null)
            {
                visual.localScale = new Vector3(visual.localScale.x, height * 0.5f, visual.localScale.z);
                visual.localPosition = new Vector3(0f, height * 0.5f, 0f);
            }
        }

        /// <summary>Devuelve al jugador al punto de salida (caída fuera del mundo o captura).</summary>
        public void Respawn()
        {
            if (_climb != null) _climb.Cancel();
            if (_leap != null) _leap.Cancel();
            _controller.enabled = false;
            transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
            _controller.enabled = true;
            _horizontalVelocity = Vector3.zero;
            _verticalVelocity = 0f;
            IsDead = false;
            _deathSeconds = 0f;
            _fall.Reset(transform.position.y);
        }
    }
}
