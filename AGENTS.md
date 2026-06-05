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

- Engine: Unity `6000.4.2f1`
- Language: C#
- Game type: 2D Bomberman-style action game
- Important packages already present:
  `com.unity.inputsystem`, `com.unity.2d.tilemap`,
  `com.unity.2d.pixel-perfect`, `com.unity.ugui`,
  `com.unity.test-framework`

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
  `SaveFileMenu`, `SkinSelect`, `ControlsMenu`, `WorldMap`, `BossRush`,
  `BattleMode_1`, and the `Stage_*` scenes.
- Scene routing and mode transitions are important parts of gameplay changes.
  Do not assume a code-only change is enough if a feature depends on scene or
  prefab wiring.

## Architecture map

- `Assets/Scripts/GameManager.cs`
  Stage orchestration, hidden objects, tilemap resolution, portal and round
  flow, enemy tracking
- `Assets/Scripts/GameSession.cs`
  Cross-scene session state
- `Assets/Scripts/PlayerPersistentStats.cs`
  Session bootstrap and persistent runtime player state
- `Assets/Scripts/PlayersSpawner.cs`
  Player spawning and player-count-aware setup

### Domain placement

- `Assets/Scripts/Abilities/`
  Player and mount ability components and the ability registry
- `Assets/Scripts/Itens/`
  Item ids, pickup behavior, and item prefab loading
- `Assets/Scripts/Bomb/` and `Assets/Scripts/Explosions/`
  Bomb placement, fuse timing, active bomb state, explosion behavior
- `Assets/Scripts/Enemies/`
  Enemy movement and enemy-specific reactions
- `Assets/Scripts/Bosses/`
  Boss-specific behavior, projectiles, intros, and sequences
- `Assets/Scripts/Mounts/`
  Louies, mounts, egg queue logic, and mount animation helpers
- `Assets/Scripts/Destructible/`, `Ground/`, `Indestructible/`,
  `StageAssets/`
  Tile interactions and stage gimmicks
- `Assets/Scripts/SaveSystem/`, `Unlock/`, `BossRush/`, `WorldMap/`
  Persistence, unlocks, progression, boss rush flow, and world progression
- `Assets/Scripts/TitleScreen/`, `SaveFileMenu/`, `Skin/`, `Controls/`,
  `Hud/`, `Mobile/`
  Menus, HUD, screen bootstraps, shared input, and mobile-specific flow

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
  `AutoItemDatabase.cs`. Item prefabs are loaded from `Resources/Items`.
- When changing bomb or explosion behavior, verify interactions with water,
  holes, destructible tiles, ground effects, indestructible effects, and chain
  reactions.
- When changing mounts or Louies, verify egg queue flow, mount ownership,
  animation helpers, and interactions with bombs, enemies, and stage tiles.
- When changing save or unlock behavior, route persistence through
  `SaveSystem` and player-facing unlock behavior through `UnlockProgress` when
  applicable.
- When changing menus or game flow, check return paths across `TitleScreen`,
  `SaveFileMenu`, `SkinSelect`, `WorldMap`, and `BossRush`.
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
- The Unity Test Framework package is installed, but there is no obvious
  first-party test suite under `Assets/` yet. Do not claim automated coverage
  unless you actually added or ran it.

## Safe defaults for agents

- Avoid editing generated files under `Library/` or `Temp/`.
- Avoid moving or renaming assets casually, because Unity `.meta` pairing
  matters.
- Do not clean unrelated untracked files or recovery scenes unless asked.
- If a task touches both code and Unity content, mention any scene or prefab
  follow-up that could not be validated from the terminal alone.
