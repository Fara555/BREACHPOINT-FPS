# Energy service loop deck

Visual-only continuation around the energy module, 2026-11-10.

The generator reads accepted module mesh data from `EntryServiceZone.blend`, ignoring collection display offsets. It does not overwrite previous sources or assets. Rigidly rotated 2x1m closures retain round fasteners; one real 1x1m closure is modeled for the last corner.

Four non-overlapping regions:

- West: X=-11..-6, Z=32..50, between workshop and energy module.
- South: X=-6..4, Z=32..36, joining the original corner deck at X=4.
- North: X=-6..4, Z=46..50.
- East: X=4..13, Z=39..50, joining the original deck at Z=39 and gallery side at X=13.

Total: 58 full 2x2m cells, 18 half-depth 2x1m cells and one 1x1m closure; 269m². Closed service hatches are kept clear of the loading-equipment footprint; ordinary floor can continue under the existing equipment, as in a normal assembled floor. The geometry stays within the existing ground plane's visual surface offset. It does not change collision, cover, navigation or actor setup.

Run Blender from the project root: `--background --factory-startup --python SourceArt/Environment/ServiceLoop/build_loop.py`. `ServiceLoop.blend` retains editable components; FBX is merged into three accepted material groups (DeckMetal, Recess, BrushedSteel). `ModelReport.json` contains all placements and export counts.

Unity prefab: `Assets/Project/Art/Environment/ServiceQuarter/ServiceLoop/PF_ServiceLoop.prefab`. Scene root: `11 Art - Energy service loop deck`. Menus under `Breachpoint > Level Design > Blockout B > Art`: Create Service Loop Deck, Validate Service Loop Visuals, Capture Service Loop Deck. Bridge commands: `service-loop-create`, `service-loop-validate`, `service-loop-capture`.

Current Blender count: 98,816 triangles. Current imported Unity count: 79,328, three renderers, shared PBR maps, no new textures/lights/colliders/scripts. Triangle budget is 130k; no GPU frame-time or Windows build claim. No LOD chain has been created for this floor yet.

Visual validation checks references, HDRP materials, UVs/normals, region and height bounds, energy/old-deck footprint exclusions, component restrictions and content hashes protecting the previous four asset sets. A read-only checksum compares gameplay data to its pre-stage state; no combat, AI or pathfinding tests are run. Four HDRP views in `Logs/BattlefieldServiceLoop` were inspected. The saved scene file before authoring is backed up there; current in-memory user edits were preserved when saving new scene wiring.

The whole room enclosure, workshop cladding, remaining outer floor and cover-prop art are future stages. The current floor keeps the established modular finish and adds no detached decoration.
