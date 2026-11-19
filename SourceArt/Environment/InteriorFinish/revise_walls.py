"""Pass 1: replace the perimeter's repeating 2m grid with composed technical bays."""
import bpy
import bmesh
import ast
import math
import json
from pathlib import Path
from mathutils import Vector

SOURCE=Path(__file__).resolve().parent
PROJECT=SOURCE.parents[2]
bpy.context.preferences.filepaths.save_version=0
bpy.context.preferences.filepaths.file_preview_type='NONE'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'InteriorFinish.blend'))
removed=0
for obj in list(bpy.context.scene.objects):
    if obj.type!='MESH': continue
    center=sum((obj.matrix_world@Vector(p) for p in obj.bound_box),Vector())/8
    x,z=-center.x,-center.y
    old=obj.name.startswith(('Interior wall recess','Interior wall upright','Interior perimeter cable trunk','Interior lower protection rail'))
    old=old or (obj.name.startswith('Attached formed cassette') and (abs(x)>17.8 or z<20.2 or z>63.8))
    if old or obj.get('WallRevision') or obj.get('WallRefinement'):
        bpy.data.objects.remove(obj,do_unlink=True); removed+=1
materials={n:bpy.data.materials.get(n) for n in ['CoatedMetal','BrushedSteel','Recess','WarmIndicator','InteriorFixture','ExitAccent']}
for n,color in [('WallCeramic',(.48,.46,.40,1)),('WallWarning',(.62,.34,.09,1))]:
    mat=bpy.data.materials.get(n) or bpy.data.materials.new(n); mat.diffuse_color=color; materials[n]=mat
groups={}
helpers={'coord','finish','box','face','strip','cassette'}
tree=ast.parse((SOURCE/'build_interior.py').read_text())
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in helpers],type_ignores=[]),'wall_helpers','exec'))
room=(-18,18,20,64)

def bay(side,lo,hi,kind):
    u,w=(lo+hi)/2,hi-lo
    # Quiet sheets use different proportions from deep mechanical features.
    strip('Wall revision recessed bay',room,side,u,4,-.035,w-.03,7.68,.05,'Recess')
    if kind=='quiet':
        cassette(room,side,u,2.0,-.10,w-.18,3.40,'CoatedMetal')
        cassette(room,side,u,5.72,-.10,w-.18,3.52,'WallCeramic')
        strip('Wall revision horizontal waist',room,side,u,4.0,-.15,w-.08,.15,.09,'BrushedSteel')
    elif kind=='service':
        cassette(room,side,u,2.45,-.11,w-.22,4.30,'WallCeramic')
        cassette(room,side,u,6.20,-.105,w-.22,2.45,'CoatedMetal')
        for du in [-w*.25,w*.25]:
            cassette(room,side,u+du,2.70,-.15,min(1.35,w*.35),2.25,'CoatedMetal')
            for y in [1.85,3.55]: strip('Wall revision captive hinge',room,side,u+du-.45,y,-.18,.07,.19,.025,'BrushedSteel',.004)
        strip('Wall revision service waist stripe',room,side,u,1.0,-.15,w-.30,.10,.015,'WallWarning',.003)
    elif kind=='vent':
        cassette(room,side,u,1.9,-.10,w-.22,3.2,'CoatedMetal')
        strip('Wall revision large intake recess',room,side,u,5.80,-.10,w-.32,2.35,.06,'Recess')
        for du in [-w/2+.18,w/2-.18]: strip('Wall revision intake frame',room,side,u+du,5.80,-.19,.12,2.55,.15,'BrushedSteel',.012)
        for y in [4.55,7.05]: strip('Wall revision intake cross frame',room,side,u,y,-.19,w-.25,.12,.15,'CoatedMetal',.014)
        for i in range(9):
            strip('Wall revision formed intake blade',room,side,u,4.77+i*.25,-.21,w-.60,.07,.15,'BrushedSteel',.004)
        for du in [-w*.22,w*.22]: strip('Wall revision intake mullion',room,side,u+du,5.80,-.25,.045,2.16,.04,'CoatedMetal',.004)
    elif kind=='portal':
        cassette(room,side,u,3.3,-.10,w-.20,6.0,'WallCeramic')
        strip('Wall revision loading door reveal',room,side,u,2.8,-.14,w-.65,5.10,.05,'Recess')
        for du in [-w*.23,w*.23]: cassette(room,side,u+du,2.75,-.18,w*.42,4.82,'CoatedMetal')
        strip('Wall revision portal warning sill',room,side,u,.43,-.215,w-.95,.11,.012,'WallWarning',.003)
        strip('Wall revision portal top transom',room,side,u,5.48,-.20,w-.55,.15,.10,'BrushedSteel',.01)
    # Strong columns are sparse, at bay boundaries rather than every two metres.
    for edge in [lo+.06,hi-.06]:
        strip('Wall revision structural column',room,side,edge,4,-.17,.12,7.65,.12,'CoatedMetal',.018)
        for y in [.65,4.0,7.3]: strip('Wall revision column collar',room,side,edge,y,-.22,.20,.22,.07,'BrushedSteel',.008)
    strip('Wall revision attached plinth',room,side,u,.24,-.13,w-.04,.22,.08,'CoatedMetal',.008)

