"""
After City — bibliothèque d'équipement des survivants (SM_Equip_*), dans Blender.

Chaque objet est un maillage fixe (SM_) qui s'accroche à une accroche du squelette commun
RIG_Humain : SOCKET_Tete, SOCKET_Torse, SOCKET_Dos, SOCKET_Ceinture, SOCKET_MainDroite,
SOCKET_MainGauche (00_conventions.md § 4, 01_survivants.md § 6). Il réutilise la palette,
le matériau et le constructeur de generer_personnages.py, dans le même dossier.

Pour chaque objet, le script exporte un FBX qui contient le squelette RIG_Humain (sans corps
ni animation) et l'objet, avec ses LOD0/1/2, enfant de son os d'accroche : dans Unity, la
position et la rotation locales de l'objet sous son accroche sont donc exactes, et il suffit
de copier l'objet sous la même accroche d'un personnage (même squelette).

Utilisation :
  blender --background --python generer_equipement.py -- --sortie CHEMIN [--sans-rendu]
  (ou python3 avec le module bpy installé)
Sorties : SM_Equip_<Emplacement>_<Nom>.fbx (CONVENTIONS.md), catalogue_equipement.json, bilan_equipement.txt,
AfterCity_Equipement.blend, apercu_equipement.png, apercu_survivants_equipes.png.
"""

import json
import math
import os
import sys

import bpy  # noqa: I001
import bmesh
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import generer_personnages as gp  # noqa: E402

Constructeur = gp.Constructeur
CORPS = {"A": gp.CORPS_A, "B": gp.CORPS_B}


# ---------------------------------------------------------------------------
# Objets portés sur le corps : géométrie dans l'espace du personnage
# (regarde vers -Y, pieds en z = 0), puis recentrée sur l'accroche.
# Signature : f(c, p, r, os) ; os = nom de l'accroche (pondération provisoire).
# ---------------------------------------------------------------------------
def _zc(p, r):
    return r["z_tete_bas"] + p["head_h"] * .46


# --- Tête --------------------------------------------------------------------
def casque_militaire(c, p, r, o, couleur="olive"):
    zc = _zc(p, r)
    c.sphere((0, .01, zc + .045), p["head_w"] * .66, couleur, o, u=12, v=6, demi=True, echelle=(1, 1.06, .88))
    c.cylindre((0, .01, zc + .05), p["head_w"] * .68, .025, couleur, o, segments=12)
    for sx in (1, -1):  # jugulaire
        c.boite((sx * p["head_w"] * .52, -.01, zc - .02), (.012, .02, .12), "semelle", o, biseau=0)
    c.boite((0, -p["head_d"] * .48, zc + .1), (.07, .03, .05), "plastique_noir", o, biseau=.008)  # lampe


def casque_police(c, p, r, o):
    casque_militaire(c, p, r, o, couleur="bleu_police")
    zc = _zc(p, r)
    c.boite((0, -p["head_d"] * .52, zc + .03), (p["head_w"] * .95, .02, .1), "denim_delave", o, biseau=.008,
            rot=(-12, 0, 0))


def casque_chantier(c, p, r, o):
    zc = _zc(p, r)
    c.sphere((0, .01, zc + .06), p["head_w"] * .62, "plastique_jaune", o, u=10, v=5, demi=True, echelle=(1, 1.05, .95))
    c.cylindre((0, -.005, zc + .06), p["head_w"] * .74, .018, "plastique_jaune", o, segments=12)
    c.boite((0, .01, zc + .17), (.03, p["head_d"] * .9, .025), "plastique_jaune", o, biseau=.008)  # arête
    c.boite((0, -p["head_d"] * .5, zc + .13), (.05, .012, .03), "adhesif", o, biseau=0)  # autocollant


def casque_moto(c, p, r, o):
    zc = _zc(p, r)
    c.sphere((0, .005, zc + .02), p["head_w"] * .74, "flanelle_rouge", o, u=12, v=7, echelle=(1, 1.05, 1.0))
    c.boite((0, -p["head_d"] * .55, zc + .01), (p["head_w"] * .8, .04, .1), "plastique_noir", o, biseau=.02)
    c.boite((p["head_w"] * .3, -p["head_d"] * .2, zc + .12), (.06, .05, .03), "adhesif", o, biseau=0)  # réparation


def casquette(c, p, r, o):
    zc = _zc(p, r)
    c.sphere((0, .01, zc + .07), p["head_w"] * .57, "denim", o, u=10, v=5, demi=True, echelle=(1, 1.02, .75))
    c.boite((0, -p["head_d"] / 2 - .05, zc + .075), (p["head_w"] * .75, .12, .015), "denim", o, biseau=.006,
            rot=(-8, 0, 0))
    c.boite((0, -p["head_d"] * .44, zc + .12), (.05, .012, .035), "orange_hv", o, biseau=0)  # logo délavé


