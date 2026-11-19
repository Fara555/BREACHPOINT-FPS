"""Encode editor inspection frames with Blender's bundled video encoder; no game dependency."""
import bpy
from pathlib import Path

project=Path(__file__).resolve().parents[3]
folder=project/'Logs/BattlefieldQuietFloor/CameraSweep'
frames=sorted(folder.glob('frame-*.png'))
assert len(frames)==48,'Capture the persistent-camera sequence first'
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene
editor=scene.sequence_editor_create()
strips=editor.strips if hasattr(editor,'strips') else editor.sequences
strip=strips.new_image('Floor camera inspection',filepath=str(frames[0]),channel=1,frame_start=1)
for path in frames[1:]:strip.elements.append(path.name)
scene.frame_start=1;scene.frame_end=len(frames)
scene.render.resolution_x=1280;scene.render.resolution_y=720;scene.render.resolution_percentage=100
scene.render.fps=24;scene.render.use_sequencer=True
scene.view_settings.view_transform='Standard';scene.view_settings.look='None'
scene.view_settings.exposure=0;scene.view_settings.gamma=1
scene.render.image_settings.media_type='VIDEO'
scene.render.image_settings.file_format='FFMPEG'
scene.render.ffmpeg.format='MPEG4';scene.render.ffmpeg.codec='H264'
scene.render.ffmpeg.constant_rate_factor='HIGH'
scene.render.filepath=str(folder/'floor-sweep.mp4')
bpy.ops.render.render(animation=True)
print('FLOOR_SWEEP_VIDEO',scene.render.filepath,flush=True)
