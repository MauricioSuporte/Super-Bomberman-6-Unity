# AGENTS.md

This file gives repository-specific guidance for AI coding agents working on
`Super-Bomberman-6-Unity`.

## Scope

- Follow this file for any work in this repository.
- Prefer repository conventions over generic Unity habits when they conflict.
- If available in your environment, the local skill at
  `.codex/skills/super-bomberman-6-unity/` is the best companion reference for
  deeper workflows.

## Project baseline

- Engine: Unity `6000.4.5f1`
- Language: C#
- Game type: 2D Bomberman-style action game
- Important packages already present:
  `com.unity.inputsystem`, `com.unity.2d.tilemap`,
  `com.unity.2d.pixel-perfect`, `com.unity.ugui`,
  `com.unity.test-framework`, `com.unity.render-pipelines.universal`

## Rendering pipeline and pixel-perfect output

- This project uses URP with the 2D renderer. Do not restore, assign, or add
  Built-in Render Pipeline assets/settings; Unity Hub reports that pipeline as
  deprecated and it is not compatible with the project baseline.
- The active pipeline assets are
  `Assets/Settings/SuperBombermanURP.asset` and
  `Assets/Settings/SuperBombermanRenderer2D.asset`. Keep Graphics and Quality
  settings pointing to the URP pipeline asset.
- New or migrated shaders must be URP-compatible HLSL shaders. Do not add
  Built-in-only `CGPROGRAM`/`UnityCG.cginc` shaders without an explicit,
  reviewed compatibility plan.
- The installed legacy `PixelPerfectCamera` package does not render its
  Built-in pipeline path correctly under URP. Preserve
  `Assets/Scripts/Camera/UrpPixelPerfectCameraFallback.cs`; it applies the
  SNES pixel-perfect viewport to gameplay cameras and draws the black bars.
- Safe-frame UI must use `PixelPerfectViewport` and the
  `UICameraViewportFitter` components. Do not use `Camera.rect` alone to infer
  the final output rectangle. The current scene baseline is PPU 16 with a
  256x224 reference resolution and integer scaling.

## Source of truth

- Treat `Assets/`, `Packages/`, and `ProjectSettings/` as the main project
  source.
- Do not treat `Library/`, `Temp/`, `Logs/`, `UserSettings/`, or `.vs/` as
  hand-edited source unless the task explicitly targets generated outputs.
- Be careful with `Assets/_Recovery/`. It may contain user recovery scenes and
  should not be deleted or cleaned up unless the user asks.

## Entry points and scene flow

- The primary local run flow starts from `Assets/Scenes/TitleScreen.unity`.
- Other important scenes include:
  `Achievements`, `SaveFileMenu`, `SkinSelect`, `ControlsMenu`, `WorldMap`,
  `BossRush`, `BattleModeMenu`, `BattleMode_1` through `BattleMode_15`, and the
  `Stage_*` scenes. Read `ProjectSettings/EditorBuildSettings.asset` for the
  live enabled list and ordering.
- Scene routing and mode transitions are important parts of gameplay changes.
  Do not assume a code-only change is enough if a feature depends on scene or
  prefab wiring.

## Architecture map

- `Assets/Scripts/GameManager.cs`
  Stage orchestration, hidden objects, tilemap resolution, portal and round
  flow, enemy tracking
- `Assets/Scripts/GameSession.cs`
  Cross-scene state for active player ids 1-6, Battle Mode wins, and shared
  life/elimination sessions
- `Assets/Scripts/PlayerPersistentStats.cs`
  Session bootstrap, runtime loadouts, selected character/skin, and stage
  commit/rollback state
- `Assets/Scripts/PlayersSpawner.cs`
  Player spawning, skin application, player-count-aware setup, and Battle Mode
  Man/COM/Off configuration
- `Assets/Scripts/Controls/PlayerInputManager.cs`
  Shared keyboard, controller, mobile, and synthetic input path

### Domain placement

- `Assets/Scripts/Abilities/`
  Player and mount ability components and the ability registry
- `Assets/Scripts/Itens/`
  Item ids and pickup behavior; the root-level `AutoItemDatabase.cs` loads item
  prefabs from `Resources/Items`
- `Assets/Scripts/Bomb/` and `Assets/Scripts/Explosions/`
  Bomb placement, fuse timing, active bomb state, explosion behavior
- `Assets/Scripts/Enemies/`
  Enemy movement and enemy-specific reactions
- `Assets/Scripts/Bosses/`, `Bombers/`, `StageIntro/`, `EndStage/`,
  `EndScreen/`
  Boss/Bomber behavior, projectiles, intros, outros, Game Over, and ending flow
- `Assets/Scripts/Mounts/`
  Louies, Mole/Tank mounts, world pickups, egg queue, dismount, and animation
  helpers
- `Assets/Scripts/Destructible/`, `Ground/`, `Indestructible/`,
  `StageAssets/`, `Interface/`
  Tile interactions, stage gimmicks, and extension contracts
