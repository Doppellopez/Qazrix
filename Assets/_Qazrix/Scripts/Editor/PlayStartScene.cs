using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Qazrix.EditorTools
{
    /// <summary>
    /// Con Play en el editor el juego empieza siempre en Z01 (la ciudad), esté abierta la escena que esté: como en el
    /// build (<see cref="BuildSceneOrder"/>, D50). Se puede desactivar en el menú Qazrix → Jugar; entonces Play vuelve a
    /// empezar en la escena abierta. No actúa al lanzar pruebas (<c>-runTests</c>), que cargan sus propias escenas.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayStartScene
    {
        private const string PrefKey = "qazrix.play.startInZ01";
        private const string MenuPath = "Qazrix/Jugar/Empezar Play en Z01";

        static PlayStartScene()
        {
            // Tras cargar los scripts el AssetDatabase puede no estar listo: se aplica en el primer ciclo del editor.
            EditorApplication.delayCall += Apply;
        }

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        [MenuItem(MenuPath, priority = 80)]
        public static void Toggle()
        {
            Enabled = !Enabled;
            Apply();
        }

        [MenuItem(MenuPath, true)]
        public static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        public static void Apply()
        {
            if (IsRunningTests()) return;

            if (Enabled && File.Exists(Z01GeoBuilder.ScenePath))
            {
                var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Z01GeoBuilder.ScenePath);
                if (scene != null) UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = scene;
            }
            else
            {
                UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = null;
            }
        }

        /// <summary>El editor se lanzó para pasar pruebas: no se toca la escena de inicio.</summary>
        private static bool IsRunningTests()
        {
            foreach (string arg in Environment.GetCommandLineArgs())
                if (string.Equals(arg, "-runTests", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
