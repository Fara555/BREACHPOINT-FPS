"""Finish remaining walls, cover props and enclosure. Intentionally generates no floor."""
import bpy
import bmesh
import math
import json
from pathlib import Path

SOURCE=Path(__file__).resolve().parent
PROJECT=SOURCE.parents[2]
OUTPUT=PROJECT/'Assets/Project/Art/Environment/ServiceQuarter/InteriorFinish/Models'
OUTPUT.mkdir(parents=True,exist_ok=True)
bpy.context.preferences.filepaths.save_version=0
bpy.context.preferences.filepaths.file_preview_type='NONE'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE.parent/'EnergyCorner/EnergyCorner.blend'))
materials={n:bpy.data.materials.get(n) for n in ['CoatedMetal','BrushedSteel','Recess','WarmIndicator']}
for n,c in [('InteriorFixture',(.65,.8,.95,1)),('ExitAccent',(.25,.85,.55,1))]:
    materials[n]=bpy.data.materials.new(n); materials[n].diffuse_color=c
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
groups={}
def coord(p): return (-p[0],-p[2],p[1])
def finish(obj,mat,bevel=.008):
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    obj.data.materials.clear(); obj.data.materials.append(materials[mat])
    if bevel:
        mod=obj.modifiers.new('Manufactured edges','BEVEL'); mod.width=bevel; mod.segments=1 if bevel<=.004 else 3
        mod.harden_normals=True; bpy.ops.object.modifier_apply(modifier=mod.name)
    for p in obj.data.polygons: p.use_smooth=True
    mod=obj.modifiers.new('Weighted face normals','WEIGHTED_NORMAL'); mod.keep_sharp=True
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    groups.setdefault(mat,[]).append(obj)
def box(name,p,size,mat,bevel=.008):
    bpy.ops.mesh.primitive_cube_add(size=1,location=coord(p)); obj=bpy.context.object; obj.name=name
    obj.scale=(size[0],size[2],size[1]); finish(obj,mat,bevel)
def face(bounds,side,u,y,d):
    x0,x1,z0,z1=bounds
    if side=='S': return (u,y,z0-d)
    if side=='N': return (u,y,z1+d)
    if side=='W': return (x0-d,y,u)
    return (x1+d,y,u)
def strip(name,bounds,side,u,y,d,w,h,t,mat,bevel=.008):
    box(name,face(bounds,side,u,y,d),(w,h,t) if side in ['S','N'] else (t,h,w),mat,bevel)
def cassette(bounds,side,u,y,d,w,h,mat='CoatedMetal'):
    clip=min(.10,w*.08,h*.15); a,b=w/2,h/2
    outline=[(-a+clip,-b),(a-clip,-b),(a,-b+clip),(a,b-clip),(a-clip,b),(-a+clip,b),(-a,b-clip),(-a,-b+clip)]
    mesh=bpy.data.meshes.new('Formed cassette')
    mesh.from_pydata([coord(face(bounds,side,u+x,y+v,d+offset)) for offset in [-.018,.018] for x,v in outline],[],
        [tuple(range(7,-1,-1)),tuple(range(8,16))]+[(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)])
    uv=mesh.uv_layers.new(name='UV0')
    for loop in mesh.loops:
        x,v=outline[loop.vertex_index%8]; uv.data[loop.index].uv=(x/w+.5,v/h+.5)
    bm=bmesh.new(); bm.from_mesh(mesh); bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(mesh); bm.free()
    obj=bpy.data.objects.new('Attached formed cassette',mesh); bpy.context.collection.objects.link(obj); finish(obj,mat,.008)
def cells(lo,hi):
    n=math.ceil((hi-lo)/2); width=(hi-lo)/n
    return [(lo+(i+.5)*width,width) for i in range(n)]
def clad(bounds,height,sides):
    for side in sides:
        lo,hi=bounds[:2] if side in ['S','N'] else bounds[2:]
        for i,(u,w) in enumerate(cells(lo,hi)):
            strip('Block panel gasket',bounds,side,u,height/2,.035,w-.035,height-.12,.035,'Recess')
            cassette(bounds,side,u,height/2,.075,w-.12,height-.30)
            for du in [-w/2+.11,w/2-.11]:
                strip('Block upright profile',bounds,side,u+du,height/2,.12,.035,height-.28,.04,'BrushedSteel')
            for y in [.18,height-.14]: strip('Block folded cap',bounds,side,u,y,.12,w-.06,.12,.085,'CoatedMetal',.014)
            if i%2==0:
                strip('Service panel gasket',bounds,side,u,1.8,.115,min(1.22,w-.3),1.45,.03,'Recess')
                cassette(bounds,side,u,1.8,.15,min(1.12,w-.4),1.35)
                for y in [1.3,2.3]: strip('Service hatch hinge',bounds,side,u-.42,y,.19,.08,.15,.04,'BrushedSteel')
                strip('Small attached indicator',bounds,side,u,2.25,.178,.22,.022,.01,'WarmIndicator',.003)
    x0,x1,z0,z1=bounds
    box('Attached block roof cap',((x0+x1)/2,height+.012,(z0+z1)/2),(x1-x0+.04,.024,z1-z0+.04),'CoatedMetal',.01)

