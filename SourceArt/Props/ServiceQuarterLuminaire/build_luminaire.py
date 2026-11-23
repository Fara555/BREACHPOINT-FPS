"""Author the Service Quarter's twin-diffuser industrial ceiling light."""

from pathlib import Path

import bmesh
import bpy
import numpy as np

SOURCE = Path(__file__).resolve().parent
ASSETS = SOURCE.parents[2] / "Assets/Project/Art/Props/ServiceQuarterLuminaire"
MODELS = ASSETS / "Models"
TEXTURES = ASSETS / "Textures"
MODELS.mkdir(parents=True, exist_ok=True)
TEXTURES.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
bpy.context.preferences.filepaths.file_preview_type = "NONE"

colors = {
    "LuminaireHousing": (.075, .084, .091, 1),
    "LuminaireFrame": (.205, .219, .225, 1),
    "LuminaireSeal": (.028, .031, .032, 1),
    "LuminaireDiffuser": (.75, .79, .82, 1),
}
groups = {name: [] for name in colors}
materials = {}
for name, color in colors.items():
    material = bpy.data.materials.new(name)
    material.diffuse_color = color
    materials[name] = material


def point(x, y, z):
    return -x, -z, y


def finish(obj, name, width):
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(materials[name])
    if width:
        chamfer = obj.modifiers.new("Manufactured bevels", "BEVEL")
        chamfer.width = width
        chamfer.segments = 2 if width >= .004 else 1
        chamfer.harden_normals = True
        bpy.ops.object.modifier_apply(modifier=chamfer.name)
        for face in obj.data.polygons:
            face.use_smooth = True
        normals = obj.modifiers.new("Weighted face normals", "WEIGHTED_NORMAL")
        normals.keep_sharp = True
        bpy.ops.object.modifier_apply(modifier=normals.name)
    groups[name].append(obj)
    return obj


def box(label, center, size, name, bevel=.008):
    bpy.ops.mesh.primitive_cube_add(size=1, location=point(*center))
    obj = bpy.context.object
    obj.name = label
    obj.scale = size[0], size[2], size[1]
    return finish(obj, name, bevel)


def loft(label, offset, rings, corner, name, bevel):
    vertices = []
    for width, depth, y in rings:
        w, d = width / 2, depth / 2
        outline = [(-w + corner, -d), (w - corner, -d), (w, -d + corner), (w, d - corner),
                   (w - corner, d), (-w + corner, d), (-w, d - corner), (-w, -d + corner)]
        vertices.extend(point(offset + x, y, z) for x, z in outline)
    bottom = tuple(reversed(range(8)))
    top = tuple(range(len(vertices) - 8, len(vertices)))
    faces = [bottom, top]
    faces.extend((r * 8 + i, r * 8 + (i + 1) % 8, (r + 1) * 8 + (i + 1) % 8, (r + 1) * 8 + i)
                 for r in range(len(rings) - 1) for i in range(8))
    mesh = bpy.data.meshes.new(label)
    mesh.from_pydata(vertices, [], faces)
    edit = bmesh.new()
    edit.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(edit, faces=list(edit.faces))
    edit.to_mesh(mesh)
    edit.free()
    obj = bpy.data.objects.new(label, mesh)
    bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    finish(obj, name, bevel)
    if name == "LuminaireDiffuser":
        uv = obj.data.uv_layers.new(name="Optical density")
        for loop in obj.data.loops:
            co = obj.data.vertices[loop.vertex_index].co
            uv.data[loop.index].uv = (.5 + (-co.x - offset) / 1.76, .5 - co.y / .770)


def screw(x, z):
    bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=.024, depth=.013, location=point(x, .005, z))
    obj = bpy.context.object
    obj.name = "Captive fixing cup"
    finish(obj, "LuminaireSeal", .002)
    bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=.014, depth=.010, location=point(x, -.002, z))
    obj = bpy.context.object
    obj.name = "Captive fixing head"
    finish(obj, "LuminaireFrame", .0015)


