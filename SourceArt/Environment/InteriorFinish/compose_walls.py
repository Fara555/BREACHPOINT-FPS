"""Replace flat perimeter sheets with framed, recessed industrial assemblies. No floor."""
import bpy
import bmesh
import ast
import math
import json
import sys
from pathlib import Path
from mathutils import Vector, Matrix

SOURCE = Path(__file__).resolve().parent
PROJECT = SOURCE.parents[2]
bpy.context.preferences.filepaths.save_version = 0
bpy.context.preferences.filepaths.file_preview_type = 'NONE'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'InteriorFinish.blend'))
removed = 0
for obj in list(bpy.context.scene.objects):
    if obj.get('WallRevision') or obj.get('WallRefinement') or obj.get('WallComposition'):
        bpy.data.objects.remove(obj, do_unlink=True)
        removed += 1
materials = {n:bpy.data.materials.get(n) for n in
             ['CoatedMetal','BrushedSteel','Recess','WallDark','WallCeramic','WallWarning','WarmIndicator']}
groups = {}
tree = ast.parse((SOURCE/'build_interior.py').read_text())
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n, ast.FunctionDef) and n.name in
    {'coord','finish','box','face','strip','cassette'}], type_ignores=[]), 'wall_helpers', 'exec'))
room = (-18,18,20,64)

def finish(obj,mat,bevel=.008):
    # Direct mesh authoring avoids thousands of scene-wide modifier/operator updates.
    transform=Matrix.LocRotScale(obj.location,obj.rotation_euler.to_quaternion(),obj.scale)
    obj.data.transform(transform); obj.matrix_world=Matrix.Identity(4)
    obj.data.materials.clear(); obj.data.materials.append(materials[mat])
    bm=bmesh.new(); bm.from_mesh(obj.data)
    if bevel:
        bmesh.ops.bevel(bm,geom=list(bm.edges),offset=bevel,segments=1 if bevel<=.004 else 3,
                        affect='EDGES',clamp_overlap=True)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(obj.data); bm.free()
    mesh=obj.data; mesh.update()
    weighted=[Vector() for _ in mesh.vertices]
    for poly in mesh.polygons:
        poly.use_smooth=True
        for vertex in poly.vertices: weighted[vertex]+=poly.normal*poly.area
    mesh.normals_split_custom_set_from_vertices([v.normalized() for v in weighted])
    groups.setdefault(mat,[]).append(obj)

def tube(name, side, u0,y0,d0, u1,y1,d1, radius, mat='WallDark', sides=16):
    a,b = Vector(coord(face(room,side,u0,y0,d0))), Vector(coord(face(room,side,u1,y1,d1)))
    bpy.ops.mesh.primitive_cylinder_add(vertices=sides, radius=radius, depth=(b-a).length, location=(a+b)/2)
    obj=bpy.context.object; obj.name=name
    obj.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler()
    finish(obj,mat,min(.003,radius*.1))