- `Assets/Scripts/BattleMode/`, `BattleModeMenu/`, `BattleRevenge/`,
  `SuddenDeath/`
  Battle rules, arenas, COM AI, pre-match configuration, Revenge Bomber, and
  Sudden Death
- `Assets/Scripts/SaveSystem/`, `Unlock/`, `BossRush/`, `WorldMap/`
  Persistence, unlocks, progression, boss rush flow, and world progression
- `Assets/Scripts/Skin/`, `Editor/`, `Hud/`
  Character/skin selection, generated sprite sheets, runtime animation, and
  HUD portraits
- `Assets/Scripts/Localization/`, `Sound/`, `Achievements/`, `Pause/`,
  `Discord/`
  Localized UI, global audio settings, achievements, pause, and Rich Presence
- `Assets/Scripts/TitleScreen/`, `SaveFileMenu/`, `Controls/`, `Mobile/`
  Menus, screen bootstraps, shared input, and mobile-specific flow

## Working rules

- Extend existing systems before introducing new managers, registries, or
  persistence layers.
- Put code in the closest matching domain folder under `Assets/Scripts/`.
- Use the root of `Assets/Scripts/` only for truly cross-cutting runtime
  coordinators.
- Match the naming style already used in the repo, such as:
  `*Controller`, `*Bootstrap`, `*Resolver`, `*Presenter`, `*Ability`,
  `*MovementController`, `*Sequence`
- Keep changes small and compatible with current scene-driven architecture.

## Integration rules

- When adding an ability, check both `AbilitySystem.cs` and
  `AbilityRegistry.cs`.
- When adding or changing an item, check `ItemType.cs`, `ItemPickup.cs`, and
  the root-level `AutoItemDatabase.cs`. Item prefabs are loaded from
  `Resources/Items`. Also check `PlayerPersistentStats` and Battle Mode
  positional item arrays when the effect persists or is configurable.
- When changing bomb or explosion behavior, verify interactions with water,
  holes, destructible tiles, ground effects, indestructible effects, and chain
  reactions.
- When changing mounts or Louies, verify egg queue flow, mount ownership,
  world pickup/dismount, animation helpers, COM behavior, and interactions with
  bombs, enemies, and stage tiles.
- When changing save or unlock behavior, route persistence through
  `SaveSystem`; use `StageUnlockProgress` for campaign stages and
  `UnlockProgress` for global/player-facing unlocks. Inspect each public
  `SaveSystem` setter's persistence contract; many configuration setters save
  internally, but direct `SaveSystem.Data` changes and non-saving setters
  require an explicit `SaveSystem.Save()`. Audio volume and voices are the
  intentional exception and use `GameAudioSettings`/`PlayerPrefs`.
- When changing menus or game flow, check return paths across `TitleScreen`,
  `SaveFileMenu`, `SkinSelect`, `WorldMap`, `BossRush`, `BattleModeMenu`,
  `Achievements`, Game Over, and Ending.
- When changing Battle Mode, inspect `BattleModeRules`, `BattleModeMenu`, the
  shared `BattleModeSystems.prefab`, `GameManager`, save/unlocks, HUD overlays,
  arena COM integration, and pause/return behavior.
- When changing characters or skins, keep resource catalogs, Editor generators,
  selection, P1-P6 persistence, runtime animation, Boss Rush/Battle Mode, and
  HUD portraits aligned.
- When adding UI copy, update all four languages in `GameTextDatabase` and
  apply Japanese font fallback where needed.
- Before adding a persistent singleton, search existing
  `RuntimeInitializeOnLoadMethod`, `DontDestroyOnLoad`, `*AutoLoader`, and
  `*Bootstrap` flows.
- If a change is player-facing, consider whether it also needs updates to scene
  references, prefabs, `Resources.Load(...)` paths, audio hooks, animations,
  or serialized defaults.

## Validation expectations

- Do not trigger Unity builds or script compilation after code edits unless the
  user explicitly asks for it.
- Prefer targeted manual validation in the touched scene or mode.
- For shared gameplay changes, validate both single-player and multiplayer
  behavior when practical.
- If input flow changes, consider keyboard, controller, and mobile code paths.
- If persistence changes, consider both fresh saves and existing saves.
- Existing first-party Edit Mode coverage lives in
  `Assets/Tests/EditMode/BattleModeComEditModeTests.cs`. Inspect or extend it
  for covered COM/input helpers, but do not claim automated coverage unless the
  relevant tests were actually run.

## Safe defaults for agents

- Avoid editing generated files under `Library/` or `Temp/`.
- Avoid moving or renaming assets casually, because Unity `.meta` pairing
  matters.
- Do not clean unrelated untracked files or recovery scenes unless asked.
- If a task touches both code and Unity content, mention any scene or prefab
  follow-up that could not be validated from the terminal alone.
