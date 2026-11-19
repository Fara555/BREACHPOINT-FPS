# Service gallery visual skin

This stage dresses the existing Blockout B gallery and both ramps. It does not modify the collision/cover/navigation layout.

`build_gallery.py` reads accepted deck mesh data from the separate collections in `EntryServiceZone.blend`. It writes only this source folder and `Assets/Project/Art/Environment/ServiceQuarter/ServiceGallery/Models/ServiceGallery.fbx`. Original source objects are not saved over. The `.blend` contains editable assembled components; the FBX is merged into five shared material groups.

- Platform: X=13..18, Z=40..48, existing floor height 1.5m.
- South ramp: Z=32..40, height=(Z-32)*0.1875m.
- North ramp: Z=48..56, height=(56-Z)*0.1875m.
- Clear deck: X=13.2..18, two 2m columns plus a true narrow edge strip. Deck components are projected onto the existing slope and normals recalculated.
- Ground aprons: X=12..18/Z=30..32 and Z=56..58. The south one touches the accepted entry/deck at X=12 without overlapping it.
- The existing solid parapet and lower gallery side receive attached cladding/profiles. Two warm emissive inserts are mounted directly in the parapet panels; no realtime lights are added.

Five existing HDRP materials, including the accepted PBR deck maps, are referenced. Blender report: 54,784 triangles; Unity import: 44,704. There are five renderers, no colliders, scripts, lights, animation or labels. This stage has no LOD chain or measured GPU frame-time claim.

Run Blender from the project root: `--background --factory-startup --python SourceArt/Environment/ServiceGallery/build_gallery.py`.

Unity menu: `Breachpoint > Level Design > Blockout B > Art > Create Service Gallery`, `Validate Gallery Visuals`, `Capture Service Gallery`. Bridge: `gallery-art-create`, `gallery-art-validate`, `gallery-art-capture`. The prefab is `PF_ServiceGallery.prefab`; scene root is `09 Art - Service gallery` outside the navigation hierarchy. Create is idempotent for the existing stage and does not regenerate it implicitly.

Validation is visual/asset-focused: material/prefab/mesh references, UVs/normals, slope/skirt/parapet bounds, component restrictions, triangle budget and content hashes of accepted CornerSample/EntryZone assets. A read-only gameplay checksum confirms that collision, NavMesh and actor setup are unchanged; it does not run any pathfinding or combat simulation. No combat routes, AI tests or Play Mode were run for this visual-only stage, as clarified by the user.

The local scene `Gallery.mat` backing now matches the existing dark architecture backing palette to avoid gray edges; shared accepted assets are unchanged. Scene wiring is saved with current user edits preserved. The previously saved scene file was copied to `Logs/BattlefieldGalleryArt/scene-before-gallery.unity` before authoring. Four actual HDRP views in that folder were inspected: overview, south approach, connection apron and north approach.

Future checks should follow impact: visual/material work gets visual/asset checks. Combat route checks are for gameplay, collision, navigation, actor-layout changes or explicit requests. Room enclosure, west flank and remaining graybox surfaces are future stages.
