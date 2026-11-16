using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Qazrix.EditorTools
{
    /// <summary>
    /// Materiales con textura de la ciudad, con color y relieve: muros (`Ciudad_Muro`, CC0-006, Castle
    /// Wall Variation), peldaños (`Ciudad_Peldano`, CC0-008, Rock Tile Floor) y tejados
    /// (`Ciudad_Tejado`, CC0-009, Clay Roof Tiles 02), todos de Poly Haven.
    /// Los usan todas las escenas que ya referencian esos materiales, sin regenerarlas.
    /// </summary>
    public static class CityWallMaterial
    {
        public const string Folder = "Assets/_Qazrix/Art/Environment/CastleWall";
        public const string ColorPath = Folder + "/castle_wall_varriation_diff_1k.jpg";
        public const string NormalPath = Folder + "/castle_wall_varriation_nor_gl_1k.jpg";

        public const string StepsFolder = "Assets/_Qazrix/Art/Environment/RockTileFloor";
        public const string StepsColorPath = StepsFolder + "/rock_tile_floor_diff_1k.jpg";
        public const string StepsNormalPath = StepsFolder + "/rock_tile_floor_nor_gl_1k.jpg";

        public const string RoofFolder = "Assets/_Qazrix/Art/Environment/ClayRoofTiles";
        public const string RoofColorPath = RoofFolder + "/clay_roof_tiles_02_diff_1k.jpg";
        public const string RoofNormalPath = RoofFolder + "/clay_roof_tiles_02_nor_gl_1k.jpg";

        [MenuItem("Qazrix/Arte/Aplicar texturas a muros, peldaños y tejados", priority = 60)]
        public static void ApplyFromMenu()
        {
            Ensure();
            EnsureSteps();
            EnsureRoof();
            AssetDatabase.SaveAssets();
        }

        /// <summary>-executeMethod Qazrix.EditorTools.CityWallMaterial.ApplyBatch</summary>
        public static void ApplyBatch()
        {
            int code = 0;
            try
            {
                ApplyFromMenu();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }
            EditorApplication.Exit(code);
        }

        /// <summary>Material de los muros de los edificios.</summary>
        public static Material Ensure() => Stone("Ciudad_Muro", ColorPath, NormalPath, "CC0-006");

        /// <summary>Material de los peldaños de las escalinatas.</summary>
        public static Material EnsureSteps() => Stone("Ciudad_Peldano", StepsColorPath, StepsNormalPath, "CC0-008");

        /// <summary>Material de teja de los tejados de los edificios.</summary>
        public static Material EnsureRoof() => Stone("Ciudad_Tejado", RoofColorPath, RoofNormalPath, "CC0-009");

        private static Material Stone(string materialName, string colorPath, string normalPath, string assetId)
        {
            if (!File.Exists(colorPath) || !File.Exists(normalPath))
                throw new FileNotFoundException($"Faltan las texturas de {materialName}: {colorPath} ({assetId} en Research/ASSET_LICENSES.csv).");

            AssetDatabase.ImportAsset(colorPath);
            AssetDatabase.ImportAsset(normalPath);
            var normalImporter = (TextureImporter)AssetImporter.GetAtPath(normalPath);
            if (normalImporter.textureType != TextureImporterType.NormalMap)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.SaveAndReimport();
            }

            // Blanco: el color lo pone la textura.
            Material material = PrototypeSceneBuilder.GetOrCreateMaterial(materialName, Color.white);
            var color = AssetDatabase.LoadAssetAtPath<Texture2D>(colorPath);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);

            foreach (string property in new[] { "_BaseMap", "_MainTex" })
                if (material.HasProperty(property)) material.SetTexture(property, color);
            if (material.HasProperty("_BumpMap"))
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.1f); // piedra mate

            EditorUtility.SetDirty(material);
            Debug.Log($"[Qazrix] Textura aplicada a {materialName} ({assetId})");
            return material;
        }
    }
}