def bonnet(c, p, r, o):
    zc = _zc(p, r)
    c.sphere((0, .01, zc + .06), p["head_w"] * .59, "orange_chasse", o, u=10, v=5, demi=True)
    c.cylindre((0, .01, zc + .055), p["head_w"] * .6, .045, "orange_chasse", o, segments=10)
    c.sphere((0, .01, zc + .2), .035, "orange_chasse", o, u=6, v=4)  # pompon


def chapeau_pisteur(c, p, r, o):
    zc = _zc(p, r)
    c.cylindre((0, 0, zc + .1), p["head_w"] * .98, .015, "brun_chasse", o, segments=12)
    c.cylindre((0, 0, zc + .155), p["head_w"] * .5, .1, "brun_chasse", o, segments=10, rayon_haut=p["head_w"] * .43)
    c.cylindre((0, 0, zc + .12), p["head_w"] * .52, .025, "cuir", o, segments=10)
    c.boite((p["head_w"] * .5, .02, zc + .16), (.01, .03, .1), "z_os", o, biseau=0, rot=(0, -20, 0))  # plume


def bandana(c, p, r, o):
    zc = _zc(p, r)
    c.boite((0, .005, zc + .07), (p["head_w"] + .025, p["head_d"] + .025, .07), "bandana", o, biseau=.03)
    c.boite((0, p["head_d"] / 2 + .03, zc + .05), (.04, .05, .07), "bandana", o, biseau=.01, rot=(30, 0, 0))
    c.boite((.02, p["head_d"] / 2 + .05, zc - .01), (.03, .02, .09), "bandana", o, biseau=0, rot=(10, 0, 15))


def masque_gaz(c, p, r, o):
    zc = _zc(p, r)
    c.boite((0, -p["head_d"] * .5, zc - .04), (p["head_w"] * .7, .06, .12), "plastique_noir", o, biseau=.025)
    c.cylindre((0, -p["head_d"] * .5 - .06, zc - .07), .035, .06, "acier_sombre", o, segments=8, rot=(90, 0, 0))
    for sx in (1, -1):
        c.cylindre((sx * p["head_w"] * .17, -p["head_d"] * .5 - .025, zc + .01), .028, .012, "denim_delave", o,
                   segments=8, rot=(90, 0, 0))
    c.boite((0, .02, zc + .0), (p["head_w"] + .02, p["head_d"] + .01, .025), "plastique_noir", o, biseau=0)  # sangle


# --- Torse -------------------------------------------------------------------
def _gilet(c, p, r, o, couleur, poches=True, plaque=None):
    zt, zh = r["z_epaule_haut"], r["z_hanche"]
    h = zt - zh - .05
    zm = zh + .07 + h / 2
    c.boite((0, 0, zm), (p["shoulder_w"] * .88, p["torso_d"] * 1.18, h), couleur, o, biseau=.03,
            effile=(1.08, 1.0))
    for sx in (1, -1):  # bretelles
        c.boite((sx * p["shoulder_w"] * .26, 0, zt + .005), (.07, p["torso_d"] * 1.15, .03), couleur, o, biseau=.01)
    if poches:
        for k in (-1, 0, 1):
            c.boite((k * .075, -p["torso_d"] * .62, zm - .04), (.06, .035, .075), couleur, o, biseau=0)
            c.boite((k * .075, -p["torso_d"] * .64, zm + .005), (.064, .037, .014), "semelle", o, biseau=0)
    if plaque:
        c.boite((0, -p["torso_d"] * .6, zm + .07), (.11, .012, .03), plaque, o, biseau=0)


def gilet_tactique(c, p, r, o):
    _gilet(c, p, r, o, "olive")


def gilet_police(c, p, r, o):
    _gilet(c, p, r, o, "bleu_police", poches=False, plaque="blanc_casse")
    zh = r["z_hanche"]
    c.boite((-.06, -p["torso_d"] * .62, zh + .12), (.05, .03, .06), "bleu_police", o, biseau=.008)


def plastron_bricole(c, p, r, o):
    zt, zh = r["z_epaule_haut"], r["z_hanche"]
    for k, (z, l) in enumerate(((zt - .07, .25), (zt - .15, .23), (zh + .16, .2))):
        c.boite((0, -p["torso_d"] * .58, z), (l, .025, .075), "acier" if k != 1 else "rouille", o, biseau=.008,
                rot=(0, 0, 3 * (k - 1)))
    for sx in (1, -1):
        c.boite((sx * .1, -p["torso_d"] * .6, zt - .15), (.025, .03, .2), "adhesif", o, biseau=0)
        c.boite((sx * p["shoulder_w"] * .26, 0, zt + .005), (.04, p["torso_d"] * 1.15, .025), "adhesif", o, biseau=0)
    c.boite((0, p["torso_d"] * .55, zt - .12), (p["shoulder_w"] * .6, .02, .03), "adhesif", o, biseau=0)


