using System;
using Qazrix.Simulation.AI;

namespace Qazrix.Simulation.Movement
{
    /// <summary>Medidas de la trepada libre por fachadas (spec §6.1). Valores de prototipo.</summary>
    [Serializable]
    public sealed class ClimbLimits
    {
        public float UpSpeed = 3f;          // m/s subiendo
        public float DownSpeed = 3.5f;      // m/s bajando
        public float SideSpeed = 1.6f;      // m/s de lado
        public float MinFacing = 0.6f;      // coseno mínimo entre la marcha y la pared
        public float MaxWallTilt = 0.3f;    // |normal.y| máximo para que cuente como pared
        public float GrabHeight = 1.1f;     // altura sobre los pies a la que se agarra
        public float MantleSeconds = 0.4f;  // tiempo de encaramarse al borde
        public float BackJumpSpeed = 5f;    // m/s horizontales al saltar hacia atrás, separándose de la pared
        public float BackJumpUp = 5.5f;     // m/s hacia arriba en ese salto
    }

    /// <summary>
    /// Reglas puras de la trepada libre: cuándo se puede agarrar una pared, a qué velocidad se sube y
    /// por dónde pasa el cuerpo al encaramarse al tejado. Sin dependencias de Unity.
    /// </summary>
    public static class ClimbRules
    {
        /// <summary>
        /// ¿Puede agarrarse a la pared que tiene delante? Hace falta ir corriendo hacia ella, de frente,
        /// de pie, y que la superficie sea trepable y casi vertical.
        /// </summary>
        public static bool CanGrab(bool wantsRun, bool isMoving, bool isCrouching, bool climbable,
            float facing, float wallNormalY, ClimbLimits limits)
        {
            if (limits == null) throw new ArgumentNullException(nameof(limits));
            if (!wantsRun || !isMoving || isCrouching || !climbable) return false;
            return facing >= limits.MinFacing && Math.Abs(wallNormalY) <= limits.MaxWallTilt;
        }

        /// <summary>
        /// ¿Puede saltar hacia atrás? Solo agarrado a la pared y sin estar pasando ya al tejado; con el
        /// cuerpo en pleno encaramado el salto no tiene de dónde impulsarse.
        /// </summary>
        public static bool CanJumpBack(bool isClimbing, bool isMantling, bool jumpPressed)
        {
            return isClimbing && !isMantling && jumpPressed;
        }

        /// <summary>
        /// Velocidad de salida del salto hacia atrás: se separa de la pared (opuesto a <paramref name="intoWall"/>,
        /// que apunta hacia dentro de ella) y sube. La componente horizontal de la entrada se normaliza.
        /// </summary>
        public static Float3 BackJumpVelocity(Float3 intoWall, ClimbLimits limits)
        {
            if (limits == null) throw new ArgumentNullException(nameof(limits));
            double length = Math.Sqrt(intoWall.X * intoWall.X + intoWall.Z * intoWall.Z);
            if (length < 1e-6) return new Float3(0f, limits.BackJumpUp, 0f);
            float awayX = (float)(-intoWall.X / length), awayZ = (float)(-intoWall.Z / length);
            return new Float3(awayX * limits.BackJumpSpeed, limits.BackJumpUp, awayZ * limits.BackJumpSpeed);
        }

        /// <summary>Velocidad vertical según la palanca (adelante sube, atrás baja).</summary>
        public static float VerticalSpeed(float input, ClimbLimits limits)
        {
            if (limits == null) throw new ArgumentNullException(nameof(limits));
            float clamped = Math.Max(-1f, Math.Min(1f, input));
            if (Math.Abs(clamped) < 0.2f) return 0f; // zona muerta: se queda colgado
            return clamped > 0f ? clamped * limits.UpSpeed : clamped * limits.DownSpeed;
        }

        /// <summary>
        /// Posición de los pies al encaramarse: primero sube hasta la cota del borde y después entra
        /// en el tejado, para no atravesar la esquina del muro.
        /// </summary>
        public static Float3 Mantle(Float3 start, Float3 end, float progress)
        {
            float t = Math.Max(0f, Math.Min(1f, progress));
            const float riseShare = 0.6f;
            if (t <= riseShare)
            {
                float k = t / riseShare;
                return new Float3(start.X, start.Y + (end.Y - start.Y) * k, start.Z);
            }
            float f = (t - riseShare) / (1f - riseShare);
            return new Float3(start.X + (end.X - start.X) * f, end.Y, start.Z + (end.Z - start.Z) * f);
        }
    }
}
