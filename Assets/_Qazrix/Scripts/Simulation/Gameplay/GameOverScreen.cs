using System;

namespace Qazrix.Simulation.Gameplay
{
    /// <summary>
    /// Pantalla de fin de partida (sin vidas): el juego se oscurece poco a poco, aparece el mensaje y, tras una pausa
    /// para leerlo, la escena vuelve a empezar. Todo en función de los segundos desde que se acabó la última vida.
    /// Sin dependencias de Unity.
    /// </summary>
    public static class GameOverScreen
    {
        public const string Message = "Vidas acabadas, los musulmanes han ganado!";

        /// <summary>Lo que tarda en oscurecerse la pantalla, y lo que oscurece como máximo (1 = negro del todo).</summary>
        public const float FadeSeconds = 1.8f;
        public const float MaxDarkness = 0.93f;

        /// <summary>El mensaje empieza a verse a esta altura del oscurecimiento y tarda este tiempo en salir del todo.</summary>
        public const float TextStart = 1.0f;
        public const float TextFadeSeconds = 1.0f;

        /// <summary>Tiempo con el mensaje a la vista antes de volver a empezar.</summary>
        public const float HoldSeconds = 4.0f;

        public const float TotalSeconds = TextStart + TextFadeSeconds + HoldSeconds;

        /// <summary>Opacidad del velo negro (0 = juego normal).</summary>
        public static float Darkness(float seconds)
        {
            if (seconds <= 0f) return 0f;
            float u = Math.Min(1f, seconds / FadeSeconds);
            return MaxDarkness * (u * u * (3f - 2f * u)); // suave al principio y al final
        }

        /// <summary>Opacidad del mensaje.</summary>
        public static float TextAlpha(float seconds)
        {
            float u = (seconds - TextStart) / TextFadeSeconds;
            return u <= 0f ? 0f : (u >= 1f ? 1f : u);
        }

        public static bool IsFinished(float seconds) => seconds >= TotalSeconds;
    }
}
