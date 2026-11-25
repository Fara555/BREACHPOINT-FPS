"""Build the fabricated extract duct above the west market."""

from math import cos, pi, sin
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector


SOURCE = Path(__file__).resolve().parent
ART = SOURCE.parents[2] / "Assets/Project/Art/Environment/ServiceQuarter"
MODELS = ART / "MarketExtract/Models"
MODELS.mkdir(parents=True, exist_ok=True)
ORIGIN = Vector((-12.05, 7.23, 32.31))
GALVANIZED, SUPPORT, MARKING = "DuctGalvanized", "DuctSupport", "DuctMarking"
COLORS = {
    GALVANIZED: (.44, .45, .425, 1),
    SUPPORT: (.215, .246, .251, 1),
    MARKING: (.47, .322, .15, 1),
}

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
bpy.context.preferences.filepaths.file_preview_type = "NONE"
groups = {name: [] for name in COLORS}
for name, color in COLORS.items():
    bpy.data.materials.new(name).diffuse_color = color


def local(point):
    x, y, z = Vector(point) - ORIGIN
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
        edge = obj.modifiers.new("Rolled sheet edge", "BEVEL")
        edge.width = bevel
        edge.segments = 2 if bevel >= .012 else 1
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


def box(name, center, size, material, bevel=.004):
    bpy.ops.mesh.primitive_cube_add(size=1, location=local(center))
    obj = bpy.context.object
    obj.name = name
    obj.scale = size[0], size[2], size[1]
    finish(obj, material, bevel)


def bolt(name, center, radius, depth, material):
    bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=radius, depth=depth, location=local(center))
    obj = bpy.context.object
    obj.name = name
    finish(obj, material, .0015)


def make_duct(name, profiles, material, bevel=.004):
    # Each profile is a chamfered rectangular section perpendicular to the duct.
    width = len(profiles[0])
    vertices = [local(point) for profile in profiles for point in profile]
    last = len(vertices) - width
    faces = [tuple(reversed(range(width))), tuple(range(last, last + width))]
    for start in range(0, last, width):
        for index in range(width):
            a = start + index
            b = start + (index + 1) % width
            faces.append((a, b, b + width, a + width))
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


def section(center, across, up, width, height, clip):
    w, h = width / 2, height / 2
    outline = ((-w + clip, -h), (w - clip, -h), (w, -h + clip), (w, h - clip),
               (w - clip, h), (-w + clip, h), (-w, h - clip), (-w, -h + clip))
    return [Vector(center) + Vector(across) * u + Vector(up) * v for u, v in outline]


def horizontal(x, z, width, height, heading=0, clip=.055):
    return section((x, ORIGIN.y, z), (cos(heading), 0, sin(heading)), (0, 1, 0), width, height, clip)


def riser_profile(x, y, z, width, depth, clip=.043):
    return section((x, y, z), (1, 0, 0), (0, 0, 1), width, depth, clip)


def flange(x, z, width, height, heading, length=.067):
    make_duct("Sealed rectangular duct flange", [
        horizontal(x - side * sin(heading) * length / 2, z + side * cos(heading) * length / 2,
                   width + .105, height + .103, heading) for side in (-1, 1)
    ], SUPPORT)


# The trunk skirts the light at z=42 and widens beyond each takeoff.
outer_x = ORIGIN.x
inner_x = -13.40
turn_begin = 53.16
sections = (
    (outer_x, 32.31, .76, .49, 0),
    (outer_x, 35.91, .76, .49, 0),
    (outer_x, 36.94, .95, .55, 0),
    (outer_x, 38.83, .95, .55, pi / 8),
    (inner_x, 40.48, .95, .55, pi / 8),
    (inner_x, 43.70, .95, .55, -pi / 8),
    (outer_x, 45.35, 1.16, .62, -pi / 8),
    (outer_x, 47.90, 1.16, .62, 0),
    (outer_x, 50.20, 1.16, .62, 0),
    (outer_x, turn_begin, 1.16, .62, 0),
)
for index, (a, b) in enumerate(zip(sections, sections[1:])):
    make_duct("Folded extract trunk section", [horizontal(*a), horizontal(*b)], GALVANIZED, .007)
    flange(*a, length=.105 if index == 0 else .067)
flange(*sections[-1])

