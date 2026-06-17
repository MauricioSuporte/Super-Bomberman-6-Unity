using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Teaches Battle Mode COM players to tap ActionA to pick up a bomb and tap
/// again to throw it immediately toward the selected landing tile.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(BombController))]
public sealed class BattleModeComPowerGloveAbility : MonoBehaviour, IBattleModeComAbility
{
    private const float SequenceTimeoutSeconds = 6f;
    private const float PickupConfirmationSeconds = 0.35f;
    private const float PickupAbortEscapeMarginSeconds = 0.15f;
    private const float ReleaseConfirmationSeconds = 0.8f;
    private const float OffensiveCooldownSeconds = 2f;
    private const float PlannedPickupAuthorizationSeconds = 0.8f;
    private const float CenterToleranceTiles = 0.12f;
    private const float CarryMaxSeconds = 5f;
    private const float CarryNoPathGraceSeconds = 1.25f;
    private const int CarrySearchMaxDepth = 14;
    private const float CarryStepDangerMarginSeconds = 0.35f;

    private static readonly Vector2Int[] CardinalTiles =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    private enum SequenceState
    {
        None,
        PickingUp,
        Carrying,
        Releasing
    }

    private PlayerIdentity identity;
    private MovementController movement;
    private BombController bombController;
    private PowerGloveAbility powerGlove;
    private PlayerMountCompanion mountCompanion;
    private GameManager gameManager;
    private Tilemap groundTilemap;
    private Tilemap destructibleTilemap;
    private Tilemap indestructibleTilemap;
    private readonly List<PlayerIdentity> activePlayers = new List<PlayerIdentity>(6);

    private SequenceState sequenceState;
    private float sequenceStartedTime = -10f;
    private float sequenceStateStartedTime = -10f;
    private float nextOffensiveTime = -10f;
    private int offensiveChanceFrame = -1;
    private bool offensiveChanceResult;
    private string bombChanceOpportunityId = string.Empty;
    private BattleModeComputerLevel bombChanceDifficulty;
    private bool bombChanceResult;
    private Vector2Int authorizedPlantTile;
    private float authorizedPlantPickupUntil = -10f;
    private Bomb sequenceBomb;
    private Vector2Int sequencePlantTile;
    private Vector2Int sequenceThrowDirection;
    private Vector2Int sequenceLandingTile;
    private bool carryModePlanned;
    private float carryStartedTime = -10f;
    private float carryNoPathSince = -10f;
    private readonly Dictionary<Vector2Int, Vector2Int> carrySearchParents =
        new Dictionary<Vector2Int, Vector2Int>(128);
    private readonly Dictionary<Vector2Int, int> carrySearchDepth =
        new Dictionary<Vector2Int, int>(128);
    private readonly Queue<Vector2Int> carrySearchOpen = new Queue<Vector2Int>(64);
    private float tileSize = 1f;
    private int explosionMask;
    private string lastDecisionTrace = "not evaluated";

    public string DiagnosticName => "PowerGlove";
    public string LastDecisionTrace => lastDecisionTrace;
    public bool HasActiveSequence =>
        sequenceState != SequenceState.None ||
        (powerGlove != null && powerGlove.IsHoldingBomb);

    public bool ShouldPrioritizeEmergency(Vector2Int myTile)
    {
        CacheReferences();
        if (IsMountedOnAnyMount())
            return false;

        return HasActiveSequence ||
               (IsAvailable && FindPotentialPickupBombAt(myTile) != null);
    }

    public bool IsAvailable
    {
        get
        {
            CacheReferences();
            return powerGlove != null &&
                   powerGlove.IsEnabled &&
                   movement != null &&
                   !movement.isDead &&
                   !IsMountedOnAnyMount() &&
                   !movement.IsRidingPlaying();
        }
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
    }

    private void OnDisable()
    {
        ReleaseSyntheticActionA();
        ResetSequence("component disabled");
    }

    private void CacheReferences()
    {
        if (identity == null)
            TryGetComponent(out identity);

        if (movement == null)
            TryGetComponent(out movement);

        if (bombController == null)
            TryGetComponent(out bombController);

        if (powerGlove == null)
            TryGetComponent(out powerGlove);

        if (mountCompanion == null)
            TryGetComponent(out mountCompanion);

        if (movement != null)
            tileSize = Mathf.Max(0.01f, movement.tileSize);

        gameManager = GameManager.Instance != null
            ? GameManager.Instance
            : FindAnyObjectByType<GameManager>();

        if (gameManager != null)
        {
            groundTilemap = gameManager.groundTilemap;
            destructibleTilemap = gameManager.destructibleTilemap;
            indestructibleTilemap = gameManager.indestructibleTilemap;
        }

        explosionMask = LayerMask.GetMask("Explosion");
    }

