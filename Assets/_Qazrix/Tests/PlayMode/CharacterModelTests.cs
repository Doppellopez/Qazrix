using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Qazrix.AI;
using Qazrix.Prototype;
using Qazrix.Simulation.Movement;
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
        public IEnumerator Player_HasFace_Collar_Buttons_AndCuffs()
        {
            yield return LoadScene();
            Renderer[] parts = _player.GetComponentsInChildren<Renderer>();
            Renderer Part(string name)
            {
                Renderer found = parts.FirstOrDefault(p => p.name == name);
                Assert.IsNotNull(found, $"falta la pieza {name}");
                return found;
            }
            // Posición de una pieza en el sistema del jugador (x derecha, y arriba, z hacia delante).
            Vector3 Local(Renderer r) => _player.transform.InverseTransformPoint(r.bounds.center);

            // Cara: dos ojos simétricos, por delante del centro de la cabeza y a la altura de la cara.
            Vector3 head = Local(Part("Cabeza")), left = Local(Part("Ojo_izq")), right = Local(Part("Ojo_der"));
            Assert.AreEqual(-left.x, right.x, 0.005f, "los ojos son simétricos");
            Assert.Greater(left.z, head.z + 0.05f, "los ojos están en la cara, no dentro de la cabeza");
            Assert.AreEqual(head.y, left.y, 0.08f, "y a la altura de la cabeza");
            Assert.Greater(Local(Part("Ceja_izq")).y, left.y, "las cejas van sobre los ojos");
            Assert.Greater(Local(Part("Oreja_der")).x, right.x, "las orejas van a los lados");

            // Golilla: un anillo de lino más ancho que el cuello, entre el cuello y la cabeza.
            Renderer ruff = Part("Golilla");
            Assert.Greater(ruff.bounds.size.x, Part("Cuello").bounds.size.x * 1.5f, "la golilla rodea el cuello");
            Assert.Less(Local(ruff).y, head.y, "y queda bajo la cabeza");

            // Tres botones por delante del pecho, uno bajo otro.
            float chestZ = Local(Part("Sayo")).z;
            float lastY = float.MaxValue;
            for (int i = 1; i <= 3; i++)
            {
                Vector3 button = Local(Part("Boton_" + i));
                Assert.Greater(button.z, chestZ + 0.08f, $"el botón {i} está en el frente del sayo");
                Assert.Less(button.y, lastY, "los botones bajan uno tras otro");
                lastY = button.y;
            }

            // Puños de lino por encima de las manos, y las manos más estrechas que el puño en grosor de frente.
            foreach (string side in new[] { "Brazo_izq", "Brazo_der" })
            {
                Transform arm = _player.GetComponentsInChildren<Transform>().First(t => t.name == side);
                Renderer cuff = arm.GetComponentsInChildren<Renderer>().First(p => p.name == "Puno");
                Renderer hand = arm.GetComponentsInChildren<Renderer>().First(p => p.name == "Mano");
                Assert.Greater(cuff.bounds.center.y, hand.bounds.center.y, $"{side}: el puño está sobre la mano");
                Assert.IsNotNull(arm.GetComponentsInChildren<Renderer>().FirstOrDefault(p => p.name == "Antebrazo"), $"{side}: tiene antebrazo");
            }

            // La suela toca el suelo.
            Assert.AreEqual(_player.transform.position.y, Part("Suela").bounds.min.y, 0.04f, "la suela apoya en el suelo");

            yield return CaptureFront("play_hidalgo_frente.png", "play_hidalgo_busto.png");
        }

        /// <summary>
        /// Fotos del hidalgo de frente, con una cámara temporal: cuerpo entero y busto. La cámara del juego
        /// lo ve de espaldas y la capa tapa el detalle.
        /// </summary>
        private IEnumerator CaptureFront(string bodyFile, string bustFile)
        {
            yield return new WaitForEndOfFrame();
            var go = new GameObject("CamaraFrente");
            Camera cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.62f, 0.70f);
            cam.fieldOfView = 30f;
            var target = new RenderTexture(768, 1024, 24);
            cam.targetTexture = target;

            Transform t = _player.transform;
            void Shoot(string file, Vector3 localPosition, Vector3 localLookAt)
            {
                cam.transform.position = t.TransformPoint(localPosition);
                cam.transform.LookAt(t.TransformPoint(localLookAt));
                cam.Render();
                RenderTexture.active = target;
                var shot = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                shot.Apply();
                Directory.CreateDirectory("Reports/QA");
                File.WriteAllBytes("Reports/QA/" + file, shot.EncodeToPNG());
                RenderTexture.active = null;
                Object.Destroy(shot);
            }

            Shoot(bodyFile, new Vector3(0.5f, 1.2f, 4.6f), new Vector3(0f, 0.9f, 0f));
            Shoot(bustFile, new Vector3(0.35f, 1.6f, 1.7f), new Vector3(0f, 1.45f, 0f));
            cam.targetTexture = null;
            target.Release();
            Object.Destroy(go);
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
        public IEnumerator Cape_IsFlexible_AndMovesWhenWalkingAndJumping()
        {
            yield return LoadScene();
            var cape = _player.GetComponentInChildren<CapeAnimator>();
            Assert.IsNotNull(cape, "el hidalgo no tiene animador de capa");
            Assert.AreEqual(CapeSim.Segments, cape.SegmentCount, "la capa tiene ocho tramos articulados");
            int last = CapeSim.Segments - 1;
            Assert.AreEqual(CapeSim.RestLift, cape.Lift(last), 0.6f, "quieto, la capa cuelga en reposo");
            Assert.AreEqual(0, CapeClipping.PenetratingPoints(_player.transform), "quieto, el cuerpo no atraviesa la capa");

            // Corriendo, el tramo de abajo se separa de la espalda más que el de arriba.
            Press(_keyboard.leftShiftKey);
            Press(_keyboard.wKey);
            float widestRun = 0f, topAtWidest = 0f;
            int worstClip = 0;
            float end = Time.time + 1.6f;
            while (Time.time < end)
            {
                if (cape.Lift(last) > widestRun) { widestRun = cape.Lift(last); topAtWidest = cape.Lift(0); }
                worstClip = Mathf.Max(worstClip, CapeClipping.PenetratingPoints(_player.transform));
                yield return null;
            }
            Assert.AreEqual(0, worstClip, "corriendo, las piernas no atraviesan la capa");
            Assert.Greater(widestRun, 6f, "corriendo, la capa debe levantarse por detrás");
            Assert.Greater(widestRun, topAtWidest + 3f, "y más abajo que arriba: se dobla");
            yield return Capture("play_hidalgo_capa_corriendo.png");

            // Foto de perfil, corriendo: una cámara temporal pegada al jugador, a su derecha. De espaldas la capa
            // se levanta hacia atrás y queda tapada por el propio cuerpo; de lado se ve cómo se dobla.
            var sideObject = new GameObject("CamaraPerfil");
            Camera side = sideObject.AddComponent<Camera>();
            side.depth = 50f;
            side.nearClipPlane = 0.05f;
            sideObject.transform.SetParent(_player.transform, false);
            sideObject.transform.localPosition = new Vector3(2.4f, 1.15f, -0.25f);
            sideObject.transform.LookAt(_player.transform.position + Vector3.up * 1.05f + _player.transform.forward * -0.25f);
            yield return null;
            yield return Capture("play_hidalgo_capa_perfil.png");
            Object.Destroy(sideObject);
            Release(_keyboard.wKey);
            Release(_keyboard.leftShiftKey);

            // Al parar, vuelve a colgar.
            float settleBy = Time.time + 3f;
            while (Time.time < settleBy) yield return null;
            Assert.AreEqual(CapeSim.RestLift, cape.Lift(last), 1.5f, "al parar la capa se asienta");
        }

        [UnityTest]
        public IEnumerator Cape_NeverCrossesTheBody_WhenJumping_AndOpensWhenFalling()
        {
            yield return LoadScene(); // quieto en el sitio de salida: ningún obstáculo cerca que convierta el salto en parkour
            var cape = _player.GetComponentInChildren<CapeAnimator>();
            Assert.IsNotNull(cape);
            Assert.IsTrue(_player.IsGrounded);
            int last = CapeSim.Segments - 1;
            float startY = _player.transform.position.y;

            Press(_keyboard.spaceKey);
            yield return null;
            Release(_keyboard.spaceKey);
            float lowest = 99f, highest = -99f, peak = startY;
            int worstClip = 0;
            float end = Time.time + 1.3f;
            while (Time.time < end)
            {
                lowest = Mathf.Min(lowest, cape.Lift(last));
                highest = Mathf.Max(highest, cape.Lift(last));
                peak = Mathf.Max(peak, _player.transform.position.y);
                worstClip = Mathf.Max(worstClip, CapeClipping.PenetratingPoints(_player.transform));
                yield return null;
            }
            Assert.Greater(peak, startY + 0.6f, "el hidalgo debe haber saltado de verdad");
            Assert.GreaterOrEqual(lowest, CapeSim.MinLift - 0.01f, "al subir la capa no se mete en el cuerpo");
            Assert.Greater(highest, 4f, "y al caer se abre hacia atrás");
            Assert.AreEqual(0, worstClip, "durante todo el salto el cuerpo no atraviesa la capa");
        }

        [UnityTest]
        public IEnumerator Characters_HaveTenTimesThePolygonsOfUnityPrimitives()
        {
            yield return LoadScene();
            foreach (var (who, root) in new[] { ("hidalgo", _player.transform), ("guardia", _guard.transform) })
            {
                int ours = 0, original = 0, others = 0;
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
                {
                    Mesh mesh = filter.sharedMesh;
                    Assert.IsNotNull(mesh, $"{who}: {filter.name} sin malla");
                    if (!mesh.name.StartsWith("Qazrix_")) { others++; continue; } // marcas sueltas (señal del guardia)
                    string primitive = mesh.name.Substring("Qazrix_".Length);
                    Mesh builtIn = Resources.GetBuiltinResource<Mesh>(primitive + ".fbx");
                    Assert.IsNotNull(builtIn, $"{who}: sin primitiva de referencia para {mesh.name}");
                    ours += mesh.triangles.Length / 3;
                    original += builtIn.triangles.Length / 3;
                }
                Debug.Log($"[Qazrix] {who}: {original} → {ours} triángulos ({ours / (float)original:0.0}×), {others} piezas sin cambiar");
                Assert.Greater(original, 0, $"{who}: ninguna pieza con malla de alta resolución");
                Assert.GreaterOrEqual(ours, original * 10, $"{who}: debe tener al menos diez veces los triángulos ({ours} frente a {original})");
                Assert.LessOrEqual(others, 3, $"{who}: casi todas las piezas deben llevar la malla nueva");
            }
            yield return Capture("play_hidalgo_alta_resolucion.png");
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
