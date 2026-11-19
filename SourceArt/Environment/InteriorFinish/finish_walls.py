"""Pass 2: refine functional details/material separation after reviewing the composed walls."""
import bpy
import bmesh
import ast
import json
from pathlib import Path
from mathutils import Vector,Matrix
SOURCE=Path(__file__).resolve().parent
PROJECT=SOURCE.parents[2]
bpy.context.preferences.filepaths.save_version=0
bpy.context.preferences.filepaths.file_preview_type='NONE'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'InteriorFinish.blend'))
for obj in list(bpy.context.scene.objects):
    if obj.get('WallRefinement'): bpy.data.objects.remove(obj,do_unlink=True)
dark=bpy.data.materials.get('WallDark') or bpy.data.materials.new('WallDark'); dark.diffuse_color=(.12,.17,.20,1)
materials={n:bpy.data.materials.get(n) for n in ['CoatedMetal','BrushedSteel','Recess','WallCeramic','WallWarning','WarmIndicator']}
materials['CoatedMetal']=dark
groups={}
tree=ast.parse((SOURCE/'build_interior.py').read_text())
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in {'coord','finish','box','face','strip','cassette'}],type_ignores=[]),'wall_helpers','exec'))
room=(-18,18,20,64)
for obj in bpy.context.scene.objects:
    if not obj.get('WallRevision'): continue
    if obj.data.materials[0].name=='CoatedMetal': obj.data.materials[0]=dark
    # Route the two bus runs above, rather than across, the intake opening.
    if obj.name.startswith(('Wall revision protected bus run','Wall revision bus clamp','Attached formed cassette')):
        center=sum((obj.matrix_world@Vector(v) for v in obj.bound_box),Vector())/8
        y=center.z
        if obj.name.startswith(('Wall revision protected bus run','Wall revision bus clamp')):
            shift=.5 if abs(y-6.8)<.02 else .47 if abs(y-7.15)<.02 else 0
        else:
            # Only the small high-level termination cases, not the large wall cassettes.
            dims=obj.dimensions
            shift=.47 if abs(y-6.98)<.02 and dims.z<.8 else 0
        if shift: obj.data.transform(Matrix.Translation((0,0,shift)))

for z in [38,42,46]:
    strip('Gallery maintenance recess',room,'E',z,3.0,-.13,1.58,1.18,.035,'Recess')
    cassette(room,'E',z,3.0,-.17,1.44,1.04,'WallCeramic')
    for y in [2.66,3.34]: strip('Gallery hatch captive hinge',room,'E',z-.57,y,-.20,.08,.15,.025,'BrushedSteel',.004)
    strip('Gallery hatch handle recess',room,'E',z+.5,3.0,-.195,.065,.26,.012,'Recess',.004)
    strip('Gallery hatch warning tab',room,'E',z,2.35,-.13,.42,.055,.012,'WallWarning',.003)

# A single electrical bypass unit gives the north service end a distinct lower-wall anchor.
strip('North bypass cabinet gasket',room,'N',16.3,2.15,-.13,1.92,3.15,.045,'Recess')
cassette(room,'N',16.3,2.15,-.18,1.78,3.0,'WallCeramic')
for y in [1.2,1.45,1.7,1.95]: strip('North cabinet cooling slit',room,'N',16.3,y,-.204,1.12,.04,.012,'Recess',.004)
strip('North bypass status bed',room,'N',16.3,3.16,-.207,.52,.13,.025,'Recess')
strip('North bypass warm status',room,'N',16.3,3.16,-.224,.26,.025,.008,'WarmIndicator',.002)
strip('North cabinet attached supply trunk',room,'N',17.50,5.35,-.31,.09,3.9,.09,'Recess')
strip('North supply trunk lower elbow',room,'N',16.95,3.4,-.31,1.18,.09,.09,'Recess')
for y in [4.0,5.2,6.4]: strip('North supply clamp',room,'N',17.5,y,-.365,.18,.09,.025,'BrushedSteel',.004)
for parts in groups.values():
    for obj in parts: obj['WallRefinement']=1
for obj in bpy.context.scene.objects:
    if obj.get('WallRevision') or obj.get('WallRefinement'):
        assert all(.10<=p.z<=7.90 for p in (obj.matrix_world@v.co for v in obj.data.vertices)), 'Wall detail outside vertical band'
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'InteriorFinish.blend'))
all_groups={}
for obj in list(bpy.context.scene.objects):
    if obj.type=='MESH': all_groups.setdefault(obj.data.materials[0].name,[]).append(obj)
for mat,parts in all_groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts: obj.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]
    if len(parts)>1: bpy.ops.object.join()
    obj=bpy.context.object; obj.name='SM_Interior_'+mat
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    mod=obj.modifiers.new('Export triangles','TRIANGULATE'); mod.keep_custom_normals=True; bpy.ops.object.modifier_apply(modifier=mod.name)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(PROJECT/'Assets/Project/Art/Environment/ServiceQuarter/InteriorFinish/Models/InteriorFinish.fbx'),
    use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,mesh_smooth_type='FACE')
report={'pass':2,'material_groups':len(all_groups),'triangles':sum(len(o.data.polygons) for o in bpy.context.selected_objects),'floor_geometry_generated':False}
(SOURCE/'WallFinishReport.json').write_text(json.dumps(report,indent=2)); print('WALL_FINISH_REPORT',json.dumps(report))
