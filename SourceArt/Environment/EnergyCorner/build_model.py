"""Blender authoring source. Run from the project root with Blender --background --python.

Reuses the verified corner envelope and material slots, replaces flat louvers,
and adds manufacturing detail. Coordinates in helpers use Unity world metres.
"""
import bpy
import bmesh
import json
import math
from pathlib import Path
from mathutils import Vector

PROJECT = Path(__file__).resolve().parents[3]
SOURCE = Path(__file__).resolve().parent
OUTPUT = PROJECT / 'Assets/Project/Art/Environment/ServiceQuarter/CornerSample/Models'
OUTPUT.mkdir(parents=True, exist_ok=True)
bpy.context.preferences.filepaths.save_version = 0
bpy.context.preferences.filepaths.file_preview_type = 'NONE'
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
materials = {}
groups = {}

def coord(p):
    return (4 - p[0], 36 - p[2], p[1])

def finish(obj, material, bevel=.012):
    obj.data.materials.clear()
    obj.data.materials.append(materials[material])
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = obj.modifiers.new('Manufactured edge radius', 'BEVEL')
        mod.width = bevel
        mod.segments = 1 if bevel <= .004 else (2 if bevel <= .012 else 3)
        mod.limit_method = 'ANGLE'
        mod.harden_normals = True
        bpy.ops.object.modifier_apply(modifier=mod.name)
    for poly in obj.data.polygons:
        poly.use_smooth = True
    mod = obj.modifiers.new('Weighted face normals', 'WEIGHTED_NORMAL')
    mod.keep_sharp = True
    mod.weight = 50
    bpy.ops.object.modifier_apply(modifier=mod.name)
    groups.setdefault(material, []).append(obj)
    return obj

def box(name, p, size, mat, bevel=.012, rotation=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=coord(p))
    obj = bpy.context.object
    obj.name = name
    obj.scale = (size[0], size[2], size[1])
    if rotation:
        obj.rotation_euler = rotation
    return finish(obj, mat, bevel)

def cylinder(name, a, b, radius, mat, sides=24):
    a, b = Vector(coord(a)), Vector(coord(b))
    bpy.ops.mesh.primitive_cylinder_add(vertices=sides, radius=radius, depth=(b-a).length, location=(a+b)/2)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_euler = (b-a).to_track_quat('Z', 'Y').to_euler()
    return finish(obj, mat, min(.003, radius*.15))

snapshot = json.loads((SOURCE / 'BlockoutMesh.json').read_text())
for part in snapshot['parts']:
    mat = part['material']
    if mat in {'Concrete','MarkingPaint'}:
        continue
    if mat not in materials:
        material = bpy.data.materials.new(mat)
        material.diffuse_color = {'CoatedMetal': (.08,.15,.18,1), 'BrushedSteel':(.3,.36,.38,1),
                                 'Recess':(.015,.02,.025,1), 'OxidizedCopper':(.3,.13,.05,1)}.get(mat,(.5,.5,.5,1))
        materials[mat] = material
    verts = [(-v['x'], -v['z'], v['y']) for v in part['vertices']]
    indices = part['triangles']
    mesh = bpy.data.meshes.new('Envelope_' + mat)
    # FBX import into Unity flips X; bake the inverse into the authoring coordinates.
    mesh.from_pydata(verts, [], [(indices[i+2],indices[i+1],indices[i]) for i in range(0,len(indices),3)])
    uv = mesh.uv_layers.new(name='Surface UV')
    for loop in mesh.loops:
        value = part['uv'][loop.vertex_index]
        uv.data[loop.index].uv = (value['x'], value['y'])
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=.000001)
    # Remove the old flat vent blades by their connected component bounds.
    seen = set()
    remove = []
    for v in list(bm.verts):
        if v in seen:
            continue
        stack, component = [v], []
        seen.add(v)
        while stack:
            current = stack.pop()
            component.append(current)
            for edge in current.link_edges:
                other = edge.other_vert(current)
                if other not in seen:
                    seen.add(other)
                    stack.append(other)
        center = sum((x.co for x in component), Vector()) / len(component)
        extent = [max(x.co[i] for x in component)-min(x.co[i] for x in component) for i in range(3)]
        front = abs(center.x-3)<.02 and abs(center.y-.12)<.03 and 1.38<center.z<2.91 and extent[0]>1.3 and extent[2]<.09
        side = abs(center.x+.115)<.03 and abs(center.y+3)<.02 and 1.38<center.z<2.81 and extent[1]>1.3 and extent[2]<.09
        # The original sample also dressed a distant gallery; it is outside this asset's scope.
        remote_gallery = center.x < -8.6
        if remote_gallery or (mat=='BrushedSteel' and (front or side)):
            remove.extend(component)
    if remove:
        bmesh.ops.delete(bm, geom=remove, context='VERTS')
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new('Beveled envelope ' + mat, mesh)
    bpy.context.collection.objects.link(obj)
    finish(obj, mat, .009 if mat!='Concrete' else .003)

