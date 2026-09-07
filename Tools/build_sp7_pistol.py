import bpy
import math
from pathlib import Path
from mathutils import Matrix, Vector


ROOT = Path(r"A:\UnityProjects\BREACHPOINT - FPS")
ART_OUT = ROOT / "Assets" / "Project" / "Art" / "Weapons" / "SP7"
SOURCE_OUT = ROOT / "SourceArt" / "Weapons" / "SP7"
ART_OUT.mkdir(parents=True, exist_ok=True)
SOURCE_OUT.mkdir(parents=True, exist_ok=True)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
    for datablock in list(datablocks):
        datablocks.remove(datablock)


def make_material(name, color, metallic, roughness, emission=None):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Metallic"].default_value = metallic
    shader.inputs["Roughness"].default_value = roughness
    if emission is not None:
        shader.inputs["Emission Color"].default_value = (*emission, 1.0)
        shader.inputs["Emission Strength"].default_value = 5.0
    return material


POLYMER = make_material("Blockout Polymer", (0.028, 0.038, 0.048), 0.18, 0.30)
GUNMETAL = make_material("Blockout Gunmetal", (0.085, 0.105, 0.125), 0.82, 0.22)
ACCENT = make_material("Blockout Accent", (0.72, 0.018, 0.008), 0.38, 0.24, (1.0, 0.012, 0.003))
RECESS = make_material("SP7 Recess", (0.003, 0.004, 0.006), 0.10, 0.72)


def add_bevel(obj, width, segments=3):
    modifier = obj.modifiers.new("Production Bevel", "BEVEL")
    modifier.width = width
    modifier.segments = segments
    modifier.limit_method = "ANGLE"
    modifier.angle_limit = math.radians(22.0)
    modifier.use_clamp_overlap = True
    modifier.harden_normals = True


def cube(name, location, size, material, bevel=0.0015, rotation=(0.0, 0.0, 0.0), parent=None):
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = (size[0] * 0.5, size[1] * 0.5, size[2] * 0.5)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    add_bevel(obj, bevel)
    if parent is not None:
        obj.parent = parent
    return obj


def cylinder(name, location, radius, depth, material, vertices=20,
             rotation=(math.pi / 2.0, 0.0, 0.0), parent=None, bevel=0.001):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        location=location,
        rotation=rotation,
    )
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material)
    if bevel > 0.0:
        add_bevel(obj, bevel, 2)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    if parent is not None:
        obj.parent = parent
    return obj


def profile(name, points, width, material, bevel=0.002, parent=None):
    half_width = width * 0.5
    vertices = [(-half_width, y, z) for y, z in points]
    vertices += [(half_width, y, z) for y, z in points]
    count = len(points)
    faces = [tuple(reversed(range(count))), tuple(range(count, count * 2))]
    for index in range(count):
        next_index = (index + 1) % count
        faces.append((index, next_index, count + next_index, count + index))

    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(material)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    add_bevel(obj, bevel)
    if parent is not None:
        obj.parent = parent
    return obj


def empty(name, location, parent=None, display_size=0.012):
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = display_size
    obj.location = location
    bpy.context.collection.objects.link(obj)
    if parent is not None:
        obj.parent = parent
    return obj


def tube_loop(name, points, bevel_depth, material, parent=None):
    curve_data = bpy.data.curves.new(name + "Curve", "CURVE")
    curve_data.dimensions = "3D"
    curve_data.resolution_u = 1
    curve_data.bevel_depth = bevel_depth
    curve_data.bevel_resolution = 2
    curve_data.resolution_u = 2
    spline = curve_data.splines.new("BEZIER")
    spline.bezier_points.add(len(points) - 1)
    for point, coordinate in zip(spline.bezier_points, points):
        point.co = coordinate
        point.handle_left_type = "AUTO"
        point.handle_right_type = "AUTO"
    spline.use_cyclic_u = True
    obj = bpy.data.objects.new(name, curve_data)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(material)
    if parent is not None:
        obj.parent = parent
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.convert(target="MESH")
    obj.select_set(False)
    return obj


# The SP7 inherits the SR7's layered graphite shell, exposed gunmetal chassis,
# red status language and compact angular silhouette without simply shrinking it.
root = empty("SP7_Pistol", (0.0, 0.0, 0.0), display_size=0.02)

