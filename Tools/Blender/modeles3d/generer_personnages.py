"""
After City — génération des personnages (survivants et zombies) dans Blender.

Construit, à partir des fiches 01_survivants.md et 02_zombies.md :
  - la palette T_Palette_256.png et le matériau MAT_Palette_Personnages ;
  - le squelette commun RIG_Humain (22 os, compatible Humanoid dans Unity) ;
  - 6 survivants (un par rôle, ère 1) et 5 zombies, avec LOD0/LOD1/LOD2 ;
  - les outils en main (objets séparés accrochés à SOCKET_MainDroite) ;
  - des animations de base (repos, marche, travail, zombies) ;
  - un FBX par personnage, un .blend de travail et des rendus d'aperçu.

Utilisation (ligne de commande, Blender 4.2 ou plus récent) :
  blender --background --python generer_personnages.py -- --sortie CHEMIN [--sans-rendu]
Ou depuis Blender : onglet Scripting, ouvrir ce fichier, « Run Script »
(les personnages sont créés dans la scène ouverte, sans export).

Conventions (00_conventions.md) : 1 unité = 1 m, survivant = 1,00 m, le personnage
regarde vers -Y, origine entre les pieds, export FBX Forward -Z / Up Y.
Dans Unity : Rig > Animation Type = Humanoid, puis Configure > Pose > Enforce T-Pose
(le modèle est construit bras le long du corps).
"""

import math
import os
import random
import sys

import bpy  # noqa: I001 (bpy avant bmesh : obligatoire hors de Blender)
import bmesh
import mathutils
from mathutils import Matrix, Vector

# ---------------------------------------------------------------------------
# Palette (palette.md) : nom -> (ligne, colonne, hex)
# ---------------------------------------------------------------------------
PALETTE = {
    # Ligne 0 : peaux et cheveux
    "peau_claire": (0, 0, "E3BF9C"), "peau_doree": (0, 1, "C99A6E"),
    "peau_brune": (0, 2, "8D5A3B"), "peau_foncee": (0, 3, "5C3A26"),
    "cheveux_noirs": (0, 4, "2A2420"), "cheveux_chatains": (0, 5, "5A3E2B"),
    "cheveux_paille": (0, 6, "C2A15A"), "cheveux_gris": (0, 7, "9A958C"),
    "barbe": (0, 8, "3A2E26"), "yeux": (0, 9, "1E1B18"),
    # Ligne 1 : tissus civils
    "denim": (1, 0, "45607A"), "denim_delave": (1, 1, "7E93A8"),
    "flanelle_rouge": (1, 2, "7E3A30"), "flanelle_verte": (1, 3, "4F6B45"),
    "tshirt_creme": (1, 4, "BFB59C"), "sweat_gris": (1, 5, "7A7A74"),
    "kaki": (1, 6, "8C8158"), "cuir": (1, 7, "6B4A30"),
    "bleu_travail": (1, 8, "3A5068"), "orange_hv": (1, 9, "B07434"),
    "toile_beige": (1, 10, "B5A57A"), "semelle": (1, 11, "2E2925"),
    # Ligne 2 : équipement de rôle
    "olive": (2, 0, "5B6340"), "sable": (2, 1, "A8996B"),
    "noir_tactique": (2, 2, "2E3030"), "bleu_police": (2, 3, "2B3546"),
    "brun_chasse": (2, 4, "6E5435"), "camo": (2, 5, "4E5A36"),
    "orange_chasse": (2, 6, "A85A30"), "blanc_casse": (2, 7, "CFC8B6"),
    "sarcelle": (2, 8, "3E8C84"), "bandana": (2, 9, "7E2F2A"),
    "adhesif": (2, 10, "9EA19C"),
    # Ligne 3 : métal, bois, objets
    "acier": (3, 0, "8A8F92"), "acier_sombre": (3, 1, "45494B"),
    "rouille": (3, 2, "8B4F2C"), "bois_manche": (3, 3, "9C7448"),
    "bois_sombre": (3, 4, "5C4632"), "plastique_noir": (3, 5, "26282A"),
    "plastique_jaune": (3, 6, "A88E3E"), "toile_sac": (3, 7, "5F6B5A"),
    "cuir_clair": (3, 8, "8A6A48"), "metal_gourde": (3, 9, "7C8478"),
    "bandage": (3, 10, "C8BFA6"), "terre_visage": (3, 11, "5A4636"),
    "couleur_colonie": (3, 15, "A8432F"),
    # Ligne 4 : zombies
    "z_peau_recente": (4, 0, "8E9A82"), "z_peau_livide": (4, 1, "A9A594"),
    "z_peau_bleuie": (4, 2, "7D8790"), "z_peau_pourrie": (4, 3, "6E6250"),
    "z_chair": (4, 4, "5E3B34"), "z_sang_seche": (4, 5, "4A1E1A"),
    "z_sang_frais": (4, 6, "6E1E1A"), "z_os": (4, 7, "CFC7AE"),
    "z_cheveux": (4, 8, "3F3A33"), "z_yeux": (4, 9, "C9C6B3"),
    # Lignes 8-9 : bâtiments (utiles pour quelques accessoires)
    "bache_bleue": (8, 8, "3F6E8F"), "tole_rouillee": (8, 7, "8B5A3A"),
    "paille": (9, 3, "C9B26A"),
    # Ligne 6 : animaux (04_animaux.md)
    "cerf_pelage": (6, 0, "7A5A3C"), "cerf_ventre": (6, 1, "B9A27E"), "sabot": (6, 2, "2B2420"),
    "bois_cerf": (6, 3, "B7A889"), "sanglier_poil": (6, 4, "4A3B2E"), "sanglier_crete": (6, 5, "2E2620"),
    "defense": (6, 6, "D8CFB4"), "groin": (6, 7, "8A6658"), "cheval_bai": (6, 8, "6B4226"),
    "crin_noir": (6, 9, "231E1B"), "vache_blanc": (6, 10, "D2CCBE"), "vache_noir": (6, 11, "2A2724"),
    "mufle_rose": (6, 12, "B98A80"), "liste_blanche": (6, 13, "E0DACB"),
}
# Ligne 5 : tissus de zombies = ligne 1 désaturée de 45 % et assombrie de 20 %
for _nom in list(PALETTE):
    _r, _c, _hex = PALETTE[_nom]
    if _r == 1:
        rgb = [int(_hex[i:i + 2], 16) / 255 for i in (0, 2, 4)]
        gris = sum(rgb) / 3
        rgb = [(v + (gris - v) * 0.45) * 0.8 for v in rgb]
        PALETTE["z_" + _nom] = (5, _c, "".join("%02X" % round(v * 255) for v in rgb))

# Pose de repos exportée en T (bras à l'horizontale) : demandé pour le type Humanoid d'Unity,
# dont « Enforce T-Pose » ne peut pas être lancé par script. Les animations restent identiques.
POSE_T = True

CASE = 16  # pixels par case, grille 16 x 16 sur 256 px


def uv_case(nom):
    ligne, col, _ = PALETTE[nom]
    return ((col + 0.5) / 16.0, 1.0 - (ligne + 0.5) / 16.0)


def creer_palette(dossier=None):
    img = bpy.data.images.get("T_Palette_256")
    if img is None:
        img = bpy.data.images.new("T_Palette_256", 256, 256, alpha=False)
    px = [0.5] * (256 * 256 * 4)
    for x in range(256 * 256):
        px[x * 4 + 3] = 1.0
    for ligne, col, hx in PALETTE.values():
        rgb = [int(hx[i:i + 2], 16) / 255 for i in (0, 2, 4)]
        for yy in range(CASE):
            y = 255 - (ligne * CASE + yy)  # les pixels Blender partent du bas
            for xx in range(CASE):
                i = (y * 256 + col * CASE + xx) * 4
                px[i:i + 3] = rgb
    img.pixels[:] = px
    if dossier:
        img.filepath_raw = os.path.join(dossier, "T_Palette_256.png")
        img.file_format = "PNG"
        img.save()
    return img


def creer_materiau(img):
    mat = bpy.data.materials.get("MAT_Palette_Personnages")
    if mat:
        return mat
    mat = bpy.data.materials.new("MAT_Palette_Personnages")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    tex.interpolation = "Closest"
    # Couleur finale = palette x occlusion (canal R) x crasse (canal G), comme le shader URP prévu
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
    cr.inputs[1].default_value, cr.inputs[2].default_value = -.55, 1.0
    fac = nt.nodes.new("ShaderNodeMath")
    fac.operation = "MULTIPLY"
    nt.links.new(ao.outputs[0], fac.inputs[0])
    nt.links.new(cr.outputs[0], fac.inputs[1])
    sc = nt.nodes.new("ShaderNodeVectorMath")
    sc.operation = "SCALE"
    nt.links.new(tex.outputs["Color"], sc.inputs[0])
    nt.links.new(fac.outputs[0], sc.inputs["Scale"])
    nt.links.new(sc.outputs[0], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.9
    return mat


# ---------------------------------------------------------------------------
# Proportions (01_survivants.md § 2, 02_zombies.md § 3)
# ---------------------------------------------------------------------------
CORPS_A = dict(head_h=.30, head_w=.26, head_d=.25, neck=.03, torso_h=.30,
               shoulder_w=.36, waist_w=.27, torso_d=.18, hip_w=.26, leg_h=.37,
               leg_t=.105, arm_len=.32, arm_t=.085, hand=.075, foot_l=.13,
               foot_w=.075, foot_h=.05, fist=1.0)
CORPS_B = dict(CORPS_A, shoulder_w=.32, waist_w=.25, hip_w=.27, arm_t=.078, leg_t=.10)


def proportions(base, **k):
    p = dict(base)
    p.update(k)
    return p


# ---------------------------------------------------------------------------
# Constructeur de maillage : toutes les pièces dans un seul bmesh,
# chaque pièce pondérée à 100 % sur un os (rendu net façon K&C).
# ---------------------------------------------------------------------------
OS = ["Hips", "Spine", "Chest", "Neck", "Head",
      "Shoulder_L", "UpperArm_L", "LowerArm_L", "Hand_L",
      "Shoulder_R", "UpperArm_R", "LowerArm_R", "Hand_R",
      "UpperLeg_L", "LowerLeg_L", "Foot_L",
      "UpperLeg_R", "LowerLeg_R", "Foot_R",
      "SOCKET_MainDroite", "SOCKET_MainGauche", "SOCKET_Dos",
      "SOCKET_Tete", "SOCKET_Torse", "SOCKET_Ceinture"]

# Emplacements d'équipement (01_survivants.md § 6) : emplacement -> os d'accroche
EMPLACEMENTS = {"Tete": "SOCKET_Tete", "Torse": "SOCKET_Torse", "Dos": "SOCKET_Dos",
                "Ceinture": "SOCKET_Ceinture", "MainDroite": "SOCKET_MainDroite",
                "MainGauche": "SOCKET_MainGauche"}


class Constructeur:
    def __init__(self, hauteur=1.0, salete=0.2, os_liste=None):
        self.os = os_liste or OS
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.verify()
        self.dv = self.bm.verts.layers.deform.verify()
        self.col = self.bm.verts.layers.float_color.new("Col")
        self.hauteur = hauteur
        self.salete = salete

    def _finir(self, avant, couleur, os_nom, mat):
        faces = [f for f in self.bm.faces if f not in avant]
        verts = {v for f in faces for v in f.verts}
        bmesh.ops.transform(self.bm, matrix=mat, verts=list(verts))
        u, v = uv_case(couleur)
        gi = self.os.index(os_nom)
        for f in faces:
            for l in f.loops:
                l[self.uv].uv = (u, v)
        for vert in verts:
            vert[self.dv].clear()
            vert[self.dv][gi] = 1.0
            # R : occlusion approchée (plus sombre vers le sol), G : saleté
            h = max(0.0, min(1.0, vert.co.z / self.hauteur))
            vert[self.col] = (0.55 + 0.45 * h, self.salete * (1.0 - h * 0.5), 0.0, 1.0)
        return faces

    def boite(self, centre, taille, couleur, os_nom, biseau=0.012, segments=1,
              effile=(1.0, 1.0), rot=(0, 0, 0), effile_bas=(1.0, 1.0)):
        """Boîte biseautée. effile = échelle x,y de la face du haut."""
        avant = set(self.bm.faces)
        res = bmesh.ops.create_cube(self.bm, size=1.0)
        vs = res["verts"]
        sx, sy, sz = taille
        for v in vs:
            fx, fy = effile if v.co.z > 0 else effile_bas
            v.co = Vector((v.co.x * sx * fx, v.co.y * sy * fy, v.co.z * sz))
        if min(taille) < 0.035 or max(taille) < 0.06:
            biseau = 0  # petites pièces : pas de biseau (budget de triangles)
        if biseau > 0:
            aretes = list({e for v in vs for e in v.link_edges})
            bmesh.ops.bevel(self.bm, geom=aretes + vs, offset=min(biseau, min(taille) * 0.45),
                            segments=segments, affect="EDGES", profile=0.5, clamp_overlap=True)
        m = Matrix.Translation(Vector(centre)) @ _rot(rot)
        return self._finir(avant, couleur, os_nom, m)

    def cylindre(self, centre, rayon, hauteur, couleur, os_nom, segments=8,
                 rayon_haut=None, rot=(0, 0, 0)):
        avant = set(self.bm.faces)
        bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=segments,
                              radius1=rayon, radius2=rayon if rayon_haut is None else rayon_haut,
                              depth=hauteur)
        m = Matrix.Translation(Vector(centre)) @ _rot(rot)
        return self._finir(avant, couleur, os_nom, m)

    def sphere(self, centre, rayon, couleur, os_nom, u=8, v=5, echelle=(1, 1, 1),
               demi=False, rot=(0, 0, 0)):
        avant = set(self.bm.faces)
        res = bmesh.ops.create_uvsphere(self.bm, u_segments=u, v_segments=v, radius=rayon)
        vs = res["verts"]
        if demi:
            bas = [x for x in vs if x.co.z < -1e-4]
            bmesh.ops.delete(self.bm, geom=bas, context="VERTS")
            vs = [x for x in vs if x.is_valid]
        for x in vs:
            x.co = Vector((x.co.x * echelle[0], x.co.y * echelle[1], x.co.z * echelle[2]))
        m = Matrix.Translation(Vector(centre)) @ _rot(rot)
        return self._finir(avant, couleur, os_nom, m)

    def vers_objet(self, nom, mat):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        me = bpy.data.meshes.new(nom)
        self.bm.to_mesh(me)
        self.bm.free()
        me.materials.append(mat)
        if me.color_attributes.get("Col"):
            me.color_attributes.active_color = me.color_attributes["Col"]
        for p in me.polygons:
            p.use_smooth = False
        ob = bpy.data.objects.new(nom, me)
        for n in self.os:
            ob.vertex_groups.new(name=n)
        return ob


