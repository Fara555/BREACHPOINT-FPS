"""Daily-use counter props and a guarded fan. Existing accepted art is never opened or changed."""
import bpy,bmesh,math,json
from pathlib import Path
from mathutils import Vector
SOURCE=Path(__file__).resolve().parent;PROJECT=SOURCE.parents[3]
OUT=PROJECT/'Assets/Project/Art/Environment/ServiceQuarter/MarketRoom/Life/Models'
OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version=0
bpy.context.preferences.filepaths.file_preview_type='NONE'
for name in ['MarketFrame','MarketWorktop','MarketDeviceShell','CounterAccent','LifePaper','LifeDrink']:
    bpy.data.materials.new(name)

def coord(p):x,y,z=p;return Vector((-x,-z,y))
def finish(obj,name,mat,bevel=0):
    obj.name=name;bpy.context.view_layer.objects.active=obj
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True)
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    obj.data.materials.append(bpy.data.materials[mat])
    if bevel:
        mod=obj.modifiers.new('Manufactured edge radius','BEVEL');mod.width=bevel;mod.segments=3
        bpy.ops.object.modifier_apply(modifier=mod.name)
    mod=obj.modifiers.new('Weighted face normals','WEIGHTED_NORMAL');mod.keep_sharp=True
    for f in obj.data.polygons:f.use_smooth=True
    bpy.ops.object.modifier_apply(modifier=mod.name)
    if not obj.data.uv_layers:
        uv=obj.data.uv_layers.new(name='UV0')
        for face in obj.data.polygons:
            axes=sorted(range(3),key=lambda a:abs(face.normal[a]))[:2]
            for loop in face.loop_indices:
                p=obj.data.vertices[obj.data.loops[loop].vertex_index].co
                uv.data[loop].uv=(p[axes[0]],p[axes[1]])
    uv=obj.data.uv_layers.new(name='SurfaceMeters')
    for face in obj.data.polygons:
        axes=sorted(range(3),key=lambda a:abs(face.normal[a]))[:2]
        for loop in face.loop_indices:
            p=obj.data.vertices[obj.data.loops[loop].vertex_index].co
            uv.data[loop].uv=(p[axes[0]],p[axes[1]])
    obj.data.uv_layers.active_index=0
    return obj

def box(name,p,size,mat,bevel=.003,yaw=0):
    bpy.ops.mesh.primitive_cube_add(size=1,location=coord(p));obj=bpy.context.object
    obj.scale=(size[0],size[2],size[1]);obj.rotation_euler.z=math.radians(yaw)
    return finish(obj,name,mat,bevel)

def cylinder(name,a,b,r,mat,r2=None):
    pa,pb=coord(a),coord(b);d=pb-pa
    if r2 is None:bpy.ops.mesh.primitive_cylinder_add(vertices=24,radius=r,depth=d.length,location=(pa+pb)/2)
    else:bpy.ops.mesh.primitive_cone_add(vertices=24,radius1=r,radius2=r2,depth=d.length,location=(pa+pb)/2)
    obj=bpy.context.object;obj.rotation_euler=d.to_track_quat('Z','Y').to_euler()
    return finish(obj,name,mat,min(.001,r*.15))

def wire(name,points,r,mat):
    curve=bpy.data.curves.new(name,'CURVE');curve.dimensions='3D';curve.bevel_depth=r;curve.bevel_resolution=2
    line=curve.splines.new('POLY');line.points.add(len(points)-1)
    for p,value in zip(line.points,points):p.co=(*coord(value),1)
    obj=bpy.data.objects.new(name,curve);bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    bpy.ops.object.convert(target='MESH');return finish(bpy.context.object,name,mat)

def ring(name,x,y,z,r,wire_radius,mat):
    return wire(name,[(x,y+math.sin(a*2*math.pi/64)*r,z+math.cos(a*2*math.pi/64)*r) for a in range(65)],wire_radius,mat)

def pen(name,x,z,angle,accent):
    direction=Vector((math.sin(angle),0,math.cos(angle)));center=Vector((x,1.152,z))
    cylinder(name+' shaft',center-direction*.067,center+direction*.055,.0055,'MarketDeviceShell')
    cylinder(name+' grip',center-direction*.045,center-direction*.016,.0063,'MarketFrame')
    cylinder(name+' tapered nib',center-direction*.083,center-direction*.067,.0008,'MarketWorktop',.0055)
    cylinder(name+' cap',center+direction*.055,center+direction*.072,.0058,'CounterAccent' if accent else 'MarketFrame')

pen('Checkout stylus',-12.40,44.12,math.radians(31),True)
pen('Spare stylus',-12.43,44.29,math.radians(-23),False)
pen('Packing pencil',-12.48,37.55,math.radians(67),True)
box('Pocket notebook cover',(-12.34,1.149,45.03),(.23,.008,.28),'MarketFrame',.006,-9)
box('Pocket notebook paper',(-12.34,1.156,45.03),(.216,.006,.264),'LifePaper',.003,-9)
for row in range(3):
    wire('Notebook handwritten stroke',[(-12.40+i*.014,1.1595,44.97+row*.04+math.sin(i*2.1)*.003) for i in range(9)],.00085,'MarketFrame')
for i in range(2):
    box('Loose packing label',(-12.49+i*.022,1.1465+i*.0015,36.62+i*.10),(.135,.0015,.19),'LifePaper',.0004,(-12 if i==0 else 17))
