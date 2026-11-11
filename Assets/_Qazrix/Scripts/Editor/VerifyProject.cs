using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Qazrix.Input;
using Qazrix.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Qazrix.EditorTools
{
    /// <summary>
    /// Comprobación del proyecto (spec §8.6): compilación, ajustes esenciales, escena principal,
    /// documentación y licencias. En headless sale con código distinto de cero si hay errores.
    /// </summary>
    public static class VerifyProject
    {
        public const string ReportPath = "Reports/QA/verify-project-latest.md";

        private enum Level { Ok, Info, Warning, Error }

        private readonly struct Check
        {
            public readonly string Name;
            public readonly Level Level;
            public readonly string Detail;

            public Check(string name, Level level, string detail)
            {
                Name = name;
                Level = level;
                Detail = detail;
            }
        }

        private static readonly string[] RequiredDocs =
        {
            "README.md", "CLAUDE.md", "QAZRIX_SPEC_v1.md", "Design/DecisionLog.md",
            "Research/Datasets.md", "Research/ASSET_LICENSES.csv"
        };

        // Tipos de archivo que casi siempre vienen de fuera y necesitan fila de licencia (spec §9.3).
        private static readonly HashSet<string> ExternalExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".tga", ".tif", ".tiff", ".psd", ".exr", ".hdr",
            ".fbx", ".obj", ".glb", ".gltf", ".blend", ".wav", ".mp3", ".ogg", ".mp4"
        };

        [MenuItem("Qazrix/Verificar proyecto", priority = 1)]
        public static void RunFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previous = SceneManager.GetActiveScene().path;

            int errors = Run("editor");

            if (!string.IsNullOrEmpty(previous) && File.Exists(previous))
                EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);

            EditorUtility.DisplayDialog("Verificar proyecto",
                errors == 0 ? $"Sin errores. Informe en {ReportPath}" : $"{errors} error(es). Informe en {ReportPath}",
                "Aceptar");
        }

        /// <summary>-executeMethod Qazrix.EditorTools.VerifyProject.RunBatch (código de salida = 0 si no hay errores)</summary>
        public static void RunBatch()
        {
            int code;
            try
            {
                code = Run("headless") == 0 ? 0 : 1;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                code = 2;
            }
            EditorApplication.Exit(code);
        }

        private static int Run(string mode)
        {
            var checks = new List<Check>();

            checks.Add(EditorUtility.scriptCompilationFailed
                ? new Check("Compilación", Level.Error, "Hay errores de compilación.")
                : new Check("Compilación", Level.Ok, "Sin errores de compilación."));

            checks.Add(Application.unityVersion.StartsWith(ProjectInfo.ExpectedUnityPrefix, StringComparison.Ordinal)
                ? new Check("Versión de Unity", Level.Ok, $"{Application.unityVersion} (Unity 6.3 LTS).")
                : new Check("Versión de Unity", Level.Error, $"{Application.unityVersion}; se espera {ProjectInfo.ExpectedUnityPrefix}x (D05)."));

            checks.Add(PlayerSettings.bundleVersion == ProjectInfo.GameVersion && PlayerSettings.productName == ProjectInfo.ProductName
                ? new Check("Versión del juego", Level.Ok, $"{PlayerSettings.productName} v{PlayerSettings.bundleVersion}")
                : new Check("Versión del juego", Level.Error,
                    $"{PlayerSettings.productName} v{PlayerSettings.bundleVersion}; usa el menú Qazrix → Aplicar versión y nombre del producto."));

            checks.Add(CheckPipeline());

#if ENABLE_INPUT_SYSTEM
            checks.Add(new Check("Input System", Level.Ok, "Paquete Input System activo."));
#else
            checks.Add(new Check("Input System", Level.Error, "Active Input Handling debe ser Input System Package o Both."));
#endif

            checks.Add(EditorSettings.serializationMode == SerializationMode.ForceText
                ? new Check("Serialización", Level.Ok, "Force Text.")
                : new Check("Serialización", Level.Error, $"{EditorSettings.serializationMode}; debe ser Force Text."));

            checks.Add(VersionControlSettings.mode == "Visible Meta Files"
                ? new Check("Archivos .meta", Level.Ok, "Visibles.")
                : new Check("Archivos .meta", Level.Error, $"Modo «{VersionControlSettings.mode}»; debe ser Visible Meta Files."));

            checks.Add(File.Exists(InputActionsBuilder.AssetPath)
                ? new Check("Input Actions", Level.Ok, InputActionsBuilder.AssetPath)
                : new Check("Input Actions", Level.Error, $"Falta {InputActionsBuilder.AssetPath}. Se genera al crear la escena Prototype."));

            checks.AddRange(CheckScene());
            checks.Add(CheckDocs());
            checks.AddRange(CheckLicenses());

            WriteReport(checks, mode);

            foreach (Check c in checks)
            {
                string line = $"[VerifyProject] {c.Level}: {c.Name} — {c.Detail}";
                if (c.Level == Level.Error) Debug.LogError(line);
                else if (c.Level == Level.Warning) Debug.LogWarning(line);
                else Debug.Log(line);
            }

            int errors = checks.Count(c => c.Level == Level.Error);
            Debug.Log($"[VerifyProject] Resultado: {errors} error(es), {checks.Count(c => c.Level == Level.Warning)} aviso(s).");
            return errors;
        }

        private static Check CheckPipeline()
        {
            RenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline;
            if (asset == null)
                return new Check("Render pipeline", Level.Warning, "Built-in RP. Asigna el URP provisional: menú Qazrix → Configurar URP provisional.");

            return new Check("Render pipeline", Level.Warning,
                $"{asset.GetType().Name} ({AssetDatabase.GetAssetPath(asset)}), provisional: D06 sigue abierta hasta el benchmark URP vs HDRP.");
        }

        private static IEnumerable<Check> CheckScene()
        {
            string path = PrototypeSceneBuilder.ScenePath;
            if (!File.Exists(path))
            {
                yield return new Check("Escena Prototype", Level.Error, $"Falta {path}. Menú Qazrix → Prototipo → Crear o abrir escena Prototype.");
                yield break;
            }
            yield return new Check("Escena Prototype", Level.Ok, path);

            EditorBuildSettingsScene[] build = EditorBuildSettings.scenes;
            bool inBuild = build.Any(s => s.enabled && s.path == path);
            yield return inBuild
                ? new Check("Escenas de build", Level.Ok, "Prototype incluida y activa.")
                : new Check("Escenas de build", Level.Error, "Prototype no está en Build Settings.");

            string[] missing = build.Where(s => s.enabled && !File.Exists(s.path)).Select(s => s.path).ToArray();
            if (missing.Length > 0)
                yield return new Check("Escenas de build", Level.Error, "Entradas a escenas inexistentes: " + string.Join(", ", missing));

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();

            int missingScripts = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            bool hasPlayer = roots.Any(r => r.GetComponentInChildren<PrototypePlayerController>(true) != null);
            bool hasOrbit = roots.Any(r => r.GetComponentInChildren<PrototypeOrbitCamera>(true) != null);
            bool hasCamera = roots.Any(r => r.GetComponentInChildren<Camera>(true) != null);
            bool hasLight = roots.Any(r => r.GetComponentInChildren<Light>(true) != null);
            QazrixInput reader = roots.Select(r => r.GetComponentInChildren<QazrixInput>(true)).FirstOrDefault(c => c != null);

            var problems = new List<string>();
            if (missingScripts > 0) problems.Add($"{missingScripts} script(s) perdidos");
            if (!hasPlayer) problems.Add("sin PrototypePlayerController");
            if (!hasOrbit) problems.Add("sin PrototypeOrbitCamera");
            if (!hasCamera) problems.Add("sin cámara");
            if (!hasLight) problems.Add("sin luz");
            if (reader == null) problems.Add("sin QazrixInput");
            else if (reader.Actions == null) problems.Add("QazrixInput sin Input Actions asignadas");

            yield return problems.Count == 0
                ? new Check("Contenido de escena", Level.Ok, "Jugador, cámara orbital, luz y entrada presentes; ningún script perdido.")
                : new Check("Contenido de escena", Level.Error, string.Join("; ", problems) + ".");
        }

        private static Check CheckDocs()
        {
            string[] missing = RequiredDocs.Where(d => !File.Exists(d)).ToArray();
            return missing.Length == 0
                ? new Check("Documentación", Level.Ok, "README, CLAUDE.md, spec, DecisionLog y registros de fuentes/licencias presentes.")
                : new Check("Documentación", Level.Error, "Faltan: " + string.Join(", ", missing));
        }

        private static IEnumerable<Check> CheckLicenses()
        {
            const string csvPath = "Research/ASSET_LICENSES.csv";
            if (!File.Exists(csvPath)) yield break; // ya lo señala CheckDocs

            string csv = File.ReadAllText(csvPath, Encoding.UTF8);
            string header = csv.Split('\n')[0];
            string[] columns = { "asset_id", "license", "source_url", "verification_status", "repo_path" };
            string[] missingColumns = columns.Where(c => !header.Contains(c)).ToArray();
            if (missingColumns.Length > 0)
            {
                yield return new Check("Registro de licencias", Level.Error, "Faltan columnas: " + string.Join(", ", missingColumns));
                yield break;
            }

            string[] registered = Regex.Matches(csv, @"Assets/[^,""\s]+")
                .Cast<Match>()
                .Select(m => m.Value.TrimEnd('*'))
                .ToArray();

            string[] unregistered = Directory.Exists("Assets/_Qazrix")
                ? Directory.GetFiles("Assets/_Qazrix", "*", SearchOption.AllDirectories)
                    .Select(f => f.Replace('\\', '/'))
                    .Where(f => ExternalExtensions.Contains(Path.GetExtension(f)))
                    .Where(f => !registered.Any(r => f.StartsWith(r, StringComparison.OrdinalIgnoreCase)))
                    .ToArray()
                : Array.Empty<string>();

            yield return unregistered.Length == 0
                ? new Check("Registro de licencias", Level.Ok, "Ningún activo de arte o audio sin fila en ASSET_LICENSES.csv.")
                : new Check("Registro de licencias", Level.Error,
                    $"{unregistered.Length} archivo(s) sin fila de licencia: " + string.Join(", ", unregistered.Take(5)));
        }

        private static void WriteReport(List<Check> checks, string mode)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# VerifyProject");
            sb.AppendLine();
            sb.AppendLine($"- Fecha: {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"- Unity: {Application.unityVersion}");
            sb.AppendLine($"- Modo: {mode}");
            sb.AppendLine($"- Errores: {checks.Count(c => c.Level == Level.Error)} · Avisos: {checks.Count(c => c.Level == Level.Warning)}");
            sb.AppendLine();
            sb.AppendLine("| Comprobación | Resultado | Detalle |");
            sb.AppendLine("|---|---|---|");
            foreach (Check c in checks)
                sb.AppendLine($"| {c.Name} | {c.Level} | {c.Detail.Replace("|", "/")} |");

            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