def _rot(r):
    return (Matrix.Rotation(math.radians(r[2]), 4, "Z") @
            Matrix.Rotation(math.radians(r[1]), 4, "Y") @
            Matrix.Rotation(math.radians(r[0]), 4, "X"))


# ---------------------------------------------------------------------------
# Géométrie du corps
# ---------------------------------------------------------------------------
def reperes(p):
    """Hauteurs et positions clés du corps (en mètres)."""
    r = {}
    r["z_hanche"] = p["leg_h"]
    r["z_epaule_haut"] = p["leg_h"] + p["torso_h"]
    r["z_tete_bas"] = r["z_epaule_haut"] + p["neck"]
    r["z_tete_haut"] = r["z_tete_bas"] + p["head_h"]
    r["z_epaule"] = r["z_epaule_haut"] - 0.04
    r["x_bras"] = p["shoulder_w"] / 2 + p["arm_t"] / 2 - 0.025
    r["seg_bras"] = (p["arm_len"] - p["hand"]) / 2
    r["x_jambe"] = p["hip_w"] / 4 + 0.005
    r["seg_jambe"] = (p["leg_h"] - p["foot_h"]) / 2
    return r


def corps(c, p, t):
    """t = tenue (dict de couleurs et d'accessoires)."""
    r = reperes(p)
    zh, zt = r["z_hanche"], r["z_epaule_haut"]
    peau, haut, bas = t["peau"], t["haut"], t["bas"]
    # Bassin et torse (deux blocs : ventre sur Spine, poitrine sur Chest)
    c.boite((0, 0, zh), (p["hip_w"], p["torso_d"] * .92, .11), bas, "Hips", biseau=.025)
    c.boite((0, 0, zh + .085), (p["waist_w"], p["torso_d"] * .95, .12), haut, "Spine",
            biseau=.03, effile=(1.06, 1.0))
    hc = zt - (zh + .14)
    c.boite((0, 0, zh + .14 + hc / 2), (p["waist_w"] * 1.05, p["torso_d"], hc), haut, "Chest",
            biseau=.035, effile=(p["shoulder_w"] / (p["waist_w"] * 1.05), 1.0))
    if t.get("ceinture", True):
        c.boite((0, 0, zh + .035), (p["hip_w"] * 1.03, p["torso_d"] * .96, .03), "semelle", "Hips",
                biseau=.008)
        c.boite((0, -p["torso_d"] * .48, zh + .035), (.04, .012, .028), "acier", "Hips", biseau=0)
    if t.get("col", True):
        c.boite((0, .01, zt - .005), (p["shoulder_w"] * .45, p["torso_d"] * .7, .03), haut, "Chest",
                biseau=.01)
    # Cou et tête
    c.cylindre((0, 0, zt + p["neck"] / 2), .045, p["neck"] + .03, peau, "Neck", segments=6)
    tete(c, p, r, t)
    # Bras
    for cote, sx in (("L", 1), ("R", -1)):
        x = sx * r["x_bras"]
        ze = r["z_epaule"]
        sb = r["seg_bras"]
        manche_h = t.get("manches", "longues")
        coul_haut_bras = haut if manche_h != "aucune" else peau
        coul_avant_bras = haut if manche_h == "longues" else peau
        if t.get("bras_nu") == cote:
            coul_haut_bras = coul_avant_bras = peau
        c.sphere((x, 0, ze), p["arm_t"] * .62, coul_haut_bras, "Shoulder_" + cote, u=6, v=3,
                 echelle=(1, 1, .85))
        c.boite((x, 0, ze - sb / 2), (p["arm_t"], p["arm_t"] * 1.05, sb + .02), coul_haut_bras,
                "UpperArm_" + cote, biseau=.02, effile=(1.05, 1.05))
        c.boite((x, 0, ze - sb * 1.5), (p["arm_t"] * .92, p["arm_t"] * .95, sb + .01),
                coul_avant_bras, "LowerArm_" + cote, biseau=.02)
        if manche_h == "retroussees":
            c.boite((x, 0, ze - sb * 1.05), (p["arm_t"] * 1.12, p["arm_t"] * 1.12, .03), haut,
                    "LowerArm_" + cote, biseau=.01)
        # Main en moufle + pouce
        hz = ze - sb * 2 - p["hand"] / 2
        f = p.get("fist", 1.0)
        coul_main = t.get("gants", peau)
        c.boite((x, 0, hz), (p["hand"] * .8 * f, p["hand"] * f, p["hand"] * f), coul_main,
                "Hand_" + cote, biseau=.022)
        c.boite((x - sx * .0, -p["hand"] * .5 * f, hz + .01), (.025 * f, .03 * f, .035 * f),
                coul_main, "Hand_" + cote, biseau=.01)
        if t.get("griffes"):
            for k in (-1, 0, 1):
                c.boite((x + k * .018, -.01, hz - p["hand"] * .55 * f), (.012, .02, .04), "z_os",
                        "Hand_" + cote, biseau=0, effile=(.3, .3))
    # Jambes et pieds
    for cote, sx in (("L", 1), ("R", -1)):
        x = sx * r["x_jambe"]
        sj = r["seg_jambe"]
        c.boite((x, 0, zh - sj / 2), (p["leg_t"], p["leg_t"] * 1.05, sj + .03), bas,
                "UpperLeg_" + cote, biseau=.02, effile_bas=(.92, .92))
        c.boite((x, 0, p["foot_h"] + sj / 2), (p["leg_t"] * .92, p["leg_t"] * .95, sj + .02),
                t.get("bas_jambe", bas), "LowerLeg_" + cote, biseau=.02)
        c.boite((x, -(p["foot_l"] / 2 - p["leg_t"] / 2) + .005, p["foot_h"] / 2 + .003),
                (p["foot_w"], p["foot_l"], p["foot_h"]), t.get("chaussures", "semelle"),
                "Foot_" + cote, biseau=.018, effile=(.95, .85))
        if t.get("genouilleres"):
            c.boite((x, -p["leg_t"] * .5, p["foot_h"] + sj), (p["leg_t"] * .9, .03, .07),
                    t["genouilleres"], "LowerLeg_" + cote, biseau=.012)
    for nom in t.get("accessoires", []):
        ACCESSOIRES[nom](c, p, r, t)
    details(c, p, r, t)


DETAILS_SURVIVANT = ["bottes", "fermeture", "poche_poitrine", "terre_visage"]
# Retour de Sam (13 octobre, phase « chara design ») : zombies plus marqués, façon Walking Dead
DETAILS_ZOMBIE = ["orbites", "sang_bouche", "mains_sang", "morsure", "lambeaux_pantalon"]