def gilet_haute_visibilite(c, p, r, o):
    _gilet(c, p, r, o, "orange_hv", poches=False)
    zt, zh = r["z_epaule_haut"], r["z_hanche"]
    for z in (zh + .12, zh + .2):
        c.boite((0, 0, z), (p["shoulder_w"] * .92, p["torso_d"] * 1.22, .022), "blanc_casse", o, biseau=0)


def brassard_medecin(c, p, r, o):
    x = p["shoulder_w"] / 2 + p["arm_t"] / 2 - .025
    z = r["z_epaule"] - r["seg_bras"] * .5
    c.cylindre((x, 0, z), p["arm_t"] * .62, .045, "sarcelle", o, segments=8)
    c.cylindre((x + p["arm_t"] * .6, 0, z), .016, .01, "blanc_casse", o, segments=8, rot=(0, 90, 0))


# --- Dos ---------------------------------------------------------------------
def _yd(p):
    return p["torso_d"] / 2


def sac_randonnee(c, p, r, o):
    z = r["z_epaule_haut"] - p["torso_h"] * .4
    y = _yd(p) + .065
    c.boite((0, y, z), (p["shoulder_w"] * .66, .13, p["torso_h"] * .75), "toile_sac", o, biseau=.035)
    c.boite((0, y + .075, z - .05), (p["shoulder_w"] * .5, .035, .12), "toile_sac", o, biseau=.015)  # poche
    c.boite((0, y + .02, z + p["torso_h"] * .42), (p["shoulder_w"] * .55, .1, .05), "denim", o, biseau=.02)  # rabat
    c.cylindre((0, y - .01, z + p["torso_h"] * .5), .05, p["shoulder_w"] * .75, "bache_bleue", o, segments=8,
               rot=(0, 90, 0))  # tapis de sol
    for sx in (1, -1):
        c.boite((sx * p["shoulder_w"] * .36, y, z + .02), (.03, .08, .15), "toile_sac", o, biseau=.01)  # poches latérales
        c.boite((sx * p["shoulder_w"] * .22, -p["torso_d"] * .52, z + .04), (.028, .012, .18), "cuir", o, biseau=0)
    c.cylindre((p["shoulder_w"] * .3, y + .04, z - .12), .03, .07, "metal_gourde", o, segments=6)  # quart


def sac_petit(c, p, r, o):
    z = r["z_epaule_haut"] - p["torso_h"] * .45
    y = _yd(p) + .045
    c.boite((0, y, z), (p["shoulder_w"] * .5, .09, p["torso_h"] * .5), "flanelle_verte", o, biseau=.03)
    c.boite((0, y + .05, z - .02), (p["shoulder_w"] * .36, .02, .08), "flanelle_verte", o, biseau=.01)
    for sx in (1, -1):
        c.boite((sx * p["shoulder_w"] * .2, -p["torso_d"] * .52, z + .04), (.025, .012, .16), "semelle", o, biseau=0)


def fusil_dos(c, p, r, o):
    z = r["z_epaule_haut"] - p["torso_h"] * .45
    y = _yd(p) + .04
    rot = (0, 35, 0)
    c.boite((0, y, z), (.035, .03, .5), "acier_sombre", o, biseau=.006, rot=rot)
    c.boite((.07, y, z - .1), (.045, .04, .16), "bois_sombre", o, biseau=.01, rot=rot)
    c.boite((-.02, y - .005, z + .05), (.03, .035, .08), "bois_sombre", o, biseau=.006, rot=rot)
    c.boite((0, -p["torso_d"] * .2, z + .02), (.02, p["torso_d"] * 1.2, .02), "cuir", o, biseau=0, rot=(0, -40, 0))


def arbalete_dos(c, p, r, o):
    z = r["z_epaule_haut"] - p["torso_h"] * .35
    y = _yd(p) + .04
    c.boite((0, y, z), (.04, .035, .32), "bois_sombre", o, biseau=.008, rot=(0, 20, 0))
    c.boite((-.04, y + .01, z + .12), (.34, .02, .025), "acier_sombre", o, biseau=0, rot=(0, 20, 0))
    c.boite((.04, y + .05, z - .05), (.06, .05, .16), "cuir", o, biseau=.01)  # carquois
    for k in range(3):
        c.boite((.025 + k * .015, y + .05, z + .06), (.008, .008, .06), "flanelle_rouge", o, biseau=0)  # empennages


def hotte_bois(c, p, r, o):
    z = r["z_epaule_haut"] - p["torso_h"] * .45
    y = _yd(p) + .07
    for sx in (1, -1):
        c.boite((sx * .1, y, z), (.025, .025, .42), "bois_manche", o, biseau=0)
    c.boite((0, y + .03, z - .2), (.24, .1, .025), "bois_manche", o, biseau=0)
    for k in range(4):  # bûches
        c.cylindre((-.07 + k * .045, y + .05, z - .05 + (k % 2) * .05), .025, .2, "bois_sombre" if k % 2 else "bois_manche",
                   o, segments=6)
    for sx in (1, -1):
        c.boite((sx * p["shoulder_w"] * .22, -p["torso_d"] * .52, z + .04), (.028, .012, .18), "cuir", o, biseau=0)


