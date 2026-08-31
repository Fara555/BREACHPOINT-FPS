import bpy
import math
from pathlib import Path

project = Path(r"A:\UnityProjects\BREACHPOINT - FPS")
asset_dir = project / "Assets" / "Project" / "Art" / "Weapons" / "SR7"
source = asset_dir / "SR7_Breacher_MK2.obj"
blend_path = asset_dir / "SR7_Breacher_MK3.blend"
fbx_path = asset_dir / "SR7_Breacher_MK3.fbx"

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)

bpy.ops.wm.obj_import(filepath=str(source), forward_axis="Y", up_axis="Z")

mesh_objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]

for obj in mesh_objects:
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    smallest = min(max(value, 0.0001) for value in obj.dimensions)
    if smallest > 0.008:
        bevel = obj.modifiers.new(name="Production Bevel", type="BEVEL")
        bevel.width = min(0.0035, smallest * 0.16)
        bevel.segments = 3
        bevel.limit_method = "ANGLE"
        bevel.angle_limit = math.radians(28.0)
        bevel.miter_outer = "MITER_ARC"
        bevel.use_clamp_overlap = True

    for polygon in obj.data.polygons:
        polygon.use_smooth = True

    smooth = obj.modifiers.new(name="Angle Smooth", type="NODES")
    node_group = bpy.data.node_groups.get("Smooth by Angle")
    if node_group is not None:
        smooth.node_group = node_group

    obj.select_set(False)

# Unity-facing attachment transforms. Blender import maps weapon forward to +Y;
# FBX export below converts it back to Unity's +Z convention.
markers = {
    "MuzzleSocket": (0.0, 0.68, 0.025),
    "LeftHandGrip": (0.0, 0.285, -0.13),
    "MagazineSocket": (0.0, -0.18, -0.15),
    "ShellEjection": (-0.065, 0.0, 0.055),
}
for name, location in markers.items():
    empty = bpy.data.objects.new(name, None)
    empty.empty_display_type = "PLAIN_AXES"
    empty.empty_display_size = 0.025
    empty.location = location
    bpy.context.collection.objects.link(empty)

bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(
    filepath=str(fbx_path),
    use_selection=True,
    apply_scale_options="FBX_SCALE_UNITS",
    axis_forward="-Z",
    axis_up="Y",
    use_mesh_modifiers=True,
    mesh_smooth_type="FACE",
    add_leaf_bones=False,
    bake_anim=False,
    path_mode="AUTO",
)

print(f"Built {blend_path}")
print(f"Exported {fbx_path}")
