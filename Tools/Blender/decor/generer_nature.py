"""
After City — décor nature (SM_Nature_*), généré dans Blender.

Arbres (pins, feuillus en 3 saisons, bouleaux, arbres morts), blocs de forêt dense,
buissons et buissons à baies, rochers et cailloux à ramasser, herbes, fougères, fleurs,
souches et troncs couchés. Style bas‑poly à facettes, lisible depuis la caméra haute,
cohérent avec les personnages (modeles3d/00_conventions.md).

Conventions : 1 unité = 1 m, 1 case = 2 m, survivant = 1,00 m. Origine au centre de
l'emprise, au niveau du sol. Export FBX Forward -Z / Up Y, LOD0/1/2.
Matériau MAT_Batiments sur T_Batiments_1024 (CONVENTIONS.md) : la palette commune en haut à
gauche (256 px), les couleurs de la nature sur ses lignes 10 à 15 (libres dans palette.md).
Couleurs de sommet : R = occlusion approchée, G = saleté, B = 0 (réservé aux fenêtres éclairées).

Utilisation :
  blender --background --python generer_nature.py -- --sortie CHEMIN [--sans-rendu]
  (ou python3 avec le module bpy)
"""

import json
import math
import os
import random
import sys

import bpy  # noqa: I001
import bmesh
from mathutils import Matrix, Vector

# ---------------------------------------------------------------------------
# Palette nature : nom -> (ligne, colonne, hex), atlas 256 px, cases de 16 px
# ---------------------------------------------------------------------------
PALETTE = {
    # Ligne 0 : feuillages d'été
    "feuille_fonce": (10, 0, "3E5A2E"), "feuille": (10, 1, "4F6E36"), "feuille_clair": (10, 2, "6B8442"),
    "feuille_jaune": (10, 3, "8A8E45"), "pin_fonce": (10, 4, "2F4A32"), "pin": (10, 5, "3D5B3B"),
    "pin_clair": (10, 6, "52704A"), "bouleau_feuille": (10, 7, "7A9150"),
    # Ligne 1 : automne
    "auto_rouge": (11, 0, "9A4A2C"), "auto_orange": (11, 1, "B4702F"), "auto_jaune": (11, 2, "B79A42"),
    "auto_brun": (11, 3, "7A5A34"), "feuille_morte": (11, 4, "6E5638"),
    # Ligne 2 : bois et écorces
    "ecorce": (12, 0, "5A4433"), "ecorce_fonce": (12, 1, "3F3026"), "ecorce_pin": (12, 2, "6A4A36"),
    "bouleau_ecorce": (12, 3, "D2CCBA"), "bouleau_tache": (12, 4, "2E2A26"), "bois_coupe": (12, 5, "C49A62"),
    "bois_coupe_cerne": (12, 6, "9C7448"), "bois_mort": (12, 7, "8A8274"), "mousse": (12, 8, "5E6E38"),
    # Ligne 3 : roches et sol
    "roche": (13, 0, "8E8B80"), "roche_fonce": (13, 1, "6B6961"), "roche_clair": (13, 2, "A9A598"),
    "roche_ocre": (13, 3, "9A8A6C"), "caillou": (13, 4, "7F7A70"), "terre": (13, 5, "6C5236"),
    "lichen": (13, 6, "A3A26A"),
    # Ligne 4 : herbes, fleurs, baies
    "herbe_fonce": (14, 0, "4A6232"), "herbe": (14, 1, "62793C"), "herbe_seche": (14, 2, "A39A5C"),
    "herbe_haute": (14, 3, "7E8A4A"), "fleur_blanche": (14, 4, "DCD8C8"), "fleur_jaune": (14, 5, "D8B640"),
    "fleur_violette": (14, 6, "7E5E8E"), "baie_rouge": (14, 7, "A8302A"), "baie_bleue": (14, 8, "3A4A7E"),
    "champignon": (14, 9, "B8A890"), "champignon_rouge": (14, 10, "A33A2A"),
    # Ligne 5 : neige
    "neige": (15, 0, "E8ECEE"), "neige_ombre": (15, 1, "C4CCD4"),
}
CASE = 16


def uv_case(nom):
    """UV au centre de la case, dans la palette en haut à gauche de T_Batiments_1024 (256 px sur 1024)."""
    ligne, col, _ = PALETTE[nom]
    return ((col + 0.5) / 64.0, 1.0 - (ligne + 0.5) / 64.0)


def _palette_commune():
    """Lignes 0 à 9 de la palette partagée (palette.md), lues dans generer_personnages.py si présent."""
    try:
        sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
                                        "modeles3d", "scripts"))
        import generer_personnages as gp
        return dict(gp.PALETTE)
    except Exception:  # noqa: BLE001
        return {}


