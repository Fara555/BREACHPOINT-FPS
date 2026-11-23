"""Through-market with two end entrances and an open east serving window."""
import bpy,bmesh,json,math,sys
from pathlib import Path
from mathutils import Vector,Matrix

SOURCE=Path(__file__).resolve().parent
PROJECT=SOURCE.parents[2]
OUTPUT=PROJECT/'Assets/Project/Art/Environment/ServiceQuarter/MarketRoom/Models'
OUTPUT.mkdir(parents=True,exist_ok=True)
bpy.context.preferences.filepaths.save_version=0
bpy.context.preferences.filepaths.file_preview_type='NONE'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE.parent/'InteriorFinish/InteriorFinish.blend'))
families=('Attached formed cassette','Block panel gasket','Block upright profile','Block folded cap',
          'Service panel gasket','Service hatch hinge','Small attached indicator','Attached block roof cap')
removed=0
for obj in list(bpy.context.scene.objects):
    if obj.type!='MESH' or obj.get('WallComposition') or obj.get('LandmarkFinish'):continue
    center=sum((obj.matrix_world@Vector(p) for p in obj.bound_box),Vector())/8
    x,y,z=-center.x,center.z,-center.y
    if obj.name.startswith(families) and -18.4<x<-10.5 and 33.5<z<47.5:
        bpy.data.objects.remove(obj,do_unlink=True);removed+=1
assert removed>60,'Workshop selection is unexpectedly incomplete'

# Standalone loading cases now provide the visuals for all three cover locations.
cover_families = (
    'Cover casing gasket', 'Attached formed cassette', 'Cover edge clamp',
    'Cover cooling slot', 'Equipment top lid', 'Cover top latch',
)
cover_locations = [('entry', 1, 31.6, 4), ('east', 8.5, 43.6, 3), ('north', -5.5, 51.6, 3)]
legacy_housings = {location: [] for location, _, _, _ in cover_locations}
for obj in bpy.context.scene.objects:
    if obj.type != 'MESH' or obj.get('WallComposition') or not obj.name.startswith(cover_families):
        continue
    center = sum((obj.matrix_world @ Vector(p) for p in obj.bound_box), Vector()) / 8
    if not .04 < center.z < 1.23:
        continue
    for location, x, z, width in cover_locations:
        if abs(-center.x - x) < width / 2 + .20 and abs(-center.y - z) < .80:
            legacy_housings[location].append(obj)
cover_counts = {location: len(objects) for location, objects in legacy_housings.items()}
assert all(24 <= count <= 54 for count in cover_counts.values()), f'Unexpected cover objects: {cover_counts}'
for objects in legacy_housings.values():
    for obj in objects:
        bpy.data.objects.remove(obj, do_unlink=True)

# The east gallery access hatches are separate prefabs attached to the retained wall.
gallery_hatch_positions = (36.4, 39.2, 42, 44.8, 47.6)
gallery_hatch_parts = {'Attached formed cassette', 'Wall folded perimeter bezel',
                       'Wall captive socket', 'Wall fastener head', 'Gallery maintenance pull'}
gallery_hatches = {z: [] for z in gallery_hatch_positions}
for obj in bpy.context.scene.objects:
    if obj.type != 'MESH' or not obj.get('WallComposition') or obj.name.split('.')[0] not in gallery_hatch_parts:
        continue
    center = sum((obj.matrix_world @ Vector(p) for p in obj.bound_box), Vector()) / 8
    if 17.58 < -center.x < 17.799 and abs(center.z - 3.05) < .42:
        for z in gallery_hatch_positions:
            if abs(-center.y - z) < .60:
                gallery_hatches[z].append(obj)
assert all(len(parts) == 12 for parts in gallery_hatches.values()), 'Expected twelve original parts per gallery hatch'
for parts in gallery_hatches.values():
    for obj in parts:
        bpy.data.objects.remove(obj, do_unlink=True)

# Both hall rows use the same standalone luminaire at their original mounting points.
luminaire_parts = {'Lamp carrier hanger', 'Attached ceiling light housing', 'Ceiling light frame', 'Ceiling diffuser'}
hall_luminaires = {(x, z): [] for x in (-10, 10) for z in (28, 42, 56)}
for obj in bpy.context.scene.objects:
    if obj.type != 'MESH' or obj.name.split('.')[0] not in luminaire_parts:
        continue
    center = sum((obj.matrix_world @ Vector(p) for p in obj.bound_box), Vector()) / 8
    if 7.50 < center.z < 7.95:
        for x, z in hall_luminaires:
            if abs(-center.x - x) < 1.70 and abs(-center.y - z) < .01:
                hall_luminaires[x, z].append(obj)
assert all(len(parts) == 5 for parts in hall_luminaires.values()), 'Expected five old parts per hall luminaire'
for parts in hall_luminaires.values():
    for obj in parts:
        bpy.data.objects.remove(obj, do_unlink=True)


