using System.Collections;
using System.IO;
using System.Linq;
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
        public IEnumerator ChasingGuard_HoldsTheArmAndScimitarHigh_AndLowersThemAfterwards()
        {
            yield return LoadScene();
            var animator = _guard.GetComponentInChildren<Qazrix.Prototype.CharacterWalkAnimator>();
            Assert.IsNotNull(animator, "el guardia no tiene animador");
            Transform grip = _guard.GetComponentsInChildren<Transform>().First(t => t.name == "Sable");
            Transform tip = _guard.GetComponentsInChildren<Transform>().First(t => t.name == "Curva_5");

            // En patrulla lleva el arma abajo: brazo recto y hoja apuntando hacia delante, no hacia arriba.
            Assert.AreEqual(0f, animator.WeaponRaiseWeight, 1e-3f);
            Assert.Less(animator.RightArmAngle, 20f, "patrullando, el brazo del sable cuelga");
            // La hoja es curva y su punta sube un poco aun con el brazo caído: se mide como punto de partida.
            float loweredTip = tip.position.y - grip.position.y;
            Assert.Less(loweredTip, 0.3f, "y la hoja no apunta hacia arriba");

            var post = new Vector3(-4f, 0f, -4f);
            _guard.SetPost(post, Facing(post, _player.transform.position));

            // Persiguiendo (alarma): brazo en alto y hoja hacia arriba.
            float raisedArm = 0f, tipAboveGrip = float.MinValue, weight = 0f;
            float end = Time.time + 5f;
            while (Time.time < end && _guard.State != AlertState.Alert) yield return null;
            Assert.AreEqual(AlertState.Alert, _guard.State, "el guardia debe llegar a perseguir");
            end = Time.time + 0.8f;
            while (Time.time < end && _player.TimesCaught == 0)
            {
                raisedArm = Mathf.Max(raisedArm, animator.RightArmAngle);
                tipAboveGrip = Mathf.Max(tipAboveGrip, tip.position.y - grip.position.y);
                weight = Mathf.Max(weight, animator.WeaponRaiseWeight);
                yield return null;
            }
            Assert.Greater(weight, 0.9f, "persiguiendo, el arma sube del todo");
            Assert.Greater(raisedArm, 110f, "el brazo del sable queda por encima del hombro");
            Assert.Greater(tipAboveGrip, loweredTip + 0.3f, $"y la punta de la hoja mucho más arriba que con el brazo caído ({loweredTip:0.00} → {tipAboveGrip:0.00} m)");

            // Pasada la persecución vuelve a bajar.
            end = Time.time + 20f;
            while (Time.time < end && _guard.State == AlertState.Alert) yield return null; // acaba alcanzando al jugador y vuelve a su puesto
            yield return Seconds(1.5f);
            Assert.AreNotEqual(AlertState.Alert, _guard.State, "la persecución debe acabar");
            Assert.Less(animator.WeaponRaiseWeight, 0.05f, "al dejar de perseguir baja el arma");
            Assert.Less(animator.RightArmAngle, 20f);
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
        public IEnumerator Investigating_GoesAroundTheWall_InsteadOfPushingIntoIt()
        {
            yield return LoadScene();
            // La pared alta ocupa x de -5 a 5 en z = -8. Guardia al sur, de espaldas; jugador justo al
            // otro lado, a 5 m. En línea recta el guardia se quedaría empujando contra la pared.
            var post = new Vector3(0f, 0f, -10.5f);
            _guard.SetPost(post, Quaternion.LookRotation(Vector3.back, Vector3.up));

            var body = _player.GetComponent<CharacterController>();
            body.enabled = false;
            _player.transform.position = new Vector3(0f, 0f, -5.5f);
            body.enabled = true;
            yield return Seconds(0.5f);
            Assert.AreEqual(AlertState.Patrol, _guard.State, "con la pared en medio y sin ruido no lo percibe");

            Press(_keyboard.leftShiftKey);
            Press(_keyboard.wKey);
            yield return Seconds(0.6f);
            Release(_keyboard.wKey);
            Release(_keyboard.leftShiftKey);
            Assert.AreNotEqual(AlertState.Patrol, _guard.State, "correr a 5 m debe oírse aunque haya pared");

            bool followedPath = false, crossed = false;
            float widestDetour = 0f;
            float end = Time.time + 7f;
            while (Time.time < end && !crossed)
            {
                followedPath |= _guard.IsFollowingPath;
                Vector3 p = _guard.transform.position;
                if (p.z < -7.5f) widestDetour = Mathf.Max(widestDetour, Mathf.Abs(p.x));
                crossed = p.z > -7.5f; // ya está al norte de la pared
                yield return null;
            }

            Assert.IsTrue(followedPath, "debería usar la malla de navegación");
            Assert.IsTrue(crossed, "debería haber pasado al otro lado de la pared");
            Assert.Greater(widestDetour, 5f, "solo se puede pasar por uno de los extremos de la pared");
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
