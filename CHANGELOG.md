# Changelog

Changes to the public package are recorded here. Release entries are created by the manual GitHub Actions release workflow from `Packages/pm.booth.aoinu607.udon.gateball/package.json`.

## [Unreleased]

- Added the v0.2 distributed-physics PoC: manually synchronized ShotStart/ShotEnd state, local Rigidbody simulation, owner-only settlement, local telemetry, event divergence comparison, late-join authoritative reconstruction, and the 12-shot development fixture set.
- Added the initial VPM package structure and Unity development project policy.
- Added public-repository validation and VPM listing workflows.
- Corrected the Local Gateball Core to real dimensions: 0.075 m ball diameter / 0.230 kg mass, 0.22 m × 0.19 m gate opening, 0.02 m × 0.20 m gate posts and goal pole, and a 15 m × 20 m court.
- Moved stopping, trajectory sampling, and VR mallet history/sweep evaluation to FixedUpdate and made every Test Shot reset all ten balls and transient state for deterministic replay.
- Corrected full-ball gate geometry and expanded EditMode/PlayMode regression coverage for collisions, Goal Pole contact, Test Shot reproducibility, and mallet hit paths.
