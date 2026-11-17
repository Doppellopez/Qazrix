using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Qazrix.EditorTools
{
    /// <summary>
    /// Icono del ejecutable: el guardia cargando con la cimitarra en alto. Parte de la imagen que saca la prueba
    /// <c>IconCaptureTests</c> (el guardia sobre fondo transparente), lo recorta y lo centra (con insignia de fondo
    /// solo si <see cref="UseBadge"/>) y genera el PNG de 1024 px, el archivo .ico (16 a 256 px) y el icono por defecto del proyecto, que Unity
    /// incrusta en el ejecutable al hacer el build. Todo sale de modelos propios del proyecto (GEN-004).
    /// </summary>
    public static class GameIcon
    {
        public const string SourcePath = "Reports/QA/icono_origen.png";
        public const string AssetFolder = "Assets/_Qazrix/Art/Icon";
        public const string AssetPng = AssetFolder + "/Qazrix_Icono.png";
        public const string DesignFolder = "Design/Icono";
        private const int Size = 1024;

        /// <summary>Con falso el icono es solo el personaje sobre fondo transparente; con verdadero, sobre una insignia roja.</summary>
        public const bool UseBadge = false;

        [MenuItem("Qazrix/Build/Crear icono del juego", priority = 75)]
        public static void BuildFromMenu() => Build();

        /// <summary>-executeMethod Qazrix.EditorTools.GameIcon.BuildBatch</summary>
        public static void BuildBatch()
        {
            int code = 0;
            try { Build(); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        public static void Build()
        {
            if (!File.Exists(SourcePath))
                throw new FileNotFoundException($"Falta {SourcePath}: lánzalo con la prueba IconCaptureTests (README).");

            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            source.LoadImage(File.ReadAllBytes(SourcePath));
            Color32[] icon = Compose(source.GetPixels32(), source.width, source.height);
            UnityEngine.Object.DestroyImmediate(source);

            Directory.CreateDirectory(AssetFolder);
            Directory.CreateDirectory(DesignFolder);
            byte[] png = EncodePng(icon, Size);
            File.WriteAllBytes(AssetPng, png);
            File.WriteAllBytes(DesignFolder + "/Qazrix_Icono_1024.png", png);

            // .ico con PNG dentro, de 256 a 16 px.
            var sizes = new[] { 256, 128, 64, 48, 32, 16 };
            var images = new List<byte[]>();
            foreach (int size in sizes) images.Add(EncodePng(Downscale(icon, Size, size), size));
            File.WriteAllBytes(DesignFolder + "/Qazrix.ico", BuildIco(sizes, images));

            AssetDatabase.ImportAsset(AssetPng, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetPng);
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetPng);
            if (texture == null) throw new InvalidOperationException($"No se pudo importar {AssetPng}");

            // Icono por defecto del proyecto: Unity lo incrusta en el .exe (y lo escala a cada tamaño).
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { texture }, IconKind.Application);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Qazrix] Icono creado: {AssetPng}, {DesignFolder}/Qazrix.ico y {DesignFolder}/Qazrix_Icono_1024.png; icono por defecto del proyecto asignado.");
        }

        // ------------------------------------------------------------------ composición

        /// <summary>Insignia cuadrada de esquinas redondeadas con un degradado radial rojo oscuro y los personajes encima.</summary>
        private static Color32[] Compose(Color32[] characters, int sourceWidth, int sourceHeight)
        {
            // Recuadro que ocupan los personajes (píxeles con alfa), para centrarlos y llenar la insignia.
            int minX = sourceWidth, minY = sourceHeight, maxX = -1, maxY = -1;
            for (int y = 0; y < sourceHeight; y++)
            {
                for (int x = 0; x < sourceWidth; x++)
                {
                    if (characters[y * sourceWidth + x].a < 20) continue;
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                }
            }
            if (maxX < 0) throw new InvalidOperationException("La imagen de origen está vacía: no hay personajes.");

            float boxW = maxX - minX + 1, boxH = maxY - minY + 1;
            float scale = (UseBadge ? 0.80f : 0.94f) * Size / Math.Max(boxW, boxH);
            float offX = (Size - boxW * scale) * 0.5f, offY = (Size - boxH * scale) * 0.5f - (UseBadge ? 0.02f * Size : 0f);

            var result = new Color32[Size * Size];
            const float corner = 0.19f * Size;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    // Sin insignia: solo el personaje, con el fondo transparente.
                    if (!UseBadge)
                    {
                        float tx = minX + (x - offX) / scale, ty = minY + (y - offY) / scale;
                        Color only = SampleBilinear(characters, sourceWidth, sourceHeight, tx, ty);
                        result[y * Size + x] = new Color(only.r, only.g, only.b, only.a);
                        continue;
                    }

                    float mask = RoundedRect(x + 0.5f, y + 0.5f, Size, corner);
                    if (mask <= 0f) { result[y * Size + x] = new Color32(0, 0, 0, 0); continue; }

                    // Fondo: rojo vino en el centro, casi negro en los bordes, con un aro claro fino junto al borde.
                    float dx = (x + 0.5f) / Size - 0.5f, dy = (y + 0.5f) / Size - 0.5f;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 0.72f);
                    Color background = Color.Lerp(new Color(0.55f, 0.13f, 0.12f), new Color(0.10f, 0.04f, 0.05f), d * d);
                    float edge = RoundedRectDistance(x + 0.5f, y + 0.5f, Size, corner);
                    if (edge < 14f && edge >= 6f) background = Color.Lerp(background, new Color(0.93f, 0.78f, 0.45f), 0.75f); // aro dorado

                    // Personajes: se toma la imagen de origen reescalada (con interpolación) y se mezcla con su alfa.
                    float sx = minX + (x - offX) / scale, sy = minY + (y - offY) / scale;
                    Color character = SampleBilinear(characters, sourceWidth, sourceHeight, sx, sy);
                    Color blended = Color.Lerp(background, new Color(character.r, character.g, character.b, 1f), character.a);
                    result[y * Size + x] = new Color(blended.r, blended.g, blended.b, mask);
                }
            }
            return result;
        }

        /// <summary>Cobertura (0 a 1) de un cuadrado de esquinas redondeadas, con un píxel de suavizado.</summary>
        private static float RoundedRect(float x, float y, float size, float radius)
        {
            return Mathf.Clamp01(0.5f - RoundedRectDistanceSigned(x, y, size, radius));
        }

        /// <summary>Distancia (px) hacia dentro desde el borde del cuadrado redondeado; 0 en el borde.</summary>
        private static float RoundedRectDistance(float x, float y, float size, float radius)
        {
            return Mathf.Max(0f, -RoundedRectDistanceSigned(x, y, size, radius));
        }

        /// <summary>Distancia con signo al borde: negativa dentro del cuadrado redondeado.</summary>
        private static float RoundedRectDistanceSigned(float x, float y, float size, float radius)
        {
            float half = size * 0.5f;
            float px = Mathf.Abs(x - half) - (half - radius), py = Mathf.Abs(y - half) - (half - radius);
            float outside = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude;
            float inside = Mathf.Min(Mathf.Max(px, py), 0f);
            return outside + inside - radius;
        }

        private static Color SampleBilinear(Color32[] pixels, int width, int height, float x, float y)
        {
            x = Mathf.Clamp(x - 0.5f, 0f, width - 1.001f);
            y = Mathf.Clamp(y - 0.5f, 0f, height - 1.001f);
            int x0 = (int)x, y0 = (int)y;
            float tx = x - x0, ty = y - y0;
            Color a = pixels[y0 * width + x0], b = pixels[y0 * width + x0 + 1];
            Color c = pixels[(y0 + 1) * width + x0], d = pixels[(y0 + 1) * width + x0 + 1];
            // Se mezcla con alfa premultiplicado para que el borde de los personajes no se oscurezca.
            Color ab = Premultiplied(a) * (1f - tx) + Premultiplied(b) * tx;
            Color cd = Premultiplied(c) * (1f - tx) + Premultiplied(d) * tx;
            Color p = ab * (1f - ty) + cd * ty;
            return p.a > 1e-4f ? new Color(p.r / p.a, p.g / p.a, p.b / p.a, p.a) : new Color(0f, 0f, 0f, 0f);
        }

        private static Color Premultiplied(Color c) => new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);

        // ------------------------------------------------------------------ archivos

        private static byte[] EncodePng(Color32[] pixels, int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            byte[] png = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            return png;
        }

        /// <summary>Reduce promediando bloques de píxeles (con alfa premultiplicado).</summary>
        private static Color32[] Downscale(Color32[] pixels, int from, int to)
        {
            var result = new Color32[to * to];
            float ratio = (float)from / to;
            for (int y = 0; y < to; y++)
            {
                for (int x = 0; x < to; x++)
                {
                    int x0 = Mathf.FloorToInt(x * ratio), x1 = Mathf.Min(from, Mathf.CeilToInt((x + 1) * ratio));
                    int y0 = Mathf.FloorToInt(y * ratio), y1 = Mathf.Min(from, Mathf.CeilToInt((y + 1) * ratio));
                    float r = 0, g = 0, b = 0, a = 0;
                    int n = 0;
                    for (int sy = y0; sy < y1; sy++)
                    {
                        for (int sx = x0; sx < x1; sx++)
                        {
                            Color32 p = pixels[sy * from + sx];
                            float alpha = p.a / 255f;
                            r += p.r * alpha; g += p.g * alpha; b += p.b * alpha; a += alpha;
                            n++;
                        }
                    }
                    if (n == 0 || a < 1e-4f) { result[y * to + x] = new Color32(0, 0, 0, 0); continue; }
                    result[y * to + x] = new Color32((byte)Mathf.Round(r / a), (byte)Mathf.Round(g / a), (byte)Mathf.Round(b / a), (byte)Mathf.Round(255f * a / n));
                }
            }
            return result;
        }

        /// <summary>Archivo .ico con una imagen PNG por tamaño (formato admitido desde Windows Vista).</summary>
        private static byte[] BuildIco(int[] sizes, List<byte[]> images)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((ushort)0);                 // reservado
                writer.Write((ushort)1);                 // tipo: icono
                writer.Write((ushort)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    writer.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));   // ancho (0 = 256)
                    writer.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));   // alto
                    writer.Write((byte)0);                                  // colores de paleta
                    writer.Write((byte)0);
                    writer.Write((ushort)1);                                // planos
                    writer.Write((ushort)32);                               // bits por píxel
                    writer.Write((uint)images[i].Length);
                    writer.Write((uint)offset);
                    offset += images[i].Length;
                }
                foreach (byte[] image in images) writer.Write(image);
                return stream.ToArray();
            }
        }
    }
}
