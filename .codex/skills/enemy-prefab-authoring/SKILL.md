---
name: enemy-prefab-authoring
description: Create or update 2D Unity enemy prefabs in Super Bomberman 6, including sprite-sheet slicing, directional animation wiring, health and movement components, and placement as prefab instances in stage scenes. Use when adding or correcting an enemy, its prefab, or its directional sprites.
---

# Enemy Prefab Authoring

Use `Assets/Prefabs/Enemies/<EnemyName>.prefab` as the source of truth. Do not leave a scene-only enemy when the enemy is reusable.

## Workflow

1. Inspect the nearest comparable enemy prefab and its scene instance.
2. Import the sprite sheet with Point filtering, PPU 16, and explicit slices. Keep `.meta` alongside the texture.
3. Create or update `Assets/Prefabs/Enemies/<EnemyName>.prefab` as an independent regular prefab, then place a prefab instance in the requested room. Do not duplicate an enemy hierarchy directly into the scene. A comparable enemy may be used only as a temporary authoring reference: do not save the new enemy as a prefab variant or retain a prefab-source reference to that enemy.
4. Attach exactly one intended movement controller and a `CharacterHealth` component. Never attach a second movement controller, including a derived controller; they would issue competing movement commands to the same `Rigidbody2D`. Configure requested health on the prefab so every instance inherits it.
5. Wire `AnimatedSpriteRenderer` children to the movement controller and keep sprite references local to the prefab. For every such child, assign its `SpriteRenderer.sprite` to exactly the same sprite as `AnimatedSpriteRenderer.idleSprite`.
6. Set the initial visual state so exactly one directional child is enabled: use `Down` when the prefab has separate Up/Down/Left directional children; when it has one shared directional child, enable only that child. Keep all other directional children disabled. On the active Death child, disable both `AnimatedSpriteRenderer` and `SpriteRenderer` so it is invisible until the death flow enables it.
7. For enemies placed in Stage 3-3, do not attach `PlayerWaterSubmersionEffect`. This stage's enemies must not show the water-submersion visual; preserve that rule for future Stage 3-3 enemy authoring.

## Prefab Persistence

- Author or repair prefab hierarchies through Unity's prefab APIs or the
  Editor. Do not hand-serialize a new prefab YAML hierarchy: incomplete
  Transform parent/child references can make Unity restore null parents and
  lose renderer children during import.
- After saving, load the prefab and confirm every expected visual child has
  both `SpriteRenderer` and `AnimatedSpriteRenderer` components.

## Custom Attack and Projectile Visuals

Apply these rules when an enemy has additional attack-renderer children or a
projectile that persists after impact:

- During an attack, disable both the `AnimatedSpriteRenderer` and
  `SpriteRenderer` of every walking-direction child. A one-frame attack still
  needs `loop = false`; after its timed attack window, disable both components
  of the attack child before restoring movement.
- If a derived movement controller overrides death, disable every custom
  attack visual (both components) before calling the base death flow, so only
  the Death renderer can become visible.
- Do not redeclare a serialized field name used by `EnemyMovementController`
  (for example `health`). Reuse protected base state where available, or use
  a distinct name only when a separate reference is truly needed.
- A projectile that remains as an impact effect must set its impact state and
  disable its collider before applying damage or starting any blink/fade
  routine. It must not move or damage again while it blinks, and should destroy
  itself only after that visual routine finishes.

## Walk Animation Sequence

When a movement direction provides exactly three walking frames (`1`, `2`, and `3`), configure its looping `animationSprite` sequence as `1-2-3-2`, rather than `1-2-3`. This gives the walk cycle a return stroke. Apply the rule independently to every authored direction; do not apply it to death animations unless that animation explicitly calls for it.

## Directional Sprite Rule

Interpret direction by world movement, not by how the character appears in the sprite sheet:

- `spriteUp`: animation while moving in positive Y / up the stage; normally shows the enemy's back.
- `spriteDown`: animation while moving in negative Y / down the stage; normally shows the enemy's front, facing the camera.
- `spriteLeft`: animation while moving left; let the controller mirror it for right unless an authored right animation exists.

Before saving, inspect both the prefab and its scene instance to confirm `spriteUp` and `spriteDown` have not been swapped and only one movement controller is present.

## World 3 (`3-N`) Death Animations

For World 3 enemies, configure the death renderer and the parent
`EnemyMovementController` (or its derived controller) as follows:

- Set `waitForFullDeathAnimation = true`.
- Use `animationTime = 0.1f` for every death frame and keep `loop = false`.
- Start the sequence with the enemy-specific death frames from its sprite
  sheet. When the sheet provides five such frames, they must occupy the first
  five positions in the sequence.
- When the sheet provides only one enemy-specific death sprite, repeat that
  sprite in each of the first five positions before the shared finish.
- Append the appropriate shared finish:
  - Small enemy: the seven final death sprites used by `Funya`.
  - Large enemy: the eight final death sprites used by `LizardMan`.
  - Non-standard enemy: the CoreMechanisms destruction sprites, following the
    same pattern as `Banbo`.
- Keep the Death `AnimatedSpriteRenderer` and `SpriteRenderer` disabled until
  the controller's death flow starts it.

Example: `Kamestone` is a large enemy, so its five sheet-specific death frames
are followed by the eight-frame `LizardMan` finish.

## Verification

- Confirm the prefab exists under `Assets/Prefabs/Enemies/` and the scene object is linked to it.
- Confirm the enemy prefab is a regular prefab with no prefab-source/variant dependency on another enemy prefab.
- Confirm its transform is inside the requested room, with a synchronized `Rigidbody2D` position.
- Confirm the requested controller type, health, collider, and directional animation frame counts. The root must contain exactly one component derived from `EnemyMovementController`.
- For every three-frame directional walk, confirm the configured loop is `1-2-3-2`.
- For every child with `AnimatedSpriteRenderer`, confirm `SpriteRenderer.sprite` equals its `idleSprite`.
- Confirm exactly one directional `AnimatedSpriteRenderer` is enabled in the prefab: Down for separate directions, or the sole shared directional child.
- Confirm the Death child's `AnimatedSpriteRenderer` and `SpriteRenderer` are both disabled initially.
- For World 3 death animations, confirm the first five frames use the required
  enemy-specific death sprites before the shared finish begins.
- For Stage 3-3 enemies, confirm `PlayerWaterSubmersionEffect` is absent.
- Check the Unity console for errors introduced by the new asset. Do not claim Play Mode or build validation unless run.
