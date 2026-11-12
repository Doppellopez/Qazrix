using System;
using Qazrix.Simulation.AI;

namespace Qazrix.Simulation.Movement
{
    /// <summary>Tipo de paso que un diseñador autoriza en un obstáculo marcado.</summary>
    public enum ParkourKind
    {
        Vault,  // pasar por encima de un obstáculo bajo y estrecho
        Climb   // encaramarse a una repisa o plataforma
    }

    public enum ParkourAction
    {
        None,
        Vault,
        Climb
    }

    /// <summary>Rangos medidos dentro de los que se permite cada acción. Valores de partida.</summary>
    [Serializable]
    public sealed class ParkourLimits
    {
        /// <summary>Por debajo de esto no es un obstáculo: se sube andando.</summary>
        public float MinHeight = 0.3f;

        public float MaxVaultHeight = 1.1f;
        public float MaxVaultDepth = 1.2f;
        public float MaxClimbHeight = 2.3f;
    }

    /// <summary>
    /// Parkour contextual y predecible (spec §6.1, D09): solo en obstáculos marcados y dentro de rangos
    /// medidos. Aquí se decide si se puede y por dónde pasa el cuerpo; las medidas las toma la vista.
    /// </summary>
    public static class ParkourRules
    {
        /// <param name="kind">Lo que autoriza la marca del obstáculo.</param>
        /// <param name="height">Altura del borde superior sobre los pies del jugador, en metros.</param>
        /// <param name="depth">Grosor del obstáculo en la dirección de avance, en metros.</param>
        /// <param name="landingClear">Hay sitio libre para el cuerpo en el punto de llegada.</param>
        public static ParkourAction Decide(ParkourKind kind, float height, float depth, bool landingClear, ParkourLimits limits)
        {
            if (limits == null) throw new ArgumentNullException(nameof(limits));
            if (!landingClear || height < limits.MinHeight) return ParkourAction.None;

            switch (kind)
            {
                case ParkourKind.Vault:
                    return height <= limits.MaxVaultHeight && depth <= limits.MaxVaultDepth
                        ? ParkourAction.Vault
                        : ParkourAction.None;
                case ParkourKind.Climb:
                    return height <= limits.MaxClimbHeight ? ParkourAction.Climb : ParkourAction.None;
                default:
                    return ParkourAction.None;
            }
        }

        /// <summary>Segundos que dura el movimiento: más alto, más lento.</summary>
        public static float Duration(ParkourAction action, float height)
        {
            height = Math.Max(0f, height);
            switch (action)
            {
                case ParkourAction.Vault: return 0.45f + 0.1f * height;
                case ParkourAction.Climb: return 0.4f + 0.35f * height;
                default: return 0f;
            }
        }
    }

    /// <summary>Trayectoria del cuerpo durante una acción de parkour. Determinista: depende solo de t.</summary>
    public static class ParkourPath
    {
        /// <summary>Margen con el que los pies pasan por encima del obstáculo en un salto.</summary>
        public const float VaultClearance = 0.35f;

        /// <summary>Fracción del tiempo que una trepada dedica a subir antes de avanzar.</summary>
        public const float ClimbRiseShare = 0.6f;

        /// <param name="obstacleTop">Cota del borde superior del obstáculo.</param>
        /// <param name="t">Avance entre 0 (inicio) y 1 (final).</param>
        public static Float3 Evaluate(ParkourAction action, Float3 start, Float3 end, float obstacleTop, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));

            if (action == ParkourAction.Climb)
            {
                // Primero sube pegado a la pared; luego avanza sobre la repisa.
                if (t < ClimbRiseShare)
                {
                    float u = Smooth(t / ClimbRiseShare);
                    return new Float3(start.X, Lerp(start.Y, end.Y, u), start.Z);
                }
                float v = Smooth((t - ClimbRiseShare) / (1f - ClimbRiseShare));
                return new Float3(Lerp(start.X, end.X, v), end.Y, Lerp(start.Z, end.Z, v));
            }

            // Salto: avance uniforme y arco que supera el borde con margen.
            float floor = Math.Min(start.Y, end.Y);
            float arc = Math.Max(obstacleTop - floor, 0f) + VaultClearance;
            float y = Lerp(start.Y, end.Y, t) + arc * 4f * t * (1f - t);
            return new Float3(Lerp(start.X, end.X, t), y, Lerp(start.Z, end.Z, t));
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
