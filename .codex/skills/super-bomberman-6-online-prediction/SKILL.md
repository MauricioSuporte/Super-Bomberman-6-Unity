---
name: super-bomberman-6-online-prediction
description: Implement or diagnose responsive online gameplay in Super Bomberman 6 using owner prediction, server validation, rollback and continuous presentation of short movement segments. Use for input latency, reconciliation hitches, bomb abilities, forced player movement and tile-to-tile bounces; not for lobby/UI flow alone.
---

# Responsive online gameplay

Use the current repository AGENTS.md and the online companion's authority rules.
Read [the maintained implementation notes](../../../Docs/multiplayer/prediction-and-reconciliation.md)
for the actual baseline; source code wins over historical descriptions. This skill
records techniques, not authorization to deploy, publish or disconnect rooms.

## Choose the timeline from the cause

Trace the offline action first: input edge, actor/target, movement lock, geometry,
collider changes, pose, sound, completion and interruption. Distinguish:

- Owner input with a known safe result: start bounded presentation immediately,
  correlate with the existing input tick, then confirm or roll back. Never wait
  for the first RPC just to animate an already predictable action.
- Autonomous movement or hidden/random decisions: stream approved trajectories.
  Preserve short arcs in order rather than speculating a random next direction.
- Known deterministic continuation: bridge a small confirmation gap only where
  local collision queries establish a safe exit. Existing Kick uses at most one
  cell / 150 ms; reassess these bounds for another mechanic rather than copying
  them blindly. Wrapping, holes, room seams and moving actors require authority.

When the user authorizes replacing a random rule with deterministic behavior,
put the pure algorithm in a shared domain script. RubberBombBounceSequence is an
example: stable launch seed + ordinal, explicit uint arithmetic, and no Unity
RNG/frame time/instance ids/platform hashes. Send seed, ordinal and geometry;
reset on a new launch and test independence from global RNG. Predict only the
bounded guaranteed portion; direction determinism does not imply deterministic
collision, termination, damage or wrapping. Repeated identical launches now
repeat the pattern, so disclose that intentional rule change.

Prediction changes presentation, not who decides game rules. Do not start bomb
fuse/damage/world coroutines, draw gameplay RNG, spawn duplicate bombs or edit
terrain speculatively on a pure client. Offline and host retain their normal path.

## Extend the existing owners

- NetworkPlayerInput samples local hardware slot 1, independent of assigned P1-P6.
  Send real directional intent and explicit action edges through its existing
  command. Movement replay must not resample hardware, emit sounds or activate
  abilities again. A local animation lock may change replayed motor input without
  erasing the original command sent to the server.
- NetworkBombFx and its partials already own bomb prediction, confirmation,
  playback and cleanup. BombMotionKind identifies presentation; it does not grant
  an ability or replicate authoritative Bomb flags. Extend this path where it
  fits, instead of installing a second NetworkBehaviour or manager.
- Rebuild movement-relevant historical state before each replay tick. A visually
  moving/airborne bomb must not still be treated as a stationary obstacle by the
  motor. Capture and restore collider/sorting state on rejection, transfer, stop,
  death, disable and timeout. Give approved moving terrain the same current/next
  cell reservations that the offline motor sees.
- Per-bomb serial revisions prevent an old actor's RPC from taking back a bomb
  after another player handles it. While an owner command is pending, a previous
  presenter's in-flight stop cannot cancel that prediction; use the owner's
  input acknowledgement or bounded timeout to resolve it.

## Preserve motion across short segments

NetworkTransform must keep receiving/decoding deltas, but its older interpolation
position must not overwrite the active presenter. Owner reconciliation can also
restore an older root position. Restore the active presentation before collision
queries and after reconciliation, then apply the final pose in the established
LateUpdate order. Mounted animation runs later and needs the same ownership gate.

Use the offline curve and pixel-grid convention. Bomb arcs use sine height;
PlayerPushedOutOfInvalidTile uses a parabola. Account separately for curve duration
and the completion time rounded to FixedUpdate steps. A 1-tile hop may be shorter
than message age: sampling every received hop at server-now can skip its apex.
Queue approved continuations and their final stop until the current arc ends,
carry only the bounded fractional-frame remainder, and avoid adding the initial
prediction lead again at every tile. Bound queues and recover after stalls.

Mounted jumps keep a ground trajectory separate from visual height. Apply parent
offsets before setting the shadow's world position, and restore both on every
exit. Owner progress and observer interpolation use different clocks; tests must
use the clock of the role they exercise. Presentation helpers must not grant the
jump's authoritative invulnerability or start its world/gameplay coroutine.

Trace an action sound's meaning before predicting it. Red Louie's punch sound
signals a hit offline, so only the gesture starts speculatively. Confirm hit
audio once; an action RPC plus GameAudioSettings' generic co-op broadcast can
otherwise play the same sound twice. Preserve the original input tick and phase
when confirming a pose, including when the visual has already completed.

When composing moving ground with the motor, execute its displacement once per
network input tick and skip that actor in the arena's separate physics loop.
Replay must restore corner target/incoming direction and the acknowledged global
phase, rather than mutating shared presentation back to an older phase. Include
base speed, modifier and remaining simulated duration in speed context; batched
replay cannot expire a modifier from the current rendered frame's wall clock.
Keep autonomous bombs and global control tiles authoritative. Test a corner
entered before its center, a newer global phase, blocked exits and double pushes.

Do not smooth a direction change by projecting the previous direction through a
wall. Small same-direction confirmation errors can decay; stops, wrapping and
rejected actions need their appropriate explicit transition. Never let a queued
stop terminate an unfinished arc, or let stale messages resurrect a completed
sequence. Pause freezes motion; cancellation/death/scene changes release it.

## Diagnose what is actually delayed

Compare matching build/protocol captures and similar action sequences. Separate
input-to-first-motion, round-trip confirmation, visible correction, time waiting
at an endpoint, and local frame hitches. Server-now error is not necessarily a
visible correction when owner presentation is intentionally ahead. Sample after
the presenter, not only before LateUpdate; attach action kind, identity/revision,
input tick, segment duration and displacement to useful events. Do not infer
latency percentiles for an ability that the capture did not instrument.

## Validate the behavior and its limits

Use the existing OnlineRoomEditModeTests fixtures for immediate presentation,
confirmation/rejection, replay, uint revisions, stale presenters, early/late arc
packets, final-stop ordering, collider restoration and offline/host guards. Include
observer and mounted presentation when touched. Run C# and Unity checks required
by AGENTS.md; manual two-peer feel is a separate validation and must be reported
as such. No source-only test proves zero input/render latency.

A changed wire struct or new RPC requires matching authenticator and directory
protocols plus compatible client/server builds. Follow the user's publication
authorization; this skill does not supply it. Keep dirty scenes/fonts/recovery
assets intact and do not close an Editor with unsaved work to run validation.
