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

For an approved effect scheduled in the future, its coroutine host must outlive
the actor that caused it. Use the existing manager's presentation helper and
capture prefab/event data instead of owner gameplay components. Cancel waits on
scene/client cleanup, reject inactive coroutine hosts and preserve host-native
effects. This protects received events only; a removed RPC endpoint needs a
separate authoritative transport. Never infer missing explosion gameplay.

For periodic or triggered arena tile visuals, gate the client's native clock and
gameplay entry points before replacing them with approved phase/frame histories.
Reuse only audited decorative tiles or generated tiles without colliders/prefabs.
Use scene/path/revision guards, explicit end/clear states, a bounded history and
the player transform timeline. A current-state heartbeat lets late observers
recover even when the open state has null tiles. Do not start an ignition/damage
routine merely to reproduce its animation; restore cached visual state on exit.

Do not draw a client-side random result just to overwrite it with an approved
path or to derive an audio wait. Copy the approved path and transmit the actual
wait chosen by the authority. Preserve the authority's original RNG order when
moving event publication. Diagnostic path fingerprints must use stable coordinate
arithmetic, not Unity RNG, entity ids or platform hashes. An approved fall start
alone does not establish its eventual terrain outcome or complete its timeline.

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

For an owner-fired projectile, correlate its real spawn with owner netId + the
original action tick. A speculative proxy must contain presentation components
only. Hand off to the real object's child visual where available, preserving
displayed position while NetworkTransform keeps owning the root. Use approved
snapshot age to bound continuation and read-only sweeps for known geometry;
never call impact/explosion/chain logic from prediction. Pause must freeze the
visual in world space despite buffered root changes, and resume must distinguish
an old pre-pause sample from a fresh one. Classify local presenter locks as
intentional waits in motion diagnostics, rather than counting them as free input
latency. In tests, construct required player colliders before dependent FX and
use a small tolerance for child world positions after changing a parent's pose.

Do not smooth a direction change by projecting the previous direction through a
wall. Small same-direction confirmation errors can decay; stops, wrapping and
rejected actions need their appropriate explicit transition. Never let a queued
stop terminate an unfinished arc, or let stale messages resurrect a completed
sequence. Pause freezes motion; cancellation/death/scene changes release it.

For an ability whose destination depends on random or hidden server state,
predict only its known preparation. Mole's startup gesture is correlated with
the C input tick and handed back at the authoritative next phase; it does not
start the real drill coroutine or protection. A manual presenter must re-enable
its active animation after mount rebind disables client behaviours, and the
player-head presenter must respect that same ownership. EditMode fixtures adding
abilities dynamically must initialize required Awake references explicitly; do
not confuse an uninitialized fixture with a runtime prediction failure.

For mounting/dismounting, separate the visual arc from ownership, egg transfer,
protection and completion callbacks. Predict a known manual dismount gesture from
approved eligibility/curve; mounting starts only with the approved actor/target.
Retain phase and pause for late observers and preserve the offline pixel-quantized
parabola. Queue approved landing bounces until the main arc ends, including their
stop. Keep native Rigidbody/transform coherent before online snapshots, without
changing offline velocity. Measure endpoint waits rather than granting gameplay
unlock early. Store raw directional input in history and mask a motor/replay copy
only while its gesture lock is still active; otherwise rejection loses the real
walking intent. Never replay ability edges to recover movement.

When adding approved external movement, first check the owner transport: an
owner that disables NetworkTransform updates can otherwise snap on every movement
reconciliation packet. A finite native authority scope can opt into interpolation
of already-decoded samples without enabling/resetting the component. Preserve the
presented root across reconciliation, prioritize specialized presenters, freeze
on pause and cut discontinuities instead of extrapolating through a wrap. Keep
server Rigidbody/Transform coherent before their two snapshot sources sample.
This is approved interpolation, not a whole-curve predictor; record that limit.

Pose histories must share the transform timeline, including send/timeline offsets.
Use mount type and generation to prevent buffered poses crossing replacements.
Interpolate compatible local offsets; discrete frames, visibility, color and flips
stay approved. Bound history and avoid timestamp traffic for unchanged mount poses.
Before gating a native client routine, trace its separate world visual: a cart
needs its own approved transport as well as the rider. A mechanism's local random
telegraph is not authoritative; publish its approved target/deadline/pause and
ensure the real gameplay object uses network spawn before motion events. Tests in
EditMode must use native collider prerequisites and isolated preview scenes; do
not create runtime scenes or require saving the user's open scene to test a view.

