"""
After City — génération du décor urbain abandonné (ruines) dans Blender.

Construit des objets fixes (SM_) prêts pour Unity :
  - épaves de voitures à fouiller et démonter (berline, pick-up, camionnette, brûlée) ;
  - débris (gravats, pneus, bidons, poubelles, palettes, planches) ;
  - maisons abandonnées (pavillon, maison à étage effondrée, cabane) ;
  - clôtures (bois, grillage, intactes et cassées) ;
  - barricades (planches, sacs de sable, cheval de frise, palettes) ;
  - panneaux et mobilier urbain (stop, limite, direction, panneau peint « AIDE »,
    lampadaire cassé, poteau électrique).

Chaque objet a trois niveaux de détail (_LOD0/_LOD1/_LOD2) obtenus en retirant les petites
pièces (pas de décimation : les volumes restent nets), des accroches SOCKET_ et un seul
matériau MAT_Batiments sur T_Batiments_1024 (palette 256 px en haut à gauche, cases unies).

Utilisation (Blender 4.2 ou plus récent, ou le module Python bpy) :
  blender --background --python generer_decor_ruines.py -- --sortie CHEMIN [--sans-rendu]
  python generer_decor_ruines.py --sortie CHEMIN            (avec le module bpy)

Conventions (modeles3d/00_conventions.md) : 1 unité = 1 m, 1 case = 2 m, 1 niveau = 0,5 m,
survivant = 1,00 m, l'objet regarde vers -Y, origine au centre de l'emprise au niveau du sol,
export FBX Forward -Z / Up Y (même réglage que generer_personnages.py).
"""

import math
import os
import random
import sys

import bpy  # noqa: I001 (bpy avant bmesh)
import bmesh
from mathutils import Euler, Matrix, Vector

# ---------------------------------------------------------------------------
# Palette : lignes 0 à 6 identiques à generer_personnages.py, lignes 8 et 9 de
# palette.md (bâtiments), ligne 10 ajoutée pour le décor urbain (voir LISEZMOI).
# ---------------------------------------------------------------------------
PALETTE = {
    "peau_claire": (0, 0, "E3BF9C"), "peau_doree": (0, 1, "C99A6E"),
    "peau_brune": (0, 2, "8D5A3B"), "peau_foncee": (0, 3, "5C3A26"),
    "cheveux_noirs": (0, 4, "2A2420"), "cheveux_chatains": (0, 5, "5A3E2B"),
    "cheveux_paille": (0, 6, "C2A15A"), "cheveux_gris": (0, 7, "9A958C"),
    "barbe": (0, 8, "3A2E26"), "yeux": (0, 9, "1E1B18"),
    "denim": (1, 0, "45607A"), "denim_delave": (1, 1, "7E93A8"),
    "flanelle_rouge": (1, 2, "7E3A30"), "flanelle_verte": (1, 3, "4F6B45"),
    "tshirt_creme": (1, 4, "BFB59C"), "sweat_gris": (1, 5, "7A7A74"),
    "kaki": (1, 6, "8C8158"), "cuir": (1, 7, "6B4A30"),
    "bleu_travail": (1, 8, "3A5068"), "orange_hv": (1, 9, "B07434"),
    "toile_beige": (1, 10, "B5A57A"), "semelle": (1, 11, "2E2925"),
    "olive": (2, 0, "5B6340"), "sable": (2, 1, "A8996B"),
    "noir_tactique": (2, 2, "2E3030"), "bleu_police": (2, 3, "2B3546"),
    "brun_chasse": (2, 4, "6E5435"), "camo": (2, 5, "4E5A36"),
    "orange_chasse": (2, 6, "A85A30"), "blanc_casse": (2, 7, "CFC8B6"),
    "sarcelle": (2, 8, "3E8C84"), "bandana": (2, 9, "7E2F2A"),
    "adhesif": (2, 10, "9EA19C"),
    "acier": (3, 0, "8A8F92"), "acier_sombre": (3, 1, "45494B"),
    "rouille": (3, 2, "8B4F2C"), "bois_manche": (3, 3, "9C7448"),
    "bois_sombre": (3, 4, "5C4632"), "plastique_noir": (3, 5, "26282A"),
    "plastique_jaune": (3, 6, "A88E3E"), "toile_sac": (3, 7, "5F6B5A"),
    "cuir_clair": (3, 8, "8A6A48"), "metal_gourde": (3, 9, "7C8478"),
    "bandage": (3, 10, "C8BFA6"), "terre_visage": (3, 11, "5A4636"),
    "couleur_colonie": (3, 15, "A8432F"),
    "z_peau_recente": (4, 0, "8E9A82"), "z_peau_livide": (4, 1, "A9A594"),
    "z_peau_bleuie": (4, 2, "7D8790"), "z_peau_pourrie": (4, 3, "6E6250"),
    "z_chair": (4, 4, "5E3B34"), "z_sang_seche": (4, 5, "4A1E1A"),
    "z_sang_frais": (4, 6, "6E1E1A"), "z_os": (4, 7, "CFC7AE"),
    "z_cheveux": (4, 8, "3F3A33"), "z_yeux": (4, 9, "C9C6B3"),
    "cerf_pelage": (6, 0, "7A5A3C"), "cerf_ventre": (6, 1, "B9A27E"), "sabot": (6, 2, "2B2420"),
    "bois_cerf": (6, 3, "B7A889"), "sanglier_poil": (6, 4, "4A3B2E"), "sanglier_crete": (6, 5, "2E2620"),
    "defense": (6, 6, "D8CFB4"), "groin": (6, 7, "8A6658"), "cheval_bai": (6, 8, "6B4226"),
    "crin_noir": (6, 9, "231E1B"), "vache_blanc": (6, 10, "D2CCBE"), "vache_noir": (6, 11, "2A2724"),
    "mufle_rose": (6, 12, "B98A80"), "liste_blanche": (6, 13, "E0DACB"),
    # Ligne 8 et 9 : bâtiments (palette.md)
    "rouge_grange": (8, 0, "8E3B2A"), "blanc_ecaille": (8, 1, "D8D2C0"),
    "bardeaux": (8, 2, "5B5650"), "bois_neuf": (8, 3, "B88A57"),
    "bois_vieilli": (8, 4, "8E8574"), "rondin": (8, 5, "7A5B3C"),
    "tole_galva": (8, 6, "A3A8A6"), "tole_rouillee": (8, 7, "8B5A3A"),
    "bache_bleue": (8, 8, "3F6E8F"), "toile_tente": (8, 9, "A49A72"),
    "sac_sable": (8, 10, "B5A57A"), "pierre": (8, 11, "9C978A"),
    "brique": (8, 12, "9A5A40"), "beton": (8, 13, "A6A39A"),
    "vitre_sale": (8, 14, "6F7C78"), "vegetation": (8, 15, "55663A"),
    "fenetre_eclairee": (9, 0, "FFB766"), "braises": (9, 1, "FF8A3D"),
    "suie": (9, 2, "2B2622"), "paille": (9, 3, "C9B26A"), "terre": (9, 4, "6C5236"),
    # Ligne 10 : décor urbain (ajout de ce script)
    "voiture_bleue": (10, 0, "5E7387"), "voiture_rouge": (10, 1, "8E4436"),
    "voiture_creme": (10, 2, "C8BC9A"), "voiture_verte": (10, 3, "5D6E55"),
    "pneu": (10, 4, "222324"), "chrome": (10, 5, "A9ADA8"),
    "feu_rouge": (10, 6, "7A2620"), "panneau_rouge": (10, 7, "A33A2E"),
    "panneau_blanc": (10, 8, "DCD8CC"), "panneau_vert": (10, 9, "3F6B4E"),
    "panneau_jaune": (10, 10, "C9A23A"), "peinture_rouge": (10, 11, "9A2B22"),
    "contreplaque": (10, 12, "B49A6A"), "grillage": (10, 13, "8D928F"),
    "poubelle_verte": (10, 14, "3E5A45"), "bidon_bleu": (10, 15, "3B5F86"),
    "interieur_sombre": (11, 0, "1F1D1B"), "mousse": (11, 1, "6F7A45"),
    "herbe_seche": (11, 2, "9A9256"), "bois_peint_vert": (11, 3, "4E6A5A"),
}
# Ligne 5 : tissus de zombies (même calcul que generer_personnages.py)
for _nom in list(PALETTE):
    _r, _c, _hex = PALETTE[_nom]
    if _r == 1:
        rgb = [int(_hex[i:i + 2], 16) / 255 for i in (0, 2, 4)]
        gris = sum(rgb) / 3
        rgb = [(v + (gris - v) * 0.45) * 0.8 for v in rgb]
        PALETTE["z_" + _nom] = (5, _c, "".join("%02X" % round(v * 255) for v in rgb))

CASE = 16
CELL = 2.0     # une case de grille
NIVEAU = 0.5   # un niveau de terrain


TEX = 1024          # T_Batiments_1024 : palette 256 px dans le coin haut-gauche
ECH = 256.0 / TEX


