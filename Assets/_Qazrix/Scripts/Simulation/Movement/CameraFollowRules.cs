using System;

namespace Qazrix.Simulation.Movement
{
    /// <summary>
    /// Cuándo la cámara se recoloca detrás del personaje, hacia donde mira. Pura, sin Unity.
    /// Con WASD (movimiento relativo a la cámara) solo sigue cuando se avanza hacia delante: si siguiera
    /// también al ir de lado, la cámara giraría tras el personaje sin parar y se entraría en un círculo.
    /// Con las flechas (control de tanque) sigue siempre que se avance, retroceda o gire.
    /// </summary>
    public static class CameraFollowRules
    {
        /// <summary>Palanca mínima para que cuente como pulsada.</summary>
        public const float Deadzone = 0.1f;

        /// <param name="enabled">La opción está activada.</param>
        /// <param name="blocked">Hay un panel abierto: no hay control del juego.</param>
        /// <param name="secondsSinceManualLook">Tiempo desde que el jugador movió la cámara a mano.</param>
        /// <param name="manualHoldSeconds">Tiempo que se respeta una cámara movida a mano.</param>
        /// <param name="moveForward">Componente hacia delante de WASD o del stick izquierdo (−1 a 1).</param>
        /// <param name="tankTurn">Flechas izquierda/derecha (−1 a 1).</param>
        /// <param name="tankForward">Flechas arriba/abajo (−1 a 1).</param>
        public static bool ShouldFollow(bool enabled, bool blocked, float secondsSinceManualLook, float manualHoldSeconds,
            float moveForward, float tankTurn, float tankForward)
        {
            if (!enabled || blocked) return false;
            if (secondsSinceManualLook < manualHoldSeconds) return false; // el jugador acaba de orientar la cámara
            bool advancing = moveForward > Deadzone;
            bool tank = Math.Abs(tankTurn) > Deadzone || Math.Abs(tankForward) > Deadzone;
            return advancing || tank;
        }

        /// <summary>
        /// ¿Empieza ahora la media vuelta? Ocurre al pulsar «atrás» (WASD o stick hacia atrás): la cámara gira 180° y
        /// se queda ahí, y el personaje se da la vuelta con ella. Solo en el momento de pulsar (no mientras se mantiene),
        /// sin otra media vuelta en curso y si el jugador no acaba de mover la cámara a mano.
        /// </summary>
        public static bool ShouldStartTurnAround(bool enabled, bool blocked, float secondsSinceManualLook, float manualHoldSeconds,
            float moveForward, bool backWasPressed, bool turning)
        {
            if (!enabled || blocked || turning || backWasPressed) return false;
            if (secondsSinceManualLook < manualHoldSeconds) return false;
            return moveForward < -Deadzone;
        }

        /// <summary>
        /// Orientación con la que se interpreta WASD. Durante una media vuelta, y mientras se siga manteniendo
        /// «atrás», se usa la orientación que tenía la cámara al empezar: así el personaje sigue yendo hacia el mismo
        /// sitio mientras la cámara se pone detrás de él, en vez de dar la vuelta otra vez.
        /// </summary>
        public static float MovementYaw(float cameraYaw, float latchedYaw, bool latched)
        {
            return latched ? latchedYaw : cameraYaw;
        }

        /// <summary>Un paso de la media vuelta: acerca el ángulo al destino sin pasarse, a velocidad constante.</summary>
        public static float TurnAroundStep(float current, float target, float degreesPerSecond, float deltaTime)
        {
            float step = Math.Max(0f, degreesPerSecond) * Math.Max(0f, deltaTime);
            float remaining = target - current;
            if (Math.Abs(remaining) <= step) return target;
            return current + Math.Sign(remaining) * step;
        }
    }
}
