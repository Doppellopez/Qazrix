using System;

namespace Qazrix.Simulation.Movement
{
    /// <summary>
    /// Lo que cambia al pasar de andar a correr: zancada más larga, piernas y brazos con más vuelo, rodillas y codos
    /// que se doblan, el cuerpo inclinado hacia delante y un pequeño rebote en cada paso. Todo sale de una mezcla
    /// (<see cref="Blend"/>) que sube con la velocidad, así que no hay un salto brusco entre andar y correr.
    /// Lógica pura, sin Unity.
    /// </summary>
    public static class RunCycle
    {
        /// <summary>Por debajo de esta velocidad (m/s) se anda; a partir de <see cref="FullSpeed"/> se corre del todo.</summary>
        public const float StartSpeed = 3.0f, FullSpeed = 4.2f;

        /// <summary>Metros por ciclo completo (dos pasos) corriendo.</summary>
        public const float RunStrideLength = 2.8f;

        /// <summary>Cuánto más abren las piernas y los brazos corriendo, respecto a andar (0,25 = un 25 % más).</summary>
        public const float ExtraSwing = 0.25f;

        /// <summary>Doblez máximo de la rodilla y del codo corriendo, en grados.</summary>
        public const float MaxKnee = 85f, RunElbow = 90f;

        /// <summary>Inclinación del cuerpo hacia delante (grados) y altura del rebote (m) corriendo.</summary>
        public const float RunLean = 9f, RunBob = 0.05f;

        /// <summary>0 andando, 1 corriendo; suave entre las dos velocidades.</summary>
        public static float Blend(float speed)
        {
            float t = Math.Max(0f, Math.Min(1f, (speed - StartSpeed) / (FullSpeed - StartSpeed)));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Metros por ciclo para esa velocidad: crece al correr, porque los pasos son más largos.</summary>
        public static float StrideLength(float speed)
        {
            float b = Blend(speed);
            return WalkCycle.StrideLength + (RunStrideLength - WalkCycle.StrideLength) * b;
        }

        /// <summary>Factor del balanceo de piernas y brazos: 1 andando, 1,25 corriendo.</summary>
        public static float SwingScale(float speed) => 1f + ExtraSwing * Blend(speed);

        /// <summary>
        /// Doblez de la rodilla hacia atrás (grados, siempre ≥ 0). La pierna que se adelanta recoge el pie bajo el cuerpo;
        /// al apoyar casi se estira. Andando solo se afloja un poco. <paramref name="left"/> es la pierna izquierda.
        /// </summary>
        public static float KneeAngle(float phase, float speed, bool left)
        {
            if (speed < WalkCycle.StillSpeed) return 0f;
            double angle = 2.0 * Math.PI * (phase + (left ? 0.0 : 0.5));
            float swing = (float)Math.Max(0.0, Math.Cos(angle)); // 1 cuando la pierna pasa por debajo yendo hacia delante
            float b = Blend(speed);
            float walking = 14f * swing;
            float running = 10f + (MaxKnee - 10f) * swing;
            return walking + (running - walking) * b;
        }

        /// <summary>Doblez del codo hacia delante (grados): casi recto andando, en ángulo recto corriendo.</summary>
        public static float ElbowAngle(float speed)
        {
            if (speed < WalkCycle.StillSpeed) return 0f;
            float b = Blend(speed);
            return 8f + (RunElbow - 8f) * b;
        }

        /// <summary>Cuánto sube el cuerpo (m): dos botes por ciclo, el más alto cuando las piernas están abiertas.</summary>
        public static float BodyLift(float phase, float speed)
        {
            float b = Blend(speed);
            if (b <= 0f) return 0f;
            return RunBob * b * (float)(0.5 - 0.5 * Math.Cos(4.0 * Math.PI * phase));
        }

        /// <summary>Inclinación del cuerpo hacia delante (grados).</summary>
        public static float Lean(float speed) => RunLean * Blend(speed);
    }
}