frame = empty("Frame", (0.0, 0.0, 0.0), root)
profile("Frame_Core", [
    (-0.105, 0.010), (-0.080, 0.038), (0.112, 0.038),
    (0.158, 0.018), (0.148, -0.016), (0.018, -0.035),
    (-0.072, -0.028),
], 0.043, POLYMER, 0.004, frame)
cube("Dust_Cover", (0.0, 0.072, -0.010), (0.046, 0.164, 0.026), GUNMETAL, 0.003, parent=frame)
profile("Rear_Beavertail", [
    (-0.116, 0.020), (-0.091, 0.025), (-0.060, 0.009),
    (-0.080, -0.007), (-0.111, -0.004),
], 0.045, GUNMETAL, 0.003, frame)

slide = empty("Slide", (0.0, 0.015, 0.056), root)
cube("Slide_Core", (0.0, 0.068, 0.0), (0.050, 0.375, 0.046), GUNMETAL, 0.006, parent=slide)
cube("Slide_Top_Armor", (0.0, 0.060, 0.029), (0.038, 0.320, 0.012), POLYMER, 0.0025, parent=slide)
profile("Slide_Rear_Shroud", [
    (-0.118, 0.018), (-0.098, 0.025), (-0.052, 0.026),
    (-0.043, -0.022), (-0.112, -0.023),
], 0.053, POLYMER, 0.004, slide)
profile("Slide_Front_Shroud", [
    (0.205, 0.025), (0.252, 0.017), (0.260, -0.015),
    (0.238, -0.024), (0.203, -0.020),
], 0.053, POLYMER, 0.004, slide)

for side in (-1, 1):
    x = side * 0.0275
    for index in range(4):
        cube(
            f"RearSerration_{'L' if side < 0 else 'R'}_{index + 1}",
            (x, -0.087 + index * 0.014, -0.003),
            (0.0045, 0.007, 0.035),
            RECESS,
            0.001,
            rotation=(0.0, math.radians(side * 10.0), 0.0),
            parent=slide,
        )
    for index in range(3):
        cube(
            f"CoolingPort_{'L' if side < 0 else 'R'}_{index + 1}",
            (x, 0.142 + index * 0.035, 0.004),
            (0.0045, 0.021, 0.018),
            RECESS,
            0.001,
            parent=slide,
        )
    cube(
        f"SlideSideRail_{'L' if side < 0 else 'R'}",
        (side * 0.0285, 0.075, -0.012),
        (0.004, 0.230, 0.011),
        POLYMER,
        0.0015,
        parent=slide,
    )
    cylinder(
        f"RearFastener_{'L' if side < 0 else 'R'}",
        (side * 0.0305, -0.071, -0.006),
        0.005,
        0.004,
        ACCENT,
        12,
        rotation=(0.0, math.pi / 2.0, 0.0),
        parent=slide,
        bevel=0.0006,
    )

cube("Ejection_Port", (0.0, 0.031, 0.029), (0.032, 0.054, 0.004), RECESS, 0.0015, parent=slide)
cube("Front_Sight", (0.0, 0.230, 0.041), (0.011, 0.018, 0.011), GUNMETAL, 0.0018, parent=slide)
cube("Front_Sight_Dot", (0.0, 0.221, 0.043), (0.004, 0.003, 0.004), ACCENT, 0.0005, parent=slide)
cube("Rear_Sight_Base", (0.0, -0.087, 0.039), (0.043, 0.019, 0.009), GUNMETAL, 0.0018, parent=slide)
cube("Rear_Sight_Left", (-0.017, -0.087, 0.046), (0.007, 0.016, 0.014), GUNMETAL, 0.0018, parent=slide)
cube("Rear_Sight_Right", (0.017, -0.087, 0.046), (0.007, 0.016, 0.014), GUNMETAL, 0.0018, parent=slide)
cube("Rear_Sight_Dot_Left", (-0.017, -0.096, 0.048), (0.0035, 0.003, 0.0035), ACCENT, 0.0005, parent=slide)
cube("Rear_Sight_Dot_Right", (0.017, -0.096, 0.048), (0.0035, 0.003, 0.0035), ACCENT, 0.0005, parent=slide)

