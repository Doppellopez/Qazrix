using System;

namespace Qazrix.Simulation.Input
{
    /// <summary>
    /// Cuentas de los controles táctiles (solo en Android e iOS), sin Unity: la palanca flotante, qué botón se toca y el
    /// pellizco de zoom. Las posiciones van en píxeles, con el eje Y hacia abajo (como el dibujo de pantalla).
    /// </summary>
    public static class TouchMath
    {
        /// <summary>Recorrido de la palanca por debajo del cual no cuenta (0 a 1).</summary>
        public const float StickDeadzone = 0.12f;

        /// <summary>
        /// Valor de la palanca flotante: del punto donde se posó el dedo (<paramref name="originX"/>, <paramref name="originY"/>) al
        /// punto actual, entre −1 y 1 con magnitud máxima 1 al llegar a <paramref name="radius"/> píxeles. «Adelante» (arriba en
        /// pantalla) es Y positivo, como en el stick del mando.
        /// </summary>
        public static void Stick(float originX, float originY, float x, float y, float radius, out float outX, out float outY)
        {
            float dx = (x - originX) / radius;
            float dy = -(y - originY) / radius; // pantalla: Y crece hacia abajo
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length < StickDeadzone) { outX = 0f; outY = 0f; return; }
            if (length > 1f) { dx /= length; dy /= length; }
            outX = dx;
            outY = dy;
        }

        /// <summary>¿Está el punto dentro del círculo (el botón)?</summary>
        public static bool InCircle(float x, float y, float centerX, float centerY, float radius)
        {
            float dx = x - centerX, dy = y - centerY;
            return dx * dx + dy * dy <= radius * radius;
        }

        /// <summary>
        /// Zoom de un pellizco: separar los dedos acerca la cámara (positivo) y juntarlos la aleja (negativo). Un valor de
        /// 1 equivale a <paramref name="pixelsPerUnit"/> píxeles de cambio en la distancia entre los dedos.
        /// </summary>
        public static float PinchZoom(float previousDistance, float distance, float pixelsPerUnit)
        {
            if (pixelsPerUnit <= 0f) return 0f;
            return (distance - previousDistance) / pixelsPerUnit;
        }
    }
}