def creer_palette(dossier=None):
    taille = 1024
    img = bpy.data.images.get("T_Batiments_1024") or bpy.data.images.new("T_Batiments_1024", taille, taille, alpha=False)
    px = [0.5, 0.5, 0.5, 1.0] * (taille * taille)
    cases = list(_palette_commune().values()) + list(PALETTE.values())
    for ligne, col, hx in cases:
        rgb = [int(hx[i:i + 2], 16) / 255 for i in (0, 2, 4)]
        for yy in range(CASE):
            y = taille - 1 - (ligne * CASE + yy)
            for xx in range(CASE):
                i = (y * taille + col * CASE + xx) * 4
                px[i:i + 3] = rgb
    img.pixels[:] = px
    if dossier:
        img.filepath_raw = os.path.join(dossier, "T_Batiments_1024.png")
        img.file_format = "PNG"
        img.save()
    return img


def creer_materiau(img):
    mat = bpy.data.materials.get("MAT_Batiments")
    if mat:
        return mat
    mat = bpy.data.materials.new("MAT_Batiments")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    tex.interpolation = "Closest"
    att = nt.nodes.new("ShaderNodeAttribute")
    att.attribute_name = "Col"
    sep = nt.nodes.new("ShaderNodeSeparateColor")
    nt.links.new(att.outputs["Color"], sep.inputs[0])
    ao = nt.nodes.new("ShaderNodeMath")
    ao.operation = "MULTIPLY_ADD"
    nt.links.new(sep.outputs[0], ao.inputs[0])
    ao.inputs[1].default_value, ao.inputs[2].default_value = .55, .45
    sc = nt.nodes.new("ShaderNodeVectorMath")
    sc.operation = "SCALE"
    nt.links.new(tex.outputs["Color"], sc.inputs[0])
    nt.links.new(ao.outputs[0], sc.inputs["Scale"])
    nt.links.new(sc.outputs[0], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.95
    return mat


def _rot(r):
    return (Matrix.Rotation(math.radians(r[2]), 4, "Z") @ Matrix.Rotation(math.radians(r[1]), 4, "Y") @
            Matrix.Rotation(math.radians(r[0]), 4, "X"))


# ---------------------------------------------------------------------------
# Constructeur bas‑poly
# ---------------------------------------------------------------------------
class Nature:
    def __init__(self, graine, hauteur=1.0):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.verify()
        self.col = self.bm.verts.layers.float_color.new("Col")
        self.rng = random.Random(graine)
        self.hauteur = max(hauteur, .2)
        self.vent = {}  # poids du vent par sommet (non exporté pour l'instant)

    def _finir(self, avant, couleur, mat, vent=None, jitter=0.0, couleurs=None):
        faces = [f for f in self.bm.faces if f not in avant]
        verts = list({v for f in faces for v in f.verts})
        bmesh.ops.transform(self.bm, matrix=mat, verts=verts)
        if jitter:
            for v in verts:
                v.co += Vector((self.rng.uniform(-jitter, jitter), self.rng.uniform(-jitter, jitter),
                                self.rng.uniform(-jitter, jitter) * .6))
        for f in faces:
            nom = self.rng.choice(couleurs) if couleurs else couleur
            u, v = uv_case(nom)
            for l in f.loops:
                l[self.uv].uv = (u, v)
        for vert in verts:
            h = max(0.0, min(1.0, vert.co.z / self.hauteur))
            w = h if vent is None else vent(vert.co)
            vert[self.col] = (0.5 + 0.5 * h, 0.15, 0.0, 1.0)  # B réservé aux fenêtres éclairées
            self.vent[vert] = max(0.0, min(1.0, w))
        return verts

    def cone(self, base, r1, r2, h, couleur, seg=6, rot=(0, 0, 0), vent=None, jitter=0.0, couleurs=None):
        avant = set(self.bm.faces)
        bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r1, radius2=r2, depth=h)
        m = Matrix.Translation(Vector(base)) @ _rot(rot) @ Matrix.Translation((0, 0, h / 2))
        return self._finir(avant, couleur, m, vent, jitter, couleurs)

    def boule(self, centre, rayon, couleur, echelle=(1, 1, 1), subdiv=1, vent=None, jitter=0.0, couleurs=None,
              rot=(0, 0, 0), aplatir_bas=False):
        avant = set(self.bm.faces)
        res = bmesh.ops.create_icosphere(self.bm, subdivisions=subdiv, radius=rayon)
        for v in res["verts"]:
            v.co = Vector((v.co.x * echelle[0], v.co.y * echelle[1], v.co.z * echelle[2]))
            if aplatir_bas and v.co.z < 0:
                v.co.z *= .25
        m = Matrix.Translation(Vector(centre)) @ _rot(rot)
        return self._finir(avant, couleur, m, vent, jitter, couleurs)

    def boite(self, centre, taille, couleur, rot=(0, 0, 0), vent=None):
        avant = set(self.bm.faces)
        res = bmesh.ops.create_cube(self.bm, size=1.0)
        for v in res["verts"]:
            v.co = Vector((v.co.x * taille[0], v.co.y * taille[1], v.co.z * taille[2]))
        m = Matrix.Translation(Vector(centre)) @ _rot(rot)
        return self._finir(avant, couleur, m, vent)

    def lame(self, base, largeur, h, couleur, inclinaison=(0, 0), rot_z=0.0):
        """Brin d'herbe : triangle épais (2 faces), pointe libre au vent."""
        avant = set(self.bm.faces)
        x, y, z = base
        a = math.radians(rot_z)
        dx, dy = math.cos(a) * largeur / 2, math.sin(a) * largeur / 2
        nx, ny = -math.sin(a) * .002, math.cos(a) * .002  # faces avant et arrière décalées de 4 mm
        p1 = self.bm.verts.new((x - dx + nx, y - dy + ny, z))
        p2 = self.bm.verts.new((x + dx + nx, y + dy + ny, z))
        p3 = self.bm.verts.new((x + inclinaison[0] + nx, y + inclinaison[1] + ny, z + h))
        q1 = self.bm.verts.new((x - dx - nx, y - dy - ny, z))
        q2 = self.bm.verts.new((x + dx - nx, y + dy - ny, z))
        q3 = self.bm.verts.new((x + inclinaison[0] - nx, y + inclinaison[1] - ny, z + h))
        self.bm.faces.new((p1, p2, p3))
        self.bm.faces.new((q2, q1, q3))
        return self._finir(avant, couleur, Matrix.Identity(4), vent=lambda co: (co.z - z) / max(h, .01))

    def vers_objet(self, nom, mat):
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=1e-5)
        me = bpy.data.meshes.new(nom)
        self.bm.to_mesh(me)
        self.bm.free()
        me.materials.append(mat)
        if me.color_attributes.get("Col"):
            me.color_attributes.active_color = me.color_attributes["Col"]
        for p in me.polygons:
            p.use_smooth = False
        return bpy.data.objects.new(nom, me)