def uv_case(nom):
    ligne, col, _ = PALETTE[nom]
    return ((col + 0.5) / 16.0 * ECH, 1.0 - (ligne + 0.5) / 16.0 * ECH)


def creer_palette(dossier=None):
    """T_Batiments_1024 : la palette (16 x 16 cases de 16 px) en haut à gauche ; le reste
    est réservé à la future trim sheet et reste gris neutre pour l'instant."""
    import numpy as np
    img = bpy.data.images.get("T_Batiments_1024") or bpy.data.images.new("T_Batiments_1024", TEX, TEX, alpha=False)
    px = np.full((TEX, TEX, 4), 0.502, dtype=np.float32)   # gris #808080 (image 8 bits : valeurs sRGB)
    px[..., 3] = 1.0
    for ligne, col, hx in PALETTE.values():
        rgb = [(int(hx[i:i + 2], 16) / 255) for i in (0, 2, 4)]
        y1 = TEX - ligne * CASE
        px[y1 - CASE:y1, col * CASE:(col + 1) * CASE, :3] = rgb
    img.pixels.foreach_set(px.ravel())
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
    ao.inputs[1].default_value, ao.inputs[2].default_value = .6, .4
    cr = nt.nodes.new("ShaderNodeMath")
    cr.operation = "MULTIPLY_ADD"
    nt.links.new(sep.outputs[1], cr.inputs[0])
    cr.inputs[1].default_value, cr.inputs[2].default_value = -.45, 1.0
    fac = nt.nodes.new("ShaderNodeMath")
    fac.operation = "MULTIPLY"
    nt.links.new(ao.outputs[0], fac.inputs[0])
    nt.links.new(cr.outputs[0], fac.inputs[1])
    sc = nt.nodes.new("ShaderNodeVectorMath")
    sc.operation = "SCALE"
    nt.links.new(tex.outputs["Color"], sc.inputs[0])
    nt.links.new(fac.outputs[0], sc.inputs["Scale"])
    nt.links.new(sc.outputs[0], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.92
    return mat


# ---------------------------------------------------------------------------
# Constructeur : pièces simples, chacune marquée d'un niveau de détail.
#   lod=2 : présente dans LOD0, LOD1 et LOD2 (les grands volumes)
#   lod=1 : présente dans LOD0 et LOD1
#   lod=0 : seulement dans LOD0 (petits détails)
# ---------------------------------------------------------------------------
def _rot(r):
    return Euler([math.radians(a) for a in r], "XYZ").to_matrix().to_4x4()


class Piece:
    __slots__ = ("kind", "args", "couleur", "lod", "sale", "matrice")

    def __init__(self, kind, args, couleur, lod, sale, matrice):
        self.kind, self.args, self.couleur, self.lod, self.sale, self.matrice = kind, args, couleur, lod, sale, matrice


class Objet:
    """Description d'un objet de décor, construite une fois puis convertie en 3 LOD."""

    def __init__(self, nom, emprise, note=""):
        self.nom = nom
        self.emprise = emprise      # (cases en X, cases en Y)
        self.note = note
        self.pieces = []
        self.sockets = []
        self.base = Matrix.Identity(4)

    def boite(self, centre, taille, couleur, lod=2, rot=(0, 0, 0), sale=0.25, effile=(1.0, 1.0)):
        m = self.base @ Matrix.Translation(Vector(centre)) @ _rot(rot)
        self.pieces.append(Piece("boite", (taille, effile), couleur, lod, sale, m))

    def cyl(self, centre, rayon, hauteur, couleur, lod=2, rot=(0, 0, 0), seg=8, rayon_haut=None, sale=0.25):
        m = self.base @ Matrix.Translation(Vector(centre)) @ _rot(rot)
        self.pieces.append(Piece("cyl", (rayon, hauteur, seg, rayon_haut), couleur, lod, sale, m))

    def prisme(self, centre, largeur, profondeur, hauteur, couleur, lod=2, rot=(0, 0, 0), sale=0.25):
        """Pignon triangulaire : base largeur x profondeur, pointe en haut au centre."""
        m = self.base @ Matrix.Translation(Vector(centre)) @ _rot(rot)
        self.pieces.append(Piece("prisme", (largeur, profondeur, hauteur), couleur, lod, sale, m))

    def socket(self, nom, pos):
        self.sockets.append((nom, Vector(pos)))

    def mesh(self, niveau, mat, hauteur):
        bm = bmesh.new()
        uv = bm.loops.layers.uv.verify()
        col = bm.loops.layers.float_color.new("Col") if hasattr(bm.loops.layers, "float_color") else None
        for p in self.pieces:
            if p.lod < niveau:
                continue
            avant = set(bm.verts)
            if p.kind == "boite":
                (sx, sy, sz), (fx, fy) = p.args
                res = bmesh.ops.create_cube(bm, size=1.0)
                for v in res["verts"]:
                    k = (fx, fy) if v.co.z > 0 else (1.0, 1.0)
                    v.co = Vector((v.co.x * sx * k[0], v.co.y * sy * k[1], v.co.z * sz))
            elif p.kind == "cyl":
                r, h, seg, rh = p.args
                bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg,
                                      radius1=r, radius2=r if rh is None else rh, depth=h)
            else:
                w, d, h = p.args
                vs = [bm.verts.new(c) for c in ((-w / 2, -d / 2, 0), (w / 2, -d / 2, 0), (0, -d / 2, h),
                                                 (-w / 2, d / 2, 0), (w / 2, d / 2, 0), (0, d / 2, h))]
                for f in ((0, 1, 2), (5, 4, 3), (0, 3, 4, 1), (1, 4, 5, 2), (2, 5, 3, 0)):
                    bm.faces.new([vs[i] for i in f])
            nouveaux = [v for v in bm.verts if v not in avant]
            bmesh.ops.transform(bm, matrix=p.matrice, verts=nouveaux)
            u, vv = uv_case(p.couleur)
            fs = {f for v in nouveaux for f in v.link_faces}
            for f in fs:
                for lo in f.loops:
                    lo[uv].uv = (u, vv)
                    if col is not None:
                        h = max(0.0, min(1.0, lo.vert.co.z / max(hauteur, 0.3)))
                        # R : occlusion approchée par la hauteur ; G : saleté et rouille
                        lo[col] = (0.55 + 0.45 * h, p.sale * (1.0 - 0.5 * h), 0.0, 1.0)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        me = bpy.data.meshes.new("%s_LOD%d" % (self.nom, niveau))
        bm.to_mesh(me)
        bm.free()
        me.materials.append(mat)
        for poly in me.polygons:
            poly.use_smooth = False
        return me


# ---------------------------------------------------------------------------
# Pièces communes
# ---------------------------------------------------------------------------
def roue(o, x, y, rayon=.19, largeur=.15, crevee=False, lod=2):
    s = .55 if crevee else 1.0
    o.cyl((x, y, rayon * s), rayon, largeur, "pneu", lod=lod, rot=(0, 90, 0), seg=8)
    if s == 1.0:
        o.cyl((x + (largeur / 2 + .005) * (1 if x > 0 else -1), y, rayon), rayon * .5, .01, "chrome", lod=0, rot=(0, 90, 0), seg=6)


def planche(o, centre, longueur, couleur="bois_vieilli", rot=(0, 0, 0), lod=1, ep=.035, larg=.14):
    o.boite(centre, (longueur, larg, ep), couleur, lod=lod, rot=rot)


def pile_gravats(o, rnd, cx, cy, rayon, nombre, couleurs, hmax=.35, lod_min=0):
    for i in range(nombre):
        a = rnd.uniform(0, math.tau)
        d = rayon * math.sqrt(rnd.random())
        t = rnd.uniform(.08, .26)
        h = max(.04, hmax * (1 - d / rayon) * rnd.uniform(.5, 1.0))
        lod = 2 if t > .2 else (1 if t > .14 else lod_min)
        o.boite((cx + math.cos(a) * d, cy + math.sin(a) * d, h * .5), (t, t * rnd.uniform(.6, 1.2), h),
                rnd.choice(couleurs), lod=lod, rot=(rnd.uniform(-12, 12), rnd.uniform(-12, 12), rnd.uniform(0, 90)), sale=.45)


