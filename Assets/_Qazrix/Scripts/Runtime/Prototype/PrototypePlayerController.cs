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

        /// <summary>Acción de parkour posible ahora mismo con la tecla de salto (None si no hay ninguna).</summary>
        public ParkourAction AvailableParkour => _parkour != null ? _parkour.Available : ParkourAction.None;

        public bool IsInParkour => _parkour != null && _parkour.IsActive;

        public void Configure(Transform cameraRig, Transform visualRoot, QazrixInput reader)
        {
            inputReader = reader;
            cameraTransform = cameraRig;
            visual = visualRoot;
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _parkour = GetComponent<PlayerParkour>(); // opcional: sin él solo hay salto normal
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
            ApplyHeight(standHeight);
        }

        private void Update()
        {
            InputFrame input = inputReader != null ? inputReader.Read() : default;
            float dt = Time.deltaTime;

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

            UpdateCrouch(input.Crouch);
            UpdateHorizontal(input, dt);
            UpdateVertical(input, dt);

            Vector3 motion = (_horizontalVelocity + Vector3.up * _verticalVelocity) * dt;
            CollisionFlags flags = _controller.Move(motion);

            bool wasGrounded = IsGrounded;
            float fallSpeed = _verticalVelocity;
            IsGrounded = _controller.isGrounded;
            if (!wasGrounded && IsGrounded && fallSpeed < hardLandingSpeed) LastLandingTime = Time.time;
            IsRunning = input.Run && !IsCrouching && IsGrounded && HorizontalSpeed > walkSpeed + 0.2f;
            if ((flags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f) _verticalVelocity = 0f;

            // Caer fuera del mundo no debe dejar al jugador perdido (spec §6.1).
            if (transform.position.y < _spawnPosition.y - respawnFallDistance) Respawn();
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

        private void UpdateHorizontal(InputFrame input, float dt)
        {
            Vector3 wished = Vector3.zero;
            if (input.Move.sqrMagnitude > 0.0001f)
            {
                float yaw = cameraTransform != null ? cameraTransform.eulerAngles.y : transform.eulerAngles.y;
                Quaternion basis = Quaternion.Euler(0f, yaw, 0f);
                wished = basis * new Vector3(input.Move.x, 0f, input.Move.y);
            }

            float topSpeed = IsCrouching ? crouchSpeed : (input.Run ? runSpeed : walkSpeed);
            Vector3 target = wished * topSpeed;

            float rate = target.sqrMagnitude > _horizontalVelocity.sqrMagnitude ? acceleration : deceleration;
            if (!IsGrounded) rate *= airControl;
            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, target, rate * dt);

            if (wished.sqrMagnitude > 0.0001f)
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
            _controller.enabled = false;
            transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
            _controller.enabled = true;
            _horizontalVelocity = Vector3.zero;
            _verticalVelocity = 0f;
        }
    }
}
