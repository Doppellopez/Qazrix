# -*- coding: utf-8 -*-
"""F0-B — Contraste horizontal de los puntos de control con el LiDAR PNOA 2018.

Rasteriza los puntos LiDAR de clase 6 (edificios) alrededor de la Plaza Mayor, les resta el
relieve MDT02 y mide, para cada punto de control, a qué distancia queda de lo construido y del
edificio alto más cercano.

Qué mide y qué no: da una cota inferior del error de cada coordenada publicada (si cae fuera de
todo edificio alto, no puede estar sobre la torre). NO identifica monumentos: decir que «el
edificio alto más cercano» es el monumento buscado es una hipótesis, y así se rotula.

Uso (con el Python de QGIS 3.44, que trae GDAL, numpy y PDAL; no descarga nada):
    "C:\\Program Files\\QGIS 3.44.15\\bin\\python-qgis-ltr.bat" Tools\\GIS\\gcp_lidar_check.py
"""
import csv
import json
import math
import os
import subprocess
import sys
from collections import deque
from datetime import datetime

import numpy as np
from osgeo import gdal

gdal.UseExceptions()

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "Research", "Source", "CNIG")
OUT = os.path.join(ROOT, "Research", "Derived", "Z01")
QA = os.path.join(ROOT, "Reports", "QA")
LAZ = os.path.join(SRC, "PNOA_2018_EXT_N_726-4374_ORT-CLA-IRC.laz")
MDT = os.path.join(SRC, "MDT02-ETRS89-HU29-0704-1-COB2.tif")
GCP = os.path.join(ROOT, "Research", "Survey", "GroundControlPoints.csv")
PDAL = os.path.join(os.environ.get("OSGEO4W_ROOT", r"C:\Program Files\QGIS 3.44.15"), "bin", "pdal.exe")

# Ventana de trabajo en ETRS89 / UTM 29N (EPSG:25829): 400 x 400 m, celdas de 1 m.
MIN_E, MIN_N, SIZE, CELL = 726000.0, 4372600.0, 400, 1.0
NODATA = -9999.0

# Umbrales de trabajo (hipótesis, no medidas):
BUILT_MIN = 2.5    # m sobre el terreno para contar una celda como construida
TALL_MIN = 15.0    # m sobre el terreno para contar una celda como «edificio alto» (torres, iglesia)


def rasterize_buildings(path):
    """Cota máxima de los puntos de clase 6 en cada celda de 1 m."""
    pipeline = [
        {"type": "readers.las", "filename": LAZ, "override_srs": "EPSG:25829"},
        {"type": "filters.crop",
         "bounds": "([%f,%f],[%f,%f])" % (MIN_E, MIN_E + SIZE * CELL, MIN_N, MIN_N + SIZE * CELL)},
        {"type": "filters.range", "limits": "Classification[6:6]"},
        {"type": "writers.gdal", "filename": path, "gdaldriver": "GTiff", "data_type": "float32",
         "output_type": "max", "resolution": CELL, "radius": 0.75, "window_size": 0, "nodata": NODATA,
         "origin_x": MIN_E, "origin_y": MIN_N, "width": SIZE, "height": SIZE},
    ]
    pipeline_path = os.path.join(OUT, "z01_lidar_edificios.pipeline.json")
    with open(pipeline_path, "w", encoding="utf-8") as f:
        json.dump(pipeline, f, indent=2)
    subprocess.run([PDAL, "pipeline", pipeline_path], check=True)


def read_band(path):
    ds = gdal.Open(path)
    return ds.GetRasterBand(1).ReadAsArray().astype(np.float64), ds.GetGeoTransform()


def terrain_on_grid():
    """MDT02 remuestreado (bilineal) a la misma rejilla de 1 m."""
    ds = gdal.Warp("", MDT, format="MEM", dstSRS="EPSG:25829", xRes=CELL, yRes=CELL, resampleAlg="bilinear",
                   outputBounds=(MIN_E, MIN_N, MIN_E + SIZE * CELL, MIN_N + SIZE * CELL))
    return ds.GetRasterBand(1).ReadAsArray().astype(np.float64)