def export(name):
    # Separate authored inside/outside finishes before merging shared materials.
    for obj in list(bpy.context.scene.objects):
        if obj.type!='MESH' or len({p.material_index for p in obj.data.polygons})<2:continue
        bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
        bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.mesh.separate(type='MATERIAL');bpy.ops.object.mode_set(mode='OBJECT')
    groups={}
    for obj in list(bpy.context.scene.objects):
        if obj.type=='MESH':
            used={p.material_index for p in obj.data.polygons};assert len(used)==1,obj.name
            material=obj.data.materials[next(iter(used))]
            obj.data.materials.clear();obj.data.materials.append(material)
            for p in obj.data.polygons:p.material_index=0
            groups.setdefault(material.name,[]).append(obj)
    for mat,parts in groups.items():
        bpy.ops.object.select_all(action='DESELECT')
        for obj in parts:obj.select_set(True)
        bpy.context.view_layer.objects.active=parts[0]
        if len(parts)>1:bpy.ops.object.join()
        obj=bpy.context.object;obj.name='SM_'+name+'_'+mat
        bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
        mod=obj.modifiers.new('Export triangles','TRIANGULATE');mod.keep_custom_normals=True;bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=str(OUTPUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},
        axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,mesh_smooth_type='FACE')
    return {'groups':len(groups),'triangles':sum(len(o.data.polygons) for o in bpy.context.selected_objects)}

report={'removed_workshop_skin_objects':removed,'removed_cover_objects':cover_counts,
        'removed_gallery_hatches':len(gallery_hatches),
        'removed_hall_luminaires':len(hall_luminaires),
        'unchanged_interior':export('InteriorWithoutWorkshop')}
if '--' in sys.argv and '--interior-only' in sys.argv[sys.argv.index('--') + 1:]:
    print('RETAINED_INTERIOR_REPORT', json.dumps(report))
    sys.exit(0)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version=0
bpy.context.preferences.filepaths.file_preview_type='NONE'
for name in ['InteriorNeutral','InteriorQuietFrame','WallDark','WallWarm','FloorField','FloorJoint','InteriorFixture','Recess','MarketFrame','MarketWorktop','MarketWarm','DisplayGlass','MarketStatus','MarketLining','MarketCeiling','MarketDeviceDisplay','MarketDeviceShell','CounterTopSatin','CounterPaint','CounterBase','CounterAccent']:
    bpy.data.materials.new(name)

def box(name,p,size,mat='InteriorNeutral',bevel=.012):
    x,y,z=p;w,h,d=size
    vertices=[(-x+sx*w/2,-z+sz*d/2,y+sy*h/2) for sx,sz,sy in
              [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(vertices,[],[(3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)])
    uv=mesh.uv_layers.new(name='UV0')
    for face in mesh.polygons:
        for i,loop in enumerate(face.loop_indices):
            p=mesh.vertices[mesh.loops[loop].vertex_index].co
            uv.data[loop].uv=(-p.x/2,-p.y/2) if mat.startswith('Floor') else [(0,0),(1,0),(1,1),(0,1)][i]
    bm=bmesh.new();bm.from_mesh(mesh)
    if bevel:bmesh.ops.bevel(bm,geom=list(bm.edges),offset=bevel,segments=3 if bevel>.004 else 1,affect='EDGES',clamp_overlap=True)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free();mesh.update()
    weighted=[Vector() for _ in mesh.vertices]
    anchors=[Vector() for _ in mesh.vertices]
    for face in mesh.polygons:
        face.use_smooth=True
        planar=max(abs(v) for v in face.normal)>.999999
        for vertex in face.vertices:
            weighted[vertex]+=face.normal*face.area
            if planar:anchors[vertex]+=face.normal
    if mat.startswith('Floor'):
        # The accepted floor is outside this refinement pass.
        mesh.normals_split_custom_set_from_vertices([v.normalized() for v in weighted])
    else:
        # Preserve exact planar corner normals. Area-weighted vertex normals alone bend
        # thin return faces toward a neighboring broad sheet by almost ninety degrees.
        normals=[Vector() for _ in mesh.loops]
        for face in mesh.polygons:
            planar=max(abs(v) for v in face.normal)>.999999
            for loop in face.loop_indices:
                vertex=mesh.loops[loop].vertex_index
                n=anchors[vertex] if anchors[vertex].length_squared>1e-10 else weighted[vertex]
                normals[loop]=face.normal if planar else n.normalized()
        mesh.normals_split_custom_set(normals)
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);mesh.materials.append(bpy.data.materials[mat])
    return obj

def fitting(name,p,size,mat='MarketFrame',bevel=.012):
    obj=box(name,p,size,mat,bevel);obj['MarketFitting']=1
    return obj

def round_fitting(name,p,radius,depth,mat='MarketWorktop'):
    x,y,z=p
    bpy.ops.mesh.primitive_cylinder_add(vertices=24,radius=radius,depth=depth,
        location=(-x,-z,y),rotation=(0,math.pi/2,0))
    obj=bpy.context.object;obj.name=name
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    obj.data.materials.append(bpy.data.materials[mat])
    bevel=obj.modifiers.new('Machined edge radius','BEVEL');bevel.width=min(.003,radius*.12);bevel.segments=3
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    for face in obj.data.polygons:face.use_smooth=abs(face.normal.x)<.98
    obj['MarketFitting']=1
    return obj

def lining(name,p,size,mat='MarketLining',bevel=.015):
    obj=box(name,p,size,mat,bevel);obj['MarketEnvelope']=1
    return obj

def end_portal_jamb(x, z):
    # One continuous post from the floor to the lintel. The lower profile widens
    # into its protective foot, with a 2 mm joint to the structural reveal.
    south = z < 40
    wall_z = 34.28 if south else 46.72
    inward = 1 if south else -1
    profiles = (
        # Height, half-width, reveal distance, roomward depth, corner chamfer.
        (.008, .074, .004, .172, .008),
        (.026, .076, .002, .175, .010),
        (.532, .076, .002, .175, .010),
        (.558, .070, .002, .150, .012),
        (3.184, .070, .002, .150, .012),
        (3.200, .060, .008, .139, .008),
    )
    vertices = []
    for height, width, near, far, chamfer in profiles:
        ring = (
            (-width + chamfer, near), (width - chamfer, near), (width, near + chamfer),
            (width, far - chamfer), (width - chamfer, far), (-width + chamfer, far),
            (-width, far - chamfer), (-width, near + chamfer),
        )
        vertices.extend((-x - side, -wall_z - inward * depth, height) for side, depth in ring)

    last = len(vertices) - 8
    faces = [tuple(range(7, -1, -1)), tuple(range(last, last + 8))]
    for start in range(0, last, 8):
        for i in range(8):
            a, b = start + i, start + (i + 1) % 8
            faces.append((a, b, b + 8, a + 8))

    name = 'Interior end portal folded jamb'
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    bm = bmesh.new(); bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh); bm.free(); mesh.update()
    origin = Vector((-x, -wall_z, 0))
    uv = mesh.uv_layers.new(name='UV0')
    for face in mesh.polygons:
        axes = sorted(range(3), key=lambda axis: abs(face.normal[axis]))[:2]
        for loop in face.loop_indices:
            point = mesh.vertices[mesh.loops[loop].vertex_index].co - origin
            uv.data[loop].uv = (point[axes[0]], point[axes[1]])
    mesh.materials.append(bpy.data.materials['MarketFrame'])
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj['MarketEnvelope'] = 1
    return obj

