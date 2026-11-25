using System;

namespace Qazrix.Simulation.Movement
{
    /// <summary>
    /// Resorte amortiguado para un trozo de tela: sigue a su ángulo objetivo con retraso y un poco de rebote, así que el
    /// ropaje no salta de golpe cuando cambia el viento o el cuerpo se mueve. Sin dependencias de Unity.
    /// </summary>
    public sealed class ClothSpring
    {
        private const float MaxStep = 1f / 120f;

        private float _speed;

        public float Stiffness { get; }
        public float Damping { get; }
        public float Value { get; private set; }

        /// <param name="stiffness">Fuerza con que tira hacia el objetivo (1/s²); más alta = más rápida.</param>
        /// <param name="damping">Freno del vaivén (1/s); más baja = más rebote.</param>
        public ClothSpring(float stiffness, float damping, float initial = 0f)
        {
            if (stiffness <= 0f) throw new ArgumentOutOfRangeException(nameof(stiffness));
            if (damping < 0f) throw new ArgumentOutOfRangeException(nameof(damping));
            Stiffness = stiffness;
            Damping = damping;
            Value = initial;
        }

        public void Reset(float value)
        {
            Value = value;
            _speed = 0f;
        }

        public void Step(float deltaTime, float target)
        {
            if (deltaTime <= 0f) return;
            float remaining = Math.Min(deltaTime, 0.25f); // una pausa larga no debe disparar la tela
            while (remaining > 0f)
            {
                float dt = Math.Min(remaining, MaxStep);
                remaining -= dt;
                _speed += (Stiffness * (target - Value) - Damping * _speed) * dt;
                Value += _speed * dt;
            }
        }
    }

    /// <summary>
    /// Inercia del ropaje: cuando el cuerpo acelera, la tela se queda atrás (y al frenar sigue hacia delante). Convierte la
    /// aceleración del cuerpo, vista desde él, en ángulos que se suman a los que da el viento. Mismos signos que <see cref="CapeSim"/>.
    /// </summary>
    public static class ClothInertia
    {
        public const float VeilLiftPerAccel = 0.7f;     // grados por m/s² hacia delante: el velo se queda atrás y se levanta
        public const float VeilRollPerAccel = 0.8f;     // grados por m/s² hacia la derecha: se va hacia la izquierda
        public const float DrapePerAccel = 0.5f;        // la gasa del rostro se echa hacia atrás al acelerar hacia delante
        public const float SkirtPerAccel = 0.4f;        // el bajo de la túnica se queda atrás
        public const float MaxAccel = 40f;              // las sacudidas (teletransporte) no valen

        public static void Offsets(float accelForward, float accelRight,
            out float veilLift, out float veilRoll, out float drape, out float skirtPitch, out float skirtRoll)
        {
            float f = Clamp(accelForward, -MaxAccel, MaxAccel);
            float r = Clamp(accelRight, -MaxAccel, MaxAccel);
            veilLift = Clamp(f * VeilLiftPerAccel, -4f, 14f);
            veilRoll = Clamp(-r * VeilRollPerAccel, -14f, 14f);
            drape = Clamp(f * DrapePerAccel, -6f, 8f);
            skirtPitch = Clamp(f * SkirtPerAccel, -4f, 6f);
            skirtRoll = Clamp(-r * SkirtPerAccel, -6f, 6f);
        }

        private static float Clamp(float value, float min, float max) => value < min ? min : (value > max ? max : value);
    }
}