def details(c, p, r, t):
    """Détails modelés en volume (retours de Sam : personnages plus détaillés, plus usés)."""
    zh, zt = r["z_hanche"], r["z_epaule_haut"]
    sj, sb = r["seg_jambe"], r["seg_bras"]
    liste = t.get("details", DETAILS_ZOMBIE if t.get("zombie") else DETAILS_SURVIVANT)
    haut, bas = t["haut"], t["bas"]
    for d in liste:
        if d == "bottes":
            for cote, sx in (("L", 1), ("R", -1)):
                c.boite((sx * r["x_jambe"], 0, p["foot_h"] + .035), (p["leg_t"] * 1.08, p["leg_t"] * 1.1, .07),
                        t.get("couleur_bottes", "cuir"), "LowerLeg_" + cote, biseau=.012)
        elif d == "fermeture":
            c.boite((0, -p["torso_d"] * .51, zh + p["torso_h"] * .5), (.012, .01, p["torso_h"] * .75),
                    "semelle", "Chest", biseau=0)
        elif d == "poche_poitrine":
            x = p["shoulder_w"] * .2
            c.boite((x, -p["torso_d"] * .52, zt - .09), (.06, .015, .055), haut, "Chest", biseau=0)
            c.boite((x, -p["torso_d"] * .535, zt - .065), (.064, .016, .016), "semelle", "Chest", biseau=0)
        elif d == "terre_visage":
            zc = r["z_tete_bas"] + p["head_h"] * .46
            c.boite((p["head_w"] * .3, -p["head_d"] / 2 - .001, zc + .085), (.04, .006, .022),
                    "terre_visage", "Head", biseau=0)
        elif d == "rapiece_genou":
            c.boite((r["x_jambe"], -p["leg_t"] * .53, p["foot_h"] + sj * .98), (p["leg_t"] * .7, .012, .07),
                    t.get("couleur_piece", "denim_delave"), "LowerLeg_L", biseau=0)
        elif d == "rapiece_coude":
            c.boite((-r["x_bras"], p["arm_t"] * .53, r["z_epaule"] - sb), (p["arm_t"] * .7, .012, .05),
                    t.get("couleur_piece", "kaki"), "UpperArm_R", biseau=0)
        elif d == "poches_cuisse":
            for cote, sx in (("L", 1), ("R", -1)):
                c.boite((sx * (r["x_jambe"] + p["leg_t"] * .5), 0, zh - sj * .55), (.016, .07, .08), bas,
                        "UpperLeg_" + cote, biseau=0)
                c.boite((sx * (r["x_jambe"] + p["leg_t"] * .52), 0, zh - sj * .55 + .045), (.018, .074, .016),
                        "semelle", "UpperLeg_" + cote, biseau=0)
        elif d == "bandage_bras":
            for k in range(3):
                c.boite((r["x_bras"], 0, r["z_epaule"] - sb * 1.35 - k * .028),
                        (p["arm_t"] * 1.06, p["arm_t"] * 1.08, .022), "bandage", "LowerArm_L", biseau=0,
                        rot=(0, 0, 8 * (k - 1)))
        elif d == "manche_dechiree":
            for k in range(3):
                c.boite((-r["x_bras"] + (k - 1) * .025, -.01, r["z_epaule"] - sb - .02), (.02, p["arm_t"] * .9, .04),
                        haut, "UpperArm_R", biseau=0, effile_bas=(.2, 1))
        elif d == "orbites":  # yeux enfoncés : cadre sombre autour de l'œil
            zc = r["z_tete_bas"] + p["head_h"] * .46
            for sx in (1, -1):
                c.boite((sx * p["head_w"] * .2, -p["head_d"] / 2 + .003, zc + .008), (.05, .01, .058),
                        "z_sang_seche", "Head", biseau=0)
        elif d == "sang_bouche":  # coulure du menton au col
            zc = r["z_tete_bas"] + p["head_h"] * .46
            yf = -p["head_d"] / 2 - .002
            c.boite((-.01, yf, zc - .1), (.045, .008, .05), "z_sang_frais", "Head", biseau=0,
                    effile_bas=(.4, 1))
            c.boite((.012, -p["torso_d"] * .515, zt - .03), (.035, .01, .07), "z_sang_frais", "Chest",
                    biseau=0, effile_bas=(.3, 1))
        elif d == "mains_sang":
            for cote, sx in (("L", 1), ("R", -1)):
                hz = r["z_epaule"] - sb * 2 - p["hand"] / 2
                f = p.get("fist", 1.0)
                c.boite((sx * r["x_bras"], -p["hand"] * f * .3, hz - p["hand"] * f * .2),
                        (p["hand"] * .86 * f, p["hand"] * .5 * f, p["hand"] * .62 * f), "z_sang_seche",
                        "Hand_" + cote, biseau=0)
        elif d == "morsure":  # plaie au cou et à l'avant-bras gauche
            c.boite((p["head_w"] * .18, -.035, zt + p["neck"] / 2 + .01), (.05, .03, .045), "z_chair",
                    "Neck", biseau=0, rot=(0, 0, 25))
            c.boite((r["x_bras"], -p["arm_t"] * .5, r["z_epaule"] - sb * 1.5), (p["arm_t"] * .6, .012, .05),
                    "z_sang_frais", "LowerArm_L", biseau=0)
        elif d == "lambeaux_pantalon":
            rnd = random.Random(t.get("graine", 1) + 17)
            for cote, sx in (("L", 1), ("R", -1)):
                for k in (-1, 1):
                    c.boite((sx * r["x_jambe"] + k * p["leg_t"] * .25, -p["leg_t"] * .5, p["foot_h"] + .02),
                            (.03, .01, .05 + rnd.random() * .03), bas, "LowerLeg_" + cote, biseau=0,
                            effile_bas=(.2, 1))
        elif d == "foulard":  # foulard noué au cou, pointe sur la poitrine
            c.boite((0, 0, zt + .01), (p["shoulder_w"] * .5, p["torso_d"] * .78, .045), t.get("couleur_foulard", "bandana"),
                    "Neck", biseau=.01)
            c.boite((0, -p["torso_d"] * .5, zt - .045), (.07, .014, .07), t.get("couleur_foulard", "bandana"), "Chest",
                    biseau=0, effile_bas=(.15, 1))
        elif d == "sang_vetement":  # sang séché d'un autre : ce survivant s'est battu
            c.boite((-p["shoulder_w"] * .18, -p["torso_d"] * .515, zh + p["torso_h"] * .35), (.06, .008, .08),
                    "z_sang_seche", "Spine", biseau=0, rot=(0, 20, 0))
            c.boite((-r["x_bras"], -p["arm_t"] * .5, r["z_epaule"] - sb * 1.6), (p["arm_t"] * .7, .01, .045),
                    "z_sang_seche", "LowerArm_R", biseau=0)
        elif d == "boue_jambes":
            for cote, sx in (("L", 1), ("R", -1)):
                c.boite((sx * r["x_jambe"], -p["leg_t"] * .49, p["foot_h"] + .09), (p["leg_t"] * .8, .01, .06),
                        "terre_visage", "LowerLeg_" + cote, biseau=0)
        elif d == "barbe_naissante":
            zc = r["z_tete_bas"] + p["head_h"] * .46
            c.boite((0, -p["head_d"] / 2 + .01, zc - .105), (p["head_w"] * .66, .025, .035), t["cheveux"], "Head",
                    biseau=0)


def tete(c, p, r, t):
    zb = r["z_tete_bas"]
    hh, hw, hd = p["head_h"], p["head_w"], p["head_d"]
    zc = zb + hh * .46
    c.boite((0, 0, zc), (hw, hd, hh * .92), t["peau"], "Head", biseau=.06, segments=2,
            effile=(.96, .96), effile_bas=(.9, .92))
    yf = -hd / 2
    oeil = "z_yeux" if t.get("zombie") else "yeux"
    for sx in (1, -1):
        c.boite((sx * hw * .2, yf + .002, zc + .01), (.032, .02, .04), oeil, "Head", biseau=.008)
        sourcil = t.get("sourcils", "barbe")
        c.boite((sx * hw * .2, yf - .002, zc + .045), (.055, .018, .016), sourcil, "Head",
                biseau=.004, rot=(0, sx * t.get("angle_sourcils", 8), 0))
    c.boite((0, yf - .012, zc - .02), (.04, .035, .055), t["peau"], "Head", biseau=.01,
            effile=(.6, .5))
    for sx in (1, -1):  # oreilles
        c.boite((sx * (hw / 2 + .008), .01, zc - .005), (.022, .045, .06), t["peau"], "Head", biseau=0,
                effile=(1, .8))
    if t.get("bouche_ouverte"):
        c.boite((0, yf + .004, zc - .07), (.07, .02, .04), "z_sang_seche", "Head", biseau=.006)
    coiffure = t.get("coiffure")
    if coiffure:
        COIFFURES[coiffure](c, p, r, t, zc)


# --- Coiffures ---------------------------------------------------------------
def _calotte(c, p, zc, couleur, epais=.025, bas=.0):
    hw, hd, hh = p["head_w"], p["head_d"], p["head_h"]
    c.boite((0, .012, zc + hh * .3 + bas / 2), (hw + epais, hd + epais * .6, hh * .45 + bas),
            couleur, "Head", biseau=.05, effile=(.92, .92))


def coif_court(c, p, r, t, zc):
    _calotte(c, p, zc, t["cheveux"])


def coif_queue(c, p, r, t, zc):
    _calotte(c, p, zc, t["cheveux"])
    c.boite((0, p["head_d"] / 2 + .03, zc - .02), (.06, .06, .14), t["cheveux"], "Head",
            biseau=.025, rot=(-15, 0, 0))


def coif_chignon(c, p, r, t, zc):
    _calotte(c, p, zc, t["cheveux"])
    c.sphere((0, p["head_d"] / 2 + .01, zc + .09), .055, t["cheveux"], "Head", u=8, v=5)


def coif_longs(c, p, r, t, zc):
    _calotte(c, p, zc, t["cheveux"])
    c.boite((0, p["head_d"] * .28, zc - .04), (p["head_w"] + .03, p["head_d"] * .55, .2),
            t["cheveux"], "Head", biseau=.03, effile_bas=(1.08, 1.0))


def coif_degarni(c, p, r, t, zc):
    hw, hd = p["head_w"], p["head_d"]
    for sx in (1, -1):
        c.boite((sx * hw * .48, .03, zc + .02), (.03, hd * .6, .09), t["cheveux"], "Head", biseau=.012)
    c.boite((0, hd * .48, zc + .02), (hw * .9, .03, .1), t["cheveux"], "Head", biseau=.012)


COIFFURES = {"court": coif_court, "queue": coif_queue, "chignon": coif_chignon,
             "longs": coif_longs, "degarni": coif_degarni}


# --- Accessoires (tenue fusionnée au corps) ---------------------------------
def acc_casquette(c, p, r, t):
    zc = r["z_tete_bas"] + p["head_h"] * .46
    col = t.get("couleur_chapeau", "bleu_travail")
    c.sphere((0, .01, zc + .07), p["head_w"] * .56, col, "Head", u=10, v=5, demi=True,
             echelle=(1, 1.02, .75))
    c.boite((0, -p["head_d"] / 2 - .05, zc + .075), (p["head_w"] * .75, .12, .015), col, "Head",
            biseau=.006, rot=(-8, 0, 0))


def acc_bonnet(c, p, r, t):
    zc = r["z_tete_bas"] + p["head_h"] * .46
    col = t.get("couleur_chapeau", "orange_chasse")
    c.sphere((0, .01, zc + .06), p["head_w"] * .58, col, "Head", u=10, v=5, demi=True,
             echelle=(1, 1.02, 1.0))
    c.cylindre((0, .01, zc + .055), p["head_w"] * .59, .04, col, "Head", segments=10)


def acc_bandana(c, p, r, t):
    zc = r["z_tete_bas"] + p["head_h"] * .46
    col = t.get("couleur_bandana", "bandana")
    c.boite((0, .005, zc + .07), (p["head_w"] + .02, p["head_d"] + .02, .07), col, "Head",
            biseau=.03)
    c.boite((0, p["head_d"] / 2 + .03, zc + .05), (.04, .05, .07), col, "Head", biseau=.01,
            rot=(30, 0, 0))


def acc_chapeau(c, p, r, t):
    zc = r["z_tete_bas"] + p["head_h"] * .46
    col = t.get("couleur_chapeau", "brun_chasse")
    c.cylindre((0, 0, zc + .1), p["head_w"] * .95, .015, col, "Head", segments=12)
    c.cylindre((0, 0, zc + .155), p["head_w"] * .5, .1, col, "Head", segments=10,
               rayon_haut=p["head_w"] * .44)
    c.cylindre((0, 0, zc + .12), p["head_w"] * .52, .025, "cuir", "Head", segments=10)


def acc_casque_mil(c, p, r, t):
    zc = r["z_tete_bas"] + p["head_h"] * .46
    col = t.get("couleur_chapeau", "olive")
    c.sphere((0, .01, zc + .045), p["head_w"] * .64, col, "Head", u=12, v=6, demi=True,
             echelle=(1, 1.05, .85))
    c.cylindre((0, .01, zc + .045), p["head_w"] * .66, .025, col, "Head", segments=12)
    c.boite((0, -p["head_d"] * .45, zc - .06), (p["head_w"] * .9, .015, .015), "semelle", "Head",
            biseau=0)


def acc_casque_chantier(c, p, r, t):
    zc = r["z_tete_bas"] + p["head_h"] * .46
    col = t.get("couleur_chapeau", "plastique_jaune")
    c.sphere((0, .0, zc + .07), p["head_w"] * .6, col, "Head", u=10, v=5, demi=True,
             echelle=(1, 1.08, .9))
    c.cylindre((0, -.02, zc + .07), p["head_w"] * .68, .015, col, "Head", segments=12)
    if t.get("fendu"):
        c.boite((.03, 0, zc + .15), (.012, .2, .05), "z_sang_seche", "Head", biseau=0)


def acc_capuche(c, p, r, t):
    zc = r["z_tete_bas"] + p["head_h"] * .46
    col = t.get("couleur_chapeau", t["haut"])
    c.sphere((0, .035, zc + .03), p["head_w"] * .64, col, "Head", u=10, v=6,
             echelle=(1, 1.05, 1.08))


