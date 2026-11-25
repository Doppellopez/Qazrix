"""Genera las 32 gárgolas cacereñas del catálogo (y su variante erosionada) con Blender.

Uso (desde la raíz del proyecto):
  blender -b --factory-startup -P Tools/Art/gargoyles/make_gargoyles.py -- --out <carpeta> [--preview <carpeta>]
          [--photos <carpeta de fotos de referencia>] [--ids 1,7,9]

Cada gárgola se modela por código (masas unidas con remallado por vóxeles, huecos restados, desgaste con
ruido) mirando las fotos de referencia; las fotos NO entran en las texturas: el granito, los líquenes y la
oclusión se pintan a partir de la posición y la normal de cada punto de la malla.

Ejes: el paramento es el plano Y=0 de Blender y la pieza sale hacia -Y (en Unity, hacia +Z). El origen
queda en la cara del muro, a la altura del arranque. Medidas en metros.
"""
import argparse
import csv
import math
import os
import random
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector, noise
from mathutils.bvhtree import BVHTree

VOXEL = 0.0085
LOD_TRIANGLES = (7000, 1400)
TEXTURE_SIZE = 512


def P(x, f, u):
    """Punto en coordenadas de la gárgola: x lateral, f hacia fuera del muro, u hacia arriba."""
    return Vector((x, -f, u))


# --------------------------------------------------------------------------------------------- geometría

class Sculpt:
    """Acumula volúmenes (que se unen) y huecos (que se restan)."""

    def __init__(self, seed):
        self.rng = random.Random(seed)
        self.solid = bmesh.new()
        self.cuts = []
        self.stack = [Matrix.Identity(4)]

    # Transformación local: pitch positivo levanta el morro; yaw positivo gira hacia +x.
    def at(self, x, f, u, pitch=0.0, yaw=0.0, roll=0.0, scale=1.0):
        sculpt = self

        class _Ctx:
            def __enter__(self):
                m = (Matrix.Translation(P(x, f, u))
                     @ Matrix.Rotation(math.radians(yaw), 4, 'Z')
                     @ Matrix.Rotation(math.radians(-pitch), 4, 'X')
                     @ Matrix.Rotation(math.radians(roll), 4, 'Y')
                     @ Matrix.Scale(scale, 4))
                sculpt.stack.append(sculpt.stack[-1] @ m)

            def __exit__(self, *exc):
                sculpt.stack.pop()

        return _Ctx()

    def _ico(self, bm, matrix, subdiv=3):
        bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0, matrix=self.stack[-1] @ matrix)

    def ell(self, c, r, pitch=0.0, yaw=0.0, roll=0.0, bm=None):
        """Elipsoide en c = (x, f, u) con radios r = (x, f, u)."""
        if isinstance(r, (int, float)):
            r = (r, r, r)
        m = (Matrix.Translation(P(*c))
             @ Matrix.Rotation(math.radians(yaw), 4, 'Z')
             @ Matrix.Rotation(math.radians(-pitch), 4, 'X')
             @ Matrix.Rotation(math.radians(roll), 4, 'Y')
             @ Matrix.Diagonal((r[0], r[1], r[2], 1.0)))
        self._ico(bm if bm is not None else self.solid, m)

    def cone(self, a, b, ra, rb, bm=None):
        """Tronco de cono con extremos redondeados."""
        bm = bm if bm is not None else self.solid
        pa, pb = P(*a), P(*b)
        axis = pb - pa
        if axis.length > 1e-5:
            m = Matrix.Translation((pa + pb) / 2) @ axis.to_track_quat('Z', 'Y').to_matrix().to_4x4()
            bmesh.ops.create_cone(bm, cap_ends=True, segments=20, radius1=ra, radius2=rb,
                                  depth=axis.length, matrix=self.stack[-1] @ m)
        self._ico(bm, Matrix.Translation(pa) @ Matrix.Scale(ra * 0.995, 4), 2)
        self._ico(bm, Matrix.Translation(pb) @ Matrix.Scale(rb * 0.995, 4), 2)

    def chain(self, points, radii):
        for i in range(len(points) - 1):
            self.cone(points[i], points[i + 1], radii[i], radii[i + 1])

    def blade(self, a, b, width, thick, side=(1, 0, 0), bm=None):
        """Elipsoide aplanado de a a b (plumas, orejas, mechones). «side» indica el eje del grosor."""
        pa, pb = P(*a), P(*b)
        axis = pb - pa
        z = axis.normalized()
        s = P(*side)
        x = (s - z * s.dot(z))
        if x.length < 1e-5:
            x = Vector((0, 0, 1)) - z * z.z
        x.normalize()
        y = z.cross(x)
        rot = Matrix((x, y, z)).transposed().to_4x4()
        m = Matrix.Translation((pa + pb) / 2) @ rot @ Matrix.Diagonal((thick, width, axis.length / 2, 1.0))
        self._ico(bm if bm is not None else self.solid, m)

    def box(self, c, size, pitch=0.0, yaw=0.0):
        m = (Matrix.Translation(P(*c))
             @ Matrix.Rotation(math.radians(yaw), 4, 'Z')
             @ Matrix.Rotation(math.radians(-pitch), 4, 'X')
             @ Matrix.Diagonal((size[0], size[1], size[2], 1.0)))
        bmesh.ops.create_cube(self.solid, size=1.0, matrix=self.stack[-1] @ m)

    # Huecos
    def cut_cone(self, a, b, ra, rb):
        bm = bmesh.new()
        pa, pb = P(*a), P(*b)
        axis = pb - pa
        m = Matrix.Translation((pa + pb) / 2) @ axis.to_track_quat('Z', 'Y').to_matrix().to_4x4()
        bmesh.ops.create_cone(bm, cap_ends=True, segments=24, radius1=ra, radius2=rb,
                              depth=axis.length, matrix=self.stack[-1] @ m)
        self.cuts.append(bm)

    def cut_ell(self, c, r, pitch=0.0, yaw=0.0, roll=0.0):
        bm = bmesh.new()
        self.ell(c, r, pitch, yaw, roll, bm=bm)
        self.cuts.append(bm)


# --------------------------------------------------------------------------------------------- piezas

def corbel(s, w=0.38, h=0.20, u=-0.06, out=0.12):
    """Sillar de arranque con moldura: es lo que se empotra en la cornisa."""
    s.box((0, (out - 0.16) / 2, u), (w, out + 0.16, h))
    s.box((0, (out + 0.03 - 0.16) / 2, u + h * 0.34), (w * 1.1, out + 0.19, h * 0.18))
    s.box((0, (out + 0.015 - 0.16) / 2, u - h * 0.36), (w * 0.9, out + 0.175, h * 0.12))


def head(s, kind, at, size, pitch=0.0, yaw=0.0, **o):
    with s.at(*at, pitch=pitch, yaw=yaw, scale=size):
        HEADS[kind](s, **o)


def eyes(s, x, f, u, r, brow=True):
    for k in (-1, 1):
        s.cut_ell((k * x, f + r * 0.35, u), (r * 1.45, r * 0.9, r * 1.05))
        s.ell((k * x, f - r * 0.1, u), r)
        if brow:
            s.cone((k * x * 0.25, f + r * 0.5, u + r * 1.25), (k * (x + r * 1.1), f - r * 0.2, u + r * 1.5), r * 0.75, r * 0.5)


def spout(s, f0, u, r, f1=2.8):
    """Boca abierta por donde desagua."""
    s.cut_cone((0, f0, u), (0, f1, u - 0.03), r * 0.8, r * 1.1)


def curls(s, centre, radii, n, r, f_max=0.2):
    """Rizos repartidos por la superficie de un elipsoide, solo por detrás de f_max."""
    golden = math.pi * (3 - math.sqrt(5))
    for i in range(n):
        z = 1 - 2 * (i + 0.5) / n
        q = math.sqrt(1 - z * z)
        a = golden * i
        x, f, u = math.cos(a) * q, math.sin(a) * q, z
        p = (centre[0] + x * radii[0], centre[1] + f * radii[1], centre[2] + u * radii[2])
        if p[1] > f_max:
            continue
        k = r * s.rng.uniform(0.8, 1.2)
        s.ell(p, k)
        s.cut_ell((p[0] + x * k * 0.95, p[1] + f * k * 0.95, p[2] + u * k * 0.95), k * 0.42)


