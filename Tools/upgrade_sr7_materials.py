import bpy
import numpy as np
from pathlib import Path
from mathutils import Vector

ROOT = Path(r"A:\UnityProjects\BREACHPOINT - FPS")
ASSET = ROOT / "Assets" / "Project" / "Art" / "Weapons" / "SR7"
TEX = ASSET / "Textures"

bpy.ops.wm.open_mainfile(filepath=str(ASSET / "SR7_Optimized.blend"))

gunmetal_path = TEX / "T_RS7_Gunmetal_BaseColor.png"
polymer_path = TEX / "T_RS7_Polymer_BaseColor.png"
wear_path = TEX / "T_SR7_WearMask.png"


def load_image(path, non_color=False):
    image = bpy.data.images.load(str(path), check_existing=True)
    image.colorspace_settings.name = "Non-Color" if non_color else "sRGB"
    return image


gunmetal_image = load_image(gunmetal_path)
polymer_image = load_image(polymer_path)
wear_image = load_image(wear_path, True)


def clear_except_output(material):
    material.use_nodes = True
    nodes = material.node_tree.nodes
    output = nodes.get("Material Output")
    for node in list(nodes):
        if node != output:
            nodes.remove(node)
    return nodes, material.node_tree.links, output


def build_gunmetal():
    material = bpy.data.materials["Blockout Gunmetal"]
    nodes, links, output = clear_except_output(material)
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Metallic"].default_value = .84

    base = nodes.new("ShaderNodeTexImage")
    base.image = gunmetal_image
    base.extension = "REPEAT"
    base.label = "Gunmetal BaseColor"
    wear = nodes.new("ShaderNodeTexImage")
    wear.image = wear_image
    wear.extension = "REPEAT"
    wear.label = "Sparse Wear Mask"

    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position = .20
    ramp.color_ramp.elements[1].position = .72
    mix = nodes.new("ShaderNodeMixRGB")
    mix.blend_type = "MIX"
    mix.inputs[2].default_value = (.34,.36,.38,1)

    roughness = nodes.new("ShaderNodeMapRange")
    roughness.inputs["From Min"].default_value = 0.0
    roughness.inputs["From Max"].default_value = 1.0
    roughness.inputs["To Min"].default_value = .34
    roughness.inputs["To Max"].default_value = .19

    bump = nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = .16
    bump.inputs["Distance"].default_value = .025

    links.new(wear.outputs["Color"], ramp.inputs["Fac"])
    links.new(ramp.outputs["Color"], mix.inputs["Fac"])
    links.new(base.outputs["Color"], mix.inputs[1])
    links.new(mix.outputs["Color"], shader.inputs["Base Color"])
    links.new(wear.outputs["Color"], roughness.inputs["Value"])
    links.new(roughness.outputs["Result"], shader.inputs["Roughness"])
    links.new(base.outputs["Color"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], shader.inputs["Normal"])
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])


def build_polymer():
    material = bpy.data.materials["Blockout Polymer"]
    nodes, links, output = clear_except_output(material)
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Metallic"].default_value = .01
    shader.inputs["Roughness"].default_value = .58
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = polymer_image
    texture.extension = "REPEAT"
    bump = nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = .20
    bump.inputs["Distance"].default_value = .018
    links.new(texture.outputs["Color"], shader.inputs["Base Color"])
    links.new(texture.outputs["Color"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], shader.inputs["Normal"])
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])


def build_accent():
    material = bpy.data.materials["Blockout Accent"]
    nodes, links, output = clear_except_output(material)
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Base Color"].default_value = (1.0,.075,.004,1)
    shader.inputs["Metallic"].default_value = .32
    shader.inputs["Roughness"].default_value = .24
    shader.inputs["Emission Color"].default_value = (1.0,.018,.001,1)
    shader.inputs["Emission Strength"].default_value = 3.0
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])


def save_mask_map(path, metallic, smoothness, size=512):
    image = bpy.data.images.new(path.stem, width=size, height=size, alpha=True, float_buffer=False)
    pixels = np.empty((size,size,4), dtype=np.float32)
    pixels[:,:,0] = metallic
    pixels[:,:,1] = 1.0
    pixels[:,:,2] = 0.0
    pixels[:,:,3] = smoothness
    image.pixels.foreach_set(pixels.ravel())
    image.filepath_raw = str(path)
    image.file_format = "PNG"
    image.save()


build_gunmetal()
build_polymer()
build_accent()
save_mask_map(TEX / "T_SR7_Gunmetal_MaskMap.png", .84, .69)
save_mask_map(TEX / "T_SR7_Polymer_MaskMap.png", .01, .42)

blend_path = ASSET / "SR7_MaterialPass2.blend"
fbx_path = ASSET / "SR7_MaterialPass2.fbx"
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(
    filepath=str(fbx_path), use_selection=True,
    axis_forward="-Z", axis_up="Y",
    apply_scale_options="FBX_SCALE_UNITS",
    use_mesh_modifiers=True, mesh_smooth_type="FACE",
    add_leaf_bones=False, bake_anim=False,
    path_mode="RELATIVE", embed_textures=False)

# Preview the final material response.
bpy.ops.object.camera_add(location=(3.2,.12,-.08))
camera = bpy.context.object
target = Vector((0,.15,-.10))
camera.rotation_euler = (target-camera.location).to_track_quat("-Z","Y").to_euler()
camera.data.type = "ORTHO"
camera.data.ortho_scale = 2.15
bpy.context.scene.camera = camera
for location, energy, size in [((2.0,.1,2.6),1150,3.0),((-1.4,.9,1.2),550,2.2)]:
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
scene.render.filepath = str(ASSET / "SR7_MaterialPass2_Preview.png")
scene.world.color = (.018,.021,.027)
bpy.ops.render.render(write_still=True)

print(f"Saved {blend_path}")
print(f"Exported {fbx_path}")
