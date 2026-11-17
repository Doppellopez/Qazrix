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
            if (report.summary.result == BuildResult.Succeeded) AddIconFiles();
            WriteReport(report, scenes);
            Debug.Log($"[Qazrix] Build de Linux: {report.summary.result} · {report.summary.totalSize / (1024f * 1024f):0.0} MB · {report.summary.totalTime.TotalSeconds:0} s");
            return report;
        }

        /// <summary>
        /// En Linux el ejecutable no lleva el icono dentro: el escritorio lo toma de un acceso directo. Junto al ejecutable
        /// se deja el icono (<c>Qazrix.png</c>), un <c>Qazrix.desktop</c> y un script que instala el acceso directo con rutas
        /// absolutas en el menú de aplicaciones del usuario. La ventana del juego ya usa el icono por defecto del proyecto.
        /// </summary>
        private static void AddIconFiles()
        {
            string icon = GameIcon.DesignFolder + "/Qazrix_Icono_1024.png";
            if (!File.Exists(icon))
            {
                Debug.LogWarning($"[Qazrix] No hay {icon}: el build de Linux queda sin icono de acceso directo (menú Qazrix → Build → Crear icono del juego).");
                return;
            }
            File.Copy(icon, OutputFolder + "/Qazrix.png", true);

            var utf8 = new UTF8Encoding(false);
            // Los archivos de texto de Linux llevan saltos de línea \n, no \r\n.
            string desktop =
                "[Desktop Entry]\n" +
                "Type=Application\n" +
                "Name=Qazrix\n" +
                "Comment=Acción y sigilo en el Cáceres de 1229\n" +
                "Exec=./Qazrix.x86_64\n" +
                "Icon=Qazrix.png\n" +
                "Terminal=false\n" +
                "Categories=Game;\n";
            File.WriteAllText(OutputFolder + "/Qazrix.desktop", desktop, utf8);

            string install =
                "#!/bin/sh\n" +
                "# Crea el acceso directo de Qazrix, con su icono, en el menú de aplicaciones del usuario.\n" +
                "cd \"$(dirname \"$0\")\" || exit 1\n" +
                "DIR=\"$(pwd)\"\n" +
                "chmod +x Qazrix.x86_64\n" +
                "mkdir -p \"$HOME/.local/share/applications\"\n" +
                "cat > \"$HOME/.local/share/applications/qazrix.desktop\" <<EOF\n" +
                "[Desktop Entry]\n" +
                "Type=Application\n" +
                "Name=Qazrix\n" +
                "Exec=$DIR/Qazrix.x86_64\n" +
                "Path=$DIR\n" +
                "Icon=$DIR/Qazrix.png\n" +
                "Terminal=false\n" +
                "Categories=Game;\n" +
                "EOF\n" +
                "echo \"Acceso directo creado en ~/.local/share/applications/qazrix.desktop\"\n";
            File.WriteAllText(OutputFolder + "/Instalar_acceso_directo.sh", install, utf8);
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
            sb.AppendLine("Icono: `Qazrix.png` y `Qazrix.desktop` junto al ejecutable; `sh Instalar_acceso_directo.sh` crea el acceso directo con icono en el menú de aplicaciones.");

            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
