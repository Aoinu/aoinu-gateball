# Distributed Physics Synchronization PoC v0.2

## Result

The v0.2 path is implemented as a single manually synchronized `GateballNetworkState`.
The stroke owner publishes a ShotStart snapshot, every client applies that snapshot and
runs the existing Rigidbody simulation locally, and only the stroke owner publishes the
ShotEnd snapshot. A remote client records its local result, then snaps to the owner's
authoritative positions. No per-ball network synchronization or mid-shot correction is
used.

The state remains `Settled` after ShotEnd so a late joiner can reconstruct the latest
static result from synchronized variables alone. The next stroke is allowed from either
`Waiting` or `Settled`.

## Network architecture

`GateballStrokeRouter` is the only existing stroke entry point that changes for v0.2. If
`NetworkState` is assigned it requests the shared state to start a shot; otherwise the
v0.1 direct path remains available. `GateballNetworkState` owns the phase, shot identity,
ownership handoff, ShotStart data, ShotEnd data, settlement decision, and late-join
reconstruction. `GateballCourt` and `GateballBall` remain the local physics owners.

The flow is:

```text
Waiting or Settled
  -> owner request / ownership transfer
  -> ShotStart serialization
  -> owner and remote local Rigidbody simulation
  -> owner velocity threshold + settle delay
  -> owner ShotEnd serialization
  -> remote error measurement and immediate authoritative snap
  -> Settled
```

Only the authority that owns `NetworkState` may finish a shot. A stale deserialization
whose `shotId` is older than the locally observed shot is ignored; this is required when
ownership is transferred to a non-master client.

## Synchronized fields

| Field | Purpose |
| --- | --- |
| `ShotId` | Monotonic shot identity and duplicate/stale guard |
| `Phase` | `Waiting`, `Simulating`, or `Settled` |
| `AuthoritativeBallPositions[10]` | Current static source of truth for late join and ShotEnd |
| `InitialBallPositions[10]` | ShotStart ten-ball snapshot |
| `StrokeBallId` | Ball receiving the stroke |
| `StrokeDirection` | Normalized stroke direction |
| `StrokeImpulse` | Fixed stroke impulse |
| `FinalBallPositions[10]` | Owner ShotEnd snapshot |
| `ShotAuthorityPlayerId` | Player that began the shot |
| `ShotEndSimulationStep` / `ShotEndElapsedSimulationTime` | Owner settlement reference |
| `ShotEndEventSequence[128]` / `ShotEndEventCount` | Owner event sequence for divergence comparison |
| `ShotEndWasForced` | Indicates the abnormal-state timeout guard was used |

There is no `VRCObjectSync`, per-ball synced state, continuous position stream, midpoint
snapshot, remote interpolation, or deterministic replacement solver.

## Serialization strategy

ShotStart increments `ShotId`, captures all ten positions, stores the stroke, sets
`Phase = Simulating`, and calls `RequestSerialization`. The owner then applies the same
captured snapshot locally before applying the stroke. A remote applies the same snapshot
and stroke once when it observes a newer shot ID.

At normal settlement, the owner captures all ten final positions, copies them to the
authoritative positions, stores the event sequence, sets `Phase = Settled`, and serializes
again. A remote computes error against the owner snapshot before applying the immediate
snap. Remote event data is diagnostic only and never becomes rule state.

If the owner remains in simulation beyond `MaximumSimulationTime` (30 seconds by default),
the owner publishes a forced ShotEnd. This keeps an abnormal state from simulating
forever; `ShotEndWasForced` and the log/UI telemetry make that result distinguishable from
normal velocity-based settlement.

## Physics and telemetry

The v0.1 Rigidbody path is reused with these fixed values:

```text
Weak impulse       0.60
Normal impulse     1.00
Strong impulse     1.40
Ball diameter      0.075 m
Ball mass          0.230 kg
Out margin         0.075 m
Project timestep   0.020 s
```

The project `TimeManager` is configured for 0.020 s. The effective client-side fixed
step observed during Unity/ClientSim inspection was approximately 0.01111111 s; the
telemetry records `Time.fixedDeltaTime` per client and should be treated as the runtime
measurement for each Build & Test run.