def acc_barbe(c, p, r, t):
    zc = r["z_tete_bas"] + p["head_h"] * .46
    c.boite((0, -p["head_d"] / 2 + .02, zc - .085), (p["head_w"] * .85, .07, .09),
            t.get("couleur_barbe", "barbe"), "Head", biseau=.025, effile_bas=(.7, .9))


def acc_masque(c, p, r, t):
    zc = r["z_tete_bas"] + p["head_h"] * .46
    c.boite((0, -p["head_d"] / 2 + .01, zc - .06), (p["head_w"] * .82, .05, .09), "tshirt_creme",
            "Head", biseau=.02)


def acc_sac_dos(c, p, r, t):
    z = r["z_epaule_haut"] - p["torso_h"] * .42
    col = t.get("couleur_sac", "toile_sac")
    c.boite((0, p["torso_d"] / 2 + .05, z), (p["shoulder_w"] * .62, .1, p["torso_h"] * .6), col,
            "Chest", biseau=.03)
    c.boite((0, p["torso_d"] / 2 + .105, z - .04), (p["shoulder_w"] * .5, .03, .09), col, "Chest",
            biseau=.015)
    for sx in (1, -1):
        c.boite((sx * p["shoulder_w"] * .22, -p["torso_d"] * .5, z + .04), (.025, .012, .17),
                "cuir", "Chest", biseau=0)


def acc_gilet_mil(c, p, r, t):
    zt = r["z_epaule_haut"]
    col = t.get("couleur_gilet", "olive")
    h = p["torso_h"] * .78
    c.boite((0, 0, zt - h / 2 - .01), (p["shoulder_w"] * 1.02, p["torso_d"] * 1.25, h), col,
            "Chest", biseau=.03, effile=(1.05, 1.0))
    for sx in (-1, 0, 1):
        c.boite((sx * .07, -p["torso_d"] * .66, zt - h * .7), (.055, .035, .065), col, "Chest",
                biseau=.01)
    for sx in (1, -1):  # épaulières qui élargissent la silhouette
        c.boite((sx * (r["x_bras"]), 0, r["z_epaule"] + .02), (p["arm_t"] * 1.5, p["arm_t"] * 1.4,
                .05), col, "Shoulder_" + ("L" if sx > 0 else "R"), biseau=.02)


def acc_manteau_long(c, p, r, t):
    """Veste longue qui s'évase (pisteur) : pan avant et arrière sur les hanches."""
    col = t.get("couleur_manteau", t["haut"])
    zh = r["z_hanche"]
    c.boite((0, 0, zh - .07), (p["hip_w"] * 1.12, p["torso_d"] * 1.1, .2), col, "Hips",
            biseau=.025, effile_bas=(1.25, 1.2))
    c.boite((0, -p["torso_d"] * .52, r["z_epaule_haut"] - .1), (.03, .015, p["torso_h"] * .7),
            "cuir", "Chest", biseau=0)


def acc_blouse(c, p, r, t):
    col = t.get("couleur_blouse", "blanc_casse")
    zh = r["z_hanche"]
    c.boite((0, 0, zh - .09), (p["hip_w"] * 1.08, p["torso_d"] * 1.06, .22), col, "Hips",
            biseau=.02, effile_bas=(1.15, 1.1))
    for sx in (1, -1):
        c.boite((sx * p["waist_w"] * .25, -p["torso_d"] * .52, zh + .1), (.06, .012, .05), col,
                "Spine", biseau=.006)


def acc_brassard(c, p, r, t):
    x = r["x_bras"]
    ze = r["z_epaule"]
    c.boite((x, 0, ze - r["seg_bras"] * .55), (p["arm_t"] * 1.12, p["arm_t"] * 1.15, .045),
            "sarcelle", "UpperArm_L", biseau=.01)
    c.cylindre((x + p["arm_t"] * .56, 0, ze - r["seg_bras"] * .55), .014, .006, "blanc_casse",
               "UpperArm_L", segments=8, rot=(0, 90, 0))


def acc_sacoche(c, p, r, t):
    """Grand sac médical en bandoulière, sur la hanche droite."""
    zh = r["z_hanche"]
    c.boite((-p["hip_w"] * .62, -.01, zh + .02), (.07, .16, .13), t.get("couleur_sac", "toile_beige"),
            "Hips", biseau=.025)
    c.boite((0, -p["torso_d"] * .5, r["z_epaule_haut"] - p["torso_h"] * .45),
            (p["shoulder_w"] * .9, .012, .03), "cuir", "Chest", biseau=0, rot=(0, 38, 0))


def acc_protege_bras(c, p, r, t):
    """Protection bricolée de l'avant-bras gauche (ruban adhésif), cousue à la tenue."""
    x = r["x_bras"]
    c.boite((x, 0, r["z_epaule"] - r["seg_bras"] * 1.5), (p["arm_t"] * 1.2, p["arm_t"] * 1.25,
            r["seg_bras"] * .8), "adhesif", "LowerArm_L", biseau=.015)


def acc_plastron(c, p, r, t):
    """Plastron bricolé du milicien : plaques de plastique et ruban adhésif (emplacement Torse)."""
    zt = r["z_epaule_haut"]
    c.boite((0, -p["torso_d"] * .55, zt - .1), (p["shoulder_w"] * .62, .025, .13), "plastique_noir",
            "Chest", biseau=.01)
    for k in (-1, 1):
        c.boite((k * p["shoulder_w"] * .18, -p["torso_d"] * .57, zt - .1), (.03, .012, .15), "adhesif",
                "Chest", biseau=0)
    c.boite((0, -p["torso_d"] * .57, zt - .1), (p["shoulder_w"] * .66, .012, .028), "adhesif", "Chest", biseau=0)
    c.boite((0, p["torso_d"] * .5, zt - .1), (p["shoulder_w"] * .5, .012, .028), "adhesif", "Chest", biseau=0)


def acc_gourde(c, p, r, t):
    zh = r["z_hanche"]
    x = p["hip_w"] * .55
    c.cylindre((x, .03, zh - .02), .032, .1, "metal_gourde", "Hips", segments=8)
    c.cylindre((x, .03, zh + .04), .014, .02, "plastique_noir", "Hips", segments=6)
    c.boite((x, .03, zh + .03), (.075, .02, .012), "cuir", "Hips", biseau=0)


def acc_couteau_etui(c, p, r, t):
    zh = r["z_hanche"]
    x = -p["hip_w"] * .52
    c.boite((x, .02, zh - .04), (.02, .04, .11), "cuir", "Hips", biseau=0, effile_bas=(.8, .5))
    c.boite((x, .02, zh + .035), (.018, .024, .045), "plastique_noir", "Hips", biseau=0)


def acc_pochettes(c, p, r, t):
    zh = r["z_hanche"]
    for k in (-1, 1):
        c.boite((k * p["hip_w"] * .28, -p["torso_d"] * .52, zh + .02), (.06, .03, .06),
                t.get("couleur_pochettes", "olive"), "Hips", biseau=0)
        c.boite((k * p["hip_w"] * .28, -p["torso_d"] * .54, zh + .05), (.062, .034, .014), "semelle", "Hips",
                biseau=0)


def acc_radio(c, p, r, t):
    zt = r["z_epaule_haut"]
    x = p["shoulder_w"] * .3
    c.boite((x, -p["torso_d"] * .6, zt - .06), (.04, .03, .07), "plastique_noir", "Chest", biseau=0)
    c.boite((x + .012, -p["torso_d"] * .6, zt + .005), (.006, .006, .06), "plastique_noir", "Chest", biseau=0)


def acc_sac_poubelle(c, p, r, t):
    zh = r["z_hanche"]
    c.sphere((-p["hip_w"] * .6, .04, zh - .1), .07, "plastique_noir", "Hips", u=7, v=4, echelle=(1, 1, 1.3))
    c.boite((-p["hip_w"] * .6, .04, zh - .01), (.03, .03, .04), "plastique_noir", "Hips", biseau=0)


def acc_tablier(c, p, r, t):
    zh = r["z_hanche"]
    c.boite((0, -p["torso_d"] * .52, zh), (p["waist_w"] * .9, .015, .3),
            t.get("couleur_tablier", "toile_beige"), "Hips",
            biseau=.006)


def acc_fusil_dos(c, p, r, t):
    z = r["z_epaule_haut"] - p["torso_h"] * .45
    c.boite((0, p["torso_d"] / 2 + .04, z), (.035, .045, .5), "acier_sombre", "Chest", biseau=.008,
            rot=(0, 40, 0))
    c.boite((.07, p["torso_d"] / 2 + .045, z - .07), (.05, .05, .16), "bois_sombre", "Chest",
            biseau=.01, rot=(0, 40, 0))


def acc_arbalete_dos(c, p, r, t):
    z = r["z_epaule_haut"] - p["torso_h"] * .35
    c.boite((0, p["torso_d"] / 2 + .04, z), (.04, .04, .32), "bois_sombre", "Chest", biseau=.008,
            rot=(0, -30, 0))
    c.boite((0, p["torso_d"] / 2 + .05, z + .1), (.3, .03, .03), "acier_sombre", "Chest", biseau=.008,
            rot=(0, -30, 0), effile=(1, 1))
    c.cylindre((-.09, p["torso_d"] / 2 + .04, z - .02), .03, .22, "cuir", "Chest", segments=6,
               rot=(0, 25, 0))  # carquois


def acc_salopette(c, p, r, t):
    """Bretelles et bavette (fermier zombie)."""
    col = t["bas"]
    zt = r["z_epaule_haut"]
    c.boite((0, -p["torso_d"] * .51, zt - p["torso_h"] * .55), (p["waist_w"] * .6, .015, .14),
            col, "Chest", biseau=.006)
    for sx in (1, -1):
        c.boite((sx * p["waist_w"] * .22, -p["torso_d"] * .51, zt - .05), (.03, .015, .12), col,
                "Chest", biseau=0)


def acc_sang(c, p, r, t):
    zt = r["z_epaule_haut"]
    c.boite((.03, -p["torso_d"] * .51, zt - .08), (.1, .01, .12), "z_sang_seche", "Chest", biseau=.004,
            rot=(0, 15, 0))
    c.boite((-.04, -p["torso_d"] * .51, zt - p["torso_h"] * .7), (.07, .01, .06), "z_chair",
            "Spine", biseau=.004)


def acc_dechirures(c, p, r, t):
    """Lambeaux qui pendent au bas du haut."""
    zh = r["z_hanche"] + .02
    rnd = random.Random(t.get("graine", 1))
    for i in range(5):
        x = (i - 2) * p["waist_w"] * .2
        c.boite((x, -p["torso_d"] * .49 if i % 2 else p["torso_d"] * .49, zh - .03),
                (.04, .012, .06 + rnd.random() * .04), t["haut"], "Spine", biseau=0,
                effile_bas=(.2, 1.0))


def acc_debris_brute(c, p, r, t):
    """Panneau tordu planté dans le dos, chaîne qui pend, grillage sur l'épaule."""
    zt = r["z_epaule_haut"]
    c.boite((.08, p["torso_d"] * .5 + .03, zt - .02), (.03, .03, .45), "acier", "Chest", biseau=0,
            rot=(-20, 15, 0))
    c.boite((.12, p["torso_d"] * .5 + .1, zt + .12), (.15, .015, .15), "rouille", "Chest", biseau=.01,
            rot=(-20, 15, 8))
    for i in range(6):
        c.boite((-r["x_bras"] * .6, -p["torso_d"] * .4, r["z_hanche"] - .05 - i * .045),
                (.03, .015, .04), "acier_sombre", "Hips", biseau=.006, rot=(0, 0, 90 * (i % 2)))
    c.boite((r["x_bras"] * .95, 0, zt + .01), (p["arm_t"] * 1.5, p["torso_d"] * .8, .03),
            "acier", "Shoulder_L", biseau=0, rot=(0, -12, 0))