def head_dog(s, ears='up', mouth=0.24, long=1.0, hole=True):
    s.ell((0, 0, 0), (0.9, 1.0, 0.85))
    s.cone((0, 0.45, -0.08), (0, 0.65 + 1.0 * long, -0.18), 0.62, 0.40)
    s.cone((0, 0.25, -0.55), (0, 0.45 + 0.9 * long, -0.78), 0.45, 0.25)
    s.ell((0, 0.75 + 1.0 * long, -0.02), (0.30, 0.20, 0.20))
    for k in (-1, 1):
        s.cut_ell((k * 0.15, 0.93 + 1.0 * long, -0.04), 0.085)
        s.ell((k * 0.42, 0.35, -0.45), (0.30, 0.45, 0.30))  # carrillos
    eyes(s, 0.5, 0.74, 0.22, 0.15)
    if ears == 'up':
        for k in (-1, 1):
            s.cone((k * 0.58, -0.1, 0.5), (k * 0.82, -0.35, 1.42), 0.33, 0.09)
            s.cut_ell((k * 0.72, 0.1, 0.98), (0.12, 0.14, 0.28))
    elif ears == 'floppy':
        for k in (-1, 1):
            s.blade((k * 0.7, -0.1, 0.55), (k * 1.1, -0.15, -0.55), 0.38, 0.17, (1, 0, 0.3))
    else:
        for k in (-1, 1):
            s.ell((k * 0.7, -0.2, 0.65), (0.25, 0.28, 0.28))
    if mouth:
        if hole:
            spout(s, 0.5, -0.46, mouth)
        else:
            s.cut_ell((0, 1.5, -0.48), (0.75, 1.1, 0.09), pitch=-6)


def locks(s, centre, radii, n, r):
    """Guedejas: mechones alargados que caen hacia atrás sobre la masa de la melena."""
    golden = math.pi * (3 - math.sqrt(5))
    for i in range(n):
        z = 1 - 2 * (i + 0.5) / n
        q = math.sqrt(1 - z * z)
        a = golden * i
        x, f, u = math.cos(a) * q, math.sin(a) * q, z
        p = (centre[0] + x * radii[0], centre[1] + f * radii[1], centre[2] + u * radii[2])
        if p[1] > -0.1:
            continue
        end = (p[0] * 1.04 + s.rng.uniform(-0.12, 0.12), p[1] - r * 2.2, p[2] - r * 1.8)
        s.blade(p, end, r * 0.85, r * 0.7, (x, f, u))


def head_lion(s, mouth=0.26, mane=1.0, jaw=1.0):
    s.ell((0, -0.05, 0.02), (0.95, 0.85, 0.92))
    s.cone((0, 0.35, -0.15), (0, 1.05, -0.25), 0.66, 0.52)
    s.ell((0, 0.85, -0.68 * jaw), (0.42, 0.40, 0.27))
    for k in (-1, 1):
        s.ell((k * 0.28, 1.17, -0.27), (0.33, 0.30, 0.27))
        s.cut_ell((k * 0.14, 1.52, 0.0), 0.075)
    s.ell((0, 1.30, 0.04), (0.29, 0.22, 0.19))
    s.cone((0, 0.9, 0.55), (0, 1.3, 0.1), 0.16, 0.17)
    eyes(s, 0.43, 0.76, 0.28, 0.15)
    for k in (-1, 1):
        s.ell((k * 0.8, -0.15, 0.78), (0.30, 0.20, 0.32))
        s.cut_ell((k * 0.82, 0.07, 0.80), (0.13, 0.12, 0.15))
    if mouth:
        spout(s, 0.45, -0.46, mouth)
    if mane:
        s.ell((0, -0.65, -0.2), (1.16, 0.92, 1.22))
        locks(s, (0, -0.58, -0.17), (1.14, 0.92, 1.22), int(46 + 50 * mane), 0.15)


def head_dragon(s, mouth=0.34):
    s.ell((0, 0, 0), (0.78, 0.95, 0.74))
    s.cone((0, 0.4, 0.02), (0, 2.0, 0.18), 0.58, 0.44)
    s.ell((0, 2.08, 0.18), (0.62, 0.17, 0.58))
    s.cone((0, 0.2, -0.55), (0, 1.5, -0.55), 0.40, 0.22)
    for i in range(4):  # barbillas
        s.cone((0, 0.35 + 0.35 * i, -0.72), (0, 0.2 + 0.35 * i, -1.12), 0.19, 0.08)
    eyes(s, 0.46, 0.68, 0.30, 0.17)
    for k in (-1, 1):
        s.chain([(k * 0.45, -0.2, 0.55), (k * 0.7, -0.8, 0.95), (k * 0.65, -1.35, 0.85)], [0.26, 0.17, 0.07])
        s.blade((k * 0.65, -0.15, 0.05), (k * 1.2, -0.9, 0.25), 0.28, 0.13)
        s.cut_ell((k * 0.22, 2.2, 0.55), 0.09)
        for i in range(3):
            s.ell((k * (0.55 - 0.03 * i), 0.75 + 0.38 * i, 0.12 + 0.03 * i), (0.12, 0.2, 0.12))
    if mouth:
        spout(s, 0.7, -0.02, mouth)


def head_bird(s, open_beak=False, hook=1.0):
    s.ell((0, 0, 0), (0.72, 0.85, 0.76))
    s.chain([(0, 0.55, 0.02), (0, 1.22, -0.05), (0, 1.45, -0.48 * hook)], [0.40, 0.26, 0.08])
    s.cone((0, 0.4, -0.38), (0, 1.02, -0.50 - (0.18 if open_beak else 0)), 0.25, 0.10)
    eyes(s, 0.45, 0.52, 0.18, 0.16)
    for k in (-1, 1):
        s.blade((k * 0.45, -0.1, -0.35), (k * 0.6, -0.8, -0.75), 0.22, 0.14)
    if open_beak:
        spout(s, 0.45, -0.28, 0.2)
    else:
        s.cut_ell((0, 1.0, -0.27), (0.55, 0.6, 0.055), pitch=-8)


def head_pig(s, mouth=0.0):
    s.ell((0, 0, 0), (0.95, 0.95, 0.88))
    s.cone((0, 0.5, -0.12), (0, 1.55, -0.36), 0.66, 0.44)
    s.ell((0, 1.60, -0.37), (0.54, 0.16, 0.48))
    for k in (-1, 1):
        s.cut_ell((k * 0.19, 1.76, -0.30), (0.11, 0.18, 0.15))
        s.blade((k * 0.55, -0.05, 0.5), (k * 0.95, 0.55, 0.95), 0.34, 0.13, (0.4, 0, 1))
        s.ell((k * 0.5, 0.25, -0.45), (0.38, 0.45, 0.35))
    eyes(s, 0.5, 0.72, 0.2, 0.13)
    if mouth:
        spout(s, 0.5, -0.65, mouth)
    else:
        s.cut_ell((0, 1.15, -0.72), (0.65, 0.8, 0.07), pitch=-8)


def head_beast(s, mouth=0.22):
    """Fiera de orejas redondas y morro corto, vista casi siempre de frente."""
    s.ell((0, 0, 0), (1.0, 0.85, 0.9))
    s.ell((0, 0.68, -0.28), (0.56, 0.42, 0.45))
    s.ell((0, 1.02, -0.08), (0.24, 0.17, 0.16))
    for k in (-1, 1):
        s.ell((k * 0.82, -0.1, 0.72), (0.38, 0.22, 0.38))
        s.cut_ell((k * 0.85, 0.15, 0.74), (0.17, 0.12, 0.17))
        s.ell((k * 0.55, 0.35, -0.2), (0.35, 0.35, 0.3))
    eyes(s, 0.42, 0.72, 0.22, 0.15)
    if mouth:
        spout(s, 0.35, -0.47, mouth)


