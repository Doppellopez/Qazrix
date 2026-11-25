using System;
using System.Globalization;
using System.IO;
using System.Text;
using Qazrix.Simulation.World;
using UnityEditor;
using UnityEngine;

namespace Qazrix.EditorTools
{
    /// <summary>
    /// Exporta los edificios de Z01 tal como los levanta el juego (las mismas huellas poligonales de <c>CityBlocks</c>) a
    /// <c>Research/Survey/city_parts.json</c>, con un número por edificio, para dibujar planos y señalar sobre ellos qué
    /// volúmenes son muralla. El número es el índice en la lista que da <see cref="BuildingFootprints.Extract"/>: estable mientras
    /// no cambien los datos de tejados ni las opciones.
    /// </summary>
    public static class CityPlanExport
    {
        public const string OutputPath = "Research/Survey/city_parts.json";

        [Serializable]
        private sealed class GridMeta
        {
            public int columns;
            public int rows;
            public float cellSize;
            public float localWestX;
            public float localNorthZ;
        }

        /// <summary>-executeMethod Qazrix.EditorTools.CityPlanExport.RunBatch (con -nographics)</summary>
        public static void RunBatch()
        {
            int code = 0;
            try { Export(); }
            catch (Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }
            EditorApplication.Exit(code);
        }

        public static void Export()
        {
            var meta = JsonUtility.FromJson<GridMeta>(AssetDatabase.LoadAssetAtPath<TextAsset>(Z01GeoBuilder.RoofsMetaPath).text);
            byte[] bytes = AssetDatabase.LoadAssetAtPath<TextAsset>(Z01GeoBuilder.RoofsPath).bytes;
            var roofs = new float[meta.columns * meta.rows];
            Buffer.BlockCopy(bytes, 0, roofs, 0, bytes.Length);

            // Las mismas opciones que CreateCity (huellas poligonales, salto de 0,5 m entre volúmenes).
            var options = new FootprintOptions
            {
                Tolerance = Z01GeoBuilder.CityFootprintTolerance,
                ChamferSize = Z01GeoBuilder.CityFootprintChamfer,
                Step = 0.5f
            };
            var parts = BuildingFootprints.Extract(roofs, meta.columns, meta.rows, meta.cellSize, meta.localWestX, meta.localNorthZ, options);

            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("{\"note\":\"Edificios de Z01 como los levanta el juego. id = indice en BuildingFootprints.Extract. Metros locales: x este, z norte.\",\"parts\":[");
            for (int i = 0; i < parts.Count; i++)
            {
                BuildingPart p = parts[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"id\":").Append(i)
                  .Append(",\"roof\":").Append(p.RoofHeight.ToString("0.00", inv))
                  .Append(",\"area\":").Append(p.Area.ToString("0.0", inv))
                  .Append(",\"volumeArea\":").Append(p.VolumeArea.ToString("0.0", inv))
                  .Append(",\"cx\":").Append(p.Centre.X.ToString("0.00", inv))
                  .Append(",\"cz\":").Append(p.Centre.Z.ToString("0.00", inv))
                  .Append(",\"loop\":[");
                Vec2[] loop = p.Loops[0];
                for (int k = 0; k < loop.Length; k++)
                {
                    if (k > 0) sb.Append(',');
                    sb.Append(loop[k].X.ToString("0.00", inv)).Append(',').Append(loop[k].Z.ToString("0.00", inv));
                }
                sb.Append("]}");
            }
            sb.Append("]}");

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            File.WriteAllText(OutputPath, sb.ToString(), new UTF8Encoding(false));
            Debug.Log($"[Qazrix] Edificios exportados: {parts.Count} → {OutputPath}");
        }
    }
}
