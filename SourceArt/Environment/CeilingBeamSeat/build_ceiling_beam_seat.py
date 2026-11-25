"""Build a rolled steel connection for the transverse ceiling beams."""

from math import pi
from pathlib import Path

import bmesh
import bpy


SOURCE = Path(__file__).resolve().parent
MODELS = SOURCE.parents[2] / "Assets/Project/Art/Environment/ServiceQuarter/CeilingBeamSeat/Models"
MODELS.mkdir(parents=True, exist_ok=True)
COAT, FASTENER = "BeamSeatCoat", "BeamSeatFastener"
COLORS = {COAT: (.309, .331, .339, 1), FASTENER: (.34, .38, .40, 1)}

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
    edge = obj.modifiers.new("Rolled edge radius", "BEVEL")
    edge.width = bevel
    edge.segments = 2 if bevel >= .010 else 1
    edge.harden_normals = True
    bpy.ops.object.modifier_apply(modifier=edge.name)
    for face in obj.data.polygons:
        face.use_smooth = True
    normals = obj.modifiers.new("Weighted planar faces", "WEIGHTED_NORMAL")
    normals.keep_sharp = True
    bpy.ops.object.modifier_apply(modifier=normals.name)
    uv = obj.data.uv_layers.active or obj.data.uv_layers.new(name="Surface metres")
    for face in obj.data.polygons:
        normal_axis = max(range(3), key=lambda axis: abs(face.normal[axis]))
        u, v = [axis for axis in range(3) if axis != normal_axis]
        for loop in face.loop_indices:
            p = obj.data.vertices[obj.data.loops[loop].vertex_index].co
            uv.data[loop].uv = p[u], p[v]
    groups[material].append(obj)


def sweep(name, sections, material, bevel):
    count = len(sections[0])
    points = [local(point) for section in sections for point in section]
    last = len(points) - count
    faces = [tuple(reversed(range(count))), tuple(range(last, last + count))]
    for start in range(0, last, count):
        for index in range(count):
            a = start + index
            b = start + (index + 1) % count
            faces.append((a, b, b + count, a + count))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(points, [], faces)
    edit = bmesh.new()
    edit.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(edit, faces=list(edit.faces))
    edit.to_mesh(mesh)
    edit.free()
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    finish(obj, material, bevel)


def plate_outline(x, bottom, top, half_span, chamfer):
    return [(x, y, z) for y, z in (
        (bottom, -half_span + chamfer), (bottom, half_span - chamfer),
        (bottom + chamfer, half_span), (top - chamfer, half_span),
        (top, half_span - chamfer), (top, -half_span + chamfer),
        (top - chamfer, -half_span), (bottom + chamfer, -half_span),
    )]


def cylinder(name, center, radius, depth, material, sides, facing_wall):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=sides, radius=radius, depth=depth, location=local(center),
        rotation=(0, pi / 2, 0) if facing_wall else (0, 0, 0))
    obj = bpy.context.object
    obj.name = name
    finish(obj, material, .0018)


# The thick plate seats inside the wall header. The returned lip covers the
# interface between the existing beam and the underside saddle.
sweep("Wall header anchor plate", [
    plate_outline(*profile) for profile in ((-.218, -.071, .245, .304, .036),
        (.032, -.071, .245, .304, .036), (.066, -.061, .235, .294, .035))
], COAT, .008)
sweep("Folded bearing saddle", [
    plate_outline(*profile) for profile in ((.044, -.078, .010, .249, .022),
        (.535, -.078, .010, .249, .022), (.683, -.067, .006, .203, .020),
        (.722, -.043, .008, .175, .020))
], COAT, .006)

rib = ((.041, -.068), (.547, -.068), (.705, -.042), (.686, .026),
       (.468, .078), (.248, .182), (.122, .239), (.041, .239))
for side in (-1, 1):
    sweep("Tapered side reinforcing cheek", [
        [(x, y, side * z) for x, y in rib] for z in (.097, .193)
    ], COAT, .009)

for y in (-.024, .184):
    for z in (-.247, .247):
        cylinder("Structural anchor washer", (.070, y, z), .024, .014, COAT, 8, True)
        cylinder("Hex anchor head", (.082, y, z), .018, .019, FASTENER, 6, True)
for z in (-.146, .146):
    cylinder("Visible saddle fastener", (.592, -.076, z), .021, .020, FASTENER, 6, False)

bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / "CeilingBeamSeat.blend"))
for name, parts in groups.items():
    bpy.ops.object.select_all(action="DESELECT")
    for part in parts:
        part.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = "SM_CeilingBeamSeat_" + name
    triangles = obj.modifiers.new("Export triangles", "TRIANGULATE")
    triangles.keep_custom_normals = True
    bpy.ops.object.modifier_apply(modifier=triangles.name)

bpy.ops.object.select_all(action="SELECT")
triangles = sum(len(obj.data.polygons) for obj in bpy.context.selected_objects)
assert triangles <= 1600, f"Beam seat exceeded its mesh budget: {triangles}"
bpy.ops.export_scene.fbx(
    filepath=str(MODELS / "CeilingBeamSeat.fbx"), use_selection=True, object_types={"MESH"},
    axis_forward="-Z", axis_up="Y", bake_anim=False, add_leaf_bones=False, mesh_smooth_type="FACE")
print(f"Ceiling beam seat: {len(groups)} material groups, {triangles} triangles.")
