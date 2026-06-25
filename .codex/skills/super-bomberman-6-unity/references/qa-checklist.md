# QA Checklist

Use this as a manual verification pass when no automated test covers the area.

## Core gameplay checks

- Verify the touched scene loads without missing references.
- Verify the mechanic still behaves correctly with 1 player and with multiple
  active players if the feature is shared gameplay.
- Verify death, restart, or end-stage flow still works after the change.
- Verify pause and resume do not leave the system in a broken state.

## Item and ability checks

- Verify the pickup can spawn, be collected, and apply the intended effect.
- Verify duplicate pickup behavior and max-value behavior remain sane.
- Verify the effect does not break mounted players, held bombs, or AI players
  when those interactions are relevant.
- Verify any HUD, audio, animation, or save implications actually show up.

## Bomb and explosion checks

- Verify bomb placement rules, fuse timing, and explosion reach.
- Verify interaction with destructible blocks, special ground, water, holes,
  and chain reactions.
- Verify alternate bomb modes still behave correctly if the change touched
  shared bomb code.

## Boss, stage, and gimmick checks

- Verify stage intro, boss intro, attack loop, defeat flow, and end-stage
  transition.
- Verify scene-authored references still point to the intended objects.
- Verify any gimmick that depends on tiles, triggers, or moving stage props can
  be repeated without soft-locking the stage.

## Save, unlock, and menu checks

- Verify a fresh run and an existing save both behave correctly if persistence
  changed.
- Verify slot reset or delete still leaves the game in a valid state if those
  paths were touched.
- Verify unlock UI and route transitions still reflect the new data.
- Verify Boss Rush return flow or world map flow if the change touches either.

## Input and platform checks

- Verify keyboard or controller input if the task touched shared input.
- Verify mobile controls if the task touched shared action routing or on-screen
  control flow.
- Verify there are no obvious regressions in scene navigation after backing out
  of the touched menu or mode.
