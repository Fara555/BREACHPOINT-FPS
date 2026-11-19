"""Quiet manufactured exterior facing for the accessible market elevations."""
import bpy,bmesh,json
from pathlib import Path
SOURCE=Path(__file__).resolve().parent;PROJECT=SOURCE.parents[2]
OUT=PROJECT/'Assets/Project/Art/Environment/ServiceQuarter/MarketFacade/Models';OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version=0;bpy.context.preferences.filepaths.file_preview_type='NONE'
for name in ['FacadeEnamel','FacadeGraphite','FacadeTrim','FacadeWarm']:bpy.data.materials.new(name)
def finish(o,mat,r):
    bpy.context.view_layer.objects.active=o
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True)
    mod=o.modifiers.new('Fold radii','BEVEL');mod.width=r;mod.segments=3;bpy.ops.object.modifier_apply(modifier=mod.name)
    for f in o.data.polygons:f.use_smooth=True
    mod=o.modifiers.new('Quiet broad normals','WEIGHTED_NORMAL');mod.keep_sharp=True;bpy.ops.object.modifier_apply(modifier=mod.name)
    o.data.materials.append(bpy.data.materials[mat])
    for layer in list(o.data.uv_layers):o.data.uv_layers.remove(layer)
    for layer_name in ['UV0','SurfaceMeters']:
        uv=o.data.uv_layers.new(name=layer_name)
        for face in o.data.polygons:
            axes=sorted(range(3),key=lambda a:abs(face.normal[a]))[:2]
            for loop in face.loop_indices:
                v=o.data.vertices[o.data.loops[loop].vertex_index].co;uv.data[loop].uv=(v[axes[0]],v[axes[1]])
    o.data.uv_layers.active_index=0
    return o
def box(name,p,size,mat,r=.008):
    bpy.ops.mesh.primitive_cube_add(size=1,location=(-p[0],-p[2],p[1]));o=bpy.context.object;o.name=name
    o.scale=(size[0],size[2],size[1]);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    return finish(o,mat,r)

def cassette(place,name,u,y,w,h,face_mat,projection=.12):
    # A continuous folded rim slopes back 78mm to the broad inset field.
    def outline(width,height,c):
        return [(-width/2+c,-height/2),(width/2-c,-height/2),(width/2,-height/2+c),
                (width/2,height/2-c),(width/2-c,height/2),(-width/2+c,height/2),
                (-width/2,height/2-c),(-width/2,-height/2+c)]
    profiles=[(-.012,w,h,.055),(projection,w,h,.055),(projection,w-.13,h-.13,.045),
              (.042,w-.25,h-.25,.035)]
    vertices=[]
    for depth,pw,ph,c in profiles:
        for a,b in outline(pw,ph,c):
            if place==east:x,z=-10.925+depth,u+a
            else:
                x=u+a;z=(47+.035+depth if place.north else 34-.035-depth)
            vertices.append((-x,-z,y+b))
    faces=[tuple(range(7,-1,-1))]+[(j*8+i,j*8+(i+1)%8,(j+1)*8+(i+1)%8,(j+1)*8+i) for j in range(3) for i in range(8)]+[tuple(24+i for i in range(8))]
    whole=bpy.data.meshes.new(name+' folded profile');whole.from_pydata(vertices,[],faces)
    whole.polygons[-1].material_index=1
    bm=bmesh.new();bm.from_mesh(whole);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(whole);bm.free()
    # Split only by finish for the four shared export groups; no overlapping decorative planes.
    for index,mat in [(0,'FacadeTrim'),(1,face_mat)]:
        polygons=[tuple(f.vertices) for f in whole.polygons if f.material_index==index]
        mesh=bpy.data.meshes.new(name+' '+mat);mesh.from_pydata(vertices,[],polygons)
        bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS');bm.to_mesh(mesh);bm.free()
        o=bpy.data.objects.new(name+' '+mat,mesh);bpy.context.collection.objects.link(o);finish(o,mat,.005)
    bpy.data.meshes.remove(whole)
def east(name,u,y,w,h,d,mat,x=-10.925,r=.008):return box(name,(x,y,u),(d,h,w),mat,r)
def end(name,north,u,y,w,h,d,mat,offset=.035,r=.008):
    z=47+offset if north else 34-offset
    return box(name,(u,y,z),(w,h,d),mat,r)
def bay(place,name,a,b):
    u=(a+b)/2;w=b-a
    place(name+' shadow bed',u,2.50,w,4.94,.026,'FacadeTrim')
    place(name+' folded impact sheet',u,.63,w-.030,1.08,.050,'FacadeGraphite')
    cassette(place,name+' folded enamel cassette',u,2.29,w-.050,2.16,'FacadeEnamel')
    cassette(place,name+' deep upper casing',u,4.11,w-.050,1.14,'FacadeGraphite',.09)
    place(name+' skirting',u,.065,w-.016,.098,.070,'FacadeTrim')
    place(name+' impact cap',u,1.19,w-.016,.064,.160,'FacadeTrim')

