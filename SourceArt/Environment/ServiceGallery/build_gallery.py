"""Visual-only gallery skin using approved deck meshes; no collision/nav export."""
import bpy
import bmesh
import json
from pathlib import Path
from mathutils import Vector

SOURCE=Path(__file__).resolve().parent
PROJECT=SOURCE.parents[2]
OUTPUT=PROJECT/'Assets/Project/Art/Environment/ServiceQuarter/ServiceGallery/Models'
OUTPUT.mkdir(parents=True,exist_ok=True)
bpy.context.preferences.filepaths.save_version=0
bpy.context.preferences.filepaths.file_preview_type='NONE'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE.parent/'EntryServiceZone/EntryServiceZone.blend'))
templates={}
for name in ['Deck_Solid','Deck_Service']:
    # Collections are displayed apart in the source; their mesh data is still centred locally.
    templates[name]=[(o.name,o.data.copy()) for o in bpy.data.collections[name].objects if o.type=='MESH']
materials={n:bpy.data.materials.get(n) for n in ['DeckMetal','CoatedMetal','BrushedSteel','Recess','WarmIndicator']}
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
groups={}
box_faces=[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]

def height(z):
    if z<32 or z>56: return 0
    if z<40: return (z-32)*.1875
    if z<=48: return 1.5
    return (56-z)*.1875

def coord(p): return (-p[0],-p[2],p[1])
def normals(obj):
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
    mod=obj.modifiers.new('Weighted normals after slope projection','WEIGHTED_NORMAL'); mod.keep_sharp=True
    bpy.ops.object.modifier_apply(modifier=mod.name)

def place(name,cx,cz):
    for original,source in templates[name]:
        mesh=source.copy()
        for v in mesh.vertices:
            x,z=cx-v.co.x,cz-v.co.y
            v.co=(-x,-z,v.co.z+height(z))
        mesh.normals_split_custom_set([(0,0,0)]*len(mesh.loops))
        obj=bpy.data.objects.new('Gallery '+original,mesh); bpy.context.collection.objects.link(obj)
        normals(obj)
        groups.setdefault(mesh.materials[0].name,[]).append(obj)

def finish(obj,mat,bevel=.006):
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    obj.data.materials.clear(); obj.data.materials.append(materials[mat])
    if bevel:
        mod=obj.modifiers.new('Manufactured edge radius','BEVEL'); mod.width=bevel; mod.segments=1 if bevel<=.003 else 3
        mod.harden_normals=True; bpy.ops.object.modifier_apply(modifier=mod.name)
    for p in obj.data.polygons: p.use_smooth=True
    normals(obj)
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    groups.setdefault(mat,[]).append(obj)
    return obj

def box(name,p,size,mat,bevel=.006):
    bpy.ops.mesh.primitive_cube_add(size=1,location=coord(p)); obj=bpy.context.object; obj.name=name
    obj.scale=(size[0],size[2],size[1]); return finish(obj,mat,bevel)

def prism(name,vertices,faces,mat,bevel=.002):
    mesh=bpy.data.meshes.new(name); mesh.from_pydata([coord(v) for v in vertices],[],faces)
    uv=mesh.uv_layers.new(name='UV0')
    for polygon in mesh.polygons:
        for index in polygon.loop_indices:
            p=vertices[mesh.loops[index].vertex_index]
            uv.data[index].uv=(p[0]/2,p[2]/2) if abs(polygon.normal.z)>.5 else (p[2]/2,p[1]/2)
    bm=bmesh.new(); bm.from_mesh(mesh); bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(mesh); bm.free()
    obj=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(obj); return finish(obj,mat,bevel)

