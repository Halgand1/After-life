"""
Villageois et zombie de test, style trapu facon Kingdoms and Castles, theme survie.

Usage :
    blender.exe --background --factory-startup --python Tools/Blender/make_villager_test.py -- <repo> [villager|zombie]

Produit :
    Art/Characters/SK_Villager_Test.blend
    Game/Assets/Art/Characters/SK_Villager_Test.fbx  (animations ANIM_Villager_Idle et ANIM_Villager_Walk)

Conventions : 1 m de haut environ, pieds a l'origine, axe Z vers le haut dans Blender (Y dans Unity).
"""
import math
import os
import sys

import bpy
import bmesh
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
REPO = os.path.abspath(argv[0] if argv else os.path.join(os.path.dirname(__file__), "..", ".."))
# Variante : "villager" (survivant) ou "zombie" (rodeur : peau verdatre, bras tendus, demarche titubante)
ZOMBIE = len(argv) > 1 and argv[1] == "zombie"
NAME = "Zombie" if ZOMBIE else "Villager"
BLEND_PATH = os.path.join(REPO, "Art", "Characters", f"SK_{NAME}_Test.blend")
FBX_PATH = os.path.join(REPO, "Game", "Assets", "Art", "Characters", f"SK_{NAME}_Test.fbx")

FPS = 30

# Remise a zero de la scene
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.fps = FPS
scene.unit_settings.system = "METRIC"


def material(name, rgb, rough=0.8):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Roughness"].default_value = rough
    m.diffuse_color = (*rgb, 1.0)
    return m


MATS = {
    "skin": material("MAT_Villager_Skin", (0.87, 0.64, 0.50)),
    "jacket": material("MAT_Villager_Jacket", (0.33, 0.40, 0.24)),   # veste kaki usee
    "pants": material("MAT_Villager_Pants", (0.25, 0.27, 0.33)),     # jean delave
    "boots": material("MAT_Villager_Boots", (0.22, 0.15, 0.10)),
    "hair": material("MAT_Villager_Hair", (0.30, 0.20, 0.12)),
    "scarf": material("MAT_Villager_Scarf", (0.66, 0.22, 0.16)),     # foulard rouge
    "pack": material("MAT_Villager_Pack", (0.47, 0.36, 0.22)),       # sac a dos
}
if ZOMBIE:
    MATS["skin"] = material("MAT_Zombie_Skin", (0.50, 0.60, 0.42))
    MATS["jacket"] = material("MAT_Zombie_Shirt", (0.45, 0.42, 0.36))   # chemise sale
    MATS["pants"] = material("MAT_Zombie_Pants", (0.22, 0.24, 0.30))
    MATS["hair"] = material("MAT_Zombie_Hair", (0.16, 0.15, 0.13))
    MATS["scarf"] = material("MAT_Zombie_Blood", (0.42, 0.06, 0.05))   # col tache de sang
    MATS["pack"] = material("MAT_Zombie_Eyes", (0.95, 0.85, 0.30))     # yeux jaunes
MAT_ORDER = list(MATS.keys())

bm = bmesh.new()
deform = bm.verts.layers.deform.verify()
mat_index = {k: i for i, k in enumerate(MAT_ORDER)}

# Groupes d'os : 0 = Root (jambes), 1 = Body (tronc, tete, bras)
GROUPS = ["Root", "Body"]


def add_part(make, mat, group, matrix):
    """Ajoute une primitive au bmesh commun, avec materiau et groupe de deformation."""
    tmp = bmesh.new()
    make(tmp)
    bmesh.ops.transform(tmp, matrix=matrix, verts=tmp.verts)
    tmp_mesh = bpy.data.meshes.new("tmp")
    tmp.to_mesh(tmp_mesh)
    tmp.free()
    offset = len(bm.verts)
    bm.from_mesh(tmp_mesh)
    bpy.data.meshes.remove(tmp_mesh)
    bm.verts.ensure_lookup_table()
    new_verts = bm.verts[offset:]
    vset = set(new_verts)
    for f in bm.faces:
        if f.verts[0] in vset:
            f.material_index = mat_index[mat]
            f.smooth = False
    for v in new_verts:
        v[deform][GROUPS.index(group)] = 1.0


