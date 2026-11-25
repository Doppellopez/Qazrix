using System.Collections.Generic;
using Qazrix.Simulation.Input;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Qazrix.Input
{
    /// <summary>
    /// Controles táctiles, SOLO para las versiones de Android e iOS (en Windows y Linux no se activan nunca).
    /// A la izquierda, una palanca flotante que aparece donde se posa el dedo: mueve al personaje. A la derecha, arrastrar
    /// con un dedo gira la cámara y pellizcar con dos hace zoom. Botones en pantalla: Saltar, Interactuar, Agacharse
    /// (mientras se toca), Correr (se activa y desactiva) e Inventario y Mapa arriba.
    /// No lee el teclado: <see cref="QazrixInput"/> lo suma a lo demás en cada fotograma.
    /// </summary>
    [DefaultExecutionOrder(-100)] // procesa los toques antes que quien lee la entrada en ese fotograma
    public sealed class TouchControls : MonoBehaviour
    {
#if UNITY_EDITOR
        /// <summary>Solo para pruebas dentro del editor: enseña y usa los controles táctiles aunque no sea un móvil.</summary>
        public static bool ForceEnabled;
#endif

        public enum Kind { Jump, Interact, Crouch, Run, Inventory, Map }

        private enum Role { Stick, Look, Button }

        private struct Button
        {
            public Kind Kind;
            public Vector2 Center; // píxeles, Y hacia abajo
            public float Radius;
            public string Label;
        }

        private sealed class Owner
        {
            public Role Role;
            public Kind Kind;
            public Vector2 Origin;   // donde se posó el dedo (palanca)
            public Vector2 Current;
        }

        private const float StickRadiusUnits = 1.1f;
        private const float PinchPixelsPerUnit = 140f;

        private readonly Dictionary<int, Owner> _owners = new Dictionary<int, Owner>();
        private readonly List<Button> _buttons = new List<Button>();
        private Texture2D _circle;
        private GUIStyle _label;
        private float _unit;
        private int _layoutWidth, _layoutHeight;
        private float _previousPinch = -1f;
        private bool _runOn;

        /// <summary>¿Se están usando los controles táctiles? Solo en móviles (o forzado en el editor, para pruebas).</summary>
        public bool Active
        {
            get
            {
#if UNITY_EDITOR
                if (ForceEnabled) return true;
#endif
                return Application.isMobilePlatform;
            }
        }

        public Vector2 Move { get; private set; }
        public Vector2 LookDelta { get; private set; }
        public float Zoom { get; private set; }
        public bool Run => _runOn;
        public bool Crouch { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool InteractPressed { get; private set; }
        public bool InventoryPressed { get; private set; }
        public bool MapPressed { get; private set; }

        private void OnEnable()
        {
            if (!Active) return;
            EnhancedTouchSupport.Enable();
        }

        private void OnDisable()
        {
            if (EnhancedTouchSupport.enabled) EnhancedTouchSupport.Disable();
            _owners.Clear();
        }

        private void OnDestroy()
        {
            if (_circle != null) Destroy(_circle);
        }

        private void Update()
        {
            // Los «pulsado este fotograma» duran solo uno.
            JumpPressed = InteractPressed = InventoryPressed = MapPressed = false;
            LookDelta = Vector2.zero;
            Zoom = 0f;
            if (!Active) return;
            if (!EnhancedTouchSupport.enabled) EnhancedTouchSupport.Enable();

            EnsureLayout();
            bool crouchHeld = false;
            var lookTouches = new List<Touch>(2);

            foreach (Touch touch in Touch.activeTouches)
            {
                Vector2 position = new Vector2(touch.screenPosition.x, Screen.height - touch.screenPosition.y); // Y hacia abajo
                bool began = touch.phase == UnityEngine.InputSystem.TouchPhase.Began;
                bool ended = touch.phase == UnityEngine.InputSystem.TouchPhase.Ended || touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled;

                if (began && !_owners.ContainsKey(touch.touchId)) _owners[touch.touchId] = Claim(position);
                if (!_owners.TryGetValue(touch.touchId, out Owner owner)) continue;
                owner.Current = position;

                switch (owner.Role)
                {
                    case Role.Stick:
                        if (ended) Move = Vector2.zero;
                        else
                        {
                            TouchMath.Stick(owner.Origin.x, owner.Origin.y, position.x, position.y, _unit * StickRadiusUnits, out float x, out float y);
                            Move = new Vector2(x, y);
                        }
                        break;
                    case Role.Look:
                        if (!ended)
                        {
                            lookTouches.Add(touch);
                            LookDelta += touch.delta;
                        }
                        break;
                    case Role.Button:
                        if (began) Press(owner.Kind);
                        if (owner.Kind == Kind.Crouch && !ended) crouchHeld = true;
                        break;
                }

                if (ended) _owners.Remove(touch.touchId);
            }

            Crouch = crouchHeld;

            // Dos dedos en la zona de cámara: pellizco (zoom), y no se gira.
            if (lookTouches.Count == 2)
            {
                float distance = Vector2.Distance(lookTouches[0].screenPosition, lookTouches[1].screenPosition);
                if (_previousPinch > 0f) Zoom = TouchMath.PinchZoom(_previousPinch, distance, PinchPixelsPerUnit);
                _previousPinch = distance;
                LookDelta = Vector2.zero;
            }
            else _previousPinch = -1f;

            if (Touch.activeTouches.Count == 0) Move = Vector2.zero;
        }

        private Owner Claim(Vector2 position)
        {
            foreach (Button b in _buttons)
                if (TouchMath.InCircle(position.x, position.y, b.Center.x, b.Center.y, b.Radius))
                    return new Owner { Role = Role.Button, Kind = b.Kind, Origin = position, Current = position };

            bool leftSide = position.x < Screen.width * 0.45f && position.y > Screen.height * 0.2f;
            if (leftSide && !HasStick()) return new Owner { Role = Role.Stick, Origin = position, Current = position };
            return new Owner { Role = Role.Look, Origin = position, Current = position };
        }

        private bool HasStick()
        {
            foreach (Owner o in _owners.Values) if (o.Role == Role.Stick) return true;
            return false;
        }

        private void Press(Kind kind)
        {
            switch (kind)
            {
                case Kind.Jump: JumpPressed = true; break;
                case Kind.Interact: InteractPressed = true; break;
                case Kind.Inventory: InventoryPressed = true; break;
                case Kind.Map: MapPressed = true; break;
                case Kind.Run: _runOn = !_runOn; break;
            }
        }

        /// <summary>Coloca los botones según el tamaño de pantalla: una «unidad» es algo más de un dedo.</summary>
        private void EnsureLayout()
        {
            if (_layoutWidth == Screen.width && _layoutHeight == Screen.height && _buttons.Count > 0) return;
            _layoutWidth = Screen.width;
            _layoutHeight = Screen.height;
            float w = Screen.width, h = Screen.height;
            _unit = Mathf.Max(h * 0.10f, 60f);
            float u = _unit;

            _buttons.Clear();
            _buttons.Add(new Button { Kind = Kind.Jump, Center = new Vector2(w - 1.7f * u, h - 1.5f * u), Radius = 0.9f * u, Label = "SALTAR" });
            _buttons.Add(new Button { Kind = Kind.Interact, Center = new Vector2(w - 3.8f * u, h - 1.1f * u), Radius = 0.7f * u, Label = "E" });
            _buttons.Add(new Button { Kind = Kind.Crouch, Center = new Vector2(w - 1.0f * u, h - 3.5f * u), Radius = 0.7f * u, Label = "AGACHAR" });
            _buttons.Add(new Button { Kind = Kind.Run, Center = new Vector2(1.0f * u, h - 4.2f * u), Radius = 0.7f * u, Label = "CORRER" });
            _buttons.Add(new Button { Kind = Kind.Inventory, Center = new Vector2(w - 1.0f * u, 0.9f * u), Radius = 0.55f * u, Label = "BOLSA" });
            _buttons.Add(new Button { Kind = Kind.Map, Center = new Vector2(w - 2.4f * u, 0.9f * u), Radius = 0.55f * u, Label = "MAPA" });
        }

        private static Texture2D MakeCircle()
        {
            const int size = 96;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - half) * (x + 0.5f - half) + (y + 0.5f - half) * (y + 0.5f - half)) / half;
                    float ring = Mathf.Clamp01((d - 0.88f) / 0.06f) * Mathf.Clamp01((1f - d) / 0.04f); // borde
                    float fill = Mathf.Clamp01((1f - d) / 0.04f);
                    byte a = (byte)(255f * Mathf.Max(fill * 0.35f, ring));
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private void OnGUI()
        {
            if (!Active || Event.current.type != EventType.Repaint) return;
            EnsureLayout();
            if (_circle == null) _circle = MakeCircle();
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, richText = false };
                _label.normal.textColor = Color.white;
            }
            _label.fontSize = Mathf.RoundToInt(_unit * 0.24f);
            GUI.depth = -1000;
            Color saved = GUI.color;

            foreach (Button b in _buttons)
            {
                bool lit = (b.Kind == Kind.Run && _runOn) || IsHeld(b.Kind);
                GUI.color = lit ? new Color(1f, 0.85f, 0.2f, 0.9f) : new Color(1f, 1f, 1f, 0.55f);
                GUI.DrawTexture(new Rect(b.Center.x - b.Radius, b.Center.y - b.Radius, b.Radius * 2f, b.Radius * 2f), _circle);
                GUI.color = Color.white;
                GUI.Label(new Rect(b.Center.x - b.Radius, b.Center.y - b.Radius, b.Radius * 2f, b.Radius * 2f), b.Label, _label);
            }

            foreach (Owner o in _owners.Values)
            {
                if (o.Role != Role.Stick) continue;
                float baseRadius = _unit * StickRadiusUnits;
                GUI.color = new Color(1f, 1f, 1f, 0.45f);
                GUI.DrawTexture(new Rect(o.Origin.x - baseRadius, o.Origin.y - baseRadius, baseRadius * 2f, baseRadius * 2f), _circle);
                Vector2 knob = o.Origin + Vector2.ClampMagnitude(o.Current - o.Origin, baseRadius);
                float knobRadius = _unit * 0.5f;
                GUI.color = new Color(1f, 1f, 1f, 0.85f);
                GUI.DrawTexture(new Rect(knob.x - knobRadius, knob.y - knobRadius, knobRadius * 2f, knobRadius * 2f), _circle);
            }
            GUI.color = saved;
        }

        private bool IsHeld(Kind kind)
        {
            foreach (Owner o in _owners.Values) if (o.Role == Role.Button && o.Kind == kind) return true;
            return false;
        }
    }
}