def acc_cotes(c, p, r, t):
    zt = r["z_epaule_haut"]
    for i in range(4):
        c.boite((0, -p["torso_d"] * .5, zt - .08 - i * .045), (p["waist_w"] * .75, .012, .016),
                "z_os", "Chest", biseau=0)


ACCESSOIRES = {
    "casquette": acc_casquette, "bonnet": acc_bonnet, "bandana": acc_bandana,
    "chapeau": acc_chapeau, "casque_mil": acc_casque_mil, "casque_chantier": acc_casque_chantier,
    "capuche": acc_capuche, "barbe": acc_barbe, "masque": acc_masque, "sac_dos": acc_sac_dos,
    "gilet_mil": acc_gilet_mil, "manteau_long": acc_manteau_long, "blouse": acc_blouse,
    "brassard": acc_brassard, "sacoche": acc_sacoche, "protege_bras": acc_protege_bras,
    "plastron": acc_plastron, "gourde": acc_gourde, "couteau_etui": acc_couteau_etui,
    "pochettes": acc_pochettes, "radio": acc_radio, "sac_poubelle": acc_sac_poubelle,
    "tablier": acc_tablier, "fusil_dos": acc_fusil_dos, "arbalete_dos": acc_arbalete_dos,
    "salopette": acc_salopette, "sang": acc_sang, "dechirures": acc_dechirures,
    "debris_brute": acc_debris_brute, "cotes": acc_cotes,
}


# ---------------------------------------------------------------------------
# Outils en main (SM_Outil_*), origine = point de prise, manche le long de +Z
# ---------------------------------------------------------------------------
def outil(nom, mat):
    c = Constructeur(hauteur=.5, salete=.1)
    g = "SOCKET_MainDroite"
    if nom == "Hache":
        c.boite((0, 0, .08), (.025, .025, .4), "bois_manche", g, biseau=.006)
        c.boite((0, -.05, .25), (.03, .12, .08), "acier", g, biseau=.008, effile=(1, 1.25))
    elif nom == "Pioche":
        c.boite((0, 0, .08), (.025, .025, .42), "bois_manche", g, biseau=.006)
        c.boite((0, 0, .27), (.025, .3, .035), "acier_sombre", g, biseau=.008, effile=(1, .3))
    elif nom == "Pelle":
        c.boite((0, 0, .1), (.022, .022, .42), "bois_manche", g, biseau=.006)
        c.boite((0, 0, .34), (.1, .02, .12), "acier", g, biseau=.01, effile=(.7, 1))
        c.boite((0, 0, -.12), (.07, .025, .025), "bois_manche", g, biseau=.006)
    elif nom == "Marteau":
        c.boite((0, 0, .06), (.022, .022, .25), "bois_manche", g, biseau=.005)
        c.boite((0, -.02, .18), (.035, .1, .035), "acier_sombre", g, biseau=.008)
    elif nom == "Batte":
        c.cylindre((0, 0, .1), .022, .38, "bois_manche", g, segments=8, rayon_haut=.04)
        for i in range(4):
            a = i * 90
            c.boite((.04 * math.cos(math.radians(a)), .04 * math.sin(math.radians(a)), .22 + i * .02),
                    (.035, .008, .008), "acier", g, biseau=0, rot=(0, 0, a))
    elif nom == "Balai":
        c.boite((0, 0, .1), (.02, .02, .45), "bois_manche", g, biseau=.005)
        c.boite((0, 0, .36), (.12, .04, .1), "paille", g, biseau=.01, effile=(1.3, 1.2))
    elif nom == "SacMedical":
        c.boite((0, 0, -.05), (.06, .14, .1), "toile_beige", g, biseau=.02)
        c.boite((0, 0, .03), (.02, .05, .05), "cuir", g, biseau=.008)
    elif nom == "Fusil":
        c.boite((0, -.12, .02), (.035, .45, .04), "acier_sombre", g, biseau=.006)
        c.boite((0, .1, -.01), (.04, .14, .07), "bois_sombre", g, biseau=.01)
    ob = c.vers_objet("SM_Outil_" + nom, mat)
    for vg in list(ob.vertex_groups):
        ob.vertex_groups.remove(vg)
    return ob


def socket_positions(p, r):
    """Position de repos de chaque accroche d'équipement (origine des objets SM_Equip_*)."""
    zt = r["z_epaule_haut"]
    zm = r["z_epaule"] - 2 * r["seg_bras"] - p["hand"] / 2
    return [("SOCKET_Tete", (0, 0, r["z_tete_bas"] + p["head_h"] * .75), "Head"),
            ("SOCKET_Torse", (0, 0, zt - p["torso_h"] * .4), "Chest"),
            ("SOCKET_Dos", (0, p["torso_d"] / 2 + .04, zt - p["torso_h"] * .45), "Chest"),
            ("SOCKET_Ceinture", (0, 0, r["z_hanche"] + .035), "Hips"),
            ("SOCKET_MainDroite", (-r["x_bras"], 0, zm), "Hand_R"),
            ("SOCKET_MainGauche", (r["x_bras"], 0, zm), "Hand_L")]


# ---------------------------------------------------------------------------
# Squelette
# ---------------------------------------------------------------------------
def creer_rig(nom, p, collection):
    r = reperes(p)
    arm_data = bpy.data.armatures.new(nom + "_Rig")
    arm = bpy.data.objects.new(nom, arm_data)
    collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm_data.edit_bones
    zh, zt = r["z_hanche"], r["z_epaule_haut"]
    avant = Vector((0, -1, 0))
    haut = Vector((0, 0, 1))

    def os_(n, t, q, parent=None, roll_vers=avant):
        b = eb.new(n)
        b.head, b.tail = Vector(t), Vector(q)
        if parent:
            b.parent = eb[parent]
            b.use_connect = False
        b.align_roll(roll_vers)
        return b

    os_("Hips", (0, 0, zh), (0, 0, zh + .07))
    os_("Spine", (0, 0, zh + .07), (0, 0, zh + .15), "Hips")
    os_("Chest", (0, 0, zh + .15), (0, 0, zt - .01), "Spine")
    os_("Neck", (0, 0, zt - .01), (0, 0, r["z_tete_bas"] + .02), "Chest")
    os_("Head", (0, 0, r["z_tete_bas"] + .02), (0, 0, r["z_tete_haut"]), "Neck")
    sb, ze = r["seg_bras"], r["z_epaule"]
    sj = r["seg_jambe"]
    for cote, sx in (("L", 1), ("R", -1)):
        x = sx * r["x_bras"]
        os_("Shoulder_" + cote, (sx * .03, 0, ze), (x, 0, ze), "Chest", roll_vers=haut)
        os_("UpperArm_" + cote, (x, 0, ze), (x, 0, ze - sb), "Shoulder_" + cote)
        os_("LowerArm_" + cote, (x, 0, ze - sb), (x, 0, ze - 2 * sb), "UpperArm_" + cote)
        os_("Hand_" + cote, (x, 0, ze - 2 * sb), (x, 0, ze - 2 * sb - p["hand"]), "LowerArm_" + cote)
        xl = sx * r["x_jambe"]
        os_("UpperLeg_" + cote, (xl, 0, zh), (xl, 0, zh - sj), "Hips")
        os_("LowerLeg_" + cote, (xl, 0, zh - sj), (xl, 0, p["foot_h"]), "UpperLeg_" + cote)
        os_("Foot_" + cote, (xl, 0, p["foot_h"]), (xl, -p["foot_l"] * .7, .01), "LowerLeg_" + cote,
            roll_vers=haut)
    zm = ze - 2 * sb - p["hand"] / 2
    os_("SOCKET_MainDroite", (-r["x_bras"], 0, zm), (-r["x_bras"], -.05, zm), "Hand_R", roll_vers=haut)
    os_("SOCKET_MainGauche", (r["x_bras"], 0, zm), (r["x_bras"], -.05, zm), "Hand_L", roll_vers=haut)
    zd = zt - p["torso_h"] * .45
    os_("SOCKET_Dos", (0, p["torso_d"] / 2 + .04, zd), (0, p["torso_d"] / 2 + .04, zd + .05), "Chest",
        roll_vers=avant)
    for n, pos, parent in socket_positions(p, r):
        if n in ("SOCKET_Tete", "SOCKET_Torse", "SOCKET_Ceinture"):
            os_(n, pos, Vector(pos) + Vector((0, 0, .05)), parent, roll_vers=avant)
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in arm.pose.bones:
        pb.rotation_mode = "XYZ"
    arm.show_in_front = True
    return arm


# ---------------------------------------------------------------------------
# Animations procédurales (en place, 30 images/s). +X = vers l'avant pour tous
# les os des membres et du tronc (roll aligné sur -Y).
# ---------------------------------------------------------------------------
def _keys(arm, nom, n_images, fonction, pas=2):
    """fonction(t) -> {os: (rx, ry, rz)} ou {os: ("loc", y)}. t dans [0, 1]."""
    if arm.animation_data is None:
        arm.animation_data_create()
    arm.animation_data.action = None
    for f in range(0, n_images + 1, pas):
        t = f / n_images
        pose = fonction(t)
        for pb in arm.pose.bones:
            pb.rotation_euler = (0, 0, 0)
            pb.location = (0, 0, 0)
        for os_nom, val in pose.items():
            pb = arm.pose.bones[os_nom]
            if val[0] == "loc":  # ("loc", haut, avant) dans l'espace de l'os Hips
                pb.location = (0, val[1], val[2] if len(val) > 2 else 0)
            else:
                pb.rotation_euler = [math.radians(a) for a in val]
        for pb in arm.pose.bones:
            pb.keyframe_insert("rotation_euler", frame=f + 1)
            if pb.name == "Hips":
                pb.keyframe_insert("location", frame=f + 1)
    act = arm.animation_data.action
    act.name = nom
    act.use_fake_user = True
    act.frame_range = (1, n_images + 1)
    arm.animation_data.action = None
    return act


S = lambda t, k=1, ph=0.0: math.sin(2 * math.pi * (k * t + ph))
C = lambda t, k=1, ph=0.0: math.cos(2 * math.pi * (k * t + ph))


