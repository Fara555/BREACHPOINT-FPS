"""Two composed portal surrounds, preserving the accepted room hierarchy and floor."""
import bpy
import bmesh
import json
from pathlib import Path
from mathutils import Vector

SOURCE=Path(__file__).resolve().parent
PROJECT=SOURCE.parents[2]
bpy.context.preferences.filepaths.save_version=0
bpy.context.preferences.filepaths.file_preview_type='NONE'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'InteriorFinish.blend'))
for obj in list(bpy.context.scene.objects):
    # The integrated header strip replaces the old separate underline, avoiding doubled neon lines.
    if obj.get('LandmarkFinish') or obj.name.split('.')[0]=='Exit mounted status strip':
        bpy.data.objects.remove(obj,do_unlink=True)

def box(name,position,size,material,bevel=.012):
    x,y,z=position; w,h,d=size
    verts=[(-x+sx*w/2,-z+sz*d/2,y+sy*h/2) for sx,sz,sy in
           [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(verts,[],[(3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)])
    uv=mesh.uv_layers.new(name='UV0')
    for face in mesh.polygons:
        for j,loop in enumerate(face.loop_indices): uv.data[loop].uv=[(0,0),(1,0),(1,1),(0,1)][j]
    bm=bmesh.new(); bm.from_mesh(mesh)
    bmesh.ops.bevel(bm,geom=list(bm.edges),offset=bevel,segments=3 if bevel>.008 else 1,affect='EDGES',clamp_overlap=True)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(mesh); bm.free(); mesh.update()
    weighted=[Vector() for _ in mesh.vertices]
    for face in mesh.polygons:
        face.use_smooth=True
        for vertex in face.vertices: weighted[vertex]+=face.normal*face.area
    mesh.normals_split_custom_set_from_vertices([v.normalized() for v in weighted])
    obj=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(obj)
    mesh.materials.append(bpy.data.materials[material]); obj['LandmarkFinish']=1

# North EXIT: manufactured deep jambs, a header and two protected continuous guide strips.
for x in [-5.95,-2.05]:
    box('EXIT structural surround jamb',(x,2.4,63.49),(.22,4.50,.30),'InteriorQuietFrame',.035)
    box('EXIT lower protective shoe',(x,.42,63.29),(.27,.55,.12),'WallDark',.02)
    box('EXIT guide strip recessed housing',(x,3.48,63.315),(.11,1.10,.065),'Recess',.012)
    box('EXIT protected guide diffuser',(x,3.48,63.276),(.027,.86,.012),'ExitAccent',.004)
    for y in [1.12,4.28]: box('EXIT jamb attachment collar',(x,y,63.31),(.28,.14,.095),'InteriorQuietFrame',.012)
box('EXIT folded lintel canopy',(-4,4.35,63.43),(4.18,.20,.45),'InteriorQuietFrame',.04)
box('EXIT lintel integrated guide diffuser',(-4,4.37,63.192),(3.65,.035,.022),'ExitAccent',.007)

# South loading portal: broader lintel and side legs, without additional light sources/sign clutter.
for x in [3.18,8.82]:
    box('Loading portal box-section jamb',(x,3.34,20.36),(.19,5.32,.30),'InteriorQuietFrame',.03)
    box('Loading portal jamb warm shoulder',(x,5.73,20.522),(.20,.40,.035),'WallWarm',.012)
box('Loading portal folded canopy',(6,6.03,20.37),(6.18,.24,.50),'InteriorQuietFrame',.04)
box('Loading canopy warm identity band',(6,6.04,20.632),(5.65,.11,.025),'WallWarm',.007)

added=[o for o in bpy.context.scene.objects if o.get('LandmarkFinish')]
for obj in added:
    assert all(.10<=p.z<=7.9 for p in (obj.matrix_world@v.co for v in obj.data.vertices)),obj.name
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'InteriorFinish.blend'))
groups={}
for obj in list(bpy.context.scene.objects):
    if obj.type=='MESH': groups.setdefault(obj.data.materials[0].name,[]).append(obj)
for material,parts in groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts: obj.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]
    if len(parts)>1: bpy.ops.object.join()
    obj=bpy.context.object; obj.name='SM_Interior_'+material
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    mod=obj.modifiers.new('Export triangles','TRIANGULATE'); mod.keep_custom_normals=True; bpy.ops.object.modifier_apply(modifier=mod.name)
bpy.ops.object.select_all(action='SELECT')
triangles=sum(len(o.data.polygons) for o in bpy.context.selected_objects)
assert triangles<500000,'Landmark detail exceeded the existing model budget'
bpy.ops.export_scene.fbx(filepath=str(PROJECT/'Assets/Project/Art/Environment/ServiceQuarter/InteriorFinish/Models/InteriorFinish.fbx'),
    use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,mesh_smooth_type='FACE')
report={'attached_landmark_parts':len(added),'material_groups':len(groups),'triangles':triangles,'floor_modified':False,'new_lights':0}
(SOURCE/'LandmarkFinishReport.json').write_text(json.dumps(report,indent=2)); print('LANDMARK_REPORT',json.dumps(report),flush=True)