def ceiling_cassette(name,z):
    # Clipped corners and a real folded depth, not a coplanar overlay on the roof slab.
    x,y=-14.5,4.79;w,d=5.30,5.55;c=.18
    outline=[(-w/2+c,-d/2),(w/2-c,-d/2),(w/2,-d/2+c),(w/2,d/2-c),
             (w/2-c,d/2),(-w/2+c,d/2),(-w/2,d/2-c),(-w/2,-d/2+c)]
    # A returned, sloped skirt shades the perimeter instead of exposing isolated bright cut faces.
    verts=[(-x-(a-math.copysign(.07,a) if h<0 else a),
            -z-(b-math.copysign(.07,b) if h<0 else b),y+h) for h in [-.055,.055] for a,b in outline]
    faces=[tuple(range(7,-1,-1)),tuple(range(8,16))]+[(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)]
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces)
    uv=mesh.uv_layers.new(name='UV0')
    for loop in mesh.loops:
        p=mesh.vertices[loop.vertex_index].co;uv.data[loop.index].uv=(p.x/2,p.y/2)
    bm=bmesh.new();bm.from_mesh(mesh)
    bmesh.ops.bevel(bm,geom=list(bm.edges),offset=.012,segments=3,affect='EDGES',clamp_overlap=True)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free();mesh.update()
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj)
    mesh.materials.append(bpy.data.materials['MarketCeiling']);obj['MarketEnvelope']=1

def conduit(name,points,radius=.024):
    curve=bpy.data.curves.new(name,'CURVE');curve.dimensions='3D';curve.resolution_u=8
    curve.bevel_depth=radius;curve.bevel_resolution=2
    spline=curve.splines.new('BEZIER');spline.bezier_points.add(len(points)-1)
    for point,(x,y,z) in zip(spline.bezier_points,points):
        point.co=(-x,-z,y);point.handle_left_type='AUTO';point.handle_right_type='AUTO'
    obj=bpy.data.objects.new(name,curve);bpy.context.collection.objects.link(obj)
    obj.data.materials.append(bpy.data.materials['MarketFrame'])
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    bpy.ops.object.convert(target='MESH');obj=bpy.context.object;obj['MarketFitting']=1
    for face in obj.data.polygons:face.use_smooth=True