# ---------------------------------------------------------------------------
# Modèles
# ---------------------------------------------------------------------------
def vent_feuillage(z0, z1):
    return lambda co: (co.z - z0) / max(z1 - z0, .01) * .8 + .2


def tronc(n, h, r, couleur="ecorce", seg=6, penche=(0, 0)):
    n.cone((0, 0, -.05), r, r * .6, h + .05, couleur, seg=seg, rot=(penche[0], penche[1], 0),
           vent=lambda co: max(0, co.z / (h * 3)))
    # racines
    for k in range(3):
        a = k * 120 + n.rng.uniform(-20, 20)
        n.cone((math.cos(math.radians(a)) * r * .7, math.sin(math.radians(a)) * r * .7, -.02), r * .45, r * .1,
               r * 1.4, couleur, seg=4, rot=(0, 55, a), vent=lambda co: 0)


def pin(graine, h=2.8, neige=False):
    n = Nature(graine, h)
    rt = h * .055
    tronc(n, h * .35, rt, "ecorce_pin")
    etages = 4
    for i in range(etages):
        t = i / etages
        z = h * (.22 + .19 * i)
        rayon = h * (.32 - .065 * i) * n.rng.uniform(.9, 1.1)
        hh = h * (.36 - .03 * i)
        cols = ["pin_fonce", "pin", "pin"] if i < 2 else ["pin", "pin_clair"]
        n.cone((0, 0, z), rayon, 0.0 if i == etages - 1 else rayon * .2, hh, cols[0], seg=7,
               vent=vent_feuillage(h * .2, h), jitter=.03 * h, couleurs=cols, rot=(0, 0, n.rng.uniform(0, 60)))
        if neige:
            n.cone((0, 0, z + hh * .35), rayon * .72, 0.0, hh * .55, "neige", seg=7, vent=vent_feuillage(h * .2, h),
                   couleurs=["neige", "neige_ombre"])
    return n


def feuillu(graine, h=2.6, saison="Ete", forme="rond"):
    n = Nature(graine, h)
    rt = h * .06
    tronc(n, h * .55, rt, "ecorce")
    # branches maîtresses
    for k in range(3):
        a = k * 120 + n.rng.uniform(0, 40)
        n.cone((0, 0, h * .38), rt * .5, rt * .2, h * .3, "ecorce", seg=4, rot=(0, 40, a),
               vent=lambda co: .3)
    if saison == "Hiver":
        for k in range(5):
            a = k * 72 + n.rng.uniform(-15, 15)
            n.cone((0, 0, h * .5), rt * .35, .005, h * .38, "ecorce_fonce", seg=4, rot=(0, 35 + n.rng.uniform(-10, 15), a),
                   vent=lambda co: .6)
        return n
    cols = {"Ete": ["feuille_fonce", "feuille", "feuille", "feuille_clair"],
            "Automne": ["auto_rouge", "auto_orange", "auto_jaune", "auto_orange", "auto_brun"]}[saison]
    if forme == "rond":
        amas = [(0, 0, h * .7, h * .33)] + [(math.cos(math.radians(a)) * h * .2, math.sin(math.radians(a)) * h * .2,
                                            h * .6 + n.rng.uniform(-.05, .1) * h, h * .22) for a in (0, 120, 240)]
    else:  # « chêne » large et bas
        amas = [(0, 0, h * .66, h * .3)] + [(math.cos(math.radians(a)) * h * .28, math.sin(math.radians(a)) * h * .28,
                                             h * .55, h * .2) for a in (30, 110, 200, 290)]
    for (x, y, z, r) in amas:
        n.boule((x, y, z), r, cols[0], echelle=(1, 1, .85), subdiv=1, vent=vent_feuillage(h * .4, h),
                jitter=r * .12, couleurs=cols, aplatir_bas=True)
    return n


