# Validation report

Candidate: **1.0.0-rc.4**, inspected 2026-10-04. Overall status: **manual release verification incomplete**. Automated results do not establish visual quality or complete parity.

## Actually exercised versions

| Editor | Executed checks | Result |
| --- | --- | --- |
| Unity 6000.4.4f1 (360f97ecca93), Windows | Storage/palette/API/rules/prefab/Undo/native hierarchy integration + graphical performance replay | 30 passed, 0 failed, 0 skipped |
| Unity 6000.0.46f1, Windows | Same functional suite; PerformanceGUI category excluded | 28 passed, 0 failed, 0 skipped |

Test Framework 1.6.0, isolated validation projects using Built-in and minimal modules. The original development project is URP 17.4.0. The extension has no render-pipeline references. HDRP was not exercised. macOS and Linux were not exercised. No other Editor versions are claimed as verified.

rc.2 regression checks: actual graphical callbacks substitute both normal and selected native tree icons and restore them on reset. Native GameObject/Create Empty creates a child under the default parent; toggling clears it on both tested versions. Package scenes are rejected. The development assembly currently has one explicit reference to each TestRunner, no optional TestAssemblies reference; the owner correction was retained. Component rendering uses explicit 16-point textures and cached tooltip content; edge tint and UI removal were checked against supplied screenshots and source, with fresh manual visual review still pending.

Covered: durable save/rename/reopen IDs, unsaved ID policy and promotion, simultaneous multi-edit Undo/Redo, transient Undo, scene moves, prefab instances and variants, prefab-stage asset identity, unsaved prefab children, prefab reorder/rename, deletion/Undo bookmarks, prefab activity overrides, unchanged runtime components/scene dirty state, appearance cloning with children, rule/manual precedence, native expansion/isolation/collapse/default parent, palette format validation/round trip/missing resources, Ctrl/Cmd modifier matching. Graphical replay measures native Hierarchy scroll/expansion/selection on 1,000 and 10,000 object scenes.

Initial diagnostic test runs exposed a wrong scene-header assertion, inability to observe native GUI in batchmode, unsupported thread-allocation counting and Unity 6.0 prefab-stage/default-parent API differences. Those were investigated rather than hidden; final results above reflect the corrected implementation and appropriate test context. Diagnostic logs remain in the development project's Artifacts folder, outside the distributed tool.

## Artifact/lifecycle gates

The lifecycle/build evidence in this paragraph was recorded for rc.1; rc.2 has its own native export and update/cold-restart smoke checks in Artifacts.

Executed in isolated Unity 6000.4.4f1 projects: native AssetDatabase.ExportPackage; native ImportPackage into a clean project; successful compilation and API smoke setup; cold-restart persistence of scene decoration, bookmark, project palette and per-user settings; successful Windows x64 player build (22.16 seconds). CompilationPipeline player assemblies and built output contain no HierarchyToolkit assembly. Tool removal preserved external settings/data. Cold restart after removal compiled cleanly and found no missing scene components. Native reinstallation completed, and another cold restart restored the same decoration, bookmark, palette and user preferences. Native reimport of the documented candidate over the installed version completed; a subsequent cold restart again preserved all four saved values. Package inspection found 26 tool-only paths with matching metadata GUIDs; migration from an older public release cannot be tested because this is the first version.

## Manual checklist (not yet passed)

Windows Computer Use could list the correct HierarchyDev window but screenshot capture timed out (`FrameArrived timed out`) and window activation timed out (`activate_window`). Thus manual visual inspection was unavailable from this session. Graphical Editor API tests ran separately and are explicitly distinguished from manual inspection.

- [ ] Palette layout, custom textures, reset, selection feedback and recursive Alt application.
- [ ] Native selection, multi-selection, drag/reparent, normal context menu, cut/copy/paste and native Duplicate with decorated children.
- [ ] Every hover key over an unselected object; no action over blank area, scene headers, search input, rename input, other windows or active drags.
- [ ] D with multiple scenes/default parents and Prefab Mode, Undo/Redo; F framing with Scene view open.
- [ ] Component popup edits, custom inspectors, repeated component ordinals, multi-edit, Undo, prefab overrides and missing scripts.
- [ ] Scene search/bookmarks, dirty-scene Save/Discard/Cancel, additive scenes and active-scene changes.
- [ ] Object bookmark chips, drag into/away from bar, deleted objects, closed scenes and prefab bookmarks.
- [ ] Multiple standard Hierarchy windows, search filtering, Prefab Mode transitions and final-row access above footer.
- [ ] Light and dark themes, 100/125/150/200% interface scaling, very narrow windows, long labels and deep nesting.
- [ ] Play Mode with normal reload and with domain reload disabled; runtime-created objects remain temporary.
- [ ] Interactive repeat import/update/removal followed by visual smoke checks (automated lifecycle results are recorded separately).

## Remaining limitations

1. Manual visual/input checklist above is not complete because the Windows UI automation helper failed. Do not market this candidate as production-validated or fully equivalent.
2. Optional new UI Toolkit Hierarchy in recent Unity releases is not supported; use the standard/legacy Hierarchy. The tool never changes that preference automatically.
3. Custom palette icon JSON retains GUID references, not binary textures. Transfer custom assets with metadata separately.
4. Minimal mode also hides custom/folder glyphs and extra row controls. E recursively toggles a subtree; exact reference descendant behavior is not specified by the available docs.
5. Native Duplicate appearance transfer depends on contextual command observation and has not been manually exercised. Script-driven duplication uses the explicit CopyAppearance API.
6. Property changes publish cache invalidation; arbitrary external rule-state changes require Toolkit.RefreshRules.
7. Performance values are short local measurements of native GUI and whole-Editor frame GC; toolbar/delayed preparation CPU and long-duration memory are not isolated. No zero-impact/zero-allocation claim.
8. Internal Unity APIs may change; adapter binding/action failures safely disable dependent capabilities. Future Unity 6 releases still need their own tests.

Repository: https://github.com/Fara555/HierarchyToolkit (private).

rc.3 icon palette update (2026-10-04): 28 functional tests passed in graphical Unity 6000.4.4f1, including native icon substitution/reset and compatibility aliases. The final flat grid compiled and exported through Unity. No fresh manual screenshot or Unity 6.0 rerun is claimed for this appearance update.

Final rc.4 suites on 2026-10-04: 30/30 tests passed in graphical Unity 6000.4.4f1 and 28/28 functional tests passed in Unity 6000.0.46f1. These runs include the native palette and full-width tint code. Performance-data.json and Performance.md contain the rc.4 replay. The owner approved the observed appearance and explicitly authorized GitHub publication; broader manual checklist items remain outside that approval evidence.
