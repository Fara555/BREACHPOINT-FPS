"""Standalone south storage cabinet: formed shell, inset double doors and service hardware."""
import bpy,bmesh,math,json
from pathlib import Path
from mathutils import Vector
SOURCE=Path(__file__).resolve().parent;PROJECT=SOURCE.parents[2]
OUT=PROJECT/'Assets/Project/Art/Props/MarketStorageCabinet/Models';OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.context.preferences.filepaths.save_version=0;bpy.context.preferences.filepaths.file_preview_type='NONE'
def coord(p):return Vector((-p[0],-p[2],p[1]))
def finish(o,name,mat,r=.006,door_half=None):
    o.name=name;bpy.context.view_layer.objects.active=o
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    if r:
        mod=o.modifiers.new('Manufactured radius','BEVEL');mod.width=r;mod.segments=3;bpy.ops.object.modifier_apply(modifier=mod.name)
    for f in o.data.polygons:f.use_smooth=True
    mod=o.modifiers.new('Broad surface normals','WEIGHTED_NORMAL');mod.keep_sharp=True;bpy.ops.object.modifier_apply(modifier=mod.name)
    o.data.materials.append(bpy.data.materials.get(mat) or bpy.data.materials.new(mat))
    for layer in list(o.data.uv_layers):o.data.uv_layers.remove(layer)
    uv=o.data.uv_layers.new(name='UV0');metric=o.data.uv_layers.new(name='SurfaceMeters')
    mins=[min(v.co[a] for v in o.data.vertices) for a in range(3)];maxs=[max(v.co[a] for v in o.data.vertices) for a in range(3)]
    for f in o.data.polygons:
        axes=sorted(range(3),key=lambda a:abs(f.normal[a]))[:2]
        for loop in f.loop_indices:
            p=o.data.vertices[o.data.loops[loop].vertex_index].co
            u=(p[axes[0]]-mins[axes[0]])/max(.00001,maxs[axes[0]]-mins[axes[0]])
            v=(p[axes[1]]-mins[axes[1]])/max(.00001,maxs[axes[1]]-mins[axes[1]])
            uv.data[loop].uv=((u+door_half)/2 if door_half is not None else u,v)
            metric.data[loop].uv=(p[axes[0]],p[axes[1]])
    o.data.uv_layers.active_index=0;return o
def box(name,p,size,mat,r=.006):
    bpy.ops.mesh.primitive_cube_add(size=1,location=coord(p));o=bpy.context.object;o.scale=(size[0],size[2],size[1]);return finish(o,name,mat,r)
def shape(w,b,t,c):return [(-w/2+c,b),(w/2-c,b),(w/2,b+c),(w/2,t-c),(w/2-c,t),(-w/2+c,t),(-w/2,t-c),(-w/2,b+c)]
def loft(name,rings,mat,center=0,annular=False,door_half=None):
    v=[coord((x,y,z+center)) for x,profile in rings for z,y in profile]
    faces=[(j*8+i,j*8+(i+1)%8,((j+1)%len(rings))*8+(i+1)%8,((j+1)%len(rings))*8+i) for j in range(len(rings) if annular else len(rings)-1) for i in range(8)]
    if not annular:faces+=[tuple(range(7,-1,-1)),tuple((len(rings)-1)*8+i for i in range(8))]
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(v,[],faces)
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
    o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);return finish(o,name,mat,.004,door_half)
def pin(name,x,y,z,r,length,mat,axis='Y',vertices=24):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=length,location=coord((x,y,z)));o=bpy.context.object
    direction={'Y':Vector((0,0,1)),'X':Vector((-1,0,0))}[axis];o.rotation_euler=direction.to_track_quat('Z','Y').to_euler();return finish(o,name,mat,min(.0015,r*.12))
