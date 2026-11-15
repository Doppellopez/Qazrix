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
            Assert.Greater(_city.ChunkCount, 50);
            Assert.Greater(_city.TriangleCount, 200000);
            Assert.Less(_city.BuildSeconds, 10f, "levantar la ciudad no debe tardar más de 10 s");

            Vector3 p = _player.transform.position;
            Assert.IsTrue(_player.IsGrounded, "el jugador debe quedar apoyado en el suelo");
            Assert.IsTrue(_city.IsClear(p.x, p.z, 8f), "y nacer en un claro, no dentro ni pegado a un edificio");
            yield return Capture("play_z01_ciudad.png");
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
        public IEnumerator Guard_PatrolsBetweenTheBuildings_FollowingTheNavMesh()
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

            Assert.Greater(Vector3.Distance(start, _guard.transform.position), 3f, "debería haber avanzado por su ruta");
            Assert.IsTrue(followedPath, "debería usar la malla de navegación");
            Vector3 g = _guard.transform.position;
            Assert.IsFalse(_city.HasBuildingAt(g.x, g.z), "el guardia no debe meterse dentro de un edificio");
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