def plate(side,u,y,w,h,mat='WallDark',d=-.19,fasteners=True):
    # A deep chamfered bezel surrounds an inset sheet; no coplanar overlays.
    cassette(room,side,u,y,d+.095,w+.08,h+.08,'Recess')
    outer=[(-w/2+.09,-h/2),(w/2-.09,-h/2),(w/2,-h/2+.09),(w/2,h/2-.09),
           (w/2-.09,h/2),(-w/2+.09,h/2),(-w/2,h/2-.09),(-w/2,-h/2+.09)]
    inner=[(x-math.copysign(.075,x),v-math.copysign(.075,v)) for x,v in outer]
    rings=[(outer,d+.045),(outer,d-.05),(inner,d-.05),(inner,d+.015)]
    mesh=bpy.data.meshes.new('Folded chamfered bezel')
    verts=[coord(face(room,side,u+x,y+v,depth)) for outline,depth in rings for x,v in outline]
    faces=[(r*8+i,r*8+(i+1)%8,(r+1)*8+(i+1)%8,(r+1)*8+i) for r in range(3) for i in range(8)]
    mesh.from_pydata(verts,[],faces)
    uv=mesh.uv_layers.new(name='UV0')
    for loop in mesh.loops:
        x,v=(outer if loop.vertex_index//8<2 else inner)[loop.vertex_index%8]
        uv.data[loop.index].uv=(x/w+.5,v/h+.5)
    bm=bmesh.new(); bm.from_mesh(mesh); bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(mesh); bm.free()
    obj=bpy.data.objects.new('Wall folded perimeter bezel',mesh); bpy.context.collection.objects.link(obj)
    finish(obj,'WallDark',.006)
    cassette(room,side,u,y,d+.035,w-.17,h-.17,mat)
    if fasteners:
        for du in [-w/2+.14,w/2-.14]:
            for dy in [-h/2+.14,h/2-.14]:
                tube('Wall captive socket',side,u+du,y+dy,d-.051,u+du,y+dy,d-.066,.032,'Recess',12)
                tube('Wall fastener head',side,u+du,y+dy,d-.067,u+du,y+dy,d-.076,.020,'BrushedSteel',6)

def vent(side,u,y,w,h):
    # Louvers have angled profiles and a deep dark plenum, rather than flat painted slits.
    cassette(room,side,u,y,-.115,w,h,'Recess')
    for du in [-w/2,w/2]: strip('HVAC deep side jamb',room,side,u+du,y,-.26,.13,h+.18,.25,'WallDark',.025)
    for dy in [-h/2,h/2]: strip('HVAC folded header',room,side,u,y+dy,-.26,w+.12,.14,.25,'WallDark',.025)
    for i in range(max(3,int(h/.19))):
        v=y-h/2+.14+i*.19
        # Each blade has a folded lip; both surfaces remain attached to the jambs.
        strip('HVAC blade web',room,side,u,v,-.24,w-.13,.055,.19,'BrushedSteel',.006)
        strip('HVAC blade folded lip',room,side,u,v-.035,-.33,w-.13,.065,.035,'WallDark',.006)
    for du in [-w*.25,w*.25]: strip('HVAC blade central support',room,side,u+du,y,-.18,.04,h-.1,.055,'WallDark',.005)

plans=json.loads((SOURCE/'WallRevisionReport.json').read_text())['plans']
for side,bays in plans.items():
    print('COMPOSING_WALL',side,flush=True)
    for index,(lo,hi,kind) in enumerate(bays):
        width=hi-lo
        strip('Wall dark continuous backpan',room,side,(lo+hi)/2,4,-.045,width-.02,7.68,.06,'Recess')
        # Wide deep columns are a primary silhouette; small panels sit between them.
        for u in [lo+.09,hi-.09]:
            strip('Wall box-section spine',room,side,u,4,-.21,.17,7.64,.30,'WallDark',.025)
            for y in [.65,4.15,7.35]:
                strip('Wall spine wrap collar',room,side,u,y,-.39,.24,.24,.08,'BrushedSteel',.012)
        n=max(1,math.ceil(width/3.2)); cell=width/n
        for i in range(n):
            u=lo+(i+.5)*cell; w=cell-.28
            # A separate lower armored band grounds the wall without adding floor geometry.
            plate(side,u,.72,w,.88,'WallDark',-.23,False)
            strip('Wall kick guard upper fold',room,side,u,1.22,-.31,w,.08,.12,'BrushedSteel',.012)
            if kind=='portal': continue
            if kind=='vent':
                plate(side,u,2.85,w,2.85,'WallDark')
                vent(side,u,5.65,w-.05,2.35)
                if side=='E' and 35<=u<=49:
                    plate(side,u,3.05,min(1.3,w-.25),1.02,'WallCeramic',-.32)
                    strip('Gallery maintenance pull',room,side,u+.42,3.05,-.40,.055,.28,.06,'BrushedSteel',.006)
            elif kind=='service':
                # Tall paired equipment leaves with raised hinges and true external pull handles.
                plate(side,u,3.25,w,3.75,'WallDark',-.23)
                for dy in [-1.25,1.25]:
                    strip('Service hinge bracket',room,side,u-w/2+.14,3.25+dy,-.35,.10,.29,.09,'BrushedSteel',.012)
                for y in [3.07,3.43]: tube('Service handle standoff',side,u+w/2-.28,y,-.30,u+w/2-.28,y,-.43,.028,'BrushedSteel')
                tube('Service door pull',side,u+w/2-.28,3.07,-.43,u+w/2-.28,3.43,-.43,.025,'BrushedSteel')
                vent(side,u,5.93,w-.15,1.0)
                strip('Service status recess',room,side,u,4.65,-.30,.56,.16,.04,'Recess',.008)
                strip('Service status bar',room,side,u-.12,4.65,-.328,.22,.025,.018,'WarmIndicator',.003)
                strip('Service ochre identity tab',room,side,u,1.55,-.31,.48,.08,.016,'WallWarning',.006)
            else:
                # Offset horizontal seams and restrained light inserts break up the height.
                plate(side,u,2.65,w,2.55,'WallDark')
                plate(side,u,4.72,w,1.27,'WallCeramic' if (i+index)%3==0 else 'WallDark')
                plate(side,u,6.28,w,1.40,'WallDark',-.17,False)
                for dy in [-.24,0,.24]: strip('Upper folded reinforcing rib',room,side,u,6.28+dy,-.24,w-.4,.035,.045,'WallDark',.006)
        if kind=='portal':
            u=(lo+hi)/2
            plate(side,u,3.35,width-.60,4.95,'WallDark',-.26)
            strip('Loading portal center gasket',room,side,u,3.35,-.31,.055,4.7,.015,'Recess',.004)
            for du in [-width*.29,width*.29]:
                plate(side,u+du,3.35,.52,4.05,'WallCeramic',-.33,False)
                tube('Portal latch bridge',side,u+du,2.5,-.42,u+du,3.0,-.42,.04,'BrushedSteel')
            vent(side,u,6.65,width-.7,.74)
        # Recessed upper distribution band is not a second oversized flat sheet.
        strip('Wall upper utility channel',room,side,(lo+hi)/2,7.51,-.11,width-.23,.44,.08,'Recess')
        for y in [7.40,7.62]: tube('Wall upper conduit',side,lo+.22,y,-.25,hi-.22,y,-.25,.045,'WallDark')
        for u in [lo+.40,hi-.40]: strip('Upper conduit mounting saddle',room,side,u,7.51,-.29,.09,.40,.07,'BrushedSteel',.007)

# Distinct exposed risers interrupt the regular cladding. All ends enter attached junctions.
for side,u in [('E',29.2),('N',14.8),('W',26.5),('S',-15.5)]:
    plate(side,u,2.1,1.13,1.45,'WallCeramic',-.38)
    vent(side,u,1.95,.70,.55)
    for offset in [-.22,0,.22]:
        tube('Wall riser supply pipe',side,u+offset,2.86,-.33,u+offset,7.12,-.33,.055)
        for y in [3.0,4.55,6.05,7.04]: tube('Riser coupling sleeve',side,u+offset,y-.055,-.33,u+offset,y+.055,-.33,.073,'BrushedSteel')
    for y in [3.6,5.3,6.7]: strip('Riser gang mounting bracket',room,side,u,y,-.41,.76,.075,.08,'WallDark',.012)
    plate(side,u,7.18,.91,.60,'WallDark',-.31,False)

for parts in groups.values():
    for obj in parts: obj['WallComposition']=1
new_objects=[o for o in bpy.context.scene.objects if o.get('WallComposition')]
for obj in new_objects:
    assert all(.10<=p.z<=7.90 for p in (obj.matrix_world@v.co for v in obj.data.vertices)), obj.name
sys.path.insert(0,str(SOURCE))
from wall_mesh_budget import reduce_wall_cost
reduce_wall_cost()
final_wall_count=sum(bool(o.get('WallComposition')) for o in bpy.context.scene.objects)
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
report={'removed_previous_wall_objects':removed,'wall_objects':final_wall_count,'material_groups':len(all_groups),
        'triangles':sum(len(o.data.polygons) for o in bpy.context.selected_objects),'floor_generated':False}
(SOURCE/'WallCompositionReport.json').write_text(json.dumps(report,indent=2)); print('WALL_COMPOSITION',json.dumps(report))
