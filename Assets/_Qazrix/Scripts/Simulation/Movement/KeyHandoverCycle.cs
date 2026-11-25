using System;

namespace Qazrix.Simulation.Movement
{
    /// <summary>
    /// Zahra entrega una llave al protagonista: ella extiende el brazo con la llave en la mano, él alarga el suyo, la llave pasa
    /// de una mano a la otra, él se la lleva al pecho y la guarda dentro de la camisa; después los dos bajan el brazo.
    /// Todo en función de los segundos desde el contacto. Ángulos en grados, positivo = hacia delante. Sin dependencias de Unity.
    /// </summary>
    public static class KeyHandoverCycle
    {
        // Zahra: levanta el brazo, lo mantiene mientras él toma la llave y lo baja.
        public const float OfferUpUntil = 0.6f, OfferDownFrom = 1.5f, OfferDownUntil = 2.1f;
        public const float OfferArmDegrees = 75f;

        // El protagonista: alarga la mano, la lleva al pecho y la suelta.
        public const float ReachFrom = 0.5f, ReachUntil = 1.0f;
        public const float ChestFrom = 1.3f, ChestUntil = 1.8f;
        public const float ReleaseFrom = 2.1f, ReleaseUntil = 2.7f;
        public const float ReachArmDegrees = 60f, ReachElbowDegrees = 25f;
        public const float ChestArmDegrees = 5f, ChestElbowDegrees = 140f;

        // La llave viaja de una mano a la otra y desaparece dentro de la camisa.
        public const float TravelFrom = 0.9f, TravelSeconds = 0.4f;
        public const float StashAt = 2.1f;

        public const float TotalSeconds = ReleaseUntil + 0.1f;

        public static bool IsFinished(float seconds) => seconds >= TotalSeconds;

        /// <summary>Peso del brazo de Zahra extendido: 0 = en reposo, 1 = ofreciendo la llave.</summary>
        public static float ZahraOffer(float seconds)
        {
            if (seconds <= 0f) return 0f;
            if (seconds < OfferUpUntil) return Smooth(seconds / OfferUpUntil);
            if (seconds <= OfferDownFrom) return 1f;
            return Smooth((OfferDownUntil - seconds) / (OfferDownUntil - OfferDownFrom));
        }

        /// <summary>Giro hacia delante del brazo derecho de Zahra.</summary>
        public static float ZahraArm(float seconds) => OfferArmDegrees * ZahraOffer(seconds);

        private static float Reach(float seconds) => Release(seconds) * Smooth((seconds - ReachFrom) / (ReachUntil - ReachFrom));

        private static float Chest(float seconds) => Release(seconds) * Smooth((seconds - ChestFrom) / (ChestUntil - ChestFrom));

        /// <summary>1 hasta que suelta el brazo, y de ahí baja a 0 hasta el final.</summary>
        private static float Release(float seconds)
        {
            if (seconds <= ReleaseFrom) return 1f;
            return Smooth((ReleaseUntil - seconds) / (ReleaseUntil - ReleaseFrom));
        }

        /// <summary>Giro hacia delante del brazo derecho del protagonista: de reposo a alargado y de ahí al pecho.</summary>
        public static float HeroArm(float seconds)
        {
            float reach = Lerp(0f, ReachArmDegrees, Reach(seconds));
            return Lerp(reach, ChestArmDegrees, Chest(seconds));
        }

        /// <summary>Doblez del codo derecho del protagonista: casi recto al alargar la mano y muy doblado al llevarla al pecho.</summary>
        public static float HeroElbow(float seconds)
        {
            float reach = Lerp(0f, ReachElbowDegrees, Reach(seconds));
            return Lerp(reach, ChestElbowDegrees, Chest(seconds));
        }

        /// <summary>Dónde está la llave: 0 = en la mano de Zahra, 1 = en la del protagonista (viaja entre los dos).</summary>
        public static float KeyBlend(float seconds) => Smooth((seconds - TravelFrom) / TravelSeconds);

        /// <summary>La llave se ve hasta que se la guarda en la camisa.</summary>
        public static bool KeyVisible(float seconds) => seconds < StashAt;

        public static bool HasStashed(float seconds) => seconds >= StashAt;

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static float Smooth(float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return t * t * (3f - 2f * t);
        }
    }
}
