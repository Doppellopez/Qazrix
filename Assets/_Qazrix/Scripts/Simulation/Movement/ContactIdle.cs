using System;

namespace Qazrix.Simulation.Movement
{
    /// <summary>Postura de espera de Zahra en un instante: pequeños movimientos que se suman a su postura de reposo.</summary>
    public struct ContactIdlePose
    {
        /// <summary>Cuánto se ensancha el pecho al respirar (0 = reposo; escala = 1 + valor).</summary>
        public float BreathExpand;

        /// <summary>Cuánto suben los hombros al coger aire (m).</summary>
        public float BreathLift;

        /// <summary>Giro de la cabeza a los lados, hacia arriba y ladeada (grados).</summary>
        public float HeadYaw, HeadPitch, HeadRoll;

        /// <summary>Balanceo del cuerpo al cambiar el peso de un pie a otro: desplazamiento lateral (m) y ladeo (grados).</summary>
        public float SwayX, SwayRoll;

        /// <summary>Giro de cada brazo hacia delante o atrás (grados).</summary>
        public float LeftArm, RightArm;

        /// <summary>Vaivén del velo que cae por la espalda (grados).</summary>
        public float VeilSwing;

        /// <summary>Párpados: 0 = ojos abiertos, 1 = cerrados.</summary>
        public float Blink;

        /// <summary>Viento en el ropaje (grados): el velo de la espalda ladeado, la gasa que cae sobre el rostro y el bajo de la túnica.</summary>
        public float VeilRoll, DrapeSwing, SkirtPitch, SkirtRoll;
    }

    /// <summary>
    /// Animación de espera de la mujer del velo violeta mientras aguarda en la X del mapa: respira, mira despacio a un lado
    /// y a otro, cambia el peso de pie, mece el velo y parpadea. Función pura del tiempo (segundos), sin dependencias de Unity.
    /// Todos los periodos son distintos para que los movimientos no se sincronicen y el conjunto no parezca un bucle corto.
    /// </summary>
    public static class ContactIdle
    {
        public const float BreathPeriod = 4.2f;
        public const float BreathExpandMax = 0.014f;
        public const float BreathLiftMax = 0.006f;

        public const float HeadPeriod = 9.3f;
        public const float HeadYawMax = 14f;
        public const float HeadPitchMax = 3f;
        public const float HeadRollMax = 2.5f;

        public const float SwayPeriod = 7.7f;
        public const float SwayXMax = 0.012f;
        public const float SwayRollMax = 0.8f;

        public const float ArmPeriod = 5.9f;
        public const float ArmMax = 1.6f;

        public const float VeilPeriod = 6.4f;
        public const float VeilMax = 2.2f;

        public const float BlinkEvery = 4.6f;
        public const float BlinkDuration = 0.16f;

        private const float TwoPi = (float)(2.0 * Math.PI);

        // Viento (grados por m/s): el velo se levanta de cara y se pega a la espalda con el viento a favor.
        public const float VeilLiftPerWind = 3.0f, VeilLiftMax = 8f, VeilPressMax = -1.5f;
        public const float VeilRollPerWind = 3.5f, VeilRollMax = 8f;
        public const float DrapePerWind = 3.0f, DrapeForwardMax = -8f, DrapeBackMax = 2f;
        public const float SkirtPerWind = 1.8f, SkirtMax = 5f;
        public const float FlutterPerWind = 0.7f, FlutterRate = 0.85f; // temblor del velo por m/s de viento (grados) y su frecuencia (Hz)

        /// <param name="windForward">m/s del viento hacia donde mira ella (positivo = le da por la espalda).</param>
        /// <param name="windRight">m/s del viento hacia su derecha.</param>
        public static ContactIdlePose Sample(float time, float windForward = 0f, float windRight = 0f)
        {
            float breath = (float)Math.Sin(TwoPi * time / BreathPeriod);
            float head = TwoPi * time / HeadPeriod;
            float sway = (float)Math.Sin(TwoPi * time / SwayPeriod);
            float arm = TwoPi * time / ArmPeriod;
            float windSpeed = (float)Math.Sqrt(windForward * windForward + windRight * windRight);
            float flutter = FlutterPerWind * windSpeed * (float)Math.Sin(TwoPi * FlutterRate * time);

            return new ContactIdlePose
            {
                // Al inspirar el pecho se ensancha y los hombros suben; al soltar el aire vuelven al reposo.
                BreathExpand = BreathExpandMax * 0.5f * (breath + 1f),
                BreathLift = BreathLiftMax * 0.5f * (breath + 1f),
                // La cabeza se vuelve despacio a un lado y a otro, con un cabeceo y un ladeo más lentos que se descompasan.
                HeadYaw = HeadYawMax * (float)Math.Sin(head),
                HeadPitch = HeadPitchMax * (float)Math.Sin(head * 0.7f + 1.1f),
                HeadRoll = HeadRollMax * (float)Math.Sin(head * 0.5f + 2.3f),
                SwayX = SwayXMax * sway,
                // El viento la empuja un poco hacia donde sopla.
                SwayRoll = SwayRollMax * sway + Clamp(windRight * 0.4f, -1f, 1f),
                // Los brazos se mecen en sentido contrario, casi imperceptibles.
                LeftArm = ArmMax * (float)Math.Sin(arm),
                RightArm = -ArmMax * (float)Math.Sin(arm + 0.6f),
                // Con el viento de cara el velo se levanta hacia atrás; con el viento a favor se le pega a la espalda (sin atravesarla).
                VeilSwing = VeilMax * (float)Math.Sin(TwoPi * time / VeilPeriod + 0.8f)
                    + Clamp(-windForward * VeilLiftPerWind, VeilPressMax, VeilLiftMax) + flutter,
                Blink = BlinkAt(time),
                // El velo, ladeado hacia donde sopla.
                VeilRoll = Clamp(windRight * VeilRollPerWind, -VeilRollMax, VeilRollMax) + 0.5f * flutter,
                // La gasa que cae sobre el rostro se aparta hacia delante con el viento a favor y se pega con el de cara.
                DrapeSwing = Clamp(-windForward * DrapePerWind, DrapeForwardMax, DrapeBackMax) + flutter * 0.8f,
                // Bajo de la túnica: se echa hacia delante con el viento por la espalda y se ladea hacia donde sopla.
                SkirtPitch = Clamp(-windForward * SkirtPerWind, -SkirtMax, SkirtMax),
                SkirtRoll = Clamp(windRight * SkirtPerWind, -SkirtMax, SkirtMax) + 0.3f * flutter
            };
        }

        private static float Clamp(float value, float min, float max)
        {
            return value < min ? min : (value > max ? max : value);
        }

        /// <summary>Cierre de los párpados: un parpadeo corto y suave cada <see cref="BlinkEvery"/> segundos.</summary>
        public static float BlinkAt(float time)
        {
            float phase = time - BlinkEvery * (float)Math.Floor(time / BlinkEvery);
            if (phase >= BlinkDuration) return 0f;
            return (float)Math.Sin(Math.PI * phase / BlinkDuration);
        }
    }
}
