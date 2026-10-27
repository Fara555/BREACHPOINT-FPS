# HierarchyToolkit 1.0.0-rc.4

Independent MIT-licensed Editor toolkit for Unity's standard Hierarchy. This prerelease includes the owner's reviewed icon palette and final full-width row tint.

- Colors cover the full row, including the reserved area at the right edge.
- Compact palette of 31 native Unity icons, with selection feedback and compatibility for earlier assignments.
- Custom icons replace the native cube, including selected rows; component icons retain their full 16-point size.
- Visual folders, recursive/multi-selection editing, bookmarks, scene navigation restricted to Assets, compact component inspectors and configurable hover shortcuts.
- D toggles Unity's default parent: new objects created through the GameObject menu become its children. Bold marks the active parent; press D again to clear it.
- Shared project data and separate user settings persist outside the installed folder. Divider was removed at the owner's request.

Install **HierarchyToolkit-1.0.0-rc.4.unitypackage** via Assets → Import Package → Custom Package. It installs into Assets/HierarchyToolkit. Open Tools → HierarchyToolkit → Settings. Download the attached package rather than GitHub's source archive.

Automated validation: Unity 6000.4.4f1 (30 tests including graphical replay) and 6000.0.46f1 (28 functional tests), Windows. Documentation/Validation.md records version-specific test and lifecycle evidence; Documentation/Performance.md contains actual 1,000/10,000-object measurements and their scope. The final package is exported by Unity, checked against source and imported over an earlier candidate with cold-restart persistence checks.

Known limits: use the standard/legacy Hierarchy; the optional new UI Toolkit Hierarchy is unsupported. Broader manual themes/DPI/input/custom inspector/Play Mode checks remain incomplete. macOS, Linux and HDRP were not exercised. Custom icon texture assets must be transferred with their metadata. Programmatic clones use Toolkit.CopyAppearance. Performance overhead is nonzero.
