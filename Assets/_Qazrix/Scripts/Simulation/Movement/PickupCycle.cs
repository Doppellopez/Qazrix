using System;

namespace Qazrix.Simulation.Movement
{
    /// <summary>
    /// Pose de agacharse a recoger algo del suelo: el cuerpo baja doblando rodillas y cadera, se inclina hacia
    /// delante y un brazo alcanza el suelo; se queda abajo un instante (ahí se coge el objeto) y se levanta.
    /// Todo sale de un peso entre 0 (de pie) y 1 (abajo del todo) que depende del tiempo. Sin dependencias de Unity.
    /// Ángulos en grados, positivo = hacia delante, salvo la rodilla (positivo = se dobla hacia atrás).
    /// </summary>
    public static class PickupCycle
    {
        /// <summary>Segundos que dura todo el gesto.</summary>
        public const float Duration = 1.1f;

        /// <summary>Segundo en que la mano toca el objeto: desde aquí desaparece de la escena y pasa al inventario.</summary>
        public const float CollectAt = 0.5f;

        private const float DownUntil = 0.35f, UpFrom = 0.7f;

        // Los brazos cuelgan del tronco, que ya va inclinado hacia delante: con poco ángulo propio apuntan al suelo.
        public const float KneeDegrees = 90f, ThighDegrees = 55f, ReachArmDegrees = 20f, FreeArmDegrees = 40f;
        public const float ElbowDegrees = 20f, LeanDegrees = 30f;

        /// <summary>Metros que baja el cuerpo (negativo = hacia el suelo). Más y los pies se hunden en el empedrado.</summary>
        public const float DropMeters = -0.25f;

        /// <summary>Peso de la pose: sube suave hasta abajo del todo, se mantiene y vuelve a subir.</summary>
        public static float Weight(float seconds)
        {
            if (seconds <= 0f || seconds >= Duration) return 0f;
            if (seconds < DownUntil) return Smooth(seconds / DownUntil);
            if (seconds <= UpFrom) return 1f;
            return Smooth((Duration - seconds) / (Duration - UpFrom));
        }

        private static float Smooth(float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return t * t * (3f - 2f * t);
        }

        public static bool IsFinished(float seconds) => seconds >= Duration;

        public static bool HasCollected(float seconds) => seconds >= CollectAt;

        public static float KneeAngle(float weight) => KneeDegrees * weight;

        /// <summary>Muslo hacia delante (las dos piernas igual: es una sentadilla).</summary>
        public static float LegAngle(float weight) => ThighDegrees * weight;

        /// <summary>El brazo derecho baja hacia el suelo (casi colgando del tronco inclinado); el otro se adelanta para equilibrar.</summary>
        public static float ArmAngle(float weight, bool reaching) => (reaching ? ReachArmDegrees : FreeArmDegrees) * weight;

        public static float ElbowAngle(float weight) => ElbowDegrees * weight;

        public static float Lean(float weight) => LeanDegrees * weight;

        public static float Drop(float weight) => DropMeters * weight;
    }
}
