# Interior finish — floor frozen

User direction on 2026-11-10: stop working on the floor, retain it for a separate redesign, finish the rest of the visual environment.

`build_interior.py` reads material definitions from the accepted Blender source and builds only non-floor geometry. It writes this source folder and its own FBX under `Assets/Project/Art/Environment/ServiceQuarter/InteriorFinish`. It covers:

- Workshop, the remaining entry service block and exit screen, including roof caps.
- All four interior boundary walls, wall protection profiles and service trunking.
- Closed ceiling panels, continuous backing, perimeter seals, beams, hangers and six attached diffuser housings.
- Three equipment/cover housings matching existing positions and approximate silhouettes.
- A decorative closed exit portal with an attached Orbitron EXIT header. No door interaction or level-completion behavior is added.

The current `.blend` retains editable objects. After the landmark/finish revision, export has twelve material groups: 485320 Blender triangles / 485224 Unity triangles, plus separate exit text. Shared accepted materials remain unchanged. No LOD chain, GPU frame-time measurement, light bake or Windows build is claimed.

`seal_enclosure.py` incrementally adds/repairs the same continuous ceiling backing, wall-roof seals and exit header support in the editable source, then exports it. It detects existing components by family and position to avoid duplicate surfaces. Full source rebuilding also includes these features; bevel tessellation can differ slightly in the incremental repair utility.

Unity menu: `Breachpoint > Level Design > Blockout B > Art > Finish Interior Without Floor`, `Validate Interior Visuals`, `Capture Finished Interior`. Bridge: `interior-finish-create`, `interior-finish-validate`, `interior-finish-capture`. Scene root: `12 Art - Interior walls ceiling and equipment`. Prefab is `PF_InteriorFinish.prefab`.

Lighting is in an owned `InteriorLighting.asset` VolumeProfile (priority 100, fixed exposure EV100=8, diffuse multiplier=.20, reflection multiplier=.35). Six cool overhead spots, 6000cd/range27m/outer115°/inner95°, three shadowed, supplement the unchanged existing cabinet light. Outdoor directional lights are disabled in the scene. The local Boundary/Equipment backing materials match the already-dark Architecture material. No project settings or packages change. This affects how the frozen floor is lit, but not its geometry/materials/textures.

The frozen hash includes all five earlier art asset sets plus `Ground.mat`; the pre-stage gameplay checksum protects colliders, NavMesh, actors and cover setup. Neither floor assets nor gameplay were changed. New geometry has no vertices below Y=.039 and no colliders. Validation checks prefab/material/font/mesh references, UVs/normals, allowed visual components, geometry/light budgets and bounds. A 1mm upper-bound tolerance accounts for FBX floating-point rounding at the roof. No combat, AI or pathfinding probes.

Current scene wiring is saved with in-memory user edits preserved. The pre-stage disk scene file is copied to `Logs/BattlefieldInteriorFinish/scene-before-interior.unity`. Six HDRP views were inspected there, including main room, workshop, entrance, gallery, exit and equipment. Modelled wall/roof seams are sealed; no sky lines remain at the ceiling perimeter.

The current finish is a visual environment pass. Manual play/feel, precise impact alignment on thin decorative skins, GPU profiling and final build acceptance remain separate. Floor redesign is explicitly deferred. When the user authorizes that redesign, update the frozen baseline deliberately after review; it is not a permanent ban on future floor work.

## Perimeter wall revision — two reviewed passes

`revise_walls.py` replaces 560 old perimeter objects with 22 composed bays across four walls. Quiet sheets, service bays, large HVAC intakes and a closed loading portal have different widths and proportions. Structural columns, upper diagonal braces and terminated bus runs replace the uniform 2m rhythm. This pass was exported, imported and reviewed in actual HDRP captures before the second pass; its captures are retained in `Logs/BattlefieldInteriorFinish/WallPass1`.

