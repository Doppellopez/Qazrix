using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Qazrix.Prototype;
using Qazrix.Simulation.World;
using Qazrix.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Qazrix.EditorTools
{
    /// <summary>
    /// Coloca una selección de gárgolas bajo las cornisas de Z01. Solo las de la Concatedral van en su edificio; las
    /// demás se reparten con libertad artística, agrupadas por su edificio de origen, cerca de la salida y de Bujaco.
    /// No llevan colisionador: no cambian la trepada ni la navegación.
    /// </summary>
    public static class Z01GargoylePlacer
    {
        public const string RootName = "Gargolas";
        private const string SheetPath = "Reports/QA/gargolas_z01.png";

        /// <summary>Centro de la nave de la Concatedral y de la Torre de Bujaco (GCP-02 y GCP-01).</summary>
        private static readonly Vector2 Concatedral = new Vector2(124.5f, 9.5f);
        private static readonly Vector2 Bujaco = new Vector2(43.29f, 77.51f);

        private static List<GargoyleGroup> Groups(Vector2 spawn, int concatedralPart)
        {
            GargoyleGroup Around(string name, Vector2 point, int[] ids, int[] eroded = null) => new GargoyleGroup
            {
                Name = name, X = point.x, Z = point.y, Ids = ids, ErodedIds = eroded ?? Array.Empty<int>()
            };

            return new List<GargoyleGroup>
            {
                new GargoyleGroup
                {
                    Name = "Concatedral", X = Concatedral.x, Z = Concatedral.y, Radius = 28.0, Spread = true, MinRise = 7f, MaxRise = 40f,
                    OnlyPart = concatedralPart,
                    Ids = new[] { 14, 15, 16, 17, 18, 19, 20, 21 }, ErodedIds = new[] { 15, 17, 19, 20 }
                },
                Around("Casa_del_Mono", spawn, new[] { 10, 11, 12 }),
                Around("San_Mateo", spawn, new[] { 7, 2, 5, 8 }, new[] { 2 }),
                Around("Godoy", Bujaco, new[] { 25, 26, 27 }),
                Around("Palacio_de_la_Isla", Bujaco, new[] { 28, 30 }),
                // Entre la plaza y la Concatedral, en el camino.
                Around("Camino", Vector2.Lerp(spawn, Concatedral, 0.5f), new[] { 9, 13, 32 }, new[] { 13 }),
            };
        }

        [MenuItem("Qazrix/Mapa/Colocar gárgolas en Z01", priority = 42)]
        public static void PlaceFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            int count = PlaceInSavedScene().Count;
            EditorUtility.DisplayDialog("Gárgolas", $"{count} gárgolas colocadas en Z01.", "Aceptar");
        }

        /// <summary>-executeMethod Qazrix.EditorTools.Z01GargoylePlacer.PlaceBatch (sin -nographics: también captura)</summary>
        public static void PlaceBatch()
        {
            int code = 0;
            try
            {
                CaptureSheet(PlaceInSavedScene());
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }
            EditorApplication.Exit(code);
        }

        /// <summary>Abre Z01, sustituye sus gárgolas (sin regenerar nada más) y la guarda.</summary>
        private static List<GargoyleSpot> PlaceInSavedScene()
        {
            Scene scene = EditorSceneManager.OpenScene(Z01GeoBuilder.ScenePath, OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();
            CityBlocks city = roots.FirstOrDefault(r => r.name == Z01GeoBuilder.CityName)?.GetComponent<CityBlocks>();
            Terrain terrain = roots.Select(r => r.GetComponentInChildren<Terrain>()).FirstOrDefault(t => t != null);
            PrototypePlayerController player = roots.Select(r => r.GetComponentInChildren<PrototypePlayerController>())
                .FirstOrDefault(p => p != null);
            if (city == null || terrain == null || player == null)
                throw new InvalidOperationException("Z01 no tiene ciudad, terreno o jugador: créala con Qazrix → Mapa.");
            if (city.Parts == null) city.Build();

            foreach (GameObject old in roots.Where(r => r.name == RootName)) UnityEngine.Object.DestroyImmediate(old);
            Vector3 start = player.transform.position;
            List<GargoyleSpot> spots = Place(city, terrain, new Vector2(start.x, start.z));

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException($"No se pudo guardar {Z01GeoBuilder.ScenePath}");
            return spots;
        }

        /// <summary>Instancia los prefabs en la escena abierta, bajo el objeto «Gargolas».</summary>
        public static List<GargoyleSpot> Place(CityBlocks city, Terrain terrain, Vector2 spawn)
        {
            var parts = city.Parts.ToList();
            float Ground(double x, double z) =>
                terrain.SampleHeight(new Vector3((float)x, 0f, (float)z)) + terrain.transform.position.y;

            var index = new FootprintIndex(parts);
            // El volumen del LiDAR que contiene la nave (en el juego va unido a sus vecinos).
            int nave = parts.IndexOf(index.At(Concatedral.x, Concatedral.y));
            if (nave < 0) throw new InvalidOperationException("No hay edificio en el punto de la Concatedral.");
            List<GargoyleGroup> groups = Groups(spawn, nave);
            List<GargoyleSpot> spots = GargoylePlanner.Place(parts, index, Ground, groups,
                new HashSet<int>(Z01GeoBuilder.CityWallParts), city.Machicolations);

            Dictionary<int, GargoyleBuilder.Entry> catalog = GargoyleBuilder.ReadCatalog().ToDictionary(e => e.Id);
            Transform root = new GameObject(RootName).transform;
            var log = new StringBuilder();
            foreach (GargoyleSpot spot in spots)
            {
                string name = catalog[spot.CatalogId].AssetName + (spot.Eroded ? GargoyleBuilder.ErodedSuffix : "");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{GargoyleBuilder.PrefabFolder}/{name}.prefab");
                if (prefab == null) throw new FileNotFoundException($"Falta el prefab {name}: Qazrix → Arte → Crear prefabs de gárgolas.");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                instance.transform.SetPositionAndRotation(
                    new Vector3((float)spot.X, spot.Height, (float)spot.Z),
                    Quaternion.LookRotation(new Vector3(spot.NormalX, 0f, spot.NormalZ)));
                log.AppendLine($"  {name} [{spot.Group}] en ({spot.X:0.0}, {spot.Height:0.0}, {spot.Z:0.0}), " +
                               $"{spot.Height - Ground(spot.X + spot.NormalX, spot.Z + spot.NormalZ):0.0} m sobre la calle");
            }
            int wanted = groups.Sum(g => g.Ids.Length);
            Debug.Log($"[Qazrix] Gárgolas: {spots.Count} de {wanted} colocadas\n{log}");
            if (spots.Count < wanted) Debug.LogWarning($"[Qazrix] Faltan {wanted - spots.Count} gárgolas por falta de pared libre.");
            return spots;
        }

        /// <summary>Un primer plano desde la calle de cada gárgola colocada, todos en una hoja.</summary>
        private static void CaptureSheet(List<GargoyleSpot> spots)
        {
            const int columns = 4, width = 480, height = 360;
            int rows = (spots.Count + columns - 1) / columns;
            var sheet = new Texture2D(columns * width, rows * height, TextureFormat.RGB24, false);
            sheet.SetPixels32(new Color32[columns * width * rows * height]);
            var go = new GameObject("PreviewCam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 40f;
            cam.farClipPlane = 2000f;
            cam.nearClipPlane = 0.05f;
            var rt = new RenderTexture(width, height, 24);
            try
            {
                cam.targetTexture = rt;
                for (int i = 0; i < spots.Count; i++)
                {
                    GargoyleSpot s = spots[i];
                    var normal = new Vector3(s.NormalX, 0f, s.NormalZ);
                    var side = new Vector3(-s.NormalZ, 0f, s.NormalX);
                    Vector3 target = new Vector3((float)s.X, s.Height + 0.25f, (float)s.Z) + normal * 0.35f;
                    Vector3 eye = target + normal * 2.1f + side * 1.5f + Vector3.down * 1.1f;
                    cam.transform.position = eye;
                    cam.transform.LookAt(target);
                    cam.Render();
                    RenderTexture.active = rt;
                    sheet.ReadPixels(new Rect(0, 0, width, height), i % columns * width, (rows - 1 - i / columns) * height);
                }
                sheet.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(SheetPath));
                File.WriteAllBytes(SheetPath, sheet.EncodeToPNG());

                // Vista desde la salida hacia la Concatedral y vista de la Concatedral.
                PrototypePreview.Capture("Reports/QA/gargolas_z01_concatedral.png",
                    new Vector3(Concatedral.x - 40f, 20f, Concatedral.y + 30f), new Vector3(Concatedral.x, 14f, Concatedral.y), 3000f);
            }
            finally
            {
                RenderTexture.active = null;
                cam.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(sheet);
            }
        }
    }
}
