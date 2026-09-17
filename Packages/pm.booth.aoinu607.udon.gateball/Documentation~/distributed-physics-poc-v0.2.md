# Distributed Physics Synchronization PoC v0.2

## Decision

**MODIFY.** Owner and Remote now receive one `ShotStart`, apply the same initial snapshot and stroke, and run local Rigidbody physics. Normal settlement is reliable in both ownership directions, and final authoritative correction is small for the measured fixtures. It is not yet safe to promote local trajectory and event sequence to gameplay-critical rule authority: the first shot after startup reached 1.530006 m trajectory max error in A-to-B and 1.871013 m in B-to-A, with non-zero event divergence on several shots.

## Runtime architecture

`GateballNetworkState` remains the single manually synchronized state owner. It publishes one ShotStart snapshot, each client applies it and runs the existing local Rigidbody simulation, and only the shot owner publishes ShotEnd. There is no per-step network position stream, mid-shot snapshot, interpolation, or divergence-hiding correction.

The stroke path uses the same impulse-to-initial-velocity value on every client: `velocity += direction * (impulse / Rigidbody.mass)`. In VRChat Build & Test, `Rigidbody.AddForce(ForceMode.Impulse)` did not change velocity on this Udon execution path even when the body was initialized and non-kinematic; the equivalent initial velocity made the local physics start condition observable and repeatable. All movement after that initial condition is Rigidbody physics.

```text
Waiting or Settled
  -> owner request / ownership transfer
  -> ShotStart serialization
  -> Owner and Remote apply initial positions once
  -> Owner and Remote apply the stroke once
  -> independent local Rigidbody simulation
  -> owner velocity threshold + settle delay
  -> ShotEnd serialization
  -> remote records local result, then applies authoritative final positions
  -> Settled
```

Stale ShotStart updates are ignored. A client that joins while `Phase == Simulating` applies the latest static authoritative positions, does not start the current local simulation, and remembers the current ShotId. When the same ShotId later becomes `Settled`, the final authoritative snapshot is applied even though the late joiner had already observed that ShotId. The transition is covered by `SimulatingLateJoinAppliesSameShotSettledSnapshot`.

## Remote physics evidence

The first post-fix two-client Build & Test run used `StraightWeak`, fixedDeltaTime `0.006944444`, and shotId `1`.

| Checkpoint | Owner | Remote |
| --- | --- | --- |
| ShotStart / step 0 | position `(0, 0.04, -7.50)`, velocity `0` | same position, velocity `0` |
| Stroke applied | velocity `2.61 m/s`, moving `1` | velocity `2.61 m/s`, moving `1` |
| step 1 | observed motion, moving `1` | observed motion, moving `1` |
| step 10 | moving `1`, max speed `2.61` | moving `1`, max speed `2.61` |
| step 100 | moving `1`, max speed `2.61` | moving `1`, max speed `2.61` |
| final | position z `-4.55`, normal ShotEnd | position z `-4.57`, normal ShotEnd |

`Sample` logs contain shotId, role, simulationStep, fixedDeltaTime, ballId, position and velocity. Validation runs additionally enabled `SampleAll` and confirmed stepwise position changes for all ten balls. `StrokeApplied` occurred once per client per ShotId.

## Comparison metrics

Telemetry stores samples locally. No sample is synchronized every step. Comparison keys are `(shotId, simulationStep, ballId)`; only samples with the same key are compared.

Per-ball metrics are `MaxTrajectoryPositionErrors[ball]`, `MeanTrajectoryPositionErrors[ball]`, and `FinalPositionErrors[ball]`.

Per-shot metrics are:

- `MaxTrajectoryPositionError` / `MeanTrajectoryPositionError` across matched trajectory samples. The legacy `MaxPositionError` / `MeanPositionError` aliases now mean these trajectory metrics when a trajectory comparison is performed.
- `MaxFinalPositionError` / `MeanFinalPositionError` from the final snapshot.
- `FinalPositionError` for the stroke ball and `FinalCorrectionDistance` for the final authoritative snap distance.
- `SettleTimeError`, `SettleStepError`, and encoded event-sequence divergence.

The old PoC wording that treated a multi-metre correction as direct evidence of PhysX nondeterminism is no longer valid. The new measurements show that a large trajectory outlier can coexist with a small final correction; startup timing, sample alignment, and event timing must be investigated separately from final-state correction.

## Fixture matrix

The twelve fixtures were run three times in a two-client local Build & Test world in the A-owner direction. All 36 shots settled normally (`forced=false`). The table reports the observed range across the three runs; trajectory values are matched sample comparisons from the full `SampleAll` capture, and final values are Remote-vs-authoritative ShotEnd values.

