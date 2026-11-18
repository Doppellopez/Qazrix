using System;

namespace Qazrix.Simulation.Movement
{
    /// <summary>
    /// Muerte del jugador a manos de un guardia: el golpe le echa hacia atrás, cae con los brazos abiertos y las
    /// rodillas flojas, queda tumbado un momento y reaparece. Todo en función de los segundos desde el golpe.
    /// Ángulos en grados; positivo = hacia delante en brazos y piernas, como en <see cref="WalkCycle"/>.
    /// Sin dependencias de Unity.
    /// </summary>
    public static class DeathCycle
    {
        /// <summary>El golpe: el cuerpo se arquea hacia atrás antes de caer.</summary>
        public const float HitSeconds = 0.2f;

        /// <summary>Lo que tarda en caer del todo, y lo que se queda tumbado antes de reaparecer.</summary>
        public const float FallSeconds = 0.8f, LieSeconds = 1.4f;

        public const float TotalSeconds = HitSeconds + FallSeconds + LieSeconds;

        /// <summary>Inclinación del cuerpo al quedar tumbado (90 = de espaldas en el suelo) y tras el golpe.</summary>
        public const float LyingPitch = 90f, StaggerPitch = 12f;

        /// <summary>Lo que se levanta el cuerpo (m) al tumbarse: el grosor del tronco, para no hundirse en el suelo.</summary>
        public const float LyingLift = 0.2f;

        public static bool IsFinished(float seconds) => seconds >= TotalSeconds;

        /// <summary>Progreso de la caída, de 0 (en pie) a 1 (tumbado): lenta al principio y cada vez más rápida, como un cuerpo que cae.</summary>
        public static float Fall(float seconds)
        {
            float u = Clamp01((seconds - HitSeconds) / FallSeconds);
            return u * u;
        }

        /// <summary>Inclinación del cuerpo hacia donde cae: primero el golpe, luego la caída hasta el suelo.</summary>
        public static float BodyPitch(float seconds)
        {
            if (seconds <= 0f) return 0f;
            if (seconds < HitSeconds) return StaggerPitch * (seconds / HitSeconds);
            return StaggerPitch + (LyingPitch - StaggerPitch) * Fall(seconds);
        }

        /// <summary>Cuánto sube el cuerpo (m) según lo inclinado que está.</summary>
        public static float BodyLift(float seconds) => LyingLift * BodyPitch(seconds) / LyingPitch;

        /// <summary>Brazo: sale disparado hacia arriba con el golpe y cae abierto al suelo. La derecha, algo más.</summary>
        public static float ArmAngle(float seconds, bool left)
        {
            float flung = 140f * Clamp01(seconds / (HitSeconds + 0.3f));
            float rest = 60f;
            float value = flung + (rest - flung) * Fall(seconds);
            return left ? value : value + 12f;
        }

        /// <summary>Pierna: una se adelanta y la otra queda atrás, según cae.</summary>
        public static float LegAngle(float seconds, bool left) => (left ? 25f : -15f) * Fall(seconds);

        /// <summary>Rodilla doblada hacia atrás (≥ 0).</summary>
        public static float KneeAngle(float seconds, bool left) => (left ? 55f : 20f) * Fall(seconds);

        /// <summary>Codo doblado hacia delante (≥ 0).</summary>
        public static float ElbowAngle(float seconds) => 30f * Fall(seconds);

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }

    /// <summary>El tajo del guardia: el brazo del arma baja de golpe desde lo alto y la hoja se abate.</summary>
    public static class StrikeCycle
    {
        /// <summary>Duración del tajo (s) y el instante en que la hoja llega al blanco.</summary>
        public const float Seconds = 0.5f, HitAt = 0.15f;

        /// <summary>Ángulo del brazo (grados hacia delante) al terminar el tajo.</summary>
        public const float LowArm = 30f;

        public static bool IsFinished(float seconds) => seconds >= Seconds;

        /// <summary>Brazo: de arriba (<see cref="WeaponRaise.ArmUp"/>) a abajo en <see cref="HitAt"/>, y ahí se queda.</summary>
        public static float ArmAngle(float seconds)
        {
            float u = seconds <= 0f ? 0f : (seconds >= HitAt ? 1f : seconds / HitAt);
            u *= u; // acelera: es un tajo
            return WeaponRaise.ArmUp + (LowArm - WeaponRaise.ArmUp) * u;
        }

        /// <summary>Peso del arma en alto: se mantiene hasta el golpe y después baja a 0.</summary>
        public static float RaiseWeight(float seconds)
        {
            if (seconds <= HitAt) return 1f;
            float u = (seconds - HitAt) / (Seconds - HitAt);
            return u >= 1f ? 0f : 1f - u;
        }
    }
}

namespace Qazrix.Simulation.Movement
{
    /// <summary>Cómo ha muerto el jugador: de un tajo (cae de espaldas) o de una caída desde mucha altura (cae de bruces).</summary>
    public enum DeathKind { Sword, Fall }

    /// <summary>Postura del cuerpo en un instante de la muerte (ángulos en grados, como en <see cref="DeathCycle"/>).</summary>
    public struct DeathPoseSample
    {
        public float Pitch, Lift, LeftArm, RightArm, LeftLeg, RightLeg, LeftKnee, RightKnee, Elbow;
    }

    /// <summary>Elige la postura de muerte según su tipo. Sin dependencias de Unity.</summary>
    public static class DeathPoses
    {
        public static float TotalSeconds(DeathKind kind) => kind == DeathKind.Fall ? FallDeathCycle.TotalSeconds : DeathCycle.TotalSeconds;

