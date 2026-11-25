using Qazrix.AI;
using Qazrix.Gameplay;
using Qazrix.Input;
using Qazrix.Simulation.AI;
using Qazrix.Simulation.Gameplay;
using Qazrix.Simulation.Movement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Qazrix.Prototype
{
    /// <summary>
    /// Panel de depuración del prototipo (spec §14.2.3): controles, versión del editor, pipeline,
    /// fps y estado del jugador. F1 o Select lo ocultan. Los controles mostrados son los de fábrica. IMGUI basta para un panel de desarrollo.
    /// </summary>
    public sealed class PrototypeHud : MonoBehaviour
    {
        [SerializeField] private PrototypePlayerController player;
        [SerializeField] private QazrixInput inputReader;
        [SerializeField] private GuardController guard;
        [SerializeField] private bool visible = true;

        private float _smoothedDelta = 1f / 60f;
        private GUIStyle _style;

        public void Configure(PrototypePlayerController playerController, QazrixInput reader)
        {
            inputReader = reader;
            player = playerController;
        }

        public bool IsVisible => visible;

        public void ShowGuard(GuardController watched)
        {
            guard = watched;
        }

        private PlayerInteractor _interactor;

        private string ProgressLine()
        {
            if (player == null) return string.Empty;
            if (_interactor == null) _interactor = player.GetComponent<PlayerInteractor>();
            string checkpoint = string.IsNullOrEmpty(player.CheckpointLabel) ? "inicio" : player.CheckpointLabel;
            int items = _interactor != null ? _interactor.ItemsCollected : 0;
            return $"Reaparición: {checkpoint} · cartas: {items}\n";
        }

        private string ParkourHint()
        {
            if (player == null) return "\n";
            if (_interactor != null && _interactor.Current != null) return $"<b>E / X: {_interactor.Current.Prompt}</b>\n";
            if (player.IsClimbing) return "<b>Espacio / A: saltar hacia atrás</b>\n";
            if (player.FaithLeapAvailable) return "<b>Espacio / A: salto de fe</b>\n";
            switch (player.AvailableParkour)
            {
                case ParkourAction.Vault: return "<b>Espacio / A: saltar el obstáculo</b>\n";
                case ParkourAction.Climb: return "<b>Espacio / A: trepar</b>\n";
                default: return "\n";
            }
        }

        private static string StateName(AlertState state)
        {
            switch (state)
            {
                case AlertState.Suspicious: return "sospecha";
                case AlertState.Investigate: return "investiga";
                case AlertState.Alert: return "ALARMA";
                case AlertState.Search: return "busca";
                case AlertState.Return: return "vuelve";
                default: return "patrulla";
            }
        }

        public static string PipelineName
        {
            get
            {
                RenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline;
                return asset == null ? "Built-in RP" : asset.GetType().Name;
            }
        }

        private bool _inventoryOpen;
        private CursorLockMode _previousLock;

        public bool IsInventoryOpen => _inventoryOpen;

        private void SetInventoryOpen(bool open)
        {
            _inventoryOpen = open;
            inputReader.GameplayBlocked = open;
            if (open)
            {
                _previousLock = Cursor.lockState;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = _previousLock;
                Cursor.visible = _previousLock != CursorLockMode.Locked;
            }
        }

        private void OnDisable()
        {
            if (_inventoryOpen && inputReader != null) SetInventoryOpen(false);
        }

        private MapPanel _map;

        public bool IsMapOpen => _map != null && _map.IsOpen;

        private void Update()
        {
            // El mapa se crea aquí, no en la escena: así lo tienen todas las escenas sin regenerarlas.
            if (_map == null && inputReader != null && player != null)
            {
                _map = GetComponent<MapPanel>();
                if (_map == null) _map = gameObject.AddComponent<MapPanel>();
                _map.Configure(inputReader, player.transform);
            }

            if (inputReader != null)
            {
                InputFrame frame = inputReader.Read();
                if (frame.ToggleHudPressed) visible = !visible;
                // Con otro panel abierto (controles) el bloqueo ya está puesto: no se abre encima.
                if (frame.ToggleInventoryPressed && (_inventoryOpen || !inputReader.GameplayBlocked))
                    SetInventoryOpen(!_inventoryOpen);
            }
            _smoothedDelta = Mathf.Lerp(_smoothedDelta, Time.unscaledDeltaTime, 0.05f);
        }

        private void DrawInventory()
        {
            if (_interactor == null && player != null) _interactor = player.GetComponent<PlayerInteractor>();

            const float width = 360f, height = 300f;
            var area = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.Box(area, "Inventario");

            Inventory inventory = _interactor != null ? _interactor.Inventory : null;
            if (inventory == null || inventory.IsEmpty)
            {
                GUI.Label(new Rect(area.x + 16f, area.y + 36f, area.width - 32f, 28f), "Vacío", _style);
            }
            else
            {
                // Cada objeto es un botón; los que señalan un punto del mapa (la carta) lo muestran al elegirlos.
                float y = area.y + 38f;
                for (int i = 0; i < inventory.Entries.Count; i++)
                {
                    InventoryEntry entry = inventory.Entries[i];
                    string label = entry.Count > 1 ? $"{entry.Name} ×{entry.Count}" : entry.Name;
                    if (entry.HasMark) label += "  — ver en el mapa";
                    GUI.enabled = entry.HasMark;
                    if (GUI.Button(new Rect(area.x + 16f, y, area.width - 32f, 30f), label)) SelectInventoryEntry(i);
                    GUI.enabled = true;
                    y += 34f;
                }
            }

            GUI.Label(new Rect(area.x + 16f, area.yMax - 34f, area.width - 32f, 26f), "Cerrar: I · Y", _style);
        }

        /// <summary>
        /// Elige un objeto del inventario. Si señala un punto del mundo (una carta), cierra el inventario y abre el mapa
        /// con una cruz roja en ese punto.
        /// </summary>
        public void SelectInventoryEntry(int index)
        {
            if (!_inventoryOpen || _interactor == null || _map == null) return;
            var entries = _interactor.Inventory.Entries;
            if (index < 0 || index >= entries.Count || !entries[index].HasMark) return;

            InventoryEntry entry = entries[index];
            SetInventoryOpen(false);
            // Ya has tocado a la mujer del velo: la carta no vuelve a marcar nada. La llave sí señala el alcázar.
            if (_map.ObjectiveReached && entry.Id == "carta") return;
            // La carta señala la X del objetivo (al azar en cada partida); si aún no hay, el punto fijo guardado en la carta.
            if (!_map.OpenObjective()) _map.OpenAt(new Vector3(entry.MarkX, 0f, entry.MarkZ));
        }

        /// <summary>Icono de una vida extra: la cabeza del hidalgo (Assets/Resources/UI/Vida_Hidalgo.png, generado por LifeIconBuilder).</summary>
        public const string LifeIconResource = "UI/Vida_Hidalgo";
        public const float LifeIconSize = 40f, LifeIconGap = 6f, LifeIconMargin = 16f;

        private Texture2D _lifeIcon;
        private bool _lifeIconTried;

        /// <summary>Rectángulo de la vida extra número <paramref name="index"/> (0 = la más a la derecha) en una pantalla de ese ancho.</summary>
        public static Rect LifeIconRect(int index, float screenWidth)
        {
            float x = screenWidth - LifeIconMargin - (index + 1) * LifeIconSize - index * LifeIconGap;
            return new Rect(x, LifeIconMargin, LifeIconSize, LifeIconSize);
        }

        /// <summary>Las vidas de reserva (sin contar la que se juega) como cabezas pequeñas del protagonista, arriba a la derecha.</summary>
        private void DrawExtraLives()
        {
            if (player == null) return;
            int extra = player.Lives.ExtraLives;
            if (extra <= 0) return;
            if (!_lifeIconTried)
            {
                _lifeIconTried = true;
                _lifeIcon = Resources.Load<Texture2D>(LifeIconResource);
                if (_lifeIcon == null) Debug.LogWarning($"[Qazrix] Falta el icono de vida: Resources/{LifeIconResource} (menú Qazrix → Arte → Icono de vida).");
            }
            if (_lifeIcon == null) return;
            for (int i = 0; i < extra; i++)
                GUI.DrawTexture(LifeIconRect(i, Screen.width), _lifeIcon, ScaleMode.ScaleToFit, true);
        }

        private GUIStyle _gameOverStyle;

        /// <summary>Fin de partida: velo oscuro sobre todo el juego y el mensaje en el centro.</summary>
        private void DrawGameOver()
        {
            if (player == null || player.GameOverSeconds < 0f) return;
            float seconds = player.GameOverSeconds;

            Color saved = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, Qazrix.Simulation.Gameplay.GameOverScreen.Darkness(seconds));
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

            if (_gameOverStyle == null)
            {
                _gameOverStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
            }
            _gameOverStyle.fontSize = Mathf.Max(22, Mathf.RoundToInt(Screen.height * 0.055f));
            _gameOverStyle.normal.textColor = Color.white;
            GUI.color = new Color(1f, 1f, 1f, Qazrix.Simulation.Gameplay.GameOverScreen.TextAlpha(seconds));
            GUI.Label(new Rect(Screen.width * 0.08f, 0f, Screen.width * 0.84f, Screen.height), Qazrix.Simulation.Gameplay.GameOverScreen.Message, _gameOverStyle);
            GUI.color = saved;
        }

        private void OnGUI()
        {
            DrawMain();
            DrawGameOver(); // lo último: tapa también el panel de depuración y el inventario
        }

        private void DrawMain()
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
                _style.normal.textColor = Color.white;
            }

            DrawExtraLives();
            if (_inventoryOpen) DrawInventory();
            if (!visible) return;

            string state = player == null
                ? "sin jugador"
                : $"{player.HorizontalSpeed:0.0} m/s · {(player.IsGrounded ? "en suelo" : "en el aire")}{(player.IsCrouching ? " · agachado" : "")}";

            string text =
                $"<b>QAZRIX v{Application.version}</b> — prototipo F0\n" +
                $"Unity {Application.unityVersion} · {PipelineName}\n" +
                $"{1f / Mathf.Max(_smoothedDelta, 0.0001f):0} fps ({_smoothedDelta * 1000f:0.0} ms) · {state}\n" +
                (guard != null ? $"Guardia: {StateName(guard.State)} · sospecha {guard.Awareness * 100f:0} %\n" : "") +
                ProgressLine() +
                ParkourHint() + "\n" +
                "Mover: WASD · stick izq.\n" +
                "Flechas: arriba/abajo avanzar y retroceder · izq./der. girar\n" +
                "Correr: Mayús izq. · L3 / gatillo der.\n" +
                "Saltar: Espacio · A\n" +
                "Trepar: correr contra una fachada · agacharse suelta · Espacio salta hacia atrás\n" +
                "Agacharse: C / Ctrl · B\n" +
                "Interactuar: E · X\n" +
                "Inventario: I · Y\n" +
                "Mapa: M · cruceta izq.\n" +
                "Cámara: ratón (clic captura, Esc suelta) · stick der.\n" +
                "Zoom: rueda · cruceta arriba/abajo\n" +
                "Ocultar panel: F1 · Select\n" +
                "Cambiar teclas: F2 · Start\n" +
                "Cambiar de escena: F3";

            var area = new Rect(12f, 12f, 420f, 408f);
            GUI.Box(area, GUIContent.none);
            GUI.Label(new Rect(area.x + 10f, area.y + 8f, area.width - 20f, area.height - 16f), text, _style);
        }
    }
}
