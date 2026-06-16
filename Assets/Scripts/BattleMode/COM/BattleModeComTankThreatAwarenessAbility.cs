using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Passive awareness for ready enemy tanks and active TankShot projectiles.
/// It predicts the projectile lane, first impact, and radius-1 cross explosion.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(BombController))]
public sealed class BattleModeComTankThreatAwarenessAbility : MonoBehaviour, IBattleModeComAbility
{
    public const int DiagnosticPlayerIdFilter = 0;
    public static readonly bool EnableTankThreatDiagnostics = false;

    private const int MaximumPredictionTiles = 24;
    private const int ImpactExplosionRadius = 1;
    private const int MinimumBaitDistanceTiles = 4;
    private const int ImmediateReadyTankDistanceTiles = 3;
    private const float ReadyTankReactionSeconds = 0.2f;
    private const float BaitExposureSeconds = 0.35f;
    private const float BaitRetryCooldownSeconds = 1.25f;
    private const float SurgicalLogIntervalSeconds = 0.25f;
    private const float ShotTrackIntervalSeconds = 0.15f;
    private const float RecentShotDeathWindowSeconds = 1.5f;
    private const float TankTraversalSafetyMarginSeconds = 0.06f;
    private const float CommittedDodgeTimeoutSeconds = 1.5f;
    private const float CommittedDodgeCenterTolerance = 0.08f;
    private const float ActiveShotLaneHalfWidthTiles = 0.6f;
    private const float ActiveShotBehindToleranceTiles = 0.55f;

    private static readonly Vector2Int[] CardinalTiles =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    private struct TankThreat
    {
        public string Kind;
        public Vector2Int OriginTile;
        public Vector2Int Direction;
        public Vector2Int ImpactTile;
        public int TravelTiles;
        public float SecondsToImpact;
        public int OwnerPlayerId;
        public int ShotId;
        public Vector2 ShotWorld;
        public float ShotSpeed;
        public string ImpactReason;
    }

    private struct SearchNode
    {
        public Vector2Int Parent;
        public int Depth;
    }

    private PlayerIdentity identity;
    private MovementController movement;
    private BombController bombController;
    private CharacterHealth characterHealth;
    private AbilitySystem abilitySystem;
    private GameManager gameManager;
    private Tilemap groundTilemap;
    private Tilemap destructibleTilemap;
    private Tilemap indestructibleTilemap;
    private ContactFilter2D obstacleFilter;
    private Collider2D[] ownColliders;
    private readonly Collider2D[] obstacleHits = new Collider2D[16];
    private readonly List<PlayerIdentity> activePlayers = new(6);
    private readonly List<TankThreat> activeThreats = new(8);
    private readonly Dictionary<Vector2Int, SearchNode> searchVisited = new(96);
    private readonly Queue<Vector2Int> searchOpen = new(96);
    private readonly Dictionary<int, float> shotLastTrackTime = new(8);
    private readonly HashSet<int> detectedShotIds = new();
    private float tileSize = 1f;
    private int explosionMask;
    private int projectileObstacleMask;
    private bool obstacleFilterInitialized;
    private int threatRefreshFrame = -1;

    private string lastDecisionTrace = "not evaluated";
    private readonly Dictionary<string, float> surgicalLogTimes = new(16);
    private bool baitActive;
    private Vector2Int baitTile;
    private int baitTankPlayerId;
    private float baitStartedTime = -10f;
    private float baitRetryAfterTime = -10f;
    private bool healthSubscribed;
    private float lastActiveShotSeenTime = -10f;
    private TankThreat lastActiveShotThreat;
    private float refreshClosestRelevantShotSeconds = float.PositiveInfinity;
    private Vector2Int lastDodgeStartTile;
    private Vector2Int lastDodgeTargetTile;
    private Vector2 lastDodgeMove;
    private float lastDodgeDecisionTime = -10f;
    private bool committedDodgeActive;
    private Vector2Int committedDodgeTarget;
    private int committedDodgeShotId;
    private float committedDodgeExpiresTime = -10f;

    public string DiagnosticName => "TankThreatAwareness";
    public string LastDecisionTrace => lastDecisionTrace;

    public bool IsAvailable
    {
        get
        {
            CacheReferences();
            return identity != null &&
                   movement != null &&
                   bombController != null &&
                   !movement.isDead;
        }
    }

    private bool CanPassBombs =>
        abilitySystem != null && abilitySystem.IsEnabled(BombPassAbility.AbilityId);

    private bool CanPassDestructibles =>
        abilitySystem != null && abilitySystem.IsEnabled(DestructiblePassAbility.AbilityId);

    private void Awake() => CacheReferences();

    private void OnEnable()
    {
        threatRefreshFrame = -1;
        CacheReferences();
        SubscribeHealth();
    }

    private void OnDisable()
    {
        if (healthSubscribed && characterHealth != null)
        {
            characterHealth.Damaged -= OnCharacterDamaged;
            characterHealth.Died -= OnCharacterDied;
        }

        healthSubscribed = false;
    }

    private void Update()
    {
        if (!EnableTankThreatDiagnostics || movement == null || movement.isDead)
            return;

        TrackDodgeProgress();
    }

    private void CacheReferences()
    {
        if (identity == null)
            TryGetComponent(out identity);

        if (movement == null)
            TryGetComponent(out movement);

        if (bombController == null)
            TryGetComponent(out bombController);

        if (characterHealth == null)
            TryGetComponent(out characterHealth);

        if (abilitySystem == null)
            TryGetComponent(out abilitySystem);

        if (ownColliders == null)
            ownColliders = GetComponentsInChildren<Collider2D>(true);

        if (movement != null)
        {
            tileSize = Mathf.Max(0.01f, movement.tileSize);
            if (!obstacleFilterInitialized)
            {
                obstacleFilter = new ContactFilter2D { useLayerMask = true, useTriggers = true };
                obstacleFilter.SetLayerMask(movement.obstacleMask);
                obstacleFilterInitialized = true;
            }
        }

        explosionMask = LayerMask.GetMask("Explosion");
        projectileObstacleMask = LayerMask.GetMask("Stage", "Water", "Explosion");

        if (gameManager == null)
        {
            gameManager = GameManager.Instance != null
                ? GameManager.Instance
                : FindAnyObjectByType<GameManager>();
        }
        if (gameManager != null)
        {
            groundTilemap = gameManager.groundTilemap;
            destructibleTilemap = gameManager.destructibleTilemap;
            indestructibleTilemap = gameManager.indestructibleTilemap;
        }

        SubscribeHealth();
    }