def bouleau(graine, h=2.9):
    n = Nature(graine, h)
    n.cone((0, 0, -.03), h * .035, h * .022, h * .75, "bouleau_ecorce", seg=6, vent=lambda co: max(0, co.z / (h * 3)))
    for k in range(4):  # taches noires
        z = h * (.1 + .15 * k)
        a = k * 97
        n.boite((math.cos(math.radians(a)) * h * .03, math.sin(math.radians(a)) * h * .03, z), (.04, .04, .025),
                "bouleau_tache", rot=(0, 0, a))
    cols = ["bouleau_feuille", "feuille_clair", "feuille_jaune"]
    for (x, y, z, r) in ((0, 0, h * .78, h * .17), (h * .08, .02, h * .58, h * .15), (-h * .07, -.03, h * .66, h * .14)):
        n.boule((x, y, z), r, cols[0], echelle=(.85, .85, 1.25), subdiv=1, vent=vent_feuillage(h * .4, h),
                jitter=r * .12, couleurs=cols)
    return n


def arbre_mort(graine, h=2.4):
    n = Nature(graine, h)
    tronc(n, h * .7, h * .055, "bois_mort", penche=(n.rng.uniform(-6, 6), n.rng.uniform(-6, 6)))
    for k in range(4):
        a = k * 90 + n.rng.uniform(-30, 30)
        z = h * (.35 + .1 * k)
        n.cone((0, 0, z), h * .025, .004, h * (.35 - .04 * k), "bois_mort", seg=4,
               rot=(0, 45 + n.rng.uniform(-15, 20), a), vent=lambda co: .4)
    n.boule((h * .04, 0, h * .15), h * .05, "mousse", echelle=(1.4, 1, .6), subdiv=1, vent=lambda co: 0)
    return n


def buisson(graine, taille=.45, baies=None, vide=False):
    n = Nature(graine, taille)
    cols = ["feuille_fonce", "feuille", "herbe_fonce"]
    for k in range(4):
        a = k * 90 + n.rng.uniform(-30, 30)
        d = taille * .35 if k else 0
        r = taille * n.rng.uniform(.38, .5)
        n.boule((math.cos(math.radians(a)) * d, math.sin(math.radians(a)) * d, r * .7), r, cols[0],
                echelle=(1, 1, .8), subdiv=1, vent=lambda co: min(1, co.z / taille), jitter=r * .15, couleurs=cols,
                aplatir_bas=True)
    if baies and not vide:
        for k in range(14):
            a = n.rng.uniform(0, 360)
            el = n.rng.uniform(10, 70)
            r = taille * .5
            x = math.cos(math.radians(a)) * math.cos(math.radians(el)) * r
            y = math.sin(math.radians(a)) * math.cos(math.radians(el)) * r
            z = taille * .35 + math.sin(math.radians(el)) * r * .8
            n.boite((x, y, z), (.04, .04, .04), baies, rot=(0, 0, a), vent=lambda co: min(1, co.z / taille))
    return n


def rocher(graine, taille=1.0, couleurs=("roche", "roche_fonce", "roche_clair"), mousse=False, aplati=.65):
    n = Nature(graine, taille)
    n.boule((0, 0, taille * aplati * .35), taille * .5, couleurs[0], echelle=(1.1, .9, aplati), subdiv=2,
            vent=lambda co: 0, jitter=taille * .09, couleurs=list(couleurs), rot=(0, 0, n.rng.uniform(0, 360)))
    if n.rng.random() < .7:
        n.boule((taille * .35, -taille * .1, taille * .15), taille * .25, couleurs[1], echelle=(1, .9, .7), subdiv=1,
                vent=lambda co: 0, jitter=taille * .05, couleurs=list(couleurs))
    if mousse:
        n.boule((0, 0, taille * aplati * .62), taille * .32, "mousse", echelle=(1.1, .9, .25), subdiv=1,
                vent=lambda co: 0, jitter=taille * .03, couleurs=["mousse", "lichen"])
    return n


def tas_cailloux(graine, nb=7):
    n = Nature(graine, .2)
    for k in range(nb):
        a = n.rng.uniform(0, 360)
        d = n.rng.uniform(0, .22)
        r = n.rng.uniform(.05, .1)
        n.boule((math.cos(math.radians(a)) * d, math.sin(math.radians(a)) * d, r * .5), r, "caillou",
                echelle=(1.2, 1, .7), subdiv=0, vent=lambda co: 0, jitter=r * .1,
                couleurs=["caillou", "roche", "roche_clair", "roche_fonce"], rot=(0, 0, a))
    return n


