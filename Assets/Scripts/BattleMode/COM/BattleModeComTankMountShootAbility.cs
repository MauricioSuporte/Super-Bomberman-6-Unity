using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Uses the Tank mount shot against the first visible adversary in a cardinal
/// lane. The minimum distance keeps the shooter outside the impact explosion.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(BombController))]
public sealed class BattleModeComTankMountShootAbility : MonoBehaviour, IBattleModeComAbility
{
    public const int DiagnosticPlayerIdFilter = 0;
    public static readonly bool EnableTankShootDiagnostics = true;

    private const int MinimumSafeTargetDistanceTiles = 3;
    private const float SurgicalLogIntervalSeconds = 0.35f;

    private static readonly Vector2Int[] CardinalTiles =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    private PlayerIdentity identity;
    private MovementController movement;
    private AbilitySystem abilitySystem;
    private TankMountShootAbility tankShoot;
    private GameManager gameManager;
    private Tilemap destructibleTilemap;
    private Tilemap indestructibleTilemap;
    private readonly List<PlayerIdentity> activePlayers = new(6);
    private float tileSize = 1f;
    private int projectileObstacleMask;

    private float nextOpportunityTime = -10f;
    private float committedShotUntil = -10f;
    private string lastDecisionTrace = "not evaluated";
    private float lastSurgicalLogTime = -10f;
    private string lastSurgicalLogKey = string.Empty;

    public string DiagnosticName => "TankShoot";
    public string LastDecisionTrace => lastDecisionTrace;

    public bool IsAvailable
    {
        get
        {
            CacheReferences();
            return identity != null &&
                   movement != null &&
                   !movement.isDead &&
                   abilitySystem != null &&
                   abilitySystem.IsEnabled(TankMountShootAbility.AbilityId) &&
                   tankShoot != null;
        }
    }

    private void Awake() => CacheReferences();
    private void OnEnable() => CacheReferences();
    private void OnDisable()
    {
        if (tankShoot != null)
            tankShoot.SetCooldownSeconds(TankMountShootAbility.DefaultCooldownSeconds);
    }

    private void CacheReferences()
    {
        if (identity == null)
            TryGetComponent(out identity);

        if (movement == null)
            TryGetComponent(out movement);

        if (abilitySystem == null)
            TryGetComponent(out abilitySystem);

        if (tankShoot == null)
            TryGetComponent(out tankShoot);

        if (movement != null)
            tileSize = Mathf.Max(0.01f, movement.tileSize);

        projectileObstacleMask = LayerMask.GetMask("Stage", "Water", "Explosion");

        if (gameManager == null)
            gameManager = FindAnyObjectByType<GameManager>();

        if (gameManager != null)
        {
            destructibleTilemap = gameManager.destructibleTilemap;
            indestructibleTilemap = gameManager.indestructibleTilemap;
        }
    }

    public bool TryBuildEmergencyDecision(
        BattleModeComDifficultySettings settings,
        BattleModeComController controller,
        Vector2Int myTile,
        float currentDangerSeconds,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;
        lastDecisionTrace = "emergency: tank shot is candidate-only";
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

        float difficultyCooldown = DifficultyCooldown(settings);
        tankShoot.SetCooldownSeconds(difficultyCooldown);

        if (!tankShoot.CanStartShot)
        {
            lastDecisionTrace =
                $"candidate ability cooldown:{tankShoot.CooldownRemainingSeconds:F2}s " +
                $"running:{tankShoot.Running}";
            return false;
        }

        if (Time.time < nextOpportunityTime)
        {
            lastDecisionTrace = $"candidate opportunity cooldown:{(nextOpportunityTime - Time.time):F2}s";
            return false;
        }

        if (!TryFindSafeTarget(
                myTile,
                out Vector2Int shootDirection,
                out Vector2Int targetTile,
                out int targetPlayerId,
                out int targetDistance,
                out string searchTrace))
        {
            lastDecisionTrace = $"candidate no safe target {searchTrace}";
            LogSurgical("REJECT_NO_SAFE_TARGET", $"my:{myTile} {searchTrace}");
            return false;
        }

        float chance = DifficultyChance(settings);
        bool hasCommittedShot = Time.time <= committedShotUntil;
        float roll = hasCommittedShot ? 0f : Random.value;
        if (!hasCommittedShot && roll > chance)
        {
            nextOpportunityTime = Time.time + difficultyCooldown;
            lastDecisionTrace =
                $"candidate chance fail roll:{roll:F2} chance:{chance:F2} " +
                $"retry:{difficultyCooldown:F1}s target:P{targetPlayerId}@{targetTile}";
            LogSurgical(
                "REJECT_CHANCE",
                $"target:P{targetPlayerId}@{targetTile} dist:{targetDistance} " +
                $"roll:{roll:F2} chance:{chance:F2} retry:{difficultyCooldown:F1}s",
                true);
            return false;
        }

        committedShotUntil = Time.time + 0.5f;
        Vector2 facing = new(shootDirection.x, shootDirection.y);
        movement.ForceFacingDirection(facing);
        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.CombatPlant,
            Weight = 900 + DifficultyWeight(settings),
            TargetTile = targetTile,
            HasTarget = true,
            FirstMove = Vector2.zero,
            Reason = $"tank shot P{targetPlayerId} distance:{targetDistance}",
            InputDescription = "ActionC",
            TapActionC = true
        };

