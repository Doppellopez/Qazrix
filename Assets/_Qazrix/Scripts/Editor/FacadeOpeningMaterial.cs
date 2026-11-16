using System.IO;
using Qazrix.World;
using UnityEditor;
using UnityEngine;

namespace Qazrix.EditorTools
{
    /// <summary>
    /// Material `Ciudad_Hueco` de las ventanas y saeteras que <see cref="CityBlocks"/> dibuja sobre las
    /// fachadas. La textura se genera por código (GEN-003, producción propia): mitad izquierda, ventana
    /// geminada con dos arcos de medio punto y parteluz; mitad derecha, saetera.
    /// </summary>
    public static class FacadeOpeningMaterial
    {
        public const string Folder = "Assets/_Qazrix/Art/Environment/Generated";
        public const string TexturePath = Folder + "/ciudad_huecos.png";
        public const string MaterialName = "Ciudad_Hueco";

        private const int Half = 256, Height = 512; // cada mitad, 256 × 512 px

        public static Material Ensure()
        {
            Directory.CreateDirectory(Folder);
            var pixels = new Color[Half * 2 * Height];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Half; x++)
                {
                    float u = (x + 0.5f) / Half, v = (y + 0.5f) / Height;
                    pixels[y * Half * 2 + x] = Window(u, v, x, y);
                    pixels[y * Half * 2 + Half + x] = ArrowSlit(u, v, x, y);
                }
            }

            var generated = new Texture2D(Half * 2, Height, TextureFormat.RGB24, false);
            generated.SetPixels(pixels);
            generated.Apply();
            File.WriteAllBytes(TexturePath, generated.EncodeToPNG());
            Object.DestroyImmediate(generated);

            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            if (importer != null && importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            Material material = PrototypeSceneBuilder.GetOrCreateMaterial(MaterialName, Color.white);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            foreach (string property in new[] { "_BaseMap", "_MainTex" })
                if (material.HasProperty(property)) material.SetTexture(property, texture);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.05f);
            EditorUtility.SetDirty(material);
            Debug.Log($"[Qazrix] Textura de ventanas y saeteras generada ({TexturePath}, GEN-003)");
            return material;
        }

        /// <summary>Ajimez: dos luces de 22 cm con arco de medio punto, separadas por un parteluz de 8 cm.</summary>
        private static Color Window(float u, float v, int px, int py)
        {
            float x = (u - 0.5f) * CityBlocks.WindowSize.x, y = v * CityBlocks.WindowSize.y;
            const float half = 0.11f, sill = 0.28f, spring = 1.12f;
            float depth = Mathf.Max(Light(x + 0.15f, y, half, sill, spring), Light(x - 0.15f, y, half, sill, spring));
            return Shade(depth, x, y, CityBlocks.WindowSize, px, py);
        }

        /// <summary>Saetera: ranura de 9 cm de ancho y 1,1 m de alto.</summary>
        private static Color ArrowSlit(float u, float v, int px, int py)
        {
            float x = (u - 0.5f) * CityBlocks.ArrowSlitSize.x, y = v * CityBlocks.ArrowSlitSize.y;
            float depth = Mathf.Min(0.045f - Mathf.Abs(x), Mathf.Min(y - 0.2f, 1.3f - y));
            return Shade(depth, x, y, CityBlocks.ArrowSlitSize, px, py);
        }

        /// <summary>Metros hacia dentro del vano (positivo) o hacia fuera (negativo) de una luz con arco.</summary>
        private static float Light(float x, float y, float half, float sill, float spring)
        {
            if (y >= spring) return half - Mathf.Sqrt(x * x + (y - spring) * (y - spring));
            return Mathf.Min(half - Mathf.Abs(x), y - sill);
        }

        private static Color Shade(float depth, float x, float y, Vector2 size, int px, int py)
        {
            if (depth > 0f)
            {
                // Vano: casi negro, algo más claro junto al borde iluminado.
                float inner = Mathf.Clamp01(depth / 0.05f);
                return Color.Lerp(new Color(0.16f, 0.15f, 0.14f), new Color(0.03f, 0.03f, 0.04f), inner);
            }

            // Marco de sillería: granito claro con grano, juntas y un filo oscuro alrededor del vano.
            float grain = Mathf.PerlinNoise(px * 0.11f + 13.7f, py * 0.11f + 4.2f);
            float speck = Mathf.PerlinNoise(px * 0.9f + 1.3f, py * 0.9f + 8.8f);
            float shade = 0.86f + 0.22f * grain + 0.08f * (speck - 0.5f);

            float joint = Mathf.Abs(Mathf.Repeat(y, 0.4f) - 0.2f);              // hiladas de 40 cm
            if (joint < 0.008f) shade *= 0.72f;
            float edge = Mathf.Min(size.x * 0.5f - Mathf.Abs(x), Mathf.Min(y, size.y - y));
            if (edge < 0.015f) shade *= 0.7f;                                    // contorno del recuadro
            if (depth > -0.02f) shade *= 0.62f;                                  // derrame del vano

            return new Color(0.66f * shade, 0.63f * shade, 0.57f * shade);
        }
    }
}
