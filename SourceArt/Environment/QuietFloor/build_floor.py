"""Quiet full-room industrial floor; legacy non-floor models copied without editing accepted assets."""
import bpy,bmesh,json
from pathlib import Path
from mathutils import Vector

SOURCE=Path(__file__).resolve().parent
PROJECT=SOURCE.parents[2]
OUTPUT=PROJECT/'Assets/Project/Art/Environment/ServiceQuarter/QuietFloor/Models'
OUTPUT.mkdir(parents=True,exist_ok=True)
bpy.context.preferences.filepaths.save_version=0
bpy.context.preferences.filepaths.file_preview_type='NONE'
report={}

def export(name):
    groups={}
    for obj in list(bpy.context.scene.objects):
        if obj.type=='MESH':groups.setdefault(obj.data.materials[0].name,[]).append(obj)
    for mat,parts in groups.items():
        bpy.ops.object.select_all(action='DESELECT')
        for obj in parts:obj.select_set(True)
        bpy.context.view_layer.objects.active=parts[0]
        if len(parts)>1:bpy.ops.object.join()
        obj=bpy.context.object;obj.name='SM_'+name+'_'+mat
        bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
        mod=obj.modifiers.new('Export triangles','TRIANGULATE');mod.keep_custom_normals=True;bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=str(OUTPUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},
        axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,mesh_smooth_type='FACE')
    report[name]={'groups':len(groups),'triangles':sum(len(o.data.polygons) for o in bpy.context.selected_objects)}

# These new body-only exports retain the original editable meshes, materials, UVs and normals.
# Original sources/FBX/prefabs remain read-only; only named floor families are omitted.
for name,path,prefixes in [
    ('CornerBody','EnergyCorner/EnergyCorner.blend',('Deck ',)),
    ('GalleryBody','ServiceGallery/ServiceGallery.blend',('Gallery Deck','Gallery narrow edge deck','Gallery edge steel insert','Gallery continuous toe strip'))]:
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE.parent/path))
    removed=0
    for obj in list(bpy.context.scene.objects):
        if obj.type!='MESH' or obj.name.startswith(prefixes):
            bpy.data.objects.remove(obj,do_unlink=True);removed+=1
    assert not any(o.data.materials[0].name=='DeckMetal' for o in bpy.context.scene.objects if o.type=='MESH'),name+' retained old deck'
    export(name);report[name]['omitted_floor_objects']=removed

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version=0
bpy.context.preferences.filepaths.file_preview_type='NONE'
for name,color in [('FloorField',(.14,.155,.16,1)),('FloorJoint',(.085,.095,.10,1)),('FloorAccess',(.16,.17,.17,1))]:
    mat=bpy.data.materials.new(name);mat.diffuse_color=color

def height(z):
    if z<32 or z>56:return 0
    if z<40:return (z-32)*.1875
    if z<=48:return 1.5
    return (56-z)*.1875

def slab(name,rect,material='FloorField',gallery=False,base=.006,top=.014,bevel=.0015):
    x0,x1,z0,z1=rect
    def h(z):return height(z) if gallery else 0
    vertices=[(-x,-z,h(z)+dy) for dy in [base,top] for x,z in [(x0,z0),(x1,z0),(x1,z1),(x0,z1)]]
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],[(3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)])
    uv=mesh.uv_layers.new(name='UV0')
    # Consistent physical texel scale. No per-tile albedo/scratch pattern or corner bolts.
    for loop in mesh.loops:
        p=mesh.vertices[loop.vertex_index].co;uv.data[loop.index].uv=(-p.x/2,-p.y/2)
    bm=bmesh.new();bm.from_mesh(mesh)
    if bevel:bmesh.ops.bevel(bm,geom=list(bm.edges),offset=bevel,segments=1,affect='EDGES',clamp_overlap=True)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free();mesh.update()
    weighted=[Vector() for _ in mesh.vertices]
    for face in mesh.polygons:
        face.use_smooth=True
        for vertex in face.vertices:weighted[vertex]+=face.normal*face.area
    mesh.normals_split_custom_set_from_vertices([n.normalized() for n in weighted])
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);mesh.materials.append(bpy.data.materials[material])

def subtract(rect,hole):
    a,b,c,d=rect;x0,x1,z0,z1=hole
    x0,x1=max(a,x0),min(b,x1);z0,z1=max(c,z0),min(d,z1)
    if x0>=x1 or z0>=z1:return [rect]
    return [r for r in [(a,x0,c,d),(x1,b,c,d),(x0,x1,c,z0),(x0,x1,z1,d)] if r[1]-r[0]>.015 and r[3]-r[2]>.015]

obstacles=[(-18,-10.92,33.92,47.08),(-6.10,4.10,35.90,46.10),(-9.08,1.08,25.92,30.08),
           (3.92,10.08,27.92,30.08),(-1.08,11.08,55.92,58.08),(13,18,32,56)]
access=[(-9.3,-8.1,39.1,40.9),(15.4,16.6,28.1,29.9)]
slab('Continuous quiet joint bed',(-18,18,20,64),'FloorJoint',base=.001,top=.003,bevel=0)
panels=0
for x in range(-18,18,4):
    for z in range(20,64,6):
        parts=[(x,min(x+4,18),z,min(z+6,64))]
        for hole in obstacles+access:parts=[part for rect in parts for part in subtract(rect,hole)]
        for a,b,c,d in parts:
            if min(b-a,d-c)<.025:continue
            slab('Large calm manufactured floor sheet',(a+.020,b-.020,c+.020,d-.020));panels+=1

# Two flush access covers at actual service edges, rather than a hatch on every tile.
for index,(a,b,c,d) in enumerate(access):
    slab('Flush service cover '+str(index),(a+.020,b-.020,c+.020,d-.020),'FloorAccess')
    for x in [a+.16,b-.16]:
        slab('Dark flush quarter-turn latch',(x-.045,x+.045,c+.14,c+.17),'FloorJoint',base=.0142,top=.015,bevel=.0003)

# Same calm surface on both existing ramps and platform; only six large sloped sheets.
for z0,z1 in [(32,36),(36,40),(40,44),(44,48),(48,52),(52,56)]:
    slab('Gallery continuous joint bed',(13,18,z0,z1),'FloorJoint',True,.001,.003,0)
    inner=13.220 if 40<=z0<48 else 13.020
    slab('Gallery large manufactured sheet',(inner,17.980,z0+.020,z1-.020),gallery=True);panels+=1

for obj in bpy.context.scene.objects:
    for v in obj.data.vertices:
        x,y,z=-v.co.x,v.co.z,-v.co.y
        expected=height(z) if x>=12.999 and 32<=z<=56 else 0
        assert -18.001<=x<=18.001 and 19.999<=z<=64.001 and -.001<=y-expected<=.016,'Floor outside surface envelope'
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'QuietFloor.blend'))
export('QuietFloor')
report['design']={'large_panels':panels,'flush_access_covers':2,'tile_bolts':0,'tile_grooves':0,'seam_width_m':.040,
                  'surface_height_m':.014,'walk_collision_changed':False,'old_art_sources_modified':False}
(SOURCE/'FloorReport.json').write_text(json.dumps(report,indent=2));print('QUIET_FLOOR_REPORT',json.dumps(report),flush=True)
