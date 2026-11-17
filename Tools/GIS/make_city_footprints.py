# -*- coding: utf-8 -*-
"""F2 — Huellas poligonales de los edificios del casco antiguo (lados rectos y diagonales).

Parte de la rejilla de tejados que escribe make_city_blocks.py (1 m, una cota por edificio), junta las celdas
vecinas de la misma cota en edificios, saca el contorno exacto (escalones de 1 m) y lo simplifica con
Douglas-Peucker. La simplificación se hace sobre toda la ciudad a la vez (cobertura): dos edificios que
comparten pared quedan con la misma pared, sin huecos ni solapes. Resultado: un JSON con el anillo exterior
y los patios de cada edificio, en metros locales, que Unity convierte en muros diagonales de verdad.

Es volumetría (spec §5.3, capa B): las medidas siguen siendo las del LiDAR, solo se quitan los dientes.

Uso (con el Python de QGIS 3.44; no descarga nada):
    "C:\\Program Files\\QGIS 3.44.15\\bin\\python-qgis-ltr.bat" Tools\\GIS\\make_city_footprints.py [tolerancia_m] [--preview imagen.png]
"""
import json
import math
import os
import sys
from datetime import datetime

import numpy as np
import shapely
from scipy import ndimage

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DATA = os.path.join(ROOT, "Assets", "_Qazrix", "Data", "Zones", "Z01")
ROOFS = os.path.join(DATA, "z01_tejados_1m.bytes")
ROOFS_META = os.path.join(DATA, "z01_tejados_1m.json")
OUT = os.path.join(DATA, "z01_edificios_poligonos.json")

TOLERANCE = 1.0          # m: lo que puede desviarse la pared simplificada del contorno del LiDAR
MIN_AREA = 8.0           # m²: un edificio menor es ruido tras simplificar
MIN_HOLE_AREA = 6.0      # m²: un patio menor se rellena
AXIS_SNAP_DEGREES = 7.0  # una pared a menos de estos grados de norte-sur o este-oeste se endereza (si es un vértice no compartido)


def load():
    with open(ROOFS_META, encoding="utf-8") as f:
        meta = json.load(f)
    cols, rows = meta["columns"], meta["rows"]
    roofs = np.fromfile(ROOFS, dtype="<f4").reshape(rows, cols)
    return meta, roofs


def building_polygons(meta, roofs):
    """Edificios = celdas vecinas con la misma cota de tejado. Devuelve [(cota, polígono)] con el contorno exacto."""
    west, north = meta["localWestX"], meta["localNorthZ"]
    built = ~np.isnan(roofs)
    key = np.round(np.where(built, roofs, 0.0), 2)
    out = []
    for k in np.unique(key[built]):
        mask = built & (key == k)
        labels, n = ndimage.label(mask)
        for lab in range(1, n + 1):
            rr, cc = np.nonzero(labels == lab)
            x0 = west + cc.astype(float)
            z1 = north - rr.astype(float)
            geom = shapely.union_all(shapely.box(x0, z1 - 1.0, x0 + 1.0, z1))
            for poly in (geom.geoms if geom.geom_type == "MultiPolygon" else [geom]):
                out.append((float(k), poly))
    return out


def clean(poly):
    """Quita patios diminutos y devuelve None si lo que queda es ruido."""
    if poly.is_empty or poly.area < MIN_AREA:
        return None
    holes = [r for r in poly.interiors if shapely.Polygon(r).area >= MIN_HOLE_AREA]
    poly = shapely.Polygon(poly.exterior, holes)
    if not poly.is_valid:
        poly = shapely.make_valid(poly)
        if poly.geom_type != "Polygon":
            parts = [g for g in getattr(poly, "geoms", []) if g.geom_type == "Polygon"]
            if not parts:
                return None
            poly = max(parts, key=lambda g: g.area)
    return shapely.geometry.polygon.orient(poly, 1.0)  # exterior en sentido antihorario, patios en horario