def face(east, u, y, d):
    return (4+d,y,u) if east else (u,y,36-d)

def facebox(name, east, u, y, d, width, height, depth, mat, bevel=.012):
    return box(name, face(east,u,y,d), (depth,height,width) if east else (width,height,depth), mat, bevel)

def screw(east,u,y,d=.15):
    cylinder('Recessed fastener washer',face(east,u,y,d),face(east,u,y,d+.008),.037,'Recess',16)
    cylinder('Hex socket screw',face(east,u,y,d+.008),face(east,u,y,d+.022),.024,'BrushedSteel',6)
    facebox('Screw drive slot',east,u,y,d+.023,.018,.004,.002,'Recess',.0005)

def shaped_plate(name, east, u, y, d, width, height, depth, clip, mat, bevel=.009):
    # An eight-sided sheet with clipped corners and real thickness.
    w,h=width/2,height/2
    outline=[(-w+clip,-h),(w-clip,-h),(w,-h+clip),(w,h-clip),
             (w-clip,h),(-w+clip,h),(-w,h-clip),(-w,-h+clip)]
    vertices=[coord(face(east,u+x,y+v,d+offset)) for offset in [-depth/2,depth/2] for x,v in outline]
    faces=[tuple(range(7,-1,-1)),tuple(range(8,16))]
    faces += [(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)]
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(vertices,[],faces)
    obj=bpy.data.objects.new(name,mesh)
    bpy.context.collection.objects.link(obj)
    # Planar UVs use the same density on both elevations.
    uv=mesh.uv_layers.new(name='Surface UV')
    for loop in mesh.loops:
        p=outline[loop.vertex_index%8]
        uv.data[loop.index].uv=((p[0]+w)/width,(p[1]+h)/height)
    bm=bmesh.new(); bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(mesh); bm.free()
    return finish(obj,mat,bevel)

for east, u, height in [(False,1,1.68),(True,39,1.6)]:
    # Deep formed frame, dark gasket and two fan rotors behind angled louvers.
    for du in [-.77,.77]:
        facebox('Vent extruded side frame',east,u+du,2.15,.16,.08,height+.12,.16,'CoatedMetal',.025)
    for y in [2.15-height/2-.04,2.15+height/2+.04]:
        facebox('Vent extruded end frame',east,u,y,.16,1.62,.08,.16,'CoatedMetal',.023)
    for y in [1.78,2.5]:
        cylinder('Fan rotor well',face(east,u,y,.10),face(east,u,y,.13),.33,'Recess',40)
        cylinder('Fan impeller hub',face(east,u,y,.13),face(east,u,y,.18),.085,'BrushedSteel',24)
        for n in range(7):
            angle = n*math.tau/7
            obj=facebox('Fan curved vane',east,u+math.cos(angle)*.18,y+math.sin(angle)*.18,.145,.24,.06,.025,'BrushedSteel',.01)
            # Rotation around the normal to the ventilation opening.
            obj.rotation_euler = (angle,0,0) if east else (0,angle,0)
    for i in range(11):
        obj=facebox('Folded angled vent louver',east,u,1.43+i*.137,.20,1.42,.065,.15,'BrushedSteel',.012)
        obj.rotation_euler = (0,.26,0) if east else (.26,0,0)
    for du in [-.77,.77]:
        for y in [1.38,2.92]: screw(east,u+du,y,.245)

# Cabinet: a gasket, frame, protected controls, hinges and a grab handle.
for x in [2.29,3.71]:
    box('Cabinet perimeter gasket',(x,2.05,35.83),(.045,2.18,.035),'Recess',.012)
    box('Raised cabinet side lip',(x,2.05,35.80),(.045,2.18,.07),'CoatedMetal',.012)
for y in [.96,3.14]:
    box('Cabinet folded lip',(3,y,35.80),(1.46,.05,.07),'CoatedMetal',.014)
