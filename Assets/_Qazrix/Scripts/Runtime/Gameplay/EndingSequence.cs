using Qazrix.Input;
using Qazrix.Simulation.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Qazrix.Gameplay
{
    /// <summary>
    /// Final de la demo, tras darle la llave al capitán: el juego se funde a negro, sale un pergamino y en él se escribe a mano,
    /// letra a letra y con tipografía manuscrita (Caveat, SIL OFL), cómo cae la fortaleza de Qazrix (<see cref="EndingText"/>).
    /// Cuando termina de escribirse se puede pulsar cualquier tecla (o tocar) para volver a empezar. IMGUI, como el resto del HUD.
    /// </summary>
    public sealed class EndingSequence : MonoBehaviour
    {
        public const float FadeSeconds = 1.6f;            // el juego se oscurece
        public const float ParchmentSeconds = 1.2f;       // el pergamino aparece
        public const float HintDelay = 2.0f;              // tras escribirse el texto, hasta ofrecer volver a empezar
        public const string FontResource = "Fonts/Caveat";
        public const string Hint = "Pulsa una tecla para volver a empezar";

        /// <summary>Multiplicador de la velocidad de escritura (solo para pruebas).</summary>
        public static float TypingSpeed = 1f;

        private static EndingSequence _instance;
        private static Texture2D _parchment;

        private float _seconds = -1f;
        private QazrixInput _input;
        private Font _font;
        private GUIStyle _style, _hintStyle;

        public bool Started => _seconds >= 0f;

        /// <summary>Opacidad del negro (0–1).</summary>
        public float Darkness { get; private set; }

        /// <summary>Opacidad del pergamino (0–1).</summary>
        public float ParchmentAlpha { get; private set; }

        /// <summary>Letras escritas hasta ahora.</summary>
        public int VisibleChars { get; private set; }

        public bool TextComplete => VisibleChars >= EndingText.Message.Length;

        /// <summary>Se ofrece volver a empezar.</summary>
        public bool WaitingForKey { get; private set; }

        public static EndingSequence Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<EndingSequence>();
                    if (_instance == null) _instance = new GameObject("Final_pergamino").AddComponent<EndingSequence>();
                }
                return _instance;
            }
        }

        /// <summary>Empieza el final (una sola vez).</summary>
        public static void Trigger() => Instance.Begin();

        private void Begin()
        {
            if (Started) return;
            _seconds = 0f;
            _input = FindFirstObjectByType<QazrixInput>();
            if (_input != null) _input.GameplayBlocked = true;
        }

        private void Update()
        {
            if (!Started) return;
            _seconds += Time.unscaledDeltaTime;

            Darkness = Mathf.Clamp01(_seconds / FadeSeconds);
            ParchmentAlpha = Mathf.Clamp01((_seconds - FadeSeconds) / ParchmentSeconds);
            float typed = Mathf.Max(0f, _seconds - FadeSeconds - ParchmentSeconds) * TypingSpeed;
            VisibleChars = HandwritingReveal.VisibleCount(EndingText.Message, typed);

            float done = HandwritingReveal.TotalSeconds(EndingText.Message) / Mathf.Max(0.01f, TypingSpeed);
            WaitingForKey = TextComplete && _seconds - FadeSeconds - ParchmentSeconds - done >= HintDelay;
            if (WaitingForKey && AnyPress()) Restart();
        }

        private static bool AnyPress()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) return true;
            return Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
        }

        private void Restart()
        {
            if (_input != null) _input.GameplayBlocked = false;
            _seconds = -1f;
            Scene scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void OnGUI()
        {
            if (!Started || Darkness <= 0.001f) return;
            GUI.depth = -2000;
            Color saved = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, Darkness);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

            if (ParchmentAlpha > 0.001f)
            {
                if (_parchment == null) _parchment = MakeParchment(512, 384);
                float width = Mathf.Min(Screen.width * 0.86f, Screen.height * 1.45f), height = width * 0.75f;
                if (height > Screen.height * 0.9f) { height = Screen.height * 0.9f; width = height / 0.75f; }
                var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
                GUI.color = new Color(1f, 1f, 1f, ParchmentAlpha);
                GUI.DrawTexture(rect, _parchment, ScaleMode.StretchToFill, true);

                // Texto: dentro del papel, lejos de los bordes quemados, con el tamaño de letra justo para que quepa entero.
                var textRect = new Rect(rect.x + width * 0.12f, rect.y + height * 0.13f, width * 0.76f, height * 0.66f);
                EnsureStyles(height, textRect);
                string text = EndingText.Message;
                int n = Mathf.Clamp(VisibleChars, 0, text.Length);
                // Lo que falta por escribir se pone transparente en vez de quitarlo: así las palabras no saltan de línea al escribirse.
                string shown = text.Substring(0, n) + "<color=#00000000>" + text.Substring(n) + "</color>";
                GUI.color = new Color(1f, 1f, 1f, ParchmentAlpha);
                GUI.Label(textRect, shown, _style);

                if (WaitingForKey)
                {
                    // En el pie del propio pergamino, con tinta, para que se lea sobre el papel.
                    float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 3f);
                    GUI.color = new Color(1f, 1f, 1f, pulse);
                    GUI.Label(new Rect(rect.x + width * 0.1f, rect.y + height * 0.82f, width * 0.8f, height * 0.09f), Hint, _hintStyle);
                }
            }
            GUI.color = saved;
        }

        /// <summary>Tamaño de letra que se está usando y si el texto entero cabe en su recuadro (para pruebas).</summary>
        public int FontSizeUsed { get; private set; }
        public bool TextFits { get; private set; }
        public float TextHeightUsed { get; private set; }

        private Vector2 _fittedFor;
        private int _fittedSize;

        private void EnsureStyles(float parchmentHeight, Rect textRect)
        {
            if (_font == null) _font = Resources.Load<Font>(FontResource);
            if (_style == null)
                _style = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = false, alignment = TextAnchor.UpperLeft, font = _font };
            _style.normal.textColor = new Color(0.17f, 0.10f, 0.05f); // tinta parda

            // Se busca el tamaño mayor con el que el texto ENTERO cabe en el recuadro (se mide con todo el texto, no con lo ya escrito,
            // para que el tamaño no cambie mientras se escribe). Se recalcula solo si cambia el tamaño de la ventana.
            var key = new Vector2(textRect.width, textRect.height);
            if (_fittedFor != key)
            {
                _fittedFor = key;
                var content = new GUIContent(EndingText.Message);
                int size = Mathf.Max(14, Mathf.RoundToInt(parchmentHeight * 0.075f));
                for (; size > 12; size--)
                {
                    _style.fontSize = size;
                    if (_style.CalcHeight(content, textRect.width) <= textRect.height) break;
                }
                _fittedSize = size;
            }
            _style.fontSize = _fittedSize;
            _style.richText = true; // la parte sin escribir va con una etiqueta de color transparente
            TextHeightUsed = _style.CalcHeight(new GUIContent(EndingText.Message), textRect.width);
            FontSizeUsed = _fittedSize;
            TextFits = TextHeightUsed <= textRect.height + 0.5f;

            if (_hintStyle == null)
                _hintStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, font = _font, wordWrap = false };
            _hintStyle.fontSize = Mathf.Max(13, _fittedSize * 2 / 3);
            _hintStyle.normal.textColor = new Color(0.30f, 0.18f, 0.08f);
        }

        /// <summary>Pergamino generado: papel manchado, bordes rotos y quemados, más oscuro hacia las esquinas. Sin imágenes externas.</summary>
        public static Texture2D MakeParchment(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "Pergamino", wrapMode = TextureWrapMode.Clamp };
            var paper = new Color(0.90f, 0.79f, 0.56f);
            var stain = new Color(0.66f, 0.50f, 0.28f);
            var burnt = new Color(0.32f, 0.20f, 0.09f);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)(width - 1), v = y / (float)(height - 1);
                    float mottle = Mathf.PerlinNoise(u * 6f, v * 6f) * 0.6f + Mathf.PerlinNoise(u * 22f + 7f, v * 22f + 3f) * 0.4f;
                    // Distancia al borde (0 = en el borde, 1 = en el centro), con el borde roto por el ruido.
                    float edge = Mathf.Min(Mathf.Min(u, 1f - u) * 1.35f, Mathf.Min(v, 1f - v)) * 2f;
                    float ragged = (Mathf.PerlinNoise(u * 38f, v * 38f) - 0.5f) * 0.10f;
                    float d = edge + ragged;
                    float alpha = Mathf.Clamp01((d - 0.015f) / 0.02f);
                    Color c = Color.Lerp(paper, stain, Mathf.Clamp01((mottle - 0.35f) * 1.4f));
                    c = Color.Lerp(burnt, c, Mathf.Clamp01(d * 9f));               // quemado en el borde
                    c *= Mathf.Lerp(0.82f, 1f, Mathf.Clamp01(d * 2.2f));           // más oscuro hacia las esquinas
                    c.a = alpha;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
