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
        public IEnumerator ArrowKeys_MoveAlongFacing_AndTurnTheBody()
        {
            yield return LoadScene();
            Transform body = _player.transform;
            Vector3 start = body.position;
            Vector3 facing = body.forward;

            // Flecha arriba: avanza hacia donde mira, sin girar.
            Press(_keyboard.upArrowKey);
            yield return Frames(45);
            Release(_keyboard.upArrowKey);
            yield return Frames(20);
            Vector3 forward = body.position - start;
            Assert.Greater(Vector3.Dot(forward, facing), 0.5f, "la flecha arriba debe avanzar hacia donde mira");
            Assert.Less(Vector3.Angle(body.forward, facing), 3f, "avanzar no debe girarlo");

            // Flecha abajo: retrocede sin darse la vuelta.
            Vector3 before = body.position;
            Press(_keyboard.downArrowKey);
            yield return Frames(45);
            Release(_keyboard.downArrowKey);
            yield return Frames(20);
            Assert.Less(Vector3.Dot(body.position - before, facing), -0.3f, "la flecha abajo debe retroceder");
            Assert.Less(Vector3.Angle(body.forward, facing), 3f, "retroceder no debe girarlo");

            // Flechas laterales: giran un poco a cada lado, sin desplazarlo.
            before = body.position;
            Press(_keyboard.rightArrowKey);
            yield return Frames(30);
            Release(_keyboard.rightArrowKey);
            yield return Frames(5);
            float right = Vector3.SignedAngle(facing, body.forward, Vector3.up);
            Assert.Greater(right, 20f, "la flecha derecha gira hacia la derecha");
            Assert.Less(right, 120f, "pero solo un poco");

            Press(_keyboard.leftArrowKey);
            yield return Frames(60);
            Release(_keyboard.leftArrowKey);
            yield return Frames(5);
            Assert.Less(Vector3.SignedAngle(facing, body.forward, Vector3.up), right - 20f, "la flecha izquierda gira hacia el otro lado");
            Assert.Less(Vector3.Distance(body.position, before), 0.2f, "girar no debe desplazarlo");
        }

        private static float FlatYaw(Transform t)
        {
            Vector3 f = t.forward;
            f.y = 0f;
            return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        }

        [UnityTest]
        public IEnumerator Camera_FollowsWhereTheCharacterFaces_WhenTurningWithArrows()
        {
            yield return LoadScene();
            var orbit = Object.FindFirstObjectByType<PrototypeOrbitCamera>();
            Assert.IsNotNull(orbit, "la escena no tiene cámara orbital");
            Transform cam = orbit.transform, body = _player.transform;
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(FlatYaw(cam), FlatYaw(body))), 10f, "al empezar, la cámara mira por donde mira el personaje");

            // Quieto, sin pulsar nada, la cámara no se mueve.
            float before = FlatYaw(cam);
            yield return Frames(30);
            Assert.AreEqual(0f, Mathf.DeltaAngle(before, FlatYaw(cam)), 0.5f, "sin pulsar nada, la cámara se queda quieta");

            // Gira con la flecha derecha y avanza: la cámara se coloca detrás de la nueva dirección.
            Press(_keyboard.rightArrowKey);
            yield return Frames(45);
            Release(_keyboard.rightArrowKey);
            // Hay que dejar pasar un fotograma entre soltar una tecla y pulsar otra: cada pulsación del teclado simulado
            // encola el estado completo del teclado y, si no, la tecla soltada seguiría pulsada.
            yield return Frames(3);
            float turned = Mathf.DeltaAngle(before, FlatYaw(body));
            Assert.Greater(turned, 30f, "el personaje debe haber girado a la derecha");

            Press(_keyboard.upArrowKey);
            int following = 0;
            float started = Time.time;
            string samples = string.Empty;
            for (int i = 0; i < 120; i++)
            {
                if (orbit.IsFollowingFacing) following++;
                if (i % 20 == 0) samples += $" [{i}: cám {FlatYaw(cam):0}° pers {FlatYaw(body):0}°]";
                yield return null;
            }
            Debug.Log("[Qazrix] Seguimiento de cámara:" + samples + $" · suavizado {orbit.FollowSmoothSeconds:0.00} s");
            float seconds = Time.time - started;
            Release(_keyboard.upArrowKey);
            yield return Frames(10);

            float gap = Mathf.Abs(Mathf.DeltaAngle(FlatYaw(cam), FlatYaw(body)));
            Assert.Less(gap, 12f, $"la cámara debe quedar detrás de donde mira el personaje (diferencia {gap:0.0}°; siguió {following}/120 fotogramas en {seconds:0.00} s; personaje giró {turned:0.0}°)");
            Assert.Greater(Mathf.DeltaAngle(before, FlatYaw(cam)), 25f, "y haber girado con él hacia la derecha");
        }

        [UnityTest]
        public IEnumerator Camera_TurnsAround180_WhenBackIsPressed_AndTheCharacterTurnsToo()
        {
            yield return LoadScene();
            var orbit = Object.FindFirstObjectByType<PrototypeOrbitCamera>();
            Assert.IsNotNull(orbit);
            Transform cam = orbit.transform, body = _player.transform;
            float startCam = FlatYaw(cam), startBody = FlatYaw(body);
            Vector3 startPosition = body.position;

            // Al pulsar atrás empieza una media vuelta lenta (no instantánea).
            Press(_keyboard.sKey);
            yield return Frames(20);
            Assert.IsTrue(orbit.IsTurningAround, "al pulsar atrás la cámara empieza a dar la vuelta");
            float partial = Mathf.Abs(Mathf.DeltaAngle(startCam, FlatYaw(cam)));
            Assert.Greater(partial, 1f, "ya ha empezado a girar");
            Assert.Less(partial, 170f, "pero va despacio: no se ha dado la vuelta de golpe");

            // Manteniendo atrás: acaba a 180° y se queda ahí.
            float limit = Time.time + 6f;
            while (Time.time < limit && orbit.IsTurningAround) yield return null;
            Assert.IsFalse(orbit.IsTurningAround, "la media vuelta debe terminar");
            yield return Frames(5);
            float afterTurn = FlatYaw(cam);
            Assert.AreEqual(180f, Mathf.Abs(Mathf.DeltaAngle(startCam, afterTurn)), 4f, "la cámara gira 180°");
            yield return Frames(60);
            Assert.AreEqual(0f, Mathf.DeltaAngle(afterTurn, FlatYaw(cam)), 1f, "y se queda ahí aunque se siga manteniendo atrás");

            // El personaje se ha dado la vuelta con ella y ha ido hacia donde miraba la cámara al final.
            Release(_keyboard.sKey);
            yield return Frames(10);
            Assert.AreEqual(180f, Mathf.Abs(Mathf.DeltaAngle(startBody, FlatYaw(body))), 25f, "el personaje también gira 180°");
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(FlatYaw(cam), FlatYaw(body))), 25f, "y la cámara queda detrás de él");
            Vector3 moved = body.position - startPosition;
            Vector3 oldBack = Quaternion.Euler(0f, startCam + 180f, 0f) * Vector3.forward;
            Assert.Greater(Vector3.Dot(moved, oldBack), 2f, "ha ido hacia atrás (hacia donde mira ahora), no ha vuelto sobre sus pasos");

            // Al soltar, se queda quieta.
            float resting = FlatYaw(cam);
            yield return Frames(40);
            Assert.AreEqual(0f, Mathf.DeltaAngle(resting, FlatYaw(cam)), 1f, "al soltar atrás la cámara se queda quieta");
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
