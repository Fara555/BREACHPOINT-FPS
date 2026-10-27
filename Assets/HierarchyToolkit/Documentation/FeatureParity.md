# Functional comparison

Comparison date: **2026-10-03**. Reference listing: **vHierarchy 2.1.11** (Asset Store release date displayed as 2026-09-03 when opened; cached search results show 2026-09-02). This is an independent implementation based on publicly documented behavior, not its code or artwork. Complete parity is **not** claimed.

Sources: [A — official Asset Store listing](https://assetstore.unity.com/packages/tools/utilities/vhierarchy-2-253397), [F — official Features](https://kubacho-lab.gitbook.io/vhierarchy-2/features), [K — official Shortcuts](https://kubacho-lab.gitbook.io/vhierarchy-2/shortcuts), [R — official API and Rules](https://kubacho-lab.gitbook.io/vhierarchy-2/api-and-rules), [G — Getting started](https://kubacho-lab.gitbook.io/vhierarchy-2/getting-started), [D — publisher-linked demonstration](https://www.youtube.com/watch?v=XBM9Ht9idYU), [U — Unity default parent documentation](https://docs.unity.com/en-us/engine/6000.0/manual/unity-editor/editor-windows-views-reference/hierarchy-window/hierarchy).

The complete four-page GitBook documentation was read as Markdown through its official `.md` endpoints. Its linked still-image descriptions confirm interactions; the YouTube stream could not be fetched, so frame-by-frame timing, transitions and exact layout were not verified. No official materials beyond this documentation/index exposed additional features. Screenshots and assets were not downloaded into the distributed tool.

**Implemented / automated** means the mechanism passed the relevant automated test. **Implemented / manual pending** means source exists and compiles, but the described visual/user interaction has not been manually observed. User-specific requirements beyond the reference are marked **Request** as a source.

| Function | Expected behavior | Source | Implementation status | Verification | Known differences |
| --- | --- | --- | --- | --- | --- |
| Colors | Apply/change/reset a row tint | F/A | Implemented / automated storage | Multi-object edit, Undo/Redo tests; manual palette checklist | Own colors/tint controls |
| Icons | Assign/change/reset a row glyph | F/A | Implemented / automated storage | API and copy tests; manual row checklist | Grouped Unity-native/custom texture icons; generated glyphs removed in rc.3 |
| Recursive appearance | Include descendants with Alt held while applying | F | Implemented / manual pending | Palette recursive action; checklist | Explicit multi-selection plus deduplication |
| Multi-selection | Apply one choice to selected objects | Request | Implemented / automated | MultiObjectEditsUndoAndRedoTogether | Hovered unselected object edits alone |
| Prefab Mode | Decoration targets persistent prefab objects | F | Implemented / automated | Stage mapping, unsaved child and reorder/rename tests | Uses file IDs; transient newly added children promote on save |
| Visual folders | Display folder marker without changing object logic | F/A | Implemented / automated data | No runtime components/scene dirty test; visual checklist | No reparenting/renaming; existing GO remains unchanged |
| Dividers | Removed at owner request in rc.2 | Request | Intentionally removed | No palette option, setting or renderer | Existing colors/bookmarks preserved |
| Palette entry | Alt-click object opens palette | F | Implemented / manual pending | Row event routing; checklist | Additional color-dot and menu entry |
| Scene selector entry | Open scene search quickly | F/A | Implemented / manual pending | Native window replay; scene checklist | Scenes toolbar button instead of scene-name click |
| Scene search | Search available scene paths | A/Request | Implemented / manual pending | Popup scans assets on open; checklist | Includes full path tooltips |
| Scene bookmarks | Persist favorite scene assets | A/Request | Implemented / automated storage | GUID-based store; checklist | Missing scenes remain removable |
| Save handling | Ask before replacing modified scenes | Request/U | Implemented / manual pending | Calls native SaveCurrentModifiedScenesIfUserWantsTo | Native save prompt, cancel aborts |
| Multiple scenes | Add, replace, or select active predictably | Request | Implemented / manual pending | Additive navigation implementation; checklist | Explicit Open/Add/Active controls |
| Object bookmarks | Persist and reveal objects | F/A | Implemented / automated identity | Save/reopen/deletion/Undo identity tests | Popup lists all; chips fit available width |
| Bookmark drag/drop | Add on bar; remove when dragged away | F | Implemented / manual pending | Toolbar drag handlers; checklist | Removal is triggered on exiting bar; explicit × also available |
| Reveal ancestors | Reveal nested target | F | Implemented / automated native APIs | Expansion adapter test; navigation checklist | Scene opens additively, prefabs open in Prefab Mode |
| Collapse button | Collapse branches in current Hierarchy | F/A | Implemented / automated | Native adapter integration test | Scene headings may stay expanded per Unity behavior |
| Deleted/closed bookmarks | Safe lookup and removal | Request | Implemented / automated IDs | DeletedBookmarkRetainsKeyForUndo; closed scene checklist | Never silently purges missing records |
| Component minimap | Show component glyphs at right | F/A | Implemented / manual pending | Actual GUI replay; visual checklist | Capped count, hides entries when label/window needs space |
| Component mini editor | Alt-click opens compact property editor | F/A | Implemented / manual pending | Native Editor lifecycle/serialized path; checklist | Uses native/custom inspector rather than custom property renderer |
| Component multi-edit | Match same type and ordinal across selection | Request | Implemented / manual pending | Popup target mapping; checklist | Custom inspectors own multi-edit support |
| Component Undo/overrides | Use Unity serialized edits | Request | Implemented / manual pending | Native Editor + ApplyModifiedProperties; checklist | Activity overrides separately automated |
| Missing scripts | Visible warning, safe interaction | Request | Implemented / manual pending | Null component path; checklist | Opens object Inspector; does not auto-remove scripts |
| Custom icons | Use project texture assets | F/A | Implemented / manual pending | GUID validation and missing-resource tests | Texture binaries transferred separately |
| Edit colors | Modify project palette | F/A | Implemented / automated format | Palette round-trip and validation tests | Independent palette UI |
| Palette export/import | Transfer valid versioned palette | F/A/Request | Implemented / automated | RoundTrip/RejectsInvalidImport/MissingIcon tests | JSON v1; icon GUIDs retained |
| Hierarchy lines | Show parent/child guides | F/A | Implemented / manual pending | Render/replay; visual checklist | Geometric vertical guides |
| Minimal mode | Hide default Unity row icons | F | Implemented / automated capability | Adapter layout field binding; checklist | Also hides extra controls and custom/folder icons |
| Zebra background | Alternate row background | F/A | Implemented / manual pending | Render/replay; visual checklist | Own subtle theme-relative opacity |
| Light/dark/DPI/narrow | Respect native editor sizing | Request | Implemented / visual pending | Uses Editor styles/GUI points; checklist | Visual verification still outstanding |
| Activity circle | Toggle without selecting | F/A | Implemented / automated operation | ActivityCreatesPrefabOverrideAndSupportsUndo; input checklist | ActiveSelf and inherited inactivity use separate indicators |
| D | Native default parent for hovered GO | K/U | Implemented / automated backend | Default-parent adapter integration tests | Native Unity toggle/active-scene semantics |
| A | Toggle hovered GO activity | K | Implemented / automated operation | Activity Undo/override test; contextual input checklist | Configurable binding |
| F | Frame hovered GO in Scene view | K | Implemented / manual pending | Native FrameSelected; checklist | Selects target before framing |
| X | Delete hovered GO | K/Request | Implemented / automated operation | Deleted-object Undo test; input checklist | Uses Undo.DestroyObjectImmediate |
| E | Expand/collapse hovered object | K | Implemented / automated backend | Native subtree toggle integration | Documentation leaves exact descendant-depth behavior unspecified; recursive subtree chosen |
| Shift+E | Expand hovered object, collapse others | K | Implemented / automated | Native Isolate integration test | Ancestors reopen so the target stays visible |
| Primary+Shift+E | Collapse all in this window | K/Request | Implemented / automated backend | Collapse test + primary-modifier test | Ctrl or Cmd; still needs hardware/macOS contextual check |
| Shortcut customization/context | Bindings, focus, text and rename guards | Request | Implemented / manual pending | Modifier tests + native rename/hover guards; checklist | First matching binding wins; unavailable native hover fails closed |
| Individual switches/reset | Optional features, general disable, defaults | G/A/Request | Implemented / manual pending | Settings serialization, event-driven removal; checklist | Project enablement separate from user UI |
| Script API | Set appearance from Editor scripts | R | Implemented / automated | Toolkit API storage tests | Own Toolkit methods and direct colors or zero-based palette index |
| Automatic rules | Compute appearance from object properties | R | Implemented / automated | RulesRespectManualOverrides | Priority-based attributes, manual values win, throwing rule disables itself |
| Durable storage | Scene/prefab IDs, temporary promotion | Request | Implemented / automated | Storage/PrefabStage tests on verified versions | Shared project-side data; no scene components |
| Duplicate/move/variant/Undo | Preserve identity/data correctly | Request | Implemented / automated mechanisms | Duplicate/copy/move/variant/Undo tests | Script-created clones call CopyAppearance explicitly; native command copying awaits manual input check |
| Performance | Cache/events, measure representative scenes | A/Request | Measured; see Performance.md | Native GUI replay with 1k/10k scenes + Unity GC frame counter | Nonzero overhead; no claim of reference asset's performance equivalence |
| New UI Toolkit Hierarchy | Recent optional Unity window implementation | Request compatibility | Not supported by this candidate | Separate internal type detected in 6000.4 | Standard/legacy Hierarchy supported; never switches user settings automatically |

Release validation and the full manual checklist are in Validation.md. An unobserved UI action is not a verified parity claim.
