---
name: super-bomberman-6-unity
description: Implement, debug, review, or refactor Super Bomberman 6 Unity gameplay and content, including Normal Game, Boss Rush, offline and Mirror online Battle Mode, COM, menus, saves, characters, URP pixel-perfect rendering, scenes and prefabs. Use for changes in this repository; consult the online companion for network authority and multiplayer flow.
---

# Super Bomberman 6 Unity

Use this skill as the procedural companion to the repository `AGENTS.md`.
Keep work aligned with the current Unity 2D architecture and its scene,
prefab, Resources, save, and player-count integration.

## Establish the live baseline

- Confirm the active checkout/branch and the Editor project root. A source repo,
  fork PR and open Unity Editor may point to different worktrees. Preserve dirty
  user files and avoid assuming this session's online changes are committed.
- Read `ProjectSettings/ProjectVersion.txt` instead of relying on a remembered
  Unity version.
- Read `ProjectSettings/EditorBuildSettings.asset` for the enabled scene list
  and ordering.
- Read `Packages/manifest.json` before assuming a package or version.
- Use `rg --files Assets/Scripts Assets/Scenes Assets/Tests` and targeted `rg`
  searches to confirm that documented owners and contracts are still current.

## Route to the right reference

- Read [references/current-baseline.md](references/current-baseline.md) for the
  current rendering, online integration, persistence and validation boundaries.
- For Mirror/network gameplay or online menu/round/pause changes, use
  [super-bomberman-6-online](../super-bomberman-6-online/SKILL.md). Keep its
  authority/presentation guidance separate from offline-only work.
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
- Shared Battle changes must account for Offline, host, owner client, remote
  client and server-controlled COM. Do not apply a server-only gate to current
  owner movement/animation without checking prediction and replication direction.
- Add sensible defaults and normalization for new persisted fields or arrays;
  test both fresh and existing save shapes.

## Validation guidance

- Inspect and extend `Assets/Tests/EditMode/BattleModeComEditModeTests.cs` when
  changing covered COM, danger, diagnostic, or synthetic-input behavior.
  Execute it through `BattleModeComEditorTests`, the concrete Editor discovery
  wrapper; the abstract base stays beside its internal runtime helpers. Confirm
  the fixture/count in the result XML instead of treating an empty filter as success.
- Edit Mode fixtures must supply a concrete Collider2D before adding components
  that require the abstract Collider2D type. Initialize ordinary MonoBehaviour
  caches explicitly when Awake has not run; native body/tilemap coordinate tests
  need active native objects and an asserted initial condition.
- Do not claim that tests, compilation, or a Unity scene passed unless they were
  actually run.
- For C# or Unity-content edits, run available C# and Unity compilation checks
  required by `AGENTS.md`, resolve introduced errors/warnings, and report checks
  blocked by the open Editor or unavailable tools. Prefer targeted runtime tests
  in the touched mode; documentation-only updates do not require compilation.
- Inspect `Assets/Tests/EditMode/Editor/OnlineRoomEditModeTests.cs` for room,
  menu, skin, pause and repeated-round regressions when touching online.
- Keep UTF-8/newline integrity and `.meta` pairs. Check changed C# bytes for
  corrupted/duplicate BOMs or replacement characters and remove obsolete private
  fields with their serialized scene/prefab settings, as required by `AGENTS.md`.
