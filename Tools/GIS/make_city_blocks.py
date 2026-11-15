# -*- coding: utf-8 -*-
"""F2 — Tejados del casco antiguo de Cáceres a partir del LiDAR PNOA 2018.

Rasteriza a 1 m los puntos de clase 6 (edificios) del recinto intramuros y su entorno, limpia el
resultado y escribe la cota de tejado de cada celda en metros locales del juego. Unity levanta con
ello los volúmenes de los edificios (CityBlocks).

Es volumetría (spec §5.3, capa B): alturas y huellas medidas, sin fachadas ni detalle. La apariencia
es la actual, no la de 1229.

Uso (con el Python de QGIS 3.44; no descarga nada):
    "C:\\Program Files\\QGIS 3.44.15\\bin\\python-qgis-ltr.bat" Tools\\GIS\\make_city_blocks.py
"""
import json
import os
import subprocess
import sys
from datetime import datetime

import numpy as np
from osgeo import gdal
from scipy import ndimage, sparse
from scipy.sparse import csgraph

gdal.UseExceptions()

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "Research", "Source", "CNIG")
DERIVED = os.path.join(ROOT, "Research", "Derived", "Z01")
DATA = os.path.join(ROOT, "Assets", "_Qazrix", "Data", "Zones", "Z01")
LAZ = os.path.join(SRC, "PNOA_2018_EXT_N_726-4374_ORT-CLA-IRC.laz")
MDT = os.path.join(SRC, "MDT02-ETRS89-HU29-0704-1-COB2.tif")
META = os.path.join(DERIVED, "z01_meta.json")
PDAL = os.path.join(os.environ.get("OSGEO4W_ROOT", r"C:\Program Files\QGIS 3.44.15"), "bin", "pdal.exe")

# Ventana en ETRS89 / UTM 29N (EPSG:25829): recinto intramuros, Plaza Mayor y un margen.
# El límite oeste (E 726000) es el borde del cuadro LiDAR 726-4374.
MIN_E, MIN_N, COLS, ROWS, CELL = 726000.0, 4372420.0, 512, 576, 1.0
NODATA = -9999.0

MIN_HEIGHT = 2.5     # m sobre el terreno para contar una celda como edificio
MIN_BLOCK_CELLS = 12  # m²: por debajo de esto es ruido (toldos, coches altos, restos de árbol)
HOLE_PASSES = 2       # pasadas de relleno de huecos de una celda dentro de los tejados

# Calles transitables. Con un radio de 0,75 m cada edificio engordaba y las calles de menos de 2 m
# desaparecían; con 0,5 m (media celda) la huella queda en su sitio. Además, todo pasillo de una
# sola celda se ensancha quitando la celda construida de cada lado: queda de 3 m.
RASTER_RADIUS = 0.5
WIDEN_NARROW_STREETS = True

# Edificios separados: salto de tejado (m) que separa dos edificios pegados y tamaño mínimo (m²)
# de un edificio; lo menor se une al vecino de altura más parecida.
SPLIT_BUILDINGS = True
ROOF_STEP = 0.9
MIN_BUILDING_CELLS = 30
MIN_TOWER_CELLS = 6   # m²: un cuerpo así de pequeño se conserva si sobresale TOWER_RISE de sus vecinos
TOWER_RISE = 4.0

# Punto de la Plaza Mayor desde el que se mide qué suelo queda conectado a pie.
SEED_E, SEED_N = 726105.0, 4372805.0


def rasterize(path):
    pipeline = [
        {"type": "readers.las", "filename": LAZ, "override_srs": "EPSG:25829"},
        {"type": "filters.crop",
         "bounds": "([%f,%f],[%f,%f])" % (MIN_E, MIN_E + COLS * CELL, MIN_N, MIN_N + ROWS * CELL)},
        {"type": "filters.range", "limits": "Classification[6:6]"},
        {"type": "writers.gdal", "filename": path, "gdaldriver": "GTiff", "data_type": "float32",
         "output_type": "max", "resolution": CELL, "radius": RASTER_RADIUS, "window_size": 0, "nodata": NODATA,
         "origin_x": MIN_E, "origin_y": MIN_N, "width": COLS, "height": ROWS},
    ]
    pipeline_path = os.path.join(DERIVED, "z01_tejados.pipeline.json")
    with open(pipeline_path, "w", encoding="utf-8") as f:
        json.dump(pipeline, f, indent=2)
    subprocess.run([PDAL, "pipeline", pipeline_path], check=True)


