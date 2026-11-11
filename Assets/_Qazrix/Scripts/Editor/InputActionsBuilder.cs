using System;
using System.IO;
using System.Text;
using Qazrix.Input;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Qazrix.EditorTools
{
    /// <summary>
    /// Genera el archivo de Input Actions a partir de <see cref="QazrixControls.CreateDefault"/>.
    /// No pisa un archivo existente salvo que se pida: así se conservan los cambios hechos en el
    /// editor de acciones de Unity.
    /// </summary>
    public static class InputActionsBuilder
    {
        public const string AssetPath = "Assets/_Qazrix/Settings/Input/QazrixControls.inputactions";

        /// <summary>Devuelve el asset de acciones, creándolo si falta.</summary>
        public static InputActionAsset Ensure()
        {
            if (!File.Exists(AssetPath)) Write();
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            if (asset == null) throw new InvalidOperationException($"No se pudo importar {AssetPath}");
            return asset;
        }

        [MenuItem("Qazrix/Entrada/Regenerar Input Actions (sobrescribe)", priority = 60)]
        public static void RegenerateFromMenu()
        {
            if (File.Exists(AssetPath) && !EditorUtility.DisplayDialog(
                    "Regenerar Input Actions",
                    "Se va a sobrescribir QazrixControls.inputactions con las asignaciones de fábrica. Los cambios hechos a mano en ese archivo se perderán.",
                    "Sobrescribir", "Cancelar"))
                return;
            Write();
        }

        private static void Write()
        {
            InputActionAsset factory = QazrixControls.CreateDefault();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
                File.WriteAllText(AssetPath, factory.ToJson(), new UTF8Encoding(false));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(factory);
            }
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[Qazrix] Input Actions generadas: {AssetPath}");
        }
    }
}
