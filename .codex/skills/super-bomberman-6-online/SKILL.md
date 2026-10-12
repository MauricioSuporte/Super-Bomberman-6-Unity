---
name: super-bomberman-6-online
description: Implement, debug, or polish Mirror multiplayer in Super Bomberman 6 Unity, including room slots, Battle Mode configuration, character selection, network gameplay, pause, round results, and coordinated scene transitions. Use for online changes or shared gameplay changes that affect network authority or presentation.
---

# Super Bomberman 6 Online

Extend the existing scene-driven game and `BombermanNetworkManager` partials.
Follow the active checkout's `AGENTS.md`. This is an evolving Battle Mode
implementation, not completed multiplayer for every mode.

## Find the live implementation

Confirm checkout, branch, dirty files, Unity version, package manifest and
enabled scenes. A fork PR, an open Editor and the shell can refer to different
checkouts. Verify the Editor project root before using Unity MCP. Do not assume
the PR's original feature branch is the current integration branch or that
session changes have been committed.

Read `Docs/multiplayer/online-battle-flow.md` and the touched runtime owners.
`plano-multiplayer.md`, `netcode-architecture.md`, `mirror-battlemode-poc.md`
and `lobby-plan.md` record useful PR history and future goals. Some assertions
(no lobby, IMGUI UI, fully server-simulated movement, lobby return after a
completed match, old engine versions and line numbers) are obsolete. Resolve
conflicts against current code and the latest user requirements.

## Load the relevant reference

- [Architecture](references/architecture.md): room/configuration flow,
  actual authority, persistence, spawn, skins and pause owners.
- [Failure patterns](references/failure-patterns.md): PR integration, client
  UI remnants, audiovisual transitions, celebrations and repeated rounds.
- [Validation](references/validation.md): multiplayer reproduction, available
  tests, compilation limits and areas still needing polish.

## Invariants

- The server owns P1-P6 slot assignments, rules, damage, gameplay RNG, gameplay
  spawns, scores and scene transitions. Connection id, player id and local
  hardware input slot are different identities.
- Human movement and normal player animation currently use owner simulation
  and ClientToServer replication. COM movement/player animation are simulated
  on the server. Inspect `PredictLocally`, `isOwned` and `syncDirection` before
  adding a gate; `ShouldSimulateLocally` alone does not describe this model.
- Only the host edits configuration or controls pause. Each participant controls
  only their character-selection turn using local P1 controls.
- Room configuration is a temporary `SaveSystem` copy. Do not write it into
  a client's offline save or clamp a remote skin against the observer's unlocks.
- Presentation accompanies authority: cursor/frame, sound, confirmation, blink,
  fade, victory, scoreboard and Win Match. Scene/step replication is insufficient.
- Reuse local victory/draw choreography. A completed online match returns all
  peers to Battle Mode Stage Select with room settings retained. Lobby return
  serves the existing disconnection/reconfiguration path.
- Deaths during spawn protection or Revenge swaps remain pending. Reevaluate
  when ready, reset readiness each round and retain cumulative match wins.

Trace changes through sender, authority validation, receiver, runtime cache,
presenter and scene/prefab wiring. Extend current owners instead of adding a
parallel lobby or match flow. Preserve Offline. Run the checks required by
`AGENTS.md`; distinguish tests added, tests run, user-reported success and
unvalidated polish. Use the general project skill when broader gameplay or
rendering conventions are relevant and that skill is available.