def jerrican_dos(c, p, r, o):
    z = r["z_epaule_haut"] - p["torso_h"] * .45
    y = _yd(p) + .06
    c.boite((0, y, z), (.2, .1, .26), "rouille", o, biseau=.02)
    c.boite((0, y, z + .15), (.12, .03, .04), "rouille", o, biseau=.008)
    c.cylindre((.07, y, z + .15), .018, .04, "plastique_noir", o, segments=6)
    for sx in (1, -1):
        c.boite((sx * p["shoulder_w"] * .22, -p["torso_d"] * .52, z + .04), (.028, .012, .18), "semelle", o, biseau=0)


# --- Ceinture ----------------------------------------------------------------
def _ceinture(c, p, r, o, couleur="semelle"):
    zh = r["z_hanche"] + .035
    c.boite((0, 0, zh), (p["hip_w"] * 1.08, p["torso_d"] * 1.02, .035), couleur, o, biseau=.008)
    c.boite((0, -p["torso_d"] * .52, zh), (.045, .012, .032), "acier", o, biseau=0)
    return zh


def ceinture_outils(c, p, r, o):
    zh = _ceinture(c, p, r, o, "cuir")
    x = p["hip_w"] * .5
    for sx in (1, -1):
        c.boite((sx * x, -.02, zh - .05), (.03, .09, .1), "cuir_clair", o, biseau=.01)  # poches
    c.boite((-x - .02, .01, zh - .08), (.018, .018, .18), "bois_manche", o, biseau=0)  # marteau
    c.boite((-x - .02, .01, zh + .01), (.03, .07, .03), "acier_sombre", o, biseau=0)
    c.boite((x + .02, -.03, zh - .03), (.012, .012, .12), "acier", o, biseau=0)  # tournevis
    c.boite((x + .02, .03, zh - .02), (.015, .015, .1), "plastique_jaune", o, biseau=0)  # mètre ruban
    c.cylindre((p["hip_w"] * .3, p["torso_d"] * .5, zh - .02), .035, .03, "plastique_jaune", o, segments=8,
               rot=(90, 0, 0))


def ceinture_tactique(c, p, r, o):
    zh = _ceinture(c, p, r, o, "olive")
    for k in (-1, 1):
        c.boite((k * .07, -p["torso_d"] * .55, zh - .025), (.05, .03, .06), "olive", o, biseau=.006)
    c.boite((-p["hip_w"] * .55, 0, zh - .07), (.04, .06, .13), "plastique_noir", o, biseau=.008)  # étui
    c.boite((-p["hip_w"] * .58, 0, zh - .01), (.03, .045, .05), "acier_sombre", o, biseau=0)  # crosse
    c.cylindre((p["hip_w"] * .55, .02, zh - .05), .03, .1, "metal_gourde", o, segments=6)  # gourde


def ceinture_survie(c, p, r, o):
    zh = _ceinture(c, p, r, o)
    c.cylindre((p["hip_w"] * .55, .02, zh - .05), .032, .11, "metal_gourde", o, segments=6)
    c.cylindre((p["hip_w"] * .55, .02, zh + .015), .015, .02, "plastique_noir", o, segments=6)
    c.boite((-p["hip_w"] * .55, 0, zh - .07), (.02, .04, .13), "cuir", o, biseau=0)  # étui de couteau
    c.boite((-p["hip_w"] * .55, 0, zh + .01), (.018, .02, .04), "bois_sombre", o, biseau=0)
    c.boite((0, p["torso_d"] * .55, zh - .03), (.12, .04, .07), "toile_beige", o, biseau=.01)  # pochette


def ceinture_medicale(c, p, r, o):
    zh = _ceinture(c, p, r, o)
    c.boite((p["hip_w"] * .45, -.03, zh - .05), (.05, .11, .1), "blanc_casse", o, biseau=.015)
    c.cylindre((p["hip_w"] * .45 + .026, -.03, zh - .05), .018, .005, "sarcelle", o, segments=8, rot=(0, 90, 0))
    for k in range(3):  # rouleaux de bandage
        c.cylindre((-p["hip_w"] * .3 + k * .03, -p["torso_d"] * .55, zh - .03), .014, .04, "bandage", o, segments=6)


# ---------------------------------------------------------------------------
# Objets tenus en main : origine = point de prise, manche le long de +Z,
# lame/tête vers -Y. L'accroche les tourne de « rotation_prise » degrés autour de X.
# ---------------------------------------------------------------------------
def hache(c, o):
    c.boite((0, 0, .08), (.025, .028, .42), "bois_manche", o, biseau=.007, effile=(.9, .9))
    c.boite((0, -.055, .26), (.03, .13, .09), "acier", o, biseau=.008, effile=(1, 1.3))
    c.boite((0, -.12, .26), (.032, .012, .12), "acier_sombre", o, biseau=0)  # fil
    c.boite((0, .025, .26), (.034, .03, .05), "acier_sombre", o, biseau=0)  # talon
    c.boite((0, 0, -.1), (.032, .034, .04), "adhesif", o, biseau=0)  # poignée scotchée


