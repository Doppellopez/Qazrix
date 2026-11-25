using System;

namespace Qazrix.Simulation.Movement
{
    /// <summary>
    /// El protagonista le da la llave al capitán: se la saca de la camisa, alarga la mano, el capitán extiende la suya y la toma, y los
    /// dos bajan el brazo; el capitán se guarda la llave en el cinto. Es el gesto de <see cref="KeyHandoverCycle"/> al revés, con los
    /// papeles cambiados. En función de los segundos desde el contacto. Ángulos en grados, positivo = hacia delante. Sin dependencias de Unity.
    /// </summary>
    public static class KeyGiveCycle
    {
        // El protagonista: lleva la mano al pecho, saca la llave, alarga el brazo y lo baja.
        public const float ChestUntil = 0.5f;               // llega al pecho: ahí saca la llave
        public const float ReachFrom = 0.5f, ReachUntil = 1.1f;
        public const float ReleaseFrom = 2.0f, ReleaseUntil = 2.6f;

        // El capitán: extiende la mano abierta mientras él alarga la suya, toma la llave y baja el brazo.
        public const float OfferFrom = 0.7f, OfferUntil = 1.2f;
        public const float OfferDownFrom = 2.0f, OfferDownUntil = 2.6f;
        public const float OfferArmDegrees = 70f;

        // La llave: sale de la camisa, viaja a la mano del capitán y este se la guarda.
        public const float DrawAt = 0.5f;
        public const float TravelFrom = 1.2f, TravelSeconds = 0.4f;
        public const float StashAt = 2.6f;

        public const float TotalSeconds = StashAt + 0.4f;

        public static bool IsFinished(float seconds) => seconds >= TotalSeconds;

        private static float Chest(float t) => Release(t) * Smooth(t / ChestUntil);

        private static float Reach(float t) => Release(t) * Smooth((t - ReachFrom) / (ReachUntil - ReachFrom));

        /// <summary>1 hasta que suelta el brazo y de ahí baja a 0.</summary>
        private static float Release(float t) => t <= ReleaseFrom ? 1f : Smooth((ReleaseUntil - t) / (ReleaseUntil - ReleaseFrom));

        /// <summary>Giro hacia delante del brazo derecho del protagonista: de reposo al pecho, y del pecho a alargado hacia el capitán.</summary>
        public static float HeroArm(float seconds)
        {
            float toChest = Lerp(0f, KeyHandoverCycle.ChestArmDegrees, Chest(seconds));
            return Lerp(toChest, KeyHandoverCycle.ReachArmDegrees, Reach(seconds));
        }

        /// <summary>Doblez del codo derecho del protagonista: muy doblado en el pecho y casi recto al alargar la mano.</summary>
        public static float HeroElbow(float seconds)
        {
            float toChest = Lerp(0f, KeyHandoverCycle.ChestElbowDegrees, Chest(seconds));
            return Lerp(toChest, KeyHandoverCycle.ReachElbowDegrees, Reach(seconds));
        }

        /// <summary>Peso del brazo del capitán extendido (0 = en reposo, 1 = con la mano abierta delante).</summary>
        public static float CaptainOffer(float seconds)
        {
            if (seconds <= OfferFrom) return 0f;
            if (seconds < OfferUntil) return Smooth((seconds - OfferFrom) / (OfferUntil - OfferFrom));
            if (seconds <= OfferDownFrom) return 1f;
            return Smooth((OfferDownUntil - seconds) / (OfferDownUntil - OfferDownFrom));
        }

        public static float CaptainArm(float seconds) => OfferArmDegrees * CaptainOffer(seconds);

        /// <summary>Dónde está la llave: 0 = en la mano del protagonista, 1 = en la del capitán.</summary>
        public static float KeyBlend(float seconds) => Smooth((seconds - TravelFrom) / TravelSeconds);

        /// <summary>La llave sale de la camisa y se ve hasta que el capitán se la guarda.</summary>
        public static bool KeyVisible(float seconds) => seconds >= DrawAt && seconds < StashAt;

        /// <summary>La llave ha llegado a manos del capitán: ya no la lleva el protagonista.</summary>
        public static bool HasHandedOver(float seconds) => seconds >= TravelFrom + TravelSeconds;

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static float Smooth(float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return t * t * (3f - 2f * t);
        }
    }
}
