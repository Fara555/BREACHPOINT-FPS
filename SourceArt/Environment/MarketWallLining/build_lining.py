"""Quiet interior wall system, preserving market openings and existing furniture."""
import bpy,bmesh,math,json
from pathlib import Path
from mathutils import Vector
SOURCE=Path(__file__).resolve().parent;PROJECT=SOURCE.parents[2]
OUT=PROJECT/'Assets/Project/Art/Environment/ServiceQuarter/MarketWallLining/Models';OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version=0;bpy.context.preferences.filepaths.file_preview_type='NONE'
for name in ['LiningCeramic','LiningGraphite','LiningTrim','LiningWarm']:bpy.data.materials.new(name)
def box(name,p,size,mat='LiningCeramic',radius=.008):
    bpy.ops.mesh.primitive_cube_add(size=1,location=(-p[0],-p[2],p[1]));o=bpy.context.object
    o.name=name;o.scale=(size[0],size[2],size[1]);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    if radius:
        mod=o.modifiers.new('Formed edge radius','BEVEL');mod.width=radius;mod.segments=3;bpy.ops.object.modifier_apply(modifier=mod.name)
    for f in o.data.polygons:f.use_smooth=True
    mod=o.modifiers.new('Broad face normals','WEIGHTED_NORMAL');mod.keep_sharp=True;bpy.ops.object.modifier_apply(modifier=mod.name)
    o.data.materials.append(bpy.data.materials[mat])
    for layer in list(o.data.uv_layers):o.data.uv_layers.remove(layer)
    uv=o.data.uv_layers.new(name='UV0');physical=o.data.uv_layers.new(name='SurfaceMeters')
    for face in o.data.polygons:
        axes=sorted(range(3),key=lambda a:abs(face.normal[a]))[:2]
        for loop in face.loop_indices:
            p=o.data.vertices[o.data.loops[loop].vertex_index].co
            physical.data[loop].uv=(p[axes[0]],p[axes[1]])
            # Meter-space maps contain restrained surface grain rather than a decorative grid.
            uv.data[loop].uv=physical.data[loop].uv
    o.data.uv_layers.active_index=0
    return o

# Side frames: parameter u runs along the room. d is distance into the room.
def side(name,west,u,y,w,h,depth,mat,inset=0,r=.008):
    x=(-17.38+inset if west else -11.28-inset)
    return box(name,(x,y,u),(depth,h,w),mat,r)
def end(name,north,u,y,w,h,depth,mat,inset=0,r=.008):
    z=(46.72-inset if north else 34.28+inset)
    return box(name,(u,y,z),(w,h,depth),mat,r)
def vertical_bay(place,name,a,b,upper=False):
    middle=(a+b)/2;width=b-a
    place(name+' joint bed',middle,2.41,width,4.62,.018,'LiningTrim',.012)
    place(name+' lower impact panel',middle,.62,width-.026,.84,.036,'LiningGraphite',.038)
    place(name+' large enamel sheet',middle,2.24,width-.026,2.34,.036,'LiningCeramic',.038)
    place(name+' upper service casing',middle,4.10,width-.026,1.22,.070,'LiningGraphite',.086)
    place(name+' recessed skirting',middle,.064,width-.014,.096,.045,'LiningTrim',.048)