def pioche(c, o):
    c.boite((0, 0, .08), (.025, .025, .44), "bois_manche", o, biseau=.006)
    c.boite((0, 0, .28), (.028, .32, .035), "acier_sombre", o, biseau=.008, effile=(1, .3))
    c.boite((0, 0, .28), (.042, .05, .05), "rouille", o, biseau=.006)


def pelle(c, o):
    c.boite((0, 0, .1), (.022, .022, .44), "bois_manche", o, biseau=.006)
    c.boite((0, 0, .36), (.11, .02, .13), "acier", o, biseau=.01, effile=(.7, 1))
    c.boite((0, -.012, .38), (.08, .008, .06), "terre_visage", o, biseau=0)  # terre collée
    c.boite((0, 0, -.13), (.08, .025, .025), "bois_manche", o, biseau=.006)


def marteau(c, o):
    c.boite((0, 0, .06), (.022, .022, .26), "bois_manche", o, biseau=.005)
    c.boite((0, -.02, .19), (.036, .11, .036), "acier_sombre", o, biseau=.008)
    c.boite((0, .045, .2), (.03, .03, .02), "acier_sombre", o, biseau=0, rot=(30, 0, 0))  # arrache‑clou


def houe(c, o):
    c.boite((0, 0, .1), (.022, .022, .46), "bois_manche", o, biseau=.006)
    c.boite((0, -.06, .32), (.1, .1, .02), "acier", o, biseau=.006, rot=(25, 0, 0))


def batte_cloutee(c, o):
    c.cylindre((0, 0, .1), .022, .38, "bois_manche", o, segments=8, rayon_haut=.042)
    c.cylindre((0, 0, -.07), .026, .04, "adhesif", o, segments=8)
    for i in range(6):
        a = i * 60
        c.boite((.042 * math.cos(math.radians(a)), .042 * math.sin(math.radians(a)), .2 + (i % 3) * .035),
                (.035, .007, .007), "acier", o, biseau=0, rot=(0, 0, a))


def machette(c, o):
    c.boite((0, 0, -.02), (.024, .03, .1), "plastique_noir", o, biseau=.006)
    c.boite((0, -.005, .17), (.008, .055, .28), "acier", o, biseau=0, effile=(1, 1.3))
    c.boite((0, 0, .04), (.03, .06, .015), "acier_sombre", o, biseau=0)


def pied_de_biche(c, o):
    c.boite((0, 0, .1), (.02, .02, .4), "flanelle_rouge", o, biseau=.004)
    c.boite((0, -.03, .3), (.018, .07, .018), "flanelle_rouge", o, biseau=0, rot=(40, 0, 0))
    c.boite((0, -.025, -.11), (.016, .05, .016), "flanelle_rouge", o, biseau=0, rot=(-20, 0, 0))


def fusil(c, o):
    c.boite((0, -.12, .02), (.035, .46, .04), "acier_sombre", o, biseau=.006)
    c.boite((0, .12, -.01), (.04, .16, .07), "bois_sombre", o, biseau=.01, effile=(1, 1.2))
    c.boite((0, -.06, -.02), (.035, .14, .035), "bois_sombre", o, biseau=.006)  # garde‑main
    c.cylindre((0, -.02, .065), .018, .12, "plastique_noir", o, segments=6, rot=(90, 0, 0))  # lunette
    c.boite((0, .02, -.05), (.012, .025, .04), "acier_sombre", o, biseau=0)  # pontet


def arbalete(c, o):
    c.boite((0, -.05, .01), (.04, .3, .045), "bois_sombre", o, biseau=.008)
    c.boite((0, -.19, .03), (.34, .025, .025), "acier_sombre", o, biseau=0, effile=(1, 1))
    c.boite((0, -.12, .035), (.3, .004, .004), "toile_beige", o, biseau=0)  # corde
    c.boite((0, -.08, .05), (.008, .2, .008), "acier", o, biseau=0)  # carreau


def seau(c, o):
    c.cylindre((0, 0, -.08), .05, .09, "acier", o, segments=10, rayon_haut=.06)
    c.boite((0, 0, -.01), (.11, .006, .006), "acier_sombre", o, biseau=0)


def sac_medical(c, o):
    c.boite((0, 0, -.06), (.07, .16, .11), "blanc_casse", o, biseau=.025)
    c.boite((0, 0, .02), (.02, .06, .05), "cuir", o, biseau=.008)
    for sx in (1, -1):
        c.cylindre((sx * .036, 0, -.06), .028, .006, "sarcelle", o, segments=10, rot=(0, 90, 0))


