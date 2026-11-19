"""Three individually reusable desk props. Unity axes: x right, y up, z depth."""
import bpy,bmesh,math,json
from pathlib import Path
from mathutils import Vector
SOURCE=Path(__file__).resolve().parent;PROJECT=SOURCE.parents[2]
OUT=PROJECT/'Assets/Project/Art/Props/PackingDesk/Models';OUT.mkdir(parents=True,exist_ok=True)
bpy.context.preferences.filepaths.save_version=0
reports={}
def start():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version=0;bpy.context.preferences.filepaths.file_preview_type='NONE'
def coord(p):return Vector((-p[0],-p[2],p[1]))
def finish(o,name,mat,bevel=0):
    o.name=name;bpy.context.view_layer.objects.active=o
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True)
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    m=bpy.data.materials.get(mat) or bpy.data.materials.new(mat);o.data.materials.append(m)
    if bevel:
        mod=o.modifiers.new('Manufactured radius','BEVEL');mod.width=bevel;mod.segments=3;bpy.ops.object.modifier_apply(modifier=mod.name)
    if not o.data.uv_layers:
        uv=o.data.uv_layers.new(name='UV0')
        for f in o.data.polygons:
            axes=sorted(range(3),key=lambda a:abs(f.normal[a]))[:2]
            for loop in f.loop_indices:
                p=o.data.vertices[o.data.loops[loop].vertex_index].co;uv.data[loop].uv=(p[axes[0]]*12,p[axes[1]]*12)
    for f in o.data.polygons:f.use_smooth=True
    mod=o.modifiers.new('Weighted surface normals','WEIGHTED_NORMAL');mod.keep_sharp=True;bpy.ops.object.modifier_apply(modifier=mod.name)
    return o
def box(name,p,size,mat,r=.001):
    bpy.ops.mesh.primitive_cube_add(size=1,location=coord(p));o=bpy.context.object;o.scale=(size[0],size[2],size[1]);return finish(o,name,mat,r)
def lathe(name,profile,mat,n=64,printed=False):
    v=[coord((math.cos(i*2*math.pi/n)*r,y,math.sin(i*2*math.pi/n)*r)) for r,y in profile for i in range(n)]
    faces=[(j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i) for j in range(len(profile)-1) for i in range(n)]
    if profile[0] != profile[-1]:
        faces += [tuple(range(n-1,-1,-1)),tuple((len(profile)-1)*n+i for i in range(n))]
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(v,[],faces);mesh.update()
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
    uv=mesh.uv_layers.new(name='UV0')
    for f in mesh.polygons:
        for k,loop in enumerate(f.loop_indices):
            index=mesh.loops[loop].vertex_index;row=index//n;i=index%n
            u=i/n
            if len(f.vertices)==4 and max(a%n for a in f.vertices)-min(a%n for a in f.vertices)>n/2 and i==0:u=1
            uv.data[loop].uv=(u,profile[row][1]/.12 if printed else profile[row][1]*12)
    o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);return finish(o,name,mat)
def cylinder(name,a,b,r,mat,r2=None):
    pa,pb=coord(a),coord(b);d=pb-pa
    bpy.ops.mesh.primitive_cone_add(vertices=24,radius1=r,radius2=r if r2 is None else r2,depth=d.length,location=(pa+pb)/2)
    o=bpy.context.object;o.rotation_euler=d.to_track_quat('Z','Y').to_euler();return finish(o,name,mat,min(r*.15,.0006))
def export(name):
    objects=list(bpy.context.scene.objects)
    # Keep meaningful parts editable in each native source, merge by finish only in FBX.
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(name+'.blend')))
    groups={}
    for o in objects:groups.setdefault(o.data.materials[0].name,[]).append(o)
    for mat,parts in groups.items():
        bpy.ops.object.select_all(action='DESELECT')
        for o in parts:o.select_set(True)
        bpy.context.view_layer.objects.active=parts[0]
        if len(parts)>1:bpy.ops.object.join()
        o=bpy.context.object;o.name='SM_'+name+'_'+mat
        mod=o.modifiers.new('Export triangles','TRIANGULATE');mod.keep_custom_normals=True;bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,mesh_smooth_type='FACE')
    reports[name]={'triangles':sum(len(o.data.polygons) for o in bpy.context.scene.objects),'materials':len(groups),'bottom_center_origin':True}