def component(mask, start):
    """Celdas conectadas (8 vecinos) a la celda de partida dentro de la máscara."""
    rows, cols = mask.shape
    seen = np.zeros(mask.shape, dtype=bool)
    seen[start] = True
    queue = deque([start])
    cells = []
    while queue:
        r, c = queue.popleft()
        cells.append((r, c))
        for dr in (-1, 0, 1):
            for dc in (-1, 0, 1):
                rr, cc = r + dr, c + dc
                if 0 <= rr < rows and 0 <= cc < cols and mask[rr, cc] and not seen[rr, cc]:
                    seen[rr, cc] = True
                    queue.append((rr, cc))
    return cells


def nearest(mask, row, col):
    """Distancia (m) y celda de la máscara más cercana a (row, col)."""
    rr, cc = np.nonzero(mask)
    if rr.size == 0:
        return float("nan"), None
    d2 = (rr - row) ** 2 + (cc - col) ** 2
    i = int(np.argmin(d2))
    return math.sqrt(float(d2[i])) * CELL, (int(rr[i]), int(cc[i]))


def main():
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(QA, exist_ok=True)

    z_path = os.path.join(OUT, "z01_lidar_edificios_z_1m.tif")
    rasterize_buildings(z_path)
    z, gt = read_band(z_path)
    if gt[5] > 0:  # por si el escritor deja el origen abajo
        z = z[::-1, :]
    top_n = MIN_N + SIZE * CELL

    terrain = terrain_on_grid()
    has_data = z > NODATA + 1
    hag = np.where(has_data, z - terrain, 0.0)

    # Guarda la altura sobre el terreno para reutilizarla al levantar los volúmenes (F2).
    hag_path = os.path.join(OUT, "z01_lidar_edificios_hag_1m.tif")
    drv = gdal.GetDriverByName("GTiff")
    out = drv.Create(hag_path, SIZE, SIZE, 1, gdal.GDT_Float32, options=["COMPRESS=DEFLATE"])
    out.SetGeoTransform((MIN_E, CELL, 0, top_n, 0, -CELL))
    out.SetProjection(gdal.Open(MDT).GetProjection())
    out.GetRasterBand(1).WriteArray(hag.astype(np.float32))
    out.FlushCache()
    out = None

    built = hag >= BUILT_MIN
    tall = hag >= TALL_MIN

    def cell_of(e, n):
        return int((top_n - n) / CELL), int((e - MIN_E) / CELL)

    def centre_of(r, c):
        return MIN_E + (c + 0.5) * CELL, top_n - (r + 0.5) * CELL

    rows = []
    with open(GCP, encoding="utf-8-sig", newline="") as f:
        for p in csv.DictReader(f):
            e, n = float(p["easting"]), float(p["northing"])
            r, c = cell_of(e, n)
            if not (0 <= r < SIZE and 0 <= c < SIZE):
                rows.append({"id": p["id"], "name": p["name"], "outside": True})
                continue

            d_built, _ = nearest(built, r, c)
            d_tall, tall_cell = nearest(tall, r, c)
            info = {"id": p["id"], "name": p["name"], "outside": False, "easting": e, "northing": n,
                    "confidence": p["confidence"], "hag_at_point": float(hag[r, c]),
                    "dist_built": d_built, "dist_tall": d_tall}
            if tall_cell is not None:
                cells = component(tall, tall_cell)
                rr = np.array([k[0] for k in cells])
                cc = np.array([k[1] for k in cells])
                ce, cn = centre_of(rr.mean(), cc.mean())
                info.update({"tall_area": len(cells) * CELL * CELL, "tall_max": float(hag[rr, cc].max()),
                             "tall_e": ce, "tall_n": cn, "dist_centroid": math.hypot(ce - e, cn - n),
                             "bearing": (math.degrees(math.atan2(ce - e, cn - n)) + 360) % 360})
            rows.append(info)

    write_report(rows, int(has_data.sum()), float(hag.max()))
    for row in rows:
        if row["outside"]:
            print("%s: fuera de la ventana LiDAR" % row["id"])
        else:
            print("%s %s: altura en el punto %.1f m; a lo construido %.1f m; a edificio alto %.1f m; a su centro %.1f m"
                  % (row["id"], row["name"], row["hag_at_point"], row["dist_built"], row["dist_tall"],
                     row.get("dist_centroid", float("nan"))))