# ---------------------------------------------------------------------------
# Épaves de véhicules (à fouiller, puis démonter à la raffinerie)
# ---------------------------------------------------------------------------
def voiture(nom, peinture, variante, rnd):
    o = Objet(nom, (1, 2), "épave à fouiller puis démonter")
    W, L = 1.12, 2.7          # échelle jeu (≈ 0,6 du réel, comme les bâtiments)
    brulee = variante == "brulee"
    p = "suie" if brulee else peinture
    sale = .7 if brulee else .45
    zc = .2
    # caisse basse
    o.boite((0, 0, zc + .17), (W, L, .34), p, sale=sale)
    # capot (avant = -Y) et coffre
    if variante == "capot":
        o.boite((0, -L / 2 + .42, zc + .5), (W - .1, .7, .04), p, rot=(-55, 0, 0), sale=sale, lod=1)
        o.boite((0, -L / 2 + .4, zc + .36), (.5, .4, .14), "acier_sombre", lod=1, sale=.6)   # moteur visible
        o.boite((0, -L / 2 + .4, zc + .44), (.3, .2, .05), "rouille", lod=0)
    else:
        o.boite((0, -L / 2 + .42, zc + .355), (W - .06, .78, .03), p, sale=sale, lod=1)
    # habitacle
    cab_y = .1
    if not brulee:
        o.boite((0, cab_y, zc + .52), (W - .1, 1.25, .32), p, effile=(.92, .82), sale=sale)
        o.boite((0, cab_y - .5, zc + .52), (W - .16, .26, .27), "vitre_sale", rot=(28, 0, 0), lod=1)  # pare-brise
        o.boite((W / 2 - .04, cab_y + .05, zc + .54), (.03, .95, .2), "vitre_sale", lod=1)
        o.boite((-W / 2 + .04, cab_y + .05, zc + .54), (.03, .95, .2), "vitre_sale" if variante != "portiere" else "interieur_sombre", lod=1)
        o.boite((0, cab_y + .62, zc + .53), (W - .2, .03, .2), "vitre_sale", lod=1)
        o.boite((.15, cab_y - .53, zc + .55), (.18, .02, .1), "interieur_sombre", rot=(28, 0, 0), lod=0)  # vitre fêlée
    else:
        o.boite((0, cab_y, zc + .52), (W - .1, 1.25, .3), "suie", effile=(.92, .82), sale=.8)
        o.boite((0, cab_y, zc + .66), (W - .3, .9, .04), "interieur_sombre", lod=1)
        o.boite((0, cab_y, zc + .4), (W - .16, 1.1, .08), "rouille", lod=1)
    # pare-chocs, phares, feux
    o.boite((0, -L / 2 - .02, zc + .12), (W + .02, .07, .1), "chrome" if not brulee else "acier_sombre", lod=1)
    o.boite((0, L / 2 + .02, zc + .12), (W + .02, .07, .1), "chrome" if not brulee else "acier_sombre", lod=1)
    for sx in (-1, 1):
        o.boite((sx * (W / 2 - .14), -L / 2 - .005, zc + .26), (.16, .03, .07), "panneau_blanc" if not brulee else "suie", lod=0)
        o.boite((sx * (W / 2 - .12), L / 2 + .005, zc + .26), (.14, .03, .07), "feu_rouge" if not brulee else "suie", lod=0)
    # rouille et taches
    for _ in range(4):
        sx = rnd.choice((-1, 1))
        o.boite((sx * (W / 2 + .004), rnd.uniform(-L / 2 + .3, L / 2 - .3), zc + rnd.uniform(.08, .26)),
                (.01, rnd.uniform(.15, .4), rnd.uniform(.06, .14)), "rouille", lod=0)
    # roues : une crevée, une remplacée par un parpaing
    roues = [(-1, -1), (1, -1), (-1, 1), (1, 1)]
    for i, (sx, sy) in enumerate(roues):
        x, y = sx * (W / 2 - .06), sy * (L / 2 - .5)
        if brulee:
            o.cyl((x, y, .12), .16, .14, "acier_sombre", rot=(0, 90, 0), seg=8)
        elif i == 2:
            o.boite((x, y, .1), (.16, .3, .2), "beton", lod=1, sale=.5)
        else:
            roue(o, x, y, crevee=(i == 1))
    if variante == "portiere":   # portière arrachée, au sol
        o.boite((-W / 2 - .2, cab_y, .03), (.55, .8, .05), p, rot=(0, 0, 14), sale=sale, lod=1)
    if variante == "capot" or variante == "portiere":
        o.boite((W / 2 + .35, L / 2 - .3, .05), (.22, .22, .1), "pneu", lod=0)
    o.socket("SOCKET_Travail_01", (W / 2 + .45, -.3, 0))
    o.socket("SOCKET_Travail_02", (-W / 2 - .45, .3, 0))
    o.socket("SOCKET_Fumee", (0, -L / 2 + .4, .6))
    return o


def pickup(nom, peinture, rnd):
    o = Objet(nom, (1, 2), "pick-up à fouiller, benne pleine de débris")
    W, L, zc = 1.16, 2.9, .22
    o.boite((0, 0, zc + .18), (W, L, .36), peinture, sale=.5)
    o.boite((0, -.55, zc + .58), (W - .08, .95, .42), peinture, effile=(.94, .8), sale=.5)
    o.boite((0, -1.0, zc + .6), (W - .14, .2, .28), "vitre_sale", rot=(25, 0, 0), lod=1)
    for sx in (-1, 1):
        o.boite((sx * (W / 2 - .03), -.55, zc + .62), (.03, .6, .2), "vitre_sale", lod=1)
    o.boite((0, -.07, zc + .62), (W - .2, .03, .2), "vitre_sale", lod=1)
    # benne
    o.boite((0, .75, zc + .37), (W - .04, 1.3, .03), "rouille", lod=1)
    for sx in (-1, 1):
        o.boite((sx * (W / 2 - .03), .75, zc + .5), (.05, 1.32, .26), peinture, sale=.55)
    o.boite((0, L / 2 - .02, zc + .5), (W, .05, .26), peinture, rot=(-35, 0, 0), sale=.55, lod=1)  # hayon tombant
    for i in range(5):
        o.boite((rnd.uniform(-.35, .35), .75 + rnd.uniform(-.45, .45), zc + .45), (rnd.uniform(.12, .3), rnd.uniform(.1, .25), rnd.uniform(.08, .18)),
                rnd.choice(("bois_vieilli", "acier", "plastique_noir", "tole_rouillee")), rot=(0, 0, rnd.uniform(0, 90)), lod=0)
    o.boite((0, -L / 2 - .02, zc + .12), (W + .04, .08, .12), "chrome", lod=1)
    o.boite((0, -L / 2 - .005, zc + .3), (.7, .03, .12), "acier_sombre", lod=0)   # calandre
    for i, (sx, sy) in enumerate(((-1, -1), (1, -1), (-1, 1), (1, 1))):
        roue(o, sx * (W / 2 - .05), sy * (L / 2 - .55), rayon=.21, crevee=(i == 3))
    o.socket("SOCKET_Travail_01", (W / 2 + .45, .7, 0))
    o.socket("SOCKET_Travail_02", (-W / 2 - .45, -.5, 0))
    return o


def camionnette(nom, rnd):
    o = Objet(nom, (1, 2), "camionnette de livraison, portes arrière ouvertes")
    W, L, zc = 1.18, 2.9, .22
    o.boite((0, .15, zc + .5), (W, L - .3, 1.0), "voiture_creme", sale=.55)
    o.boite((0, -L / 2 + .2, zc + .3), (W - .04, .5, .6), "voiture_creme", effile=(1, .7), sale=.55)
    o.boite((0, -L / 2 + .42, zc + .78), (W - .16, .05, .34), "vitre_sale", rot=(18, 0, 0), lod=1)
    o.boite((0, -L / 2 + .02, zc + .1), (W + .02, .08, .1), "acier_sombre", lod=1)
    o.boite((0, .3, zc + 1.0), (W - .1, 1.6, .02), "rouille", lod=0)  # toit rouillé
    o.boite((W / 2 + .004, .4, zc + .6), (.01, 1.2, .3), "panneau_vert", lod=0)  # bande de l'ancienne société
    # portes arrière ouvertes, intérieur sombre
    o.boite((0, L / 2 - .02, zc + .5), (W - .14, .02, .86), "interieur_sombre", lod=1)
    o.boite((W / 2 + .05, L / 2 + .25, zc + .5), (.04, .52, .86), "voiture_creme", rot=(0, 0, 0), lod=1)
    o.boite((-W / 2 - .05, L / 2 + .25, zc + .5), (.04, .52, .86), "voiture_creme", lod=1)
    o.boite((.15, L / 2 - .3, zc + .1), (.35, .3, .3), "toile_sac", lod=0)
    o.boite((-.2, L / 2 - .4, zc + .08), (.3, .3, .25), "bois_vieilli", lod=0, rot=(0, 0, 20))
    for sx, sy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
        roue(o, sx * (W / 2 - .05), sy * (L / 2 - .5), rayon=.21, crevee=(sx > 0 and sy > 0))
    o.socket("SOCKET_Travail_01", (0, L / 2 + .7, 0))
    o.socket("SOCKET_Travail_02", (W / 2 + .45, -.6, 0))
    return o


# ---------------------------------------------------------------------------
# Débris
# ---------------------------------------------------------------------------
def gravats(nom, rnd, couleurs, n=34, rayon=.9):
    o = Objet(nom, (1, 1), "tas de gravats : ordre Nettoyer")
    pile_gravats(o, rnd, 0, 0, rayon, n, couleurs, hmax=.42)
    planche(o, (.2, -.1, .3), 1.1, rot=(0, 18, 30), lod=1)
    planche(o, (-.3, .2, .2), .9, couleur="bois_sombre", rot=(0, -12, -50), lod=1)
    o.socket("SOCKET_Travail_01", (0, -1.0, 0))
    return o


