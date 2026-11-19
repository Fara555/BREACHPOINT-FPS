"""Inspect visible horizontal contacts in the editable Blender source."""
import bpy
import bmesh
import json
from pathlib import Path
from mathutils import Vector

source=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(source/'EnergyCorner.blend'))
def bounds(obj):
    points=[obj.matrix_world@Vector(p) for p in obj.bound_box]
    return [min(p[i] for p in points) for i in range(3)],[max(p[i] for p in points) for i in range(3)]
lids=[o for o in bpy.context.scene.objects if o.name.startswith('Deck removable service hatch')]
recesses=[o for o in bpy.context.scene.objects if o.name.startswith('Deck lifting recess')]
contacts=[]
for lid in lids:
    lo,hi=bounds(lid)
    for recess in recesses:
        rlo,rhi=bounds(recess)
        overlap=min(hi[0],rhi[0])>max(lo[0],rlo[0]) and min(hi[1],rhi[1])>max(lo[1],rlo[1])
        if overlap and abs(hi[2]-rhi[2])<.000001:
            contacts.append({'lid':lid.name,'recess':recess.name,'top_metres':hi[2]})
topology=[]
for lid in lids:
    bm=bmesh.new(); bm.from_mesh(lid.data)
    euler=len(bm.verts)-len(bm.edges)+len(bm.faces)
    nonmanifold=sum(not e.is_manifold for e in bm.edges)
    bm.free()
    topology.append({'lid':lid.name,'euler':euler,'nonmanifold_edges':nonmanifold})
crossbars=[o for o in bpy.context.scene.objects if o.name.startswith('Deck lifting crossbar')]
clearances=[]
for lid in lids:
    lo,hi=bounds(lid)
    for crossbar in crossbars:
        clo,chi=bounds(crossbar)
        if clo[0]>lo[0] and chi[0]<hi[0] and clo[1]>lo[1] and chi[1]<hi[1]:
            clearances.append(round(hi[2]-chi[2],6))
report={'coplanar_visible_lifting_recesses':contacts,'count':len(contacts),'hatches':len(lids),
        'topology':topology,'handle_below_lid_metres':clearances}
(source/'HatchSurfaceAudit.json').write_text(json.dumps(report,indent=2))
print('HATCH_AUDIT',json.dumps(report))
if not contacts:
    assert len(lids)==6 and all(t['euler']==-2 and t['nonmanifold_edges']==0 for t in topology), 'Each lid must be closed with two true through-holes'
    assert len(clearances)==12 and min(clearances)>=.0015, 'Handles must remain below the visible lid surface'
