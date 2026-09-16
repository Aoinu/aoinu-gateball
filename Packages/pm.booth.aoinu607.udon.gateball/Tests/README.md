# Package Tests

`Tests/Editor` contains pure rule and geometry tests. `Tests/Runtime` contains PlayMode physics tests. Add the package ID to the project manifest `testables` array so Unity Test Runner discovers these tests.

The development project also contains `Assets/Editor/GateballTestRunnerCompatibility.cs`. The installed VRChat SDK 3.10.5 filter strips Unity Test Runner callback fields from temporary PlayMode scenes, so this editor-only compatibility hook clears that cached filter during a test run and restores it afterward. It does not ship in the VPM package.