def pneus(nom, rnd):
    o = Objet(nom, (1, 1), "pneus usagés : caoutchouc au recycleur")
    for i in range(4):
        o.cyl((-.3 + rnd.uniform(-.02, .02), .1, .07 + i * .15), .32, .14, "pneu", seg=10, lod=2 if i < 2 else 1)
        o.cyl((-.3, .1, .07 + i * .15), .14, .15, "interieur_sombre", seg=8, lod=0)
    o.cyl((.42, -.25, .32), .32, .14, "pneu", rot=(80, 0, 25), seg=10, lod=1)
    o.cyl((.35, .45, .07), .32, .14, "pneu", seg=10, lod=1)
    return o


def bidons(nom, rnd):
    o = Objet(nom, (1, 1), "bidons de 200 l : un renversé, un brûle-tout")
    o.cyl((-.3, -.2, .38), .25, .76, "bidon_bleu", seg=10, sale=.5)
    o.cyl((.3, -.1, .38), .25, .76, "rouille", seg=10, sale=.6)
    o.cyl((-.3, -.2, .765), .23, .01, "acier_sombre", seg=10, lod=0)
    o.cyl((.1, .45, .25), .25, .76, "acier", rot=(90, 0, 70), seg=10, sale=.5, lod=1)
    o.boite((.35, .7, .01), (.5, .35, .02), "suie", lod=0)    # flaque
    for k in range(3):
        o.boite((.3, -.1 + (k - 1) * .14, .2 + .2 * k), (.52, .01, .03), "acier_sombre", lod=0)
    return o


def poubelles(nom, rnd):
    o = Objet(nom, (1, 1), "conteneur et sacs : déchets pour le recycleur")
    o.boite((0, .2, .4), (1.2, .7, .8), "poubelle_verte", effile=(1.0, 1.05), sale=.6)
    o.boite((0, .02, .86), (1.24, .72, .06), "poubelle_verte", rot=(-30, 0, 0), lod=1, sale=.6)
    for sx in (-1, 1):
        o.cyl((sx * .48, -.05, .06), .06, .05, "plastique_noir", rot=(0, 90, 0), seg=6, lod=0)
    for i in range(6):
        o.boite((rnd.uniform(-.7, .7), rnd.uniform(-.75, -.25), .14), (rnd.uniform(.25, .36),) * 2 + (rnd.uniform(.22, .3),),
                rnd.choice(("plastique_noir", "plastique_noir", "bidon_bleu")), rot=(0, 0, rnd.uniform(0, 90)), lod=1 if i < 3 else 0, effile=(.7, .7), sale=.3)
    return o


def palettes(nom, rnd):
    o = Objet(nom, (1, 1), "palettes et planches : bois de récupération")
    for k in range(3):
        z = k * .14
        r = rnd.uniform(-8, 8)
        for j in range(5):
            o.boite((0, -.4 + j * .2, z + .12), (1.2, .12, .025), "bois_vieilli", rot=(0, 0, r), lod=1 if k < 2 else 0)
        for x in (-.5, 0, .5):
            o.boite((x, 0, z + .055), (.1, 1.0, .09), "bois_sombre", rot=(0, 0, r), lod=2 if k == 0 else 1)
    planche(o, (.1, -.75, .2), 1.4, rot=(0, -10, 8), lod=1)
    planche(o, (-.6, .65, .05), 1.1, rot=(0, 0, 60), lod=0)
    return o


def debris_tole(nom, rnd):
    o = Objet(nom, (1, 1), "tôles arrachées et ferraille : ordre Démonter")
    for i in range(4):
        o.boite((rnd.uniform(-.4, .4), rnd.uniform(-.4, .4), .04 + i * .03), (1.1, .65, .015),
                rnd.choice(("tole_rouillee", "tole_galva")), rot=(rnd.uniform(-8, 8), rnd.uniform(-8, 8), rnd.uniform(0, 180)), lod=2 if i < 2 else 1)
    o.boite((.5, .5, .25), (.06, .06, .5), "acier_sombre", rot=(25, 0, 0), lod=1)
    o.cyl((-.55, .55, .12), .12, .24, "rouille", seg=8, lod=0)
    return o


# ---------------------------------------------------------------------------
# Maisons abandonnées
# ---------------------------------------------------------------------------
def mur(o, axe, centre, longueur, hauteur, ep, couleur, ouvertures, z0=0.0, lod=2):
    """Mur plein découpé autour des ouvertures (position, largeur, bas, haut)."""
    cx, cy = centre
    a0 = -longueur / 2
    ouvertures = sorted(ouvertures)

    def piece(a, b, zb, zh):
        if b - a < .01 or zh - zb < .01:
            return
        m = (a + b) / 2
        if axe == "x":
            o.boite((cx + m, cy, z0 + (zb + zh) / 2), (b - a, ep, zh - zb), couleur, lod=lod)
        else:
            o.boite((cx, cy + m, z0 + (zb + zh) / 2), (ep, b - a, zh - zb), couleur, lod=lod)

    cur = a0
    for pos, larg, bas, haut in ouvertures:
        g, d = pos - larg / 2, pos + larg / 2
        piece(cur, g, 0, hauteur)
        piece(g, d, 0, bas)
        piece(g, d, haut, hauteur)
        cur = d
    piece(cur, longueur / 2, 0, hauteur)


def fenetre(o, axe, centre, pos, z, larg, haut, etat, ep, rnd, z0=0.0):
    """Remplit une ouverture : vitre sale, vitre cassée (trou sombre) ou planches clouées."""
    cx, cy = centre
    p = (cx + pos, cy) if axe == "x" else (cx, cy + pos)
    t = (larg, .02, haut) if axe == "x" else (.02, larg, haut)
    zc = z0 + z + haut / 2
    if etat == "vitre":
        o.boite((p[0], p[1], zc), t, "vitre_sale", lod=1)
    else:
        o.boite((p[0], p[1], zc), t, "interieur_sombre", lod=1)
    if etat == "planches":
        for k, ang in enumerate((18, -14, 4)):
            off = (ep / 2 + .02)
            q = (p[0], p[1] - off) if axe == "x" else (p[0] + off, p[1])
            rot = (0, ang, 0) if axe == "x" else (ang, 0, 90)
            o.boite((q[0], q[1], zc + (k - 1) * haut * .28), (larg + .14, .03, .1) if axe == "x" else (larg + .14, .03, .1),
                    "bois_vieilli" if k != 1 else "bois_neuf", rot=rot if axe == "x" else (0, ang, 90), lod=0)
    # appui de fenêtre
    if axe == "x":
        o.boite((p[0], p[1] - ep / 2 - .02, z0 + z - .02), (larg + .1, .06, .04), "blanc_ecaille", lod=0)
    else:
        o.boite((p[0] + ep / 2 + .02, p[1], z0 + z - .02), (.06, larg + .1, .04), "blanc_ecaille", lod=0)


def toit_deux_pans(o, cx, cy, lx, ly, z, pente, couleur, ep=.07, debord=.18, trous=(), axe="x", lod=2, tombes=None, rnd=None):
    """Toit à deux pans, faîtage le long de `axe`, en plaques pour pouvoir en retirer (trous)."""
    demi = (ly if axe == "x" else lx) / 2 + debord
    long_ = (lx if axe == "x" else ly) + 2 * debord
    h = demi * math.tan(math.radians(pente))
    l_pan = demi / math.cos(math.radians(pente))
    n = 4
    for cote in (-1, 1):
        for i in range(n):
            if (cote, i) in trous:
                continue
            a = -long_ / 2 + (i + .5) * long_ / n
            if axe == "x":
                o.boite((cx + a, cy + cote * demi / 2, z + h / 2), (long_ / n + .002, l_pan, ep), couleur,
                        rot=(-cote * pente, 0, 0), lod=lod, sale=.5)
            else:
                o.boite((cx + cote * demi / 2, cy + a, z + h / 2), (l_pan, long_ / n + .002, ep), couleur,
                        rot=(0, cote * pente, 0), lod=lod, sale=.5)
    # pignons
    if axe == "x":
        for s in (-1, 1):
            o.prisme((cx + s * lx / 2, cy, z), ly, .1, h - .02, "blanc_ecaille", rot=(0, 0, 90), lod=lod)
        o.boite((cx, cy, z + h + .01), (long_, .1, .06), "bardeaux", lod=1)
    else:
        for s in (-1, 1):
            o.prisme((cx, cy + s * ly / 2, z), lx, .1, h - .02, "blanc_ecaille", lod=lod)
        o.boite((cx, cy, z + h + .01), (.1, long_, .06), "bardeaux", lod=1)
    # plaques tombées au sol
    if tombes and rnd:
        for (x, y) in tombes:
            o.boite((x, y, .06), (long_ / n, l_pan * .9, ep), couleur, rot=(rnd.uniform(-15, 15), rnd.uniform(-10, 10), rnd.uniform(0, 60)), lod=1, sale=.6)
    return h


