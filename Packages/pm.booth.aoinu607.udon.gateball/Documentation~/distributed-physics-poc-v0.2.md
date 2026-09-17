# Distributed Physics Synchronization PoC v0.2

## Decision

**GO.** Owner and Remote now receive one `ShotStart`, apply the same initial snapshot and stroke at a FixedUpdate boundary, and run local Rigidbody physics. The startup outlier is explained by the validation harness starting a shot before the second client had joined; after both clients were ready, the first shot was in the same sub-millimetre range as later shots. Event differences in the representative runs were only `Settled` missing/extra callbacks. Authoritative ShotEnd correction remains the gameplay boundary.

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

## FixedUpdate-only ShotStart result

Render-frame deserialization only copies network state and raises pending flags. The following state changes are now performed only by `GateballNetworkState.FixedUpdate()`:

- applying the initial ball positions and resetting Rigidbody velocity/angular velocity;
- recording trajectory step 0;
- applying the stroke exactly once; and
- entering the local simulation step sequence.

`ShotStart` and `RemoteShotStart` log the receive frame and fixedDeltaTime. `StrokeApplied` logs `simulationStep=0`; the next checkpoint is step 1. The final Build & Test logs show the same sequence on Owner and Remote, with `fixedDeltaTime=0.006944444`, `step 0`, `step 1`, `step 10`, and `step 100` samples. No `Update()` path changes Rigidbody state.

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

## Startup first-shot investigation

The historical multi-metre values are retained as pre-fix history. They were measured before FixedUpdate-only application and with the earlier matrix runner. The old B-owner run also advanced past its first preset while ownership transfer was still pending, so its first recorded ShotId was not the advertised first fixture.

The post-fix four-fixture matrix used 20 shots per direction. In the A-owner run, shot 1 reached `ShotEnd` on the owner before the remote had a local ShotStart sample, so that shot is explicitly marked join-incomplete rather than treated as a Physics trajectory comparison. In the B-owner run, the remote did receive shot 1, but it reproduced the startup-only trajectory outlier (`1.697610 m` max). Both cases occurred while the world was still establishing the two-client session.

The harness was then rerun with no production-code delay or network change: it only waited for both Build & Test clients to join before starting the first fixture. Five `StraightWeak` shots in each direction produced these paired `SampleAll` results:

| Direction | first-shot trajectory max | all five trajectory max range | forced ShotEnd |
| --- | ---: | ---: | ---: |
| A owner -> B remote | `0 m` | `0–0.000135 m` | `0/5` |
| B owner -> A remote | `0.000135 m` | `0.000135 m` | `0/5` |

The startup outlier therefore tracks an incomplete/initializing two-client measurement, not a steady-state Physics divergence. The production path does not add a magic delay; the logs now expose the receive frame and step so an early-start attempt is distinguishable from a valid paired trajectory.

## Historical fixture matrix (pre-FixedUpdate validation)

The twelve fixtures were run three times in a two-client local Build & Test world in the A-owner direction before the FixedUpdate-only change. All 36 shots settled normally (`forced=false`). The table reports the observed range across the three runs; trajectory values are matched sample comparisons from the full `SampleAll` capture, and final values are Remote-vs-authoritative ShotEnd values. These values remain as pre-fix history; the final additional matrix below is the acceptance result.

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

### Final additional matrix

After the FixedUpdate change, the required four fixtures were run five times in each direction. All 40 scheduled shots settled normally (`forced=false`). The full-capture post-fix trajectory comparison paired 20/20 B-owner shots and 19/20 A-owner shots; the one unpaired A-owner shot is the join-incomplete startup case described above. For the paired steady-state shots, trajectory max stayed at or below `0.001226 m` A-owner and `0.001088 m` B-owner, with mean trajectory error below `0.000001 m` per shot. The warmup five-shot runs removed the only metre-scale first-shot values.

Final ShotEnd records from the classification reruns were also normal for all 40 shots. Excluding the join-incomplete startup record, final correction was sub-millimetre to low-millimetre; the largest startup-warmup correction was `0.000271321 m` A-owner and `0.000135422 m` B-owner.