loft("Folded upper enclosure", 0, [(4.08, 1.14, .057), (4.08, 1.14, .143), (3.92, .92, .257)],
     .114, "LuminaireHousing", .015)
box("Removable gear compartment cover", (0, .267, 0), (2.24, .028, .50), "LuminaireHousing", .01)

for side in (-1, 1):
    box("Long folded lens rail", (0, .028, side * .463), (3.94, .072, .150), "LuminaireFrame", .013)
    box("Shielded lens seal", (0, .009, side * .385), (3.74, .027, .037), "LuminaireSeal", .004)
    box("Capped lamp end", (side * 1.918, .033, 0), (.202, .083, .913), "LuminaireFrame", .018)
    box("Lens seat end gasket", (side * 1.782, .015, 0), (.045, .036, .793), "LuminaireSeal", .006)
    for z in (-.42, .42):
        screw(side * 1.914, z)
    for z in (-.414, .414):
        box("Captive diffuser clip", (side * .94, -.004, z), (.164, .035, .082), "LuminaireFrame", .010)

box("Replaceable optic divider", (0, .028, 0), (.103, .061, .804), "LuminaireFrame", .009)
for x in (-.924, .924):
    loft("Sealed frosted diffuser", x, [(1.740, .744, -.014), (1.772, .769, .010), (1.764, .762, .044)],
         .031, "LuminaireDiffuser", .006)
for x in (-1.56, 1.56):
    box("Housing attachment shoe", (x, .240, 0), (.356, .048, .690), "LuminaireHousing", .011)
    for z in (-.287, .287):
        box("Folded suspended bracket", (x, .363, z), (.102, .281, .082), "LuminaireFrame", .006)
    box("Ceiling mounting plate", (x, .498, 0), (.304, .032, .715), "LuminaireHousing", .011)
box("Sealed supply inlet", (.11, .307, 0), (.143, .116, .16), "LuminaireSeal", .01)

bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / "ServiceQuarterLuminaire.blend"))
for name, parts in groups.items():
    bpy.ops.object.select_all(action="DESELECT")
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    if len(parts) > 1:
        bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = "SM_Luminaire_" + name
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    tri = obj.modifiers.new("Export triangles", "TRIANGULATE")
    tri.keep_custom_normals = True
    bpy.ops.object.modifier_apply(modifier=tri.name)

bpy.ops.object.select_all(action="SELECT")
triangles = sum(len(obj.data.polygons) for obj in bpy.context.selected_objects)
assert triangles < 6200, triangles
bpy.ops.export_scene.fbx(
    filepath=str(MODELS / "ServiceQuarterLuminaire.fbx"), use_selection=True,
    object_types={"MESH"}, axis_forward="-Z", axis_up="Y", bake_anim=False,
    add_leaf_bones=False, mesh_smooth_type="FACE",
)
print("Ceiling luminaire triangles:", triangles)

# Broad falloff at the sealed ends of the diffuser. Avoid fine emitter patterns.
width, height = 256, 128
y, x = np.mgrid[0:height, 0:width].astype(np.float32)
u, v = x / (width - 1), y / (height - 1)
border = np.minimum(u, 1 - u) * 2
sides = np.minimum(v, 1 - v) * 2
luminance = .758 + .182 * np.sqrt(border) + .039 * np.sqrt(sides)
luminance += .012 * np.cos((u - .5) * np.pi) * np.cos((v - .5) * np.pi)
pixels = np.float32(np.dstack((luminance, luminance, luminance, np.ones_like(luminance))))
image = bpy.data.images.new("LuminaireDiffuser_Emission", width, height, alpha=True)
image.colorspace_settings.name = "Non-Color"
image.pixels.foreach_set(pixels.ravel())
image.filepath_raw = str(TEXTURES / "LuminaireDiffuser_Emission.png")
image.file_format = "PNG"
image.save()
