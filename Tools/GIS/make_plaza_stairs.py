# -*- coding: utf-8 -*-
"""F2 — Escalinatas de la Plaza Mayor de Cáceres como peldaños.

El MDT02 (2 m) convierte las escalinatas de la plaza en rampas lisas. Este script localiza, dentro
de la ventana de la plaza, el suelo libre con pendiente fuerte y lo sustituye por peldaños: cada
celda de 0,2 m recibe la cota del relieve redondeada hacia arriba a múltiplos de la contrahuella.
Unity lo levanta con el mismo mallador que los edificios (CityBlocks con salto pequeño).

Es una interpretación: sitúa peldaños donde el relieve tiene pendiente, no donde un plano dice que
hay escalera, y no reproduce el número ni el trazado real de los peldaños.

Uso (después de make_city_blocks.py, con el Python de QGIS):
    "C:\\Program Files\\QGIS 3.44.15\\bin\\python-qgis-ltr.bat" Tools\\GIS\\make_plaza_stairs.py
"""
import csv
import json
import os
import sys
from datetime import datetime

import numpy as np
from scipy import ndimage

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DERIVED = os.path.join(ROOT, "Research", "Derived", "Z01")
DATA = os.path.join(ROOT, "Assets", "_Qazrix", "Data", "Zones", "Z01")

# Ventana en metros locales: Plaza Mayor y subida hacia el Arco de la Estrella.
WEST_X, EAST_X, SOUTH_Z, NORTH_Z = -30.0, 60.0, 40.0, 112.0
CELL = 0.2          # m: huella mínima de un peldaño
RISER = 0.2         # m: contrahuella (el jugador sube 0,35 m y el guardia 0,3 m)
MIN_SLOPE = 14.0    # grados: por debajo se deja el relieve tal cual
MARGIN = 0.05       # m por encima del relieve, para que el peldaño nunca quede enterrado
GROW = 1.0          # m que se ensancha la zona de peldaños alrededor de la pendiente
MIN_AREA = 12.0     # m²: manchas de pendiente menores no son una escalinata


