using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Qazrix.Input;
using Qazrix.Prototype;
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
                CreateVolume(root, p, terrainGrid, surfaceGrid, meta, block);
            }

            // El jugador nace en el origen local, medio metro por encima del relieve.
            float groundY = (float)(terrainGrid.Sample(0, 0) - meta.originHeight);
            QazrixInput input = PrototypeSceneBuilder.CreateInput();
            PrototypePlayerController player = PrototypeSceneBuilder.CreatePlayer(body, pole);
            player.transform.position = new Vector3(0f, groundY + 0.5f, 0f);
            Transform cameraRig = PrototypeSceneBuilder.CreateCamera(player.transform, input);
            cameraRig.position = player.transform.position + new Vector3(0f, 2.9f, -4.3f);
            cameraRig.GetComponent<Camera>().farClipPlane = 3000f;
            player.Configure(cameraRig, player.transform.Find("Visual"), input);
            EditorUtility.SetDirty(player);

            var hud = new GameObject("HUD").AddComponent<PrototypeHud>();
            hud.Configure(player, input);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException($"No se pudo guardar la escena en {ScenePath}");

            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Qazrix] Escena Z01 creada: {ScenePath} · relieve {terrainGrid.Min:0.0}–{terrainGrid.Max:0.0} m · {points.Count} puntos de control · {terrain.name}");
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
