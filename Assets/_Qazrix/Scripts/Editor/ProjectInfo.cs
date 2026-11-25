using UnityEditor;
using UnityEngine;

namespace Qazrix.EditorTools
{
    /// <summary>Nombre y versión del juego (D16). Único sitio donde se escriben.</summary>
    public static class ProjectInfo
    {
        public const string ProductName = "Qazrix";
        public const string GameVersion = "0.2";
        public const string ExpectedUnityPrefix = "6000.3.";

        [MenuItem("Qazrix/Aplicar versión y nombre del producto", priority = 2)]
        public static void Apply()
        {
            if (PlayerSettings.productName != ProductName) PlayerSettings.productName = ProductName;
            if (PlayerSettings.bundleVersion != GameVersion) PlayerSettings.bundleVersion = GameVersion;
            AssetDatabase.SaveAssets();
            Debug.Log($"[Qazrix] Producto: {PlayerSettings.productName} v{PlayerSettings.bundleVersion}");
        }
    }
}