    private void SubscribeHealth()
    {
        if (!isActiveAndEnabled || healthSubscribed || characterHealth == null)
            return;

        characterHealth.Damaged += OnCharacterDamaged;
        characterHealth.Died += OnCharacterDied;
        healthSubscribed = true;
    }

    public bool HasImmediateThreat(Vector2Int myTile, out float dangerSeconds)
    {
        dangerSeconds = float.PositiveInfinity;
        if (!IsAvailable || !RefreshThreats())
            return false;

        bool found = false;
        for (int i = 0; i < activeThreats.Count; i++)
        {
            TankThreat threat = activeThreats[i];
            if (!IsImmediateThreatAt(threat, myTile))
                continue;

            dangerSeconds = Mathf.Min(
                dangerSeconds,
                EstimateThreatSecondsAt(threat, myTile));
            found = true;
        }

        return found;
    }

    public void LogPreventedEntry(
        Vector2Int currentTile,
        Vector2Int nextTile,
        float dangerSeconds,
        string blockReason)
    {
        LogSurgical(
            "BLOCK_ENTRY",
            $"from:{currentTile} next:{nextTile} danger:{dangerSeconds:F2} " +
            $"reason:{blockReason}");
    }

    public bool CanSafelyTraverseThreatenedTile(
        Vector2Int currentTile,
        Vector2Int tile,
        float clearanceSeconds,
        out string blockReason)
    {
        RefreshThreats();

        float speed = movement != null ? Mathf.Max(1f, movement.speed) : 4f;
        float arrivalSeconds = movement != null
            ? Vector2.Distance(movement.transform.position, TileToWorld(tile)) / speed
            : EstimateTraversalSeconds(1);
        bool canTraverse = CanTraverseTankThreats(
            currentTile,
            tile,
            arrivalSeconds,
            clearanceSeconds,
            out blockReason);
        if (canTraverse)
        {
            LogSurgical(
                "TRANSIT_ALLOW",
                $"from:{currentTile} tile:{tile} arrival:{arrivalSeconds:F2} " +
                $"clear:{clearanceSeconds:F2}");
        }

        return canTraverse;
    }