def head_human(s, mouth=0.18, hair='curly', beard=False):
    s.ell((0, 0, 0.05), (0.78, 0.84, 0.98))
    s.ell((0, 0.22, -0.42), (0.58, 0.58, 0.45))
    s.cone((0, 0.80, 0.18), (0, 1.02, -0.18), 0.09, 0.16)
    for k in (-1, 1):
        s.cone((k * 0.08, 0.80, 0.32), (k * 0.55, 0.62, 0.30), 0.12, 0.10)
        s.cut_ell((k * 0.30, 0.90, 0.11), (0.19, 0.14, 0.12))
        s.ell((k * 0.30, 0.73, 0.10), (0.13, 0.11, 0.09))
        s.ell((k * 0.42, 0.55, -0.15), (0.24, 0.24, 0.22))
        s.ell((k * 0.80, -0.05, -0.02), (0.12, 0.18, 0.26))
    if mouth:
        s.ell((0, 0.80, -0.50), (mouth * 1.8, 0.2, mouth * 1.5))
        spout(s, 0.25, -0.50, mouth)
    else:
        s.cut_ell((0, 0.95, -0.45), (0.30, 0.18, 0.045))
        s.ell((0, 0.82, -0.58), (0.28, 0.16, 0.09))
    if hair == 'curly':
        curls(s, (0, -0.1, 0.22), (0.82, 0.86, 0.92), 80, 0.2, f_max=0.38)
    elif hair == 'long':
        s.ell((0, -0.25, 0.3), (0.95, 0.85, 0.9))
        for k in (-1, 1):
            for i in range(3):
                s.chain([(k * (0.45 + 0.18 * i), 0.35 - 0.3 * i, 0.82 - 0.1 * i),
                         (k * (0.9 + 0.05 * i), 0.15 - 0.3 * i, 0.0),
                         (k * (0.78 + 0.1 * i), 0.2 - 0.3 * i, -1.05)], [0.2, 0.23, 0.09])
    if beard:
        for i, x in enumerate((-0.48, -0.24, 0.0, 0.24, 0.48)):
            low = -2.5 + abs(x) * 1.6
            s.chain([(x, 0.68 - abs(x) * 0.35, -0.68), (x * 1.15 + 0.07, 0.72 - abs(x) * 0.4, -1.35),
                     (x * 0.7 - 0.05, 0.4, low)], [0.20, 0.22, 0.07])
        for k in (-1, 1):
            s.chain([(k * 0.1, 1.0, -0.31), (k * 0.55, 0.75, -0.55), (k * 0.62, 0.55, -1.05)], [0.13, 0.12, 0.05])


HEADS = {'dog': head_dog, 'lion': head_lion, 'dragon': head_dragon, 'bird': head_bird,
         'pig': head_pig, 'beast': head_beast, 'human': head_human}


def paw(s, x, f, u, r, toes=3, yaw=0.0):
    s.ell((x, f, u), (r, r * 1.15, r * 0.72))
    for i in range(toes):
        dx = (i - (toes - 1) / 2) * r * 0.68
        s.cone((x + dx, f + r * 0.4, u), (x + dx * 1.15, f + r * 1.35, u - r * 0.35), r * 0.36, r * 0.25)


def talons(s, x, f, u, r):
    s.ell((x, f, u), r)
    for dx in (-1, 0, 1):
        s.chain([(x, f, u), (x + dx * r * 1.3, f + r * 1.7, u + r * 0.1),
                 (x + dx * r * 1.6, f + r * 2.2, u - r * 1.1)], [r * 0.62, r * 0.45, r * 0.16])


def feathers(s, x, f, u, rows, cols, dx, du, length, width, k):
    """Hileras de plumas en relieve sobre un costado o un pecho."""
    for j in range(rows):
        for i in range(cols):
            fx = x + (i - (cols - 1) / 2 + (j % 2) * 0.5) * dx
            fu = u - j * du
            s.blade((fx, f - j * 0.012, fu), (fx + k * 0.012, f + 0.012 - j * 0.012, fu - length), width, 0.017, (0, 1, 0))


def wing(s, side, root, tip, width, quills=5, thick=0.045):
    """Ala pegada al costado: masa principal y plumas remeras escalonadas."""
    k = side
    s.blade((k * root[0], root[1], root[2]), (k * tip[0], tip[1], tip[2]), width, thick, (1, 0, 0))
    for i in range(quills):
        t = (i + 0.5) / quills
        a = [root[j] + (tip[j] - root[j]) * (0.25 + 0.55 * t) for j in range(3)]
        b = [tip[0] + 0.01, tip[1] + (t - 0.5) * width * 2.1, tip[2] + (t - 0.5) * 0.05 - 0.04 * math.sin(t * 3.14)]
        s.blade((k * (a[0] + thick * 0.8), a[1] + (t - 0.5) * width, a[2]), (k * (b[0] + thick * 0.5), b[1], b[2]),
                width * 0.26, thick * 0.45, (1, 0, 0))


# --------------------------------------------------------------------------------------------- posturas

def perched(s, kind, head_size=0.14, head_f=0.50, head_u=0.62, pitch=0.0, girth=1.0, legs=True,
            collar=False, ridges=0, breast=0, **o):
    """Animal sentado sobre la cornisa, con el cuerpo en alto hacia fuera."""
    corbel(s)
    g = girth
    neck = (0, head_f - 0.13, head_u - 0.14)
    s.chain([(0, 0.0, 0.06), (0, 0.17, 0.22), (0, neck[1] - 0.05, neck[2] - 0.03), neck,
             (0, head_f - 0.02, head_u - 0.02)],
            [0.17 * g, 0.165 * g, 0.125 * g, 0.105 * g, 0.095 * g])
    for k in (-1, 1):
        s.ell((k * 0.135 * g, 0.07, 0.11), (0.09, 0.17, 0.14))
        if legs:
            s.chain([(k * 0.10 * g, neck[1] - 0.07, neck[2] - 0.07), (k * 0.115 * g, neck[1] - 0.01, 0.17),
                     (k * 0.11 * g, neck[1] - 0.04, 0.0)], [0.07 * g, 0.055, 0.042])
            paw(s, k * 0.11 * g, neck[1] - 0.02, -0.015, 0.055)
    if legs:
        s.blade((0, 0.10, 0.07), (0, neck[1] - 0.06, 0.12), 0.09, 0.07, (1, 0, 0))
    if collar:
        for i in range(11):
            a = math.pi * (i / 10 - 0.5) * 1.7
            s.ell((math.sin(a) * 0.12 * g, neck[1] + math.cos(a) * 0.09, neck[2] - 0.05 - 0.035 * math.cos(a)), 0.028)
    for i in range(ridges):
        t = i / max(1, ridges - 1)
        s.cone((-0.10, 0.14 + 0.16 * t, 0.2 + 0.2 * t), (0.10, 0.17 + 0.16 * t, 0.17 + 0.2 * t), 0.028, 0.028)
    if breast:
        feathers(s, 0, neck[1] + 0.055, neck[2] - 0.02, breast, 4, 0.05, 0.06, 0.075, 0.026, 0)
    head(s, kind, (0, head_f, head_u), head_size, pitch=pitch, **o)


def projecting(s, kind, length=0.62, head_size=0.13, drop=0.0, pitch=-6.0, girth=1.0, legs=True,
               belly=0.0, mane_body=0, collar=False, ridge=False, **o):
    """Canecillo largo: el cuerpo sale casi horizontal del muro, típico de la concatedral."""
    g = girth
    corbel(s, w=0.34, h=0.26, u=0.02, out=0.08)
    end = (0, length - 0.1, 0.12 + drop * 0.6)
    s.chain([(0, 0.0, 0.08), (0, length * 0.4, 0.11 + drop * 0.2), end, (0, length + 0.04, 0.14 + drop)],
            [0.155 * g, 0.125 * g, 0.095 * g, 0.09 * g])
    s.blade((0, -0.02, -0.12), (0, length * 0.45, 0.04), 0.11, 0.085, (1, 0, 0))  # apeo bajo el cuerpo
    if belly:
        s.blade((0, length * 0.12, -0.02), (0, length * 0.92, 0.10 + drop * 0.8 - belly * 0.15), belly * 0.72, 0.075 * g, (1, 0, 0))
    if legs:
        for k in (-1, 1):
            s.chain([(k * 0.09 * g, length * 0.55, 0.1), (k * 0.135 * g, length * 0.28, 0.0),
                     (k * 0.115 * g, length * 0.50, -0.035)], [0.065, 0.05, 0.035])
            paw(s, k * 0.115 * g, length * 0.54, -0.04, 0.043)
            s.ell((k * 0.12 * g, 0.12, 0.09), (0.075, 0.14, 0.11))
    if mane_body:
        for i in range(mane_body):
            f = length * (0.38 + 0.55 * s.rng.random())
            a = s.rng.uniform(-2.0, 2.0)
            r = 0.125 * g - (f / length) * 0.03
            s.ell((math.sin(a) * r, f, 0.12 + drop * f / length + math.cos(a) * r), 0.032)
    if collar:
        for i in range(14):
            a = 2 * math.pi * i / 14
            s.ell((math.sin(a) * 0.11 * g, length * 0.74, 0.12 + drop * 0.5 + math.cos(a) * 0.11 * g), 0.028)
    if ridge:
        s.chain([(0, 0.02, 0.24), (0, length * 0.8, 0.21 + drop)], [0.04, 0.025])
    head(s, kind, (0, length + 0.1, 0.15 + drop), head_size, pitch=pitch, **o)


