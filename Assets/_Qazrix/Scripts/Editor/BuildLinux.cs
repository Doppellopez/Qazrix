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
    /// Build de Linux (x86_64, Mono) con las mismas escenas y opciones que el de Windows. Necesita el
    /// módulo «Linux Build Support (Mono)» del editor. Deja un informe en Reports/QA.
    /// </summary>
    public static class BuildLinux
    {
        public const string OutputFolder = "Builds/Linux";
        public const string ExecutablePath = OutputFolder + "/Qazrix.x86_64";
        public const string ReportPath = "Reports/QA/build-linux-latest.md";

        [MenuItem("Qazrix/Build/Linux (desarrollo)", priority = 81)]
        public static void BuildFromMenu()
        {
            BuildReport report = Build();
            if (report.summary.result == BuildResult.Succeeded)
                EditorUtility.RevealInFinder(ExecutablePath);
            EditorUtility.DisplayDialog("Build de Linux",
                report.summary.result == BuildResult.Succeeded
                    ? $"Build correcto en {OutputFolder}. Para probarlo en otro PC, copia la carpeta entera."
                    : $"El build ha fallado ({report.summary.totalErrors} errores). Informe en {ReportPath}",
                "Aceptar");
        }

        /// <summary>-executeMethod Qazrix.EditorTools.BuildLinux.BuildBatch (salida 0 si el build es correcto)</summary>
        public static void BuildBatch()
        {
            int code;
            try { code = Build().summary.result == BuildResult.Succeeded ? 0 : 1; }
            catch (Exception e) { Debug.LogException(e); code = 2; }
            EditorApplication.Exit(code);
        }

        private static BuildReport Build()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64))
                throw new InvalidOperationException("Falta el módulo «Linux Build Support (Mono)» en este editor.");

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
                target = BuildTarget.StandaloneLinux64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.Development // igual que el de Windows: es un build de prueba
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            WriteReport(report, scenes);
            Debug.Log($"[Qazrix] Build de Linux: {report.summary.result} · {report.summary.totalSize / (1024f * 1024f):0.0} MB · {report.summary.totalTime.TotalSeconds:0} s");
            return report;
        }

        private static void WriteReport(BuildReport report, string[] scenes)
        {
            BuildSummary s = report.summary;
            var sb = new StringBuilder();
            sb.AppendLine("# Build de Linux");
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
            sb.AppendLine("Para probarlo en otro PC, copia la carpeta `Builds/Linux` entera y da permiso de ejecución: `chmod +x Qazrix.x86_64`.");

            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
