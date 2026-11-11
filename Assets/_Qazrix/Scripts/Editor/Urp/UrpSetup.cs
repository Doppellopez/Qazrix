using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Qazrix.EditorTools
{
    /// <summary>
    /// Crea y asigna el URP provisional (D06) con API de Editor. Idempotente: reutiliza los assets si existen.
    /// Vive en un asmdef aparte que solo compila con el paquete URP instalado, de modo que un cambio
    /// de pipeline tras el benchmark no rompe el resto de herramientas.
    /// </summary>
    public static class UrpSetup
    {
        private const string Folder = "Assets/_Qazrix/Settings/URP";
        private const string PipelinePath = Folder + "/Qazrix_URP.asset";
        private const string RendererPath = Folder + "/Qazrix_URP_Renderer.asset";
        private const string PostProcessDataPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

        [MenuItem("Qazrix/Configurar URP provisional", priority = 3)]
        public static void Configure()
        {
            Directory.CreateDirectory(Folder);

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessDataPath);
                AssetDatabase.CreateAsset(renderer, RendererPath);
                ResourceReloader.ReloadAllNullIn(renderer, UniversalRenderPipelineAsset.packagePath);
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;

            GraphicsSettings.defaultRenderPipeline = pipeline;

            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);

            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Qazrix] URP provisional asignado: {PipelinePath} (espacio de color: {PlayerSettings.colorSpace})");
        }

        /// <summary>-executeMethod Qazrix.EditorTools.UrpSetup.ConfigureBatch</summary>
        public static void ConfigureBatch()
        {
            int code = 0;
            try { Configure(); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }
    }
}
