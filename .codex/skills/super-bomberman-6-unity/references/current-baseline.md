# Current baseline

Snapshot updated 2026-10-12. Verify the active tree rather than treating this
date as a release marker. The live online integration has been developed in
`codex/online-battle-flow`; do not assume it exists in every branch/worktree.

## Rendering and engine

The inspected checkout uses Unity 6000.6.0f1; repository instructions/old plans
may mention earlier versions. Resolve the live version from ProjectVersion.
The game uses URP with the 2D renderer, not Built-in. Graphics/Quality point to
`Assets/Settings/SuperBombermanURP.asset`; the renderer is
`Assets/Settings/SuperBombermanRenderer2D.asset`.

Preserve `Assets/Scripts/Camera/UrpPixelPerfectCameraFallback.cs`. The legacy
PixelPerfectCamera Built-in path does not provide the intended URP viewport.
The game baseline is PPU 16, 256x224 reference and integer scaling.
Safe-frame UI uses `PixelPerfectViewport` and `UICameraViewportFitter`, not
Camera.rect alone. New/migrated shaders use URP-compatible HLSL. New raster
assets use Default compression None with platform overrides disabled.

Unity 6000.5+ deprecates GetInstanceID and FindObjectsSortMode overloads:
follow the project convention `GetEntityId().GetHashCode()` for int keys and
`FindObjectsByType<T>()` (or the inactive overload when intentionally required).
Do not rewrite unrelated assets merely to address these warnings.

## Online and offline coexist

Title Battle Mode offers Offline first, then Online. Offline opens the existing
BattleModeMenu; Online opens the shared OnlineLobby before native configuration.
Only Battle Mode currently has the integrated online flow. Normal Game/Boss
Rush room modes are identifiers for future work.

The server owns room ids, configuration, gameplay decisions and scene changes;
human movement/normal player animation currently use owner simulation and
ClientToServer replication. COM simulate on the server. Client menu panels
observe the host, except each participant's sequential character-selection turn.
Pause is controlled by the host and presented on every peer.

Results reuse local celebration, scoreboard and Win Match presentation. A
completed online match returns to Stage Select retaining room settings;
ordinary round reloads retain wins. Death checks during spawn grace remain
pending until readiness. Read the online companion for exact owners and known
failure paths, rather than restoring obsolete shortcut behavior from old plans.

## Save, selection and audio boundaries

Public SaveSystem configuration setters often save internally. For offline
direct Data changes/non-saving setters, inspect whether an explicit Save is
needed. Online Battle uses temporary BattleData/BattleConfigurationData and a
snapshot whitelist; those setters intentionally do not persist room settings.
Do not mix room settings with campaign slots, local controls or unlocks.
Audio/voice settings intentionally use GameAudioSettings/PlayerPrefs.

Per-slot online character/skin choices populate base and runtime player state
before spawn and initial SyncVars. Render remote skins using the generated
catalog, without the observer's unlock clamp. Preserve local choice validation
and ordinary offline persistence.

## Verification and remaining scope

Run checks required by AGENTS for runtime/content edits and report blocked
Unity checks explicitly. C# compilation does not prove scene wiring or a
two-peer session. New tests are not automatically executed coverage. A connected
MCP instance must be checked against the intended checkout before use.

The user has tested the initial online and repeated-round fixes successfully;
this does not certify all arenas, mounts, abilities, network latency or input
platforms. Existing multiplayer plans contain historical audits and future
goals; current source and `Docs/multiplayer/online-battle-flow.md` take precedence.