def raptor(s, head_at=(0, 0.40, 0.66), head_size=0.115, pitch=-10.0, yaw=0.0, spread=0.0, quills=6, **o):
    """Ave rapaz erguida con las alas a los costados."""
    corbel(s)
    s.ell((0, 0.19, 0.28), (0.14, 0.16, 0.25), pitch=-32)
    s.chain([(0, 0.20, 0.34), (0, head_at[1] - 0.09, head_at[2] - 0.12), head_at], [0.11, 0.075, 0.065])
    for k in (-1, 1):
        s.chain([(k * 0.08, 0.20, 0.16), (k * 0.09, 0.23, 0.01)], [0.065, 0.04])
        talons(s, k * 0.09, 0.23, -0.005, 0.034)
        wing(s, k, (0.12, 0.27, 0.50), (0.15 + spread, -0.02, -0.02), 0.12, quills)
    s.blade((0, 0.12, 0.2), (0, -0.02, -0.14), 0.12, 0.05, (0, 1, 0.4))
    feathers(s, 0, 0.34, 0.40, 4, 4, 0.052, 0.062, 0.08, 0.027, 0)
    head(s, 'bird', head_at, head_size, pitch=pitch, yaw=yaw, **o)


def crouched_legs(s, spread=0.17, knee_f=0.33, knee_u=0.24, foot_u=-0.04):
    for k in (-1, 1):
        s.chain([(k * 0.09, 0.07, 0.10), (k * spread, knee_f, knee_u)], [0.105, 0.075])
        s.chain([(k * spread, knee_f, knee_u), (k * spread * 0.7, knee_f - 0.08, foot_u)], [0.068, 0.042])
        paw(s, k * spread * 0.7, knee_f - 0.05, foot_u, 0.05, toes=4)


def torso(s, top=(0, 0.22, 0.52)):
    s.chain([(0, 0.04, 0.10), (0, 0.10, 0.33), top], [0.15, 0.14, 0.125])


def arm(s, k, elbow, hand, shoulder=(0.14, 0.22, 0.52)):
    s.chain([(k * shoulder[0], shoulder[1], shoulder[2]), elbow, hand], [0.075, 0.056, 0.036])
    s.ell(hand, (0.05, 0.05, 0.036))
    dx, df, du = [hand[i] - elbow[i] for i in range(3)]
    d = Vector((dx, df, du)).normalized() * 0.07
    for j in (-1, 0, 1):
        s.cone(hand, (hand[0] + d.x + j * 0.027, hand[1] + d.y, hand[2] + d.z), 0.018, 0.012)


# --------------------------------------------------------------------------------------------- catálogo

def g01(s):  # Ulloa, perro: cuello largo erguido con pliegues, boca abierta
    perched(s, 'dog', head_size=0.125, head_f=0.42, head_u=0.74, pitch=4, girth=0.95, ridges=4, ears='stub', mouth=0.3, long=0.75)


def g02(s):
    perched(s, 'dog', head_size=0.135, head_f=0.48, head_u=0.66, pitch=-4, ears='stub', mouth=0.2, long=0.9)


def g03(s):
    perched(s, 'beast', head_size=0.13, head_f=0.38, head_u=0.76, pitch=-8, girth=0.82, mouth=0.18)


def g04(s):
    projecting(s, 'lion', length=0.46, head_size=0.165, pitch=-14, girth=1.15, drop=0.08, mouth=0.18, mane=0.0)


def g05(s):
    perched(s, 'dog', head_size=0.15, head_f=0.44, head_u=0.68, pitch=2, girth=1.05, ears='up', mouth=0.4, long=0.55)


def g06(s):
    perched(s, 'dog', head_size=0.14, head_f=0.46, head_u=0.62, pitch=-8, girth=1.05, collar=True, ears='up', mouth=0.2, long=0.5)


def g07(s):  # San Mateo, dragón en diagonal con alas enroscadas
    corbel(s, w=0.34, h=0.3, u=-0.04, out=0.1)
    s.chain([(0, 0.02, 0.02), (0, 0.24, 0.17), (0, 0.46, 0.33), (0, 0.62, 0.48), (0, 0.72, 0.57)],
            [0.13, 0.145, 0.115, 0.09, 0.085])
    for k in (-1, 1):
        s.blade((k * 0.11, 0.50, 0.46), (k * 0.14, -0.04, 0.22), 0.085, 0.032)
        pts = []
        for i in range(15):  # voluta del ala
            a = i * 0.52
            r = 0.012 + 0.0052 * i
            pts.append((k * 0.14, 0.17 + math.cos(a) * r, 0.20 + math.sin(a) * r))
        s.chain(pts, [0.022] * 15)
        s.chain([(k * 0.1, 0.38, 0.24), (k * 0.14, 0.26, 0.04), (k * 0.11, 0.40, -0.01)], [0.06, 0.042, 0.03])
        talons(s, k * 0.11, 0.40, -0.015, 0.032)
        for i in range(6):
            s.ell((k * 0.115, 0.30 + 0.065 * i, 0.20 + 0.052 * i), (0.022, 0.034, 0.034))
    for i in range(8):
        s.cone((0, 0.08 + 0.08 * i, 0.15 + 0.062 * i), (0, 0.05 + 0.08 * i, 0.23 + 0.062 * i), 0.034, 0.012)
    head(s, 'dragon', (0, 0.76, 0.62), 0.105, pitch=28)


def g08(s):  # San Mateo, águila erguida con caño plano sobre la cabeza
    raptor(s, head_at=(0, 0.35, 0.68), head_size=0.11, pitch=-18, spread=0.07, quills=5, hook=0.8)
    s.box((0, 0.40, 0.79), (0.15, 0.24, 0.09), pitch=22)
    s.cut_cone((0, 0.38, 0.815), (0, 0.62, 0.90), 0.03, 0.036)


def g09(s):  # Casa del Sol, águila de alas alzadas con presa
    corbel(s, w=0.38, h=0.2, u=-0.08, out=0.1)
    s.ell((0, 0.27, 0.30), (0.13, 0.15, 0.21), pitch=-28)
    s.chain([(0, 0.31, 0.38), (0, 0.38, 0.57), (0, 0.49, 0.62), (0, 0.55, 0.52)], [0.09, 0.055, 0.052, 0.05])
    head(s, 'bird', (0, 0.57, 0.50), 0.082, pitch=-48)
    for k in (-1, 1):
        s.blade((k * 0.10, 0.20, 0.22), (k * 0.18, 0.40, 0.80), 0.13, 0.042, (1, 0, 0))
        for i in range(4):
            s.blade((k * (0.15 + 0.012 * i), 0.14 + 0.07 * i, 0.30), (k * (0.185 + 0.006 * i), 0.24 + 0.075 * i, 0.77 - 0.012 * i), 0.026, 0.026)
        s.chain([(k * 0.07, 0.25, 0.18), (k * 0.085, 0.28, 0.06)], [0.055, 0.032])
        talons(s, k * 0.085, 0.28, 0.055, 0.032)
    # presa: animal pequeño atravesado bajo las garras
    s.ell((0.0, 0.27, -0.01), (0.17, 0.085, 0.07), yaw=-12)
    s.ell((-0.19, 0.30, -0.035), (0.062, 0.058, 0.052))
    for x in (-0.09, 0.03, 0.11):
        s.chain([(x, 0.29, -0.03), (x - 0.02, 0.34, -0.13)], [0.026, 0.014])
    s.chain([(0.15, 0.25, 0.0), (0.24, 0.26, -0.05), (0.23, 0.28, -0.13)], [0.03, 0.019, 0.01])
    s.blade((0, 0.16, 0.14), (0, 0.02, -0.14), 0.1, 0.05, (0, 1, 0.4))
    feathers(s, 0, 0.40, 0.36, 3, 3, 0.055, 0.06, 0.075, 0.024, 0)


