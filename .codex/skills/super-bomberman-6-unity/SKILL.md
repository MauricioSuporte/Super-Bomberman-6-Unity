---
name: super-bomberman-6-unity
description: Use for implementation, debugging, reviews, and refactors in the Super Bomberman 6 Unity repository, including Normal Game, Boss Rush, Battle Mode and COM AI, stages and bosses, items and abilities, bombs, mounts, characters and skins, HUD and menus, saves and unlocks, achievements, localization, audio, input, mobile, and Unity scene, prefab, tilemap, Resources, and generated-asset integration.
---

# Super Bomberman 6 Unity

Use this skill as the procedural companion to the repository `AGENTS.md`.
Keep work aligned with the current Unity 2D architecture and its scene,
prefab, Resources, save, and player-count integration.

## Establish the live baseline

- Read `ProjectSettings/ProjectVersion.txt` instead of relying on a remembered
  Unity version.
- Read `ProjectSettings/EditorBuildSettings.asset` for the enabled scene list
  and ordering.
- Read `Packages/manifest.json` before assuming a package or version.
- Use `rg --files Assets/Scripts Assets/Scenes Assets/Tests` and targeted `rg`
  searches to confirm that documented owners and contracts are still current.

## Route to the right reference

- Read `references/project-map.md` to find the current owner and neighboring
  systems.
- Read `references/workflows.md` for items, abilities, bombs, mounts, bosses,
  campaign flow, saves, unlocks, menus, input, localization, audio, and global
  runtime services.
- Read `references/battle-mode.md` for rules, menu configuration, arenas,
  rounds, teams, Revenge Bomber, Sudden Death, and arena integration.
- Read `references/battle-mode-com-abilities.md` for Battle Mode COM decisions,
  arena abilities, danger providers, synthetic input, and diagnostics.
- Read `references/characters-and-skins.md` for playable characters, palettes,
  generated sheets, selection, persistence, animations, and HUD portraits.
- Read `Assets/Scripts/IA/NormalGameAI_README.md` when working on the optional
  `ENABLE_NORMAL_GAME_AI` recording assistant.
- Read `references/qa-checklist.md` before closing a player-facing change.

## Core workflow

1. Locate the existing owner and inspect the nearest comparable feature before
   designing a new abstraction.
2. Extend existing registries, resolvers, bootstraps, providers, and session
   state before introducing another manager or persistence layer.
3. Place code in the closest domain folder. Use the root of `Assets/Scripts/`
   only for a truly cross-cutting runtime coordinator.
4. Trace the complete player-facing path: code, scene or prefab, serialized
   defaults, Resources or generated assets, UI/HUD, audio, animation, input,
   save migration, unlocks, and mode transitions.
5. Account for active player ids 1-6 and for Normal Game, Boss Rush, and Battle
   Mode differences when shared runtime code is touched.
6. Keep the change small, preserve Unity `.meta` pairings, and report any
   authoring work that cannot be verified from the terminal.

## Integration gates

- Check `GameManager`, `GameSession`, `PlayersSpawner`,
  `PlayerPersistentStats`, `BossRushSession`, and `BattleModeRules` only when
  the feature crosses their ownership boundaries.
- Keep `Resources.Load(...)` strings, asset locations, editor generators, and
  serialized scene/prefab references synchronized.
- Search for `RuntimeInitializeOnLoadMethod`, `DontDestroyOnLoad`, and existing
  `*AutoLoader` or `*Bootstrap` components before adding a persistent service.
- Preserve keyboard, Gamepad/Joystick/HID, synthetic COM/AI, and mobile input
  paths when action routing changes.
- Add sensible defaults and normalization for new persisted fields or arrays;
  test both fresh and existing save shapes.

## Validation guidance

- Inspect and extend `Assets/Tests/EditMode/BattleModeComEditModeTests.cs` when
  changing covered COM, danger, diagnostic, or synthetic-input behavior.
- Do not claim that tests, compilation, or a Unity scene passed unless they were
  actually run.
- Do not trigger Unity builds or script compilation unless the user explicitly
  asks. Prefer targeted manual validation in the affected scene or mode.
