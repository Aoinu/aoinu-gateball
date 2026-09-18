# Gameplay Integration v0.3

v0.3 is a Gateball Prototype + Multiplayer Gameplay Prototype. It adds the
minimum multiplayer gameplay loop on top of the v0.2 Distributed Physics PoC.
It is not a complete implementation of official competition rules.

## State diagram

```text
Setup
  -> WaitingForStroke
  -> Simulating
  -> ResolvingShot
  -> WaitingForStroke       (no extra stroke)
  -> SparkPlacement          (valid Touch)
  -> WaitingForSparkStroke
  -> Simulating              (Spark stroke uses the normal ShotStart path)
  -> WaitingForStroke        (continued stroke)
  -> GameOver                (all ten balls are Goal or Manual End Match)
```

`GateballNetworkState.PhaseWaiting`, `PhaseSimulating`, and `PhaseSettled`
remain the physics phases. `GateballGameplayState.GameplayPhase` is the
separate gameplay phase and must not be used as a Rigidbody settlement signal.

## Distributed Physics boundary

The v0.2 architecture is unchanged:

```text
current controller / Shot Authority
  -> one ShotStart snapshot (positions, stroke, Spark target impulse)
  -> Owner and Remote apply the same initial conditions at FixedUpdate
  -> every client runs local Rigidbody physics
  -> only the Shot Authority resolves gameplay events
  -> one ShotEnd authoritative position snapshot
  -> every client applies the static authoritative result
```

Remote Touch, Gate, Out, and Goal observations are diagnostic only. They never
modify authoritative gameplay state. There is no continuous ball position
sync, mid-shot correction, VRCObjectSync on individual balls, or deterministic
physics solver.

## Player and ball assignment

Match mode requires at least two valid players. Players are sorted by
`playerId`, then Ball 1 through Ball 10 are assigned round-robin. The assignment
is synchronized as `ballId -> playerId` in `BallControllerPlayerIds`.

Ball teams remain fixed and independent of controller assignment:

```text
Red:   1, 3, 5, 7, 9
White: 2, 4, 6, 8, 10
```

Practice mode is available with one player and does not require assignment or
turn progression.

## Match start and turns

Starting Match mode resets all ten Rigidbody balls and all gameplay arrays,
sets Ball 1 as the current ball, clears score/gate/Touch/Spark/Out state, and
enters `WaitingForStroke`. The normal order is `1 -> 2 -> ... -> 10 -> 1`;
Goal balls are skipped. A Gate pass grants an extra stroke. A valid Touch enters
the Spark flow before the next stroke decision.

Only the current ball's assigned player may request a Match stroke. When the
current player changes, that player takes ownership of the shared NetworkState
GameObject immediately before beginning the next ShotStart. A stroke is not
accepted before this ownership check.

## Gate and score rules

Each ball has authoritative progress:

```text
0 = no Gate passed
1 = Gate 1
2 = Gate 2
3 = Gate 3
4 = Goal
```

Only a forward crossing of the next required gate advances progress. Scores are
`+1` for each Gate and `+2` for Goal. Both per-ball scores and Red/White team
totals are synchronized.

Goal requires Gate 3 progress and authoritative Goal Pole contact. All ten Goal
states produce `GameOver`; Manual End Match is also available from the controls.

## Touch and Spark

The Shot Authority records the current striker's main Ball-Ball Touch and
synchronizes `DidTouch` and `TouchedBallId`. A playable target enters
`SparkPlacement`.

The development Scene supplies a simple Spark placement control. It chooses a
fixed forward-adjacent candidate, with right/back/left methods available for
simple UI wiring. Candidate positions stay inside the court and avoid other
balls. The striker ball is fixed at the adjacent position until the Spark
stroke.

The Spark stroke reuses the normal `direction + impulse` representation. The
ShotStart carries one additional target Ball impulse; all clients apply both
impulses locally. After ShotEnd, the Spark stroke returns to
`WaitingForStroke` for the same ball.

## Out simplification

The Shot Authority stores an Out state and `LastOutBallId`. An Out ball is not
removed from the turn order. When it becomes current again, it is returned to a
safe position near the south boundary using a deterministic Ball-number offset,
then its active Out flag is cleared before the stroke. This is the v0.3
simplification; official placement and foul rules are not implemented.

## Late Join and disconnect recovery

Late joiners receive the Manual-sync snapshot containing gameplay phase, current
ball/controller/authority, assignment, score, gate progress, Out/Goal states,
Touch/Spark state, and the v0.2 authoritative static ball positions. A client
joining during physics simulation does not replay the current ShotStart; it
shows the latest static state and converges when the matching ShotEnd arrives.

If the current player leaves, the remaining players are deterministically
reassigned to the departed player's balls and the current controller is moved
to the new assignment. If the Shot Authority leaves during simulation, the new
owner rolls back to the last confirmed authoritative static snapshot and
returns to `WaitingForStroke`; the shot can be retried. The system does not try
to continue a half-observed shot.

## Practice mode

Practice mode accepts strokes for any non-Goal ball and keeps the v0.2 local
physics, Gate, Touch, Out, and Goal detection paths active. It does not assign
players, advance Match turns, or update Match team totals. Use the Practice
control in the development Scene for one-player testing.

## Unsupported official rules

The following remain outside v0.3: the complete ten-second rule, thirty-minute
match timer, all foul rules, every compound Touch/Spark edge case, referee and
ranking systems, persistence, replay, spectators, AI, multiple courts, and
polished art/audio/VFX.

## Automated validation

The focused Unity test assemblies cover the v0.1/v0.2 physics contracts plus
the v0.3 assignment, turn, Goal skip, Gate/score, extra stroke, Touch/Spark,
Out, GameOver, disconnect reassignment, snapshot copy, Gate resolution,
Spark placement, and late-join state application helpers.

The final validation record for this checkout is maintained below:

```text
UdonSharp compile: succeeded, 0 errors (Unity 2022.3.22f1, 2026-09-18)
EditMode tests: passed, 29/29
PlayMode tests: passed, 13/13
VRChat SDK World Builder Build & Test: Task RanToCompletion, fault none; Build-only and Run-with-result API returned True and produced a local .vrcw URL. A second VRChat client was not available in this checkout, so two-client interaction was not claimed.
Repository validation: package metadata passed, credential-signature scan passed, and git diff --check passed for text changes. Full git status/diff remains blocked by the installed git-vrc filter failing to create its Windows signal pipe.
```
