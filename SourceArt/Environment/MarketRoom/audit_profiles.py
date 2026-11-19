"""Verify that profile refinement only edits the named existing parts and leaves the floor intact."""
import bpy,hashlib,json,math
from pathlib import Path
SOURCE=Path(__file__).resolve().parent;PROJECT=SOURCE.parents[2]
ALLOWED=('Supply cassette locked lid','Storage formed door pull','Reader low molded housing','Reader inset contact pad','Reader quiet confirmation strip')

def snapshot(path):
    bpy.ops.wm.open_mainfile(filepath=str(path));result={}
    for obj in bpy.context.scene.objects:
        if obj.type!='MESH':continue
        mesh=obj.data
        geometry={'verts':[[round(x,7) for x in v.co] for v in mesh.vertices], 'faces':[list(f.vertices) for f in mesh.polygons]}
        uvs={uv.name:[tuple(round(x,7) for x in v.uv) for v in uv.data] for uv in mesh.uv_layers}
        normals=[tuple(round(x,6) for x in n.vector) for n in mesh.corner_normals]
        result[obj.name]={'geometry':geometry,'uv':uvs,'normals':normals,
            'transform':[list(row) for row in obj.matrix_world], 'materials':[m.name for m in mesh.materials]}
    return result

before=snapshot(PROJECT/'Logs/BattlefieldMarketRoom/BeforeProfile/MarketRoom.blend')
after=snapshot(SOURCE/'MarketRoom.blend')
assert set(before)==set(after),'Object inventory changed'
changed=[];physical=[];metric_details=[]
for name,current in after.items():
    previous=before[name]
    assert current['transform']==previous['transform'],('Object transform changed',name)
    assert current['materials']==previous['materials'],('Material inventory changed',name)
    if current['geometry']!=previous['geometry']:
        assert name.startswith(ALLOWED),('Unrelated geometry changed',name)
        changed.append(name)
    if name.startswith('Market calm floor sheet'):
        assert current==previous,('Floor changed',name)
    elif not name.startswith('Storage formed door pull'):
        for uv,data in previous['uv'].items():
            updated=current['uv'][uv]
            assert len(updated)==len(data) and all(abs(a-b)<1e-6 for p,q in zip(updated,data) for a,b in zip(p,q)),('Base UV changed',name)

# Check the actual saved UV1 metric on broad faces, independent of the authoring loop.
for obj in bpy.context.scene.objects:
    if obj.type!='MESH' or not any(m.name in ('MarketFrame','MarketWorktop','MarketWarm') for m in obj.data.materials):continue
    mesh=obj.data;uv=mesh.uv_layers.get('SurfaceMeters');assert uv is not None,obj.name
    for face in mesh.polygons:
        if face.area<.01:continue
        loops=list(face.loop_indices)
        for i in range(1,len(loops)-1):
            a,b,c=loops[0],loops[i],loops[i+1]
            ta,tb,tc=[uv.data[v].uv.copy() for v in (a,b,c)]
            p,q,r=[mesh.vertices[mesh.loops[v].vertex_index].co for v in (a,b,c)]
            du,dv=tb-ta,tc-ta;det=du.x*dv.y-du.y*dv.x
            if abs(det)<1e-10:continue
            tangent=((q-p)*dv.y-(r-p)*du.y)/det
            bitangent=((r-p)*du.x-(q-p)*dv.x)/det
            error=max(abs(tangent.length-1),abs(bitangent.length-1));physical.append(error)
            metric_details.append((error,obj.name,face.index,tangent.length,bitangent.length));break
assert changed,'No existing profile changed'
assert physical and max(physical)<.001,('Detail UV density is inconsistent',sorted(metric_details,reverse=True)[:5])
report={'object_count':len(after),'no_added_or_removed_objects':True,'unchanged_object_transforms':True,
    'changed_profiles':sorted(changed),'unrelated_geometry_unchanged':True,'floor_mesh_uv_normals_unchanged':True,
    'material_inventory_unchanged':True,'physical_uv_faces_tested':len(physical),'max_metric_error':max(physical)}
(SOURCE/'ProfileAudit.json').write_text(json.dumps(report,indent=2));print('PROFILE_AUDIT',json.dumps(report),flush=True)
