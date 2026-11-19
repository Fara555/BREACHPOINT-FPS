"""Spatial color hierarchy without changing geometry or any floor/shared asset."""
import bpy
import json
from pathlib import Path
from mathutils import Vector

SOURCE=Path(__file__).resolve().parent
PROJECT=SOURCE.parents[2]
bpy.context.preferences.filepaths.save_version=0
bpy.context.preferences.filepaths.file_preview_type='NONE'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'InteriorFinish.blend'))
colors={'WallWarm':(.52,.29,.06,1),'InteriorNeutral':(.55,.53,.44,1),
        'InteriorQuietFrame':(.08,.09,.095,1),'InteriorCeiling':(.035,.04,.043,1)}
for name,color in colors.items():
    mat=bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color=color
changes={name:0 for name in colors}
for obj in bpy.context.scene.objects:
    if obj.type!='MESH': continue
    if not obj.get('PaletteOriginalMaterial'): obj['PaletteOriginalMaterial']=obj.data.materials[0].name
    old=obj['PaletteOriginalMaterial']
    obj.data.materials[0]=bpy.data.materials[old]
    center=sum((obj.matrix_world@Vector(v) for v in obj.bound_box),Vector())/8
    x,y,z=-center.x,center.z,-center.y
    name=obj.name.split('.')[0]; target=None
    if obj.get('WallComposition'):
        if old=='BrushedSteel': target='InteriorQuietFrame'
        if name=='Service status bar' and old=='WarmIndicator':
            target='WallWarning'
        if name=='Attached formed cassette' and old=='WallCeramic' and y>4.2:
            target='WallDark'
        # Three adjacent leaves form ONE broad zone marker; no random colored checkerboard.
        if name=='Attached formed cassette' and old=='WallDark' and x>17 and 27<z<35 and 2<y<4.5:
            target='WallWarm'
        if name=='Attached formed cassette' and old=='WallCeramic' and z<21 and 3<x<9:
            target='WallWarm'
        if name=='Attached formed cassette' and old in {'WallDark','WallCeramic'} and z>63 and -6<x<-2 and y>4:
            target='WallWarm'
        if name=='Attached formed cassette' and old in {'WallDark','WallCeramic'} and x<-17 and 47<z<55 and 1.3<y<5.5:
            target='InteriorNeutral'
    else:
        if name=='Small attached indicator' and old=='WarmIndicator': target='WallWarning'
        if name.startswith('Ceiling metal cassette'): target='InteriorCeiling'
        if old=='BrushedSteel' and name.startswith(('Ceiling','Lamp carrier','Block upright','Block folded','Cover edge','Cover top')):
            target='InteriorQuietFrame'
        if name=='Attached formed cassette' and old=='CoatedMetal':
            # Existing west workshop becomes a readable light mass behind the energy module.
            if x<-10 and 33<z<48: target='InteriorNeutral'
            # Three existing cover silhouettes gain a consistent, readable equipment finish.
            if y<1: target='InteriorNeutral'
        if name=='Equipment top lid': target='InteriorNeutral'
    if target:
        obj.data.materials[0]=bpy.data.materials[target]; changes[target]=changes.get(target,0)+1

bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'InteriorFinish.blend'))
groups={}
for obj in list(bpy.context.scene.objects):
    if obj.type=='MESH': groups.setdefault(obj.data.materials[0].name,[]).append(obj)
for mat,parts in groups.items():
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
report={'material_assignments':changes,'material_groups':len(groups),
        'triangles':sum(len(o.data.polygons) for o in bpy.context.selected_objects),'geometry_modified':False,'floor_modified':False}
(SOURCE/'PaletteCompositionReport.json').write_text(json.dumps(report,indent=2)); print('PALETTE_REPORT',json.dumps(report),flush=True)
