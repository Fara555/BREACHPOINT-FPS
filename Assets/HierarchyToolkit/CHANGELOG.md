# Changelog

## 1.0.0-rc.4 — 2026-10-04
- Extend row tint to the right viewport edge, covering the reserved native control area.
- Package the reviewed native icon palette and all preceding corrections for the first GitHub prerelease.


## 1.0.0-rc.3 — 2026-10-04
- Replace generated geometric glyphs with six compact groups of native Unity Editor icons.
- Flat icon cells, blue selected state, clear action and custom-icon settings shortcut.
- Preserve existing assignments through legacy-key aliases.


## 1.0.0-rc.2 — 2026-10-03
- Draw component textures at 16 GUI points without label padding or shrinking.
- Substitute custom/folder icons in the native tree, including selected icons; restore native icons on reset, disable and reload.
- Extend row tint to the left edge and move the color marker away from object icons.
- Remove Divider controls, settings, glyph and renderer; ignore legacy divider icon keys without changing other decoration data.
- Limit scene search and scene bookmarks to Assets, excluding package samples.
- Explain D through feedback and documentation; test actual default-parent object creation and toggling.

## 1.0.0-rc.1 — 2026-10-03

Initial independently implemented release candidate.

- Native Hierarchy decoration, original icons, visual folders and dividers, recursive/multi-selection palette editing.
- Scene search, bookmarks, additive opening, active scene selection and Unity's save prompts.
- Object navigation, drag/drop bookmarks, subtree isolation and collapse.
- Component minimap and native Inspector popup with multi-editing, Undo, prefab overrides and missing-script handling.
- Activity control, configurable contextual hover keys and project/user feature switches.
- External persistent storage, temporary session data and prefab-stage/local-ID mapping.
- Versioned validated palette interchange, custom textures, public API and attribute-based rules.
- Editor-only assemblies, tests, native package exporter and release documentation.

Manual visual verification and optional new Hierarchy support are not completed; see Validation.md. No public release has been published.