For an autonomous contact force, trace its clock before copying conveyor replay.
The server may consume zero to four player input steps before one world physics
step. Preserve the native pending MovePosition while other mechanisms still cast:
immediate body writes change their contacts, and summing forces changes native
last-target behavior. Deduplicate attached bodies before notifying recovery, and
publish changed authority positions even when their input acknowledgement repeats.
A native grid snap before bounce/hole checks is a separate transition; force
telemetry and snapshot fixes alone do not implement owner force prediction.

For bounded confirmed forces, restore the owner's own source geometry, phase and
budget before replay. Do not rewind the global mechanism. Expire stale approvals,
revoke contact even when actor position/input ack are unchanged, and bound exits
before unknown terrain or a phase reversal. A walking motor may recenter the
perpendicular row; compare its force contribution with the identical motor step
without force rather than assuming net displacement is additive across centering.

An enemy pull recipe must retain the native conditional input rule: Hurricane's
radial flee dot can classify sideways movement as fleeing. Project its known
historical modifier without granting a pull that a slow-only receipt did not
confirm. Bound known active animation frames and finite push distance, and never
sample AI/RNG to extend the phase. Preserve pending world targets across source
queries; conflicting world forces need authority rather than summed forecasts.

In native cast tests, assert actual contact/count/normal before testing motion,
advance physics explicitly and restore its simulation mode. Use coordinates close
to gameplay scale: the pusher fixture at71000 units found no hits while the nearby
kinematic fixture did. Do not infer a runtime body-type bug from that failed setup.

## Check collision geometry before predicting

A co-op scene disables original client gameplay components. Never reactivate
them to make Physics2D prediction work. Use server-approved native query views
through the existing co-op world coordinator; preserve collider tags/layers for
acknowledged pass capabilities. Do not clone original TileBase instances: tile
Startup can spawn gameplay objects. Generate plain collision tiles instead.
Wait for complete map/collider snapshots and fall back to authority for unknown
active shapes, without dropping the real input command. Refresh queries from
approved tile-motion events and remove them on scene exit. This establishes
known geometry; it does not predict autonomous mechanisms or terrain destruction.

Separate an ability's confirmed eligibility from its cooldown. If the only
remaining restriction is a known timer, project its deadline using the approved
snapshot timestamp and the estimated validation time of the next input; waiting
for another ready=true packet adds avoidable latency at the boundary. Do not
credit snapshot age twice on confirmation. Zero cooldown must never grant lost
eligibility, ownership or resources, and the server still validates every shot.
Test expiry, aged snapshots, disable/mount changes, rejection and pause/resume.

For multi-object commands, correlate proxies by the original input tick, action
and cell; do not manufacture ticks per object or use A's tick for a C ability.
Share stock reservations across ordinary and grouped placement until the
authoritative snapshot acknowledges them. Confirm each spawn separately so
partial rejection removes only unmatched presentation. Use shared read-only
geometry and resolve first-only variants for subsequent reservations without
spawning a temporary gameplay object. A cast may still have an offline gesture
when zero objects fit; confirm that gesture independently of spawn count. Audio
is once per command group and must not echo through co-op broadcast.

For authoritative followers, do not infer queue state or ownership from an
interpolated position. Send approved visibility, idle/facing and sorting on the
same observer timeline as their transform, including its timeline offset. Keep a
hidden network identity active for late observers, hide presentation separately,
and preserve the original hitbox states on the authority. The pure client must
never restore gameplay hitboxes. Destruction presentation takes over explicitly.
Static detached pickups need approved emotes, not invented extrapolation.

A shared transport must present both the carrier and its rider before disabling
native client boarding. Send approved render state and align it with rider time;
keep end poses queued even when occupancy changes. Rail samples with an approved
turn should follow their corner, not a diagonal. Cuts and hidden transitions need
explicit handling. Treat looping audio separately from one-shot broadcasts to
avoid duplicate starts. Confirm sorting independently of an owner's predicted
frame so leaving a carrier does not leave stale head ordering.

When native gameplay also creates particles, gating that gameplay must preserve
its approved VFX. Publish chosen render parameters after the server's existing RNG
draws, create renderer-only client views and reuse the visual curve. Do not start
the boarding/teleport routine to get its particles. Revisioned clears and disable
must cancel waiting visual coroutines as well as objects already instantiated.

## Diagnose what is actually delayed

