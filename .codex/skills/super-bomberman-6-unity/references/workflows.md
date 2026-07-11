# Workflows

## Contents

- [Items and abilities](#add-or-change-an-item-or-ability)
- [Bombs and explosions](#add-or-change-bomb-or-explosion-behavior)
- [Bosses and stages](#add-or-change-a-boss-or-stage-feature)
- [Mounts and Louies](#add-or-change-mount-or-louie-behavior)
- [Saves and progression](#add-or-change-save-unlock-or-progression-behavior)
- [Menus, localization, audio, and input](#add-or-change-menus-localization-audio-or-input)
- [Persistent services](#add-or-change-a-persistent-runtime-service)
- [Optional Normal Game AI](#change-the-optional-normal-game-recording-ai)

## Add or change an item or ability

1. Decide whether the mechanic is a pickup, an ability component, or both.
2. Update `Assets/Scripts/Itens/ItemType.cs`, `ItemPickup.cs`, and exactly one
   `ItemPickup` prefab under `Assets/Resources/Items/`. The root-level
   `Assets/Scripts/AutoItemDatabase.cs` keeps the first prefab found per type.
3. Implement `IItemPickupBehavior` when a custom pickup should replace the
   normal path rather than expanding unrelated switch logic.
4. For a dynamically enabled ability, implement `IPlayerAbility`, place it
   under `Assets/Scripts/Abilities/`, and register it in `AbilityRegistry` so
   `AbilitySystem` can create and cache the component.
5. For stage-persistent effects, update every relevant field, copy/reset path,
   `StageApplyPickup`, stage snapshot, runtime application, and expulsion path
   in `PlayerPersistentStats`.
6. If hidden or selectable in Battle Mode, keep
   `GameManager.BattleModeHiddenDropEntries`, `BattleModeMenu` entry mappings,
   COM behavior, icons, and `SaveSystem` positional-array migration aligned.
   Never insert or reorder a persisted positional entry without migration.

## Add or change bomb or explosion behavior

1. Start with `Assets/Scripts/Bomb/BombController.cs` and inspect neighboring
   files in `Bomb/` and `Explosions/`.
2. Check contracts under `Assets/Scripts/Interface/` plus the resolvers and
   handlers under `Destructible/`, `Ground/`, and `Indestructible/` before
   adding a tile-specific branch.
3. Check ability-driven bomb variants such as kick, punch, power, control,
   rubber, pierce, or magnet behavior before adding duplicate logic.
4. Verify water, holes, stage props, moving bombs, chain explosions, current
   occupants, Revenge Bomber, Sudden Death, and both human and AI/COM paths as
   applicable.

## Add or change a boss or stage feature

1. Keep boss or Bomber encounter code near its existing owner under `Bosses/`
   or `Bombers/`; keep shared choreography in `StageIntro/`, `EndStage/`, or
   `EndScreen/` only when it is genuinely shared.
2. Check `GameManager`, `BossEndStageSequence`, `StageUnlockProgress`,
   `PlayerPersistentStats` stage transactions, `BossRushSession`, life/Game
   Over behavior, and the `END_SCREEN` route when progression changes.
3. Confirm scene/prefab wiring, music and voice routing, projectiles, VFX,
   tilemaps, layers, colliders, Resources, and return transitions.
4. Use `battle-mode.md` instead when the stage is a Battle Mode arena.

## Add or change mount or Louie behavior

1. Update `MountedType`, `PlayerMountCompanion`, the prefab resolver,
   `MountWorldPickup`, `MountEggQueue`, `PlayerManualDismount`, movement,
   animator, ability, SFX, and persistent state as applicable.
2. Keep `AbilityRegistry`, external-animation interfaces, and the matching COM
   ability in sync with a mount ability.
3. If selectable in Battle Mode, update
   `GameManager.BattleModeRandomEggMountTypes`, Louie menu ordering, starting
   loadouts/handicap, and positional save-array migration. Note that Mole and
   Tank use world prefabs rather than colored-Louie egg `ItemType` values.
4. Verify empty/mounted egg pickup, queue consumption, death/swap/dismount,
   bombs, enemies, tiles, portals, launchers, and input locks.

## Add or change save, unlock, or progression behavior

1. Prefer public `SaveSystem` setters and inspect their persistence contract.
   Many configuration setters save internally, but call `SaveSystem.Save()`
   after a direct `SaveSystem.Data` mutation or a setter that only changes
   in-memory data.
2. Add defaults and repair/migration logic in `SaveSystem.NormalizeData()` and
   the relevant `Ensure*` helper for every new field, enum, list, or array.
3. Use `StageUnlockProgress` for campaign-stage order/completion in the active
   slot. Use `UnlockProgress` for skins, Boss Rush, Hardcore, Battle arenas,
   Golden Bomber, events, and player-facing unlock refreshes.
4. Keep `PlayerPersistentStats.BeginStage`, `CommitStage`, and `RollbackStage`
   aligned with restart, death, pause exit, Game Over, and stage completion.
5. Validate three slots, fresh and legacy data, reset/delete boundaries,
   Normal/Hard/Hardcore behavior, Boss Rush isolation, and Battle Mode arrays.
6. Do not route audio volume or voice settings through the JSON save:
   `GameAudioSettings` intentionally persists them in `PlayerPrefs`.

## Add or change menus, localization, audio, or input

1. Keep scene-local controllers near `TitleScreen/`, `SaveFileMenu/`,
   `BattleModeMenu/`, `Achievements/`, `Skin/`, `Controls/`, `Pause/`, or the
   owning screen folder.
2. Add user-facing copy through `GameTextDatabase` for English, Japanese,
   Spanish, and Portuguese BR. Apply `LocalizedTmpFontFallback` to relevant TMP
   or legacy text and test long strings.
3. Route music through `GameMusicController` and SFX/voice volume through
   `GameAudioSettings` helpers, including pause-independent overlay audio when
   needed.
4. Treat `PlayerInputManager` and `PlayerInputBootstrapper` as the shared input
   path. Preserve keyboard, Gamepad/Joystick/HID, mobile, and synthetic input;
   prefer current `SaveSystem` control persistence and treat
   `PlayerInputProfile` PlayerPrefs methods as legacy unless a live caller is
   found.
5. Validate all forward and back routes, especially Skin Select destinations,
   Boss Rush, World Map, Battle stage select, Achievements, Game Over, and the
   ending screen.

## Add or change a persistent runtime service

1. Search for `RuntimeInitializeOnLoadMethod`, `DontDestroyOnLoad`, and an
   existing bootstrap before adding another singleton.
2. Reuse `PlayerInputBootstrapper`, `PauseAutoLoader`,
   `EndingScreenAutoLoader`, `MobileControlsAutoBootstrap`,
   `GlobalUnlockController`, or `DiscordRichPresenceController` when the
   responsibility belongs there.
3. Keep Resources prefab paths and duplicate-instance cleanup correct when a
   service is loaded before scenes.

## Change the optional Normal Game recording AI

- Read `Assets/Scripts/IA/NormalGameAI_README.md` first.
- Keep the feature behind `ENABLE_NORMAL_GAME_AI`; it controls P2-P4 for
  recording assistance and must not take over Battle Mode or Boss Rush.
- Treat `NormalGameComRecordingAssist` as inactive unless a live caller is
  found.

## When in doubt

- Inspect the nearest existing feature in the same folder before creating new
  abstractions.
- Prefer extending registries, resolvers, and bootstraps that already exist in
  the repo over introducing another global manager.
