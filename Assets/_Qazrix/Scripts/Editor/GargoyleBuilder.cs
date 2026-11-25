using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Qazrix.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Qazrix.EditorTools
{
    /// <summary>
    /// Convierte los modelos de `Tools/Art/gargoyles/make_gargoyles.py` (FBX con dos niveles de detalle y
    /// su textura) en prefabs independientes: uno por gárgola del catálogo y otro por su variante
    /// erosionada. Cada prefab tiene el origen en la cara del muro y sale hacia +Z, sin colisionador.
    /// </summary>
    public static class GargoyleBuilder
    {
        public const string Root = "Assets/_Qazrix/Art/Environment/Gargoyles";
        public const string ModelFolder = Root + "/Models";
        public const string TextureFolder = Root + "/Textures";
        public const string MaterialFolder = Root + "/Materials";
        public const string PrefabFolder = Root + "/Prefabs";
        public const string CatalogPath = "Tools/Art/gargoyles/catalogo.csv";
        public const string ErodedSuffix = "_Erosionada";

        /// <summary>Altura en pantalla (0-1) a partir de la cual se ve el detalle alto y por debajo de la cual no se dibuja.</summary>
        private const float NearTransition = 0.09f, CullTransition = 0.006f;

        public readonly struct Entry
        {
            public readonly int Id;
            public readonly string Building, Motif, Source, Author;

            public Entry(int id, string building, string motif, string source, string author)
            {
                Id = id;
                Building = building;
                Motif = motif;
                Source = source;
                Author = author;
            }

            public string AssetName => $"Gargola_{Id:00}_{Camel(Building)}_{Camel(Motif)}";

            private static string Camel(string text) =>
                string.Concat(text.Split('_').Where(w => w.Length > 0).Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1)));
        }

        public static List<Entry> ReadCatalog()
        {
            return File.ReadAllLines(CatalogPath, Encoding.UTF8)
                .Skip(1)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Split(',')) // el catálogo no lleva comas dentro de los campos
                .Select(f => new Entry(int.Parse(f[0]), f[1], f[2], f[3], f[4].Trim()))
                .ToList();
        }

        [MenuItem("Qazrix/Arte/Crear prefabs de gárgolas", priority = 61)]
        public static void BuildFromMenu()
        {
            int count = Build();
            EditorUtility.DisplayDialog("Gárgolas", $"{count} prefabs en {PrefabFolder}", "Aceptar");
        }

        /// <summary>-executeMethod Qazrix.EditorTools.GargoyleBuilder.BuildBatch (sin -nographics: también captura las hojas)</summary>
        public static void BuildBatch()
        {
            int code = 0;
            try
            {
                Build();
                CaptureSheets();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }
            EditorApplication.Exit(code);
        }

        public static int Build()
        {
            AssetDatabase.Refresh();
            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory(PrefabFolder);
            AssetDatabase.Refresh();

            int count = 0;
            foreach (Entry entry in ReadCatalog())
            {
                foreach (bool eroded in new[] { false, true })
                {
                    BuildPrefab(entry, eroded);
                    count++;
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Qazrix] {count} prefabs de gárgolas en {PrefabFolder}");
            return count;
        }

        private static void BuildPrefab(Entry entry, bool eroded)
        {
            string name = entry.AssetName + (eroded ? ErodedSuffix : "");
            string modelPath = $"{ModelFolder}/{name}.fbx";
            string texturePath = $"{TextureFolder}/{name}.jpg";
            if (!File.Exists(modelPath) || !File.Exists(texturePath))
                throw new FileNotFoundException($"Falta el modelo o la textura de {name}: ejecuta Tools/Art/gargoyles/make_gargoyles.py.");

            ConfigureModel(modelPath);
            ConfigureTexture(texturePath);

            Material material = PrototypeSceneBuilder.GetOrCreateMaterial(name, Color.white, MaterialFolder);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            foreach (string property in new[] { "_BaseMap", "_MainTex" })
                if (material.HasProperty(property)) material.SetTexture(property, texture);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.08f); // granito mate
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);

            var root = new GameObject(name);
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                model.name = "Modelo";
                model.transform.SetParent(root.transform, false);
                // El FBX trae su propio LODGroup (nombres _LOD0/_LOD1): se sustituye por uno en la raíz.
                foreach (LODGroup imported in model.GetComponentsInChildren<LODGroup>())
                    UnityEngine.Object.DestroyImmediate(imported);

                MeshRenderer[] renderers = model.GetComponentsInChildren<MeshRenderer>();
                MeshRenderer high = renderers.Single(r => r.name.EndsWith("_LOD0", StringComparison.Ordinal));
                MeshRenderer low = renderers.Single(r => r.name.EndsWith("_LOD1", StringComparison.Ordinal));
                high.sharedMaterial = material;
                low.sharedMaterial = material;

                var group = root.AddComponent<LODGroup>();
                group.SetLODs(new[]
                {
                    new LOD(NearTransition, new Renderer[] { high }),
                    new LOD(CullTransition, new Renderer[] { low })
                });
                group.RecalculateBounds();

                root.AddComponent<GargoyleInfo>().Describe(entry.Id, entry.Building, entry.Motif, entry.Source, entry.Author, eroded);
                PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{name}.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ConfigureModel(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer.materialImportMode == ModelImporterMaterialImportMode.None && !importer.importAnimation) return;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        private static void ConfigureTexture(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.maxTextureSize == 512 && !importer.alphaIsTransparency && importer.alphaSource == TextureImporterAlphaSource.None) return;
            importer.maxTextureSize = 512;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.SaveAndReimport();
        }

        /// <summary>Hojas de contactos: las 32 colocadas sobre un paramento, tal cual y erosionadas.</summary>
        public static void CaptureSheets()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); // no se guarda

            var sun = new GameObject("Sol").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.5f;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(42f, 215f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.46f, 0.50f, 0.58f);

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Paramento";
            wall.transform.position = new Vector3(0f, 0f, -0.35f);
            wall.transform.localScale = new Vector3(40f, 24f, 0.4f);
            Material stone = new Material(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.defaultMaterial);
            stone.SetColor("_BaseColor", new Color(0.78f, 0.74f, 0.66f));
            stone.SetFloat("_Smoothness", 0.05f);
            wall.GetComponent<MeshRenderer>().sharedMaterial = stone;

            const int columns = 8;
            const float stepX = 1.75f, stepY = 1.45f;
            List<Entry> entries = ReadCatalog();
            foreach (bool eroded in new[] { false, true })
            {
                var placed = new List<GameObject>();
                for (int i = 0; i < entries.Count; i++)
                {
                    string name = entries[i].AssetName + (eroded ? ErodedSuffix : "");
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{name}.prefab");
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    // La cámara mira hacia -Z y su derecha es -X: la numeración se lee de izquierda a derecha.
                    float x = ((columns - 1) / 2f - i % columns) * stepX;
                    float y = (1.5f - i / columns) * stepY - 0.35f;
                    instance.transform.SetPositionAndRotation(new Vector3(x, y, -0.15f), Quaternion.Euler(0f, 28f, 0f));
                    instance.GetComponent<LODGroup>().ForceLOD(0);
                    placed.Add(instance);
                }
                string path = eroded ? "Reports/QA/gargolas_erosionadas.png" : "Reports/QA/gargolas.png";
                PrototypePreview.Capture(path, new Vector3(0f, -1.2f, 8.7f), new Vector3(0f, 0f, 0f));
                placed.ForEach(UnityEngine.Object.DestroyImmediate);
            }
            UnityEngine.Object.DestroyImmediate(stone);
        }
    }
}