for y in [1.3,2.9]:
    box('Hinge mount',(2.32,y,35.77),(.13,.25,.08),'CoatedMetal',.025)
    cylinder('Hinge knuckle',(2.32,y-.11,35.72),(2.32,y+.11,35.72),.042,'BrushedSteel')
for y in [1.5,1.95]:
    box('Handle standoff',(3.55,y,35.73),(.09,.09,.17),'BrushedSteel',.025)
cylinder('Cabinet pull handle',(3.55,1.5,35.65),(3.55,1.95,35.65),.032,'BrushedSteel')
for x in [2.4,3.6]:
    box('Display chamfered bezel',(x,2.62,35.80),(.065,.75,.08),'CoatedMetal',.02)
for y in [2.25,2.99]:
    box('Display chamfered bezel',(3,y,35.80),(1.26,.065,.08),'CoatedMetal',.02)
for x in [2.38,3.62]:
    for y in [1.05,3.05]: screw(False,x,y,.25)

# Visible construction: socket screws, latches and a service hatch on each elevation.
for east in [False,True]:
    for u in ([-1,1,3] if not east else [37,39,41]):
        for du in [-.81,.81]:
            for y in [.93,3.27]: screw(east,u+du,y,.12)
    for u in ([-2,0,2,4] if not east else [36,38,40,42]):
        facebox('Profiled frame foot',east,u,.55,.14,.14,.32,.17,'CoatedMetal',.035)
        facebox('Top frame joint',east,u,4.27,.13,.13,.21,.14,'BrushedSteel',.025)
    for y in [1.13,2.45,3.12]:
        facebox('Quarter turn service latch',east,-1.7 if not east else 41.7,y,.14,.075,.16,.06,'BrushedSteel',.018)

# Cable glands and flange around the side junction case.
for z in [36.53,37.47]:
    box('Junction frame flange',(4.20,2.38,z),(.13,.98,.055),'CoatedMetal',.018)
    for y in [1.98,2.78]: screw(True,z,y,.28)
for z in [36.78,37.02,37.26]:
    cylinder('Junction cable gland collar',(4.20,1.88,z),(4.20,1.97,z),.057,'BrushedSteel',16)
    cylinder('Junction sealed gland',(4.20,1.81,z),(4.20,1.89,z),.037,'Recess')
    cylinder('Junction short wire',(4.20,1.62,z),(4.20,1.83,z),.019,'Recess')

# A formed top canopy and skirt change the silhouette, rather than adding bolts alone.
for east in [False, True]:
    centers=[-1,1,3] if not east else [37,39,41]
    for u in centers:
        shaped_plate('Canopy dark seam',east,u,4.31,.13,1.98,.35,.10,.09,'Recess')
        shaped_plate('Canopy folded metal face',east,u,4.34,.19,1.92,.30,.10,.085,'CoatedMetal',.018)
        facebox('Canopy steel drip lip',east,u,4.23,.25,1.75,.035,.045,'BrushedSteel',.008)
        facebox('Canopy top return',east,u,4.49,.08,1.92,.04,.30,'CoatedMetal',.014)
        # Lower panels have seams, sloping ends and drainage rather than a solid box.
        shaped_plate('Skirt access gasket',east,u,.91,.105,1.61,.27,.028,.05,'Recess',.005)
        shaped_plate('Skirt access cover',east,u,.91,.13,1.55,.22,.035,.045,'CoatedMetal',.008)
        for du in [-.55,-.33,-.11,.11,.33,.55]:
            facebox('Skirt drainage slot',east,u+du,.90,.15,.10,.018,.006,'Recess',.004)
        for du in [-.69,.69]: screw(east,u+du,.91,.155)
    # A removable clipped service plate above the vent keeps the larger masses legible.
    u=1 if not east else 39
    shaped_plate('Upper service gasket',east,u,3.36,.11,1.52,.22,.025,.05,'Recess')
    shaped_plate('Upper clipped service lid',east,u,3.36,.135,1.43,.18,.035,.04,'CoatedMetal')
    for du in [-.61,.61]: screw(east,u+du,3.36,.16)

# Three formed corner guards with tapered ends, leaving the cable route open above.
for y,h,w in [(1.02,.48,.22),(2.05,1.52,.19),(3.16,.42,.22)]:
    shaped_plate('Front corner armor',False,3.95,y,.20,w,h,.09,.06,'CoatedMetal',.018)
    shaped_plate('Side corner armor',True,36.05,y,.20,w,h,.09,.06,'CoatedMetal',.018)
    for dy in [-h*.32,h*.32]:
        screw(False,3.95,y+dy,.255)
        screw(True,36.05,y+dy,.255)

