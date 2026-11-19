"""Bound the device rebuild to the old checkout assemblies and verify their seating/facing."""
import bpy,json
from pathlib import Path
SOURCE=Path(__file__).resolve().parent;PROJECT=SOURCE.parents[2]
PREFIXES=('Vendor terminal ','Terminal quiet status','Terminal physical confirmation key','Terminal head','Reader ')

def snapshot(path):
    bpy.ops.wm.open_mainfile(filepath=str(path));result={}
    for obj in bpy.context.scene.objects:
        if obj.type!='MESH':continue
        mesh=obj.data;mesh.update()
        result[obj.name]={'verts':[[round(x,7) for x in v.co] for v in mesh.vertices],
            'faces':[list(p.vertices) for p in mesh.polygons],
            'matrix':[list(row) for row in obj.matrix_world],
            'materials':[m.name for m in mesh.materials],
            'uv0':[list(v.uv) for v in mesh.uv_layers[0].data]}
    return result

before=snapshot(PROJECT/'Logs/BattlefieldMarketRoom/BeforeDesk/MarketRoom.blend')
after=snapshot(SOURCE/'MarketRoom.blend')
assert set(after).issubset(before),'Unrequested new objects were introduced'
unrelated=[]
for name,old in before.items():
    if name.startswith(PREFIXES):continue
    new=after[name]
    assert new['faces']==old['faces'] and new['matrix']==old['matrix'] and new['materials']==old['materials'],('Unrelated object changed',name)
    for field in ('verts','uv0'):
        assert len(new[field])==len(old[field]) and all(abs(a-b)<1e-6 for p,q in zip(new[field],old[field]) for a,b in zip(p,q)),('Unrelated geometry/UV changed',name)
    unrelated.append(name)
devices=[o for o in bpy.context.scene.objects if o.get('CheckoutDevice')]
assert len(devices)==6,'Exactly the two devices, split into six coherent components, are expected'
for obj in devices:
    assert all(face.area>1e-9 for face in obj.data.polygons),('Degenerate device face',obj.name)
    assert all(-12.45<=-v.co.x<=-11.65 and 1.145<=v.co.z<=1.95 and 43.65<=-v.co.y<=45.05 for v in obj.data.vertices),('Device left counter footprint',obj.name)
screen=bpy.data.objects['Vendor terminal glass inset'].data
reader=bpy.data.objects['Reader inset contact pad'].data
assert screen.polygons[0].normal.x>.97,'Terminal glass faces away from the operator'
assert reader.polygons[0].normal.z>.97,'Reader pad faces downward'
foot=bpy.data.objects['Vendor terminal foot'].data
assert abs(min(v.co.z for v in foot.vertices)-1.145)<.002,'Terminal base does not seat on the worktop'
report={'old_source_objects':len(before),'new_source_objects':len(after),'new_devices':0,'device_components':6,
    'obsolete_overlays_removed':len(set(before)-set(after)),'unrelated_meshes_unchanged':len(unrelated),
    'counter_footprint_clear':True,'screen_facing_correct':True,'base_seated':True,'floor_unchanged':True}
(SOURCE/'WorkstationAudit.json').write_text(json.dumps(report,indent=2));print('WORKSTATION_AUDIT',json.dumps(report),flush=True)