def main():
    with open(os.path.join(DERIVED, "z01_meta.json"), encoding="utf-8") as f:
        zone = json.load(f)
    n, step = zone["samples"], zone["cellSize"]
    half = (n - 1) * step / 2.0
    terrain = np.fromfile(os.path.join(DERIVED, zone["terrainFile"]), dtype="<f4").reshape(n, n).astype(np.float64)
    terrain -= zone["originHeight"]  # metros locales; filas de norte a sur

    with open(os.path.join(DATA, "z01_tejados_1m.json"), encoding="utf-8") as f:
        rm = json.load(f)
    roofs = np.fromfile(os.path.join(DATA, "z01_tejados_1m.bytes"), dtype="<f4").reshape(rm["rows"], rm["columns"])

    cols, rows = int(round((EAST_X - WEST_X) / CELL)), int(round((NORTH_Z - SOUTH_Z) / CELL))
    x = WEST_X + (np.arange(cols) + 0.5) * CELL
    z = NORTH_Z - (np.arange(rows) + 0.5) * CELL
    X, Z = np.meshgrid(x, z)

    # Relieve y pendiente tal como los ve Unity: interpolación bilineal de la rejilla de 2 m.
    gz, gx = np.gradient(terrain, step)
    slope = np.degrees(np.arctan(np.hypot(gx, gz)))
    coords = [(half - Z) / step, (X + half) / step]
    height = ndimage.map_coordinates(terrain, coords, order=1)
    # Cota más alta del relieve dentro de la celda (sus cuatro esquinas), para no enterrar el peldaño.
    for dz in (-0.5, 0.5):
        for dx in (-0.5, 0.5):
            corner = ndimage.map_coordinates(terrain, [(half - (Z + dz * CELL)) / step, (X + dx * CELL + half) / step], order=1)
            height = np.maximum(height, corner)
    steep = ndimage.map_coordinates(slope, coords, order=1) >= MIN_SLOPE

    rc = np.floor((X - rm["localWestX"]) / rm["cellSize"]).astype(int)
    rr = np.floor((rm["localNorthZ"] - Z) / rm["cellSize"]).astype(int)
    inside = (rc >= 0) & (rc < rm["columns"]) & (rr >= 0) & (rr < rm["rows"])
    built = np.zeros_like(inside)
    built[inside] = ~np.isnan(roofs[rr[inside], rc[inside]])

    stairs = steep & ~built
    labels, count = ndimage.label(stairs)
    areas = ndimage.sum(stairs, labels, index=np.arange(1, count + 1)) * CELL * CELL
    stairs = np.isin(labels, np.nonzero(areas >= MIN_AREA)[0] + 1)
    stairs = ndimage.binary_dilation(stairs, iterations=int(round(GROW / CELL))) & ~built

    tread = np.ceil((height + MARGIN) / RISER) * RISER

    # Ningún peldaño puede subir más de una contrahuella respecto a su vecino: donde el relieve es
    # más empinado que eso, los peldaños de abajo se recrecen (y la escalinata se alarga si hace falta).
    level = np.where(stairs, tread, -np.inf)
    cross = ndimage.generate_binary_structure(2, 1)
    for _ in range(200):
        pushed = ndimage.grey_dilation(level, footprint=cross, mode="constant", cval=-np.inf) - RISER
        grown = np.where(built, -np.inf, np.where(stairs, np.maximum(level, pushed), np.where(pushed > tread + 1e-6, pushed, -np.inf)))
        if np.array_equal(grown, level):
            break
        level = grown
    stairs = np.isfinite(level)
    tread = np.where(stairs, np.round(level / RISER) * RISER, tread)
    out = np.where(stairs, tread, np.nan).astype("<f4")
    out.tofile(os.path.join(DATA, "z01_escalinata.bytes"))

    labels, count = ndimage.label(stairs)
    flights = []
    for lab in range(1, count + 1):
        mask = labels == lab
        h = tread[mask]
        lo, hi = np.argmin(np.where(mask, tread, np.inf)), np.argmax(np.where(mask, tread, -np.inf))
        flights.append({
            "areaM2": round(float(mask.sum() * CELL * CELL), 1),
            "riseM": round(float(h.max() - h.min()), 2),
            "bottom": [round(float(X.flat[lo]), 1), round(float(Z.flat[lo]), 1)],
            "top": [round(float(X.flat[hi]), 1), round(float(Z.flat[hi]), 1)],
        })
    flights.sort(key=lambda f: -f["areaM2"])

    meta = {
        "zone": "Z01",
        "source": "relieve MDT02 (DATA-001); zonas libres de edificio con pendiente >= %.0f grados" % MIN_SLOPE,
        "columns": cols,
        "rows": rows,
        "cellSize": CELL,
        "rowOrder": "north-to-south",
        "format": "float32 little-endian; NaN = sin peldaño; valor = cota del peldaño en metros locales (Y)",
        "localWestX": WEST_X,
        "localNorthZ": NORTH_Z,
        "riser": RISER,
        "flights": flights,
        "generated": datetime.now().strftime("%Y-%m-%d %H:%M"),
    }
    with open(os.path.join(DATA, "z01_escalinata.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(meta, f, indent=2, ensure_ascii=False)
        f.write("\n")

    print("Escalinatas: %d x %d celdas de %.1f m, %.0f m2 con peldaños en %d tramos" % (cols, rows, CELL, stairs.sum() * CELL * CELL, count))
    for fl in flights:
        print("  tramo de %.0f m2, desnivel %.2f m, de (%.1f, %.1f) a (%.1f, %.1f)"
              % (fl["areaM2"], fl["riseM"], fl["bottom"][0], fl["bottom"][1], fl["top"][0], fl["top"][1]))
    # Mayor salto entre celdas vecinas con peldaño: debe poder subirlo el jugador (0,35 m).
    dh = np.nanmax(np.abs(np.diff(out, axis=1))), np.nanmax(np.abs(np.diff(out, axis=0)))
    print("  mayor salto entre celdas vecinas: %.2f m" % max(dh))

    # Comprobación de los puntos de control con los tejados planos: tejado más alto dentro de su caja.
    with open(os.path.join(ROOT, "Research", "Survey", "GroundControlPoints.csv"), encoding="utf-8-sig") as f:
        for row in csv.DictReader(f):
            if row["role"] != "control":
                continue
            px, pz = float(row["local_x"]), float(row["local_z"])
            hx, hz = float(row["size_ew"]) / 2, float(row["size_ns"]) / 2
            c0, c1 = int(px - hx - rm["localWestX"]), int(px + hx - rm["localWestX"]) + 1
            r0, r1 = int(rm["localNorthZ"] - pz - hz), int(rm["localNorthZ"] - pz + hz) + 1
            top = np.nanmax(roofs[r0:r1, c0:c1]) - float(row["local_y"])
            print("  %s: tejado más alto en su caja %.1f m (medido %s m)" % (row["id"], top, row["height_m"]))


if __name__ == "__main__":
    sys.exit(main())
