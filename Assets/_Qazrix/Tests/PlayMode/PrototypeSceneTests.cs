using System.Collections;
using System.IO;
using NUnit.Framework;
using Qazrix.Input;
using Qazrix.Prototype;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Qazrix.Tests.PlayMode
{
    /// <summary>
    /// Pruebas en Play de la escena Prototype con teclado simulado (spec §13.1). Comprueban que la
    /// entrada llega de verdad al jugador y a los paneles, y dejan capturas en Reports/QA.
    /// </summary>
    public sealed class PrototypeSceneTests : InputTestFixture
    {
        private const string ScenePath = "Assets/_Qazrix/Scenes/Prototype/Prototype.unity";

        private Keyboard _keyboard;
        private PrototypePlayerController _player;
        private PrototypeHud _hud;
        private ControlsPanel _panel;
        private QazrixInput _input;

        private IEnumerator LoadScene()
        {
            _keyboard = InputSystem.AddDevice<Keyboard>();
            SceneManager.LoadScene(ScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            _player = Object.FindFirstObjectByType<PrototypePlayerController>();
            _hud = Object.FindFirstObjectByType<PrototypeHud>();
            _panel = Object.FindFirstObjectByType<ControlsPanel>();
            _input = Object.FindFirstObjectByType<QazrixInput>();
            Assert.IsNotNull(_player, "la escena no tiene jugador");
            Assert.IsNotNull(_hud, "la escena no tiene HUD");
            Assert.IsNotNull(_panel, "la escena no tiene panel de controles");
            Assert.IsNotNull(_input, "la escena no tiene lector de entrada");

            // Deja que el jugador se asiente en el suelo.
            for (int i = 0; i < 30; i++) yield return null;
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        private IEnumerator Tap(UnityEngine.InputSystem.Controls.KeyControl key)
        {
            Press(key);
            yield return null;
            yield return null;
            Release(key);
            yield return null;
        }

        private static IEnumerator Capture(string fileName)
        {
            yield return new WaitForEndOfFrame();
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            Directory.CreateDirectory("Reports/QA");
            File.WriteAllBytes("Reports/QA/" + fileName, shot.EncodeToPNG());
            Object.Destroy(shot);
        }

        [UnityTest]
        public IEnumerator W_MovesThePlayerForward()
        {
            yield return LoadScene();
            Vector3 start = _player.transform.position;

            Press(_keyboard.wKey);
            yield return Frames(60);
            Release(_keyboard.wKey);
            yield return null;

            Vector3 moved = _player.transform.position - start;
            Assert.Greater(moved.z, 0.5f, "con W debería avanzar hacia +Z (la cámara mira hacia +Z al empezar)");
            Assert.Less(Mathf.Abs(moved.x), 0.3f, "no debería desviarse de lado");
        }

        [UnityTest]
        public IEnumerator Space_MakesThePlayerJump()
        {
            yield return LoadScene();
            Assert.IsTrue(_player.IsGrounded, "debería empezar en el suelo");
            float startY = _player.transform.position.y;

            Press(_keyboard.spaceKey);
            yield return null;
            Release(_keyboard.spaceKey);

            float maxY = startY;
            for (int i = 0; i < 40; i++)
            {
                yield return null;
                maxY = Mathf.Max(maxY, _player.transform.position.y);
            }
            Assert.Greater(maxY - startY, 0.5f, "con Espacio debería elevarse");
        }

        [UnityTest]
        public IEnumerator F2_OpensAndClosesTheControlsPanel_AndBlocksMovement()
        {
            yield return LoadScene();
            Assert.IsFalse(_panel.IsOpen);
            yield return Capture("play_prototype_hud.png");

            yield return Tap(_keyboard.f2Key);
            Assert.IsTrue(_panel.IsOpen, "F2 debería abrir el panel de controles");
            Assert.IsTrue(_input.GameplayBlocked, "con el panel abierto el juego no recibe movimiento");
            yield return Capture("play_prototype_controles.png");

            Vector3 before = _player.transform.position;
            Press(_keyboard.wKey);
            yield return Frames(30);
            Release(_keyboard.wKey);
            Assert.Less(Vector3.Distance(before, _player.transform.position), 0.05f, "con el panel abierto no debe moverse");

            yield return Tap(_keyboard.f2Key);
            Assert.IsFalse(_panel.IsOpen, "F2 debería cerrar el panel");
            Assert.IsFalse(_input.GameplayBlocked);
        }

        [UnityTest]
        public IEnumerator F1_TogglesTheHud()
        {
            yield return LoadScene();
            Assert.IsTrue(_hud.IsVisible);

            yield return Tap(_keyboard.f1Key);
            Assert.IsFalse(_hud.IsVisible, "F1 debería ocultar el panel de depuración");

            yield return Tap(_keyboard.f1Key);
            Assert.IsTrue(_hud.IsVisible, "F1 debería volver a mostrarlo");
        }
    }
}
