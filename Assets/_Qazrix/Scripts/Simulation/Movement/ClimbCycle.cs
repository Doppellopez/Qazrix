using System;

namespace Qazrix.Simulation.Movement
{
    /// <summary>
    /// Ciclo de escalada de un personaje bípedo con un solo pivote por extremidad: los brazos van por
    /// encima de la cabeza y se alternan para alcanzar el siguiente agarre, y las piernas empujan en
    /// diagonal (brazo izquierdo con pierna derecha). La fase avanza con lo que el cuerpo se desplaza
    /// por la pared, así que colgado y quieto conserva la postura. Sin dependencias de Unity.
    /// Ángulos en grados, positivo = hacia delante; 180 = recto hacia arriba.
    /// </summary>
    public static class ClimbCycle
    {
        /// <summary>Metros de pared que se recorren en un ciclo completo (un agarre con cada mano).</summary>
        public const float ReachLength = 1.1f;

        public const float ArmCentre = 148f;   // brazos levantados hacia la pared
        public const float ArmSwing = 24f;     // cuánto se estira y recoge cada brazo
        public const float LegCentre = 32f;    // rodillas hacia la pared
        public const float LegSwing = 30f;

        /// <summary>Avanza la fase (0..1) según la distancia recorrida por la pared, en cualquier sentido.</summary>
        public static float Advance(float phase, float distance)
        {
            phase += Math.Abs(distance) / ReachLength;
            return phase - (float)Math.Floor(phase);
        }

        /// <summary>Ángulo del brazo: el izquierdo se estira cuando el derecho se recoge.</summary>
        public static float ArmAngle(float phase, bool left)
        {
            float wave = (float)Math.Sin(phase * 2.0 * Math.PI);
            return ArmCentre + (left ? wave : -wave) * ArmSwing;
        }

        /// <summary>Ángulo de la pierna: sube la contraria al brazo que se estira.</summary>
        public static float LegAngle(float phase, bool left)
        {
            float wave = (float)Math.Sin(phase * 2.0 * Math.PI);
            return LegCentre + (left ? -wave : wave) * LegSwing;
        }

        /// <summary>Al encaramarse, los brazos pasan de estar arriba a empujar hacia abajo sobre el borde.</summary>
        public static float MantleArmAngle(float progress)
        {
            float t = Math.Max(0f, Math.Min(1f, progress));
            return ArmCentre + (25f - ArmCentre) * t;
        }

        /// <summary>Al encaramarse, una pierna sube al borde y la otra queda estirada; al final, de pie.</summary>
        public static float MantleLegAngle(float progress, bool left)
        {
            float t = Math.Max(0f, Math.Min(1f, progress));
            float lift = (float)Math.Sin(t * Math.PI); // sube y vuelve a cero
            return left ? 70f * lift : -10f * lift;
        }
    }
}
