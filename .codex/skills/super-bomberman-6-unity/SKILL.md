---
name: super-bomberman-6-unity
description: Use for feature work, bug fixes, refactors, stage logic, boss logic, items, mounts, save flow, UI flow, and integration tasks in the Super Bomberman 6 Unity repository. Best when Codex must place code in the right Assets/Scripts area, respect existing scene flow, and remember Unity-specific follow-through such as prefabs, tilemaps, Resources, Input System, and save data implications.
---

# Super Bomberman 6 Unity

Use this skill when working inside the `Super-Bomberman-6-Unity` repository.
It keeps changes aligned with the current Unity 2D architecture instead of
creating parallel systems or dropping scripts into the wrong folder.

## Quick start

- Read `references/project-map.md` first when the main question is "where does
  this change belong?" or "which files own this flow?"
- Read `references/workflows.md` when the task touches items, abilities, bombs,
  explosions, bosses, mounts, stage gimmicks, save data, unlocks, menus, or
  scene flow.
- Read `references/qa-checklist.md` before closing any gameplay-facing change.

## Working rules

- Extend existing systems before introducing new managers, registries, or
  persistence layers.
- Keep gameplay code in the closest domain folder under `Assets/Scripts/`.
  Only use the root of `Assets/Scripts/` for true cross-cutting runtime
  orchestrators.
- If a change is player-facing, handle both code and Unity integration:
  scene or prefab wiring, serialized defaults, Resources paths, UI or audio
  hooks, and save or unlock implications when relevant.
- Follow naming patterns already used in the project such as `*Controller`,
  `*Bootstrap`, `*Resolver`, `*Presenter`, `*Ability`, `*MovementController`,
  and `*Sequence`.
- Assume the project uses Unity 2D, Tilemap, Pixel Perfect, Input System,
  UGUI, Resources-based loading in some flows, and scene-driven gameplay.

## Placement heuristics

- New player power-up or mechanic on the player object:
  `Assets/Scripts/Abilities/`
- Bomb placement, fuse, placement limits, or explosion behavior:
  `Assets/Scripts/Bomb/` and `Assets/Scripts/Explosions/`
- Enemy-specific movement or reactions:
  `Assets/Scripts/Enemies/`
- Boss-specific behavior and scene choreography:
  `Assets/Scripts/Bosses/<BossName>/`
- Mount and Louie runtime behavior:
  `Assets/Scripts/Mounts/`
- Tile reaction logic:
  `Assets/Scripts/Destructible/`, `Ground/`, `Indestructible/`, or
  `StageAssets/`
- Save, unlock, slot, and progression changes:
  `Assets/Scripts/SaveSystem/`, `Unlock/`, `BossRush/`, or `WorldMap/`
- Menu, HUD, bootstrap, and scene navigation changes:
  `Assets/Scripts/TitleScreen/`, `Controls/`, `SaveFileMenu/`, `Skin/`,
  `Hud/`, or `WorldMap/`

## Integration checklist

- Check whether the feature needs serialized references in a scene or prefab.
- Check whether a `Resources.Load(...)` or `Resources.LoadAll(...)` path must
  be kept in sync with asset location.
- Check whether `GameManager`, `GameSession`, `BossRushSession`, or
  `PlayerPersistentStats` should know about the new behavior.
- Check whether the mechanic should persist through `SaveSystem` or
  `UnlockProgress`.
- Check whether the affected scene under `Assets/Scenes/` needs authoring
  changes in addition to code.
- Check whether keyboard, controller, and mobile code paths should stay aligned.

## Validation guidance

- The project has Unity Test Framework packages installed, but there is no
  obvious first-party test suite under `Assets/` yet.
- Prefer targeted manual validation tied to the touched scene and mechanic.
- If adding automated tests, prefer a dedicated test assembly instead of mixing
  test code into gameplay folders.

## Typical asks

- Add a new item and wire pickup, effect, and content integration.
- Create or fix a boss attack, intro, outro, or Boss Rush behavior.
- Adjust mount ability logic and keep animation and stage interaction coherent.
- Add or repair a stage gimmick through tile resolvers and scene wiring.
- Update progression, saves, unlocks, or scene routing without regressions.