def anim_survivant(arm, p):
    def repos(t):
        return {"Chest": (1.5 * S(t, 2), 0, 0), "Head": (2 * S(t, 2, .2), 4 * S(t), 0),
                "UpperArm_L": (2 * S(t, 2, .1), 0, -4), "UpperArm_R": (2 * S(t, 2, .1), 0, 4),
                "LowerArm_L": (6, 0, 0), "LowerArm_R": (6, 0, 0)}

    def marche(t, amp=1.0, charge=False):
        d = {"UpperLeg_L": (28 * amp * S(t), 0, 0), "UpperLeg_R": (-28 * amp * S(t), 0, 0),
             "LowerLeg_L": (-35 * amp * max(0, -S(t, 1, .1)), 0, 0),
             "LowerLeg_R": (-35 * amp * max(0, S(t, 1, .1)), 0, 0),
             "Hips": ("loc", .018 * amp * abs(C(t))),
             "Spine": (3 * amp, 4 * amp * S(t), 0), "Chest": (2, -3 * amp * S(t), 0),
             "Head": (0, 2 * S(t), 0)}
        if charge:
            d.update({"UpperArm_L": (55, 0, 0), "UpperArm_R": (55, 0, 0),
                      "LowerArm_L": (60, 0, 0), "LowerArm_R": (60, 0, 0), "Spine": (10, 0, 0)})
        else:
            d.update({"UpperArm_L": (-24 * amp * S(t), 0, -4), "UpperArm_R": (24 * amp * S(t), 0, 4),
                      "LowerArm_L": (15 + 10 * amp * max(0, -S(t)), 0, 0),
                      "LowerArm_R": (15 + 10 * amp * max(0, S(t)), 0, 0)})
        return d

    def frapper(t):
        # monte lentement (0 -> 0.55), frappe vite (0.55 -> 0.7), revient
        if t < .55:
            a = t / .55
            bras = 40 + 130 * (0.5 - 0.5 * math.cos(math.pi * a))
            buste = -6 * a
        elif t < .7:
            a = (t - .55) / .15
            bras = 170 - 140 * a
            buste = -6 + 22 * a
        else:
            a = (t - .7) / .3
            bras = 30 + 10 * a
            buste = 16 * (1 - a)
        rz = 24 * min(1.0, bras / 120.0)
        return {"UpperArm_R": (bras, 0, -rz), "UpperArm_L": (bras - 5, 0, rz),
                "LowerArm_R": (25, 0, 0), "LowerArm_L": (25, 0, 0),
                "Spine": (buste * .5, 0, 0), "Chest": (buste * .5, 0, 0), "Head": (-buste * .3, 0, 0),
                "UpperLeg_L": (12, 0, 0), "UpperLeg_R": (-8, 0, 0), "LowerLeg_L": (-10, 0, 0)}

    r = reperes(p)
    sj = r["seg_jambe"]

    def accroupi(a1, a2):
        """Cuisses à a1° vers l'avant, tibias à a2° (négatif = vers l'arrière) : bassin baissé,
        pieds gardés à plat et sous le corps."""
        w1, w2 = math.radians(a1), math.radians(a1 + a2)
        baisse = 2 * sj - sj * math.cos(w1) - sj * math.cos(w2)
        recul = sj * math.sin(w1) + sj * math.sin(w2)
        return {"UpperLeg_L": (a1, 0, -4), "UpperLeg_R": (a1, 0, 4),
                "LowerLeg_L": (a2, 0, 0), "LowerLeg_R": (a2, 0, 0),
                "Foot_L": (-(a1 + a2), 0, 0), "Foot_R": (-(a1 + a2), 0, 0),
                "Hips": ("loc", -baisse, -recul)}

    def ramasser(t):
        """Accroupi, petits gestes rapides : la main droite pique un caillou au sol (2 fois par
        cycle) et le glisse dans la main gauche."""
        d = accroupi(75, -120)
        g = abs(S(t, 2))  # 0 -> 1 -> 0 deux fois : 1 = main au sol, 0 = main ramenée
        d.update({"Spine": (24, 0, -6 * g), "Chest": (12, 0, 0), "Head": (8 + 10 * g, 0, -8 * g),
                  "UpperArm_R": (25 + 50 * g, 0, -14 + 22 * g), "LowerArm_R": (70 - 60 * g, 0, 0),
                  "Hand_R": (35 * g, 0, 0),
                  "UpperArm_L": (40, 0, 18), "LowerArm_L": (75, 0, 0)})
        return d

    def piocher(t):
        """Coups de pioche vers le sol devant soi : montée au-dessus de l'épaule, frappe basse."""
        if t < .5:
            a = 0.5 - 0.5 * math.cos(math.pi * t / .5)
            bras, buste, gen = 30 + 140 * a, -4 * a, 0
        elif t < .62:
            a = (t - .5) / .12
            bras, buste, gen = 170 - 125 * a, -4 + 36 * a, 20 * a
        else:
            a = (t - .62) / .38
            bras, buste, gen = 45 - 15 * a, 32 * (1 - a), 20 * (1 - a)
        rz = 24 * min(1.0, bras / 120.0)
        return {"UpperArm_R": (bras, 0, -rz), "UpperArm_L": (bras - 8, 0, rz),
                "LowerArm_R": (15, 0, 0), "LowerArm_L": (25, 0, 0),
                "Spine": (buste * .6, 0, 0), "Chest": (buste * .4, 0, 0), "Head": (-buste * .2, 0, 0),
                "UpperLeg_L": (18 + gen * .5, 0, 0), "UpperLeg_R": (-10, 0, 0),
                "LowerLeg_L": (-15 - gen, 0, 0), "LowerLeg_R": (-5, 0, 0),
                "Hips": ("loc", -.012 * gen / 20, 0)}

    def cueillir(t):
        """Debout devant un buisson, les mains piochent des baies à hauteur de poitrine."""
        g1, g2 = max(0, S(t)), max(0, -S(t))
        return {"Spine": (8, 0, 0), "Head": (10, 0, 6 * S(t)),
                "UpperArm_R": (55 + 25 * g1, 0, -10), "LowerArm_R": (35 - 20 * g1, 0, 0),
                "UpperArm_L": (55 + 25 * g2, 0, 10), "LowerArm_L": (35 - 20 * g2, 0, 0),
                "Hand_R": (25 * g1, 0, 0), "Hand_L": (25 * g2, 0, 0)}

    def fouiller(t):
        """Penché dans une épave de voiture, les bras fouillent (gestes irréguliers)."""
        return {"Spine": (35, 0, 4 * S(t)), "Chest": (25, 0, 6 * S(t, 2)), "Head": (-10, 8 * S(t), 0),
                "UpperArm_R": (85 + 15 * S(t, 2), 0, -10 + 8 * S(t, 3)), "LowerArm_R": (30 + 20 * S(t, 2, .3), 0, 0),
                "UpperArm_L": (80 + 15 * S(t, 2, .5), 0, 10), "LowerArm_L": (30 + 20 * S(t, 3, .1), 0, 0),
                "UpperLeg_L": (12, 0, 0), "UpperLeg_R": (-6, 0, 0), "Hips": ("loc", -.01, .03)}

    return [_keys(arm, "ANIM_Humain_Repos", 60, repos),
            _keys(arm, "ANIM_Humain_Marche", 24, marche),
            _keys(arm, "ANIM_Humain_Course", 16, lambda t: dict(marche(t, 1.6), Spine=(12, 0, 0))),
            _keys(arm, "ANIM_Humain_MarcheChargee", 28, lambda t: marche(t, .75, True)),
            _keys(arm, "ANIM_Humain_Frapper", 32, frapper),
            _keys(arm, "ANIM_Humain_RamasserCailloux", 24, ramasser),
            _keys(arm, "ANIM_Humain_Piocher", 36, piocher),
            _keys(arm, "ANIM_Humain_Cueillir", 30, cueillir),
            _keys(arm, "ANIM_Humain_Fouiller", 48, fouiller)]


def anim_zombie(arm, p, z):
    """z = posture : penche, bras, tete, accroupi, raide (jambe droite raide)."""
    pen, bras, tete_inc = z["penche"], z["bras"], z["tete"]
    acc = z.get("accroupi", 0)
    r = reperes(p)
    sj = r["seg_jambe"]
    baisse = 0.0
    if acc:
        a1, a2 = math.radians(acc), math.radians(acc * 1.6 - acc)
        baisse = sj * (1 - math.cos(a1)) + sj * (1 - math.cos(a2))

    def base(t):
        return {"Spine": (pen * .5, 0, 0), "Chest": (pen * .5, 0, 3 * S(t)),
                "Neck": (z.get("cou", 10), 0, 0), "Head": (-pen * .4, 0, tete_inc + 4 * S(t, 1, .3)),
                "UpperArm_L": (bras + 4 * S(t, 1, .2), 0, -6), "UpperArm_R": (bras - 10 + 5 * S(t), 0, 8),
                "LowerArm_L": (12, 0, 0), "LowerArm_R": (25, 0, 0),
                "UpperLeg_L": (acc, 0, 0), "UpperLeg_R": (acc, 0, 0),
                "LowerLeg_L": (-acc * 1.6, 0, 0), "LowerLeg_R": (-acc * 1.6, 0, 0),
                "Foot_L": (acc * .6, 0, 0), "Foot_R": (acc * .6, 0, 0),
                "Hips": ("loc", -baisse)}

    def repos(t):
        return base(t)

    def marche(t):
        d = base(t)
        d["UpperLeg_L"] = (acc + 24 * S(t), 0, 0)
        d["UpperLeg_R"] = (acc - 16 * S(t), 0, -4)  # jambe raide, traînée
        d["LowerLeg_L"] = (-acc * 1.6 - 40 * max(0, -S(t, 1, .1)), 0, 0)
        d["LowerLeg_R"] = (-acc * 1.6 - 6 * max(0, S(t)), 0, 0)
        d["Chest"] = (pen * .5, 0, 8 * S(t))
        d["Spine"] = (pen * .5, 5 * S(t), 0)
        d["Hips"] = ("loc", -baisse + .012 * abs(C(t)))
        return d

    def course(t):
        d = base(t)
        d.update({"UpperLeg_L": (acc + 38 * S(t), 0, 0), "UpperLeg_R": (acc - 38 * S(t), 0, 0),
                  "LowerLeg_L": (-acc * 1.6 - 60 * max(0, -S(t, 1, .1)), 0, 0),
                  "LowerLeg_R": (-acc * 1.6 - 60 * max(0, S(t, 1, .1)), 0, 0),
                  "UpperArm_L": (-35 + 20 * S(t), 0, -10), "UpperArm_R": (-35 - 20 * S(t), 0, 10),
                  "Hips": ("loc", -baisse + .025 * abs(C(t)))})
        return d

    def attaque(t):
        d = base(t)
        a = math.sin(math.pi * t)
        d["Spine"] = (pen * .5 + 15 * a, 0, 0)
        d["UpperArm_L"] = (bras + 40 * a, 0, -6 + 10 * a)
        d["UpperArm_R"] = (bras + 40 * a, 0, 6 - 10 * a)
        d["LowerArm_L"] = (12 - 10 * a, 0, 0)
        d["LowerArm_R"] = (12 - 10 * a, 0, 0)
        d["Head"] = (-pen * .4 + 15 * a, 0, tete_inc)
        return d

    acts = [_keys(arm, "ANIM_Zombie_Repos", 60, repos),
            _keys(arm, "ANIM_Zombie_Marche", 32, marche),
            _keys(arm, "ANIM_Zombie_Attaque", 24, attaque)]
    if z.get("course"):
        acts.append(_keys(arm, "ANIM_Zombie_Course", 16, course))
    return acts


