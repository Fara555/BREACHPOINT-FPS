# Architecture

## Boundaries

All distributed code uses namespace HierarchyToolkit and assembly HierarchyToolkit.Editor, restricted to Editor. Tests use a separate Editor test assembly and are excluded from export. No runtime assembly, render-pipeline dependency, hidden scene GameObject or runtime component is required. Built-in, URP and HDRP are therefore structurally independent; pipeline-specific visual testing is listed separately in Validation.md.

ProjectSettings.cs, Preferences.cs, DecorationStore.cs and SessionDecorations.cs give each serialized class its own matching script file; Data.cs contains the shared value types and stable identity helpers. Shared files use Unity ScriptableSingleton serialization outside Assets. A hidden ScriptableObject retains unsaved references through domain reload and participates in Undo. Only saved GlobalObjectId strings are persisted to disk. Saved prefab-stage targets are resolved against cached asset-local IDs, not sibling positions or names. Reconciliation examines tracked decorated objects after scene changes/save; it does not enumerate every loaded object.

HierarchyExtension subscribes once per domain to hierarchy, project, object-change, selection, Undo, scene-save, scene-open and Play Mode events. Before assembly reload it removes delegates, queued work and installed navigation bars and restores native icon/footer layout. Play Mode transitions clear caches even when domain reload is disabled.

## Rendering and preparation

Native hierarchy row callbacks only look up cached rows, read live activity flags, paint rectangles/icons, and handle local input. New visible rows enqueue their IDs into a deduplicated work set. A single delayed preparation callback resolves GameObjects, stable keys, components, native icon textures, depth and label width. GUI resources are reused; asset lookup and rule evaluation occur during preparation rather than inside row drawing. Property-change notifications invalidate only the affected GameObject; structural changes invalidate visible-row caches. There is no continuous polling or unconditional update/repaint loop in the distributed tool.

Toolbar bookmarks are cached on data/hierarchy changes. Scene search scans scene assets once when its popup opens. Bookmark resolution and optional additive loading occur only when navigating. Component popups own a native Editor instance and destroy it on close. Serialization, Undo and prefab handling remain owned by Unity's inspector/editor APIs.

## Native integration

HierarchyCompatibility is the single internal-API boundary: hierarchy window detection, contextual global event routing, hover/rename state, native expansion/isolation/collapse, default parent and native icon/footer dimensions. Reflection is bound and cached once. Operations are availability checked, catch failures, warn once and then stop attempting the failed capability.

The navigation bar is a UIElements IMGUI container over a reserved bottom band of the standard Hierarchy. Extra native bottom content margin ensures the final rows can scroll clear of it. It does not replace the tree, mutate scene composition or intercept ordinary right-clicks. Native icon width is restored on disable/unload. ToolkitIcons caches grouped references to native Unity Editor textures; it does not generate, copy or destroy Unity-owned textures.

## Rules and script API

Toolkit mutates decorations via the same Undo-enabled store used by the palette. RuleEngine discovers attributed static methods through TypeCache once, binds delegates, sorts by priority/name, and evaluates them when preparing visible rows. Manual decoration takes precedence over calculated rule values. Rules do not persist changes or dirty scenes. A rule throwing an exception is disabled until the next reload and produces one warning. Property updates or Toolkit.RefreshRules rebuild computed views.

## Current design differences

The toolbar uses its own interface rather than imitating another asset. Palette interchange transfers GUID references rather than texture binaries. Prefab bookmarks open Prefab Mode; closed scene bookmarks add their scene rather than replacing other open scenes. Minimal mode hides additional controls/custom icons along with native icons. Programmatic duplication requires an explicit CopyAppearance call; native Duplicate is observed through contextual command events. The optional UI Toolkit-based new Hierarchy in recent Unity is not bound by this compatibility layer.
