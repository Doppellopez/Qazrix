using System.Collections.Generic;
using Qazrix.Simulation.Movement;
using UnityEngine;

namespace Qazrix.Gameplay
{
    /// <summary>
    /// Espera de la mujer del velo violeta: respira, mueve la cabeza, cambia el peso de pie, mece el velo y parpadea
    /// (<see cref="ContactIdle"/>). El modelo es de primitivas sueltas sin esqueleto, así que al despertar agrupa sus
    /// piezas bajo unos pivotes (cuello, torso, velo de la espalda) y los mueve; las posiciones de reposo quedan como estaban.
    /// </summary>
    public sealed class ContactIdleAnimator : MonoBehaviour
    {
        private const string ModelName = "Contacto_modelo";

        private static readonly string[] HeadParts =
        {
            "Cabeza", "Barbilla", "Nariz", "Boca", "Ojo", "Ceja", "Cabello_flequillo",
            "Velo_cabeza", "Velo_borde", "Velo_cara"
        };

        private static readonly string[] SkirtParts = { "Falda_baja", "Falda_media", "Ribete" };

        private static readonly string[] TorsoParts = { "Busto", "Pecho", "Escote_ribete", "Hombro_izq", "Hombro_der", "Velo_hombros", "Faja" };

        private Transform _model;
        private Transform _torso, _head, _veil, _leftArm, _rightArm;
        private Transform _drape, _skirt;   // gasa que cae sobre el rostro y bajo de la túnica: los mueve el viento
        private Quaternion _drapeRotation, _skirtRotation;
        private Transform _chest;     // busto y pecho: se ensanchan al respirar
        private readonly List<Transform> _eyes = new List<Transform>();
        private readonly List<Vector3> _eyeScales = new List<Vector3>();
        private Vector3 _modelPosition, _chestScale, _torsoPosition;
        private Quaternion _modelRotation, _headRotation, _veilRotation, _leftRotation, _rightRotation;
        private float _offset;
        private bool _ready;

        private void Awake()
        {
            _model = transform.Find(ModelName);
            if (_model == null) return;

            // Cada una empieza en un punto distinto del ciclo: dos instancias no respiran a la vez.
            _offset = Random.value * 20f;

            // Torso (respiración) → cuello (cabeza); el velo de la espalda cuelga de sus hombros.
            _torso = Pivot("Torso_pivote", _model, new Vector3(0f, 1.0f, 0f));
            _head = Pivot("Cuello_pivote", _torso, new Vector3(0f, 1.46f, 0f));
            _veil = Pivot("Velo_pivote", _torso, new Vector3(0f, 1.42f, -0.15f));

            _drape = Pivot("Gasa_pivote", _head, new Vector3(0f, 0.05f, 0.085f)); // borde de arriba de la gasa del rostro
            _skirt = Pivot("Falda_pivote", _model, new Vector3(0f, 0.95f, 0f));     // a la altura de las caderas

            foreach (string name in TorsoParts) Adopt(_model, name, _torso);
            foreach (string name in HeadParts) Adopt(_model, name, _head);
            Adopt(_model, "Velo_cara_caida", _drape);
            foreach (string name in SkirtParts) Adopt(_model, name, _skirt);
            Adopt(_model, "Velo_caida", _veil);
            _leftArm = Adopt(_model, "Brazo_izq", _torso);
            _rightArm = Adopt(_model, "Brazo_der", _torso);

            _chest = _torso.Find("Busto");
            foreach (Transform t in _head.GetComponentsInChildren<Transform>())
                if (t.name == "Ojo") { _eyes.Add(t); _eyeScales.Add(t.localScale); }

            _modelPosition = _model.localPosition;
            _modelRotation = _model.localRotation;
            _torsoPosition = _torso.localPosition;
            _chestScale = _chest != null ? _chest.localScale : Vector3.one;
            _headRotation = _head.localRotation;
            _veilRotation = _veil.localRotation;
            _drapeRotation = _drape.localRotation;
            _skirtRotation = _skirt.localRotation;
            _leftRotation = _leftArm != null ? _leftArm.localRotation : Quaternion.identity;
            _rightRotation = _rightArm != null ? _rightArm.localRotation : Quaternion.identity;
            _ready = true;
        }

