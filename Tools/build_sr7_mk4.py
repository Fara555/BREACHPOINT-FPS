import bpy
import math
from pathlib import Path
from mathutils import Vector

ROOT = Path(r"A:\UnityProjects\BREACHPOINT - FPS")
OUT = ROOT / "Assets" / "Project" / "Art" / "Weapons" / "SR7"

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)


def material(name, color, metallic=0.0, roughness=0.4, emission=None):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 4.0
    return mat


GRAPHITE = material("Graphite Polymer", (0.035, 0.045, 0.055), 0.15, 0.28)
METAL = material("Gunmetal", (0.10, 0.12, 0.14), 0.82, 0.2)
RUBBER = material("Grip Rubber", (0.012, 0.014, 0.016), 0.0, 0.68)
ACCENT = material("Safety Red", (0.55, 0.012, 0.008), 0.45, 0.24)
BLACK = material("Recess", (0.002, 0.003, 0.004), 0.1, 0.72)
GLOW = material("Status Emission", (0.3, 0.004, 0.002), 0.1, 0.2, (1.0, 0.01, 0.002))


def profile(name, points, width, mat, bevel=0.006):
    """Extrude a custom side silhouette along X; points are (forward Y, up Z)."""
    half = width * 0.5
    verts = [(-half, y, z) for y, z in points] + [(half, y, z) for y, z in points]
    n = len(points)
    faces = [tuple(reversed(range(n))), tuple(range(n, n * 2))]
    for i in range(n):
        j = (i + 1) % n
        faces.append((i, j, n + j, n + i))
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    modifier = obj.modifiers.new("Edge treatment", "BEVEL")
    modifier.width = bevel
    modifier.segments = 3
    modifier.limit_method = "ANGLE"
    modifier.angle_limit = math.radians(24)
    modifier.use_clamp_overlap = True
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    return obj


