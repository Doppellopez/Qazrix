using System;

namespace Qazrix.Simulation.Movement
{
    /// <summary>
    /// Capa flexible hecha con una cadena de ocho tramos articulados, cada uno con su resorte amortiguado.
    /// Al andar y correr el viento la separa de la espalda (más cuanto más abajo); al caer se abre; si una
    /// pierna se echa hacia atrás (salto, trepada) la capa se aparta para no atravesarla; nunca baja de
    /// <see cref="MinLift"/>, así que no se mete dentro del cuerpo. De lado queda rezagada y ondea. Como los resortes
    /// tienen inercia, al parar la capa se columpia un poco antes de asentarse. Sin dependencias de Unity.
    /// Ángulos en grados, relativos al tramo de arriba: <c>Lift</c> positivo = el borde de abajo se aleja
    /// de la espalda hacia atrás; <c>Sway</c> positivo = se desvía hacia el lado derecho del personaje.
    /// Con ocho tramos el doblez es suave: el efecto total del viento, la caída o la pierna se reparte entre ellos
    /// (más peso cuanto más abajo, de modo que la suma es la misma que con tres), y el ondeo lateral es una onda
    /// que recorre la capa de arriba abajo.
    /// </summary>
    public sealed class CapeSim
    {
        public const int Segments = 8;

        /// <summary>Inclinación en reposo de cada tramo: entre todos, la capa cuelga 6° separada de la espalda.</summary>
        public const float RestLift = 6f / Segments;

        /// <summary>
        /// Tope inferior: por debajo de este ángulo la capa se metería dentro de la cadera. Es un límite duro,
        /// también para el rebote del resorte.
        /// </summary>
        public const float MinLift = RestLift * 0.6f;

        public const float MaxLift = 38f, MaxSway = 28f;

        /// <summary>Largo total de la capa, en metros.</summary>
        public const float CapeLength = 0.66f;

        /// <summary>Largo de cada tramo, en metros (lo usa el constructor del modelo).</summary>
        public const float SegmentLength = CapeLength / Segments;

        private const float WindPerMetre = 2.4f;     // grados por m/s de avance, en el tramo medio
        private const float FallFlare = 1.6f;        // grados por m/s de caída
        private const float LegClearance = 0.25f;    // grados de separación por grado de pierna echada hacia atrás
        private const float SwayPerMetre = 5f;
        private const float AccelLift = 0.7f;        // grados por m/s² de aceleración hacia delante: la capa se queda atrás
        private const float AccelRise = 0.35f;       // grados por m/s² de aceleración vertical: al despegar se queda abajo, al frenar se abre
        private const float AccelSway = 0.8f;        // grados por m/s² de aceleración lateral       // grados por m/s de desplazamiento lateral
        private const float RippleDegrees = 1.2f;    // ondeo lateral por m/s de avance
        private const float RippleRate = 9f;         // rad/s del ondeo
        private const float RipplePhase = 0.75f;     // desfase del ondeo entre un tramo y el siguiente (rad): la onda baja por la capa
        private const float MaxStep = 1f / 120f;     // paso máximo de integración

        // El tramo de abajo es el más suelto: ondea más y tarda más en volver. El peso crece en rampa de arriba abajo y
        // suma 3 en total (igual que con tres tramos de pesos 0,5 · 1 · 1,5), así el doblez conjunto no cambia.
        private static readonly float[] Weight = Ramp(i => 6f * Position(i) / Segments);
        private static readonly float[] Stiffness = Ramp(i => 85f - 55f * Position(i));
        private static readonly float[] Damping = Ramp(i => 8.8f - 5.0f * Position(i));

        /// <summary>Posición del tramo en la capa: 0 arriba, 1 abajo (punto medio del tramo).</summary>
        private static float Position(int segment) => (segment + 0.5f) / Segments;

        private static float[] Ramp(Func<int, float> value)
        {
            var values = new float[Segments];
            for (int i = 0; i < Segments; i++) values[i] = value(i);
            return values;
        }

        private readonly float[] _lift = new float[Segments];
        private readonly float[] _liftSpeed = new float[Segments];
        private readonly float[] _sway = new float[Segments];
        private readonly float[] _swaySpeed = new float[Segments];
        private float _time;

        public CapeSim()
        {
            for (int i = 0; i < Segments; i++) _lift[i] = RestLift;
        }

        public float Lift(int segment) => _lift[segment];

        public float Sway(int segment) => _sway[segment];

        /// <summary>Vuelve a la postura de reposo (reaparición, teletransporte).</summary>
        public void Reset()
        {
            _time = 0f;
            for (int i = 0; i < Segments; i++)
            {
                _lift[i] = RestLift;
                _liftSpeed[i] = 0f;
                _sway[i] = 0f;
                _swaySpeed[i] = 0f;
            }
        }

