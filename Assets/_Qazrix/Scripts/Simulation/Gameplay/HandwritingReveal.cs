using System;

namespace Qazrix.Simulation.Gameplay
{
    /// <summary>El texto del pergamino final.</summary>
    public static class EndingText
    {
        /// <summary>Con «los cristianos» en vez de «lo cristianos» (errata del encargo).</summary>
        public const string Message =
            "Así la noche del 22 al 23 de Abril el ejército cristiano entra en la fortaleza de Cáceres o Qazrix, como era conocida por los " +
            "musulmanes y logran conquistarla para los cristianos de manos de los musulmanes, y todo gracias a Zahra, hija del Caíd de Qazrix";
    }

    /// <summary>
    /// Cómo se escribe a mano un texto letra a letra: cada letra tarda un poco distinto (no es una máquina de escribir), los espacios
    /// son rápidos y hay pausas tras las comas y los puntos. Dada la hora, dice cuántas letras se han escrito. Determinista: el «temblor»
    /// de cada letra sale de su posición, no del azar. Sin dependencias de Unity.
    /// </summary>
    public static class HandwritingReveal
    {
        public const float LetterSeconds = 0.065f;
        public const float SpaceSeconds = 0.035f;
        public const float CommaPause = 0.30f;
        public const float SentencePause = 0.60f;
        public const float Jitter = 0.30f;   // ±30 % de lo que tarda cada letra

        /// <summary>Lo que tarda en escribirse la letra <paramref name="index"/> (con su pausa si es una coma o un punto).</summary>
        public static float CharSeconds(string text, int index)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (index < 0 || index >= text.Length) throw new ArgumentOutOfRangeException(nameof(index));
            char c = text[index];
            if (c == ' ') return SpaceSeconds;
            // Variación fija de cada letra entre 1 − Jitter y 1 + Jitter.
            uint h = (uint)(index * 2654435761u) ^ (uint)c;
            float wobble = 1f + Jitter * (((h >> 8) & 0xFF) / 127.5f - 1f);
            float t = LetterSeconds * wobble;
            if (c == ',' || c == ';' || c == ':') t += CommaPause;
            else if (c == '.' || c == '!' || c == '?') t += SentencePause;
            return t;
        }

        public static float TotalSeconds(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            float total = 0f;
            for (int i = 0; i < text.Length; i++) total += CharSeconds(text, i);
            return total;
        }

        /// <summary>Cuántas letras hay escritas pasados <paramref name="seconds"/> desde que empezó.</summary>
        public static int VisibleCount(string text, float seconds)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (seconds <= 0f) return 0;
            float elapsed = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                elapsed += CharSeconds(text, i);
                if (elapsed > seconds) return i;
            }
            return text.Length;
        }

        public static bool IsComplete(string text, float seconds) => VisibleCount(text, seconds) >= text.Length;
    }
}
