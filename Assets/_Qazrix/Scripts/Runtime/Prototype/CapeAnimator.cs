using Qazrix.Simulation.Movement;
using UnityEngine;

namespace Qazrix.Prototype
{
    /// <summary>
    /// Mueve la capa del hidalgo. Mide el desplazamiento de la raíz del personaje (hacia delante, de lado
    /// y en vertical, sin depender de quién lo mueve) y se lo pasa a <see cref="CapeSim"/>, que calcula el
    /// ángulo de cada tramo articulado. Aquí solo se aplican esos ángulos a los pivotes.
    /// </summary>
    public sealed class CapeAnimator : MonoBehaviour
    {
        [SerializeField] private Transform root;
        [SerializeField] private Transform[] segments;
        [SerializeField] private Transform leftLeg;    // pivotes de cadera: si una pierna va hacia atrás, la capa se aparta
        [SerializeField] private Transform rightLeg;
        [SerializeField] private float maxSpeed = 60f; // descarta teletransportes y reapariciones

        private readonly CapeSim _sim = new CapeSim();
        private Vector3 _lastPosition;
        private bool _hasLast;
        private float _forward, _vertical, _lateral;
        private float _forwardAccel, _verticalAccel, _lateralAccel;

        /// <summary>Ángulo de separación del tramo (0 = arriba, 2 = el borde de abajo), en grados.</summary>
        public float Lift(int segment) => _sim.Lift(segment);

        public float Sway(int segment) => _sim.Sway(segment);

        public int SegmentCount => segments != null ? segments.Length : 0;

        public void Configure(Transform characterRoot, Transform[] capeSegments, Transform legLeft = null, Transform legRight = null)
        {
            root = characterRoot;
            segments = capeSegments;
            leftLeg = legLeft;
            rightLeg = legRight;
        }

        /// <summary>Grados que la pierna más retrasada está echada hacia atrás (0 si ninguna lo está).</summary>
        private float LegBackSwing()
        {
            // CharacterWalkAnimator gira el pivote en X negativo para ir hacia delante: delante = -x.
            float back = 0f;
            if (leftLeg != null) back = Mathf.Max(back, Mathf.DeltaAngle(0f, leftLeg.localEulerAngles.x));
            if (rightLeg != null) back = Mathf.Max(back, Mathf.DeltaAngle(0f, rightLeg.localEulerAngles.x));
            return back;
        }

        private void LateUpdate()
        {
            if (root == null || segments == null || Time.deltaTime <= 0f) return;

            Vector3 position = root.position;
            if (_hasLast)
            {
                Vector3 velocity = (position - _lastPosition) / Time.deltaTime;
                if (velocity.magnitude > maxSpeed)
                {
                    _sim.Reset(); // teletransporte o reaparición
                    _forward = _vertical = _lateral = 0f;
                    _forwardAccel = _verticalAccel = _lateralAccel = 0f;
                }
                else
                {
                    Vector3 local = root.InverseTransformDirection(velocity);
                    // Un poco suavizado: la cámara de fotogramas sueltos no debe sacudir la capa.
                    float k = Mathf.Clamp01(Time.deltaTime * 25f);
                    float forward = Mathf.Lerp(_forward, local.z, k);
                    float lateral = Mathf.Lerp(_lateral, local.x, k);
                    float vertical = Mathf.Lerp(_vertical, velocity.y, k);
                    // Aceleración (inercia): cambio de la velocidad suavizada, también suavizado para no dar sacudidas.
                    float ka = Mathf.Clamp01(Time.deltaTime * 12f);
                    _forwardAccel = Mathf.Lerp(_forwardAccel, Mathf.Clamp((forward - _forward) / Time.deltaTime, -40f, 40f), ka);
                    _lateralAccel = Mathf.Lerp(_lateralAccel, Mathf.Clamp((lateral - _lateral) / Time.deltaTime, -40f, 40f), ka);
                    _verticalAccel = Mathf.Lerp(_verticalAccel, Mathf.Clamp((vertical - _vertical) / Time.deltaTime, -60f, 60f), ka);
                    _forward = forward;
                    _lateral = lateral;
                    _vertical = vertical;
                }
            }
            _lastPosition = position;
            _hasLast = true;

            // Viento leve del oeste: el aire que nota la capa es el que sopla menos el que provoca su propio avance. Con viento a
            // favor queda pegada a la espalda; de cara o de lado se levanta y ondea aunque esté parado.
            Qazrix.Simulation.World.Wind.RelativeToBody(Time.time, root.eulerAngles.y, out float windForward, out float windRight);

            _sim.Step(Time.deltaTime, _forward - windForward, _vertical, _lateral - windRight, LegBackSwing(), _forwardAccel, _verticalAccel, _lateralAccel);
            for (int i = 0; i < segments.Length && i < CapeSim.Segments; i++)
            {
                if (segments[i] != null) segments[i].localRotation = Quaternion.Euler(_sim.Lift(i), 0f, _sim.Sway(i));
            }
        }
    }
}
