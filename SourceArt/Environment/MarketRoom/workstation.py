"""Replace the existing checkout devices with coherent manufactured shells, not stacked boxes."""
import bpy,bmesh,math
from mathutils import Vector,Matrix

def outline(w,h,r):
    points=[]
    for cx,cy,start in [(w/2-r,h/2-r,0),(-w/2+r,h/2-r,90),(-w/2+r,-h/2+r,180),(w/2-r,-h/2+r,270)]:
        for step in range(7):
            angle=math.radians(start+step*15)
            points.append((cx+math.cos(angle)*r,cy+math.sin(angle)*r))
    return points

def make_mesh(name,rings,mapper,mats,strip_mats,cap_start=True,cap_end=True,atlas=None,surface_normal=None):
    n=len(rings[0]);verts=[mapper(*p) for ring in rings for p in ring]
    faces=[];material_ids=[]
    for ring in range(len(rings)-1):
        for i in range(n):
            faces.append((ring*n+i,ring*n+(i+1)%n,(ring+1)*n+(i+1)%n,(ring+1)*n+i))
            material_ids.append(strip_mats[ring])
    if cap_start:faces.append(tuple(range(n-1,-1,-1)));material_ids.append(0)
    if cap_end:faces.append(tuple(range((len(rings)-1)*n,len(rings)*n)));material_ids.append(0)
    mesh=bpy.data.meshes.new(name+' manufactured mesh');mesh.from_pydata(verts,[],faces)
    for mat in mats:mesh.materials.append(bpy.data.materials[mat])
    for face,mat in zip(mesh.polygons,material_ids):face.material_index=mat
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.faces.ensure_lookup_table()
    if cap_start and len(rings)>1:
        outward=sum((Vector(mapper(*p)) for p in rings[0]),Vector())/n-sum((Vector(mapper(*p)) for p in rings[1]),Vector())/n
        if bm.faces[n*(len(rings)-1)].normal.dot(outward)<0:bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
    elif surface_normal and bm.faces[-1].normal.dot(Vector(surface_normal))<0:
        bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
    bm.to_mesh(mesh);bm.free();mesh.update()
    uv=mesh.uv_layers.new(name='UV0')
    lower=[min(v.co[axis] for v in mesh.vertices) for axis in range(3)]
    upper=[max(v.co[axis] for v in mesh.vertices) for axis in range(3)]
    for face in mesh.polygons:
        axes=sorted(range(3),key=lambda a:abs(face.normal[a]))[:2]
        for loop in face.loop_indices:
            p=mesh.vertices[mesh.loops[loop].vertex_index].co
            uv.data[loop].uv=atlas(p) if atlas else tuple((p[a]-lower[a])/max(upper[a]-lower[a],1e-6) for a in axes)
    weighted=[Vector() for _ in mesh.vertices];anchors=[Vector() for _ in mesh.vertices]
    for face in mesh.polygons:
        face.use_smooth=True;flat=max(abs(v) for v in face.normal)>.999999
        for i in face.vertices:
            weighted[i]+=face.normal
            if flat:anchors[i]+=face.normal
    normals=[Vector() for _ in mesh.loops]
    for face in mesh.polygons:
        flat=max(abs(v) for v in face.normal)>.999999
        for loop in face.loop_indices:
            i=mesh.loops[loop].vertex_index;n=anchors[i] if anchors[i].length_squared>1e-8 else weighted[i]
            normals[loop]=face.normal if flat else n.normalized()
    mesh.normals_split_custom_set(normals)
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);obj['MarketFitting']=1;obj['CheckoutDevice']=1
    return obj

def build_workstation():
    # Each housing is a single continuous loft, with an actual annular display opening.
    rings=[]
    for x,w,h,r in [(-11.925,.605,.405,.044),(-11.936,.647,.447,.050),(-12.055,.675,.475,.050),
                     (-12.070,.662,.462,.043),(-12.070,.566,.354,.024),(-12.056,.554,.342,.021)]:
        rings.append([(x,1.64+v,44.65+u) for u,v in outline(w,h,r)])
    housing=make_mesh('Vendor terminal beveled housing',rings,lambda x,y,z:(-x,-z,y),
        ['MarketFrame','MarketDeviceShell','Recess'],[1,1,0,0,2],True,False)
    glassring=[(-12.057,1.64+v,44.65+u) for u,v in outline(.554,.342,.021)]
    glass=make_mesh('Vendor terminal glass inset',[glassring],lambda x,y,z:(-x,-z,y),
        ['MarketDeviceDisplay'],[],False,True,
        lambda p:(.5-(-p.y-44.65)/.554,.375+(p.z-1.64+.171)/.342*.625),(1,0,0))
    # Screen and shell share the same inclination; the glass stays safely behind the bezel.
    center=Vector((12.0,-44.65,1.64))
    tilt=Matrix.Translation(center)@Matrix.Rotation(math.radians(-12),4,'Y')@Matrix.Translation(-center)
    for obj in (housing,glass):obj.data.transform(tilt)

    rings=[]
    for y,w,d,r in [(1.146,.36,.43,.055),(1.153,.40,.47,.065),(1.178,.39,.46,.065),(1.192,.33,.40,.055)]:
        rings.append([(-11.98+u,y,44.65+v) for u,v in outline(w,d,r)])
    make_mesh('Vendor terminal foot',rings,lambda x,y,z:(-x,-z,y),['MarketDeviceShell','MarketFrame'],[1,0,0])
    rings=[]
    for x,y,w,d in [(-11.98,1.184,.135,.165),(-11.96,1.22,.13,.16),(-11.925,1.30,.105,.145),(-11.94,1.39,.09,.13),(-11.965,1.465,.09,.14)]:
        rings.append([(x+u,y,44.65+v) for u,v in outline(w,d,.025)])
    make_mesh('Vendor terminal support',rings,lambda x,y,z:(-x,-z,y),['MarketFrame'],[0]*4)

    # One molded reader shell. The touch surface is recessed, not placed on an extra slab.
    rings=[]
    for y,w,d,r,slope in [(1.146,.345,.265,.043,0),(1.158,.39,.30,.053,0),
                          (1.242,.375,.285,.051,.18),(1.250,.36,.27,.046,.18),
                          (1.250,.266,.196,.031,.18),(1.241,.254,.184,.027,.18)]:
        rings.append([(-12.12+u,y+u*slope,43.83+v) for u,v in outline(w,d,r)])
    make_mesh('Reader low molded housing',rings,lambda x,y,z:(-x,-z,y),
        ['MarketFrame','MarketDeviceShell','Recess'],[0,1,1,0,2],True,False)
    ring=[(-12.12+u,1.242+u*.18,43.83+v) for u,v in outline(.254,.184,.027)]
    make_mesh('Reader inset contact pad',[ring],lambda x,y,z:(-x,-z,y),['MarketDeviceDisplay'],[],False,True,
        lambda p:(.375+(.092-(-p.y-43.83))/.184*.25,(-p.x+12.12+.127)/.254*.34375),(0,0,1))
