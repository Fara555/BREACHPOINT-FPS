"""Prove that the counter material pass changes no geometry and no other device/room finish."""
import bpy,json
from pathlib import Path
SOURCE=Path(__file__).resolve().parent;PROJECT=SOURCE.parents[2]
ALLOWED=('Market counter base','Market counter dark plinth','Market counter durable top','Market counter identity inset','Counter top folded nose','Counter rear splash return','Counter captive pull','Counter removable front')

def read(path):
    bpy.ops.wm.open_mainfile(filepath=str(path));objects={}
    for obj in bpy.context.scene.objects:
        if obj.type!='MESH':continue
        mesh=obj.data;mesh.update()
        objects[obj.name]={'verts':[tuple(v.co) for v in mesh.vertices],'faces':[list(f.vertices) for f in mesh.polygons],
            'matrix':[list(row) for row in obj.matrix_world], 'materials':[m.name for m in mesh.materials],
            'uv':{uv.name:[tuple(p.uv) for p in uv.data] for uv in mesh.uv_layers}}
    return objects

before=read(PROJECT/'Logs/BattlefieldMarketRoom/BeforeCounterSurface/MarketRoom.blend')
after=read(SOURCE/'MarketRoom.blend')
assert set(after)==set(before),'Object inventory changed'
changed=[]
for name,current in after.items():
    old=before[name]
    assert current['faces']==old['faces'] and current['matrix']==old['matrix'],('Geometry/transform changed',name)
    assert len(current['verts'])==len(old['verts']) and all(abs(a-b)<1e-6 for p,q in zip(current['verts'],old['verts']) for a,b in zip(p,q)),('Vertex positions changed',name)
    assert current['uv'].keys()==old['uv'].keys(),('UV channels changed',name)
    for uv,data in current['uv'].items():
        assert len(data)==len(old['uv'][uv]) and all(abs(a-b)<1e-5 for p,q in zip(data,old['uv'][uv]) for a,b in zip(p,q)),('UV mapping changed',name)
    if current['materials']!=old['materials']:
        center_z=-sum(v[1] for v in current['verts'])/len(current['verts'])
        assert name.startswith(ALLOWED) and 43<center_z<45.4,('Out-of-scope material changed',name)
        changed.append(name)
assert len(changed)==10,('Expected ten existing counter meshes to receive finishes',changed)
report={'object_count':len(after),'geometry_transforms_uv_unchanged':True,'changed_counter_materials':changed,
    'devices_window_other_counter_floor_unchanged':True,'no_new_objects':True}
(SOURCE/'CounterSurfaceAudit.json').write_text(json.dumps(report,indent=2));print('COUNTER_SURFACE_AUDIT',json.dumps(report),flush=True)
