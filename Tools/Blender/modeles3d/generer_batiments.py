"""
After City — générateur Blender des bâtiments du palier 1 (fiche 03_batiments_palier1.md).

Construit en géométrie procédurale les bâtiments de la ferme abandonnée, la défense (palissade,
tour de guet), le stockage, les petits bâtiments du palier 1 et les enclos, avec leurs accroches
SOCKET_ (objets vides), 3 niveaux de détail, puis exporte un FBX par modèle.

Utilisation (Blender 4.2 ou plus récent, même dossier que generer_personnages.py) :
    blender --background --python generer_batiments.py -- --sortie DOSSIER [--sans-rendu]

Conventions : 00_conventions.md et CONVENTIONS.md. 1 unité = 1 m, 1 case = 2 m, la façade
(porte principale) regarde vers −Y dans Blender, origine au centre de l'emprise, au sol.
Matériau unique MAT_Batiments sur T_Batiments_1024 (la palette T_Palette_256 agrandie 4 fois :
mêmes coordonnées UV). Couleur de sommet Col : R occlusion, G saleté, B fenêtres éclairées la nuit.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402  (bpy avant bmesh)
import bmesh  # noqa: E402,F401
from mathutils import Vector, Matrix  # noqa: E402

import generer_personnages as gp  # noqa: E402

# ---------------------------------------------------------------------------
# Palette des bâtiments (palette.md, lignes 8 et 9)
# ---------------------------------------------------------------------------
gp.PALETTE.update({
    "rouge_grange": (8, 0, "7E3528"), "blanc_ferme": (8, 1, "BDB6A3"), "bardeaux": (8, 2, "4E4A45"),
    "bois_neuf": (8, 3, "A87F51"), "bois_gris": (8, 4, "7E7667"), "rondin": (8, 5, "6A4F35"),
    "tole_galva": (8, 6, "959A98"), "tole_rouillee": (8, 7, "6E4A35"), "bache_bleue": (8, 8, "3F6E8F"),
    "toile_tente": (8, 9, "938A66"), "sac_sable": (8, 10, "A59670"), "pierre": (8, 11, "8A857A"),
    "brique": (8, 12, "9A5A40"), "beton": (8, 13, "A6A39A"), "vitre_sale": (8, 14, "4E5A57"),
    "lierre": (8, 15, "4A5A32"),
    "fenetre_eclairee": (9, 0, "FFB766"), "braises": (9, 1, "FF8A3D"), "suie": (9, 2, "2B2622"),
    "paille": (9, 3, "B8A160"), "terre": (9, 4, "5E4730"), "grillage": (9, 5, "5C5E58"),
    "gravats": (9, 6, "6F695E"), "eau_sale": (9, 7, "3E4C47"), "feuillage": (9, 8, "3F5230"),
    "chaux": (9, 9, "A9A394"),
    # Ligne 7 et ligne 9 col. 12-15 : décor urbain en ruines (fil interface, decor/ruines/), déplacé
    # depuis ses lignes 10-11 d'origine pour laisser les lignes 10-15 à la nature
    "rui_voiture_bleue": (7, 0, "5E7387"), "rui_voiture_rouge": (7, 1, "8E4436"),
    "rui_voiture_creme": (7, 2, "C8BC9A"), "rui_voiture_verte": (7, 3, "5D6E55"),
    "rui_pneu": (7, 4, "222324"), "rui_chrome": (7, 5, "A9ADA8"), "rui_feu_rouge": (7, 6, "7A2620"),
    "rui_panneau_rouge": (7, 7, "A33A2E"), "rui_panneau_blanc": (7, 8, "DCD8CC"),
    "rui_panneau_vert": (7, 9, "3F6B4E"), "rui_panneau_jaune": (7, 10, "C9A23A"),
    "rui_peinture_rouge": (7, 11, "9A2B22"), "rui_contreplaque": (7, 12, "B49A6A"),
    "rui_grillage": (7, 13, "8D928F"), "rui_poubelle_verte": (7, 14, "3E5A45"),
    "rui_bidon_bleu": (7, 15, "3B5F86"),
    "rui_interieur_sombre": (9, 12, "1F1D1B"), "rui_mousse": (9, 13, "6F7A45"),
    "rui_herbe_seche": (9, 14, "9A9256"), "rui_bois_peint_vert": (9, 15, "4E6A5A"),
    # Lignes 10-15 : nature (fil équilibrage, decor/nature/, generer_nature.py)
    "nat_feuille_fonce": (10, 0, "3E5A2E"), "nat_feuille": (10, 1, "4F6E36"), "nat_feuille_clair": (10, 2, "6B8442"),
    "nat_feuille_jaune": (10, 3, "8A8E45"), "nat_pin_fonce": (10, 4, "2F4A32"), "nat_pin": (10, 5, "3D5B3B"),
    "nat_pin_clair": (10, 6, "52704A"), "nat_bouleau_feuille": (10, 7, "7A9150"),
    "nat_auto_rouge": (11, 0, "9A4A2C"), "nat_auto_orange": (11, 1, "B4702F"), "nat_auto_jaune": (11, 2, "B79A42"),
    "nat_auto_brun": (11, 3, "7A5A34"), "nat_feuille_morte": (11, 4, "6E5638"),
    "nat_ecorce": (12, 0, "5A4433"), "nat_ecorce_fonce": (12, 1, "3F3026"), "nat_ecorce_pin": (12, 2, "6A4A36"),
    "nat_bouleau_ecorce": (12, 3, "D2CCBA"), "nat_bouleau_tache": (12, 4, "2E2A26"),
    "nat_bois_coupe": (12, 5, "C49A62"), "nat_bois_coupe_cerne": (12, 6, "9C7448"),
    "nat_bois_mort": (12, 7, "8A8274"), "nat_mousse": (12, 8, "5E6E38"),
    "nat_roche": (13, 0, "8E8B80"), "nat_roche_fonce": (13, 1, "6B6961"), "nat_roche_clair": (13, 2, "A9A598"),
    "nat_roche_ocre": (13, 3, "9A8A6C"), "nat_caillou": (13, 4, "7F7A70"), "nat_terre": (13, 5, "6C5236"),
    "nat_lichen": (13, 6, "A3A26A"),
    "nat_herbe_fonce": (14, 0, "4A6232"), "nat_herbe": (14, 1, "62793C"), "nat_herbe_seche": (14, 2, "A39A5C"),
    "nat_herbe_haute": (14, 3, "7E8A4A"), "nat_fleur_blanche": (14, 4, "DCD8C8"),
    "nat_fleur_jaune": (14, 5, "D8B640"), "nat_fleur_violette": (14, 6, "7E5E8E"),
    "nat_baie_rouge": (14, 7, "A8302A"), "nat_baie_bleue": (14, 8, "3A4A7E"), "nat_champignon": (14, 9, "B8A890"),
    "nat_champignon_rouge": (14, 10, "A33A2A"),
    "nat_neige": (15, 0, "E8ECEE"), "nat_neige_ombre": (15, 1, "C4CCD4"),
})
# Contrôle : deux couleurs ne doivent jamais partager une case
_cases = {}
for _n, (_l, _c, _h) in gp.PALETTE.items():
    assert (_l, _c) not in _cases, "case (%d, %d) en double : %s et %s" % (_l, _c, _n, _cases[(_l, _c)])
    _cases[(_l, _c)] = _n

CASE_M = 2.0      # 1 case = 2 m
ETAGE = 1.6       # hauteur d'un étage
PORTE_H, PORTE_L = 1.3, 0.8


class Batisseur(gp.Constructeur):
    """Constructeur sans squelette (un seul groupe Racine, retiré à la fin) avec rotation de bloc
    et repérage des faces de fenêtres éclairées la nuit (canal B)."""

    def __init__(self, hauteur, salete=.3):
        super().__init__(hauteur=hauteur, salete=salete, os_liste=["Racine"])
        self.rz = 0.0           # rotation du bloc en cours autour de l'origine (degrés)
        self.ox = self.oy = 0.0  # décalage du bloc en cours
        self.sockets = []

    def _place(self, centre, rot):
        a = math.radians(self.rz)
        x, y, z = centre
        xr = x * math.cos(a) - y * math.sin(a) + self.ox
        yr = x * math.sin(a) + y * math.cos(a) + self.oy
        return (xr, yr, z), (rot[0], rot[1], rot[2] + self.rz)

    def b(self, centre, taille, coul, rot=(0, 0, 0), biseau=0, **kw):
        c, r = self._place(centre, rot)
        return self.boite(c, taille, coul, "Racine", biseau=biseau, rot=r, **kw)

    def cyl(self, centre, rayon, haut, coul, segments=8, rot=(0, 0, 0), **kw):
        c, r = self._place(centre, rot)
        return self.cylindre(c, rayon, haut, coul, "Racine", segments=segments, rot=r, **kw)

    def socket(self, nom, centre):
        c, _ = self._place(centre, (0, 0, 0))
        self.sockets.append((nom, c))

    def eclaire(self, faces):
        for f in faces:
            for v in f.verts:
                col = v[self.col]
                v[self.col] = (col[0], col[1], 1.0, 1.0)

    def sale(self, faces, g):
        for f in faces:
            for v in f.verts:
                col = v[self.col]
                v[self.col] = (col[0], max(col[1], g), col[2], 1.0)

    # --- éléments d'architecture ------------------------------------------------
    def toit_2pans(self, longueur, profondeur, z_base, haut, coul, debord=.25, ep=.08, cy=0.0,
                   manque=None):
        """Toit à deux pans, faîtage le long de X. manque = (x0, x1, 'av'|'ar') : trou de ruine."""
        demi = profondeur / 2 + debord
        a = math.atan2(haut, profondeur / 2)
        s = demi / math.cos(a)
        for sens in (-1, 1):
            dy, dz = sens * math.cos(a), -math.sin(a)
            n = (0, sens * math.sin(a), math.cos(a))
            cyc = cy + dy * s / 2 + n[1] * ep / 2
            czc = z_base + haut + dz * s / 2 + n[2] * ep / 2
            rx = -math.degrees(a) * sens
            pans = [(-longueur / 2 - debord, longueur / 2 + debord)]
            if manque and (manque[2] == "av") == (sens < 0):
                pans = [(-longueur / 2 - debord, manque[0]), (manque[1], longueur / 2 + debord)]
            for x0, x1 in pans:
                if x1 - x0 > .05:
                    self.b(((x0 + x1) / 2, cyc, czc), (x1 - x0, s, ep), coul, rot=(rx, 0, 0))
        # faîtière
        self.b((0, cy, z_base + haut + ep * .6), (longueur + 2 * debord, .14, .07), "bois_gris")

    def pignon(self, x, profondeur, z_base, haut, coul, ep=.12, cy=0.0):
        """Mur triangulaire sous un toit à deux pans (faîtage le long de X)."""
        return self.b((x, cy, z_base + haut / 2), (ep, profondeur, haut), coul, effile=(1, .02))

    def fenetre(self, x, y, z, cote, coul_cadre="blanc_ferme", l=.5, h=.6, eclairee=True,
                brisee=False, planches=False, volets=None):
        """Fenêtre posée sur un mur. cote : '-y', '+y', '-x', '+x' (vers l'extérieur)."""
        ax = cote[1]
        sg = -1 if cote[0] == "-" else 1
        def sz(w, d, hh):
            return (w, d, hh) if ax == "y" else (d, w, hh)
        def pos(u, off, zz):
            return (x + u, y + sg * off, zz) if ax == "y" else (x + sg * off, y + u, zz)
        self.b(pos(0, .02, z), sz(l + .1, .05, h + .1), coul_cadre)
        f = self.b(pos(0, .045, z), sz(l, .02, h), "suie" if brisee else "vitre_sale")
        if eclairee and not brisee:
            self.eclaire(f)
        self.b(pos(0, .06, z), sz(.04, .02, h), coul_cadre)  # croisillon
        self.b(pos(0, .06, z - h / 2 - .06), sz(l + .16, .1, .04), coul_cadre)  # appui
        if planches:
            for k in (-1, 1):
                self.b(pos(0, .08, z), sz(l + .2, .025, .1), "bois_neuf", rot=(0, k * 30, 0) if ax == "y"
                       else (k * 30, 0, 0))
        if volets:
            for k in (-1, 1):
                self.b(pos(k * (l / 2 + .16), .04, z), sz(.24, .03, h + .05), volets)

    def porte(self, x, y, cote, coul="bois_gris", l=PORTE_L, h=PORTE_H, cadre="blanc_ferme",
              eclairee=False):
        ax = cote[1]
        sg = -1 if cote[0] == "-" else 1
        def sz(w, d, hh):
            return (w, d, hh) if ax == "y" else (d, w, hh)
        def pos(u, off, zz):
            return (x + u, y + sg * off, zz) if ax == "y" else (x + sg * off, y + u, zz)
        self.b(pos(0, .02, h / 2 + .03), sz(l + .12, .05, h + .06), cadre)
        f = self.b(pos(0, .045, h / 2), sz(l, .03, h), coul)
        self.b(pos(l * .32, .07, h * .5), sz(.05, .03, .05), "acier_sombre")  # poignée
        if eclairee:
            self.eclaire(f)

    def bardage(self, x0, x1, y, z0, z1, cote, coul, pas=.27, ep=.025):
        """Planches horizontales en relief sur un mur (lecture de la matière sans texture)."""
        ax = cote[1]
        sg = -1 if cote[0] == "-" else 1
        z = z0 + pas
        while z < z1 - .05:
            L = x1 - x0
            c = ((x0 + x1) / 2, y + sg * ep / 2, z) if ax == "y" else (y + sg * ep / 2, (x0 + x1) / 2, z)
            t = (L, ep, .05) if ax == "y" else (ep, L, .05)
            self.b(c, t, coul)
            z += pas

    def poteau(self, x, y, h, coul="bois_gris", e=.12, z0=0.0, rot=(0, 0, 0)):
        return self.b((x, y, z0 + h / 2), (e, e, h), coul, rot=rot)

    def herbes(self, rnd, n, rx, ry, coul="lierre", h=(.15, .4)):
        """Touffes d'herbes hautes et gravats autour d'une ruine."""
        for _ in range(n):
            x, y = rnd.uniform(-rx, rx), rnd.uniform(-ry, ry)
            hh = rnd.uniform(*h)
            self.b((x, y, hh / 2), (.18, .18, hh), coul, effile=(.2, .2), rot=(0, 0, rnd.uniform(0, 90)))


# ---------------------------------------------------------------------------
# Ferme abandonnée
# ---------------------------------------------------------------------------
def corps_de_ferme(c, etat):
    """3 × 3 cases (6 × 6 m), 2 étages, toit à deux pans (faîtage le long de X), véranda en façade."""
    rnd = random.Random(1)
    ruine = etat == "Ruine"
    L, P = 5.0, 3.8          # maison
    yv = -P / 2              # façade
    zc = 2 * ETAGE           # corniche à 3,2 m
    blanc = "blanc_ferme"
    # soubassement en pierre et murs
    c.b((0, .4, .15), (L + .1, P + .1, .3), "pierre")
    murs = c.b((0, .4, .3 + (zc - .3) / 2), (L, P, zc - .3), blanc)
    c.sale(murs, .35 if not ruine else .7)
    for x in (-L / 2, L / 2):
        c.pignon(x, P, zc, 1.25, blanc, cy=.4)
    # bardage
    for cote, y in (("-y", .4 - P / 2), ("+y", .4 + P / 2)):
        c.bardage(-L / 2, L / 2, y, .3, zc, cote, blanc)
    for cote, x in (("-x", -L / 2), ("+x", L / 2)):
        c.bardage(.4 - P / 2, .4 + P / 2, x, .3, zc, cote, blanc)
    # angles
    for x in (-L / 2, L / 2):
        for y in (.4 - P / 2, .4 + P / 2):
            c.b((x, y, zc / 2), (.12, .12, zc), "bois_gris" if ruine else blanc)
    # toit
    manque = (-1.6, .4, "av") if ruine else None
    c.toit_2pans(L, P, zc, 1.25, "bardeaux", cy=.4, manque=manque)
    if ruine:  # charpente visible dans le trou, poutres tombées
        for x in (-1.3, -.8, -.3, .2):
            c.b((x, .4 - P / 4, zc + .55), (.08, P / 2 + .2, .08), "bois_sombre", rot=(-33, 0, 0))
        c.b((-.6, .4 - .6, zc - .4), (1.8, .1, .1), "bois_sombre", rot=(0, 25, 10))
    else:  # bâche bleue sur l'ancien trou
        c.b((-.6, .4 - P / 4 - .1, zc + .66), (1.9, 1.5, .03), "bache_bleue", rot=(-33, 0, 0))
        for x in (-1.5, .3):
            c.b((x, .4 - P / 4 - .1, zc + .68), (.06, 1.5, .03), "bois_neuf", rot=(-33, 0, 0))
    # lucarne
    c.b((1.2, .4 - .9, zc + .62), (.8, .7, .7), blanc)
    c.b((1.2, .4 - .95, zc + 1.02), (1.0, .9, .07), "bardeaux", rot=(-10, 0, 0))
    c.fenetre(1.2, .4 - 1.25, zc + .6, "-y", l=.4, h=.4, brisee=ruine)
    # cheminée en pierre sur le pignon est
    c.b((L / 2 - .45, .4 + .5, zc + 1.0), (.5, .5, 2.4), "pierre")
    c.b((L / 2 - .45, .4 + .5, zc + 2.25), (.6, .6, .12), "pierre")
    c.socket("SOCKET_Fumee", (L / 2 - .45, .9, zc + 2.4))
    # fenêtres
    yf = .4 - P / 2
    for i, x in enumerate((-1.7, -.6, .6, 1.7)):
        bas = i in (0, 3)
        c.fenetre(x, yf, ETAGE + 1.0, "-y", brisee=ruine and i % 2 == 0, volets=None if ruine else "bardeaux")
        if x not in (-.6, .6):
            c.fenetre(x, yf, .95, "-y", brisee=ruine, planches=(not ruine and bas))
    for x in (-1.5, 0, 1.5):
        c.fenetre(x, .4 + P / 2, ETAGE + 1.0, "+y", brisee=ruine and x == 0, eclairee=not ruine)
        c.fenetre(x, .4 + P / 2, .95, "+y", brisee=ruine)
    for y in (-.3, 1.1):
        c.fenetre(-L / 2, y, ETAGE + 1.0, "-x", brisee=ruine)
        c.fenetre(L / 2, y, .95, "+x", brisee=ruine and y > 0)
    c.porte(0, yf, "-y", coul="rouge_grange" if not ruine else "bois_gris")
    # véranda
    yv = yf - .9
    c.b((0, yf - .45, .15), (L - .4, .9, .1), "bois_gris")
    c.b((0, yf - .95, .06), (1.0, .3, .12), "bois_gris")  # marche
    poteaux = (-2.2, -1.1, 1.1, 2.2)
    for x in poteaux:
        if ruine and x == 1.1:
            c.b((x + .3, yv + .1, .3), (.08, .08, 1.5), "bois_gris", rot=(0, 75, 0))  # colonne cassée
            continue
        c.poteau(x, yv + .05, 1.55, blanc, e=.08, z0=.2)
    pente = (0, 0, 0) if not ruine else (0, 6, 0)
    c.b((0, yf - .45, 1.85), (L - .2, 1.15, .07), "bardeaux", rot=(-12 + pente[1] * 0, pente[1], 0))
    for x0, x1 in ((-2.2, -.5), (.5, 2.2)):
        c.b(((x0 + x1) / 2, yv + .05, .75), (x1 - x0, .05, .06), blanc)
        c.b(((x0 + x1) / 2, yv + .05, .45), (x1 - x0, .04, .05), blanc)
    c.socket("SOCKET_Porte", (0, yf - 1.6, 0))
    for i, (x, y, z) in enumerate(((-1.7, yf - .1, .95), (1.5, .4 + P / 2 + .1, ETAGE + 1), (-L / 2 - .1, 1.1, ETAGE + 1))):
        c.socket("SOCKET_Lumiere_%02d" % (i + 1), (x, y, z))
    if ruine:
        # lierre sur l'angle, matelas et meubles jetés devant, gravats
        for k in range(5):
            c.b((-L / 2 - .03, .4 + P / 2 - .2 - k * .25, .5 + k * .45), (.08, .5, .6), "lierre",
                rot=(k * 15, 0, 0))
        c.b((1.4, yf - 1.5, .1), (1.0, .7, .16), "tshirt_creme", rot=(0, 0, 20))
        c.b((-1.5, yf - 1.6, .25), (.5, .5, .5), "bois_sombre", rot=(0, 25, 40))
        c.b((-1.9, yf - 1.1, .05), (.6, .3, .1), "bois_gris", rot=(0, 0, 60))
        for _ in range(10):
            x, y = rnd.uniform(-2.6, 2.6), rnd.uniform(-2.9, -2.1)
            c.b((x, y, .05), (rnd.uniform(.1, .3), rnd.uniform(.1, .3), .1), "gravats",
                rot=(0, 0, rnd.uniform(0, 90)))
        c.herbes(rnd, 14, 2.9, 2.9)
    else:
        # fauteuil à bascule, pile de bois
        c.b((-1.6, yf - .5, .45), (.4, .4, .06), "bois_sombre")
        c.b((-1.6, yf - .35, .7), (.4, .06, .5), "bois_sombre", rot=(-10, 0, 0))
        for k in range(3):
            c.cyl((2.2, yf + .6 + k * .0, .12 + k * .2), .1, .9, "rondin", segments=6, rot=(90, 0, 90))
        c.herbes(rnd, 4, 2.9, 2.9)


def grange(c, etat):
    """3 × 4 cases (6 × 8 m), toit en mansarde, porte coulissante sur le pignon avant (−Y)."""
    rnd = random.Random(2)
    ruine = etat == "Ruine"
    W, P = 5.2, 7.2            # largeur (X), profondeur (Y)
    zm = 2.5                   # haut des murs
    rouge = "rouge_grange"
    murs = c.b((0, 0, zm / 2), (W, P, zm), rouge)
    c.sale(murs, .4 if not ruine else .75)
    # planches verticales en relief (lisibilité)
    for cote, y in (("-y", -P / 2), ("+y", P / 2)):
        for k in range(-6, 7):
            x = k * .4
            if abs(x) < 1.3 and cote == "-y":
                continue
            c.b((x, y + (-.015 if cote == "-y" else .015), zm / 2), (.06, .03, zm), rouge)
    for cote, x in (("-x", -W / 2), ("+x", W / 2)):
        for k in range(-8, 9):
            c.b((x + (-.015 if cote == "-x" else .015), k * .42, zm / 2), (.03, .06, zm), rouge)
    # mansarde : brisis (pente raide) puis terrasson (pente douce), faîtage le long de Y
    c.rz = 90
    hb, hs = 1.3, 1.0                       # hauteur brisis / terrasson
    wb = W / 2 - .7                         # demi-largeur au brisis
    # pignons (avant / arrière) : partie basse trapèze + triangle
    for y in (-P / 2, P / 2):
        c.b((y, 0, zm + hb / 2), (.12, W, hb), rouge, effile=(1, (2 * wb) / W))
        c.b((y, 0, zm + hb + hs / 2), (.12, 2 * wb, hs), rouge, effile=(1, .02))
    toit = "tole_galva" if ruine else "tole_galva"
    for sens in (-1, 1):
        # brisis
        a = math.atan2(hb, W / 2 - wb)
        s = math.hypot(hb, W / 2 - wb) + .25
        yc = sens * (W / 2 + wb) / 2
        pans = [(-P / 2 - .25, P / 2 + .25)]
        if ruine and sens < 0:
            pans = [(-P / 2 - .25, -1.0), (1.5, P / 2 + .25)]
        for x0, x1 in pans:
            c.b(((x0 + x1) / 2, yc + sens * .05, zm + hb / 2 - .05), (x1 - x0, s, .07), toit,
                rot=(-sens * math.degrees(a), 0, 0))
        # terrasson
        a2 = math.atan2(hs, wb)
        s2 = math.hypot(hs, wb) + .1
        coul2 = "tole_rouillee" if (not ruine and sens > 0) else "tole_galva"
        for x0, x1 in pans:
            c.b(((x0 + x1) / 2, sens * wb / 2, zm + hb + hs / 2 + .04), (x1 - x0, s2, .07), coul2,
                rot=(-sens * math.degrees(a2), 0, 0))
    c.b((0, 0, zm + hb + hs + .05), (P + .5, .16, .08), "tole_galva")
    if ruine:
        for x in (-.6, 0, .6, 1.2):
            c.b((x, -W / 4 - .2, zm + .9), (.08, 1.6, .08), "bois_sombre", rot=(40, 0, 0))
    c.rz = 0
    # grande porte coulissante double + rail, porte de fenil + poulie
    yf = -P / 2
    c.b((0, yf - .05, zm + .1), (3.2, .08, .1), "acier_sombre")  # rail
    if ruine:
        c.b((-.7, yf - .5, .06), (1.2, 2.2, .08), rouge, rot=(0, 0, 15))  # battant tombé
        c.b((.65, yf - .04, 1.1), (1.2, .06, 2.2), rouge)
        c.b((-.65, yf + .02, 1.1), (1.2, .02, 2.2), "suie")
    else:
        for x in (-.65, .65):
            c.b((x, yf - .04, 1.1), (1.25, .06, 2.2), rouge)
            c.b((x, yf - .08, 1.1), (1.1, .03, .08), "blanc_ferme", rot=(0, 55 if x < 0 else -55, 0))
            c.b((x, yf - .08, 1.1), (1.1, .03, .08), "blanc_ferme", rot=(0, -55 if x < 0 else 55, 0))
            c.b((x, yf - .07, 1.1), (1.25, .04, 2.2), "blanc_ferme", effile=(1, 1)) if False else None
    c.b((0, yf - .03, zm + .8), (.9, .05, .9), "bois_gris" if not ruine else "suie")
    c.b((0, yf - .03, zm + .8), (1.0, .03, 1.0), "blanc_ferme")
    c.b((0, yf - .5, zm + hb + .5), (.08, 1.0, .08), "bois_sombre")  # potence
    c.cyl((0, yf - .9, zm + hb + .38), .07, .05, "acier_sombre", segments=6, rot=(90, 0, 0))
    c.b((0, yf - .9, zm + hb - .2), (.02, .02, 1.1), "cuir")
    for k in range(3):
        c.fenetre(-W / 2, -2 + k * 2, 1.6, "-x", coul_cadre="blanc_ferme", l=.5, h=.4, brisee=ruine, eclairee=False)
        c.fenetre(W / 2, -2 + k * 2, 1.6, "+x", coul_cadre="blanc_ferme", l=.5, h=.4, brisee=ruine, eclairee=False)
    c.socket("SOCKET_Porte", (0, yf - 1.2, 0))
    for i in range(6):
        c.socket("SOCKET_Stock_%02d" % (i + 1), (-1.5 + (i % 3) * 1.5, -1.5 + (i // 3) * 2.5, 0))
    if ruine:
        for _ in range(10):
            c.b((rnd.uniform(-2.5, 2.5), rnd.uniform(-4.2, -3.7), .05), (.3, .2, .1), "gravats",
                rot=(0, 0, rnd.uniform(0, 90)))
        c.herbes(rnd, 16, 3.0, 4.0)
    else:
        for i, (x, y) in enumerate(((-2.0, yf - .6), (2.1, yf - .5))):
            c.cyl((x, y, .45), .45, .7, "paille", segments=10, rot=(90, 0, 90))  # botte ronde
        c.herbes(rnd, 5, 3.0, 4.0)


def poulailler(c, etat):
    """2 × 2 cases : cabane à pente unique + enclos grillagé de 2 × 1 cases attenant (côté +X)."""
    rnd = random.Random(3)
    c.b((-.9, 0, .55), (1.4, 1.4, 1.1), "bois_gris")
    for k in range(-3, 4):
        c.b((-.9 + k * .2, -.71, .55), (.05, .03, 1.1), "bois_gris")
    c.b((-.9, 0, 1.3), (1.7, 1.7, .07), "tole_rouillee", rot=(14, 0, 0))
    c.b((-.9, .7, 1.2), (1.4, .05, .3), "bois_gris")
    c.b((-1.3, -.72, .3), (.25, .03, .3), "suie")  # trou des poules
    c.b((-1.3, -1.0, .15), (.2, .6, .03), "bois_neuf", rot=(28, 0, 0))  # rampe
    c.porte(-.6, -.7, "-y", coul="bois_neuf", l=.5, h=.95, cadre="bois_gris")
    # enclos grillagé
    for (x, y) in ((.2, -.9), (1.9, -.9), (1.9, .9), (.2, .9), (1.05, -.9), (1.05, .9), (1.9, 0)):
        c.poteau(x, y, .9, "bois_gris", e=.07)
    for x0, y0, x1, y1 in ((.2, -.9, 1.9, -.9), (1.9, -.9, 1.9, .9), (.2, .9, 1.9, .9)):
        L = math.hypot(x1 - x0, y1 - y0)
        rz = math.degrees(math.atan2(y1 - y0, x1 - x0))
        c.b(((x0 + x1) / 2, (y0 + y1) / 2, .45), (L, .015, .8), "grillage", rot=(0, 0, rz))
        c.b(((x0 + x1) / 2, (y0 + y1) / 2, .88), (L, .05, .04), "bois_gris", rot=(0, 0, rz))
    c.b((1.2, .3, .08), (.6, .2, .12), "acier_sombre")  # mangeoire
    c.socket("SOCKET_Porte", (-.6, -1.3, 0))
    c.socket("SOCKET_Travail_01", (1.0, -.2, 0))
    c.herbes(rnd, 4, 1.9, 1.9)


def silo(c, etat):
    """1 × 1 case, 6,5 m : le repère le plus visible de la base."""
    r = .85
    c.b((0, 0, .1), (1.9, 1.9, .2), "beton")
    corps = c.cyl((0, 0, 2.6), r, 4.8, "tole_galva", segments=16)
    c.sale(corps, .3)
    for z in (1.0, 2.2, 3.4, 4.6):
        c.cyl((0, 0, z), r + .02, .08, "acier_sombre", segments=16)
    c.cyl((0, 0, 5.0 + .7), r + .05, 1.4, "tole_galva", segments=16, rayon_haut=.12)
    c.cyl((0, 0, 6.45), .14, .1, "tole_rouillee", segments=8)
    # coulures de rouille
    for a in (20, 140, 250):
        x, y = math.cos(math.radians(a)) * (r + .015), math.sin(math.radians(a)) * (r + .015)
        c.b((x, y, 3.8), (.12, .02, 1.6), "tole_rouillee", rot=(0, 0, a + 90), effile_bas=(.2, 1))
    # échelle à crinoline
    for k in (-1, 1):
        c.b((k * .18, -r - .12, 3.0), (.04, .04, 5.6), "acier")
    for z in range(1, 19):
        c.b((0, -r - .12, z * .3), (.36, .03, .03), "acier")
    for z in (2.5, 3.5, 4.5, 5.3):
        c.cyl((0, -r - .22, z), .26, .04, "acier", segments=8)
    # conduite de chargement vers la grange
    c.cyl((.6, -.2, 5.6), .08, 1.8, "acier_sombre", segments=6, rot=(0, 60, 0))


def hangar(c, etat):
    """3 × 3 cases : structure ouverte vers −Y, toit de tôle à une pente."""
    rnd = random.Random(5)
    W, P = 5.6, 4.6
    for x in (-W / 2, -W / 6, W / 6, W / 2):
        c.poteau(x, -P / 2, 2.6, "bois_gris", e=.16)
        c.poteau(x, P / 2, 2.1, "bois_gris", e=.16)
    c.b((0, -P / 2, 2.55), (W + .2, .16, .2), "bois_gris")
    c.b((0, P / 2, 2.05), (W + .2, .16, .2), "bois_gris")
    a = math.degrees(math.atan2(.5, P))
    for k in range(7):
        x = -W / 2 + .4 + k * (W - .8) / 6
        coul = "tole_rouillee" if k in (1, 4) else "tole_galva"
        c.b((x, 0, 2.4), (.9, P + .6, .05), coul, rot=(-a, 0, 0))
        for o in (-.3, 0, .3):
            c.b((x + o, 0, 2.43), (.05, P + .6, .03), coul, rot=(-a, 0, 0))
    # bardage arrière et côtés
    c.b((0, P / 2, 1.0), (W, .08, 2.0), "bois_gris")
    for x in (-W / 2, W / 2):
        c.b((x, 0, 1.0), (.08, P, 2.0), "tole_rouillee")
    # établi, bidons, pneus
    c.b((1.5, P / 2 - .4, .45), (1.6, .5, .08), "bois_sombre")
    for x in (.9, 2.1):
        c.poteau(x, P / 2 - .4, .45, "bois_sombre", e=.07)
    for i, x in enumerate((-2.3, -1.9)):
        c.cyl((x, P / 2 - .35, .3), .18, .6, "rouille" if i else "orange_hv", segments=8)
    for k in range(3):
        c.cyl((-2.2, -.5, .1 + k * .16), .3, .14, "plastique_noir", segments=10)
    c.socket("SOCKET_Porte", (0, -P / 2 - .6, 0))
    c.socket("SOCKET_Travail_01", (1.5, P / 2 - .9, 0))
    c.socket("SOCKET_Vehicule", (0, 0, 0))
    c.herbes(rnd, 5, 3, 2.9)


# ---------------------------------------------------------------------------
# Défense
# ---------------------------------------------------------------------------
def _rondin(c, x, y, h, rnd, coul="rondin", r=.12, abime=False):
    hh = h * (rnd.uniform(.6, .8) if abime else rnd.uniform(.94, 1.04))
    c.cyl((x, y, hh / 2), r, hh, coul, segments=5)
    if not abime:
        c.cyl((x, y, hh + .12), r, .24, coul, segments=5, rayon_haut=0)


def palissade(c, piece, graine=0):
    """Un segment par case (2 m), le long de X, extérieur vers −Y. Pièces : Droit_A/B/C, Coin, Fin, Porte."""
    rnd = random.Random(10 + graine)
    H = 1.6
    if piece == "Porte":
        for x in (-1.95, 1.95):
            c.cyl((x, 0, 1.0), .17, 2.0, "rondin", segments=8)
        c.b((0, 0, 2.05), (4.2, .2, .2), "rondin")
        for k, x0 in enumerate((-1.85, .05)):
            for i in range(5):
                c.b((x0 + .18 + i * .36, 0, .8), (.32, .12, 1.5), "bois_gris")
            c.b((x0 + .9, -.08, .4), (1.8, .06, .14), "bois_sombre")
            c.b((x0 + .9, -.08, 1.2), (1.8, .06, .14), "bois_sombre")
            c.b((x0 + .9, -.09, .8), (1.9, .05, .12), "bois_sombre", rot=(0, 24 if k else -24, 0))
        c.socket("SOCKET_Porte", (0, -1.0, 0))
        return
    n = 8 if piece != "Fin" else 5
    x0 = -1.0 if piece != "Fin" else -1.0
    pas = 2.0 / 8
    for i in range(n):
        _rondin(c, x0 + pas / 2 + i * pas, 0, H, rnd)
    if piece == "Coin":
        for i in range(1, 5):
            _rondin(c, -1.0 + pas / 2, i * pas, H, rnd)
        c.cyl((-1.0 + pas / 2, 0, (H + .3) / 2), .18, H + .3, "rondin", segments=8)
    # traverses côté intérieur (+Y) et passerelle de tir
    L = pas * n
    xm = x0 + L / 2
    for z in (.45, 1.2):
        c.b((xm, .15, z), (L, .08, .1), "bois_sombre")
    c.b((xm, .42, 1.0), (L, .4, .05), "bois_neuf")
    for x in (x0 + .2, x0 + L - .2):
        c.b((x, .35, .5), (.07, .07, 1.0), "bois_sombre")
    # variantes : tôle ou panneau routier cloué côté extérieur
    if piece == "Droit_B":
        c.b((.2, -.15, .8), (.9, .03, .7), "tole_rouillee", rot=(0, 4, 0))
    elif piece == "Droit_C":
        c.b((-.3, -.15, .9), (.6, .03, .6), "acier", rot=(0, 45, 0))
        c.b((-.3, -.17, .9), (.45, .02, .45), "orange_hv", rot=(0, 45, 0))
    # extérieur plus sale (ligne de crasse)
    c.b((xm, -.13, .15), (L, .02, .3), "terre")


def tour_de_guet(c, etat):
    """1 × 1 case, 4 m, plateforme à 3 m."""
    zp = 3.0
    for sx in (-1, 1):
        for sy in (-1, 1):
            c.b((sx * .7, sy * .7, zp / 2), (.14, .14, zp + .1), "rondin",
                rot=(-sy * 4, sx * 4, 0))
    for sy in (-1, 1):  # croisillons
        for k in (-1, 1):
            c.b((0, sy * .72, 1.4), (.07, .07, 2.0), "bois_gris", rot=(0, k * 38, 0))
            c.b((sy * .72, 0, 1.4), (.07, .07, 2.0), "bois_gris", rot=(k * 38, 0, 0))
    c.b((0, 0, zp), (1.8, 1.8, .1), "bois_neuf")
    for cote in range(4):
        c.rz = cote * 90
        if cote != 0:
            c.b((0, -.88, zp + .45), (1.8, .06, .8), "bois_gris")
        else:
            c.b((-.55, -.88, zp + .45), (.7, .06, .8), "bois_gris")
            c.b((.55, -.88, zp + .45), (.7, .06, .8), "bois_gris")
        c.b((0, -.9, zp + .9), (1.85, .1, .08), "bois_sombre")
        c.poteau(-.85, -.85, 1.6, "rondin", e=.1, z0=zp)
    c.rz = 0
    c.b((0, .1, zp + 1.75), (2.1, 2.2, .06), "tole_rouillee", rot=(12, 0, 0))
    # échelle face −Y
    for k in (-1, 1):
        c.b((k * .2, -1.05, zp / 2), (.05, .05, zp + .2), "bois_gris", rot=(-10, 0, 0))
    for z in range(1, 11):
        c.b((0, -1.05 - (zp / 2 - z * .28) * math.tan(math.radians(10)) * -1 * 0, z * .28), (.42, .04, .04),
            "bois_gris")
    c.b((.6, -.88, zp + .7), (.1, .1, .14), "fenetre_eclairee")
    c.socket("SOCKET_Garde", (0, 0, zp + .05))
    c.socket("SOCKET_Lumiere", (.6, -.9, zp + .7))
    c.socket("SOCKET_Porte", (0, -1.4, 0))


# ---------------------------------------------------------------------------
# Stockage et petits bâtiments du palier 1
# ---------------------------------------------------------------------------
def tas_de_stockage(c, etat):
    """2 × 2 cases, palettes, piquets, bâche bleue sur une moitié, 4 piles visibles (plein)."""
    rnd = random.Random(6)
    for i, (x, y) in enumerate(((-.9, -.9), (.9, -.9), (-.9, .9), (.9, .9))):
        c.b((x, y, .07), (1.4, 1.4, .14), "bois_neuf")
        for k in (-1, 0, 1):
            c.b((x + k * .5, y, .02), (.12, 1.4, .04), "bois_gris")
        c.socket("SOCKET_Stock_%02d" % (i + 1), (x, y, .14))
    for sx in (-1, 1):
        for sy in (-1, 1):
            c.poteau(sx * 1.85, sy * 1.85, 1.5, "bois_gris", e=.1)
    c.b((0, .9, 1.45), (3.9, 2.0, .03), "bache_bleue", rot=(-6, 0, 0))
    # piles (le jeu peut les remplacer par niveau de remplissage)
    for k in range(4):  # bûches
        for j in range(4 - k):
            c.cyl((-.9 - .45 + j * .3 + k * .15, -.9, .3 + k * .26), .13, 1.2, "rondin", segments=6,
                  rot=(90, 0, 0))
    for _ in range(9):  # pierres
        c.b((.9 + rnd.uniform(-.45, .45), -.9 + rnd.uniform(-.45, .45), .3 + rnd.uniform(0, .25)),
            (.3, .28, .22), "pierre", rot=(rnd.uniform(0, 30), 0, rnd.uniform(0, 90)))
    for k in range(5):  # sacs de terre / nourriture
        c.b((-.9 + (k % 3 - 1) * .4, .9, .3 + (k // 3) * .25), (.36, .55, .22), "sac_sable",
            biseau=.06)
    for _ in range(6):  # ferraille
        c.b((.9 + rnd.uniform(-.4, .4), .9 + rnd.uniform(-.4, .4), .3 + rnd.uniform(0, .3)),
            (rnd.uniform(.2, .6), .08, .12), rnd.choice(["acier", "rouille", "acier_sombre"]),
            rot=(rnd.uniform(0, 40), rnd.uniform(0, 40), rnd.uniform(0, 180)))
    c.socket("SOCKET_Porte", (0, -2.2, 0))


def tente(c, etat):
    """1 × 1 case, tente canadienne en toile, bâche bleue en plus."""
    c.b((0, .1, .55), (1.5, 1.7, 1.1), "toile_tente", effile=(.03, 1))
    c.b((0, -.76, .4), (.6, .02, .75), "suie", effile=(.05, 1))
    c.b((.35, .4, .82), (.9, 1.1, .03), "bache_bleue", rot=(0, -38, 0))
    for y in (-.75, .95):
        c.poteau(0, y, 1.15, "bois_gris", e=.04)
    c.b((0, -1.0, .06), (.5, .9, .1), "olive", biseau=.03)  # sac de couchage
    c.socket("SOCKET_Porte", (0, -1.3, 0))


def cabane_bucheron(c, etat):
    """2 × 2 cases, cabane en rondins, billot avec hache, bûches contre un mur."""
    W, P = 2.6, 2.2
    for k in range(7):
        z = .13 + k * .24
        for y in (-P / 2, P / 2):
            c.cyl((0, y, z), .12, W + .3, "rondin", segments=6, rot=(0, 90, 0))
        for x in (-W / 2, W / 2):
            c.cyl((x, 0, z + .12), .12, P + .3, "rondin", segments=6, rot=(90, 0, 0))
    zc = 1.75
    for x in (-W / 2, W / 2):
        c.pignon(x, P, zc, .8, "rondin", ep=.2)
    c.toit_2pans(W, P, zc, .8, "tole_rouillee", debord=.3)
    c.porte(-.5, -P / 2 - .08, "-y", coul="bois_gris", cadre="bois_sombre")
    c.fenetre(.6, -P / 2 - .08, 1.0, "-y", coul_cadre="bois_sombre", l=.45, h=.4)
    c.cyl((W / 2 - .4, P / 2 - .3, zc + .6), .12, 1.4, "acier_sombre", segments=6)  # tuyau de poêle
    c.socket("SOCKET_Fumee", (W / 2 - .4, P / 2 - .3, zc + 1.35))
    # billot + hache plantée
    c.cyl((.9, -1.9, .25), .3, .5, "rondin", segments=8)
    c.cyl((.9, -1.9, .505), .28, .01, "bois_neuf", segments=8)
    c.b((.95, -1.9, .78), (.04, .04, .55), "bois_manche", rot=(0, 30, 0))
    c.b((.82, -1.9, .55), (.16, .03, .12), "acier", rot=(0, 30, 0))
    # bûches empilées contre le mur est
    for k in range(4):
        for j in range(4):
            c.cyl((W / 2 + .3, -.75 + j * .5, .13 + k * .22), .11, .45, "rondin", segments=6,
                  rot=(0, 90, 0))
    c.socket("SOCKET_Porte", (-.5, -P / 2 - .7, 0))
    c.socket("SOCKET_Travail_01", (.9, -2.4, 0))
    c.socket("SOCKET_Travail_02", (-1.6, -1.6, 0))
    c.socket("SOCKET_Stock_01", (W / 2 + .3, 0, 0))


def puits(c, etat):
    """1 × 1 case : margelle en pierre, potence, manivelle, seau."""
    for k in range(10):
        a = k * 36
        x, y = math.cos(math.radians(a)) * .55, math.sin(math.radians(a)) * .55
        c.b((x, y, .3), (.32, .22, .6), "pierre", rot=(0, 0, a + 90))
    c.cyl((0, 0, .45), .45, .02, "eau_sale", segments=10)
    for x in (-.6, .6):
        c.b((x, 0, 1.0), (.1, .1, 1.4), "bois_gris")
    c.b((0, 0, 1.55), (1.5, .1, .1), "bois_sombre")
    c.cyl((0, 0, 1.3), .08, 1.1, "bois_manche", segments=6, rot=(0, 90, 0))
    c.b((.72, -.1, 1.25), (.04, .2, .04), "acier_sombre")
    c.b((0, 0, .95), (.02, .02, .6), "cuir")
    c.cyl((0, 0, .6), .12, .2, "metal_gourde", segments=8, rayon_haut=.14)
    c.b((0, 0, 1.75), (1.4, 1.0, .05), "bardeaux", effile=(1, .02))
    c.socket("SOCKET_Travail_01", (0, -.9, 0))


def potager(c, etat):
    """2 × 2 cases : bacs surélevés, grillage bas, épouvantail."""
    rnd = random.Random(8)
    for i, (x, y) in enumerate(((-.85, -.8), (.85, -.8), (-.85, .8), (.85, .8))):
        c.b((x, y, .15), (1.3, 1.1, .3), "bois_gris")
        c.b((x, y, .31), (1.2, 1.0, .03), "terre")
        for k in range(3):
            for j in range(2):
                c.b((x - .4 + k * .4, y - .25 + j * .5, .42), (.18, .18, .2), "feuillage",
                    effile=(.3, .3), rot=(0, 0, rnd.uniform(0, 90)))
        c.socket("SOCKET_Travail_%02d" % (i + 1), (x, y - .7 if y < 0 else y + .7, 0))
    for x0, y0, x1, y1 in ((-1.9, -1.9, 1.9, -1.9), (1.9, -1.9, 1.9, 1.9), (1.9, 1.9, -1.9, 1.9),
                           (-1.9, 1.9, -1.9, -1.9)):
        L = math.hypot(x1 - x0, y1 - y0)
        rz = math.degrees(math.atan2(y1 - y0, x1 - x0))
        c.b(((x0 + x1) / 2, (y0 + y1) / 2, .3), (L, .01, .55), "grillage", rot=(0, 0, rz))
        c.poteau(x0, y0, .6, "bois_gris", e=.06)
    # épouvantail
    c.b((0, 0, .8), (.06, .06, 1.6), "bois_gris")
    c.b((0, 0, 1.3), (.9, .05, .05), "bois_gris")
    c.b((0, 0, 1.2), (.4, .2, .45), "flanelle_rouge")
    c.b((0, 0, 1.6), (.22, .2, .22), "toile_sac", biseau=.04)
    c.cyl((0, 0, 1.75), .22, .03, "paille", segments=8)
    c.cyl((0, 0, 1.83), .11, .14, "paille", segments=8)


def fosse_incineration(c, etat):
    """2 × 2 cases : fosse ronde bordée de pierres, rondins calcinés, braises (canal B)."""
    for k in range(14):
        a = k * 360 / 14
        x, y = math.cos(math.radians(a)) * 1.4, math.sin(math.radians(a)) * 1.4
        c.b((x, y, .15), (.4, .3, .3), "pierre", rot=(0, 0, a + 90))
    c.cyl((0, 0, .02), 1.3, .04, "suie", segments=14)
    f = c.cyl((0, 0, .06), .7, .03, "braises", segments=10)
    c.eclaire(f)
    for k in range(5):
        c.cyl((0, 0, .15), .1, 1.6, "suie", segments=6, rot=(90, 0, k * 36))
    c.socket("SOCKET_Fumee", (0, 0, .4))
    c.socket("SOCKET_Travail_01", (0, -1.8, 0))


# ---------------------------------------------------------------------------
# Enclos (fiche 03 § 9)
# ---------------------------------------------------------------------------
def cloture(c, piece, haut=.9):
    """Clôture de ranch à 3 lisses, 1 case le long de X. Pièces : Droit, Coin, Porte."""
    def poteau(x, y):
        c.b((x, y, haut / 2 + .05), (.1, .1, haut + .1), "bois_gris")
    if piece == "Porte":
        poteau(-1.0, 0)
        poteau(1.0, 0)
        for z in (haut * .3, haut * .6, haut * .9):
            c.b((0, 0, z), (1.8, .05, .1), "bois_neuf")
        c.b((0, 0, haut * .6), (1.9, .05, .1), "bois_neuf", rot=(0, math.degrees(math.atan2(haut * .6, 1.8)), 0))
        c.socket("SOCKET_Porte", (0, -.8, 0))
        return
    poteau(-1.0, 0)
    for z in (haut * .3, haut * .6, haut * .9):
        c.b((0, 0, z), (2.05, .05, .1), "bois_gris")
    if piece == "Coin":
        for z in (haut * .3, haut * .6, haut * .9):
            c.b((-1.0, 1.0, z), (.05, 2.05, .1), "bois_gris")


def enclos_betail(c, etat):
    """4 × 4 cases (8 × 8 m) : abri à toit de tôle au fond, abreuvoir-baignoire, botte de foin.
    La clôture est posée par le jeu avec les pièces SM_Cloture_* (non incluse ici)."""
    rnd = random.Random(9)
    c.b((0, 0, .015), (7.8, 7.8, .03), "terre")
    # abri au fond (+Y)
    for x in (-2.5, -.8, .8, 2.5):
        c.poteau(x, 2.2, 1.7, "bois_gris", e=.12)
        c.poteau(x, 3.6, 1.4, "bois_gris", e=.12)
    c.b((0, 2.9, 1.6), (5.6, 2.0, .05), "tole_rouillee", rot=(-9, 0, 0))
    c.b((0, 3.65, .7), (5.2, .06, 1.4), "bois_gris")
    c.b((0, 3.0, .25), (4.5, 1.0, .5), "paille", biseau=.05)
    # abreuvoir en baignoire récupérée
    c.b((-2.3, -1.2, .25), (1.2, .6, .5), "blanc_ferme", biseau=.12, effile=(1.05, 1.1))
    c.b((-2.3, -1.2, .47), (1.0, .45, .03), "eau_sale")
    c.cyl((1.8, -.8, .45), .45, .7, "paille", segments=10, rot=(90, 0, 0))
    c.b((.4, 1.4, .3), (1.6, .35, .4), "bois_gris")  # mangeoire
    c.socket("SOCKET_Travail_01", (.4, .9, 0))
    c.socket("SOCKET_Travail_02", (-2.3, -1.9, 0))
    for i in range(6):
        c.socket("SOCKET_Animal_%02d" % (i + 1), (-2.4 + (i % 3) * 2.4, -2.6 + (i // 3) * 2.4, 0))
    c.herbes(rnd, 8, 3.8, 3.8, coul="feuillage", h=(.1, .25))


def enclos_chevaux(c, etat):
    """4 × 5 cases (8 × 10 m) : écurie ouverte à 3 stalles au fond, râtelier, selles, abreuvoir-fût."""
    rnd = random.Random(10)
    c.b((0, 0, .015), (7.8, 9.8, .03), "terre")
    yb = 3.4
    for k in range(4):
        x = -2.7 + k * 1.8
        c.poteau(x, yb, 2.0, "bois_gris", e=.14)
        c.poteau(x, yb + 1.4, 1.8, "bois_gris", e=.14)
        c.b((x, yb + .7, .6), (.08, 1.4, 1.2), "bois_gris")  # cloison de stalle
    c.b((0, yb + .7, 1.95), (6.0, 2.0, .06), "tole_galva", rot=(-6, 0, 0))
    c.b((0, yb + 1.45, .9), (5.6, .06, 1.8), "bois_gris")
    for k in range(3):
        c.b((-1.8 + k * 1.8, yb + 1.3, 1.1), (1.0, .25, .4), "paille", effile=(1.1, 1.6))
    # barrière avec selles
    c.b((-1.0, -1.5, .9), (2.4, .08, .08), "bois_gris")
    for x in (-2.1, .1):
        c.poteau(x, -1.5, 1.0, "bois_gris", e=.1)
    for x in (-1.6, -.5):
        c.b((x, -1.5, .98), (.45, .5, .12), "cuir", biseau=.05)
        c.b((x, -1.5, .78), (.4, .52, .3), "cuir_clair")
    # abreuvoir en fût coupé
    c.cyl((2.3, -.8, .3), .45, .6, "rouille", segments=10)
    c.cyl((2.3, -.8, .55), .42, .02, "eau_sale", segments=10)
    c.socket("SOCKET_Travail_01", (-1.0, -2.1, 0))
    c.socket("SOCKET_Travail_02", (2.3, -1.6, 0))
    for i in range(4):
        c.socket("SOCKET_Animal_%02d" % (i + 1), (-2.7 + i * 1.8 + .9, yb + .6 if i < 3 else -3.0, 0))
    c.herbes(rnd, 10, 3.8, 4.8, coul="feuillage", h=(.1, .25))


# ---------------------------------------------------------------------------
# Catalogue : nom -> (fonction, état ou pièce, emprise en cases, budget de triangles LOD0)
# ---------------------------------------------------------------------------
BATIMENTS = {
    "SM_Ferme_CorpsDeFerme_Fini": (corps_de_ferme, "Fini", (3, 3), 4000),
    "SM_Ferme_CorpsDeFerme_Ruine": (corps_de_ferme, "Ruine", (3, 3), 4000),
    "SM_Ferme_Grange_Fini": (grange, "Fini", (3, 4), 4500),
    "SM_Ferme_Grange_Ruine": (grange, "Ruine", (3, 4), 4500),
    "SM_Ferme_Poulailler_Fini": (poulailler, "Fini", (2, 2), 1500),
    "SM_Ferme_Silo_Fini": (silo, "Fini", (1, 1), 1200),
    "SM_Ferme_Hangar_Fini": (hangar, "Fini", (3, 3), 2500),
    "SM_TourDeGuet_Fini": (tour_de_guet, "Fini", (1, 1), 1200),
    "SM_TasDeStockage_Plein": (tas_de_stockage, "Fini", (2, 2), 1400),
    "SM_Tente_Fini": (tente, "Fini", (1, 1), 400),
    "SM_CabaneBucheron_Fini": (cabane_bucheron, "Fini", (2, 2), 1500),
    "SM_Puits_Fini": (puits, "Fini", (1, 1), 500),
    "SM_JardinPotager_Fini": (potager, "Fini", (2, 2), 1100),
    "SM_FosseIncineration_Fini": (fosse_incineration, "Fini", (2, 2), 600),
    "SM_EnclosBetail_Fini": (enclos_betail, "Fini", (4, 4), 1200),
    "SM_EnclosChevaux_Fini": (enclos_chevaux, "Fini", (4, 5), 1500),
}
KIT = {  # pièces modulaires : nom -> (fonction, pièce, budget)
    "SM_Palissade_Droit_A": (palissade, "Droit_A", 250),
    "SM_Palissade_Droit_B": (palissade, "Droit_B", 270),
    "SM_Palissade_Droit_C": (palissade, "Droit_C", 270),
    "SM_Palissade_Coin": (palissade, "Coin", 400),
    "SM_Palissade_Fin": (palissade, "Fin", 200),
    "SM_Palissade_Porte": (palissade, "Porte", 600),
    "SM_Cloture_Droit": (cloture, "Droit", 120),
    "SM_Cloture_Coin": (cloture, "Coin", 160),
    "SM_Cloture_Porte": (cloture, "Porte", 160),
    "SM_ClotureChevaux_Droit": (lambda c, p: cloture(c, p, haut=1.1), "Droit", 120),
}


# ---------------------------------------------------------------------------
# Assemblage, niveaux de détail, export
# ---------------------------------------------------------------------------
def creer_materiau_batiments(img256, dossier=None):
    mat = bpy.data.materials.get("MAT_Batiments")
    if mat:
        return mat
    img = bpy.data.images.get("T_Batiments_1024") or bpy.data.images.new("T_Batiments_1024", 1024, 1024,
                                                                         alpha=False)
    # Palette 256 px dans le quart haut-gauche, reste gris neutre réservé à la future trim sheet
    import numpy as np
    src = np.array(img256.pixels[:], dtype=np.float32).reshape(256, 256, 4)
    buf = np.full((1024, 1024, 4), .5, dtype=np.float32)
    buf[..., 3] = 1.0
    buf[768:, :256] = src  # les pixels Blender partent du bas
    img.pixels.foreach_set(buf.ravel())
    if dossier:
        img.filepath_raw = os.path.join(dossier, "T_Batiments_1024.png")
        img.file_format = "PNG"
        img.save()
    base = gp.creer_materiau(img256)
    mat = base.copy()
    mat.name = "MAT_Batiments"
    for n in mat.node_tree.nodes:
        if n.type == "TEX_IMAGE":
            n.image = img
    return mat


def creer_batiment(nom, fn, arg, mat, collection, x=0.0, y=0.0):
    hmax = 7.0
    c = Batisseur(hmax, salete=.25)
    fn(c, arg)
    sockets = c.sockets
    ob = c.vers_objet(nom + "_LOD0", mat)
    for vg in list(ob.vertex_groups):
        ob.vertex_groups.remove(vg)
    # UV ramenées dans le quart haut-gauche de T_Batiments_1024 (palette 256 px sur 1024)
    for d in ob.data.uv_layers.active.data:
        d.uv = (d.uv[0] * .25, 1 - (1 - d.uv[1]) * .25)
    # occlusion recalée sur la hauteur réelle du modèle
    zs = [v.co.z for v in ob.data.vertices]
    ztop = max(zs) if zs else 1.0
    col = ob.data.color_attributes.get("Col")
    if col:
        for i, v in enumerate(ob.data.vertices):
            r, g, b, a = col.data[i].color
            h = max(0.0, min(1.0, v.co.z / max(ztop, .5)))
            col.data[i].color = (.55 + .45 * h, max(g, .35 * (1 - h)), b, 1.0)
    racine = bpy.data.objects.new(nom, None)
    racine.empty_display_size = .3
    collection.objects.link(racine)
    collection.objects.link(ob)
    ob.parent = racine
    lods = [ob]
    for i, ratio in ((1, .5), (2, .22)):
        lod = bpy.data.objects.new(nom + "_LOD%d" % i, ob.data.copy())
        collection.objects.link(lod)
        mod = lod.modifiers.new("Decimate", "DECIMATE")
        mod.ratio = ratio
        bpy.context.view_layer.objects.active = lod
        bpy.ops.object.modifier_apply(modifier=mod.name)
        lod.parent = racine
        lods.append(lod)
    vides = []
    for sn, pos in sockets:
        e = bpy.data.objects.new(sn, None)
        e.empty_display_type = "ARROWS"
        e.empty_display_size = .25
        e.location = pos
        collection.objects.link(e)
        e.parent = racine
        vides.append(e)
    racine.location = (x, y, 0)
    tri = [gp.nb_triangles(o) for o in lods]
    return dict(nom=nom, racine=racine, lods=lods, sockets=vides, tri=tri, hauteur=ztop)


def exporter(b, dossier):
    bpy.ops.object.select_all(action="DESELECT")
    loc = tuple(b["racine"].location)
    b["racine"].location = (0, 0, 0)
    for o in [b["racine"]] + b["lods"] + b["sockets"]:
        o.select_set(True)
    bpy.context.view_layer.objects.active = b["racine"]
    bpy.context.view_layer.update()
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(dossier, b["nom"] + ".fbx"), use_selection=True,
        object_types={"EMPTY", "MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z", axis_up="Y", bake_space_transform=True, use_mesh_modifiers=True,
        mesh_smooth_type="FACE", colors_type="SRGB", path_mode="AUTO", bake_anim=False)
    b["racine"].location = loc


def principal(sortie=None, rendu=True):
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    img = gp.creer_palette(sortie)
    mat = creer_materiau_batiments(img, sortie)
    racine = bpy.context.scene.collection
    col = bpy.data.collections.new("Batiments")
    racine.children.link(col)
    tous = []
    # disposition en rangées pour l'aperçu (espacement selon l'emprise)
    rangées = [["SM_Ferme_CorpsDeFerme_Ruine", "SM_Ferme_CorpsDeFerme_Fini", "SM_Ferme_Grange_Ruine",
                "SM_Ferme_Grange_Fini", "SM_Ferme_Silo_Fini"],
               ["SM_Ferme_Hangar_Fini", "SM_Ferme_Poulailler_Fini", "SM_CabaneBucheron_Fini",
                "SM_TourDeGuet_Fini", "SM_TasDeStockage_Plein", "SM_Tente_Fini", "SM_Puits_Fini"],
               ["SM_JardinPotager_Fini", "SM_FosseIncineration_Fini", "SM_EnclosBetail_Fini",
                "SM_EnclosChevaux_Fini"]]
    y = 0.0
    for rang in rangées:
        x = 0.0
        prof = 0.0
        for nom in rang:
            fn, arg, (cx, cy), budget = BATIMENTS[nom]
            larg = cx * CASE_M
            b = creer_batiment(nom, fn, arg, mat, col, x=x + larg / 2, y=y)
            b["budget"], b["emprise"] = budget, (cx, cy)
            tous.append(b)
            x += larg + 1.0
            prof = max(prof, cy * CASE_M)
        y += prof + 1.5
    kit = []
    x = 0.0
    for nom, (fn, arg, budget) in KIT.items():
        larg = 4.0 if arg == "Porte" and nom.startswith("SM_Palissade") else 2.0
        b = creer_batiment(nom, fn, arg, mat, col, x=x + larg / 2, y=y + 1.0)
        b["budget"], b["emprise"] = budget, (int(larg / 2), 1)
        kit.append(b)
        x += larg + .6
    bilan = ["%-30s emprise %dx%d  hauteur %.1f m  triangles LOD0/1/2 = %s  (budget %d)%s  accroches = %s" % (
        b["nom"], b["emprise"][0], b["emprise"][1], b["hauteur"], "/".join(map(str, b["tri"])), b["budget"],
        "  DEPASSE" if b["tri"][0] > b["budget"] else "", ", ".join(s.name.split(".")[0] for s in b["sockets"]) or "-")
        for b in tous + kit]
    print("\n".join(bilan))
    if sortie:
        os.makedirs(sortie, exist_ok=True)
        with open(os.path.join(sortie, "bilan_batiments.txt"), "w", encoding="utf-8") as f:
            f.write("\n".join(bilan) + "\n")
        for b in tous + kit:
            exporter(b, sortie)
    if sortie and rendu:
        for b in tous + kit:
            for o in b["lods"][1:]:
                o.hide_render = True
        # aperçu seulement : clôtures posées autour des enclos, comme le ferait le jeu
        piece = next(b for b in kit if b["nom"] == "SM_Cloture_Droit")["lods"][0]
        porte = next(b for b in kit if b["nom"] == "SM_Cloture_Porte")["lods"][0]
        haute = next(b for b in kit if b["nom"] == "SM_ClotureChevaux_Droit")["lods"][0]
        for b in tous:
            if not b["nom"].startswith("SM_Enclos"):
                continue
            nx, ny = b["emprise"]
            ox, oy = b["racine"].location.x, b["racine"].location.y
            modele = haute if "Chevaux" in b["nom"] else piece
            for cote in range(4):
                n = nx if cote % 2 == 0 else ny
                for i in range(n):
                    u = -n + 1 + 2 * i
                    if cote == 0:
                        x, y, rz = ox + u, oy - ny, 0
                    elif cote == 1:
                        x, y, rz = ox + nx, oy + u, 90
                    elif cote == 2:
                        x, y, rz = ox - u, oy + ny, 180
                    else:
                        x, y, rz = ox - nx, oy - u, 270
                    src = porte if (cote == 0 and i == n // 2) else modele
                    d = bpy.data.objects.new(b["nom"] + "_apercu_cloture", src.data)
                    col.objects.link(d)
                    d.location = (x, y, 0)
                    d.rotation_euler = (0, 0, math.radians(rz))
        # un survivant pour l'échelle devant la ferme
        ech = gp.creer_personnage("Survivant_Echelle", gp.SURVIVANTS["Ouvrier"], gp.creer_materiau(img), col,
                                  x=0)
        ferme = next(b for b in tous if b["nom"] == "SM_Ferme_CorpsDeFerme_Fini")
        ech["arm"].location = (ferme["racine"].location.x - 1.2, -4.0, 0)
        for o in ech["objets"]:
            if "_LOD1" in o.name or "_LOD2" in o.name:
                o.hide_render = True
        sc = gp.scene_rendu(1900, 1100, 0, 10)
        cam = sc.camera
        sol = bpy.data.objects.get("Sol")
        if sol:
            sol.scale = (4, 4, 1)
        # vue de jeu : 3/4 plongeante, comme la caméra d'After City
        def vue(cible, ortho, chemin, larg=1900, haut=1100):
            sc.render.resolution_x, sc.render.resolution_y = larg, haut
            cam.data.ortho_scale = ortho
            d = Vector((-.55, -1.0, .95)).normalized() * 60
            cam.location = Vector(cible) + d
            cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
            cam.data.clip_end = 200
            gp.rendre(chemin, sc)
        xs = [b["racine"].location.x for b in tous]
        ys = [b["racine"].location.y for b in tous]
        r0 = [b for b in tous if b["nom"] in rangées[0]]
        vue((sum(b["racine"].location.x for b in r0) / len(r0), 0, 1.5), 30,
            os.path.join(sortie, "apercu_ferme.png"))
        r12 = [b for b in tous if b["nom"] in rangées[1] + rangées[2]]
        cx = (min(b["racine"].location.x for b in r12) + max(b["racine"].location.x for b in r12)) / 2
        cy = (min(b["racine"].location.y for b in r12) + max(b["racine"].location.y for b in r12)) / 2
        vue((cx, cy, .8), 30, os.path.join(sortie, "apercu_batiments.png"))
        kx = (kit[0]["racine"].location.x + kit[-1]["racine"].location.x) / 2
        vue((kx, kit[0]["racine"].location.y, .6), 24, os.path.join(sortie, "apercu_palissade.png"), 1900, 700)
        del xs, ys
    if sortie:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(sortie, "AfterCity_Batiments.blend"))
    return tous + kit


if __name__ == "__main__":
    _sortie, _rendu = gp._args()
    principal(_sortie, _rendu)
