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

        private float _yaw;
        private float _pitch = 18f;
        private float _currentDistance;
        private Vector3 _pivot;
        private Vector3 _pivotVelocity;

        public void Configure(Transform followTarget)
        {
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

            PrototypeInputFrame input = PrototypeInput.Read();
            UpdateCursor(input);

            float dt = Time.deltaTime;
            Vector2 look = input.LookStick * (stickDegreesPerSecond * dt);
            if (Cursor.lockState == CursorLockMode.Locked) look += input.LookMouse * mouseDegreesPerPixel;

            _yaw += look.x;
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

        private static void UpdateCursor(PrototypeInputFrame input)
        {
            if (input.ReleaseCursorPressed)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (input.ClickPressed && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}
