"""Complete only the uncovered energy-module elevations; accepted corner stays untouched."""
import bpy
import bmesh
import math
import json
from pathlib import Path
from mathutils import Vector

SOURCE=Path(__file__).resolve().parent
PROJECT=SOURCE.parents[2]
OUTPUT=PROJECT/'Assets/Project/Art/Environment/ServiceQuarter/EnergyCompletion/Models'
OUTPUT.mkdir(parents=True,exist_ok=True)
bpy.context.preferences.filepaths.save_version=0
bpy.context.preferences.filepaths.file_preview_type='NONE'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE.parent/'EnergyCorner/EnergyCorner.blend'))
materials={name:bpy.data.materials.get(name) for name in ['CoatedMetal','BrushedSteel','Recess','OxidizedCopper','WarmIndicator']}
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
groups={}
def coord(p): return (-p[0],-p[2],p[1])
def face(side,u,y,d):
    if side=='S': return (u,y,36-d)
    if side=='N': return (u,y,46+d)
    if side=='E': return (4+d,y,u)
    return (-6-d,y,u)
def finish(obj,mat,bevel=.008):
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    obj.data.materials.clear(); obj.data.materials.append(materials[mat])
    if bevel:
        mod=obj.modifiers.new('Manufactured edge','BEVEL'); mod.width=bevel
        mod.segments=1 if bevel<=.003 else (2 if bevel<=.009 and mat!='Recess' else 3)
        mod.harden_normals=True; bpy.ops.object.modifier_apply(modifier=mod.name)
    for p in obj.data.polygons: p.use_smooth=True
    mod=obj.modifiers.new('Face weighted normals','WEIGHTED_NORMAL'); mod.keep_sharp=True
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    groups.setdefault(mat,[]).append(obj)
    return obj
def box(name,side,u,y,d,w,h,t,mat,bevel=.008):
    bpy.ops.mesh.primitive_cube_add(size=1,location=coord(face(side,u,y,d)))
    obj=bpy.context.object; obj.name=name
    obj.scale=(w,t,h) if side in ['S','N'] else (t,w,h)
    return finish(obj,mat,bevel)
def panel(name,side,u,y,d,w,h,t,mat,clip=.08):
    a,b=w/2,h/2
    outline=[(-a+clip,-b),(a-clip,-b),(a,-b+clip),(a,b-clip),(a-clip,b),(-a+clip,b),(-a,b-clip),(-a,-b+clip)]
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata([coord(face(side,u+x,y+v,d+depth)) for depth in [-t/2,t/2] for x,v in outline],[],
        [tuple(range(7,-1,-1)),tuple(range(8,16))]+[(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)])
    uv=mesh.uv_layers.new(name='UV0')
    for loop in mesh.loops:
        x,v=outline[loop.vertex_index%8]; uv.data[loop.index].uv=(x/w+.5,v/h+.5)
    bm=bmesh.new(); bm.from_mesh(mesh); bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(mesh); bm.free()
    obj=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(obj)
    return finish(obj,mat,.009)
def pipe(name,points,radius,mat):
    curve=bpy.data.curves.new(name,'CURVE'); curve.dimensions='3D'; curve.bevel_depth=radius; curve.bevel_resolution=2
    spline=curve.splines.new('POLY'); spline.points.add(len(points)-1)
    for p,v in zip(spline.points,points): p.co=(*coord(v),1)
    obj=bpy.data.objects.new(name,curve); bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active=obj; bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
    bpy.ops.object.convert(target='MESH'); return finish(bpy.context.object,mat,0)
def bolt(side,u,y,d=.12):
    a,b=Vector(coord(face(side,u,y,d))),Vector(coord(face(side,u,y,d+.018)))
    bpy.ops.mesh.primitive_cylinder_add(vertices=6,radius=.023,depth=(b-a).length,location=(a+b)/2)
    obj=bpy.context.object; obj.name='Energy captive socket bolt'; obj.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler()
    finish(obj,'BrushedSteel',.002)