# Match collision shell sections. End doors remain open; the east aperture has a solid 1.2m sill.
# Deep lining conceals the boundary-wall kick folds rather than intersecting their front faces.
box('West internal wall',(-17.7,2.5,40.5),(.6,5,13))
for z in [34.14,46.86]:
    box('End wall west return',(-16.9,2.5,z),(2.2,5,.28))
    box('End wall east return',(-12,2.5,z),(2,5,.28))
    box('End doorway lintel',(-14.4,4.1,z),(2.8,1.8,.28))
for z in [36.5,44.5]:box('East facade wall',(-11.14,2.5,z),(.28,5,5))
box('East doorway lintel',(-11.14,4.1,40.5),(.28,1.8,3))
box('Serving window solid breast wall',(-11.14,.575,40.5),(.28,1.15,3))
box('Original-height closed market roof',(-14.5,4.99,40.5),(7.04,.16,13.04),'InteriorQuietFrame',.02)

# Sparse large facade seams and formed door jambs retain the accepted quiet white/ochre language.
for z in [34.1,36,38,43,45,46.9]:
    box('East attached facade seam',(-10.978,2.5,z),(.035,4.75,.035),'InteriorQuietFrame',.004)
for z in [38.96,42.04]:
    box('Window deep folded jamb',(-11.03,2.17,z),(.45,2.06,.16),'InteriorQuietFrame',.022)
    box('Window recessed guide rail',(-10.79,2.17,z),(.035,1.83,.045),'Recess',.004)
    for y in [1.38,2.91]:
        box('Window jamb mounting shoe',(-10.765,y,z),(.045,.15,.21),'WallDark',.009)
box('Serving window header housing',(-11.06,3.28,40.5),(.56,.34,3.40),'InteriorQuietFrame',.03)
box('Window warm folded canopy',(-10.90,3.46,40.5),(.78,.10,3.55),'WallWarm',.022)
box('Window open shutter bottom bar',(-11.00,3.10,40.5),(.14,.13,2.86),'WallDark',.015)
for y in [3.21,3.30,3.39]:
    box('Retracted shutter broad slat',(-10.763,y,40.5),(.025,.055,2.92),'WallDark',.007)
box('Serving sill deep folded edge',(-11.10,1.185,40.5),(.93,.13,3.42),'InteriorQuietFrame',.025)
box('Serving sill inset work surface',(-11.10,1.258,40.5),(.77,.025,3.20),'WallDark',.008)
box('Serving sill front return',(-10.625,1.14,40.5),(.045,.15,3.32),'WallWarm',.008)
box('Window lower maintenance recess',(-10.984,.58,40.5),(.025,.78,2.74),'InteriorQuietFrame',.009)
for z in [39.81,41.19]:
    box('Window lower removable cassette',(-10.958,.58,z),(.025,.67,1.30),'InteriorNeutral',.016)
    box('Window lower recessed release',(-10.937,.83,z),(.012,.045,.18),'Recess',.004)

# Broad architectural folds give the pale facade a built edge without repeating small greebles.
for za,zb in [(34.03,38.82),(42.18,46.97)]:
    box('Facade lower recessed rail',(-10.974,.19,(za+zb)/2),(.035,.28,zb-za),'InteriorQuietFrame',.009)
    box('Facade folded cap rail',(-10.95,4.83,(za+zb)/2),(.14,.18,zb-za),'InteriorQuietFrame',.018)
for z in [34.06,46.94]:
    box('Facade corner protective fold',(-10.96,2.46,z),(.13,4.56,.15),'InteriorQuietFrame',.02)
for z in [33.94,47.06]:
    for x in [-15.85,-12.95]:box('End framed open jamb',(x,1.6,z),(.14,3.2,.20),'InteriorQuietFrame',.018)
    box('End open doorway warm header',(-14.4,3.24,z),(3.16,.18,.30),'WallWarm',.025)

# Fill only the old workshop floor cutout. Same 14mm skin,40mm joints and material as the room outside.
# QuietFloor's existing full-room joint bed already extends under this footprint.
# Add only the missing sheets, avoiding coplanar duplicate bed surfaces.
for xa,xb in [(-18,-14),(-14,-10.92)]:
    for za,zb in [(33.92,38),(38,44),(44,47.08)]:
        box('Market calm floor sheet',((xa+xb)/2,.010,(za+zb)/2),(xb-xa-.04,.008,zb-za-.04),'FloorField',.0015)

# Low counters leave the east access and the entire north-south aisle open.
for z in [36.8,44.2]:
    box('Market counter base',(-12,.51,z),(1.2,1.02,2.2),'InteriorNeutral',.04)
    box('Market counter dark plinth',(-12,.12,z),(1.25,.22,2.25),'InteriorQuietFrame',.018)
    box('Market counter durable top',(-12,1.08,z),(1.34,.13,2.34),'WallDark',.035)
    box('Market counter identity inset',(-12.63,.75,z),(.025,.12,1.85),'WallWarm',.006)
    # Recessed service doors sit below the identity stripe, leaving a calm upper face.
    box('Counter service door shadow',(-12.612,.43,z),(.025,.47,1.93),'Recess',.008)
    for dz in [-.49,.49]:
        box('Counter removable front',(-12.637,.43,z+dz),(.035,.40,.90),'InteriorNeutral',.014)
        box('Counter captive pull',(-12.661,.56,z+dz),(.018,.045,.18),'InteriorQuietFrame',.005)
    box('Counter top folded nose',(-12.68,1.045,z),(.06,.10,2.24),'InteriorQuietFrame',.014)
    box('Counter rear splash return',(-11.38,1.22,z),(.06,.28,2.22),'InteriorQuietFrame',.012)
