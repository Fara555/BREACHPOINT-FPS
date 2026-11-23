"""Build a reusable Service Quarter equipment case in meters."""

from pathlib import Path
import math

import bpy
import bmesh

SOURCE = Path(__file__).resolve().parent
OUTPUT = SOURCE.parents[2] / "Assets/Project/Art/Props/LoadingCase/Models"
OUTPUT.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
bpy.context.preferences.filepaths.file_preview_type = "NONE"

colors = {
    "CasePaint": (0.25, 0.21, 0.13, 1),
    "CaseBumper": (0.058, 0.060, 0.057, 1),
    "CaseHardware": (0.32, 0.34, 0.33, 1),
    "CaseIdentification": (0.48, 0.47, 0.39, 1),
}
groups = {key: [] for key in colors}
materials = {}
for name, color in colors.items():
    materials[name] = bpy.data.materials.new(name)
    materials[name].diffuse_color = color


def coordinates(point):
    x, y, z = point
    return -x, -z, y


def finish(obj, finish_name, bevel):
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(materials[finish_name])
    if bevel:
        modifier = obj.modifiers.new("Machined edges", "BEVEL")
        modifier.width = bevel
        modifier.segments = 3 if bevel >= 0.012 else 2
        modifier.harden_normals = True
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
        normal = obj.modifiers.new("Weighted normals", "WEIGHTED_NORMAL")
        normal.keep_sharp = True
        bpy.ops.object.modifier_apply(modifier=normal.name)
    groups[finish_name].append(obj)


def box(name, point, size, finish_name, bevel=0.009):
    bpy.ops.mesh.primitive_cube_add(size=1, location=coordinates(point))
    obj = bpy.context.object
    obj.name = name
    obj.scale = size[0], size[2], size[1]
    finish(obj, finish_name, bevel)
    return obj


def profile(name, width, depth, clip, bottom, top, finish_name, bevel):
    w, d = width / 2, depth / 2
    outline = [(-w + clip, -d), (w - clip, -d), (w, -d + clip), (w, d - clip),
               (w - clip, d), (-w + clip, d), (-w, d - clip), (-w, -d + clip)]
    vertices = [coordinates((x, y, z)) for y in (bottom, top) for x, z in outline]
    faces = [tuple(reversed(range(8))), tuple(range(8, 16))]
    faces.extend((i, (i + 1) % 8, (i + 1) % 8 + 8, i + 8) for i in range(8))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    edit = bmesh.new()
    edit.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(edit, faces=list(edit.faces))
    edit.to_mesh(mesh)
    edit.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    finish(obj, finish_name, bevel)


def bolt(point):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=8, radius=0.012, depth=0.008, location=coordinates(point),
        rotation=(math.pi / 2, 0, 0),
    )
    obj = bpy.context.object
    obj.name = "Captive hex fastener"
    finish(obj, "CaseHardware", 0.002)


