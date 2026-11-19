"""Closed utility bulkhead replacing the storage rack within its existing collision envelope."""
import bpy,bmesh,math,json
from pathlib import Path
from mathutils import Vector
SOURCE=Path(__file__).resolve().parent;PROJECT=SOURCE.parents[2]
OUT=PROJECT/'Assets/Project/Art/Props/MarketServiceDoor/Models';OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.context.preferences.filepaths.save_version=0;bpy.context.preferences.filepaths.file_preview_type='NONE'
def coord(p):return Vector((-p[0],-p[2],p[1]))
def finish(o,name,mat,bevel=.008):
    o.name=name;bpy.context.view_layer.objects.active=o;bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    o.data.materials.append(bpy.data.materials.get(mat) or bpy.data.materials.new(mat))
    if bevel:
        mod=o.modifiers.new('Formed edge radius','BEVEL');mod.width=bevel;mod.segments=3;bpy.ops.object.modifier_apply(modifier=mod.name)
    for f in o.data.polygons:f.use_smooth=True
    mod=o.modifiers.new('Broad face normals','WEIGHTED_NORMAL');mod.keep_sharp=True;bpy.ops.object.modifier_apply(modifier=mod.name)
    for layer in list(o.data.uv_layers):o.data.uv_layers.remove(layer)
    uv=o.data.uv_layers.new(name='UV0')
    for f in o.data.polygons:
        axes=sorted(range(3),key=lambda a:abs(f.normal[a]))[:2]
        for loop in f.loop_indices:
            p=o.data.vertices[o.data.loops[loop].vertex_index].co;uv.data[loop].uv=(p[axes[0]],p[axes[1]])
    return o
def box(name,p,size,mat,r=.008):
    bpy.ops.mesh.primitive_cube_add(size=1,location=coord(p));o=bpy.context.object;o.scale=(size[0],size[2],size[1]);return finish(o,name,mat,r)
def outline(w,bottom,top,c):
    return [(-w/2+c,bottom),(w/2-c,bottom),(w/2,bottom+c),(w/2,top-c),(w/2-c,top),(-w/2+c,top),(-w/2,top-c),(-w/2,bottom+c)]
def shaped(name,rings,mat):
    v=[coord((depth,y,z)) for depth,shape in rings for z,y in shape]
    faces=[(j*8+i,j*8+(i+1)%8,(j+1)*8+(i+1)%8,(j+1)*8+i) for j in range(len(rings)-1) for i in range(8)]
    faces+=[tuple(range(7,-1,-1)),tuple((len(rings)-1)*8+i for i in range(8))]
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(v,[],faces)
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
    o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);return finish(o,name,mat)
# Closed annular ring: no coplanar bars or disk cap over the door opening.
outer=outline(2.30,.016,2.91,.11);inner=outline(1.66,.045,2.70,.07)
rings=[(-.55,outer),(.10,outer),(.15,outline(2.20,.026,2.87,.10)),(.15,inner),(.075,inner),(-.55,inner)]
v=[coord((depth,y,z)) for depth,shape in rings for z,y in shape]
faces=[(j*8+i,j*8+(i+1)%8,((j+1)%len(rings))*8+(i+1)%8,((j+1)%len(rings))*8+i) for j in range(len(rings)) for i in range(8)]
mesh=bpy.data.meshes.new('Continuous folded bulkhead surround');mesh.from_pydata(v,[],faces)
bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
o=bpy.data.objects.new('Continuous bulkhead frame',mesh);bpy.context.collection.objects.link(o);finish(o,o.name,'LiningTrim',.012)
shaped('Continuous closed door seal backing',[(-.005,outline(1.70,.026,2.73,.075)),(.020,outline(1.70,.026,2.73,.075))],'LiningTrim')
shaped('Recessed door leaf',[(.025,outline(1.61,.048,2.67,.075)),(.095,outline(1.61,.048,2.67,.075))],'LiningGraphite')
shaped('Replaceable enamel face',[(.099,outline(1.43,.47,2.48,.055)),(.123,outline(1.43,.47,2.48,.055))],'LiningCeramic')
shaped('Lower impact shoe',[(.099,outline(1.43,.10,.42,.04)),(.129,outline(1.43,.10,.42,.04))],'LiningTrim')
box('Header identity inlay',(.166,2.785,0),(.018,.045,.52),'LiningWarm',.005)
box('Latch recessed mount',(.143,1.31,-.52),(.034,.34,.16),'LiningTrim',.016)
for z in [-.57,-.40]:box('Lever stand off',(.189,1.32,z),(.065,.048,.048),'LiningGraphite',.014)
box('Rounded latch lever',(.224,1.32,-.485),(.036,.038,.23),'LiningWarm',.015)
for y in [.43,2.24]:
    box('Captive hinge casting',(.145,y,.77),(.07,.27,.10),'LiningTrim',.018)
    bpy.ops.mesh.primitive_cylinder_add(vertices=32,radius=.035,depth=.22,location=coord((.19,y,.77)));finish(bpy.context.object,'Vertical hinge pin','LiningGraphite',.004)
box('Threshold contact plate',(.10,.033,0),(.16,.026,1.62),'LiningGraphite',.005)
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'MarketServiceDoor.blend'))
groups={}
for o in list(bpy.context.scene.objects):groups.setdefault(o.data.materials[0].name,[]).append(o)
for mat,parts in groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts:o.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]
    if len(parts)>1:bpy.ops.object.join()
    o=bpy.context.object;o.name='SM_ServiceDoor_'+mat
    mod=o.modifiers.new('Export triangles','TRIANGULATE');mod.keep_custom_normals=True;bpy.ops.object.modifier_apply(modifier=mod.name)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(OUT/'MarketServiceDoor.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,mesh_smooth_type='FACE')
report={'triangles':sum(len(o.data.polygons) for o in bpy.context.scene.objects),'materials':len(groups),'placement':[-17.45,0,40.5],'closed_door':True,'frame_proud_of_lining_m':.024,'enamel_recess_m':.003,'collision_fitted_to_recess':True}
(SOURCE/'ServiceDoorReport.json').write_text(json.dumps(report,indent=2));print('SERVICE_DOOR_REPORT',json.dumps(report))