def touffe_herbe(graine, h=.22, nb=9, couleurs=("herbe", "herbe_fonce", "herbe_haute"), rayon=.12):
    n = Nature(graine, h)
    for k in range(nb):
        a = n.rng.uniform(0, 360)
        d = n.rng.uniform(0, rayon)
        hh = h * n.rng.uniform(.6, 1.1)
        n.lame((math.cos(math.radians(a)) * d, math.sin(math.radians(a)) * d, 0), .05, hh, n.rng.choice(couleurs),
               inclinaison=(math.cos(math.radians(a)) * hh * .3, math.sin(math.radians(a)) * hh * .3),
               rot_z=n.rng.uniform(0, 180))
    return n


def fleurs(graine):
    n = touffe_herbe(graine, .16, 7)
    for k in range(5):
        a = n.rng.uniform(0, 360)
        d = n.rng.uniform(.02, .1)
        x, y = math.cos(math.radians(a)) * d, math.sin(math.radians(a)) * d
        n.cone((x, y, 0), .006, .006, .2, "herbe_fonce", seg=3, vent=lambda co: co.z / .25)
        n.boule((x, y, .21), .025, "fleur_blanche", subdiv=0, vent=lambda co: 1,
                couleurs=["fleur_blanche", "fleur_jaune", "fleur_violette"])
    return n


def fougere(graine, h=.3):
    n = Nature(graine, h)
    for k in range(6):
        a = k * 60 + n.rng.uniform(-15, 15)
        n.cone((0, 0, .01), .05, .005, h * n.rng.uniform(.8, 1.2), "feuille_fonce", seg=3, rot=(0, 55, a),
               vent=lambda co: min(1, (abs(co.x) + abs(co.y)) / .3), couleurs=["feuille_fonce", "pin_clair"])
    return n


def champignons(graine):
    n = Nature(graine, .1)
    for k in range(4):
        a = n.rng.uniform(0, 360)
        d = n.rng.uniform(0, .08)
        x, y = math.cos(math.radians(a)) * d, math.sin(math.radians(a)) * d
        hh = n.rng.uniform(.04, .08)
        n.cone((x, y, 0), .012, .01, hh, "champignon", seg=5, vent=lambda co: 0)
        n.cone((x, y, hh), .035, .0, .03, "champignon_rouge" if k == 0 else "champignon", seg=6, vent=lambda co: 0)
    return n


def souche(graine, r=.14, h=.18):
    n = Nature(graine, h)
    n.cone((0, 0, -.02), r * 1.15, r, h, "ecorce", seg=7, vent=lambda co: 0)
    n.cone((0, 0, h - .02), r * .92, r * .92, .025, "bois_coupe", seg=7, vent=lambda co: 0)
    n.cone((0, 0, h - .005), r * .45, r * .45, .015, "bois_coupe_cerne", seg=7, vent=lambda co: 0)
    for k in range(3):
        a = k * 120 + 30
        n.cone((math.cos(math.radians(a)) * r * .9, math.sin(math.radians(a)) * r * .9, -.02), r * .4, r * .1, r * 1.1,
               "ecorce", seg=4, rot=(0, 60, a), vent=lambda co: 0)
    if n.rng.random() < .6:
        n.boule((r * .6, 0, h * .4), .05, "champignon", echelle=(1, 1, .3), subdiv=0, vent=lambda co: 0)
    return n


def tronc_couche(graine, l=1.4, r=.12):
    n = Nature(graine, r * 2)
    n.cone((-l / 2, 0, r * .85), r, r * .85, l, "ecorce", seg=7, rot=(0, 90, 0), vent=lambda co: 0)
    n.cone((-l / 2 - .005, 0, r * .85), r * .92, r * .92, .02, "bois_coupe", seg=7, rot=(0, 90, 0), vent=lambda co: 0)
    n.cone((l * .1, 0, r * 1.4), r * .3, .01, r * 2.2, "ecorce_fonce", seg=4, rot=(0, 50, 20), vent=lambda co: .2)
    n.boule((l * .2, 0, r * 1.6), r * .6, "mousse", echelle=(2, 1, .4), subdiv=1, vent=lambda co: 0,
            couleurs=["mousse", "lichen"])
    return n


# Bloc de forêt dense : plusieurs arbres serrés + sous‑bois, sur une emprise de 2 x 2 cases (4 x 4 m)
def bloc_foret(graine, emprise=4.0, nb_arbres=9, part_pins=.6):
    rng = random.Random(graine)
    n = Nature(graine, 3.4)
    places = []
    essais = 0
    while len(places) < nb_arbres and essais < 400:
        essais += 1
        x, y = rng.uniform(-emprise / 2 + .35, emprise / 2 - .35), rng.uniform(-emprise / 2 + .35, emprise / 2 - .35)
        if all((x - a) ** 2 + (y - b) ** 2 > .75 ** 2 for a, b in places):
            places.append((x, y))
    for i, (x, y) in enumerate(places):
        h = rng.uniform(2.4, 3.4)
        sous = pin(graine * 31 + i, h) if rng.random() < part_pins else feuillu(graine * 31 + i, h * .9)
        _fusionner(n, sous, (x, y, 0), rng.uniform(0, 360))
    for k in range(10):  # sous‑bois
        x, y = rng.uniform(-emprise / 2, emprise / 2), rng.uniform(-emprise / 2, emprise / 2)
        f = rng.choice([lambda s: fougere(s), lambda s: buisson(s, .35), lambda s: touffe_herbe(s, .25, 6)])
        _fusionner(n, f(graine * 97 + k), (x, y, 0), rng.uniform(0, 360))
    _fusionner(n, tronc_couche(graine + 5, 1.2, .1), (rng.uniform(-1, 1), rng.uniform(-1, 1), 0), rng.uniform(0, 180))
    # tapis de feuilles mortes / ombre au sol
    n.boule((0, 0, .0), emprise * .5, "feuille_morte", echelle=(1, 1, .01), subdiv=1, vent=lambda co: 0,
            couleurs=["feuille_morte", "terre", "herbe_fonce"])
    return n


