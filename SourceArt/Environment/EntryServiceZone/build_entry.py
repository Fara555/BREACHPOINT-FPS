"""Reuse accepted deck modules and build the next, bounded entry-zone wall kit."""
import bpy
import bmesh
import json
from pathlib import Path
from mathutils import Vector, Matrix

SOURCE=Path(__file__).resolve().parent
PROJECT=SOURCE.parents[2]
OUTPUT=PROJECT/'Assets/Project/Art/Environment/ServiceQuarter/EntryZone/Models'
OUTPUT.mkdir(parents=True,exist_ok=True)
bpy.context.preferences.filepaths.save_version=0
bpy.context.preferences.filepaths.file_preview_type='NONE'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE.parent/'EnergyCorner/EnergyCorner.blend'))
templates={}
for name,cx,cz in [('Deck_Service',5,32),('Deck_Solid',5,34),('Deck_Vent',11,34)]:
    copies=[]
    for obj in list(bpy.context.scene.objects):
        if obj.type!='MESH' or not obj.name.startswith('Deck '): continue
        points=[obj.matrix_world@Vector(p) for p in obj.bound_box]
        center=sum(points,Vector())/8
        ux,uz=4-center.x,36-center.y
        if abs(ux-cx)>.99 or abs(uz-cz)>.99: continue
        copy=bpy.data.objects.new(obj.name,obj.data.copy())
        for vertex in copy.data.vertices:
            vertex.co=obj.matrix_world@vertex.co-Vector((4-cx,36-cz,0))
        copies.append(copy)
    assert copies, f'No accepted components for {name}'
    templates[name]=copies
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
report={}

def export_group(name,objects,display_offset):
    collection=bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(collection)
    for obj in objects: collection.objects.link(obj)
    by_material={}
    for obj in objects: by_material.setdefault(obj.data.materials[0].name,[]).append(obj)
    joined=[]
    for mat,parts in by_material.items():
        bpy.ops.object.select_all(action='DESELECT')
        for obj in parts: obj.select_set(True)
        bpy.context.view_layer.objects.active=parts[0]
        if len(parts)>1: bpy.ops.object.join()
        obj=bpy.context.object
        obj.name=name+'_'+mat
        triangulate=obj.modifiers.new('Export triangles','TRIANGULATE')
        triangulate.keep_custom_normals=True
        bpy.ops.object.modifier_apply(modifier=triangulate.name)
        joined.append(obj)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in joined: obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(OUTPUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},
        axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,mesh_smooth_type='FACE')
    report[name]={'material_groups':len(joined),'triangles':sum(len(o.data.polygons) for o in joined)}
    for obj in joined: obj.location.x+=display_offset

for i,(name,objects) in enumerate(templates.items()): export_group(name,objects,i*3)

# The wall model uses scene coordinates; it is a skin for the existing 6x4x2m screen.
materials={name:bpy.data.materials.get(name) for name in ['CoatedMetal','BrushedSteel','Recess','WarmIndicator','DeckMetal']}
wall=[]
def finish(obj,mat,bevel):
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    obj.data.materials.clear(); obj.data.materials.append(materials[mat])
    if bevel:
        mod=obj.modifiers.new('Manufactured bevel','BEVEL'); mod.width=bevel; mod.segments=1 if bevel<=.004 else 3
        mod.harden_normals=True; bpy.ops.object.modifier_apply(modifier=mod.name)
    for face in obj.data.polygons: face.use_smooth=True
    mod=obj.modifiers.new('Face normals','WEIGHTED_NORMAL'); mod.keep_sharp=True
    bpy.ops.object.modifier_apply(modifier=mod.name)
    # Export world-positioned geometry with an identity object transform.
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    wall.append(obj)

def coord(p): return (-p[0],-p[2],p[1])
def face(side,u,y,d):
    if side=='S': return (u,y,28-d)
    if side=='N': return (u,y,30+d)
    if side=='E': return (10+d,y,u)
    return (4-d,y,u)
def box(name,side,u,y,d,w,h,t,mat,bevel=.008):
    bpy.ops.mesh.primitive_cube_add(size=1,location=coord(face(side,u,y,d)))
    obj=bpy.context.object; obj.name=name
    obj.scale=(w,t,h) if side in ['S','N'] else (t,w,h)
    finish(obj,mat,bevel)