plans={
 'S':[(-18,-12,'service'),(-12,-4,'quiet'),(-4,3,'quiet'),(3,9,'portal'),(9,14,'service'),(14,18,'quiet')],
 'N':[(-18,-9,'vent'),(-9,-6,'quiet'),(-6,-2,'quiet'),(-2,4,'service'),(4,12,'quiet'),(12,18,'vent')],
 'E':[(20,27,'quiet'),(27,35,'service'),(35,49,'vent'),(49,57,'service'),(57,64,'quiet')],
 'W':[(20,27,'service'),(27,34,'quiet'),(34,47,'vent'),(47,55,'quiet'),(55,64,'service')]
}
for side,entries in plans.items():
    for lo,hi,kind in entries: bay(side,lo,hi,kind)

# Fitted pipe/bus runs provide direction and terminate in actual attached cases.
for side,lo,hi in [('N',-16,16),('E',29,58),('W',25,59)]:
    for y in [6.8,7.15]:
        strip('Wall revision protected bus run',room,side,(lo+hi)/2,y,-.31,hi-lo,.10,.14,'Recess',.012)
        for u in range(math.ceil(lo),math.floor(hi),4): strip('Wall revision bus clamp',room,side,u,y,-.395,.08,.23,.025,'BrushedSteel',.004)
    for u in [lo,hi]: cassette(room,side,u,6.98,-.34,.80,.72,'CoatedMetal')

# Diagonal structural braces are confined to the high wall band.
def brace(side,u0,y0,u1,y1):
    a,b=Vector(coord(face(room,side,u0,y0,-.23))),Vector(coord(face(room,side,u1,y1,-.23)))
    bpy.ops.mesh.primitive_cube_add(size=1,location=(a+b)/2)
    obj=bpy.context.object; obj.name='Wall revision upper diagonal brace'
    obj.scale=(.12,.12,(b-a).length); obj.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler()
    finish(obj,'CoatedMetal',.012)
for side,segments in [('S',[(-17,-13),(-11,-7),(10,13)]),('N',[(-16,-12),(6,10)]),('E',[(22,25),(58,62)])]:
    for lo,hi in segments: brace(side,lo,7.5,hi,5.1)

for parts in groups.values():
    for obj in parts: obj['WallRevision']=1
for obj in bpy.context.scene.objects:
    if obj.get('WallRevision'):
        assert all(.10<=p.z<=7.90 for p in (obj.matrix_world@v.co for v in obj.data.vertices)), 'Wall revision outside vertical band'
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
report={'pass':1,'old_wall_objects_removed':removed,'bays':sum(len(v) for v in plans.values()),'material_groups':len(all_groups),
        'triangles':sum(len(o.data.polygons) for o in bpy.context.selected_objects),'plans':plans,'floor_geometry_generated':False}
(SOURCE/'WallRevisionReport.json').write_text(json.dumps(report,indent=2))
print('WALL_REVISION',json.dumps({k:v for k,v in report.items() if k!='plans'}))