| Fixture | A owner runs | trajectory max range (m) | final max range (m) | forced |
| --- | ---: | ---: | ---: | ---: |
| StraightWeak | 3 | 0.000135–1.530006 | 0.000001–0.068398 | 0/3 |
| StraightNormal | 3 | 0.000136–0.000816 | 0.000001–0.000952 | 0/3 |
| StraightStrong | 3 | 0.000135–0.000543 | 0.000000–0.000679 | 0/3 |
| BallCollisionFront | 3 | 0.000136–0.000951 | 0.000002–0.001089 | 0/3 |
| BallCollisionAngle | 3 | 0.000136–0.000137 | 0.000002–0.000271 | 0/3 |
| DoubleCollision | 3 + follow-up | 0.000147–0.000179 | 0.000092–0.073920 | 0/3 |
| MultiBallCollision | 3 | 0.000003–0.000684 | 0.000002–0.000831 | 0/3 |
| GateCenter | 3 | 0.000135–0.001226 | 0.000000–0.001363 | 0/3 |
| GateEdge | 3 | about 0.000135 | below 0.000001 | 0/3 |
| GatePost | 3 | 0 | below 0.000001 | 0/3 |
| LongRoll | 3 | 0.000134–0.000140 | 0.000002–0.000004 | 0/3 |
| VeryLowSpeedTouch | 3 | 0.000133–0.000135 | below 0.000003 | 0/3 |

The A-owner DoubleCollision follow-up used a two-second inter-shot validation delay and produced Remote ShotEnd records for all three runs.

The final A-owner rerun produced owner and remote ShotEnd records for all 36 shots. Across those records, remote local event counts were 0-14, event divergence was 0-2, settle-time error was 0-0.08333588 s, and settle-step error was 0-12; all ShotEnd records were `forced=false`.

The required reverse-direction representative matrix was also run three times per fixture. All 18 B-owner shots were normal:

| Fixture | B owner runs | Owner events | Remote events | final max range (m) | divergence range |
| --- | ---: | ---: | ---: | ---: | ---: |
| StraightWeak | 3 | 1 | 0–1 | 0.000001–0.000407 | 0–1 |
| StraightStrong | 3 | 3 | 2–3 | below 0.000001 | 0–1 |
| BallCollisionFront | 3 | 4 | 4–5 | 0.000002–0.000408 | 0–1 |
| MultiBallCollision | 3 | 13 | 14 | 0.000014–0.000016 | 1 |
| GatePost | 3 | 3 | 3 | below 0.000001 | 0 |
| GateCenter | 3 | 2 | 2 | below 0.000001 | 0 |

Collision fixture event evidence was real rather than a layout false positive: A-owner counts were Front 4, Angle 3, Double 7, and Multi 13; B-owner counts were Front 4, Multi 13, GatePost 3, and GateCenter 2. The event sequence remains diagnostic and is compared separately from position error.

## Forced-settlement investigation

The previous non-master StraightWeak timeout was caused by a stroke that reached the Udon method but did not change Rigidbody velocity on the VRChat client path. The diagnostic sequence showed `initialized=true`, `isKinematic=false`, zero velocity after `AddForce`, `observedMotion=false`, and `forced=true` at 30 seconds. Replacing that initial impulse with the equivalent mass-scaled velocity fixed the root cause.

After the fix, StraightWeak and StraightStrong settle normally in both ownership directions. The timeout guard remains enabled and was not used in the 36 A-owner or 18 B-owner matrix shots.

## CI and automated tests

The repository workflow now validates a general SemVer package version, requires the package URL to contain that version, requires the archive filename to match `<package-id>-<version>.zip`, and checks required author and dependency metadata without pinning a version-specific value. Future version bumps do not require a workflow edit.

Focused Unity validation after the fix:

```text
EditMode  Pm.Booth.Aoinu607.Udon.Gateball.Tests.Editor   17 passed
PlayMode  Pm.Booth.Aoinu607.Udon.Gateball.Tests.Runtime    8 passed
UdonSharp compile                                        0 errors
```

The SDK World Builder was run with two VRChat Build & Test clients. Validation also confirms the remote local event count, normal/forced distinction, simulation step, fixedDeltaTime, and final correction metrics.

## Known limitations

- The first shot after world startup can have a large trajectory outlier even when final correction is small; the cause is not yet isolated to startup scheduling versus physics timing.
- Event comparison is diagnostic and local. It does not make Remote events authoritative.
- The 30-second timeout is intentionally retained as an abnormal-state guard.
- Late Join behavior is covered by the deterministic state-transition test; a full automated two-process join/leave scenario is still an environment-level test.
- No gameplay-critical score, turn, or Spark rule should depend on local event timing yet.

## Go / Modify decision for v0.3

**MODIFY:** local physics execution and final authoritative settlement are operational, but the first-shot 1.530006–1.871013 m trajectory outliers and non-zero event divergence show that trajectory and event parity is not yet strong enough for gameplay-critical rules. Keep the current architecture and telemetry, isolate startup/sample timing, and repeat the matrix after that cause is removed. Do not add continuous synchronization or hide the divergence with mid-shot correction.
