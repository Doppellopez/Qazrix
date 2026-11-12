using System.Collections;
using System.IO;
using NUnit.Framework;
using Qazrix.AI;
using Qazrix.Prototype;
using Qazrix.Simulation.AI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Qazrix.Tests.PlayMode
{
    /// <summary>
    /// Escenarios reproducibles de sigilo sobre la escena Prototype (spec §6.2): el jugador nace en el
    /// origen mirando al norte (+Z) y el guardia se coloca de centinela donde haga falta.
    /// </summary>
    public sealed class GuardTests : InputTestFixture
    {
        private const string ScenePath = "Assets/_Qazrix/Scenes/Prototype/Prototype.unity";

        private Keyboard _keyboard;
        private GuardController _guard;
        private PlayerStealthTarget _player;

        private IEnumerator LoadScene()
        {
            _keyboard = InputSystem.AddDevice<Keyboard>();
            SceneManager.LoadScene(ScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            _guard = Object.FindFirstObjectByType<GuardController>();
            _player = Object.FindFirstObjectByType<PlayerStealthTarget>();
            Assert.IsNotNull(_guard, "la escena no tiene guardia");
            Assert.IsNotNull(_player, "el jugador no tiene PlayerStealthTarget");
            for (int i = 0; i < 30; i++) yield return null;
        }

        private static IEnumerator Seconds(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        private static Quaternion Facing(Vector3 from, Vector3 to)
        {
            Vector3 flat = to - from;
            flat.y = 0f;
            return Quaternion.LookRotation(flat.normalized, Vector3.up);
        }

        [UnityTest]
        public IEnumerator Patrol_WalksTheRoute_WithoutNoticingADistantPlayer()
        {
            yield return LoadScene();
            Vector3 start = _guard.transform.position;

            yield return Seconds(3f);

            Assert.AreEqual(AlertState.Patrol, _guard.State);
            Assert.Greater(Vector3.Distance(start, _guard.transform.position), 2f, "debería haber avanzado por su ruta");
            Assert.AreEqual(0f, _guard.Awareness);
        }

        [UnityTest]
        public IEnumerator PlayerInFront_IsDiscoveredGradually_ThenCaught()
        {
            yield return LoadScene();
            var post = new Vector3(-4f, 0f, -4f);
            _guard.SetPost(post, Facing(post, _player.transform.position));

            bool wasSuspicious = false, wasAlert = false;
            float end = Time.time + 5f;
            while (Time.time < end && _player.TimesCaught == 0)
            {
                if (_guard.State == AlertState.Suspicious) wasSuspicious = true;
                if (_guard.State == AlertState.Alert) wasAlert = true;
                yield return null;
            }

            Assert.IsTrue(wasSuspicious, "antes de la alarma debe pasar por sospecha");
            Assert.IsTrue(wasAlert, "con el jugador de frente y a la vista debe dar la alarma");
            Assert.AreEqual(1, _player.TimesCaught, "al alcanzarlo, el jugador vuelve al punto de salida");
            Assert.AreEqual(AlertState.Return, _guard.State, "tras alcanzarlo, el guardia vuelve a su puesto");
        }

        [UnityTest]
        public IEnumerator PlayerBehindAWall_IsNotSeen()
        {
            yield return LoadScene();
            // La pared alta está en z = -8. El guardia queda al sur, mirando hacia el jugador, a 12 m:
            // dentro del alcance y del cono, pero con la pared en medio.
            var post = new Vector3(0f, 0f, -12f);
            _guard.SetPost(post, Facing(post, _player.transform.position));

            float maxVisibility = 0f;
            float end = Time.time + 3f;
            while (Time.time < end)
            {
                maxVisibility = Mathf.Max(maxVisibility, _guard.LastVisibility);
                yield return null;
            }

            Assert.AreEqual(0f, maxVisibility, "no puede ver a través de la pared");
            Assert.AreEqual(AlertState.Patrol, _guard.State);
            Assert.AreEqual(0f, _guard.Awareness);
        }

        [UnityTest]
        public IEnumerator RunningBehindTheGuard_IsHeard_AndInvestigated()
        {
            yield return LoadScene();
            // Guardia a 5 m al sur del jugador, de espaldas a él.
            var post = new Vector3(0f, 0f, -5f);
            _guard.SetPost(post, Quaternion.LookRotation(Vector3.back, Vector3.up));

            yield return Seconds(0.5f);
            Assert.AreEqual(AlertState.Patrol, _guard.State, "quieto y a su espalda no lo percibe");

            Press(_keyboard.leftShiftKey);
            Press(_keyboard.wKey);
            bool heard = false;
            float end = Time.time + 1f;
            while (Time.time < end)
            {
                heard |= _guard.LastHeard;
                yield return null;
            }
            Release(_keyboard.wKey);
            Release(_keyboard.leftShiftKey);

            Assert.IsTrue(heard, "correr a 5-8 m debe oírse");
            Assert.AreEqual(AlertState.Investigate, _guard.State, "un ruido fuera de la vista se investiga, no dispara la alarma");
            yield return Capture("play_guardia_investiga.png");
        }

        [UnityTest]
        public IEnumerator CrouchWalkingBehindTheGuard_IsSilent()
        {
            yield return LoadScene();
            var post = new Vector3(0f, 0f, -5f);
            _guard.SetPost(post, Quaternion.LookRotation(Vector3.back, Vector3.up));

            Press(_keyboard.cKey);
            Press(_keyboard.wKey);
            yield return Seconds(1.5f);
            Release(_keyboard.wKey);
            Release(_keyboard.cKey);

            Assert.AreEqual(AlertState.Patrol, _guard.State, "agachado no hace ruido");
            Assert.AreEqual(0f, _guard.Awareness);
        }

        private static IEnumerator Capture(string fileName)
        {
            yield return new WaitForEndOfFrame();
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            Directory.CreateDirectory("Reports/QA");
            File.WriteAllBytes("Reports/QA/" + fileName, shot.EncodeToPNG());
            Object.Destroy(shot);
        }
    }
}
