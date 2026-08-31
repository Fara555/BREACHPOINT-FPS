import bpy
from pathlib import Path

ROOT = Path(r"A:\UnityProjects\BREACHPOINT - FPS")
ASSET = ROOT / "Assets" / "Project" / "Art" / "Weapons" / "SR7"
SOURCE = ASSET / "SR7.fbx"

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(SOURCE))

# The source blend was saved before preview cameras and lights were created,
# but remove presentation-only objects defensively if the source changes.
for obj in list(bpy.context.scene.objects):
    if obj.type in {"CAMERA", "LIGHT"}:
        bpy.data.objects.remove(obj, do_unlink=True)


def apply_modifiers(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    for modifier in list(obj.modifiers):
        try:
            bpy.ops.object.modifier_apply(modifier=modifier.name)
        except RuntimeError:
            pass
    obj.select_set(False)


mesh_objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
for mesh_object in mesh_objects:
    apply_modifiers(mesh_object)


def category(obj):
    name = obj.name.lower()
    if name.startswith("magazine") or name.startswith("feed"):
        return "MagazineAssembly"
    if name.startswith("trigger"):
        return "TriggerAssembly"
    if "charginghandle" in name or "charging_handle" in name:
        return "ChargingHandleAssembly"
    return "RifleStatic"


def join_objects(objects, result_name):
    if not objects:
        return None
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    active = objects[0]
    bpy.context.view_layer.objects.active = active
    bpy.ops.object.join()
    active.name = result_name
    return active


groups = {}
for mesh_object in [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]:
    groups.setdefault(category(mesh_object), []).append(mesh_object)

joined = {}
for group_name, objects in groups.items():
    joined[group_name] = join_objects(objects, group_name)

# Give animated assemblies useful pivots without changing their world geometry.
pivots = {
    "MagazineAssembly": (0.0, -0.24, -0.15),
    "TriggerAssembly": (0.0, -0.368, 0.004),
    "ChargingHandleAssembly": (0.0, 0.145, 0.105),
}
for name, location in pivots.items():
    obj = joined.get(name)
    if obj is None:
        continue
    bpy.context.scene.cursor.location = location
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR", center="MEDIAN")
    obj.select_set(False)

# A single explicit root keeps Unity's imported hierarchy predictable.
root = bpy.data.objects.new("ReferenceRifle_Optimized", None)
bpy.context.collection.objects.link(root)
for obj in bpy.context.scene.objects:
    if obj != root and obj.parent is None:
        obj.parent = root

blend_path = ASSET / "SR7_Optimized.blend"
fbx_path = ASSET / "SR7_Optimized.fbx"
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(
    filepath=str(fbx_path),
    use_selection=True,
    axis_forward="-Z",
    axis_up="Y",
    apply_scale_options="FBX_SCALE_UNITS",
    use_mesh_modifiers=True,
    mesh_smooth_type="FACE",
    add_leaf_bones=False,
    bake_anim=False,
    path_mode="RELATIVE",
    embed_textures=False,
)

meshes = [obj.name for obj in bpy.context.scene.objects if obj.type == "MESH"]
empties = [obj.name for obj in bpy.context.scene.objects if obj.type == "EMPTY"]
print(f"Optimized mesh objects ({len(meshes)}): {', '.join(sorted(meshes))}")
print(f"Hierarchy empties ({len(empties)}): {', '.join(sorted(empties))}")
print(f"Saved {blend_path}")
print(f"Exported {fbx_path}")
