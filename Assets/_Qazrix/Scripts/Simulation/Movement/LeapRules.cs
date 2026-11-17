using System;
using Qazrix.Simulation.AI;

namespace Qazrix.Simulation.Movement
{
    /// <summary>Medidas del salto de fe desde una atalaya. Valores de prototipo.</summary>
    [Serializable]
    public sealed class LeapLimits
    {
        public float Gravity = -22f;   // m/s², igual que el controlador del jugador
        public float LaunchUp = 3f;    // m/s hacia arriba al saltar del borde
        public float MinDrop = 6f;     // desnivel mínimo para que sea un salto de fe y no un salto cualquiera
        public float MaxReach = 18f;   // distancia horizontal máxima al destino
    }

    /// <summary>
    /// Reglas puras del salto de fe: si un desnivel y una distancia admiten el salto, cuánto dura el
    /// vuelo y por dónde pasa el cuerpo (horizontal uniforme, vertical balístico, llegando justo al
    /// destino). Sin dependencias de Unity.
    /// </summary>
    public static class LeapRules
    {
        /// <summary>Postura de buceo (grados, positivo = hacia delante; 180 = recto arriba).</summary>
        public const float DiveArm = 165f, DiveLeg = -10f;

        public static bool CanLeap(float drop, float horizontalDistance, LeapLimits limits)
        {
            if (limits == null) throw new ArgumentNullException(nameof(limits));
            return drop >= limits.MinDrop && horizontalDistance > 0.5f && horizontalDistance <= limits.MaxReach;
        }

        /// <summary>Segundos de vuelo para bajar <paramref name="drop"/> metros saliendo con impulso hacia arriba.</summary>
        public static float FlightTime(float drop, LeapLimits limits)
        {
            if (limits == null) throw new ArgumentNullException(nameof(limits));
            float g = Math.Abs(limits.Gravity);
            if (g < 0.01f) throw new ArgumentException("la gravedad no puede ser cero", nameof(limits));
            float up = limits.LaunchUp;
            return (up + (float)Math.Sqrt(up * up + 2f * g * Math.Max(0f, drop))) / g;
        }

        /// <summary>Posición de los pies a los <paramref name="t"/> segundos; en la duración exacta está en el destino.</summary>
        public static Float3 Position(Float3 start, Float3 end, float duration, float t, LeapLimits limits)
        {
            if (limits == null) throw new ArgumentNullException(nameof(limits));
            float d = Math.Max(duration, 0.01f);
            float clamped = Math.Max(0f, Math.Min(d, t));
            float k = clamped / d;
            float y = start.Y + limits.LaunchUp * clamped + 0.5f * limits.Gravity * clamped * clamped;
            if (clamped >= d) y = end.Y;
            return new Float3(start.X + (end.X - start.X) * k, y, start.Z + (end.Z - start.Z) * k);
        }
    }
}
