"""Continue the accepted flat deck around the existing energy block without changing gameplay."""
import bpy
import bmesh
import math
import json
from pathlib import Path
from mathutils import Matrix

SOURCE=Path(__file__).resolve().parent
PROJECT=SOURCE.parents[2]
OUTPUT=PROJECT/'Assets/Project/Art/Environment/ServiceQuarter/ServiceLoop/Models'
OUTPUT.mkdir(parents=True,exist_ok=True)
bpy.context.preferences.filepaths.save_version=0
bpy.context.preferences.filepaths.file_preview_type='NONE'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE.parent/'EntryServiceZone/EntryServiceZone.blend'))
templates={name:[(o.name,o.data.copy()) for o in bpy.data.collections[name].objects if o.type=='MESH']
           for name in ['Deck_Solid','Deck_Service','Deck_Transition']}
materials={n:bpy.data.materials.get(n) for n in ['DeckMetal','BrushedSteel','Recess']}
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
groups={}
placements=[]

def weighted(obj):
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
    mod=obj.modifiers.new('Weighted normals','WEIGHTED_NORMAL'); mod.keep_sharp=True
    bpy.ops.object.modifier_apply(modifier=mod.name)

def place(model,x,z,rotate=False):
    placements.append({'model':model,'x':x,'z':z,'rotate':rotate})
    for name,source in templates[model]:
        mesh=source.copy()
        if rotate:
            mesh.transform(Matrix.Rotation(-math.pi/2,4,'Z'))
            mesh.normals_split_custom_set([(0,0,0)]*len(mesh.loops))
        mesh.transform(Matrix.Translation((-x,-z,0)))
        obj=bpy.data.objects.new('Loop '+name,mesh); bpy.context.collection.objects.link(obj)
        if rotate: weighted(obj)
        groups.setdefault(mesh.materials[0].name,[]).append(obj)

def full(x,z,i,j):
    # Keep removable hatches clear of the loading equipment footprint.
    service=(i+j)%7==0 and not(z==44 and x in [7,9])
    place('Deck_Service' if service else 'Deck_Solid',x,z)

# West corridor, fitting exactly between workshop and energy-module footprints.
for row in range(9):
    z=33+row*2
    for col,x in enumerate([-10,-8]): full(x,z,col,row)
    place('Deck_Transition',-6.5,z,True)
# Southern and northern links, ending precisely at the energy walls.
for z0 in [32,46]:
    for row in range(2):
        for col in range(5): full(-5+col*2,z0+1+row*2,col,row+1)
# Eastern passage joins the accepted corner at Z=39 and the gallery skin at X=13.
for row in range(5):
    for col in range(4): full(5+col*2,40+row*2,col,row+2)
    place('Deck_Transition',12.5,40+row*2,True)
for col in range(4): place('Deck_Transition',5+col*2,49.5)

# A true 1x1m closure fills the last corner without scaling the accepted fasteners.
def coord(p): return (-p[0],-p[2],p[1])
def finish(obj,mat,bevel=.001):
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    obj.data.materials.clear(); obj.data.materials.append(materials[mat])
    if bevel:
        mod=obj.modifiers.new('Small manufactured edge','BEVEL'); mod.width=bevel; mod.segments=1
        mod.harden_normals=True; bpy.ops.object.modifier_apply(modifier=mod.name)
    for p in obj.data.polygons: p.use_smooth=True
    weighted(obj)
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    groups.setdefault(mat,[]).append(obj)

x,z=12.5,49.5
outline=[(-.42,-.48),(.42,-.48),(.48,-.42),(.48,.42),(.42,.48),(-.42,.48),(-.48,.42),(-.48,-.42)]
mesh=bpy.data.meshes.new('One metre square closure')
mesh.from_pydata([coord((x+u,y,z+v)) for y in [-.004,.012] for u,v in outline],[],
    [tuple(range(7,-1,-1)),tuple(range(8,16))]+[(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)])
uv=mesh.uv_layers.new(name='UV0')
for loop in mesh.loops:
    u,v=outline[loop.vertex_index%8]; uv.data[loop.index].uv=(u+.5,v+.5)
bm=bmesh.new(); bm.from_mesh(mesh); bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(mesh); bm.free()
obj=bpy.data.objects.new('Loop one metre closure plate',mesh); bpy.context.collection.objects.link(obj); finish(obj,'DeckMetal',.002)
for name,p,size,mat in [('Closure seam backing',(x,-.002,z),(.99,.012,.99),'Recess')]+[
    ('Closure steel insert',(x,.014,z+dz),(.66,.004,.014),'BrushedSteel') for dz in [-.37,.37]]:
    bpy.ops.mesh.primitive_cube_add(size=1,location=coord(p)); obj=bpy.context.object; obj.name=name
    obj.scale=(size[0],size[2],size[1]); finish(obj,mat)
for dx in [-.35,.35]:
    for dz in [-.32,.32]:
        bpy.ops.mesh.primitive_cylinder_add(vertices=12,radius=.032,depth=.004,location=coord((x+dx,.014,z+dz)))
        finish(bpy.context.object,'Recess',.0005)
        bpy.ops.mesh.primitive_cylinder_add(vertices=6,radius=.018,depth=.003,location=coord((x+dx,.017,z+dz)))
        finish(bpy.context.object,'BrushedSteel',.0005)
placements.append({'model':'Square_Closure','x':x,'z':z,'rotate':False})

def allowed(x,y,z):
    return -.021<=y<=.035 and ((-11.001<=x<=-5.999 and 31.999<=z<=50.001) or
        (-6.001<=x<=4.001 and 31.999<=z<=36.001) or (-6.001<=x<=4.001 and 45.999<=z<=50.001) or
        (3.999<=x<=13.001 and 38.999<=z<=50.001))
for obj in bpy.context.scene.objects:
    if obj.type=='MESH':
        assert all(allowed(-p.x,p.z,-p.y) for p in (obj.matrix_world@v.co for v in obj.data.vertices)), 'Out-of-scope deck '+obj.name
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'ServiceLoop.blend'))
for mat,parts in groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts: obj.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]
    if len(parts)>1: bpy.ops.object.join()
    obj=bpy.context.object; obj.name='SM_ServiceLoop_'+mat
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    mod=obj.modifiers.new('Export triangles','TRIANGULATE'); mod.keep_custom_normals=True
    bpy.ops.object.modifier_apply(modifier=mod.name)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(OUTPUT/'ServiceLoop.fbx'),use_selection=True,object_types={'MESH'},
    axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,mesh_smooth_type='FACE')
report={'material_groups':len(groups),'triangles':sum(len(o.data.polygons) for o in bpy.context.selected_objects),
        'full_cells':58,'closure_cells':18,'square_cells':1,'horizontal_area_m2':269,'placements':placements}
(SOURCE/'ModelReport.json').write_text(json.dumps(report,indent=2))
print('SERVICE_LOOP_REPORT',json.dumps({k:v for k,v in report.items() if k!='placements'}))
