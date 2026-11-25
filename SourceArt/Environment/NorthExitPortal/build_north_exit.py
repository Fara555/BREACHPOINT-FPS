"""Build the north service exit as an editable model and a six-material FBX."""

from math import pi
from pathlib import Path

import bmesh
import bpy


SOURCE = Path(__file__).resolve().parent
PROJECT = SOURCE.parents[2]
ART = PROJECT / "Assets/Project/Art/Environment/ServiceQuarter"
MODELS = ART / "NorthExitPortal/Models"
MODELS.mkdir(parents=True, exist_ok=True)

COLORS = {
    "PortalFrame": (.235, .265, .265, 1),
    "PortalEnamel": (.315, .350, .352, 1),
    "PortalHardware": (.36, .385, .38, 1),
    "PortalSeal": (.092, .12, .12, 1),
    "PortalMarking": (.64, .64, .54, 1),
    "PortalGuide": (.245, .49, .36, 1),
}

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
bpy.context.preferences.filepaths.file_preview_type = "NONE"
groups = {name: [] for name in COLORS}
for name, color in COLORS.items():
    bpy.data.materials.new(name).diffuse_color = color


def activate(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def finish(obj, material, bevel):
    activate(obj)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    obj.data.materials.append(bpy.data.materials[material])
    if bevel:
        edge = obj.modifiers.new("Edge radius", "BEVEL")
        edge.width = bevel
        edge.segments = 2 if bevel >= .009 else 1
        edge.harden_normals = True
        bpy.ops.object.modifier_apply(modifier=edge.name)
        for face in obj.data.polygons:
            face.use_smooth = True
        normals = obj.modifiers.new("Weighted metal faces", "WEIGHTED_NORMAL")
        normals.keep_sharp = True
        bpy.ops.object.modifier_apply(modifier=normals.name)
    uv = obj.data.uv_layers.active or obj.data.uv_layers.new(name="Surface metres")
    for face in obj.data.polygons:
        dominant_axis = max(range(3), key=lambda axis: abs(face.normal[axis]))
        axes = [axis for axis in range(3) if axis != dominant_axis]
        for loop in face.loop_indices:
            p = obj.data.vertices[obj.data.loops[loop].vertex_index].co
            uv.data[loop].uv = p[axes[0]], p[axes[1]]
    groups[material].append(obj)


def local(p):
    # Local Unity axes, with the occupied side toward negative Z.
    return -p[0], -p[2], p[1]


def box(name, center, size, material, bevel=.006):
    bpy.ops.mesh.primitive_cube_add(size=1, location=local(center))
    obj = bpy.context.object
    obj.name = name
    obj.scale = size[0], size[2], size[1]
    finish(obj, material, bevel)


def rings(name, profiles, material, bevel):
    width = len(profiles[0])
    points = [local(p) for profile in profiles for p in profile]
    last = len(points) - width
    faces = [tuple(reversed(range(width))), tuple(range(last, last + width))]
    for start in range(0, last, width):
        for index in range(width):
            a = start + index
            b = start + (index + 1) % width
            faces.append((a, b, b + width, a + width))
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


def rectangle(x, y, width, height, clip, z):
    w, h = width * .5, height * .5
    corners = ((-w + clip, -h), (w - clip, -h), (w, -h + clip), (w, h - clip),
               (w - clip, h), (-w + clip, h), (-w, h - clip), (-w, -h + clip))
    return [(x + u, y + v, z) for u, v in corners]


def stamping(name, x, y, profiles, material, bevel=.005):
    rings(name, [rectangle(x, y, *profile) for profile in profiles], material, bevel)


def pin(name, center, radius, depth, material, axis="z", sides=8):
    rotation = (0, 0, 0) if axis == "y" else (pi / 2, 0, 0)
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=sides, radius=radius, depth=depth, location=local(center), rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    finish(obj, material, .0015)


def stencil():
    text = bpy.data.curves.new("North service opening stencil", "FONT")
    text.body = "N-06"
    text.font = bpy.data.fonts.load(str(ART / "CornerSample/Fonts/Orbitron.ttf"))
    text.align_x = "CENTER"
    text.align_y = "CENTER"
    text.size = .36
    text.resolution_u = 2
    obj = bpy.data.objects.new(text.name, text)
    bpy.context.collection.objects.link(obj)
    obj.location = local((-.83, 2.89, .011))
    obj.rotation_euler = (pi / 2, 0, pi)
    activate(obj)
    bpy.ops.object.convert(target="MESH")
    finish(bpy.context.object, "PortalMarking", 0)


box("Dark steel door pocket", (0, 2.15, .272), (3.56, 4.25, .105), "PortalSeal", .012)
box("Recessed approach threshold", (0, .024, -.003), (3.52, .032, .399), "PortalHardware", .006)
for side in (-1, 1):
    post = [(1.69, .025), (2.055, .025), (2.095, .065), (2.055, .53),
            (2.055, 3.965), (1.975, 4.37), (1.82, 4.434), (1.66, 4.434), (1.59, 4.27)]
    front = [(1.72, .035), (2.02, .035), (2.064, .075), (2.023, .53),
             (2.023, 3.96), (1.941, 4.34), (1.802, 4.389), (1.69, 4.389), (1.63, 4.25)]
    rings("Folded frame upright", [[(side * x, y, z) for x, y in outline] for outline, z
          in ((post, .304), (post, -.154), (front, -.213))], "PortalFrame", .009)
    x = side * 1.888
    stamping("Replaceable base guard", x, .267, ((.278, .48, .042, -.176), (.247, .447, .045, -.274)),
             "PortalHardware", .007)
    stamping("Guide channel black gasket", x, 3.367, ((.109, .795, .034, -.196), (.098, .772, .033, -.24)),
             "PortalSeal", .004)
    stamping("Recessed green guide lens", x, 3.367, ((.031, .667, .012, -.232), (.028, .656, .012, -.251)),
             "PortalGuide", .002)
    for y in (.115, .425, 2.19, 3.94):
        z = -.275 if y < .5 else -.215
        pin("Captive frame fastener", (x, y, z), .024, .018, "PortalHardware", sides=8)

    leaf = side * .831
    stamping("Pressed double door leaf", leaf, 2.182,
             ((1.626, 4.211, .111, .251), (1.626, 4.211, .111, .106), (1.567, 4.14, .099, .057),
              (1.315, 2.92, .158, .057), (1.165, 2.735, .137, .014)),
             "PortalEnamel", .008)
    box("Meeting stile", (side * .050, 2.17, .03), (.064, 4.12, .071), "PortalFrame", .006)
    stamping("Lower wear plate", leaf, .423, ((1.352, .52, .054, .066), (1.322, .489, .055, .024)),
             "PortalFrame", .006)
    for x_offset in (-.55, .55):
        for y in (.247, .595):
            pin("Wear plate rivet", (leaf + x_offset, y, .026), .016, .012, "PortalHardware")
    for y in (.853, 2.187, 3.62):
        x = side * 1.646
        box("Hinge mount", (x, y, .056), (.208, .304, .100), "PortalFrame", .013)
        pin("Hinge knuckle", (x, y, -.018), .052, .246, "PortalHardware", "y", 12)
        for offset in (-.072, .072):
            pin("Hinge spacer", (x, y + offset, -.018), .055, .017, "PortalSeal", "y", 12)

    x = side * .389
    stamping("Recessed pull handle pocket", x, 1.68, ((.385, .275, .051, .031), (.362, .25, .047, -.010)),
             "PortalSeal", .007)
    for offset in (-.121, .121):
        box("Pull handle mount", (x + offset, 1.68, -.048), (.056, .053, .108), "PortalHardware", .011)
    box("Bridge pull handle", (x, 1.68, -.090), (.294, .058, .041), "PortalHardware", .011)

stamping("Folded header cover", 0, 4.366,
         ((4.116, .359, .092, .302), (4.116, .359, .092, -.224), (4.044, .316, .071, -.287)),
         "PortalFrame", .012)
for x in (-1.263, 1.263):
    stamping("Header indicator guard", x, 4.356,
             ((.77, .115, .027, -.278), (.747, .099, .025, -.312)), "PortalSeal", .004)
    stamping("Header indicator lens", x, 4.354,
             ((.58, .030, .013, -.311), (.57, .026, .012, -.321)), "PortalGuide", .0015)

stamping("Badge reader enclosure", 1.91, 1.45,
         ((.261, .364, .048, -.189), (.231, .332, .044, -.286)), "PortalHardware", .006)
stamping("Badge reader face", 1.913, 1.45,
         ((.183, .266, .025, -.277), (.172, .253, .022, -.297)), "PortalSeal", .004)
box("Badge reader indicator", (1.913, 1.528, -.299), (.101, .023, .012), "PortalGuide", .003)
for y in (1.38, 1.413):
    box("Badge reader molded grip", (1.913, y, -.300), (.083, .008, .014), "PortalHardware", .001)
stencil()

bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / "NorthExitPortal.blend"))
for name, parts in groups.items():
    bpy.ops.object.select_all(action="DESELECT")
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    if len(parts) > 1:
        bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = "SM_NorthExit_" + name
    triangles = obj.modifiers.new("Export triangles", "TRIANGULATE")
    triangles.keep_custom_normals = True
    bpy.ops.object.modifier_apply(modifier=triangles.name)

bpy.ops.object.select_all(action="SELECT")
triangles = sum(len(obj.data.polygons) for obj in bpy.context.selected_objects)
assert triangles <= 8000, f"North portal exceeds its mesh budget: {triangles}"
bpy.ops.export_scene.fbx(
    filepath=str(MODELS / "NorthExitPortal.fbx"), use_selection=True, object_types={"MESH"},
    axis_forward="-Z", axis_up="Y", bake_anim=False, add_leaf_bones=False, mesh_smooth_type="FACE")
print(f"North exit: {len(groups)} material groups, {triangles} triangles.")
