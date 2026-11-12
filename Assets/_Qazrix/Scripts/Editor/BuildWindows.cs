using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Qazrix.EditorTools
{
    /// <summary>
    /// Build de Windows reproducible (spec §8.6): mismas escenas, misma carpeta y mismas opciones cada
    /// vez, desde el menú o sin interfaz. Deja un informe en Reports/QA.
    /// </summary>
    public static class BuildWindows
    {
        public const string OutputFolder = "Builds/Windows";
        public const string ExecutablePath = OutputFolder + "/Qazrix.exe";
        public const string ReportPath = "Reports/QA/build-windows-latest.md";

        [MenuItem("Qazrix/Build/Windows (desarrollo)", priority = 80)]
        public static void BuildFromMenu()
        {
            BuildReport report = Build();
            if (report.summary.result == BuildResult.Succeeded)
                EditorUtility.RevealInFinder(ExecutablePath);
            EditorUtility.DisplayDialog("Build de Windows",
                report.summary.result == BuildResult.Succeeded
                    ? $"Build correcto en {OutputFolder}. Para probarlo en otro PC, copia la carpeta entera."
                    : $"El build ha fallado ({report.summary.totalErrors} errores). Informe en {ReportPath}",
                "Aceptar");
        }

        /// <summary>-executeMethod Qazrix.EditorTools.BuildWindows.BuildBatch (salida 0 si el build es correcto)</summary>
        public static void BuildBatch()
        {
            int code;
            try { code = Build().summary.result == BuildResult.Succeeded ? 0 : 1; }
            catch (Exception e) { Debug.LogException(e); code = 2; }
            EditorApplication.Exit(code);
        }

        private static BuildReport Build()
        {
            ProjectInfo.Apply();

            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled && File.Exists(s.path))
                .Select(s => s.path)
                .ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("No hay escenas activas en Build Settings.");

            // Carpeta limpia: un build nunca arrastra restos del anterior.
            if (Directory.Exists(OutputFolder)) Directory.Delete(OutputFolder, true);
            Directory.CreateDirectory(OutputFolder);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = ExecutablePath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.Development // con consola de errores y nombres legibles: es un build de prueba
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            WriteReport(report, scenes);
            Debug.Log($"[Qazrix] Build de Windows: {report.summary.result} · {report.summary.totalSize / (1024f * 1024f):0.0} MB · {report.summary.totalTime.TotalSeconds:0} s");
            return report;
        }

        private static void WriteReport(BuildReport report, string[] scenes)
        {
            BuildSummary s = report.summary;
            var sb = new StringBuilder();
            sb.AppendLine("# Build de Windows");
            sb.AppendLine();
            sb.AppendLine($"- Fecha: {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"- Resultado: **{s.result}** · errores: {s.totalErrors} · avisos: {s.totalWarnings}");
            sb.AppendLine($"- Unity: {Application.unityVersion} · juego: {PlayerSettings.productName} v{PlayerSettings.bundleVersion}");
            sb.AppendLine($"- Plataforma: {s.platform} · opciones: {s.options}");
            sb.AppendLine($"- Salida: `{ExecutablePath}` · tamaño: {s.totalSize / (1024f * 1024f):0.0} MB · duración: {s.totalTime.TotalSeconds:0} s");
            sb.AppendLine();
            sb.AppendLine("## Escenas incluidas");
            sb.AppendLine();
            for (int i = 0; i < scenes.Length; i++) sb.AppendLine($"{i}. `{scenes[i]}`");
            sb.AppendLine();
            sb.AppendLine("Para probarlo en otro PC, copia la carpeta `Builds/Windows` entera, no solo el `.exe`.");

            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
