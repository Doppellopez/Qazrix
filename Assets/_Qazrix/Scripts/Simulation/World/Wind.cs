using System;

namespace Qazrix.Simulation.World
{
    /// <summary>
    /// Viento leve del oeste, con ráfagas suaves. En la escena la X crece hacia el este y la Z hacia el norte (como el mapa),
    /// así que el viento que viene del oeste sopla hacia +X. Es una brisa constante que sube y baja, no un temporal:
    /// dos ondas lentas de periodos distintos que nunca la paran del todo. Sin dependencias de Unity.
    /// </summary>
    public static class Wind
    {
        /// <summary>Hacia dónde sopla (unitario): del oeste hacia el este.</summary>
        public const float DirectionX = 1f, DirectionZ = 0f;

        public const float MeanSpeed = 1.6f;      // m/s: brisa floja
        public const float GustSlow = 0.70f;      // m/s de la ráfaga larga
        public const float GustFast = 0.35f;      // m/s de la ráfaga corta
        public const float SlowPeriod = 11.3f, FastPeriod = 4.1f;

        public const float MinSpeed = MeanSpeed - GustSlow - GustFast;
        public const float MaxSpeed = MeanSpeed + GustSlow + GustFast;

        private const float TwoPi = (float)(2.0 * Math.PI);

        /// <summary>Velocidad del viento (m/s) en ese instante (segundos).</summary>
        public static float Speed(float time)
        {
            float s = MeanSpeed
                + GustSlow * (float)Math.Sin(TwoPi * time / SlowPeriod)
                + GustFast * (float)Math.Sin(TwoPi * time / FastPeriod + 1.3f);
            return Math.Max(0f, s);
        }

        /// <summary>Velocidad del viento en el mundo (m/s), en X y en Z.</summary>
        public static void Velocity(float time, out float x, out float z)
        {
            float speed = Speed(time);
            x = DirectionX * speed;
            z = DirectionZ * speed;
        }

        /// <summary>
        /// El viento visto desde un personaje que mira en la dirección <paramref name="yawDegrees"/> (giro en Y de Unity: 0 = norte, +Z;
        /// 90 = este, +X). <paramref name="forward"/> es positivo si el viento sopla hacia donde mira (le da por la espalda) y
        /// <paramref name="right"/> positivo si sopla hacia su derecha.
        /// </summary>
        public static void RelativeToBody(float time, float yawDegrees, out float forward, out float right)
        {
            Velocity(time, out float wx, out float wz);
            float yaw = yawDegrees * (float)(Math.PI / 180.0);
            float sin = (float)Math.Sin(yaw), cos = (float)Math.Cos(yaw);
            forward = wx * sin + wz * cos;
            right = wx * cos - wz * sin;
        }
    }
}
