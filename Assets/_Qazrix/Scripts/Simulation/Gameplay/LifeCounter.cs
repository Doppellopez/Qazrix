namespace Qazrix.Simulation.Gameplay
{
    /// <summary>
    /// Vidas de la partida: se empieza con <see cref="StartLives"/> (la que se está jugando y dos extra) y cada muerte
    /// gasta una. Sin vidas, fin de la partida. Sin dependencias de Unity.
    /// </summary>
    public sealed class LifeCounter
    {
        public const int StartLives = 3;

        /// <summary>Vidas que quedan, contando la que se está jugando.</summary>
        public int Lives { get; private set; } = StartLives;

        /// <summary>Vidas de reserva: las que se enseñan como cabezas pequeñas (la que se juega no cuenta).</summary>
        public int ExtraLives => Lives > 1 ? Lives - 1 : 0;

        public bool IsGameOver => Lives <= 0;

        /// <summary>Una muerte: gasta una vida (nunca baja de cero).</summary>
        public void Lose()
        {
            if (Lives > 0) Lives--;
        }

        /// <summary>Partida nueva: vuelve a las vidas de salida.</summary>
        public void Reset()
        {
            Lives = StartLives;
        }
    }
}