def maison_a(nom, rnd):
    """Pavillon de plain-pied 2 x 2 cases, véranda, toit percé."""
    o = Objet(nom, (2, 2), "pavillon abandonné : à fouiller puis rénover")
    lx, ly, H, ep = 3.4, 2.8, 1.6, .12
    cy = .3
    o.boite((0, cy, .1), (lx + .1, ly + .1, .2), "pierre", sale=.5)
    z0 = .2
    yf = cy - ly / 2   # façade (avant, -Y)
    mur(o, "x", (0, yf), lx, H, ep, "blanc_ecaille", [(-.9, .8, .55, 1.25), (0.15, .8, 0, 1.3), (1.05, .8, .55, 1.25)], z0=z0)
    mur(o, "x", (0, cy + ly / 2), lx, H, ep, "blanc_ecaille", [(-.6, .8, .55, 1.25), (.9, .8, .55, 1.25)], z0=z0)
    mur(o, "y", (-lx / 2, cy), ly, H, ep, "blanc_ecaille", [(0, .8, .55, 1.25)], z0=z0)
    mur(o, "y", (lx / 2, cy), ly, H, ep, "blanc_ecaille", [(.2, .8, .55, 1.25)], z0=z0)
    fenetre(o, "x", (0, yf), -.9, .55, .8, .7, "planches", ep, rnd, z0=z0)
    fenetre(o, "x", (0, yf), 1.05, .55, .8, .7, "cassee", ep, rnd, z0=z0)
    fenetre(o, "x", (0, cy + ly / 2), -.6, .55, .8, .7, "vitre", ep, rnd, z0=z0)
    fenetre(o, "x", (0, cy + ly / 2), .9, .55, .8, .7, "cassee", ep, rnd, z0=z0)
    fenetre(o, "y", (-lx / 2, cy), 0, .55, .8, .7, "vitre", ep, rnd, z0=z0)
    fenetre(o, "y", (lx / 2, cy), .2, .55, .8, .7, "planches", ep, rnd, z0=z0)
    # porte arrachée (au sol) et encadrement sombre
    o.boite((.15, yf + .02, z0 + .65), (.8, .02, 1.3), "interieur_sombre", lod=1)
    o.boite((.55, yf - .75, .24), (.8, 1.3, .05), "bois_peint_vert", rot=(0, 0, 25), lod=1)
    # plancher, intérieur sombre visible par le trou
    o.boite((0, cy, z0 + .01), (lx - .2, ly - .2, .02), "bois_sombre", lod=1)
    # bardage : bandes horizontales et coins
    for k in range(3):
        o.boite((0, yf - ep / 2 - .004, z0 + .3 + k * .5), (lx, .008, .03), "bois_vieilli", lod=0)
    for sx in (-1, 1):
        o.boite((sx * lx / 2, yf, z0 + H / 2), (.14, .14, H), "blanc_ecaille", lod=1)
    # toit percé, plaques tombées
    toit_deux_pans(o, 0, cy, lx, ly, z0 + H, 30, "bardeaux", trous={(-1, 2), (-1, 1)}, axe="x",
                   tombes=[(.6, cy - .3), (1.0, yf - 1.0)], rnd=rnd)
    o.boite((.6, cy, z0 + .3), (1.0, .8, .4), "bardeaux", rot=(0, 20, 30), lod=1)   # éboulis dedans
    # cheminée
    o.boite((-1.1, cy + .6, z0 + H + .7), (.3, .3, 1.2), "brique", lod=1, sale=.5)
    # véranda
    o.boite((0, yf - .45, .1), (lx, .8, .08), "bois_vieilli", lod=2, sale=.5)
    for x in (-1.6, -.4, .8, 1.6):
        o.boite((x, yf - .8, .14 + .6), (.08, .08, 1.2), "bois_vieilli", lod=1)
    o.boite((0, yf - .45, 1.38), (lx + .2, .95, .05), "tole_rouillee", rot=(12, 0, 0), lod=1, sale=.6)
    o.boite((-1.3, yf - .82, .45), (.7, .04, .05), "bois_vieilli", lod=0)   # garde-corps cassé
    o.boite((-1.0, yf - .9, .2), (.7, .04, .05), "bois_vieilli", rot=(0, 25, 0), lod=0)
    # végétation qui reprend
    for i in range(8):
        x = rnd.uniform(-lx / 2, lx / 2)
        side = rnd.choice((yf - ep / 2 - .05, cy + ly / 2 + ep / 2 + .05))
        o.boite((x, side, .25), (rnd.uniform(.2, .45), .12, rnd.uniform(.3, .7)), rnd.choice(("vegetation", "mousse", "herbe_seche")), lod=0, sale=.1)
    o.boite((lx / 2 + .07, cy - .4, z0 + .9), (.04, .6, 1.4), "vegetation", lod=0)   # lierre
    pile_gravats(o, rnd, -1.5, yf - .8, .35, 6, ("brique", "pierre", "bois_vieilli"), hmax=.2)
    o.socket("SOCKET_Porte", (.15, yf - 1.1, 0))
    o.socket("SOCKET_Travail_01", (-1.2, yf - 1.1, 0))
    o.socket("SOCKET_Travail_02", (lx / 2 + .5, cy, 0))
    return o


def maison_b(nom, rnd):
    """Maison à étage 3 x 2 cases, angle arrière droit effondré, garage."""
    o = Objet(nom, (3, 2), "maison à étage effondrée : gravats et pièces rares à l'étage")
    lx, ly, H, ep = 5.0, 3.0, 1.6, .12
    cy = .25
    yf = cy - ly / 2
    o.boite((0, cy, .1), (lx + .1, ly + .1, .2), "beton", sale=.5)
    z0 = .2
    # rez-de-chaussée
    mur(o, "x", (0, yf), lx, H, ep, "voiture_creme", [(-1.7, .8, .55, 1.25), (-.6, .8, 0, 1.3), (1.4, 1.6, 0, 1.25)], z0=z0)
    mur(o, "x", (0, cy + ly / 2), lx, H, ep, "voiture_creme", [(-1.4, .8, .55, 1.25), (0, .8, .55, 1.25)], z0=z0)
    mur(o, "y", (-lx / 2, cy), ly, H, ep, "voiture_creme", [(.3, .8, .55, 1.25)], z0=z0)
    mur(o, "y", (lx / 2, cy), ly, H, ep, "voiture_creme", [], z0=z0)
    fenetre(o, "x", (0, yf), -1.7, .55, .8, .7, "vitre", ep, rnd, z0=z0)
    fenetre(o, "x", (0, cy + ly / 2), -1.4, .55, .8, .7, "cassee", ep, rnd, z0=z0)
    fenetre(o, "x", (0, cy + ly / 2), 0, .55, .8, .7, "planches", ep, rnd, z0=z0)
    fenetre(o, "y", (-lx / 2, cy), .3, .55, .8, .7, "cassee", ep, rnd, z0=z0)
    o.boite((-.6, yf + .02, z0 + .65), (.8, .02, 1.3), "interieur_sombre", lod=1)
    # porte de garage à moitié levée, tordue
    o.boite((1.4, yf + .02, z0 + .62), (1.6, .02, 1.25), "interieur_sombre", lod=1)
    o.boite((1.4, yf - .02, z0 + .95), (1.6, .05, .6), "tole_galva", rot=(-14, 0, 2), lod=1, sale=.6)
    # plancher de l'étage et étage (partiel : l'angle arrière droit est effondré)
    z1 = z0 + H
    o.boite((-.6, cy, z1), (lx - 1.2, ly, .1), "bois_sombre", lod=2)
    mur(o, "x", (-.6, yf), lx - 1.2, H, ep, "voiture_creme", [(-1.1, .8, .55, 1.25), (.5, .8, .55, 1.25)], z0=z1 + .05)
    mur(o, "y", (-lx / 2, cy), ly, H, ep, "voiture_creme", [(0, .8, .55, 1.25)], z0=z1 + .05)
    mur(o, "x", (-1.2, cy + ly / 2), lx - 2.4, H, ep, "voiture_creme", [(.0, .8, .55, 1.25)], z0=z1 + .05)
    fenetre(o, "x", (-.6, yf), -1.1, .55, .8, .7, "cassee", ep, rnd, z0=z1 + .05)
    fenetre(o, "x", (-.6, yf), .5, .55, .8, .7, "planches", ep, rnd, z0=z1 + .05)
    fenetre(o, "y", (-lx / 2, cy), 0, .55, .8, .7, "vitre", ep, rnd, z0=z1 + .05)
    # moignon de mur cassé en escalier
    for k in range(4):
        o.boite((lx / 2 - 1.2 + .05, cy - ly / 2 + .4 + k * .35, z1 + .05 + (3 - k) * .3 / 2), (ep, .35, (3 - k) * .3 + .1), "voiture_creme", lod=1)
    # toit sur la partie debout, en appentis cassé sur l'angle
    toit_deux_pans(o, -.6, cy, lx - 1.2, ly, z1 + .05 + H, 28, "bardeaux", trous={(1, 3)}, axe="x")
    o.boite((1.6, cy + .5, z1 + .2), (1.5, 1.6, .07), "bardeaux", rot=(25, -18, 0), lod=1, sale=.6)
    pile_gravats(o, rnd, 1.9, cy + .8, .9, 26, ("voiture_creme", "brique", "bois_sombre", "bardeaux"), hmax=.6)
    pile_gravats(o, rnd, 1.6, cy + ly / 2 + .1, .4, 10, ("voiture_creme", "bois_vieilli"), hmax=.3)
    # bardage et détails
    for k in range(6):
        o.boite((0, yf - ep / 2 - .004, z0 + .25 + k * .5), (lx - .02, .008, .025), "blanc_ecaille", lod=0)
    o.boite((-lx / 2 - .05, yf + .3, z0 + 1.2), (.04, .04, 2.4), "acier_sombre", lod=0)  # gouttière
    o.boite((-1.4, cy + .7, z1 + .05 + H + 1.0), (.3, .3, 1.1), "brique", lod=1, sale=.5)
    # perron
    o.boite((-.6, yf - .3, .06), (1.1, .55, .12), "beton", lod=1)
    o.boite((-.6, yf - .62, .03), (1.1, .2, .06), "beton", lod=0)
    # boîte aux lettres renversée
    o.boite((-2.3, yf - .35, .35), (.05, .05, .7), "bois_vieilli", rot=(0, 30, 0), lod=0)
    o.boite((-2.05, yf - .35, .1), (.22, .38, .16), "panneau_bleu" if "panneau_bleu" in PALETTE else "bidon_bleu", rot=(0, 0, 15), lod=0)
    for i in range(10):
        o.boite((rnd.uniform(-lx / 2, lx / 2), rnd.choice((yf - .15, cy + ly / 2 + .15)), .2), (rnd.uniform(.2, .4), .12, rnd.uniform(.2, .55)),
                rnd.choice(("vegetation", "mousse", "herbe_seche")), lod=0, sale=.1)
    o.socket("SOCKET_Porte", (-.6, yf - 1.0, 0))
    o.socket("SOCKET_Travail_01", (1.4, yf - 1.0, 0))
    o.socket("SOCKET_Travail_02", (2.0, cy + ly / 2 + 1.0, 0))
    return o