# ---------------------------------------------------------------------------
# Catalogue (fiches 01 et 02)
# ---------------------------------------------------------------------------
# tenue = vêtements fusionnés au corps ; equipement = objets séparés accrochés aux emplacements
# (le jeu peut les changer). Chaque survivant sort pré-équipé selon son rôle.
SURVIVANTS = {
    "Ouvrier": dict(corps=CORPS_A, tenue=dict(
        peau="peau_doree", cheveux="cheveux_chatains", coiffure="court", haut="bleu_travail",
        bas="bleu_travail", manches="retroussees", couleur_chapeau="denim",
        details=DETAILS_SURVIVANT + ["rapiece_genou", "barbe_naissante", "boue_jambes"]),
        equipement=dict(Tete=["casquette"], Dos=["sac_dos"], Ceinture=["gourde"], MainDroite="Hache")),
    "Carrier": dict(corps=CORPS_B, tenue=dict(
        peau="peau_claire", cheveux="cheveux_paille", coiffure="queue", haut="tshirt_creme",
        bas="kaki", manches="aucune", gants="cuir", couleur_chapeau="plastique_jaune",
        details=DETAILS_SURVIVANT + ["poches_cuisse", "bandage_bras", "foulard"]),
        equipement=dict(Tete=["casque_chantier"], Ceinture=["gourde", "couteau_etui"],
                        MainDroite="Pioche")),
    "Milicien": dict(corps=CORPS_B, tenue=dict(
        peau="peau_brune", cheveux="cheveux_noirs", coiffure="queue", haut="flanelle_rouge",
        bas="denim", genouilleres="adhesif", accessoires=["protege_bras"],
        details=DETAILS_SURVIVANT + ["rapiece_coude", "manche_dechiree", "sang_vetement"]),
        equipement=dict(Tete=["bandana"], Torse=["plastron"], Ceinture=["couteau_etui"],
                        MainDroite="Batte")),
    "Militaire": dict(corps=CORPS_A, tenue=dict(
        peau="peau_foncee", cheveux="cheveux_noirs", coiffure="court", haut="sable", bas="olive",
        couleur_bottes="semelle", details=DETAILS_SURVIVANT + ["poches_cuisse", "barbe_naissante", "boue_jambes"]),
        equipement=dict(Tete=["casque_mil"], Torse=["gilet_mil", "radio"], Dos=["fusil_dos"],
                        Ceinture=["pochettes", "gourde"])),
    "Pisteur": dict(corps=CORPS_A, tenue=dict(
        peau="peau_claire", cheveux="cheveux_chatains", coiffure="longs", haut="brun_chasse",
        bas="camo", couleur_chapeau="brun_chasse", accessoires=["barbe", "manteau_long"],
        couleur_foulard="olive", details=DETAILS_SURVIVANT + ["rapiece_coude", "foulard"]),
        equipement=dict(Tete=["chapeau"], Dos=["arbalete_dos"], Ceinture=["couteau_etui", "gourde"])),
    "Medecin": dict(corps=CORPS_B, tenue=dict(
        peau="peau_doree", cheveux="cheveux_paille", coiffure="chignon", haut="blanc_casse",
        bas="denim_delave", accessoires=["blouse", "brassard"],
        details=DETAILS_SURVIVANT + ["bandage_bras"]),
        equipement=dict(Ceinture=["sacoche"], MainGauche="SacMedical")),
    "Nettoyeur": dict(corps=CORPS_A, tenue=dict(
        peau="peau_claire", cheveux="cheveux_gris", coiffure="degarni", haut="sweat_gris",
        bas="kaki", gants="cuir", accessoires=["masque", "tablier"], couleur_chapeau="sweat_gris",
        details=DETAILS_SURVIVANT + ["rapiece_genou", "sang_vetement", "boue_jambes"]),
        equipement=dict(Tete=["bonnet"], Ceinture=["sac_poubelle"], MainDroite="Balai")),
}

ZOMBIES = {
    "Rodeur": dict(corps=proportions(CORPS_A, shoulder_w=.33, head_h=.29), salete=.6,
                   posture=dict(penche=22, bras=70, tete=16, cou=8),
                   tenue=dict(peau="z_peau_recente", cheveux="z_cheveux", coiffure="degarni",
                              haut="z_tshirt_creme", bas="z_bleu_travail", zombie=True,
                              bras_nu="R", sourcils="z_cheveux", angle_sourcils=-10,
                              chaussures="z_semelle",
                              accessoires=["salopette", "sang", "dechirures"], graine=3)),
    "Coureur": dict(corps=proportions(CORPS_B, arm_t=.064, leg_t=.085, waist_w=.22,
                                      shoulder_w=.29, torso_d=.15), salete=.4,
                    posture=dict(penche=28, bras=-20, tete=6, cou=14, course=True),
                    tenue=dict(peau="z_peau_livide", cheveux="z_cheveux", coiffure="queue",
                               haut="z_sweat_gris", bas="z_denim", zombie=True,
                               bouche_ouverte=True, chaussures="z_tshirt_creme",
                               sourcils="z_cheveux", angle_sourcils=-12,
                               accessoires=["sang", "dechirures"], graine=5)),
    "Cogneur": dict(corps=proportions(CORPS_A, shoulder_w=.55, waist_w=.40, torso_d=.26,
                                      torso_h=.44, leg_h=.45, leg_t=.14, arm_t=.13,
                                      arm_len=.40, hand=.1, fist=1.6, head_h=.26,
                                      head_w=.24, neck=.0, hip_w=.32), salete=.7,
                    posture=dict(penche=14, bras=20, tete=4, cou=-4),
                    tenue=dict(peau="z_peau_bleuie", haut="z_orange_hv", bas="z_denim",
                               zombie=True, manches="aucune", sourcils="z_cheveux",
                               chaussures="z_semelle", fendu=True, angle_sourcils=-14,
                               couleur_chapeau="z_tshirt_creme",
                               accessoires=["casque_chantier", "sang", "dechirures"], graine=7)),
    "Grimpeur": dict(corps=proportions(CORPS_B, arm_len=.50, arm_t=.06, leg_t=.075,
                                       waist_w=.2, shoulder_w=.28, torso_d=.13, leg_h=.40,
                                       hand=.09), salete=.6,
                     posture=dict(penche=40, bras=55, tete=10, cou=-20, accroupi=40),
                     tenue=dict(peau="z_peau_pourrie", cheveux="z_cheveux", coiffure="longs",
                                haut="z_peau_pourrie", bas="z_kaki", zombie=True,
                                manches="aucune", chaussures="z_peau_pourrie", griffes=True,
                                sourcils="z_cheveux", angle_sourcils=-15, ceinture=False,
                                col=False, accessoires=["cotes"], graine=11)),
    "Brute": dict(corps=proportions(CORPS_A, shoulder_w=.80, waist_w=.62, torso_d=.42,
                                    torso_h=.70, leg_h=.64, leg_t=.22, arm_t=.19,
                                    arm_len=.78, hand=.15, fist=1.3, head_h=.27,
                                    head_w=.25, head_d=.25, neck=.0, hip_w=.5,
                                    foot_l=.22, foot_w=.12, foot_h=.07), salete=.9,
                  posture=dict(penche=18, bras=15, tete=2, cou=-10),
                  tenue=dict(peau="z_peau_pourrie", haut="z_peau_pourrie", bas="z_kaki",
                             zombie=True, manches="aucune", bouche_ouverte=True,
                             sourcils="z_cheveux", angle_sourcils=-18, chaussures="z_semelle",
                             col=False, couleur_tablier="z_toile_beige",
                             accessoires=["tablier", "debris_brute", "sang"],
                             graine=13)),
}


# ---------------------------------------------------------------------------
# Assemblage
# ---------------------------------------------------------------------------
def nb_triangles(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def faire_lods(ob, arm, collection):
    lods = [ob]
    for i, ratio in ((1, .5), (2, .22)):
        me = ob.data.copy()
        lod = bpy.data.objects.new(ob.name.replace("_LOD0", "_LOD%d" % i), me)
        collection.objects.link(lod)
        for vg in ob.vertex_groups:
            lod.vertex_groups.new(name=vg.name)
        mod = lod.modifiers.new("Decimate", "DECIMATE")
        mod.ratio = ratio
        bpy.context.view_layer.objects.active = lod
        bpy.ops.object.modifier_apply(modifier=mod.name)
        lod.parent = arm
        m = lod.modifiers.new("Armature", "ARMATURE")
        m.object = arm
        lods.append(lod)
    return lods


def creer_personnage(nom, fiche, mat, collection, x=0.0, zombie=False):
    p = fiche["corps"]
    t = dict(fiche["tenue"])
    r = reperes(p)
    hauteur = r["z_tete_haut"]
    col = bpy.data.collections.new(nom)
    collection.children.link(col)
    arm = creer_rig("RIG_" + nom, p, col)
    c = Constructeur(hauteur=hauteur, salete=fiche.get("salete", .25))
    corps(c, p, t)
    ob = c.vers_objet("SK_%s_LOD0" % nom, mat)
    col.objects.link(ob)
    ob.parent = arm
    m = ob.modifiers.new("Armature", "ARMATURE")
    m.object = arm
    lods = faire_lods(ob, arm, col)
    objets = [arm] + lods
    equip = []
    for emplacement, contenu in fiche.get("equipement", {}).items():
        o = objet_equipement(nom, emplacement, contenu, p, r, t, mat)
        col.objects.link(o)
        accrocher(o, arm, EMPLACEMENTS[emplacement])
        objets.append(o)
        equip.append(o)
    if zombie:
        acts = anim_zombie(arm, p, fiche["posture"])
    else:
        acts = anim_survivant(arm, p)
    arm.location.x = x
    arm["triangles_LOD0"] = nb_triangles(ob)
    return dict(nom=nom, arm=arm, objets=objets, actions=acts, lods=lods,
                tri=[nb_triangles(l) for l in lods], hauteur=hauteur,
                equip={o.name: nb_triangles(o) for o in equip})


def objet_equipement(perso, emplacement, contenu, p, r, t, mat):
    """Objet SM_ séparé, origine sur l'accroche de l'emplacement."""
    pos = {n: Vector(v) for n, v, _ in socket_positions(p, r)}[EMPLACEMENTS[emplacement]]
    if isinstance(contenu, str):  # outil tenu en main
        o = outil(contenu, mat)
        o.name = "SM_Equip_%s_%s" % (perso, contenu)
        angle = {"SacMedical": 0, "Fusil": 0}.get(contenu, 80)  # manche vers l'avant
        o["rotation_prise"] = angle
        o["position"] = list(pos)
        return o
    c = Constructeur(hauteur=r["z_tete_haut"], salete=.3)
    for n in contenu:
        ACCESSOIRES[n](c, p, r, t)
    bmesh.ops.translate(c.bm, vec=-pos, verts=c.bm.verts)
    o = c.vers_objet("SM_Equip_%s_%s" % (perso, "_".join(contenu)), mat)
    for vg in list(o.vertex_groups):
        o.vertex_groups.remove(vg)
    o["position"] = list(pos)
    return o


def accrocher(o, arm, socket):
    o.parent = arm
    o.parent_type = "BONE"
    o.parent_bone = socket
    bpy.context.view_layer.update()
    pos = Vector(o["position"])
    o.matrix_world = Matrix.Translation(pos) @ _rot((o.get("rotation_prise", 0), 0, 0))


def assigner_action(arm, act, image):
    if arm.animation_data is None:
        arm.animation_data_create()
    arm.animation_data.action = act
    if getattr(arm.animation_data, "action_slot", None) is None and hasattr(act, "slots") and act.slots:
        arm.animation_data.action_slot = act.slots[0]
    bpy.context.scene.frame_set(image)


def exporter_fbx(perso, dossier):
    bpy.ops.object.select_all(action="DESELECT")
    for o in perso["objets"]:
        o.select_set(True)
    bpy.context.view_layer.objects.active = perso["arm"]
    x = perso["arm"].location.x
    perso["arm"].location.x = 0
    if perso["arm"].animation_data:
        perso["arm"].animation_data.action = None
    chemin = os.path.join(dossier, "SK_%s.fbx" % perso["nom"])
    bpy.ops.export_scene.fbx(
        filepath=chemin, use_selection=True, object_types={"ARMATURE", "MESH"},
        axis_forward="-Z", axis_up="Y", apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
        bake_space_transform=False, mesh_smooth_type="FACE", use_mesh_modifiers=False,
        add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
        armature_nodetype="NULL", bake_anim=True, bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=True, bake_anim_force_startend_keying=True,
        bake_anim_simplify_factor=0.0, colors_type="SRGB", path_mode="AUTO")
    perso["arm"].location.x = x
    return chemin


def _fcurves(act):
    """Courbes d'une action, API de Blender 4.4+ (calques et emplacements) ou ancienne API."""
    if getattr(act, "layers", None):
        res = []
        for lay in act.layers:
            for st in lay.strips:
                for cb in getattr(st, "channelbags", []):
                    res.extend(cb.fcurves)
        if res:
            return res
    return list(getattr(act, "fcurves", []))


def passer_en_pose_t(perso):
    """Repos en T (bras à l'horizontale) pour le type Humanoid d'Unity, animations inchangées.

    Les bras sont levés de 90° et cette pose devient la pose de repos (maillages compris) ;
    chaque clé des os UpperArm est compensée (L' = D⁻¹ · L) pour que le mouvement reste identique."""
    arm = perso["arm"]
    bpy.context.view_layer.objects.active = arm
    for o in bpy.context.selected_objects:
        o.select_set(False)
    arm.select_set(True)
    if arm.animation_data:
        arm.animation_data.action = None
    D = {}
    bpy.ops.object.mode_set(mode="POSE")
    for pb in arm.pose.bones:
        pb.rotation_euler = (0, 0, 0)
        pb.location = (0, 0, 0)
    for cote, sx in (("L", 1), ("R", -1)):
        pb = arm.pose.bones["UpperArm_" + cote]
        meilleur = None
        for signe in (1, -1):  # le bon sens est celui qui écarte le bras du corps
            pb.rotation_euler = (0, 0, math.radians(90 * signe))
            bpy.context.view_layer.update()
            dx = sx * (pb.tail.x - pb.head.x)
            if meilleur is None or dx > meilleur[0]:
                meilleur = (dx, signe)
        pb.rotation_euler = (0, 0, math.radians(90 * meilleur[1]))
        D[pb.name] = pb.rotation_euler.to_matrix()
    bpy.context.view_layer.update()
    bpy.ops.object.mode_set(mode="OBJECT")
    # maillages : figer la pose dans les sommets, puis remettre le modificateur
    for o in perso["objets"]:
        if o.type != "MESH":
            continue
        mod = next((m for m in o.modifiers if m.type == "ARMATURE"), None)
        if mod is None:
            continue
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_apply(modifier=mod.name)
        m = o.modifiers.new("Armature", "ARMATURE")
        m.object = arm
    # objets d'équipement accrochés aux os : garder leur place dans le monde
    enfants = [(o, o.matrix_world.copy()) for o in perso["objets"] if o.parent == arm and o.parent_type == "BONE"]
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.select_all(action="SELECT")
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.update()
    for o, mw in enfants:
        o.matrix_world = mw
    # compensation des clés
    for act in perso["actions"]:
        courbes = {}
        for fc in _fcurves(act):
            for nom in D:
                if fc.data_path == 'pose.bones["%s"].rotation_euler' % nom:
                    courbes.setdefault(nom, {})[fc.array_index] = fc
        for nom, fcs in courbes.items():
            if len(fcs) < 3:
                continue
            Dinv = D[nom].inverted()
            images = sorted({kp.co.x for kp in fcs[0].keyframe_points})
            prec = None
            nouvelles = {}
            for f in images:
                e = mathutils.Euler([fcs[i].evaluate(f) for i in range(3)], "XYZ")
                n = (Dinv @ e.to_matrix()).to_euler("XYZ", prec) if prec else (Dinv @ e.to_matrix()).to_euler("XYZ")
                prec = n
                nouvelles[f] = n
            for i in range(3):
                for kp in fcs[i].keyframe_points:
                    v = nouvelles[kp.co.x][i]
                    d = v - kp.co.y
                    kp.co.y = v
                    kp.handle_left.y += d
                    kp.handle_right.y += d
                fcs[i].update()
    perso["pose_t"] = True


def preparer_nla(perso):
    """Une piste NLA par animation : l'export FBX en fait une prise (take) chacune."""
    arm = perso["arm"]
    if arm.animation_data is None:
        arm.animation_data_create()
    arm.animation_data.action = None
    for act in perso["actions"]:
        propre = act.name.split(".")[0]
        tr = arm.animation_data.nla_tracks.new()
        tr.name = propre
        st = tr.strips.new(propre, int(act.frame_range[0]), act)
        st.name = propre
        tr.mute = True


# ---------------------------------------------------------------------------
# Rendus d'aperçu (Cycles, processeur)
# ---------------------------------------------------------------------------
def scene_rendu(largeur, hauteur_px, centre_x, ortho, cam_y=-6.0, cam_z=2.6, cible_z=.5):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.samples = 48
    sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = largeur, hauteur_px
    sc.render.film_transparent = False
    sc.view_settings.view_transform = "AgX" if "AgX" in [
        i.identifier for i in sc.view_settings.bl_rna.properties["view_transform"].enum_items] else "Standard"
    looks = [i.identifier for i in sc.view_settings.bl_rna.properties["look"].enum_items]
    for look in ("AgX - Medium High Contrast", "Medium High Contrast"):
        if look in looks:
            sc.view_settings.look = look
            break
    cam = bpy.data.objects.get("Camera_Apercu")
    if cam is None:
        cam = bpy.data.objects.new("Camera_Apercu", bpy.data.cameras.new("Camera_Apercu"))
        sc.collection.objects.link(cam)
        soleil = bpy.data.objects.new("Soleil", bpy.data.lights.new("Soleil", "SUN"))
        # Ambiance plus sombre (retours de la démo) : soleil bas et chaud, ciel couvert froid
        soleil.data.energy = 2.6
        soleil.data.color = (1.0, .82, .62)
        soleil.data.angle = math.radians(6)
        soleil.rotation_euler = (math.radians(62), math.radians(8), math.radians(-40))
        sc.collection.objects.link(soleil)
        sol = bpy.data.meshes.new("Sol")
        bm = bmesh.new()
        bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=40)
        bm.to_mesh(sol)
        bm.free()
        msol = bpy.data.materials.new("Sol")
        msol.use_nodes = True
        msol.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (.13, .12, .09, 1)
        sol.materials.append(msol)
        sc.collection.objects.link(bpy.data.objects.new("Sol", sol))
        w = sc.world or bpy.data.worlds.new("Monde")
        sc.world = w
        w.use_nodes = True
        w.node_tree.nodes["Background"].inputs["Color"].default_value = (.32, .36, .42, 1)
        w.node_tree.nodes["Background"].inputs["Strength"].default_value = .55
    sc.camera = cam
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = ortho
    cam.location = (centre_x, cam_y, cam_z)
    d = Vector((centre_x, 0, cible_z)) - cam.location
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    return sc


