# Entry service zone

First bounded expansion after the user accepted the energy-corner sample on 2026-11-07.

The accepted sample is unchanged. `build_entry.py` reads its editable Blender source, copies three complete deck variants and exports them into this zone's own model folder. It never saves over the accepted Blender/FBX/prefab/materials. An additional 2x1m closure module is modeled separately; screw heads are not stretched. Five existing HDRP materials are shared, including the authored deck PBR maps.

The stage adds 14 full 2x2m cells and six 2x1m closures (68m²) in X=4..12, Z=21..31, excluding X=4..10/Z=28..30 occupied by the entry sight screen. The north edge meets the accepted deck at Z=31 with the same seam spacing. The existing 6x4x2m screen receives formed wall cassettes, gaskets/profiles, a closed side service hatch and one small attached emissive indicator. No new realtime lights, text, colliders, navigation, AI or interaction scripts.

- Blender: `EntryServiceZone.blend`, with separate collections for modules and the positioned wall.
- Generator: `build_entry.py`; run from the project root with Blender `--background --factory-startup --python SourceArt/Environment/EntryServiceZone/build_entry.py`.
- Export report: `ModelReport.json` (individual model counts, not placed stage totals).
- Unity models: `Assets/Project/Art/Environment/ServiceQuarter/EntryZone/Models`.
- Unity prefab: `Assets/Project/Art/Environment/ServiceQuarter/EntryZone/PF_EntryServiceZone.prefab`.
- Scene root: `08 Art - Entry service zone` in BattlefieldBlockout, outside the navigation hierarchy.

Editor menu: `Breachpoint > Level Design > Blockout B > Art > Create Entry Service Zone`, `Rebuild Entry Geometry`, `Validate Entry Service Zone`, `Capture Entry Service Zone`.

Bridge commands: `entry-art-create`, `entry-art-rebuild`, `entry-art-validate`, `entry-art-capture`. Create is idempotent and preserves an existing authored stage. Explicit Rebuild updates only this stage's five mesh assets while preserving their GUIDs. Rebuild after exporting the module/wall FBXs; it remaps materials to the accepted sample and does not regenerate its surface maps.

Validation protects a SHA-256 hash of all accepted sample asset contents via `AcceptedCorner.sha256.txt`, the established gameplay fingerprint, prefab wiring, UVs/normals, visual-only components, bounds, the excluded screen footprint and the 60k-triangle/five-renderer budget. Current imported placed stage: 41,900 triangles and five renderers. No claims about measured GPU frame time or Windows build readiness.

The initial whole-tile attempt was rejected by bounds validation because it crossed the screen footprint; true half-depth modules resolved that overlap. Current scene wiring is saved, including existing in-memory user edits, without discarding/reloading them. The previously saved scene file was copied to `Logs/BattlefieldEntryArt/scene-before-entry.unity` before authoring. Review captures and validation are in the same Logs folder.

The target remains an enclosed technical interior. This stage does not finish the room enclosure or the whole level. Inspect wall-panel scale, the turn around the sight screen and the deck connection in normal movement before extending to another zone.
All three real-controller combat smoke routes passed after stage wiring (runtimeIssues=0); the known WeaponAmmoEmission reload-segment warning remains. Human feel/visual acceptance of this new stage remains open. Accepted base is already user-approved.
