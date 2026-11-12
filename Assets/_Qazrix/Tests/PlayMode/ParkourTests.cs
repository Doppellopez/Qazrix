using System.Collections;
using System.IO;
using NUnit.Framework;
using Qazrix.Prototype;
using Qazrix.Simulation.Movement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Qazrix.Tests.PlayMode
{
    /// <summary>
    /// Parkour contextual en la escena Prototype. El obstáculo bajo está en z = 5 (de 4,8 a 5,2; 0,5 m de
    /// alto) y la plataforma naranja ocupa x de -8 a -4 y z de 7 a 11, con 1 m de alto.
    /// </summary>
    public sealed class ParkourTests : InputTestFixture
    {
        private const string ScenePath = "Assets/_Qazrix/Scenes/Prototype/Prototype.unity";

        private Keyboard _keyboard;
        private PrototypePlayerController _player;
        private PlayerParkour _parkour;

        private IEnumerator LoadScene()
        {
            _keyboard = InputSystem.AddDevice<Keyboard>();
            SceneManager.LoadScene(ScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            _player = Object.FindFirstObjectByType<PrototypePlayerController>();
            _parkour = Object.FindFirstObjectByType<PlayerParkour>();
            Assert.IsNotNull(_player, "la escena no tiene jugador");
            Assert.IsNotNull(_parkour, "el jugador no tiene PlayerParkour");
            for (int i = 0; i < 30; i++) yield return null;
        }

        private IEnumerator Teleport(Vector3 position)
        {
            var body = _player.GetComponent<CharacterController>();
            body.enabled = false;
            _player.transform.SetPositionAndRotation(position, Quaternion.identity);
            body.enabled = true;
            for (int i = 0; i < 20; i++) yield return null;
        }

        /// <summary>Anda hacia el norte hasta pasar de la z indicada (o agota el tiempo).</summary>
        private IEnumerator WalkNorthUntil(float z, float timeout = 4f)
        {
            Press(_keyboard.wKey);
            float end = Time.time + timeout;
            while (Time.time < end && _player.transform.position.z < z) yield return null;
        }

        private IEnumerator WaitForAction(float timeout = 3f)
        {
            float end = Time.time + timeout;
            while (Time.time < end && _parkour.ActionsDone == 0) yield return null;
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
        public IEnumerator JumpKey_VaultsTheLowObstacle()
        {
            yield return LoadScene();
            yield return WalkNorthUntil(4.1f);
            Assert.AreEqual(ParkourAction.Vault, _player.AvailableParkour, "delante del obstáculo debe ofrecerse saltarlo");
            yield return Capture("play_parkour_aviso.png");

            Press(_keyboard.spaceKey);
            yield return null;
            Release(_keyboard.spaceKey);

            bool wasInParkour = false;
            float end = Time.time + 3f;
            while (Time.time < end && _parkour.ActionsDone == 0)
            {
                wasInParkour |= _player.IsInParkour;
                yield return null;
            }
            Release(_keyboard.wKey);
            for (int i = 0; i < 10; i++) yield return null;

            Assert.IsTrue(wasInParkour);
            Assert.AreEqual(ParkourAction.Vault, _parkour.LastAction);
            Assert.Greater(_player.transform.position.z, 5.4f, "debe quedar al otro lado del obstáculo");
            Assert.Less(Mathf.Abs(_player.transform.position.y), 0.15f, "y de pie en el suelo");
            Assert.IsTrue(_player.IsGrounded);
        }

        [UnityTest]
        public IEnumerator JumpKey_ClimbsOntoThePlatform()
        {
            yield return LoadScene();
            // Se acerca por la cara norte (z = 11) andando hacia el sur: por la cara sur está el escalón.
            yield return Teleport(new Vector3(-6f, 0f, 12.4f));
            Press(_keyboard.sKey);
            float limit = Time.time + 4f;
            while (Time.time < limit && _player.AvailableParkour != ParkourAction.Climb) yield return null;
            Assert.AreEqual(ParkourAction.Climb, _player.AvailableParkour, "delante de la plataforma debe ofrecerse trepar");

            Press(_keyboard.spaceKey);
            yield return null;
            Release(_keyboard.spaceKey);
            yield return WaitForAction();
            Release(_keyboard.sKey);
            for (int i = 0; i < 10; i++) yield return null;

            Assert.AreEqual(ParkourAction.Climb, _parkour.LastAction);
            Assert.AreEqual(1f, _player.transform.position.y, 0.15f, "debe quedar encima de la plataforma (1 m)");
            Assert.Less(_player.transform.position.z, 11f);
            Assert.Greater(_player.transform.position.z, 7f);
            Assert.IsTrue(_player.IsGrounded);
        }

        [UnityTest]
        public IEnumerator JumpKey_InOpenGround_IsAPlainJump()
        {
            yield return LoadScene();
            Assert.AreEqual(ParkourAction.None, _player.AvailableParkour);

            Press(_keyboard.spaceKey);
            yield return null;
            Release(_keyboard.spaceKey);

            float startY = _player.transform.position.y, maxY = startY;
            for (int i = 0; i < 40; i++)
            {
                yield return null;
                maxY = Mathf.Max(maxY, _player.transform.position.y);
            }

            Assert.AreEqual(0, _parkour.ActionsDone, "sin obstáculo marcado no hay parkour");
            Assert.Greater(maxY - startY, 0.5f, "pero sí salta");
        }

        [UnityTest]
        public IEnumerator UnmarkedWall_CannotBeClimbed()
        {
            yield return LoadScene();
            // La pared alta (z = -8) no lleva marca: aunque esté pegado y de frente, no se trepa.
            yield return Teleport(new Vector3(0f, 0f, -9.5f));
            yield return WalkNorthUntil(-8.7f, 2f);
            for (int i = 0; i < 20; i++) yield return null;

            Assert.AreEqual(ParkourAction.None, _player.AvailableParkour);
            Press(_keyboard.spaceKey);
            yield return null;
            Release(_keyboard.spaceKey);
            for (int i = 0; i < 60; i++) yield return null;
            Release(_keyboard.wKey);

            Assert.AreEqual(0, _parkour.ActionsDone);
            Assert.Less(_player.transform.position.z, -8f, "sigue al sur de la pared");
        }
    }
}