clad((-18,-11,34,47),5,['S','E','N'])
clad((-9,1,26,30),4,['S','E','N','W'])
clad((-1,11,56,58),4,['S','E','N','W'])
# Interior boundary faces point into the room.
room=(-18,18,20,64)
for side in ['S','N','E','W']:
    lo,hi=room[:2] if side in ['S','N'] else room[2:]
    for u,w in cells(lo,hi):
        for y in [2,6]:
            strip('Interior wall recess',room,side,u,y,-.045,w-.025,3.92,.04,'Recess')
            cassette(room,side,u,y,-.10,w-.09,3.78)
        strip('Interior wall upright',room,side,u-w/2+.04,4,-.14,.055,7.70,.055,'BrushedSteel')
        strip('Interior perimeter cable trunk',room,side,u,6.65,-.18,w-.035,.22,.20,'Recess',.012)
        strip('Interior lower protection rail',room,side,u,.25,-.15,w-.03,.20,.09,'CoatedMetal')

# Real roof planes, beams and attached light housings close the previously open scene.
for ix in range(6):
    for z,length in [(24,7.98),(32,7.98),(40,7.98),(48,7.98),(56,7.98),(62,3.98)]:
        box('Ceiling metal cassette',(-15+ix*6,8.12,z),(5.98,.16,length),'CoatedMetal',.015)
for z in [20.1,28,36,44,52,60,63.9]:
    box('Ceiling transverse beam',(0,7.90,z),(35.9,.25,.18),'BrushedSteel',.014)
box('Ceiling continuous dark backing',(0,8.23,42),(36.4,.04,44.4),'Recess',.003)
for x in [-17.95,17.95]: box('Wall roof sealed longitudinal joint',(x,7.98,42),(.40,.28,44.2),'Recess',.012)
for z in [20,64]: box('Wall roof sealed transverse joint',(0,7.98,z),(36.3,.28,.40),'Recess',.012)
for x in [-10,10]:
    box('Ceiling service carrier',(x,7.91,42),(.24,.24,43.8),'Recess',.018)
    for z in [28,42,56]:
        for dx in [-1.6,1.6]: box('Lamp carrier hanger',(x+dx,7.90,z),(.08,.36,.08),'BrushedSteel')
        box('Attached ceiling light housing',(x,7.68,z),(4.1,.20,1.20),'Recess',.02)
        box('Ceiling light frame',(x,7.56,z),(3.94,.055,1.06),'BrushedSteel',.012)
        box('Ceiling diffuser',(x,7.523,z),(3.70,.018,.83),'InteriorFixture',.006)

# Equipment housings fit the existing three low-cover blocks.
for cx,cz,width in [(1,31.6,4),(8.5,43.6,3),(-5.5,51.6,3)]:
    bounds=(cx-width/2,cx+width/2,cz-.6,cz+.6)
    for side in ['S','N','E','W']:
        lo,hi=bounds[:2] if side in ['S','N'] else bounds[2:]
        for u,w in cells(lo,hi):
            strip('Cover casing gasket',bounds,side,u,.56,.025,w-.025,1.02,.025,'Recess')
            cassette(bounds,side,u,.56,.06,w-.08,.94)
            for du in [-w/2+.10,w/2-.10]: strip('Cover edge clamp',bounds,side,u+du,.56,.095,.055,.85,.045,'BrushedSteel')
            for du in [-.24,0,.24]: strip('Cover cooling slot',bounds,side,u+du,.36,.083,.12,.02,.006,'Recess',.002)
    box('Equipment top lid',(cx,1.107,cz),(width+.045,.014,1.245),'CoatedMetal',.008)
    for dx in [-width/2+.16,width/2-.16]: box('Cover top latch',(cx+dx,1.12,cz),(.055,.012,.72),'BrushedSteel',.003)

# A decorative closed exit portal preserves the original exit marker/physical boundary.
portal=(-5.7,-2.3,63.65,64)
strip('Exit portal black reveal',portal,'S',-4,2.24,0,3.65,4.4,.10,'Recess')
for x in [-5.61,-2.39]: strip('Exit portal frame',portal,'S',x,2.24,.07,.13,4.4,.15,'BrushedSteel',.02)
strip('Exit portal lintel',portal,'S',-4,4.4,.07,3.35,.13,.15,'BrushedSteel',.02)
for x in [-4.78,-3.22]: cassette(portal,'S',x,2.15,.08,1.46,4.05)
strip('Exit header backing',portal,'S',-4,4.90,.10,3.0,.70,.08,'Recess',.008)
for x in [-5.35,-2.65]: strip('Exit header support bracket',portal,'S',x,4.50,.12,.06,.48,.06,'BrushedSteel',.004)
strip('Exit mounted status strip',portal,'S',-4,4.65,.165,2.6,.045,.025,'ExitAccent',.004)

for obj in bpy.context.scene.objects:
    if obj.type=='MESH':
        assert all(-18.301<=-p.x<=18.301 and .039<=p.z<=8.251 and 19.699<=-p.y<=64.301
                   for p in (obj.matrix_world@v.co for v in obj.data.vertices)), 'Out of interior scope: '+obj.name
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'InteriorFinish.blend'))
for mat,parts in groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts: obj.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]
    if len(parts)>1: bpy.ops.object.join()
    obj=bpy.context.object; obj.name='SM_Interior_'+mat
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    mod=obj.modifiers.new('Export triangles','TRIANGULATE'); mod.keep_custom_normals=True; bpy.ops.object.modifier_apply(modifier=mod.name)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(OUTPUT/'InteriorFinish.fbx'),use_selection=True,object_types={'MESH'},
    axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,mesh_smooth_type='FACE')
report={'material_groups':len(groups),'triangles':sum(len(o.data.polygons) for o in bpy.context.selected_objects),'floor_geometry_generated':False}
(SOURCE/'ModelReport.json').write_text(json.dumps(report,indent=2))
print('INTERIOR_REPORT',json.dumps(report))