def _fusionner(dest, src, pos, angle):
    me = bpy.data.meshes.new("tmp")
    bmesh.ops.transform(src.bm, matrix=Matrix.Translation(Vector(pos)) @ Matrix.Rotation(math.radians(angle), 4, "Z"),
                        verts=src.bm.verts)
    src.bm.to_mesh(me)
    src.bm.free()
    dest.bm.from_mesh(me)
    bpy.data.meshes.remove(me)
    dest.uv = dest.bm.loops.layers.uv.verify()
    dest.col = dest.bm.verts.layers.float_color.get("Col")


# ---------------------------------------------------------------------------
# Catalogue : nom -> (fabrique, emprise en cases, ressource, catégorie)
# ---------------------------------------------------------------------------
CATALOGUE = {
    "Pin_A": (lambda: pin(11, 2.6), 1, "Wood", "Arbre"),
    "Pin_B": (lambda: pin(12, 3.1), 1, "Wood", "Arbre"),
    "Pin_C": (lambda: pin(13, 2.2), 1, "Wood", "Arbre"),
    "Pin_Neige_A": (lambda: pin(11, 2.6, neige=True), 1, "Wood", "Arbre_Hiver"),
    "Pin_Neige_B": (lambda: pin(12, 3.1, neige=True), 1, "Wood", "Arbre_Hiver"),
    "Feuillu_A_Ete": (lambda: feuillu(21, 2.5), 1, "Wood", "Arbre"),
    "Feuillu_B_Ete": (lambda: feuillu(22, 2.8, forme="large"), 1, "Wood", "Arbre"),
    "Feuillu_C_Ete": (lambda: feuillu(23, 2.2), 1, "Wood", "Arbre"),
    "Feuillu_A_Automne": (lambda: feuillu(21, 2.5, "Automne"), 1, "Wood", "Arbre_Automne"),
    "Feuillu_B_Automne": (lambda: feuillu(22, 2.8, "Automne", "large"), 1, "Wood", "Arbre_Automne"),
    "Feuillu_C_Automne": (lambda: feuillu(23, 2.2, "Automne"), 1, "Wood", "Arbre_Automne"),
    "Feuillu_A_Hiver": (lambda: feuillu(21, 2.5, "Hiver"), 1, "Wood", "Arbre_Hiver"),
    "Feuillu_B_Hiver": (lambda: feuillu(22, 2.8, "Hiver", "large"), 1, "Wood", "Arbre_Hiver"),
    "Bouleau_A": (lambda: bouleau(31, 2.9), 1, "Wood", "Arbre"),
    "Bouleau_B": (lambda: bouleau(32, 2.5), 1, "Wood", "Arbre"),
    "Arbre_Mort_A": (lambda: arbre_mort(41, 2.4), 1, "Wood", "Arbre"),
    "Arbre_Mort_B": (lambda: arbre_mort(42, 2.0), 1, "Wood", "Arbre"),
    "Foret_Dense_A": (lambda: bloc_foret(51, 4.0, 9, .6), 2, "Wood", "Foret"),
    "Foret_Dense_B": (lambda: bloc_foret(52, 4.0, 8, .3), 2, "Wood", "Foret"),
    "Foret_Dense_C": (lambda: bloc_foret(53, 4.0, 10, .9), 2, "Wood", "Foret"),
    "Buisson_A": (lambda: buisson(61, .45), 1, None, "Buisson"),
    "Buisson_B": (lambda: buisson(62, .6), 1, None, "Buisson"),
    "Buisson_Baies_Rouges": (lambda: buisson(63, .5, "baie_rouge"), 1, "Vegetables", "Cueillette"),
    "Buisson_Baies_Bleues": (lambda: buisson(64, .5, "baie_bleue"), 1, "Vegetables", "Cueillette"),
    "Buisson_Baies_Vide": (lambda: buisson(63, .5, "baie_rouge", vide=True), 1, None, "Cueillette"),
    "Rocher_A": (lambda: rocher(71, 1.3), 1, "Stone", "Rocher"),
    "Rocher_B": (lambda: rocher(72, 1.7, aplati=.5), 1, "Stone", "Rocher"),
    "Rocher_C": (lambda: rocher(73, .9, aplati=.8), 1, "Stone", "Rocher"),
    "Rocher_Mousse": (lambda: rocher(74, 1.4, mousse=True), 1, "Stone", "Rocher"),
    "Rocher_Ocre": (lambda: rocher(75, 1.2, ("roche_ocre", "roche", "lichen")), 1, "Stone", "Rocher"),
    "Cailloux_A": (lambda: tas_cailloux(81, 7), 1, "Stone", "Ramassage"),
    "Cailloux_B": (lambda: tas_cailloux(82, 11), 1, "Stone", "Ramassage"),
    "Herbe_A": (lambda: touffe_herbe(91), 0, None, "Herbe"),
    "Herbe_B": (lambda: touffe_herbe(92, .18, 7, ("herbe_seche", "herbe_haute", "herbe")), 0, None, "Herbe"),
    "Herbe_Haute": (lambda: touffe_herbe(93, .42, 12, ("herbe_haute", "herbe_seche", "herbe"), .16), 0, None, "Herbe"),
    "Fleurs": (lambda: fleurs(94), 0, None, "Herbe"),
    "Fougere": (lambda: fougere(95), 0, None, "Herbe"),
    "Champignons": (lambda: champignons(96), 0, "Vegetables", "Cueillette"),
    "Souche_A": (lambda: souche(101, .14, .18), 1, None, "Souche"),
    "Souche_B": (lambda: souche(102, .18, .12), 1, None, "Souche"),
    "Tronc_Couche": (lambda: tronc_couche(103), 1, "Wood", "Souche"),
}