for center_x, z, width, height, mount in (
    (outer_x, 33.26, .76, .49, 8.047), (outer_x, 36, .78, .50, 7.779),
    (inner_x, 41.14, .95, .55, 8.047), (-13.15, 44, 1.01, .57, 7.779),
    (outer_x, 48.03, 1.16, .62, 8.047), (outer_x, 52, 1.16, .62, 7.779),
):
    bottom = ORIGIN.y - height / 2 - .033
    box("Trapeze cross-member", (center_x, bottom, z), (width + .278, .081, .137), SUPPORT, .008)
    for side in (-1, 1):
        x = center_x + side * (width / 2 + .083)
        box("Structural anchor plate", (x, mount - .015, z), (.131, .035, .133), SUPPORT, .004)
        bolt("Threaded hanger rod", (x, (bottom + mount) / 2, z), .021, mount - bottom, GALVANIZED)
        bolt("Hanger locknut", (x, bottom - .050, z), .041, .023, SUPPORT)

for x, z in ((outer_x, 37.40), (inner_x, 43.17)):
    make_duct("Extract riser into market roof", [
        riser_profile(x, 5.056, z, .59, .655),
        riser_profile(x, 6.40, z, .59, .655),
        riser_profile(x, 6.80, z, .72, .78),
        riser_profile(x, 7.03, z, .72, .78),
    ], GALVANIZED, .006)
    make_duct("Roof curb and weathered flashing", [
        riser_profile(x, 5.048, z, .95, 1.02),
        riser_profile(x, 5.092, z, .95, 1.02),
        riser_profile(x, 5.211, z, .69, .75),
    ], SUPPORT, .010)
    for y, w, d in ((5.92, .647, .708), (6.43, .672, .742), (6.942, .81, .87)):
        make_duct("Bolted riser slip collar", [
            riser_profile(x, y + sign * .035, z, w, d) for sign in (-1, 1)
        ], SUPPORT)
    box("Rectangular damper shaft cover", (x + .341, 6.18, z), (.103, .254, .318), SUPPORT, .010)
    box("Damper position marker", (x + .399, 6.18, z), (.020, .032, .156), MARKING)
    for dx in (-.37, .37):
        for dz in (-.403, .403):
            bolt("Roof curb anchor", (x + dx, 5.104, z + dz), .023, .028, GALVANIZED)

# A swept elbow joins the wall plenum, using the real hall roof above it for support.
radius = 1.65
turn_x = outer_x - radius
turn_z = turn_begin
elbow = []
for i in range(11):
    theta = pi * i / 20
    elbow.append(horizontal(turn_x + cos(theta) * radius, turn_z + sin(theta) * radius,
                             1.16, .62, theta))
make_duct("Swept ninety degree extract elbow", elbow, GALVANIZED, .004)
for theta in (pi / 6, pi / 3):
    make_duct("Curved elbow seam", [horizontal(
        turn_x + cos(angle) * radius, turn_z + sin(angle) * radius, 1.199, .652, angle)
        for angle in (theta - .011, theta + .011)], SUPPORT, .002)

west_z = turn_z + radius
west = [(turn_x, 1.16, .62), (-16.64, 1.16, .62), (-17.36, 1.42, .71), (-17.84, 1.42, .71)]
make_duct("Wall plenum transition", [horizontal(x, west_z, width, height, pi / 2)
                                     for x, width, height in west], GALVANIZED, .006)
for x, width, height in ((turn_x, 1.265, .723), (-16.64, 1.26, .72), (-17.84, 1.58, .86)):
    make_duct("Bolted plenum flange", [horizontal(x + side * .033, west_z, width, height, pi / 2)
                                     for side in (-1, 1)], SUPPORT)

box("Wall penetration back frame", (-17.913, 7.234, west_z), (.161, .964, 1.74), SUPPORT, .015)
for x in (-15.65, -16.94):
    box("Wall branch trapeze", (x, 6.878, west_z), (.146, .079, 1.58), SUPPORT, .010)
    for side in (-1, 1):
        z = west_z + side * .687
        box("Wall branch hanger", (x, 7.455, z), (.041, 1.170, .041), GALVANIZED)
        box("Wall branch anchor plate", (x, 8.031, z), (.134, .033, .128), SUPPORT)

# The former south end flange now joins a continuation into the west wall.
south_bend_z = 31.89
south_radius = 1.47
south_bend_x = outer_x - south_radius
south_wall_z = south_bend_z - south_radius
make_duct("South trunk continuation", [
    horizontal(outer_x, z, .76, .49) for z in (ORIGIN.z, south_bend_z)
], GALVANIZED, .006)
south_elbow = []
for step in range(11):
    theta = pi * step / 20
    south_elbow.append(horizontal(south_bend_x + cos(theta) * south_radius,
                                  south_bend_z - sin(theta) * south_radius, .76, .49, -theta))
