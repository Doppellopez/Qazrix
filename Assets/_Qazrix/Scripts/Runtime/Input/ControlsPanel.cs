using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Qazrix.Input
{
    /// <summary>
    /// Panel de controles (spec §6.7): muestra las teclas actuales y permite reasignarlas.
    /// F2 o Start lo abren y cierran. Mientras está abierto, el juego no recibe movimiento.
    /// En este corte solo se reasigna el teclado; el mando queda para un corte posterior.
    /// </summary>
    public sealed class ControlsPanel : MonoBehaviour
    {
        [SerializeField] private QazrixInput input;

        private static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            { QazrixControls.Move, "Mover" },
            { QazrixControls.Run, "Correr" },
            { QazrixControls.Crouch, "Agacharse" },
            { QazrixControls.Jump, "Saltar" },
            { QazrixControls.Interact, "Interactuar" },
            { "up", "adelante" }, { "down", "atrás" }, { "left", "izquierda" }, { "right", "derecha" }
        };

        private struct Row
        {
            public InputAction Action;
            public int BindingIndex;
            public string Label;
        }

        private readonly List<Row> _rows = new List<Row>();
        private InputActionRebindingExtensions.RebindingOperation _operation;
        private int _waitingRow = -1;
        private bool _open;
        private CursorLockMode _previousLock;
        private Vector2 _scroll;
        private GUIStyle _label;

        public bool IsOpen => _open;

        public void Configure(QazrixInput reader)
        {
            input = reader;
        }

        private void Update()
        {
            if (input == null || _operation != null) return;
            if (input.Read().ToggleControlsPressed) SetOpen(!_open);
        }

        private void OnDisable()
        {
            CancelRebind();
            if (_open) SetOpen(false);
        }

        private void SetOpen(bool open)
        {
            _open = open;
            input.GameplayBlocked = open;
            if (open)
            {
                BuildRows();
                _previousLock = Cursor.lockState;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                CancelRebind();
                Cursor.lockState = _previousLock;
                Cursor.visible = _previousLock != CursorLockMode.Locked;
            }
        }

        private void BuildRows()
        {
            _rows.Clear();
            InputActionMap map = input.Actions.FindActionMap(QazrixControls.GameplayMap, true);
            foreach (string actionName in QazrixControls.RebindableActions)
            {
                InputAction action = map.FindAction(actionName, true);
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    InputBinding binding = action.bindings[i];
                    if (binding.isComposite) continue;
                    if (binding.groups == null || !binding.groups.Contains(QazrixControls.KeyboardMouse)) continue;

                    string label = Text(actionName);
                    if (binding.isPartOfComposite) label += " " + Text(binding.name.ToLowerInvariant());
                    _rows.Add(new Row { Action = action, BindingIndex = i, Label = label });
                }
            }
        }

        private static string Text(string key)
        {
            return Labels.TryGetValue(key, out string text) ? text : key;
        }

        private void StartRebind(int rowIndex)
        {
            CancelRebind();
            Row row = _rows[rowIndex];
            _waitingRow = rowIndex;

            row.Action.Disable(); // una acción activa no se puede reasignar
            _operation = row.Action.PerformInteractiveRebinding(row.BindingIndex)
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithControlsExcluding("<Keyboard>/anyKey")
                .WithControlsExcluding("<Keyboard>/escape")
                .WithControlsExcluding("<Keyboard>/f1")
                .WithControlsExcluding("<Keyboard>/f2")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(_ => FinishRebind(true))
                .OnCancel(_ => FinishRebind(false))
                .Start();
        }

        private void FinishRebind(bool completed)
        {
            InputAction action = _waitingRow >= 0 ? _rows[_waitingRow].Action : null;
            _operation?.Dispose();
            _operation = null;
            _waitingRow = -1;
            action?.Enable();
            if (completed) input.SaveOverrides();
        }

        private void CancelRebind()
        {
            if (_operation != null) _operation.Cancel(); // dispara OnCancel → FinishRebind(false)
        }

        private void OnGUI()
        {
            if (!_open) return;

            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { fontSize = 14 };
                _label.normal.textColor = Color.white;
            }

            float width = 430f, height = Mathf.Min(Screen.height - 40f, 80f + _rows.Count * 28f + 70f);
            var area = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.Box(area, "Controles (teclado)");

            GUILayout.BeginArea(new Rect(area.x + 14f, area.y + 30f, area.width - 28f, area.height - 44f));
            _scroll = GUILayout.BeginScrollView(_scroll);

            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                GUILayout.BeginHorizontal(GUILayout.Height(26f));
                GUILayout.Label(row.Label, _label, GUILayout.Width(190f));

                string current = i == _waitingRow
                    ? "pulsa una tecla…"
                    : row.Action.GetBindingDisplayString(row.BindingIndex);
                GUI.enabled = _operation == null;
                if (GUILayout.Button(current, GUILayout.Width(190f))) StartRebind(i);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUILayout.Space(6f);

            GUILayout.BeginHorizontal();
            GUI.enabled = _operation == null;
            if (GUILayout.Button("Restablecer", GUILayout.Width(130f)))
            {
                input.ResetOverrides();
                BuildRows();
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Cerrar (F2)", GUILayout.Width(130f))) SetOpen(false);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Label(_operation != null ? "Esc cancela la reasignación." : "Haz clic en una tecla para cambiarla.", _label);
            GUILayout.EndArea();
        }
    }
}
