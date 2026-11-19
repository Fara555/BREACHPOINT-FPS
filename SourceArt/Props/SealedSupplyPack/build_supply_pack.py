"""Standalone heat-sealed foil ration pouch, modeled at real scale with a bottom-center origin."""
import bpy,bmesh,math,json
from pathlib import Path
SOURCE=Path(__file__).resolve().parent;PROJECT=SOURCE.parents[2]
OUT=PROJECT/'Assets/Project/Art/Props/SealedSupplyPack/Models';OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version=0;bpy.context.preferences.filepaths.file_preview_type='NONE'
W,D=.38,.26;R=.025
outline=[]
def edge(a,b,n):
    for i in range(n):outline.append((a[0]+(b[0]-a[0])*i/n,a[1]+(b[1]-a[1])*i/n))
def corner(cx,cz,start,n=8):
    for i in range(n):
        a=math.radians(start+i*90/n);outline.append((cx+math.cos(a)*R,cz+math.sin(a)*R))
edge((W/2-R,D/2),(-W/2+R,D/2),20);corner(-W/2+R,D/2-R,90)
edge((-W/2,D/2-R),(-W/2,-D/2+R),14);corner(-W/2+R,-D/2+R,180)
edge((-W/2+R,-D/2),(W/2-R,-D/2),20);corner(W/2-R,-D/2+R,270)
edge((W/2,-D/2+R),(W/2,D/2-R),14);corner(W/2-R,D/2-R,0)
n=len(outline);vertices=[];faces=[];top_flags=[]
radii=[1,.985,.95,.88,.77,.61,.43,.24,.08]
for top in [True,False]:
    for ring,r in enumerate(radii):
        for i,(x,z) in enumerate(outline):
            # Flat crimp flange, an inflated center and restrained non-repeating seam folds.
            h=(.010 if top else .007) if r>=.95 else (.010+.062*pow(max(0,1-(r/.95)**2),.72) if top else .007-.007*pow(max(0,1-(r/.95)**2),.6))
            fold=math.sin(i*1.71+r*23)*.0016*math.exp(-((r-.86)/.12)**2)
            if top:h+=fold
            notch=.006*math.exp(-((z+.078)/.009)**2) if x>W*.46 and r>.95 else 0
            px=x*r-notch;pz=z*r
            vertices.append((-px,-pz,h))
    base=(0 if top else len(radii)*n)
    for ring in range(len(radii)-1):
        for i in range(n):
            face=(base+ring*n+i,base+ring*n+(i+1)%n,base+(ring+1)*n+(i+1)%n,base+(ring+1)*n+i)
            faces.append(face if top else tuple(reversed(face)));top_flags.append(top)
    faces.append(tuple(base+(len(radii)-1)*n+i for i in range(n)));top_flags.append(top)
for i in range(n):
    faces.append((i,(i+1)%n,len(radii)*n+(i+1)%n,len(radii)*n+i));top_flags.append(False)
mesh=bpy.data.meshes.new('Heat sealed pouch surface');mesh.from_pydata(vertices,[],faces)
bottom=min(v.co.z for v in mesh.vertices)
for vertex in mesh.vertices:vertex.co.z-=bottom
uv=mesh.uv_layers.new(name='UV0')
for face,top in zip(mesh.polygons,top_flags):
    for loop in face.loop_indices:
        p=mesh.vertices[mesh.loops[loop].vertex_index].co;x,z=-p.x,-p.y
        uv.data[loop].uv=(.5-z/D,(x/W+.5)*.5+(.5 if top else 0))
bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free();mesh.update()
for face in mesh.polygons:face.use_smooth=True
material=bpy.data.materials.new('SupplyRationFoil');mesh.materials.append(material)
obj=bpy.data.objects.new('SM_SealedSupplyPack',mesh);bpy.context.collection.objects.link(obj)
bpy.context.view_layer.objects.active=obj;obj.select_set(True)
# The flange is closed and every edge belongs to two faces, including the tear-notch outline.
bm=bmesh.new();bm.from_mesh(mesh)
assert all(edge.is_manifold for edge in bm.edges),'Pouch must be a closed manifold'
assert all(face.calc_area()>1e-10 for face in bm.faces),'Degenerate pouch face'
bm.free()
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'SealedSupplyPack.blend'))
mod=obj.modifiers.new('Export triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=mod.name)
bpy.ops.export_scene.fbx(filepath=str(OUT/'SealedSupplyPack.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,mesh_smooth_type='FACE')
report={'triangles':len(obj.data.polygons),'width_m':W,'depth_m':D,'height_m':max(v.co.z for v in obj.data.vertices),
        'bottom_center_origin':True,'closed_manifold':True,'material_slots':1,'separate_reusable_asset':True}
(SOURCE/'SupplyPackReport.json').write_text(json.dumps(report,indent=2));print('SUPPLY_PACK_REPORT',json.dumps(report),flush=True)
