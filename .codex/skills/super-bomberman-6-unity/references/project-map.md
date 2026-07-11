# Project Map

Confirm volatile facts from `ProjectSettings/ProjectVersion.txt`,
`ProjectSettings/EditorBuildSettings.asset`, and `Packages/manifest.json`.
Treat this file as an ownership map, not a replacement for inspecting the live
tree.

## Cross-scene anchors

- `Assets/Scripts/GameManager.cs`
  Per-stage tilemap resolution, hidden content, enemy tracking, portal and
  campaign flow, Battle Mode round/match orchestration, timers, and item drops.
- `Assets/Scripts/GameSession.cs`
  Cross-scene active-player mask for ids 1-6, match wins, and shared life or
  elimination state.
- `Assets/Scripts/PlayerPersistentStats.cs`
  Runtime player loadouts, selected character/skin, stage pickup transactions,
  and `BeginStage`/`CommitStage`/`RollbackStage` state.
- `Assets/Scripts/PlayersSpawner.cs` and `Assets/Scripts/PlayerIdentity.cs`
  Spawn ownership, player identity, skin application, Boss Rush participation,
  and Battle Mode Man/COM/Off setup.
- `Assets/Scripts/Controls/PlayerInputManager.cs`
  Keyboard, controller, mobile, and synthetic input aggregation.

## Major gameplay domains

- `Assets/Scripts/Abilities/`
  Player and mount abilities. `AbilitySystem` caches components and
  `AbilityRegistry` maps ids to component types.
- `Assets/Scripts/Itens/` and `Assets/Scripts/AutoItemDatabase.cs`
  Item ids, pickup/custom behavior, stage persistence, expulsion, and prefab
  lookup from `Assets/Resources/Items/`.
- `Assets/Scripts/Bomb/` and `Assets/Scripts/Explosions/`
  Placement, active limits, fuse, movement, variants, chain reactions, and
  explosion propagation.
- `Assets/Scripts/PlayerMovimentation/` and `Assets/Scripts/Health/`
  Player movement/riding/animation locks and character death/life behavior.
- `Assets/Scripts/Enemies/`
  Enemy movement controllers and enemy-specific interactions.
- `Assets/Scripts/Bosses/`, `Assets/Scripts/Bombers/`,
  `Assets/Scripts/StageIntro/`, `Assets/Scripts/EndStage/`, and
  `Assets/Scripts/EndScreen/`
  Bosses and Bomber encounters, intro/outro choreography, campaign completion,
  Game Over, and ending/credits flow.
- `Assets/Scripts/Mounts/`
  Louies and other mounts, ownership, world pickups, egg queue, movement,
  manual dismount, animators, and ability SFX.
- `Assets/Scripts/Destructible/`, `Ground/`, `Indestructible/`,
  `StageAssets/`, and `Interface/`
  Tile resolvers, stage props, and extension contracts for explosion, kicked
  bomb, shadow, movement, and external-animation behavior.

## Battle Mode

- `Assets/Scripts/BattleModeMenu/` owns the complete pre-match flow.
- `Assets/Scripts/BattleMode/` owns rules and arena controllers;
  `Assets/Scripts/BattleMode/COM/` owns COM decisions and arena providers.
- `Assets/Prefabs/BattleMode/BattleModeSystems.prefab` supplies shared rules,
  teams, Revenge Bomber, and Sudden Death systems to every arena.
- `Assets/Scripts/BattleRevenge/`, `Assets/Scripts/SuddenDeath/`, and Battle
  overlays under `Assets/Scripts/Hud/` own round-specific presentation.
- Resolve the current arenas from `EditorBuildSettings.asset`. The current
  project includes `BattleMode_1` through `BattleMode_15`.

## Progression, UI, and scene flow

- `Assets/Scripts/SaveSystem/`
  JSON save data, slot/difficulty progress, controls, video/mobile settings,
  Battle Mode configuration, Boss Rush times, and compatibility normalization.
  `StageUnlockProgress` owns campaign-stage unlock progression.
- `Assets/Scripts/Unlock/` and `Assets/Scripts/Achievements/`
  `UnlockProgress` owns skins, Boss Rush, Hardcore, Battle arenas, and Golden
  Bomber. `AchievementCatalog` and `AchievementsMenu` own achievement data and
  presentation.
- `Assets/Scripts/BossRush/`
  Boss Rush bootstrap, menu, timing, unlocks, and session state.
- `Assets/Scripts/Skin/`, `Assets/Scripts/Editor/`, and
  `Assets/Scripts/Hud/`
  Character/skin selection, generated sprite sheets, runtime animation, and
  normal/Battle portrait presentation.
- `Assets/Scripts/Localization/` and `Assets/Scripts/Sound/`
  Four-language text/font fallback and global music/SFX/voice settings.
- `Assets/Scripts/TitleScreen/`, `SaveFileMenu/`, `Controls/`, `Pause/`,
  `WorldMap/`, and `Mobile/`
  Screen bootstraps, navigation, remapping, pause routes, world progression,
  and touch controls.
- `Assets/Scripts/Discord/`
  Persistent Rich Presence bootstrap and scene/mode activity mapping.

## Runtime-loaded content and tests

- `Assets/Resources/Items/`, `Sprites/`, `HUD/`, `Sounds/`, `UI/`,
  `BattleMode/`, and `Systems/` contain content loaded by string path.
- `PauseAutoLoader` and `EndingScreenAutoLoader` instantiate
  `Resources/Systems` prefabs; input, mobile, unlock, Discord, and optional
  Normal Game AI also use runtime bootstraps.
- `Assets/Tests/EditMode/BattleModeComEditModeTests.cs` contains the current
  first-party Edit Mode coverage for synthetic input and COM helpers.