def nb_triangles(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def faire_lods(ob, collection, cat):
    res = [ob]
    ratios = {"Herbe": (.6, .3), "Foret": (.4, .15)}.get(cat, (.5, .22))
    for i, ratio in zip((1, 2), ratios):
        lod = bpy.data.objects.new(ob.name.replace("_LOD0", "_LOD%d" % i), ob.data.copy())
        collection.objects.link(lod)
        if nb_triangles(ob) > 30:
            mod = lod.modifiers.new("Decimate", "DECIMATE")
            mod.ratio = ratio
            bpy.context.view_layer.objects.active = lod
            bpy.ops.object.modifier_apply(modifier=mod.name)
        res.append(lod)
    return res


def exporter(objets, chemin):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objets:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objets[0]
    loc = objets[0].location.copy()
    for o in objets:
        o.location = (0, 0, 0)
    bpy.ops.export_scene.fbx(
        filepath=chemin, use_selection=True, object_types={"MESH"}, axis_forward="-Z", axis_up="Y",
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=False,
        mesh_smooth_type="FACE", use_mesh_modifiers=False, bake_anim=False, colors_type="SRGB", path_mode="AUTO")
    for o in objets:
        o.location = loc


def principal(sortie=None, rendu=True):
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    img = creer_palette(sortie)
    mat = creer_materiau(img)
    col = bpy.data.collections.new("Nature")
    bpy.context.scene.collection.children.link(col)
    bilan, catalogue, objets = [], {}, {}
    for i, (nom, (fab, emprise, res, cat)) in enumerate(CATALOGUE.items()):
        n = fab()
        haut = max(v.co.z for v in n.bm.verts)
        ob = n.vers_objet("SM_Nature_%s_LOD0" % nom, mat)
        col.objects.link(ob)
        lods = faire_lods(ob, col, cat)
        tri = [nb_triangles(o) for o in lods]
        objets[nom] = lods
        catalogue[nom] = dict(fichier="SM_Nature_%s.fbx" % nom, categorie=cat, empriseCases=emprise,
                              ressource=res, hauteur_m=round(haut, 2), triangles=tri)
        bilan.append("%-22s %-14s haut %.2f m  triangles LOD0/1/2 = %s" % (nom, cat, haut, "/".join(map(str, tri))))
        if sortie:
            exporter(lods, os.path.join(sortie, "SM_Nature_%s.fbx" % nom))
    print("\n".join(bilan))
    if sortie:
        with open(os.path.join(sortie, "bilan_nature.txt"), "w", encoding="utf-8") as f:
            f.write("\n".join(bilan) + "\n")
        with open(os.path.join(sortie, "catalogue_nature.json"), "w", encoding="utf-8") as f:
            json.dump({"version": "0.1", "materiau": "MAT_Batiments / T_Batiments_1024.png (palette lignes 10 à 15 = nature)",
                       "couleursDeSommet": "R occlusion, G saleté, B 0",
                       "objets": catalogue}, f, ensure_ascii=False, indent=2)
    if sortie and rendu:
        rendus(sortie, objets)
    if sortie:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(sortie, "AfterCity_Nature.blend"))


def _placer(lods, pos, angle=0.0, echelle=1.0):
    for k, o in enumerate(lods):
        o.location = pos
        o.rotation_euler = (0, 0, math.radians(angle))
        o.scale = (echelle,) * 3
        o.hide_render = k != 0