barrel = empty("Barrel", (0.0, 0.020, -0.004), slide)
cylinder("Barrel_Tube", (0.0, 0.070, 0.0), 0.0165, 0.435, GUNMETAL, 24, parent=barrel, bevel=0.001)
cylinder("Muzzle_Shroud", (0.0, 0.287, 0.0), 0.0225, 0.062, GUNMETAL, 20, parent=barrel, bevel=0.0015)
cylinder("Threaded_Barrel", (0.0, 0.313, 0.0), 0.0215, 0.038, RECESS, 20, parent=barrel, bevel=0.0006)
for index in range(4):
    cylinder(
        f"Barrel_Thread_{index + 1}",
        (0.0, 0.301 + index * 0.008, 0.0),
        0.0228,
        0.0025,
        GUNMETAL,
        20,
        parent=barrel,
        bevel=0.0003,
    )

grip = empty("Grip", (0.0, -0.064, -0.024), root)
profile("Grip_Body", [
    (-0.043, 0.014), (0.031, 0.007), (0.052, -0.185),
    (-0.022, -0.202), (-0.064, -0.035),
], 0.045, POLYMER, 0.005, grip)
profile("Grip_Backstrap", [
    (-0.057, -0.006), (-0.039, -0.019), (0.040, -0.178),
    (0.054, -0.186), (0.032, -0.024),
], 0.048, GUNMETAL, 0.003, grip)
for index in range(5):
    cube(
        f"Grip_Rib_{index + 1}",
        (0.0, -0.012 + index * 0.016, -0.045 - index * 0.028),
        (0.049, 0.007, 0.036),
        GUNMETAL,
        0.0015,
        rotation=(math.radians(-8.0), 0.0, 0.0),
        parent=grip,
    )

magazine_socket = empty("MagazineSocket", (0.0, 0.004, 0.0), grip)
magazine = empty("Magazine", (0.0, 0.0, 0.0), magazine_socket)
profile("Magazine_Body", [
    (-0.025, -0.006), (0.026, -0.008), (0.050, -0.176),
    (-0.018, -0.190),
], 0.034, GUNMETAL, 0.003, magazine)
cube("Magazine_Base", (0.0, 0.015, -0.197), (0.047, 0.064, 0.016), ACCENT, 0.004, parent=magazine)
cube("Magazine_Witness", (-0.0185, 0.014, -0.104), (0.004, 0.010, 0.078), ACCENT, 0.001, parent=magazine)

trigger_assembly = empty("TriggerAssembly", (0.0, -0.006, -0.034), root)
tube_loop("TriggerGuard", [
    (0.0, -0.024, 0.004),
    (0.0, 0.060, 0.002),
    (0.0, 0.090, -0.043),
    (0.0, 0.054, -0.073),
    (0.0, -0.017, -0.070),
], 0.0045, GUNMETAL, trigger_assembly)
profile("Trigger", [
    (-0.004, 0.010), (0.010, 0.006), (0.006, -0.032), (-0.007, -0.024),
], 0.011, ACCENT, 0.0015, trigger_assembly)

cube("Accessory_Rail", (0.0, 0.066, -0.043), (0.034, 0.112, 0.012), GUNMETAL, 0.002, parent=root)
for index in range(3):
    cube(f"Rail_Notch_{index + 1}", (0.0, 0.040 + index * 0.032, -0.050),
         (0.039, 0.009, 0.006), RECESS, 0.001, parent=root)

cube("Slide_Stop", (-0.026, -0.048, 0.005), (0.006, 0.030, 0.014), ACCENT, 0.002, parent=root)
cube("Status_Light", (-0.026, 0.012, 0.023), (0.004, 0.055, 0.010), ACCENT, 0.001, parent=root)

empty("MuzzleSocket", (0.0, 0.337, 0.052), root)
empty("ShellEjection", (0.030, 0.046, 0.084), root)
right_hand_grip = empty("RightHandGrip", (0.0, -0.064, -0.104), root)
right_hand_grip.rotation_euler.x = math.radians(-11.0)

# A genuinely separate, removable suppressor assembly. It is mounted in the
# exported model for presentation, while the named hierarchy lets Unity hide,
# detach or animate it without touching the pistol meshes.
suppressor_socket = empty("SuppressorSocket", (0.0, 0.337, 0.052), root)
suppressor = empty("Suppressor", (0.0, 0.0, 0.0), suppressor_socket)
cylinder("Suppressor_RearLock", (0.0, 0.016, 0.0), 0.0240, 0.032, GUNMETAL, 20,
         parent=suppressor, bevel=0.0015)
