# Aoinu Gateball Package

This package contains the distributable v0.1 Local Gateball Core for VRChat. Runtime scripts use the `Pm.Booth.Aoinu607.Udon.Gateball` namespace and are delivered as `pm.booth.aoinu607.udon.gateball`. Its fixed-step deterministic Test Shot behavior is also the comparison fixture for the v0.2 Distributed Physics PoC; networking is not part of this package.

The development scene contains a 15 m × 20 m court, three gates, one goal pole, ten Rigidbody balls, a desktop controller, and a VR pickup mallet. The physical dimensions are ball diameter 0.075 m and mass 0.230 kg, gate opening 0.22 m × 0.19 m, gate post diameter 0.02 m and height 0.20 m, and goal pole diameter 0.02 m and height 0.20 m. Gate crossing uses geometry in addition to physical gate-post collisions. The court records ball touches, boundary exits, goal-pole contact, and low-speed settling on FixedUpdate physics steps.

Fixed Test Shot presets are provided by `GateballTestShotController`: weak/strong straight strokes, front collision, gate center, boundary out, touch, and goal-pole contact. Every preset resets all ten balls and all transient Court state before applying the same stroke. Use the component's Interact action to cycle presets and Use to run the selected shot. `GateballMallet` keeps a FixedUpdate position history, uses the actual head collider for sweep coverage, supports trigger/collision hits, and suppresses duplicate hits with a cooldown.

Development-only Scene, materials, and controllers remain in the root project under `Assets/Aoinu Works/Gateball/`. Package tests are in `Tests/Editor` and `Tests/Runtime`; add the package to the project's `testables` list and run them from Unity Test Runner.

Networking, turn management, scoring, spark flow, formal rules, and polished content are outside v0.1.
