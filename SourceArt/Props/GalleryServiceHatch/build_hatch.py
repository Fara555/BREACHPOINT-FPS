"""Build a shallow wall-mounted electrical service hatch in meters."""

import math
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

SOURCE = Path(__file__).resolve().parent
OUTPUT = SOURCE.parents[2] / "Assets/Project/Art/Props/GalleryServiceHatch/Models"
OUTPUT.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
bpy.context.preferences.filepaths.file_preview_type = "NONE"

finishes = {
    "HatchEnamel": (.29, .33, .33, 1),
    "HatchSeal": (.045, .053, .052, 1),
    "HatchHardware": (.22, .23, .24, 1),
    "HatchLabel": (.48, .44, .36, 1),
}
groups = {name: [] for name in finishes}
materials = {}
for name, color in finishes.items():
    material = bpy.data.materials.new(name)
    material.diffuse_color = color
    materials[name] = material


def convert(point):
    x, y, z = point
    return -x, -z, y


def add_finish(obj, finish_name, bevel):
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(materials[finish_name])
    if bevel:
        edge = obj.modifiers.new("Folded edges", "BEVEL")
        edge.width = bevel
        edge.segments = 3 if bevel >= .008 else 1 if bevel <= .002 else 2
        edge.harden_normals = True
        bpy.ops.object.modifier_apply(modifier=edge.name)
        for face in obj.data.polygons:
            face.use_smooth = True
        normal = obj.modifiers.new("Weighted face normals", "WEIGHTED_NORMAL")
        normal.keep_sharp = True
        bpy.ops.object.modifier_apply(modifier=normal.name)
    groups[finish_name].append(obj)


def block(name, position, dimensions, finish_name, bevel=.005):
    bpy.ops.mesh.primitive_cube_add(size=1, location=convert(position))
    obj = bpy.context.object
    obj.name = name
    obj.scale = dimensions[0], dimensions[2], dimensions[1]
    add_finish(obj, finish_name, bevel)


def panel(name, w, h, depth_back, depth_front, trim, finish_name, bevel):
    w, h = w / 2, h / 2
    profile = [(-w + trim, -h), (w - trim, -h), (w, -h + trim), (w, h - trim),
               (w - trim, h), (-w + trim, h), (-w, h - trim), (-w, -h + trim)]
    vertices = [convert((x, y, d)) for d in (depth_back, depth_front) for x, y in profile]
    faces = [tuple(range(7, -1, -1)), tuple(range(8, 16))]
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
    add_finish(obj, finish_name, bevel)


def cylinder(name, a, b, radius, finish_name, sides=12):
    start, end = Vector(convert(a)), Vector(convert(b))
    direction = end - start
    bpy.ops.mesh.primitive_cylinder_add(vertices=sides, radius=radius, depth=direction.length,
                                       location=(start + end) / 2)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()
    add_finish(obj, finish_name, .0015)