def cabane(nom, rnd):
    o = Objet(nom, (1, 1), "cabane de jardin en ruine : outils")
    lx, ly, H = 1.5, 1.3, 1.2
    o.boite((0, 0, .04), (lx + .1, ly + .1, .08), "beton", lod=2)
    for sx in (-1, 1):
        o.boite((sx * lx / 2, 0, .08 + H / 2), (.06, ly, H), "bois_vieilli", lod=2)
    o.boite((0, ly / 2, .08 + H / 2), (lx, .06, H), "bois_vieilli", lod=2)
    o.boite((-.35, -ly / 2, .08 + H / 2), (.8, .06, H), "bois_vieilli", lod=2)
    o.boite((.4, -ly / 2 - .25, .1), (.7, 1.0, .05), "bois_vieilli", rot=(0, 0, -18), lod=1)   # porte tombée
    o.boite((.4, -ly / 2 + .02, .08 + H / 2), (.7, .02, H), "interieur_sombre", lod=1)
    o.boite((0, 0, H + .2), (lx + .3, ly + .3, .04), "tole_rouillee", rot=(-12, 0, 0), lod=2, sale=.6)
    o.boite((.6, .3, H + .15), (.6, .6, .045), "tole_galva", rot=(-10, 8, 20), lod=1)
    for k in range(4):
        o.boite((-lx / 2 - .035, -.4 + k * .27, .08 + H / 2), (.01, .02, H), "bois_sombre", lod=0)
    o.boite((-.3, .4, .5), (.03, .03, .8), "bois_manche", rot=(0, 20, 0), lod=0)   # râteau
    o.boite((-.17, .4, .9), (.25, .04, .03), "acier", rot=(0, 20, 0), lod=0)
    o.socket("SOCKET_Porte", (.4, -ly / 2 - 1.0, 0))
    return o


# ---------------------------------------------------------------------------
# Clôtures (un segment = 1 case = 2 m, le long de X)
# ---------------------------------------------------------------------------
def cloture_bois(nom, cassee, rnd):
    o = Objet(nom, (1, 1), "clôture de jardin, segment d'une case")
    Lg, H = 2.0, .75
    for x in (-Lg / 2 + .05, Lg / 2 - .05):
        o.boite((x, 0, H / 2 + .05), (.09, .09, H + .1), "bois_vieilli", lod=2)
    for z in (.22, .58):
        if cassee and z > .5:
            o.boite((-.45, 0, z), (.95, .05, .07), "blanc_ecaille", lod=2)
            o.boite((.6, -.1, z - .3), (.8, .05, .07), "blanc_ecaille", rot=(0, 28, 10), lod=1)
        else:
            o.boite((0, .03, z), (Lg, .05, .07), "blanc_ecaille", lod=2)
    for i in range(10):
        x = -Lg / 2 + .15 + i * (Lg - .3) / 9
        if cassee and i in (6, 7):
            if i == 6:
                o.boite((x + .1, -.35, .04), (.09, H, .025), "blanc_ecaille", rot=(0, 0, 70), lod=0)
            continue
        tilt = rnd.uniform(-10, 10) if cassee and i > 4 else rnd.uniform(-2, 2)
        h = H - (rnd.uniform(.1, .25) if cassee and i == 8 else 0)
        o.boite((x, -.035, h / 2), (.09, .025, h), "blanc_ecaille", rot=(0, tilt, 0), lod=1 if i % 2 == 0 else 0, effile=(.5, 1))
    o.boite((0, -.15, .15), (.9, .1, .3), "herbe_seche", lod=0, sale=.1)
    return o


def cloture_grillage(nom, cassee, rnd):
    o = Objet(nom, (1, 1), "grillage sur poteaux, segment d'une case")
    Lg, H = 2.0, 1.4
    for x in (-Lg / 2 + .03, Lg / 2 - .03):
        o.cyl((x, 0, H / 2), .035, H, "acier", seg=6, lod=2)
    o.cyl((0, 0, H - .02), .025, Lg, "acier", rot=(0, 90, 0), seg=6, lod=1)
    if cassee:
        o.boite((-.5, 0, H / 2), (.95, .015, H - .05), "grillage", lod=2)
        o.boite((.45, -.25, .35), (.95, .015, .8), "grillage", rot=(-55, 0, 8), lod=2)
        o.boite((.45, -.48, .05), (.9, .3, .02), "grillage", rot=(0, 0, 8), lod=0)
    else:
        o.boite((0, 0, H / 2), (Lg - .06, .015, H - .05), "grillage", lod=2)
    # fils de fer barbelé au sommet
    for k in range(2):
        o.boite((0, 0, H + .08 + k * .08), (Lg, .01, .01), "acier_sombre", lod=0)
    o.boite((-.3, .02, H * .6), (.5, .02, .3), "plastique_noir", rot=(0, 10, 0), lod=0)   # sac pris dans le grillage
    return o


# ---------------------------------------------------------------------------
# Barricades (segment d'une case)
# ---------------------------------------------------------------------------
def barricade_planches(nom, rnd):
    o = Objet(nom, (1, 1), "barricade de planches clouées sur poteaux")
    for x in (-.85, 0, .85):
        o.boite((x, .05, .7), (.12, .12, 1.4), "rondin", lod=2)
    for k in range(6):
        ang = rnd.uniform(-9, 9)
        c = rnd.choice(("bois_vieilli", "bois_neuf", "bois_vieilli", "contreplaque"))
        o.boite((rnd.uniform(-.06, .06), -.03, .18 + k * .21), (2.0 + rnd.uniform(-.15, .1), .04, .17), c, rot=(0, ang, 0), lod=2 if k % 2 == 0 else 1)
    for s in (-1, 1):
        o.boite((s * .4, -.07, .7), (1.15, .045, .14), "bois_sombre", rot=(0, s * 48, 0), lod=1)
    for i in range(10):
        o.boite((rnd.uniform(-.9, .9), -.065, rnd.uniform(.2, 1.3)), (.02, .02, .02), "acier", lod=0)
    o.boite((.6, -.2, .12), (.5, .3, .24), "sac_sable", lod=1, effile=(.85, .8))
    return o