# A used cup rests on the packing counter, with a real hollow rim rather than a capped cylinder.
cup=(-11.61,1.145,37.47)
rings=[]
for r,y in [(.026,0),(.037,.105),(.034,.105),(.024,.009)]:
    rings.append([coord((cup[0]+math.cos(a*2*math.pi/40)*r,cup[1]+y,cup[2]+math.sin(a*2*math.pi/40)*r)) for a in range(40)])
verts=[p for row in rings for p in row];faces=[]
for row in range(3):
    for i in range(40):faces.append((row*40+i,row*40+(i+1)%40,(row+1)*40+(i+1)%40,(row+1)*40+i))
faces.extend([tuple(range(39,-1,-1)),tuple(range(120,160))])
mesh=bpy.data.meshes.new('Hollow paper cup');mesh.from_pydata(verts,[],faces)
bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
obj=bpy.data.objects.new('Used packing cup',mesh);bpy.context.collection.objects.link(obj);finish(obj,obj.name,'LifePaper')
cylinder('Remaining drink',(-11.61,1.237,37.47),(-11.61,1.238,37.47),.032,'LifeDrink')

# Fan body and cage remain static; the blade mesh is exported separately about its local center.
center=Vector((-11.65,1.51,43.32))
box('Fan weighted foot',(-11.65,1.164,43.32),(.24,.038,.21),'MarketDeviceShell',.018)
box('Fan support neck',(-11.62,1.282,43.32),(.075,.22,.075),'MarketFrame',.018)
for x in [-11.69,-11.60]:ring('Fan rolled outer shroud',x,1.51,43.32,.163,.009,'MarketDeviceShell')
for r in [.053,.106,.154]:ring('Fan front guard ring',center.x-.10*(1-r/.158*.5),1.51,43.32,r,.0018,'MarketWorktop')
for i in range(10):
    a=i*2*math.pi/10
    wire('Fan formed front spoke',[(center.x-.10*(1-t*.5),center.y+math.sin(a)*.158*t,center.z+math.cos(a)*.158*t) for t in [0,.25,.5,.75,1]],.0018,'MarketWorktop')
    wire('Fan rear captive spoke',[(center.x+.052,center.y,center.z),(center.x+.048,center.y+math.sin(a)*.158,center.z+math.cos(a)*.158)],.0021,'MarketFrame')
cylinder('Fan rear motor housing',(-11.60,1.51,43.32),(-11.555,1.51,43.32),.041,'MarketDeviceShell')
cylinder('Fan fixed front badge',(-11.755,1.51,43.32),(-11.748,1.51,43.32),.022,'CounterAccent')

static=list(bpy.context.scene.objects)
for obj in static:
    for v in obj.data.vertices:
        p=obj.matrix_world@v.co;x,y,z=-p.x,p.z,-p.y
        assert -12.64<x<-11.3 and 1.144<y<1.70 and ((43.03<z<45.37) or (35.63<z<37.97)),(obj.name,(x,y,z))

def export(objects,name):
    groups={}
    for obj in objects:groups.setdefault(obj.data.materials[0].name,[]).append(obj)
    exported=[]
    for material,parts in groups.items():
        bpy.ops.object.select_all(action='DESELECT')
        for obj in parts:obj.select_set(True)
        bpy.context.view_layer.objects.active=parts[0]
        if len(parts)>1:bpy.ops.object.join()
        obj=bpy.context.object;obj.name='SM_'+name+'_'+material
        mod=obj.modifiers.new('Triangles','TRIANGULATE');mod.keep_custom_normals=True;bpy.ops.object.modifier_apply(modifier=mod.name)
        exported.append(obj)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in exported:obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,mesh_smooth_type='FACE')
    return {'materials':len(exported),'triangles':sum(len(o.data.polygons) for o in exported)}

bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'DailyUse.blend'))
static_report=export(static,'DailyUse')
for obj in list(bpy.context.scene.objects):bpy.data.objects.remove(obj,do_unlink=True)
rotor=[]
for blade in range(3):
    angle=blade*2*math.pi/3;outline=[]
    for radius,offset in [(.028,-.2),(.062,-.19),(.112,-.10),(.137,.05),(.13,.36),(.10,.43),(.055,.38),(.028,.25)]:
        a=angle+offset;outline.append((radius*math.sin(a),radius*math.cos(a)))
    vertices=[coord((x,y,z)) for x in [-.006,.006] for y,z in outline]
    faces=[tuple(range(7,-1,-1)),tuple(range(8,16))]+[(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)]
    mesh=bpy.data.meshes.new('Swept fan blade');mesh.from_pydata(vertices,[],faces)
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
    obj=bpy.data.objects.new('Fan swept rotor blade',mesh);bpy.context.collection.objects.link(obj)
    rotor.append(finish(obj,obj.name,'MarketFrame',.002))
rotor.append(cylinder('Fan rotating hub',(-.01,0,0),(.014,0,0),.029,'MarketDeviceShell'))
rotor_report=export(rotor,'FanRotor')
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'FanRotor.blend'))
report={'static':static_report,'rotor':rotor_report,'rotor_world_pivot':list(center),'rotation_axis':[1,0,0],
    'counter_bounds_audit':True,'all_existing_art_untouched':True,'new_lights':0,'new_colliders':0}
(SOURCE/'DailyUseReport.json').write_text(json.dumps(report,indent=2));print('DAILY_USE_REPORT',json.dumps(report),flush=True)