Compare matching build/protocol captures and similar action sequences. Separate
input-to-first-motion, round-trip confirmation, visible correction, time waiting
at an endpoint, and local frame hitches. Server-now error is not necessarily a
visible correction when owner presentation is intentionally ahead. Sample after
the presenter, not only before LateUpdate; attach action kind, identity/revision,
input tick, segment duration and displacement to useful events. Do not infer
latency percentiles for an ability that the capture did not instrument.

## Validate the behavior and its limits

Use the [manual validation matrix](../../../Docs/multiplayer/matriz-validacao-predicao-online.md)
for per-ID scenarios and evidence. Its existence is not a completed test run;
record actual roles/builds/captures in the progress checkpoint separately from
implementation and automated results.

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


## Preserve full render poses for portals and transport

A runtime animation offset is not the full rendered transform: sink/rise may
scale or move SpriteRenderer transforms directly, and mount ancestors may add
offsets. Capture the pose relative to the replicated actor, including scale,
rotation and mask interaction. Apply it after frame refresh only to presentation
children; never rewind or scale a gameplay root. Restore the owner's original
visual baseline when approved motion returns to prediction, and rebind it when
the mount changes. Visibility and mask mode are discrete approved state.

A SpriteMask is not a SpriteRenderer and is absent from the co-op sprite stream.
Transport its approved render-only descriptor with bounded history, revision,
scene/time guards and an explicit empty snapshot. Cleanup must cover disabled
controllers as well as iterator disposal; stopping a coroutine alone is not a
sufficient visual cleanup contract. Preserve approved particles before gating
native portal routines, without running gameplay RNG again on clients.