def barricade_sacs(nom, rnd):
    o = Objet(nom, (1, 1), "mur de sacs de sable, hauteur d'un niveau")
    for rang in range(4):
        n = 5 - (rang % 2)
        for i in range(n):
            x = -.8 + i * .4 + (.2 if rang % 2 else 0)
            o.boite((x + rnd.uniform(-.02, .02), rnd.uniform(-.02, .02), .1 + rang * .19), (.4, .3, .2),
                    rnd.choice(("sac_sable", "sac_sable", "toile_beige", "toile_sac")), effile=(.88, .85), rot=(0, 0, rnd.uniform(-5, 5)),
                    lod=2 if rang < 3 else 1, sale=.35)
    o.boite((.7, -.25, .1), (.38, .28, .18), "sac_sable", rot=(0, 0, 30), lod=0, effile=(.85, .85))
    return o


def cheval_de_frise(nom, rnd):
    o = Objet(nom, (1, 1), "cheval de frise : ralentit et blesse les zombies")
    o.cyl((0, 0, .45), .07, 2.0, "rondin", rot=(0, 90, 0), seg=6, lod=2)
    for i, x in enumerate((-.75, -.25, .25, .75)):
        a = 45 if i % 2 == 0 else -45
        o.cyl((x, 0, .45), .045, 1.25, "bois_vieilli", rot=(a, 0, 0), seg=6, rayon_haut=.015, lod=2)
        o.cyl((x + .02, 0, .45), .045, 1.25, "bois_vieilli", rot=(-a, 0, 0), seg=6, rayon_haut=.015, lod=1)
    for x in (-.5, .5):
        o.boite((x, 0, .45), (.04, .3, .04), "acier_sombre", rot=(45, 0, 0), lod=0)   # fil de fer
    return o


def barricade_palettes(nom, rnd):
    o = Objet(nom, (1, 1), "palettes dressées et tôle : barricade d'ère 1")
    for i, x in enumerate((-.62, 0, .62)):
        r = rnd.uniform(-6, 6)
        for j in range(5):
            o.boite((x, 0, .1 + j * .22), (.6, .025, .12), "bois_vieilli", rot=(0, r, 0), lod=2 if j % 2 == 0 else 1)
        for xx in (-.25, .25):
            o.boite((x + xx, .05, .55), (.08, .08, 1.1), "bois_sombre", rot=(0, r, 0), lod=1)
    o.boite((.1, -.06, .7), (1.2, .02, .7), "tole_rouillee", rot=(0, -6, 0), lod=1, sale=.6)
    for s in (-1, 1):
        o.boite((s * .95, .45, .45), (.07, 1.1, .07), "rondin", rot=(40, 0, 0), lod=1)   # étais
    return o


# ---------------------------------------------------------------------------
# Panneaux et mobilier urbain
# ---------------------------------------------------------------------------
def panneau_stop(nom, rnd):
    o = Objet(nom, (1, 1), "panneau stop tordu")
    o.base = Matrix.Rotation(math.radians(12), 4, "Y")
    o.cyl((0, 0, .8), .03, 1.6, "acier", seg=6, lod=2)
    o.cyl((0, -.04, 1.55), .3, .02, "panneau_blanc", rot=(90, 0, 22.5), seg=8, lod=2)
    o.cyl((0, -.055, 1.55), .26, .02, "panneau_rouge", rot=(90, 0, 22.5), seg=8, lod=2)
    for k, x in enumerate((-.13, -.04, .05, .14)):   # lettres simplifiées
        o.boite((x, -.068, 1.55), (.06, .005, .12), "panneau_blanc", lod=0)
    o.boite((.08, -.07, 1.62), (.1, .004, .06), "rouille", lod=0)
    o.base = Matrix.Identity(4)
    o.boite((0, 0, .02), (.3, .3, .04), "beton", lod=1)
    return o


def panneau_limite(nom, rnd):
    o = Objet(nom, (1, 1), "limite de vitesse criblée")
    o.base = Matrix.Rotation(math.radians(-8), 4, "X")
    o.boite((0, 0, .8), (.06, .06, 1.6), "acier", lod=2)
    o.boite((0, -.04, 1.45), (.45, .02, .58), "panneau_blanc", lod=2)
    o.boite((0, -.052, 1.45), (.41, .005, .54), "panneau_blanc", lod=0)
    o.boite((0, -.056, 1.6), (.3, .004, .05), "plastique_noir", lod=0)
    o.boite((-.06, -.056, 1.4), (.08, .004, .18), "plastique_noir", lod=0)
    o.boite((.07, -.056, 1.4), (.08, .004, .18), "plastique_noir", lod=0)
    for _ in range(5):
        o.boite((rnd.uniform(-.18, .18), -.058, rnd.uniform(1.22, 1.68)), (.025, .004, .025), "interieur_sombre", lod=0)
    o.base = Matrix.Identity(4)
    return o


def panneau_direction(nom, rnd):
    o = Objet(nom, (2, 1), "grand panneau de direction vert, à moitié tombé")
    for x in (-1.1, 1.1):
        o.boite((x, 0, 1.0), (.1, .1, 2.0), "acier", lod=2)
    o.boite((-.1, -.07, 1.75), (2.4, .05, .8), "panneau_vert", rot=(0, -9, 0), lod=2)
    o.boite((-.1, -.1, 1.75), (2.25, .005, .68), "panneau_blanc", rot=(0, -9, 0), lod=0)
    o.boite((-.1, -.104, 1.75), (2.17, .005, .6), "panneau_vert", rot=(0, -9, 0), lod=0)
    for k, w in enumerate((.5, .3, .6)):          # écriture simplifiée
        o.boite((-.8 + k * .7, -.11, 1.85 - .02 * k), (w, .004, .1), "panneau_blanc", rot=(0, -9, 0), lod=0)
    o.boite((.5, -.11, 1.58), (.2, .004, .2), "panneau_blanc", rot=(0, -9, 45), lod=0)   # flèche
    o.boite((.7, -.11, 1.4), (.25, .004, .14), "rouille", rot=(0, -9, 0), lod=0)
    return o


def panneau_aide(nom, rnd):
    """Contreplaqué peint « AIDE » appuyé sur deux piquets : appel des survivants."""
    o = Objet(nom, (1, 1), "contreplaqué peint AIDE : signal de sauvetage")
    for x in (-.7, .7):
        o.boite((x, .12, .65), (.07, .07, 1.3), "bois_vieilli", rot=(-10, 0, 0), lod=2)
    o.boite((0, 0, .75), (1.7, .03, .9), "contreplaque", rot=(-12, 0, 0), lod=2, sale=.4)
    o.base = Matrix.Translation(Vector((0, -.02, .75))) @ _rot((-12, 0, 0)) @ Matrix.Translation(Vector((0, -.02, 0)))
    t, ep, c = .07, .006, "peinture_rouge"
    # A
    o.boite((-.62, 0, 0), (t, ep, .55), c, rot=(0, 14, 0), lod=0)
    o.boite((-.48, 0, 0), (t, ep, .55), c, rot=(0, -14, 0), lod=0)
    o.boite((-.55, 0, -.04), (.15, ep, t), c, lod=0)
    o.boite((-.55, 0, .25), (.08, ep, t), c, lod=0)
    # I
    o.boite((-.25, 0, 0), (t, ep, .55), c, lod=0)
    # D
    o.boite((-.02, 0, 0), (t, ep, .55), c, lod=0)
    o.boite((.07, 0, .24), (.16, ep, t), c, lod=0)
    o.boite((.07, 0, -.24), (.16, ep, t), c, lod=0)
    o.boite((.16, 0, 0), (t, ep, .42), c, lod=0)
    # E
    o.boite((.36, 0, 0), (t, ep, .55), c, lod=0)
    for z in (.24, 0, -.24):
        o.boite((.46, 0, z), (.2, ep, t), c, lod=0)
    o.base = Matrix.Identity(4)
    o.boite((.6, -.3, .06), (.12, .12, .12), "peinture_rouge", lod=0)   # pot de peinture
    return o


def lampadaire(nom, rnd):
    o = Objet(nom, (1, 1), "lampadaire plié par une voiture")
    o.cyl((0, 0, .5), .05, 1.0, "acier_sombre", seg=6, lod=2)
    o.cyl((.4, 0, 1.6), .045, 1.4, "acier_sombre", rot=(0, 35, 0), seg=6, lod=2)
    o.boite((1.0, 0, 2.05), (.5, .12, .08), "acier_sombre", rot=(0, 20, 0), lod=1)
    o.boite((1.15, 0, 1.98), (.3, .1, .03), "vitre_sale", rot=(0, 20, 0), lod=0)
    o.cyl((0, 0, .05), .12, .1, "beton", seg=8, lod=1)
    return o


def poteau_electrique(nom, rnd):
    o = Objet(nom, (1, 1), "poteau électrique en bois, câbles coupés")
    o.cyl((0, 0, 1.6), .08, 3.2, "rondin", seg=8, lod=2, rayon_haut=.065)
    o.boite((0, 0, 2.95), (1.3, .1, .1), "bois_sombre", lod=2)
    for x in (-.55, 0, .55):
        o.cyl((x, 0, 3.05), .03, .1, "panneau_blanc", seg=6, lod=0)
    o.cyl((.2, -.1, 2.55), .12, .35, "acier", seg=8, lod=1)   # transformateur
    o.boite((-.55, .6, 1.6), (.012, 1.3, .012), "plastique_noir", rot=(55, 0, 0), lod=0)   # câble qui pend
    o.boite((.55, -.7, 2.1), (.012, 1.6, .012), "plastique_noir", rot=(-40, 0, 0), lod=0)
    for k in range(4):
        o.boite((0, -.085, .6 + k * .4), (.12, .015, .14), rnd.choice(("panneau_blanc", "panneau_jaune", "contreplaque")), lod=0)  # affiches
    return o


