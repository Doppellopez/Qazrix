using System.Linq;
using NUnit.Framework;
using Qazrix.Input;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Qazrix.Tests
{
    /// <summary>Acciones de entrada y reasignación de teclas (spec §6.7). No necesitan dispositivos conectados.</summary>
    public sealed class QazrixControlsTests
    {
        private const string AssetPath = "Assets/_Qazrix/Settings/Input/QazrixControls.inputactions";

        private InputActionAsset _controls;
        private InputActionAsset _other;

        [SetUp]
        public void SetUp()
        {
            _controls = QazrixControls.CreateDefault();
        }

        [TearDown]
        public void TearDown()
        {
            if (_controls != null) Object.DestroyImmediate(_controls);
            if (_other != null) Object.DestroyImmediate(_other);
        }

        private static InputAction Find(InputActionAsset asset, string action)
        {
            return asset.FindActionMap(QazrixControls.GameplayMap, true).FindAction(action, true);
        }

        private static int KeyboardBindingIndex(InputAction action)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding b = action.bindings[i];
                if (!b.isComposite && b.groups != null && b.groups.Contains(QazrixControls.KeyboardMouse)) return i;
            }
            return -1;
        }

        [Test]
        public void Defaults_ContainEveryAction()
        {
            InputActionMap map = _controls.FindActionMap(QazrixControls.GameplayMap, true);
            foreach (string name in QazrixControls.AllActions)
                Assert.IsNotNull(map.FindAction(name), $"falta la acción {name}");
            Assert.AreEqual(QazrixControls.AllActions.Length, map.actions.Count, "hay acciones sin declarar en AllActions");
        }

        [Test]
        public void GameplayButtons_WorkOnKeyboardAndGamepad()
        {
            foreach (string name in new[] { QazrixControls.Move, QazrixControls.Run, QazrixControls.Crouch, QazrixControls.Jump,
                         QazrixControls.Zoom, QazrixControls.ToggleHud, QazrixControls.ToggleControls })
            {
                InputAction action = Find(_controls, name);
                Assert.IsTrue(action.bindings.Any(b => b.groups != null && b.groups.Contains(QazrixControls.KeyboardMouse)), $"{name} sin teclado/ratón");
                Assert.IsTrue(action.bindings.Any(b => b.groups != null && b.groups.Contains(QazrixControls.Gamepad)), $"{name} sin mando");
            }
        }

        [Test]
        public void ArrowKeys_DriveTheTankAction_NotTheCameraRelativeMove()
        {
            string[] arrows = { "<Keyboard>/upArrow", "<Keyboard>/downArrow", "<Keyboard>/leftArrow", "<Keyboard>/rightArrow" };
            string[] tank = Find(_controls, QazrixControls.Tank).bindings.Select(b => b.path).ToArray();
            string[] move = Find(_controls, QazrixControls.Move).bindings.Select(b => b.path).ToArray();
            foreach (string arrow in arrows)
            {
                CollectionAssert.Contains(tank, arrow, "las flechas deben mover al personaje de tanque");
                CollectionAssert.DoesNotContain(move, arrow, "y no la acción de movimiento relativa a la cámara");
            }
        }

        [Test]
        public void EveryBinding_BelongsToAControlScheme()
        {
            foreach (InputBinding b in _controls.FindActionMap(QazrixControls.GameplayMap, true).bindings)
            {
                if (b.isComposite) continue;
                Assert.IsFalse(string.IsNullOrEmpty(b.groups), $"asignación sin esquema: {b.path}");
            }
            CollectionAssert.AreEquivalent(new[] { QazrixControls.KeyboardMouse, QazrixControls.Gamepad },
                _controls.controlSchemes.Select(s => s.name));
        }

        [Test]
        public void RebindableActions_HaveAKeyboardBinding()
        {
            foreach (string name in QazrixControls.RebindableActions)
                Assert.GreaterOrEqual(KeyboardBindingIndex(Find(_controls, name)), 0, $"{name} no se puede reasignar en teclado");
        }

        [Test]
        public void Override_SurvivesSaveAndLoad()
        {
            InputAction jump = Find(_controls, QazrixControls.Jump);
            int index = KeyboardBindingIndex(jump);
            Assert.AreEqual("<Keyboard>/space", jump.bindings[index].effectivePath);

            // Las reasignaciones se guardan por identificador de asignación, y ese identificador vive
            // en el archivo .inputactions. «Otra sesión» es, por tanto, volver a leer el mismo archivo.
            string file = _controls.ToJson();

            jump.ApplyBindingOverride(index, "<Keyboard>/j");
            string saved = _controls.SaveBindingOverridesAsJson();

            _other = InputActionAsset.FromJson(file);
            _other.LoadBindingOverridesFromJson(saved);
            InputAction loaded = Find(_other, QazrixControls.Jump);

            Assert.AreEqual("<Keyboard>/j", loaded.bindings[index].effectivePath);
            Assert.AreEqual("<Keyboard>/space", loaded.bindings[index].path, "la asignación de fábrica no se pierde");
        }

        [Test]
        public void ResetOverrides_RestoresDefaults()
        {
            InputAction run = Find(_controls, QazrixControls.Run);
            int index = KeyboardBindingIndex(run);
            run.ApplyBindingOverride(index, "<Keyboard>/r");

            _controls.RemoveAllBindingOverrides();

            Assert.AreEqual("<Keyboard>/leftShift", run.bindings[index].effectivePath);
        }

        [Test]
        public void Json_RoundTrip_KeepsActionsAndBindings()
        {
            _other = InputActionAsset.FromJson(_controls.ToJson());
            InputActionMap a = _controls.FindActionMap(QazrixControls.GameplayMap, true);
            InputActionMap b = _other.FindActionMap(QazrixControls.GameplayMap, true);

            CollectionAssert.AreEqual(a.actions.Select(x => x.name), b.actions.Select(x => x.name));
            CollectionAssert.AreEqual(a.bindings.Select(x => x.path), b.bindings.Select(x => x.path));
        }

        [Test]
        public void ProjectAsset_HasEveryActionTheCodeReads()
        {
            // El juego usa el archivo, no la definición de fábrica: si alguien lo edita y borra o
            // renombra una acción, QazrixInput fallaría al arrancar. Este test lo detecta antes.
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            Assert.IsNotNull(asset, $"falta {AssetPath}; se genera al crear la escena Prototype");

            InputActionMap map = asset.FindActionMap(QazrixControls.GameplayMap);
            Assert.IsNotNull(map, "falta el mapa Gameplay");
            foreach (string name in QazrixControls.AllActions)
                Assert.IsNotNull(map.FindAction(name), $"el archivo no tiene la acción {name}");
        }
    }
}
