"""Build the replaceable walkway-facing sheet for the Service Quarter parapet."""

import math
from pathlib import Path

import bpy
import bmesh
import numpy as np

SOURCE = Path(__file__).resolve().parent
PROJECT = SOURCE.parents[2]
ASSETS = PROJECT / "Assets/Project/Art/Environment/ServiceQuarter/GalleryParapet"
MODELS = ASSETS / "Models"
TEXTURES = ASSETS / "Textures"
MODELS.mkdir(parents=True, exist_ok=True)
TEXTURES.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
bpy.context.preferences.filepaths.file_preview_type = "NONE"

WIDTH = 1.88
HEIGHT = .96
NAME = "GalleryParapetPaint"
material = bpy.data.materials.new(NAME)
material.diffuse_color = (.12, .18, .19, 1)
fittings = bpy.data.materials.new("GalleryParapetFittings")
fittings.diffuse_color = (.14, .16, .17, 1)


def contour(horizontal, vertical, chamfer):
    w, h = WIDTH / 2 - horizontal, HEIGHT / 2 - vertical
    return [(-w + chamfer, -h), (w - chamfer, -h), (w, -h + chamfer),
            (w, h - chamfer), (w - chamfer, h), (-w + chamfer, h),
            (-w, h - chamfer), (-w, -h + chamfer)]


# A folded outer flange, a broad shoulder and a pressed central face.
rings = [
    (-.013, .000, .000, .047),
    (.010, .000, .000, .047),
    (.019, .012, .012, .044),
    (.019, .053, .052, .049),
    (.016, .064, .061, .052),
    (.003, .095, .081, .057),
    (.001, .111, .093, .060),
]
vertices, faces = [], []
for depth, horizontal, vertical, corner in rings:
    vertices.extend((-depth, -z, y + HEIGHT / 2)
                    for z, y in contour(horizontal, vertical, corner))
for ring in range(len(rings) - 1):
    a, b = ring * 8, (ring + 1) * 8
    faces.extend((a + i, a + (i + 1) % 8, b + (i + 1) % 8, b + i)
                 for i in range(8))
faces.extend((tuple(reversed(range(8))), tuple(range(len(vertices) - 8, len(vertices)))))
coated_faces = len(faces)


def closed_profile(first, second):
    start, count = len(vertices), len(first)
    vertices.extend(first)
    vertices.extend(second)
    faces.extend((tuple(range(start + count - 1, start - 1, -1)),
                  tuple(range(start + count, start + count * 2))))
    faces.extend((start + i, start + (i + 1) % count,
                  start + (i + 1) % count + count, start + i + count)
                 for i in range(count))


# The folded return overlaps the top cap and the existing gasket.
return_section = [(-.029, 1.042), (-.007, 1.042), (-.002, 1.036),
                  (-.002, .999), (-.007, .994), (-.019, .994)]
closed_profile([(-x, -.987, y) for x, y in return_section],
               [(-x, .987, y) for x, y in return_section])

# Metal washers read separately from the dark captive mounts already in the parapet.
for mount in (-.82, .82):
    for depth, radius in ((.018, .0115), (.026, .0115), (.029, .0085), (.026, .0050)):
        vertices.extend((-depth, -mount + math.cos(i * math.tau / 12) * radius,
                         .830 + math.sin(i * math.tau / 12) * radius) for i in range(12))
    start = len(vertices) - 48
    for layer in range(3):
        a, b = start + layer * 12, start + (layer + 1) * 12
        faces.extend((a + i, a + (i + 1) % 12, b + (i + 1) % 12, b + i) for i in range(12))
    faces.extend((tuple(reversed(range(start, start + 12))), tuple(range(start + 36, start + 48))))

surface = bpy.data.meshes.new("Pressed safety sheet")
surface.from_pydata(vertices, [], faces)
surface.materials.append(material)
surface.materials.append(fittings)
for face in surface.polygons:
    face.material_index = int(face.index >= coated_faces)
bm = bmesh.new()
bm.from_mesh(surface)
bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
assert all(edge.is_manifold for edge in bm.edges)
bm.to_mesh(surface)
bm.free()
surface.update()
uv = surface.uv_layers.new(name="Coating")
for loop in surface.loops:
    point = surface.vertices[loop.vertex_index].co
    uv.data[loop.index].uv = (.5 - point.y / WIDTH, point.z / HEIGHT)