cylinder("Suppressor_Core", (0.0, 0.094, 0.0), 0.0255, 0.122, RECESS, 24,
         parent=suppressor, bevel=0.002)
cylinder("Suppressor_FrontCap", (0.0, 0.164, 0.0), 0.0270, 0.018, GUNMETAL, 24,
         parent=suppressor, bevel=0.002)
cylinder("Suppressor_Bore", (0.0, 0.174, 0.0), 0.008, 0.004, RECESS, 24,
         parent=suppressor, bevel=0.0)
cylinder("Suppressor_AccentRing", (0.0, 0.043, 0.0), 0.0265, 0.007, ACCENT, 24,
         parent=suppressor, bevel=0.0008)
for side in (-1, 1):
    for index in range(3):
        cube(
            f"Suppressor_Vent_{'L' if side < 0 else 'R'}_{index + 1}",
            (side * 0.0254, 0.112 + index * 0.014, 0.0),
            (0.0035, 0.010, 0.020),
            GUNMETAL,
            0.0008,
            rotation=(0.0, math.radians(side * 8.0), 0.0),
            parent=suppressor,
        )


# The SR7 is intentionally oversized and stylised. A 1.28 baked scale gives
# the sidearm a believable full-size presence next to it without leaving a
# non-unit scale on the exported animation hierarchy.
model_scale = 1.28
scale_matrix = Matrix.Scale(model_scale, 4)
for obj in [item for item in bpy.context.scene.objects if item != root]:
    obj.location *= model_scale
    if obj.type == "MESH":
        obj.data.transform(scale_matrix)
        for modifier in obj.modifiers:
            if modifier.type == "BEVEL":
                modifier.width *= model_scale


for obj in [item for item in bpy.context.scene.objects if item.type == "MESH"]:
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    obj.select_set(False)


blend_path = SOURCE_OUT / "SP7_Pistol.blend"
fbx_path = ART_OUT / "SP7_Pistol.fbx"
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
    path_mode="AUTO",
)


# Studio preview is rendered after export so cameras and lights never enter the FBX.
camera_data = bpy.data.cameras.new("PreviewCamera")
camera = bpy.data.objects.new("PreviewCamera", camera_data)
bpy.context.collection.objects.link(camera)
camera.location = (0.92, -0.18, 0.10)
camera_data.type = "ORTHO"
camera_data.ortho_scale = 0.88
bpy.context.scene.camera = camera


def aim_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


aim_at(camera, (0.0, 0.250, -0.080))
for name, location, energy, size in (
    ("Key", (0.38, -0.22, 0.48), 38.0, 1.4),
    ("Rim", (-0.36, 0.28, 0.30), 26.0, 1.1),
    ("Fill", (0.15, 0.25, -0.05), 14.0, 0.9),
):
    light_data = bpy.data.lights.new(name, "AREA")
    light_data.energy = energy
    light_data.shape = "DISK"
    light_data.size = size
    light = bpy.data.objects.new(name, light_data)
    light.location = location
    bpy.context.collection.objects.link(light)
    aim_at(light, (0.0, 0.02, -0.04))

bpy.ops.mesh.primitive_plane_add(size=3.0, location=(0.0, 0.04, -0.315))
ground = bpy.context.object
ground.name = "PreviewGround"
ground.data.materials.append(make_material("Preview Ground", (0.012, 0.016, 0.021), 0.05, 0.48))

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1280
scene.render.resolution_y = 720
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = str(ART_OUT / "SP7_Preview.png")
scene.render.film_transparent = False
scene.world.use_nodes = True
background = scene.world.node_tree.nodes.get("Background")
background.inputs["Color"].default_value = (0.004, 0.006, 0.009, 1.0)
background.inputs["Strength"].default_value = 0.08
scene.view_settings.look = "AgX - Medium High Contrast"
bpy.ops.render.render(write_still=True)

print(f"Built {blend_path}")
print(f"Exported {fbx_path}")
print(f"Rendered {ART_OUT / 'SP7_Preview.png'}")
