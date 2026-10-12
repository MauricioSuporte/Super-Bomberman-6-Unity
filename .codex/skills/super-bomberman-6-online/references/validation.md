# Validation and remaining polish

## Multiplayer reproduction

Both peers must use current sources. Rebuild the graphical client after runtime
changes; an old executable does not test new code. `Assets/Editor/OnlineClientBuild.cs`
provides Build -> Online Client (Windows/Linux), boots TitleScreen and writes
under `Build/OnlineClient/`. Dedicated `HeadlessBootstrap`/`-server` is a
separate path, not a substitute for testing graphical host menus.

Use Editor plus fresh executable, Host in one and Join `127.0.0.1` in the other.
Use the host's LAN address for LAN testing. Direct IP does not supply internet
room discovery or relay. Pick relevant checks:

- Offline first at Title and unchanged offline persistence.
- Stable slots, two participants, six-slot limit, departure/reused holes;
  host assigns empty slots COM/Off.
- Lobby controls disappear; same native configuration panels, options, cursors
  and restore prompts appear on clients without accepting host-only edits.
- Local P1 controls character selection only on that participant's turn;
  host handles COM. Different characters/skins stay attached to their slots
  and render on every peer with differing local unlocks.
- Start sound, music stop, cursor/frame, blink and fade on both peers.
- One human/COM spawn per slot; loadout, HUD, bombs, pickups, destructible cells
  and affected arena behavior agree across peers.
- Host-only pause/menu/confirmation, ignored gameplay commands, resume/exits
  and synthetic input cleanup.
- Two rounds, early second-round death during grace, no further death needed,
  cumulative wins retained. Include Single/Tag, surviving teammates, Draw and
  Time Up when affected.
- Complete celebration/scoreboard; target wins -> Win Match/audio -> Stage
  Select retains settings and starts a new match score.
- Client disconnect returns remaining peers to lobby; host stop returns clients
  to Title. Leaving clears pause, synthetic inputs and static caches while
  restoring offline settings.

## Checks and evidence

`Assets/Tests/EditMode/Editor/OnlineRoomEditModeTests.cs` defines slot, room
settings/whitelist, skins, native panel/transition, client pause, lobby and
consecutive-round regressions. Some EditMode tests enter PlayMode; Editor API
tests belong in the Editor assembly. Existing
`Assets/Tests/EditMode/BattleModeComEditModeTests.cs` covers COM/input helpers.
Inspect current test names before choosing filters.

Run C# and Unity compilation required by `AGENTS.md`. Generated csproj files
may omit new scripts until refreshed: successful compilation must include the
touched files. Check the correct Unity MCP instance/root and editor readiness.
An open checkout blocks a second batch Unity: report it and use the correct
connected Editor when possible, without closing the user's Editor unasked.

Check introduced warnings, strict UTF-8/BOM integrity and `.meta` pairing.
Existing Mirror/vendor warnings differ from newly introduced game warnings.
Documentation-only skill updates do not require Unity compilation.

Evidence from this implementation: user-reported success includes the online
flow and early-death/repeated-round fix; C# compilation passed. An earlier
five-test room suite ran successfully. Later panel/pause/skin/round regressions
were added and compiled but execution was blocked by the open Editor.
Do not claim that all current tests, arenas or multiplayer combinations passed.

## Remaining areas to inspect when relevant

This is a working beginning with substantial polish remaining. These are
follow-up areas, not instructions to expand every task:

- Authority/RNG/tile events across all fifteen arenas and non-explosion gimmicks.
- Mounts, mounted COM, eggs, hopping, glove/kick/punch and unusual character
  poses; companions can have different authority from their player.
- Latency, disconnect races, acknowledgement timeout, late messages, pause/fade
  timing, multi-controller input and mobile paths.
- Owner movement validation/reconciliation and authoritative security checks;
  current owner movement is not complete server simulation.
- Dedicated-server parity, readiness UX, discovery/relay/EOS, reconnect and host
  migration. A roadmap item is not evidence of implementation.
- Normal Game/Boss Rush co-op, enemy/boss replication and progression/save
  boundaries; only Battle Mode is currently integrated.