def g10(s):  # Casa del Mono, borracho con odre
    corbel(s, w=0.44, h=0.18, u=-0.1, out=0.2)
    torso(s)
    crouched_legs(s, spread=0.16)
    s.ell((-0.16, 0.30, 0.20), (0.13, 0.095, 0.24), roll=-14, pitch=10)       # odre
    s.chain([(-0.14, 0.34, 0.38), (-0.07, 0.40, 0.56), (-0.01, 0.43, 0.62)], [0.075, 0.035, 0.03])
    arm(s, -1, (-0.24, 0.28, 0.36), (-0.12, 0.40, 0.45))
    arm(s, 1, (0.23, 0.30, 0.40), (0.01, 0.42, 0.50))
    head(s, 'human', (0.015, 0.30, 0.68), 0.115, pitch=6, yaw=-14, hair='bald', mouth=0.26)


def g11(s):  # Casa del Mono, mujer con niño
    corbel(s, w=0.44, h=0.18, u=-0.1, out=0.2)
    torso(s, (0, 0.21, 0.54))
    crouched_legs(s, spread=0.17, knee_f=0.31, knee_u=0.22)
    s.chain([(0.03, 0.27, 0.22), (-0.02, 0.31, 0.37)], [0.07, 0.062])          # niño
    head(s, 'human', (-0.035, 0.34, 0.455), 0.062, pitch=14, yaw=12, hair='curly', mouth=0.0)
    s.chain([(0.02, 0.30, 0.25), (-0.08, 0.37, 0.21), (-0.08, 0.34, 0.12)], [0.036, 0.028, 0.02])
    arm(s, -1, (-0.22, 0.24, 0.30), (-0.07, 0.38, 0.32))
    arm(s, 1, (0.23, 0.26, 0.30), (0.08, 0.37, 0.23))
    head(s, 'human', (0.01, 0.28, 0.70), 0.12, pitch=-22, hair='long', mouth=0.0)
    curls(s, (0.0, 0.22, 0.76), (0.13, 0.13, 0.11), 40, 0.035, f_max=0.30)


def g12(s):  # Casa del Mono, melancólico: cabeza vencida sobre la mano
    corbel(s, w=0.46, h=0.2, u=-0.09, out=0.2)
    s.chain([(0, 0.03, 0.12), (0.02, 0.06, 0.38), (0.03, 0.25, 0.54)], [0.16, 0.165, 0.10])
    crouched_legs(s, spread=0.17, knee_f=0.33, knee_u=0.24)
    arm(s, -1, (-0.15, 0.35, 0.28), (-0.06, 0.45, 0.46), shoulder=(0.12, 0.24, 0.52))
    arm(s, 1, (0.20, 0.18, 0.28), (0.12, 0.33, 0.22))
    head(s, 'human', (-0.035, 0.42, 0.59), 0.115, pitch=-36, yaw=-10, hair='long', mouth=0.0)
    for k in (-1, 1):  # pliegues del manto
        for i in range(3):
            s.chain([(k * 0.14, 0.22 - 0.05 * i, 0.50 - 0.02 * i), (k * 0.175, 0.04, 0.34 - 0.07 * i), (k * 0.13, -0.02, 0.10)],
                    [0.022, 0.026, 0.018])


def g13(s):  # Golfines de Abajo, ser fantástico de pecho redondo y caño en la boca
    corbel(s, w=0.36, h=0.22, u=-0.08, out=0.1)
    s.ell((0, 0.19, 0.19), (0.15, 0.17, 0.18), pitch=-25)
    s.chain([(0, 0.20, 0.26), (0, 0.30, 0.38), (0, 0.34, 0.47)], [0.125, 0.095, 0.085])
    head(s, 'beast', (0, 0.39, 0.52), 0.125, pitch=4, mouth=0.0)
    s.cone((0, 0.46, 0.485), (0, 0.60, 0.465), 0.075, 0.066)
    s.cut_cone((0, 0.38, 0.49), (0, 0.70, 0.452), 0.032, 0.042)
    for k in (-1, 1):
        s.chain([(k * 0.11, 0.24, 0.22), (k * 0.15, 0.24, 0.03), (k * 0.11, 0.30, -0.06)], [0.06, 0.042, 0.03])
        paw(s, k * 0.11, 0.30, -0.06, 0.042)
        wing(s, k, (0.13, 0.24, 0.42), (0.20, -0.02, 0.28), 0.085, 4, 0.032)


def g14(s):
    projecting(s, 'lion', length=0.66, head_size=0.12, pitch=-10, girth=0.9, legs=False, mane_body=70, mouth=0.28, mane=0.7)


def g15(s):
    projecting(s, 'pig', length=0.56, head_size=0.145, pitch=-22, girth=1.1, mane_body=22)


def g16(s):
    projecting(s, 'dog', length=0.58, head_size=0.115, pitch=-34, girth=0.95, drop=-0.02, ears='stub', long=0.95, mouth=0.16)


def g17(s):
    projecting(s, 'dog', length=0.64, head_size=0.11, pitch=-8, girth=0.9, ridge=True, ears='stub', long=1.2, mouth=0.18, hole=False)


def g18(s):
    projecting(s, 'dog', length=0.62, head_size=0.115, pitch=14, girth=1.0, drop=0.05, legs=False, belly=0.11, ears='stub', long=1.05, mouth=0.3)


def g19(s):
    projecting(s, 'lion', length=0.66, head_size=0.125, pitch=-26, girth=0.85, belly=0.12, legs=False, ridge=True, mouth=0.2, mane=0.0, jaw=1.35)


def g20(s):
    projecting(s, 'bird', length=0.55, head_size=0.14, pitch=-20, girth=0.9, legs=False, belly=0.09, hook=1.15)


def g21(s):
    projecting(s, 'lion', length=0.52, head_size=0.14, pitch=-4, girth=1.0, drop=0.2, collar=True, mouth=0.32, mane=0.45)


def g22(s):  # Godoy, «cerdo»: en la foto, cabeza redonda de orejas redondas y caño
    perched(s, 'beast', head_size=0.14, head_f=0.34, head_u=0.70, girth=0.92, legs=False, mouth=0.2)
    for k in (-1, 1):
        s.blade((k * 0.15, 0.10, 0.52), (k * 0.14, 0.14, -0.02), 0.07, 0.035)


def g23(s):
    perched(s, 'lion', head_size=0.125, head_f=0.42, head_u=0.72, pitch=-6, girth=0.9, collar=True, mouth=0.24, mane=0.3)
    for i in range(7):  # roseta del pecho
        a = 2 * math.pi * i / 6
        r = 0.0 if i == 6 else 0.05
        s.ell((math.sin(a) * r, 0.40, 0.40 + math.cos(a) * r), 0.03)


def g24(s):
    perched(s, 'beast', head_size=0.105, head_f=0.38, head_u=0.76, pitch=6, girth=0.85, breast=4, mouth=0.34)
    for k in (-1, 1):
        wing(s, k, (0.10, 0.26, 0.50), (0.13, -0.02, 0.06), 0.09, 4, 0.032)


def g25(s):  # Godoy, querubín
    corbel(s)
    s.chain([(0, 0.0, 0.07), (0, 0.18, 0.24), (0, 0.27, 0.43)], [0.13, 0.12, 0.125])
    s.chain([(0, 0.28, 0.46), (0, 0.32, 0.60)], [0.06, 0.055])
    for k in (-1, 1):
        wing(s, k, (0.12, 0.23, 0.54), (0.21, 0.0, 0.20), 0.095, 5, 0.032)
        s.blade((k * 0.13, 0.23, 0.44), (k * 0.23, 0.19, 0.66), 0.055, 0.028, (0, 1, 0))
        arm(s, k, (k * 0.17, 0.27, 0.30), (k * 0.035, 0.40, 0.31), shoulder=(0.11, 0.27, 0.45))
    feathers(s, 0, 0.34, 0.27, 3, 4, 0.05, 0.06, 0.085, 0.025, 0)
    head(s, 'human', (0, 0.35, 0.69), 0.115, pitch=14, hair='curly', mouth=0.14)