def sphere(seg, rings, r):
    return lambda b: bmesh.ops.create_uvsphere(b, u_segments=seg, v_segments=rings, radius=r)


def cyl(seg, r1, r2, depth):
    return lambda b: bmesh.ops.create_cone(b, cap_ends=True, segments=seg, radius1=r1, radius2=r2, depth=depth)


def cube(size=1.0):
    return lambda b: bmesh.ops.create_cube(b, size=size)


def T(x, y, z):
    return Matrix.Translation((x, y, z))


def S(x, y, z):
    return Matrix.Diagonal((x, y, z, 1.0))


# Jambes et bottes
for side in (-1, 1):
    add_part(cyl(8, 0.075, 0.07, 0.24), "pants", "Root", T(side * 0.085, 0, 0.20))
    add_part(cube(1.0), "boots", "Root", T(side * 0.085, -0.025, 0.045) @ S(0.13, 0.19, 0.09))

# Tronc rond (veste) : sphere ecrasee, facon K&C
add_part(sphere(12, 8, 1.0), "jacket", "Body", T(0, 0, 0.50) @ S(0.21, 0.18, 0.25))
# Ceinture / bas de veste
add_part(cyl(12, 0.19, 0.17, 0.06), "pants", "Body", T(0, 0, 0.33))
# Foulard
add_part(cyl(10, 0.12, 0.10, 0.06), "scarf", "Body", T(0, 0, 0.72))
# Tete
add_part(sphere(12, 8, 0.15), "skin", "Body", T(0, 0, 0.86))
# Nez
add_part(sphere(6, 4, 0.03), "skin", "Body", T(0, -0.15, 0.85))
# Cheveux / bonnet : calotte
add_part(sphere(12, 6, 1.0), "hair", "Body", T(0, 0.02, 0.92) @ S(0.16, 0.16, 0.11))
# Bras
if ZOMBIE:
    # Bras tendus vers l'avant (-Y), manches arrachees, yeux jaunes
    for side in (-1, 1):
        fwd = Matrix.Rotation(math.radians(80), 4, "X")
        add_part(cyl(8, 0.05, 0.045, 0.30), "skin", "Body", T(side * 0.17, -0.17, 0.60) @ fwd)
        add_part(sphere(8, 5, 0.05), "skin", "Body", T(side * 0.17, -0.33, 0.58))
        add_part(sphere(6, 4, 0.025), "pack", "Body", T(side * 0.055, -0.135, 0.89))
else:
    for side in (-1, 1):
        rot = Matrix.Rotation(math.radians(-side * 12), 4, "Y")
        add_part(cyl(8, 0.055, 0.045, 0.30), "jacket", "Body", T(side * 0.215, 0, 0.52) @ rot)
        add_part(sphere(8, 5, 0.05), "skin", "Body", T(side * 0.245, 0, 0.36))
    # Sac a dos
    add_part(cube(1.0), "pack", "Body", T(0, 0.19, 0.52) @ S(0.24, 0.12, 0.26))
    add_part(cyl(8, 0.05, 0.05, 0.22), "jacket", "Body",
             T(0, 0.19, 0.69) @ Matrix.Rotation(math.radians(90), 4, "Y"))  # couverture roulee

mesh = bpy.data.meshes.new(f"SM_{NAME}_Test")
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bm.to_mesh(mesh)
tris = sum(len(f.verts) - 2 for f in bm.faces)
bm.free()
for k in MAT_ORDER:
    mesh.materials.append(MATS[k])

body = bpy.data.objects.new(f"SK_{NAME}_Test", mesh)
scene.collection.objects.link(body)
for g in GROUPS:
    body.vertex_groups.new(name=g)
