# QA Checklist

Use this as a targeted verification pass. Select the sections affected by the
change; do not imply that unchecked combinations were covered.

## Contents

- [Automated baseline](#automated-baseline)
- [Core gameplay](#core-gameplay-checks)
- [Items and abilities](#item-and-ability-checks)
- [Bombs and explosions](#bomb-and-explosion-checks)
- [Mounts](#mount-checks)
- [Bosses and stages](#boss-stage-and-gimmick-checks)
- [Battle Mode](#battle-mode-checks)
- [Characters and skins](#character-and-skin-checks)
- [Save, unlock, and menus](#save-unlock-and-menu-checks)
- [Localization and audio](#localization-and-audio-checks)
- [Input and platforms](#input-and-platform-checks)

## Automated baseline

- Inspect `Assets/Tests/EditMode/BattleModeComEditModeTests.cs` for the current
  first-party coverage of synthetic input, explosion lines, plant/escape, and
  COM diagnostics.
- Extend the nearest test when a deterministic helper or contract can be
  covered without scene authoring.
- Do not claim that tests, compilation, a build, or a scene passed unless it
  was actually run.

## Core gameplay checks

- Verify the touched scene loads without missing references.
- Verify the relevant active-player ids. Shared systems support players 1-6;
  do not assume four contiguous players.
- Verify death, restart, or end-stage flow still works after the change.
- Verify pause and resume do not leave the system in a broken state.
- Verify repeated scene entry does not duplicate persistent bootstraps,
  subscriptions, overlays, audio, or Resources-loaded systems.

## Item and ability checks

- Verify the pickup can spawn, be collected, and apply the intended effect.
- Verify duplicate pickup behavior and max-value behavior remain sane.
- Verify stage snapshot/rollback, expulsion, item loss on Battle death, and
  fresh/existing persistent state when relevant.
- Verify the effect does not break mounted players, held or moving bombs,
  human players, COM players, or the optional Normal Game AI.
- Verify any HUD, audio, animation, or save implications actually show up.
- If selectable in Battle Mode, verify item/Louie amounts, defaults, restored
  settings, and positional-array migration.

## Bomb and explosion checks

- Verify bomb placement rules, fuse timing, and explosion reach.
- Verify interaction with destructible blocks, special ground, water, holes,
  indestructible handlers, stage props, occupants, and chain reactions.
- Verify alternate bomb modes, kicks/punches/throws, moving bombs, planned COM
  danger, Revenge Bomber, and Sudden Death when shared code is touched.

## Mount checks

- Verify world pickup, first mount, mounted pickup, egg queue order, death/swap,
  queue consumption, and manual dismount.
- Verify mount ability, external animator, SFX, shadow, movement/input lock,
  bombs, enemies, tiles, portals, launchers, and COM behavior as applicable.

## Boss, stage, and gimmick checks

- Verify stage intro, boss intro, attack loop, defeat flow, and end-stage
  transition.
- Verify scene-authored references still point to the intended objects.
- Verify any gimmick that depends on tiles, triggers, or moving stage props can
  be repeated without soft-locking the stage.
- Verify `BeginStage`/`CommitStage`/`RollbackStage`, lives, Game Over, World Map,
  Boss Rush, Hardcore slot deletion, and ending/credits routes when affected.

## Battle Mode checks

- Verify relevant combinations of 2-6 active players and Man/COM/Off modes.
- Verify Single and Tag matches, teams, score persistence, round reload, final
  match return, pause restart, stage select, and title return.
- Verify Easy, Normal, and Hard COM when weights or chances changed.
- Verify finite/infinite time, Time Up, Draw, Revenge Bomber, Sudden Death, and
  round/match overlays when affected.
- For arena work, verify the shared systems prefab, HUD prefab, miniatures,
  Build Settings, unlock state, arena gimmick, and stage COM ability/provider.

## Character and skin checks

- Verify affected characters and representative default, alternate, and
  unlockable skins for players 1-6 where relevant.
- Verify standalone and embedded Battle skin selection, Boss Rush return,
  save/reload, invalid fallback, and locked-skin hints.
- Verify walk directions, AFK, cornered, death, end-stage, punch, Power Glove,
  mounted visuals, and all six HUD portrait expressions.
- Verify generated sheets and portraits resolve from their exact Resources
  paths; report Editor generation that was not run.

## Save, unlock, and menu checks

- Verify a fresh run and an existing save both behave correctly if persistence
  changed.
- Verify all three slots, reset/delete boundaries, Normal/Hard/Hardcore,
  campaign stage progress, global unlocks, Boss Rush records, and Battle arrays
  as applicable.
- Verify achievement state, unlock toast/icon/text, Golden Bomber eligibility,
  and Achievements menu/ending totals when affected.
- Verify every forward/back transition across Title, Save File, Skin Select,
  World Map, Boss Rush, Battle Mode, Achievements, Game Over, and Ending.

## Localization and audio checks

- Verify English, Japanese, Spanish, and Portuguese BR, including long strings,
  formatting placeholders, and Japanese TMP/legacy fallback.
- Verify music, SFX, and voices respect `GameAudioSettings`, scene changes,
  pause state, overlays, and independent PlayerPrefs persistence.

## Input and platform checks

- Verify keyboard plus relevant Gamepad/Joystick/HID paths.
- Verify synthetic input for COM/AI and clear held/tapped actions on disable,
  death, scene exit, or control-mode changes.
- Verify mobile controls if shared action routing or on-screen control flow was
  touched.