def split_buildings(z, built):
    """Parte las manzanas en edificios: dos celdas vecinas son del mismo edificio si sus tejados
    difieren menos de ROOF_STEP. Los trozos menores de MIN_BUILDING_CELLS se unen al vecino de
    altura más parecida. Devuelve (etiquetas desde 1, número de edificios)."""
    rows, cols = z.shape
    ids = np.arange(rows * cols).reshape(rows, cols)
    same_h = built[:, :-1] & built[:, 1:] & (np.abs(z[:, :-1] - z[:, 1:]) < ROOF_STEP)
    same_v = built[:-1, :] & built[1:, :] & (np.abs(z[:-1, :] - z[1:, :]) < ROOF_STEP)
    a = np.concatenate([ids[:, :-1][same_h], ids[:-1, :][same_v]])
    b = np.concatenate([ids[:, 1:][same_h], ids[1:, :][same_v]])
    graph = sparse.coo_matrix((np.ones(a.size, dtype=np.int8), (a, b)), shape=(rows * cols, rows * cols))
    _, comp = csgraph.connected_components(graph, directed=False)
    comp = comp.reshape(rows, cols)
    _, dense = np.unique(comp[built], return_inverse=True)
    labels = np.zeros((rows, cols), dtype=np.int64)
    labels[built] = dense + 1

    cross = ndimage.generate_binary_structure(2, 1)
    for _ in range(6):  # varias pasadas: un trozo pequeño puede quedar rodeado de otros pequeños
        count = int(labels.max())
        index = np.arange(1, count + 1)
        sizes = ndimage.sum(built, labels, index=index)
        medians = ndimage.median(z, labels, index=index)
        small = np.nonzero(sizes < MIN_BUILDING_CELLS)[0] + 1
        if small.size == 0:
            break
        merged = 0
        slices = ndimage.find_objects(labels)
        for lab in small[np.argsort(sizes[small - 1])]:
            sl = slices[lab - 1]
            if sl is None:
                continue  # etiqueta ya absorbida en una pasada anterior
            r0, r1 = max(sl[0].start - 1, 0), min(sl[0].stop + 1, rows)
            c0, c1 = max(sl[1].start - 1, 0), min(sl[1].stop + 1, cols)
            window = labels[r0:r1, c0:c1]
            mine = window == lab
            if not mine.any():
                continue  # ya absorbido en esta pasada
            ring = ndimage.binary_dilation(mine, structure=cross) & ~mine
            around = np.unique(window[ring])
            around = around[around > 0]
            if around.size == 0:
                continue  # edificio pequeño y aislado: se queda
            # Torres, espadañas y agujas: pequeñas pero claramente más altas que todo lo que tocan.
            if sizes[lab - 1] >= MIN_TOWER_CELLS and medians[lab - 1] - medians[around - 1].max() >= TOWER_RISE:
                continue
            target = around[np.argmin(np.abs(medians[around - 1] - medians[lab - 1]))]
            window[mine] = target
            merged += 1
        if merged == 0:
            break

    _, dense = np.unique(labels[built], return_inverse=True)
    labels[built] = dense + 1
    return labels, int(labels.max())


