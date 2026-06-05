# Battle Mode COM Ability Implementation Pattern

Use this reference when creating or changing Battle Mode computer-player
abilities under `Assets/Scripts/BattleMode/COM/`.

## Files To Inspect

- `Assets/Scripts/BattleMode/COM/IBattleModeComAbility.cs`
- `Assets/Scripts/BattleMode/COM/BattleModeComAbilityDecision.cs`
- `Assets/Scripts/BattleMode/COM/BattleModeComActionType.cs`
- `Assets/Scripts/BattleMode/COM/BattleModeComDifficultySettings.cs`
- `Assets/Scripts/BattleMode/COM/BattleModeComController.cs`
- Existing examples:
  `BattleModeComKickBombAbility.cs`,
  `BattleModeComPunchBombAbility.cs`,
  `BattleModeComHazardAwarenessAbility.cs`

## Controller Contract

- `BattleModeComController.RefreshComAbilities()` calls
  `EnsureKnownComAbilityScripts()` and then collects every `MonoBehaviour` on
  the player that implements `IBattleModeComAbility`.
- In danger, `TryBuildAbilityEmergencyCandidate()` asks abilities in component
  order. The first available ability returning `true` wins before normal escape.
- Outside danger, `TryBuildAbilityCandidate()` asks all available abilities,
  keeps the highest positive-weight ability decision, and adds it to the normal
  candidate pool between combat and patrol.
- If the COM has its own unresolved bomb/explosion, the controller tries chain
  bombs first, then ability candidates, then holds a safe tile.
- The controller executes ability decisions via synthetic inputs:
  `TapBomb` -> `ActionA`, `TapActionR` -> `ActionR`,
  `TapActionC` -> `ActionC`.
- Ability methods should set `LastDecisionTrace` for both success and failure;
  the controller copies those strings into rejected action diagnostics.

## Decision Rules

- `Action`: use an existing `BattleModeComActionType` unless the controller
  truly needs new action-specific safety or diagnostic behavior.
- `Weight`: must be positive to compete. Use nearby existing weights as scale:
  patrol is usually low, combat is mid, urgent defensive ability decisions are
  high.
- `TargetTile` and `HasTarget`: set both when movement is aimed at a tile or
  when diagnostics need a specific target.
- `FirstMove`: return a cardinal `Vector2` or `Vector2.zero`; the controller
  turns this into held movement.
- `Reason`: short human-readable decision reason for logs.
- `InputDescription`: match the movement/taps, for example
  `ActionA+MoveLeft` or `ActionC`.
- Tap flags are edge inputs. Let the controller apply cooldowns.
- Treat `Action = KickBomb` carefully: the controller allows KickBomb movement
  through its safety gate because kick sequences sometimes need to step toward a
  bomb. Use it only for mechanics that truly need that exception.

## Ability Shape

Use this structure unless the existing closest ability has a stronger local
pattern:

```csharp
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(BombController))]
public sealed class BattleModeComExampleAbility : MonoBehaviour, IBattleModeComAbility
{
    private PlayerIdentity identity;
    private MovementController movement;
    private BombController bombController;
    private float tileSize = 1f;
    private string lastDecisionTrace = "not evaluated";

    public string DiagnosticName => "Example";
    public string LastDecisionTrace => lastDecisionTrace;

    public bool IsAvailable
    {
        get
        {
            CacheReferences();
            return identity != null && movement != null && bombController != null && !movement.isDead;
        }
    }

    private void Awake() => CacheReferences();
    private void OnEnable() => CacheReferences();

    private void CacheReferences()
    {
        if (identity == null)
            TryGetComponent(out identity);

        if (movement == null)
            TryGetComponent(out movement);

        if (bombController == null)
            TryGetComponent(out bombController);

        if (movement != null)
            tileSize = Mathf.Max(0.01f, movement.tileSize);
    }

    public bool TryBuildEmergencyDecision(
        BattleModeComDifficultySettings settings,
        BattleModeComController controller,
        Vector2Int myTile,
        float currentDangerSeconds,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;
        lastDecisionTrace = "emergency start";

        if (!IsAvailable)
        {
            lastDecisionTrace = "emergency unavailable";
            return false;
        }

        lastDecisionTrace = $"emergency no option danger:{FormatDanger(currentDangerSeconds)}";
        return false;
    }

    public bool TryBuildCandidateDecision(
        BattleModeComDifficultySettings settings,
        BattleModeComController controller,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;
        lastDecisionTrace = "candidate start";

        if (!IsAvailable)
        {
            lastDecisionTrace = "candidate unavailable";
            return false;
        }

        lastDecisionTrace = "candidate no applicable target";
        return false;
    }

    private Vector2Int WorldToTile(Vector3 world)
    {
        float size = Mathf.Max(0.01f, tileSize);
        return new Vector2Int(
            Mathf.RoundToInt(world.x / size),
            Mathf.RoundToInt(world.y / size));
    }

    private Vector3 TileToWorld(Vector2Int tile)
    {
        float size = Mathf.Max(0.01f, tileSize);
        return new Vector3(tile.x * size, tile.y * size, 0f);
    }

    private static string FirstMoveDescription(Vector2 move)
    {
        if (move == Vector2.zero)
            return "none";

        if (move.x > 0.5f)
            return "MoveRight";

        if (move.x < -0.5f)
            return "MoveLeft";

        if (move.y > 0.5f)
            return "MoveUp";

        return "MoveDown";
    }

    private static string FormatDanger(float seconds)
    {
        if (float.IsInfinity(seconds))
            return "safe";

        if (seconds <= 0f)
            return "now";

        return $"{seconds:F2}";
    }
}
```

## Wiring Patterns

- Passive COM ability:
  add the component in `EnsureKnownComAbilityScripts()` guarded by `isCom`.
- AbilitySystem-gated behavior:
  mirror Kick/Punch. Read `PlayerPersistentStats.GetRuntime(playerId)`,
  ensure an `AbilitySystem`, enable the matching ability id, then add the COM
  ability component when `abilitySystem.IsEnabled(...)`.
- Existing prefab/scene component:
  no controller auto-add is needed, but verify the component is present on the
  Battle Mode player prefab or relevant scene object.
- New player power-up:
  check `AbilitySystem.cs`, `AbilityRegistry.cs`, item pickup/database wiring,
  save runtime state, Resources paths, and prefabs.

## Safety Checklist

- Check walkability against ground, destructible, indestructible, bombs, and
  obstacle masks. Existing ability scripts duplicate small tile helpers because
  most controller helpers are private.
- Check danger timing with `settings.dangerReactionSeconds`,
  `settings.safeTileMinimumSeconds`, and estimated traversal time.
- For bomb-moving abilities, handle solid/non-solid timing, fuse windows,
  chain reactions, and post-action escape.
- For multi-step behavior, use an internal state machine with clear reset paths:
  ability unavailable, target missing, bomb exploded, timeout, blocked movement,
  or command already sent.
- Cache random/chance rolls within one Think cycle when repeat calls could
  inflate the effective chance.
- Keep diagnostics throttled and filterable by player id when adding logs.

## Validation

- Do not trigger Unity builds or script compilation unless the user explicitly
  asks for it.
- Test in a `BattleMode_*` scene with the relevant player set to COM when
  practical.
- Test Easy, Normal, and Hard if the ability uses difficulty weights/chances.
- Test at least one multiplayer setup when practical because Battle Mode has
  several player ids and synthetic inputs are player-specific.
- For bomb/explosion behavior, inspect water, holes, destructibles,
  indestructibles, active explosions, chain bombs, and sudden death when they
  could affect the mechanic.