for face in surface.polygons:
    face.use_smooth = True

panel = bpy.data.objects.new("SM_GalleryParapetPanel", surface)
bpy.context.collection.objects.link(panel)
panel.select_set(True)
bpy.context.view_layer.objects.active = panel
normals = panel.modifiers.new("Weighted formed-metal normals", "WEIGHTED_NORMAL")
normals.keep_sharp = True
normals.weight = 35
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / "GalleryParapetPanel.blend"))
bpy.ops.object.modifier_apply(modifier=normals.name)
tri = panel.modifiers.new("Export triangles", "TRIANGULATE")
tri.keep_custom_normals = True
bpy.ops.object.modifier_apply(modifier=tri.name)
bpy.ops.export_scene.fbx(
    filepath=str(MODELS / "GalleryParapetPanel.fbx"), use_selection=True,
    object_types={"MESH"}, axis_forward="-Z", axis_up="Y", bake_anim=False,
    add_leaf_bones=False, mesh_smooth_type="FACE",
)
print("Parapet panel triangles:", len(panel.data.polygons))

size = 512
rng = np.random.default_rng(2517)
v, u = np.mgrid[0:size, 0:size].astype(np.float32) / (size - 1)


def grain(cell_count):
    lattice = rng.random((cell_count, cell_count), dtype=np.float32)
    x, y = u * cell_count, v * cell_count
    ix, iy = x.astype(int), y.astype(int)
    a, b = x - ix, y - iy
    a, b = a * a * (3 - 2 * a), b * b * (3 - 2 * b)
    return ((1 - a) * (1 - b) * lattice[iy % cell_count, ix % cell_count] +
            a * (1 - b) * lattice[iy % cell_count, (ix + 1) % cell_count] +
            (1 - a) * b * lattice[(iy + 1) % cell_count, ix % cell_count] +
            a * b * lattice[(iy + 1) % cell_count, (ix + 1) % cell_count]) - .5


def encode(name, rgba):
    # File-space values are supplied directly. Unity selects the proper import space.
    pixels = np.float32(np.clip(rgba, 0, 1))
    image = bpy.data.images.new(name, size, size, alpha=True)
    image.colorspace_settings.name = "Non-Color"
    image.pixels.foreach_set(pixels.ravel())
    image.filepath_raw = str(TEXTURES / (name + ".png"))
    image.file_format = "PNG"
    image.save()


micro, coating, broad = grain(257), grain(55), grain(13)
edge = np.minimum(np.minimum(u, 1 - u), np.minimum(v, 1 - v))
bare = np.maximum(0, micro + .46 * coating - .39) * np.maximum(0, 1 - edge * 122)
edge_dirt = np.maximum(0, 1 - v * 12) * (.56 + broad * .55)
scuffs = np.zeros_like(u)
for _ in range(32):
    x, y = rng.uniform(.05, .94), rng.uniform(.68, .95)
    length = rng.uniform(.003, .034)
    dist = np.sqrt(((u - x) / length) ** 2 + ((v - y) / .00165) ** 2)
    scuffs += np.maximum(0, 1 - dist) * rng.uniform(.04, .17)
wear = np.clip(3.3 * bare + scuffs, 0, .72)
tint = np.array([.326, .403, .423], dtype=np.float32)
base = tint * (1 + broad * .047 + coating * .035 + micro * .015 - edge_dirt * .078)[:, :, None]
base = base * (1 - wear[:, :, None]) + np.array([.50, .54, .55]) * wear[:, :, None]
one = np.ones_like(u)
encode(NAME + "_BaseColor", np.dstack((base, one)))
smooth = .35 + coating * .056 + micro * .040 + wear * .24 - edge_dirt * .039
encode(NAME + "_Mask", np.stack((.075 + wear * .86, 1 - edge_dirt * .06, one, smooth), axis=-1))
height = micro * .047 + coating * .11 - scuffs * .12
dy, dx = np.gradient(height)
normal = np.stack((-dx * .9, -dy * .75, one), axis=-1)
normal /= np.linalg.norm(normal, axis=-1, keepdims=True)
encode(NAME + "_Normal", np.dstack((normal * .5 + .5, one)))
