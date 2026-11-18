using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Qazrix.AI;
using Qazrix.Prototype;
using Qazrix.Simulation.AI;
using Qazrix.Simulation.Movement;
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
        public IEnumerator RandomLetter_IsInTheOuterRing_Clear_AndReachableOnFoot()
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
            Assert.GreaterOrEqual(flat, 15f, "fuera del claro de la plaza");
            Assert.LessOrEqual(flat, 56f);
            Assert.IsTrue(_city.IsClear(spot.x, spot.z, 1.2f), "no está dentro de un edificio");

            var path = new UnityEngine.AI.NavMeshPath();
            Assert.IsTrue(UnityEngine.AI.NavMesh.CalculatePath(p, spot, UnityEngine.AI.NavMesh.AllAreas, path));
            Assert.AreEqual(UnityEngine.AI.NavMeshPathStatus.PathComplete, path.status, "se puede llegar andando");
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
            Assert.AreEqual(1, panel.MarkCount, "con una cruz roja");

            var entry = interactor.Inventory.Entries[0];
            Vector3 p = _player.transform.position;
            float away = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(entry.MarkX, entry.MarkZ));
            Debug.Log($"[Qazrix] Cruz roja a {away:0} m del jugador, en ({entry.MarkX:0.0}, {entry.MarkZ:0.0})");
            Assert.Greater(away, 25f, "no está pegada al jugador");
            Assert.IsTrue(_city.IsClear(entry.MarkX, entry.MarkZ, 1.2f), "ni dentro de un edificio");
            Assert.IsFalse(_city.IsClear(entry.MarkX, entry.MarkZ, 20f), "está en el interior del casco, con edificios alrededor");

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
        public IEnumerator PlazaMayor_HasTwoGuards_BothOnTheNavMesh_AndFarFromEachOther()
        {
            yield return LoadScene();
            GuardController[] guards = Object.FindObjectsByType<GuardController>(FindObjectsSortMode.None);
            Assert.AreEqual(2, guards.Length, "la plaza tiene dos guardias");
            foreach (GuardController g in guards)
            {
                Assert.IsTrue(UnityEngine.AI.NavMesh.SamplePosition(g.transform.position, out _, 1.5f, UnityEngine.AI.NavMesh.AllAreas),
                    $"{g.name} debe estar sobre la malla de navegación");
                Assert.IsTrue(_city.IsClear(g.transform.position.x, g.transform.position.z, 0.5f), $"{g.name} no debe nacer dentro de un edificio");
                Assert.Less(Vector3.Distance(g.transform.position, _player.transform.position), 60f, $"{g.name} está en la plaza");
            }
            Assert.Greater(Vector3.Distance(guards[0].transform.position, guards[1].transform.position), 3f, "no nacen apilados");
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
