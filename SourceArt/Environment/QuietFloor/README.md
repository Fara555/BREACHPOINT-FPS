# Quiet full-room industrial floor

The user explicitly lifted the previous floor freeze and requested a complete replacement on 2026-11-10: the old per-tile screws, borders, grooves, anti-slip inserts and repeated hatch details produced visual shimmer. Walls, equipment, accepted palette and lighting remain protected.

`build_floor.py` creates the independent `QuietFloor.blend` and three FBX assets under `Assets/Project/Art/Environment/ServiceQuarter/QuietFloor/Models`. Existing source blends, FBX, prefabs, maps and materials are opened read-only and never overwritten.

- QuietFloor: 96 large rectangular/trimmed sheets, nominal 4x6m on the room floor, including six gallery/ramp sheets. Two flush 1.2x1.8m access covers at service edges, two small dark flush latches each. No repeated tile bolts, border strips, grooves or grille pattern.
- Joints: 40mm gaps over a low-contrast continuous bed; 1.5mm geometric bevels. Field top14mm above the unchanged collision surface, below the former most detailed tile hardware. Ramps preserve their original 0→1.5m→0 profile; platform walking surface remains1.5m plus the visual skin. Ramp sheets extend to X13, while platform sheets respect the existing X13..13.2 parapet footprint.
- Ground sheets subtract the actual energy/workshop/entry/exit-screen/gallery footprints with small cladding clearance. Flat joint bed continues below hidden structures. It does not alter their collision or placement.
- CornerBody: original energy equipment geometry/materials/UVs/normals, omitting only named `Deck ` source objects (296). Labels and the cabinet light remain on the original scene root.
- GalleryBody: original side casing, parapet and guide fixtures, omitting named deck modules/narrow floor/steel floor insert/toe-strip families (210). The original entry screen uses the existing accepted Entry_Wall.fbx directly.

The floor alone is4572 triangles/3 HDRP materials. The installed assembly including retained equipment/screen/gallery bodies is113000 imported triangles,18 mesh renderers. These are visual-only meshes with no new colliders, lights, runtime scripts or navigation bake. FloorField/FloorJoint/FloorAccess are matte coated metal, metallic .2/.05/.2; colors .28/.30/.31, .23/.245/.25, .30/.315/.32. The512px periodic normal and packed HDRP mask use a consistent2m physical UV scale, mips/trilinear/aniso8 and normal scale .8. Mask channels are metallic .2 / AO1 / detail mask1 / smoothness approximately .135-.185 (access covers slightly higher). The joint is uniformly matte, smoothness .10. Geometric specular AA is enabled on the three owned materials, variance .1/threshold .2. No albedo mottling, POM, sharp edge strips or global camera/render settings changes. The powder grain is periodic smooth noise, avoiding uncorrelated pixel sparkle and visible tiling.

Unity menus/bridge:

- Replace Noisy Floor / `quiet-floor-create`: initial creation only. Installs PF_QuietFloor under `13 Art - Quiet industrial floor`, places retained bodies at the exact original model transforms, disables only the original mixed-deck mesh renderers (keeps original labels/lights/GameObjects), preserves source prefabs and saves scene overrides.
- Update Quiet Floor Visuals / `quiet-floor-update`: reimports the three owned FBX assets, tunes the three owned materials and saves without resetting other scene work.
- Capture Quiet Floor / `quiet-floor-capture`: 14 whole-room/detail views plus floor-close, gallery-floor, ramp-transition and service-cover. Uses the accepted lighting, then copies evidence to Logs/BattlefieldQuietFloor.
- Validate Quiet Floor Visuals / `quiet-floor-validate`: checks asset hashes, gameplay/collider/NavMesh fingerprint without simulation, disabled legacy renderers, prefab/material/normal/UV references, floor height envelope and mesh budgets; also validates the unchanged accepted interior.

Rebuild with Blender5.1.2: `blender --background --factory-startup --python SourceArt/Environment/QuietFloor/build_floor.py`, then quiet-floor-update and a separate capture/validate after the Editor update. No manual scene wiring or packages required. Do not use the earlier live deck rollout commands for this replacement.

The prior accepted deck files remain an archive, so the old protection hash can still pass even though their renderers are disabled. `ProtectedArtBefore.sha256.txt` additionally protects all earlier art (including InteriorFinish lighting/materials) and Ground.mat; GameplayBefore.sha256.txt protects the current gameplay state. The new QuietFloor folder is the editable active floor. No floor-freeze baseline is bypassed or silently rewritten.

Rollback evidence: Logs/BattlefieldQuietFloor/Before images, scene-before-floor.unity and archived-renderers.txt (original hierarchy paths/enabled values). To revert visuals, remove the new scene instance and restore those original renderer values. This preserves the originals for review; it does retain their referenced legacy assets in the project.

Final evidence: FloorReport.json, visual-validation.txt, actual HDRP views including ramp joins and a flush cover. Console0 errors/0 warnings; old accepted asset hashes/gameplay checksum pass. No combat/AI/pathfinding/Play Mode runs, moving-camera human review, GPU profiling or Windows build is claimed. Thin visual skins retain the pre-existing decorative-surface impact alignment limitation.

## Wider joints and rougher coating revision

User feedback identified thin-joint shimmer during movement. Joints increased12→40mm, with a softer gray gasket contrast, preserving all96 panels/two covers and the same mesh budgets. The normal was replaced with a512px coherent periodic grain map; new linear HDRP mask varies smoothness gently rather than varying albedo. Update and initial create both bake the maps with stable GUIDs.

The old primitive Ground36x44, gallery platform and two ramp mesh renderers are also disabled now: their surfaces sat only3mm below the joint bed and are redundant for rendering. Objects/transforms/colliders remain active and untouched; the existing NavMeshSurface uses PhysicsColliders and its baked data/checksum is unchanged. primitive-renderers-before.txt records their initial enabled values for rollback in addition to archived-renderers.txt. This removes the competing backing surfaces without changing walking physics.

quiet-floor-motion / Capture Floor Camera Sweep creates48 persistent-camera HDRP/TAA frames at eye height1.7m, with forward/side translation, without Play Mode or actor simulation. encode_sweep.py uses Blender5.1's bundled video encoder (no game package) to create a2s1280x720/24fps H264 MP4 at Logs/BattlefieldQuietFloor/CameraSweep/floor-sweep.mp4. Selected start/middle/end frames and still low-angle/close views were inspected; this is editor visual evidence, not a human live-input or FPS test. Capture now also includes floor-material, for19 still views total. BeforeWiderJoints retains the previous stills. No combat/navigation/Play Mode tests or GPU profiling.
