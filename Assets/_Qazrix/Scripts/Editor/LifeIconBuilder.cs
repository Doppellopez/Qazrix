using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Qazrix.EditorTools
{
    /// <summary>
    /// Icono de las vidas extra: la cabeza del hidalgo, renderizada de frente desde su propio modelo (<see cref="HidalgoBuilder"/>)
    /// sobre fondo transparente. Se guarda en <c>Assets/Resources/UI</c> para que el HUD lo cargue también en los builds.
    /// Activo propio (GEN-006): sale del modelo del proyecto, sin material externo.
    /// </summary>
    public static class LifeIconBuilder
    {
        public const string IconPath = "Assets/Resources/UI/Vida_Hidalgo.png";
        public const int Size = 128;

        [MenuItem("Qazrix/Arte/Icono de vida", priority = 60)]
        public static void BuildFromMenu()
        {
            Build();
            EditorUtility.DisplayDialog("Icono de vida", $"Guardado en {IconPath}", "Vale");
        }

        /// <summary>-executeMethod Qazrix.EditorTools.LifeIconBuilder.BuildBatch (sin -nographics)</summary>
        public static void BuildBatch()
        {
            int code = 0;
            try { Build(); }
            catch (Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }
            EditorApplication.Exit(code);
        }

        public static void Build()
        {
            var root = new GameObject("IconoVida_tmp") { hideFlags = HideFlags.HideAndDontSave };
            var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            try
            {
                var visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);
                visual.localPosition = new Vector3(0f, 1f, 0f); // el modelo cuelga 1 m por debajo del centro de la cápsula: sus pies quedan en y = 0
                var body = root.AddComponent<CharacterController>();
                HidalgoBuilder.Build(visual, root.transform, body);

                var lightGo = new GameObject("Luz") { hideFlags = HideFlags.HideAndDontSave };
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.25f;
                lightGo.transform.rotation = Quaternion.Euler(35f, -25f, 0f);

                var camGo = new GameObject("Camara") { hideFlags = HideFlags.HideAndDontSave };
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 0.27f;                      // cabeza y bonete
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);   // transparente
                cam.allowHDR = false;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 6f;
                camGo.transform.position = new Vector3(0f, 1.72f, 2.5f);
                camGo.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up); // el modelo mira a +Z: se le ve de frente desde +Z
                cam.targetTexture = rt;
                for (int i = 0; i < 4; i++) cam.Render();

                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                tex.Apply();
                RenderTexture.active = null;

                Directory.CreateDirectory(Path.GetDirectoryName(IconPath));
                File.WriteAllBytes(IconPath, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(lightGo);
            }
            finally
            {
                RenderTexture.active = null;
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(tex);
            }

            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            Debug.Log($"[Qazrix] Icono de vida guardado: {IconPath}");
        }
    }
}
