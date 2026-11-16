using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Qazrix.EditorTools
{
    /// <summary>
    /// Orden de las escenas del build (D50): el juego empieza en Z01 (la ciudad) y Prototype queda
    /// la segunda, a la que se llega con F3. Las entradas a escenas que ya no existen se quitan.
    /// </summary>
    public static class BuildSceneOrder
    {
        /// <summary>Escenas propias en el orden en que deben ir; las demás se conservan detrás.</summary>
        public static readonly string[] Order = { Z01GeoBuilder.ScenePath, PrototypeSceneBuilder.ScenePath };

        [MenuItem("Qazrix/Build/Empezar el juego en Z01", priority = 70)]
        public static void Apply()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            foreach (string path in Order)
                if (File.Exists(path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            scenes.AddRange(EditorBuildSettings.scenes.Where(s => !Order.Contains(s.path) && File.Exists(s.path)));

            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[Qazrix] Escenas del build: " + string.Join(" → ", scenes.Select(s => Path.GetFileNameWithoutExtension(s.path))));
        }

        /// <summary>-executeMethod Qazrix.EditorTools.BuildSceneOrder.ApplyBatch</summary>
        public static void ApplyBatch()
        {
            int code = 0;
            try { Apply(); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }
    }
}
