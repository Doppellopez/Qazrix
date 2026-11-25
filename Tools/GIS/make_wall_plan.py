"""Plano del casco antiguo de Z01 con los edificios numerados (los mismos que levanta el juego) para señalar cuáles son muralla.

Lee Research/Survey/city_parts.json (lo escribe Qazrix.EditorTools.CityPlanExport.RunBatch) y dibuja:
  - Reports/QA/plano_muralla/plano_general.png : toda la zona, los candidatos a muralla numerados.
  - Reports/QA/plano_muralla/plano_{NO,NE,SO,SE}.png : cuatro cuadrantes ampliados con TODOS los edificios numerados.
  - Reports/QA/plano_muralla/candidatos.csv : id, centro, tamaños y por qué es candidato.
Candidato = volumen alargado y estrecho (lienzo) o torre pequeña y alta pegada a uno. Es solo una ayuda: decide quien conoce Cáceres.
"""
import csv, json, math, os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "Research", "Survey", "city_parts.json")
OUT = os.path.join(ROOT, "Reports", "QA", "plano_muralla")
os.makedirs(OUT, exist_ok=True)

parts = json.load(open(SRC, encoding="utf-8"))["parts"]
for p in parts:
    pts = np.array(p["loop"], dtype=float).reshape(-1, 2)
    p["pts"] = pts
    c = pts.mean(axis=0)
    u, s, vt = np.linalg.svd(pts - c, full_matrices=False)
    proj = (pts - c) @ vt.T
    p["length"] = float(proj[:, 0].max() - proj[:, 0].min())
    p["width"] = float(proj[:, 1].max() - proj[:, 1].min())
    p["axis"] = vt[0]

# Candidatos: lienzo = largo y estrecho; torre = pequeña y alta.
for p in parts:
    why = []
    if p["length"] >= 18 and p["width"] <= 9 and p["length"] / max(p["width"], 0.1) >= 3.0:
        why.append("lienzo (largo y estrecho)")
    if p["area"] <= 150 and p["roof"] - 0 >= 0 and p["length"] <= 16 and p["width"] >= 5:
        pass
    p["why"] = why
tall = np.percentile([p["roof"] for p in parts], 75)
for p in parts:
    if p["area"] <= 120 and p["roof"] >= tall and not p["why"]:
        p["why"].append("posible torre (pequeña y alta)")

xs = np.concatenate([p["pts"][:, 0] for p in parts]); zs = np.concatenate([p["pts"][:, 1] for p in parts])
x0, x1, z0, z1 = xs.min() - 10, xs.max() + 10, zs.min() - 10, zs.max() + 10

def font(size):
    for name in ("arialbd.ttf", "arial.ttf", "DejaVuSans-Bold.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()

def render(bounds, scale, label_all, path, title):
    bx0, bx1, bz0, bz1 = bounds
    w, h = int((bx1 - bx0) * scale), int((bz1 - bz0) * scale)
    img = Image.new("RGB", (w, h), (245, 242, 232))
    d = ImageDraw.Draw(img, "RGBA")
    px = lambda x, z: ((x - bx0) * scale, (bz1 - z) * scale)

    # cuadrícula cada 50 m, con la coordenada local en el borde
    gf = font(max(11, int(scale * 2.2)))
    gx = math.ceil(bx0 / 50) * 50
    while gx <= bx1:
        a = px(gx, bz0)[0]; d.line([(a, 0), (a, h)], fill=(200, 200, 200, 255), width=1); d.text((a + 3, 3), f"x={int(gx)}", fill=(120, 120, 120), font=gf); gx += 50
    gz = math.ceil(bz0 / 50) * 50
    while gz <= bz1:
        b = px(bx0, gz)[1]; d.line([(0, b), (w, b)], fill=(200, 200, 200, 255), width=1); d.text((3, b + 3), f"z={int(gz)}", fill=(120, 120, 120), font=gf); gz += 50

    lf = font(max(10, int(scale * 2.6)))
    for p in parts:
        pts = p["pts"]
        if pts[:, 0].max() < bx0 or pts[:, 0].min() > bx1 or pts[:, 1].max() < bz0 or pts[:, 1].min() > bz1:
            continue
        poly = [px(x, z) for x, z in pts]
        cand = bool(p["why"])
        fill = (232, 110, 40, 200) if cand else (150, 150, 160, 170)
        d.polygon(poly, fill=fill, outline=(60, 60, 70, 255))
        if label_all or cand:
            cx, cz = p["cx"], p["cz"]
            if not (bx0 <= cx <= bx1 and bz0 <= cz <= bz1):
                continue
            t = str(p["id"]); tx, tz = px(cx, cz)
            tw = d.textlength(t, font=lf)
            d.rectangle([tx - tw / 2 - 2, tz - lf.size / 2 - 1, tx + tw / 2 + 2, tz + lf.size / 2 + 1], fill=(255, 255, 255, 215))
            d.text((tx - tw / 2, tz - lf.size / 2 - 1), t, fill=(150, 20, 20) if cand else (20, 20, 20), font=lf)
    tf = font(int(max(16, scale * 5)))
    d.rectangle([0, 0, w, tf.size + 14], fill=(255, 255, 255, 235))
    d.text((8, 5), title, fill=(0, 0, 0), font=tf)
    # norte
    d.text((w - 70, h - 60), "N ↑", fill=(0, 0, 0), font=tf)
    img.save(path)
    print(path, img.size)

render((x0, x1, z0, z1), 3.0, False, os.path.join(OUT, "plano_general.png"),
       "Z01 · en naranja los candidatos a muralla (números = id) · cuadrícula 50 m")
mx, mz = (x0 + x1) / 2, (z0 + z1) / 2
quads = {"NO": (x0, mx + 10, mz - 10, z1), "NE": (mx - 10, x1, mz - 10, z1), "SO": (x0, mx + 10, z0, mz + 10), "SE": (mx - 10, x1, z0, mz + 10)}
for name, b in quads.items():
    render(b, 8.0, True, os.path.join(OUT, f"plano_{name}.png"), f"Cuadrante {name} · todos los edificios numerados · naranja = candidato")

with open(os.path.join(OUT, "candidatos.csv"), "w", newline="", encoding="utf-8") as f:
    wr = csv.writer(f); wr.writerow(["id", "cx", "cz", "largo_m", "ancho_m", "area_m2", "cota_tejado", "motivo"])
    for p in parts:
        if p["why"]:
            wr.writerow([p["id"], f'{p["cx"]:.1f}', f'{p["cz"]:.1f}', f'{p["length"]:.1f}', f'{p["width"]:.1f}', f'{p["area"]:.0f}', f'{p["roof"]:.1f}', "; ".join(p["why"])])
print("candidatos:", sum(1 for p in parts if p["why"]), "de", len(parts))