def write_report(rows, building_cells, max_hag):
    md = []
    md.append("# Contraste de los puntos de control con el LiDAR (Z01)")
    md.append("")
    md.append("- Fecha: %s" % datetime.now().strftime("%Y-%m-%d %H:%M"))
    md.append("- LiDAR: PNOA 2018, cuadro 726-4374, clase 6 (edificios) — DATA-002. Relieve: MDT02 — DATA-001.")
    md.append("- Ventana: E %.0f–%.0f, N %.0f–%.0f (EPSG:25829), celdas de %.0f m. Celdas con edificio: %d; altura máxima sobre el terreno: %.1f m."
              % (MIN_E, MIN_E + SIZE * CELL, MIN_N, MIN_N + SIZE * CELL, CELL, building_cells, max_hag))
    md.append("- Umbrales de trabajo: construido ≥ %.1f m; «edificio alto» ≥ %.1f m sobre el terreno." % (BUILT_MIN, TALL_MIN))
    md.append("")
    md.append("| Punto | Altura construida en el punto | Distancia a lo construido | Distancia al edificio alto más cercano | Ese edificio alto: superficie / altura máx. | Distancia a su centro | Rumbo |")
    md.append("|---|---|---|---|---|---|---|")
    for r in rows:
        if r["outside"]:
            md.append("| %s %s | fuera de la ventana | | | | | |" % (r["id"], r["name"]))
            continue
        if "tall_area" in r:
            md.append("| %s %s | %.1f m | %.1f m | %.1f m | %.0f m² / %.1f m | %.1f m | %.0f° |"
                      % (r["id"], r["name"], r["hag_at_point"], r["dist_built"], r["dist_tall"],
                         r["tall_area"], r["tall_max"], r["dist_centroid"], r["bearing"]))
        else:
            md.append("| %s %s | %.1f m | %.1f m | no hay | | | |" % (r["id"], r["name"], r["hag_at_point"], r["dist_built"]))
    md.append("")
    md.append("## Cómo leerlo")
    md.append("")
    md.append("- **Distancia al edificio alto más cercano** es una cota inferior del error horizontal *si* el monumento es ese edificio. 0 m significa que la coordenada cae sobre un edificio alto; no prueba que sea el correcto.")
    md.append("- **Distancia a su centro** sería el error si la coordenada pretendiera marcar el centro de ese edificio.")
    md.append("- El rumbo va del punto publicado al centro del edificio (0° = norte, 90° = este).")
    md.append("- Identificar el edificio alto con el monumento es una **hipótesis** [RESEARCH]: hace falta ortofoto, plano o visita para confirmarlo.")
    md.append("- El Arco de la Estrella es un vano en la muralla, no una torre: su punto puede caer legítimamente en hueco o en el lienzo contiguo.")
    with open(os.path.join(QA, "z01_gcp_lidar.md"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(md) + "\n")

    fields = ["id", "name", "easting", "northing", "confidence", "hag_at_point", "dist_built", "dist_tall",
              "tall_area", "tall_max", "tall_e", "tall_n", "dist_centroid", "bearing"]
    with open(os.path.join(QA, "z01_gcp_lidar.csv"), "w", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, fieldnames=fields, extrasaction="ignore")
        w.writeheader()
        for r in rows:
            if not r["outside"]:
                w.writerow({k: ("%.3f" % v if isinstance(v, float) else v) for k, v in r.items()})


if __name__ == "__main__":
    sys.exit(main())
