# HierarchyToolkit

Independent Editor-only hierarchy tooling for Unity 6. MIT licensed. Version **1.0.0-rc.4** is a release candidate; see [Validation](Documentation/Validation.md) for measured evidence and remaining manual checks. It extends Unity's standard IMGUI Hierarchy, rather than replacing it.

## Install

Import `HierarchyToolkit-1.0.0-rc.4.unitypackage` through **Assets → Import Package → Custom Package**. The package installs into `Assets/HierarchyToolkit`. Alternatively clone this repository directly into that folder. Do not clone at the root of your game project.

Open **Tools → HierarchyToolkit → Settings**. The bottom navigation bar appears in each standard Hierarchy window. All features have separate switches; **Enabled for me** and **Enabled for project** disable the complete extension.

## Use

- **Alt-click a row** (or click its color dot) to open the palette. Edit colors, grouped native Unity icons, custom textures, visual folders and bookmarks. The current multi-selection is used when the clicked object is selected. Hold Alt while applying a palette choice to include all descendants. Reset clears only decoration, without adding components or renaming/reparenting objects.
- **Scenes** searches scene asset paths. **Open** uses Unity's unsaved-scene prompt before replacing loaded scenes. **Add** loads alongside them; **Active** selects the scene for newly created objects. Stars keep scene bookmarks across asset moves.
- **Bookmarks** lists saved and temporary object bookmarks. Drag GameObjects to the navigation bar to add them. Visible chips reveal objects; drag a chip away to remove its bookmark. Closed scenes load additively on navigation, prefab bookmarks open Prefab Mode, and missing objects remain removable.
- Components appear at the right when space permits. **Alt-click a component icon** opens its native Inspector in a compact popup, with multi-editing of matching component ordinals, Undo and prefab overrides. Missing scripts display `!` and open the object in the native Inspector.
- Click the activity circle without selecting the object. Green means active; amber means activeSelf is enabled but a parent disables activeInHierarchy; grey means disabled locally.
- Appearance settings include hierarchy lines, alternating row backgrounds, tint intensity and minimal mode. Minimal mode hides Unity's default icons and reduces extra row controls. Custom icons/folders are also hidden while minimal mode is active.

## Hover keys

The Hierarchy must be focused and the cursor must be over it. Text fields, rename mode, active drags and other windows retain their native input.

| Default | Action |
| --- | --- |
| D | Toggle Unity's default parent using the hovered object |
| A | Toggle the hovered object's activeSelf |
| F | Select and frame the hovered object in Scene view |
| X | Delete the hovered object with Undo |
| E | Expand/collapse the hovered subtree |
| Shift+E | Expand the hovered object and its ancestors, collapse other branches |
| Ctrl+Shift+E / Cmd+Shift+E | Collapse objects in this Hierarchy |

Bindings are editable in Settings → Keys. Set a key to None to disable an individual action. Ctrl and Command share the primary modifier; Alt maps to Option on macOS. Conflicting bindings are reported; the first matching enabled action wins. macOS behavior has not been exercised on hardware.

## Data and portability

| Data | Location (outside installed tool) | Scope |
| --- | --- | --- |
| Project enablement and palette | `ProjectSettings/HierarchyToolkitSettings.asset` | Shared project settings |
| Decoration, object/scene bookmarks | `ProjectSettings/HierarchyToolkitData.asset` | Shared project data |
| Feature switches, appearance and keys | `UserSettings/HierarchyToolkitPreferences.asset` | This project/user UI |
| Unsaved objects | Hidden Editor session object | Temporary, supports Undo; promoted after saving |

Commit project settings/data in the **game project's** repository if you want teammates to share them; ignore `UserSettings`. None are written under the installed tool. Saved objects use `GlobalObjectId`; saved prefab-stage objects map to the asset's local IDs. No persistent instance IDs or runtime storage components are used.

Palette JSON is versioned and checked before replacing the current palette. JSON includes texture GUID references, so transfer custom textures **with their .meta files** separately. Missing references remain listed and do not crash drawing. A project-specific icon asset is not automatically bundled in releases of the toolkit.

Native Duplicate commands copy decorations, including descendants, but intentionally do not duplicate bookmarks. Scripting integrations that duplicate objects should call `Toolkit.CopyAppearance(source, duplicate)`. See [API and rules](Documentation/API.md).

## Compatibility and limits

Target: Unity 6, independent of Built-in/URP/HDRP. Tested versions and precise check types are recorded in [Validation](Documentation/Validation.md). The optional new UI Toolkit Hierarchy in newer Unity versions is a separate internal implementation and is not supported by this release candidate; use the standard/legacy Hierarchy. Internal APIs for native expansion, default parent, icon layout and contextual event routing are isolated in one compatibility adapter and checked for availability. If Unity changes them, the corresponding action fails safely and reports one warning.

Color overlays are drawn after Unity's labels; selection remains native. Native prefab indicators, picking/visibility, foldouts and dragging are preserved. Theme, scaling and narrow-window visual quality still require the manual checklist. This candidate does **not** claim complete visual/functional parity with another asset.

## Update / remove

Back up the project data files above. Import a newer package over the same path; retain `.meta` files if updating from Git. On removal, delete only `Assets/HierarchyToolkit` (and its folder `.meta`) after closing its popups; let Unity recompile. GameObjects have no toolkit components to clean up. Data files remain available for reinstalling. Permanently removing those external data files is a separate, deliberate action.

## Development and release

Unity Test Framework is needed only to run repository tests. The exported package excludes `Tests/`, and no tool assembly enters a player build. A single source tree builds a real Unity package via **Tools → HierarchyToolkit → Build unitypackage** or `HierarchyToolkit.ReleaseBuilder.Build` in a licensed Editor. See [Release](Documentation/Release.md), [Architecture](Documentation/Architecture.md), [Feature parity](Documentation/FeatureParity.md) and [Performance](Documentation/Performance.md).

Local source repository: `Assets/HierarchyToolkit`. Private remote: https://github.com/Fara555/HierarchyToolkit. Publishing is pending explicit owner approval.


D toggles Unity's default parent for the hovered object. Bold means it is enabled: new objects created through Unity's GameObject menu become its children. Press D over the same object again to clear it. This uses Unity's native default-parent Undo. Scene search and saved scene bookmarks show only project scenes under Assets; package sample scenes are excluded.