        private void Update()
        {
            if (!_ready) return;
            // Viento leve del oeste visto desde ella (está girada hacia el jugador): mece el velo, la gasa y el bajo de la túnica.
            Qazrix.Simulation.World.Wind.RelativeToBody(Time.time, transform.eulerAngles.y, out float windForward, out float windRight);
            ContactIdlePose pose = ContactIdle.Sample(Time.time + _offset, windForward, windRight);

            // Cambio de peso: el cuerpo entero se desplaza y se ladea desde los pies.
            _model.localPosition = _modelPosition + new Vector3(pose.SwayX, 0f, 0f);
            _model.localRotation = _modelRotation * Quaternion.Euler(0f, 0f, pose.SwayRoll);

            // Respiración: los hombros suben un poco y el pecho se ensancha.
            _torso.localPosition = _torsoPosition + new Vector3(0f, pose.BreathLift, 0f);
            if (_chest != null)
                _chest.localScale = new Vector3(_chestScale.x * (1f + pose.BreathExpand), _chestScale.y, _chestScale.z * (1f + pose.BreathExpand));

            _head.localRotation = _headRotation * Quaternion.Euler(pose.HeadPitch, pose.HeadYaw, pose.HeadRoll);
            // Inercia: la aceleración del cuerpo (se desplaza al cambiar el peso, o si la mueven) deja la tela atrás. Se suma al viento,
            // y cada tela sigue el resultado con su resorte: llega con retraso y rebota un poco, no salta.
            MeasureBodyAcceleration(out float accelForward, out float accelRight);
            ClothInertia.Offsets(accelForward, accelRight, out float inertiaLift, out float inertiaRoll, out float inertiaDrape,
                out float inertiaSkirtPitch, out float inertiaSkirtRoll);
            float dt = Time.deltaTime;
            _veilLift.Step(dt, pose.VeilSwing + inertiaLift);
            _veilRoll.Step(dt, pose.VeilRoll + inertiaRoll);
            _drapeSwing.Step(dt, pose.DrapeSwing + inertiaDrape);
            _skirtPitch.Step(dt, pose.SkirtPitch + inertiaSkirtPitch);
            _skirtRoll.Step(dt, pose.SkirtRoll + inertiaSkirtRoll);
            _veil.localRotation = _veilRotation * Quaternion.Euler(_veilLift.Value, 0f, _veilRoll.Value);
            _drape.localRotation = _drapeRotation * Quaternion.Euler(_drapeSwing.Value, 0f, 0f);
            _skirt.localRotation = _skirtRotation * Quaternion.Euler(_skirtPitch.Value, 0f, _skirtRoll.Value);
            if (_leftArm != null) _leftArm.localRotation = _leftRotation * Quaternion.Euler(pose.LeftArm, 0f, 0f);
            // Al ofrecer la llave el brazo derecho sube hacia delante (giro negativo en X) y el vaivén de espera se apaga.
            if (_rightArm != null)
                _rightArm.localRotation = _rightRotation * Quaternion.Euler(pose.RightArm * (1f - OfferWeight) - KeyHandoverCycle.OfferArmDegrees * OfferWeight, 0f, 0f);

            // Parpadeo: los ojos se aplastan casi del todo.
            for (int i = 0; i < _eyes.Count; i++)
            {
                Vector3 s = _eyeScales[i];
                _eyes[i].localScale = new Vector3(s.x, s.y * (1f - 0.9f * pose.Blink), s.z);
            }
        }

        // Telas con resorte: el velo de la espalda (levantado y ladeado), la gasa del rostro y el bajo de la túnica.
        private readonly ClothSpring _veilLift = new ClothSpring(45f, 6f), _veilRoll = new ClothSpring(40f, 5.5f);
        private readonly ClothSpring _drapeSwing = new ClothSpring(70f, 7f);
        private readonly ClothSpring _skirtPitch = new ClothSpring(55f, 8f), _skirtRoll = new ClothSpring(50f, 8f);

        /// <summary>Cuánto tiene el brazo derecho extendido ofreciendo la llave (0 = en reposo, 1 = del todo); lo mueve <see cref="KeyHandover"/>.</summary>
        public float OfferWeight { get; set; }

        /// <summary>La mano derecha de Zahra, donde sostiene la llave; null si el modelo aún no está montado.</summary>
        public Transform RightHand => _rightArm != null ? _rightArm.Find("Mano") : null;

        private Vector3 _lastBodyPosition, _bodyVelocity;
        private float _accelForward, _accelRight, _lastForward, _lastRight;
        private bool _hasLastBody;

        /// <summary>
        /// Aceleración del cuerpo en su propio sistema (hacia delante y hacia su derecha, m/s²), suavizada para que los
        /// fotogramas sueltos no sacudan la tela; mide la posición del modelo, así que incluye el balanceo al cambiar de pie.
        /// </summary>
        private void MeasureBodyAcceleration(out float forward, out float right)
        {
            forward = right = 0f;
            float dt = Time.deltaTime;
            Vector3 position = _model.position;
            if (_hasLastBody && dt > 0f)
            {
                Vector3 velocity = (position - _lastBodyPosition) / dt;
                if (velocity.magnitude > 60f)
                {
                    // Teletransporte o recolocación: no cuenta como aceleración, la tela no recibe sacudida.
                    _accelForward = _accelRight = _lastForward = _lastRight = 0f;
                }
                else
                {
                    Vector3 local = transform.InverseTransformDirection(velocity);
                    float k = Mathf.Clamp01(dt * 25f), ka = Mathf.Clamp01(dt * 12f);
                    float f = Mathf.Lerp(_lastForward, local.z, k), r = Mathf.Lerp(_lastRight, local.x, k);
                    _accelForward = Mathf.Lerp(_accelForward, Mathf.Clamp((f - _lastForward) / dt, -40f, 40f), ka);
                    _accelRight = Mathf.Lerp(_accelRight, Mathf.Clamp((r - _lastRight) / dt, -40f, 40f), ka);
                    _lastForward = f;
                    _lastRight = r;
                }
            }
            _lastBodyPosition = position;
            _hasLastBody = true;
            forward = _accelForward;
            right = _accelRight;
        }

        private static Transform Pivot(string name, Transform parent, Vector3 localPosition)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            return t;
        }

        /// <summary>Cuelga de <paramref name="pivot"/> todas las piezas hijas del modelo con ese nombre, sin moverlas del sitio.</summary>
        private static Transform Adopt(Transform model, string name, Transform pivot)
        {
            Transform first = null;
            for (int i = model.childCount - 1; i >= 0; i--)
            {
                Transform child = model.GetChild(i);
                if (child.name != name) continue;
                child.SetParent(pivot, true);
                if (first == null) first = child;
            }
            return first;
        }
    }
}