for west in [True,False]:
    place=lambda name,u,y,w,h,d,mat,inset=0,r=.008:side(name,west,u,y,w,h,d,mat,inset,r)
    place('Upper perimeter closure',40.5,4.755,12.20,.13,.070,'LiningGraphite',.086)
    intervals=[(34.40,37.30),(37.30,39.43),(41.57,43.70),(43.70,46.60)] if west else [(34.40,36.62),(36.62,38.84),(42.16,44.38),(44.38,46.60)]
    for i,(a,b) in enumerate(intervals):vertical_bay(place,('Storage' if west else 'Counter')+' bay '+str(i),a,b)
    # Two continuous datum folds tie the wall system to its corners; no dense trim grid.
    for a,b in ([(34.40,39.43),(41.57,46.60)] if west else [(34.40,38.84),(42.16,46.60)]):
        place('Impact rail cap',(a+b)/2,1.062,b-a,.028,.052,'LiningTrim',.052,.005)
        place('Upper band shadow reveal',(a+b)/2,3.455,b-a,.050,.022,'LiningTrim',.040,.005)
    if west:
        place('Service door header backing',40.5,3.82,2.14,1.80,.018,'LiningTrim',.012)
        place('Service door head enamel sheet',40.5,3.18,2.14,.46,.036,'LiningCeramic',.038)
        place('Service door upper casing',40.5,4.10,2.14,1.22,.070,'LiningGraphite',.086)
        place('Service door upper datum',40.5,3.455,2.14,.05,.022,'LiningTrim',.040,.005)
        # Existing service conduit at x=-17.29 remains in front of the main sheets.
        # Upper casings cover the obsolete deep backpans without touching that run.
        for z in [35.85,45.12]:
            place('Integrated ventilation well',z,4.10,1.45,.56,.026,'LiningTrim',.130,.012)
            for i in range(7):place('Vent folded louver',z,3.90+i*.066,1.34,.029,.030,'LiningGraphite',.149,.005)
    else:
        place('Window breast backing',40.5,.61,3.32,1.04,.025,'LiningTrim',.033)
        for z in [39.67,41.33]:place('Window inner impact facing',z,.62,1.63,.84,.040,'LiningGraphite',.070)
        place('Window recessed skirting',40.5,.064,3.32,.096,.045,'LiningTrim',.048)
        place('Window upper service casing',40.5,4.10,3.30,1.22,.070,'LiningGraphite',.086)
        place('Window head backing',40.5,3.50,3.30,.10,.048,'LiningTrim',.065)
        for z in [38.915,42.085]:
            place('Window inner return fold',z,2.21,.125,2.18,.16,'LiningTrim',.100,.012)

for north in [False,True]:
    place=lambda name,u,y,w,h,d,mat,inset=0,r=.008:end(name,north,u,y,w,h,d,mat,inset,r)
    place('End perimeter closure',-14.34,4.755,6.02,.13,.070,'LiningGraphite',.086)
    for a,b in [(-17.35,-15.96),(-12.84,-11.33)]:vertical_bay(place,'End return',a,b)
    place('End head joint bed',-14.40,4.10,3.12,1.22,.022,'LiningTrim',.020)
    place('End head formed field',-14.40,4.10,3.094,1.22,.070,'LiningGraphite',.086)
    # A sparse color break locates each doorway without introducing more signage/emission.
    place('Door head deep return',-14.40,3.445,3.12,.13,.10,'LiningTrim',.065)
    place('Doorway inset warm identification',-14.40,3.54,.78,.045,.030,'LiningWarm',.110,.006)
    for x in [-17.32,-11.36]:place('Folded inside corner',x,2.42,.075,4.58,.12,'LiningTrim',.060,.012)

# Audit the new lining itself: no aperture geometry or floor/ceiling slabs.
for o in bpy.context.scene.objects:
    for v in o.data.vertices:
        p=o.matrix_world@v.co;x,y,z=-p.x,p.z,-p.y
        assert y>=.015-1e-5 and y<=4.83+1e-5,(o.name,y)
        if x>-11.60 and 39.08<z<41.92:assert not 1.29<y<3.09,(o.name,(x,y,z))
        if z<34.60 or z>46.40:
            if -15.79<x<-13.01:assert y>3.20,(o.name,(x,y,z))
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'MarketWallLining.blend'))
groups={}
for o in list(bpy.context.scene.objects):groups.setdefault(o.data.materials[0].name,[]).append(o)
for mat,parts in groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts:o.select_set(True)
    bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();o=bpy.context.object;o.name='SM_MarketWallLining_'+mat
    mod=o.modifiers.new('Export triangles','TRIANGULATE');mod.keep_custom_normals=True;bpy.ops.object.modifier_apply(modifier=mod.name)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(OUT/'MarketWallLining.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,mesh_smooth_type='FACE')
report={'triangles':sum(len(o.data.polygons) for o in bpy.context.scene.objects),'materials':len(groups),'aperture_vertex_audit':True,'floor_ceiling_excluded':True}
(SOURCE/'WallLiningReport.json').write_text(json.dumps(report,indent=2));print('WALL_LINING_REPORT',json.dumps(report),flush=True)