def g26(s):  # Godoy, anciano barbado asomado a la cornisa
    corbel(s, w=0.44, h=0.34, u=0.08, out=0.12)
    s.chain([(0, 0.0, 0.16), (0, 0.19, 0.30), (0, 0.23, 0.50)], [0.17, 0.15, 0.11])
    s.blade((0, 0.34, 0.50), (0, 0.30, 0.14), 0.12, 0.07, (0, 1, 0.1))
    head(s, 'human', (0, 0.27, 0.58), 0.15, pitch=-4, hair='curly', beard=True, mouth=0.18)


def g27(s):  # Godoy, águila de alas desplegadas hacia abajo
    raptor(s, head_at=(0, 0.37, 0.70), head_size=0.10, pitch=-6, yaw=-24, spread=0.13, quills=8, hook=1.0)
    for i in range(7):
        a = (i - 3) * 0.27
        s.blade((math.sin(a) * 0.06, 0.17, 0.12), (math.sin(a) * 0.27, 0.13, -0.15 + 0.04 * abs(i - 3)), 0.028, 0.025, (0, 1, 0))


def g28(s):  # Palacio de la Isla, figura humana acuclillada (anatomía sin detalle genital)
    corbel(s, w=0.40, h=0.22, u=-0.08, out=0.16)
    torso(s, (0, 0.30, 0.50))
    for k in (-1, 1):
        s.ell((k * 0.062, 0.36, 0.43), (0.062, 0.058, 0.058))
    crouched_legs(s, spread=0.18, knee_f=0.28, knee_u=0.23)
    arm(s, 1, (0.25, 0.34, 0.63), (0.07, 0.40, 0.74), shoulder=(0.13, 0.30, 0.50))
    arm(s, -1, (-0.20, 0.36, 0.30), (-0.14, 0.33, 0.21), shoulder=(0.13, 0.30, 0.50))
    head(s, 'human', (-0.01, 0.43, 0.65), 0.11, pitch=-6, yaw=-10, hair='curly', mouth=0.32)


def g29(s):  # Palacio de la Isla, águila (muy erosionada en la foto) con caño en el pico
    raptor(s, head_at=(0, 0.38, 0.66), head_size=0.115, pitch=2, spread=0.03, quills=4, hook=0.6, open_beak=True)
    for k in (-1, 1):
        s.chain([(k * 0.11, 0.33, 0.72), (k * 0.15, 0.26, 0.55), (k * 0.15, 0.18, 0.34)], [0.035, 0.05, 0.03])


def g30(s):  # Palacio de la Isla, alma en pena
    corbel(s)
    s.chain([(0, 0.0, 0.08), (0, 0.18, 0.26), (0, 0.28, 0.48), (0, 0.32, 0.58)], [0.15, 0.12, 0.10, 0.065])
    for k in (-1, 1):
        arm(s, k, (k * 0.16, 0.33, 0.23), (k * 0.075, 0.35, 0.03), shoulder=(0.10, 0.27, 0.47))
        s.chain([(k * 0.10, 0.03, 0.12), (k * 0.15, 0.20, 0.05)], [0.09, 0.055])
    head(s, 'human', (0, 0.37, 0.66), 0.12, pitch=8, hair='long', mouth=0.34)


def g31(s):  # San Juan, «querubín» según el pie de foto; la foto muestra una cabeza rizada de aspecto leonino
    corbel(s)
    s.chain([(0, 0.0, 0.08), (0, 0.17, 0.25), (0, 0.30, 0.45)], [0.16, 0.14, 0.12])
    curls(s, (0, 0.20, 0.30), (0.15, 0.17, 0.19), 110, 0.033, f_max=0.40)
    head(s, 'lion', (0, 0.43, 0.58), 0.175, pitch=-16, mouth=0.0, mane=1.0)
    s.ell((0, 0.625, 0.465), 0.034)  # bola ante la boca
    for k in (-1, 1):
        s.chain([(k * 0.12, 0.25, 0.28), (k * 0.12, 0.33, 0.04)], [0.065, 0.043])
        paw(s, k * 0.12, 0.34, 0.0, 0.05)


def g32(s):
    perched(s, 'lion', head_size=0.17, head_f=0.55, head_u=0.64, pitch=-10, girth=1.05, mouth=0.24, mane=0.9)
    curls(s, (0, 0.24, 0.30), (0.15, 0.19, 0.18), 120, 0.032, f_max=0.43)


BUILDERS = {i: globals()['g%02d' % i] for i in range(1, 33)}

# Piedra y colonización de cada edificio, a ojo sobre las fotos: color base y cuánto liquen o musgo.
STYLES = {
    'Ulloa':              dict(base=(0.60, 0.57, 0.50), ochre=0.05, yellow=0.30, moss=0.05, dark=0.20),
    'San_Mateo':          dict(base=(0.50, 0.43, 0.33), ochre=0.35, yellow=0.40, moss=0.08, dark=0.35),
    'Casa_del_Sol':       dict(base=(0.64, 0.61, 0.54), ochre=0.00, yellow=0.00, moss=0.35, dark=0.25),
    'Casa_del_Mono':      dict(base=(0.54, 0.50, 0.41), ochre=0.08, yellow=0.08, moss=0.45, dark=0.40),
    'Golfines_de_Abajo':  dict(base=(0.63, 0.60, 0.52), ochre=0.20, yellow=0.22, moss=0.03, dark=0.20),
    'Concatedral':        dict(base=(0.70, 0.57, 0.40), ochre=0.10, yellow=0.12, moss=0.00, dark=0.08),
    'Godoy':              dict(base=(0.64, 0.50, 0.34), ochre=0.18, yellow=0.08, moss=0.00, dark=0.42),
    'Palacio_de_la_Isla': dict(base=(0.69, 0.55, 0.36), ochre=0.45, yellow=0.15, moss=0.02, dark=0.12),
    'San_Juan':           dict(base=(0.53, 0.50, 0.43), ochre=0.04, yellow=0.10, moss=0.15, dark=0.35),
}


# --------------------------------------------------------------------------------------------- mallas

def new_object(name, bm):
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def activate(obj):
    for o in bpy.context.selected_objects:
        o.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def remesh(obj, size):
    activate(obj)
    obj.data.remesh_voxel_size = size
    obj.data.remesh_voxel_adaptivity = 0.0
    bpy.ops.object.voxel_remesh()


def apply(obj, kind, **settings):
    activate(obj)
    mod = obj.modifiers.new(kind, kind)
    for key, value in settings.items():
        setattr(mod, key, value)
    bpy.ops.object.modifier_apply(modifier=mod.name)


