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


## Co-op sprites arrive but disappear behind tilemaps

With CustomAxis Y sorting and Chunk TilemapRenderers, empty authored margins
in Tilemap.origin/size affect the sort anchor. ClearAllTiles plus occupied-cell
reconstruction shrinks those margins: Stage2-3 water/ground can cover correctly
received boats and springs. Compare map bounds/renderer center as well as the
sprite's visibility, color, material and pose. Preserve authoritative origin/size
in full snapshots and deltas; do not fix this by globally raising sprite orders.
B09 NG08/10 evidence is in Docs/multiplayer/correcoes-playtest-normal-game-2026-11-12.md.

## Spotlight material becomes ready after an initial plain fade

A cinematic Image may first arrive as a hidden plain fade while its native
material initializes. When it later receives spotlight parameters, release its
old coopFadeSamples entry: the continuous fade presenter otherwise keeps
zeroing Image.color.a and deactivating it after every correct spotlight update.
Test the hidden-fade -> spotlight-visible -> next presentation-frame edge,
including a fresh scene stream. Preserve the native shader and light parameters.


### Rendered Development smokes must prove the effective save directory

Setting SB6_SAVE_DIRECTORY in a child environment is insufficient evidence of isolation. SaveSystem previously accepted it only when Application.isBatchMode; rendered Development clients/offline/host silently used the user's actual save. Never launch older binaries for rendered campaign fixtures. Development builds now accept the explicit override too, and LocalPredictionSmoke.ValidateSaveIsolationGuard checks the effective resolved path before mutating session/save state. Require the method in the immutable DLL before launch, require the actual path log, and compare the real save SHA before/after each controlled run. Preserve an incident snapshot and establish backup provenance before any recovery; an old empty save must not replace current progress.

### Disabling reliable position interpolation can corrupt owner delta decoding

NetworkTransformReliable.OnDisable calls ResetState, including lastDeserializedPosition. Disabling the component in OnStartLocalPlayer after initial spawn deserialization loses the compression origin while the server continues sending deltas. Ordinary reconciliation hides the error; approved authority motion reading clientSnapshots then shifts by the negated spawn position (observed water current owner P1 +7, P2 -7). Keep the decoder enabled. PlayerNetworkTransform overrides only UpdateClient for the local owner; observers retain native interpolation and the owner motor owns its root. Regress initial→delta across OnStartLocalPlayer and inspect actual owner/observer motion, not just server completion.

### Native room changes are cuts under black

Replicating the selected camera alone can show first-room roots interpolating into the new room. World3RoomTwoTransition uses PlayerNetworkTransform.ServerRoomCut and immediate owner reconciliation when placing actors under black. The native reset happens once; the existing teleport RPC is reused, but its delayed local host echo must not reset an encoder that already sent a new delta. Plain ServerTeleport reset both times and shifted the remote host reader after the first new packet. Inspect the first rendered frames, per-player viewport and one active MainCamera; a native transition completion marker cannot prove client focus.


### Preparation must preserve the same reader cuts as native room placement

An authority-only test teleport with Rigidbody/Transform and reconcile can still be overwritten by an owner's old position stream. Use the existing safe room cut for fixture setup as well; verify actual contact position before diagnosing a rejected pickup. RestoreMounted(None) intentionally does not dismount: explicit setup teardown must clear the old mount before restoring another one. Headless renderers cannot prove visible material treatment; check native component configuration there and require actual rendered owner/observer views separately.

### Observe blink and bounce on their native presentation timeline

CharacterHealth's hit blink uses alpha0.2, so a fixture waiting for alpha<0.1 never tests its alleged invisible phase. Inspect the native implementation before choosing phase thresholds. NetworkPlayerAnimation's player_visual_sample runs before NetworkBombFx.LateUpdate; its root sample cannot prove the final bounce pose. Observe after production presenters (for example EOF) and retain each role. Explosion world proxies also need actual renderer observations: absence of BombExplosion/Geyser gameplay components on a client does not prove absence of the approved visual.

### Native recovery bounce in multiroom stages

A minecart landing on a solid bomb can bounce through the border of Room1 and stop in an empty inter-room gap if PlayerPushedOutOfInvalidTile wraps against the global three-room Tilemap bounds. Capture the authored room when the native recovery starts and use the same interior bounds as airborne Bomb wrap; preserve global bounds for stages without rooms. Verify both shuttle directions, foot/mounted actors, real owner input after landing and the final EOF presentation on owner/observer. Source fix and four regressions entered B22; do not claim B21 binaries contain it.

For WaterCycleCurrent fixtures, score corridor ground/wall cells at tile centers. IsBlockedAhead queried inside an offset contact may optimize to the current cell and falsely report a clear corridor. Clear only destructible cover through its native owner without a temporary break-prefab collider, preserve ground/walls, then walk naturally into the unchanged current. Confirm both owners/senses and active pause with complete captures. Raw peer/server timeline subtraction is not a rendered-error metric during pause/interpolation; retain local EOF trajectories and approved phase boundaries.

Correction to the preceding recovery note: B22 gate rejected two new edge cases. A presence/camera collider is broader than the actual board, so copying Bomb wrap arithmetic alone is insufficient. The subsequent source revision derives the painted Ground+Indestructibles footprint whose centers belong to the authored room, only when recovery is actually blocked. This revision awaits B23; do not present the failed B22 formula as validated.


### Safe-frame typography: rich text, whitespace and paused clients

StageLabel must derive physical zoom from PixelPerfectViewport.ResolveFinalPixelRect, clamp zoom before dividing by authored designUpscale and Canvas.scaleFactor, and rebuild its current local view on resize. Pure-client GamePauseController.Update returns early, so the label owns its local LateUpdate presentation. Scale TMP base fontSize together with rich-text size spans: inter-span spaces and empty lines otherwise remain at authored size. Preserve the authored base per TMP object instead of recapturing an already scaled value on every enable. Verify normalized preferred bounds and actual PNGs, not only glyph pointSize. Intro probes cannot wait on scaled time while IntroRunning freezes Time.timeScale; explicitly reject paused menus as intro evidence. B24 was rejected for these gaps despite numeric markers; B25 has meaningful regressions and offline/actual-peer acceptance recorded in the Normal Game checkpoint.