        /// <summary>Ángulo al que tiende el tramo con ese movimiento (para pruebas y para el resorte).</summary>
        public static float LiftTarget(int segment, float forwardSpeed, float verticalSpeed, float legBackSwing = 0f,
            float forwardAccel = 0f, float verticalAccel = 0f)
        {
            float target = RestLift;
            // Hacia delante el viento la levanta; hacia atrás no puede meterse en la espalda (tope MinLift).
            float wind = Clamp(forwardSpeed * WindPerMetre, -4f, 16f);
            target += wind * Weight[segment];
            if (verticalSpeed < 0f) target += Clamp(-verticalSpeed * FallFlare, 0f, 22f) * Weight[segment];
            // Inercia: al acelerar hacia delante la capa se queda atrás; al frenar sigue hacia delante (hasta el cuerpo).
            target += Clamp(forwardAccel * AccelLift, -12f, 16f) * Weight[segment];
            // Al empujarla hacia arriba (despegue, aterrizaje) la tela se queda abajo y pegada; al empezar a caer flota y se abre.
            target -= Clamp(verticalAccel * AccelRise, -8f, 8f) * Weight[segment];
            // Una pierna echada hacia atrás (salto, trepada) empuja el borde de la capa hacia atrás para no atravesarla.
            target += Clamp(legBackSwing, 0f, 70f) * LegClearance * Weight[segment];
            return Clamp(target, MinLift, MaxLift);
        }

        /// <summary>
        /// Cuánto se separa hacia atrás el borde de abajo respecto a una capa que colgara recta, en metros.
        /// Si es pequeño, la capa queda pegada a la espalda y a la cadera.
        /// </summary>
        public float HemSetback()
        {
            float cumulative = 0f, setback = 0f;
            for (int i = 0; i < Segments; i++)
            {
                cumulative += _lift[i];
                setback += SegmentLength * (float)Math.Sin(cumulative * Math.PI / 180.0);
            }
            return setback;
        }

        public static float SwayTarget(int segment, float lateralSpeed, float lateralAccel = 0f)
        {
            // Si el cuerpo va a la derecha, la capa se queda atrás, o sea, hacia la izquierda.
            return Clamp(-(lateralSpeed * SwayPerMetre + lateralAccel * AccelSway) * Weight[segment], -MaxSway, MaxSway);
        }

        /// <param name="forwardSpeed">m/s hacia donde mira el personaje (negativo = hacia atrás).</param>
        /// <param name="verticalSpeed">m/s hacia arriba (negativo = cayendo).</param>
        /// <param name="lateralSpeed">m/s hacia su derecha.</param>
        /// <param name="legBackSwing">grados que una pierna está echada hacia atrás (0 si ninguna).</param>
        /// <param name="forwardAccel">aceleración hacia delante, en m/s² (negativa al frenar).</param>
        /// <param name="verticalAccel">aceleración hacia arriba, en m/s².</param>
        /// <param name="lateralAccel">aceleración hacia la derecha, en m/s².</param>
        public void Step(float deltaTime, float forwardSpeed, float verticalSpeed, float lateralSpeed, float legBackSwing = 0f,
            float forwardAccel = 0f, float verticalAccel = 0f, float lateralAccel = 0f)
        {
            if (deltaTime <= 0f) return;
            float remaining = Math.Min(deltaTime, 0.25f); // una pausa larga no debe disparar la capa
            while (remaining > 0f)
            {
                float dt = Math.Min(remaining, MaxStep);
                remaining -= dt;
                _time += dt;
                float ripple = RippleDegrees * Clamp(Math.Abs(forwardSpeed), 0f, 6f);

                for (int i = 0; i < Segments; i++)
                {
                    float liftTarget = LiftTarget(i, forwardSpeed, verticalSpeed, legBackSwing, forwardAccel, verticalAccel);
                    _liftSpeed[i] += (Stiffness[i] * (liftTarget - _lift[i]) - Damping[i] * _liftSpeed[i]) * dt;
                    float next = _lift[i] + _liftSpeed[i] * dt;
                    if (next < MinLift) { next = MinLift; if (_liftSpeed[i] < 0f) _liftSpeed[i] = 0f; } // topa con el cuerpo
                    _lift[i] = Clamp(next, MinLift, MaxLift + 12f);

                    float swayTarget = SwayTarget(i, lateralSpeed, lateralAccel) + ripple * Weight[i] * (float)Math.Sin(_time * RippleRate - i * RipplePhase);
                    _swaySpeed[i] += (Stiffness[i] * (swayTarget - _sway[i]) - Damping[i] * _swaySpeed[i]) * dt;
                    _sway[i] = Clamp(_sway[i] + _swaySpeed[i] * dt, -MaxSway - 12f, MaxSway + 12f);
                }
            }
        }

        private static float Clamp(float value, float min, float max)
        {
            return value < min ? min : (value > max ? max : value);
        }
    }
}