def rendre(chemin, sc):
    sc.render.filepath = chemin
    bpy.ops.render.render(write_still=True)


def planche(chemins, colonnes, sortie):
    """Assemble des images de même taille en une planche (lignes de `colonnes` images)."""
    import numpy as np
    imgs = [bpy.data.images.load(c) for c in chemins]
    w, h = imgs[0].size
    lignes = (len(imgs) + colonnes - 1) // colonnes
    buf = np.zeros((h * lignes, w * colonnes, 4), dtype=np.float32)
    for i, im in enumerate(imgs):
        a = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)
        lg, cl = lignes - 1 - i // colonnes, i % colonnes
        buf[lg * h:(lg + 1) * h, cl * w:(cl + 1) * w] = a
    out = bpy.data.images.new("Planche", w * colonnes, h * lignes)
    out.pixels[:] = buf.ravel()
    out.filepath_raw = sortie
    out.file_format = "PNG"
    out.save()
    for im in imgs:
        bpy.data.images.remove(im)


def masquer_lods(persos, visibles=(0,)):
    for p in persos:
        for i, l in enumerate(p["lods"]):
            l.hide_render = i not in visibles


# ---------------------------------------------------------------------------
# Principal
# ---------------------------------------------------------------------------
def principal(sortie=None, rendu=True):
    # Repartir d'une scène vide
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    racine = bpy.context.scene.collection
    img = creer_palette(sortie)
    mat = creer_materiau(img)
    col_s = bpy.data.collections.new("Survivants")
    col_z = bpy.data.collections.new("Zombies")
    racine.children.link(col_s)
    racine.children.link(col_z)

    persos_s, persos_z = [], []
    for i, (nom, fiche) in enumerate(SURVIVANTS.items()):
        persos_s.append(creer_personnage("Survivant_" + nom, fiche, mat, col_s, x=i * .64))
    x = 0.0
    for nom, fiche in ZOMBIES.items():
        pz = creer_personnage(nom, fiche, mat, col_z, x=x, zombie=True)
        pz["arm"].location.y = 3.0
        persos_z.append(pz)
        x += {"Cogneur": .95, "Grimpeur": 1.15}.get(nom, .8)

    if POSE_T:
        for p in persos_s + persos_z:
            passer_en_pose_t(p)
    bilan = []
    for p in persos_s + persos_z:
        bilan.append("%-22s hauteur %.2f m  triangles LOD0/1/2 = %s  equipement = %d  animations = %s" % (
            p["nom"], p["hauteur"], "/".join(map(str, p["tri"])), sum(p["equip"].values()),
            ", ".join(a.name.split(".")[0].replace("ANIM_", "") for a in p["actions"])))
    print("\n".join(bilan))

    if sortie:
        os.makedirs(sortie, exist_ok=True)
        with open(os.path.join(sortie, "bilan.txt"), "w", encoding="utf-8") as f:
            f.write("\n".join(bilan) + "\n")
            if POSE_T:
                f.write("Pose de repos des SK_ : en T (bras a l'horizontale), animations identiques.\n")
        for p in persos_s + persos_z:
            preparer_nla(p)
            exporter_fbx(p, sortie)
            for tr in p["arm"].animation_data.nla_tracks:
                tr.mute = True

    if sortie and rendu:
        masquer_lods(persos_s + persos_z)
        tous = persos_s + persos_z

        def montrer(liste):
            for p in tous:
                for o in p["objets"]:
                    o.hide_render = p not in liste
                if p in liste:
                    masquer_lods([p])

        # 1. Survivants au repos, pré-équipés
        montrer(persos_s)
        for p in persos_s:
            assigner_action(p["arm"], p["actions"][0], 20)
        sc = scene_rendu(1900, 760, persos_s[-1]["arm"].location.x / 2, 4.6, cam_y=-6, cam_z=2.2)
        bpy.context.scene.frame_set(20)
        rendre(os.path.join(sortie, "apercu_survivants.png"), sc)
        # 2. Zombies au repos
        montrer(persos_z)
        for p in persos_z:
            assigner_action(p["arm"], p["actions"][0], 20)
        cx = (persos_z[0]["arm"].location.x + persos_z[-1]["arm"].location.x) / 2
        sc = scene_rendu(1800, 860, cx, 5.0, cam_y=-3, cam_z=2.2, cible_z=.6)
        cam = sc.camera
        cam.location = (cx + 3.2, 3.0 - 5.2, 2.4)
        d = Vector((cx, 3.0, .65)) - cam.location
        cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
        bpy.context.scene.frame_set(20)
        rendre(os.path.join(sortie, "apercu_zombies.png"), sc)
        # 3. Planche d'animations : marche, ramasser des cailloux, piocher
        par_nom = {p["nom"]: p for p in persos_s}
        lignes = [("Survivant_Ouvrier", "ANIM_Humain_Marche", (1, 7, 13, 19)),
                  ("Survivant_Ouvrier", "ANIM_Humain_RamasserCailloux", (1, 4, 7, 10)),
                  ("Survivant_Carrier", "ANIM_Humain_Piocher", (1, 10, 18, 22))]
        chemins = []
        for nom, anim, images in lignes:
            perso = par_nom[nom]
            montrer([perso])
            if "Ramasser" in anim:  # le jeu range l'outil pendant la récolte de cailloux
                for o in perso["objets"]:
                    if o.parent_bone == "SOCKET_MainDroite":
                        o.hide_render = True
            act = next(a for a in perso["actions"] if a.name.split(".")[0] == anim)
            assigner_action(perso["arm"], act, 1)
            x = perso["arm"].location.x
            sc = scene_rendu(480, 560, x, 1.5, cam_y=-5, cam_z=2.0, cible_z=.45)
            sc.camera.location.x = x + 3.0
            if "Piocher" in anim:  # de profil : le geste se lit mieux
                sc.camera.location = (x + 5.0, -1.2, 2.0)
            d = Vector((x, 0, .45)) - sc.camera.location
            sc.camera.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
            for k, image in enumerate(images):
                sc.frame_set(image)
                ch = os.path.join(sortie, "_image_%s_%d.png" % (anim, k))
                rendre(ch, sc)
                chemins.append(ch)
        planche(chemins, 4, os.path.join(sortie, "apercu_animations.png"))
        for ch in chemins:
            os.remove(ch)
        montrer(tous)

    if sortie:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(sortie, "AfterCity_Personnages.blend"))
    return persos_s, persos_z


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
    _sortie, _rendu = _args()
    principal(_sortie, _rendu)
