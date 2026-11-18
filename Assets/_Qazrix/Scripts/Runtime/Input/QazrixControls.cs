using UnityEngine;
using UnityEngine.InputSystem;

namespace Qazrix.Input
{
    /// <summary>
    /// Definición de fábrica de las acciones de entrada (spec §6.7, sustituye a la lectura directa D13).
    /// El archivo <c>Settings/Input/QazrixControls.inputactions</c> se genera a partir de aquí con la
    /// herramienta de Editor; a partir de entonces el archivo es la fuente que usa el juego.
    /// </summary>
    public static class QazrixControls
    {
        public const string AssetName = "QazrixControls";
        public const string GameplayMap = "Gameplay";

        public const string KeyboardMouse = "KeyboardMouse";
        public const string Gamepad = "Gamepad";

        public const string Move = "Move";
        public const string Tank = "Tank";
        public const string LookPointer = "LookPointer";
        public const string LookStick = "LookStick";
        public const string Zoom = "Zoom";
        public const string Run = "Run";
        public const string Crouch = "Crouch";
        public const string Jump = "Jump";
        public const string Interact = "Interact";
        public const string ToggleHud = "ToggleHud";
        public const string ToggleControls = "ToggleControls";
        public const string ToggleInventory = "ToggleInventory";
        public const string ToggleMap = "ToggleMap";
        public const string SwitchScene = "SwitchScene";
        public const string CaptureCursor = "CaptureCursor";
        public const string ReleaseCursor = "ReleaseCursor";

        public static readonly string[] AllActions =
        {
            Move, Tank, LookPointer, LookStick, Zoom, Run, Crouch, Jump, Interact,
            ToggleHud, ToggleControls, ToggleInventory, ToggleMap, SwitchScene, CaptureCursor, ReleaseCursor
        };

        /// <summary>Acciones que el jugador puede reasignar desde el panel de controles.</summary>
        public static readonly string[] RebindableActions = { Move, Run, Crouch, Jump, Interact };

        /// <summary>Crea las acciones con las asignaciones por defecto. Quien la llama debe destruir el objeto.</summary>
        public static InputActionAsset CreateDefault()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = AssetName;

            asset.AddControlScheme(KeyboardMouse).WithRequiredDevice("<Keyboard>").WithOptionalDevice("<Mouse>");
            asset.AddControlScheme(Gamepad).WithRequiredDevice("<Gamepad>");

            InputActionMap map = asset.AddActionMap(GameplayMap);

            InputAction move = map.AddAction(Move, InputActionType.Value, expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w", KeyboardMouse)
                .With("Down", "<Keyboard>/s", KeyboardMouse)
                .With("Left", "<Keyboard>/a", KeyboardMouse)
                .With("Right", "<Keyboard>/d", KeyboardMouse);
            move.AddBinding("<Gamepad>/leftStick", processors: "stickDeadzone(min=0.2)", groups: Gamepad);

            // Flechas: control «de tanque». Arriba y abajo avanzan y retroceden en la dirección en que mira
            // el personaje; izquierda y derecha lo giran sobre sí mismo (y = avance, x = giro).
            InputAction tank = map.AddAction(Tank, InputActionType.Value, expectedControlLayout: "Vector2");
            tank.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow", KeyboardMouse)
                .With("Down", "<Keyboard>/downArrow", KeyboardMouse)
                .With("Left", "<Keyboard>/leftArrow", KeyboardMouse)
                .With("Right", "<Keyboard>/rightArrow", KeyboardMouse);

            // El ratón entrega píxeles por fotograma y el stick un valor -1..1: son acciones distintas
            // porque la cámara los escala de forma diferente.
            map.AddAction(LookPointer, InputActionType.PassThrough, expectedControlLayout: "Vector2")
                .AddBinding("<Mouse>/delta", groups: KeyboardMouse);
            map.AddAction(LookStick, InputActionType.Value, expectedControlLayout: "Vector2")
                .AddBinding("<Gamepad>/rightStick", processors: "stickDeadzone(min=0.2)", groups: Gamepad);

            InputAction zoom = map.AddAction(Zoom, InputActionType.PassThrough, expectedControlLayout: "Axis");
            zoom.AddBinding("<Mouse>/scroll/y", groups: KeyboardMouse);
            zoom.AddCompositeBinding("1DAxis")
                .With("Positive", "<Gamepad>/dpad/up", Gamepad)
                .With("Negative", "<Gamepad>/dpad/down", Gamepad);

            Button(map, Run, new[] { "<Keyboard>/leftShift" }, new[] { "<Gamepad>/leftStickPress", "<Gamepad>/rightTrigger" });
            Button(map, Crouch, new[] { "<Keyboard>/c", "<Keyboard>/leftCtrl" }, new[] { "<Gamepad>/buttonEast" });
            Button(map, Jump, new[] { "<Keyboard>/space" }, new[] { "<Gamepad>/buttonSouth" });
            Button(map, Interact, new[] { "<Keyboard>/e" }, new[] { "<Gamepad>/buttonWest" });
            Button(map, ToggleHud, new[] { "<Keyboard>/f1" }, new[] { "<Gamepad>/select" });
            Button(map, ToggleControls, new[] { "<Keyboard>/f2" }, new[] { "<Gamepad>/start" });
            Button(map, ToggleInventory, new[] { "<Keyboard>/i" }, new[] { "<Gamepad>/buttonNorth" });
            Button(map, ToggleMap, new[] { "<Keyboard>/m" }, new[] { "<Gamepad>/dpad/left" });
            Button(map, SwitchScene, new[] { "<Keyboard>/f3" }, new string[0]);
            Button(map, CaptureCursor, new[] { "<Mouse>/leftButton" }, new string[0]);
            Button(map, ReleaseCursor, new[] { "<Keyboard>/escape" }, new string[0]);

            return asset;
        }

        private static void Button(InputActionMap map, string name, string[] keyboardMouse, string[] gamepad)
        {
            InputAction action = map.AddAction(name, InputActionType.Button, expectedControlLayout: "Button");
            foreach (string path in keyboardMouse) action.AddBinding(path, groups: KeyboardMouse);
            foreach (string path in gamepad) action.AddBinding(path, groups: Gamepad);
        }
    }
}
