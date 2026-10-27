# Release procedure

Version authority: `ReleaseBuilder.Version` in Editor/ReleaseBuilder.cs. Prepared candidate: **1.0.0-rc.4**, local tag name **v1.0.0-rc.4**. No code or release may be uploaded until the owner explicitly approves the prepared result. The empty private remote under Fara555 was created with prior authorization.

## Build a genuine package

Use the source folder Assets/HierarchyToolkit in a licensed Unity 6 project. Open **Tools → HierarchyToolkit → Build unitypackage**. Output is `Artifacts/HierarchyToolkit-<version>.unitypackage` at the project root.

A closed source project can be built headlessly:

```powershell
& $unityEditor -batchmode -nographics -quit -projectPath $sourceProject -executeMethod HierarchyToolkit.ReleaseBuilder.Build -logFile $exportLog
```

Do not launch batchmode against a project already open in another Editor. The exporter calls Unity AssetDatabase.ExportPackage, with an explicit sorted set of imported paths under the toolkit root. It does not include asset dependencies, Tests, hidden Git files, benchmark scenes, runtime project assets, external project/user settings or release artifacts. GUIDs come from the source .meta files. Temporary validation projects are populated automatically; source changes are made only in the main repository.

Byte-identical compressed output is not guaranteed by Unity's exporter (archive timestamps may differ). The same source tree and GUID set reproducibly define the package contents; inspect archive path/GUID coverage and record its SHA-256 for the exact release artifact.

## Verify before publication

1. Run EditMode tests in the source development project (Test Framework required). For graphics/PerformanceGUI tests launch a graphical Editor; batchmode/nographics cannot supply meaningful GUI events.
2. Import the built `.unitypackage` into another clean Unity 6 project; compile, run the package smoke checks and manual checklist.
3. Reimport over itself, restart, confirm preferences/data persistence; remove the tool, confirm clean compilation and intact external data, then reinstall.
4. Build a player; check tool assemblies are absent from CompilationPipeline's Player assembly list and the actual player output.
5. Record Unity versions, test results, performance workload, known limitations and package hash. Archive test logs outside the distributed source folder. Test scenes are development assets outside the tool.

All release gates are described honestly in Validation.md; no automatic successful workflow is provided for an unavailable Unity license or unperformed manual checks.

## Local preparation

Commit only this repository. Ensure `git status --short` is clean, `origin` is `https://github.com/Fara555/HierarchyToolkit.git`, and the release package hash matches the reviewed artifact. Create a local annotated tag at the reviewed commit if desired. The changelog/release notes must refer to that exact source version.

## After explicit approval

Verify the signed-in account is **Fara555**, and verify the remote is still private and belongs to that account. Use the existing Git/GitHub authentication or `gh auth login` (never put a token/password in source, logs or chat).

From the toolkit repository:

```powershell
git push origin main
git push origin v1.0.0-rc.4
gh release create v1.0.0-rc.4 $verifiedUnityPackage --repo Fara555/HierarchyToolkit --title 'HierarchyToolkit 1.0.0-rc.4' --notes-file Documentation/ReleaseNotes.md --prerelease --verify-tag
```

For a stable release after remaining verification, update the version/changelog, rebuild and re-verify its own artifact before tagging it. Approval for an older artifact is not approval for a materially changed version.

Finally inspect the published release and its assets via `gh release view ... --json url,assets`. Download the attached `.unitypackage` to a temporary verification path and compare SHA-256 with the prepared artifact. Return links to the repository, release and individual asset. Private repository downloads require GitHub authorization; an automatic GitHub source archive is not a Unity package.

## Automation prerequisites

Exporting/testing/building requires an installed matching Editor, required platform support and a valid Unity license. GitHub-hosted runners require license activation/entitlement setup and appropriate secrets; a licensed self-hosted runner can execute the commands above. Publishing additionally needs a GitHub account/token with this repository's contents permission. This candidate supplies local Unity export/test commands and release instructions, not a workflow that pretends activation or validation already succeeded.