def rounded_pull():
    # A bent round pull with two return legs and a clear grip behind the bar.
    x = .453
    points = [(x, -.199, -.139), (x, -.199, -.167), (x, -.188, -.186),
              (x, -.168, -.193), (x, .034, -.193), (x, .056, -.187),
              (x, .069, -.169), (x, .069, -.139)]
    curve = bpy.data.curves.new("Bent pull profile", "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 4
    curve.bevel_depth = .017
    curve.bevel_resolution = 2
    curve.use_fill_caps = True
    poly = curve.splines.new("BEZIER")
    poly.bezier_points.add(len(points) - 1)
    for point, coordinate in zip(poly.bezier_points, points):
        point.co = convert(coordinate)
        point.handle_left_type = "AUTO"
        point.handle_right_type = "AUTO"
    poly.bezier_points[0].handle_right_type = "VECTOR"
    poly.bezier_points[-1].handle_left_type = "VECTOR"
    obj = bpy.data.objects.new("Bent maintenance pull", curve)
    bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.convert(target="MESH")
    add_finish(bpy.context.object, "HatchHardware", 0)


def identification():
    x0, x1, y0, y1, depth = -.232, .124, .041, .182, -.159
    points = [(x0, y0, depth), (x1, y0, depth), (x1, y1, depth), (x0, y1, depth)]
    mesh = bpy.data.meshes.new("Electrical warning print")
    mesh.from_pydata([convert(p) for p in points], [], [(0, 1, 2, 3)])
    uv = mesh.uv_layers.new(name="Print")
    for l, coordinate in zip(mesh.loops, [(0, 0), (1, 0), (1, 1), (0, 1)]):
        uv.data[l.index].uv = coordinate
    obj = bpy.data.objects.new(mesh.name, mesh)
    bpy.context.collection.objects.link(obj)
    mesh.materials.append(materials["HatchLabel"])
    groups["HatchLabel"].append(obj)


panel("Chamfered mounting flange", 1.365, 1.087, .038, -.072, .074, "HatchSeal", .012)
panel("Folded flange face", 1.315, 1.039, -.064, -.099, .068, "HatchHardware", .009)
panel("Compressed perimeter gasket", 1.235, .958, -.093, -.130, .060, "HatchSeal", .007)
panel("Pressed steel service door", 1.196, .921, -.122, -.154, .064, "HatchEnamel", .012)

for h in (-.276, .276):
    block("Fixed hinge leaf", (-.546, h, -.131), (.156, .090, .035), "HatchHardware", .008)
    block("Moving hinge leaf", (-.477, h, -.153), (.105, .074, .026), "HatchHardware", .006)
    cylinder("Hinge pin", (-.562, h - .069, -.161), (-.562, h + .069, -.161), .018, "HatchHardware")
    for sign in (-1, 1):
        low, high = sorted((h + sign * .031, h + sign * .063))
        cylinder("Hinge barrel", (-.562, low, -.161), (-.562, high, -.161), .031, "HatchHardware")

for x in (-.624, .624):
    for y in (-.413, .413):
        cylinder("Dark captive screw cup", (x, y, -.101), (x, y, -.113), .024, "HatchSeal")
        cylinder("Captive screw head", (x, y, -.112), (x, y, -.119), .012, "HatchHardware", 8)

block("Pull reinforcement", (.453, -.064, -.151), (.135, .360, .023), "HatchSeal", .012)
for y in (-.199, .069):
    block("Pull mounting boss", (.453, y, -.167), (.091, .068, .021), "HatchHardware", .008)
rounded_pull()

cylinder("Quarter-turn latch bezel", (.442, .264, -.151), (.442, .264, -.169), .047, "HatchSeal", 24)
cylinder("Quarter-turn latch face", (.442, .264, -.168), (.442, .264, -.179), .029, "HatchHardware", 16)
block("Quarter-turn recessed drive", (.442, .264, -.181), (.032, .006, .003), "HatchSeal", .001)
block("Etched identification plate", (-.054, .112, -.152), (.380, .162, .012), "HatchHardware", .007)
identification()

bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / "GalleryServiceHatch.blend"))
for name, parts in groups.items():
    bpy.ops.object.select_all(action="DESELECT")
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    if len(parts) > 1:
        bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = "SM_GalleryServiceHatch_" + name
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if name == "HatchEnamel":
        uv = obj.data.uv_layers.new(name="Paint")
        for loop in obj.data.loops:
            p = obj.data.vertices[loop.vertex_index].co
            uv.data[loop.index].uv = (.5 - p.x / 1.196, .5 + p.z / .921)
    triangulate = obj.modifiers.new("Export triangles", "TRIANGULATE")
    triangulate.keep_custom_normals = True
    bpy.ops.object.modifier_apply(modifier=triangulate.name)

bpy.ops.object.select_all(action="SELECT")
triangles = sum(len(obj.data.polygons) for obj in bpy.context.selected_objects)
assert triangles < 5200
assert max(v.co.y for obj in bpy.context.selected_objects for v in obj.data.vertices) < .232
bpy.ops.export_scene.fbx(
    filepath=str(OUTPUT / "GalleryServiceHatch.fbx"), use_selection=True,
    object_types={"MESH"}, axis_forward="-Z", axis_up="Y", bake_anim=False,
    add_leaf_bones=False, mesh_smooth_type="FACE",
)
print("Gallery service hatch triangles:", triangles)
