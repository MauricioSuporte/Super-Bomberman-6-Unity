# Battle Mode

Use this reference for Battle Mode rules, menus, arenas, round flow, stage
gimmicks, unlocks, and mode-specific integration. Use
`battle-mode-com-abilities.md` for the COM decision contract itself.

## Contents

- [Ownership map](#ownership-map)
- [Runtime flow](#runtime-flow)
- [Add or change an arena](#add-or-change-an-arena)
- [Preserve arena unlock semantics](#preserve-arena-unlock-semantics)
- [Items, Louies, and handicap](#change-selectable-items-louies-or-handicap)

## Ownership map

- `Assets/Scripts/BattleModeMenu/BattleModeMenu.cs`
  Owns player control modes, embedded character/skin selection, teams, rules,
  stage selection, items, Louies, handicap, music, and the transition into an
  arena.
- `Assets/Scripts/BattleMode/BattleModeRules.cs`
  Materializes saved match settings and starting loadouts inside an arena.
- `Assets/Prefabs/BattleMode/BattleModeSystems.prefab`
  Provides shared `BattleModeRules`, `BattleModeTeams`,
  `BattleRevengeSystem`, and `BattleSuddenDeathController` components. Keep it
  present in every Battle Mode arena.
- `Assets/Scripts/GameManager.cs`
  Owns the round timer, player/team victory resolution, draw/time-up behavior,
  round restarts, match completion, item drops after death, and return to
  `BattleModeMenu`.
- `Assets/Scripts/GameSession.cs` and `Assets/Scripts/PlayersSpawner.cs`
  Own active players 1-6, match wins, spawning, and Man/COM/Off setup.
- `Assets/Scripts/BattleMode/`
  Contains arena-specific controllers and shared rules.
- `Assets/Scripts/BattleMode/COM/`
  Contains the COM controller, item/mount abilities, arena abilities, hazard
  providers, diagnostics, and the stage ability loader.
- `Assets/Scripts/BattleRevenge/` and `Assets/Scripts/SuddenDeath/`
  Own Revenge Bomber and falling-tile round pressure.
- `Assets/Scripts/Hud/`
  Owns the Battle HUD and draw, time-up, round-win, scoreboard, and match-win
  overlays. Every arena currently includes
  `Assets/Resources/HUD/BattleModeHud.prefab`.
- `Assets/Scripts/SaveSystem/`, `Assets/Scripts/Unlock/`, and
  `Assets/Resources/BattleMode/`
  Own persisted configuration, arena unlocks/achievements, and stage
  miniatures.

## Runtime flow

1. Route from `TitleScreenBootstrap` to `BattleModeMenu`.
2. Let `BattleModeMenu` write settings through `SaveSystem` setters. The menu
   embeds `BomberSkinSelectMenu`; do not create a parallel Battle Mode skin
   flow.
3. Resolve the selected arena as `BattleMode_<index>` and load it only after
   saving player modes, teams, rules, stage, content amounts, and handicap.
4. In the arena, let `BattleModeRules` read the saved configuration and let
   `PlayersSpawner` attach `BattleModeComController` only to COM players.
5. Let `GameManager` resolve time-up, draw, winner/team score, round restart,
   and final match flow. Coordinate Revenge Bomber and Sudden Death cleanup
   before reloading or leaving the scene.
6. Keep pause routes aligned with restart round, return to stage select, and
   return to title behavior in `GamePauseController`.

## Add or change an arena

1. Read `ProjectSettings/EditorBuildSettings.asset` for the live arena list.
   The current project contains `BattleMode_1` through `BattleMode_15`.
2. Copy the closest arena pattern and preserve the shared
   `BattleModeSystems.prefab`, player spawns, HUD, tilemap names, layers, and
   serialized references.
3. Put arena runtime logic under `Assets/Scripts/BattleMode/`; reuse handlers
   under `Ground/`, `Destructible/`, `Indestructible/`, `StageAssets/`, and
   contracts under `Interface/` before adding special cases to bombs.
4. Register the scene in `ProjectSettings/EditorBuildSettings.asset` and add
   its miniature at `Assets/Resources/BattleMode/BM<index> Miniature.png`.
5. Check `BattleModeMenu` code and serialized scene values for stage count,
   scene naming, display data, stage lock hint, and any arena-specific settings
   or handicap profile.
6. Check `SaveSystem` stage-count clamps, win arrays, data normalization, and
   migrations. Preserve existing arrays when their size changes.
7. If the arena is locked, update `UnlockProgress`, `AchievementCatalog`,
   `UnlockToastCatalog`, localized unlock text, and icon Resources together.
8. Add or reuse an `IBattleModeComStageAbility` through
   `BattleModeComStageAbilityLoader` when the gimmick changes COM navigation,
   danger, planting, or kick trajectories.
9. Inspect Discord Rich Presence when adding a new scene or changing party
   limits or mode labels.
10. Validate Man and COM play, Single and Tag matches, pause/exit, round
    restart, time-up/draw, Revenge Bomber, and Sudden Death as applicable.

## Preserve arena unlock semantics

- Arenas 1-10 are available by default. The current rules unlock 11 by winning
  arena 10, 12 by winning arenas 7 and 9, 13 by winning any arena, 14 by
  winning seven different arenas, and 15 by winning arenas 1-14.
- Record a stage win only after a completed match whose winners include at
  least one Man player; COM-only victories do not advance these unlocks.
- Keep the flags for arenas 11-15, the 15-entry Man-win array, normalization,
  achievements, toasts, localized hints, and Resources icons consistent.

## Change selectable items, Louies, or handicap

- Keep `GameManager.BattleModeHiddenDropEntries`, `BattleModeMenu` entry maps,
  `SaveSystem` arrays/migrations, Resources icons, and COM behavior aligned.
- Keep `GameManager.BattleModeRandomEggMountTypes`, Louie menu ordering,
  `MountedType`, egg prefabs, starting loadouts, and saved amount arrays aligned.
- Preserve stage-specific handicap profiles and older-save defaults when
  changing the shape of `BattleModeHandicapSave`.
- Prefer public `SaveSystem` setters and verify their persistence contract.
  Battle configuration setters normally save, but not every setter in
  `SaveSystem` guarantees a disk write.
