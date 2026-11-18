using System;

namespace Qazrix.Simulation.Movement
{
    /// <summary>
    /// Posturas en el aire de un personaje bípedo con un solo pivote por extremidad. La postura sale
    /// de la velocidad vertical: al subir, los brazos se lanzan hacia arriba y una rodilla va delante;
    /// en lo alto se recoge; al caer, los brazos se abren arriba y bracean, y las piernas pedalean
    /// buscando el suelo. Al tocar tierra hay una pose breve de amortiguación. Sin dependencias de Unity.
    /// Ángulos en grados, positivo = hacia delante; 180 = recto hacia arriba.
    /// </summary>
    public static class AirCycle
    {
        /// <summary>Velocidad vertical (m/s) a la que la postura de subida o de caída es completa.</summary>
        public const float FullSpeed = 5f;

        public const float RiseArm = 125f, ApexArm = 70f, FallArm = 150f;
        public const float RiseLeadLeg = 50f, ApexLeadLeg = 28f, FallLeg = 16f;
        public const float RiseTrailLeg = -32f, ApexTrailLeg = -8f;

        /// <summary>Braceo y pedaleo durante la caída: amplitud (grados) y ritmo (ciclos por segundo).</summary>
        public const float FlailArm = 18f, FlailLeg = 14f, FlailRate = 2.5f;

        /// <summary>Segundos que dura la pose de amortiguación al aterrizar.</summary>
        public const float LandingSeconds = 0.22f;
        public const float LandingArm = 48f, LandingLeg = 26f;

        /// <summary>De −1 (cayendo a fondo) a 1 (subiendo a fondo); 0 en lo alto del salto.</summary>
        public static float Blend(float verticalSpeed)
        {
            return Math.Max(-1f, Math.Min(1f, verticalSpeed / FullSpeed));
        }

        /// <summary>Ángulo del brazo. Los dos suben juntos; al caer bracean alternándose.</summary>
        public static float ArmAngle(float verticalSpeed, float airSeconds, bool left)
        {
            float b = Blend(verticalSpeed);
            if (b >= 0f) return ApexArm + (RiseArm - ApexArm) * b;
            float fall = -b;
            float wave = (float)Math.Sin(airSeconds * FlailRate * 2.0 * Math.PI);
            return ApexArm + (FallArm - ApexArm) * fall + (left ? wave : -wave) * FlailArm * fall;
        }

        /// <summary>Ángulo de la pierna. La izquierda es la que va delante en la subida.</summary>
        public static float LegAngle(float verticalSpeed, float airSeconds, bool left)
        {
            float b = Blend(verticalSpeed);
            float apex = left ? ApexLeadLeg : ApexTrailLeg;
            if (b >= 0f) return apex + ((left ? RiseLeadLeg : RiseTrailLeg) - apex) * b;
            float fall = -b;
            float wave = (float)Math.Sin(airSeconds * FlailRate * 2.0 * Math.PI);
            return apex + (FallLeg - apex) * fall + (left ? -wave : wave) * FlailLeg * fall;
        }

        /// <summary>Flexión de rodilla (grados, hacia atrás) al subir, en lo alto y al caer: la que va delante se recoge más.</summary>
        public const float RiseLeadKnee = 85f, RiseTrailKnee = 55f, ApexLeadKnee = 70f, ApexTrailKnee = 60f, FallKnee = 28f;
        public const float FlailKnee = 10f, LandingKnee = 55f;

        /// <summary>Rodilla en el aire: se dobla al despegar, se recoge arriba y se estira a medias al caer.</summary>
        public static float KneeAngle(float verticalSpeed, float airSeconds, bool left)
        {
            float b = Blend(verticalSpeed);
            float apex = left ? ApexLeadKnee : ApexTrailKnee;
            if (b >= 0f) return apex + ((left ? RiseLeadKnee : RiseTrailKnee) - apex) * b;
            float fall = -b;
            float wave = (float)Math.Sin(airSeconds * FlailRate * 2.0 * Math.PI);
            return apex + (FallKnee - apex) * fall + (left ? wave : -wave) * FlailKnee * fall;
        }

        /// <summary>Al aterrizar las dos rodillas se flexionan para amortiguar y se estiran al acabar la pose.</summary>
        public static float LandingKneeAngle(float secondsSinceLanding)
        {
            return LandingKnee * LandingWeight(secondsSinceLanding);
        }

        /// <summary>¿Sigue la pose de amortiguación tras ese tiempo en el suelo?</summary>
        public static bool IsLanding(float secondsSinceLanding)
        {
            return secondsSinceLanding >= 0f && secondsSinceLanding < LandingSeconds;
        }

        /// <summary>Peso de la amortiguación: 1 al tocar el suelo, 0 al acabar.</summary>
        public static float LandingWeight(float secondsSinceLanding)
        {
            if (!IsLanding(secondsSinceLanding)) return 0f;
            return 1f - secondsSinceLanding / LandingSeconds;
        }

        /// <summary>Al aterrizar los brazos caen hacia delante para equilibrar.</summary>
        public static float LandingArmAngle(float secondsSinceLanding)
        {
            return LandingArm * LandingWeight(secondsSinceLanding);
        }

        /// <summary>Al aterrizar las piernas se abren en zancada: la izquierda delante.</summary>
        public static float LandingLegAngle(float secondsSinceLanding, bool left)
        {
            return (left ? LandingLeg : -LandingLeg) * LandingWeight(secondsSinceLanding);
        }
    }
}
