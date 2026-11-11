using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Qazrix.Simulation.World;
using Qazrix.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Qazrix.EditorTools
{
    /// <summary>
    /// Informe de georreferenciación de Z01 (spec §5.6 y §8.6). Para cada punto de control compara:
    ///  · la conversión a metros locales hecha en C# con la del script GIS (deben coincidir);
    ///  · la cota del relieve ya importado en Unity con la cota leída por GDAL del MDT02 original.
    /// Lo que NO mide es si la coordenada publicada cae de verdad sobre el monumento: eso exige
    /// contrastar con ortofoto o medición de campo, y se deja anotado.
    /// </summary>
    public static class Z01GeoReport
    {
        public const string ReportPath = "Reports/QA/z01_control_points.md";
        public const string CsvPath = "Reports/QA/z01_control_points.csv";

        /// <summary>Error vertical admitido entre Unity y GDAL (m). El relieve se guarda en 16 bits y se triangula.</summary>
        public const double VerticalTolerance = 0.25;

        /// <summary>Diferencia admitida entre la conversión de C# y la del script (m).</summary>
        public const double ConversionTolerance = 0.002;

        [MenuItem("Qazrix/Mapa/Informe de puntos de control Z01", priority = 41)]
        public static void RunFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            int errors = Run();
            EditorUtility.DisplayDialog("Puntos de control Z01",
                errors == 0 ? $"Sin errores. Informe en {ReportPath}" : $"{errors} error(es). Informe en {ReportPath}", "Aceptar");
        }

        /// <summary>-executeMethod Qazrix.EditorTools.Z01GeoReport.RunBatch (salida 0 si todo está en tolerancia)</summary>
        public static void RunBatch()
        {
            int code;
            try { code = Run() == 0 ? 0 : 1; }
            catch (Exception e) { Debug.LogException(e); code = 2; }
            EditorApplication.Exit(code);
        }

        private static int Run()
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            Z01GeoData.Meta meta = Z01GeoData.LoadMeta();
            HeightGrid terrainGrid = Z01GeoData.LoadGrid(meta, meta.terrainFile);
            HeightGrid surfaceGrid = Z01GeoData.LoadGrid(meta, meta.surfaceFile);
            List<Z01GeoData.ControlPoint> all = Z01GeoData.LoadControlPoints();
            List<Z01GeoData.ControlPoint> points = all.Where(p => p.IsControl).ToList();
            List<Z01GeoData.ControlPoint> discarded = all.Where(p => !p.IsControl).ToList();

            EditorSceneManager.OpenScene(Z01GeoBuilder.ScenePath, OpenSceneMode.Single);
            GameObject terrainObject = GameObject.Find(Z01GeoBuilder.TerrainName);
            if (terrainObject == null) throw new InvalidOperationException("La escena Z01 no tiene el relieve. Regenérala desde el menú Qazrix → Mapa.");
            var terrain = terrainObject.GetComponent<Terrain>();

            var md = new StringBuilder();
            var csv = new StringBuilder("id,name,easting,northing,unity_x,unity_y,unity_z,conversion_error_m,gdal_height,unity_height,vertical_error_m,surface_minus_terrain_at_point,volume_height,confidence,result\n");
            int errors = 0;
            var verticalErrors = new List<double>();

            md.AppendLine("# Puntos de control de Z01");
            md.AppendLine();
            md.AppendLine($"- Fecha: {DateTime.Now:yyyy-MM-dd HH:mm} · Unity {Application.unityVersion}");
            md.AppendLine($"- Origen local: E {meta.originEasting.ToString("0.###", inv)}, N {meta.originNorthing.ToString("0.###", inv)}, cota {meta.originHeight.ToString("0.###", inv)} m ({meta.crs}); X = este, Y = altura, Z = norte.");
            md.AppendLine($"- Relieve: {meta.terrainSource}, {meta.samples}×{meta.samples} muestras cada {meta.cellSize.ToString("0.###", inv)} m. Superficie: {meta.surfaceSource}.");
            md.AppendLine($"- Tolerancias: conversión {ConversionTolerance * 1000:0} mm; cota Unity frente a GDAL {VerticalTolerance.ToString("0.00", inv)} m.");
            md.AppendLine();
            md.AppendLine("| Punto | Este / Norte (fuente) | Unity X / Y / Z | Error de conversión | Cota GDAL | Cota Unity | Error vertical | Confianza | Resultado |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|");

            foreach (Z01GeoData.ControlPoint p in points)
            {
                LocalPoint local = meta.Origin.ToLocal(p.Easting, p.Northing, p.TerrainHeight);
                Vector3 unity = local.ToUnity();

                // 1) La conversión de C# frente a la del script GIS (columnas local_*).
                double conversionError = GeoOrigin.HorizontalError(local, new LocalPoint(p.LocalX, p.LocalY, p.LocalZ));

                // 2) El relieve importado frente al MDT02 original leído por GDAL.
                bool inside = terrainGrid.Contains(p.LocalX, p.LocalZ);
                double unityHeight = double.NaN, verticalError = double.NaN, surfaceDelta = double.NaN, volumeHeight = double.NaN;
                if (inside)
                {
                    unityHeight = terrain.SampleHeight(unity) + terrain.transform.position.y + meta.originHeight;
                    verticalError = unityHeight - p.TerrainHeight;
                    verticalErrors.Add(Math.Abs(verticalError));
                    surfaceDelta = surfaceGrid.Sample(p.LocalX, p.LocalZ) - terrainGrid.Sample(p.LocalX, p.LocalZ);
                    Z01GeoBuilder.MeasureVolume(p, terrainGrid, surfaceGrid, out _, out volumeHeight);
                }

                bool ok = inside && conversionError <= ConversionTolerance && Math.Abs(verticalError) <= VerticalTolerance;
                if (!ok) errors++;
                string result = !inside ? "FUERA DE ZONA" : (ok ? "en tolerancia" : "FUERA DE TOLERANCIA");

                md.AppendLine(string.Format(inv,
                    "| {0} {1} | {2:0.000} / {3:0.000} | {4:0.00} / {5:0.00} / {6:0.00} | {7:0.0000} m | {8:0.000} m | {9:0.000} m | {10:+0.000;-0.000} m | {11} | {12} |",
                    p.Id, p.Name, p.Easting, p.Northing, unity.x, unity.y, unity.z, conversionError,
                    p.TerrainHeight, unityHeight, verticalError, p.Confidence, result));

                csv.AppendLine(string.Format(inv, "{0},{1},{2:0.000},{3:0.000},{4:0.000},{5:0.000},{6:0.000},{7:0.0000},{8:0.000},{9:0.000},{10:0.000},{11:0.00},{12:0.00},{13},{14}",
                    p.Id, p.Name, p.Easting, p.Northing, unity.x, unity.y, unity.z, conversionError,
                    p.TerrainHeight, unityHeight, verticalError, surfaceDelta, volumeHeight, p.Confidence, result));
            }

            md.AppendLine();
            if (verticalErrors.Count > 0)
            {
                verticalErrors.Sort();
                md.AppendLine(string.Format(inv, "**Error vertical:** mediana {0:0.000} m, máximo {1:0.000} m ({2} puntos).",
                    verticalErrors[verticalErrors.Count / 2], verticalErrors[verticalErrors.Count - 1], verticalErrors.Count));
                md.AppendLine();
            }

            md.AppendLine("## Volúmenes");
            md.AppendLine();
            md.AppendLine("Cada volumen es la caja envolvente del cuerpo de 15 m o más medido en el LiDAR, con su altura máxima. Es un volumen simple de prueba: no es la huella real ni la forma del edificio.");
            md.AppendLine();
            md.AppendLine("| Punto | Caja (este-oeste × norte-sur) | Altura | Fuente |");
            md.AppendLine("|---|---|---|---|");
            foreach (Z01GeoData.ControlPoint p in points)
                md.AppendLine(string.Format(inv, "| {0} {1} | {2:0} × {3:0} m | {4:0.0} m | {5} |", p.Id, p.Name, p.SizeEastWest, p.SizeNorthSouth, p.BuiltHeight, p.Source));

            if (discarded.Count > 0)
            {
                md.AppendLine();
                md.AppendLine("## Puntos descartados");
                md.AppendLine();
                md.AppendLine("Se conservan solo como registro; no se colocan en la escena.");
                md.AppendLine();
                md.AppendLine("| Punto | Este / Norte | Fuente |");
                md.AppendLine("|---|---|---|");
                foreach (Z01GeoData.ControlPoint p in discarded)
                    md.AppendLine(string.Format(inv, "| {0} {1} | {2:0.0} / {3:0.0} | {4} |", p.Id, p.Name, p.Easting, p.Northing, p.Source));
            }

            md.AppendLine();
            md.AppendLine("## Avisos");
            md.AppendLine();
            md.AppendLine("- **Qué garantizan estos puntos:** la posición sale del LiDAR PNOA 2018 (celdas de 1 m) y el nombre lo confirmó Jorge sobre el plano de candidatos. Cada punto es el centro del cuerpo de 15 m o más, no una esquina medida en campo.");
            md.AppendLine("- **Qué falta:** el error horizontal de la spec (§5.6) compara estos puntos con la geometría modelada, y aún no hay edificios modelados: se medirá en F2. Son 3 puntos; la spec pide de 15 a 25.");
            md.AppendLine("- El error de conversión y el vertical de la tabla validan la cadena de datos a Unity, no la posición de los monumentos.");
            md.AppendLine("- Licencia de DATA-001, DATA-002 y DATA-003 pendiente de confirmar en la ficha de cada producto del CNIG.");
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            var utf8 = new UTF8Encoding(false);
            File.WriteAllText(ReportPath, md.ToString(), utf8);
            File.WriteAllText(CsvPath, csv.ToString(), utf8);

            Debug.Log($"[Qazrix] Informe de puntos de control: {ReportPath} · {errors} fuera de tolerancia de {points.Count}");
            return errors;
        }
    }
}