        public static bool IsFinished(DeathKind kind, float seconds) => seconds >= TotalSeconds(kind);

        public static DeathPoseSample Sample(DeathKind kind, float t)
        {
            if (kind == DeathKind.Fall)
                return new DeathPoseSample
                {
                    Pitch = FallDeathCycle.BodyPitch(t), Lift = FallDeathCycle.BodyLift(t),
                    LeftArm = FallDeathCycle.ArmAngle(t, true), RightArm = FallDeathCycle.ArmAngle(t, false),
                    LeftLeg = FallDeathCycle.LegAngle(t, true), RightLeg = FallDeathCycle.LegAngle(t, false),
                    LeftKnee = FallDeathCycle.KneeAngle(t), RightKnee = FallDeathCycle.KneeAngle(t),
                    Elbow = FallDeathCycle.ElbowAngle(t),
                };
            return new DeathPoseSample
            {
                Pitch = DeathCycle.BodyPitch(t), Lift = DeathCycle.BodyLift(t),
                LeftArm = DeathCycle.ArmAngle(t, true), RightArm = DeathCycle.ArmAngle(t, false),
                LeftLeg = DeathCycle.LegAngle(t, true), RightLeg = DeathCycle.LegAngle(t, false),
                LeftKnee = DeathCycle.KneeAngle(t, true), RightKnee = DeathCycle.KneeAngle(t, false),
                Elbow = DeathCycle.ElbowAngle(t),
            };
        }
    }

    /// <summary>
    /// Muerte por caída: al aterrizar las rodillas ceden, el cuerpo se derrumba de bruces con los brazos lanzados hacia
    /// delante y las piernas abiertas, rebota un poco contra el suelo y queda tendido. Más seca y más larga que la del tajo.
    /// </summary>
    public static class FallDeathCycle
    {
        /// <summary>Las rodillas ceden, el derrumbe y el rebote contra el suelo (s), y cuánto dura todo hasta reaparecer.</summary>
        public const float CrumpleSeconds = 0.25f, ToppleSeconds = 0.45f, BounceSeconds = 0.3f, TotalSeconds = 3.0f;

        /// <summary>Inclinación al quedar tendido (de bruces) y al ceder las rodillas.</summary>
        public const float LyingPitch = 90f, CrumplePitch = 15f;

        /// <summary>Altura (m) que levanta el cuerpo tumbado y la del rebote.</summary>
        public const float LyingLift = 0.2f, BounceHeight = 0.08f;

        private static float ToppleEnd => CrumpleSeconds + ToppleSeconds;

        /// <summary>Progreso del derrumbe de 0 a 1 (cada vez más rápido).</summary>
        public static float Topple(float seconds)
        {
            float u = Clamp01((seconds - CrumpleSeconds) / ToppleSeconds);
            return u * u;
        }

        public static float BodyPitch(float seconds)
        {
            if (seconds <= 0f) return 0f;
            if (seconds < CrumpleSeconds) return CrumplePitch * (seconds / CrumpleSeconds);
            return CrumplePitch + (LyingPitch - CrumplePitch) * Topple(seconds);
        }

        /// <summary>Rebote: tras el derrumbe el cuerpo salta un poquito y vuelve a caer.</summary>
        public static float BodyLift(float seconds)
        {
            float lift = LyingLift * BodyPitch(seconds) / LyingPitch;
            if (seconds > ToppleEnd && seconds < ToppleEnd + BounceSeconds)
                lift += BounceHeight * (float)Math.Sin(Math.PI * (seconds - ToppleEnd) / BounceSeconds);
            return lift;
        }

        /// <summary>Los brazos se lanzan hacia delante (por encima de la cabeza) al caer.</summary>
        public static float ArmAngle(float seconds, bool left) => (left ? 170f : 160f) * Clamp01(seconds / CrumpleSeconds);

        public static float LegAngle(float seconds, bool left) => (left ? 22f : -22f) * Clamp01(seconds / (CrumpleSeconds + 0.15f));

        /// <summary>Rodilla: cede al aterrizar (70°) y se relaja a 20° al quedar tendido.</summary>
        public static float KneeAngle(float seconds)
        {
            float buckle = 70f * Clamp01(seconds / CrumpleSeconds);
            return buckle + (20f - buckle) * Topple(seconds);
        }

        public static float ElbowAngle(float seconds) => 20f * Clamp01(seconds / CrumpleSeconds);

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }

    /// <summary>
    /// Cuánto ha caído el jugador desde el punto más alto que alcanzó en el aire. Cada fotograma se le dice si está en el
    /// suelo y a qué altura; al aterrizar devuelve la caída en metros (0 el resto del tiempo). Sin dependencias de Unity.
    /// </summary>
    public sealed class FallTracker
    {
        /// <summary>Desde esta caída (m) el golpe es mortal.</summary>
        public const float FatalDrop = 15f;

        private float _peak;
        private bool _wasGrounded = true;

        public float Peak => _peak;

        public static bool IsFatal(float drop) => drop >= FatalDrop;

        /// <summary>Vuelve a empezar desde el suelo a esa altura (reaparición, teletransporte).</summary>
        public void Reset(float y)
        {
            _peak = y;
            _wasGrounded = true;
        }

        public float Step(bool grounded, float y)
        {
            float drop = 0f;
            if (grounded)
            {
                if (!_wasGrounded) drop = Math.Max(0f, _peak - y);
                _peak = y;
            }
            else _peak = Math.Max(_peak, y);
            _wasGrounded = grounded;
            return drop;
        }
    }
}
