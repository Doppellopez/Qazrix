using UnityEngine;
using UnityEngine.InputSystem;

namespace Qazrix.Input
{
    /// <summary>Entrada de un fotograma, ya combinada entre teclado/ratón y mando.</summary>
    public struct InputFrame
    {
        public Vector2 Move;        // x = derecha, y = adelante; magnitud máxima 1
        public Vector2 Tank;        // flechas: x = girar (derecha +), y = avanzar (+) o retroceder (-)
        public Vector2 LookPointer; // píxeles de ratón de este fotograma
        public Vector2 LookStick;   // stick derecho, -1..1
        public Vector2 LookTouch;   // píxeles arrastrados con el dedo en este fotograma (solo Android e iOS)
        public float Zoom;          // >0 acerca, <0 aleja
        public bool Run;
        public bool Crouch;
        public bool JumpPressed;
        public bool InteractPressed;
        public bool ToggleHudPressed;
        public bool ToggleControlsPressed;
        public bool ToggleInventoryPressed;
        public bool ToggleMapPressed;
        public bool SwitchScenePressed;
        public bool CaptureCursorPressed;
        public bool ReleaseCursorPressed;
    }

    /// <summary>
    /// Único punto de lectura de la entrada del juego. Envuelve las Input Actions, aplica las
    /// reasignaciones guardadas por el jugador y las persiste. Los demás componentes reciben
    /// una referencia a este (la asignan las herramientas de Editor); nadie lo busca en la escena.
    /// </summary>
    public sealed class QazrixInput : MonoBehaviour
    {
        /// <summary>Clave de PlayerPrefs con las reasignaciones. Lleva versión: si cambian las acciones, se sube.</summary>
        public const string OverridesKey = "qazrix.input.overrides.v1";

        [SerializeField] private InputActionAsset actions;

        private InputActionMap _map;
        private InputAction _move, _tank, _lookPointer, _lookStick, _zoom, _run, _crouch, _jump, _interact;
        private InputAction _toggleHud, _toggleControls, _toggleInventory, _toggleMap, _switchScene, _captureCursor, _releaseCursor;
        private bool _warned;
        private TouchControls _touch;

        /// <summary>Con un panel abierto el juego no recibe movimiento ni cámara; solo las teclas de panel.</summary>
        public bool GameplayBlocked { get; set; }

        public InputActionAsset Actions => actions;

        public void Configure(InputActionAsset asset)
        {
            actions = asset;
        }

        private void OnEnable()
        {
            if (actions == null) return;

            _map = actions.FindActionMap(QazrixControls.GameplayMap, throwIfNotFound: true);
            _move = _map.FindAction(QazrixControls.Move, true);
            _tank = _map.FindAction(QazrixControls.Tank, true);
            _lookPointer = _map.FindAction(QazrixControls.LookPointer, true);
            _lookStick = _map.FindAction(QazrixControls.LookStick, true);
            _zoom = _map.FindAction(QazrixControls.Zoom, true);
            _run = _map.FindAction(QazrixControls.Run, true);
            _crouch = _map.FindAction(QazrixControls.Crouch, true);
            _jump = _map.FindAction(QazrixControls.Jump, true);
            _interact = _map.FindAction(QazrixControls.Interact, true);
            _toggleHud = _map.FindAction(QazrixControls.ToggleHud, true);
            _toggleControls = _map.FindAction(QazrixControls.ToggleControls, true);
            _toggleInventory = _map.FindAction(QazrixControls.ToggleInventory, true);
            _toggleMap = _map.FindAction(QazrixControls.ToggleMap, true);
            _switchScene = _map.FindAction(QazrixControls.SwitchScene, true);
            _captureCursor = _map.FindAction(QazrixControls.CaptureCursor, true);
            _releaseCursor = _map.FindAction(QazrixControls.ReleaseCursor, true);

            // Controles táctiles: solo en Android e iOS; en Windows y Linux el componente existe pero no hace nada.
            _touch = GetComponent<TouchControls>();
            if (_touch == null) _touch = gameObject.AddComponent<TouchControls>();

            LoadOverrides();
            _map.Enable();
        }

        private void OnDisable()
        {
            _map?.Disable();
        }

        public InputFrame Read()
        {
            var frame = new InputFrame();
            if (_map == null)
            {
                if (!_warned)
                {
                    Debug.LogError("[Qazrix] QazrixInput no tiene asignadas las Input Actions. Regenera la escena desde el menú Qazrix.", this);
                    _warned = true;
                }
                return frame;
            }

            frame.ToggleHudPressed = _toggleHud.WasPressedThisFrame();
            frame.ToggleControlsPressed = _toggleControls.WasPressedThisFrame();
            frame.ReleaseCursorPressed = _releaseCursor.WasPressedThisFrame();
            frame.ToggleInventoryPressed = _toggleInventory.WasPressedThisFrame();
            frame.ToggleMapPressed = _toggleMap.WasPressedThisFrame();
            bool touch = _touch != null && _touch.Active;
            if (touch)
            {
                frame.ToggleInventoryPressed |= _touch.InventoryPressed;
                frame.ToggleMapPressed |= _touch.MapPressed;
            }
            if (GameplayBlocked) return frame;

            frame.Move = Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f);
            frame.Tank = Vector2.ClampMagnitude(_tank.ReadValue<Vector2>(), 1f);
            frame.LookPointer = _lookPointer.ReadValue<Vector2>();
            frame.LookStick = _lookStick.ReadValue<Vector2>();
            frame.Zoom = Mathf.Clamp(_zoom.ReadValue<float>(), -1f, 1f);
            frame.Run = _run.IsPressed();
            frame.Crouch = _crouch.IsPressed();
            frame.JumpPressed = _jump.WasPressedThisFrame();
            frame.InteractPressed = _interact.WasPressedThisFrame();
            frame.CaptureCursorPressed = _captureCursor.WasPressedThisFrame();
            frame.SwitchScenePressed = _switchScene.WasPressedThisFrame();

            if (touch)
            {
                frame.Move = Vector2.ClampMagnitude(frame.Move + _touch.Move, 1f);
                frame.Run |= _touch.Run;
                frame.Crouch |= _touch.Crouch;
                frame.JumpPressed |= _touch.JumpPressed;
                frame.InteractPressed |= _touch.InteractPressed;
                frame.LookTouch = _touch.LookDelta;
                frame.Zoom = Mathf.Clamp(frame.Zoom + _touch.Zoom, -1f, 1f);
            }
            return frame;
        }

        public void SaveOverrides()
        {
            if (actions == null) return;
            PlayerPrefs.SetString(OverridesKey, actions.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        public void LoadOverrides()
        {
            if (actions == null) return;
            string json = PlayerPrefs.GetString(OverridesKey, string.Empty);
            if (!string.IsNullOrEmpty(json)) actions.LoadBindingOverridesFromJson(json);
        }

        public void ResetOverrides()
        {
            if (actions == null) return;
            actions.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(OverridesKey);
            PlayerPrefs.Save();
        }
    }
}