make_duct("South swept elbow", south_elbow, GALVANIZED, .004)
flange(south_bend_x, south_wall_z, .76, .49, -pi / 2)
for theta in (pi / 6, pi / 3):
    make_duct("South elbow seam", [
        horizontal(south_bend_x + cos(angle) * south_radius, south_bend_z - sin(angle) * south_radius,
                   .801, .53, -angle) for angle in (theta - .011, theta + .011)
    ], SUPPORT, .002)


def south_wall_profile(x, y, width, height):
    return section((x, y, south_wall_z), (0, 0, -1), (0, 1, 0), width, height, .055)


make_duct("South descending wall adapter", [
    south_wall_profile(x, y, width, height) for x, y, width, height in (
        (south_bend_x, ORIGIN.y, .76, .49), (-16.04, ORIGIN.y, .76, .49),
        (-17.29, 6.98, .89, .51), (-18.023, 6.98, .89, .51),
    )
], GALVANIZED, .006)
make_duct("South adapter inlet flange", [
    south_wall_profile(-16.04 + side * .034, ORIGIN.y, .854, .592) for side in (-1, 1)
], SUPPORT)
make_duct("South wall sealed penetration flange", [
    south_wall_profile(x, 6.98, width, height)
    for x, width, height in ((-18.008, 1.103, .666), (-17.764, 1.103, .666), (-17.706, 1.055, .621))
], SUPPORT, .010)
for x, center_y, width, height in ((-14.73, ORIGIN.y, .76, .49), (-16.71, 7.098, .830, .503)):
    bottom = center_y - height / 2 - .030
    box("South branch hanger crossbar", (x, bottom, south_wall_z), (.137, .081, width + .272), SUPPORT, .008)
    for side in (-1, 1):
        z = south_wall_z + side * (width / 2 + .081)
        box("South branch ceiling anchor", (x, 8.030, z), (.135, .035, .130), SUPPORT)
        bolt("South branch threaded rod", (x, (bottom + 8.04) / 2, z), .021, 8.04 - bottom, GALVANIZED)
        bolt("South branch locknut", (x, bottom - .051, z), .039, .024, SUPPORT)

# Removable inspection hatches face the aisle, above the roof line.
for z, face, height in ((41.29, inner_x + .475, .55), (49.36, outer_x + .58, .62)):
    make_duct("Inspection cover", [horizontal(face + offset, z, .545, height - .146, pi / 2, .037)
                                  for offset in (-.009, .023)], SUPPORT, .004)
    box("Inspection cover pull bracket", (face + .036, 7.195, z + .105), (.049, .059, .155), GALVANIZED, .007)
    box("Small inspection service tab", (face + .025, 7.346, z - .112), (.008, .052, .156), MARKING)

label = bpy.data.curves.new("Air path stencil", "FONT")
label.body = "EXTRACT"
label.font = bpy.data.fonts.load(str(ART / "CornerSample/Fonts/Orbitron.ttf"))
label.align_x = "CENTER"
label.align_y = "CENTER"
label.size = .22
label.resolution_u = 2
obj = bpy.data.objects.new(label.name, label)
bpy.context.collection.objects.link(obj)
obj.location = local((outer_x + .586, 7.195, 50.91))
obj.rotation_euler = Matrix(((0, 0, -1), (-1, 0, 0), (0, 1, 0))).to_euler()
activate(obj)
bpy.ops.object.convert(target="MESH")
finish(bpy.context.object, MARKING, 0)

bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / "MarketExtract.blend"))
for name, parts in groups.items():
    bpy.ops.object.select_all(action="DESELECT")
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = "SM_MarketExtract_" + name
    triangles = obj.modifiers.new("Export triangles", "TRIANGULATE")
    triangles.keep_custom_normals = True
    bpy.ops.object.modifier_apply(modifier=triangles.name)

bpy.ops.object.select_all(action="SELECT")
triangles = sum(len(obj.data.polygons) for obj in bpy.context.selected_objects)
assert triangles <= 11500, f"Market extract exceeded its mesh budget: {triangles}"
bpy.ops.export_scene.fbx(
    filepath=str(MODELS / "MarketExtract.fbx"), use_selection=True, object_types={"MESH"},
    axis_forward="-Z", axis_up="Y", bake_anim=False, add_leaf_bones=False, mesh_smooth_type="FACE")
print(f"Market extract: {len(groups)} material groups, {triangles} triangles.")
