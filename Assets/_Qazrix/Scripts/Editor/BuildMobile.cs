using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Qazrix.EditorTools
{
    /// <summary>
    /// Builds de móviles. Android: un APK instalable (IL2CPP, ARM64). iOS: el proyecto de Xcode exportado; desde Windows no se
    /// puede compilar ni firmar, hace falta un Mac con Xcode y una cuenta de desarrollador de Apple. Los controles táctiles
    /// (<c>TouchControls</c>) solo se activan en estas dos plataformas.
    /// Los identificadores de paquete son provisionales y los decide el equipo antes de publicar nada.
    /// </summary>
    public static class BuildMobile
    {
        public const string AndroidOutput = "Builds/Android";
        public const string AndroidApk = AndroidOutput + "/Qazrix.apk";
        public const string IosOutput = "Builds/iOS";
        public const string ReportPath = "Reports/QA/build-mobile-latest.md";

        /// <summary>Identificador provisional del paquete (Android) y del bundle (iOS).</summary>
        public const string ApplicationId = "com.qazrix.game";

        [MenuItem("Qazrix/Build/Android (APK de desarrollo)", priority = 82)]
        public static void BuildAndroidFromMenu() => Report(BuildAndroid(), AndroidOutput);

        [MenuItem("Qazrix/Build/iOS (proyecto de Xcode)", priority = 83)]
        public static void BuildIosFromMenu() => Report(BuildIos(), IosOutput);

        /// <summary>-executeMethod Qazrix.EditorTools.BuildMobile.BuildAndroidBatch (salida 0 si el build es correcto)</summary>
        public static void BuildAndroidBatch() => Batch(BuildAndroid);

        /// <summary>-executeMethod Qazrix.EditorTools.BuildMobile.BuildIosBatch (salida 0 si el build es correcto)</summary>
        public static void BuildIosBatch() => Batch(BuildIos);

        private static void Batch(Func<BuildReport> build)
        {
            int code;
            try { code = build().summary.result == BuildResult.Succeeded ? 0 : 1; }
            catch (Exception e) { Debug.LogException(e); code = 2; }
            EditorApplication.Exit(code);
        }

        private static void Report(BuildReport report, string folder)
        {
            if (report.summary.result == BuildResult.Succeeded) EditorUtility.RevealInFinder(folder);
            EditorUtility.DisplayDialog("Build de móvil",
                report.summary.result == BuildResult.Succeeded ? $"Build correcto en {folder}." : $"El build ha fallado ({report.summary.totalErrors} errores). Informe en {ReportPath}",
                "Aceptar");
        }

        private static string[] Scenes()
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled && File.Exists(s.path)).Select(s => s.path).ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("No hay escenas activas en Build Settings.");
            return scenes;
        }

        /// <summary>Juego horizontal en los dos sistemas (los controles táctiles están pensados para eso).</summary>
        private static void Landscape()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
        }

        public static BuildReport BuildAndroid()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Falta el módulo de Android en este editor (Unity Hub → Instalaciones → Añadir módulos).");

            ProjectInfo.Apply();
            Landscape();
            NamedBuildTarget target = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(target, ApplicationId);
            PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP); // ARM64 solo existe con IL2CPP
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            EditorUserBuildSettings.buildAppBundle = false; // un APK que se instala directamente
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

            if (Directory.Exists(AndroidOutput)) Directory.Delete(AndroidOutput, true);
            Directory.CreateDirectory(AndroidOutput);

            string[] scenes = Scenes();
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = AndroidApk,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development // firmado con la clave de depuración: solo para probar
            });
            WriteReport("Android", report, scenes, AndroidApk);
            return report;
        }

        public static BuildReport BuildIos()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
                throw new InvalidOperationException("Falta el módulo de iOS en este editor (Unity Hub → Instalaciones → Añadir módulos).");

            ProjectInfo.Apply();
            Landscape();
            NamedBuildTarget target = NamedBuildTarget.iOS;
            PlayerSettings.SetApplicationIdentifier(target, ApplicationId);
            PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.targetOSVersionString = "13.0";

            if (Directory.Exists(IosOutput)) Directory.Delete(IosOutput, true);
            Directory.CreateDirectory(IosOutput);

            string[] scenes = Scenes();
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = IosOutput,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = BuildOptions.Development
            });
            if (report.summary.result == BuildResult.Succeeded)
                File.WriteAllText(IosOutput + "/LEEME_Xcode.txt",
                    "Proyecto de Xcode generado por Unity. Para tenerlo en un iPhone hace falta un Mac con Xcode, una cuenta de\n" +
                    "desarrollador de Apple y firmar el proyecto (Signing & Capabilities). Desde Windows no se puede compilar.\n" +
                    "Abre Unity-iPhone.xcodeproj, elige tu equipo de firma y tu iPhone, y pulsa Run.\n", new UTF8Encoding(false));
            WriteReport("iOS", report, scenes, IosOutput);
            return report;
        }

        private static void WriteReport(string platform, BuildReport report, string[] scenes, string output)
        {
            BuildSummary s = report.summary;
            var sb = new StringBuilder();
            sb.AppendLine($"# Build de {platform}");
            sb.AppendLine();
            sb.AppendLine($"- Fecha: {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"- Resultado: **{s.result}** · errores: {s.totalErrors} · avisos: {s.totalWarnings}");
            sb.AppendLine($"- Unity: {Application.unityVersion} · juego: {PlayerSettings.productName} v{PlayerSettings.bundleVersion} · id: {ApplicationId} (provisional)");
            sb.AppendLine($"- Salida: `{output}` · tamaño: {s.totalSize / (1024f * 1024f):0.0} MB · duración: {s.totalTime.TotalSeconds:0} s");
            sb.AppendLine();
            sb.AppendLine("## Escenas incluidas");
            sb.AppendLine();
            for (int i = 0; i < scenes.Length; i++) sb.AppendLine($"{i}. `{scenes[i]}`");
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, sb.ToString(), new UTF8Encoding(false));
            Debug.Log($"[Qazrix] Build de {platform}: {s.result} · {s.totalSize / (1024f * 1024f):0.0} MB · {s.totalTime.TotalSeconds:0} s");
        }
    }
}
