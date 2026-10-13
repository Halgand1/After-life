"""
After City — génération des animaux (cerf, sanglier, cheval, vache) dans Blender.

Fiche : 04_animaux.md. Réutilise la palette, le matériau et le constructeur de
generer_personnages.py (à garder dans le même dossier).

Utilisation :
  blender --background --python generer_animaux.py -- --sortie CHEMIN [--sans-rendu]

Squelette RIG_Quadrupede (19 os, type Generic dans Unity). Comportements (GDD, retours de
Sam) : le cerf fuit, le sanglier charge, aucun n'attaque le premier. Le cheval et la vache
vivent dans les enclos.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

import generer_personnages as gp  # noqa: E402

OS_Q = ["Hips", "Spine", "Chest", "Neck", "Head", "Queue",
        "AvantHaut_L", "AvantBas_L", "AvantSabot_L", "AvantHaut_R", "AvantBas_R", "AvantSabot_R",
        "ArriereHaut_L", "ArriereBas_L", "ArriereSabot_L", "ArriereHaut_R", "ArriereBas_R", "ArriereSabot_R"]

# Dimensions à l'échelle jeu (survivant = 1,00 m). L = longueur du corps, H = hauteur au garrot.
ANIMAUX = {
    "Cerf": dict(L=.72, H=.62, corps_h=.24, corps_l=.17, patte=.038, cou=.30, cou_angle=58,
                 tete_l=.19, tete_h=.085, tete_w=.075, pelage="cerf_pelage", ventre="cerf_ventre",
                 queue="liste_blanche", bois=True, oreilles=True, budget=900,
                 sprint="ANIM_Animal_Fuite"),
    "Sanglier": dict(L=.62, H=.38, corps_h=.27, corps_l=.22, patte=.05, cou=.1, cou_angle=12,
                     tete_l=.22, tete_h=.13, tete_w=.11, pelage="sanglier_poil", ventre="sanglier_poil",
                     queue="sanglier_crete", defenses=True, crete=True, groin=True, oreilles=True,
                     budget=800, sprint="ANIM_Animal_Charge"),
    "Cheval": dict(L=1.0, H=.86, corps_h=.36, corps_l=.27, patte=.058, cou=.42, cou_angle=52,
                   tete_l=.3, tete_h=.12, tete_w=.1, pelage="cheval_bai", ventre="cheval_bai",
                   queue="crin_noir", crinière=True, oreilles=True, liste=True, budget=1100,
                   sprint="ANIM_Animal_Galop"),
    "Vache": dict(L=.95, H=.68, corps_h=.38, corps_l=.32, patte=.07, cou=.2, cou_angle=22,
                  tete_l=.25, tete_h=.15, tete_w=.14, pelage="vache_blanc", ventre="vache_blanc",
                  queue="vache_noir", taches=True, cornes=True, mufle=True, pis=True, oreilles=True,
                  budget=1000, sprint="ANIM_Animal_Course"),
}


def reperes_q(a):
    r = {}
    r["z_dos"] = a["H"]
    r["z_ventre"] = a["H"] - a["corps_h"]
    r["y_avant"] = -a["L"] / 2 + a["patte"] * 1.6  # pattes avant (l'animal regarde vers -Y)
    r["y_arriere"] = a["L"] / 2 - a["patte"] * 1.6
    r["x_patte"] = a["corps_l"] / 2 - a["patte"] / 2 - .005
    r["z_attache"] = r["z_ventre"] + a["corps_h"] * .35
    r["seg"] = (r["z_attache"] - .03) / 2
    ang = math.radians(a["cou_angle"])
    r["base_cou"] = Vector((0, -a["L"] / 2 + .04, a["H"] - a["corps_h"] * .25))
    r["dir_cou"] = Vector((0, -math.cos(ang), math.sin(ang)))
    r["tete"] = r["base_cou"] + r["dir_cou"] * a["cou"]
    return r


def construire(a):
    r = reperes_q(a)
    c = gp.Constructeur(hauteur=a["H"] + .3, salete=.25, os_liste=OS_Q)
    pel, ventre = a["pelage"], a["ventre"]
    L, zc = a["L"], a["H"] - a["corps_h"] / 2
    w, h = a["corps_l"], a["corps_h"]
    # Corps en trois blocs : croupe (Hips), milieu (Spine), poitrail (Chest)
    c.boite((0, L * .32, zc), (w, L * .38, h), pel, "Hips", biseau=.04, effile=(.95, 1.0))
    c.boite((0, 0, zc - .005), (w * 1.04, L * .34, h * 1.02), pel, "Spine", biseau=.04)
    c.boite((0, -L * .32, zc + h * .03), (w * .98, L * .38, h * 1.04), pel, "Chest", biseau=.04,
            effile=(.92, 1.0))
    if ventre != pel:
        c.boite((0, 0, a["H"] - h + .012), (w * .8, L * .7, .03), ventre, "Spine", biseau=0)
    if a.get("taches"):
        for (y, x, sx, sy) in ((.25, 1, .18, .2), (-.15, -1, .22, .16), (.05, 1, .12, .14)):
            c.boite((x * (w / 2 + .002), y * L, zc + .02), (.01, sy, sx), "vache_noir", "Spine", biseau=0)
        c.boite((0, L * .3, a["H"] + .002), (w * .5, .18, .01), "vache_noir", "Hips", biseau=0)
    if a.get("pis"):
        c.boite((0, L * .22, a["H"] - h - .02), (.09, .08, .05), "mufle_rose", "Hips", biseau=.015)
    if a.get("crete"):  # crête de poils raides du sanglier
        for k in range(5):
            c.boite((0, -L * .4 + k * L * .17, a["H"] + .015), (.025, .1, .05), "sanglier_crete",
                    ["Chest", "Chest", "Spine", "Spine", "Hips"][k], biseau=0, effile=(.3, .7))
    # Pattes
    for avant in (True, False):
        y = r["y_avant"] if avant else r["y_arriere"]
        pre = "Avant" if avant else "Arriere"
        for cote, sx in (("L", 1), ("R", -1)):
            x = sx * r["x_patte"]
            seg = r["seg"]
            ep = a["patte"] * (1.35 if not avant else 1.15)
            c.boite((x, y, r["z_attache"] - seg / 2), (ep, ep * 1.3, seg + .03), pel,
                    pre + "Haut_" + cote, biseau=.012, effile_bas=(.75, .7))
            c.boite((x, y, .03 + seg / 2), (a["patte"] * .8, a["patte"] * .85, seg), pel,
                    pre + "Bas_" + cote, biseau=.008)
            c.boite((x, y - .006, .015), (a["patte"] * .95, a["patte"] * 1.1, .03), "sabot",
                    pre + "Sabot_" + cote, biseau=0)
    # Cou et tête
    bc, dc, tp = r["base_cou"], r["dir_cou"], r["tete"]
    milieu = bc + dc * (a["cou"] / 2)
    ang = a["cou_angle"]
    c.boite(tuple(milieu), (w * .62, h * .55, a["cou"] + .08), pel, "Neck", biseau=.025,
            rot=(90 - ang, 0, 0), effile=(.8, .85))
    if a.get("crinière"):
        c.boite(tuple(milieu + Vector((0, .03, .045))), (.035, .05, a["cou"] + .06), "crin_noir", "Neck",
                biseau=0, rot=(90 - ang, 0, 0))
        c.boite((0, -L / 2 + .02, a["H"] + .03), (.03, .08, .05), "crin_noir", "Chest", biseau=0)
    tl, th, tw = a["tete_l"], a["tete_h"], a["tete_w"]
    centre_t = tp + Vector((0, -tl * .35, -th * .25))
    c.boite(tuple(centre_t), (tw, tl, th), pel, "Head", biseau=.025, rot=(-12, 0, 0),
            effile=(.85, .8), effile_bas=(.9, .9))
    nez = centre_t + Vector((0, -tl * .5, -th * .1))
    if a.get("groin"):
        c.cylindre(tuple(nez + Vector((0, -.01, 0))), tw * .45, .03, "groin", "Head", segments=8,
                   rot=(90, 0, 0))
    if a.get("mufle"):
        c.boite(tuple(nez + Vector((0, .01, -.01))), (tw * 1.05, .05, th * .7), "mufle_rose", "Head", biseau=.015)
    if a.get("liste"):
        c.boite(tuple(centre_t + Vector((0, -.02, th * .51))), (.025, tl * .7, .006), "liste_blanche", "Head",
                biseau=0, rot=(-12, 0, 0))
    if a.get("defenses"):
        for sx in (1, -1):
            c.boite(tuple(nez + Vector((sx * tw * .45, .03, -.005))), (.014, .014, .06), "defense", "Head",
                    biseau=0, rot=(-25, sx * 20, 0), effile=(.3, .3))
    for sx in (1, -1):  # yeux
        c.boite(tuple(centre_t + Vector((sx * tw * .5, tl * .1, th * .2))), (.008, .018, .018), "yeux", "Head",
                biseau=0)
    if a.get("oreilles"):
        for sx in (1, -1):
            c.boite(tuple(centre_t + Vector((sx * tw * .45, tl * .38, th * .55))), (.03, .015, .07), pel,
                    "Head", biseau=0, rot=(-20, sx * 35, 0), effile=(.5, 1))
    if a.get("cornes"):
        for sx in (1, -1):
            c.boite(tuple(centre_t + Vector((sx * tw * .55, tl * .3, th * .55))), (.06, .022, .022), "bois_cerf",
                    "Head", biseau=0, rot=(0, sx * -25, 0), effile=(.4, .4))
    if a.get("bois"):  # bois du cerf : merrain + andouillers
        for sx in (1, -1):
            base = centre_t + Vector((sx * tw * .3, tl * .32, th * .5))
            c.boite(tuple(base + Vector((sx * .03, .02, .09))), (.016, .016, .2), "bois_cerf", "Head", biseau=0,
                    rot=(-20, sx * 25, 0))
            for k in range(3):
                c.boite(tuple(base + Vector((sx * (.05 + k * .015), -.01 + k * .01, .08 + k * .05))),
                        (.012, .012, .08), "bois_cerf", "Head", biseau=0, rot=(35, sx * -15, 0))
    # Queue
    lq = .3 if a["queue"] == "crin_noir" else (.1 if a.get("bois") else .22)
    if a.get("bois"):  # queue courte du cerf, relevée
        c.boite((0, L / 2 + .03, a["H"] - h * .15), (.045, .04, lq), a["queue"], "Queue", biseau=0, rot=(-35, 0, 0))
    else:  # queue qui pend, légèrement écartée de la croupe
        c.boite((0, L / 2 + .02 + lq * .17, a["H"] - h * .2 - lq / 2), (.06 if a.get("crinière") else .03, .04, lq),
                a["queue"], "Queue", biseau=0, rot=(20, 0, 0), effile_bas=(1.3 if a.get("crinière") else 1, 1))
    return c, r


def creer_rig_q(nom, a, r, collection):
    arm_data = bpy.data.armatures.new(nom + "_Rig")
    arm = bpy.data.objects.new(nom, arm_data)
    collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm_data.edit_bones
    haut, avant = Vector((0, 0, 1)), Vector((0, -1, 0))
    L, zc = a["L"], a["H"] - a["corps_h"] / 2

    def os_(n, t, q, parent=None, roll_vers=haut):
        b = eb.new(n)
        b.head, b.tail = Vector(t), Vector(q)
        if parent:
            b.parent = eb[parent]
        b.align_roll(roll_vers)

    os_("Hips", (0, L * .45, zc), (0, L * .15, zc))
    os_("Spine", (0, L * .15, zc), (0, -L * .15, zc), "Hips")
    os_("Chest", (0, -L * .15, zc), (0, -L * .45, zc), "Spine")
    os_("Neck", tuple(r["base_cou"]), tuple(r["tete"]), "Chest")
    os_("Head", tuple(r["tete"]), tuple(r["tete"] + Vector((0, -a["tete_l"], -.03))), "Neck")
    os_("Queue", (0, L / 2, a["H"] - a["corps_h"] * .15), (0, L / 2 + .1, a["H"] - a["corps_h"] * .4), "Hips")
    for av in (True, False):
        y = r["y_avant"] if av else r["y_arriere"]
        pre = "Avant" if av else "Arriere"
        parent = "Chest" if av else "Hips"
        for cote, sx in (("L", 1), ("R", -1)):
            x = sx * r["x_patte"]
            za, seg = r["z_attache"], r["seg"]
            os_(pre + "Haut_" + cote, (x, y, za), (x, y, za - seg), parent, roll_vers=avant)
            os_(pre + "Bas_" + cote, (x, y, za - seg), (x, y, .03), pre + "Haut_" + cote, roll_vers=avant)
            os_(pre + "Sabot_" + cote, (x, y, .03), (x, y - .05, .01), pre + "Bas_" + cote)
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in arm.pose.bones:
        pb.rotation_mode = "XYZ"
    return arm


S = gp.S


def animations(arm, nom, a):
    sprint = a["sprint"]
    herbivore = nom != "Sanglier"

    def jambes(t, ph, amp, flex):
        d = {}
        for (pre, cote), p0 in ph.items():
            v = S(t, 1, p0)
            lev = max(0.0, S(t, 1, p0 + .25))
            d[pre + "Haut_" + cote] = (amp * v, 0, 0)
            d[pre + "Bas_" + cote] = (-flex * lev, 0, 0)
            d[pre + "Sabot_" + cote] = (flex * .5 * lev, 0, 0)
        return d

    marche_ph = {("Arriere", "L"): 0, ("Avant", "L"): .25, ("Arriere", "R"): .5, ("Avant", "R"): .75}
    galop_ph = {("Avant", "L"): 0, ("Avant", "R"): .1, ("Arriere", "L"): .5, ("Arriere", "R"): .6}

    def repos(t):
        return {"Chest": (1.0 * S(t, 2), 0, 0), "Neck": (3 * S(t, 1, .3), 0, 6 * S(t)),
                "Head": (2 * S(t, 2), 0, 0), "Queue": (0, 0, 15 * S(t, 3))}

    def marche(t):
        d = jambes(t, marche_ph, 20, 35)
        d.update({"Neck": (4 * S(t, 2), 0, 0), "Spine": (0, 0, 3 * S(t)), "Queue": (0, 0, 10 * S(t, 2)),
                  "Hips": ("loc", .008 * abs(S(t, 2)))})
        return d

    def galop(t, tete=0.0, queue=0.0):
        d = jambes(t, galop_ph, 38, 60)
        d.update({"Spine": (5 * S(t), 0, 0), "Chest": (4 * S(t, 1, .2), 0, 0),
                  "Neck": (tete + 8 * S(t, 1, .1), 0, 0), "Queue": (queue, 0, 0),
                  "Hips": ("loc", .03 * abs(S(t, 1, .1)))})
        return d

    def brouter(t):
        return {"Neck": (-55, 0, 4 * S(t)), "Head": (-25 + 5 * abs(S(t, 3)), 0, 0),
                "AvantHaut_L": (6, 0, 0), "AvantHaut_R": (-4, 0, 0), "Queue": (0, 0, 12 * S(t, 2))}

    acts = [gp._keys(arm, "ANIM_Animal_Repos", 60, repos),
            gp._keys(arm, "ANIM_Animal_Marche", 32, marche)]
    if sprint == "ANIM_Animal_Fuite":  # le cerf fuit : tête haute, queue blanche dressée
        acts.append(gp._keys(arm, sprint, 14, lambda t: galop(t, tete=10, queue=55)))
    elif sprint == "ANIM_Animal_Charge":  # le sanglier charge : tête basse, défenses en avant
        acts.append(gp._keys(arm, sprint, 12, lambda t: galop(t, tete=-12, queue=20)))
    else:
        acts.append(gp._keys(arm, sprint, 16, galop))
    if herbivore:
        acts.append(gp._keys(arm, "ANIM_Animal_Brouter", 60, brouter))
    else:  # le sanglier fouille le sol du groin
        acts.append(gp._keys(arm, "ANIM_Animal_Fouir", 40, lambda t: {
            "Neck": (-20, 0, 6 * S(t, 2)), "Head": (-20 + 10 * abs(S(t, 2)), 0, 0)}))
    return acts


def creer_animal(nom, a, mat, collection, x=0.0):
    col = bpy.data.collections.new(nom)
    collection.children.link(col)
    c, r = construire(a)
    arm = creer_rig_q("RIG_" + nom, a, r, col)
    ob = c.vers_objet("SK_%s_LOD0" % nom, mat)
    col.objects.link(ob)
    ob.parent = arm
    m = ob.modifiers.new("Armature", "ARMATURE")
    m.object = arm
    lods = gp.faire_lods(ob, arm, col)
    acts = animations(arm, nom, a)
    arm.location.x = x
    return dict(nom=nom, arm=arm, objets=[arm] + lods, actions=acts, lods=lods,
                tri=[gp.nb_triangles(l) for l in lods], hauteur=max(a["H"], r["tete"].z + a["tete_h"]),
                equip={})


def principal(sortie=None, rendu=True):
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    img = gp.creer_palette(sortie)
    mat = gp.creer_materiau(img)
    racine = bpy.context.scene.collection
    col = bpy.data.collections.new("Animaux")
    racine.children.link(col)
    animaux = []
    x = 0.0
    for nom, a in ANIMAUX.items():
        animaux.append(creer_animal(nom, a, mat, col, x=x))
        x += a["corps_l"] + .75
    # un survivant pour l'échelle
    ech = gp.creer_personnage("Survivant_Echelle", gp.SURVIVANTS["Ouvrier"], mat, col, x=x - .2)
    bilan = ["%-10s triangles LOD0/1/2 = %s  animations = %s" % (
        p["nom"], "/".join(map(str, p["tri"])),
        ", ".join(ac.name.split(".")[0].replace("ANIM_", "") for ac in p["actions"])) for p in animaux]
    print("\n".join(bilan))
    if sortie:
        os.makedirs(sortie, exist_ok=True)
        with open(os.path.join(sortie, "bilan_animaux.txt"), "w", encoding="utf-8") as f:
            f.write("\n".join(bilan) + "\n")
        for p in animaux:
            gp.preparer_nla(p)
            gp.exporter_fbx(p, sortie)
            for tr in p["arm"].animation_data.nla_tracks:
                tr.mute = True
    if sortie and rendu:
        gp.masquer_lods(animaux + [ech])
        poses = {"Cerf": ("ANIM_Animal_Repos", 20), "Sanglier": ("ANIM_Animal_Charge", 4),
                 "Cheval": ("ANIM_Animal_Marche", 10), "Vache": ("ANIM_Animal_Brouter", 30)}
        for p in animaux:
            anim, image = poses[p["nom"]]
            act = next(ac for ac in p["actions"] if ac.name.split(".")[0] == anim)
            gp.assigner_action(p["arm"], act, image)
            p["image"] = image
        gp.assigner_action(ech["arm"], ech["actions"][0], 20)
        cx = (animaux[0]["arm"].location.x + ech["arm"].location.x) / 2
        sc = gp.scene_rendu(1800, 760, cx, 4.4, cam_y=-3, cam_z=2.2, cible_z=.4)
        cam = sc.camera
        cam.location = (cx - 2.6, -4.6, 2.3)
        d = Vector((cx, 0, .4)) - cam.location
        cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
        # chaque animal montre sa propre image de pose
        for p in animaux:
            if p["image"] != 20:
                decaler_action(p["arm"], p["image"] - 20)
        sc.frame_set(20)
        gp.rendre(os.path.join(sortie, "apercu_animaux.png"), sc)
    if sortie:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(sortie, "AfterCity_Animaux.blend"))
    return animaux


def decaler_action(arm, decalage):
    """Pour l'aperçu seulement : joue l'action via une piste NLA décalée."""
    ad = arm.animation_data
    act = ad.action
    ad.action = None
    tr = ad.nla_tracks.new()
    st = tr.strips.new("apercu", int(act.frame_range[0]) - decalage, act)
    st.name = "apercu"


if __name__ == "__main__":
    _sortie, _rendu = gp._args()
    principal(_sortie, _rendu)
