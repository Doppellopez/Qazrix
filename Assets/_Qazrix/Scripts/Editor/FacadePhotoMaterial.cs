using System.Collections.Generic;
using System.IO;
using Qazrix.World;
using UnityEditor;
using UnityEngine;

namespace Qazrix.EditorTools
{
    /// <summary>
    /// Fotos de edificios de Cáceres (Wikimedia Commons, licencia revisada por Jorge; COM-005 y COM-006) colgadas de la pared
    /// de su edificio en Z01. El punto es el centro del volumen en metros locales (GroundControlPoints.csv).
    /// La foto aérea del Arco de la Estrella (COM-007) no se usa: no hay volumen identificado del arco.
    /// </summary>
    public static class FacadePhotoMaterial
    {
        private const string Folder = "Assets/_Qazrix/Art/Environment/Facades/";

        // GCP-01 Torre de Bujaco y GCP-02 Concatedral (nave): local_x, local_z.
        private static readonly (string Name, string File, Vector2 Point, string AssetId)[] Entries =
        {
            ("Bujaco", "Bujaco_fachada.jpg", new Vector2(43.29f, 77.51f), "COM-005"),
            ("Concatedral", "Concatedral_fachada.jpg", new Vector2(124.5f, 9.5f), "COM-006"),
        };

        public static CityBlocks.FacadePhoto[] Photos()
        {
            var list = new List<CityBlocks.FacadePhoto>();
            foreach (var e in Entries)
            {
                string path = Folder + e.File;
                if (!File.Exists(path)) throw new FileNotFoundException($"Falta la foto de {e.Name}: {path} ({e.AssetId} en Research/ASSET_LICENSES.csv).");
                AssetDatabase.ImportAsset(path);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Material material = PrototypeSceneBuilder.GetOrCreateMaterial("Ciudad_Foto_" + e.Name, Color.white);
                foreach (string property in new[] { "_BaseMap", "_MainTex" })
                    if (material.HasProperty(property)) material.SetTexture(property, texture);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.05f);
                EditorUtility.SetDirty(material);
                list.Add(new CityBlocks.FacadePhoto { name = e.Name, point = e.Point, material = material });
            }
            return list.ToArray();
        }
    }
}