def rendus(sortie, objets):
    sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
                                    "modeles3d", "scripts"))
    import generer_personnages as gp
    # 1. Planche : tous les modèles en rangées
    rangees = [["Pin_A", "Pin_B", "Pin_C", "Pin_Neige_A", "Pin_Neige_B", "Bouleau_A", "Bouleau_B", "Arbre_Mort_A", "Arbre_Mort_B"],
               ["Feuillu_A_Ete", "Feuillu_B_Ete", "Feuillu_C_Ete", "Feuillu_A_Automne", "Feuillu_B_Automne",
                "Feuillu_C_Automne", "Feuillu_A_Hiver", "Feuillu_B_Hiver"],
               ["Foret_Dense_A", "Foret_Dense_B", "Foret_Dense_C"],
               ["Rocher_A", "Rocher_B", "Rocher_C", "Rocher_Mousse", "Rocher_Ocre", "Souche_A", "Souche_B", "Tronc_Couche"],
               ["Buisson_A", "Buisson_B", "Buisson_Baies_Rouges", "Buisson_Baies_Bleues", "Buisson_Baies_Vide",
                "Cailloux_A", "Cailloux_B", "Herbe_A", "Herbe_B", "Herbe_Haute", "Fleurs", "Fougere", "Champignons"]]
    pas = [2.0, 2.2, 5.0, 1.9, .9]
    y = 0.0
    for ligne, p in zip(rangees, pas):
        largeur = (len(ligne) - 1) * p
        for k, nom in enumerate(ligne):
            _placer(objets[nom], (k * p - largeur / 2, y, 0))
        y += {2.0: 3.2, 2.2: 3.4, 5.0: 5.0, 1.9: 2.6, .9: 2.0}[p]
    sc = gp.scene_rendu(2200, 1500, 0, 21, cam_y=-14, cam_z=13, cible_z=0)
    cam = sc.camera
    cam.location = (0, y / 2 - 16, 15)
    d = Vector((0, y / 2 - 1.5, 0)) - cam.location
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    gp.rendre(os.path.join(sortie, "apercu_nature.png"), sc)
    # 2. Diorama « lisière de forêt » vu par la caméra du jeu (haute, 50°)
    rng = random.Random(7)
    for lods in objets.values():
        for o in lods:
            o.hide_render = True
    copies = []

    def poser(nom, pos, angle=None, echelle=1.0):
        src = objets[nom][0]
        o = src.copy()
        bpy.context.scene.collection.objects.link(o)
        o.location = pos
        o.rotation_euler = (0, 0, math.radians(rng.uniform(0, 360) if angle is None else angle))
        o.scale = (echelle,) * 3
        o.hide_render = False
        copies.append(o)

    ox = 60.0
    for gx in range(-3, 4):
        for gy in range(2, 5):
            poser(rng.choice(["Foret_Dense_A", "Foret_Dense_B", "Foret_Dense_C"]), (ox + gx * 4, gy * 4, 0))
    for k in range(14):
        poser(rng.choice(["Pin_A", "Pin_B", "Feuillu_A_Ete", "Feuillu_B_Ete", "Bouleau_A", "Feuillu_C_Ete"]),
              (ox + rng.uniform(-13, 13), rng.uniform(2, 6.5), 0), echelle=rng.uniform(.85, 1.15))
    for k in range(5):
        poser(rng.choice(["Rocher_A", "Rocher_B", "Rocher_Mousse", "Rocher_C"]), (ox + rng.uniform(-12, 12), rng.uniform(-6, 2), 0))
    for k in range(6):
        poser(rng.choice(["Buisson_A", "Buisson_B", "Buisson_Baies_Rouges", "Buisson_Baies_Bleues"]),
              (ox + rng.uniform(-12, 12), rng.uniform(-6, 3), 0))
    for k in range(4):
        poser(rng.choice(["Souche_A", "Souche_B", "Cailloux_A", "Cailloux_B"]), (ox + rng.uniform(-10, 10), rng.uniform(-6, 1), 0))
    poser("Arbre_Mort_A", (ox + 6, -2, 0))
    poser("Tronc_Couche", (ox - 5, -1, 0))
    for k in range(70):
        poser(rng.choice(["Herbe_A", "Herbe_B", "Herbe_Haute", "Fleurs", "Herbe_A", "Fougere"]),
              (ox + rng.uniform(-14, 14), rng.uniform(-8, 3), 0), echelle=rng.uniform(.9, 1.4))
    sc = gp.scene_rendu(1920, 1080, ox, 26, cam_y=-14, cam_z=13, cible_z=0)
    cam = sc.camera
    cam.location = (ox, -17, 16)
    d = Vector((ox, 0, 0)) - cam.location
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    sol = bpy.data.objects.get("Sol")
    if sol:
        sol.scale = (4, 4, 1)
        sol.location = (ox, 0, 0)
        sol.data.materials[0].node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (.12, .14, .07, 1)
    gp.rendre(os.path.join(sortie, "apercu_lisiere.png"), sc)
    for o in copies:
        bpy.data.objects.remove(o, do_unlink=True)


def _args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    sortie, rendu = None, True
    i = 0
    while i < len(argv):
        if argv[i] == "--sortie":
            sortie = os.path.abspath(argv[i + 1])
            i += 1
        elif argv[i] == "--sans-rendu":
            rendu = False
        i += 1
    return sortie, rendu


if __name__ == "__main__":
    s, rd = _args()
    if s:
        os.makedirs(s, exist_ok=True)
    principal(s, rd)
