using System.IO;
using UnityEditor;
using UnityEngine;

namespace Qazrix.EditorTools
{
    /// <summary>
    /// Piezas comunes para construir personajes con primitivas de Unity y texturas generadas por
    /// código. Todo lo que sale de aquí es de producción propia: no usa activos externos.
    /// </summary>
    public static class CharacterKit
    {
        public enum Pattern { Weave, Pleats, Leather, Plain }

        /// <summary>
        /// Añade una primitiva sin colisionador. En cilindros y cápsulas (2 unidades de alto en Unity),
        /// la componente Y del tamaño es la semialtura.
        /// </summary>
        public static Transform Part(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 size,
            Material material, Vector3 euler = default)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // el cuerpo no colisiona: lo hace el CharacterController
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go.transform;
        }

        /// <summary>Nodo vacío que sirve de pivote (cadera, hombro, empuñadura).</summary>
        public static Transform Pivot(string name, Transform parent, Vector3 position, Vector3 euler = default)
        {
            Transform pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = position;
            pivot.localRotation = Quaternion.Euler(euler);
            return pivot;
        }

        /// <summary>Material con textura generada. El color va en la textura; el material queda en blanco.</summary>
        public static Material Textured(string folder, string name, Color color, Pattern pattern, float tiling, bool metal = false)
        {
            Texture2D texture = GetOrCreateTexture(folder, name, color, pattern);
            Material material = PrototypeSceneBuilder.GetOrCreateMaterial(name, Color.white, folder);
            foreach (string property in new[] { "_BaseMap", "_MainTex" })
            {
                if (!material.HasProperty(property)) continue;
                material.SetTexture(property, texture);
                material.SetTextureScale(property, new Vector2(tiling, tiling));
            }
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", metal ? 0.75f : 0.12f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metal ? 0.9f : 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Textura de 128 px generada por código, siempre igual para el mismo nombre.</summary>
        private static Texture2D GetOrCreateTexture(string folder, string name, Color color, Pattern pattern)
        {
            const int size = 128;
            Directory.CreateDirectory(folder);
            string path = $"{folder}/{name}.png";

            var random = new System.Random(StableSeed(name));
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float noise = (float)random.NextDouble();
                    float shade;
                    switch (pattern)
                    {
                        case Pattern.Weave: // trama de hilos cruzados
                            shade = (((x / 2) + (y / 2)) % 2 == 0 ? 0.90f : 1.04f) * (0.94f + 0.10f * noise);
                            break;
                        case Pattern.Pleats: // pliegues verticales de una tela amplia
                            float fold = Mathf.Abs(Mathf.Sin(x * Mathf.PI * 9f / size));
                            shade = (0.70f + 0.36f * fold) * (0.96f + 0.07f * noise);
                            break;
                        case Pattern.Leather: // manchas irregulares
                            float blotch = Mathf.PerlinNoise(x * 0.09f + 3.1f, y * 0.09f + 7.7f);
                            shade = 0.78f + 0.34f * blotch + 0.06f * (noise - 0.5f);
                            break;
                        default:
                            shade = 0.97f + 0.05f * noise;
                            break;
                    }
                    pixels[y * size + x] = new Color(Mathf.Clamp01(color.r * shade), Mathf.Clamp01(color.g * shade), Mathf.Clamp01(color.b * shade), 1f);
                }
            }

            var generated = new Texture2D(size, size, TextureFormat.RGB24, false);
            generated.SetPixels(pixels);
            generated.Apply();
            File.WriteAllBytes(path, generated.EncodeToPNG());
            Object.DestroyImmediate(generated);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null && importer.wrapMode != TextureWrapMode.Repeat)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Semilla que no cambia entre ejecuciones (string.GetHashCode sí puede cambiar).</summary>
        private static int StableSeed(string text)
        {
            unchecked
            {
                int hash = 23;
                foreach (char c in text) hash = hash * 31 + c;
                return hash & 0x7fffffff;
            }
        }
    }
}
