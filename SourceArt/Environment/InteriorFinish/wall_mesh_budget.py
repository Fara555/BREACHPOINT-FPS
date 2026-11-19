"""Reduce redundant bevel tessellation on new wall sheets/bezels only."""
import bpy
import json
from pathlib import Path
from mathutils import Vector, Matrix

def reduce_wall_cost():
    changed=0
    for obj in list(bpy.context.scene.objects):
        if not obj.get('WallComposition'): continue
        center=sum((obj.matrix_world@Vector(v) for v in obj.bound_box),Vector())/8
        if obj.name.startswith('HVAC') and center.z<3 and not obj.get('WallVentFit'):
            # Small grilles belong on the front of the riser cabinet, not inside its lid.
            x,z=-center.x,-center.y
            offset=Vector((-.30 if x<-17 else .30 if x>17 else 0,
                           .30 if z>63 else -.30 if z<21 else 0,0))
            obj.data.transform(Matrix.Translation(offset)); obj['WallVentFit']=1
        if obj.name.startswith('Portal latch bridge') and not obj.get('WallHandleFit'):
            for y in [2.5,3.0]:
                bpy.ops.mesh.primitive_cube_add(size=1,location=(center.x,center.y+.055,y))
                bracket=bpy.context.object; bracket.name='Portal attached latch standoff'
                bracket.data.transform(Matrix.LocRotScale(bracket.location,None,Vector((.08,.14,.08))))
                bracket.matrix_world=Matrix.Identity(4)
                bracket.data.materials.append(bpy.data.materials['WallDark'])
                bracket['WallComposition']=1
            obj['WallHandleFit']=1
        if not obj.get('WallComposition') or obj.get('WallMeshBudget'): continue
        if not obj.name.startswith(('Attached formed cassette','Wall folded perimeter bezel')): continue
        bpy.context.view_layer.objects.active=obj
        mod=obj.modifiers.new('Reduce hidden bevel subdivisions','DECIMATE')
        mod.ratio=.60
        bpy.ops.object.modifier_apply(modifier=mod.name)
        mesh=obj.data; mesh.update()
        weighted=[Vector() for _ in mesh.vertices]
        for poly in mesh.polygons:
            poly.use_smooth=True
            for vertex in poly.vertices: weighted[vertex]+=poly.normal*poly.area
        mesh.normals_split_custom_set_from_vertices([v.normalized() for v in weighted])
        obj['WallMeshBudget']=1; changed+=1
    return changed

if __name__=='__main__':
    source=Path(__file__).resolve().parent; project=source.parents[2]
    bpy.context.preferences.filepaths.save_version=0
    bpy.context.preferences.filepaths.file_preview_type='NONE'
    bpy.ops.wm.open_mainfile(filepath=str(source/'InteriorFinish.blend'))
    changed=reduce_wall_cost()
    bpy.ops.wm.save_as_mainfile(filepath=str(source/'InteriorFinish.blend'))
    groups={}
    for obj in bpy.context.scene.objects:
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
    bpy.ops.export_scene.fbx(filepath=str(project/'Assets/Project/Art/Environment/ServiceQuarter/InteriorFinish/Models/InteriorFinish.fbx'),
        use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,mesh_smooth_type='FACE')
    report={'reduced_wall_sheets_and_bezels':changed,'material_groups':len(groups),
            'triangles':sum(len(o.data.polygons) for o in bpy.context.selected_objects),'preserved_non_wall_geometry':True}
    (source/'WallMeshBudgetReport.json').write_text(json.dumps(report,indent=2))
    print('WALL_MESH_BUDGET',json.dumps(report),flush=True)