def lampe_torche(c, o):
    c.cylindre((0, 0, 0), .018, .13, "plastique_noir", o, segments=8, rot=(90, 0, 0))
    c.cylindre((0, -.08, 0), .028, .04, "plastique_noir", o, segments=8, rot=(90, 0, 0))
    c.cylindre((0, -.101, 0), .024, .004, "tshirt_creme", o, segments=8, rot=(90, 0, 0))


def bouclier_panneau(c, o):
    c.cylindre((0, -.03, 0), .17, .015, "flanelle_rouge", o, segments=8, rot=(90, 22.5, 0))  # panneau octogonal
    c.cylindre((0, -.04, 0), .13, .004, "blanc_casse", o, segments=8, rot=(90, 22.5, 0))
    c.boite((0, -.045, 0), (.18, .004, .05), "flanelle_rouge", o, biseau=0)
    c.boite((0, 0, 0), (.03, .04, .12), "adhesif", o, biseau=0)  # poignée


# ---------------------------------------------------------------------------
# Catalogue : nom -> (emplacement, constructeur, angle de prise, rôles, description)
# ---------------------------------------------------------------------------
TETE, TORSE, DOS, CEINT, MD, MG = "Tete", "Torse", "Dos", "Ceinture", "MainDroite", "MainGauche"
CATALOGUE = {
    "Casque_Militaire":    (TETE, casque_militaire, None, ["Militaire"], "Casque olive, jugulaire, lampe frontale"),
    "Casque_Police":      (TETE, casque_police, None, ["Militaire", "Milicien"], "Casque anti‑émeute récupéré, visière (ère 2)"),
    "Casque_Chantier":    (TETE, casque_chantier, None, ["Ouvrier", "Carrier"], "Casque jaune de chantier"),
    "Casque_Moto":        (TETE, casque_moto, None, ["Milicien"], "Casque de moto réparé au scotch"),
    "Casquette":          (TETE, casquette, None, ["Ouvrier", "Nettoyeur"], "Casquette en denim délavé"),
    "Bonnet":             (TETE, bonnet, None, ["Pisteur", "Nettoyeur"], "Bonnet orange de chasse à pompon"),
    "Chapeau_Pisteur":    (TETE, chapeau_pisteur, None, ["Pisteur"], "Chapeau de brousse, plume"),
    "Bandana":            (TETE, bandana, None, ["Milicien"], "Bandana rouge noué"),
    "Masque_Gaz":         (TETE, masque_gaz, None, ["Nettoyeur", "Militaire"], "Masque à gaz (nettoyage des cadavres)"),
    "Gilet_Tactique":     (TORSE, gilet_tactique, None, ["Militaire"], "Gilet porte‑plaques olive à poches"),
    "Gilet_Police":       (TORSE, gilet_police, None, ["Militaire", "Milicien"], "Gilet pare‑balles de police (ère 2)"),
    "Plastron_Bricole":   (TORSE, plastron_bricole, None, ["Milicien"], "Tôles et scotch : armure de fortune"),
    "Gilet_HauteVisibilite": (TORSE, gilet_haute_visibilite, None, ["Ouvrier", "Nettoyeur"], "Gilet orange à bandes"),
    "Brassard_Medecin":   (TORSE, brassard_medecin, None, ["Medecin"], "Brassard sarcelle, cercle blanc (pas de croix rouge)"),
    "Sac_Randonnee":      (DOS, sac_randonnee, None, ["Pisteur", "Ouvrier"], "Gros sac de randonnée, tapis de sol bleu"),
    "Sac_Petit":          (DOS, sac_petit, None, ["Medecin", "Milicien"], "Petit sac à dos d'écolier"),
    "Fusil_Dos":          (DOS, fusil_dos, None, ["Militaire"], "Fusil en bandoulière"),
    "Arbalete_Dos":       (DOS, arbalete_dos, None, ["Pisteur"], "Arbalète et carquois"),
    "Hotte_Bois":         (DOS, hotte_bois, None, ["Ouvrier"], "Hotte à bûches (porte 4 bûches)"),
    "Jerrican_Dos":       (DOS, jerrican_dos, None, ["Ouvrier", "Militaire"], "Jerrican de carburant"),
    "Ceinture_Outils":    (CEINT, ceinture_outils, None, ["Ouvrier", "Carrier"], "Ceinture porte‑outils, marteau, mètre"),
    "Ceinture_Tactique":  (CEINT, ceinture_tactique, None, ["Militaire"], "Porte‑chargeurs, étui, gourde"),
    "Ceinture_Survie":    (CEINT, ceinture_survie, None, ["Pisteur", "Milicien", "Nettoyeur"], "Gourde, couteau, pochette"),
    "Ceinture_Medicale":  (CEINT, ceinture_medicale, None, ["Medecin"], "Trousse et bandages"),
    "Hache":              (MD, hache, 80, ["Ouvrier"], "Couper du bois"),
    "Pioche":             (MD, pioche, 80, ["Carrier"], "Tailler la pierre"),
    "Pelle":              (MD, pelle, 80, ["Ouvrier", "Carrier"], "Creuser, remblayer"),
    "Marteau":            (MD, marteau, 80, ["Ouvrier"], "Construire, réparer"),
    "Houe":               (MD, houe, 80, ["Ouvrier"], "Potager, champs"),
    "Batte_Cloutee":      (MD, batte_cloutee, 80, ["Milicien"], "Arme improvisée"),
    "Machette":           (MD, machette, 80, ["Milicien", "Pisteur"], "Arme et débroussaillage"),
    "Pied_De_Biche":      (MD, pied_de_biche, 80, ["Nettoyeur", "Milicien"], "Démonter, ouvrir, frapper"),
    "Fusil":              (MD, fusil, 0, ["Militaire"], "Fusil tenu en main"),
    "Arbalete":           (MD, arbalete, 0, ["Pisteur"], "Arbalète tenue en main"),
    "Seau":               (MD, seau, 0, ["Ouvrier"], "Puits, eau"),
    "Sac_Medical":        (MG, sac_medical, 0, ["Medecin"], "Sac médical"),
    "Lampe_Torche":       (MG, lampe_torche, 0, ["Pisteur", "Militaire"], "Lampe torche (nuit)"),
    "Bouclier_Panneau":   (MG, bouclier_panneau, 0, ["Milicien"], "Bouclier fait d'un panneau STOP"),
}