# ---------------------------------------------------------------------------
# Catalogue
# ---------------------------------------------------------------------------
def catalogue():
    rnd = random.Random(1713)
    return [
        ("Vehicules", [
            voiture("SM_Epave_Berline_A", "voiture_bleue", "capot", rnd),
            voiture("SM_Epave_Berline_B", "voiture_verte", "portiere", rnd),
            voiture("SM_Epave_Berline_Brulee", "voiture_rouge", "brulee", rnd),
            pickup("SM_Epave_Pickup", "voiture_rouge", rnd),
            camionnette("SM_Epave_Camionnette", rnd),
        ]),
        ("Debris", [
            gravats("SM_Debris_Gravats_Brique", rnd, ("brique", "brique", "pierre", "beton", "bois_sombre")),
            gravats("SM_Debris_Gravats_Beton", rnd, ("beton", "beton", "pierre", "acier_sombre", "bois_vieilli")),
            pneus("SM_Debris_Pneus", rnd),
            bidons("SM_Debris_Bidons", rnd),
            poubelles("SM_Debris_Poubelles", rnd),
            palettes("SM_Debris_Palettes", rnd),
            debris_tole("SM_Debris_Toles", rnd),
        ]),
        ("Maisons", [
            maison_a("SM_Maison_Pavillon_Ruine", rnd),
            maison_b("SM_Maison_Etage_Ruine", rnd),
            cabane("SM_Cabane_Jardin_Ruine", rnd),
        ]),
        ("Clotures", [
            cloture_bois("SM_Cloture_Bois", False, rnd),
            cloture_bois("SM_Cloture_Bois_Cassee", True, rnd),
            cloture_grillage("SM_Cloture_Grillage", False, rnd),
            cloture_grillage("SM_Cloture_Grillage_Cassee", True, rnd),
        ]),
        ("Barricades", [
            barricade_planches("SM_Barricade_Planches", rnd),
            barricade_sacs("SM_Barricade_SacsDeSable", rnd),
            cheval_de_frise("SM_Barricade_ChevalDeFrise", rnd),
            barricade_palettes("SM_Barricade_Palettes", rnd),
        ]),
        ("Panneaux", [
            panneau_stop("SM_Panneau_Stop", rnd),
            panneau_limite("SM_Panneau_Limite", rnd),
            panneau_direction("SM_Panneau_Direction", rnd),
            panneau_aide("SM_Panneau_Aide", rnd),
            lampadaire("SM_Lampadaire_Casse", rnd),
            poteau_electrique("SM_Poteau_Electrique", rnd),
        ]),
    ]


# ---------------------------------------------------------------------------
# Construction, export, aperçu
# ---------------------------------------------------------------------------
def vider_scene():
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    for me in list(bpy.data.meshes):
        bpy.data.meshes.remove(me)


def nb_tri(me):
    return sum(len(p.vertices) - 2 for p in me.polygons)


def hauteur_max(o):
    h = 0.3
    for p in o.pieces:
        h = max(h, p.matrice.translation.z)
    return h


def construire(o, mat, collection):
    h = hauteur_max(o) + .3
    objets = []
    for niveau in (0, 1, 2):
        me = o.mesh(niveau, mat, h)
        ob = bpy.data.objects.new("%s_LOD%d" % (o.nom, niveau), me)
        collection.objects.link(ob)
        objets.append(ob)
    for nom, pos in o.sockets:
        e = bpy.data.objects.new(nom, None)
        e.empty_display_type = "PLAIN_AXES"
        e.empty_display_size = .2
        e.location = pos
        e.parent = objets[0]
        collection.objects.link(e)
        objets.append(e)
    return objets


def exporter(objets, chemin):
    bpy.ops.object.select_all(action="DESELECT")
    for ob in objets:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = objets[0]
    bpy.ops.export_scene.fbx(
        filepath=chemin, use_selection=True, object_types={"MESH", "EMPTY"},
        axis_forward="-Z", axis_up="Y", apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
        bake_space_transform=False, mesh_smooth_type="FACE", use_mesh_modifiers=True,
        add_leaf_bones=False, bake_anim=False, colors_type="SRGB", path_mode="AUTO")


def apercu(groupes, mat, dossier):
    """Planche de contrôle : tous les objets rangés par famille, caméra de jeu."""
    sc = bpy.context.scene
    for c in bpy.data.collections:          # les objets exportés restent à l'origine : on les cache
        for ob in c.objects:
            ob.hide_render = True
    col = bpy.data.collections.new("Apercu")
    sc.collection.children.link(col)
    y = 0.0
    xmax = 0.0
    for _, objs in groupes:
        x = 0.0
        prof = 0.0
        for o in objs:
            w, d = o.emprise[0] * CELL, o.emprise[1] * CELL
            me = o.mesh(0, mat, hauteur_max(o) + .3)
            ob = bpy.data.objects.new(o.nom + "_apercu", me)
            ob.location = (x + w / 2, -y - d / 2, 0)
            col.objects.link(ob)
            x += w + .8
            prof = max(prof, d + hauteur_max(o) * .9)
        xmax = max(xmax, x)
        y += prof + 1.0
    # sol
    bpy.ops.mesh.primitive_plane_add(size=1, location=(xmax / 2, -y / 2, -0.001))
    sol = bpy.context.active_object
    sol.scale = (60, y + 20, 1)
    msol = bpy.data.materials.new("Sol")
    msol.use_nodes = True
    msol.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.11, 0.1, 0.07, 1)
    sol.data.materials.append(msol)
    # caméra orthographique en vue de jeu
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = max(xmax * 1.05, y * 1.15)
    sc.collection.objects.link(cam)
    cible = Vector((xmax / 2, -y / 2 + 1.5, 0))
    direction = Vector((0, 1, 0)).normalized()
    cam.location = cible - direction * 30 * math.cos(math.radians(50)) + Vector((0, 0, 30 * math.sin(math.radians(50))))
    cam.rotation_euler = (cible - cam.location).to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam
    sun = bpy.data.objects.new("Soleil", bpy.data.lights.new("Soleil", "SUN"))
    sun.data.energy = 5.0
    sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(-35))
    sc.collection.objects.link(sun)
    sc.render.engine = "CYCLES"   # Workbench demande un GPU ; Cycles tourne sur le processeur
    sc.cycles.device = "CPU"
    sc.cycles.samples = 24
    sc.cycles.use_denoising = False
    w = bpy.data.worlds.new("Ciel")
    w.color = (0.55, 0.56, 0.6)
    sc.world = w
    sc.render.resolution_x, sc.render.resolution_y = 1800, 1100
    sc.render.film_transparent = False
    sc.render.filepath = os.path.join(dossier, "apercu_decor_ruines.png")
    bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    sortie, rendu = None, True
    i = 0
    while i < len(argv):
        if argv[i] == "--sortie":
            sortie = os.path.abspath(argv[i + 1])
            i += 1
        elif argv[i] == "--sans-rendu":
            rendu = False
        i += 1
    vider_scene()
    if sortie:
        os.makedirs(sortie, exist_ok=True)
    img = creer_palette(sortie)
    mat = creer_materiau(img)
    groupes = catalogue()
    bilan = ["After City · décor urbain abandonné · triangles par LOD, emprise en cases (1 case = 2 m)", ""]
    for famille, objs in groupes:
        bilan.append("[%s]" % famille)
        for o in objs:
            colc = bpy.data.collections.new(o.nom)
            bpy.context.scene.collection.children.link(colc)
            obs = construire(o, mat, colc)
            tri = [nb_tri(ob.data) for ob in obs[:3]]
            bilan.append("%-30s %d x %d cases  hauteur %.2f m  triangles LOD0/1/2 = %d/%d/%d  accroches = %s  · %s" % (
                o.nom, o.emprise[0], o.emprise[1], max(v.co.z for v in obs[0].data.vertices), tri[0], tri[1], tri[2],
                ", ".join(n for n, _ in o.sockets) or "aucune", o.note))
            if sortie:
                exporter(obs, os.path.join(sortie, o.nom + ".fbx"))
        bilan.append("")
    if sortie:
        with open(os.path.join(sortie, "bilan.txt"), "w", encoding="utf-8") as f:
            f.write("\n".join(bilan) + "\n")
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(sortie, "AfterCity_Decor_Ruines.blend"))
        if rendu:
            apercu(groupes, mat, sortie)
    print("\n".join(bilan))


if __name__ == "__main__":
    main()
