---
name: enemy-prefab-authoring
description: Create or update 2D Unity enemy prefabs in Super Bomberman 6, including sprite-sheet slicing, directional animation wiring, health and movement components, and placement as prefab instances in stage scenes. Use when adding or correcting an enemy, its prefab, or its directional sprites.
---

# Enemy Prefab Authoring

Use `Assets/Prefabs/Enemies/<EnemyName>.prefab` as the source of truth. Do not leave a scene-only enemy when the enemy is reusable.

## Workflow

1. Inspect the nearest comparable enemy prefab and its scene instance.
2. Import the sprite sheet with Point filtering, PPU 16, and explicit slices. Keep `.meta` alongside the texture.
3. Create or update `Assets/Prefabs/Enemies/<EnemyName>.prefab`, then place a prefab instance in the requested room. Do not duplicate an enemy hierarchy directly into the scene.
4. Attach exactly one intended movement controller and a `CharacterHealth` component. Never attach a second movement controller, including a derived controller; they would issue competing movement commands to the same `Rigidbody2D`. Configure requested health on the prefab so every instance inherits it.
5. Wire `AnimatedSpriteRenderer` children to the movement controller and keep sprite references local to the prefab. For every such child, assign its `SpriteRenderer.sprite` to exactly the same sprite as `AnimatedSpriteRenderer.idleSprite`.

## Directional Sprite Rule

Interpret direction by world movement, not by how the character appears in the sprite sheet:

- `spriteUp`: animation while moving in positive Y / up the stage; normally shows the enemy's back.
- `spriteDown`: animation while moving in negative Y / down the stage; normally shows the enemy's front, facing the camera.
- `spriteLeft`: animation while moving left; let the controller mirror it for right unless an authored right animation exists.

Before saving, inspect both the prefab and its scene instance to confirm `spriteUp` and `spriteDown` have not been swapped and only one movement controller is present.

## Verification

- Confirm the prefab exists under `Assets/Prefabs/Enemies/` and the scene object is linked to it.
- Confirm its transform is inside the requested room, with a synchronized `Rigidbody2D` position.
- Confirm the requested controller type, health, collider, and directional animation frame counts. The root must contain exactly one component derived from `EnemyMovementController`.
- For every child with `AnimatedSpriteRenderer`, confirm `SpriteRenderer.sprite` equals its `idleSprite`.
- Check the Unity console for errors introduced by the new asset. Do not claim Play Mode or build validation unless run.