# Équipement de départ par rôle (Sam : chaque survivant pré‑équipé)
DOTATIONS = {
    "Ouvrier":   {TETE: "Casquette", TORSE: "Gilet_HauteVisibilite", DOS: "Hotte_Bois", CEINT: "Ceinture_Outils", MD: "Hache"},
    "Carrier":   {TETE: "Casque_Chantier", CEINT: "Ceinture_Outils", MD: "Pioche"},
    "Milicien":  {TETE: "Bandana", TORSE: "Plastron_Bricole", CEINT: "Ceinture_Survie", MD: "Batte_Cloutee", MG: "Bouclier_Panneau"},
    "Militaire": {TETE: "Casque_Militaire", TORSE: "Gilet_Tactique", CEINT: "Ceinture_Tactique", MD: "Fusil"},
    "Pisteur":   {TETE: "Chapeau_Pisteur", DOS: "Arbalete_Dos", CEINT: "Ceinture_Survie", MD: "Machette", MG: "Lampe_Torche"},
    "Medecin":   {TORSE: "Brassard_Medecin", DOS: "Sac_Petit", CEINT: "Ceinture_Medicale", MG: "Sac_Medical"},
    "Nettoyeur": {TETE: "Masque_Gaz", TORSE: "Gilet_HauteVisibilite", CEINT: "Ceinture_Survie", MD: "Pied_De_Biche"},
}



# ---------------------------------------------------------------------------
def nom_fichier(nom):
    """CONVENTIONS.md : SM_Equip_<Emplacement>_<Nom>, ex. SM_Equip_Tete_CasqueChantier."""
    return "SM_Equip_%s_%s" % (CATALOGUE[nom][0], nom.replace("_", ""))


def construire(nom, mat, p):
    emplacement, f, angle, _, _ = CATALOGUE[nom]
    socket = gp.EMPLACEMENTS[emplacement]
    r = gp.reperes(p)
    c = Constructeur(hauteur=.5 if emplacement in (MD, MG) else r["z_tete_haut"], salete=.25)
    if emplacement in (MD, MG):
        f(c, socket)
    else:
        f(c, p, r, socket)
        pos = {n: Vector(v) for n, v, _ in gp.socket_positions(p, r)}[socket]
        bmesh.ops.translate(c.bm, vec=-pos, verts=c.bm.verts)
    ob = c.vers_objet(nom_fichier(nom) + "_LOD0", mat)
    for vg in list(ob.vertex_groups):
        ob.vertex_groups.remove(vg)
    pos = {n: Vector(v) for n, v, _ in gp.socket_positions(p, r)}[socket]
    ob["position"] = list(pos)
    ob["rotation_prise"] = angle or 0
    ob["emplacement"] = emplacement
    return ob


def lods(ob, collection):
    res = [ob]
    for i, ratio in ((1, .5), (2, .25)):
        lod = bpy.data.objects.new(ob.name.replace("_LOD0", "_LOD%d" % i), ob.data.copy())
        collection.objects.link(lod)
        for k in ("position", "rotation_prise", "emplacement"):
            lod[k] = ob[k]
        if gp.nb_triangles(ob) > 40:
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
    bpy.ops.export_scene.fbx(
        filepath=chemin, use_selection=True, object_types={"ARMATURE", "MESH"},
        axis_forward="-Z", axis_up="Y", apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
        bake_space_transform=False, mesh_smooth_type="FACE", use_mesh_modifiers=False,
        add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
        armature_nodetype="NULL", bake_anim=False, colors_type="SRGB", path_mode="AUTO")


