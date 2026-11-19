"""Incrementally add the same roof seals as build_interior.py, retaining editable source."""
import bpy
import json
from mathutils import Matrix, Vector
from pathlib import Path
SOURCE=Path(__file__).resolve().parent
PROJECT=SOURCE.parents[2]
bpy.context.preferences.filepaths.save_version=0
bpy.context.preferences.filepaths.file_preview_type='NONE'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'InteriorFinish.blend'))
parts=[('Ceiling continuous dark backing',(0,8.23,42),(36.4,.04,44.4))]
parts += [('Wall roof sealed longitudinal joint '+str(x),(x,7.98,42),(.40,.28,44.2)) for x in [-17.95,17.95]]
parts += [('Wall roof sealed transverse joint '+str(z),(0,7.98,z),(36.3,.28,.40)) for z in [20,64]]
parts += [('Exit header backing',(-4,4.90,63.55),(3.0,.70,.08))]
parts += [('Exit header support bracket '+str(x),(x,4.50,63.53),(.06,.48,.06)) for x in [-5.35,-2.65]]
for name,p,size in parts:
    family=next((prefix for prefix in ['Wall roof sealed longitudinal joint','Wall roof sealed transverse joint','Exit header support bracket'] if name.startswith(prefix)),name)
    expected=Vector((-p[0],-p[2],p[1]))
    if any(o.type=='MESH' and o.name.startswith(family) and
           ((sum((o.matrix_world@Vector(v) for v in o.bound_box),Vector())/8)-expected).length<.001
           for o in bpy.context.scene.objects): continue
    bpy.ops.mesh.primitive_cube_add(size=1,location=(-p[0],-p[2],p[1])); obj=bpy.context.object; obj.name=name
    obj.scale=(size[0],size[2],size[1]); bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    obj.data.materials.append(bpy.data.materials['BrushedSteel' if 'bracket' in name else 'Recess'])
    mod=obj.modifiers.new('Roof seal radius','BEVEL'); mod.width=.003 if 'backing' in name else .012; mod.segments=1
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
status=bpy.data.objects.get('Exit mounted status strip')
if status:
    center=sum((status.matrix_world@Vector(v) for v in status.bound_box),Vector())/8
    if abs(center.y+63.57)<.001: status.data.transform(Matrix.Translation((0,.085,0)))
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'InteriorFinish.blend'))
groups={}
for obj in list(bpy.context.scene.objects):
    if obj.type=='MESH': groups.setdefault(obj.data.materials[0].name,[]).append(obj)
for mat,objects in groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    if len(objects)>1: bpy.ops.object.join()
    obj=bpy.context.object; obj.name='SM_Interior_'+mat
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    mod=obj.modifiers.new('Export triangles','TRIANGULATE'); mod.keep_custom_normals=True; bpy.ops.object.modifier_apply(modifier=mod.name)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(PROJECT/'Assets/Project/Art/Environment/ServiceQuarter/InteriorFinish/Models/InteriorFinish.fbx'),
    use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,mesh_smooth_type='FACE')
report={'material_groups':len(groups),'triangles':sum(len(o.data.polygons) for o in bpy.context.selected_objects),'floor_geometry_generated':False}
(SOURCE/'ModelReport.json').write_text(json.dumps(report,indent=2)); print('SEALED_INTERIOR_REPORT',json.dumps(report))
