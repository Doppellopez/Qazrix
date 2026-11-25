using System.Collections.Generic;
using System.IO;
using Qazrix.Simulation.World;
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
            // No usa CreatePrimitive: la malla es la nuestra, con diez veces los triángulos y el mismo tamaño
            // que la de Unity (ART-11). Sin colisionador: el cuerpo no colisiona, lo hace el CharacterController.
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = MeshFor(type);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = size;
            return go.transform;
        }

        public const string MeshFolder = "Assets/_Qazrix/Art/Characters/Meshes";

        private static readonly Dictionary<PrimitiveType, Mesh> MeshCache = new Dictionary<PrimitiveType, Mesh>();

        /// <summary>
        /// Malla de alta resolución de una primitiva (esfera, cápsula, cilindro o cubo), guardada como activo
        /// generado en <see cref="MeshFolder"/>. Se rellena una vez por sesión del editor, así que cambiar las
        /// subdivisiones en <see cref="PrimitiveGeometry"/> se aplica al regenerar.
        /// </summary>
        public static Mesh MeshFor(PrimitiveType type)
        {
            if (MeshCache.TryGetValue(type, out Mesh cached) && cached != null) return cached;

            MeshData data;
            switch (type)
            {
                case PrimitiveType.Sphere: data = PrimitiveGeometry.Sphere(); break;
                case PrimitiveType.Capsule: data = PrimitiveGeometry.Capsule(); break;
                case PrimitiveType.Cylinder: data = PrimitiveGeometry.Cylinder(); break;
                case PrimitiveType.Cube: data = PrimitiveGeometry.Box(); break;
                default: throw new System.ArgumentException($"Sin malla de alta resolución para {type}", nameof(type));
            }

            Directory.CreateDirectory(MeshFolder);
            string path = $"{MeshFolder}/Qazrix_{type}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh();

            mesh.Clear();
            mesh.name = $"Qazrix_{type}";
            mesh.indexFormat = data.VertexCount > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            var vertices = new List<Vector3>(data.VertexCount);
            var normals = new List<Vector3>(data.VertexCount);
            var uvs = new List<Vector2>(data.VertexCount);
            for (int i = 0; i < data.VertexCount; i++)
            {
                vertices.Add(new Vector3(data.Vertices[i * 3], data.Vertices[i * 3 + 1], data.Vertices[i * 3 + 2]));
                normals.Add(new Vector3(data.Normals[i * 3], data.Normals[i * 3 + 1], data.Normals[i * 3 + 2]));
                uvs.Add(new Vector2(data.Uvs[i * 2], data.Uvs[i * 2 + 1]));
            }
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(data.Triangles, 0);
            mesh.RecalculateBounds();

            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            MeshCache[type] = mesh;
            return mesh;
        }

        /// <summary>
        /// Nivel de detalle bajo para un modelo ya construido (D74): junto a cada pieza de alta resolución se añade una copia con la
        /// malla sencilla de Unity (mismos pivotes y material), y un LODGroup en <paramref name="model"/> enseña la de alta cerca,
        /// la sencilla a media distancia y nada muy lejos. Las animaciones no cambian: las copias cuelgan de los mismos pivotes.
        /// </summary>
        public static LODGroup AddLowDetail(Transform model, float nearTransition = 0.12f, float cullTransition = 0.012f)
        {
            var high = new List<Renderer>();
            var low = new List<Renderer>();
            foreach (MeshRenderer r in model.GetComponentsInChildren<MeshRenderer>())
            {
                var filter = r.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.name.StartsWith("Qazrix_")) continue; // solo las mallas de alta resolución
                string kind = filter.sharedMesh.name.Substring("Qazrix_".Length); // Sphere, Capsule, Cylinder, Cube
                var simple = Resources.GetBuiltinResource<Mesh>(kind + ".fbx");
                if (simple == null) continue;

                var copy = new GameObject(r.name + "_LOD1");
                copy.transform.SetParent(r.transform.parent, false);
                copy.transform.localPosition = r.transform.localPosition;
                copy.transform.localRotation = r.transform.localRotation;
                copy.transform.localScale = r.transform.localScale;
                copy.AddComponent<MeshFilter>().sharedMesh = simple;
                var copyRenderer = copy.AddComponent<MeshRenderer>();
                copyRenderer.sharedMaterial = r.sharedMaterial;
                high.Add(r);
                low.Add(copyRenderer);
            }

            var group = model.gameObject.AddComponent<LODGroup>();
            group.SetLODs(new[]
            {
                new LOD(nearTransition, high.ToArray()),
                new LOD(cullTransition, low.ToArray())
            });
            group.RecalculateBounds();
            return group;
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

        /// <summary>Resolución por defecto de las texturas de personaje, en píxeles por lado.</summary>
        public const int DefaultResolution = 256;

        /// <summary>Material con textura generada. El color va en la textura; el material queda en blanco.</summary>
        public static Material Textured(string folder, string name, Color color, Pattern pattern, float tiling,
            bool metal = false, int resolution = DefaultResolution)
        {
            Texture2D texture = GetOrCreateTexture(folder, name, color, pattern, metal, resolution);
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

        /// <summary>
        /// Ruido de Perlin que se repite sin costura al tilear la textura. <paramref name="u"/> y
        /// <paramref name="v"/> van de 0 a 1 dentro de la textura; <paramref name="frequency"/> es el número
        /// de «manchas» por lado; <paramref name="offset"/> separa unas capas de otras.
        /// </summary>
        private static float TileNoise(float u, float v, float frequency, float offset)
        {
            float a = Mathf.PerlinNoise(u * frequency + offset, v * frequency + offset);
            float b = Mathf.PerlinNoise((u - 1f) * frequency + offset, v * frequency + offset);
            float c = Mathf.PerlinNoise(u * frequency + offset, (v - 1f) * frequency + offset);
            float d = Mathf.PerlinNoise((u - 1f) * frequency + offset, (v - 1f) * frequency + offset);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        /// <summary>
        /// Textura generada por código, siempre igual para el mismo nombre. Los patrones se definen en
        /// coordenadas relativas (0–1), así que la escala visual no cambia con la resolución.
        /// </summary>
        private static Texture2D GetOrCreateTexture(string folder, string name, Color color, Pattern pattern,
            bool metal, int size)
        {
            Directory.CreateDirectory(folder);
            string path = $"{folder}/{name}.png";

            var random = new System.Random(StableSeed(name));
            // Cada hilo de la trama tiene un grosor algo distinto: es lo que hace que la tela no parezca un damero.
            int thread = Mathf.Max(1, size / 64);
            int threads = size / thread + 1;
            var warp = new float[threads];
            var weft = new float[threads];
            for (int i = 0; i < threads; i++)
            {
                warp[i] = (float)random.NextDouble();
                weft[i] = (float)random.NextDouble();
            }

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = y / (float)size;
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float noise = (float)random.NextDouble();
                    float shade;
                    float warm = 0f; // desplaza el tono hacia cálido (+) o frío (-)
                    switch (pattern)
                    {
                        case Pattern.Weave: // sarga fina: hilos en diagonal, con hilos más claros y más oscuros
                        {
                            int tx = x / thread, ty = y / thread;
                            float twill = (tx + ty) % 4 < 2 ? 1.05f : 0.89f;
                            float yarn = 0.93f + 0.10f * warp[tx] + 0.06f * weft[ty];
                            float slub = 0.94f + 0.12f * TileNoise(u, v, 5f, 11.3f); // vetas de hilo grueso
                            shade = twill * yarn * slub * (0.97f + 0.05f * noise);
                            break;
                        }
                        case Pattern.Pleats: // pliegues verticales y suaves de una tela amplia, con roce
                        {
                            float fold = Mathf.Pow(Mathf.Abs(Mathf.Sin(u * Mathf.PI * 9f)), 0.6f);
                            float wear = 0.90f + 0.14f * TileNoise(u, v, 6f, 5.7f);
                            shade = (0.70f + 0.36f * fold) * wear * (0.97f + 0.05f * noise);
                            break;
                        }
                        case Pattern.Leather: // manchas, arrugas finas y zonas gastadas más claras
                        {
                            float blotch = TileNoise(u, v, 11f, 3.1f);
                            float ridge = 1f - Mathf.Abs(2f * TileNoise(u, v, 5f, 21.4f) - 1f);
                            float worn = Mathf.Clamp01((TileNoise(u, v, 3f, 8.8f) - 0.62f) * 4f);
                            shade = 0.80f + 0.26f * blotch - 0.16f * ridge * ridge * ridge + 0.10f * worn
                                    + 0.05f * (noise - 0.5f);
                            break;
                        }
                        default: // piel o metal liso
                        {
                            float tone = TileNoise(u, v, 3f, 14.2f);
                            shade = 0.96f + 0.08f * tone + 0.03f * (noise - 0.5f);
                            warm = tone - 0.5f;
                            if (metal) shade *= 0.90f + 0.14f * TileNoise(u, v * 12f, 2f, 2.5f); // vetas de pulido en horizontal
                            break;
                        }
                    }
                    float r = color.r * shade * (1f + 0.05f * warm);
                    float g = color.g * shade;
                    float b = color.b * shade * (1f - 0.05f * warm);
                    pixels[y * size + x] = new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), 1f);
                }
            }

            var generated = new Texture2D(size, size, TextureFormat.RGB24, true);
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