# Four broad bays replace the tall monolithic rhythm without dense bolts or repeated trim grids.
for i,(a,b) in enumerate([(34.05,36.435),(36.435,38.82),(42.18,44.565),(44.565,46.95)]):
    bay(east,'East facade bay '+str(i),a,b)
for a,b in [(34.05,38.82),(42.18,46.95)]:
    east('East head closure',(a+b)/2,4.87,b-a,.25,.20,'FacadeTrim',x=-10.900,r=.018)
    east('East upper shadow fold',(a+b)/2,3.46,b-a,.080,.040,'FacadeTrim')
# Window surround stays clear of the accepted sill, shutter and opening.
east('Window upper field',40.5,4.19,3.36,1.48,.065,'FacadeGraphite')
east('Window head closure',40.5,4.87,3.36,.25,.20,'FacadeTrim',x=-10.900,r=.018)
east('Market sign attached plaque',40.5,3.77,3.12,.62,.050,'FacadeTrim',x=-10.8765,r=.018)
east('Window lower service bed',40.5,.59,3.36,1.10,.060,'FacadeTrim',x=-10.948)
for z in [39.81,41.19]:
    east('Window replaceable maintenance hatch',z,.58,1.30,.67,.040,'FacadeGraphite',x=-10.910,r=.016)
    east('Hatch recessed pull well',z,.81,.19,.047,.014,'FacadeTrim',x=-10.883,r=.005)
    east('Hatch folded pull lip',z,.785,.17,.018,.022,'FacadeGraphite',x=-10.872,r=.005)
for z in [38.835,42.165]:east('Window skin return',z,2.22,.105,2.23,.060,'FacadeTrim',x=-10.923,r=.012)
east('Window low skirting',40.5,.065,3.36,.098,.070,'FacadeTrim',x=-10.948)

for north in [False,True]:
    place=lambda name,u,y,w,h,d,mat:end(name,north,u,y,w,h,d,mat)
    place.north=north
    for a,b in [(-17.94,-15.96),(-12.84,-11.06)]:bay(place,'End portal return',a,b)
    # All cladding over the doorway stays above the existing lintel.
    end('Portal head joint bed',north,-14.40,4.19,3.12,1.48,.026,'FacadeTrim')
    end('Portal head service field',north,-14.40,4.14,3.09,1.27,.050,'FacadeGraphite')
    end('Portal top closure',north,-14.50,4.87,6.88,.25,.20,'FacadeTrim',offset=.060,r=.018)
    end('Portal sparse identity inset',north,-14.40,3.54,.72,.040,.028,'FacadeWarm',offset=.068,r=.005)
    for x in [-15.94,-12.86]:end('Portal lining return fold',north,x,1.63,.095,3.20,.070,'FacadeTrim',offset=.035,r=.012)
    # Corner posts bridge the perpendicular sheets, with 20mm separation of their visible planes.
    z=46.990 if north else 34.010
    box('Folded east corner post',(-10.930,2.50,z),(.300,4.96,.380),'FacadeTrim',.018)
    end('West terminal corner fold',north,-17.96,2.50,.12,4.96,.10,'FacadeTrim',offset=.050,r=.014)

# Apertures and contact planes are checked before export; no floor/roof/door geometry is generated.
for o in bpy.context.scene.objects:
    for v in o.data.vertices:
        p=o.matrix_world@v.co;x,y,z=-p.x,p.z,-p.y
        assert .015<=y<=5.001,(o.name,y)
        if x>-11.20 and 39.08<z<41.92:assert not 1.29<y<3.09,(o.name,(x,y,z))
        if z<34.20 or z>46.80:
            if -15.79<x<-13.01:assert y>3.20,(o.name,(x,y,z))
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'MarketFacade.blend'))
groups={}
for o in list(bpy.context.scene.objects):groups.setdefault(o.data.materials[0].name,[]).append(o)
for mat,parts in groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts:o.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]
    if len(parts)>1:bpy.ops.object.join()
    o=bpy.context.object;o.name='SM_MarketFacade_'+mat
    mod=o.modifiers.new('Export triangles','TRIANGULATE');mod.keep_custom_normals=True;bpy.ops.object.modifier_apply(modifier=mod.name)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(OUT/'MarketFacade.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,mesh_smooth_type='FACE')
report={'triangles':sum(len(o.data.polygons) for o in bpy.context.scene.objects),'materials':len(groups),'door_window_envelope_audit':True,'existing_floor_roof_and_interior_untouched':True}
(SOURCE/'FacadeReport.json').write_text(json.dumps(report,indent=2));print('FACADE_REPORT',json.dumps(report))
