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
                Capture("Reports/QA/personaje_hidalgo.png", new Vector3(1.5f, 1.35f, 2.4f), new Vector3(0f, 0.95f, 0f));
                Capture("Reports/QA/personaje_guardia.png", new Vector3(13.4f, 1.45f, 19.0f), new Vector3(16f, 1.0f, 17f));
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
                // Casco antiguo desde el suroeste, en alto; Plaza Mayor a pie de calle; y vista cenital.
                Capture("Reports/QA/z01_vista_aerea.png", new Vector3(-120f, 230f, -260f), new Vector3(150f, 0f, 20f), 4000f);
                Capture("Reports/QA/z01_vista_plaza.png", new Vector3(-8f, 0.5f, 55f), new Vector3(45f, 8f, 80f), 4000f);
                Capture("Reports/QA/z01_vista_cenital.png", new Vector3(160f, 620f, -25f), new Vector3(160f, 0f, -24f), 4000f);
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