A forced-motion exit can leave constraints that normal walking/replay still needs.
Transport that approved context (for example WaterCycleCurrent's exit barrier)
in NetworkMovementState and keep a separate owner copy; speculative queries must
not mutate the server's static world state. Restore the context on reconciliation.
For supports that center a rider even while stopped, keep approved pose ownership
until confirmed unmount; do not re-enable walking prediction merely because the
native input lock cleared. Keep one scope per rider and dispose after the final
unmount destination is applied. Use the same transform timeline offset for support
world poses and animation playback. Preserve each bomb's own timeline offset.
When restoring coupled flags, inspect setters: SetExternalMovementOverride clears
hazard damage, so restore the override before its saved hazard setting.

A support that allows free walking needs approved collision context in the motor
and replay, not a locked-flight scope. A boat's owner view can follow the predicted
root using renderer-only proxies and current head assets without granting native
water permission, occupancy or protection. Coordinate player/mount presenters,
freeze root/idle/frame on pause and yield to approved transport scopes. Hide the
duplicate co-op view using server renderer IDs with owner-scoped claims, restoring
its last approved visibility on release. Cache a definition key before native
anchoring/following; fail closed on missing or ambiguous definitions. Encode an
absent support compactly, and classify its diagnostic samples separately from
walking so new statistics do not mix unrelated motion contexts.


Client startup must not reset authored/approved field directions just to build a
read-only cache. For magnetic tiles, gate native scan/pull/rotation first and send
revisioned field phases with a late-observer heartbeat. Apply approved tile views
without resurrecting a cell already replaced by sudden death. Transporting the
field does not authorize the client to choose a target or simulate a real pull.


Do not tie confirmed world presentation to an owner's enabled state. Cancel that
player's unconfirmed commands/poses on disable, but retain approved bomb motion
while its network identity remains spawned. Tick only the presentation loop from
the existing manager's helper after NetworkTransform, never input or abilities.
Clean up on scene/identity/client stop and guard against advancing twice in one
frame during a disable/reactivate transition. This alone does not provide an
ownerless transport or make owner-hosted VFX coroutines survive deactivation.

For decorative tile deformation on collision tilemaps, keep the native collision
source intact. Sprite-collider animation frames can otherwise change the client
query geometry. Use generated plain visual tiles without prefabs/colliders, hide
only the source cell color, restore its flags/color on clear and retire overlays
immediately when terrain is replaced. Native TileBase assets may run gameplay
Startup; never reuse them in the visual layer. Publish approved frames after the
native coroutine step, with revisions, full late-observer state and explicit clear.

Audit interpolation cut thresholds against the native curve: Battle4 lifts a
visual20 tiles in100 ms, exceeding the normal4-unit pose cut between samples.
Use an authority-scoped, bounded limit recorded in each historical visual pose;
keep root teleport cuts, renderer/frame changes and delivery-gap guards separate.
Do not globally relax transform discontinuities to fix a fast sprite lift.

Plain SpriteRenderers need the same prompt discovery hook as animated visuals.
Register after their spawn is configured, deduplicate in the existing world
coordinator and exclude NetworkIdentity descendants. This still cannot capture
an effect born and destroyed between two visual ticks; use an approved event
for that case. Guard shared contact helpers as well as expression-bodied trigger
adapters: native sweeps can call the helper directly.

For an autonomous short bomb pull, use approved environment presentation rather
than masquerading as a kick or a player ability. Preserve the segment before an
already-arrived stop, start its visible clock at receipt, never extrapolate a new
cell or Rubber bounce and do not grant an ability gesture or change its collider.
Keep the native capture/fuse/damage/tile edits authoritative; emit a clear in both
iterator cleanup and explicit disable. This does not solve a removed RPC owner.

A shader-driven overlay needs its approved uniforms, not only its sprite and
material asset id. Blackout recipes include region/palette, room bounds and barrel
texture/UV/transform; clone material per view and hide when required data is unknown.
Submit fixed-capacity color arrays from the first update (Unity cannot later grow
them). Cache immutable material snapshots so changes are visible to delta checks.
Do not mutate templates or reconstruct local enemy gameplay to produce eyes.

A blackout may deactivate the GameObject hosting its own UI component. Keep an
external approved-state presenter with inactive references and idempotent visual
initialization; do not depend on its Update resuming itself. Preserve the native
clock: blackout alpha fades in unscaled time even during pause. Send logical world
lights rather than camera-dependent shader coordinates from a headless server.
Client room geometry can use Box transform/offset/size without enabling a collider,
but keep offline physics semantics and transport the server room's active status.

For approved NPC jumps, keep body and ground shadow on one transform timeline.
Register detached shadows/ripples promptly and propagate the audited motion profile
to their eye masks. Keep an already presented pose/frame during gameplay pause;
reapplying an old animation sample can rewind it even when local advancement is
paused. Continue receiving bounded history and confirmed clears. Distinguish this
from blackout's unscaled alpha. Restrict wrap/gap cuts to audited motion ranges;
a blanket distance threshold can break fast projectiles or large custom arcs.
Derived Start/Fixed/callback overrides need their own authority gates when they
bypass the base; checking only the base loop is insufficient.


For a dead player's projectile, inspect the spawn call before debugging its arc.
A registered prefab is insufficient: spawn the authoritative instance before its
first motion event so receivers can resolve its netId, and replicate a failure
removal. Distinguish approved Revenge launch from a living player's Punch pose;
it must not grant return eligibility or inherit an unrelated input prediction lead.
An inactive owner's retained presenter does not itself supply cart input, carrier
transport or confirmed player reactivation. Keep those checklist items explicit.


Scene suppression is not a replacement for native authority guards: OnEnable can
start a boss intro or lifetime before suppression runs, and shared contact helpers
can be called outside the normal loop. Audit explicit combat/kill/orbit/phase
entry points as well as Update. Do not alter global Physics2D layer collision rules
from a decorative client clone. Preserve the authority/offline RNG and phase clock;
these gates alone do not replicate missing telegraphs, intro masks or looping audio.


A dead owner's input cannot depend on that inactive Player's Update/FixedUpdate.
Dispatch through the existing manager, authenticate connection slot and identity,
and bind commands to cart id/generation/scene. Packet count grants no simulation
time. Bound the queue and cancel charge on overflow/timeout rather than fabricating
an A release. Keep raw input and each tick's dt; replay only the shared perimeter
motor, never charge, sounds or launch. Predict no further when approvals go stale.

A confirmed Revenge return is a separate presentation phase from being alive.
Reactivate rendering/approved Riding while keeping life0/dead/input/physics locked;
only a confirmed arc can bypass its visual death guards at both begin and update.
The final approved stats/position/ack reset old input and player poses before the
next bounce. Do not run LoadInto on the client and import the machine's base stats.
Apply role/identity/serial guards, initial late-observer phase and victim elimination.
Retain unrelated approved bomb motion through return cleanup.

Keep fixed world targets separate from a predicted carrier root: a landing marker
must stay on its approved cell rather than slide with the cart. Separate paused
root/frame state from already-approved unscaled child recoil. Exclude localConnection
from remote-only view messages: the host retains native actors and may intentionally
have no handler. Test the real host round/reload path, not only pure helpers.
