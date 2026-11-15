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

        /// <summary>Ángulo actual de la pierna izquierda, en grados (para pruebas).</summary>
        public float LeftLegAngle => _leftLeg;

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

            // Con el CharacterController desactivado (salto de obstáculo, trepada) o sin suelo, pose de aire.
            bool airborne = body != null && (!body.enabled || !body.isGrounded);

            float targetLeftLeg, targetRightLeg, targetLeftArm, targetRightArm;
            if (airborne)
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
            if (!swingRightArm) targetRightArm = 0f;

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