def cube(name, location, scale, mat, bevel=0.003, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = (scale[0] / 2, scale[1] / 2, scale[2] / 2)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    mod = obj.modifiers.new("Edge treatment", "BEVEL")
    mod.width = bevel
    mod.segments = 3
    mod.limit_method = "ANGLE"
    mod.use_clamp_overlap = True
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def cylinder(name, location, radius, depth, mat, vertices=16, rotation=(math.pi/2, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(mat)
    mod = obj.modifiers.new("Edge treatment", "BEVEL")
    mod.width = min(radius * .14, .0025)
    mod.segments = 2
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


# A single flowing receiver silhouette instead of stacked cuboids.
profile("Receiver", [
    (-.24, .085), (-.19, .135), (.04, .145), (.19, .12), (.25, .075),
    (.24, -.035), (.15, -.072), (-.08, -.07), (-.19, -.035), (-.245, .015)
], .105, GRAPHITE, .009)

# Tapered, sloped handguard and sculpted rear stock.
profile("Handguard", [
    (.16, .105), (.30, .115), (.39, .075), (.405, -.005),
    (.35, -.05), (.205, -.065), (.155, -.025)
], .088, GRAPHITE, .008)
profile("StockShell", [
    (-.43, .075), (-.36, .125), (-.22, .115), (-.17, .065),
    (-.19, -.035), (-.29, -.065), (-.405, -.045), (-.445, .005)
], .098, GRAPHITE, .010)
profile("CheekRest", [(-.39,.11),(-.32,.155),(-.20,.15),(-.17,.115),(-.23,.095),(-.37,.09)], .078, RUBBER, .006)
profile("ButtPad", [(-.455,.07),(-.43,.09),(-.41,.055),(-.415,-.04),(-.44,-.07),(-.465,-.035)], .102, RUBBER, .005)

# Organic angled pistol grip and curved magazine built from tapered segments.
grip = profile("PistolGrip", [(-.10,-.045),(-.035,-.055),(-.045,-.19),(-.095,-.255),(-.145,-.235),(-.15,-.12)], .068, RUBBER, .009)
profile("TriggerGuard", [(-.035,-.045),(.065,-.045),(.08,-.075),(.045,-.12),(-.04,-.115),(-.065,-.08)], .018, METAL, .003)
profile("Trigger", [(-.005,-.06),(.018,-.07),(.01,-.115),(-.012,-.105)], .012, ACCENT, .002)
profile("Magazine", [(-.20,-.04),(-.115,-.055),(-.10,-.19),(-.13,-.285),(-.19,-.28),(-.205,-.17)], .072, METAL, .007)
profile("MagazineInset", [(-.19,-.09),(-.13,-.10),(-.125,-.225),(-.155,-.25),(-.18,-.23)], .075, BLACK, .002)

# Barrel system with stepped diameters and a vented muzzle brake.
cylinder("Barrel", (0, .48, .035), .016, .29, METAL, 20)
cylinder("BarrelSleeve", (0, .385, .035), .027, .095, METAL, 16)
cylinder("MuzzleBrake", (0, .645, .035), .031, .075, GRAPHITE, 16)
cylinder("MuzzleOpening", (0, .684, .035), .019, .006, BLACK, 20)
for side in (-1, 1):
    for y in (.625, .65):
        cube(f"MuzzlePort_{side}_{y}", (side*.027, y, .035), (.008,.018,.024), BLACK, .001)

# Recessed side architecture and controls.
for side in (-1, 1):
    x = side * .056
    profile(f"SideInlay_{side}", [(-.17,.075),(.10,.09),(.16,.055),(.12,-.035),(-.12,-.04),(-.18,.005)], .008, METAL, .002).location.x = side*.058
    for idx, y in enumerate((.22,.26,.30,.34)):
        cube(f"Vent_{side}_{idx}", (side*.047, y, .025), (.008,.025,.028), BLACK, .002, (0,0,math.radians(12)))
    for idx, y in enumerate((-.35,-.31,-.27)):
        cube(f"StockRib_{side}_{idx}", (side*.05, y, .025), (.009,.016,.075), METAL, .002)
    cylinder(f"Selector_{side}", (side*.061, -.06, .015), .016, .009, ACCENT, 16, (0,math.pi/2,0))

# Low-profile top rail and compact holographic optic.
cube("TopSpine", (0, .02, .147), (.065,.34,.022), METAL, .003)
for idx, y in enumerate((-.12,-.07,-.02,.03,.08,.13)):
    cube(f"RailNotch_{idx}", (0,y,.162), (.07,.018,.012), BLACK, .001)
profile("OpticBody", [(-.06,.17),(.025,.19),(.075,.25),(.02,.295),(-.055,.285),(-.085,.225)], .072, GRAPHITE, .007)
cube("OpticLens", (0, .067, .245), (.052,.006,.058), GLOW, .004)
cylinder("OpticDial", (.045, .0, .225), .014, .014, ACCENT, 16, (0,math.pi/2,0))

# Angled foregrip and tactile details.
profile("Foregrip", [(.18,-.065),(.27,-.07),(.29,-.12),(.255,-.205),(.20,-.18),(.16,-.105)], .06, RUBBER, .008)
for side in (-1, 1):
    for idx, y in enumerate((-.16,-.05,.08,.19,.31)):
        cylinder(f"Fastener_{side}_{idx}", (side*.061,y,.065), .0055, .006, METAL, 12, (0,math.pi/2,0))
cube("StatusStrip", (-.059,.075,.045), (.007,.075,.012), GLOW, .002)

# Unity sockets.
for name, loc in {
    "MuzzleSocket": (0,.69,.035), "LeftHandGrip": (0,.235,-.14),
    "MagazineSocket": (0,-.16,-.08), "ShellEjection": (-.063,.03,.075)
}.items():
    empty = bpy.data.objects.new(name, None)
    empty.empty_display_type = "PLAIN_AXES"
    empty.empty_display_size = .025
    empty.location = loc
    bpy.context.collection.objects.link(empty)

# Custom profile extrusion can inherit inconsistent loop winding depending on
# the silhouette direction. Recalculate every closed mesh explicitly outside
# before saving or exporting to Unity.
for obj in [item for item in bpy.context.scene.objects if item.type == "MESH"]:
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")

bpy.ops.wm.save_as_mainfile(filepath=str(OUT / "SR7_Breacher_MK4.blend"))
bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(filepath=str(OUT / "SR7_Breacher_MK4.fbx"), use_selection=True,
    axis_forward="-Z", axis_up="Y", apply_scale_options="FBX_SCALE_UNITS",
    use_mesh_modifiers=True, mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False)
print("SR7 Breacher MK4 built from custom profiles")