start()
# Closed double wall, inset base, rolled mouth and a separate recessed liquid surface.
lathe('Double wall cup',[(.024,0),(.027,.002),(.028,.008),(.035,.100),(.0358,.111),(.0356,.115),(.0345,.117),(.033,.116),(.0325,.113),(.032,.102),(.025,.014),(.024,.012)],'CupCoating',printed=True)
lathe('Brushed mouth rim',[(.0347,.111),(.0358,.112),(.0361,.114),(.0357,.116),(.0348,.117),(.0334,.1168),(.0328,.1155),(.033,.113),(.0347,.111)],'DeskSteel')
lathe('Protective foot',[(.0235,0),(.026,0),(.027,.002),(.027,.005),(.026,.006),(.024,.005),(.0235,0)],'DeskRubber')
lathe('Recessed coffee meniscus',[(.0001,.076),(.0294,.076),(.0298,.077),(.030,.078),(.030,.075),(.0001,.075)],'DeskCoffee')
export('InsulatedCup')

start()
# Marker lies horizontally; bottom-center origin includes a small contact flat.
cylinder('Faceted barrel',(0,.007,-.048),(0,.007,.035),.0063,'DeskPolymer')
cylinder('Anodized tail',(0,.007,-.068),(0,.007,-.048),.0066,'DeskAccent')
cylinder('Tail inset',(0,.007,-.069),(0,.007,-.068),.0045,'DeskRubber')
cylinder('Grip',(0,.007,.024),(0,.007,.043),.0067,'DeskRubber')
for j in range(7):cylinder('Grip rib',(0,.007,.025+j*.0024),(0,.007,.026+j*.0024),.007,'DeskPolymer')
cylinder('Cap shoulder',(0,.007,.043),(0,.007,.047),.0074,'DeskSteel')
cylinder('Rounded cap',(0,.007,.047),(0,.007,.077),.0072,'DeskPolymer',.0064)
cylinder('Cap end',(0,.007,.077),(0,.007,.079),.0057,'DeskAccent',.0045)
box('Folded pocket clip',(0,.0155,.058),(.003,.0015,.032),'DeskSteel',.00065)
box('Clip return',(0,.0137,.042),(.003,.003,.003),'DeskSteel',.0006)
for z in [-.033,-.026,-.019]:box('Stamped index',(0,.01325,z),(.0025,.0003,.003),'DeskAccent',.0001)
export('UtilityMarker')

start()
# Subdivided, gently warped paper with thickness and a turned corner, rather than a flat box.
nx,nz=12,22;w,d=.115,.175;verts=[];faces=[]
for j in range(nz+1):
    for i in range(nx+1):
        x=(i/nx-.5)*w;z=(j/nz-.5)*d
        h=.001+math.sin(j/nz*math.pi)*.0012 + .011*max(0,(i/nx+j/nz-1.62)/.38)**2
        verts.append(coord((x,h,z)))
for j in range(nz):
    for i in range(nx):
        a=j*(nx+1)+i;faces.append((a,a+1,a+nx+2,a+nx+1))
mesh=bpy.data.meshes.new('Bent thermal paper');mesh.from_pydata(verts,[],faces)
uv=mesh.uv_layers.new(name='UV0')
for f in mesh.polygons:
    for loop in f.loop_indices:
        index=mesh.loops[loop].vertex_index;uv.data[loop].uv=(index%(nx+1)/nx,index//(nx+1)/nz)
o=bpy.data.objects.new('Printed dispatch slip',mesh);bpy.context.collection.objects.link(o)
finish(o,o.name,'DispatchPrint');mod=o.modifiers.new('Paper stock thickness','SOLIDIFY');mod.thickness=.0004;bpy.ops.object.modifier_apply(modifier=mod.name)
export('DispatchSlip')
(SOURCE/'PackingDeskReport.json').write_text(json.dumps(reports,indent=2));print('PACKING_DESK_REPORT',json.dumps(reports),flush=True)
