using System.Collections;
using NUnit.Framework;
using Qazrix.AI;
using Qazrix.Prototype;
using Qazrix.Simulation.AI;
using Qazrix.Simulation.Movement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Qazrix.Tests.PlayMode
{
    /// <summary>Humo de la escena Z01_Relieve: sobre el relieve real también hay guardia y parkour.</summary>
    public sealed class Z01SceneTests : InputTestFixture
    {
        private const string ScenePath = "Assets/_Qazrix/Scenes/Z01/Z01_Relieve.unity";

        private Keyboard _keyboard;
        private PrototypePlayerController _player;
        private PlayerParkour _parkour;
        private GuardController _guard;

        private IEnumerator LoadScene()
        {
            _keyboard = InputSystem.AddDevice<Keyboard>();
            SceneManager.LoadScene(ScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            _player = Object.FindFirstObjectByType<PrototypePlayerController>();
            _parkour = Object.FindFirstObjectByType<PlayerParkour>();
            _guard = Object.FindFirstObjectByType<GuardController>();
            Assert.IsNotNull(_player, "la escena no tiene jugador");
            Assert.IsNotNull(_parkour, "el jugador no tiene PlayerParkour");
            Assert.IsNotNull(_guard, "la escena no tiene guardia");
            for (int i = 0; i < 40; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator Player_StandsOnTheTerrain()
        {
            yield return LoadScene();
            Assert.IsTrue(_player.IsGrounded, "debe quedar apoyado en el relieve");
            Assert.Greater(_player.transform.position.y, -20f, "no ha atravesado el suelo");
        }

        [UnityTest]
        public IEnumerator Guard_PatrolsOverTheTerrain_FollowingTheNavMesh()
        {
            yield return LoadScene();
            Vector3 start = _guard.transform.position;

            bool followedPath = false;
            float end = Time.time + 4f;
            while (Time.time < end)
            {
                followedPath |= _guard.IsFollowingPath;
                yield return null;
            }

            Assert.AreEqual(AlertState.Patrol, _guard.State, "a 28 m del jugador no debe percibirlo");
            Assert.Greater(Vector3.Distance(start, _guard.transform.position), 3f, "debería haber avanzado por su ruta");
            Assert.IsTrue(followedPath, "debería usar la malla de navegación del relieve");
        }

        [UnityTest]
        public IEnumerator JumpKey_VaultsTheLowWall()
        {
            yield return LoadScene();
            Press(_keyboard.wKey);
            float limit = Time.time + 6f;
            while (Time.time < limit && _player.AvailableParkour != ParkourAction.Vault) yield return null;
            Assert.AreEqual(ParkourAction.Vault, _player.AvailableParkour, "delante del murete debe ofrecerse saltarlo");

            Press(_keyboard.spaceKey);
            yield return null;
            Release(_keyboard.spaceKey);
            limit = Time.time + 3f;
            while (Time.time < limit && _parkour.ActionsDone == 0) yield return null;
            Release(_keyboard.wKey);
            for (int i = 0; i < 15; i++) yield return null;

            Assert.AreEqual(ParkourAction.Vault, _parkour.LastAction);
            Assert.Greater(_player.transform.position.z, 6.4f, "debe quedar al otro lado del murete");
            Assert.IsTrue(_player.IsGrounded);
        }
    }
}
