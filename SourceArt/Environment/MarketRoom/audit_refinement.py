"""Verify an existing-only model refinement against its pre-pass snapshot."""
import bpy,hashlib,json,math,sys
from pathlib import Path

SOURCE=Path(__file__).resolve().parent
PROJECT=SOURCE.parents[2]
OUTPUT=PROJECT/'Logs/BattlefieldMarketRoom/BeforeRefine'
BASELINE=OUTPUT/'model-audit.json'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'MarketRoom.blend'))
objects={};floor={};angles=[]
for obj in sorted(bpy.context.scene.objects,key=lambda o:o.name):
    if obj.type!='MESH':continue
    mesh=obj.data;mesh.update()
    geometry={'vertices':[[round(v,7) for v in obj.matrix_world@p.co] for p in mesh.vertices],
              'faces':[list(p.vertices) for p in mesh.polygons]}
    objects[obj.name]=hashlib.sha256(json.dumps(geometry,sort_keys=True).encode()).hexdigest()
    if obj.name.startswith('Market calm floor sheet'):
        floor[obj.name]=hashlib.sha256(json.dumps([(round(v.uv.x,7),round(v.uv.y,7)) for v in mesh.uv_layers.active.data]).encode()).hexdigest()
    if obj.get('BoxSurface') or len(mesh.polygons)>6:
        for face in mesh.polygons:
            if face.area<.005 or max(abs(v) for v in face.normal)<.999999:continue
            for loop in face.loop_indices:
                n=mesh.corner_normals[loop].vector
                if n.length>0:
                    angles.append((math.degrees(n.angle(face.normal)),obj.name))
angles.sort(reverse=True)
report={'object_count':len(objects),'objects':objects,'floor_uv':floor,
        'planar_corner_max_degrees':angles[0][0] if angles else 0,
        'planar_corner_mean_degrees':sum(a for a,_ in angles)/len(angles) if angles else 0,
        'worst_planar_objects':angles[:8]}
if '--baseline' in sys.argv:
    OUTPUT.mkdir(parents=True,exist_ok=True);BASELINE.write_text(json.dumps(report,indent=2))
else:
    before=json.loads(BASELINE.read_text())
    assert report['objects']==before['objects'],'Object inventory, vertex positions or topology changed'
    assert report['floor_uv']==before['floor_uv'],'Floor UV changed'
    assert report['planar_corner_mean_degrees']<before['planar_corner_mean_degrees'],'Planar shading did not improve'
    report['existing_objects_only']=True;report['geometry_unchanged']=True;report['floor_unchanged']=True
    report['before_mean_degrees']=before['planar_corner_mean_degrees']
    report['before_max_degrees']=before['planar_corner_max_degrees']
    (SOURCE/'RefinementAudit.json').write_text(json.dumps(report,indent=2))
print('MODEL_REFINEMENT_AUDIT',json.dumps({k:v for k,v in report.items() if k not in ['objects','floor_uv']}),flush=True)