def deflection_degrees(a, b, c):
    """Cuánto se desvía la dirección en b (0 = recto)."""
    v1 = (b[0] - a[0], b[1] - a[1])
    v2 = (c[0] - b[0], c[1] - b[1])
    n1, n2 = math.hypot(*v1), math.hypot(*v2)
    if n1 < 1e-9 or n2 < 1e-9:
        return 0.0
    cos = max(-1.0, min(1.0, (v1[0] * v2[0] + v1[1] * v2[1]) / (n1 * n2)))
    return math.degrees(math.acos(cos))


def ring_stats(ring):
    pts = list(ring.coords)[:-1]
    n = len(pts)
    lengths = [math.hypot(pts[(i + 1) % n][0] - pts[i][0], pts[(i + 1) % n][1] - pts[i][1]) for i in range(n)]
    flat = sum(1 for i in range(n) if deflection_degrees(pts[i - 1], pts[i], pts[(i + 1) % n]) < 12.0)
    return n, lengths, flat


def simplify_city(buildings, tolerance):
    polys = [p for _, p in buildings]
    if not shapely.coverage_is_valid(polys).all():
        # Los vértices de dos edificios que comparten pared no siempre coinciden: se anodan juntos.
        lines = shapely.unary_union([shapely.boundary(p) for p in polys])
        faces = list(shapely.polygonize([lines]).geoms)
        tree = shapely.STRtree(polys)
        rebuilt = {}
        for face in faces:
            probe = face.representative_point()
            hit = tree.query(probe, predicate="intersects")
            if len(hit) == 0:
                continue  # patio o calle: no es edificio
            rebuilt.setdefault(int(hit[0]), []).append(face)
        polys = [shapely.union_all(rebuilt[i]) if i in rebuilt else polys[i] for i in range(len(polys))]
        polys = [g if g.geom_type == "Polygon" else max(g.geoms, key=lambda x: x.area) for g in polys]
    simplified = shapely.coverage_simplify(polys, tolerance)
    return list(simplified)


def to_ring(coords):
    pts = list(coords)[:-1]
    flat = []
    for x, z in pts:
        flat.extend([round(x, 2), round(z, 2)])
    return flat


def main():
    tolerance = TOLERANCE
    preview = None
    args = sys.argv[1:]
    if args and not args[0].startswith("--"):
        tolerance = float(args.pop(0))
    if "--preview" in args:
        preview = args[args.index("--preview") + 1]

    meta, roofs = load()
    buildings = building_polygons(meta, roofs)
    heights = [h for h, _ in buildings]
    simplified = simplify_city(buildings, tolerance)

    result = []
    raw_vertices = sum(len(p.exterior.coords) - 1 + sum(len(r.coords) - 1 for r in p.interiors) for _, p in buildings)
    for h, poly in zip(heights, simplified):
        poly = clean(poly)
        if poly is None:
            continue
        result.append((h, poly))

    vertices = flats = 0
    shortest = []
    for _, poly in result:
        for ring in [poly.exterior] + list(poly.interiors):
            n, lengths, flat = ring_stats(ring)
            vertices += n
            flats += flat
            shortest.extend(lengths)
    shortest = np.array(shortest)

    data = {
        "zone": "Z01",
        "source": "Contorno de los edificios de z01_tejados_1m.bytes simplificado (Douglas-Peucker en cobertura)",
        "tolerance": tolerance,
        "origin": "metros locales del juego (x este, z norte); cota = Y del tejado",
        "generated": datetime.now().strftime("%Y-%m-%d %H:%M"),
        "buildings": [
            {"roof": round(h, 3),
             "outer": to_ring(poly.exterior.coords),
             "holes": [to_ring(r.coords) for r in poly.interiors]}
            for h, poly in result
        ],
    }
    os.makedirs(DATA, exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, ensure_ascii=False, separators=(",", ":"))
        f.write("\n")

    area_before = sum(p.area for _, p in buildings)
    area_after = sum(p.area for _, p in result)
    print("Huellas: %d edificios -> %s" % (len(result), OUT))
    print("  vértices: %d -> %d (%.0f %% menos), tolerancia %.2f m" % (raw_vertices, vertices, 100.0 * (1 - vertices / raw_vertices), tolerance))
    print("  superficie: %.0f -> %.0f m2 (%.2f %%)" % (area_before, area_after, 100.0 * (area_after - area_before) / area_before))
    print("  lados: mediana %.1f m, menor %.1f m, %d de menos de 1,5 m; quiebros casi rectos (<12 grados): %d"
          % (np.median(shortest), shortest.min(), int((shortest < 1.5).sum()), flats))

    # Cobertura: no deben solaparse (los huecos entre edificios son calles).
    geoms = [p for _, p in result]
    overlaps = 0
    tree = shapely.STRtree(geoms)
    for i, g in enumerate(geoms):
        for j in tree.query(g, predicate="intersects"):
            if j > i and g.intersection(geoms[j]).area > 0.05:
                overlaps += 1
    print("  pares de edificios que se solapan más de 0,05 m2: %d" % overlaps)

    if preview:
        draw_preview(meta, buildings, result, tolerance, preview)
        print("  vista previa: %s" % preview)