## Event divergence classification

The existing encoded event sequence is decoded into `eventType`, `ballId`, `targetBallId`, `gateIndex`, and sequence index. Telemetry records missing-remote, extra-remote, different-type, different-ball, different-target, different-gate, ordering-only, duplicate, gameplay-critical, and diagnostic counts. It also logs the decoded event payload for each unmatched sequence entry.

The two 20-shot classification reruns produced:

| Direction | divergent shots | missing | extra | different fields | ordering-only | duplicate | gameplay-critical | diagnostic |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| A owner -> B remote | 11/20 | 5 | 6 | 0 | 0 | 0 | 0 | 11 |
| B owner -> A remote | 10/20 | 3 | 7 | 0 | 0 | 0 | 0 | 10 |

Every logged unmatched event was `type=5` (`Settled`), with the corresponding ball id and no target/gate. There were no Ball-Ball Touch, Gate crossing, Gate post, or Out gameplay-critical divergences. The remaining differences are diagnostic timing/callback presence differences, not an event payload disagreement. The comparison does not deduplicate gameplay events or synchronize events mid-shot.

## Debug UI metric policy

Runtime ShotEnd telemetry can compare final authoritative state and correction distance, but it does not have the Owner's complete trajectory without adding per-step network synchronization. The in-world Debug UI therefore displays `trajectory metrics offline validation only` and does not show a misleading zero or stale runtime trajectory value. The offline `SampleAll` parser and the local `(shotId, simulationStep, ballId)` samples remain the source of trajectory max/mean values.

## Forced-settlement investigation

The previous non-master StraightWeak timeout was caused by a stroke that reached the Udon method but did not change Rigidbody velocity on the VRChat client path. The diagnostic sequence showed `initialized=true`, `isKinematic=false`, zero velocity after `AddForce`, `observedMotion=false`, and `forced=true` at 30 seconds. Replacing that initial impulse with the equivalent mass-scaled velocity fixed the root cause.

After the fix, StraightWeak and StraightStrong settle normally in both ownership directions. The timeout guard remains enabled and was not used in the 36 A-owner or 18 B-owner matrix shots.

## CI and automated tests

The repository workflow now validates a general SemVer package version, requires the package URL to contain that version, requires the archive filename to match `<package-id>-<version>.zip`, and checks required author and dependency metadata without pinning a version-specific value. Future version bumps do not require a workflow edit.

Focused Unity validation after the fix:

```text
EditMode  Pm.Booth.Aoinu607.Udon.Gateball.Tests.Editor   18 passed
PlayMode  Pm.Booth.Aoinu607.Udon.Gateball.Tests.Runtime    8 passed
UdonSharp compile                                        0 errors
```

The SDK World Builder was run with two VRChat Build & Test clients. Validation also confirms the remote local event count, normal/forced distinction, simulation step, fixedDeltaTime, and final correction metrics.

## Known limitations

- A shot started before the second Build & Test client has joined is not a valid paired trajectory measurement. The receive-frame and ShotStart/StrokeApplied logs expose this condition; gameplay should use the authoritative ShotEnd for that startup case.
- Event comparison is diagnostic and local. It does not make Remote events authoritative.
- The 30-second timeout is intentionally retained as an abnormal-state guard.
- Late Join behavior is covered by the deterministic state-transition test; a full automated two-process join/leave scenario is still an environment-level test.
- No gameplay-critical score, turn, or Spark rule should depend on local event timing yet.

## Go / Modify decision for v0.3

**GO:** the FixedUpdate boundary makes Owner and Remote start semantics equivalent; Remote physics executes step-by-step in every valid trial; normal shots did not force-settle; the startup metre-scale result is explained by a pre-join/incomplete comparison and disappears after both clients are ready; steady-state trajectory error is millimetre-level or below; gameplay-critical event divergence was zero in the representative classification runs; final corrections are normally sub-millimetre to low-millimetre; and the Debug UI no longer presents an invalid runtime trajectory value. Continue to keep trajectory and event metrics diagnostic, with authoritative ShotEnd as the gameplay boundary.
