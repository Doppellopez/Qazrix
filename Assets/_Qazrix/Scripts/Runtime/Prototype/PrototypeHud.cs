using Qazrix.Input;
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
        [SerializeField] private bool visible = true;

        private float _smoothedDelta = 1f / 60f;
        private GUIStyle _style;

        public void Configure(PrototypePlayerController playerController, QazrixInput reader)
        {
            inputReader = reader;
            player = playerController;
        }

        public bool IsVisible => visible;

        public static string PipelineName
        {
            get
            {
                RenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline;
                return asset == null ? "Built-in RP" : asset.GetType().Name;
            }
        }

        private void Update()
        {
            if (inputReader != null && inputReader.Read().ToggleHudPressed) visible = !visible;
            _smoothedDelta = Mathf.Lerp(_smoothedDelta, Time.unscaledDeltaTime, 0.05f);
        }

        private void OnGUI()
        {
            if (!visible) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
                _style.normal.textColor = Color.white;
            }

            string state = player == null
                ? "sin jugador"
                : $"{player.HorizontalSpeed:0.0} m/s · {(player.IsGrounded ? "en suelo" : "en el aire")}{(player.IsCrouching ? " · agachado" : "")}";

            string text =
                $"<b>QAZRIX v{Application.version}</b> — prototipo F0\n" +
                $"Unity {Application.unityVersion} · {PipelineName}\n" +
                $"{1f / Mathf.Max(_smoothedDelta, 0.0001f):0} fps ({_smoothedDelta * 1000f:0.0} ms) · {state}\n\n" +
                "Mover: WASD / flechas · stick izq.\n" +
                "Correr: Mayús izq. · L3 / gatillo der.\n" +
                "Saltar: Espacio · A\n" +
                "Agacharse: C / Ctrl · B\n" +
                "Cámara: ratón (clic captura, Esc suelta) · stick der.\n" +
                "Zoom: rueda · cruceta arriba/abajo\n" +
                "Ocultar panel: F1 · Select\n" +
                "Cambiar teclas: F2 · Start";

            var area = new Rect(12f, 12f, 420f, 250f);
            GUI.Box(area, GUIContent.none);
            GUI.Label(new Rect(area.x + 10f, area.y + 8f, area.width - 20f, area.height - 16f), text, _style);
        }
    }
}