def principal(sortie=None, rendu=True):
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    racine = bpy.context.scene.collection
    img = gp.creer_palette(None)  # même palette que les personnages (modeles3d/sorties/T_Palette_256.png)
    mat = gp.creer_materiau(img)
    col = bpy.data.collections.new("Equipement")
    racine.children.link(col)
    p = gp.CORPS_A
    catalogue, bilan, par_nom = {}, [], {}
    for i, nom in enumerate(CATALOGUE):
        emplacement, _, _, roles, desc = CATALOGUE[nom]
        socket = gp.EMPLACEMENTS[emplacement]
        sous = bpy.data.collections.new(nom)
        col.children.link(sous)
        arm = gp.creer_rig("RIG_Humain", p, sous)
        arm.name = "RIG_Equip_" + nom
        ob = construire(nom, mat, p)
        sous.objects.link(ob)
        les_lods = lods(ob, sous)
        for o in les_lods:
            gp.accrocher(o, arm, socket)
        tri = [gp.nb_triangles(o) for o in les_lods]
        par_nom[nom] = dict(arm=arm, lods=les_lods, col=sous)
        catalogue[nom] = dict(fichier=nom_fichier(nom) + ".fbx", emplacement=emplacement, accroche=socket,
                              triangles=tri, roles=roles, description=desc)
        bilan.append("%-36s %-18s triangles LOD0/1/2 = %s" % (nom_fichier(nom), socket, "/".join(map(str, tri))))
        if sortie:
            arm.location.x = 0
            exporter([arm] + les_lods, os.path.join(sortie, nom_fichier(nom) + ".fbx"))
        arm.location = ((i % 10) * .7, (i // 10) * 1.2, 0)
    print("\n".join(bilan))
    if sortie:
        os.makedirs(sortie, exist_ok=True)
        with open(os.path.join(sortie, "bilan_equipement.txt"), "w", encoding="utf-8") as fb:
            fb.write("\n".join(bilan) + "\n")
        with open(os.path.join(sortie, "catalogue_equipement.json"), "w", encoding="utf-8") as fj:
            json.dump({"version": "0.1", "squelette": "RIG_Humain (corps A ; corps B : mêmes accroches à 2 cm près)",
                       "objets": catalogue, "dotations": DOTATIONS}, fj, ensure_ascii=False, indent=2)
    if sortie and rendu:
        rendus(sortie, par_nom, mat, col)
    if sortie:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(sortie, "AfterCity_Equipement.blend"))


def rendus(sortie, par_nom, mat, col):
    for d in par_nom.values():
        for o in d["lods"][1:]:
            o.hide_render = True
        d["arm"].hide_render = True
    # 1. Planche de tous les objets (squelettes cachés)
    n = len(par_nom)
    cx = 9 * .7 / 2
    sc = gp.scene_rendu(2000, 1000, cx, 7.6, cam_y=-6, cam_z=3.2, cible_z=.4)
    cam = sc.camera
    cam.location = (cx, 1.2 - 7.0, 4.2)
    d = Vector((cx, 1.2, .3)) - cam.location
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    gp.rendre(os.path.join(sortie, "apercu_equipement.png"), sc)
    for d in par_nom.values():
        for o in d["lods"]:
            o.hide_render = True
    # 2. Survivants pré‑équipés : corps du script des personnages + dotation
    col_s = bpy.data.collections.new("Survivants_Equipes")
    bpy.context.scene.collection.children.link(col_s)
    x = 0.0
    for role, dot in DOTATIONS.items():
        fiche = dict(gp.SURVIVANTS.get(role, gp.SURVIVANTS["Ouvrier"]))
        tenue = dict(fiche["tenue"])
        tenue["accessoires"] = [a for a in tenue.get("accessoires", []) if a not in ("brassard", "masque")]
        fiche = dict(fiche, tenue=tenue, equipement={})
        perso = gp.creer_personnage("Equipe_" + role, fiche, mat, col_s, x=x)
        gp.masquer_lods([perso])
        perso["arm"].location.x = 0
        perso["arm"].data.pose_position = "REST"  # accrocher en pose de repos, pas en pose animée
        bpy.context.view_layer.update()
        for emplacement, nom in dot.items():
            o = construire(nom, mat, fiche["corps"])
            col_s.objects.link(o)
            gp.accrocher(o, perso["arm"], gp.EMPLACEMENTS[emplacement])
        perso["arm"].data.pose_position = "POSE"
        perso["arm"].location.x = x
        gp.assigner_action(perso["arm"], perso["actions"][0], 20)
        x += .8
    sc = gp.scene_rendu(1900, 800, (x - .8) / 2, 5.0, cam_y=-6, cam_z=2.2)
    cam = sc.camera
    cam.location = ((x - .8) / 2 + 2.5, -5.5, 2.6)
    d = Vector(((x - .8) / 2, 0, .5)) - cam.location
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.frame_set(20)
    gp.rendre(os.path.join(sortie, "apercu_survivants_equipes.png"), sc)


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
