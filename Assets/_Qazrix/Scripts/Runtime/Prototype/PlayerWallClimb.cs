using Qazrix.Simulation.AI;
using Qazrix.Simulation.Movement;
using Qazrix.World;
using UnityEngine;

namespace Qazrix.Prototype
{
    /// <summary>
    /// Trepada libre: corriendo contra la fachada de un edificio el jugador se agarra, sube o baja con
    /// la palanca y, al llegar al borde, se encarama al tejado. Las reglas están en
    /// <see cref="ClimbRules"/>; aquí se mide la pared y se mueve el cuerpo. El controlador del
    /// jugador decide cuándo intentarlo y le pasa la entrada.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerWallClimb : MonoBehaviour
    {
        [SerializeField] private ClimbLimits limits = new ClimbLimits();
        [SerializeField] private float reach = 0.45f;        // metros desde el borde del cuerpo
        [SerializeField] private float wallGap = 0.06f;      // separación entre el cuerpo y la pared
        [SerializeField] private float roofInset = 0.45f;    // cuánto entra en el tejado al encaramarse
        [SerializeField] private LayerMask wallMask = ~0;

        /// <summary>Lo que cuenta como pared o suelo al trepar: todo menos las losas de los matacanes, que se pisan pero no se escalan.</summary>
        private int ClimbMask => wallMask.value & ~(1 << Qazrix.World.CityBlocks.MachicolationLayer);

        private CharacterController _controller;
        private Vector3 _into;          // dirección horizontal hacia dentro de la pared
        private bool _mantling;
        private Vector3 _mantleFrom, _mantleTo;
        private float _mantleProgress;

        public bool IsActive { get; private set; }

        /// <summary>Está pasando del borde de la pared al tejado.</summary>
        public bool IsMantling => IsActive && _mantling;

        /// <summary>Avance de ese paso, de 0 a 1.</summary>
        public float MantleProgress => _mantling ? Mathf.Clamp01(_mantleProgress) : 0f;

        /// <summary>La última trepada terminó de pie (en el tejado o en el suelo), no soltándose.</summary>
        public bool EndedStanding { get; private set; }

        public int RoofsReached { get; private set; }

        /// <summary>La última trepada terminó con un salto hacia atrás (y no soltándose ni en el tejado).</summary>
        public bool EndedWithBackJump { get; private set; }

        /// <summary>Velocidad con la que sale ese salto (válida si <see cref="EndedWithBackJump"/>).</summary>
        public Vector3 BackJumpVelocity { get; private set; }

        public int BackJumpsDone { get; private set; }

        private float _noGrabUntil;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        /// <summary>Se agarra a la pared que tiene delante si las reglas lo permiten.</summary>
        /// <param name="wished">Dirección horizontal hacia la que el jugador quiere ir (cero si no empuja).</param>
        public bool TryBegin(Vector3 wished, bool wantsRun, bool isCrouching)
        {
            if (IsActive || !wantsRun || wished.sqrMagnitude < 0.25f) return false;
            if (Time.time < _noGrabUntil) return false; // recién saltado hacia atrás: no se vuelve a agarrar al instante
            wished.y = 0f;
            wished.Normalize();
            if (!FindWall(transform.position, wished, _controller.radius + reach, out RaycastHit wall)) return false;

            Vector3 into = -wall.normal;
            into.y = 0f;
            if (into.sqrMagnitude < 0.01f) return false;
            into.Normalize();

            if (!ClimbRules.CanGrab(wantsRun, true, isCrouching, IsClimbable(wall.collider),
                    Vector3.Dot(into, wished), wall.normal.y, limits))
                return false;

            _into = into;
            _mantling = false;
            IsActive = true;
            EndedStanding = false;
            EndedWithBackJump = false;
            _controller.enabled = false; // mientras trepa el cuerpo va pegado a la pared, no lo mueve la física
            SnapTo(wall);
            return true;
        }

        /// <summary>
        /// Avanza la trepada. <paramref name="move"/>: x de lado, y arriba/abajo. Con
        /// <paramref name="jumpPressed"/> salta hacia atrás, separándose de la pared.
        /// </summary>
        public void Tick(float deltaTime, Vector2 move, bool letGo, bool jumpPressed = false)
        {
            if (!IsActive) return;

            if (ClimbRules.CanJumpBack(true, _mantling, jumpPressed))
            {
                Float3 v = ClimbRules.BackJumpVelocity(ToFloat3(_into), limits);
                BackJumpVelocity = new Vector3(v.X, v.Y, v.Z);
                // De cara al sitio al que salta, algo separado de la pared para no quedar dentro de ella.
                transform.rotation = Quaternion.LookRotation(-_into, Vector3.up);
                transform.position += -_into * 0.1f;
                BackJumpsDone++;
                _noGrabUntil = Time.time + 0.4f;
                End(false);
                EndedWithBackJump = true;
                return;
            }

            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(_into, Vector3.up), 720f * deltaTime);

            if (_mantling)
            {
                _mantleProgress += deltaTime / Mathf.Max(limits.MantleSeconds, 0.05f);
                Float3 p = ClimbRules.Mantle(ToFloat3(_mantleFrom), ToFloat3(_mantleTo), _mantleProgress);
                transform.position = new Vector3(p.X, p.Y, p.Z);
                if (_mantleProgress >= 1f)
                {
                    transform.position = _mantleTo;
                    RoofsReached++;
                    End(true);
                }
                return;
            }

            if (letGo)
            {
                End(false);
                return;
            }

            Vector3 position = transform.position;

            // De lado, siguiendo la pared, solo si sigue habiendo pared y sitio para el cuerpo.
            if (Mathf.Abs(move.x) > 0.2f)
            {
                Vector3 side = Vector3.Cross(Vector3.up, _into) * (move.x * limits.SideSpeed * deltaTime);
                Vector3 candidate = position + side;
                if (FindWall(candidate, _into, _controller.radius + reach + 0.6f, out RaycastHit beside)
                    && IsClimbable(beside.collider) && HasRoomAt(candidate))
                    position = candidate;
            }

            float vertical = ClimbRules.VerticalSpeed(move.y, limits) * deltaTime;
            if (vertical < 0f && Physics.Raycast(position + Vector3.up * 0.3f, Vector3.down, out RaycastHit floor,
                    0.3f - vertical + 0.05f, ClimbMask, QueryTriggerInteraction.Ignore))
            {
                // Ha llegado al suelo bajando.
                transform.position = new Vector3(position.x, floor.point.y, position.z);
                End(true);
                return;
            }
            position.y += vertical;

            if (FindWall(position, _into, _controller.radius + reach + 0.6f, out RaycastHit wall) && IsClimbable(wall.collider))
            {
                transform.position = position;
                SnapTo(wall);
                return;
            }

            // Ya no hay pared a la altura de las manos: es el borde. Se busca el tejado para subirse.
            Vector3 over = position + _into * (_controller.radius + wallGap + roofInset);
            if (vertical >= 0f && Physics.Raycast(over + Vector3.up * (limits.GrabHeight + 1.2f), Vector3.down, out RaycastHit roof,
                    2.6f, ClimbMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 landing = new Vector3(over.x, roof.point.y, over.z);
                if (roof.normal.y > 0.7f && HasRoomAt(landing))
                {
                    _mantling = true;
                    _mantleProgress = 0f;
                    _mantleFrom = position;
                    _mantleTo = landing;
                    return;
                }
            }
            // Sin tejado donde ponerse (o la pared se acabó de lado): se queda donde estaba.
        }

        /// <summary>Suelta la pared sin más (reaparición, captura).</summary>
        public void Cancel()
        {
            if (IsActive) End(false);
        }

        private void End(bool standing)
        {
            IsActive = false;
            _mantling = false;
            EndedStanding = standing;
            _controller.enabled = true;
        }

        /// <summary>Pared a la altura de las manos, en la dirección dada.</summary>
        private bool FindWall(Vector3 feet, Vector3 direction, float distance, out RaycastHit hit)
        {
            Vector3 origin = feet + Vector3.up * limits.GrabHeight - direction * 0.05f;
            return Physics.Raycast(origin, direction, out hit, distance + 0.05f, ClimbMask, QueryTriggerInteraction.Ignore);
        }

        private void SnapTo(RaycastHit wall)
        {
            Vector3 p = wall.point - _into * (_controller.radius + wallGap);
            transform.position = new Vector3(p.x, transform.position.y, p.z);
        }

        private static bool IsClimbable(Collider surface)
        {
            var blocks = surface.GetComponentInParent<CityBlocks>();
            return blocks != null && blocks.Climbable;
        }

        private bool HasRoomAt(Vector3 feet)
        {
            float radius = _controller.radius * 0.8f;
            Vector3 bottom = feet + Vector3.up * (radius + 0.1f);
            Vector3 top = feet + Vector3.up * (_controller.height - radius);
            return !Physics.CheckCapsule(bottom, top, radius, ClimbMask, QueryTriggerInteraction.Ignore);
        }

        private static Float3 ToFloat3(Vector3 v) => new Float3(v.x, v.y, v.z);
    }
}
