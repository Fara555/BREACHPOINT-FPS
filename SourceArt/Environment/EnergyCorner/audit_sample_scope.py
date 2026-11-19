"""Check the editable sample for geometry outside its two intended regions."""
import bpy
import json
from pathlib import Path

source=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(source/'EnergyCorner.blend'))
outside=[]
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
for obj in meshes:
    count=0
    for vertex in obj.data.vertices:
        p=obj.matrix_world@vertex.co
        x,y,z=4-p.x,p.z,36-p.y
        module=-2.2<=x<=4.4 and -.02<=y<=4.6 and 35.6<=z<=42.2
        deck=4<=x<=12 and -.02<=y<=.04 and 31<=z<=39
        count+=not(module or deck)
    if count: outside.append({'name':obj.name,'outside_vertices':count})
materials=sorted({m.name for obj in meshes for m in obj.data.materials if m})
report={'mesh_objects':len(meshes),'outside_objects':outside,'materials':materials,
        'scope':'energy module and 8x8m deck only; gallery and loose markings excluded'}
(source/'SampleScopeAudit.json').write_text(json.dumps(report,indent=2))
print('SAMPLE_SCOPE_AUDIT',json.dumps(report))
assert not outside, 'Stray geometry outside sample regions'
assert 'MarkingPaint' not in materials and 'Concrete' not in materials, 'Legacy materials remain'