    public bool TryGetCommittedDodgeMove(
        out Vector2 move,
        out Vector2Int target)
    {
        move = Vector2.zero;
        target = committedDodgeTarget;
        if (!committedDodgeActive || movement == null)
            return false;

        Vector2 delta = TileToWorld(committedDodgeTarget) -
                        (Vector2)movement.transform.position;
        float tolerance =
            Mathf.Max(0.01f, tileSize * CommittedDodgeCenterTolerance);
        if (Mathf.Abs(delta.x) <= tolerance &&
            Mathf.Abs(delta.y) <= tolerance)
        {
            LogSurgical(
                "DODGE_COMMIT_REACHED",
                $"shot:S{committedDodgeShotId} target:{committedDodgeTarget}",
                true);
            ClearCommittedDodge();
            return false;
        }

        if (Time.time >= committedDodgeExpiresTime)
        {
            LogSurgical(
                "DODGE_COMMIT_TIMEOUT",
                $"shot:S{committedDodgeShotId} target:{committedDodgeTarget} " +
                $"world:{movement.transform.position}",
                true);
            ClearCommittedDodge();
            return false;
        }

        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y) &&
            Mathf.Abs(delta.x) > tolerance)
        {
            move = delta.x > 0f ? Vector2.right : Vector2.left;
        }
        else if (Mathf.Abs(delta.y) > tolerance)
        {
            move = delta.y > 0f ? Vector2.up : Vector2.down;
        }

        target = committedDodgeTarget;
        return move != Vector2.zero;
    }

    public bool TryBuildEmergencyDecision(
        BattleModeComDifficultySettings settings,
        BattleModeComController controller,
        Vector2Int myTile,
        float currentDangerSeconds,
        out BattleModeComAbilityDecision decision)
    {
        return TryBuildDodgeDecision(settings, myTile, "emergency", 5000, out decision);
    }

    public bool TryBuildCandidateDecision(
        BattleModeComDifficultySettings settings,
        BattleModeComController controller,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        return TryBuildDodgeDecision(settings, myTile, "candidate", 5000, out decision);
    }

    private bool TryBuildDodgeDecision(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        string phase,
        int weight,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;
        lastDecisionTrace = $"{phase} start";

        if (!IsAvailable)
        {
            lastDecisionTrace = $"{phase} unavailable";
            return false;
        }

        if (!RefreshThreats())
        {
            lastDecisionTrace = $"{phase} no tank threat";
            ClearBait();
            return false;
        }

        if (TryGetWorstImmediateThreatAt(myTile, out TankThreat immediateThreat))
        {
            ClearBait();
            return TryBuildEscapeDecision(
                settings,
                myTile,
                phase,
                weight,
                immediateThreat,
                out decision);
        }

        if (phase == "emergency")
        {
            lastDecisionTrace = "emergency no active or close tank threat";
            return false;
        }

        if (!TryGetWorstBaitThreatAt(myTile, out TankThreat baitThreat))
        {
            ClearBait();
            if (Time.time >= baitRetryAfterTime &&
                TryFindBaitSetupTile(
                    settings,
                    myTile,
                    out Vector2 setupMove,
                    out Vector2Int setupTile,
                    out TankThreat setupThreat,
                    out int setupDepth))
            {
                decision = new BattleModeComAbilityDecision
                {
                    Action = BattleModeComActionType.Reposition,
                    Weight = 3500,
                    TargetTile = setupTile,
                    HasTarget = true,
                    FirstMove = setupMove,
                    Reason = $"tank bait setup P{setupThreat.OwnerPlayerId}",
                    InputDescription = FirstMoveDescription(setupMove)
                };
                lastDecisionTrace =
                    $"candidate BAIT_SETUP tank:P{setupThreat.OwnerPlayerId} " +
                    $"target:{setupTile} depth:{setupDepth}";
                LogSurgical(
                    "BAIT_SETUP",
                    $"my:{myTile} tank:P{setupThreat.OwnerPlayerId}@{setupThreat.OriginTile} " +
                    $"target:{setupTile} move:{FirstMoveDescription(setupMove)} depth:{setupDepth}");
                return true;
            }

            lastDecisionTrace = "candidate no bait opportunity";
            return false;
        }

        if (!TryFindSafeTile(
                settings,
                myTile,
                out Vector2 baitEscapeMove,
                out Vector2Int baitEscapeTile,
                out int baitEscapeDepth))
        {
            lastDecisionTrace =
                $"candidate bait rejected no escape owner:P{baitThreat.OwnerPlayerId} " +
                $"origin:{baitThreat.OriginTile}";
            LogSurgical(
                "BAIT_REJECT_NO_ESCAPE",
                $"my:{myTile} owner:P{baitThreat.OwnerPlayerId} origin:{baitThreat.OriginTile}");
            ClearBait();
            return false;
        }

        if (!baitActive ||
            baitTile != myTile ||
            baitTankPlayerId != baitThreat.OwnerPlayerId)
        {
            if (Time.time < baitRetryAfterTime)
            {
                lastDecisionTrace =
                    $"candidate bait cooldown:{(baitRetryAfterTime - Time.time):F2}s";
                return false;
            }

            baitActive = true;
            baitTile = myTile;
            baitTankPlayerId = baitThreat.OwnerPlayerId;
            baitStartedTime = Time.time;
            LogSurgical(
                "BAIT_START",
                $"my:{myTile} tank:P{baitTankPlayerId}@{baitThreat.OriginTile} " +
                $"distance:{GetForwardDistance(baitThreat, myTile)} " +
                $"escape:{baitEscapeTile} move:{FirstMoveDescription(baitEscapeMove)}",
                true);
        }

        float baitAge = Time.time - baitStartedTime;
        if (baitAge < BaitExposureSeconds)
        {
            decision = new BattleModeComAbilityDecision
            {
                Action = BattleModeComActionType.Stopped,
                Weight = weight,
                TargetTile = myTile,
                HasTarget = true,
                FirstMove = Vector2.zero,
                Reason = $"tank bait P{baitTankPlayerId}",
                InputDescription = "none"
            };
            lastDecisionTrace =
                $"candidate BAIT_HOLD tank:P{baitTankPlayerId} age:{baitAge:F2}/" +
                $"{BaitExposureSeconds:F2} escape:{baitEscapeTile} depth:{baitEscapeDepth}";
            return true;
        }

        baitRetryAfterTime = Time.time + BaitRetryCooldownSeconds;
        ClearBait();
        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.Reposition,
            Weight = weight,
            TargetTile = baitEscapeTile,
            HasTarget = true,
            FirstMove = baitEscapeMove,
            Reason = $"tank bait retreat P{baitThreat.OwnerPlayerId}",
            InputDescription = FirstMoveDescription(baitEscapeMove)
        };
        lastDecisionTrace =
            $"candidate BAIT_RETREAT tank:P{baitThreat.OwnerPlayerId} " +
            $"target:{baitEscapeTile} depth:{baitEscapeDepth}";
        LogSurgical(
            "BAIT_RETREAT",
            $"my:{myTile} tank:P{baitThreat.OwnerPlayerId} target:{baitEscapeTile} " +
            $"move:{FirstMoveDescription(baitEscapeMove)}",
            true);
        return true;
    }

    private bool TryBuildEscapeDecision(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        string phase,
        int weight,
        TankThreat worst,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;
        if (!TryFindSafeTile(
                settings,
                myTile,
                out Vector2 firstMove,
                out Vector2Int targetTile,
                out int depth))
        {
            lastDecisionTrace =
                $"{phase} no route kind:{worst.Kind} origin:{worst.OriginTile} " +
                $"impact:{worst.ImpactTile} eta:{worst.SecondsToImpact:F2}";
            LogSurgical(
                "DODGE_NO_ROUTE",
                $"my:{myTile} kind:{worst.Kind} shot:S{worst.ShotId} " +
                $"origin:{worst.OriginTile} impact:{worst.ImpactTile} " +
                $"etaTile:{FormatSeconds(EstimateThreatSecondsAt(worst, myTile))} " +
                $"etaImpact:{worst.SecondsToImpact:F2}");
            return false;
        }

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.Reposition,
            Weight = weight,
            TargetTile = targetTile,
            HasTarget = true,
            FirstMove = firstMove,
            Reason = $"tank-threat dodge {worst.Kind}",
            InputDescription = FirstMoveDescription(firstMove)
        };

        lastDecisionTrace =
            $"{phase} DODGE kind:{worst.Kind} origin:{worst.OriginTile} " +
            $"impact:{worst.ImpactTile} eta:{worst.SecondsToImpact:F2} " +
            $"target:{targetTile} depth:{depth}";
        Vector2Int committedDirection = CardinalizeToTile(firstMove);
        Vector2Int committedDelta = targetTile - myTile;
        int committedDistance =
            Mathf.Abs(committedDelta.x) + Mathf.Abs(committedDelta.y);
        bool shouldCommitDodge =
            committedDirection != Vector2Int.zero &&
            committedDelta == committedDirection * committedDistance;
        bool sameCommittedDodge =
            shouldCommitDodge &&
            committedDodgeActive &&
            committedDodgeShotId == worst.ShotId &&
            committedDodgeTarget == targetTile;
        if (!sameCommittedDodge)
        {
            lastDodgeStartTile = myTile;
            lastDodgeTargetTile = targetTile;
            lastDodgeMove = firstMove;
            lastDodgeDecisionTime = Time.time;
        }

        if (shouldCommitDodge)
        {
            committedDodgeActive = true;
            if (!sameCommittedDodge)
            {
                committedDodgeTarget = targetTile;
                committedDodgeShotId = worst.ShotId;
                committedDodgeExpiresTime =
                    Time.time + CommittedDodgeTimeoutSeconds;
            }
        }
        else
        {
            ClearCommittedDodge();
        }
        LogSurgical(
            "DODGE",
            $"phase:{phase} my:{myTile} kind:{worst.Kind} owner:P{worst.OwnerPlayerId} " +
            $"shot:S{worst.ShotId} origin:{worst.OriginTile} impact:{worst.ImpactTile} " +
            $"etaTile:{FormatSeconds(EstimateThreatSecondsAt(worst, myTile))} " +
            $"etaImpact:{worst.SecondsToImpact:F2} target:{targetTile} " +
            $"targetEta:{FormatSeconds(EstimateThreatSecondsAt(worst, targetTile))} " +
            $"move:{FirstMoveDescription(firstMove)} depth:{depth}",
            !sameCommittedDodge);
        return true;
    }

    private bool RefreshThreats()
    {
        if (threatRefreshFrame == Time.frameCount)
            return activeThreats.Count > 0;

        threatRefreshFrame = Time.frameCount;
        activeThreats.Clear();
        refreshClosestRelevantShotSeconds = float.PositiveInfinity;
        AddActiveShotThreats();
        AddReadyTankThreats();
        return activeThreats.Count > 0;
    }

    private void AddActiveShotThreats()
    {
        TankShot[] shots = FindObjectsByType<TankShot>(FindObjectsInactive.Exclude);
        for (int i = 0; i < shots.Length; i++)
        {
            TankShot shot = shots[i];
            if (shot == null || shot.ImpactHandled)
                continue;

            Vector2Int direction = CardinalizeToTile(shot.Direction);
            if (direction == Vector2Int.zero)
                continue;

            Vector2Int origin = WorldToTile(shot.transform.position);
            PredictImpact(
                origin,
                direction,
                out Vector2Int impact,
                out int travelTiles,
                out string impactReason);

            int ownerId = ResolveOwnerPlayerId(shot.Owner);
            float seconds = EstimateShotImpactSeconds(
                shot.transform.position,
                impact,
                shot.Speed);
            activeThreats.Add(new TankThreat
            {
                Kind = "active-shot",
                OriginTile = origin,
                Direction = direction,
                ImpactTile = impact,
                TravelTiles = travelTiles,
                SecondsToImpact = seconds,
                OwnerPlayerId = ownerId,
                ShotId = shot.ShotId,
                ShotWorld = shot.transform.position,
                ShotSpeed = shot.Speed,
                ImpactReason = impactReason
            });
            TankThreat threat = activeThreats[activeThreats.Count - 1];
            Vector2Int myTile = movement != null
                ? WorldToTile(movement.transform.position)
                : Vector2Int.zero;
            float relevantSeconds = EstimateThreatSecondsAt(threat, myTile);
            if (!float.IsInfinity(relevantSeconds) &&
                relevantSeconds < refreshClosestRelevantShotSeconds)
            {
                refreshClosestRelevantShotSeconds = relevantSeconds;
                lastActiveShotSeenTime = Time.time;
                lastActiveShotThreat = threat;
            }
            LogActiveShotTracking(threat);
        }
    }

    private void AddReadyTankThreats()
    {
        activePlayers.Clear();
        PlayerIdentity.GetActivePlayers(activePlayers);

        for (int i = 0; i < activePlayers.Count; i++)
        {
            PlayerIdentity player = activePlayers[i];
            if (player == null || player == identity || IsAlly(player.playerId))
                continue;

            if (!player.TryGetComponent(out MovementController tankMovement) ||
                tankMovement == null ||
                tankMovement.isDead)
                continue;

            if (!player.TryGetComponent(out PlayerMountCompanion mount) ||
                mount == null ||
                mount.GetMountedLouieType() != MountedType.Tank)
                continue;

            if (!player.TryGetComponent(out TankMountShootAbility shootAbility) ||
                shootAbility == null ||
                !shootAbility.CanStartShot)
                continue;

            Vector2Int direction = CardinalizeToTile(tankMovement.FacingDirection);
            if (direction == Vector2Int.zero)
                continue;

            Vector2Int origin = WorldToTile(player.transform.position);
            PredictImpact(
                origin,
                direction,
                out Vector2Int impact,
                out int travelTiles,
                out string impactReason);

            activeThreats.Add(new TankThreat
            {
                Kind = "ready-tank",
                OriginTile = origin,
                Direction = direction,
                ImpactTile = impact,
                TravelTiles = travelTiles,
                SecondsToImpact = ReadyTankReactionSeconds,
                OwnerPlayerId = player.playerId,
                ShotId = 0,
                ShotWorld = player.transform.position,
                ShotSpeed = 0f,
                ImpactReason = impactReason
            });
        }
    }

    private void PredictImpact(
        Vector2Int origin,
        Vector2Int direction,
        out Vector2Int impactTile,
        out int travelTiles,
        out string impactReason)
    {
        impactTile = origin + direction * MaximumPredictionTiles;
        travelTiles = MaximumPredictionTiles;
        impactReason = "prediction-limit";

        for (int step = 1; step <= MaximumPredictionTiles; step++)
        {
            Vector2Int tile = origin + direction * step;
            if (!TryGetProjectileImpactReason(tile, out string reason))
                continue;

            impactTile = tile;
            travelTiles = step;
            impactReason = reason;
            return;
        }
    }

    private bool TryGetProjectileImpactReason(
        Vector2Int tile,
        out string reason)
    {
        if (!HasGroundTile(tile))
        {
            reason = "no-ground";
            return true;
        }

        if (HasIndestructibleTile(tile))
        {
            reason = "indestructible";
            return true;
        }

        if (HasDestructibleTile(tile))
        {
            reason = "destructible";
            return true;
        }

        if (FindBombAt(tile) != null)
        {
            reason = "bomb";
            return true;
        }

        if (projectileObstacleMask != 0 &&
            Physics2D.OverlapCircle(
                TileToWorld(tile),
                tileSize * 0.2f,
                projectileObstacleMask) != null)
        {
            reason = "physics-obstacle";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private bool TryGetWorstImmediateThreatAt(Vector2Int tile, out TankThreat worst)
    {
        worst = default;
        float bestSeconds = float.PositiveInfinity;
        bool found = false;

        for (int i = 0; i < activeThreats.Count; i++)
        {
            TankThreat threat = activeThreats[i];
            if (!IsImmediateThreatAt(threat, tile))
                continue;

            float prioritySeconds = threat.Kind == "active-shot"
                ? EstimateThreatSecondsAt(threat, tile)
                : ReadyTankReactionSeconds + 10f;
            if (prioritySeconds >= bestSeconds)
                continue;

            bestSeconds = prioritySeconds;
            worst = threat;
            found = true;
        }

        return found;
    }

    private bool TryGetWorstBaitThreatAt(Vector2Int tile, out TankThreat worst)
    {
        worst = default;
        int bestDistance = int.MaxValue;
        bool found = false;

        for (int i = 0; i < activeThreats.Count; i++)
        {
            TankThreat threat = activeThreats[i];
            if (threat.Kind != "ready-tank" || !IsTileOnProjectileLane(threat, tile))
                continue;

            int distance = GetForwardDistance(threat, tile);
            if (distance < MinimumBaitDistanceTiles || distance >= bestDistance)
                continue;

            bestDistance = distance;
            worst = threat;
            found = true;
        }

        return found;
    }

    private bool IsImmediateThreatAt(TankThreat threat, Vector2Int tile)
    {
        if (threat.Kind == "active-shot")
            return IsTileThreatenedBy(threat, tile);

        return threat.Kind == "ready-tank" &&
               IsTileOnProjectileLane(threat, tile) &&
               GetForwardDistance(threat, tile) == ImmediateReadyTankDistanceTiles;
    }

    private static int GetForwardDistance(TankThreat threat, Vector2Int tile)
    {
        Vector2Int delta = tile - threat.OriginTile;
        return delta.x * threat.Direction.x + delta.y * threat.Direction.y;
    }

    private bool IsTileThreatenedBy(TankThreat threat, Vector2Int tile)
    {
        if (threat.Kind == "active-shot")
        {
            return IsTileOnActiveProjectilePath(threat, tile) ||
                   IsTileInSimpleBlastZone(
                       threat.ImpactTile,
                       tile,
                       ImpactExplosionRadius);
        }

        return IsTileOnProjectileLane(threat, tile) ||
               IsTileInSimpleBlastZone(threat.ImpactTile, tile, ImpactExplosionRadius);
    }

    private static bool IsTileOnProjectileLane(TankThreat threat, Vector2Int tile)
    {
        Vector2Int delta = tile - threat.OriginTile;
        int forwardDistance = GetForwardDistance(threat, tile);
        return forwardDistance >= 1 &&
               forwardDistance <= threat.TravelTiles &&
               delta == threat.Direction * forwardDistance;
    }

    private bool IsTileThreatenedByAnyTank(Vector2Int tile)
    {
        for (int i = 0; i < activeThreats.Count; i++)
        {
            TankThreat threat = activeThreats[i];
            if (threat.Kind == "active-shot" && IsTileThreatenedBy(threat, tile))
                return true;
        }

        return false;
    }

    private bool IsTileOnActiveProjectilePath(TankThreat threat, Vector2Int tile)
    {
        GetActiveShotOffsets(threat, tile, out float forward, out float lateral);
        float size = Mathf.Max(0.01f, tileSize);
        Vector2 direction = new(threat.Direction.x, threat.Direction.y);
        float impactForward = Vector2.Dot(
            TileToWorld(threat.ImpactTile) - threat.ShotWorld,
            direction);
        float minForward = -size * ActiveShotBehindToleranceTiles;
        float maxForward = Mathf.Max(0f, impactForward) + size * 0.5f;
        return lateral <= size * ActiveShotLaneHalfWidthTiles &&
               forward >= minForward &&
               forward <= maxForward;
    }

    private void GetActiveShotOffsets(
        TankThreat threat,
        Vector2Int tile,
        out float forward,
        out float lateral)
    {
        Vector2 direction = new(threat.Direction.x, threat.Direction.y);
        Vector2 toTile = TileToWorld(tile) - threat.ShotWorld;
        forward = Vector2.Dot(toTile, direction);
        Vector2 perpendicular = new(-direction.y, direction.x);
        lateral = Mathf.Abs(Vector2.Dot(toTile, perpendicular));
    }

    private bool TryFindSafeTile(
        BattleModeComDifficultySettings settings,
        Vector2Int start,
        out Vector2 firstMove,
        out Vector2Int target,
        out int resultDepth)
    {
        firstMove = Vector2.zero;
        target = start;
        resultDepth = 0;

        searchVisited.Clear();
        searchOpen.Clear();
        searchVisited[start] = new SearchNode { Parent = start, Depth = 0 };
        searchOpen.Enqueue(start);

        int rejectedWalkable = 0;
        int rejectedTank = 0;
        int rejectedDanger = 0;
        int rejectedVisited = 0;
        int maxDepth = Mathf.Max(4, settings.searchDepth + 2);
        while (searchOpen.Count > 0)
        {
            Vector2Int tile = searchOpen.Dequeue();
            SearchNode node = searchVisited[tile];
            float eta = EstimateTraversalSeconds(node.Depth);

            if (node.Depth > 0 &&
                !IsUnsafeTankEscapeTarget(tile) &&
                float.IsInfinity(GetBombDangerSeconds(tile)) &&
                !IsDangerousAt(tile, eta + settings.safeTileMinimumSeconds, settings))
            {
                target = tile;
                resultDepth = node.Depth;
                Vector2Int firstStep = ReconstructFirstStep(start, tile);
                firstMove = new Vector2(firstStep.x, firstStep.y);
                return firstMove != Vector2.zero;
            }

            if (node.Depth >= maxDepth)
                continue;

            for (int i = 0; i < CardinalTiles.Length; i++)
            {
                Vector2Int next = tile + CardinalTiles[i];
                if (searchVisited.ContainsKey(next))
                {
                    rejectedVisited++;
                    continue;
                }

                if (!IsWalkableTile(next, start))
                {
                    rejectedWalkable++;
                    continue;
                }

                float nextArrival = EstimateTraversalSeconds(node.Depth + 1);
                float clearanceSeconds = EstimateTraversalSeconds(1);
                if (!CanTraverseTankThreats(
                        tile,
                        next,
                        nextArrival,
                        clearanceSeconds,
                        out _))
                {
                    rejectedTank++;
                    continue;
                }

                if (IsDangerousAt(next, nextArrival, settings))
                {
                    rejectedDanger++;
                    continue;
                }

                searchVisited[next] = new SearchNode
                {
                    Parent = tile,
                    Depth = node.Depth + 1
                };
                searchOpen.Enqueue(next);
            }
        }

        LogSurgical(
            "DODGE_SEARCH_EXHAUSTED",
            $"start:{start} maxDepth:{maxDepth} visited:{searchVisited.Count} " +
            $"reject[visited:{rejectedVisited} walk:{rejectedWalkable} " +
            $"tank:{rejectedTank} danger:{rejectedDanger}] " +
            $"neighbors:{DescribeNeighborSafety(start, settings)}");
        return false;
    }

    private bool IsUnsafeTankEscapeTarget(Vector2Int tile)
    {
        for (int i = 0; i < activeThreats.Count; i++)
        {
            TankThreat threat = activeThreats[i];
            if (IsTileThreatenedBy(threat, tile))
                return true;
        }

        return false;
    }

    private bool CanTraverseTankThreats(
        Vector2Int fromTile,
        Vector2Int tile,
        float arrivalSeconds,
        float clearanceSeconds,
        out string blockReason)
    {
        blockReason = "none";
        for (int i = 0; i < activeThreats.Count; i++)
        {
            TankThreat threat = activeThreats[i];
            if (threat.Kind != "active-shot")
                continue;

            Vector2Int step = tile - fromTile;
            bool fromOnDirectPath = IsTileOnActiveProjectilePath(threat, fromTile);
            bool parallelStep =
                step.x * threat.Direction.x + step.y * threat.Direction.y != 0;
            if (fromOnDirectPath && parallelStep)
            {
                blockReason =
                    $"parallel-shot:S{threat.ShotId} dir:{threat.Direction} " +
                    $"fromEta:{FormatSeconds(EstimateThreatSecondsAt(threat, fromTile))}";
                return false;
            }

            if (fromOnDirectPath &&
                WouldCrossActiveShotLine(threat, fromTile, tile))
            {
                blockReason =
                    $"cross-shot-line:S{threat.ShotId} dir:{threat.Direction}";
                return false;
            }

            if (!IsTileThreatenedBy(threat, tile))
                continue;

            float dangerSeconds = EstimateThreatSecondsAt(threat, tile);
            float clearAt =
                arrivalSeconds +
                clearanceSeconds +
                TankTraversalSafetyMarginSeconds;
            if (float.IsInfinity(dangerSeconds) || clearAt < dangerSeconds)
                continue;

            blockReason =
                $"timing-shot:S{threat.ShotId} eta:{FormatSeconds(dangerSeconds)} " +
                $"clearAt:{clearAt:F2}";
            return false;
        }

        return true;
    }

    private bool WouldCrossActiveShotLine(
        TankThreat threat,
        Vector2Int fromTile,
        Vector2Int toTile)
    {
        Vector2Int currentMovementTile = movement != null
            ? WorldToTile(movement.transform.position)
            : new Vector2Int(int.MinValue, int.MinValue);
        Vector2 fromWorld = movement != null && fromTile == currentMovementTile
            ? movement.transform.position
            : TileToWorld(fromTile);
        float fromLateral = GetActiveShotSignedLateral(threat, fromWorld);
        float toLateral = GetActiveShotSignedLateral(threat, TileToWorld(toTile));
        float centerTolerance = Mathf.Max(0.01f, tileSize * 0.05f);
        if (Mathf.Abs(fromLateral) <= centerTolerance)
            return false;

        bool crossesLine = Mathf.Sign(fromLateral) != Mathf.Sign(toLateral);
        bool movesTowardLine = Mathf.Abs(toLateral) < Mathf.Abs(fromLateral);
        return crossesLine || movesTowardLine;
    }

    private static float GetActiveShotSignedLateral(
        TankThreat threat,
        Vector2 world)
    {
        Vector2 direction = new(threat.Direction.x, threat.Direction.y);
        Vector2 perpendicular = new(-direction.y, direction.x);
        return Vector2.Dot(world - threat.ShotWorld, perpendicular);
    }

    private bool TryFindBaitSetupTile(
        BattleModeComDifficultySettings settings,
        Vector2Int start,
        out Vector2 firstMove,
        out Vector2Int target,
        out TankThreat targetThreat,
        out int resultDepth)
    {
        firstMove = Vector2.zero;
        target = start;
        targetThreat = default;
        resultDepth = 0;

        searchVisited.Clear();
        searchOpen.Clear();
        searchVisited[start] = new SearchNode { Parent = start, Depth = 0 };
        searchOpen.Enqueue(start);

        int maxDepth = Mathf.Clamp(settings.searchDepth, 3, 6);
        while (searchOpen.Count > 0)
        {
            Vector2Int tile = searchOpen.Dequeue();
            SearchNode node = searchVisited[tile];

            if (node.Depth > 0 &&
                TryGetWorstBaitThreatAt(tile, out TankThreat baitThreat) &&
                HasBaitEscapeStep(tile, start, settings))
            {
                target = tile;
                targetThreat = baitThreat;
                resultDepth = node.Depth;
                Vector2Int firstStep = ReconstructFirstStep(start, tile);
                firstMove = new Vector2(firstStep.x, firstStep.y);
                return firstMove != Vector2.zero;
            }

            if (node.Depth >= maxDepth)
                continue;

            for (int i = 0; i < CardinalTiles.Length; i++)
            {
                Vector2Int next = tile + CardinalTiles[i];
                if (searchVisited.ContainsKey(next) ||
                    !IsWalkableTile(next, start) ||
                    IsImmediateThreatInSnapshot(next) ||
                    IsDangerousAt(next, EstimateTraversalSeconds(node.Depth + 1), settings))
                    continue;

                searchVisited[next] = new SearchNode
                {
                    Parent = tile,
                    Depth = node.Depth + 1
                };
                searchOpen.Enqueue(next);
            }
        }

        return false;
    }

    private bool HasBaitEscapeStep(
        Vector2Int baitCandidate,
        Vector2Int start,
        BattleModeComDifficultySettings settings)
    {
        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int escape = baitCandidate + CardinalTiles[i];
            if (escape == start)
                return true;

            if (!IsWalkableTile(escape, baitCandidate) ||
                IsTileThreatenedByAnyTank(escape) ||
                IsDangerousAt(escape, EstimateTraversalSeconds(1), settings))
                continue;

            return true;
        }

        return false;
    }

    private bool IsImmediateThreatInSnapshot(Vector2Int tile)
    {
        for (int i = 0; i < activeThreats.Count; i++)
        {
            TankThreat threat = activeThreats[i];
            if (IsImmediateThreatAt(threat, tile))
                return true;
        }

        return false;
    }

    private float GetBombDangerSeconds(Vector2Int tile)
    {
        if (explosionMask != 0 &&
            Physics2D.OverlapCircle(TileToWorld(tile), tileSize * 0.25f, explosionMask) != null)
            return 0f;

        float danger = float.PositiveInfinity;
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded || bomb.IsBeingHeldByPowerGlove)
                continue;

            Vector2Int bombTile = WorldToTile(bomb.GetLogicalPosition());
            int radius = bomb.Owner != null
                ? Mathf.Max(1, bomb.Owner.GetPredictedBlastRadius(bomb))
                : 2;
            if (!IsTileInBlastLine(bombTile, tile, radius))
                continue;

            float seconds = bomb.IsControlBomb ? 0.65f : bomb.RemainingFuseSeconds;
            danger = Mathf.Min(danger, seconds);
        }

        return danger;
    }

    private bool IsDangerousAt(
        Vector2Int tile,
        float arrivalSeconds,
        BattleModeComDifficultySettings settings)
    {
        float dangerSeconds = GetBombDangerSeconds(tile);
        return !float.IsInfinity(dangerSeconds) &&
               dangerSeconds <= arrivalSeconds + settings.dangerReactionSeconds;
    }

    private bool IsTileInBlastLine(Vector2Int origin, Vector2Int tile, int radius)
    {
        if (origin == tile)
            return true;

        Vector2Int delta = tile - origin;
        bool aligned = (delta.x == 0 && delta.y != 0) ||
                       (delta.y == 0 && delta.x != 0);
        if (!aligned)
            return false;

        int distance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        if (distance > radius)
            return false;

        Vector2Int direction = new(
            Mathf.Clamp(delta.x, -1, 1),
            Mathf.Clamp(delta.y, -1, 1));
        for (int step = 1; step < distance; step++)
        {
            Vector2Int check = origin + direction * step;
            if (HasIndestructibleTile(check) ||
                HasDestructibleTile(check) ||
                FindBombAt(check) != null)
                return false;
        }

        return true;
    }

    private static bool IsTileInSimpleBlastZone(
        Vector2Int origin,
        Vector2Int tile,
        int radius)
    {
        Vector2Int delta = tile - origin;
        return delta == Vector2Int.zero ||
               (delta.x == 0 && Mathf.Abs(delta.y) <= radius) ||
               (delta.y == 0 && Mathf.Abs(delta.x) <= radius);
    }

    private bool IsWalkableTile(Vector2Int tile, Vector2Int startTile)
    {
        if (!HasGroundTile(tile) ||
            HasIndestructibleTile(tile) ||
            (HasDestructibleTile(tile) && !CanPassDestructibles) ||
            (FindBombAt(tile) != null && tile != startTile && !CanPassBombs))
            return false;

        if (movement != null && movement.obstacleMask.value != 0)
        {
            Vector2 center = TileToWorld(tile);
            Vector2 size = Vector2.one * (tileSize * 0.55f);
            int hitCount = Physics2D.OverlapBox(
                center,
                size,
                0f,
                obstacleFilter,
                obstacleHits);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = obstacleHits[i];
                if (hit == null || IsOwnCollider(hit))
                    continue;

                if (hit.GetComponentInParent<ItemPickup>() != null ||
                    hit.GetComponentInParent<PlayerIdentity>() != null)
                    continue;

                if (hit.GetComponentInParent<Bomb>() != null &&
                    (tile == startTile || CanPassBombs))
                    continue;

                if (CanPassDestructibles && IsDestructibleCollider(hit))
                    continue;

                return false;
            }
        }

        return true;
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

    private bool HasGroundTile(Vector2Int tile)
    {
        if (groundTilemap == null)
            return true;

        return groundTilemap.HasTile(groundTilemap.WorldToCell(TileToWorld(tile)));
    }

    private bool HasDestructibleTile(Vector2Int tile) =>
        destructibleTilemap != null &&
        destructibleTilemap.HasTile(destructibleTilemap.WorldToCell(TileToWorld(tile)));

    private bool HasIndestructibleTile(Vector2Int tile) =>
        indestructibleTilemap != null &&
        indestructibleTilemap.HasTile(indestructibleTilemap.WorldToCell(TileToWorld(tile)));

    private bool IsOwnCollider(Collider2D colliderToCheck)
    {
        if (ownColliders == null)
            return false;

        for (int i = 0; i < ownColliders.Length; i++)
        {
            if (ownColliders[i] == colliderToCheck)
                return true;
        }

        return false;
    }

    private static bool IsDestructibleCollider(Collider2D collider)
    {
        Transform current = collider != null ? collider.transform : null;
        int guard = 0;
        while (current != null && guard++ < 6)
        {
            if (current.CompareTag("Destructibles"))
                return true;

            current = current.parent;
        }

        return false;
    }

    private bool IsAlly(int otherPlayerId)
    {
        if (BattleModeRules.Instance == null ||
            !BattleModeRules.Instance.UsesTeams ||
            identity == null)
            return false;

        return BattleModeRules.Instance.GetTeamForPlayer(identity.playerId) ==
               BattleModeRules.Instance.GetTeamForPlayer(otherPlayerId);
    }

    private int ResolveOwnerPlayerId(GameObject owner)
    {
        if (owner == null)
            return 0;

        PlayerIdentity ownerIdentity = owner.GetComponentInParent<PlayerIdentity>();
        return ownerIdentity != null ? ownerIdentity.playerId : 0;
    }

    private void LogActiveShotTracking(TankThreat threat)
    {
        Vector2Int myTile = movement != null
            ? WorldToTile(movement.transform.position)
            : Vector2Int.zero;
        bool firstDetection = detectedShotIds.Add(threat.ShotId);
        bool shouldTrack =
            !shotLastTrackTime.TryGetValue(threat.ShotId, out float lastTime) ||
            Time.time - lastTime >= ShotTrackIntervalSeconds;
        if (!firstDetection && !shouldTrack)
            return;

        shotLastTrackTime[threat.ShotId] = Time.time;
        string key = firstDetection ? "SHOT_DETECTED" : "SHOT_TRACK";
        GetActiveShotOffsets(threat, myTile, out float forward, out float lateral);
        LogSurgical(
            key,
            $"shot:S{threat.ShotId} owner:P{threat.OwnerPlayerId} " +
            $"world:{threat.ShotWorld} origin:{threat.OriginTile} dir:{threat.Direction} " +
            $"speed:{threat.ShotSpeed:F2} my:{myTile} " +
            $"lane:{IsTileOnActiveProjectilePath(threat, myTile)} " +
            $"forward:{forward:F2} lateral:{lateral:F2} " +
            $"blast:{IsTileInSimpleBlastZone(threat.ImpactTile, myTile, ImpactExplosionRadius)} " +
            $"etaTile:{FormatSeconds(EstimateThreatSecondsAt(threat, myTile))} " +
            $"impact:{threat.ImpactTile} etaImpact:{threat.SecondsToImpact:F2} " +
            $"impactReason:{threat.ImpactReason}",
            firstDetection);
    }

    private float EstimateThreatSecondsAt(TankThreat threat, Vector2Int tile)
    {
        if (threat.Kind != "active-shot")
        {
            return IsTileOnProjectileLane(threat, tile)
                ? threat.SecondsToImpact
                : float.PositiveInfinity;
        }

        if (IsTileOnActiveProjectilePath(threat, tile))
        {
            GetActiveShotOffsets(threat, tile, out float forward, out _);
            return Mathf.Max(0f, forward) / Mathf.Max(0.1f, threat.ShotSpeed);
        }

        if (IsTileInSimpleBlastZone(threat.ImpactTile, tile, ImpactExplosionRadius))
            return threat.SecondsToImpact;

        return float.PositiveInfinity;
    }

    private string DescribeNeighborSafety(
        Vector2Int start,
        BattleModeComDifficultySettings settings)
    {
        List<string> details = new(CardinalTiles.Length);
        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int tile = start + CardinalTiles[i];
            bool walkable = IsWalkableTile(tile, start);
            bool tank = IsTileThreatenedByAnyTank(tile);
            bool traversable = CanTraverseTankThreats(
                start,
                tile,
                EstimateTraversalSeconds(1),
                EstimateTraversalSeconds(1),
                out string tankReason);
            float bomb = GetBombDangerSeconds(tile);
            bool danger = IsDangerousAt(tile, EstimateTraversalSeconds(1), settings);
            details.Add(
                $"{tile}[walk:{walkable} tank:{tank} traverse:{traversable} " +
                $"tankReason:{tankReason} bomb:{FormatSeconds(bomb)} danger:{danger}]");
        }

        return string.Join(";", details);
    }

    private void TrackDodgeProgress()
    {
        float age = Time.time - lastDodgeDecisionTime;
        if (age < 0f || age > 1.25f)
            return;

        Vector2Int currentTile = WorldToTile(movement.transform.position);
        LogSurgical(
            "DODGE_PROGRESS",
            $"age:{age:F2} start:{lastDodgeStartTile} current:{currentTile} " +
            $"target:{lastDodgeTargetTile} requested:{FirstMoveDescription(lastDodgeMove)} " +
            $"world:{movement.transform.position} speed:{movement.speed:F2}");
    }

    private void OnCharacterDied()
    {
        float shotAge = Time.time - lastActiveShotSeenTime;
        if (shotAge > RecentShotDeathWindowSeconds)
            return;

        Vector2Int tile = movement != null
            ? WorldToTile(movement.transform.position)
            : Vector2Int.zero;
        LogSurgical(
            "DEATH_NEAR_SHOT",
            $"shotAge:{shotAge:F2} shot:S{lastActiveShotThreat.ShotId} " +
            $"owner:P{lastActiveShotThreat.OwnerPlayerId} shotWorld:{lastActiveShotThreat.ShotWorld} " +
            $"dir:{lastActiveShotThreat.Direction} impact:{lastActiveShotThreat.ImpactTile} " +
            $"etaTile:{FormatSeconds(EstimateThreatSecondsAt(lastActiveShotThreat, tile))} " +
            $"etaImpact:{lastActiveShotThreat.SecondsToImpact:F2} " +
            $"lastDecisionAge:{(Time.time - lastDodgeDecisionTime):F2} " +
            $"dodge:{lastDodgeStartTile}->{lastDodgeTargetTile} " +
            $"move:{FirstMoveDescription(lastDodgeMove)} trace:{lastDecisionTrace}",
            true);
    }

    private void OnCharacterDamaged(int amount)
    {
        float shotAge = Time.time - lastActiveShotSeenTime;
        if (shotAge > RecentShotDeathWindowSeconds)
            return;

        Vector2Int tile = movement != null
            ? WorldToTile(movement.transform.position)
            : Vector2Int.zero;
        LogSurgical(
            "DAMAGE_NEAR_SHOT",
            $"amount:{amount} shotAge:{shotAge:F2} shot:S{lastActiveShotThreat.ShotId} " +
            $"owner:P{lastActiveShotThreat.OwnerPlayerId} tile:{tile} " +
            $"shotWorld:{lastActiveShotThreat.ShotWorld} dir:{lastActiveShotThreat.Direction} " +
            $"impact:{lastActiveShotThreat.ImpactTile} " +
            $"lastDecisionAge:{(Time.time - lastDodgeDecisionTime):F2} " +
            $"dodge:{lastDodgeStartTile}->{lastDodgeTargetTile} " +
            $"move:{FirstMoveDescription(lastDodgeMove)} trace:{lastDecisionTrace}",
            true);
    }

    private static string FormatSeconds(float seconds) =>
        float.IsInfinity(seconds) ? "inf" : $"{seconds:F2}";

    private Vector2Int ReconstructFirstStep(Vector2Int start, Vector2Int goal)
    {
        Vector2Int current = goal;
        int guard = 0;
        while (searchVisited.TryGetValue(current, out SearchNode node) &&
               node.Parent != start &&
               current != start &&
               guard++ < 128)
        {
            current = node.Parent;
        }

        return current - start;
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

    private float EstimateTraversalSeconds(int depth)
    {
        float tilesPerSecond = movement != null
            ? Mathf.Max(1f, movement.speed)
            : 4f;
        return depth / tilesPerSecond;
    }

    private float EstimateShotImpactSeconds(
        Vector2 shotWorld,
        Vector2Int impactTile,
        float shotSpeed)
    {
        float worldDistance = Vector2.Distance(shotWorld, TileToWorld(impactTile));
        return worldDistance / Mathf.Max(0.1f, shotSpeed);
    }

    private static Vector2Int CardinalizeToTile(Vector2 direction)
    {
        if (direction == Vector2.zero)
            return Vector2Int.zero;

        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
            return direction.x >= 0f ? Vector2Int.right : Vector2Int.left;

        return direction.y >= 0f ? Vector2Int.up : Vector2Int.down;
    }

    private static string FirstMoveDescription(Vector2 move)
    {
        if (move == Vector2.zero) return "none";
        if (move.x > 0.5f) return "MoveRight";
        if (move.x < -0.5f) return "MoveLeft";
        if (move.y > 0.5f) return "MoveUp";
        if (move.y < -0.5f) return "MoveDown";
        return "none";
    }

    private void ClearBait()
    {
        baitActive = false;
        baitTile = Vector2Int.zero;
        baitTankPlayerId = 0;
        baitStartedTime = -10f;
    }

    private void ClearCommittedDodge()
    {
        committedDodgeActive = false;
        committedDodgeTarget = Vector2Int.zero;
        committedDodgeShotId = 0;
        committedDodgeExpiresTime = -10f;
    }

    private void LogSurgical(string key, string message, bool force = false)
    {
        if (!EnableTankThreatDiagnostics)
            return;

        int id = identity != null ? Mathf.Clamp(identity.playerId, 1, 6) : 0;
        if (DiagnosticPlayerIdFilter != 0 && id != DiagnosticPlayerIdFilter)
            return;

        if (!force &&
            surgicalLogTimes.TryGetValue(key, out float lastLogTime) &&
            Time.time - lastLogTime < SurgicalLogIntervalSeconds)
            return;

        surgicalLogTimes[key] = Time.time;
        Vector2Int tile = movement != null
            ? WorldToTile(movement.transform.position)
            : Vector2Int.zero;
        Debug.LogWarning(
            $"[BattleCOM{DiagnosticName}][P{id}] tile:{tile} {key} {message}",
            this);
    }
}