    private bool IsMountedOnAnyMount()
    {
        if (movement != null && movement.IsMounted)
            return true;

        return mountCompanion != null &&
               mountCompanion.GetMountedLouieType() != MountedType.None;
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
            ResetSequence("emergency unavailable");
            lastDecisionTrace = IsMountedOnAnyMount()
                ? "emergency power glove unavailable: mounted"
                : "emergency power glove unavailable";
            return false;
        }

        if (TryContinueSequence(settings, myTile, out decision))
            return true;

        if (TryAdoptHeldBomb(settings, myTile, out decision))
            return true;

        Bomb potentialBomb = FindPotentialPickupBombAt(myTile);
        if (potentialBomb == null)
            ResetBombChanceOpportunity();

        if (potentialBomb != null &&
            !IsAuthorizedPlantedBomb(potentialBomb, myTile) &&
            !RollBombOpportunityChance(settings, potentialBomb))
        {
            lastDecisionTrace =
                $"emergency chance failed chance:{GetUsageChance(settings):P0} bomb:{potentialBomb.GetEntityId()}";
            return false;
        }

        if (potentialBomb != null && !powerGlove.CanPickupBombNow(potentialBomb))
        {
            lastDecisionTrace =
                $"emergency skip pickup window age:{Time.time - potentialBomb.PlacedTime:F3}s";
            return false;
        }

        Bomb bomb = FindPickupBombAt(myTile);
        if (bomb == null)
        {
            lastDecisionTrace =
                $"emergency no bomb on current tile danger:{FormatDanger(currentDangerSeconds)}";
            return false;
        }

        if (!TryChooseThrowDirection(
                myTile,
                bomb,
                requireOffensiveTarget: false,
                out Vector2Int throwDirection,
                out Vector2Int landingTile,
                out _))
        {
            lastDecisionTrace = $"emergency no throw direction bomb:{myTile}";
            return false;
        }