`finish_walls.py` moves bus runs above the intakes, adds three gallery-access maintenance hatches and a north electrical bypass cabinet with attached supply trunk. WallDark separates the new perimeter from existing equipment finishes. WallCeramic and WallWarning provide restrained contrast. Only objects tagged WallRevision/WallRefinement are revised; existing roof, block equipment and floor are preserved.

Full rebuilding order: `build_interior.py`, optional `seal_enclosure.py`, `revise_walls.py`, `finish_walls.py`. Incremental wall scripts read the current editable blend. Then run Unity bridge `interior-wall-update` followed by `interior-wall-surface`, `interior-finish-validate`, `interior-finish-capture`. Surface baking creates local 512px base color, HDRP packed metallic/AO/detail-mask/smoothness and normal maps for WallDark/WallCeramic. Fine paint grain is deliberately low amplitude; sparse scratches and edge wear replace the old oversized cloudy coating. Shared accepted materials are never overwritten.

Eleven inspection views cover room composition, four perimeter directions, entry portal, workshop, gallery, exit and equipment. Six spots/three shadowed are retained without additions. Frozen art hashes/gameplay checksum pass. No Play Mode, combat or navigation probes are used for this revision.

## Current depth/composition revision

The subsequent user review rejected the broad flat wall sheets. `compose_walls.py` is now the final geometry step, after `finish_walls.py` for a complete rebuild, or directly against the editable current blend. It removes earlier WallRevision/WallRefinement/WallComposition objects, preserving every non-wall object. It retains the 22 functional zones but subdivides their widths into manufactured assemblies: folded chamfered bezels, recessed sheets, captive fasteners, a separate armored lower band, boxed structural spines, deep louver plenums, service doors with hinges/pulls and four attached pipe-riser assemblies. Sparse muted light inserts replace the large pale sheets. No new light or floor geometry is created.

Run `interior-wall-update`, `interior-wall-surface`, `interior-finish-capture`, `interior-finish-validate` after exporting. The local surface baker now uses a darker metallic coating, muted neutral inserts and an avalanche hash for fine grain. The same material/texture GUIDs are preserved. Additional `wall-close` and `north-services` views inspect player-height detail; prior wall captures are retained in `Logs/BattlefieldInteriorFinish/BeforeWallDepth`. Current geometry counts are recorded in `WallCompositionReport.json` and the Unity visual-validation report, superseding the earlier counts above.

`wall_mesh_budget.py` is included by the final composition step and can also run incrementally. It reduces redundant bevel tessellation only on the 602 new sheets/bezels, retains UVs and restores weighted vertex normals. The first full draft was 576228 triangles; the final model fits the existing 500k budget at 483716. It also places the small riser grilles in front of their cabinet lids and adds attached portal handle standoffs. It is tagged/idempotent and never reduces preserved non-wall geometry (117240 source triangles). `WallMeshBudgetReport.json` records the latest incremental export; `audit_wall_cost.py` provides per-family counts.

The former cloudy appearance was isolated with temporary map-off captures, then fixed at its source: Unity Mathf.SmoothStep takes from/to/t, so edge distance must first be normalized by the intended fade width. The previous expression kept dirt over the whole panel. The corrected mask restricts dirt to the edge strip. Final inspection cameras use TAA/24 warmup renders; this is capture-only and does not edit game cameras or project rendering settings.

## Current color hierarchy and visual noise pass

The user accepted the model quality but rejected the monochrome whole-room composition. `compose_palette.py` is the final Blender step after composition/budgeting. It only reassigns material slots, retaining PaletteOriginalMaterial for deterministic reruns. Geometry, normals, UVs and triangle count are unchanged. It does not open any floor/shared art file.

The energy module retains its accepted cyan identity. Nine contiguous sheets/inserts define warm entry and EXIT markers. Fifty workshop/cover/west-bay sheets form a light neutral equipment family. 1200 frame/fastener/rib parts get a quieter local graphite finish; 36 ceiling sheets become matte carbon. Seven scattered upper light inserts revert to the background coating. Forty-two small wall/block emissive indicators become painted labels, retaining the primary energy and EXIT lights. The unused WarmIndicator mesh group disappears; four local materials are added, giving twelve active groups overall. Protected shared materials are not recolored.