Each client records identity, owner/remote role, shot ID, fixed-step index, simulation
time, wall-clock elapsed time, ten-ball position and velocity samples, and encoded events:
ball collision, gate crossing, gate-post collision, out, and settled. The comparison
reports maximum, mean, and final position error, settle-time error, settle-step error,
final correction distance, and event-sequence divergence.

## Test Shot fixtures

All fixtures reset all ten balls. The network-enabled fixture path then places the fixed
ten-ball layout before applying the fixture stroke.

| Preset | Stroke |
| --- | --- |
| `StraightWeak` | Ball 1, forward, 0.60 |
| `StraightNormal` | Ball 1, forward, 1.00 |
| `StraightStrong` | Ball 1, forward, 1.40 |
| `BallCollisionFront` | Ball 1 rightward into Ball 2, 1.00 |
| `BallCollisionAngle` | Ball 1 diagonal toward Ball 2, 1.00 |
| `DoubleCollision` | Ball 1 rightward through three-ball line, 1.40 |
| `MultiBallCollision` | Ball 1 rightward through four-ball line, 1.40 |
| `GateCenter` | Ball 1 forward through gate center, 1.00 |
| `GateEdge` | Ball 1 forward at gate edge, 1.00 |
| `GatePost` | Ball 1 forward at gate post, 1.00 |
| `LongRoll` | Ball 1 forward from the long-roll start, 1.40 |
| `VeryLowSpeedTouch` | Ball 1 rightward at low speed toward Ball 2, 0.60 |

The development-only `AutoRunOnStart` switch supports repeatable Build & Test runs. A
target player ID selects the initiating client; target ID `0` selects the local master.
It is disabled in the checked-in development scene after validation.

## Build & Test measurements

The following are completed two-client local-world observations. Values are metres unless
noted. `Forced` means the 30-second abnormal-state guard ended the shot.

| Preset / direction | Owner result | Remote comparison |
| --- | --- | --- |
| `StraightWeak`, Client A owner -> Client B | step 2534, 17.59762 s, normal ShotEnd | max 2.953425, mean 0.2953425, final 2.953425, correction 2.953425, divergence 0 |
| `StraightStrong`, Client A owner -> Client B | step 2706, 18.7921 s, 3 events, normal ShotEnd | max 8.046773, mean 0.8046773, final 8.046773, correction 8.046773, divergence 3 |
| `StraightWeak`, Client B owner -> Client A | step 4320, 30.00078 s, forced ShotEnd | max 2.953425, mean 0.2953425, final 2.953425, correction 2.953425, divergence 0 |
| `BallCollisionFront`, Client B owner -> Client A | step 4320, 30.00078 s, forced ShotEnd, 1 owner event | max 3.203938, mean 0.5521759, final 3.203938, correction 3.203938, divergence 4 |

These are PoC measurements, not acceptance thresholds. The large corrections and forced
settlements are intentionally visible evidence that local Unity physics is not yet
deterministic across clients. The collision fixture also needs refinement: this run did
not produce the expected owner-side ball-collision event before timeout.

The remaining fixtures are implemented and manually selectable but were not represented
by a completed numeric row in this validation pass. They must be included in a broader
reproducibility matrix before v0.3 relies on local physics for gameplay-critical rules.

## Automated validation

The focused Unity tests pass after the v0.2 changes:

```text
EditMode  Pm.Booth.Aoinu607.Udon.Gateball.Tests.Editor   15 passed
PlayMode  Pm.Booth.Aoinu607.Udon.Gateball.Tests.Runtime   8 passed
UdonSharp compile                                      0 errors
```

The SDK Builder was run with two VRChat Build & Test clients. Both clients loaded the
development world, produced local ShotStart/RemoteShotStart traces, and produced
owner-only ShotEnd followed by RemoteShotEnd traces for the completed samples.

## Go / Modify decision for v0.3

Go to a v0.3 investigation only with telemetry retained and expanded to all fixtures and
ten-run samples. Modify before gameplay use if the target is deterministic rule authority:
the current samples show multi-metre local divergence, non-zero event divergence, and
forced settlement on non-master ownership. The next experiment should compare a reduced
physics surface or a server/authority simulation; it should not hide this divergence with
mid-shot correction until the measurement matrix explains its causes.