for z in [36.6,40.5,44.4]:
    if z==40.5:
        # The middle cabinet becomes an actual open rack, inside the existing collision envelope.
        fitting('Rack recessed back',(-17.32,1.18,z),(.10,2.36,2.3),'WallDark',.018)
        for dz in [-1.10,1.10]:
            fitting('Rack folded upright',(-17.03,1.18,z+dz),(.72,2.36,.10),'MarketFrame',.018)
        for y in [.28,.98,1.68,2.30]:
            fitting('Rack formed shelf',(-17.03,y,z),(.72,.075,2.20),'MarketFrame',.012)
            fitting('Rack warm shelf label',(-16.658,y,z-.73),(.025,.055,.28),'MarketWarm',.005)
        for dz,y in [(-.58,.5175),(.55,.5175),(-.58,1.2175),(.55,1.2175),(.25,1.9175)]:
            fitting('Supply cassette body',(-17.00,y,z+dz),(.55,.40,.78),'InteriorNeutral' if y>1.5 else 'WallDark',.03)
            fitting('Supply cassette locked lid',(-17.00,y+.21,z+dz),(.58,.045,.81),'MarketFrame',.01)
            fitting('Supply cassette recessed grip',(-16.716,y+.02,z+dz),(.018,.08,.27),'Recess',.008)
            for side in [-.26,.26]:
                fitting('Cassette latch recessed mount',(-16.704,y+.14,z+dz+side),(.055,.16,.085),'MarketFrame',.009)
                fitting('Cassette overcenter latch leaf',(-16.663,y+.19,z+dz+side),(.030,.14,.055),'MarketWorktop',.008)
                round_fitting('Cassette latch captive pivot',(-16.640,y+.13,z+dz+side),.025,.023)
    else:
        box('Wall storage cabinet',(-17.15,1.18,z),(.9,2.36,2.3),'WallDark',.035)
        for dz in [-.58,.58]:
            box('Storage access face',(-16.681,1.2,z+dz),(.035,2.15,1.05),'InteriorNeutral',.012)
            for y in [1.08,1.32]:
                fitting('Storage handle standoff',(-16.621,y,z+dz-.32),(.085,.045,.07),'MarketFrame',.008)
            fitting('Storage formed door pull',(-16.573,1.20,z+dz-.32),(.045,.28,.045),'MarketFrame',.009)
            for y in [.58,1.85]:
                fitting('Storage captive hinge',(-16.646,y,z+dz+.45),(.045,.16,.07),'MarketFrame',.009)
        fitting('Storage recessed toe plinth',(-17.07,.10,z),(.70,.17,2.20),'MarketFrame',.014)
        fitting('Storage folded top cap',(-17.01,2.35,z),(.74,.10,2.30),'MarketFrame',.018)
    box('Storage cabinet warm header',(-16.65,2.3,z),(.02,.06,2.0),'MarketWarm',.004)
    if z==44.4:
        fitting('Storage cooling grille recess',(-16.638,.38,z+.58),(.025,.30,.72),'Recess',.008)
        for y in [.28,.34,.40,.46]:
            fitting('Storage cooling folded blade',(-16.614,y,z+.58),(.035,.025,.64),'MarketFrame',.004)

# Rebuild only the existing terminal and reader as six cohesive manufactured components.
sys.path.insert(0,str(SOURCE))
from workstation import build_workstation
build_workstation()
fitting('Packing tray base',(-12.0,1.18,36.6),(.76,.06,1.05),'MarketFrame',.02)
for x in [-12.36,-11.64]:fitting('Packing tray folded side',(x,1.25,36.6),(.045,.16,1.05),'MarketFrame',.01)
for z in [36.10,37.10]:fitting('Packing tray folded end',(-12.0,1.25,z),(.72,.16,.045),'MarketFrame',.01)
# Small standalone supply pouches are owned by Art/Props/SealedSupplyPack and placed as prefabs.

# One purposeful utility run supplies storage; short branches terminate at the cabinet caps.
conduit('Storage overhead supply',[(-17.29,3.10,35.3),(-17.29,3.10,38),(-17.29,3.10,42),(-17.29,3.10,45.3)])
for z in [35.5,38.4,42.3,45.1]:
    fitting('Supply run captive saddle',(-17.33,3.10,z),(.11,.13,.09),'MarketFrame',.008)
for z in [36.6,44.4]:
    conduit('Storage connected drop',[(-17.29,3.10,z-.25),(-17.29,3.08,z),(-17.29,2.88,z+.09),(-17.29,2.41,z+.09)])
