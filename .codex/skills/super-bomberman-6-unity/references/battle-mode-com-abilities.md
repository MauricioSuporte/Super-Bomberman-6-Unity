# Battle Mode COM Abilities

Use this reference when changing computer-player decisions under
`Assets/Scripts/BattleMode/COM/`. Read `battle-mode.md` for the surrounding
menu, arena, rules, round, and unlock flow.

## Contents

- [Files to inspect](#files-to-inspect)
- [Choose the extension shape](#choose-the-extension-shape)
- [Controller contract](#controller-contract)
- [Decision fields](#decision-fields)
- [Wiring and safety](#wiring-and-safety)
- [Validation](#validation)

## Files to inspect

- Core contract: `IBattleModeComAbility.cs`,
  `BattleModeComAbilityDecision.cs`, `BattleModeComActionType.cs`, and
  `BattleModeComDifficultySettings.cs`.
- Orchestration: `BattleModeComController.cs` and
  `BattleModeComDiagnostics.cs`.
- Arena integration: `IBattleModeComStageAbility.cs` and
  `BattleModeComStageAbilityLoader.cs`.
- Optional providers: `IBattleModeComDangerProvider.cs`,
  `IBattleModeComPlannedBombDangerProvider.cs`, and
  `IBattleModeComKickTrajectoryProvider.cs`.
- Shared obstacle handling: `BattleModeComPickupObstacleUtility.cs`.
- Choose the closest example by role:
  `BattleModeComHazardAwarenessAbility`, `BattleModeComKickBombAbility`,
  `BattleModeComPowerGloveAbility`, or a matching
  `BattleModeComStage*Ability`.

## Choose the extension shape

- Implement `IBattleModeComAbility` for a general item, mount, awareness, or
  action decision.
- Implement `IBattleModeComStageAbility` for an arena gimmick and register it
  in `BattleModeComStageAbilityLoader.EnsureForActiveStage()`.
- Implement `IBattleModeComDangerProvider` when the mechanic contributes a
  time-to-danger value for arbitrary tiles.
- Implement `IBattleModeComPlannedBombDangerProvider` when a planned plant must
  add future danger tiles before the bomb exists.
- Implement `IBattleModeComKickTrajectoryProvider` when an arena redirects or
  retunes offensive kick planning.
- Extend the shared pickup-obstacle utility or the nearest provider before
  copying another private pathfinding helper.

## Controller contract

- `RefreshComAbilities()` calls `EnsureKnownComAbilityScripts()`, lets the
  stage loader attach the active arena ability, and collects live
  `IBattleModeComAbility` components from the player.
- `EnsureKnownComAbilityScripts()` synchronizes persistent stats,
  `AbilitySystem`, always-on awareness, item abilities, mount abilities, and
  Man/COM state. Add and remove both sides of any new gated component.
- Arena emergency decisions have a dedicated phase before the general
  emergency fallback. The controller also has special priority routes for
  mechanics such as tank shooting, minecart behavior, and Power Glove; inspect
  the current Think flow before assuming component order decides globally.
- Within the general non-emergency ability phase, the highest positive weight
  wins and competes with the controller's normal candidate pool.
- Set `LastDecisionTrace` on success and every meaningful rejection. The
  controller includes it in diagnostics and rejected-action summaries.

## Decision fields

- Reuse an existing `Action` unless new controller-level safety or diagnostics
  truly require another `BattleModeComActionType`.
- Set a positive `Weight`; compare with nearby abilities and difficulty
  weights rather than inventing a separate scale.
- Set `TargetTile` and `HasTarget` together. Return a cardinal `FirstMove` or
  `Vector2.zero`.
- Keep `Reason` short and human-readable. Make `InputDescription` match the
  actual taps, holds, and movement.
- Input mapping is:
  `TapBomb` and `TapActionA` -> `ActionA`,
  `HoldActionA` -> held `ActionA`,
  `TapActionB` -> `ActionB`,
  `TapActionR` -> `ActionR`, and
  `TapActionC` -> `ActionC`.
- Treat tap flags as edge inputs and let the controller own cooldowns. Clear
  held input and internal state on disable, death, timeout, or cancellation.
- Set `UsesEscapeAbilityChance` only for emergency escape decisions that should
  use Easy/Normal/Hard escape chances. Keep it false for an already committed
  offensive or multi-step sequence that must continue under danger.
- Use `Action = KickBomb` only when the mechanic genuinely needs the kick
  movement safety exception.

## Wiring and safety

- Add a passive awareness component in `EnsureKnownComAbilityScripts()` only
  for COM players.
- For an item-gated component, mirror persistent runtime state into
  `AbilitySystem`, enable the matching ability id, and add/remove the COM
  component as availability changes.
- For an arena component, update the stage loader instead of hard-coding scene
  behavior into the main controller.
- Check ground, destructible, indestructible, bombs, pickups, mounts, moving
  stage props, obstacle masks, danger timing, fuse/chain timing, and
  post-action escape.
- Use an internal state machine for multi-step actions with explicit reset paths
  for lost targets, exploded bombs, blocked movement, completed commands,
  timeout, ability removal, death, and round end.
- Cache random rolls within one Think cycle when repeated evaluation could
  inflate a configured chance. Keep logs throttled and filterable by player id.

## Validation

- Inspect and extend
  `Assets/Tests/EditMode/BattleModeComEditModeTests.cs` when changing synthetic
  input, explosion-line helpers, plant/escape checks, or diagnostics.
- Test the relevant `BattleMode_*` arena with a COM player on Easy, Normal, and
  Hard when difficulty weights or chances participate.
- Test multiple player ids and any Man/COM/Off transition that can add or remove
  the component.
- Test interactions with active and planned explosions, chain bombs, water,
  holes, tile handlers, pickups, mounts, Revenge Bomber, and Sudden Death as
  applicable.
- Do not claim tests, compilation, or scene validation unless they were run.
  Do not trigger Unity compilation or builds unless the user explicitly asks.