def main():
    os.makedirs(DERIVED, exist_ok=True)
    os.makedirs(DATA, exist_ok=True)
    with open(META, encoding="utf-8") as f:
        zone = json.load(f)
    origin_e, origin_n, origin_h = zone["originEasting"], zone["originNorthing"], zone["originHeight"]

    tif = os.path.join(DERIVED, "z01_tejados_z_1m.tif")
    rasterize(tif)
    ds = gdal.Open(tif)
    z = ds.GetRasterBand(1).ReadAsArray().astype(np.float64)
    if ds.GetGeoTransform()[5] > 0:
        z = z[::-1, :]
    top_n = MIN_N + ROWS * CELL

    # El dataset debe seguir vivo mientras se lee la banda: se guarda en una variable.
    warped = gdal.Warp("", MDT, format="MEM", dstSRS="EPSG:25829", xRes=CELL, yRes=CELL, resampleAlg="bilinear",
                       outputBounds=(MIN_E, MIN_N, MIN_E + COLS * CELL, top_n))
    terrain = warped.GetRasterBand(1).ReadAsArray().astype(np.float64)

    built = (z > NODATA + 1) & (z - terrain >= MIN_HEIGHT)
    raw_cells = int(built.sum())

    # Rellena huecos de una celda rodeados de tejado (retornos perdidos), con la media de sus vecinos.
    cross = np.array([[0, 1, 0], [1, 0, 1], [0, 1, 0]], dtype=float)
    for _ in range(HOLE_PASSES):
        neighbours = ndimage.convolve(built.astype(float), cross, mode="constant")
        sums = ndimage.convolve(np.where(built, z, 0.0), cross, mode="constant")
        fill = (~built) & (neighbours >= 3)
        z[fill] = sums[fill] / neighbours[fill]
        built |= fill

    # Quita motas sueltas.
    labels, count = ndimage.label(built, structure=np.ones((3, 3)))
    sizes = ndimage.sum(built, labels, index=np.arange(1, count + 1))
    keep = np.isin(labels, np.nonzero(sizes >= MIN_BLOCK_CELLS)[0] + 1)
    removed = int(built.sum() - keep.sum())
    built = keep

    # --- Calles transitables ---
    def shifted(mask, dr, dc):
        """Copia de la máscara desplazada dr filas (sur) y dc columnas (este); fuera queda False."""
        out = np.zeros_like(mask)
        src = mask[max(0, -dr):mask.shape[0] - max(0, dr), max(0, -dc):mask.shape[1] - max(0, dc)]
        out[max(0, dr):max(0, dr) + src.shape[0], max(0, dc):max(0, dc) + src.shape[1]] = src
        return out

    def reachable_share(mask):
        """Porcentaje del suelo libre al que se llega andando desde la Plaza Mayor (4 vecinos)."""
        free = ~mask
        regions, _ = ndimage.label(free)  # conectividad en cruz por defecto
        r, c = int((top_n - SEED_N) / CELL), int((SEED_E - MIN_E) / CELL)
        if regions[r, c] == 0:  # la semilla cayó en edificio: usa la celda libre más cercana
            rr, cc = np.nonzero(free)
            i = int(np.argmin((rr - r) ** 2 + (cc - c) ** 2))
            r, c = int(rr[i]), int(cc[i])
        return 100.0 * float((regions == regions[r, c]).sum()) / float(free.sum())

    reach_before = reachable_share(built)
    widened = 0
    if WIDEN_NARROW_STREETS:
        free = ~built
        north, south = shifted(built, 1, 0), shifted(built, -1, 0)   # hay edificio al norte / al sur
        west, east = shifted(built, 0, 1), shifted(built, 0, -1)
        corridor_ns = free & north & south   # pasillo este-oeste de una celda de ancho
        corridor_ew = free & west & east     # pasillo norte-sur de una celda de ancho
        remove = (shifted(corridor_ns, -1, 0) | shifted(corridor_ns, 1, 0) |
                  shifted(corridor_ew, 0, -1) | shifted(corridor_ew, 0, 1)) & built
        widened = int(remove.sum())
        built &= ~remove
    reach_after = reachable_share(built)

    # --- Edificios separados (D44) ---
    # Cada manzana se parte en edificios allí donde el tejado da un salto, y cada edificio recibe
    # una sola cota (la mediana de su tejado): salen volúmenes nítidos en vez de una masa continua.
    labels, buildings = split_buildings(z, built)
    if SPLIT_BUILDINGS:
        index = np.arange(1, buildings + 1)
        flat = ndimage.median(z, labels, index=index)
        z = np.where(built, flat[np.maximum(labels, 1) - 1], z)
        # Un edificio nunca queda por debajo de la altura mínima sobre el terreno más alto que pisa.
        floor = ndimage.maximum(terrain, labels, index=index) + MIN_HEIGHT
        z = np.where(built, np.maximum(z, floor[np.maximum(labels, 1) - 1]), z)

    # Cota de tejado en metros locales (Y del juego); NaN donde no hay edificio.
    local = np.where(built, z - origin_h, np.nan).astype("<f4")
    roofs_path = os.path.join(DATA, "z01_tejados_1m.bytes")
    local.tofile(roofs_path)

    heights = (z - terrain)[built]
    meta = {
        "zone": "Z01",
        "source": "LiDAR PNOA 2018, cuadro 726-4374, clase 6 (DATA-002); relieve MDT02 (DATA-001)",
        "crs": "EPSG:25829",
        "columns": COLS,
        "rows": ROWS,
        "cellSize": CELL,
        "rowOrder": "north-to-south",
        "format": "float32 little-endian; NaN = sin edificio; valor = cota del tejado en metros locales (Y)",
        "westEasting": MIN_E,
        "northNorthing": top_n,
        "localWestX": MIN_E - origin_e,
        "localNorthZ": top_n - origin_n,
        "minBuildingHeight": MIN_HEIGHT,
        "builtCells": int(built.sum()),
        "rasterRadius": RASTER_RADIUS,
        "buildings": buildings if SPLIT_BUILDINGS else 0,
        "roofStep": ROOF_STEP,
        "cellsRemovedToWidenStreets": widened,
        "walkableReachablePercent": round(reach_after, 1),
        "generated": datetime.now().strftime("%Y-%m-%d %H:%M"),
    }
    with open(os.path.join(DATA, "z01_tejados_1m.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(meta, f, indent=2, ensure_ascii=False)
        f.write("\n")

    print("Tejados: %d x %d celdas de %.0f m -> %s" % (COLS, ROWS, CELL, roofs_path))
    print("  celdas con edificio: %d (brutas %d, motas quitadas %d) = %.1f %% de la ventana"
          % (built.sum(), raw_cells, removed, 100.0 * built.sum() / built.size))
    print("  altura sobre el terreno: mediana %.1f m, p95 %.1f m, max %.1f m"
          % (np.median(heights), np.percentile(heights, 95), heights.max()))
    print("  bloques conectados: %d" % ndimage.label(built, structure=np.ones((3, 3)))[1])
    sizes = ndimage.sum(built, labels, index=np.arange(1, buildings + 1))
    print("  edificios separados: %d (superficie mediana %.0f m2, mayor %.0f m2)"
          % (buildings, np.median(sizes), sizes.max()))
    # Plano de comprobación: cada edificio con un tono distinto.
    shade = np.where(built, 60 + (labels * 73) % 180, 0).astype(np.uint8)
    drv = gdal.GetDriverByName("GTiff").Create(os.path.join(DERIVED, "z01_edificios.tif"), COLS, ROWS, 1, gdal.GDT_Byte)
    drv.GetRasterBand(1).WriteArray(shade)
    drv = None
    gdal.Translate(os.path.join(DERIVED, "z01_edificios.png"), os.path.join(DERIVED, "z01_edificios.tif"),
                   format="PNG", width=COLS * 2, height=ROWS * 2)
    print("  calles: %d celdas quitadas para ensanchar pasillos; suelo libre alcanzable desde la Plaza Mayor: %.1f %% -> %.1f %%"
          % (widened, reach_before, reach_after))
    print("  esquina NO local: x %.1f, z %.1f" % (meta["localWestX"], meta["localNorthZ"]))


if __name__ == "__main__":
    sys.exit(main())
