"""Build a flush ceiling return grille with folded frame and angled vanes."""

from pathlib import Path

import bmesh
import bpy


SOURCE = Path(__file__).resolve().parent
MODELS = SOURCE.parents[2] / "Assets/Project/Art/Environment/ServiceQuarter/CeilingReturnGrille/Models"
MODELS.mkdir(parents=True, exist_ok=True)
FRAME, FINS, RECESS = "ReturnFrame", "ReturnFins", "ReturnRecess"
COLORS = {FRAME: (.22, .245, .25, 1), FINS: (.42, .44, .42, 1), RECESS: (.055, .064, .066, 1)}

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
bpy.context.preferences.filepaths.file_preview_type = "NONE"
groups = {name: [] for name in COLORS}
for name, color in COLORS.items():
    bpy.data.materials.new(name).diffuse_color = color


def local(point):
    x, y, z = point
    return -x, -z, y


def activate(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def finish(obj, material, bevel):
    activate(obj)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    obj.data.materials.append(bpy.data.materials[material])
    if bevel:
        edge = obj.modifiers.new("Rounded sheet edges", "BEVEL")
        edge.width = bevel
        edge.segments = 2 if bevel >= .009 else 1
        edge.harden_normals = True
        bpy.ops.object.modifier_apply(modifier=edge.name)
        for face in obj.data.polygons:
            face.use_smooth = True
        normals = obj.modifiers.new("Weighted face normals", "WEIGHTED_NORMAL")
        normals.keep_sharp = True
        bpy.ops.object.modifier_apply(modifier=normals.name)
    uv = obj.data.uv_layers.active or obj.data.uv_layers.new(name="Surface metres")
    for face in obj.data.polygons:
        normal_axis = max(range(3), key=lambda axis: abs(face.normal[axis]))
        u, v = [axis for axis in range(3) if axis != normal_axis]
        for loop in face.loop_indices:
            point = obj.data.vertices[obj.data.loops[loop].vertex_index].co
            uv.data[loop].uv = point[u], point[v]
    groups[material].append(obj)


def form(name, rings, material, bevel, closed_frame=False):
    count = len(rings[0])
    vertices = [local(point) for ring in rings for point in ring]
    last = len(vertices) - count
    faces = [] if closed_frame else [tuple(reversed(range(count))), tuple(range(last, last + count))]
    for start in range(0, len(vertices) if closed_frame else last, count):
        following = (start + count) % len(vertices)
        for index in range(count):
            next_index = (index + 1) % count
            faces.append((start + index, start + next_index, following + next_index, following + index))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    edit = bmesh.new()
    edit.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(edit, faces=list(edit.faces))
    edit.to_mesh(mesh)
    edit.free()
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    finish(obj, material, bevel)


def outline(width, depth, chamfer, y):
    w, d = width / 2, depth / 2
    corners = ((-w + chamfer, -d), (w - chamfer, -d), (w, -d + chamfer), (w, d - chamfer),
               (w - chamfer, d), (-w + chamfer, d), (-w, d - chamfer), (-w, -d + chamfer))
    return [(x, y, z) for x, z in corners]


def box(name, center, size, material, bevel=.002):
    bpy.ops.mesh.primitive_cube_add(size=1, location=local(center))
    obj = bpy.context.object
    obj.name = name
    obj.scale = size[0], size[2], size[1]
    finish(obj, material, bevel)


form("Folded access frame", [
    outline(*profile) for profile in ((2.55, 1.55, .144, .022), (2.55, 1.55, .144, -.100),
        (2.46, 1.46, .122, -.174), (2.21, 1.216, .104, -.174),
        (2.16, 1.155, .092, -.128), (2.16, 1.155, .092, .022))
], FRAME, .009, closed_frame=True)
form("Dark return plenum", [
    outline(2.30, 1.27, .108, y) for y in (-.041, .014)
], RECESS, .006)

for index in range(12):
    x = (index - 5.5) * .172
    cross = ((-.048, -.141), (.031, -.103), (.048, -.096),
             (.050, -.102), (.040, -.115), (-.043, -.152))
    depth = .553 if index in (0, 11) else .579
    form("Angled sheet steel vane", [
        [(x + dx, y, z) for dx, y in cross] for z in (-depth, depth)
    ], FINS, .0025)
box("Vane support spine", (0, -.138, 0), (2.18, .051, .072), FRAME, .005)

for x in (-1.04, -.40, .40, 1.04):
    for z in (-.669, .669):
        bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=.023, depth=.013, location=local((x, -.176, z)))
        head = bpy.context.object
        head.name = "Captive frame fastener"
        finish(head, FINS, .0015)

bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / "CeilingReturnGrille.blend"))
for name, parts in groups.items():
    bpy.ops.object.select_all(action="DESELECT")
    for part in parts:
        part.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    if len(parts) > 1:
        bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = "SM_CeilingReturn_" + name
    triangles = obj.modifiers.new("Export triangles", "TRIANGULATE")
    triangles.keep_custom_normals = True
    bpy.ops.object.modifier_apply(modifier=triangles.name)

bpy.ops.object.select_all(action="SELECT")
triangles = sum(len(obj.data.polygons) for obj in bpy.context.selected_objects)
assert triangles < 3000, f"Ceiling return grille exceeded its mesh budget: {triangles}"
bpy.ops.export_scene.fbx(
    filepath=str(MODELS / "CeilingReturnGrille.fbx"), use_selection=True, object_types={"MESH"},
    axis_forward="-Z", axis_up="Y", bake_anim=False, add_leaf_bones=False, mesh_smooth_type="FACE")
print(f"Ceiling return grille: {len(groups)} material groups, {triangles} triangles.")
