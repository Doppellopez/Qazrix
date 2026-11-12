using System;

namespace Qazrix.Simulation.AI
{
    /// <summary>Vector de tres componentes sin dependencia de Unity (X = este, Y = altura, Z = norte).</summary>
    public readonly struct Float3
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public Float3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Float3 operator -(Float3 a, Float3 b) => new Float3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public float Length => (float)Math.Sqrt(X * X + Y * Y + Z * Z);
    }

    /// <summary>Parámetros de la vista de un guardia. Valores de partida: se ajustan jugando.</summary>
    [Serializable]
    public sealed class VisionConfig
    {
        /// <summary>Alcance máximo de la vista, en metros.</summary>
        public float Range = 14f;

        /// <summary>Mitad del ángulo del cono, en grados, medido en horizontal.</summary>
        public float HalfAngleDegrees = 50f;

        /// <summary>Por debajo de esta distancia percibe al jugador aunque esté a su espalda.</summary>
        public float ProximityRadius = 1.5f;

        /// <summary>Fracción de visibilidad que queda justo en el límite del alcance.</summary>
        public float VisibilityAtRange = 0.25f;

        /// <summary>A partir de esta fracción del medio ángulo empieza la visión periférica.</summary>
        public float PeripheralStart = 0.7f;

        /// <summary>Factor de la visión periférica: en el borde del cono se ve peor.</summary>
        public float PeripheralFactor = 0.5f;
    }

    /// <summary>
    /// Vista de un guardia (spec §6.2): cono, distancia, obstrucción y exposición. Devuelve cuánto ve
    /// al objetivo, entre 0 y 1. La obstrucción la calcula la vista Unity con un rayo y llega aquí
    /// como <c>lineOfSight</c>: sin línea de visión el resultado es siempre 0.
    /// </summary>
    public static class VisionCone
    {
        public static float Visibility(Float3 eye, Float3 forward, Float3 target, bool lineOfSight, float exposure, VisionConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (!lineOfSight || exposure <= 0f) return 0f;

            Float3 toTarget = target - eye;
            float distance = toTarget.Length;
            if (distance > config.Range) return 0f;

            exposure = Math.Min(exposure, 1f);
            if (distance <= config.ProximityRadius) return exposure;

            // El ángulo se mide en el plano horizontal: mirar hacia arriba o abajo no cierra el cono.
            float forwardLength = (float)Math.Sqrt(forward.X * forward.X + forward.Z * forward.Z);
            float flatLength = (float)Math.Sqrt(toTarget.X * toTarget.X + toTarget.Z * toTarget.Z);
            if (forwardLength < 1e-5f) return 0f;

            float angle = 0f;
            if (flatLength > 1e-5f)
            {
                float cos = (forward.X * toTarget.X + forward.Z * toTarget.Z) / (forwardLength * flatLength);
                cos = Math.Max(-1f, Math.Min(1f, cos));
                angle = (float)(Math.Acos(cos) * 180.0 / Math.PI);
            }
            if (angle > config.HalfAngleDegrees) return 0f;

            float span = Math.Max(config.Range - config.ProximityRadius, 1e-3f);
            float t = (distance - config.ProximityRadius) / span;
            float byDistance = 1f - (1f - config.VisibilityAtRange) * t;
            float byAngle = angle > config.HalfAngleDegrees * config.PeripheralStart ? config.PeripheralFactor : 1f;

            return Math.Max(0f, Math.Min(1f, exposure * byDistance * byAngle));
        }
    }

    /// <summary>
    /// Cuánto se expone y cuánto ruido hace el jugador según lo que está haciendo (spec §6.2: audición
    /// por eventos cuantificados). Reglas simples y explicables; valores de partida.
    /// </summary>
    public static class StealthRules
    {
        public const float CrouchExposure = 0.5f;
        public const float WalkNoiseRadius = 2.5f;
        public const float RunNoiseRadius = 8f;
        public const float LandingNoiseRadius = 7f;

        /// <summary>Velocidad (m/s) por debajo de la cual se considera quieto.</summary>
        public const float StillSpeed = 0.5f;

        public static float Exposure(bool crouching)
        {
            return crouching ? CrouchExposure : 1f;
        }

        /// <summary>Radio en metros dentro del cual un guardia oye al jugador. 0 = silencio.</summary>
        public static float NoiseRadius(float speed, bool crouching, bool running, bool justLanded)
        {
            if (justLanded) return LandingNoiseRadius;      // caer suena aunque vayas agachado
            if (crouching || speed < StillSpeed) return 0f; // agachado o quieto: silencio
            return running ? RunNoiseRadius : WalkNoiseRadius;
        }

        public static bool Hears(float distance, float noiseRadius)
        {
            return noiseRadius > 0f && distance <= noiseRadius;
        }
    }
}
