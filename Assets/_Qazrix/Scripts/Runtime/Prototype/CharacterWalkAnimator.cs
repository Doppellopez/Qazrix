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
        [SerializeField] private float airGraceSeconds = 0.06f;      // no cambia de pose por perder el suelo un instante
        [SerializeField] private float landingMinAirSeconds = 0.25f; // aire mínimo para amortiguar al caer
        [SerializeField] private float blendDegreesPerSecond = 540f;
        [SerializeField] private float maxSpeed = 12f;        // descarta teletransportes y reapariciones

        private Vector3 _lastPosition;
        private bool _hasLast;
        private float _phase;
        private float _leftLeg, _rightLeg, _leftArm, _rightArm;
        [SerializeField] private Transform weaponGrip; // puño del arma (solo el guardia): sube con el brazo
        private float _raise;

        /// <summary>Pide el brazo y el arma en alto (guardia persiguiendo) o abajo.</summary>
        public bool WeaponRaised { get; set; }

        /// <summary>Cuánto está en alto el arma, de 0 (abajo) a 1 (arriba del todo).</summary>
        public float WeaponRaiseWeight => _raise;

        /// <summary>Indica qué nodo es el puño del arma que acompaña al brazo derecho.</summary>
        public void SetWeapon(Transform grip) => weaponGrip = grip;
        private PlayerWallClimb _climb;
        private PlayerFaithLeap _leap;
        private bool _lookedForClimb;
        private float _climbPhase;
        private float _lastHeight;
        private bool _hasLastHeight;
        private float _verticalSpeed;
        private float _airSeconds;
        private float _sinceLanding = -1f;

        /// <summary>¿Está con la postura de salto o caída?</summary>
        public bool IsAirPose { get; private set; }

        /// <summary>¿Está amortiguando un aterrizaje?</summary>
        public bool IsLandingPose { get; private set; }

        /// <summary>Velocidad vertical que usa la animación, en m/s (para pruebas).</summary>
        public float VerticalSpeed => _verticalSpeed;

        /// <summary>Ángulo actual de la pierna izquierda, en grados (para pruebas).</summary>
        public float LeftLegAngle => _leftLeg;

        public float LeftArmAngle => _leftArm;

        public float RightArmAngle => _rightArm;

        public float RightLegAngle => _rightLeg;

        // Articulaciones y cuerpo para la carrera (se buscan por nombre bajo cada pierna y brazo: "Rodilla", "Codo").
        [SerializeField] private Transform bodyNode; // nodo que se inclina y rebota al correr (el modelo)
        private Transform _leftKnee, _rightKnee, _leftElbow, _rightElbow;
        private bool _jointsFound;
        private float _leftKneeAngle, _rightKneeAngle, _elbowAngle, _lean, _lift;
        private Vector3 _bodyBasePosition;
        private Quaternion _bodyBaseRotation;
        private bool _bodyBaseKnown;

        // Tajo del guardia y muerte del jugador.
        private float _strikeSeconds = -1f;
        private PrototypePlayerController _playerBody;
        private bool _lookedForPlayerBody, _wasDead;
        private Vector3 _hostBasePosition;
        private Quaternion _hostBaseRotation;

        /// <summary>Descarga un tajo con la cimitarra: el brazo baja de golpe (guardia que alcanza al jugador).</summary>
        public void PlayStrike() => _strikeSeconds = 0f;

        // Agacharse a recoger algo del suelo (PickupCycle).
        private float _pickupSeconds = -1f;

        /// <summary>Empieza el gesto de agacharse a recoger algo del suelo.</summary>
        public void PlayPickup() => _pickupSeconds = 0f;

        /// <summary>¿Está agachándose a recoger algo?</summary>
        public bool IsPickingUp => _pickupSeconds >= 0f;

        // Recibir la llave de Zahra: alarga la mano derecha, la lleva al pecho y la guarda en la camisa (KeyHandoverCycle).
        private float _keySeconds = -1f;

        /// <summary>Empieza el gesto de tomar la llave y guardarla en la camisa con la mano derecha.</summary>
        public void PlayKeyHandover() => _keySeconds = 0f;

        // Dar la llave al capitán: se la saca de la camisa y alarga la mano derecha hacia él (KeyGiveCycle).
        private float _giveSeconds = -1f;

        /// <summary>Empieza el gesto de sacar la llave de la camisa y dársela a alguien con la mano derecha.</summary>
        public void PlayKeyGive() => _giveSeconds = 0f;

        /// <summary>¿Está dando la llave?</summary>
        public bool IsGivingKey => _giveSeconds >= 0f;

        /// <summary>Segundos desde que empezó a dar la llave; -1 si no está en ello.</summary>
        public float GiveSeconds => _giveSeconds;

        /// <summary>¿Está recibiendo la llave?</summary>
        public bool IsReceivingKey => _keySeconds >= 0f;

        /// <summary>Segundos desde que empezó la entrega de la llave; -1 si no está en ella.</summary>
        public float KeySeconds => _keySeconds;

        private Transform _rightHand;

        /// <summary>La mano derecha del modelo (bajo el codo); null si el modelo no la tiene.</summary>
        public Transform RightHand
        {
            get
            {
                if (_rightHand == null && rightArm != null)
                {
                    Transform elbow = rightArm.Find("Codo");
                    if (elbow != null) _rightHand = elbow.Find("Mano");
                }
                return _rightHand;
            }
        }

        /// <summary>Segundos desde que empezó el gesto de recoger; -1 si no está en él.</summary>
        public float PickupSeconds => _pickupSeconds;

        /// <summary>¿Está en pleno tajo? (para pruebas)</summary>
        public bool IsStriking => _strikeSeconds >= 0f;

        /// <summary>¿Está mostrando la postura de muerte? (para pruebas)</summary>
        public bool IsDeathPose { get; private set; }

        /// <summary>Indica el nodo del modelo que se inclina hacia delante y rebota al correr.</summary>
        public void SetBody(Transform body)
        {
            bodyNode = body;
            _bodyBaseKnown = false;
        }

        /// <summary>Fuerza de la carrera ahora mismo, de 0 (andando) a 1 (corriendo): para pruebas.</summary>
        public float RunBlend { get; private set; }

        /// <summary>Doblez actual de la rodilla izquierda y del codo, en grados (para pruebas).</summary>
        public float LeftKneeAngle => _leftKneeAngle;
        public float ElbowAngle => _elbowAngle;
        public float LeanAngle => _lean;

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

            if (UpdateDeathPose()) return;

            // Con el CharacterController desactivado (salto de obstáculo) o sin suelo, pose de aire.
            bool airborne = body != null && (!body.enabled || !body.isGrounded);
            if (!_lookedForClimb)
            {
                _climb = root.GetComponent<PlayerWallClimb>(); // solo el jugador trepa fachadas
                _leap = root.GetComponent<PlayerFaithLeap>();  // y hace el salto de fe
                _lookedForClimb = true;
            }
            bool climbing = _climb != null && _climb.IsActive;
            bool diving = _leap != null && _leap.IsActive;
            float climbed = _hasLastHeight ? position.y - _lastHeight : 0f;
            _lastHeight = position.y;
            _hasLastHeight = true;

            // Velocidad vertical medida, algo suavizada, y tiempos en el aire y desde el aterrizaje.
            float measured = climbed / Time.deltaTime;
            if (Mathf.Abs(measured) > 60f) measured = 0f; // teletransporte o reaparición
            _verticalSpeed = Mathf.Lerp(_verticalSpeed, measured, Mathf.Clamp01(Time.deltaTime * 20f));
            if (airborne && !climbing)
            {
                _airSeconds += Time.deltaTime;
                _sinceLanding = -1f;
            }
            else
            {
                // Solo amortigua si de verdad venía de un salto o una caída, no de un escalón.
                if (_airSeconds >= landingMinAirSeconds && !climbing) _sinceLanding = 0f;
                else if (_sinceLanding >= 0f) _sinceLanding += Time.deltaTime;
                _airSeconds = 0f;
            }
            IsAirPose = airborne && !climbing && _airSeconds >= airGraceSeconds;
            IsLandingPose = !airborne && !climbing && AirCycle.IsLanding(_sinceLanding);

            float targetLeftLeg, targetRightLeg, targetLeftArm, targetRightArm;
            bool bothArms = false;
            bool walkingOnGround = false;
            float? airLeftKnee = null, airRightKnee = null;
            float? pickupWeight = null;
            if (_pickupSeconds >= 0f)
            {
                _pickupSeconds += Time.deltaTime;
                if (PickupCycle.IsFinished(_pickupSeconds) || airborne || climbing || diving) _pickupSeconds = -1f; // acabó, o algo lo interrumpe
            }
            if (_pickupSeconds >= 0f)
            {
                // Agachado a recoger algo: sentadilla con el cuerpo inclinado y un brazo hacia el suelo.
                float w = PickupCycle.Weight(_pickupSeconds);
                pickupWeight = w;
                targetLeftLeg = targetRightLeg = PickupCycle.LegAngle(w);
                targetLeftArm = PickupCycle.ArmAngle(w, false);
                targetRightArm = PickupCycle.ArmAngle(w, true);
                bothArms = true;
                airLeftKnee = airRightKnee = PickupCycle.KneeAngle(w);
            }
            else if (diving)
            {
                // Salto de fe: brazos arriba y piernas rectas, ligeramente hacia atrás.
                targetLeftArm = targetRightArm = LeapRules.DiveArm;
                targetLeftLeg = targetRightLeg = LeapRules.DiveLeg;
                bothArms = true;
            }
            else if (climbing && _climb.IsMantling)
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
            else if (IsAirPose)
            {
                // Salto y caída: la postura sale de la velocidad vertical (AirCycle).
                targetLeftLeg = AirCycle.LegAngle(_verticalSpeed, _airSeconds, true);
                targetRightLeg = AirCycle.LegAngle(_verticalSpeed, _airSeconds, false);
                targetLeftArm = AirCycle.ArmAngle(_verticalSpeed, _airSeconds, true);
                targetRightArm = AirCycle.ArmAngle(_verticalSpeed, _airSeconds, false);
                airLeftKnee = AirCycle.KneeAngle(_verticalSpeed, _airSeconds, true);
                airRightKnee = AirCycle.KneeAngle(_verticalSpeed, _airSeconds, false);
            }
            else if (IsLandingPose)
            {
                // Recién aterrizado: amortigua con una zancada y los brazos por delante.
                targetLeftLeg = AirCycle.LandingLegAngle(_sinceLanding, true);
                targetRightLeg = AirCycle.LandingLegAngle(_sinceLanding, false);
                targetLeftArm = targetRightArm = AirCycle.LandingArmAngle(_sinceLanding);
                airLeftKnee = airRightKnee = AirCycle.LandingKneeAngle(_sinceLanding);
            }
            else
            {
                _phase = WalkCycle.Advance(_phase, speed, Time.deltaTime);
                float swing = RunCycle.SwingScale(speed); // corriendo abre más piernas y brazos
                targetLeftLeg = WalkCycle.LegAngle(_phase, speed) * swing;
                targetRightLeg = -targetLeftLeg;
                targetLeftArm = WalkCycle.ArmAngle(_phase, speed) * swing;
                targetRightArm = -targetLeftArm;
                walkingOnGround = true;
            }
            if (!swingRightArm && !bothArms) targetRightArm = 0f; // para trepar hacen falta las dos manos

            // Recibir la llave: la mano derecha manda sobre lo demás mientras dura el gesto (el cuerpo está quieto).
            float? keyElbow = null;
            if (_keySeconds >= 0f)
            {
                _keySeconds += Time.deltaTime;
                // Solo acaba al terminar el ciclo: el juego está parado mientras dura, y un pequeño desajuste del suelo no debe cortarlo.
                if (KeyHandoverCycle.IsFinished(_keySeconds) || climbing || diving) _keySeconds = -1f;
            }
            if (_keySeconds >= 0f)
            {
                targetRightArm = KeyHandoverCycle.HeroArm(_keySeconds);
                keyElbow = KeyHandoverCycle.HeroElbow(_keySeconds);
            }

            // Dar la llave: misma mano, al revés (del pecho hacia el capitán).
            if (_giveSeconds >= 0f)
            {
                _giveSeconds += Time.deltaTime;
                if (KeyGiveCycle.IsFinished(_giveSeconds) || climbing || diving) _giveSeconds = -1f;
            }
            if (_giveSeconds >= 0f)
            {
                targetRightArm = KeyGiveCycle.HeroArm(_giveSeconds);
                keyElbow = KeyGiveCycle.HeroElbow(_giveSeconds);
            }

            // Con el arma en alto (el guardia que persigue) el brazo derecho sube y la hoja apunta arriba y adelante.
            if (weaponGrip != null)
            {
                _raise = WeaponRaise.Blend(_raise, WeaponRaised, Time.deltaTime);
                if (_raise > 0f) targetRightArm = WeaponRaise.ArmAngle(targetRightArm, _raise, Time.time);

                // Tajo: el brazo baja de golpe desde lo alto y la hoja se abate; después sigue lo normal.
                if (_strikeSeconds >= 0f)
                {
                    _strikeSeconds += Time.deltaTime;
                    if (StrikeCycle.IsFinished(_strikeSeconds)) _strikeSeconds = -1f;
                    else
                    {
                        _raise = StrikeCycle.RaiseWeight(_strikeSeconds);
                        targetRightArm = StrikeCycle.ArmAngle(_strikeSeconds);
                    }
                }
            }

            float step = blendDegreesPerSecond * Time.deltaTime;
            _leftLeg = Mathf.MoveTowards(_leftLeg, targetLeftLeg, step);
            _rightLeg = Mathf.MoveTowards(_rightLeg, targetRightLeg, step);
            _leftArm = Mathf.MoveTowards(_leftArm, targetLeftArm, step);
            // El tajo es rápido; el gesto de la llave ya viene suavizado por su ciclo, así que la mano lo sigue sin retraso.
            _rightArm = (_keySeconds >= 0f || _giveSeconds >= 0f) ? targetRightArm : Mathf.MoveTowards(_rightArm, targetRightArm, _strikeSeconds >= 0f ? step * 3f : step);

            // Girar en X negativo lleva el extremo de la extremidad hacia delante (+Z).
            Apply(leftLeg, _leftLeg);
            Apply(rightLeg, _rightLeg);
            Apply(leftArm, _leftArm);
            Apply(rightArm, _rightArm);
            // El puño del arma cuelga del brazo: se le suma el giro del brazo para que la hoja quede donde toca.
            if (weaponGrip != null) weaponGrip.localRotation = Quaternion.Euler(WeaponRaise.GripAngle(_raise, _rightArm), 0f, 0f);

            ApplyRun(walkingOnGround ? speed : 0f, step, airLeftKnee, airRightKnee, pickupWeight, keyElbow);
        }

        /// <summary>
        /// Postura de muerte del jugador: brazos abiertos, rodillas flojas y el cuerpo entero cae hacia atrás sobre los pies
        /// (se gira este nodo, que no tiene escala propia que deforme la rotación). Devuelve true mientras dura.
        /// </summary>
        private bool UpdateDeathPose()
        {
            if (!_lookedForPlayerBody)
            {
                _lookedForPlayerBody = true;
                _playerBody = root != null ? root.GetComponent<PrototypePlayerController>() : null;
            }
            float t = _playerBody != null ? _playerBody.DeathSeconds : -1f;
            if (t < 0f)
            {
                if (_wasDead)
                {
                    // Reaparece: el nodo vuelve a su sitio y a su giro.
                    _wasDead = false;
                    IsDeathPose = false;
                    transform.localPosition = _hostBasePosition;
                    transform.localRotation = _hostBaseRotation;
                }
                return false;
            }

            if (!_wasDead)
            {
                _wasDead = true;
                _hostBasePosition = transform.localPosition;
                _hostBaseRotation = transform.localRotation;
            }
            IsDeathPose = true;
            WeaponRaised = false;
            _strikeSeconds = -1f;

            DeathPoseSample pose = DeathPoses.Sample(_playerBody.DeathKind, t);
            float step = blendDegreesPerSecond * 2f * Time.deltaTime;
            _leftLeg = Mathf.MoveTowards(_leftLeg, pose.LeftLeg, step);
            _rightLeg = Mathf.MoveTowards(_rightLeg, pose.RightLeg, step);
            _leftArm = Mathf.MoveTowards(_leftArm, pose.LeftArm, step);
            _rightArm = Mathf.MoveTowards(_rightArm, pose.RightArm, step);
            Apply(leftLeg, _leftLeg);
            Apply(rightLeg, _rightLeg);
            Apply(leftArm, _leftArm);
            Apply(rightArm, _rightArm);

            _jointsFound = _jointsFound || FindJoints();
            _leftKneeAngle = pose.LeftKnee;
            _rightKneeAngle = pose.RightKnee;
            _elbowAngle = pose.Elbow;
            if (_leftKnee != null) _leftKnee.localRotation = Quaternion.Euler(_leftKneeAngle, 0f, 0f);
            if (_rightKnee != null) _rightKnee.localRotation = Quaternion.Euler(_rightKneeAngle, 0f, 0f);
            if (_leftElbow != null) _leftElbow.localRotation = Quaternion.Euler(-_elbowAngle, 0f, 0f);
            if (_rightElbow != null) _rightElbow.localRotation = Quaternion.Euler(-_elbowAngle, 0f, 0f);
            if (bodyNode != null) bodyNode.localRotation = _bodyBaseKnown ? _bodyBaseRotation : bodyNode.localRotation;
            _lean = 0f;
            _lift = 0f;

            // El cuerpo cae sobre los pies (la raíz) hacia donde empuja el golpe.
            Vector3 fall = _playerBody.DeathDirection;
            Vector3 axis = Vector3.Cross(Vector3.up, fall);
            float pitch = pose.Pitch;
            if (Vector3.Dot(Quaternion.AngleAxis(10f, axis) * Vector3.up, fall) < 0f) axis = -axis;
            Quaternion tilt = Quaternion.AngleAxis(pitch, axis);
            Transform parent = transform.parent;
            Quaternion parentRotation = parent != null ? parent.rotation : Quaternion.identity;
            Vector3 feet = root.position;
            Vector3 baseWorld = parent != null ? parent.TransformPoint(_hostBasePosition) : _hostBasePosition;
            transform.rotation = tilt * (parentRotation * _hostBaseRotation);
            transform.position = feet + tilt * (baseWorld - feet) + Vector3.up * pose.Lift;
            return true;
        }

        private bool FindJoints()
        {
            _leftKnee = leftLeg != null ? leftLeg.Find("Rodilla") : null;
            _rightKnee = rightLeg != null ? rightLeg.Find("Rodilla") : null;
            _leftElbow = leftArm != null ? leftArm.Find("Codo") : null;
            _rightElbow = rightArm != null ? rightArm.Find("Codo") : null;
            return true;
        }

        /// <summary>Rodillas, codos, inclinación y rebote del andar y el correr; todo vuelve a cero en el aire, trepando o parado.</summary>
        private void ApplyRun(float speed, float step, float? kneeLeftOverride = null, float? kneeRightOverride = null, float? pickupWeight = null, float? rightElbowOverride = null)
        {
            if (!_jointsFound)
            {
                _jointsFound = true;
                _leftKnee = leftLeg != null ? leftLeg.Find("Rodilla") : null;
                _rightKnee = rightLeg != null ? rightLeg.Find("Rodilla") : null;
                _leftElbow = leftArm != null ? leftArm.Find("Codo") : null;
                _rightElbow = rightArm != null ? rightArm.Find("Codo") : null;
            }
            if (bodyNode != null && !_bodyBaseKnown)
            {
                _bodyBasePosition = bodyNode.localPosition;
                _bodyBaseRotation = bodyNode.localRotation;
                _bodyBaseKnown = true;
            }

            RunBlend = RunCycle.Blend(speed);
            // Rodillas: se mueven al ritmo de la fase; cada una vuelve a 0 cuando no se anda.
            // En el aire y al aterrizar mandan las rodillas de AirCycle (flexionadas); en el suelo, las del andar.
            _leftKneeAngle = Mathf.MoveTowards(_leftKneeAngle, kneeLeftOverride ?? RunCycle.KneeAngle(_phase, speed, true), step);
            _rightKneeAngle = Mathf.MoveTowards(_rightKneeAngle, kneeRightOverride ?? RunCycle.KneeAngle(_phase, speed, false), step);
            _elbowAngle = Mathf.MoveTowards(_elbowAngle, pickupWeight.HasValue ? PickupCycle.ElbowAngle(pickupWeight.Value) : RunCycle.ElbowAngle(speed), step);
            // Hacia atrás en la rodilla (+X) y hacia delante en el codo (−X).
            if (_leftKnee != null) _leftKnee.localRotation = Quaternion.Euler(_leftKneeAngle, 0f, 0f);
            if (_rightKnee != null) _rightKnee.localRotation = Quaternion.Euler(_rightKneeAngle, 0f, 0f);
            // El brazo del arma del guardia no se dobla: la hoja apunta donde toca.
            if (_leftElbow != null) _leftElbow.localRotation = Quaternion.Euler(-_elbowAngle, 0f, 0f);
            if (_rightElbow != null) _rightElbow.localRotation = Quaternion.Euler(-(rightElbowOverride ?? (swingRightArm ? _elbowAngle : 0f)), 0f, 0f);

            float smooth = Mathf.Clamp01(Time.deltaTime * 10f);
            _lean = Mathf.Lerp(_lean, pickupWeight.HasValue ? PickupCycle.Lean(pickupWeight.Value) : RunCycle.Lean(speed), smooth);
            _lift = Mathf.Lerp(_lift, pickupWeight.HasValue ? PickupCycle.Drop(pickupWeight.Value) : RunCycle.BodyLift(_phase, speed), Mathf.Clamp01(Time.deltaTime * 30f));
            if (bodyNode != null)
            {
                float scaleY = bodyNode.parent != null ? Mathf.Max(0.01f, bodyNode.parent.lossyScale.y) : 1f;
                bodyNode.localRotation = _bodyBaseRotation * Quaternion.Euler(_lean, 0f, 0f);
                bodyNode.localPosition = _bodyBasePosition + Vector3.up * (_lift / scaleY);
            }
        }

        private static void Apply(Transform pivot, float forwardDegrees)
        {
            if (pivot != null) pivot.localRotation = Quaternion.Euler(-forwardDegrees, 0f, 0f);
        }
    }
}