        StartPickupSequence(settings, bomb, myTile, throwDirection, landingTile);
        decision = BuildPickupDecision(settings, "emergency pickup");
        lastDecisionTrace =
            $"emergency PICKUP bomb:{myTile} dir:{throwDirection} landing:{landingTile}";
        return true;
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
            ResetSequence("candidate unavailable");
            lastDecisionTrace = IsMountedOnAnyMount()
                ? "candidate power glove unavailable: mounted"
                : "candidate power glove unavailable";
            return false;
        }

        if (TryContinueSequence(settings, myTile, out decision))
            return true;

        if (TryAdoptHeldBomb(settings, myTile, out decision))
            return true;

        Bomb potentialBomb = FindPotentialPickupBombAt(myTile);
        if (potentialBomb == null)
            ResetBombChanceOpportunity();

        Bomb existingBomb = FindPickupBombAt(myTile);
        if (existingBomb != null)
        {
            bool pickupAuthorized =
                IsAuthorizedPlantedBomb(existingBomb, myTile) ||
                RollBombOpportunityChance(settings, existingBomb);
            if (!pickupAuthorized)
            {
                lastDecisionTrace =
                    $"candidate chance failed chance:{GetUsageChance(settings):P0} bomb:{existingBomb.GetEntityId()}";
                return false;
            }

            if (!TryChooseThrowDirection(
                myTile,
                existingBomb,
                requireOffensiveTarget: false,
                out Vector2Int existingDirection,
                out Vector2Int existingLanding,
                out _))
            {
                lastDecisionTrace = $"candidate no throw direction bomb:{myTile}";
                return false;
            }

            StartPickupSequence(
                settings,
                existingBomb,
                myTile,
                existingDirection,
                existingLanding);
            decision = BuildPickupDecision(settings, "pickup existing bomb");
            lastDecisionTrace =
                $"candidate PICKUP existing bomb:{myTile} dir:{existingDirection} landing:{existingLanding}";
            return true;
        }

        if (Time.time < nextOffensiveTime)
        {
            lastDecisionTrace = $"candidate cooldown {(nextOffensiveTime - Time.time):F2}s";
            return false;
        }

        if (bombController == null || bombController.BombsRemaining <= 0)
        {
            lastDecisionTrace = "candidate no bombs remaining";
            return false;
        }

        if (!IsCenteredOnTile(myTile))
        {
            lastDecisionTrace = "candidate not centered for glove plant";
            return false;
        }

        if (!float.IsInfinity(GetDangerSeconds(myTile, null)))
        {
            lastDecisionTrace = "candidate current tile already dangerous";
            return false;
        }

        if (!TryChooseThrowDirection(
                myTile,
                null,
                requireOffensiveTarget: true,
                out Vector2Int throwDirection,
                out Vector2Int landingTile,
                out int targetPlayerId))
        {
            lastDecisionTrace = "candidate no offensive throw target";
            return false;
        }

        bool shouldUsePowerGlove = RollOffensiveChance(settings);
        nextOffensiveTime =
            Time.time + GetOffensiveCooldownSeconds();
        if (!shouldUsePowerGlove)
        {
            lastDecisionTrace =
                $"candidate offensive chance failed chance:{GetUsageChance(settings):P0}";
            return false;
        }

        authorizedPlantTile = myTile;
        authorizedPlantPickupUntil = Time.time + PlannedPickupAuthorizationSeconds;

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.KickBomb,
            Weight =
                420 +
                DifficultyWeight(settings) +
                GetPowerGloveWeightBonus(),
            TargetTile = landingTile,
            HasTarget = true,
            FirstMove = Vector2.zero,
            Reason = $"plant for power glove throw toward P{targetPlayerId}",
            InputDescription = "ActionA",
            TapBomb = true
        };

        lastDecisionTrace =
            $"candidate PLAN plant:{myTile} dir:{throwDirection} landing:{landingTile} target:P{targetPlayerId}";
        return true;
    }

    private bool TryContinueSequence(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        if (sequenceState == SequenceState.None)
            return false;

        if (Time.time - sequenceStartedTime > SequenceTimeoutSeconds)
        {
            ResetSequence($"sequence timeout state:{sequenceState}");
            return false;
        }

        switch (sequenceState)
        {
            case SequenceState.PickingUp:
                return ContinuePickup(settings, myTile, out decision);

            case SequenceState.Carrying:
                return ContinueCarry(settings, myTile, out decision);

            case SequenceState.Releasing:
                return ContinueRelease(settings, out decision);

            default:
                return false;
        }
    }

    private bool ContinuePickup(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        if (!IsSequenceBombValid())
        {
            ResetSequence("pickup bomb invalid");
            return false;
        }

        if (powerGlove.IsHoldingBomb && sequenceBomb.IsBeingHeldByPowerGlove)
        {
            if (carryModePlanned)
            {
                carryStartedTime = Time.time;
                carryNoPathSince = -10f;
                sequenceStartedTime = Time.time;
                SetSequenceState(SequenceState.Carrying);
                lastDecisionTrace = $"sequence CARRY_START my:{myTile}";
                return ContinueCarry(settings, myTile, out decision);
            }

            movement.ForceFacingDirection(TileDirectionToVector(sequenceThrowDirection));
            SetSequenceState(SequenceState.Releasing);
            decision = BuildThrowTapDecision(settings);
            lastDecisionTrace =
                $"sequence SECOND_TAP dir:{sequenceThrowDirection} landing:{sequenceLandingTile}";
            return true;
        }

        if (sequenceBomb.IsBeingPunched)
        {
            ResetSequence("pickup tap already released bomb");
            return false;
        }

        float pickupElapsed = Time.time - sequenceStateStartedTime;
        float dangerHere = GetDangerSeconds(myTile, null);
        float oneStepEscapeSeconds =
            EstimateTraversalSeconds(1) +
            settings.dangerReactionSeconds +
            PickupAbortEscapeMarginSeconds;
        bool escapeWindowClosing =
            !float.IsInfinity(dangerHere) &&
            dangerHere <= oneStepEscapeSeconds;

        if (pickupElapsed > PickupConfirmationSeconds || escapeWindowClosing)
        {
            ResetSequence(
                $"pickup aborted bomb:{sequencePlantTile} my:{myTile} " +
                $"elapsed:{pickupElapsed:F2}s danger:{FormatDanger(dangerHere)} " +
                $"escapeNeed:{oneStepEscapeSeconds:F2}s");
            return false;
        }

        decision = BuildWaitForPickupDecision(settings);
        lastDecisionTrace =
            $"sequence WAIT_PICKUP bomb:{sequencePlantTile} my:{myTile}";
        return true;
    }

    private bool ContinueRelease(
        BattleModeComDifficultySettings settings,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        movement.ForceFacingDirection(TileDirectionToVector(sequenceThrowDirection));

        if (!IsSequenceBombValid())
        {
            ResetSequence("release bomb invalid");
            return false;
        }

        if (!powerGlove.IsHoldingBomb &&
            !sequenceBomb.IsBeingHeldByPowerGlove)
        {
            ResetSequence("throw complete");
            return false;
        }

        if (Time.time - sequenceStateStartedTime > ReleaseConfirmationSeconds)
        {
            ResetSequence("release timeout");
            return false;
        }

        decision = BuildWaitForReleaseDecision(settings);
        lastDecisionTrace = $"sequence RELEASE_WAIT dir:{sequenceThrowDirection}";
        return true;
    }

    private bool ContinueCarry(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        if (!IsSequenceBombValid())
        {
            ResetSequence("carry bomb invalid");
            return false;
        }

        if (!powerGlove.IsHoldingBomb || !sequenceBomb.IsBeingHeldByPowerGlove)
        {
            ResetSequence("carry lost held bomb");
            return false;
        }

        if (!FindNearestOpponentTile(myTile, out Vector2Int targetTile))
            return ForceCarryThrow(settings, myTile, "carry no opponent alive", out decision);

        if (Time.time - carryStartedTime > CarryMaxSeconds)
            return ForceCarryThrow(settings, myTile, "carry timeout", out decision);

        if (IsCenteredOnTile(myTile) &&
            TryGetThrowSpotDirection(
                myTile,
                targetTile,
                out Vector2Int throwDirection,
                out Vector2Int throwLanding))
        {
            return BeginCarryRelease(
                settings,
                myTile,
                throwDirection,
                throwLanding,
                $"carry throw at P-target tile:{targetTile}",
                out decision);
        }

        if (TryFindCarryStep(myTile, targetTile, out Vector2 stepMove, out Vector2Int stepTarget))
        {
            carryNoPathSince = -10f;
            decision = new BattleModeComAbilityDecision
            {
                Action = BattleModeComActionType.KickBomb,
                Weight = 540 + DifficultyWeight(settings),
                TargetTile = stepTarget,
                HasTarget = true,
                FirstMove = stepMove,
                Reason = $"carrying bomb toward throw spot {stepTarget}",
                InputDescription = AppendInput("HoldActionA", FirstMoveDescription(stepMove)),
                HoldActionA = true
            };
            lastDecisionTrace =
                $"sequence CARRY my:{myTile} target:{targetTile} step:{FirstMoveDescription(stepMove)} goal:{stepTarget}";
            return true;
        }

        if (carryNoPathSince < 0f)
            carryNoPathSince = Time.time;

        if (Time.time - carryNoPathSince > CarryNoPathGraceSeconds)
            return ForceCarryThrow(settings, myTile, "carry no path to throw spot", out decision);

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.KickBomb,
            Weight = 540 + DifficultyWeight(settings),
            TargetTile = myTile,
            HasTarget = true,
            FirstMove = Vector2.zero,
            Reason = "carrying bomb waiting for path",
            InputDescription = "HoldActionA",
            HoldActionA = true
        };
        lastDecisionTrace = $"sequence CARRY_WAIT my:{myTile} target:{targetTile}";
        return true;
    }

    private bool BeginCarryRelease(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        Vector2Int throwDirection,
        Vector2Int landingTile,
        string reason,
        out BattleModeComAbilityDecision decision)
    {
        sequencePlantTile = myTile;
        sequenceThrowDirection = throwDirection;
        sequenceLandingTile = landingTile;
        movement.ForceFacingDirection(TileDirectionToVector(throwDirection));
        SetSequenceState(SequenceState.Releasing);

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.KickBomb,
            Weight = 560 + DifficultyWeight(settings),
            TargetTile = landingTile,
            HasTarget = true,
            FirstMove = Vector2.zero,
            Reason = reason,
            InputDescription = "ReleaseActionA",
            HoldActionA = false
        };

        lastDecisionTrace =
            $"sequence CARRY_RELEASE dir:{throwDirection} landing:{landingTile} reason:{reason}";
        return true;
    }

    private bool ForceCarryThrow(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        string reason,
        out BattleModeComAbilityDecision decision)
    {
        if (!TryChooseThrowDirection(
                myTile,
                sequenceBomb,
                requireOffensiveTarget: false,
                out Vector2Int throwDirection,
                out Vector2Int landingTile,
                out _))
        {
            throwDirection = Vector2Int.down;
            landingTile = PredictLandingTile(myTile, throwDirection);
        }

        return BeginCarryRelease(settings, myTile, throwDirection, landingTile, reason, out decision);
    }

    private bool TryGetThrowSpotDirection(
        Vector2Int origin,
        Vector2Int targetTile,
        out Vector2Int bestDirection,
        out Vector2Int bestLanding)
    {
        bestDirection = Vector2Int.zero;
        bestLanding = origin;
        int bestScore = int.MinValue;
        int radius = GetBombRadius(sequenceBomb);

        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int direction = CardinalTiles[i];
            Vector2Int landing = PredictLandingTile(origin, direction);
            if (landing == origin)
                continue;

            int score;
            if (landing == targetTile)
                score = 100;
            else if (IsTileInBlastLine(landing, targetTile, radius) &&
                     !IsTileInBlastLine(landing, origin, radius))
                score = 50;
            else
                continue;

            if (score <= bestScore)
                continue;

            bestScore = score;
            bestDirection = direction;
            bestLanding = landing;
        }

        return bestDirection != Vector2Int.zero;
    }

    private bool FindNearestOpponentTile(Vector2Int myTile, out Vector2Int targetTile)
    {
        targetTile = myTile;
        int bestDistance = int.MaxValue;

        activePlayers.Clear();
        PlayerIdentity.GetActivePlayers(activePlayers);

        for (int i = 0; i < activePlayers.Count; i++)
        {
            PlayerIdentity player = activePlayers[i];
            if (player == null || player == identity)
                continue;

            if (!player.TryGetComponent(out MovementController targetMovement) ||
                targetMovement == null ||
                targetMovement.isDead)
            {
                continue;
            }

            Vector2Int tile = WorldToTile(player.transform.position);
            int distance = Manhattan(myTile, tile);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            targetTile = tile;
        }

        return bestDistance != int.MaxValue;
    }

    private bool TryFindCarryStep(
        Vector2Int start,
        Vector2Int targetTile,
        out Vector2 firstMove,
        out Vector2Int goal)
    {
        firstMove = Vector2.zero;
        goal = start;

        carrySearchParents.Clear();
        carrySearchDepth.Clear();
        carrySearchOpen.Clear();

        carrySearchParents[start] = start;
        carrySearchDepth[start] = 0;
        carrySearchOpen.Enqueue(start);

        Vector2Int bestApproach = start;
        int bestApproachDistance = Manhattan(start, targetTile);

        while (carrySearchOpen.Count > 0)
        {
            Vector2Int tile = carrySearchOpen.Dequeue();
            int depth = carrySearchDepth[tile];

            if (depth > 0 && TryGetThrowSpotDirection(tile, targetTile, out _, out _))
            {
                goal = tile;
                firstMove = TileDirectionToVector(ReconstructCarryFirstStep(start, tile));
                return firstMove != Vector2.zero;
            }

            int distance = Manhattan(tile, targetTile);
            if (depth > 0 && distance < bestApproachDistance)
            {
                bestApproachDistance = distance;
                bestApproach = tile;
            }

            if (depth >= CarrySearchMaxDepth)
                continue;

            for (int i = 0; i < CardinalTiles.Length; i++)
            {
                Vector2Int next = tile + CardinalTiles[i];
                if (carrySearchParents.ContainsKey(next))
                    continue;

                if (next == targetTile)
                    continue;

                if (!IsCarryWalkable(next))
                    continue;

                float danger = GetDangerSeconds(next, null);
                if (!float.IsInfinity(danger) &&
                    danger <= EstimateTraversalSeconds(depth + 1) + CarryStepDangerMarginSeconds)
                {
                    continue;
                }

                carrySearchParents[next] = tile;
                carrySearchDepth[next] = depth + 1;
                carrySearchOpen.Enqueue(next);
            }
        }

        if (bestApproach != start)
        {
            goal = bestApproach;
            firstMove = TileDirectionToVector(ReconstructCarryFirstStep(start, bestApproach));
            return firstMove != Vector2.zero;
        }

        return false;
    }

    private Vector2Int ReconstructCarryFirstStep(Vector2Int start, Vector2Int goal)
    {
        Vector2Int current = goal;
        while (carrySearchParents.TryGetValue(current, out Vector2Int parent) &&
               parent != start)
        {
            if (parent == current)
                break;

            current = parent;
        }

        return current - start;
    }

    private bool IsCarryWalkable(Vector2Int tile)
    {
        return HasGroundTile(tile) &&
               !HasIndestructibleTile(tile) &&
               !HasDestructibleTile(tile) &&
               FindBoardBombAt(tile) == null;
    }

    private bool TryAdoptHeldBomb(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        if (!powerGlove.IsHoldingBomb)
            return false;

        Bomb heldBomb = FindHeldBomb();
        if (heldBomb == null)
            return false;

        if (!TryChooseThrowDirection(
                myTile,
                heldBomb,
                requireOffensiveTarget: false,
                out Vector2Int throwDirection,
                out Vector2Int landingTile,
                out _))
        {
            return false;
        }

        sequenceBomb = heldBomb;
        sequencePlantTile = myTile;
        sequenceThrowDirection = throwDirection;
        sequenceLandingTile = landingTile;
        sequenceStartedTime = Time.time;
        movement.ForceFacingDirection(TileDirectionToVector(sequenceThrowDirection));
        SetSequenceState(SequenceState.Releasing);
        decision = BuildThrowTapDecision(settings);
        lastDecisionTrace =
            $"adopt held bomb SECOND_TAP dir:{throwDirection} landing:{landingTile}";
        return true;
    }

    private void StartPickupSequence(
        BattleModeComDifficultySettings settings,
        Bomb bomb,
        Vector2Int plantTile,
        Vector2Int throwDirection,
        Vector2Int landingTile)
    {
        carryModePlanned =
            FindNearestOpponentTile(plantTile, out _) &&
            Random.value <= GetCarryChance(settings);
        carryStartedTime = -10f;
        carryNoPathSince = -10f;
        sequenceBomb = bomb;
        sequencePlantTile = plantTile;
        sequenceThrowDirection = throwDirection;
        sequenceLandingTile = landingTile;
        sequenceStartedTime = Time.time;
        authorizedPlantPickupUntil = -10f;
        movement.ForceFacingDirection(TileDirectionToVector(sequenceThrowDirection));
        SetSequenceState(SequenceState.PickingUp);
    }

    private BattleModeComAbilityDecision BuildPickupDecision(
        BattleModeComDifficultySettings settings,
        string reason)
    {
        return new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.KickBomb,
            Weight = 520 + DifficultyWeight(settings),
            TargetTile = sequencePlantTile,
            HasTarget = true,
            FirstMove = Vector2.zero,
            Reason = reason,
            InputDescription = carryModePlanned ? "ActionA+Hold" : "ActionA",
            TapActionA = true,
            HoldActionA = carryModePlanned
        };
    }

    private BattleModeComAbilityDecision BuildWaitForPickupDecision(
        BattleModeComDifficultySettings settings)
    {
        return new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.KickBomb,
            Weight = 520 + DifficultyWeight(settings),
            TargetTile = sequencePlantTile,
            HasTarget = true,
            FirstMove = Vector2.zero,
            Reason = "waiting for power glove pickup",
            InputDescription = carryModePlanned ? "HoldActionA" : "none",
            HoldActionA = carryModePlanned
        };
    }

    private BattleModeComAbilityDecision BuildThrowTapDecision(
        BattleModeComDifficultySettings settings)
    {
        return new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.KickBomb,
            Weight = 560 + DifficultyWeight(settings),
            TargetTile = sequenceLandingTile,
            HasTarget = true,
            FirstMove = Vector2.zero,
            Reason = $"double tap power glove throw toward {sequenceLandingTile}",
            InputDescription = "ActionA",
            TapActionA = true
        };
    }

    private BattleModeComAbilityDecision BuildWaitForReleaseDecision(
        BattleModeComDifficultySettings settings)
    {
        return new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.KickBomb,
            Weight = 560 + DifficultyWeight(settings),
            TargetTile = sequenceLandingTile,
            HasTarget = true,
            FirstMove = Vector2.zero,
            Reason = $"waiting for power glove throw toward {sequenceLandingTile}",
            InputDescription = "none"
        };
    }

    private bool TryChooseThrowDirection(
        Vector2Int origin,
        Bomb bomb,
        bool requireOffensiveTarget,
        out Vector2Int bestDirection,
        out Vector2Int bestLandingTile,
        out int targetPlayerId)
    {
        bestDirection = Vector2Int.zero;
        bestLandingTile = origin;
        targetPlayerId = 0;
        int bestScore = int.MinValue;

        activePlayers.Clear();
        PlayerIdentity.GetActivePlayers(activePlayers);

        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int direction = CardinalTiles[i];
            Vector2Int landingTile = PredictLandingTile(origin, direction);
            if (landingTile == origin)
                continue;

            int directionScore = int.MinValue;
            int directionTargetId = 0;

            for (int playerIndex = 0; playerIndex < activePlayers.Count; playerIndex++)
            {
                PlayerIdentity player = activePlayers[playerIndex];
                if (player == null || player == identity)
                    continue;

                if (!player.TryGetComponent(out MovementController targetMovement) ||
                    targetMovement == null ||
                    targetMovement.isDead)
                {
                    continue;
                }

                Vector2Int targetTile = WorldToTile(player.transform.position);
                int radius = GetBombRadius(bomb);
                if (!IsTileInBlastLine(landingTile, targetTile, radius))
                    continue;

                int score =
                    1000 -
                    Manhattan(landingTile, targetTile) * 40 -
                    Manhattan(origin, targetTile) * 4 +
                    CountOpenNeighbors(landingTile) * 3;

                if (score <= directionScore)
                    continue;

                directionScore = score;
                directionTargetId = player.playerId;
            }

            if (directionScore == int.MinValue && !requireOffensiveTarget)
            {
                directionScore =
                    CountOpenNeighbors(landingTile) * 20 +
                    Manhattan(origin, landingTile) * 5;

                if (IsTileInBlastLine(landingTile, origin, GetBombRadius(bomb)))
                    directionScore -= 200;
            }

            if (directionScore <= bestScore)
                continue;

            bestScore = directionScore;
            bestDirection = direction;
            bestLandingTile = landingTile;
            targetPlayerId = directionTargetId;
        }

        return bestDirection != Vector2Int.zero &&
               (!requireOffensiveTarget || targetPlayerId != 0);
    }

    private Vector2Int PredictLandingTile(Vector2Int origin, Vector2Int direction)
    {
        int distance = powerGlove != null ? powerGlove.ThrowDistanceTiles : 3;
        Vector2Int landing = origin;

        for (int step = 1; step <= distance; step++)
        {
            Vector2Int next = origin + direction * step;
            if (!HasGroundTile(next) || HasIndestructibleTile(next))
                break;

            landing = next;
        }

        return landing;
    }

    private float GetDangerSeconds(Vector2Int tile, Bomb thrownBomb)
    {
        if (explosionMask != 0)
        {
            Collider2D explosion = Physics2D.OverlapCircle(
                TileToWorld(tile),
                tileSize * 0.25f,
                explosionMask);
            if (explosion != null)
                return 0f;
        }

        float danger = float.PositiveInfinity;
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null ||
                bomb.HasExploded ||
                bomb.IsBeingHeldByPowerGlove)
            {
                continue;
            }

            Vector2Int bombTile =
                bomb == thrownBomb && bomb.IsBeingPunched
                    ? sequenceLandingTile
                    : WorldToTile(bomb.GetLogicalPosition());
            int radius = GetBombRadius(bomb);
            if (!IsTileInBlastLine(bombTile, tile, radius))
                continue;

            float seconds = bomb.IsControlBomb ? 0.65f : bomb.RemainingFuseSeconds;
            danger = Mathf.Min(danger, seconds);
        }

        return danger;
    }

    private bool IsTileInBlastLine(Vector2Int origin, Vector2Int tile, int radius)
    {
        if (origin == tile)
            return true;

        Vector2Int delta = tile - origin;
        if (delta.x != 0 && delta.y != 0)
            return false;

        int distance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        if (distance > radius)
            return false;

        Vector2Int direction = new Vector2Int(
            Mathf.Clamp(delta.x, -1, 1),
            Mathf.Clamp(delta.y, -1, 1));

        for (int step = 1; step < distance; step++)
        {
            Vector2Int check = origin + direction * step;
            if (HasIndestructibleTile(check) ||
                HasDestructibleTile(check) ||
                FindBoardBombAt(check) != null)
            {
                return false;
            }
        }

        return true;
    }

    private Bomb FindPickupBombAt(Vector2Int tile)
    {
        Bomb bomb = FindPotentialPickupBombAt(tile);
        if (bomb == null ||
            powerGlove == null ||
            !powerGlove.CanPickupBombNow(bomb))
        {
            return null;
        }

        return bomb;
    }

    private Bomb FindPotentialPickupBombAt(Vector2Int tile)
    {
        Bomb bomb = FindBoardBombAt(tile);
        if (bomb == null ||
            bomb.IsBeingHeldByPowerGlove ||
            bomb.IsBeingKicked ||
            bomb.IsBeingPunched ||
            bomb.GetComponent<BoilerCapturedBomb>() != null)
        {
            return null;
        }

        return bomb;
    }

    private Bomb FindBoardBombAt(Vector2Int tile)
    {
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null ||
                bomb.HasExploded ||
                bomb.IsBeingHeldByPowerGlove)
            {
                continue;
            }

            if (WorldToTile(bomb.GetLogicalPosition()) == tile)
                return bomb;
        }

        return null;
    }

    private Bomb FindHeldBomb()
    {
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null ||
                bomb.HasExploded ||
                !bomb.IsBeingHeldByPowerGlove)
            {
                continue;
            }

            if (bomb.transform.IsChildOf(transform))
                return bomb;
        }

        return null;
    }

    private bool IsCenteredOnTile(Vector2Int tile)
    {
        Vector2 position = movement.Rigidbody != null
            ? movement.Rigidbody.position
            : (Vector2)transform.position;
        Vector2 center = TileToWorld(tile);
        float tolerance = tileSize * CenterToleranceTiles;
        return Mathf.Abs(position.x - center.x) <= tolerance &&
               Mathf.Abs(position.y - center.y) <= tolerance;
    }

    private int CountOpenNeighbors(Vector2Int tile)
    {
        int count = 0;
        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int next = tile + CardinalTiles[i];
            if (HasGroundTile(next) &&
                !HasIndestructibleTile(next) &&
                !HasDestructibleTile(next))
            {
                count++;
            }
        }

        return count;
    }

    private bool HasGroundTile(Vector2Int tile)
    {
        return groundTilemap == null ||
               groundTilemap.HasTile(groundTilemap.WorldToCell(TileToWorld(tile)));
    }

    private bool HasIndestructibleTile(Vector2Int tile)
    {
        return indestructibleTilemap != null &&
               indestructibleTilemap.HasTile(
                   indestructibleTilemap.WorldToCell(TileToWorld(tile)));
    }

    private bool HasDestructibleTile(Vector2Int tile)
    {
        return destructibleTilemap != null &&
               destructibleTilemap.HasTile(
                   destructibleTilemap.WorldToCell(TileToWorld(tile)));
    }

    private int GetBombRadius(Bomb bomb)
    {
        if (bomb != null && bomb.Owner != null)
            return Mathf.Max(1, bomb.Owner.GetPredictedBlastRadius(bomb));

        return bombController != null
            ? Mathf.Max(1, bombController.GetPlannedExplosionRadius())
            : 2;
    }

    private float EstimateTraversalSeconds(int depth)
    {
        float speed = movement != null ? Mathf.Max(1f, movement.speed) : 4f;
        return depth * tileSize / speed;
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

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static Vector2 TileDirectionToVector(Vector2Int direction)
    {
        return new Vector2(
            Mathf.Clamp(direction.x, -1, 1),
            Mathf.Clamp(direction.y, -1, 1));
    }

    private static int DifficultyWeight(BattleModeComDifficultySettings settings)
    {
        if (settings == null)
            return 0;

        switch (settings.difficulty)
        {
            case BattleModeComputerLevel.Hard:
                return 40;

            case BattleModeComputerLevel.Easy:
                return -20;

            default:
                return 0;
        }
    }

    private bool RollOffensiveChance(BattleModeComDifficultySettings settings)
    {
        if (offensiveChanceFrame == Time.frameCount)
            return offensiveChanceResult;

        float chance = GetUsageChance(settings);

        offensiveChanceFrame = Time.frameCount;
        offensiveChanceResult = Random.value <= chance;
        return offensiveChanceResult;
    }

    private bool RollBombOpportunityChance(
        BattleModeComDifficultySettings settings,
        Bomb bomb)
    {
        if (bomb == null)
            return false;

        string opportunityId = bomb.GetEntityId().ToString();
        if (bombChanceOpportunityId == opportunityId &&
            bombChanceDifficulty == settings.difficulty)
        {
            return bombChanceResult;
        }

        bombChanceOpportunityId = opportunityId;
        bombChanceDifficulty = settings.difficulty;
        bombChanceResult = Random.value <= GetUsageChance(settings);
        return bombChanceResult;
    }

    private bool IsAuthorizedPlantedBomb(Bomb bomb, Vector2Int myTile)
    {
        return bomb != null &&
               bomb.Owner == bombController &&
               myTile == authorizedPlantTile &&
               Time.time <= authorizedPlantPickupUntil;
    }

    private void ResetBombChanceOpportunity()
    {
        bombChanceOpportunityId = string.Empty;
        bombChanceResult = false;
    }

    private float GetUsageChance(BattleModeComDifficultySettings settings)
    {
        float chance = settings.difficulty switch
        {
            BattleModeComputerLevel.Easy => 0.10f,
            BattleModeComputerLevel.Hard => 0.50f,
            _ => 0.25f
        };
        return Mathf.Clamp01(
            chance *
            GetPowerGloveChanceMultiplier());
    }

    private float GetCarryChance(BattleModeComDifficultySettings settings)
    {
        float chance = settings.difficulty switch
        {
            BattleModeComputerLevel.Easy => 0.25f,
            BattleModeComputerLevel.Hard => 0.55f,
            _ => 0.40f
        };
        return Mathf.Clamp01(
            chance *
            GetPowerGloveChanceMultiplier());
    }

    private float GetOffensiveCooldownSeconds()
    {
        BattleModeComStage10PowerZoneAggressionAbility stage10 =
            GetStageAggression();
        if (stage10 != null)
            return Mathf.Max(0.1f, stage10.PowerGloveCooldownSeconds);

        BattleModeComStage11ImpulseRopeAbility stage11 =
            GetStage11Aggression();
        return Mathf.Max(
            0.1f,
            stage11 != null
                ? stage11.PowerGloveCooldownSeconds
                : OffensiveCooldownSeconds);
    }

    private float GetPowerGloveChanceMultiplier()
    {
        BattleModeComStage10PowerZoneAggressionAbility stage10 =
            GetStageAggression();
        if (stage10 != null)
            return stage10.PowerGloveChanceMultiplier;

        BattleModeComStage11ImpulseRopeAbility stage11 =
            GetStage11Aggression();
        return stage11 != null
            ? stage11.PowerGloveChanceMultiplier
            : 1f;
    }

    private int GetPowerGloveWeightBonus()
    {
        BattleModeComStage10PowerZoneAggressionAbility stage10 =
            GetStageAggression();
        if (stage10 != null)
            return stage10.PowerGloveWeightBonus;

        BattleModeComStage11ImpulseRopeAbility stage11 =
            GetStage11Aggression();
        return stage11 != null ? stage11.PowerGloveWeightBonus : 0;
    }

    private BattleModeComStage10PowerZoneAggressionAbility
        GetStageAggression()
        => TryGetComponent(
            out BattleModeComStage10PowerZoneAggressionAbility aggression)
            ? aggression
            : null;

    private BattleModeComStage11ImpulseRopeAbility
        GetStage11Aggression()
        => TryGetComponent(
            out BattleModeComStage11ImpulseRopeAbility aggression)
            ? aggression
            : null;

    private static string AppendInput(string existing, string input)
    {
        return string.IsNullOrEmpty(existing) ? input : existing + "+" + input;
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

        return $"{seconds:F2}s";
    }

    private bool IsSequenceBombValid()
    {
        return sequenceBomb != null && !sequenceBomb.HasExploded;
    }

    private void SetSequenceState(SequenceState state)
    {
        sequenceState = state;
        sequenceStateStartedTime = Time.time;
    }

    private void ResetSequence(string reason)
    {
        ReleaseSyntheticActionA();
        sequenceState = SequenceState.None;
        sequenceStartedTime = -10f;
        sequenceStateStartedTime = -10f;
        sequenceBomb = null;
        sequencePlantTile = default;
        sequenceThrowDirection = default;
        sequenceLandingTile = default;
        carryModePlanned = false;
        carryStartedTime = -10f;
        carryNoPathSince = -10f;

        if (!string.IsNullOrEmpty(reason))
            lastDecisionTrace = $"reset {reason}";
    }

    private void ReleaseSyntheticActionA()
    {
        if (movement == null)
            return;

        PlayerInputManager input = PlayerInputManager.Instance;
        if (input != null)
            input.SetSyntheticHeld(movement.PlayerId, PlayerAction.ActionA, false);
    }
}