        lastDecisionTrace =
            $"candidate SHOOT target:P{targetPlayerId}@{targetTile} " +
            $"dir:{DirectionLabel(shootDirection)} dist:{targetDistance} " +
            $"roll:{(hasCommittedShot ? "committed" : roll.ToString("F2"))}/{chance:F2} " +
            $"cooldown:{difficultyCooldown:F1}s";
        LogSurgical(
            "TANK_SHOOT_READY",
            $"target:P{targetPlayerId}@{targetTile} dir:{DirectionLabel(shootDirection)} " +
            $"dist:{targetDistance} roll:{(hasCommittedShot ? "committed" : roll.ToString("F2"))}/{chance:F2} " +
            $"cooldown:{difficultyCooldown:F1}s",
            true);
        return true;
    }

    private bool TryFindSafeTarget(
        Vector2Int myTile,
        out Vector2Int bestDirection,
        out Vector2Int bestTargetTile,
        out int bestPlayerId,
        out int bestDistance,
        out string trace)
    {
        bestDirection = Vector2Int.zero;
        bestTargetTile = Vector2Int.zero;
        bestPlayerId = 0;
        bestDistance = int.MaxValue;
        trace = string.Empty;

        activePlayers.Clear();
        PlayerIdentity.GetActivePlayers(activePlayers);

        for (int directionIndex = 0; directionIndex < CardinalTiles.Length; directionIndex++)
        {
            Vector2Int direction = CardinalTiles[directionIndex];
            PlayerIdentity firstPlayer = null;
            Vector2Int firstPlayerTile = Vector2Int.zero;
            int firstPlayerDistance = int.MaxValue;

            for (int i = 0; i < activePlayers.Count; i++)
            {
                PlayerIdentity player = activePlayers[i];
                if (player == null || player == identity)
                    continue;

                if (!player.TryGetComponent(out MovementController playerMovement) ||
                    playerMovement == null ||
                    playerMovement.isDead)
                    continue;

                Vector2Int playerTile = WorldToTile(player.transform.position);
                Vector2Int delta = playerTile - myTile;
                if (!IsInDirection(delta, direction, out int distance) ||
                    distance >= firstPlayerDistance)
                    continue;

                firstPlayer = player;
                firstPlayerTile = playerTile;
                firstPlayerDistance = distance;
            }

            if (firstPlayer == null)
            {
                AppendTrace(ref trace, direction, "no-player");
                continue;
            }

            if (IsAlly(firstPlayer.playerId))
            {
                AppendTrace(
                    ref trace,
                    direction,
                    $"ally-first:P{firstPlayer.playerId}@{firstPlayerTile}:dist:{firstPlayerDistance}");
                continue;
            }

            if (firstPlayerDistance < MinimumSafeTargetDistanceTiles)
            {
                AppendTrace(
                    ref trace,
                    direction,
                    $"enemy-too-close:P{firstPlayer.playerId}@{firstPlayerTile}:dist:{firstPlayerDistance}");
                continue;
            }

            if (TryFindPathBlocker(
                    myTile,
                    direction,
                    firstPlayerDistance,
                    out Vector2Int blockerTile,
                    out string blocker))
            {
                AppendTrace(
                    ref trace,
                    direction,
                    $"{blocker}@{blockerTile}:target:P{firstPlayer.playerId}:dist:{firstPlayerDistance}");
                continue;
            }

            AppendTrace(
                ref trace,
                direction,
                $"safe-target:P{firstPlayer.playerId}@{firstPlayerTile}:dist:{firstPlayerDistance}");

            if (firstPlayerDistance >= bestDistance)
                continue;

            bestDirection = direction;
            bestTargetTile = firstPlayerTile;
            bestPlayerId = firstPlayer.playerId;
            bestDistance = firstPlayerDistance;
        }

        return bestPlayerId != 0;
    }

    private bool TryFindPathBlocker(
        Vector2Int origin,
        Vector2Int direction,
        int targetDistance,
        out Vector2Int blockerTile,
        out string blocker)
    {
        blockerTile = Vector2Int.zero;
        blocker = string.Empty;

        for (int step = 1; step < targetDistance; step++)
        {
            Vector2Int tile = origin + direction * step;
            if (FindBombAt(tile) != null)
            {
                blockerTile = tile;
                blocker = "bomb";
                return true;
            }

            if (HasIndestructibleTile(tile))
            {
                blockerTile = tile;
                blocker = "indestructible";
                return true;
            }

            if (HasDestructibleTile(tile))
            {
                blockerTile = tile;
                blocker = "destructible";
                return true;
            }

            if (projectileObstacleMask != 0 &&
                Physics2D.OverlapCircle(TileToWorld(tile), tileSize * 0.2f, projectileObstacleMask) != null)
            {
                blockerTile = tile;
                blocker = "projectile-obstacle";
                return true;
            }
        }

        return false;
    }

    private Bomb FindBombAt(Vector2Int tile)
    {
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded || bomb.IsBeingHeldByPowerGlove)
                continue;

            if (WorldToTile(bomb.GetLogicalPosition()) == tile)
                return bomb;
        }

        return null;
    }

    private bool HasDestructibleTile(Vector2Int tile) =>
        destructibleTilemap != null &&
        destructibleTilemap.HasTile(destructibleTilemap.WorldToCell(TileToWorld(tile)));

    private bool HasIndestructibleTile(Vector2Int tile) =>
        indestructibleTilemap != null &&
        indestructibleTilemap.HasTile(indestructibleTilemap.WorldToCell(TileToWorld(tile)));

    private bool IsAlly(int otherPlayerId)
    {
        if (BattleModeRules.Instance == null ||
            !BattleModeRules.Instance.UsesTeams ||
            identity == null)
            return false;

        return BattleModeRules.Instance.GetTeamForPlayer(identity.playerId) ==
               BattleModeRules.Instance.GetTeamForPlayer(otherPlayerId);
    }

    private Vector2Int WorldToTile(Vector2 world)
    {
        float size = Mathf.Max(0.01f, tileSize);
        return new Vector2Int(
            Mathf.RoundToInt(world.x / size),
            Mathf.RoundToInt(world.y / size));
    }

    private Vector2 TileToWorld(Vector2Int tile)
    {
        float size = Mathf.Max(0.01f, tileSize);
        return new Vector2(tile.x * size, tile.y * size);
    }

    private static bool IsInDirection(
        Vector2Int delta,
        Vector2Int direction,
        out int distance)
    {
        distance = 0;
        if (direction.x != 0)
        {
            if (delta.y != 0 || delta.x * direction.x <= 0)
                return false;

            distance = Mathf.Abs(delta.x);
            return true;
        }

        if (delta.x != 0 || delta.y * direction.y <= 0)
            return false;

        distance = Mathf.Abs(delta.y);
        return true;
    }

    private static float DifficultyChance(BattleModeComDifficultySettings settings) =>
        settings.difficulty switch
        {
            BattleModeComputerLevel.Easy => 0.25f,
            BattleModeComputerLevel.Hard => 1f,
            _ => 0.5f
        };

    private static float DifficultyCooldown(BattleModeComDifficultySettings settings) =>
        settings.difficulty switch
        {
            BattleModeComputerLevel.Easy => 20f,
            BattleModeComputerLevel.Hard => 10f,
            _ => 15f
        };

    private static int DifficultyWeight(BattleModeComDifficultySettings settings) =>
        settings.difficulty switch
        {
            BattleModeComputerLevel.Easy => -20,
            BattleModeComputerLevel.Hard => 40,
            _ => 0
        };

    private static string DirectionLabel(Vector2Int direction)
    {
        if (direction == Vector2Int.right) return "R";
        if (direction == Vector2Int.left) return "L";
        if (direction == Vector2Int.up) return "U";
        if (direction == Vector2Int.down) return "D";
        return "?";
    }

    private static void AppendTrace(
        ref string trace,
        Vector2Int direction,
        string directionTrace)
    {
        if (!string.IsNullOrEmpty(trace))
            trace += " | ";

        trace += $"{DirectionLabel(direction)}:{directionTrace}";
    }

    private void LogSurgical(string key, string message, bool force = false)
    {
        if (!EnableTankShootDiagnostics)
            return;

        int id = identity != null ? Mathf.Clamp(identity.playerId, 1, 6) : 0;
        if (DiagnosticPlayerIdFilter != 0 && id != DiagnosticPlayerIdFilter)
            return;

        if (!force &&
            key == lastSurgicalLogKey &&
            Time.time - lastSurgicalLogTime < SurgicalLogIntervalSeconds)
            return;

        lastSurgicalLogKey = key;
        lastSurgicalLogTime = Time.time;
        Vector2Int tile = movement != null
            ? WorldToTile(movement.transform.position)
            : Vector2Int.zero;
        Debug.LogWarning(
            $"[BattleCOM{DiagnosticName}][P{id}] tile:{tile} {key} {message}",
            this);
    }
}
