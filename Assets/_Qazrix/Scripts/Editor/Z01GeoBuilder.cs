using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Qazrix.Input;
using Qazrix.AI;
using Qazrix.Prototype;
using Qazrix.Simulation.Movement;
using Qazrix.Simulation.World;
using Qazrix.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Qazrix.EditorTools
{
    /// <summary>
    /// F0-B (spec §14.3): escena de prueba geoespacial de Z01. Importa el relieve MDT02 en metros
    /// locales, coloca un poste en cada punto de control y un volumen simple junto a él, y deja al
    /// jugador del prototipo en el origen para recorrerlo. Idempotente: se puede regenerar siempre,
    /// porque todo sale de los datos derivados y nada se edita a mano.
    /// </summary>
    public static class Z01GeoBuilder
    {
        public const string ScenePath = "Assets/_Qazrix/Scenes/Z01/Z01_Relieve.unity";
        public const string TerrainDataPath = "Assets/_Qazrix/Scenes/Z01/Z01_TerrainData.asset";
        public const string TerrainName = "Z01_Relieve";
        public const string NavMeshPath = "Assets/_Qazrix/Scenes/Z01/Z01_NavMesh.asset";

        /// <summary>Lado (m) del cuadrado alrededor del origen que recibe malla de navegación.</summary>
        public const float PlayAreaSide = 160f;
        public const string ControlPointsRoot = "Puntos_de_control";

        /// <summary>Lado de la ventana (m) donde se mide la altura de cada volumen.</summary>
        public const double VolumeWindow = 12.0;

        [MenuItem("Qazrix/Mapa/Crear escena Z01 (relieve CNIG)", priority = 40)]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        /// <summary>-executeMethod Qazrix.EditorTools.Z01GeoBuilder.BuildBatch</summary>
        public static void BuildBatch()
        {
            int code = 0;
            try { Build(); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        public static void Build()
        {
            Z01GeoData.Meta meta = Z01GeoData.LoadMeta();
            HeightGrid terrainGrid = Z01GeoData.LoadGrid(meta, meta.terrainFile);
            HeightGrid surfaceGrid = Z01GeoData.LoadGrid(meta, meta.surfaceFile);
            List<Z01GeoData.ControlPoint> points = Z01GeoData.LoadControlPoints().Where(p => p.IsControl).ToList();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sun = new GameObject("Sol").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

            Terrain terrain = CreateTerrain(meta, terrainGrid);

            Material pole = PrototypeSceneBuilder.GetOrCreateMaterial("Proto_Ruta", new Color(0.78f, 0.52f, 0.24f));
            Material block = PrototypeSceneBuilder.GetOrCreateMaterial("Proto_Obstaculo", new Color(0.62f, 0.60f, 0.56f));
            Material body = PrototypeSceneBuilder.GetOrCreateMaterial("Proto_Jugador", new Color(0.86f, 0.84f, 0.78f));

            Transform root = new GameObject(ControlPointsRoot).transform;
            foreach (Z01GeoData.ControlPoint p in points)
            {
                if (!terrainGrid.Contains(p.LocalX, p.LocalZ))
                {
                    Debug.LogWarning($"[Qazrix] {p.Id} queda fuera de Z01 y no se coloca.");
                    continue;
                }
                CreateControlPoint(root, p, meta.Origin, pole);
            }

            float Ground(float x, float z) => (float)(terrainGrid.Sample(x, z) - meta.originHeight);

            // --- Ciudad: volumetría de los edificios del casco antiguo (LiDAR PNOA 2018, D42) ---
            CityBlocks city = CreateCity(terrain);
            // Escalinatas de la Plaza Mayor: peldaños donde el relieve solo da una rampa (D44).
            CreateStairs(terrain);

            // --- Zona de juego de prueba en la Plaza Mayor (no es geometría de Cáceres) ---
            // Se busca un claro de 12 m de radio cerca del punto deseado para no nacer dentro de un edificio.
            Vector2 spawn = city.FindOpen(SpawnHint.x, SpawnHint.y, 12f);
            float sx = spawn.x, sz = spawn.y;

            // Dos obstáculos de parkour hundidos en el relieve, para que la pendiente no deje huecos:
            // un murete de 0,5 m para saltar y una plataforma de 1 m para trepar.
            Transform play = new GameObject("Zona_de_juego").transform;
            const float sunk = 2f;
            float wallTop = Ground(sx, sz + 6f) + 0.5f;
            PrototypeSceneBuilder.Box("Murete", play, block, new Vector3(sx, wallTop - (0.5f + sunk) * 0.5f, sz + 6f), new Vector3(3f, 0.5f + sunk, 0.4f))
                .gameObject.AddComponent<ParkourMarker>().Configure(ParkourKind.Vault);
            float deckTop = Ground(sx - 8f, sz + 6f) + 1f;
            PrototypeSceneBuilder.Box("Plataforma", play, pole, new Vector3(sx - 8f, deckTop - (1f + sunk) * 0.5f, sz + 6f), new Vector3(4f, 1f + sunk, 4f))
                .gameObject.AddComponent<ParkourMarker>().Configure(ParkourKind.Climb);

            // Malla de navegación en un cuadrado alrededor del punto de salida: incluye calles y edificios.
            var navigation = new GameObject("Navegacion");
            PrototypeSceneBuilder.BakeNavMesh(navigation,
                new Bounds(new Vector3(sx, Ground(sx, sz), sz), new Vector3(PlayAreaSide, 160f, PlayAreaSide)), NavMeshPath);

            // El jugador nace en el claro, medio metro por encima del relieve.
            float groundY = Ground(sx, sz);
            QazrixInput input = PrototypeSceneBuilder.CreateInput();
            PrototypePlayerController player = PrototypeSceneBuilder.CreatePlayer(body, pole);
            player.transform.position = new Vector3(sx, groundY + 0.5f, sz);
            Transform cameraRig = PrototypeSceneBuilder.CreateCamera(player.transform, input);
            cameraRig.position = player.transform.position + new Vector3(0f, 2.9f, -4.3f);
            cameraRig.GetComponent<Camera>().farClipPlane = 3000f;
            player.Configure(cameraRig, player.transform.Find("Visual"), input);
            EditorUtility.SetDirty(player);

            // Carta sobre la plataforma y checkpoint al otro lado del murete.
            PrototypeSceneBuilder.CreateLetter(play, pole, new Vector3(sx - 8f, deckTop + 0.25f, sz + 6f));
            PrototypeSceneBuilder.CreateCheckpoint(play, "tras el murete", new Vector3(sx, Ground(sx, sz + 9f) + 1f, sz + 9f), new Vector3(4f, 2.4f, 2f));

            var hud = new GameObject("HUD").AddComponent<PrototypeHud>();
            hud.Configure(player, input);

            // Guardia que da la vuelta a un cuadrado de 40 m alrededor del punto de salida.
            Material guardBody = PrototypeSceneBuilder.GetOrCreateMaterial("Proto_Guardia", new Color(0.45f, 0.20f, 0.18f));
            Material signal = PrototypeSceneBuilder.GetOrCreateMaterial("Proto_Senal", Color.white);
            // Cada esquina se lleva al punto despejado más cercano, para que ninguna caiga dentro de un edificio.
            var corners = new[] { new Vector2(20f, 20f), new Vector2(-20f, 20f), new Vector2(-20f, -20f), new Vector2(20f, -20f) };
            Vector3[] route = corners
                .Select(c => city.FindOpen(sx + c.x, sz + c.y, 1.5f))
                .Select(c => new Vector3(c.x, Ground(c.x, c.y) + 0.1f, c.y))
                .ToArray();
            GuardController guard = PrototypeSceneBuilder.CreateGuard("Guardia", guardBody, signal, player.GetComponent<StealthTarget>(), route);
            hud.ShowGuard(guard);
            EditorUtility.SetDirty(hud);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException($"No se pudo guardar la escena en {ScenePath}");

            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Qazrix] Escena Z01 creada: {ScenePath} · relieve {terrainGrid.Min:0.0}–{terrainGrid.Max:0.0} m · {points.Count} puntos de control · {terrain.name}");
            Debug.Log($"[Qazrix] Ciudad: {city.BuiltCells} m² construidos, {city.ChunkCount} trozos, {city.TriangleCount} triángulos, {city.BuildSeconds:0.00} s · salida en ({sx:0.0}, {sz:0.0})");
        }

        /// <summary>Punto deseado de salida del jugador: centro aproximado de la Plaza Mayor, en metros locales.</summary>
        private static readonly Vector2 SpawnHint = new Vector2(11f, 73f);

        public const string RoofsPath = "Assets/_Qazrix/Data/Zones/Z01/z01_tejados_1m.bytes";
        public const string RoofsMetaPath = "Assets/_Qazrix/Data/Zones/Z01/z01_tejados_1m.json";
        public const string CityName = "Ciudad";

        private static CityBlocks CreateCity(Terrain terrain)
        {
            var roofs = AssetDatabase.LoadAssetAtPath<TextAsset>(RoofsPath);
            var roofsMeta = AssetDatabase.LoadAssetAtPath<TextAsset>(RoofsMetaPath);
            if (roofs == null || roofsMeta == null)
                throw new FileNotFoundException($"Faltan los datos de tejados ({RoofsPath}). Ejecuta Tools/GIS/make_city_blocks.py (README).");

            // Teja de color plano y muro con textura de piedra (CC0-006). No es la apariencia de 1229.
            Material roof = PrototypeSceneBuilder.GetOrCreateMaterial("Ciudad_Tejado", new Color(0.62f, 0.36f, 0.26f));
            Material wall = CityWallMaterial.Ensure();

            var city = new GameObject(CityName).AddComponent<CityBlocks>();
            city.Configure(roofs, roofsMeta, terrain, roof, wall);
            city.Build(); // las mallas no se guardan en la escena; hacen falta ahora para la malla de navegación
            EditorUtility.SetDirty(city);
            return city;
        }

        public const string StairsPath = "Assets/_Qazrix/Data/Zones/Z01/z01_escalinata.bytes";
        public const string StairsMetaPath = "Assets/_Qazrix/Data/Zones/Z01/z01_escalinata.json";
        public const string StairsName = "Escalinata";

        /// <summary>
        /// Peldaños de 0,2 m generados por <c>Tools/GIS/make_plaza_stairs.py</c>. Usan el mismo mallador
        /// que los edificios, con un salto menor que la contrahuella para que cada peldaño sea plano.
        /// </summary>
        private static void CreateStairs(Terrain terrain)
        {
            var steps = AssetDatabase.LoadAssetAtPath<TextAsset>(StairsPath);
            var stepsMeta = AssetDatabase.LoadAssetAtPath<TextAsset>(StairsMetaPath);
            if (steps == null || stepsMeta == null)
                throw new FileNotFoundException($"Faltan los datos de la escalinata ({StairsPath}). Ejecuta Tools/GIS/make_plaza_stairs.py (README).");

            Material stone = CityWallMaterial.EnsureSteps(); // losa de granito (CC0-008)
            var stairs = new GameObject(StairsName).AddComponent<CityBlocks>();
            stairs.Configure(steps, stepsMeta, terrain, stone, stone, 0.05f);
            stairs.Climbable = false; // se suben andando, no trepando
            stairs.Build(); // hacen falta ahora para la malla de navegación
            EditorUtility.SetDirty(stairs);
            Debug.Log($"[Qazrix] Escalinata: {stairs.BuiltCells * 0.04f:0} m² de peldaños, {stairs.TriangleCount} triángulos");
        }

        private static Terrain CreateTerrain(Z01GeoData.Meta meta, HeightGrid grid)
        {
            int n = grid.Samples;
            float range = Mathf.Max(grid.Max - grid.Min, 1f);
            float side = (float)(2 * grid.HalfExtent);

            // Unity indexa las alturas como [fila desde el sur, columna desde el oeste], de 0 a 1.
            var heights = new float[n, n];
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                    heights[z, x] = (grid.At(x, z) - grid.Min) / range;

            Directory.CreateDirectory(Path.GetDirectoryName(TerrainDataPath));
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            if (data == null)
            {
                data = new TerrainData();
                AssetDatabase.CreateAsset(data, TerrainDataPath);
            }

            data.heightmapResolution = n;                  // antes que el tamaño: cambiarla lo reinicia
            data.size = new Vector3(side, range, side);
            data.SetHeights(0, 0, heights);
            EditorUtility.SetDirty(data);

            GameObject go = Terrain.CreateTerrainGameObject(data);
            go.name = TerrainName;
            go.isStatic = true;
            go.transform.position = new Vector3(-side * 0.5f, (float)(grid.Min - meta.originHeight), -side * 0.5f);

            var terrain = go.GetComponent<Terrain>();
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline != null && pipeline.defaultTerrainMaterial != null)
                terrain.materialTemplate = pipeline.defaultTerrainMaterial;
            terrain.heightmapPixelError = 2f;
            return terrain;
        }

        private static void CreateControlPoint(Transform root, Z01GeoData.ControlPoint p, GeoOrigin origin, Material material)
        {
            // La posición se calcula aquí desde las coordenadas fuente, no se copia de las columnas local_*:
            // así el informe puede comparar la conversión de C# con la del script GIS.
            Vector3 position = origin.ToUnity(p.Easting, p.Northing, p.TerrainHeight);

            var marker = new GameObject($"{p.Id} {p.Name}");
            marker.transform.SetParent(root, false);
            marker.transform.position = position;

            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Poste";
            UnityEngine.Object.DestroyImmediate(pole.GetComponent<Collider>());
            pole.transform.SetParent(marker.transform, false);
            pole.transform.localPosition = new Vector3(0f, 20f, 0f);
            pole.transform.localScale = new Vector3(0.4f, 20f, 0.4f); // 40 m de alto: se ve desde lejos
            pole.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        /// <summary>
        /// Volumen indicativo: base fija de <see cref="VolumeWindow"/> m (NO es la huella real) y altura
        /// igual a la mayor diferencia MDS02 − MDT02 dentro de esa ventana. Es una estimación etiquetada
        /// (spec §5.6), no una medida del edificio.
        /// </summary>
        private static void CreateVolume(Transform root, Z01GeoData.ControlPoint p, HeightGrid terrain, HeightGrid surface,
            Z01GeoData.Meta meta, Material material)
        {
            MeasureVolume(p, terrain, surface, out double baseHeight, out double height);
            double sizeX = VolumeWindow, sizeZ = VolumeWindow;
            if (p.BuiltHeight > 0 && p.SizeEastWest > 0 && p.SizeNorthSouth > 0)
            {
                // Punto sacado del LiDAR: caja envolvente y altura maxima del cuerpo medido (no es la huella real).
                sizeX = p.SizeEastWest;
                sizeZ = p.SizeNorthSouth;
                height = p.BuiltHeight;
                baseHeight = p.TerrainHeight;
            }
            if (height < 0.5) return; // en ese punto no hay nada construido que representar

            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = $"Volumen {p.Id} ({height:0.0} m)";
            box.isStatic = true;
            box.transform.SetParent(root, false);
            box.transform.position = new Vector3((float)p.LocalX, (float)(baseHeight - meta.originHeight + height * 0.5), (float)p.LocalZ);
            box.transform.localScale = new Vector3((float)sizeX, (float)height, (float)sizeZ);
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        public static void MeasureVolume(Z01GeoData.ControlPoint p, HeightGrid terrain, HeightGrid surface,
            out double baseHeight, out double height)
        {
            double half = VolumeWindow * 0.5;
            int x0 = terrain.NearestIndex(p.LocalX - half), x1 = terrain.NearestIndex(p.LocalX + half);
            int z0 = terrain.NearestIndex(p.LocalZ - half), z1 = terrain.NearestIndex(p.LocalZ + half);

            baseHeight = double.MaxValue;
            height = 0;
            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    baseHeight = Math.Min(baseHeight, terrain.At(x, z));
                    height = Math.Max(height, surface.At(x, z) - terrain.At(x, z));
                }
            }
        }

        private static void AddToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
                .Where(s => s.path != ScenePath && File.Exists(s.path))
                .ToList();
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