# A ribbed removable connection panel adds a mechanical focal point on the side.
shaped_plate('Junction gasket',True,37,2.38,.255,.73,.68,.03,.09,'Recess')
shaped_plate('Junction clipped cover',True,37,2.38,.285,.66,.62,.035,.07,'CoatedMetal')
for z in [36.76,37.24]:
    for y in [2.16,2.60]: screw(True,z,y,.31)
for y in [2.26,2.38,2.50]:
    facebox('Junction recessed heat slot',True,37,y,.307,.36,.025,.008,'Recess',.006)

# Formed feet, visible rubber isolation and a narrow perimeter seam above the plinth.
for east in [False,True]:
    for u in ([-1.82,.02,2.02,3.82] if not east else [36.18,38,40,41.82]):
        shaped_plate('Foot isolation pad',east,u,.63,.15,.19,.18,.04,.03,'Recess')
        shaped_plate('Folded plinth shoe',east,u,.62,.18,.15,.14,.045,.03,'BrushedSteel')

# Replace the concrete foundation with removable technical cladding.
for east in [False,True]:
    for u in ([-1,1,3] if not east else [37,39,41]):
        facebox('Foundation black backing',east,u,.32,.03,1.98,.64,.06,'Recess',.008)
        shaped_plate('Foundation removable metal panel',east,u,.33,.075,1.90,.54,.04,.07,'CoatedMetal',.012)
        facebox('Foundation steel sill',east,u,.10,.11,1.70,.035,.035,'BrushedSteel',.007)
        for du in [-.79,.79]: screw(east,u+du,.35,.103)
        for du in [-.42,-.14,.14,.42]:
            facebox('Foundation lower vent',east,u+du,.23,.099,.14,.023,.009,'Recess',.004)

materials['DeckMetal']=bpy.data.materials.new('DeckMetal')
materials['DeckMetal'].diffuse_color=(.24,.28,.30,1)

