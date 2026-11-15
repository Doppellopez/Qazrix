using Qazrix.Simulation.Movement;
using UnityEngine;

namespace Qazrix.Prototype
{
    /// <summary>
    /// Mueve piernas y brazos de un personaje según lo que se desplaza. No depende de quién lo mueva
    /// (jugador o guardia): mide la velocidad a partir del cambio de posición de la raíz. Los ángulos
    /// los da <see cref="WalkCycle"/>; aquí solo se aplican a los pivotes de cadera y hombro.
    /// </summary>
    public sealed class CharacterWalkAnimator : MonoBehaviour
    {
        [SerializeField] private Transform root;
        [SerializeField] private CharacterController body;
        [SerializeField] private Transform leftLeg;
        [SerializeField] private Transform rightLeg;
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;
        [SerializeField] private bool swingRightArm = true;  // falso si lleva algo en la mano derecha
        [SerializeField] private float airLegAngle = 18f;     // piernas algo recogidas en el aire
        [SerializeField] private float blendDegreesPerSecond = 540f;
        [SerializeField] private float maxSpeed = 12f;        // descarta teletransportes y reapariciones

        private Vector3 _lastPosition;
        private bool _hasLast;
        private float _phase;
        private float _leftLeg, _rightLeg, _leftArm, _rightArm;
        private PlayerWallClimb _climb;
        private bool _lookedForClimb;
        private float _climbPhase;
        private float _lastHeight;
        private bool _hasLastHeight;

        /// <summary>Ángulo actual de la pierna izquierda, en grados (para pruebas).</summary>
        public float LeftLegAngle => _leftLeg;

        public float LeftArmAngle => _leftArm;

        public float RightArmAngle => _rightArm;

        public float RightLegAngle => _rightLeg;

        public void Configure(Transform characterRoot, CharacterController controller,
            Transform legLeft, Transform legRight, Transform armLeft, Transform armRight, bool rightArmSwings = true)
        {
            root = characterRoot;
            body = controller;
            leftLeg = legLeft;
            rightLeg = legRight;
            leftArm = armLeft;
            rightArm = armRight;
            swingRightArm = rightArmSwings;
        }

        private void LateUpdate()
        {
            if (root == null || Time.deltaTime <= 0f) return;

            Vector3 position = root.position;
            float speed = 0f;
            if (_hasLast)
            {
                Vector3 moved = position - _lastPosition;
                moved.y = 0f;
                speed = moved.magnitude / Time.deltaTime;
                if (speed > maxSpeed) speed = 0f;
            }
            _lastPosition = position;
            _hasLast = true;

            // Con el CharacterController desactivado (salto de obstáculo) o sin suelo, pose de aire.
            bool airborne = body != null && (!body.enabled || !body.isGrounded);
            if (!_lookedForClimb)
            {
                _climb = root.GetComponent<PlayerWallClimb>(); // solo el jugador trepa fachadas
                _lookedForClimb = true;
            }
            bool climbing = _climb != null && _climb.IsActive;
            float climbed = _hasLastHeight ? position.y - _lastHeight : 0f;
            _lastHeight = position.y;
            _hasLastHeight = true;

            float targetLeftLeg, targetRightLeg, targetLeftArm, targetRightArm;
            bool bothArms = false;
            if (climbing && _climb.IsMantling)
            {
                // Encaramándose al tejado: los brazos empujan hacia abajo y una pierna sube al borde.
                float t = _climb.MantleProgress;
                targetLeftArm = targetRightArm = ClimbCycle.MantleArmAngle(t);
                targetLeftLeg = ClimbCycle.MantleLegAngle(t, true);
                targetRightLeg = ClimbCycle.MantleLegAngle(t, false);
                bothArms = true;
            }
            else if (climbing)
            {
                // Trepando: brazos arriba alternándose, al ritmo de lo que el cuerpo se mueve por la pared.
                float along = Mathf.Abs(climbed) + speed * Time.deltaTime;
                if (along < maxSpeed * Time.deltaTime) _climbPhase = ClimbCycle.Advance(_climbPhase, along);
                targetLeftArm = ClimbCycle.ArmAngle(_climbPhase, true);
                targetRightArm = ClimbCycle.ArmAngle(_climbPhase, false);
                targetLeftLeg = ClimbCycle.LegAngle(_climbPhase, true);
                targetRightLeg = ClimbCycle.LegAngle(_climbPhase, false);
                bothArms = true;
            }
            else if (airborne)
            {
                targetLeftLeg = airLegAngle;
                targetRightLeg = -airLegAngle * 0.5f;
                targetLeftArm = -airLegAngle;
                targetRightArm = -airLegAngle;
            }
            else
            {
                _phase = WalkCycle.Advance(_phase, speed, Time.deltaTime);
                targetLeftLeg = WalkCycle.LegAngle(_phase, speed);
                targetRightLeg = -targetLeftLeg;
                targetLeftArm = WalkCycle.ArmAngle(_phase, speed);
                targetRightArm = -targetLeftArm;
            }
            if (!swingRightArm && !bothArms) targetRightArm = 0f; // para trepar hacen falta las dos manos

            float step = blendDegreesPerSecond * Time.deltaTime;
            _leftLeg = Mathf.MoveTowards(_leftLeg, targetLeftLeg, step);
            _rightLeg = Mathf.MoveTowards(_rightLeg, targetRightLeg, step);
            _leftArm = Mathf.MoveTowards(_leftArm, targetLeftArm, step);
            _rightArm = Mathf.MoveTowards(_rightArm, targetRightArm, step);

            // Girar en X negativo lleva el extremo de la extremidad hacia delante (+Z).
            Apply(leftLeg, _leftLeg);
            Apply(rightLeg, _rightLeg);
            Apply(leftArm, _leftArm);
            Apply(rightArm, _rightArm);
        }

        private static void Apply(Transform pivot, float forwardDegrees)
        {
            if (pivot != null) pivot.localRotation = Quaternion.Euler(-forwardDegrees, 0f, 0f);
        }
    }
}
