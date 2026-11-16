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
        private const string Z01ScenePath = "Assets/_Qazrix/Scenes/Z01/Z01_Relieve.unity";

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
        public IEnumerator Jump_ShowsRise_Fall_AndLandingPoses()
        {
            yield return LoadScene();
            var animator = _player.GetComponentInChildren<CharacterWalkAnimator>();
            Assert.IsNotNull(animator, "el jugador no tiene animador");
            Assert.IsFalse(animator.IsAirPose, "en el suelo no hay pose de aire");

            Press(_keyboard.spaceKey);
            yield return null;
            Release(_keyboard.spaceKey);

            float highestArmRising = 0f, highestArmFalling = 0f, widestLanding = 0f;
            bool rose = false, fell = false, landed = false, captured = false;
            for (int i = 0; i < 200 && !(landed && !animator.IsLandingPose); i++)
            {
                yield return null;
                if (animator.IsAirPose && animator.VerticalSpeed > 0.5f)
                {
                    rose = true;
                    highestArmRising = Mathf.Max(highestArmRising, animator.LeftArmAngle);
                }
                if (animator.IsAirPose && animator.VerticalSpeed < -0.5f)
                {
                    fell = true;
                    highestArmFalling = Mathf.Max(highestArmFalling, animator.LeftArmAngle);
                    if (!captured)
                    {
                        captured = true;
                        // Cámara lateral solo para la captura: desde detrás el cuerpo tapa los brazos.
                        var side = new GameObject("CamaraLateral").AddComponent<Camera>();
                        side.depth = 100f;
                        Vector3 chest = _player.transform.position + Vector3.up * 0.9f;
                        side.transform.position = chest + _player.transform.right * 3.2f + _player.transform.forward * 1.2f;
                        side.transform.LookAt(chest);
                        yield return Capture("play_salto.png");
                        Object.Destroy(side.gameObject);
                    }
                }
                if (animator.IsLandingPose)
                {
                    landed = true;
                    widestLanding = Mathf.Max(widestLanding, animator.LeftLegAngle - animator.RightLegAngle);
                }
            }

            Assert.IsTrue(rose, "al saltar debe pasar por la pose de subida");
            Assert.IsTrue(fell, "y por la de caída");
            Assert.IsTrue(landed, "y amortiguar al tocar el suelo");
            Assert.Greater(highestArmRising, 60f, "subiendo, los brazos se lanzan hacia arriba");
            Assert.Greater(highestArmFalling, 60f, "cayendo siguen levantados");
            Assert.Greater(widestLanding, 5f, "al aterrizar las piernas se abren en zancada");
            Assert.IsTrue(_player.IsGrounded, "acaba en el suelo");
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
        public IEnumerator F3_SwitchesToTheNextScene()
        {
            // El juego empieza en Z01 (D50) y Prototype es la siguiente. La vuelta de la última a la
            // primera no se puede probar aquí: en estas pruebas el primer puesto del build lo ocupa la
            // escena del propio lanzador de tests, y recargarla tumba el editor.
            _keyboard = InputSystem.AddDevice<Keyboard>();
            SceneManager.LoadScene(Z01ScenePath, LoadSceneMode.Single);
            for (int i = 0; i < 40; i++) yield return null;
            Assert.AreEqual("Z01_Relieve", SceneManager.GetActiveScene().name);

            yield return Tap(_keyboard.f3Key);
            for (int i = 0; i < 10; i++) yield return null;

            Assert.AreEqual("Prototype", SceneManager.GetActiveScene().name, "F3 debe cargar la siguiente escena del build");
            Assert.IsNotNull(Object.FindFirstObjectByType<PrototypePlayerController>(), "y en ella hay jugador");
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
