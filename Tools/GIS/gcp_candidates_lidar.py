# -*- coding: utf-8 -*-
"""F0-B — Candidatos a punto de control sacados del propio LiDAR.

Lee la altura de edificios sobre el terreno (z01_lidar_edificios_hag_1m.tif, que genera
gcp_lidar_check.py), separa los cuerpos altos y propone como punto de control el centro de cada uno.
Escribe la lista y un plano numerado para que una persona que conozca Cáceres diga qué es cada cosa.

La posición de cada candidato es exacta respecto al LiDAR (celdas de 1 m); el NOMBRE no lo da el
LiDAR: hay que asignarlo a mano en Research/Survey/gcp_identificados.csv.

Uso: "C:\\Program Files\\QGIS 3.44.15\\bin\\python-qgis-ltr.bat" Tools\\GIS\\gcp_candidates_lidar.py
"""
import csv
import os
import sys

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
from osgeo import gdal
from scipy import ndimage

gdal.UseExceptions()

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
HAG = os.path.join(ROOT, "Research", "Derived", "Z01", "z01_lidar_edificios_hag_1m.tif")
WIKIDATA = os.path.join(ROOT, "Research", "Survey", "GroundControlPoints.csv")
OUT_CSV = os.path.join(ROOT, "Research", "Survey", "gcp_candidatos_lidar.csv")
OUT_PNG = os.path.join(ROOT, "Reports", "QA", "z01_candidatos_lidar.png")

TALL_MIN = 15.0   # m sobre el terreno para contar como cuerpo alto (hipótesis de trabajo)
MIN_AREA = 25     # m²: descarta picos sueltos y ruido


def main():
    ds = gdal.Open(HAG)
    hag = ds.GetRasterBand(1).ReadAsArray().astype(np.float64)
    gt = ds.GetGeoTransform()
    min_e, top_n, cell = gt[0], gt[3], gt[1]
    rows, cols = hag.shape

    labels, count = ndimage.label(hag >= TALL_MIN, structure=np.ones((3, 3)))
    candidates = []
    for i in range(1, count + 1):
        rr, cc = np.nonzero(labels == i)
        if rr.size < MIN_AREA:
            continue
        candidates.append({
            "easting": min_e + (cc.mean() + 0.5) * cell,
            "northing": top_n - (rr.mean() + 0.5) * cell,
            "area_m2": rr.size * cell * cell,
            "max_height": float(hag[rr, cc].max()),
            "mean_height": float(hag[rr, cc].mean()),
            "size_ew": (cc.max() - cc.min() + 1) * cell,
            "size_ns": (rr.max() - rr.min() + 1) * cell,
            "label": i,
        })

    # Numeración estable: de norte a sur y, a igual norte, de oeste a este.
    candidates.sort(key=lambda c: (-round(c["northing"] / 10.0), c["easting"]))
    for n, c in enumerate(candidates, 1):
        c["id"] = "L%02d" % n

    fields = ["id", "easting", "northing", "area_m2", "max_height", "mean_height", "size_ew", "size_ns"]
    with open(OUT_CSV, "w", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, fieldnames=fields, extrasaction="ignore")
        w.writeheader()
        for c in candidates:
            w.writerow({k: ("%.2f" % c[k] if isinstance(c[k], float) else c[k]) for k in fields})

    draw(hag, labels, candidates, min_e, top_n, cell, rows, cols)

    print("%d candidatos -> %s" % (len(candidates), OUT_CSV))
    for c in candidates:
        print("%s  E %.1f  N %.1f  %4.0f m2  max %.1f m  (%.0f x %.0f m)"
              % (c["id"], c["easting"], c["northing"], c["area_m2"], c["max_height"], c["size_ew"], c["size_ns"]))


def draw(hag, labels, candidates, min_e, top_n, cell, rows, cols):
    extent = (min_e, min_e + cols * cell, top_n - rows * cell, top_n)
    fig, ax = plt.subplots(figsize=(13, 13), dpi=120)

    shown = np.ma.masked_less(hag, 2.5)
    image = ax.imshow(shown, extent=extent, cmap="viridis", vmin=2.5, vmax=30, interpolation="nearest")
    ax.set_facecolor("#f2f0ea")

    keep = np.isin(labels, [c["label"] for c in candidates])
    ax.contour(np.flipud(keep.astype(float)), levels=[0.5], colors="red", linewidths=1.2,
               extent=extent)

    for c in candidates:
        ax.plot(c["easting"], c["northing"], "o", color="red", markersize=4)
        ax.annotate(c["id"], (c["easting"], c["northing"]), xytext=(5, 5), textcoords="offset points",
                    fontsize=13, fontweight="bold", color="white",
                    bbox=dict(boxstyle="round,pad=0.2", fc="#c00000", ec="none"))

    if os.path.exists(WIKIDATA):
        with open(WIKIDATA, encoding="utf-8-sig", newline="") as f:
            for p in csv.DictReader(f):
                e, n = float(p["easting"]), float(p["northing"])
                if p.get("role") != "descartado":
                    continue
                ax.plot(e, n, "x", color="black", markersize=12, markeredgewidth=3)
                ax.annotate("Descartado: " + p["name"], (e, n), xytext=(8, -14), textcoords="offset points",
                            fontsize=10, color="black",
                            bbox=dict(boxstyle="round,pad=0.2", fc="white", ec="black", alpha=0.85))

    ax.set_title("Z01 — cuerpos de 15 m o más sobre el terreno (LiDAR PNOA 2018, clase 6)\n"
                 "Norte arriba · cuadrícula cada 50 m · L## = candidato a punto de control (centro del cuerpo)",
                 fontsize=13)
    ax.set_xlabel("Este (m) — ETRS89 / UTM 29N")
    ax.set_ylabel("Norte (m)")
    ax.set_xticks(np.arange(extent[0], extent[1] + 1, 50))
    ax.set_yticks(np.arange(extent[2], extent[3] + 1, 50))
    ax.tick_params(axis="x", labelrotation=90)
    ax.ticklabel_format(useOffset=False, style="plain")
    ax.grid(color="gray", alpha=0.35, linewidth=0.6)
    fig.colorbar(image, ax=ax, shrink=0.7, label="Altura construida sobre el terreno (m)")
    fig.tight_layout()
    os.makedirs(os.path.dirname(OUT_PNG), exist_ok=True)
    fig.savefig(OUT_PNG)
    plt.close(fig)


if __name__ == "__main__":
    sys.exit(main())