# Le bmesh a deja rempli les poids ; les groupes doivent exister dans le meme ordre (fait ci-dessus).

# Squelette minimal : Root (au sol) -> Body (bassin)
arm_data = bpy.data.armatures.new(f"ARM_{NAME}")
rig = bpy.data.objects.new("Rig", arm_data)
scene.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode="EDIT")
root = arm_data.edit_bones.new("Root")
root.head, root.tail = Vector((0, 0, 0)), Vector((0, 0, 0.25))
bone = arm_data.edit_bones.new("Body")
bone.head, bone.tail = Vector((0, 0, 0.30)), Vector((0, 0, 0.75))
bone.parent = root
bpy.ops.object.mode_set(mode="OBJECT")

body.parent = rig
mod = body.modifiers.new("Armature", "ARMATURE")
mod.object = rig

# Animations : rebond sur l'os Body
rig.animation_data_create()
pb = rig.pose.bones["Body"]
pb.rotation_mode = "XYZ"


def make_action(name, frames, keys):
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    rig.animation_data.action = act
    for f, loc_z, rot_y, rot_x in keys:
        pb.location = (0, loc_z, 0)  # axe Y de l'os = vertical du monde
        pb.rotation_euler = (math.radians(rot_x), math.radians(rot_y), 0)
        pb.keyframe_insert("location", frame=f)
        pb.keyframe_insert("rotation_euler", frame=f)
    act.frame_range = (1, frames)
    return act


if ZOMBIE:
    # Penche en avant, oscille ; marche titubante lente (1,3 s par cycle)
    idle = make_action("ANIM_Zombie_Idle", 61, [
        (1, 0.0, -4, 14), (31, 0.01, 4, 18), (61, 0.0, -4, 14)])
    walk = make_action("ANIM_Zombie_Walk", 41, [
        (1, 0.0, -12, 16), (11, 0.03, 0, 20), (21, 0.0, 12, 16), (31, 0.03, 0, 20), (41, 0.0, -12, 16)])
else:
    # Respiration lente : 2 s
    idle = make_action("ANIM_Villager_Idle", 61, [
        (1, 0.0, 0, 0), (31, 0.02, 0, 0), (61, 0.0, 0, 0)])
    # Marche : petits sauts avec balancement gauche/droite, 0.66 s par cycle
    walk = make_action("ANIM_Villager_Walk", 21, [
        (1, 0.0, -6, 4), (6, 0.05, 0, 4), (11, 0.0, 6, 4), (16, 0.05, 0, 4), (21, 0.0, -6, 4)])

# Pistes NLA pour que l'export FBX garde chaque action comme clip separe
for act in (idle, walk):
    track = rig.animation_data.nla_tracks.new()
    track.name = act.name
    track.strips.new(act.name, 1, act)
rig.animation_data.action = idle
scene.frame_start, scene.frame_end = 1, 61

os.makedirs(os.path.dirname(BLEND_PATH), exist_ok=True)
os.makedirs(os.path.dirname(FBX_PATH), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)

bpy.ops.export_scene.fbx(
    filepath=FBX_PATH,
    use_selection=False,
    object_types={"ARMATURE", "MESH"},
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_UNITS",
    axis_forward="-Z",
    axis_up="Y",
    add_leaf_bones=False,
    bake_anim=True,
    bake_anim_use_all_actions=True,
    bake_anim_use_nla_strips=False,
    bake_anim_force_startend_keying=True,
    mesh_smooth_type="FACE",
)

height = max((rig.matrix_world @ body.matrix_world @ v.co).z for v in mesh.vertices)
print(f"[AfterCity] {NAME} : {tris} triangles, hauteur {height:.2f} m")
print(f"[AfterCity] blend : {BLEND_PATH}")
print(f"[AfterCity] fbx   : {FBX_PATH}")