def keep_main_part(obj):
    """Quita esquirlas sueltas que dejan los huecos."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    seen, parts = set(), []
    for v in bm.verts:
        if v in seen:
            continue
        part, todo = [], [v]
        seen.add(v)
        while todo:
            a = todo.pop()
            part.append(a)
            for e in a.link_edges:
                b = e.other_vert(a)
                if b not in seen:
                    seen.add(b)
                    todo.append(b)
        parts.append(part)
    parts.sort(key=len, reverse=True)
    loose = [v for part in parts[1:] for v in part]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bm.to_mesh(obj.data)
    bm.free()
    return len(parts) - 1


def carve(obj, cutters):
    for index, bm in enumerate(cutters):
        cutter = new_object('hueco', bm)
        before = obj.data.copy()
        for solver in ('MANIFOLD', 'EXACT', 'FLOAT'):
            apply(obj, 'BOOLEAN', operation='DIFFERENCE', object=cutter, solver=solver)
            if len(obj.data.vertices) > len(before.vertices) * 0.8:
                break
            obj.data = before.copy()  # el solver ha destruido la malla: se prueba el siguiente
        else:
            print('AVISO: hueco %d sin aplicar en %s' % (index, obj.name), flush=True)
        bpy.data.meshes.remove(before)
        bpy.data.objects.remove(cutter)


def weather(obj, rng, eroded):
    """Desgaste: grano de la piedra siempre; en la variante erosionada, formas romas, melladuras y picaduras."""
    if eroded:
        apply(obj, 'SMOOTH', factor=0.65, iterations=36)
        points = [v.co.copy() for v in obj.data.vertices if v.co.y < -0.2]
        chips = []
        for _ in range(6):
            p = rng.choice(points)
            bm = bmesh.new()
            r = rng.uniform(0.022, 0.045)
            # La esfera se descentra hacia fuera para morder solo el borde.
            centre = p + (p - Vector((0, -0.25, 0.3))).normalized() * r * 0.7
            bmesh.ops.create_icosphere(bm, subdivisions=2, radius=r, matrix=Matrix.Translation(centre))
            for v in bm.verts:
                v.co += v.co.__class__((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1))) * r * 0.18
            chips.append(bm)
        carve(obj, chips)
        remesh(obj, VOXEL)
        apply(obj, 'SMOOTH', factor=0.5, iterations=3)
    else:
        apply(obj, 'SMOOTH', factor=0.5, iterations=2)

    mesh = obj.data
    offset = Vector((rng.uniform(-50, 50), rng.uniform(-50, 50), rng.uniform(-50, 50)))
    normals = [v.normal.copy() for v in mesh.vertices]
    for v, n in zip(mesh.vertices, normals):
        p = v.co + offset
        d = noise.fractal(p * 14.0, 1.0, 2.0, 3) * (0.0070 if eroded else 0.0028)
        d += noise.noise(p * 60.0) * (0.0018 if eroded else 0.0014)
        if eroded:
            d -= max(0.0, noise.noise(p * 24.0) - 0.40) * 0.012
        v.co += n * d
    mesh.update()
    keep_main_part(obj)


def triangles(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def decimate(obj, target):
    count = triangles(obj)
    if count > target:
        apply(obj, 'DECIMATE', ratio=target / count, use_collapse_triangulate=True)


def unwrap(obj):
    activate(obj)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.004)
    bpy.ops.object.mode_set(mode='OBJECT')


# --------------------------------------------------------------------------------------------- textura

_TABLE = np.random.RandomState(1229).rand(1 << 16).astype(np.float32)


def vnoise(p, scale, seed):
    """Ruido de valor 3D vectorizado, 0..1."""
    q = p * scale + seed * 17.31
    i = np.floor(q).astype(np.int64)
    f = q - i
    f = f * f * (3 - 2 * f)
    out = np.zeros(len(p), np.float32)
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                h = ((i[:, 0] + dx) * 73856093 ^ (i[:, 1] + dy) * 19349663 ^ (i[:, 2] + dz) * 83492791) & 0xFFFF
                w = ((f[:, 0] if dx else 1 - f[:, 0]) * (f[:, 1] if dy else 1 - f[:, 1])
                     * (f[:, 2] if dz else 1 - f[:, 2]))
                out += _TABLE[h] * w
    return out


def fbm(p, scale, seed):
    return (vnoise(p, scale, seed) * 0.55 + vnoise(p, scale * 2.1, seed + 3) * 0.30
            + vnoise(p, scale * 4.3, seed + 7) * 0.15)


def smooth(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def mix(c, target, t):
    return c + (np.array(target, np.float32) - c) * t[:, None]


def vertex_occlusion(obj, rays=24, reach=0.16):
    mesh = obj.data
    tree = BVHTree.FromObject(obj, bpy.context.evaluated_depsgraph_get())
    golden = math.pi * (3 - math.sqrt(5))
    result = np.ones(len(mesh.vertices), np.float32)
    for index, v in enumerate(mesh.vertices):
        n = v.normal
        t = n.orthogonal().normalized()
        b = n.cross(t)
        hits = 0.0
        for k in range(rays):
            r = math.sqrt((k + 0.5) / rays)
            a = golden * k
            d = t * (r * math.cos(a)) + b * (r * math.sin(a)) + n * math.sqrt(max(0.0, 1 - r * r))
            hit = tree.ray_cast(v.co + n * 0.002, d, reach)
            if hit[0] is not None:
                hits += 1.0 - 0.5 * hit[3] / reach
        result[index] = 1.0 - hits / rays
    return result


def bake(obj, style, seed, eroded, size=TEXTURE_SIZE):
    mesh = obj.data
    mesh.calc_loop_triangles()
    ao = vertex_occlusion(obj)
    uv = mesh.uv_layers.active.data
    positions = np.array([v.co[:] for v in mesh.vertices], np.float32)
    normals = np.array([v.normal[:] for v in mesh.vertices], np.float32)
    buf = np.zeros((size, size, 7), np.float32)
    mask = np.zeros((size, size), bool)
    for tri in mesh.loop_triangles:
        idx = list(tri.vertices)
        t = np.array([uv[l].uv[:] for l in tri.loops], np.float64) * size - 0.5
        x0, y0 = np.maximum(np.floor(t.min(0) - 1).astype(int), 0)
        x1, y1 = np.minimum(np.ceil(t.max(0) + 1).astype(int), size - 1)
        if x1 < x0 or y1 < y0:
            continue
        gx, gy = np.meshgrid(np.arange(x0, x1 + 1), np.arange(y0, y1 + 1))
        d = (t[1, 1] - t[2, 1]) * (t[0, 0] - t[2, 0]) + (t[2, 0] - t[1, 0]) * (t[0, 1] - t[2, 1])
        if abs(d) < 1e-9:
            continue
        w0 = ((t[1, 1] - t[2, 1]) * (gx - t[2, 0]) + (t[2, 0] - t[1, 0]) * (gy - t[2, 1])) / d
        w1 = ((t[2, 1] - t[0, 1]) * (gx - t[2, 0]) + (t[0, 0] - t[2, 0]) * (gy - t[2, 1])) / d
        w2 = 1 - w0 - w1
        inside = (w0 >= -0.02) & (w1 >= -0.02) & (w2 >= -0.02)
        if not inside.any():
            continue
        w = np.stack([w0[inside], w1[inside], w2[inside]], 1)
        px, py = gx[inside], gy[inside]
        buf[py, px, 0:3] = w @ positions[idx]
        buf[py, px, 3:6] = w @ normals[idx]
        buf[py, px, 6] = w @ ao[idx]
        mask[py, px] = True

    p = buf[..., 0:3][mask]
    n = buf[..., 3:6][mask]
    n /= np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-5)
    occ = np.clip(buf[..., 6][mask], 0, 1)

    count = len(p)
    colour = np.tile(np.array(style['base'], np.float32), (count, 1))
    colour *= (0.82 + 0.36 * fbm(p, 6.0, seed))[:, None]                          # manchas grandes
    warm = smooth(0.45, 0.75, fbm(p, 9.0, seed + 11))
    colour = mix(colour, (style['base'][0] * 1.05, style['base'][1] * 0.86, style['base'][2] * 0.70), warm * 0.45)
    grain = vnoise(p, 190.0, seed + 5)                                            # grano del granito
    colour = mix(colour, (0.19, 0.17, 0.15), smooth(0.66, 0.80, grain) * 0.75)
    colour = mix(colour, (0.88, 0.84, 0.76), smooth(0.68, 0.84, vnoise(p, 150.0, seed + 9)) * 0.6)

    wear = 1.25 if eroded else 1.0
    up = n[:, 2]
    # Churretes oscuros: sobre todo en caras bajo vuelo y en vetas verticales.
    streak = vnoise(p * np.array((1.0, 1.0, 0.18), np.float32), 22.0, seed + 21)
    dark = smooth(0.50, 0.78, fbm(p, 11.0, seed + 13) * 0.6 + streak * 0.4) * style['dark'] * wear
    colour = mix(colour, (0.14, 0.12, 0.09), np.clip(dark * 1.3, 0, 0.85))

    def patches(amount, scale, offset, tint, lift):
        if amount <= 0:
            return colour
        field = fbm(p, scale, seed + offset) + np.clip(up, -0.4, 1.0) * lift + (vnoise(p, 85.0, seed + offset + 1) - 0.5) * 0.2
        edge = 0.86 - 0.34 * min(1.0, amount * wear)
        return mix(colour, tint, smooth(edge, edge + 0.09, field) * 0.88)

    colour = patches(style['ochre'], 8.0, 31, (0.74, 0.43, 0.10), 0.10)
    colour = patches(style['yellow'], 13.0, 41, (0.63, 0.59, 0.20), 0.18)
    colour = patches(style['moss'], 7.0, 51, (0.16, 0.21, 0.10), 0.24)
    colour *= (1.16 * (0.52 + 0.48 * np.power(occ, 1.15)))[:, None]
    colour = np.clip(colour, 0, 1)

    image = np.zeros((size, size, 4), np.float32)
    image[..., 3] = 1.0
    image[..., :3][mask] = colour
    filled = mask.copy()
    for _ in range(8):  # sangrado de las islas UV
        for shift, axis in ((1, 0), (-1, 0), (1, 1), (-1, 1)):
            take = ~filled & np.roll(filled, shift, axis)
            image[take] = np.roll(image, shift, axis)[take]
            filled |= take
    mean = np.array(style['base'], np.float32) * 0.7
    image[..., :3][~filled] = mean
    return image


def save_image(pixels, path, file_format='JPEG'):
    h, w = pixels.shape[:2]
    img = bpy.data.images.new(os.path.basename(path), w, h, alpha=True)
    img.pixels.foreach_set(np.ascontiguousarray(pixels, np.float32).ravel())
    img.filepath_raw = path
    img.file_format = file_format
    img.save(quality=92)
    return img


# --------------------------------------------------------------------------------------------- salida

def camel(text):
    return ''.join(w[:1].upper() + w[1:] for w in text.split('_'))


def asset_name(row):
    return 'Gargola_%02d_%s_%s' % (int(row['ID']), camel(row['Edificio']), camel(row['Motivo']))


def build(row, eroded):
    """Devuelve (lod0, lod1, imagen) de una gárgola."""
    ident = int(row['ID'])
    sculpt = Sculpt(1229 + ident)
    BUILDERS[ident](sculpt)
    name = asset_name(row) + ('_Erosionada' if eroded else '')
    obj = new_object(name + '_LOD0', sculpt.solid)
    sculpt.solid.free()
    remesh(obj, VOXEL)
    carve(obj, sculpt.cuts)
    remesh(obj, VOXEL)
    rng = random.Random(ident * 7 + (1 if eroded else 0))
    weather(obj, rng, eroded)

    # El sillar se corta a ras: nada queda a más de 16 cm dentro del muro.
    for v in obj.data.vertices:
        v.co.y = min(v.co.y, 0.16)

    if eroded:  # escala moderada: entre un 10 % y un 18 % menor o mayor
        factor = 0.84 + 0.06 * rng.random() if ident % 2 else 1.10 + 0.08 * rng.random()
        obj.data.transform(Matrix.Scale(factor, 4))

    decimate(obj, LOD_TRIANGLES[0])
    unwrap(obj)
    pixels = bake(obj, STYLES[row['Edificio']], ident, eroded)

    low = obj.copy()
    low.data = obj.data.copy()
    low.name = name + '_LOD1'
    low.data.name = low.name
    bpy.context.scene.collection.objects.link(low)
    decimate(low, LOD_TRIANGLES[1])
    obj.data.name = obj.name
    for o in (obj, low):
        for poly in o.data.polygons:
            poly.use_smooth = True
    return obj, low, pixels


def textured_material(name, image):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    tex = nodes.new('ShaderNodeTexImage')
    tex.image = image
    bsdf = nodes.get('Principled BSDF')
    bsdf.inputs['Roughness'].default_value = 0.95
    mat.node_tree.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    nodes.active = tex
    return mat


def export(path, objects):
    for o in bpy.context.selected_objects:
        o.select_set(False)
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'}, axis_forward='-Z', axis_up='Y',
                             apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True, mesh_smooth_type='FACE',
                             add_leaf_bones=False, bake_anim=False, path_mode='STRIP')


def setup_preview():
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'TEXTURE'
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = 'BOTH'
    scene.display.shading.show_shadows = True
    scene.display.shading.show_specular_highlight = False
    scene.display.shading.studio_light = 'paint.sl'
    scene.display.shadow_shift = 0.1
    scene.display.light_direction = (0.35, -0.2, 0.9)
    scene.render.resolution_x = scene.render.resolution_y = 420
    scene.render.film_transparent = False
    scene.world = bpy.data.worlds.new('cielo')
    scene.world.color = (0.36, 0.55, 0.80)
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.exposure = 0.25
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
    cam.data.type = 'ORTHO'
    scene.collection.objects.link(cam)
    scene.camera = cam
    return cam


def render(cam, obj, path, azimuth, elevation):
    corners = [Vector(c) for c in obj.bound_box]
    centre = sum(corners, Vector()) / 8
    a, e = math.radians(azimuth), math.radians(elevation)
    direction = Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))
    cam.location = centre + direction * 4
    cam.rotation_euler = (-direction).to_track_quat('-Z', 'Y').to_euler()
    bpy.context.view_layer.update()
    inverse = cam.rotation_euler.to_matrix().inverted()
    local = [inverse @ (c - centre) for c in corners]
    cam.data.ortho_scale = max(max(abs(c.x) for c in local), max(abs(c.y) for c in local)) * 2.08
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def load_pixels(path, w, h):
    img = bpy.data.images.load(path)
    sw, sh = img.size
    k = min(w / sw, h / sh)
    img.scale(max(1, int(sw * k)), max(1, int(sh * k)))
    sw, sh = img.size
    data = np.empty(sw * sh * 4, np.float32)
    img.pixels.foreach_get(data)
    bpy.data.images.remove(img)
    tile = np.zeros((h, w, 4), np.float32)
    tile[..., 3] = 1
    y, x = (h - sh) // 2, (w - sw) // 2
    tile[y:y + sh, x:x + sw] = data.reshape(sh, sw, 4)
    return tile


def sheet(rows, cell, path):
    """rows = lista de filas; cada fila, lista de rutas de imagen (None = hueco)."""
    cols = max(len(r) for r in rows)
    canvas = np.zeros((cell * len(rows), cell * cols, 4), np.float32)
    canvas[..., 3] = 1
    for j, row in enumerate(rows):
        for i, source in enumerate(row):
            if source and os.path.exists(source):
                y = cell * (len(rows) - 1 - j)
                canvas[y:y + cell, cell * i:cell * (i + 1)] = load_pixels(source, cell, cell)
    save_image(canvas, path)


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    here = os.path.dirname(os.path.abspath(__file__))
    parser = argparse.ArgumentParser()
    parser.add_argument('--out', required=True)
    parser.add_argument('--catalog', default=os.path.join(here, 'catalogo.csv'))
    parser.add_argument('--preview')
    parser.add_argument('--photos')
    parser.add_argument('--ids')
    parser.add_argument('--only-eroded', action='store_true')
    args = parser.parse_args(argv)

    with open(args.catalog, encoding='utf-8-sig', newline='') as handle:
        rows = list(csv.DictReader(handle))
    wanted = {int(i) for i in args.ids.split(',')} if args.ids else None
    models = os.path.join(os.path.abspath(args.out), 'Models')
    textures = os.path.join(os.path.abspath(args.out), 'Textures')
    os.makedirs(models, exist_ok=True)
    os.makedirs(textures, exist_ok=True)

    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
    cam = setup_preview() if args.preview else None
    if args.preview:
        os.makedirs(args.preview, exist_ok=True)

    report = []
    for row in rows:
        ident = int(row['ID'])
        if wanted and ident not in wanted:
            continue
        for eroded in ((True,) if args.only_eroded else (False, True)):
            obj, low, pixels = build(row, eroded)
            name = obj.name[:-len('_LOD0')]
            image = save_image(pixels, os.path.join(textures, name + '.jpg'))
            material = textured_material(name, image)
            for o in (obj, low):
                o.data.materials.clear()
                o.data.materials.append(material)
            export(os.path.join(models, name + '.fbx'), [obj, low])
            d = obj.dimensions
            report.append((name, triangles(obj), triangles(low), d.x, d.y, d.z))
            print('GARGOLA %s tris=%d/%d ancho=%.2f vuelo=%.2f alto=%.2f' % report[-1], flush=True)
            if cam:
                low.hide_render = True
                tag = '%02d%s' % (ident, 'e' if eroded else '')
                render(cam, obj, os.path.join(args.preview, tag + '_a.png'), 32, -16)
                if not eroded:
                    render(cam, obj, os.path.join(args.preview, tag + '_b.png'), 78, 4)
            for o in (obj, low):
                bpy.data.objects.remove(o)
            bpy.data.materials.remove(material)
            bpy.data.images.remove(image)

    if args.preview and args.photos:
        ids = sorted({int(r[0].split('_')[1]) for r in report})
        for page in range(0, len(ids), 4):
            lines = []
            for ident in ids[page:page + 4]:
                row = rows[ident - 1]
                photo = os.path.join(args.photos, row['Edificio'], '%02d_%s.jpg' % (ident, row['Motivo']))
                lines.append([photo] + [os.path.join(args.preview, '%02d%s.png' % (ident, t)) for t in ('_a', '_b', 'e_a')])
            sheet(lines, 420, os.path.join(args.preview, 'comparacion_%02d.jpg' % (page // 4 + 1)))
    print('HECHO %d modelos' % len(report))


main()
