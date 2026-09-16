# Aoinu Gateball Package

This package contains the distributable v0.1 Local Gateball Core for VRChat. Runtime scripts use the `Pm.Booth.Aoinu607.Udon.Gateball` namespace and are delivered as `pm.booth.aoinu607.udon.gateball`.

The development scene contains a 15 m × 20 m court, three gates, one goal pole, ten Rigidbody balls, a desktop controller, and a VR pickup mallet. Gate crossing uses geometry in addition to physical gate-post collisions. The court records ball touches, boundary exits, goal-pole contact, and low-speed settling.

Fixed Test Shot presets are provided by `GateballTestShotController`: weak/strong straight strokes, front collision, gate center, boundary out, touch, and goal-pole contact. Use the component's Interact action to cycle presets and Use to run the selected shot.

Development-only Scene, materials, and controllers remain in the root project under `Assets/Aoinu Works/Gateball/`. Package tests are in `Tests/Editor` and `Tests/Runtime`; add the package to the project's `testables` list and run them from Unity Test Runner.

Networking, turn management, scoring, spark flow, formal rules, and polished content are outside v0.1.