fitting('Storage distribution enclosure',(-17.26,2.91,38.4),(.20,.42,.52),'MarketFrame',.025)
fitting('Storage distribution access lid',(-17.149,2.91,38.4),(.035,.34,.44),'InteriorNeutral',.014)
fitting('Storage distribution latch',(-17.124,2.91,38.53),(.015,.075,.04),'MarketWarm',.004)
conduit('Terminal enclosed power lead',[(-11.41,1.22,44.83),(-11.41,1.54,44.83),(-11.41,2.10,44.83)])
fitting('Counter wall power enclosure',(-11.41,2.29,44.83),(.20,.36,.32),'MarketFrame',.024)
fitting('Counter wall power face',(-11.519,2.29,44.83),(.035,.27,.23),'InteriorNeutral',.01)

# Recessed shutter station: physical rims shield the controls, without extra lettering/neon.
fitting('Shutter station attached backpan',(-11.33,1.82,42.45),(.10,.52,.36),'MarketFrame',.018)
for z in [42.278,42.622]:
    fitting('Shutter station protective cheek',(-11.41,1.82,z),(.18,.52,.035),'MarketFrame',.012)
for y in [1.575,2.065]:
    fitting('Shutter station folded rim',(-11.41,y,42.45),(.18,.035,.34),'MarketFrame',.009)
fitting('Shutter station inset control face',(-11.389,1.82,42.45),(.025,.41,.27),'InteriorNeutral',.012)
for y in [1.76,1.92]:
    round_fitting('Shutter station recessed push button',(-11.420,y,42.45),.041,.035,'MarketFrame')
    round_fitting('Shutter station button inset',(-11.442,y,42.45),.024,.008,'MarketWarm')
fitting('Shutter station status slit',(-11.410,1.65,42.45),(.012,.014,.065),'MarketStatus',.003)
for z in [37.6,43.4]:
    box('Interior light housing',(-14.6,4.70,z),(2.8,.18,.55),'InteriorQuietFrame',.025)
    box('Interior mounted diffuser',(-14.6,4.596,z),(2.55,.025,.38),'InteriorFixture',.006)

# Room envelope pass: broad quiet panels, deep portals and two suspended ceiling cassettes.
for z in [37.5,43.5]:
    ceiling_cassette('Suspended folded ceiling cassette',z)
    lining('Ceiling cassette recessed mounting bed',(-14.5,4.875,z),(4.94,.075,5.2),'MarketCeiling',.008)
for x in [-17.29,-11.40]:
    lining('Continuous ceiling perimeter reveal',(x,4.84,40.5),(.20,.14,12.48),'InteriorQuietFrame',.018)
for z in [34.38,46.62]:
    lining('End ceiling perimeter reveal',(-14.35,4.84,z),(5.96,.14,.18),'InteriorQuietFrame',.018)
for z in [37.45,43.55]:
    lining('Storage upper lining backpan',(-17.355,3.93,z),(.09,.88,5.70),'InteriorQuietFrame',.014)
    lining('Storage upper lining field',(-17.294,3.93,z),(.04,.75,5.55),'MarketLining',.025)
for z in [34.34,46.66]:
    for x in [-15.87,-12.93]:
        end_portal_jamb(x,z)
    lining('Interior end portal lintel',(-14.4,3.30,z),(3.08,.20,.22),'MarketFrame',.025)
    lining('Interior end portal warm inset',(-14.4,3.31,z+(.12 if z<40 else -.12)),(1.1,.045,.025),'MarketWarm',.005)
    for x,width in [(-16.69,1.28),(-12.04,1.34)]:
        lining('End wall protective backpan',(x,.57,z),(width,.84,.075),'InteriorQuietFrame',.018)
        lining('End wall replaceable impact sheet',(x,.57,z+(.052 if z<40 else -.052)),(width-.14,.68,.035),'MarketLining',.025)
    lining('End wall upper folded lining',(-14.4,4.12,z),(3.52,.87,.07),'MarketLining',.028)
# An internal breast-wall facing completes the window return without changing its collider.
lining('Window inner breast wall liner',(-11.345,.64,40.5),(.10,.91,2.88),'MarketLining',.022)

def reset_profile_normals(mesh,flat_faces):
    mesh.update();weighted=[Vector() for _ in mesh.vertices];anchors=[Vector() for _ in mesh.vertices]
    for face in mesh.polygons:
        for vertex in face.vertices:
            weighted[vertex]+=face.normal*face.area
            if face.index in flat_faces:anchors[vertex]+=face.normal
    normals=[Vector() for _ in mesh.loops]
    for face in mesh.polygons:
        for loop in face.loop_indices:
            vertex=mesh.loops[loop].vertex_index
            n=anchors[vertex] if anchors[vertex].length_squared>1e-10 else weighted[vertex]
            normals[loop]=face.normal if face.index in flat_faces else n.normalized()
    mesh.normals_split_custom_set(normals)