def plate_face():
    x, y, z = -0.075, 0.610, -0.580
    width, height = 0.31, 0.145
    corners = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
    mesh = bpy.data.meshes.new("Printed equipment identification")
    mesh.from_pydata([coordinates((x + a * width / 2, y + b * height / 2, z))
                      for a, b in corners], [], [(0, 1, 2, 3)])
    uv = mesh.uv_layers.new(name="Surface")
    for loop, (a, b) in zip(mesh.loops, corners):
        uv.data[loop.index].uv = ((a + 1) / 2, (b + 1) / 2)
    obj = bpy.data.objects.new(mesh.name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(materials["CaseIdentification"])
    groups["CaseIdentification"].append(obj)


profile("Reinforced lower skirt", 1.46, 1.157, .103, .035, .185, "CaseBumper", .023)
profile("Pressed painted shell", 1.395, 1.112, .092, .125, .863, "CasePaint", .029)
profile("Recessed lid gasket", 1.414, 1.12, .09, .832, .876, "CaseBumper", .008)
profile("Folded lid", 1.475, 1.174, .105, .873, 1.077, "CasePaint", .027)
profile("Recessed lid center", 1.10, .815, .072, 1.066, 1.090, "CasePaint", .011)

for x in (-.512, .512):
    for z in (-.365, .365):
        box("Load-bearing skid foot", (x, .036, z), (.211, .072, .225), "CaseBumper", .017)
    for z in (-.366, .366):
        box("Reinforced lid corner plate", (x, 1.081, z), (.25, .024, .322), "CaseBumper", .012)

for side in (-1, 1):
    z = side * .553
    for x in (-.555, .555):
        box("Formed vertical stiffener", (x, .501, z), (.132, .591, .056), "CasePaint", .019)
    for x in (-.253, .253):
        box("Lower stamped reinforcing rib", (x, .294, z), (.375, .082, .037), "CasePaint", .012)

for x in (-.432, .432):
    box("Latch countersunk housing", (x, .838, -.561), (.146, .218, .042), "CaseBumper", .022)
    box("Latch over-center lever", (x, .816, -.586), (.079, .139, .022), "CaseHardware", .011)
    box("Latch hook receiver", (x, .926, -.584), (.108, .047, .025), "CaseHardware", .008)
    box("Rear hinge leaf", (x, .853, .568), (.163, .137, .034), "CaseHardware", .008)
    box("Rear hinge barrel", (x, .883, .580), (.127, .041, .035), "CaseHardware", .012)

for side in (-1, 1):
    x = side * .691
    box("Side handle cup", (x, .644, 0), (.039, .255, .490), "CaseBumper", .020)
    for z in (-.153, .153):
        box("Captive side handle pivot", (side * .714, .656, z), (.047, .051, .052), "CaseHardware", .011)
        box("Side handle return", (side * .719, .61, z), (.037, .085, .032), "CaseHardware", .012)
    box("Side handle grip", (side * .721, .576, 0), (.032, .037, .330), "CaseHardware", .012)

box("Riveted plate carrier", (-.075, .610, -.564), (.367, .177, .026), "CaseBumper", .009)
bolt((-.241, .610, -.581))
bolt((.091, .610, -.581))
plate_face()

# Present the source as individually editable parts. Export one mesh per finish.
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / "LoadingCase.blend"))
for name, parts in groups.items():
    bpy.ops.object.select_all(action="DESELECT")
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    if len(parts) > 1:
        bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = "SM_LoadingCase_" + name
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if name != "CaseIdentification":
        uv = obj.data.uv_layers.active or obj.data.uv_layers.new(name="Surface")
        for face in obj.data.polygons:
            axis = max(range(3), key=lambda i: abs(face.normal[i]))
            u, v = ((1, 2), (0, 2), (0, 1))[axis]
            for index in face.loop_indices:
                coordinate = obj.data.vertices[obj.data.loops[index].vertex_index].co
                uv.data[index].uv = coordinate[u] * 1.6 + .5, coordinate[v] * 1.6 + .5
    tri = obj.modifiers.new("Export triangles", "TRIANGULATE")
    tri.keep_custom_normals = True
    bpy.ops.object.modifier_apply(modifier=tri.name)

bpy.ops.object.select_all(action="SELECT")
vertices = [v.co for obj in bpy.context.selected_objects for v in obj.data.vertices]
bounds = [(min(v[i] for v in vertices), max(v[i] for v in vertices)) for i in range(3)]
triangles = sum(len(obj.data.polygons) for obj in bpy.context.selected_objects)
assert triangles < 11000 and max(v.z for v in vertices) < 1.105, (triangles, bounds)
bpy.ops.export_scene.fbx(
    filepath=str(OUTPUT / "LoadingCase.fbx"), use_selection=True,
    object_types={"MESH"}, axis_forward="-Z", axis_up="Y", bake_anim=False,
    add_leaf_bones=False, mesh_smooth_type="FACE",
)
print("Loading case:", triangles, "triangles,", bounds)