def panel(side,u,w):
    height=3.55; clip=.13
    outline=[(-w/2+clip,-height/2),(w/2-clip,-height/2),(w/2,-height/2+clip),
             (w/2,height/2-clip),(w/2-clip,height/2),(-w/2+clip,height/2),
             (-w/2,height/2-clip),(-w/2,-height/2+clip)]
    mesh=bpy.data.meshes.new('Clipped entry wall panel')
    mesh.from_pydata([coord(face(side,u+x,2+y,d)) for d in [.04,.095] for x,y in outline],[],
        [tuple(range(7,-1,-1)),tuple(range(8,16))]+[(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)])
    uv=mesh.uv_layers.new(name='UV0')
    for loop in mesh.loops:
        x,y=outline[loop.vertex_index%8]; uv.data[loop.index].uv=(x/w+.5,y/height+.5)
    bm=bmesh.new(); bm.from_mesh(mesh); bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(mesh); bm.free()
    obj=bpy.data.objects.new('Entry formed wall cassette',mesh); bpy.context.collection.objects.link(obj)
    finish(obj,'CoatedMetal',.014)

for side in ['S','N','E','W']:
    centers=[5,7,9] if side in ['S','N'] else [29]
    for u in centers:
        box('Wall cassette backing',side,u,2,.02,1.98,3.91,.03,'Recess')
        panel(side,u,1.87)
        for du in [-.84,.84]:
            box('Wall upright profile',side,u+du,2,.11,.042,3.65,.04,'BrushedSteel')
            for y in [.45,3.55]:
                box('Wall flush socket',side,u+du,y,.135,.045,.075,.014,'Recess',.005)
        for y in [.18,3.87]:
            box('Folded wall cap',side,u,y,.11,1.92,.10,.09,'CoatedMetal',.018)
        for du in [-.48,-.24,0,.24,.48]:
            box('Wall lower service slot',side,u+du,.46,.126,.15,.018,.008,'Recess',.004)
# One closed maintenance hatch is attached to the side, not a standalone prop.
box('Entry service hatch gasket','E',29,1.9,.115,1.04,1.34,.04,'Recess')
box('Entry service hatch','E',29,1.9,.15,.96,1.26,.04,'CoatedMetal',.018)
for u in [28.60,29.40]:
    for y in [1.40,2.40]: box('Entry hatch latch','E',u,y,.18,.055,.10,.035,'BrushedSteel',.008)
box('Entry equipment indicator housing','S',9,2.85,.14,.42,.10,.06,'Recess')
box('Entry equipment warm indicator','S',9,2.85,.176,.29,.025,.008,'WarmIndicator',.003)
export_group('Entry_Wall',wall,0)
# Proper 2x1m closure modules fit around the sight screen without stretching bolt heads.
wall=[]
outline=[(-.88,-.48),(.88,-.48),(.98,-.38),(.98,.38),(.88,.48),(-.88,.48),(-.98,.38),(-.98,-.38)]
mesh=bpy.data.meshes.new('One metre deck closure')
mesh.from_pydata([coord((x,y,z)) for y in [-.004,.012] for x,z in outline],[],
    [tuple(range(7,-1,-1)),tuple(range(8,16))]+[(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)])
uv=mesh.uv_layers.new(name='UV0')
for loop in mesh.loops:
    x,z=outline[loop.vertex_index%8]; uv.data[loop.index].uv=(x/2+.5,z+.5)
bm=bmesh.new(); bm.from_mesh(mesh); bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(mesh); bm.free()
obj=bpy.data.objects.new('Deck one metre closure plate',mesh); bpy.context.collection.objects.link(obj)
finish(obj,'DeckMetal',.002)
for name,p,size,mat in [('Deck closure backing',(0,-.002,0),(1.99,.012,.99),'Recess')]+[
        ('Deck closure steel edge',(0,.014,z),(1.44,.004,.018),'BrushedSteel') for z in [-.4,.4]]:
    bpy.ops.mesh.primitive_cube_add(size=1,location=coord(p)); obj=bpy.context.object; obj.name=name
    obj.scale=(size[0],size[2],size[1]); finish(obj,mat,.001)
for x in [-.82,.82]:
    for z in [-.33,.33]:
        bpy.ops.mesh.primitive_cylinder_add(vertices=16,radius=.032,depth=.004,location=coord((x,.014,z)))
        finish(bpy.context.object,'Recess',.0005)
        bpy.ops.mesh.primitive_cylinder_add(vertices=6,radius=.018,depth=.003,location=coord((x,.017,z)))
        finish(bpy.context.object,'BrushedSteel',.0005)
export_group('Deck_Transition',wall,9)
bpy.ops.object.select_all(action='DESELECT')
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'EntryServiceZone.blend'))
(SOURCE/'ModelReport.json').write_text(json.dumps(report,indent=2))
print('ENTRY_KIT_REPORT',json.dumps(report))
