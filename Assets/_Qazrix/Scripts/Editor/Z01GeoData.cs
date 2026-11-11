using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Qazrix.Simulation.World;
using UnityEngine;

namespace Qazrix.EditorTools
{
    /// <summary>
    /// Lectura de los datos derivados de Z01 que genera <c>Tools/GIS/make_z01.ps1</c>:
    /// metadatos, rejillas de cotas y puntos de control. Solo lee; no modifica nada.
    /// </summary>
    public static class Z01GeoData
    {
        public const string DerivedFolder = "Research/Derived/Z01";
        public const string MetaPath = DerivedFolder + "/z01_meta.json";
        public const string ControlPointsPath = "Research/Survey/GroundControlPoints.csv";

        [Serializable]
        public sealed class Meta
        {
            public string zone;
            public string crs;
            public double originEasting;
            public double originNorthing;
            public double originHeight;
            public int samples;
            public double cellSize;
            public string rowOrder;
            public string terrainFile;
            public string surfaceFile;
            public string terrainSource;
            public string surfaceSource;
            public string tool;

            public GeoOrigin Origin => new GeoOrigin(originEasting, originNorthing, originHeight);
        }

        public sealed class ControlPoint
        {
            public string Id;
            public string Name;
            public double Easting;
            public double Northing;
            public double TerrainHeight;
            public double SurfaceHeight;
            public double LocalX;
            public double LocalY;
            public double LocalZ;
            public string Confidence;
            public string Source;
            public string Role;
            public double SizeEastWest;   // 0 si no se conoce
            public double SizeNorthSouth; // 0 si no se conoce
            public double BuiltHeight;    // 0 si no se conoce

            public bool IsControl => Role == "control";
        }

        public static Meta LoadMeta()
        {
            if (!File.Exists(MetaPath))
                throw new FileNotFoundException($"Falta {MetaPath}. Ejecuta Tools/GIS/make_z01.ps1 (README).");

            var meta = JsonUtility.FromJson<Meta>(File.ReadAllText(MetaPath, Encoding.UTF8));
            if (meta.rowOrder != "north-to-south") throw new InvalidDataException($"Orden de filas no soportado: {meta.rowOrder}");
            if (meta.crs != "EPSG:25829") throw new InvalidDataException($"Sistema de referencia inesperado: {meta.crs}");
            return meta;
        }

        public static HeightGrid LoadGrid(Meta meta, string fileName)
        {
            string path = DerivedFolder + "/" + fileName;
            byte[] bytes = File.ReadAllBytes(path);
            int count = meta.samples * meta.samples;
            if (bytes.Length != count * sizeof(float))
                throw new InvalidDataException($"{path}: {bytes.Length} bytes, se esperaban {count * sizeof(float)}.");
            if (!BitConverter.IsLittleEndian) throw new PlatformNotSupportedException("Los datos son little-endian.");

            var heights = new float[count];
            Buffer.BlockCopy(bytes, 0, heights, 0, bytes.Length);
            return new HeightGrid(heights, meta.samples, meta.cellSize);
        }

        public static List<ControlPoint> LoadControlPoints()
        {
            if (!File.Exists(ControlPointsPath))
                throw new FileNotFoundException($"Falta {ControlPointsPath}. Ejecuta Tools/GIS/make_z01.ps1 (README).");

            string[] lines = File.ReadAllLines(ControlPointsPath, Encoding.UTF8);
            List<string> header = SplitCsvLine(lines[0]);
            var points = new List<ControlPoint>();

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                List<string> cells = SplitCsvLine(lines[i]);
                string Text(string column)
                {
                    int index = header.IndexOf(column);
                    if (index < 0) throw new InvalidDataException($"Falta la columna «{column}» en {ControlPointsPath}.");
                    return cells[index];
                }
                double Number(string column) => double.Parse(Text(column), CultureInfo.InvariantCulture);
                double Optional(string column) => string.IsNullOrWhiteSpace(Text(column)) ? 0.0 : Number(column);

                points.Add(new ControlPoint
                {
                    Id = Text("id"),
                    Name = Text("name"),
                    Easting = Number("easting"),
                    Northing = Number("northing"),
                    TerrainHeight = Number("mdt_height"),
                    SurfaceHeight = Number("mds_height"),
                    LocalX = Number("local_x"),
                    LocalY = Number("local_y"),
                    LocalZ = Number("local_z"),
                    Confidence = Text("confidence"),
                    Source = Text("source"),
                    Role = Text("role"),
                    SizeEastWest = Optional("size_ew"),
                    SizeNorthSouth = Optional("size_ns"),
                    BuiltHeight = Optional("height_m")
                });
            }
            return points;
        }

        private static List<string> SplitCsvLine(string line)
        {
            var cells = new List<string>();
            var current = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else current.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == ',') { cells.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }
            cells.Add(current.ToString());
            return cells;
        }
    }
}
