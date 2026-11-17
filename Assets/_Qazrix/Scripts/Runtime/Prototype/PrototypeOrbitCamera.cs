using Qazrix.Input;
using Qazrix.Simulation.Movement;
using UnityEngine;

namespace Qazrix.Prototype
{
    /// <summary>
    /// Cámara orbital propia (D14): gira alrededor del jugador con ratón o stick derecho,
    /// hace zoom y se acerca cuando una pared se interpone, para no atravesarla.
    /// </summary>
    public sealed class PrototypeOrbitCamera : MonoBehaviour
    {
        [Header("Referencia (la asigna PrototypeSceneBuilder)")]
        [SerializeField] private Transform target;
        [SerializeField] private QazrixInput inputReader;

        [Header("Encuadre")]
        [SerializeField] private float pivotHeight = 1.5f;
        [SerializeField] private float distance = 4.5f;
        [SerializeField] private float minDistance = 1.5f;
        [SerializeField] private float maxDistance = 8f;
        [SerializeField] private float minPitch = -30f;
        [SerializeField] private float maxPitch = 70f;

        [Header("Sensibilidad")]
        [SerializeField] private float mouseDegreesPerPixel = 0.12f;
        [SerializeField] private float stickDegreesPerSecond = 160f;
        [SerializeField] private float zoomSpeed = 6f;
        [SerializeField] private bool invertY;

        [Header("Colisión")]
        [SerializeField] private float collisionRadius = 0.25f;
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField] private float followSmoothTime = 0.06f;
        [SerializeField] private float distanceRecoverSpeed = 6f;

        [Header("Seguir hacia donde mira")]
        [SerializeField] private bool followFacing = true;
        [SerializeField] private float followSmoothSeconds = 0.35f; // cuanto menor, más rápido se coloca detrás
        [SerializeField] private float manualHoldSeconds = 1.5f;    // tras mover la cámara a mano, no se recoloca

        [Header("Media vuelta al pulsar atrás")]
        [SerializeField] private bool turnAround = true;
        [SerializeField] private float turnAroundDegreesPerSecond = 90f; // 180° en unos 2 s

        private float _yaw;
        private float _yawVelocity;
        private bool _turning, _latched, _backWasPressed;
        private float _turnTarget, _latchedYaw;
        private float _sinceManualLook = 999f;
        private float _pitch = 18f;
        private float _currentDistance;
        private Vector3 _pivot;
        private Vector3 _pivotVelocity;

        /// <summary>En este fotograma la cámara se está colocando detrás de donde mira el personaje.</summary>
        public bool IsFollowingFacing { get; private set; }

        public float FollowSmoothSeconds => followSmoothSeconds;

        /// <summary>La cámara está dando la media vuelta que provoca pulsar «atrás».</summary>
        public bool IsTurningAround => _turning;

        /// <summary>Orientación con la que el jugador interpreta WASD (ver <see cref="CameraFollowRules.MovementYaw"/>).</summary>
        public float MovementYaw => CameraFollowRules.MovementYaw(_yaw, _latchedYaw, _latched);

        public void Configure(Transform followTarget, QazrixInput reader)
        {
            inputReader = reader;
            target = followTarget;
        }

        private void Start()
        {
            _currentDistance = distance;
            if (target != null)
            {
                _yaw = target.eulerAngles.y;
                _pivot = target.position + Vector3.up * pivotHeight;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            InputFrame input = inputReader != null ? inputReader.Read() : default;
            bool blocked = inputReader != null && inputReader.GameplayBlocked;
            if (!blocked) UpdateCursor(input); // con un panel abierto el cursor lo gestiona el panel

            float dt = Time.deltaTime;
            Vector2 look = input.LookStick * (stickDegreesPerSecond * dt);
            if (Cursor.lockState == CursorLockMode.Locked) look += input.LookPointer * mouseDegreesPerPixel;

            _yaw += look.x;

            // Una cámara movida a mano cancela la media vuelta en curso.
            if (look.sqrMagnitude > 0.0001f) { _sinceManualLook = 0f; _turning = false; }
            else _sinceManualLook += dt;

            // Pulsar «atrás»: la cámara da media vuelta despacio y se queda ahí; el personaje se da la vuelta con ella
            // porque WASD se sigue interpretando con la orientación de antes mientras se mantenga «atrás».
            bool backPressed = input.Move.y < -CameraFollowRules.Deadzone;
            if (!backPressed) _latched = false;
            if (CameraFollowRules.ShouldStartTurnAround(turnAround, blocked, _sinceManualLook, manualHoldSeconds,
                    input.Move.y, _backWasPressed, _turning))
            {
                _turning = true;
                _turnTarget = _yaw + 180f;
                _latchedYaw = _yaw;
                _latched = true;
            }
            _backWasPressed = backPressed;

            if (_turning)
            {
                _yaw = CameraFollowRules.TurnAroundStep(_yaw, _turnTarget, turnAroundDegreesPerSecond, dt);
                if (Mathf.Approximately(_yaw, _turnTarget)) _turning = false;
            }

            // La cámara se coloca detrás de donde mira el personaje, salvo que el jugador la haya movido a mano hace poco.
            IsFollowingFacing = !_turning && CameraFollowRules.ShouldFollow(followFacing, blocked, _sinceManualLook, manualHoldSeconds,
                input.Move.y, input.Tank.x, input.Tank.y);
            if (IsFollowingFacing)
                _yaw = Mathf.SmoothDampAngle(_yaw, target.eulerAngles.y, ref _yawVelocity, followSmoothSeconds);
            else
                _yawVelocity = 0f;

            _pitch = Mathf.Clamp(_pitch + (invertY ? look.y : -look.y), minPitch, maxPitch);
            distance = Mathf.Clamp(distance - input.Zoom * zoomSpeed * dt * 10f, minDistance, maxDistance);

            Vector3 wantedPivot = target.position + Vector3.up * pivotHeight;
            _pivot = Vector3.SmoothDamp(_pivot, wantedPivot, ref _pivotVelocity, followSmoothTime);

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 back = rotation * Vector3.back;

            // Si algo tapa la línea entre el jugador y la cámara, la cámara se acerca de golpe
            // y se aleja despacio cuando vuelve a haber sitio.
            float allowed = distance;
            if (Physics.SphereCast(_pivot, collisionRadius, back, out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(target))
            {
                allowed = Mathf.Max(hit.distance, 0.3f);
            }

            _currentDistance = allowed < _currentDistance
                ? allowed
                : Mathf.MoveTowards(_currentDistance, allowed, distanceRecoverSpeed * dt);

            transform.SetPositionAndRotation(_pivot + back * _currentDistance, rotation);
        }

        private static void UpdateCursor(InputFrame input)
        {
            if (input.ReleaseCursorPressed)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (input.CaptureCursorPressed && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}