# Existing-only profile pass. Objects stay in place; only selected meshes are reshaped.
for obj in list(bpy.context.scene.objects):
    mesh=obj.data
    if obj.name.startswith('Supply cassette locked lid'):
        flat={face.index for face in mesh.polygons if max(abs(v) for v in face.normal)>.999999}
        center=sum((v.co for v in mesh.vertices),Vector())/len(mesh.vertices)
        lo=min(v.co.z for v in mesh.vertices);hi=max(v.co.z for v in mesh.vertices)
        for vertex in mesh.vertices:
            t=(vertex.co.z-lo)/(hi-lo)
            if obj.name.startswith('Supply'):
                vertex.co.x=center.x+(vertex.co.x-center.x)*(1-.045*t)
                vertex.co.y=center.y+(vertex.co.y-center.y)*(1-.035*t)
            else:
                vertex.co.z+=(-vertex.co.x+12.12)*.13*t
        reset_profile_normals(mesh,flat)
    if obj.name.startswith('Storage formed door pull'):
        center=sum((v.co for v in mesh.vertices),Vector())/len(mesh.vertices)
        rows=[-.14,-.11,-.07,0,.07,.11,.14]
        verts=[]
        for y in rows:
            arch=math.sin(math.pi*(y+.14)/.28)
            radius=.0175+.0045*arch
            for i in range(8):
                a=2*math.pi*i/8
                verts.append((center.x-.018*arch+math.cos(a)*radius,
                              center.y+math.sin(a)*radius,center.z+y))
        faces=[tuple(range(7,-1,-1)),tuple(range(48,56))]
        for row in range(6):
            for i in range(8):faces.append((row*8+i,row*8+(i+1)%8,(row+1)*8+(i+1)%8,(row+1)*8+i))
        replacement=bpy.data.meshes.new(obj.name+' refined casting');replacement.from_pydata(verts,[],faces)
        bm=bmesh.new();bm.from_mesh(replacement);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(replacement);bm.free();replacement.update()
        uv=replacement.uv_layers.new(name='UV0')
        for face in replacement.polygons:
            face.use_smooth=face.index>=2
            for loop in face.loop_indices:
                index=replacement.loops[loop].vertex_index
                uv.data[loop].uv=(index%8/7,index//8/6)
        replacement.materials.append(mesh.materials[0]);obj.data=replacement

# Keep the room's broad shell matte; furniture and outside facade retain their accepted finishes.
for obj in bpy.context.scene.objects:
    if obj.name.startswith(('West internal wall','End wall west return','End wall east return','End doorway lintel','East facade wall','East doorway lintel','Serving window solid breast wall')):
        obj.data.materials.append(bpy.data.materials['MarketLining'])
        center=sum((obj.matrix_world@Vector(p) for p in obj.bound_box),Vector())/8
        world_z=-center.y
        for face in obj.data.polygons:
            n=face.normal
            inward=(n.x<-.5 if obj.name.startswith('West') else
                    n.x>.5 if obj.name.startswith(('East','Serving')) else
                    n.y<-.5 if world_z<40 else n.y>.5)
            if inward:face.material_index=1

# Bounded surface pass: only handled window/counter components receive dedicated finishes.
roles={
    'MarketFrame':('Window deep folded jamb','Window jamb mounting shoe','Serving window header housing',
                   'Serving sill deep folded edge','Counter top folded nose','Counter rear splash return'),
    'MarketWorktop':('Serving sill inset work surface','Market counter durable top'),
    'MarketWarm':('Window warm folded canopy','Serving sill front return','Market counter identity inset')}
for obj in bpy.context.scene.objects:
    for material,names in roles.items():
        if obj.name.startswith(names):
            obj.data.materials[0]=bpy.data.materials[material]
            break
    # Refinish existing furniture using already owned paints, without more material slots.
    if obj.name.startswith(('Storage access face','Supply cassette body')):
        old=obj.data.materials[0].name
        obj.data.materials[0]=bpy.data.materials['MarketLining' if old=='InteriorNeutral' else 'MarketFrame']
    # Isolate the requested checkout counter; the other counter, window and devices retain their finishes.
    counter_roles={
        'CounterTopSatin':('Market counter durable top','Counter top folded nose','Counter rear splash return','Counter captive pull'),
        'CounterPaint':('Market counter base','Counter removable front'),
        'CounterAccent':('Market counter identity inset',),
        'CounterBase':('Market counter dark plinth',)}
    for material,names in counter_roles.items():
        if obj.name.startswith(names):
            center=sum((obj.matrix_world@Vector(p) for p in obj.bound_box),Vector())/8
            if 43<-center.y<45.4:obj.data.materials[0]=bpy.data.materials[material]
fittings=[o for o in bpy.context.scene.objects if o.get('MarketFitting')]
for obj in fittings:
    for vertex in obj.data.vertices:
        p=obj.matrix_world@vertex.co;x,y,z=-p.x,p.z,-p.y
        assert -17.45<=x<=-11.25 and -.001<=y<=3.5 and 34.5<=z<=46.5,('Fitting out of room',obj.name,(x,y,z))
        assert not (-15.8<x<-13.0 and y<2.6),('Fitting intrudes into central aisle',obj.name)
report['fittings_audit']={'objects':len(fittings),'inside_room':True,'central_aisle_clear':True}
envelope=[o for o in bpy.context.scene.objects if o.get('MarketEnvelope')]
for obj in envelope:
    for vertex in obj.data.vertices:
        p=obj.matrix_world@vertex.co;x,y,z=-p.x,p.z,-p.y
        assert -17.41<=x<=-11.25 and -.001<=y<=4.92 and 34.15<=z<=46.85,('Lining out of room',obj.name,(x,y,z))
        assert not (-15.79<x<-13.01 and y<3.19 and (z<34.5 or z>46.5)),('Lining obstructs an end door',obj.name)
report['envelope_audit']={'objects':len(envelope),'inside_room':True,'end_doorways_clear':True}
# UV1 carries meters in the same orientation as UV0, keeping micrograin density uniform.
detail_faces=0
for obj in bpy.context.scene.objects:
    if obj.type!='MESH' or obj.name.startswith('Market calm floor sheet'):continue
    mesh=obj.data;uv0=mesh.uv_layers.active
    if uv0 is None:continue
    uv1=mesh.uv_layers.new(name='SurfaceMeters')
    for face in mesh.polygons:
        loops=list(face.loop_indices);a=loops[0]
        origin=mesh.vertices[mesh.loops[a].vertex_index].co;ta=uv0.data[a].uv.copy()
        basis=None;best=0
        for i in range(1,len(loops)-1):
            b,c=loops[i],loops[i+1]
            du=uv0.data[b].uv-ta;dv=uv0.data[c].uv-ta;det=du.x*dv.y-du.y*dv.x
            if abs(det)<1e-10:continue
            e1=mesh.vertices[mesh.loops[b].vertex_index].co-origin
            e2=mesh.vertices[mesh.loops[c].vertex_index].co-origin
            u=(e1*dv.y-e2*du.y)/det;v=(e2*du.x-e1*dv.x)/det
            if abs(det)>best and e1.cross(e2).length>1e-8:
                tangent=u-face.normal*u.dot(face.normal)
                if tangent.length<1e-6:continue
                tangent.normalize();bitangent=face.normal.cross(tangent).normalized()
                if bitangent.dot(v)<0:bitangent=-bitangent
                basis=(tangent,bitangent);best=abs(det)
        if basis is None:
            edge=max((mesh.vertices[mesh.loops[v].vertex_index].co-origin for v in loops),key=lambda v:v.length_squared)
            tangent=edge.normalized();basis=(tangent,face.normal.cross(tangent).normalized())
        for loop in loops:
            p=mesh.vertices[mesh.loops[loop].vertex_index].co
            uv1.data[loop].uv=(p.dot(basis[0]),p.dot(basis[1]))
        detail_faces+=1
    mesh.uv_layers.active_index=0
report['detail_uv_audit']={'physical_faces':detail_faces,'floor_uv_untouched':True}
if '--counter-finish-only' in sys.argv:
    # A finish-only pass must not rotate existing UV frames due to tiny tessellation roundoff.
    baseline=PROJECT/'Logs/BattlefieldMarketRoom/BeforeCounterSurface/MarketRoom.blend'
    with bpy.data.libraries.load(str(baseline),link=False) as (source_data,target_data):
        saved_names=list(source_data.objects);target_data.objects=list(saved_names)
    references=dict(zip(saved_names,target_data.objects))
    for obj in bpy.context.scene.objects:
        if obj.type!='MESH' or obj.name.startswith('Interior end portal folded jamb'):continue
        saved=references[obj.name].data;mesh=obj.data
        assert len(mesh.vertices)==len(saved.vertices) and len(mesh.loops)==len(saved.loops),obj.name
        for uv in saved.uv_layers:
            for target,original in zip(mesh.uv_layers[uv.name].data,uv.data):target.uv=original.uv
        mesh.normals_split_custom_set([n.vector for n in saved.corner_normals])
        mesh.uv_layers.active_index=saved.uv_layers.active_index
    for old in target_data.objects:
        mesh=old.data;bpy.data.objects.remove(old,do_unlink=True)
        if mesh.users==0:bpy.data.meshes.remove(mesh)
    report['existing_finish_mapping_preserved']=True
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'MarketRoom.blend'))
report['market_room']=export('MarketRoom')
report['door_clear_widths_m']=[2.8,2.8]
report['door_clear_height_m']=3.2
report['serving_window']={'width_m':3.0,'breast_wall_height_m':1.15,'sill_top_m':1.2705,'walk_through':False,'glass':False}
report['detail_pass']=9
report['original_art_files_modified']=False
(SOURCE/'MarketRoomReport.json').write_text(json.dumps(report,indent=2));print('MARKET_ROOM_REPORT',json.dumps(report),flush=True)