def draw_preview(meta, buildings, result, tolerance, path):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from matplotlib.patches import PathPatch
    from matplotlib.path import Path

    VX0, VX1, VZ0, VZ1 = -5.0, 75.0, 45.0, 125.0

    def patch(poly, **kw):
        verts, codes = [], []
        for ring in [poly.exterior] + list(poly.interiors):
            pts = np.asarray(ring.coords)
            verts.extend(pts)
            codes.extend([Path.MOVETO] + [Path.LINETO] * (len(pts) - 2) + [Path.CLOSEPOLY])
        return PathPatch(Path(verts, codes), **kw)

    def visible(poly):
        x0, z0, x1, z1 = poly.bounds
        return not (x1 < VX0 or x0 > VX1 or z1 < VZ0 or z0 > VZ1)

    fig, axes = plt.subplots(1, 2, figsize=(17, 8.6), dpi=100)
    for ax, polys, title in ((axes[0], [p for _, p in buildings], "Ahora: rejilla de 1 m (escalones del LiDAR)"),
                             (axes[1], [p for _, p in result], "Polígonos con paredes compartidas (tol. %.1f m)" % tolerance)):
        ax.set_facecolor("#c9c5bb")
        diagonals = 0
        simple = polys is not None and ax is axes[1]
        for poly in polys:
            if not visible(poly):
                continue
            ax.add_patch(patch(poly, facecolor="#9a5b4b", edgecolor="#2b2b2b", linewidth=1.0, alpha=0.95))
            if simple:
                for ring in [poly.exterior] + list(poly.interiors):
                    pts = np.asarray(ring.coords)
                    ax.plot(pts[:, 0], pts[:, 1], "o", color="#f5f0e0", markersize=2.5)
                    for a, b in zip(pts[:-1], pts[1:]):
                        if abs(b[0] - a[0]) > 0.3 and abs(b[1] - a[1]) > 0.3:
                            diagonals += 1
                            ax.plot([a[0], b[0]], [a[1], b[1]], color="#ffd23f", linewidth=2.6)
        ax.set_xlim(VX0, VX1)
        ax.set_ylim(VZ0, VZ1)
        ax.set_aspect("equal")
        ax.set_title(title + (("  · amarillo = diagonales (%d)" % diagonals) if simple else ""), fontsize=10)
        ax.set_xlabel("x local (m)")
        ax.set_ylabel("z local (m)")
    fig.suptitle("Edificios de Z01 alrededor de la Plaza Mayor, vista cenital (vista previa 2D, sin Unity)", fontsize=12)
    fig.tight_layout()
    fig.savefig(path)


if __name__ == "__main__":
    sys.exit(main())