outer=shape(2.30,.16,2.35,.055);inner=shape(2.15,.205,2.30,.035)
loft('Continuous folded cabinet shell',[(-.45,outer),(.41,outer),(.46,outer),(.46,inner),(.35,inner),(-.425,inner)],'CabinetCoat',annular=True)
box('Recessed rear closure',(-.435,1.26,0),(.024,2.14,2.17),'CabinetCoat',.012)
loft('Overhanging folded crown',[(.49,shape(2.34,2.35,2.40,.035)),(-.46,shape(2.34,2.35,2.40,.035))],'CabinetCoat')
box('Inset toe pedestal',(-.01,.095,0),(.77,.15,2.19),'CabinetRubber',.018)
box('Hemmed front toe lip',(.397,.09,0),(.032,.12,2.16),'CabinetCoat',.009)
box('Header recessed identity strip',(.472,2.327,0),(.025,.035,1.95),'CabinetAccent',.006)
for index,center in enumerate([-.535,.535]):
    loft('Deep closed door seal',[(.449,shape(1.066,.208,2.295,.025)),(.462,shape(1.066,.208,2.295,.025))],'CabinetRubber',center)
    loft('Pressed metal door '+str(index),[(.464,shape(1.064,.22,2.28,.028)),(.501,shape(1.064,.22,2.28,.028)),(.505,shape(.982,.261,2.239,.024)),(.494,shape(.95,.277,2.223,.021))],'CabinetDoorPaint',center,door_half=index)
    handle_z=-.16 if center<0 else .16
    for y in [1.08,1.35]:
        box('Handle captive mounting shoe',(.514,y,handle_z),(.040,.075,.10),'CabinetMetal',.016)
        for dz in [-.032,.032]:
            pin('Flush handle screw',.532,y,handle_z+dz,.006,.005,'CabinetMetal','X',16)
            box('Screw slot',(.535,y,handle_z+dz),(.0015,.002,.007),'CabinetRubber',.0005)
    curve=bpy.data.curves.new('Continuous rounded pull','CURVE');curve.dimensions='3D';curve.bevel_depth=.013;curve.bevel_resolution=4;curve.resolution_u=12
    spline=curve.splines.new('BEZIER');points=[(.53,1.08),(.591,1.105),(.604,1.16),(.604,1.28),(.591,1.325),(.53,1.35)];spline.bezier_points.add(len(points)-1)
    for p,(x,y) in zip(spline.bezier_points,points):p.co=coord((x,y,handle_z));p.handle_left_type='AUTO';p.handle_right_type='AUTO'
    o=bpy.data.objects.new('One piece tubular pull',curve);bpy.context.collection.objects.link(o);bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.convert(target='MESH');finish(bpy.context.object,o.name,'CabinetMetal',0)
    hinge_z=-1.067 if center<0 else 1.067
    for y in [.45,1.24,2.02]:
        box('Hinge fixed leaf',(.484,y,hinge_z),(.035,.18,.092),'CabinetCoat',.010)
        pin('Machined hinge barrel',.522,y,hinge_z,.022,.145,'CabinetMetal')
        for dy in [-.075,.075]:pin('Hinge end washer',.522,y+dy,hinge_z,.024,.009,'CabinetMetal')
        box('Hinge leaf fixing',(.506,y,hinge_z+(.055 if center<0 else -.055)),(.012,.08,.045),'CabinetMetal',.005)
# A key cylinder and short keeper are part of the closed cabinet, not an interaction system.
pin('Recessed lock bezel',.516,1.47,.16,.025,.019,'CabinetMetal','X')
pin('Lock core',.528,1.47,.16,.017,.007,'CabinetRubber','X')
box('Keyway',(.533,1.47,.16),(.002,.025,.003),'CabinetMetal',.0006)
# A properly UV-mapped plate carries the authored serial/load print.
box('Identification plate backing',(.510,1.96,.55),(.018,.13,.35),'CabinetRubber',.008)
vertices=[coord((.521,y,z)) for z,y in [(.387,1.905),(.713,1.905),(.713,2.015),(.387,2.015)]]
mesh=bpy.data.meshes.new('Identification print');mesh.from_pydata(vertices,[],[(0,1,2,3)]);uv=mesh.uv_layers.new(name='UV0')
for loop,point in zip(mesh.loops,[(0,0),(1,0),(1,1),(0,1)]):uv.data[loop.index].uv=point
o=bpy.data.objects.new('Printed identification inset',mesh);bpy.context.collection.objects.link(o);mesh.materials.append(bpy.data.materials.new('CabinetLabel'))
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'MarketStorageCabinet.blend'))
groups={}
for o in list(bpy.context.scene.objects):groups.setdefault(o.data.materials[0].name,[]).append(o)
for mat,parts in groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts:o.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]
    if len(parts)>1:bpy.ops.object.join()
    o=bpy.context.object;o.name='SM_StorageCabinet_'+mat
    mod=o.modifiers.new('Export triangles','TRIANGULATE');mod.keep_custom_normals=True;bpy.ops.object.modifier_apply(modifier=mod.name)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(OUT/'MarketStorageCabinet.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,mesh_smooth_type='FACE')
report={'triangles':sum(len(o.data.polygons) for o in bpy.context.scene.objects),'material_groups':len(groups),'placement':[-17.15,0,36.6],'editable_standalone_prop':True}
(SOURCE/'CabinetReport.json').write_text(json.dumps(report,indent=2));print('CABINET_REPORT',json.dumps(report))
