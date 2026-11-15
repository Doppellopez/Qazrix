using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Qazrix.AI;
using Qazrix.Prototype;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Qazrix.Tests.PlayMode
{
    /// <summary>Modelos del hidalgo (jugador) y del guardia en la escena Prototype.</summary>
    public sealed class CharacterModelTests : InputTestFixture
    {
        private const string ScenePath = "Assets/_Qazrix/Scenes/Prototype/Prototype.unity";

        private Keyboard _keyboard;
        private PrototypePlayerController _player;
        private GuardController _guard;

        private IEnumerator LoadScene()
        {
            _keyboard = InputSystem.AddDevice<Keyboard>();
            SceneManager.LoadScene(ScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;
            _player = Object.FindFirstObjectByType<PrototypePlayerController>();
            _guard = Object.FindFirstObjectByType<GuardController>();
            Assert.IsNotNull(_player);
            Assert.IsNotNull(_guard);
            for (int i = 0; i < 30; i++) yield return null;
        }

        private static IEnumerator Capture(string fileName)
        {
            yield return new WaitForEndOfFrame();
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            Directory.CreateDirectory("Reports/QA");
            File.WriteAllBytes("Reports/QA/" + fileName, shot.EncodeToPNG());
            Object.Destroy(shot);
        }

        private static void AssertTextured(Renderer[] renderers, string who)
        {
            foreach (Renderer r in renderers.Where(x => x.name != "Senal"))
            {
                Assert.IsNotNull(r.sharedMaterial, $"{who}: {r.name} sin material");
                Assert.IsNotNull(r.sharedMaterial.mainTexture, $"{who}: {r.name} sin textura");
            }
        }

        [UnityTest]
        public IEnumerator Player_IsATexturedHidalgo_WithWideBreeches()
        {
            yield return LoadScene();
            Renderer[] parts = _player.GetComponentsInChildren<Renderer>();
            Assert.Greater(parts.Length, 25, "el modelo tiene decenas de piezas, no una cápsula");
            AssertTextured(parts, "hidalgo");

            // Los calzones son la parte más ancha de la pierna: más que la bota que hay debajo.
            Renderer breeches = parts.First(p => p.name == "Calzon");
            Renderer boot = parts.First(p => p.name == "Canya");
            Assert.Greater(breeches.bounds.size.x, boot.bounds.size.x * 1.8f, "los calzones deben ser claramente anchos");
            Assert.IsNotNull(_player.transform.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Espada"));

            // El modelo cabe en el cuerpo físico: unos 1,8 m de alto con los pies en el suelo.
            var bounds = new Bounds(parts[0].bounds.center, Vector3.zero);
            foreach (Renderer p in parts) bounds.Encapsulate(p.bounds);
            Assert.AreEqual(_player.transform.position.y, bounds.min.y, 0.08f, "pies en el suelo");
            Assert.AreEqual(1.8f, bounds.size.y, 0.12f, "altura de 1,8 m");
            yield return Capture("play_hidalgo.png");
        }

        [UnityTest]
        public IEnumerator Player_LegsSwing_WhenWalking()
        {
            yield return LoadScene();
            var animator = _player.GetComponentInChildren<CharacterWalkAnimator>();
            Assert.IsNotNull(animator);
            Assert.AreEqual(0f, animator.LeftLegAngle, 0.5f, "quieto, las piernas están rectas");

            Press(_keyboard.wKey);
            float widest = 0f;
            bool opposite = false;
            float end = Time.time + 1.5f;
            while (Time.time < end)
            {
                widest = Mathf.Max(widest, Mathf.Abs(animator.LeftLegAngle));
                opposite |= animator.LeftLegAngle * animator.RightLegAngle < -1f;
                yield return null;
            }
            Release(_keyboard.wKey);

            Assert.Greater(widest, 10f, "al andar, las piernas deben balancearse");
            Assert.IsTrue(opposite, "y hacerlo una contraria a la otra");
        }

        [UnityTest]
        public IEnumerator Guard_IsATexturedFigure_WithACurvedSabre()
        {
            yield return LoadScene();
            Renderer[] parts = _guard.GetComponentsInChildren<Renderer>();
            Assert.Greater(parts.Length, 25);
            AssertTextured(parts, "guardia");

            Transform[] nodes = _guard.GetComponentsInChildren<Transform>();
            Assert.IsNotNull(nodes.FirstOrDefault(t => t.name == "Sable"), "debe llevar el sable");
            Assert.IsNotNull(nodes.FirstOrDefault(t => t.name == "Turbante_medio"), "y turbante");

            // La hoja es curva: su punta no está en la prolongación recta de la empuñadura.
            Transform grip = nodes.First(t => t.name == "Sable");
            Transform tip = nodes.First(t => t.name == "Curva_5");
            Vector3 along = grip.up;
            Vector3 toTip = tip.position - grip.position;
            float off = Vector3.Cross(along, toTip).magnitude;
            Assert.Greater(off, 0.15f, "la punta debe desviarse de la recta de la empuñadura");

            // Es más ancho que el hidalgo.
            float guardWidth = parts.First(p => p.name == "Vientre").bounds.size.x;
            float playerWidth = _player.GetComponentsInChildren<Renderer>().First(p => p.name == "Sayo").bounds.size.x;
            Assert.Greater(guardWidth, playerWidth * 1.3f, "el guardia es corpulento");
        }
    }
}