def deck_plate(x,z):
    outline=[(-.86,-.96),(.86,-.96),(.96,-.86),(.96,.86),(.86,.96),(-.86,.96),(-.96,.86),(-.96,-.86)]
    # One continuous shell contains the channel; no overlay plane intersects the tray.
    rings=[(1,-.004),(1,.012),(.805,.009),(.805,.0015),(.785,.0015),(.785,.006)]
    vertices=[coord((x+u*scale,y,z+v*scale)) for scale,y in rings for u,v in outline]
    faces=[tuple(range(7,-1,-1)),tuple(range(40,48))]
    for ring in range(len(rings)-1):
        faces += [(i+ring*8,(i+1)%8+ring*8,(i+1)%8+(ring+1)*8,i+(ring+1)*8) for i in range(8)]
    mesh=bpy.data.meshes.new('Chamfered deck module')
    mesh.from_pydata(vertices,[],faces)
    uv=mesh.uv_layers.new(name='Surface UV')
    for loop in mesh.loops:
        u,v=outline[loop.vertex_index%8]
        scale=rings[loop.vertex_index//8][0]
        u,v=u*scale,v*scale
        uv.data[loop.index].uv=((u+1)/2,(v+1)/2)
    bm=bmesh.new(); bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(mesh); bm.free()
    obj=bpy.data.objects.new('Deck removable clipped plate',mesh)
    bpy.context.collection.objects.link(obj)
    finish(obj,'DeckMetal',.0006)

for ix in range(4):
    for iz in range(4):
        x,z=5+ix*2,32+iz*2
        box('Deck seam backing',(x,-.002,z),(1.99,.012,1.99),'Recess',.002)
        deck_plate(x,z)
        # Steel edge inserts and sockets make the module read as an assembled floor.
        for dz in [-.82,.82]:
            box('Deck steel edge insert',(x,.014,z+dz),(1.42,.004,.018),'BrushedSteel',.002)
        for dx in [-.81,.81]:
            for dz in [-.81,.81]:
                cylinder('Deck fastener socket',(x+dx,.012,z+dz),(x+dx,.015,z+dz),.034,'Recess',12)
                cylinder('Deck flush fastener',(x+dx,.015,z+dz),(x+dx,.017,z+dz),.019,'BrushedSteel',6)
        if (ix+iz)%3==0:
            box('Deck service hatch gasket',(x,.006,z),(1.34,.006,1.34),'Recess',.003)
            bpy.ops.mesh.primitive_cube_add(size=1,location=coord((x,.014,z)))
            lid=bpy.context.object
            lid.name='Deck removable service hatch'
            lid.scale=(1.29,1.29,.008)
            bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
            for dx in [-.52,.52]:
                bpy.ops.mesh.primitive_cube_add(size=1,location=coord((x+dx,.014,z)))
                cutter=bpy.context.object
                cutter.name='Temporary handle aperture cutter'
                cutter.scale=(.075,.30,.04)
                bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
                bpy.context.view_layer.objects.active=lid
                cut=lid.modifiers.new('Through-cut lifting aperture','BOOLEAN')
                cut.operation='DIFFERENCE'
                cut.solver='EXACT'
                cut.object=cutter
                bpy.ops.object.modifier_apply(modifier=cut.name)
                bpy.data.objects.remove(cutter,do_unlink=True)
            # Cube-atlas UVs hide most of the authored panel wear. Use the full panel map.
            uv=lid.data.uv_layers.active
            for loop in lid.data.loops:
                p=lid.data.vertices[loop.vertex_index].co
                uv.data[loop.index].uv=(p.x/1.29+.5,-p.y/1.29+.5)
            finish(lid,'DeckMetal',.002)
            for dx,dz in [(-.51,-.51),(.51,.51)]:
                cylinder('Deck quarter turn lock',(x+dx,.018,z+dz),(x+dx,.021,z+dz),.045,'BrushedSteel',16)
                box('Deck lock screwdriver slot',(x+dx,.0215,z+dz),(.042,.001,.006),'Recess',.0003)
            for dx in [-.52,.52]:
                box('Deck lifting crossbar',(x+dx,.013,z),(.022,.006,.23),'BrushedSteel',.001)
        elif ix==3 and iz in [1,3]:
            box('Deck vent dark pocket',(x,.008,z),(1.21,.004,1.14),'Recess',.02)
            for dz in [-.47,-.34,-.21,-.08,.05,.18,.31,.44]:
                box('Deck ventilation rib',(x,.012,z+dz),(1.14,.012,.035),'BrushedSteel',.006)
            for dx in [-.60,.60]:
                box('Deck vent side frame',(x+dx,.013,z),(.025,.012,1.17),'DeckMetal',.006)
        else:
            for dz in [-.40,-.20,0,.20,.40]:
                box('Deck antislip inset',(x,.014,z+dz),(1.18,.003,.016),'Recess',.003)

# All visible geometry must belong to the equipment envelope or the deck patch.
def owned_position(p):
    x,y,z=4-p.x,p.z,36-p.y
    module=-2.2<=x<=4.4 and -.02<=y<=4.6 and 35.6<=z<=42.2
    deck=4<=x<=12 and -.02<=y<=.04 and 31<=z<=39
    return module or deck

for obj in bpy.context.scene.objects:
    if obj.type=='MESH':
        outside=sum(not owned_position(obj.matrix_world@v.co) for v in obj.data.vertices)
        assert outside==0, f'Stray geometry outside module/deck: {obj.name}, {outside} vertices'

# Merge by material to keep rendering cost bounded, retain source objects in the .blend.
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / 'EnergyCorner.blend'))
for mat, objects in groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    if len(objects) > 1:
        bpy.ops.object.join()
    obj=bpy.context.object
    obj.name='SM_EnergyCorner_'+mat
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    mod=obj.modifiers.new('Export triangulation','TRIANGULATE')
    mod.keep_custom_normals=True
    bpy.ops.object.modifier_apply(modifier=mod.name)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(OUTPUT/'EnergyCorner.fbx'),use_selection=True,object_types={'MESH'},
    apply_unit_scale=True,global_scale=1,axis_forward='-Z',axis_up='Y',bake_anim=False,
    mesh_smooth_type='FACE',add_leaf_bones=False,use_mesh_modifiers=True)
report={'meshes':len(groups),'triangles':sum(len(o.data.polygons) for o in bpy.context.selected_objects),
        'vertices':sum(len(o.data.vertices) for o in bpy.context.selected_objects)}
(SOURCE/'ModelReport.json').write_text(json.dumps(report,indent=2))
print('ENERGY_CORNER_REPORT',report)
