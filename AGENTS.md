# Repository Guidelines

This repository is the Unity 2022.3.22f1 development project and embedded VPM package for the VRChat v0.1 Local Gateball Core. Keep public files, comments, documentation, and commit messages free of personal information and inappropriate language.

## Project Structure

- `Packages/pm.booth.aoinu607.udon.gateball/` is the distributable package; its `Runtime/`, `Tests/Editor`, and `Tests/Runtime` directories contain the runtime and package tests.
- `Assets/Aoinu Works/Gateball/` contains the development Scene, materials, and debug harness. `Assets/Editor/` contains editor-only builders and test-runner compatibility code.
- `Assets/SerializedUdonPrograms/` is generated and ignored; do not hand-edit or commit it.
- `Packages/manifest.json`, `packages-lock.json`, and `vpm-manifest.json` define dependencies. SDK packages and UnityMCP are development dependencies, not package contents.

## Scope and Simplicity

Implement only the requested outcome and the changes required for it to work correctly. Before adding code, check whether the behavior is needed now, already exists in the repository, or is covered by Unity, UdonSharp, VRChat SDK, the C# standard library, or an installed dependency.

- Prefer updating the existing execution path over adding a parallel path.
- Do not turn adjacent improvements, hypothetical risks, or possible future requirements into current work. Report unrelated findings without investigating or fixing them.
- Do not add abstractions, interfaces, wrappers, factories, managers, configuration options, extension points, or compatibility layers without a current concrete need. An interface with one implementation requires specific justification.
- Do not add a dependency when the repository can already solve the problem directly.
- Prefer deletion, direct control flow, and explicit ownership over additional layers. Use the fewest files consistent with the repository's one-public-type-per-file rule.
- Avoid unrelated refactors, renames, formatting changes, and cleanup.
- If completing the request requires a material scope expansion, stop and ask before proceeding.

## Execution and Verification

- Work as a single agent by default. Do not delegate or spawn subagents unless the user explicitly requests it or the task contains genuinely independent, bounded work that materially benefits from parallel execution.
- For bug fixes, trace the relevant Unity event, UdonSharp call, or data flow and fix the root cause in the shared owner rather than adding guards to individual symptoms.
- Add or change tests only for a concrete, plausible regression in behavior or an owned contract. Prefer extending an existing focused test over introducing new fixtures, helpers, frameworks, or test-only production hooks.
- Run the smallest relevant validation first. Broaden or repeat validation only after a failure, a material code change, or a concrete unresolved concern.
- Do not create speculative validation, audit, review, documentation, or hardening loops after the requested acceptance criteria pass.
- Stop when the requested outcome is implemented, the relevant checks pass, and no unresolved issue can materially change that result.

## Build, Test, and Development

Open the project with Unity 2022.3.22f1 and wait for imports and UdonSharp compilation to finish. Use **Aoinu Gateball > Build Development Scene** when regenerating the harness, **Window > General > Test Runner** for EditMode/PlayMode tests, and the VRChat SDK Builder for **Build & Test**. GitHub Actions validates package metadata and credential signatures.

After cloning, install `git-vrc` 0.1.0 or a compatible filter-v1 release, then enable the repository filter:

```powershell
git config --local include.path ../.gitconfig
git-vrc --version
```

The legacy TextMesh Pro `Text Popup.prefab` is intentionally excluded from the filter because git-vrc 0.1.0 cannot parse its serialized format. Do not broaden this exception without checking the asset and its license.

## Style and Naming

Use 4 spaces, braces on their own lines, explicit namespaces, PascalCase for types/files, and camelCase for fields. Keep one public type per file and use `[Header]`/`[Tooltip]` for authoring-facing fields. Preserve `.meta` files with serialized Unity assets.

## Testing

Name tests `FeatureNameTests.cs`. Put pure rule/geometry tests in `Packages/pm.booth.aoinu607.udon.gateball/Tests/Editor` and physics tests in `Tests/Runtime`. Run both test assemblies from Unity Test Runner; also check physics and VRChat behavior in Play Mode and SDK Build & Test because compilation alone is not sufficient. `GateballTestRunnerCompatibility.cs` preserves Unity Test Runner callbacks that the installed SDK filter would otherwise strip during PlayMode tests.

## Commits and Pull Requests

Use English Conventional Commits, such as `feat: add gate sensor` or `chore: update package metadata`. Prefer PRs with a summary, affected scenes/packages, validation results, and screenshots for visual changes; direct pushes to `main` are allowed during early development.

## Public-Repository Safety

Never commit credentials, machine-local paths, personal email addresses, private SDK data, or unlicensed assets. Review serialized diffs and run the repository validation workflow before publishing a package release.
