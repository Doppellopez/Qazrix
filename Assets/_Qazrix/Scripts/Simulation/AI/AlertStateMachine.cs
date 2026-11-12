using System;

namespace Qazrix.Simulation.AI
{
    /// <summary>Estados de un guardia (spec §6.2): Patrol → Suspicious → Investigate → Alert → Search → Return.</summary>
    public enum AlertState
    {
        Patrol,
        Suspicious,
        Investigate,
        Alert,
        Search,
        Return
    }

    /// <summary>Lo que percibe el guardia en un paso. Lo calcula la vista (conos, rayos, sonido); aquí solo se decide.</summary>
    public readonly struct GuardPercept
    {
        /// <summary>Exposición del jugador entre 0 y 1. Debe ser 0 si no hay línea de visión.</summary>
        public readonly float Visibility;

        /// <summary>Ha oído un ruido relevante en este paso.</summary>
        public readonly bool NoiseHeard;

        /// <summary>Está en su puesto o ruta de patrulla (solo importa en Return).</summary>
        public readonly bool AtPost;

        public GuardPercept(float visibility, bool noiseHeard = false, bool atPost = false)
        {
            Visibility = visibility < 0f ? 0f : (visibility > 1f ? 1f : visibility);
            NoiseHeard = noiseHeard;
            AtPost = atPost;
        }

        public static GuardPercept Nothing => new GuardPercept(0f);
        public bool SeesTarget => Visibility > 0f;
    }

    /// <summary>Tiempos y umbrales. Valores iniciales de trabajo: se ajustan jugando, no son definitivos.</summary>
    [Serializable]
    public sealed class AlertConfig
    {
        /// <summary>Sospecha ganada por segundo con exposición total.</summary>
        public float AwarenessGainPerSecond = 1.0f;

        /// <summary>Sospecha perdida por segundo sin ver al jugador.</summary>
        public float AwarenessDecayPerSecond = 0.25f;

        /// <summary>Sospecha a partir de la cual deja la patrulla y se queda mirando.</summary>
        public float SuspiciousThreshold = 0.35f;

        /// <summary>Segundos sin ver nada, estando en Suspicious, antes de ir a comprobar el sitio.</summary>
        public float SuspiciousLostSeconds = 1.5f;

        /// <summary>Segundos que dedica a investigar un punto.</summary>
        public float InvestigateSeconds = 6f;

        /// <summary>Segundos sin contacto visual, en Alert, antes de pasar a buscar.</summary>
        public float LoseContactSeconds = 3f;

        /// <summary>Segundos de búsqueda antes de rendirse.</summary>
        public float SearchSeconds = 10f;
    }

    /// <summary>
    /// Máquina de alerta determinista y sin dependencias de Unity. Se avanza con <see cref="Tick"/>;
    /// el mismo historial de percepciones produce siempre el mismo resultado.
    /// </summary>
    public sealed class AlertStateMachine
    {
        private readonly AlertConfig _config;
        private float _timer;

        public AlertStateMachine(AlertConfig config = null)
        {
            _config = config ?? new AlertConfig();
        }

        public AlertState State { get; private set; } = AlertState.Patrol;

        /// <summary>Sospecha acumulada, entre 0 y 1. Con 1 el guardia da la alarma.</summary>
        public float Awareness { get; private set; }

        /// <summary>Se dispara en cada cambio de estado: (anterior, nuevo).</summary>
        public event Action<AlertState, AlertState> StateChanged;

        public void Tick(float deltaSeconds, GuardPercept percept)
        {
            if (deltaSeconds <= 0f) return;

            UpdateAwareness(deltaSeconds, percept);

            switch (State)
            {
                case AlertState.Patrol:
                    if (Awareness >= 1f) Enter(AlertState.Alert);
                    else if (Awareness >= _config.SuspiciousThreshold) Enter(AlertState.Suspicious);
                    else if (percept.NoiseHeard) Enter(AlertState.Investigate);
                    break;

                case AlertState.Suspicious:
                    if (Awareness >= 1f) { Enter(AlertState.Alert); break; }
                    _timer = percept.SeesTarget ? 0f : _timer + deltaSeconds;
                    if (percept.NoiseHeard || _timer >= _config.SuspiciousLostSeconds) Enter(AlertState.Investigate);
                    break;

                case AlertState.Investigate:
                    if (Awareness >= 1f) { Enter(AlertState.Alert); break; }
                    if (percept.NoiseHeard) _timer = 0f; // un ruido nuevo reinicia la comprobación
                    else _timer += deltaSeconds;
                    if (_timer >= _config.InvestigateSeconds) Enter(AlertState.Return);
                    break;

                case AlertState.Alert:
                    _timer = percept.SeesTarget ? 0f : _timer + deltaSeconds;
                    if (_timer >= _config.LoseContactSeconds) Enter(AlertState.Search);
                    break;

                case AlertState.Search:
                    if (percept.SeesTarget) { Enter(AlertState.Alert); break; }
                    _timer += deltaSeconds;
                    if (_timer >= _config.SearchSeconds) Enter(AlertState.Return);
                    break;

                case AlertState.Return:
                    if (Awareness >= 1f) Enter(AlertState.Alert);
                    else if (Awareness >= _config.SuspiciousThreshold) Enter(AlertState.Suspicious);
                    else if (percept.NoiseHeard) Enter(AlertState.Investigate);
                    else if (percept.AtPost) Enter(AlertState.Patrol);
                    break;
            }
        }

        /// <summary>Da por terminada la alarma (por ejemplo, tras alcanzar al intruso) y vuelve a su puesto.</summary>
        public void GiveUp()
        {
            if (State != AlertState.Return) Enter(AlertState.Return);
        }

        private void UpdateAwareness(float dt, GuardPercept percept)
        {
            // En alerta o búsqueda el guardia ya sabe que hay alguien: la sospecha no baja.
            if (State == AlertState.Alert || State == AlertState.Search)
            {
                Awareness = 1f;
                return;
            }

            float delta = percept.SeesTarget
                ? percept.Visibility * _config.AwarenessGainPerSecond * dt
                : -_config.AwarenessDecayPerSecond * dt;

            Awareness = Math.Min(1f, Math.Max(0f, Awareness + delta));
        }

        private void Enter(AlertState next)
        {
            AlertState previous = State;
            State = next;
            _timer = 0f;
            if (next == AlertState.Alert) Awareness = 1f;
            if (next == AlertState.Return) Awareness = 0f; // se rinde: vuelve a empezar de cero
            StateChanged?.Invoke(previous, next);
        }
    }
}
