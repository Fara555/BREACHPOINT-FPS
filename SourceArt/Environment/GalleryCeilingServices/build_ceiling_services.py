"""Build the east gallery cable ladder and its fixture connections."""

import math
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector


SOURCE = Path(__file__).resolve().parent
MODELS = SOURCE.parents[2] / "Assets/Project/Art/Environment/ServiceQuarter/GalleryCeilingServices/Models"
MODELS.mkdir(parents=True, exist_ok=True)
ORIGIN = Vector((14.4, 7.35, 27.4))
TRAY_X, TRAY_Y = ORIGIN.x, ORIGIN.y
MATERIALS = {
    "TrayGalvanized": (.39, .425, .437, 1),
    "TrayFittings": (.17, .205, .22, 1),
    "TrayCableSheath": (.115, .124, .127, 1),
    "TrayOchre": (.41, .26, .12, 1),
}

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
bpy.context.preferences.filepaths.file_preview_type = "NONE"
groups = {}
for name, color in MATERIALS.items():
    material = bpy.data.materials.new(name)
    material.diffuse_color = color
    groups[name] = []


def local(point):
    p = Vector(point) - ORIGIN
    return Vector((-p.x, -p.z, p.y))


def activate(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def finish(obj, material, bevel):
    activate(obj)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    obj.data.materials.append(bpy.data.materials[material])
    if bevel:
        bevel_modifier = obj.modifiers.new("Formed edges", "BEVEL")
        bevel_modifier.width = bevel
        bevel_modifier.segments = 2 if bevel >= .010 else 1
        bevel_modifier.harden_normals = True
        bpy.ops.object.modifier_apply(modifier=bevel_modifier.name)
        for face in obj.data.polygons:
            face.use_smooth = True
        normals = obj.modifiers.new("Weighted metal faces", "WEIGHTED_NORMAL")
        normals.keep_sharp = True
        bpy.ops.object.modifier_apply(modifier=normals.name)
    if not obj.data.uv_layers:
        uv = obj.data.uv_layers.new(name="Surface metres")
        for face in obj.data.polygons:
            axes = sorted(range(3), key=lambda axis: abs(face.normal[axis]))[:2]
            for loop in face.loop_indices:
                p = obj.data.vertices[obj.data.loops[loop].vertex_index].co
                uv.data[loop].uv = p[axes[0]], p[axes[1]]
    groups[material].append(obj)
    return obj


def box(name, center, size, material, bevel=.006):
    bpy.ops.mesh.primitive_cube_add(size=1, location=local(center))
    obj = bpy.context.object
    obj.name = name
    obj.scale = size[0], size[2], size[1]
    return finish(obj, material, bevel)


def joined_rings(name, rings, material, bevel=0, smooth=False):
    width = len(rings[0])
    vertices = [local(point) for ring in rings for point in ring]
    last = len(vertices) - width
    faces = [tuple(reversed(range(width))), tuple(range(last, last+width))]
    for start in range(0, last, width):
        for i in range(width):
            a = start+i
            b = start+(i+1)%width
            faces.append((a,b,b+width,a+width))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    edit = bmesh.new()
    edit.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(edit, faces=list(edit.faces))
    edit.to_mesh(mesh)
    edit.free()
    mesh.update()
    for face in list(mesh.polygons)[2:]:
        face.use_smooth = smooth
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return finish(obj, material, bevel)


def direction(path, index):
    return (Vector(path[min(index+1,len(path)-1)]) - Vector(path[max(index-1,0)])).normalized()


def cable(name, path, radius, material, sides=10):
    rings = []
    for index, point in enumerate(path):
        tangent = direction(path, index)
        guide = Vector((0,1,0)) if abs(tangent.y)<.8 else Vector((0,0,1))
        across = guide.cross(tangent).normalized()
        vertical = tangent.cross(across).normalized()
        ring = []
        for side in range(sides):
            angle = math.tau*side/sides
            ring.append(Vector(point) + radius*(across*math.cos(angle)+vertical*math.sin(angle)))
        rings.append(ring)
    return joined_rings(name,rings,material,smooth=True)


def horizontal_steering(path, i):
    tangent = direction(path,i)
    return Vector((tangent.z,0,-tangent.x)).normalized()


def raceway_path():
    # Swept north end eases the bundle into the wall service trunk.
    elbow_x, elbow_z, radius = 15.15, 55.13, .75
    path = [(TRAY_X,TRAY_Y,27.4)]
    for step in range(9):
        theta = math.pi - step*math.pi/16
        path.append((elbow_x+radius*math.cos(theta), TRAY_Y, elbow_z+radius*math.sin(theta)))
    path.append((17.28,TRAY_Y,elbow_z+radius))
    return path


route = raceway_path()
profile = ((.156,0),(.246,0),(.246,.158),(.207,.158),
           (.207,.139),(.226,.139),(.226,.020),(.156,.020))
for sign in (-1,1):
    rings = []
    for i, point in enumerate(route):
        right = horizontal_steering(route,i)
        rings.append([Vector(point)+right*(sign*u)+Vector((0,h,0)) for u,h in profile])
    joined_rings("Folded raceway side rail",rings,"TrayGalvanized",.002)

# Broad removable covers alternate with short open inspection sections.
covers = ((28.0,32.1),(36.2,39.65),(45.55,48.75),(52.05,54.56))
for start,end in covers:
    box("Removable sheet cover",(TRAY_X,7.375,(start+end)/2),(.376,.026,end-start),"TrayGalvanized")
    for z in (start+.21,end-.21):
        box("Cover retaining bridge",(TRAY_X,7.347,z),(.474,.036,.090),"TrayFittings",.005)
for i in range(44):
    z = 27.8+i*.62
    if any(start<z<end for start,end in covers):
        continue
    box("Open ladder crosspiece",(TRAY_X,7.408,z),(.385,.063,.12),"TrayGalvanized",.004)
for x in (15.38,16.06,16.76,17.17):
    box("Turn crosspiece",(x,7.408,55.88),(.12,.063,.385),"TrayGalvanized",.004)

for z in (28,32,36,40,44,48,52,55.30):
    at_beam = z in (28,36,44,52)
    mount = 7.775 if at_beam else 8.04
    box("Suspension crossbar",(TRAY_X,7.319,z),(.664,.076,.108),"TrayFittings",.008)
    for side in (-1,1):
        x = TRAY_X+side*.298
        box("Slotted ceiling plate",(x,mount-.012,z),(.114,.028,.132),"TrayFittings",.006)
        cable("Suspension rod",[(x,7.300,z),(x,mount-.014,z)],.018,"TrayGalvanized",8)
        box("Adjustment block",(x,7.365,z),(.060,.059,.073),"TrayFittings",.005)
for x in (15.95,17.12):
    box("Turn support crossbar",(x,7.319,55.88),(.108,.076,.664),"TrayFittings")
    for delta in (-.299,.299):
        z=55.88+delta
        box("Turn ceiling anchor",(x,8.028,z),(.132,.028,.112),"TrayFittings")
        cable("Turn suspension rod",[(x,7.301,z),(x,8.014,z)],.017,"TrayGalvanized",8)

for i,(offset,radius) in enumerate(((-.104,.029),(-.025,.033),(.057,.029),(.126,.024))):
    path = []
    for j,point in enumerate(route):
        point = Vector(point)+horizontal_steering(route,j)*offset
        point.y=7.47+i*.006
        path.append(point)
    cable("Insulated supply bundle",path,radius,"TrayCableSheath")

# The south gland enters the ceiling. The north adapter drops into the wall trunk.
box("Overhead supply junction",(TRAY_X,7.762,27.40),(.57,.54,.34),"TrayFittings",.018)
box("Supply junction hatch",(TRAY_X,7.718,27.216),(.464,.28,.033),"TrayGalvanized",.010)
box("Supply circuit code strip",(TRAY_X,7.674,27.197),(.208,.032,.009),"TrayOchre",.003)
box("Wall trunk adapter",(17.70,6.69,55.88),(.18,.28,.48),"TrayFittings",.015)
for i,offset in enumerate((-.12,-.04,.04,.12)):
    z=55.88+offset
    cable("Wall downcomer",[(17.24,7.49+i*.004,z),(17.40,7.48,z),(17.54,7.37,z),
                            (17.58,7.24,z),(17.58,6.94,z),(17.61,6.82,z),(17.67,6.76,z)],
          .028,"TrayCableSheath")
box("Downcomer wall clamp",(17.604,7.02,55.88),(.11,.054,.405),"TrayGalvanized",.005)

for fixture in (28,42,56):
    z=fixture-.46
    if fixture==56:
        z=55.02
    box("Fixture branch junction",(14.132,7.470,z),(.176,.214,.328),"TrayFittings",.017)
    box("Junction cover",(14.033,7.470,z),(.034,.157,.262),"TrayGalvanized",.007)
    for offset in (-.053,.053):
        cable("Fixture supply conduit",
              [(14.045,7.51,z+offset),(13.88,7.55,z+offset),(13.68,7.641,z+offset),(12.55,7.641,z+offset),
               (12.33,7.641,z+offset),(12.21,7.641,z+.155+offset),(12.16,7.641,fixture-.10+offset),
               (12.11,7.641,fixture+offset),(12.033,7.641,fixture+offset)],.020,"TrayCableSheath")
        cable("Fixture cable gland",[(12.037,7.641,fixture+offset),(12.103,7.641,fixture+offset)],.031,"TrayFittings",8)
    box("Luminaire circuit sleeve",(13.82,7.533,z),(.094,.058,.167),"TrayOchre",.008)

bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/"GalleryCeilingServices.blend"))
for material, parts in groups.items():
    bpy.ops.object.select_all(action="DESELECT")
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]
    bpy.ops.object.join()
    obj=bpy.context.object
    obj.name="SM_GalleryCeilingServices_"+material
    triangles=obj.modifiers.new("Export triangles","TRIANGULATE")
    triangles.keep_custom_normals=True
    bpy.ops.object.modifier_apply(modifier=triangles.name)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(filepath=str(MODELS/"GalleryCeilingServices.fbx"),use_selection=True,
    object_types={"MESH"},axis_forward="-Z",axis_up="Y",bake_anim=False,add_leaf_bones=False,mesh_smooth_type="FACE")
print("GALLERY_CEILING",len(bpy.context.selected_objects),
      sum(len(obj.data.polygons) for obj in bpy.context.selected_objects),"triangles",flush=True)
