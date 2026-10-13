"""
Apercu rapide d'un .blend (rendu Workbench, couleurs des materiaux).

Usage :
    blender.exe --background <fichier.blend> --python Tools/Blender/render_preview.py -- <sortie.png>
"""
import math
import sys

import bpy
from mathutils import Vector

out = sys.argv[sys.argv.index("--") + 1]
scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "MATERIAL"
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.render.resolution_x = 640
scene.render.resolution_y = 640
scene.render.film_transparent = False
scene.frame_set(1)

cam_data = bpy.data.cameras.new("PreviewCam")
cam_data.lens = 70
cam = bpy.data.objects.new("PreviewCam", cam_data)
scene.collection.objects.link(cam)
cam.location = Vector((1.9, -2.6, 1.6))
direction = Vector((0, 0, 0.5)) - cam.location
cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
scene.camera = cam

scene.render.filepath = out
bpy.ops.render.render(write_still=True)
print("[AfterCity] apercu :", out)