def bay(side,u,kind):
    box('Energy cassette gasket',side,u,2.1,.035,1.92,2.82,.06,'Recess')
    panel('Energy formed cassette',side,u,2.1,.075,1.82,2.68,.035,'CoatedMetal')
    panel('Energy plinth cover',side,u,.33,.075,1.90,.54,.04,'CoatedMetal',.065)
    box('Energy plinth backing',side,u,.32,.03,1.92,.64,.06,'Recess')
    box('Energy plinth steel sill',side,u,.10,.11,1.70,.035,.035,'BrushedSteel',.003)
    panel('Energy upper removable cover',side,u,3.94,.07,1.90,.70,.06,'CoatedMetal')
    panel('Energy canopy face',side,u,4.34,.19,1.90,.30,.10,'CoatedMetal',.08)
    box('Energy canopy top return',side,u,4.49,.08,1.90,.04,.30,'CoatedMetal')
    box('Energy canopy drip edge',side,u,4.23,.25,1.72,.035,.045,'BrushedSteel',.003)
    box('Energy lower profile',side,u,.72,.11,1.90,.06,.05,'BrushedSteel')
    # The existing accepted upright at Z=42 remains the eastern starting seam.
    if not(side=='E' and u==43):
        box('Energy structural upright',side,u-.95,2.1,.10,.045,4.2,.055,'BrushedSteel')
    for du in [-.79,.79]:
        for y in [.96,3.26]: bolt(side,u+du,y)
    for du in [-.42,-.14,.14,.42]: box('Energy plinth slot',side,u+du,.23,.099,.14,.023,.008,'Recess',.003)
    if kind=='vent':
        box('Energy cooling recess',side,u,2.15,.105,1.48,1.68,.04,'Recess')
        for du in [-.77,.77]: box('Energy vent side frame',side,u+du,2.15,.17,.07,1.80,.15,'CoatedMetal',.018)
        for y in [1.28,3.02]: box('Energy vent end frame',side,u,y,.17,1.6,.07,.15,'CoatedMetal',.018)
        for i in range(11):
            # Formed louver blade with a sloping cross-section, not an intersecting overlay.
            y=1.43+i*.137
            vertices=[face(side,u+x,y+dy,.18+depth) for depth,dy in [(-.065,-.02),(.065,.025),(.065,.05),(-.065,.005)] for x in [-.70,.70]]
            mesh=bpy.data.meshes.new('Energy folded louver')
            mesh.from_pydata([coord(p) for p in vertices],[],[(0,2,3,1),(2,4,5,3),(4,6,7,5),(6,0,1,7),(0,6,4,2),(1,3,5,7)])
            uv=mesh.uv_layers.new(name='UV0')
            for loop in mesh.loops: uv.data[loop.index].uv=(loop.vertex_index%2,loop.vertex_index//2/3)
            bm=bmesh.new(); bm.from_mesh(mesh); bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(mesh); bm.free()
            obj=bpy.data.objects.new('Energy folded cooling louver',mesh); bpy.context.collection.objects.link(obj); finish(obj,'BrushedSteel',.003)
    elif kind=='service':
        panel('Energy maintenance gasket',side,u,2.05,.11,1.42,2.12,.035,'Recess')
        panel('Energy maintenance cover',side,u,2.05,.15,1.32,2.02,.035,'CoatedMetal')
        for y in [1.36,2.76]: box('Energy maintenance hinge',side,u-.55,y,.19,.11,.23,.05,'BrushedSteel')
        box('Energy maintenance pull mount',side,u+.48,1.90,.19,.07,.33,.06,'Recess')
        pipe('Energy maintenance handle',[face(side,u+.48,1.76,.23),face(side,u+.48,2.04,.23)],.022,'BrushedSteel')
        box('Energy service status backing',side,u,2.80,.185,.40,.09,.035,'Recess')
        box('Energy attached service indicator',side,u,2.80,.207,.26,.025,.008,'WarmIndicator',.003)

bays=[('S',-5,'solid'),('S',-3,'vent'),('E',43,'service'),('E',45,'solid')]
bays += [('N',u,k) for u,k in zip([-5,-3,-1,1,3],['service','solid','vent','solid','service'])]
bays += [('W',u,k) for u,k in zip([37,39,41,43,45],['solid','vent','service','vent','solid'])]
for args in bays: bay(*args)
# Cable bundles follow the new north/west elevations and are fixed by saddles.
for i in range(3):
    y=3.56+i*.065
    points=[(-6.23,y,36.3),(-6.23,y,45.99)]
    points += [(-5.99-.24*math.cos(a*math.pi/12),y,45.99+.24*math.sin(a*math.pi/12)) for a in range(1,7)]
    points += [(3.8,y,46.23)]
    pipe('Energy routed cable bundle',points,.018,'Recess')
for z in [37,39,41,43,45]: box('Energy west cable saddle','W',z,3.64,.26,.08,.29,.025,'BrushedSteel',.003)
for x in [-5,-3,-1,1,3]: box('Energy north cable saddle','N',x,3.64,.26,.08,.29,.025,'BrushedSteel',.003)
for side,u in [('W',36.45),('N',3.65)]:
    panel('Energy cable termination gasket',side,u,3.625,.17,.55,.43,.05,'Recess',.06)
    panel('Energy closed cable junction',side,u,3.625,.22,.50,.38,.12,'CoatedMetal',.06)
    for du in [-.17,.17]: bolt(side,u+du,3.625,.28)
pipe('Energy short protected copper conduit',[face('N',-5.67,1.05,.13),face('N',-5.67,3.25,.13)],.024,'OxidizedCopper')

def allowed(x,y,z):
    upper_return=4.46<=y<=4.53 and ((-6.3<=x<=-2.03 and 35.9<=z<=36.08) or
        (3.92<=x<=4.1 and 42.03<=z<=46.3) or (-6.35<=x<=4.1 and 45.92<=z<=46.1) or
        (-6.1<=x<=-5.92 and 35.9<=z<=46.35))
    return -.02<=y<=4.6 and (upper_return or (-6.3<=x<=-2.03 and 35.6<=z<=36.005) or
        (3.99<=x<=4.35 and 42.03<=z<=46.3) or (-6.35<=x<=4.1 and 45.99<=z<=46.35) or
        (-6.35<=x<=-5.99 and 35.9<=z<=46.35))
for obj in bpy.context.scene.objects:
    if obj.type=='MESH':
        assert all(allowed(-p.x,p.z,-p.y) for p in (obj.matrix_world@v.co for v in obj.data.vertices)), 'Out-of-scope skin: '+obj.name
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'EnergyCompletion.blend'))
for mat,parts in groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts: obj.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]
    if len(parts)>1: bpy.ops.object.join()
    obj=bpy.context.object; obj.name='SM_EnergyCompletion_'+mat
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    mod=obj.modifiers.new('Export triangles','TRIANGULATE'); mod.keep_custom_normals=True; bpy.ops.object.modifier_apply(modifier=mod.name)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(OUTPUT/'EnergyCompletion.fbx'),use_selection=True,object_types={'MESH'},
    axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,mesh_smooth_type='FACE')
report={'bays':len(bays),'material_groups':len(groups),'triangles':sum(len(o.data.polygons) for o in bpy.context.selected_objects)}
(SOURCE/'ModelReport.json').write_text(json.dumps(report,indent=2))
print('ENERGY_COMPLETION_REPORT',json.dumps(report))
