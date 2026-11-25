using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Qazrix.AI;
using Qazrix.Prototype;
using Qazrix.Simulation.AI;
using Qazrix.Simulation.Movement;
using Qazrix.Simulation.World;
using Qazrix.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Qazrix.Tests.PlayMode
{
    /// <summary>
    /// Escena Z01_Relieve: relieve real, volumetría del casco antiguo de Cáceres y zona de juego de
    /// prueba en la Plaza Mayor (guardia, murete y plataforma).
    /// </summary>
    public sealed class Z01SceneTests : InputTestFixture
    {
        private const string ScenePath = "Assets/_Qazrix/Scenes/Z01/Z01_Relieve.unity";

        private Keyboard _keyboard;
        private PrototypePlayerController _player;
        private PlayerParkour _parkour;
        private GuardController _guard;
        private CityBlocks _city;
        private CityBlocks _stairs;

        private IEnumerator LoadScene()
        {
            _keyboard = InputSystem.AddDevice<Keyboard>();
            SceneManager.LoadScene(ScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            _player = Object.FindFirstObjectByType<PrototypePlayerController>();
            _parkour = Object.FindFirstObjectByType<PlayerParkour>();
            _guard = Object.FindFirstObjectByType<GuardController>();
            // Hay dos CityBlocks: los edificios («Ciudad») y los peldaños de la plaza («Escalinata»).
            GameObject cityObject = GameObject.Find("Ciudad"), stairsObject = GameObject.Find("Escalinata");
            _city = cityObject != null ? cityObject.GetComponent<CityBlocks>() : null;
            _stairs = stairsObject != null ? stairsObject.GetComponent<CityBlocks>() : null;
            Assert.IsNotNull(_player, "la escena no tiene jugador");
            Assert.IsNotNull(_parkour, "el jugador no tiene PlayerParkour");
            Assert.IsNotNull(_guard, "la escena no tiene guardia");
            Assert.IsNotNull(_city, "la escena no tiene ciudad");
            for (int i = 0; i < 40; i++) yield return null;
        }

        /// <summary>
        /// Fachada de un edificio de la ciudad al norte del punto dado, mirando al sur, y de al menos 9 m de alto:
        /// con una más baja el jugador se encarama al tejado antes de que la prueba lo suelte. Al cambiar la forma
        /// de las esquinas cambia qué fachada se encuentra primero, así que se pide la altura en vez de fiarse.
        /// </summary>
        private bool FindTallFacade(Vector3 from, out RaycastHit wall)
        {
            for (float dx = -20f; dx <= 20f; dx += 2f)
            {
                var origin = new Vector3(from.x + dx, from.y + 1.3f, from.z);
                if (!Physics.Raycast(origin, Vector3.forward, out wall, 150f)) continue;
                if (wall.collider.GetComponentInParent<CityBlocks>() != _city || wall.normal.z >= -0.9f) continue;
                Vector3 above = wall.point + Vector3.forward * 0.5f + Vector3.up * 80f;
                if (Physics.Raycast(above, Vector3.down, out RaycastHit roof, 160f) && roof.point.y > from.y + 9f) return true;
            }
            wall = default;
            return false;
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
        public IEnumerator City_IsBuilt_AndThePlayerStartsInTheOpen()
        {
            yield return LoadScene();
            Debug.Log($"[Qazrix] Ciudad en Play: {_city.ChunkCount} trozos, {_city.TriangleCount} triángulos, {_city.BuildSeconds:0.00} s");

            Assert.Greater(_city.BuiltCells, 100000, "el casco antiguo ocupa más de 10 hectáreas construidas");
            Assert.Greater(_city.ChunkCount, 20);
            Assert.Greater(_city.TriangleCount, 20000, "los edificios son polígonos: muchos menos triángulos que por celdas");
            Assert.Less(_city.BuildSeconds, 10f, "levantar la ciudad no debe tardar más de 10 s");

            Vector3 p = _player.transform.position;
            Assert.IsTrue(_player.IsGrounded, "el jugador debe quedar apoyado en el suelo");
            Assert.IsTrue(_city.IsClear(p.x, p.z, 8f), "y nacer en un claro, no dentro ni pegado a un edificio");
            yield return Capture("play_z01_ciudad.png");
        }

        [UnityTest]
        public IEnumerator RandomLetter_IsInThePlazaMayor_Clear_AndReachableOnFoot()
        {
            yield return LoadScene();
            GameObject go = GameObject.Find("Carta_Azar");
            Assert.IsNotNull(go, "la escena tiene la carta al azar");
            var placer = go.GetComponent<Qazrix.Gameplay.RandomLetterPlacer>();
            Assert.IsNotNull(placer);
            Assert.IsTrue(placer.Placed.HasValue, "se colocó al empezar");

            Vector3 spot = placer.Placed.Value, p = _player.transform.position;
            float flat = Vector2.Distance(new Vector2(spot.x, spot.z), new Vector2(p.x, p.z));
            Debug.Log($"[Qazrix] Carta al azar a {flat:0.0} m del jugador");
            Assert.GreaterOrEqual(flat, 5.5f, "no pegada al jugador");
            Assert.LessOrEqual(flat, 28.5f, "dentro de la Plaza Mayor");
            Assert.IsTrue(_city.IsClear(spot.x, spot.z, 1.2f), "no está dentro de un edificio");

            var path = new UnityEngine.AI.NavMeshPath();
            Assert.IsTrue(UnityEngine.AI.NavMesh.CalculatePath(p, spot, UnityEngine.AI.NavMesh.AllAreas, path));
            Assert.AreEqual(UnityEngine.AI.NavMeshPathStatus.PathComplete, path.status, "se puede llegar andando");
            yield return CaptureLookingAt(spot, "play_z01_carta_plaza.png");
        }

        /// <summary>Foto de un punto desde unos 5 m, a la altura de la cabeza de un jugador que se acerca (cámara temporal).</summary>
        private IEnumerator CaptureLookingAt(Vector3 target, string fileName, float distance = 3.5f, float height = 1.8f, float fov = 60f)
        {
            yield return new WaitForEndOfFrame();
            var go = new GameObject("CamaraCarta");
            var cam = go.AddComponent<Camera>();
            Vector3 toward = new Vector3(target.x - _player.transform.position.x, 0f, target.z - _player.transform.position.z).normalized;
            go.transform.position = target - toward * distance + Vector3.up * height;
            go.transform.LookAt(target);
            cam.fieldOfView = fov;
            var rt = new RenderTexture(960, 540, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            Directory.CreateDirectory("Reports/QA");
            File.WriteAllBytes("Reports/QA/" + fileName, tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
            Object.Destroy(go);
        }

        /// <summary>Foto desde un lado del jugador (una cámara temporal: desde detrás el cuerpo tapa las piernas).</summary>
        private IEnumerator CaptureFromSide(string fileName)
        {
            yield return new WaitForEndOfFrame();
            var go = new GameObject("CamaraLateral");
            var cam = go.AddComponent<Camera>();
            Vector3 p = _player.transform.position;
            Vector3 side = _player.transform.right;
            go.transform.position = p + side * 3.2f + Vector3.up * 0.9f;
            go.transform.LookAt(p + Vector3.up * 0.75f);
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.1f;
            var rt = new RenderTexture(640, 640, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(640, 640, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            Directory.CreateDirectory("Reports/QA");
            File.WriteAllBytes("Reports/QA/" + fileName, tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
            Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator StandingOnTheRandomLetter_CrouchesToPickItUp_ItVanishes_AndGoesToTheInventory()
        {
            yield return LoadScene();
            GameObject go = GameObject.Find("Carta_Azar");
            var letter = go.GetComponent<Qazrix.Gameplay.LetterPickup>();
            var placer = go.GetComponent<Qazrix.Gameplay.RandomLetterPlacer>();
            var interactor = _player.GetComponent<Qazrix.Gameplay.PlayerInteractor>();
            var animator = _player.GetComponentInChildren<CharacterWalkAnimator>();
            Assert.IsTrue(placer.Placed.HasValue);
            Assert.IsEmpty(interactor.Inventory.Entries, "el inventario empieza vacío");
            Assert.IsFalse(letter.Collected);

            // Se coloca al jugador sobre la carta y se le deja quieto.
            var body = _player.GetComponent<CharacterController>();
            Vector3 spot = placer.Placed.Value;
            body.enabled = false;
            _player.transform.SetPositionAndRotation(new Vector3(spot.x, spot.y - 0.2f, spot.z), Quaternion.identity);
            body.enabled = true;

            float limit = Time.time + 3f;
            while (Time.time < limit && !interactor.IsPickingUp) yield return null;
            Assert.IsTrue(interactor.IsPickingUp, "parado encima de la carta se agacha a recogerla");
            Assert.IsTrue(animator.IsPickingUp, "con la animación de agacharse");
            Assert.IsFalse(letter.Collected, "todavía no la ha cogido");

            // A mitad del gesto, abajo del todo: foto de la pose; ahí la carta pasa al inventario.
            yield return new WaitForSeconds(Qazrix.Simulation.Movement.PickupCycle.CollectAt - 0.05f);
            yield return CaptureFromSide("play_z01_recoger_carta.png");
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(letter.Collected, "la carta se coge a mitad del gesto");
            Assert.IsFalse(letter.GetComponent<Renderer>().enabled, "desaparece de la escena");
            Assert.AreEqual(1, interactor.Inventory.CountOf("carta"), "y aparece en el inventario");

            yield return new WaitForSeconds(Qazrix.Simulation.Movement.PickupCycle.Duration);
            Assert.IsFalse(interactor.IsPickingUp, "después se levanta");
            Assert.IsFalse(animator.IsPickingUp);
            Assert.AreEqual(1, interactor.ItemsCollected);
        }

        [UnityTest]
        public IEnumerator TheMap_MarksAnObjectiveFarFromThePlaza_OnlyAfterPickingUpTheLetter()
        {
            yield return LoadScene();
            var panel = Object.FindFirstObjectByType<PrototypeHud>().GetComponent<MapPanel>();
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsFalse(panel.ObjectiveRevealed, "antes de recoger la carta no se enseña ninguna X");
            var letter = GameObject.Find("Carta_Azar").GetComponent<Qazrix.Gameplay.LetterPickup>();
            _player.GetComponent<Qazrix.Gameplay.PlayerInteractor>().BeginPickup(letter);
            yield return new WaitForSeconds(Qazrix.Simulation.Movement.PickupCycle.Duration + 0.3f);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsTrue(panel.ObjectiveRevealed, "tras recogerla, la X aparece");
            Vector3 o = panel.Objective.Value, p = _player.transform.position;
            Assert.Greater(Vector2.Distance(new Vector2(o.x, o.z), new Vector2(p.x, p.z)), 100f, "lejos de la plaza");
            Assert.IsTrue(_city.IsClear(o.x, o.z, 2f), "no dentro de un edificio");
            Assert.IsFalse(_city.IsClear(o.x, o.z, 18f), "en el casco antiguo, con edificios alrededor");

            var contact = Object.FindFirstObjectByType<Qazrix.Gameplay.ObjectiveContact>(FindObjectsInactive.Include);
            Assert.IsNotNull(contact, "la escena tiene a la mujer del velo");
            Assert.IsTrue(contact.gameObject.activeInHierarchy, "aparece al salir la X");
            Assert.Less(Vector2.Distance(new Vector2(contact.transform.position.x, contact.transform.position.z), new Vector2(o.x, o.z)), 0.5f, "justo en la X");
            yield return CaptureLookingAt(contact.transform.position + Vector3.up * 1.1f, "play_z01_contacto.png");
            yield return CaptureLookingAt(contact.transform.position + Vector3.up * 0.95f, "play_z01_contacto_cerca.png", 2.6f, 1.1f, 45f);
        }

        [UnityTest]
        public IEnumerator TheHero_HasThreeLives_EachDeathCostsOne_AndTheLastRestartsTheRun()
        {
            yield return LoadScene();
            var hud = Object.FindFirstObjectByType<PrototypeHud>();
            Assert.AreEqual(3, _player.Lives.Lives, "empieza con tres vidas");
            Assert.AreEqual(2, _player.Lives.ExtraLives, "la que juega y dos extra");

            // Dos cabezas pequeñas arriba a la derecha, alineadas a la derecha y sin solaparse.
            Rect first = PrototypeHud.LifeIconRect(0, 1920f), second = PrototypeHud.LifeIconRect(1, 1920f);
            Assert.AreEqual(1920f - PrototypeHud.LifeIconMargin, first.xMax, 0.01f);
            Assert.AreEqual(PrototypeHud.LifeIconMargin, first.y, 0.01f);
            Assert.LessOrEqual(second.xMax, first.xMin, "una al lado de la otra");
            Assert.IsNotNull(Resources.Load<Texture2D>(PrototypeHud.LifeIconResource), "el icono de vida está en Resources");
            Directory.CreateDirectory("Reports/QA");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("Reports/QA/play_vidas.png"); // las dos cabezas del HUD
            yield return null;
            yield return null;

            for (int death = 1; death <= 2; death++)
            {
                _player.Die(_player.transform.position + Vector3.forward * 2f);
                Assert.AreEqual(3 - death, _player.Lives.Lives, $"la muerte {death} gasta una vida");
                yield return new WaitForSeconds(Qazrix.Simulation.Movement.DeathCycle.TotalSeconds + 0.6f);
                Assert.IsFalse(_player.IsDead, "tras la muerte reaparece mientras queden vidas");
            }
            Assert.AreEqual(1, _player.Lives.Lives);
            Assert.AreEqual(0, _player.Lives.ExtraLives, "ya no queda ninguna de reserva");

            // La tercera muerte acaba la partida: la escena vuelve a empezar con las tres vidas.
            _player.Die(_player.transform.position + Vector3.forward * 2f);
            Assert.IsTrue(_player.Lives.IsGameOver);
            Assert.AreEqual(-1f, _player.GameOverSeconds, "hasta que termina el gesto de caer no sale la pantalla");

            // Cuando acaba de caer, el juego se oscurece y sale el mensaje.
            yield return new WaitForSeconds(Qazrix.Simulation.Movement.DeathCycle.TotalSeconds + Qazrix.Simulation.Gameplay.GameOverScreen.FadeSeconds + 0.5f);
            Assert.GreaterOrEqual(_player.GameOverSeconds, Qazrix.Simulation.Gameplay.GameOverScreen.FadeSeconds, "pantalla de fin de partida en marcha");
            Assert.IsTrue(_player.IsDead, "sigue sin mandos mientras se lee el mensaje");
            Assert.Greater(Qazrix.Simulation.Gameplay.GameOverScreen.Darkness(_player.GameOverSeconds), 0.9f, "todo oscuro");
            Directory.CreateDirectory("Reports/QA");
            ScreenCapture.CaptureScreenshot("Reports/QA/play_fin_de_partida.png"); // con el HUD (IMGUI) encima
            yield return null;
            yield return null;

            yield return new WaitForSeconds(Qazrix.Simulation.Gameplay.GameOverScreen.TotalSeconds);
            for (int i = 0; i < 10; i++) yield return null;
            var fresh = Object.FindFirstObjectByType<PrototypePlayerController>();
            Assert.IsNotNull(fresh);
            Assert.AreEqual(3, fresh.Lives.Lives, "partida nueva con las tres vidas");
            Assert.IsFalse(fresh.IsDead);
        }

        /// <summary>
        /// Media de un ángulo de la ropa durante un ciclo completo del velo. El viento y los vaivenes dependen del reloj (y el de
        /// Zahra empieza en un punto al azar): comparar un solo fotograma hacía que el resultado dependiera del momento.
        /// </summary>
        private static IEnumerator AverageOverWindCycle(System.Func<float> angle, System.Action<float> result)
        {
            float sum = 0f, time = 0f;
            while (time < Qazrix.Simulation.Movement.ContactIdle.VeilPeriod)
            {
                yield return null;
                sum += angle() * Time.deltaTime;
                time += Time.deltaTime;
            }
            result(sum / time);
        }

        [UnityTest]
        public IEnumerator TheCape_ReactsToTheWestWind_FacingIntoItLiftsMoreThanWithItAtTheBack()
        {
            yield return LoadScene();
            var cape = _player.GetComponentInChildren<CapeAnimator>();
            Assert.IsNotNull(cape, "el hidalgo tiene capa");
            int last = Qazrix.Simulation.Movement.CapeSim.Segments - 1;
            // Sin guardias: durante la medida podrían alcanzarle y recolocarle.
            foreach (GuardController guard in Object.FindObjectsByType<GuardController>(FindObjectsSortMode.None))
                guard.gameObject.SetActive(false);

            float headwind = 0f, tailwind = 0f;
            _player.transform.rotation = Quaternion.Euler(0f, 270f, 0f); // mirando al oeste: el viento sopla de cara
            yield return new WaitForSeconds(2.5f);
            yield return AverageOverWindCycle(() => cape.Lift(last), mean => headwind = mean);
            _player.transform.rotation = Quaternion.Euler(0f, 90f, 0f); // mirando al este: el viento por la espalda
            yield return new WaitForSeconds(2.5f);
            yield return AverageOverWindCycle(() => cape.Lift(last), mean => tailwind = mean);
            Assert.Greater(headwind, tailwind + 1.5f, $"de cara se levanta más ({headwind:0.0}° contra {tailwind:0.0}° de media)");
        }

        [UnityTest]
        public IEnumerator TheVeiledWomansClothes_ReactToTheWindAndToInertia()
        {
            yield return LoadScene();
            var letter = GameObject.Find("Carta_Azar").GetComponent<Qazrix.Gameplay.LetterPickup>();
            _player.GetComponent<Qazrix.Gameplay.PlayerInteractor>().BeginPickup(letter);
            yield return new WaitForSeconds(Qazrix.Simulation.Movement.PickupCycle.Duration + 0.3f);
            for (int i = 0; i < 5; i++) yield return null;

            var contact = Object.FindFirstObjectByType<Qazrix.Gameplay.ObjectiveContact>();
            Transform veil = contact.transform.Find("Contacto_modelo/Torso_pivote/Velo_pivote");
            Transform skirt = contact.transform.Find("Contacto_modelo/Falda_pivote");
            Transform drape = contact.transform.Find("Contacto_modelo/Torso_pivote/Cuello_pivote/Gasa_pivote");
            Assert.IsNotNull(veil, "pivote del velo");
            Assert.IsNotNull(skirt, "pivote del bajo de la túnica");
            Assert.IsNotNull(drape, "pivote de la gasa del rostro");
            Assert.IsNotNull(skirt.Find("Falda_baja"), "el bajo cuelga de su pivote");

            // Viento: de cara (mirando al oeste) el velo se levanta más que con el viento a favor (mirando al este).
            float VeilAngle() => Mathf.DeltaAngle(0f, veil.localEulerAngles.x);
            float headwind = 0f, tailwind = 0f;
            contact.transform.rotation = Quaternion.Euler(0f, 270f, 0f);
            yield return new WaitForSeconds(2.5f);
            yield return AverageOverWindCycle(VeilAngle, mean => headwind = mean);
            contact.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            yield return new WaitForSeconds(2.5f);
            yield return AverageOverWindCycle(VeilAngle, mean => tailwind = mean);
            Assert.Greater(headwind, tailwind + 2f, $"el velo se levanta con el viento de cara ({headwind:0.0}° contra {tailwind:0.0}° de media)");

            // Inercia: si la mueven hacia delante con aceleración, el velo se queda atrás (se levanta más).
            Vector3 start = contact.transform.position;
            float rest = Mathf.DeltaAngle(0f, veil.localEulerAngles.x);
            float peak = rest;
            float speed = 0f;
            // Un tirón fuerte (20 m/s²): su efecto (unos 14°) destaca sobre el vaivén propio del velo, que solo no llega a 7°.
            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                speed += 20f * Time.deltaTime;
                contact.transform.position += contact.transform.forward * (speed * Time.deltaTime);
                yield return null;
                peak = Mathf.Max(peak, Mathf.DeltaAngle(0f, veil.localEulerAngles.x));
            }
            Assert.Greater(peak, rest + 7f, $"al acelerar, la tela se queda atrás ({rest:0.0}° → {peak:0.0}°)");
            contact.transform.position = start;
        }

        [UnityTest]
        public IEnumerator TouchingZahra_SheGivesTheKey_HeTakesItAndTucksItInHisShirt()
        {
            yield return LoadScene();
            var interactor = _player.GetComponent<Qazrix.Gameplay.PlayerInteractor>();
            var letter = GameObject.Find("Carta_Azar").GetComponent<Qazrix.Gameplay.LetterPickup>();
            interactor.BeginPickup(letter);
            yield return new WaitForSeconds(Qazrix.Simulation.Movement.PickupCycle.Duration + 0.3f);
            for (int i = 0; i < 5; i++) yield return null;

            var contact = Object.FindFirstObjectByType<Qazrix.Gameplay.ObjectiveContact>();
            var handover = contact.GetComponent<Qazrix.Gameplay.KeyHandover>();
            var hero = _player.GetComponentInChildren<CharacterWalkAnimator>();
            Transform zahraArm = contact.transform.Find("Contacto_modelo/Torso_pivote/Brazo_der");
            Assert.IsNotNull(handover, "Zahra sabe dar la llave");
            Assert.IsNotNull(zahraArm);
            Assert.IsNotNull(hero.RightHand, "el hidalgo tiene mano derecha");
            Assert.IsFalse(handover.Started, "aún no la ha tocado");
            Assert.AreEqual(0, interactor.Inventory.CountOf(Qazrix.Gameplay.KeyHandover.KeyItemId), "sin llave todavía");

            // La toca: se coloca a 0,9 m de ella.
            Vector3 woman = contact.transform.position;
            _player.transform.position = woman + new Vector3(0.9f, 0.1f, 0f);
            Physics.SyncTransforms();
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsTrue(handover.Started, "al tocarla empieza la entrega");
            Assert.IsNotNull(GameObject.Find("Llave"), "ella tiene la llave en la mano");

            // Ofrece: con el brazo extendido y la llave, antes de que él la tome.
            yield return new WaitForSeconds(0.85f - handover.Seconds);
            float zahraArmX = Mathf.DeltaAngle(0f, zahraArm.localEulerAngles.x);
            Assert.Less(zahraArmX, -60f, $"Zahra extiende el brazo ({zahraArmX:0}°)");
            Directory.CreateDirectory("Reports/QA");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("Reports/QA/play_llave_ofrece.png");
            yield return null;

            // Él alarga la mano y la llave pasa de una a otra.
            yield return new WaitForSeconds(1.15f - handover.Seconds);
            Assert.Greater(hero.RightArmAngle, 40f, $"el protagonista alarga el brazo ({hero.RightArmAngle:0}°)");
            Vector3 keyAt = GameObject.Find("Llave").transform.position;
            Assert.Less(Vector3.Distance(keyAt, hero.RightHand.position), 0.25f, "la llave ya llega a su mano");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("Reports/QA/play_llave_toma.png");
            yield return null;

            // La lleva al pecho (codo muy doblado) y se ve a punto de guardarla.
            yield return new WaitForSeconds(1.95f - handover.Seconds);
            Assert.IsNotNull(GameObject.Find("Llave"), "todavía a la vista, en su mano");
            Assert.AreEqual(0, interactor.Inventory.CountOf(Qazrix.Gameplay.KeyHandover.KeyItemId), "aún no está guardada");
            Assert.Greater(hero.ElbowAngle + 0f, -1f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("Reports/QA/play_llave_pecho.png");
            yield return null;

            // Guardada en la camisa: desaparece de la mano y entra en el inventario; todo vuelve a su sitio.
            yield return new WaitForSeconds(Qazrix.Simulation.Movement.KeyHandoverCycle.TotalSeconds + 0.3f - handover.Seconds);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsTrue(handover.Delivered);
            Assert.IsNull(GameObject.Find("Llave"), "la llave ya está dentro de la camisa");
            Assert.AreEqual(1, interactor.Inventory.CountOf(Qazrix.Gameplay.KeyHandover.KeyItemId), "la llave está en el inventario");

            // Al coger la llave aparece una X en el mapa, sobre el antiguo alcázar (Palacio de las Veletas).
            var map = Object.FindFirstObjectByType<PrototypeHud>().GetComponent<MapPanel>();
            Vector2 alcazar = Qazrix.Gameplay.MapPoints.OldAlcazar;
            Assert.IsTrue(map.HasMarkAt(new Vector3(alcazar.x, 0f, alcazar.y)), "X en el antiguo alcázar");
            Assert.IsTrue(_city.HasBuildingAt(alcazar.x, alcazar.y) || _city.HasBuildingAt(alcazar.x + 6f, alcazar.y - 6f), "el punto cae sobre el casco, junto a un edificio");
            Qazrix.Simulation.Gameplay.InventoryEntry keyEntry = interactor.Inventory.Entries[interactor.Inventory.Entries.Count - 1];
            Assert.AreEqual(Qazrix.Gameplay.KeyHandover.KeyItemId, keyEntry.Id);
            Assert.IsTrue(keyEntry.HasMark, "la llave señala un punto del mapa");
            Assert.AreEqual(alcazar.x, keyEntry.MarkX, 0.01f);
            Assert.AreEqual(alcazar.y, keyEntry.MarkZ, 0.01f);
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(0f, zahraArm.localEulerAngles.x)), 5f, "Zahra baja el brazo");
            Assert.Less(Mathf.Abs(hero.RightArmAngle), 5f, "el protagonista baja el brazo");
            Assert.IsFalse(hero.IsReceivingKey);
        }

        [UnityTest]
        public IEnumerator TheCityWalls_AreSolid_CannotBeClimbed_AndHaveAnInvisibleBarrierOnTop()
        {
            yield return LoadScene();
            Assert.AreEqual(Z01GeoBuilder_WallIds.Length, _city.WallPartCount, "las murallas del plano están levantadas");
            GameObject wallRoot = GameObject.Find("Ciudad_Muralla");
            Assert.IsNotNull(wallRoot, "la muralla va aparte del resto de edificios");
            Assert.IsNotNull(wallRoot.GetComponent<UnbreachableWall>(), "marcada como infranqueable");
            Assert.IsNotNull(wallRoot.GetComponentInChildren<MeshCollider>(), "sólida");

            Transform barrier = wallRoot.transform.Find("Muralla_Barrera");
            Assert.IsNotNull(barrier, "tiene barrera invisible");
            Assert.IsNull(barrier.GetComponent<MeshRenderer>(), "invisible");

            // Cada muralla tiene la barrera a su altura: un rayo horizontal diez metros por encima de su tejado la toca.
            foreach (int id in Z01GeoBuilder_WallIds)
            {
                BuildingPart wall = _city.Parts[id];
                var centre = new Vector3((float)wall.Centre.X, wall.RoofHeight + 10f, (float)wall.Centre.Z);
                bool hitBarrier = false;
                foreach (Vector3 dir in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                {
                    RaycastHit[] hits = Physics.RaycastAll(centre - dir * 120f, dir, 240f, ~0, QueryTriggerInteraction.Ignore);
                    foreach (RaycastHit h in hits) if (h.collider.name == "Muralla_Barrera") hitBarrier = true;
                }
                Assert.IsTrue(hitBarrier, $"la barrera cubre la muralla {id}");
            }

            // Ninguna pared de la muralla se puede escalar; las de los demás edificios, sí.
            var climb = _player.GetComponent<PlayerWallClimb>();
            Assert.IsNotNull(climb);
            var isClimbable = typeof(PlayerWallClimb).GetMethod("IsClimbable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            foreach (MeshCollider c in wallRoot.GetComponentsInChildren<MeshCollider>())
                Assert.IsFalse((bool)isClimbable.Invoke(null, new object[] { c }), $"{c.name} no se escala");
            MeshCollider ordinary = _city.GetComponentsInChildren<MeshCollider>().First(c => c.GetComponentInParent<UnbreachableWall>() == null);
            Assert.IsTrue((bool)isClimbable.Invoke(null, new object[] { ordinary }), "los edificios normales siguen siendo escalables");
        }

        private static readonly int[] Z01GeoBuilder_WallIds = { 148, 170, 315, 344 };

        [UnityTest]
        public IEnumerator TheAlcazarDoor_OpensOnlyWithTheKey()
        {
            yield return LoadScene();
            Assert.AreEqual(1, _city.Doors.Count, "hay una puerta en el antiguo alcázar");
            DoorPlacement placement = _city.Doors[0];
            Vector2 alcazar = Qazrix.Gameplay.MapPoints.OldAlcazar;
            Assert.Less(Vector2.Distance(new Vector2((float)placement.X, (float)placement.Z), alcazar), 40f, "cerca de la X del alcázar");

            var door = _city.GetComponentInChildren<Qazrix.Gameplay.DoorPortal>();
            Assert.IsNotNull(door, "la puerta es interactuable");
            var interactor = _player.GetComponent<Qazrix.Gameplay.PlayerInteractor>();
            for (int i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(Qazrix.Gameplay.DoorPortal.PromptWithoutKey, door.Prompt, "sin llave avisa de que está cerrada");
            Assert.IsTrue(door.CanInteract);

            // Cerrada: un rayo a la altura del pecho por el hueco choca con la hoja; sobre el dintel, con la pared.
            var normal = new Vector3((float)placement.NormalX, 0f, (float)placement.NormalZ);
            Vector3 threshold = new Vector3((float)placement.X, (float)placement.GroundY, (float)placement.Z);
            bool Blocked(float height) => Physics.Raycast(threshold + normal * 2f + Vector3.up * height, -normal, 2.6f, ~0, QueryTriggerInteraction.Ignore);
            Assert.IsTrue(Blocked(1.2f), "cerrada, la hoja tapa el hueco");
            Assert.IsTrue(Blocked((float)placement.Height + 0.6f), "sobre el dintel hay pared");

            // Sin la llave no se abre por mucho que se intente.
            door.Interact(interactor);
            yield return new WaitForSeconds(0.3f);
            Assert.IsFalse(door.Opened, "sin llave no se abre");

            // Con la llave se abre, gira y deja pasar.
            interactor.Inventory.Add(Qazrix.Gameplay.KeyHandover.KeyItemId, "Llave");
            for (int i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(Qazrix.Gameplay.DoorPortal.PromptWithKey, door.Prompt, "con llave ofrece abrirla");
            door.Interact(interactor);
            Assert.IsTrue(door.Opened);
            yield return new WaitForSeconds(Qazrix.Gameplay.DoorPortal.OpenSeconds + 0.3f);
            Assert.AreEqual(Qazrix.Gameplay.DoorPortal.OpenDegrees, door.Angle, 0.5f, "la hoja ha girado del todo");
            Assert.IsFalse(Blocked(1.2f), "abierta, se puede pasar por el hueco");
            Assert.IsTrue(Blocked((float)placement.Height + 0.6f), "el dintel sigue ahí");
            Assert.IsFalse(door.CanInteract, "una vez abierta ya no hay nada que pulsar");
        }

        [UnityTest]
        public IEnumerator TheAlcazarPassage_RunsUnderTheWall_AndLeadsToTheTreasureOrchard()
        {
            yield return LoadScene();
            var tunnel = Object.FindFirstObjectByType<Qazrix.Gameplay.PassageTunnel>();
            Assert.IsNotNull(tunnel, "la escena tiene el pasadizo");
            for (int i = 0; i < 20 && !tunnel.Built; i++) yield return null;
            Assert.IsTrue(tunnel.Built, "se construye al empezar");

            // Del alcázar a la huerta, pasando bajo la muralla 170 (más de 500 m).
            Vector2 orchard = Qazrix.Gameplay.MapPoints.TreasureOrchard;
            Vector2 alcazar = Qazrix.Gameplay.MapPoints.OldAlcazar;
            Assert.AreEqual(3, tunnel.Route.Count, "alcázar, bajo la muralla y huerta");
            Assert.Less(Vector2.Distance(tunnel.Route[0], alcazar), 12f, "empieza tras la puerta del alcázar");
            BuildingPart wall = _city.Parts[Z01GeoBuilder_PassageWall];
            Assert.Less(Vector2.Distance(tunnel.Route[1], new Vector2((float)wall.Centre.X, (float)wall.Centre.Z)), 0.01f, "pasa por debajo de la muralla");
            Assert.Less(Vector2.Distance(tunnel.Route[2], orchard), 0.01f, "acaba en la huerta");
            Assert.Greater(tunnel.Length, 500f, "es un pasadizo largo de verdad");

            GameObject tube = GameObject.Find("Pasadizo_Tunel");
            Assert.IsNotNull(tube.GetComponent<MeshCollider>(), "el tubo es sólido");
            Assert.Less(tube.GetComponent<MeshRenderer>().bounds.max.y, Qazrix.Gameplay.PassageTunnel.FloorY + 4f, "va muy por debajo del relieve");

            var interactor = _player.GetComponent<Qazrix.Gameplay.PlayerInteractor>();
            Qazrix.Gameplay.PassageStairs Find(string name) => GameObject.Find(name).GetComponent<Qazrix.Gameplay.PassageStairs>();

            // Baja por la trampilla del alcázar: fundido y aparece al principio del tubo.
            Find("Trampilla_alcazar").Interact(interactor);
            yield return new WaitForSeconds(Qazrix.Gameplay.ScreenFade.OutSeconds + Qazrix.Gameplay.ScreenFade.HoldSeconds + Qazrix.Gameplay.ScreenFade.InSeconds + 0.4f);
            Assert.Less(_player.transform.position.y, Qazrix.Gameplay.PassageTunnel.FloorY + 2f, "ya está bajo tierra");
            Assert.IsTrue(_player.IsGrounded, "pisa el suelo del pasadizo");
            Assert.Greater(_player.transform.position.y, Qazrix.Gameplay.PassageTunnel.FloorY - 0.6f, "sin atravesar el suelo");
            Directory.CreateDirectory("Reports/QA");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("Reports/QA/play_pasadizo_inicio.png");
            yield return null;
            yield return null;

            // El tubo aguanta al jugador en toda su longitud: a lo largo del eje, en la curva bajo la muralla y al final.
            Vector2 a = tunnel.Route[0], b = tunnel.Route[1], c = tunnel.Route[2];
            foreach (Vector2 p in new[] { Vector2.Lerp(a, b, 0.5f), b + (a - b).normalized * 2.5f, b + (c - b).normalized * 2.5f, Vector2.Lerp(b, c, 0.5f), c - (c - b).normalized * 3f })
            {
                _player.TeleportTo(new Vector3(p.x, Qazrix.Gameplay.PassageTunnel.FloorY + 0.3f, p.y), Quaternion.identity);
                yield return new WaitForSeconds(0.6f);
                Assert.IsTrue(_player.IsGrounded, $"pisa el suelo en ({p.x:0}, {p.y:0})");
                Assert.Greater(_player.transform.position.y, Qazrix.Gameplay.PassageTunnel.FloorY - 0.6f, $"no atraviesa el suelo en ({p.x:0}, {p.y:0})");
                Assert.Less(_player.transform.position.y, Qazrix.Gameplay.PassageTunnel.FloorY + 1.2f, $"no sale del tubo en ({p.x:0}, {p.y:0})");
            }
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("Reports/QA/play_pasadizo_final.png");
            yield return null;
            yield return null;

            // Sube a la huerta por la escalera del final.
            Find("Escalera_huerta").Interact(interactor);
            yield return new WaitForSeconds(Qazrix.Gameplay.ScreenFade.OutSeconds + Qazrix.Gameplay.ScreenFade.HoldSeconds + Qazrix.Gameplay.ScreenFade.InSeconds + 0.5f);
            Vector3 at = _player.transform.position;
            Assert.Greater(at.y, Qazrix.Gameplay.PassageTunnel.FloorY + 30f, "de vuelta en la superficie");
            Assert.Less(Vector2.Distance(new Vector2(at.x, at.z), orchard), 6f, "en la huerta del tesoro");
            Assert.IsNotNull(GameObject.Find("Huerta_del_tesoro"), "la huerta tiene sus árboles y el cofre");
            // La guarnición espera a la salida: un capitán y 50 soldados con el emblema de San Jorge en el escudo.
            var garrison = tunnel.Garrison;
            Assert.IsNotNull(garrison, "hay guarnición a la salida");
            Assert.AreEqual(50, garrison.SoldierCount, "50 soldados");
            Assert.IsTrue(garrison.HasCaptain, "y un capitán");
            Assert.AreEqual(1, garrison.GetComponentsInChildren<Transform>().Count(t => t.name == Qazrix.Gameplay.OrchardGarrison.CaptainName));
            Assert.AreEqual(50, garrison.GetComponentsInChildren<Transform>().Count(t => t.name == Qazrix.Gameplay.OrchardGarrison.SoldierName));
            Transform captainT = garrison.GetComponentsInChildren<Transform>().First(t => t.name == Qazrix.Gameplay.OrchardGarrison.CaptainName);
            Transform soldierT = garrison.GetComponentsInChildren<Transform>().First(t => t.name == Qazrix.Gameplay.OrchardGarrison.SoldierName);
            Assert.Less(Vector3.Distance(captainT.position, at), Vector3.Distance(soldierT.position, at), "el capitán está más cerca de la salida");
            Assert.Greater(Vector3.Dot(captainT.forward, (at - captainT.position).normalized), 0.95f, "el capitán mira a quien sale");
            Assert.IsNotNull(captainT.GetComponent<CapsuleCollider>(), "son sólidos");
            // El capitán se nota: lleva muchos más adornos que un soldado (cresta y pluma en el yelmo, cruz en el pecho, gorguera,
            // hombreras, cinto con hebilla, capa con ribete dorado, escudo con borde y espada con empuñadura de oro).
            int captainParts = captainT.GetComponentsInChildren<MeshRenderer>().Length, soldierParts = soldierT.GetComponentsInChildren<MeshRenderer>().Length;
            Assert.GreaterOrEqual(captainParts, soldierParts + 12, $"el capitán tiene más piezas ({captainParts} contra {soldierParts})");
            foreach (string ornament in new[] { "Yelmo_cresta", "Penacho", "Penacho_pluma", "Cruz_vertical", "Cruz_horizontal", "Gola", "Hombrera", "Cinto_ancho",
                "Hebilla", "Capa", "Capa_ribete", "Escudo_borde", "Espada_guarda", "Espada_pomo" })
                Assert.IsTrue(captainT.GetComponentsInChildren<Transform>().Any(t => t.name == ornament), $"el capitán lleva «{ornament}»");
            Assert.IsFalse(soldierT.GetComponentsInChildren<Transform>().Any(t => t.name == "Penacho" || t.name == "Capa"), "los soldados no");
            Assert.Greater(captainT.Find("Modelo").localScale.y, soldierT.Find("Modelo").localScale.y, "y es algo más alto");
            Texture2D shield = garrison.ShieldTexture;
            Assert.IsNotNull(shield, "escudo con el emblema");
            Color centreColor = shield.GetPixel(shield.width / 2, shield.height / 2), corner = shield.GetPixel(shield.width / 4, shield.height / 4);
            Assert.Greater(centreColor.r, 0.6f); Assert.Less(centreColor.g, 0.2f); Assert.Less(centreColor.b, 0.2f); // el centro: cruz roja
            Assert.Greater(corner.r, 0.85f); Assert.Greater(corner.g, 0.85f); Assert.Greater(corner.b, 0.8f);        // un cuadrante: campo blanco
            var shieldRenderer = soldierT.GetComponentsInChildren<MeshRenderer>().First(r => r.name == "Escudo");
            Assert.AreSame(shield, shieldRenderer.sharedMaterial.GetTexture("_BaseMap"), "el escudo lleva la cruz de San Jorge");

            // Suelo a la salida: un manto de hierba que sigue el relieve, justo sobre el terreno bajo la trampilla y alrededor.
            GameObject floor = GameObject.Find("Huerta_suelo");
            Assert.IsNotNull(floor, "la huerta tiene su suelo");
            Bounds fb = floor.GetComponent<MeshRenderer>().bounds;
            Assert.IsTrue(fb.min.x < at.x && at.x < fb.max.x && fb.min.z < at.z && at.z < fb.max.z, "el suelo cubre la salida");
            Assert.GreaterOrEqual(fb.size.x, Qazrix.Gameplay.PassageTunnel.GroundSize - 0.1f);
            Mesh fm = floor.GetComponent<MeshFilter>().sharedMesh;
            foreach (Vector3 v in fm.vertices)
            {
                Assert.IsTrue(Physics.Raycast(new Vector3(v.x, v.y + 5f, v.z), Vector3.down, out RaycastHit under, 10f, ~0, QueryTriggerInteraction.Ignore));
                Assert.Less(Mathf.Abs(v.y - under.point.y - Qazrix.Gameplay.PassageTunnel.GroundLift), 0.5f, "pegado al relieve, sin flotar ni hundirse");
                break; // basta una muestra: el resto sigue el mismo cálculo
            }
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("Reports/QA/play_huerta.png");
            yield return null;
            yield return null;
        }

        private const int Z01GeoBuilder_PassageWall = 170;

        [UnityTest]
        public IEnumerator TouchingTheCaptain_HeGivesHimTheKey_AndHeTakesItAndPocketsIt()
        {
            yield return LoadScene();
            var tunnel = Object.FindFirstObjectByType<Qazrix.Gameplay.PassageTunnel>();
            for (int i = 0; i < 20 && !tunnel.Built; i++) yield return null;
            Assert.IsTrue(tunnel.Built);
            var interactor = _player.GetComponent<Qazrix.Gameplay.PlayerInteractor>();
            var hero = _player.GetComponentInChildren<CharacterWalkAnimator>();

            Transform captain = tunnel.Garrison.GetComponentsInChildren<Transform>().First(t => t.name == Qazrix.Gameplay.OrchardGarrison.CaptainName);
            var give = captain.GetComponent<Qazrix.Gameplay.CaptainKeyGive>();
            Transform captainArm = captain.Find("Modelo/Brazo_der_pivote");
            Assert.IsNotNull(give, "el capitán sabe recibir la llave");
            Assert.IsNotNull(captainArm, "con el brazo derecho articulado");

            // Sin la llave, tocarlo no hace nada.
            Vector3 front = captain.position + captain.forward * 0.9f;
            _player.TeleportTo(front + Vector3.up * 0.1f, Quaternion.identity);
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(give.Started, "sin llave no pasa nada");

            // Con la llave, al tocarlo se la da.
            interactor.Inventory.Add(Qazrix.Gameplay.KeyHandover.KeyItemId, "Llave");
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsTrue(give.Started, "al tocarlo, empieza a dársela");
            Assert.IsTrue(hero.IsGivingKey);
            Assert.AreEqual(1, interactor.Inventory.CountOf(Qazrix.Gameplay.KeyHandover.KeyItemId), "aún la lleva encima");

            // Saca la llave del pecho y alarga el brazo; el capitán extiende la mano.
            yield return new WaitForSeconds(1.0f - give.Seconds);
            Assert.Greater(hero.RightArmAngle, 40f, $"el protagonista alarga el brazo ({hero.RightArmAngle:0}°)");
            float captainArmX = Mathf.DeltaAngle(0f, captainArm.localEulerAngles.x);
            Assert.Less(captainArmX, -30f, $"el capitán extiende la mano ({captainArmX:0}°)");
            Assert.IsNotNull(GameObject.Find("Llave"), "la llave ya está fuera de la camisa");
            Directory.CreateDirectory("Reports/QA");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("Reports/QA/play_capitan_ofrece.png");
            yield return null;

            // La llave pasa a la mano del capitán y sale del inventario.
            yield return new WaitForSeconds(1.9f - give.Seconds);
            Assert.AreEqual(0, interactor.Inventory.CountOf(Qazrix.Gameplay.KeyHandover.KeyItemId), "ya no la lleva él");
            Assert.IsTrue(give.Delivered);
            Assert.IsNotNull(GameObject.Find("Llave"), "el capitán la tiene en la mano");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("Reports/QA/play_capitan_toma.png");
            yield return null;

            // Al recibir el capitán la llave, los soldados alzan las lanzas al descompás: no todos a la vez ni al mismo ritmo.
            var soldiers = tunnel.Garrison.Soldiers().ToArray();
            Assert.AreEqual(50, soldiers.Length, "50 soldados con lanza");
            Assert.IsTrue(tunnel.Garrison.SpearsRaised);
            yield return new WaitForSeconds(0.9f);
            float[] weights = soldiers.Select(s => s.SaluteWeight).ToArray();
            Assert.GreaterOrEqual(weights.Count(w => w >= 0.99f), 3, "algunos ya la tienen alzada");
            Assert.GreaterOrEqual(weights.Count(w => w > 0.01f && w < 0.99f), 2, "otros la están subiendo");
            Assert.GreaterOrEqual(weights.Select(w => Mathf.Round(w * 20f)).Distinct().Count(), 5, "cada uno va por un punto distinto: al descompás, no a la vez");
            Qazrix.Gameplay.SoldierIdle raised = soldiers.First(s => s.SaluteWeight >= 0.99f);
            Assert.Greater(raised.transform.Find("Modelo/Lanza_grupo").localPosition.y, 1.15f + SpearSalute.MaxLift * 0.9f, "la lanza sube");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("Reports/QA/play_lanzas_descompas.png");
            yield return null;

            // Se la guarda en el cinto y todo vuelve a su sitio.
            yield return new WaitForSeconds(Qazrix.Simulation.Movement.KeyGiveCycle.TotalSeconds + 0.3f - give.Seconds);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsNull(GameObject.Find("Llave"), "guardada en el cinto");
            Assert.IsFalse(hero.IsGivingKey);
            Assert.Less(Mathf.Abs(hero.RightArmAngle), 5f, "el protagonista baja el brazo");
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(10f, captainArm.localEulerAngles.x)), 5f, "el capitán baja el brazo");
        }

        [UnityTest]
        public IEnumerator TheEnding_FadesToBlack_ThenAParchmentIsHandwrittenLetterByLetter()
        {
            yield return LoadScene();
            Qazrix.Gameplay.EndingSequence.TypingSpeed = 6f; // para no esperar los 16 s reales
            var ending = Qazrix.Gameplay.EndingSequence.Instance;
            Assert.IsFalse(ending.Started);
            Assert.IsNotNull(Resources.Load<Font>(Qazrix.Gameplay.EndingSequence.FontResource), "la tipografía manuscrita (Caveat) está en el juego");
            Qazrix.Gameplay.EndingSequence.Trigger();
            yield return null;
            Assert.IsTrue(ending.Started);
            Assert.AreEqual(0, ending.VisibleChars, "aún no hay nada escrito");

            // Primero se funde a negro; el pergamino todavía no.
            yield return new WaitForSeconds(Qazrix.Gameplay.EndingSequence.FadeSeconds * 0.5f);
            Assert.Greater(ending.Darkness, 0.3f); Assert.Less(ending.Darkness, 1f);
            Assert.AreEqual(0f, ending.ParchmentAlpha, 1e-4f, "sin pergamino mientras oscurece");
            yield return new WaitForSeconds(Qazrix.Gameplay.EndingSequence.FadeSeconds * 0.6f);
            Assert.AreEqual(1f, ending.Darkness, 1e-3f, "todo negro");

            // Sale el pergamino y se empieza a escribir letra a letra.
            yield return new WaitForSeconds(Qazrix.Gameplay.EndingSequence.ParchmentSeconds + 0.1f);
            Assert.AreEqual(1f, ending.ParchmentAlpha, 1e-3f);
            int previous = ending.VisibleChars;
            yield return new WaitForSeconds(1.0f);
            Assert.Greater(ending.VisibleChars, previous, "se va escribiendo");
            Assert.Less(ending.VisibleChars, Qazrix.Simulation.Gameplay.EndingText.Message.Length, "aún no ha terminado");
            Directory.CreateDirectory("Reports/QA");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("Reports/QA/play_final_pergamino_escribiendo.png");
            yield return null;

            // Termina el texto completo y se ofrece volver a empezar.
            float remaining = Qazrix.Simulation.Gameplay.HandwritingReveal.TotalSeconds(Qazrix.Simulation.Gameplay.EndingText.Message) / 6f;
            yield return new WaitForSeconds(remaining + Qazrix.Gameplay.EndingSequence.HintDelay + 0.5f);
            Assert.IsTrue(ending.TextComplete, "texto completo");
            Assert.IsTrue(ending.WaitingForKey, "se ofrece volver a empezar");
            Assert.IsTrue(ending.TextFits, $"el texto entero cabe en el pergamino (letra de {ending.FontSizeUsed} px, altura {ending.TextHeightUsed:0} px)");
            Assert.GreaterOrEqual(ending.FontSizeUsed, 14, "y se lee");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("Reports/QA/play_final_pergamino.png");
            yield return null;
            Qazrix.Gameplay.EndingSequence.TypingSpeed = 1f;
        }

        [UnityTest]
        public IEnumerator TheVeiledWoman_WaitsWithAnIdleAnimation()
        {
            yield return LoadScene();
            var panel = Object.FindFirstObjectByType<PrototypeHud>().GetComponent<MapPanel>();
            for (int i = 0; i < 5; i++) yield return null;
            var letter = GameObject.Find("Carta_Azar").GetComponent<Qazrix.Gameplay.LetterPickup>();
            _player.GetComponent<Qazrix.Gameplay.PlayerInteractor>().BeginPickup(letter);
            yield return new WaitForSeconds(Qazrix.Simulation.Movement.PickupCycle.Duration + 0.3f);
            for (int i = 0; i < 5; i++) yield return null;

            var contact = Object.FindFirstObjectByType<Qazrix.Gameplay.ObjectiveContact>();
            Assert.IsNotNull(contact.GetComponent<Qazrix.Gameplay.ContactIdleAnimator>(), "tiene animación de espera");
            Transform head = contact.transform.Find("Contacto_modelo/Torso_pivote/Cuello_pivote");
            Transform torso = contact.transform.Find("Contacto_modelo/Torso_pivote");
            Assert.IsNotNull(head, "el cuello se ha montado como pivote");
            Assert.IsNotNull(torso);
            Assert.IsNotNull(head.Find("Cabeza"), "la cabeza cuelga del cuello");

            float maxHeadMove = 0f, minLift = float.MaxValue, maxLift = float.MinValue;
            Quaternion first = head.localRotation;
            float end = Time.time + 5f;
            while (Time.time < end)
            {
                yield return null;
                maxHeadMove = Mathf.Max(maxHeadMove, Quaternion.Angle(first, head.localRotation));
                minLift = Mathf.Min(minLift, torso.localPosition.y);
                maxLift = Mathf.Max(maxLift, torso.localPosition.y);
            }
            Assert.Greater(maxHeadMove, 1f, "mueve la cabeza");
            Assert.Greater(maxLift - minLift, 0.002f, "respira");
        }

        [UnityTest]
        public IEnumerator TouchingTheVeiledWoman_RemovesTheRedCrossFromTheMap()
        {
            yield return LoadScene();
            var panel = Object.FindFirstObjectByType<PrototypeHud>().GetComponent<MapPanel>();
            for (int i = 0; i < 5; i++) yield return null;
            var letter = GameObject.Find("Carta_Azar").GetComponent<Qazrix.Gameplay.LetterPickup>();
            _player.GetComponent<Qazrix.Gameplay.PlayerInteractor>().BeginPickup(letter);
            yield return new WaitForSeconds(Qazrix.Simulation.Movement.PickupCycle.Duration + 0.3f);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsTrue(panel.ObjectiveRevealed, "con la carta, la X se ve");
            var contact = Object.FindFirstObjectByType<Qazrix.Gameplay.ObjectiveContact>();
            Assert.IsFalse(contact.Touched, "aún no la has tocado");

            // Se la deja a su lado, sin tocarla, y luego pegada a ella.
            Vector3 woman = contact.transform.position;
            _player.transform.position = woman + new Vector3(3f, 0.1f, 0f);
            Physics.SyncTransforms();
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsTrue(panel.ObjectiveRevealed, "a 3 m la X sigue");

            _player.transform.position = woman + new Vector3(0.9f, 0.1f, 0f);
            Physics.SyncTransforms();
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsTrue(contact.Touched, "pegado a ella, la ha tocado");
            Assert.IsTrue(panel.ObjectiveReached);
            Assert.IsFalse(panel.ObjectiveRevealed, "la X desaparece del mapa");

            _player.transform.position = woman + new Vector3(30f, 0.1f, 0f);
            Physics.SyncTransforms();
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsFalse(panel.ObjectiveRevealed, "y no vuelve al alejarse");
        }

        [UnityTest]
        public IEnumerator SelectingTheLetterInTheInventory_OpensTheMapWithARedCross()
        {
            yield return LoadScene();
            var hud = Object.FindFirstObjectByType<PrototypeHud>();
            var interactor = _player.GetComponent<Qazrix.Gameplay.PlayerInteractor>();
            var panel = hud.GetComponent<MapPanel>();
            var letter = GameObject.Find("Carta_Azar").GetComponent<Qazrix.Gameplay.LetterPickup>();
            Assert.IsTrue(letter.HasMark, "la carta señala un punto");

            // Se recoge la carta (el gesto de agacharse ya se prueba aparte): aquí solo importa el inventario.
            interactor.BeginPickup(letter);
            yield return new WaitForSeconds(Qazrix.Simulation.Movement.PickupCycle.Duration + 0.3f);
            Assert.AreEqual(1, interactor.Inventory.CountOf("carta"));
            Assert.IsTrue(interactor.Inventory.Entries[0].HasMark, "la carta guardada conserva su marca");
            Assert.IsFalse(panel.HasMark, "hasta elegirla no hay cruz");

            Press(_keyboard.iKey);
            yield return null;
            yield return null;
            Release(_keyboard.iKey);
            Assert.IsTrue(hud.IsInventoryOpen);

            hud.SelectInventoryEntry(0);
            for (int i = 0; i < 12; i++) yield return null;
            Assert.IsFalse(hud.IsInventoryOpen, "al elegir la carta se cierra el inventario");
            Assert.IsTrue(hud.IsMapOpen, "y se abre el mapa");
            Assert.IsTrue(panel.ObjectiveRevealed, "con la X roja del objetivo");

            Vector3 o = panel.Objective.Value, p = _player.transform.position;
            float away = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(o.x, o.z));
            Debug.Log($"[Qazrix] X roja a {away:0} m del jugador, en ({o.x:0.0}, {o.z:0.0})");
            Assert.Greater(away, 100f, "lejos de la plaza");
            Assert.IsTrue(_city.IsClear(o.x, o.z, 1.2f), "ni dentro de un edificio");
            Assert.IsFalse(_city.IsClear(o.x, o.z, 20f), "está en el interior del casco, con edificios alrededor");

            yield return Capture("play_z01_mapa_cruz.png");
        }

        [UnityTest]
        public IEnumerator Map_OpensWithM_BlocksMovement_AndClosesAgain()
        {
            yield return LoadScene();
            var hud = Object.FindFirstObjectByType<PrototypeHud>();
            Assert.IsNotNull(hud);
            Assert.IsFalse(hud.IsMapOpen);

            Press(_keyboard.mKey);
            yield return null;
            yield return null;
            Release(_keyboard.mKey);
            Assert.IsTrue(hud.IsMapOpen, "M abre el mapa");
            var panel = hud.GetComponent<MapPanel>();
            Assert.Greater(panel.ExploredCells, 500, "ya ha descubierto el entorno del jugador");
            Assert.Less(panel.ExploredCells, 4000, "pero no todo el mapa: solo un radio de visión alrededor");
            Debug.Log($"[Qazrix] Mapa: {panel.ExploredCells} celdas exploradas, {panel.SeenGuards} guardias vistos, {panel.SeenLetters} cartas vistas");
            for (int i = 0; i < 10; i++) yield return null;
            yield return Capture("play_z01_mapa.png");

            Press(_keyboard.mKey);
            yield return null;
            yield return null;
            Release(_keyboard.mKey);
            Assert.IsFalse(hud.IsMapOpen, "M lo cierra");
        }

        [UnityTest]
        public IEnumerator Guards_StartOnTheGround_InRandomSpotsOfTheOldTown_ChosenAnewEachGame()
        {
            yield return LoadScene();
            for (int i = 0; i < 5; i++) yield return null;
            var scatter = Object.FindFirstObjectByType<Qazrix.Gameplay.GuardScatter>();
            Assert.IsNotNull(scatter, "la escena reparte a los guardias");
            GuardController[] guards = Object.FindObjectsByType<GuardController>(FindObjectsSortMode.None);
            Assert.AreEqual(20, guards.Length, "2 de la plaza y 18 rondas");
            Assert.AreEqual(guards.Length, scatter.Placed.Count, "todos han recibido un sitio al azar");

            Vector3 spawn = _player.transform.position;
            foreach (GuardController g in guards)
            {
                Vector3 p = g.transform.position;
                Assert.IsTrue(UnityEngine.AI.NavMesh.SamplePosition(p, out _, 1.5f, UnityEngine.AI.NavMesh.AllAreas), $"{g.name} sobre la malla de navegación");
                Assert.IsTrue(_city.IsClear(p.x, p.z, 1.5f), $"{g.name} no nace dentro de un edificio");
                Assert.IsFalse(_city.IsClear(p.x, p.z, 25f), $"{g.name} está en el casco, con edificios alrededor");
                Assert.IsTrue(Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out RaycastHit floor, 1.5f, ~0, QueryTriggerInteraction.Ignore), $"{g.name} pisa el suelo");
                Assert.Less(Mathf.Abs(floor.point.y - p.y), 0.35f, $"{g.name} a ras de suelo");
                Assert.GreaterOrEqual(Vector2.Distance(new Vector2(p.x, p.z), new Vector2(spawn.x, spawn.z)), 29f, $"{g.name} lejos del jugador");
            }
            // Separación en el momento de colocarlos (después ya andan su ronda y pueden acercarse unos metros).
            Vector3[] placed = scatter.Placed.Values.ToArray();
            for (int i = 0; i < placed.Length; i++)
                for (int j = i + 1; j < placed.Length; j++)
                    Assert.GreaterOrEqual(Vector3.Distance(placed[i], placed[j]), 19f, "no nacen apilados");

            // Repartidos por todo el casco: cada guardia va a una casilla distinta (8 × 8 sobre la zona) mientras haya casillas con
            // calles alcanzables; las del borde, casi sin edificios, quedan vacías y sus guardias se reparten por el resto.
            Rect area = _city.LocalArea;
            var cellsUsed = new System.Collections.Generic.HashSet<int>();
            foreach (Vector3 q in placed)
            {
                int c = Mathf.Clamp((int)((q.x - area.xMin) / (area.width / 8f)), 0, 7);
                int r = Mathf.Clamp((int)((q.z - area.yMin) / (area.height / 8f)), 0, 7);
                cellsUsed.Add(r * 8 + c);
            }
            Assert.GreaterOrEqual(cellsUsed.Count, 15, $"guardias repartidos en casillas distintas ({cellsUsed.Count} de 20 guardias)");

            // Cada partida es distinta: al recargar la escena salen en otros sitios.
            var first = guards.Select(g => g.name + g.transform.position).OrderBy(x => x).ToArray();
            yield return LoadScene();
            for (int i = 0; i < 5; i++) yield return null;
            var second = Object.FindObjectsByType<GuardController>(FindObjectsSortMode.None).Select(g => g.name + g.transform.position).OrderBy(x => x).ToArray();
            Assert.AreNotEqual(string.Join("|", first), string.Join("|", second), "el reparto cambia de una partida a otra");
        }

        private static IEnumerator MeasureFrames(float seconds, System.Action<float, float> done)
        {
            for (int i = 0; i < 30; i++) yield return null; // calentamiento
            var times = new System.Collections.Generic.List<float>();
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) { yield return null; times.Add(Time.unscaledDeltaTime); }
            times.Sort();
            done(times.Average() * 1000f, times[(int)(times.Count * 0.99f)] * 1000f);
        }

        [UnityTest]
        public IEnumerator Perf_CityGuards_CostOfTheExtraPatrols()
        {
            yield return LoadScene();
            GuardController[] patrols = Object.FindObjectsByType<GuardController>(FindObjectsSortMode.None)
                .Where(g => g.name.StartsWith("Guardia_Ciudad")).ToArray();
            int triangles = 0;
            int lowTriangles = 0;
            foreach (MeshFilter f in patrols[0].GetComponentsInChildren<MeshFilter>(true))
            {
                int t = f.sharedMesh.triangles.Length / 3;
                if (f.name.EndsWith("_LOD1")) lowTriangles += t; else triangles += t;
            }
            int parts = patrols[0].GetComponentsInChildren<MeshRenderer>(true).Length / 2;

            float withAvg = 0, withP99 = 0, withoutAvg = 0, withoutP99 = 0;
            yield return MeasureFrames(6f, (a, p) => { withAvg = a; withP99 = p; });
            foreach (GuardController g in patrols) g.gameObject.SetActive(false);
            yield return MeasureFrames(6f, (a, p) => { withoutAvg = a; withoutP99 = p; });
            Debug.Log($"[Qazrix] PERF con {patrols.Length} guardias de ronda: {withAvg:0.0} ms medios, {withP99:0.0} ms p99 ({1000f / withAvg:0} fps) · sin ellos: {withoutAvg:0.0} ms, {withoutP99:0.0} ms p99 ({1000f / withoutAvg:0} fps) · cada guardia: {parts} piezas, {triangles} triángulos de cerca y {lowTriangles} de lejos");
            Assert.Pass();
        }

        [UnityTest]
        public IEnumerator Monuments_StandWhereTheControlPointsSay()
        {
            yield return LoadScene();
            // Cada punto de control es el centro de un cuerpo alto medido en el LiDAR, con su caja
            // envolvente y su altura máxima. Dentro de esa caja, la ciudad levantada debe alcanzar esa
            // altura (con 5 m de margen por el suavizado de los tejados). Si la ciudad estuviera
            // desplazada decenas de metros o en espejo, fallaría. Se busca en toda la caja, y no solo en
            // el punto, porque el centro de un edificio con claustro (San Francisco Javier) cae en patio.
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string[] lines = File.ReadAllLines("Research/Survey/GroundControlPoints.csv");
            string[] header = lines[0].Split(',');
            int Col(string name) => System.Array.IndexOf(header, name);
            int checkedPoints = 0;

            foreach (string line in lines.Skip(1).Where(l => l.Contains(",control,")))
            {
                string[] cell = line.Split(','); // las columnas usadas van antes de cualquier campo con comas
                string id = cell[Col("id")];
                float x = float.Parse(cell[Col("local_x")], inv), z = float.Parse(cell[Col("local_z")], inv);
                float halfX = float.Parse(cell[Col("size_ew")], inv) * 0.5f, halfZ = float.Parse(cell[Col("size_ns")], inv) * 0.5f;
                float measured = float.Parse(cell[Col("height_m")], inv);
                float groundY = float.Parse(cell[Col("local_y")], inv);

                float tallest = 0f;
                for (float dz = -halfZ; dz <= halfZ; dz += 1f)
                {
                    for (float dx = -halfX; dx <= halfX; dx += 1f)
                    {
                        var from = new Vector3(x + dx, groundY + 150f, z + dz);
                        if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 300f))
                            tallest = Mathf.Max(tallest, hit.point.y - groundY);
                    }
                }
                // Cada edificio tiene un tejado plano a la cota mediana del suyo (D44): las agujas y remates
                // quedan rebajados, así que se exige el 70 % de la altura máxima medida. Las casas de
                // alrededor rondan los 10 m: una ciudad desplazada o en espejo seguiría fallando.
                Assert.Greater(tallest, measured * 0.7f, $"{id}: en su caja debería haber tejado de unos {measured:0} m");
                checkedPoints++;
            }
            Assert.AreEqual(3, checkedPoints, "se esperan los tres puntos de control");
        }

        [UnityTest]
        public IEnumerator Streets_AreWalkable_NotSealedByTheBuildings()
        {
            yield return LoadScene();
            // Andar 6 s hacia el sur desde la plaza: sin atascarse en el sitio y sin atravesar edificios.
            Vector3 start = _player.transform.position;
            Press(_keyboard.sKey);
            float end = Time.time + 6f;
            bool insideBuilding = false;
            while (Time.time < end)
            {
                Vector3 p = _player.transform.position;
                insideBuilding |= _city.HasBuildingAt(p.x, p.z) && p.y < start.y + 2f;
                yield return null;
            }
            Release(_keyboard.sKey);

            Assert.IsFalse(insideBuilding, "el jugador no debe poder meterse dentro de un edificio");
            Assert.Greater(Vector3.Distance(start, _player.transform.position), 4f, "debe poder avanzar por la plaza");
        }

        [UnityTest]
        public IEnumerator Buildings_AreSeparateVolumes_WithFlatRoofs()
        {
            yield return LoadScene();
            // Sobre un edificio, el tejado es plano: en un cuadrado de 2 m alrededor del centro de un
            // tejado la cota no varía. Se comprueba en un punto de control (cuerpo alto y ancho).
            string[] lines = File.ReadAllLines("Research/Survey/GroundControlPoints.csv");
            string[] header = lines[0].Split(',');
            string[] cell = lines.Skip(1).First(l => l.Contains(",control,")).Split(',');
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            float x = float.Parse(cell[System.Array.IndexOf(header, "local_x")], inv);
            float z = float.Parse(cell[System.Array.IndexOf(header, "local_z")], inv);

            float min = float.MaxValue, max = float.MinValue;
            for (float dz = -1f; dz <= 1f; dz += 0.5f)
            {
                for (float dx = -1f; dx <= 1f; dx += 0.5f)
                {
                    Assert.IsTrue(Physics.Raycast(new Vector3(x + dx, 200f, z + dz), Vector3.down, out RaycastHit hit, 400f));
                    min = Mathf.Min(min, hit.point.y);
                    max = Mathf.Max(max, hit.point.y);
                }
            }
            Assert.Less(max - min, 0.05f, "el tejado de un edificio debe ser plano, no una superficie ondulada");
            yield return Capture("play_z01_edificios.png");
        }

        [UnityTest]
        public IEnumerator PlazaStairs_CanBeClimbedOnFoot()
        {
            yield return LoadScene();
            Assert.IsNotNull(_stairs, "la escena no tiene escalinata");
            Assert.Greater(_stairs.BuiltCells, 5000, "la escalinata de la plaza ocupa más de 200 m²");

            // Pie del tramo grande de la plaza (lado norte, abajo): el jugador se coloca ahí y anda hacia
            // el sur, escalinata arriba, hasta la plataforma alta (unos 3 m de desnivel).
            const float x = 22f, z = 106f;
            Assert.IsTrue(Physics.Raycast(new Vector3(x, 100f, z), Vector3.down, out RaycastHit ground, 300f));
            var body = _player.GetComponent<CharacterController>();
            body.enabled = false;
            _player.transform.position = ground.point + Vector3.up * 0.2f;
            body.enabled = true;
            for (int i = 0; i < 20; i++) yield return null;
            float startY = _player.transform.position.y;

            Press(_keyboard.sKey);
            float end = Time.time + 7f;
            bool onSteps = false;
            while (Time.time < end)
            {
                Vector3 p = _player.transform.position;
                onSteps |= _stairs.HasBuildingAt(p.x, p.z);
                if (p.y > startY + 1.4f && !_stairs.HasBuildingAt(p.x, p.z) && p.z < 96f) break; // ya está arriba
                yield return null;
            }
            yield return Capture("play_z01_escalinata.png");
            Release(_keyboard.sKey);

            Vector3 reached = _player.transform.position;
            Assert.IsTrue(onSteps, "el recorrido debe pasar por los peldaños");
            Assert.Greater(reached.y - startY, 2.3f, $"debe subir la escalinata (quedó en z {reached.z:0.0}, subió {reached.y - startY:0.00} m)");
            Assert.IsTrue(_player.IsGrounded);
        }

        [UnityTest]
        public IEnumerator Player_ClimbsAFacade_LetsGo_AndReachesTheRoof()
        {
            yield return LoadScene();
            var climb = _player.GetComponent<PlayerWallClimb>();
            Assert.IsNotNull(climb, "el jugador no tiene PlayerWallClimb");

            // Primera fachada alta de la ciudad al norte de la plaza que mire al sur (W lleva hacia el norte).
            Vector3 spawn = _player.transform.position;
            bool found = FindTallFacade(spawn, out RaycastHit wall);
            Assert.IsTrue(found, "no se encontró una fachada alta al norte de la plaza");
            Assert.IsTrue(Physics.Raycast(wall.point + new Vector3(0f, 1f, -0.9f), Vector3.down, out RaycastHit ground, 40f));

            var body = _player.GetComponent<CharacterController>();
            body.enabled = false;
            _player.transform.position = ground.point + Vector3.up * 0.1f;
            body.enabled = true;
            for (int i = 0; i < 20; i++) yield return null;
            float groundY = _player.transform.position.y;

            // Corre contra la fachada: se agarra y sube.
            Press(_keyboard.leftShiftKey);
            Press(_keyboard.wKey);
            float limit = Time.time + 4f;
            while (Time.time < limit && !_player.IsClimbing) yield return null;
            Assert.IsTrue(_player.IsClimbing, "corriendo contra la fachada debe agarrarse a ella");
            limit = Time.time + 4f;
            while (Time.time < limit && _player.transform.position.y < groundY + 2f) yield return null;
            Assert.Greater(_player.transform.position.y, groundY + 2f, "debe subir por la fachada");

            // Animación de escalada: los dos brazos por encima de los hombros y alternándose.
            var animator = _player.GetComponentInChildren<CharacterWalkAnimator>();
            Assert.IsNotNull(animator, "el hidalgo no tiene animador");
            float widestGap = 0f;
            for (int i = 0; i < 40; i++)
            {
                Assert.AreEqual(0, CapeClipping.PenetratingPoints(_player.transform), "trepando, el cuerpo no debe atravesar la capa");
                Assert.Greater(animator.LeftArmAngle, 100f, "trepando, el brazo izquierdo va levantado");
                Assert.Greater(animator.RightArmAngle, 100f, "y el derecho también");
                widestGap = Mathf.Max(widestGap, Mathf.Abs(animator.LeftArmAngle - animator.RightArmAngle));
                yield return null;
            }
            Assert.Greater(widestGap, 25f, "los brazos deben alternarse al subir");
            yield return Capture("play_z01_trepada.png");

            // Agacharse suelta la pared y cae al suelo.
            Release(_keyboard.wKey);
            Press(_keyboard.cKey);
            yield return null;
            yield return null;
            Assert.IsFalse(_player.IsClimbing, "agacharse debe soltar la pared");
            Release(_keyboard.cKey);
            limit = Time.time + 3f;
            while (Time.time < limit && !_player.IsGrounded) yield return null;
            Assert.IsTrue(_player.IsGrounded, "al soltarse cae al suelo");
            Assert.Less(_player.transform.position.y, groundY + 0.6f);

            // Segundo intento, hasta arriba: se encarama al tejado.
            Press(_keyboard.wKey);
            limit = Time.time + 25f;
            while (Time.time < limit && climb.RoofsReached == 0) yield return null;
            Release(_keyboard.wKey);
            Release(_keyboard.leftShiftKey);
            for (int i = 0; i < 20; i++) yield return null;

            Vector3 top = _player.transform.position;
            Assert.AreEqual(1, climb.RoofsReached, "debe llegar al tejado");
            Assert.IsFalse(_player.IsClimbing);
            Assert.IsTrue(_player.IsGrounded, "y quedar de pie en él");
            yield return Capture("play_z01_tejado.png");
            // Lo que pisa es la ciudad (puede quedar en el mismo borde del tejado, medio cuerpo fuera).
            Assert.IsTrue(Physics.SphereCast(top + Vector3.up, 0.25f, Vector3.down, out RaycastHit under, 1.2f)
                          && under.collider.GetComponentInParent<CityBlocks>() == _city,
                $"debe quedar de pie sobre un edificio (pared en {wall.point}, quedó en {top}, calle a {groundY:0.0})");
            Assert.Greater(top.y, groundY + 2.4f);
            Debug.Log($"[Qazrix] Trepada: tejado a {top.y - groundY:0.0} m sobre la calle");
            yield return Capture("play_z01_tejado.png");
        }

        [UnityTest]
        public IEnumerator Watchtower_Synchronizes_AndTheFaithLeapEndsInTheHay()
        {
            yield return LoadScene();
            var viewpoint = Object.FindFirstObjectByType<Qazrix.Gameplay.ViewpointZone>();
            var leapZone = Object.FindFirstObjectByType<Qazrix.Gameplay.FaithLeapZone>();
            var leap = _player.GetComponent<PlayerFaithLeap>();
            Assert.IsNotNull(viewpoint, "la escena no tiene atalaya");
            Assert.IsNotNull(leapZone, "la atalaya no tiene zona de salto de fe");
            Assert.IsNotNull(leap, "el jugador no tiene PlayerFaithLeap");
            Assert.IsNotNull(leapZone.Target, "la zona de salto no tiene pajar");
            Assert.IsFalse(viewpoint.IsSynchronized);

            var body = _player.GetComponent<CharacterController>();

            // Sube a la azotea (la trepada ya está probada aparte) y se sincroniza.
            Vector3 roof = viewpoint.transform.position;
            Assert.IsTrue(Physics.Raycast(roof + Vector3.up * 5f, Vector3.down, out RaycastHit top, 20f, ~0, QueryTriggerInteraction.Ignore),
                "bajo la atalaya debe haber azotea (los triggers de la zona no cuentan)");
            body.enabled = false;
            _player.transform.position = top.point + Vector3.up * 0.1f;
            body.enabled = true;
            for (int i = 0; i < 20; i++) yield return null;
            Assert.IsTrue(viewpoint.IsSynchronized, "pisar la azotea debe sincronizar la atalaya");
            Assert.AreEqual(viewpoint.Label, _player.CheckpointLabel);
            Assert.IsTrue(_player.IsGrounded, "en la azotea el jugador está de pie");

            // Al borde: se ofrece el salto de fe.
            body.enabled = false;
            _player.transform.position = leapZone.transform.position - Vector3.up * 1.4f;
            body.enabled = true;
            for (int i = 0; i < 15; i++) yield return null;
            Assert.IsTrue(_player.IsGrounded);
            Assert.IsTrue(_player.FaithLeapAvailable, "en el borde debe ofrecerse el salto de fe");
            float roofY = _player.transform.position.y;
            yield return Capture("play_z01_atalaya_borde.png");

            Press(_keyboard.spaceKey);
            yield return null;
            Release(_keyboard.spaceKey);
            Assert.IsTrue(_player.IsLeaping, "espacio en el borde debe lanzar el salto de fe");

            // A mitad de vuelo: en el aire, muy por debajo de la azotea, con los brazos arriba.
            float limit = Time.time + 3f;
            while (Time.time < limit && leap.Progress < 0.6f) yield return null;
            var animator = _player.GetComponentInChildren<CharacterWalkAnimator>();
            Assert.Greater(animator.LeftArmAngle, 100f, "en el salto de fe los brazos van arriba");
            yield return Capture("play_z01_salto_de_fe.png");

            limit = Time.time + 6f;
            while (Time.time < limit && _player.IsLeaping) yield return null;
            for (int i = 0; i < 20; i++) yield return null;

            Vector3 end = _player.transform.position;
            Assert.AreEqual(1, leap.LeapsDone, "debe completar un salto");
            Assert.IsTrue(leap.LastLeapSafe, "debe acabar sobre el pajar");
            Assert.AreEqual(1, leapZone.Target.LandingsDone);
            Assert.Less(Vector3.Distance(new Vector3(end.x, 0f, end.z), new Vector3(leapZone.Target.transform.position.x, 0f, leapZone.Target.transform.position.z)),
                leapZone.Target.Radius, "debe quedar dentro del radio del pajar");
            Assert.Less(end.y, roofY - 8f, "debe haber bajado muchos metros");
            Assert.IsTrue(_player.IsGrounded, "y quedar de pie en la calle");
            Assert.IsFalse(_player.IsLeaping);
            Debug.Log($"[Qazrix] Salto de fe: {roofY - end.y:0.0} m de desnivel");
            yield return Capture("play_z01_pajar.png");
        }

        [UnityTest]
        public IEnumerator Player_ClimbingJumpsBackwards_OffTheWall()
        {
            yield return LoadScene();
            var climb = _player.GetComponent<PlayerWallClimb>();
            Assert.IsNotNull(climb);
            _guard.gameObject.SetActive(false); // si lo alcanza el guardia, reaparece y gira: no es lo que se prueba aquí

            // Primera fachada alta al norte de la plaza que mire al sur (W lleva hacia el norte).
            Vector3 spawn = _player.transform.position;
            bool found = FindTallFacade(spawn, out RaycastHit wall);
            Assert.IsTrue(found, "no se encontró una fachada alta al norte de la plaza");
            Assert.IsTrue(Physics.Raycast(wall.point + new Vector3(0f, 1f, -0.9f), Vector3.down, out RaycastHit ground, 40f));

            var body = _player.GetComponent<CharacterController>();
            body.enabled = false;
            _player.transform.position = ground.point + Vector3.up * 0.1f;
            body.enabled = true;
            for (int i = 0; i < 20; i++) yield return null;
            float groundY = _player.transform.position.y;

            // Sube hasta unos 3 m por la fachada.
            Press(_keyboard.leftShiftKey);
            Press(_keyboard.wKey);
            float limit = Time.time + 4f;
            while (Time.time < limit && !_player.IsClimbing) yield return null;
            Assert.IsTrue(_player.IsClimbing, "debe agarrarse a la fachada");
            limit = Time.time + 5f;
            while (Time.time < limit && _player.transform.position.y < groundY + 3f) yield return null;
            // Una tecla por fotograma: dos eventos de teclado en el mismo fotograma pueden pisarse entre sí.
            Release(_keyboard.wKey);
            yield return null;
            Release(_keyboard.leftShiftKey);
            yield return null;
            yield return null;
            Assert.IsFalse(_keyboard.wKey.isPressed, "la tecla W debe estar suelta antes de saltar");
            Assert.IsTrue(_player.IsClimbing, "sigue colgado de la pared");

            Vector3 hang = _player.transform.position;
            Press(_keyboard.spaceKey);
            yield return null;
            Release(_keyboard.spaceKey);

            // Sale de la pared: ya no está trepando, se aleja hacia atrás (al sur) y sube un poco antes de caer.
            float peak = hang.y, farthest = 0f;
            bool grabbedAgain = false;
            var facing = new System.Text.StringBuilder();
            float nextSample = Time.time;
            limit = Time.time + 1.2f;
            while (Time.time < limit)
            {
                Vector3 p = _player.transform.position;
                if (Time.time >= nextSample)
                {
                    facing.Append($"[{Time.time - (limit - 1.2f):0.0}s fz={_player.transform.forward.z:0.00} z={p.z - hang.z:0.0} grounded={_player.IsGrounded} w={_keyboard.wKey.isPressed} shift={_keyboard.leftShiftKey.isPressed} v={_player.HorizontalSpeed:0.0}] ");
                    nextSample += 0.15f;
                }
                peak = Mathf.Max(peak, p.y);
                farthest = Mathf.Max(farthest, hang.z - p.z);
                grabbedAgain |= _player.IsClimbing;
                yield return null;
            }
            Assert.AreEqual(1, climb.BackJumpsDone, "se ha contado un salto hacia atrás");
            Assert.IsFalse(grabbedAgain, "no se vuelve a agarrar al instante a la misma pared");
            Assert.Greater(farthest, 1.5f, "debe separarse de la pared hacia atrás");
            Assert.Greater(peak, hang.y + 0.4f, "y subir un poco");
            Assert.Less(_player.transform.forward.z, -0.5f, "mirando hacia donde salta, no hacia la pared: " + facing);
            yield return Capture("play_z01_salto_atras.png");

            limit = Time.time + 4f;
            while (Time.time < limit && !_player.IsGrounded) yield return null;
            Assert.IsTrue(_player.IsGrounded, "acaba cayendo al suelo");
            Debug.Log($"[Qazrix] Salto hacia atrás desde {hang.y - groundY:0.0} m: se separó {farthest:0.0} m y subió {peak - hang.y:0.0} m");
        }

        [UnityTest]
        public IEnumerator City_BuildingsArePolygons_WithStraightWalls_AndDiagonalCorners()
        {
            yield return LoadScene();
            Assert.IsTrue(_city.UseFootprints, "la ciudad de Z01 se levanta con huellas poligonales");
            var parts = _city.Parts;
            Assert.IsNotNull(parts);
            Assert.Greater(parts.Count, 300, "cientos de edificios");

            int simplified = 0, cellRoofs = 0, edges = 0, shortEdges = 0, diagonal = 0, long5 = 0;
            foreach (Qazrix.Simulation.World.BuildingPart part in parts)
            {
                if (part.Simplified) simplified++;
                if (part.CellRoof) cellRoofs++;
                foreach (Qazrix.Simulation.World.Vec2[] loop in part.Loops)
                {
                    for (int i = 0; i < loop.Length; i++)
                    {
                        Qazrix.Simulation.World.Vec2 a = loop[i], b = loop[(i + 1) % loop.Length];
                        double dx = b.X - a.X, dz = b.Z - a.Z, length = System.Math.Sqrt(dx * dx + dz * dz);
                        edges++;
                        if (length < 1.0) shortEdges++;
                        if (length >= 5.0) long5++;
                        double angle = System.Math.Abs(System.Math.Atan2(dz, dx) * 180.0 / System.Math.PI) % 90.0;
                        if (angle > 8.0 && angle < 82.0) diagonal++;
                    }
                }
            }
            Debug.Log($"[Qazrix] Huellas: {parts.Count} partes, {simplified} simplificadas, {cellRoofs} con tejado por celdas, " +
                      $"{edges} lados ({long5} de 5 m o más, {shortEdges} de menos de 1 m, {diagonal} en diagonal)");
            yield return Capture("play_z01_poligonos.png");

            // Vista cenital de varias manzanas alrededor de la plaza.
            var topObject = new GameObject("CamaraCenital");
            Camera top = topObject.AddComponent<Camera>();
            top.depth = 50f;
            top.orthographic = true;
            top.orthographicSize = 38f;
            top.farClipPlane = 600f;
            Vector3 around = _player.transform.position;
            topObject.transform.position = new Vector3(around.x, around.y + 150f, around.z);
            topObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            yield return null;
            yield return Capture("play_z01_poligonos_cenital.png");

            // Y más de cerca, 18 m de lado.
            top.orthographicSize = 18f;
            yield return null;
            yield return Capture("play_z01_poligonos_cenital_cerca.png");
            Object.Destroy(topObject);

            Assert.Greater(simplified, parts.Count * 8 / 10, "casi todos los contornos se enderezan");
            Assert.LessOrEqual(cellRoofs, parts.Count / 50, "casi ningún tejado necesita el relleno por celdas");
            Assert.Greater(diagonal, 100, "hay paredes en diagonal");
            Assert.Greater(long5, edges / 8, "muchas paredes son largas y rectas, no dientes de sierra");
            Assert.Less(shortEdges, edges / 6, "pocos lados quedan por debajo de 1 m: sin muescas ni chaflanes a mitad de pared");

        }

        [UnityTest]
        public IEnumerator City_HasAFewMachicolations_VisualOnly_AndOneIsPhotographedUpClose()
        {
            yield return LoadScene();
            Assert.IsTrue(_city.WithMachicolations, "la ciudad de Z01 lleva matacanes");
            Debug.Log($"[Qazrix] Matacanes: {_city.MachicolationCount} ({_city.Machicolations.Count(m => m.Tower)} en torres)");
            Assert.That(_city.MachicolationCount, Is.InRange(150, 400), "muchos matacanes, sin pasar del tope");
            Assert.Greater(_city.Machicolations.Count(m => m.Tower), 0, "al menos uno en una torre");
            Assert.Greater(_city.Machicolations.Count(m => !m.Tower), 50, "y muchos en edificios altos");
            var all = _city.Machicolations;
            double nearest2 = double.MaxValue;
            for (int i = 0; i < all.Count; i++)
                for (int j = i + 1; j < all.Count; j++)
                {
                    if (all[i].Tower && all[j].Tower) continue; // las torres tienen prioridad y no se separan entre sí
                    double dx = all[i].X - all[j].X, dz = all[i].Z - all[j].Z;
                    nearest2 = System.Math.Min(nearest2, dx * dx + dz * dz);
                }
            Assert.GreaterOrEqual(System.Math.Sqrt(nearest2), 6.0 - 1e-6, "ningún matacán de edificio alto a menos de 6 m de otro");

            // Solo visuales: ni la malla de piedra ni la de los huecos llevan colisionador.
            foreach (string name in new[] { "Ciudad_Matacanes", "Ciudad_Matacanes_Huecos" })
            {
                Transform child = _city.transform.Find(name);
                Assert.IsNotNull(child, $"falta la malla {name}");
                Assert.IsNull(child.GetComponent<Collider>(), $"{name} no debe llevar colisionador: no estorba trepar ni encaramarse");
                var renderer = child.GetComponent<MeshRenderer>();
                var filter = child.GetComponent<MeshFilter>();
                Debug.Log($"[Qazrix] {name}: activo {child.gameObject.activeInHierarchy}, render {renderer.enabled}, material {(renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "NINGUNO")}, " +
                          $"{filter.sharedMesh.vertexCount} vértices, {filter.sharedMesh.triangles.Length / 3} triángulos, límites {renderer.bounds}");
            }

            // Foto de cerca del más cercano al jugador, con una cámara temporal puesta fuera de la pared.
            Qazrix.Simulation.World.Machicolation nearest = _city.Machicolations
                .OrderBy(m => (m.X - _player.transform.position.x) * (m.X - _player.transform.position.x)
                            + (m.Z - _player.transform.position.z) * (m.Z - _player.transform.position.z)).First();
            var cameraObject = new GameObject("CamaraMatacan");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.depth = 50f;
            camera.farClipPlane = 800f;
            var wall = new Vector3((float)nearest.X, nearest.RoofHeight - 0.9f, (float)nearest.Z);
            var outward = new Vector3(nearest.NormalX, 0f, nearest.NormalZ);
            var along = new Vector3(nearest.TangentX, 0f, nearest.TangentZ);
            Debug.Log($"[Qazrix] Matacán a ({nearest.X:0.0}, {nearest.Z:0.0}), tejado {nearest.RoofHeight:0.0} m, normal ({nearest.NormalX:0.00}, {nearest.NormalZ:0.00}), largo {nearest.Length:0.0} m");

            // Diagnóstico: dónde está la pared de verdad y dónde ha quedado la malla del matacán.
            Vector3 probe = new Vector3((float)nearest.X, nearest.RoofHeight - 0.7f, (float)nearest.Z) + outward * 2f;
            bool hitWall = Physics.Raycast(probe, -outward, out RaycastHit wallHit, 6f);
            Debug.Log($"[Qazrix] Pared a {(hitWall ? wallHit.distance.ToString("0.00") : "sin impacto")} m del punto a 2 m del borde previsto (debería ser ~2,00); choca con {(hitWall ? wallHit.collider.name : "-")}");
            Transform stone = _city.transform.Find("Ciudad_Matacanes");
            Mesh stoneMesh = stone.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] verts = stoneMesh.vertices;
            float minD = float.MaxValue, maxD = float.MinValue, minY = float.MaxValue, maxY = float.MinValue; int near = 0;
            foreach (Vector3 v in verts)
            {
                if (Mathf.Abs(v.x - (float)nearest.X) > 3f || Mathf.Abs(v.z - (float)nearest.Z) > 3f) continue;
                near++;
                float d = (v.x - (float)nearest.X) * nearest.NormalX + (v.z - (float)nearest.Z) * nearest.NormalZ;
                minD = Mathf.Min(minD, d); maxD = Mathf.Max(maxD, d); minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
            }
            Debug.Log($"[Qazrix] Malla del matacán cerca: {near} vértices; hacia fuera de {minD:0.00} a {maxD:0.00} m; altura de {minY:0.00} a {maxY:0.00} m (tejado {nearest.RoofHeight:0.00})");

            var slab = new Vector3((float)nearest.X, nearest.RoofHeight - 1.2f, (float)nearest.Z);

            // Perfil: desde un lado, a la altura de la losa, mirando a lo largo de la pared: el vuelo se ve de canto.
            cameraObject.transform.position = slab + along * 7f + outward * 0.4f;
            cameraObject.transform.LookAt(slab + outward * 0.4f);
            yield return null;
            yield return Capture("play_z01_matacan.png");

            // De frente y de cerca, un poco desde abajo.
            cameraObject.transform.position = slab + outward * 4.5f - Vector3.up * 1.3f;
            cameraObject.transform.LookAt(slab);
            yield return null;
            yield return Capture("play_z01_matacan_frente.png");

            // Y desde la calle, de más lejos y desde abajo.
            cameraObject.transform.position = wall + outward * 8f - Vector3.up * 3.5f;
            cameraObject.transform.LookAt(wall);
            yield return null;
            yield return Capture("play_z01_matacan_abajo.png");

            // Vista de calle de varios a la vez: desde el jugador hacia el grupo de matacanes más cercano.
            Vector3 eye = _player.transform.position + Vector3.up * 1.7f;
            var group = _city.Machicolations
                .Where(m => (m.X - eye.x) * (m.X - eye.x) + (m.Z - eye.z) * (m.Z - eye.z) < 60f * 60f)
                .OrderBy(m => (m.X - eye.x) * (m.X - eye.x) + (m.Z - eye.z) * (m.Z - eye.z)).Take(6).ToList();
            if (group.Count > 0)
            {
                float gx = (float)group.Average(m => m.X), gz = (float)group.Average(m => m.Z), gy = group.Average(m => m.RoofHeight) - 1.5f;
                cameraObject.transform.position = eye;
                cameraObject.transform.LookAt(new Vector3(gx, gy, gz));
                yield return null;
                yield return Capture("play_z01_matacanes_calle.png");
            }
            Object.Destroy(cameraObject);
        }

        [UnityTest]
        public IEnumerator Player_CanPerchInsideAMachicolation_DroppingFromTheRoof_AndItStaysPut()
        {
            yield return LoadScene();
            _guard.enabled = false;
            Assert.IsNotNull(_city.transform.Find("Ciudad_Matacanes_Colision"), "los matacanes llevan suelo y pretil sólidos");

            // El más cercano al jugador, para que el terreno y los edificios de alrededor estén cargados.
            var rules = new Qazrix.Simulation.World.MachicolationRules();
            Qazrix.Simulation.World.Machicolation m = _city.Machicolations
                .OrderBy(x => (x.X - _player.transform.position.x) * (x.X - _player.transform.position.x)
                            + (x.Z - _player.transform.position.z) * (x.Z - _player.transform.position.z)).First();
            double floor = Qazrix.Simulation.World.MachicolationColliders.FloorHeight(m, rules);
            Qazrix.Simulation.World.MachicolationColliders.WorldPoint(m, 0.0, 0.55, out double px, out double pz);
            var outward = new Vector3(m.NormalX, 0f, m.NormalZ);

            // Se deja caer desde el tejado, justo encima del suelo del matacán, entre el pretil y la pared.
            var body = _player.GetComponent<CharacterController>();
            body.enabled = false;
            _player.transform.position = new Vector3((float)px, m.RoofHeight + 0.2f, (float)pz);
            _player.transform.rotation = Quaternion.LookRotation(-outward, Vector3.up);
            Physics.SyncTransforms();
            body.enabled = true;

            float end = Time.time + 1.5f;
            while (Time.time < end) yield return null;
            Assert.IsTrue(_player.IsGrounded, "aterriza sobre el suelo del matacán");
            Assert.AreEqual((float)floor, _player.transform.position.y, 0.2f, "pisa la cara de arriba de la losa (el controlador queda ~13 cm por encima por su piel), no cae al vacío");
            Vector3 settled = _player.transform.position;

            // Se queda ahí: sin más empuje no se mueve ni cae.
            float later = Time.time + 1.5f;
            while (Time.time < later) yield return null;
            Assert.IsTrue(_player.IsGrounded, "sigue apoyado");
            Assert.Less(Vector3.Distance(settled, _player.transform.position), 0.05f, "se posa y no resbala");
            double along = (_player.transform.position.x - m.X) * m.TangentX + (_player.transform.position.z - m.Z) * m.TangentZ;
            double outFrom = (_player.transform.position.x - m.X) * m.NormalX + (_player.transform.position.z - m.Z) * m.NormalZ;
            Assert.That(outFrom, Is.InRange(-0.05, rules.Overhang), "dentro de la losa, entre la pared y el pretil");
            Assert.That(System.Math.Abs(along), Is.LessThan(m.Length * 0.5), "y a lo largo de su tramo");

            // Foto de frente y algo desde abajo: el hidalgo asomado en el matacán.
            var cameraObject = new GameObject("CamaraPosado");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.depth = 50f;
            camera.farClipPlane = 800f;
            Vector3 at = _player.transform.position + Vector3.up * 0.9f;
            cameraObject.transform.position = at + outward * 6f - Vector3.up * 1.8f;
            cameraObject.transform.LookAt(at);
            yield return null;
            yield return Capture("play_z01_matacan_posado.png");
            Object.Destroy(cameraObject);
        }

        [UnityTest]
        public IEnumerator Guard_PatrolsBetweenTheBuildings_FollowingTheNavMesh()
        {
            yield return LoadScene();
            // Hay dos guardias en la plaza: los dos deben patrullar.
            GuardController[] guards = Object.FindObjectsByType<GuardController>(FindObjectsSortMode.None);
            var starts = guards.Select(x => x.transform.position).ToArray();
            var followed = new bool[guards.Length];

            float end = Time.time + 4f;
            while (Time.time < end)
            {
                for (int i = 0; i < guards.Length; i++) followed[i] |= guards[i].IsFollowingPath;
                yield return null;
            }

            for (int i = 0; i < guards.Length; i++)
            {
                GuardController g = guards[i];
                bool onMesh = UnityEngine.AI.NavMesh.SamplePosition(g.transform.position, out UnityEngine.AI.NavMeshHit near, 2f, UnityEngine.AI.NavMesh.AllAreas);
                Debug.Log($"[Qazrix] {g.name}: sale de {starts[i]} y acaba en {g.transform.position} · estado {g.State} · sigue camino {followed[i]} · " +
                          $"malla a 2 m: {onMesh} ({(onMesh ? near.position.ToString() : "-")}) · sospecha {g.Awareness:0.00}");
                Assert.Greater(Vector3.Distance(starts[i], g.transform.position), 3f, $"{g.name} debería haber avanzado por su ruta (estado {g.State})");
                Assert.IsTrue(followed[i], $"{g.name} debería usar la malla de navegación");
                Vector3 p = g.transform.position;
                Assert.IsFalse(_city.HasBuildingAt(p.x, p.z), $"{g.name} no debe meterse dentro de un edificio");
            }
        }

        [UnityTest]
        public IEnumerator JumpKey_VaultsTheLowWall()
        {
            yield return LoadScene();
            ParkourMarker wall = Object.FindObjectsByType<ParkourMarker>(FindObjectsSortMode.None).First(m => m.Kind == ParkourKind.Vault);

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
            Assert.Greater(_player.transform.position.z, wall.transform.position.z + 0.4f, "debe quedar al otro lado del murete");
            Assert.IsTrue(_player.IsGrounded);
        }
    }
}
