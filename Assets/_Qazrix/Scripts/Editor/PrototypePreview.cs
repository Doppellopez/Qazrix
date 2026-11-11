using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Qazrix.EditorTools
{
    /// <summary>Captura la escena Prototype a PNG sin entrar en Play, como evidencia de cada informe.</summary>
    public static class PrototypePreview
    {
        public const string OutputPath = "Reports/QA/prototype_vista.png";

        /// <summary>-executeMethod Qazrix.EditorTools.PrototypePreview.CaptureBatch (sin -nographics)</summary>
        public static void CaptureBatch()
        {
            int code = 0;
            try
            {
                EditorSceneManager.OpenScene(PrototypeSceneBuilder.ScenePath, OpenSceneMode.Single);
                Capture(OutputPath, new Vector3(-9f, 7f, -9f), new Vector3(3f, 0.5f, 7f));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }
            EditorApplication.Exit(code);
        }

        /// <summary>-executeMethod Qazrix.EditorTools.PrototypePreview.CaptureZ01Batch (sin -nographics)</summary>
        public static void CaptureZ01Batch()
        {
            int code = 0;
            try
            {
                EditorSceneManager.OpenScene(Z01GeoBuilder.ScenePath, OpenSceneMode.Single);
                Capture("Reports/QA/z01_vista_aerea.png", new Vector3(-260f, 260f, -420f), new Vector3(40f, -10f, 40f), 4000f);
                Capture("Reports/QA/z01_vista_puntos.png", new Vector3(-40f, 25f, -60f), new Vector3(60f, 5f, 60f), 4000f);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }
            EditorApplication.Exit(code);
        }

        internal static void Capture(string path, Vector3 position, Vector3 lookAt, float farClip = 1000f)
        {
            const int width = 1600, height = 900;

            var go = new GameObject("PreviewCam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            cam.transform.position = position;
            cam.transform.LookAt(lookAt);
            cam.fieldOfView = 55f;
            cam.farClipPlane = farClip;

            var rt = new RenderTexture(width, height, 24);
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                cam.targetTexture = rt;
                // Varias pasadas: la exposición automática de HDRP necesita unos fotogramas para asentarse.
                for (int i = 0; i < 8; i++) cam.Render();

                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Debug.Log($"[Qazrix] Captura guardada: {path}");
            }
            finally
            {
                RenderTexture.active = null;
                cam.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }
    }
}
