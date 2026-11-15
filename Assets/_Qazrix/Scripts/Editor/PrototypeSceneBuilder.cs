using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Qazrix.AI;
using Qazrix.Gameplay;
using Qazrix.Input;
using Qazrix.Prototype;
using Qazrix.Simulation.Movement;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Qazrix.EditorTools
{
    /// <summary>
    /// Crea la escena Prototype con API de Editor (nunca YAML a mano): suelo, jugador, cámara, luz
    /// y un pequeño circuito de obstáculos en gris. No pisa una escena existente salvo que se pida.
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        public const string ScenePath = "Assets/_Qazrix/Scenes/Prototype/Prototype.unity";
        public const string NavMeshPath = "Assets/_Qazrix/Scenes/Prototype/Prototype_NavMesh.asset";
        private const string MaterialFolder = "Assets/_Qazrix/Art/Prototype";

        [MenuItem("Qazrix/Prototipo/Crear o abrir escena Prototype", priority = 20)]
        public static void CreateOrOpen()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Debug.Log($"[Qazrix] Escena abierta: {ScenePath}");
                return;
            }

            Build();
        }

        [MenuItem("Qazrix/Prototipo/Regenerar escena Prototype (sobrescribe)", priority = 21)]
        public static void Regenerate()
        {
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog(
                    "Regenerar Prototype",
                    "Se va a sobrescribir la escena Prototype. Los cambios hechos a mano en ella se perderán.",
                    "Sobrescribir", "Cancelar"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        /// <summary>-executeMethod Qazrix.EditorTools.PrototypeSceneBuilder.CreateBatch (crea la escena solo si falta)</summary>
        public static void CreateBatch()
        {
            int code = 0;
            try
            {
                if (File.Exists(ScenePath)) Debug.Log($"[Qazrix] La escena ya existe, no se toca: {ScenePath}");
                else Build();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }
            EditorApplication.Exit(code);
        }

        /// <summary>-executeMethod Qazrix.EditorTools.PrototypeSceneBuilder.RebuildBatch (sobrescribe la escena)</summary>
        public static void RebuildBatch()
        {
            int code = 0;
            try { Build(); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        public static void Build()
        {
            ProjectInfo.Apply();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material ground = GetOrCreateMaterial("Proto_Suelo", new Color(0.42f, 0.44f, 0.40f));
            Material block = GetOrCreateMaterial("Proto_Obstaculo", new Color(0.62f, 0.60f, 0.56f));
            Material accent = GetOrCreateMaterial("Proto_Ruta", new Color(0.78f, 0.52f, 0.24f));
            Material body = GetOrCreateMaterial("Proto_Jugador", new Color(0.86f, 0.84f, 0.78f));

            CreateLight();

            var level = new GameObject("Circuito").transform;
            Box("Suelo", level, ground, new Vector3(0f, -0.5f, 0f), new Vector3(60f, 1f, 60f));
            Box("Obstaculo_bajo", level, block, new Vector3(0f, 0.25f, 5f), new Vector3(3f, 0.5f, 0.4f))
                .gameObject.AddComponent<ParkourMarker>().Configure(ParkourKind.Vault);
            Box("Escalon", level, block, new Vector3(-6f, 0.15f, 5f), new Vector3(3f, 0.3f, 2f));
            Box("Plataforma", level, accent, new Vector3(-6f, 0.5f, 9f), new Vector3(4f, 1f, 4f))
                .gameObject.AddComponent<ParkourMarker>().Configure(ParkourKind.Climb);

            // Túnel de 1,3 m de alto: solo se pasa agachado (1,0 m) y dentro no cabe de pie (1,8 m).
            Box("Tunel_pared_izq", level, block, new Vector3(5.35f, 0.65f, 8f), new Vector3(0.3f, 1.3f, 5f));
            Box("Tunel_pared_der", level, block, new Vector3(7.65f, 0.65f, 8f), new Vector3(0.3f, 1.3f, 5f));
            Box("Tunel_techo", level, accent, new Vector3(6.5f, 1.5f, 8f), new Vector3(2.6f, 0.4f, 5f));

            // Rampa de 20° que sube 2,7 m hasta un mirador.
            Transform ramp = Box("Rampa", level, block, new Vector3(12f, 1.23f, 6f), new Vector3(3f, 0.3f, 8f));
            ramp.rotation = Quaternion.Euler(-20f, 0f, 0f);
            Box("Mirador", level, accent, new Vector3(12f, 1.35f, 11.7f), new Vector3(4f, 2.7f, 4f));

            // Pared alta para comprobar que la cámara no la atraviesa.
            Box("Pared_alta", level, block, new Vector3(0f, 2.5f, -8f), new Vector3(10f, 5f, 0.5f));

            // La malla se calcula con el circuito ya colocado y antes de crear a los personajes.
            BakeNavMesh(level.gameObject, new Bounds(Vector3.zero, new Vector3(70f, 20f, 70f)), NavMeshPath);

            // Objetivo y checkpoint del circuito: una carta en lo alto del mirador y un punto de
            // reaparición a la salida del túnel.
            CreateLetter(level, accent, new Vector3(12f, 3.0f, 12.3f));
            CreateCheckpoint(level, "salida del túnel", new Vector3(6.5f, 1f, 11.6f), new Vector3(3f, 2f, 1.6f));

            QazrixInput input = CreateInput();
            PrototypePlayerController player = CreatePlayer(body, accent);
            Transform cameraRig = CreateCamera(player.transform, input);
            player.Configure(cameraRig, player.transform.Find("Visual"), input);
            EditorUtility.SetDirty(player);

            var hud = new GameObject("HUD").AddComponent<PrototypeHud>();
            hud.Configure(player, input);

            // Guardia que rodea el circuito. Empieza en la esquina noreste, lejos y de espaldas al jugador.
            Material guardBody = GetOrCreateMaterial("Proto_Guardia", new Color(0.45f, 0.20f, 0.18f));
            Material signal = GetOrCreateMaterial("Proto_Senal", Color.white);
            GuardController guard = CreateGuard("Guardia", guardBody, signal, player.GetComponent<StealthTarget>(), new[]
            {
                new Vector3(16f, 0f, 17f), new Vector3(-12f, 0f, 17f), new Vector3(-12f, 0f, -5f), new Vector3(16f, 0f, -5f)
            });
            hud.ShowGuard(guard);
            EditorUtility.SetDirty(hud);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException($"No se pudo guardar la escena en {ScenePath}");

            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Qazrix] Escena Prototype creada: {ScenePath}");
        }

        private static void CreateLight()
        {
            var go = new GameObject("Sol");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            light.color = new Color(1f, 0.96f, 0.88f);
            go.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        }

        internal static PrototypePlayerController CreatePlayer(Material body, Material accent)
        {
            var go = new GameObject("Jugador");
            go.transform.position = Vector3.zero;

            var cc = go.AddComponent<CharacterController>();
            cc.radius = 0.3f;
            cc.height = 1.8f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.35f;
            cc.slopeLimit = 45f;
            cc.skinWidth = 0.03f;

            // «Visual» es el nodo que el controlador escala al agacharse, con las medidas de una cápsula
            // de Unity. El modelo del hidalgo cuelga de él.
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            visual.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
            CharacterWalkAnimator animator = HidalgoBuilder.Build(visual.transform, go.transform, cc);

            go.AddComponent<PlayerParkour>();
            go.AddComponent<PlayerWallClimb>();
            go.AddComponent<PlayerInteractor>();
            var controller = go.AddComponent<PrototypePlayerController>();
            go.AddComponent<PlayerStealthTarget>();
            EditorUtility.SetDirty(animator);
            return controller;
        }

        /// <summary>
        /// Malla de navegación del circuito para un personaje como el guardia: 0,4 m de radio, 1,8 m de
        /// alto y escalones de hasta 0,3 m (lo que sube su CharacterController). Se guarda como asset y
        /// la carga un NavMeshSurface al arrancar la escena.
        /// </summary>
        /// <param name="root">Objeto en el origen y sin girar que llevará el NavMeshSurface.</param>
        internal static void BakeNavMesh(GameObject root, Bounds bounds, string assetPath)
        {
            Physics.SyncTransforms();
            var sources = new List<NavMeshBuildSource>();
            UnityEngine.AI.NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0,
                new List<NavMeshBuildMarkup>(), sources);

            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.4f;
            settings.agentHeight = 1.8f;
            settings.agentClimb = 0.3f;
            settings.agentSlope = 40f;

            NavMeshData data = UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null) throw new InvalidOperationException("No se pudo calcular la malla de navegación.");
            data.name = Path.GetFileNameWithoutExtension(assetPath);

            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(data, assetPath);

            var surface = root.AddComponent<NavMeshSurface>();
            surface.navMeshData = data;
            EditorUtility.SetDirty(surface);
        }

        /// <summary>Carta que se recoge con la tecla de interactuar.</summary>
        internal static LetterPickup CreateLetter(Transform parent, Material material, Vector3 position)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Carta";
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, 25f, 0f);
            go.transform.localScale = new Vector3(0.35f, 0.06f, 0.25f);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.GetComponent<BoxCollider>().isTrigger = true; // se encuentra, pero no estorba el paso

            var letter = go.AddComponent<LetterPickup>();
            letter.SetPrompt("recoger la carta");
            return letter;
        }

        /// <summary>Zona invisible que fija el punto de reaparición del jugador al entrar en ella.</summary>
        internal static CheckpointZone CreateCheckpoint(Transform parent, string label, Vector3 center, Vector3 size)
        {
            var go = new GameObject("Checkpoint_" + label.Replace(' ', '_'));
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            box.isTrigger = true;

            // El jugador reaparece a ras de suelo en el centro de la zona.
            var point = new GameObject("Reaparicion").transform;
            point.SetParent(go.transform, false);
            point.localPosition = new Vector3(0f, -size.y * 0.5f + 0.1f, 0f);

            var zone = go.AddComponent<CheckpointZone>();
            zone.Configure(label, point);
            return zone;
        }

        /// <summary>Guardia con su ruta de patrulla. Empieza en el primer punto, mirando hacia el segundo.</summary>
        internal static GuardController CreateGuard(string name, Material body, Material signal, StealthTarget target, Vector3[] route)
        {
            var routeRoot = new GameObject(name + "_Ruta").transform;
            var points = new Transform[route.Length];
            for (int i = 0; i < route.Length; i++)
            {
                points[i] = new GameObject($"Punto_{i + 1}").transform;
                points[i].SetParent(routeRoot, false);
                points[i].position = route[i];
            }

            var go = new GameObject(name);
            go.transform.position = route[0];
            if (route.Length > 1) go.transform.rotation = Quaternion.LookRotation(route[1] - route[0], Vector3.up);

            var cc = go.AddComponent<CharacterController>();
            cc.radius = 0.3f;
            cc.height = 1.8f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.35f;
            cc.skinWidth = 0.03f;

            // Modelo: guardia andalusí corpulento con sable curvo (GuardModelBuilder, D43).
            EditorUtility.SetDirty(GuardModelBuilder.Build(go.transform, cc));

            // Señal sobre la cabeza: color por estado y tamaño por sospecha.
            GameObject mark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mark.name = "Senal";
            UnityEngine.Object.DestroyImmediate(mark.GetComponent<Collider>());
            mark.transform.SetParent(go.transform, false);
            mark.transform.localPosition = new Vector3(0f, 2.35f, 0f); // por encima del turbante
            mark.transform.localScale = Vector3.one * 0.2f;
            var markRenderer = mark.GetComponent<MeshRenderer>();
            markRenderer.sharedMaterial = signal;
            markRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var guard = go.AddComponent<GuardController>();
            guard.Configure(target, points, markRenderer);
            EditorUtility.SetDirty(guard);
            return guard;
        }

        /// <summary>Objeto «Entrada»: lector de Input Actions y panel de reasignación de teclas.</summary>
        internal static QazrixInput CreateInput()
        {
            var go = new GameObject("Entrada");
            var reader = go.AddComponent<QazrixInput>();
            reader.Configure(InputActionsBuilder.Ensure());
            go.AddComponent<ControlsPanel>().Configure(reader);
            go.AddComponent<SceneSwitcher>().Configure(reader);
            EditorUtility.SetDirty(go);
            return reader;
        }

        internal static Transform CreateCamera(Transform target, QazrixInput input)
        {
            var go = new GameObject("Camara") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 500f;
            go.AddComponent<AudioListener>();
            go.transform.position = new Vector3(0f, 2.9f, -4.3f);
            go.transform.rotation = Quaternion.Euler(18f, 0f, 0f);

            var orbit = go.AddComponent<PrototypeOrbitCamera>();
            orbit.Configure(target, input);
            EditorUtility.SetDirty(orbit);
            return go.transform;
        }

        internal static Transform Box(string name, Transform parent, Material material, Vector3 position, Vector3 size)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.isStatic = true;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go.transform;
        }

        /// <summary>Material con el shader por defecto del pipeline activo, así vale con HDRP, URP o Built-in.</summary>
        internal static Material GetOrCreateMaterial(string name, Color color, string folder = MaterialFolder)
        {
            Directory.CreateDirectory(folder);
            string path = $"{folder}/{name}.mat";

            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            Shader shader = pipeline != null && pipeline.defaultMaterial != null
                ? pipeline.defaultMaterial.shader
                : Shader.Find("Standard");

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            foreach (string property in new[] { "_BaseColor", "_Color" })
                if (material.HasProperty(property)) material.SetColor(property, color);

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void AddToBuildSettings()
        {
            // Quita entradas a escenas que ya no existen y deja Prototype la primera.
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.path != ScenePath && File.Exists(s.path))
                .ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
