# Failure patterns

## Fork PR integration

PR #7 introduced Mirror from the fork's `feature/mirror-v0.5.0` into `v0.5.0`.
These are historical identifiers, not default commands for future tasks.
Inspect actual remotes, PR head/base and dirty changes. Preserve current game
behavior while restoring network integration; do not take every conflict file
from one side.

After a merge, check Player prefab components, `.meta` pairs, spawnable prefabs,
OnlineLobby/build scenes, entry/build helpers, routing, assemblies, lifecycle,
identity, save access and input mapping. Keep unrelated imported fonts/vendor
metas, generated solutions, recovery files and user edits out of the change.
Past PR authorization is not permanent permission to publish future changes.

Mirror's URP conversion prompt concerns example assets, not the game's pipeline.
Initial asset indexing may run in the background. Nested NetworkIdentity errors
on `PrefabWithChildrenForClientScene` / `Child Network Identity` can come from
vendor test fixtures: locate the asset before changing game prefabs or deleting
vendor tests. Actual game prefabs need one root identity with no nested ones.

## Previous panels persist in Character Select

Changing `state` or starting `SelectSkinRoutine` does not hide previous UI.
Apply the SkinSelect view, deactivate previous panel roots and the left panel,
hide/suppress its cursor, then enter the embedded selection routine.
`UpdateOptionVisuals` may reactivate unsuppressed cursors. Inspect prompt title,
dynamic parents, cloned cursors and Rule/Team return paths too.
Persistent lobby canvas/EventSystem belongs only to the lobby and can obscure
or block configuration even with correct networking.

## Client start lacks sound, cursor animation or fade

Trace `CaptureOnlineView` -> room message -> `ApplyOnlineMenuView` ->
`ApplyOnlineFeedback`. A selected option/confirmation flag is insufficient.
Transmit start cursor position/frame, blink, music stop, sound/volume and fade
alpha/visibility. Sound sequence prevents replay on every snapshot. Clear
cached views on scene changes so old cues/fades do not return in a fresh menu.

## Wrong remote character or skin

Trace connection -> slot -> turn owner -> room settings -> base/runtime cache
-> pre-spawn LoadInto -> initial SyncVars -> skin controller -> HUD. Local-save
defaults or observer unlock clamps can overwrite correct network selections.
Do not globally unlock skins, send whole SaveData or confuse hardware P1 with
the remote player's id as a workaround.

## Victory skips celebration/Win Match or returns to lobby

The first online shortcut showed only the scoreboard and loaded the lobby.
Reuse native choreography on all peers and coordinate its final scene change.
Send actual surviving winners and match-completion result instead of guessing
from a client's delayed deaths. Freeze owner prediction and prevent normal
network animation/pose drivers from replacing replayed victory. Wait for peer
presentation completion. A completed match returns to Stage Select with room
configuration retained.

## Later round resolves only after another death/draw

The former check returned during the four-second scene-start grace and left
no retry. Later rounds skip the initial walk-in, making this timing easier to
hit. Another death after grace evaluated a draw/winner, making a lost event
look like stale round state.

Retain the death check until grace, spawn readiness and Revenge swap finish.
Register humans and COM, reset readiness on reload, latch it after every slot
spawns and do not require dead actors to remain active. A partial spawn roster
must not be treated as the final survivors. Use a fresh cancellable delay;
shared static waits can retain timing when stopped. Preserve cumulative wins.
Reproduce two rounds with an early second-round death and no further death;
a first-round-only test misses the failure.
