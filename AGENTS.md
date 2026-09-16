# Repository Guidelines

This repository is the Unity 2022.3.22f1 development project and embedded VPM package for a VRChat gateball prototype. Keep public files, comments, documentation, and commit messages free of personal information and inappropriate language.

## Project Structure

- `Packages/pm.booth.aoinu607.udon.gateball/` is the distributable package.
- `Runtime/` contains runtime UdonSharp code under `Pm.Booth.Aoinu607.Udon.Gateball`.
- `Samples~/` contains optional sample content; `Tests~/` contains package tests.
- `Assets/` is the development-only world and debug harness. `Assets/SerializedUdonPrograms/` is generated; do not hand-edit it.
- `Packages/manifest.json`, `packages-lock.json`, and `vpm-manifest.json` define dependencies. SDK packages and UnityMCP are development dependencies, not package contents.

## Build, Test, and Development

Open the project with Unity 2022.3.22f1 and wait for imports and UdonSharp compilation to finish. Use **Window > General > Test Runner** for EditMode/PlayMode tests and the VRChat SDK Builder for **Build & Test**. The current GitHub Actions workflow validates metadata and credential signatures; Unity automation is intentionally deferred until a runner is selected.

After cloning, install `git-vrc` 0.1.0 or a compatible filter-v1 release, then enable the repository filter:

```powershell
git config --local include.path ../.gitconfig
git-vrc --version
```

The legacy TextMesh Pro `Text Popup.prefab` is intentionally excluded from the filter because git-vrc 0.1.0 cannot parse its serialized format. Do not broaden this exception without checking the asset and its license.

## Style and Naming

Use 4 spaces, braces on their own lines, explicit namespaces, PascalCase for types/files, and camelCase for fields. Keep one public type per file and use `[Header]`/`[Tooltip]` for authoring-facing fields. Preserve `.meta` files with serialized Unity assets.

## Testing

Name tests `FeatureNameTests.cs`. Add EditMode and PlayMode coverage for each behavior as implementation proceeds. Physics and VRChat behavior must also be checked in Unity Play Mode and SDK Build & Test; compilation alone is not sufficient.

## Commits and Pull Requests

Use English Conventional Commits, such as `feat: add gate sensor` or `chore: update package metadata`. Prefer PRs with a summary, affected scenes/packages, validation results, and screenshots for visual changes; direct pushes to `main` are allowed during early development.

## Public-Repository Safety

Never commit credentials, machine-local paths, personal email addresses, private SDK data, or unlicensed assets. Review serialized diffs and run the repository validation workflow before publishing a package release.
