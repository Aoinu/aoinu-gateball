# Aoinu Gateball Package

This package contains the distributable runtime for the VRChat gateball prototype. Runtime scripts use the `Pm.Booth.Aoinu607.Udon.Gateball` namespace.

The v0.3 MVP covers local ball physics, gate/touch/out sensing, Owner-authoritative shot results, and the basic turn, score, touch, spark, and late-join flows. Detailed network ownership and synchronization contracts are defined during implementation and must be documented with their tests.

Install the package through VCC after a public release. Development-only scenes and the current debug harness remain in the root Unity project under `Assets/`.
