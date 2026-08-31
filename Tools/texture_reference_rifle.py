import bpy
from pathlib import Path
from mathutils import Vector

ROOT = Path(r"A:\UnityProjects\BREACHPOINT - FPS")
ASSET = ROOT / "Assets" / "Project" / "Art" / "Weapons" / "ReferenceRifle"
TEXTURES = ASSET / "Textures"

bpy.ops.wm.open_mainfile(filepath=str(ASSET / "ReferenceRifle_DetailPass15.blend"))


def unwrap_object(obj):
    if obj.type != "MESH" or not obj.data.polygons:
        return
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=0.785398, island_margin=0.018)
    bpy.ops.object.mode_set(mode="OBJECT")


for mesh_object in [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]:
    unwrap_object(mesh_object)


def textured_material(name, image_path, metallic, roughness, bump_strength):
    material = bpy.data.materials.get(name)
    if material is None:
        return
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    shader = nodes.get("Principled BSDF")
    image = bpy.data.images.load(str(image_path), check_existing=True)

    texture = nodes.get("Surface BaseColor") or nodes.new("ShaderNodeTexImage")
    texture.name = "Surface BaseColor"
    texture.label = "Unity Base Color"
    texture.image = image
    texture.interpolation = "Linear"
    texture.extension = "REPEAT"

    bump = nodes.get("Surface Micro Bump") or nodes.new("ShaderNodeBump")
    bump.name = "Surface Micro Bump"
    bump.inputs["Strength"].default_value = bump_strength
    bump.inputs["Distance"].default_value = 0.035

    links.new(texture.outputs["Color"], shader.inputs["Base Color"])
    links.new(texture.outputs["Color"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], shader.inputs["Normal"])
    shader.inputs["Metallic"].default_value = metallic
    shader.inputs["Roughness"].default_value = roughness


textured_material(
    "Blockout Gunmetal",
    TEXTURES / "T_ReferenceRifle_Gunmetal_BaseColor.png",
    metallic=.82,
    roughness=.29,
    bump_strength=.16,
)
textured_material(
    "Blockout Polymer",
    TEXTURES / "T_ReferenceRifle_Polymer_BaseColor.png",
    metallic=.02,
    roughness=.56,
    bump_strength=.22,
)

accent = bpy.data.materials.get("Blockout Accent")
if accent and accent.use_nodes:
    shader = accent.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (1.0, .12, .008, 1.0)
    shader.inputs["Metallic"].default_value = .35
    shader.inputs["Roughness"].default_value = .24
    shader.inputs["Emission Color"].default_value = (.8, .035, .002, 1.0)
    shader.inputs["Emission Strength"].default_value = 2.2

blend_path = ASSET / "ReferenceRifle_TexturedPass1.blend"
fbx_path = ASSET / "ReferenceRifle_TexturedPass1.fbx"
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(
    filepath=str(fbx_path), use_selection=True,
    axis_forward="-Z", axis_up="Y",
    apply_scale_options="FBX_SCALE_UNITS",
    use_mesh_modifiers=True, mesh_smooth_type="FACE",
    add_leaf_bones=False, bake_anim=False,
    path_mode="RELATIVE", embed_textures=False)

# Material validation render.
bpy.ops.object.camera_add(location=(3.2,.12,-.08))
camera = bpy.context.object
target = Vector((0,.15,-.10))
camera.rotation_euler = (target-camera.location).to_track_quat("-Z","Y").to_euler()
camera.data.type = "ORTHO"
camera.data.ortho_scale = 2.15
bpy.context.scene.camera = camera
for location, energy, size in [((2.1,.25,2.4),1250,3.0),((-1.6,.7,1.1),600,2.0)]:
    bpy.ops.object.light_add(type="AREA", location=location)
    light = bpy.context.object
    light.data.energy = energy
    light.data.shape = "DISK"
    light.data.size = size
    light.rotation_euler = (target-light.location).to_track_quat("-Z","Y").to_euler()

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1200
scene.render.resolution_y = 700
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = str(ASSET / "ReferenceRifle_TexturedPass1_Preview.png")
scene.world.color = (.025,.028,.035)
bpy.ops.render.render(write_still=True)

print(f"Saved {blend_path}")
print(f"Exported {fbx_path}")
