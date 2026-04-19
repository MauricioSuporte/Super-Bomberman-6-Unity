# Workflows

## Add or change an item or ability

1. Decide whether the mechanic is a pickup, an ability component, or both.
2. If it is a new pickup id, update `Assets/Scripts/Itens/ItemType.cs`.
3. If it is a toggled player ability, add or update a component under
   `Assets/Scripts/Abilities/` and register the id in
   `Assets/Scripts/Abilities/AbilityRegistry.cs`.
4. If pickup behavior is special, use `ItemPickup.cs` and
   `IItemPickupBehavior` instead of packing more unrelated logic into one
   switch path.
5. Keep item prefab placement aligned with `AutoItemDatabase.cs`, which loads
   `ItemPickup` prefabs from `Resources/Items`.
6. If stages can hide the item inside blocks, verify whether `GameManager.cs`
   needs new hidden-item configuration fields or stage authoring changes.

## Add or change bomb or explosion behavior

1. Start with `Assets/Scripts/Bomb/BombController.cs` and inspect neighboring
   files in `Bomb/` and `Explosions/`.
2. Check interactions with water, holes, destructible tiles, ground tile
   effects, indestructible tile effects, and chain explosions.
3. Check ability-driven bomb variants such as kick, punch, power, control,
   rubber, pierce, or magnet behavior before adding duplicate logic.
4. Verify both local player and AI code paths if bomb behavior can be triggered
   by either.

## Add or change a boss or stage feature

1. Keep boss-specific code inside the owning boss folder under
   `Assets/Scripts/Bosses/`.
2. Keep stage intros, outros, and end-stage choreography close to the owning
   mode or boss flow.
3. Check whether `GameManager.cs`, `BossEndStageSequence.cs`, `EndStage/`, or
   `BossRush/` should participate in the new flow.
4. Confirm scene wiring, music, projectiles, VFX, and collider setup in the
   affected stage scene.

## Add or change mount or Louie behavior

1. Split mount runtime state, animation helpers, and ability effects according
   to the existing `Mounts/` structure.
2. Check egg queue and mount ownership flow through `MountEggQueue`,
   `PlayerMountCompanion`, and the relevant animator or ability classes.
3. Verify how the mount interacts with bombs, explosions, enemies, and
   destructible or special stage tiles.

## Add or change save, unlock, or progression behavior

1. Use `Assets/Scripts/SaveSystem/SaveSystem.cs` as the persistence source of
   truth and call `SaveSystem.Save()` when mutating persisted data.
2. Route unlock behavior through `Assets/Scripts/Unlock/UnlockProgress.cs`
   when player-facing unlock events or UI refreshes are involved.
3. Be careful with slot resets, stage name keys, and new fields that might need
   sensible defaults for older save files.
4. If the change affects Boss Rush progression or timing, inspect the
   `Assets/Scripts/BossRush/` folder rather than adding parallel state.

## Add or change menu, scene flow, or input behavior

1. Keep scene-local bootstraps near their screen folder, such as
   `TitleScreen/`, `SaveFileMenu/`, `Skin/`, or `Controls/`.
2. If the task mentions controller remapping or player joins, inspect
   `Assets/Scripts/Controls/`.
3. If the task mentions touch or Android parity, inspect `Assets/Scripts/Mobile/`.
4. Validate return paths and scene transitions, especially for Boss Rush and
   skin selection flow.

## When in doubt

- Inspect the nearest existing feature in the same folder before creating new
  abstractions.
- Prefer extending registries, resolvers, and bootstraps that already exist in
  the repo over introducing another global manager.