Unity `interior-palette` / Compose Interior Color Hierarchy creates/updates four owned HDRP/Lit materials using the owned fine normal/mask maps, rebakes the owned WallDark/WallCeramic maps with neutral hues and weaker scratch contrast, remaps/imports the model and saves scene overrides. It also replaces inherited blue outdoor ambience with an owned neutral GradientSky (exposure11, top .14/.15/.16, middle .22/.215/.20, bottom .07/.075/.08), sets fixed camera exposure6.8, and balances the same six spots at18000cd with restrained neutral/warm/cool tints. Middle gallery/workshop spots are aimed at their respective surfaces; three shadowed lights and all positions/ranges remain unchanged. Ceiling emissive color is .65/.62/.56 to reduce dominant white highlights. No extra lights or global pipeline settings. These lighting changes affect floor appearance but never change its assets or geometry.

After `interior-palette`, run `interior-finish-capture` separately after the Editor update for settled lighting history, then `interior-finish-validate`. Baseline views are in `Logs/BattlefieldInteriorFinish/BeforePalette`; material assignments are in PaletteCompositionReport.json, binding/light evidence in Logs/BattlefieldInteriorFinish. Temporary unlit-color/culling/intensity experiments were removed; the capture scene-mask behavior is unchanged. No play/combat/navigation tests.

## Landmark and coating refinement after palette acceptance

The user accepted the room hierarchy. `finish_landmarks.py` is the next final Blender step, after `compose_palette.py`. It adds 20 attached, beveled/weighted-normal parts: a broad loading-portal canopy with warm identity band and boxed side jambs; a deep EXIT surround with protective shoes, collars and two recessed vertical guide diffusers. An integrated front header diffuser replaces the old separate EXIT underline, preventing doubled green lines. Close low-angle inspection found that the first header clipped the sign, so the final canopy was lowered to Y4.35 (height .20); the EXIT text is fully visible. No new text or actual lights.

The script removes only its own LandmarkFinish objects on rerun and the replaced old underline. Editable source stays unmerged; export joins the same twelve material families. Net increase is 1604 triangles, retaining the unchanged 500k limit. All floor and earlier accepted art assets remain protected. Existing light colors, intensities, directions and Volume settings are retained from the accepted palette pass.

`interior-palette` also bakes dedicated 512px base/mask/normal maps for WallWarm and InteriorNeutral using stable GUIDs. These add subtle paint grain, sparse scratches and edge-localized wear, variable metallic/smoothness response and a restrained .1 HDRP coat mask. Accepted material Color values are converted to linear before encoding sRGB albedo, preserving the accepted brightness rather than doubling it when replacing a color property with a texture. Background colors and decorative density are not expanded. All maps are local to InteriorFinish; no shared or floor material is modified.

Current full build order ends with compose_walls.py (includes wall_mesh_budget.py), compose_palette.py, finish_landmarks.py. In Unity run interior-palette, then a separate interior-finish-capture and interior-finish-validate. For geometry-only landmark reruns after materials exist, interior-wall-update suffices. BeforeLandmarks holds the accepted prior captures; LandmarkFinishReport.json records current geometry scope. No manual scene wiring, gameplay changes, new dependencies, Play Mode or combat/navigation tests.

## Floor status after explicit redesign request

The user has now lifted the historical floor freeze. QuietFloor/root13 replaces the active deck, with previous mixed floor renderers archived disabled and their bodies retained separately. InteriorFinish geometry/materials/lighting and all archived original floor files remain unchanged, so existing protected hashes still pass. The new floor is editable under SourceArt/Environment/QuietFloor and its own asset folder; use its commands/docs rather than reviving the noisy legacy deck builders. See Docs/Development/BattlefieldQuietFloor.md.
