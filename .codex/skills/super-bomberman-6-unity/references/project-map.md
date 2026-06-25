# Project Map

## Repo anchors

- `Assets/Scripts/GameManager.cs`
  Per-stage orchestration. Handles hidden objects, enemy counting, tilemap
  resolution, portal spawning, restart flow, and stage progression.
- `Assets/Scripts/GameSession.cs`
  Cross-scene session state.
- `Assets/Scripts/PlayerPersistentStats.cs`
  Session bootstrap and longer-lived player runtime state.
- `Assets/Scripts/PlayersSpawner.cs` and `Assets/Scripts/PlayerIdentity.cs`
  Player identity, player count, and spawn ownership.

## Major gameplay domains

- `Assets/Scripts/Abilities/`
  Player and mount abilities. `AbilitySystem.cs` caches ability components on
  the player object, and `AbilityRegistry.cs` maps ability ids to component
  types.
- `Assets/Scripts/Itens/`
  Pickup enum and runtime pickup flow. `ItemType.cs` defines item ids,
  `ItemPickup.cs` applies default behavior or custom behavior, and
  `AutoItemDatabase.cs` loads item prefabs from `Resources/Items`.
- `Assets/Scripts/Bomb/` and `Assets/Scripts/Explosions/`
  Bomb placement, active bomb limits, fuse timing, and explosion behavior.
  `BombController.cs` is the main integration point for player bomb behavior.
- `Assets/Scripts/Enemies/`
  Enemy movement controllers and enemy-specific interactions.
- `Assets/Scripts/Bosses/`
  Boss-specific behavior organized by boss folder, plus intro or projectile
  helpers near the owning boss.
- `Assets/Scripts/Mounts/`
  Louie and mount behavior, animators, egg queue flow, and mount-specific
  interactions.
- `Assets/Scripts/Destructible/`, `Ground/`, `Indestructible/`,
  `StageAssets/`
  Tile and stage-gimmick behavior. Use these before inventing a new global
  stage system.

## Progression, UI, and scene flow

- `Assets/Scripts/SaveSystem/`
  Disk persistence, slots, controls, stage progress, and Boss Rush times.
- `Assets/Scripts/Unlock/`
  Unlock events and unlock persistence.
- `Assets/Scripts/BossRush/`
  Boss Rush bootstrap, menu, timing, unlocks, and session state.
- `Assets/Scripts/TitleScreen/`, `SaveFileMenu/`, `Skin/`, `Controls/`,
  `Hud/`, `WorldMap/`
  Menu bootstraps, screen flow, HUD layout, and world navigation.
- `Assets/Scripts/Mobile/`
  Mobile controls and input bridge behavior.

## Scenes currently present

- `Assets/Scenes/TitleScreen.unity`
- `Assets/Scenes/SaveFileMenu.unity`
- `Assets/Scenes/SkinSelect.unity`
- `Assets/Scenes/ControlsMenu.unity`
- `Assets/Scenes/WorldMap.unity`
- `Assets/Scenes/BossRush.unity`
- `Assets/Scenes/BattleMode_1.unity`
- `Assets/Scenes/Stage_1-1.unity` through `Assets/Scenes/Stage_2-7.unity`

## Package assumptions visible in this repo

- `com.unity.inputsystem`
- `com.unity.2d.tilemap`
- `com.unity.2d.pixel-perfect`
- `com.unity.ugui`
- `com.unity.test-framework`

Treat those packages as part of the project baseline when planning changes.