# Floor: two 2m columns and a true 0.8m edge column fit the existing clear width.
for row in range(12):
    z=33+row*2
    for col,x in enumerate([14.2,16.2]):
        model='Deck_Service' if 40<z<48 and (row+col)%3==0 else 'Deck_Solid'
        place(model,x,z)
    x=17.6
    outline=[(-.30,-.98),(.30,-.98),(.38,-.90),(.38,.90),(.30,.98),(-.30,.98),(-.38,.90),(-.38,-.90)]
    vertices=[(x+u,height(z+v)+y,z+v) for y in [-.004,.012] for u,v in outline]
    faces=[tuple(range(7,-1,-1)),tuple(range(8,16))]+[(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)]
    obj=prism('Gallery narrow edge deck',vertices,faces,'DeckMetal')
    for offset in [-.30,.30]:
        # Sloped narrow-profile strips are projected rather than left horizontal.
        for segment in range(4):
            sz=z-.75+segment*.5
            vertices=[(x+offset+dx,height(sz+dz)+dy,sz+dz) for dy in [.013,.017]
                      for dx,dz in [(-.007,-.235),(.007,-.235),(.007,.235),(-.007,.235)]]
            prism('Gallery edge steel insert',vertices,box_faces,'BrushedSteel',.001)

# Ground aprons meet both existing ramp feet. The south one joins the accepted entry deck.
for z in [31,57]:
    for x in [13,15,17]: place('Deck_Solid',x,z)

faces=[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]
for row in range(12):
    z0,z1=32+row*2+.015,34+row*2-.015
    # Attached skin of the original wedge/platform, below the existing walking surface.
    vertices=[(x,y,z) for x in [12.97,13.01] for z,y in [(z0,-.12),(z1,-.12),(z1,height(z1)-.025),(z0,height(z0)-.025)]]
    prism('Gallery fitted lower side cassette',vertices,faces,'CoatedMetal',.004)
    if 40<z0<48:
        z=(z0+z1)/2
        box('Gallery lower side latch',(12.947,.65,z),(.025,.09,.05),'BrushedSteel',.004)
    # A narrow metal toe strip covers the 0.2m ledge beside the clear walking width.
    vertices=[(x,height(z)+y,z) for y in [.006,.018] for x,z in [(13.01,z0),(13.19,z0),(13.19,z1),(13.01,z1)]]
    prism('Gallery continuous toe strip',vertices,faces,'BrushedSteel',.002)

# The existing solid parapet stays its original height and cover silhouette.
for z in [41,43,45,47]:
    for x in [12.98,13.22]:
        box('Gallery parapet gasket',(x,2.05,z),(.025,1.06,1.98),'Recess')
        box('Gallery parapet formed panel',(x+(-.018 if x<13.1 else .018),2.05,z),(.03,.96,1.88),'CoatedMetal',.012)
        for dz in [-.82,.82]:
            box('Gallery parapet fastener',(x+(-.038 if x<13.1 else .038),2.40,z+dz),(.012,.035,.035),'BrushedSteel',.002)
    box('Gallery attached top profile',(13.1,2.615,z),(.24,.03,1.98),'BrushedSteel',.006)
    if z in [41,47]:
        box('Gallery guide indicator housing',(12.942,2.32,z),(.025,.09,.38),'Recess')
        box('Gallery attached warm guide indicator',(12.924,2.32,z),(.008,.025,.25),'WarmIndicator',.002)

for obj in bpy.context.scene.objects:
    if obj.type!='MESH': continue
    for v in obj.data.vertices:
        p=obj.matrix_world@v.co
        x,y,z=-p.x,p.z,-p.y
        assert 11.99<=x<=18.01 and -.21<=y<=2.67 and 29.99<=z<=58.01, f'Stray gallery geometry {obj.name}: {(x,y,z)}'
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'ServiceGallery.blend'))
for mat,parts in groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts: obj.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]
    if len(parts)>1: bpy.ops.object.join()
    obj=bpy.context.object; obj.name='SM_Gallery_'+mat
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    triangulate=obj.modifiers.new('Export triangles','TRIANGULATE'); triangulate.keep_custom_normals=True
    bpy.ops.object.modifier_apply(modifier=triangulate.name)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(OUTPUT/'ServiceGallery.fbx'),use_selection=True,object_types={'MESH'},
    axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,mesh_smooth_type='FACE')
report={'material_groups':len(groups),'triangles':sum(len(o.data.polygons) for o in bpy.context.selected_objects)}
(SOURCE/'ModelReport.json').write_text(json.dumps(report,indent=2))
print('GALLERY_REPORT',json.dumps(report))
