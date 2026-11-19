import bpy,collections,json
from pathlib import Path
source=Path(__file__).resolve().parent
bpy.context.preferences.filepaths.file_preview_type='NONE'
bpy.ops.wm.open_mainfile(filepath=str(source/'InteriorFinish.blend'))
cost=collections.Counter()
for obj in bpy.context.scene.objects:
    if obj.type!='MESH':continue
    obj.data.calc_loop_triangles()
    name=obj.name.split('.')[0] if obj.get('WallComposition') else 'Preserved non-wall geometry'
    cost[name]+=len(obj.data.loop_triangles)
print('WALL_COST',json.dumps(cost.most_common()),flush=True)
