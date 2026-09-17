# Package Tests

`Tests/Editor` contains pure rule and geometry tests, including the 0.075 m ball radius, 0.22 m × 0.19 m gate opening, forward/reverse/edge/above-bar cases, pole contact bounds, court bounds, and stop thresholds. `Tests/Runtime` contains PlayMode tests through the project implementation for Ball stroke and FixedUpdate stop, Ball-Ball collision/touch, gate post collision, Goal Pole contact, deterministic ten-ball Test Shot reset, and Mallet head-shaped sweep. Add the package ID to the project manifest `testables` array so Unity Test Runner discovers these tests.

The development project also contains `Assets/Editor/GateballTestRunnerCompatibility.cs`. The installed VRChat SDK 3.10.5 filter strips Unity Test Runner callback fields from temporary PlayMode scenes, so this editor-only compatibility hook clears that cached filter during a test run and restores it afterward. It does not ship in the VPM package.
