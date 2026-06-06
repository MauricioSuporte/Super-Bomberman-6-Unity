using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BattleRevengeController))]
public sealed class BattleRevengeComController : MonoBehaviour
{
    private static readonly bool EnableSurgicalLogs = true;

    private const float ThinkIntervalSeconds = 0.12f;
    private const float ChargeStepSeconds = 0.12f;
    private const float ReleasePaddingSeconds = 0.04f;
    private const float DirectHitTolerance = 0.55f;
    private const float NearHitTolerance = 1.1f;
    private const float MinimumNearShotChance = 0.62f;
    private const float MaximumNearShotChance = 0.88f;
    private const float NormalDifficultyNextShotDelaySeconds = 5f;
    private const float EasyDifficultyNextShotDelaySeconds = 10f;
    private const float SameWallTargetAxisTolerance = 0.25f;

    private const int MinLaunchDistanceTiles = 3;
    private const int MaxLaunchDistanceTiles = 7;

    private static readonly PlayerAction[] MovementActions =
    {
        PlayerAction.MoveUp,
        PlayerAction.MoveDown,
        PlayerAction.MoveLeft,
        PlayerAction.MoveRight
    };

    private readonly List<PlayerIdentity> activePlayers = new(6);

    private BattleRevengeController cart;
    private float nextThinkTime;
    private float roamWallSwitchAt;
    private int roamWallIndex;
    private int roamWallStep = 1;
    private int sameWallFallbackStep = 1;
    private float thinkIntervalJitter;
    private float roamIntervalSeconds = 1.35f;
    private float scoreNoiseMagnitude;
    private float releaseJitterSeconds;
    private float nearShotChance = 0.75f;
    private bool holdingLaunch;
    private float launchHoldStartedAt;
    private int desiredLaunchDistance = MinLaunchDistanceTiles;
    private float nextAiLaunchAllowedAt;
    private float lastSurgicalLogTime = -10f;
    private string lastSurgicalLogKey = string.Empty;

    public void Initialize(BattleRevengeController ownerCart)
    {
        cart = ownerCart != null ? ownerCart : GetComponent<BattleRevengeController>();
        ResetInputState();
    }

    private void Awake()
    {
        cart = GetComponent<BattleRevengeController>();
    }

    private void OnEnable()
    {
        ResetInputState();
    }

    private void OnDisable()
    {
        ClearSyntheticInputs();
        holdingLaunch = false;
    }

    private void Update()
    {
        if (cart == null)
            cart = GetComponent<BattleRevengeController>();

        if (!CanControl())
        {
            SLog("CONTROL_DISABLED", $"holding:{holdingLaunch}", force: holdingLaunch);
            ClearSyntheticInputs();
            holdingLaunch = false;
            return;
        }

        if (Time.unscaledTime < nextThinkTime)
            return;

        nextThinkTime = Time.unscaledTime + GetNextThinkInterval();
        Think();
    }

    private bool CanControl()
    {
        if (cart == null || BattleRevengeSystem.Instance == null)
            return false;

        int ownerId = cart.OwnerPlayerId;
        if (!GameSession.IsValidPlayerId(ownerId))
            return false;

        if (!BattleRevengeSystem.Instance.IsRuntimeEnabled || GamePauseController.IsPaused)
            return false;

        return SaveSystem.GetBattleModePlayerControlMode(ownerId) == BattleModePlayerControlMode.Com;
    }

    private void Think()
    {
        ClearMovementInputs();

        if (TryFindLaunchPlan(out LaunchPlan launchPlan))
        {
            SLog("PLAN_SELECTED", FormatPlan(launchPlan), force: launchPlan.HasViableShot);
            DriveTowardLaunchPlan(launchPlan);
            return;
        }

        PlayerAction roamAction = GetRoamAction();
        SLog("ROAM", $"targets:0 action:{roamAction}");
        StopCharging();
        SetMovement(roamAction);
    }

    private void DriveTowardLaunchPlan(LaunchPlan plan)
    {
        if (plan.HasViableShot)
        {
            if (!CanStartOrContinueLaunch())
            {
                float remaining = Mathf.Max(0f, nextAiLaunchAllowedAt - Time.unscaledTime);

                StopCharging();

                PlayerAction roamAction = GetRoamAction();
                SLog(
                    "SHOT_WAITING_RECHARGE",
                    $"{FormatPlan(plan)} remaining:{remaining:F2} action:{roamAction}");

                SetMovement(roamAction);
                return;
            }

            ClearMovementInputs();
            SLog("SHOT_VIABLE", FormatPlan(plan), force: true);
            HandleLaunchCharge(plan.DistanceTiles);
            return;
        }

        StopCharging();

        PlayerAction patrolAction = GetPatrolAction(plan);
        SLog("PATROL_NO_SHOT", $"{FormatPlan(plan)} action:{patrolAction}");
        SetMovement(patrolAction);
    }

    private bool CanStartOrContinueLaunch()
    {
        if (holdingLaunch)
            return true;

        return Time.unscaledTime >= nextAiLaunchAllowedAt;
    }

    private bool TryFindLaunchPlan(out LaunchPlan bestPlan)
    {
        bestPlan = default;
        bool foundTarget = false;
        float bestScore = float.NegativeInfinity;

        activePlayers.Clear();
        PlayerIdentity.GetActivePlayers(activePlayers);

        for (int i = 0; i < activePlayers.Count; i++)
        {
            PlayerIdentity target = activePlayers[i];
            if (!IsValidTarget(target))
                continue;

            foundTarget = true;
            int wins = GameSession.Instance != null ? GameSession.Instance.GetBattleMatchWins(target.playerId) : 0;
            Vector2 targetPosition = target.transform.position;

            for (int distance = MinLaunchDistanceTiles; distance <= MaxLaunchDistanceTiles; distance++)
            {
                if (!BattleRevengeSystem.Instance.TryGetPredictedLandingPosition(cart, distance, out Vector2 landing))
                    continue;

                float landingError = Vector2.Distance(landing, targetPosition);
                float score = wins * 100f - landingError * 12f;

                if (landingError <= DirectHitTolerance)
                    score += 80f;
                else if (landingError <= NearHitTolerance)
                    score += 30f;

                score += UnityEngine.Random.Range(-scoreNoiseMagnitude, scoreNoiseMagnitude);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestPlan = new LaunchPlan
                    {
                        Target = target,
                        TargetWins = wins,
                        DistanceTiles = distance,
                        LandingError = landingError,
                        HasViableShot = ShouldTakeShot(landingError),
                        DesiredWall = ResolveDesiredWallForTarget(targetPosition)
                    };
                }
            }

            float approachScore = wins * 100f - Vector2.Distance(cart.transform.position, targetPosition);
            approachScore += UnityEngine.Random.Range(-scoreNoiseMagnitude, scoreNoiseMagnitude);

            if (approachScore > bestScore)
            {
                bestScore = approachScore;
                bestPlan = new LaunchPlan
                {
                    Target = target,
                    TargetWins = wins,
                    DistanceTiles = MinLaunchDistanceTiles,
                    LandingError = float.PositiveInfinity,
                    HasViableShot = false,
                    DesiredWall = ResolveDesiredWallForTarget(targetPosition)
                };
            }
        }

        bool foundPlan = foundTarget && bestPlan.Target != null;
        if (!foundPlan)
            SLog("NO_TARGET", $"activePlayers:{activePlayers.Count}");

        return foundPlan;
    }

    private bool ShouldTakeShot(float landingError)
    {
        if (landingError <= DirectHitTolerance)
            return true;

        return landingError <= NearHitTolerance &&
               UnityEngine.Random.value <= nearShotChance;
    }

    private bool IsValidTarget(PlayerIdentity target)
    {
        if (target == null || target.playerId == cart.OwnerPlayerId)
            return false;

        if (IsAlly(target.playerId))
            return false;

        if (!target.TryGetComponent<MovementController>(out var movement) ||
            movement == null ||
            movement.isDead ||
            movement.IsEndingStage ||
            !movement.gameObject.activeInHierarchy)
        {
            return false;
        }

        return true;
    }

    private bool IsAlly(int otherPlayerId)
    {
        if (BattleModeRules.Instance == null || !BattleModeRules.Instance.UsesTeams)
            return false;

        return BattleModeRules.Instance.GetTeamForPlayer(cart.OwnerPlayerId) ==
               BattleModeRules.Instance.GetTeamForPlayer(otherPlayerId);
    }

    private void HandleLaunchCharge(int distanceTiles)
    {
        PlayerInputManager input = PlayerInputManager.Instance;
        if (input == null)
        {
            SLog("CHARGE_ABORT", "missing PlayerInputManager", force: true);
            return;
        }

        desiredLaunchDistance = Mathf.Clamp(distanceTiles, MinLaunchDistanceTiles, MaxLaunchDistanceTiles);

        if (!holdingLaunch)
        {
            desiredLaunchDistance = Mathf.Clamp(
                desiredLaunchDistance + UnityEngine.Random.Range(-1, 2),
                MinLaunchDistanceTiles,
                MaxLaunchDistanceTiles);

            holdingLaunch = true;
            launchHoldStartedAt = Time.unscaledTime;
            input.SetSyntheticHeld(cart.OwnerPlayerId, PlayerAction.ActionA, true);

            SLog(
                "CHARGE_START",
                $"distance:{desiredLaunchDistance} holdStart:{launchHoldStartedAt:F2} nextAllowed:{nextAiLaunchAllowedAt:F2}",
                force: true);

            return;
        }

        int chargeSteps = Mathf.Max(0, desiredLaunchDistance - MinLaunchDistanceTiles);
        float requiredHoldSeconds = chargeSteps * ChargeStepSeconds + ReleasePaddingSeconds + releaseJitterSeconds;

        float heldSeconds = Time.unscaledTime - launchHoldStartedAt;
        if (heldSeconds >= requiredHoldSeconds)
        {
            SLog(
                "CHARGE_RELEASE",
                $"distance:{desiredLaunchDistance} held:{heldSeconds:F2} required:{requiredHoldSeconds:F2}",
                force: true);

            StopChargingAndStartNextShotDelay();
        }
        else
        {
            SLog(
                "CHARGE_HOLD",
                $"distance:{desiredLaunchDistance} held:{heldSeconds:F2} required:{requiredHoldSeconds:F2}");

            input.SetSyntheticHeld(cart.OwnerPlayerId, PlayerAction.ActionA, true);
        }
    }

    private void StopCharging()
    {
        if (!holdingLaunch)
            return;

        PlayerInputManager input = PlayerInputManager.Instance;
        if (input != null)
            input.SetSyntheticHeld(cart.OwnerPlayerId, PlayerAction.ActionA, false);

        SLog("CHARGE_STOP", $"distance:{desiredLaunchDistance}", force: true);
        holdingLaunch = false;
    }

    private void StopChargingAndStartNextShotDelay()
    {
        if (!holdingLaunch)
            return;

        PlayerInputManager input = PlayerInputManager.Instance;
        if (input != null)
            input.SetSyntheticHeld(cart.OwnerPlayerId, PlayerAction.ActionA, false);

        float delaySeconds = GetNextShotDelaySeconds();
        nextAiLaunchAllowedAt = Time.unscaledTime + delaySeconds;

        SLog(
            "CHARGE_STOP_AND_RECHARGE",
            $"distance:{desiredLaunchDistance} delay:{delaySeconds:F2} nextAllowed:{nextAiLaunchAllowedAt:F2}",
            force: true);

        holdingLaunch = false;
    }

    private float GetNextShotDelaySeconds()
    {
        float cartCooldownSeconds = BattleRevengeSystem.Instance != null
            ? BattleRevengeSystem.Instance.CartBombCooldownSeconds
            : 2.5f;

        BattleModeComputerLevel computerLevel = SaveSystem.GetBattleModeComputerLevel();

        switch (computerLevel)
        {
            case BattleModeComputerLevel.Easy:
                return Mathf.Max(cartCooldownSeconds, EasyDifficultyNextShotDelaySeconds);

            case BattleModeComputerLevel.Hard:
                return cartCooldownSeconds;

            case BattleModeComputerLevel.Normal:
            default:
                return Mathf.Max(cartCooldownSeconds, NormalDifficultyNextShotDelaySeconds);
        }
    }

    private PlayerAction GetPatrolAction(LaunchPlan plan)
    {
        RevengeTargetWall currentWall = ResolveCurrentWallFromCart();

        if (currentWall != plan.DesiredWall)
            return GetActionTowardWall(plan.DesiredWall);

        if (plan.Target == null || cart == null)
            return GetRoamAction();

        Vector2 targetPosition = plan.Target.transform.position;
        Vector2 cartPosition = cart.transform.position;

        switch (currentWall)
        {
            case RevengeTargetWall.Left:
            case RevengeTargetWall.Right:
                return GetVerticalPatrolAction(cartPosition.y, targetPosition.y);

            case RevengeTargetWall.Top:
            case RevengeTargetWall.Bottom:
                return GetHorizontalPatrolAction(cartPosition.x, targetPosition.x);

            default:
                return GetRoamAction();
        }
    }

    private PlayerAction GetVerticalPatrolAction(float cartY, float targetY)
    {
        float deltaY = targetY - cartY;

        if (Mathf.Abs(deltaY) > SameWallTargetAxisTolerance)
            return deltaY > 0f ? PlayerAction.MoveUp : PlayerAction.MoveDown;

        sameWallFallbackStep *= -1;
        return sameWallFallbackStep > 0 ? PlayerAction.MoveUp : PlayerAction.MoveDown;
    }

    private PlayerAction GetHorizontalPatrolAction(float cartX, float targetX)
    {
        float deltaX = targetX - cartX;

        if (Mathf.Abs(deltaX) > SameWallTargetAxisTolerance)
            return deltaX > 0f ? PlayerAction.MoveRight : PlayerAction.MoveLeft;

        sameWallFallbackStep *= -1;
        return sameWallFallbackStep > 0 ? PlayerAction.MoveRight : PlayerAction.MoveLeft;
    }

    private PlayerAction GetActionTowardWall(RevengeTargetWall wall)
    {
        switch (wall)
        {
            case RevengeTargetWall.Left:
                return PlayerAction.MoveLeft;

            case RevengeTargetWall.Right:
                return PlayerAction.MoveRight;

            case RevengeTargetWall.Top:
                return PlayerAction.MoveUp;

            case RevengeTargetWall.Bottom:
                return PlayerAction.MoveDown;

            default:
                return GetRoamAction();
        }
    }

    private RevengeTargetWall ResolveCurrentWallFromCart()
    {
        if (cart == null)
            return RevengeTargetWall.Left;

        Vector2 launchDirection = cart.LaunchDirection;

        if (Mathf.Abs(launchDirection.x) >= Mathf.Abs(launchDirection.y))
            return launchDirection.x > 0f ? RevengeTargetWall.Left : RevengeTargetWall.Right;

        return launchDirection.y > 0f ? RevengeTargetWall.Bottom : RevengeTargetWall.Top;
    }

    private PlayerAction GetRoamAction()
    {
        if (Time.unscaledTime >= roamWallSwitchAt)
        {
            roamWallSwitchAt = Time.unscaledTime + roamIntervalSeconds + UnityEngine.Random.Range(-0.18f, 0.22f);
            roamWallIndex = (roamWallIndex + roamWallStep + 4) % 4;
        }

        return roamWallIndex switch
        {
            0 => PlayerAction.MoveUp,
            1 => PlayerAction.MoveRight,
            2 => PlayerAction.MoveDown,
            _ => PlayerAction.MoveLeft
        };
    }

    private RevengeTargetWall ResolveDesiredWallForTarget(Vector2 targetPosition)
    {
        Vector2 delta = targetPosition - (Vector2)cart.transform.position;

        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            return delta.x >= 0f ? RevengeTargetWall.Left : RevengeTargetWall.Right;

        return delta.y >= 0f ? RevengeTargetWall.Bottom : RevengeTargetWall.Top;
    }

    private float GetNextThinkInterval()
    {
        return Mathf.Max(0.04f, ThinkIntervalSeconds + UnityEngine.Random.Range(-thinkIntervalJitter, thinkIntervalJitter));
    }

    private void SetMovement(PlayerAction action)
    {
        PlayerInputManager input = PlayerInputManager.Instance;
        if (input == null)
            return;

        int ownerId = cart.OwnerPlayerId;
        for (int i = 0; i < MovementActions.Length; i++)
            input.SetSyntheticHeld(ownerId, MovementActions[i], MovementActions[i] == action);
    }

    private void ClearMovementInputs()
    {
        PlayerInputManager input = PlayerInputManager.Instance;
        if (input == null || cart == null)
            return;

        for (int i = 0; i < MovementActions.Length; i++)
            input.SetSyntheticHeld(cart.OwnerPlayerId, MovementActions[i], false);
    }

    private void ClearSyntheticInputs()
    {
        PlayerInputManager input = PlayerInputManager.Instance;
        if (input != null && cart != null)
            input.ClearSyntheticPlayer(cart.OwnerPlayerId);
    }

    private void ResetInputState()
    {
        holdingLaunch = false;
        desiredLaunchDistance = MinLaunchDistanceTiles;
        nextAiLaunchAllowedAt = 0f;
        sameWallFallbackStep = UnityEngine.Random.value < 0.5f ? -1 : 1;

        RandomizePersonality();

        nextThinkTime = Time.unscaledTime + UnityEngine.Random.Range(0f, ThinkIntervalSeconds + thinkIntervalJitter);
        roamWallSwitchAt = Time.unscaledTime + UnityEngine.Random.Range(0f, roamIntervalSeconds);

        ClearSyntheticInputs();

        SLog(
            "RESET",
            $"roamIndex:{roamWallIndex} roamStep:{roamWallStep} sameWallStep:{sameWallFallbackStep} " +
            $"thinkJitter:{thinkIntervalJitter:F3} roamInterval:{roamIntervalSeconds:F2} " +
            $"noise:{scoreNoiseMagnitude:F1} releaseJitter:{releaseJitterSeconds:F3} " +
            $"nearChance:{nearShotChance:F2} nextAllowed:{nextAiLaunchAllowedAt:F2}",
            force: true);
    }

    private void RandomizePersonality()
    {
        roamWallIndex = UnityEngine.Random.Range(0, 4);
        roamWallStep = UnityEngine.Random.value < 0.5f ? -1 : 1;
        thinkIntervalJitter = UnityEngine.Random.Range(0.015f, 0.055f);
        roamIntervalSeconds = UnityEngine.Random.Range(0.95f, 1.75f);
        scoreNoiseMagnitude = UnityEngine.Random.Range(3f, 13f);
        releaseJitterSeconds = UnityEngine.Random.Range(-0.02f, 0.055f);
        nearShotChance = UnityEngine.Random.Range(MinimumNearShotChance, MaximumNearShotChance);
    }

    private enum RevengeTargetWall
    {
        Left,
        Right,
        Top,
        Bottom
    }

    private struct LaunchPlan
    {
        public PlayerIdentity Target;
        public int TargetWins;
        public int DistanceTiles;
        public float LandingError;
        public bool HasViableShot;
        public RevengeTargetWall DesiredWall;
    }

    private string FormatPlan(LaunchPlan plan)
    {
        int targetId = plan.Target != null ? plan.Target.playerId : 0;
        Vector2 targetPos = plan.Target != null ? (Vector2)plan.Target.transform.position : Vector2.zero;
        RevengeTargetWall currentWall = cart != null ? ResolveCurrentWallFromCart() : RevengeTargetWall.Left;

        return
            $"target:P{targetId}@{targetPos} wins:{plan.TargetWins} distance:{plan.DistanceTiles} " +
            $"error:{plan.LandingError:F2} viable:{plan.HasViableShot} wall:{plan.DesiredWall} " +
            $"currentWall:{currentWall} cartPos:{(cart != null ? cart.transform.position.ToString() : "null")}";
    }

    private void SLog(string key, string message, bool force = false)
    {
        if (!EnableSurgicalLogs)
            return;

        string logKey = key + ":" + message;
        if (!force && logKey == lastSurgicalLogKey && Time.unscaledTime - lastSurgicalLogTime < 0.35f)
            return;

        lastSurgicalLogKey = logKey;
        lastSurgicalLogTime = Time.unscaledTime;

        int ownerId = cart != null ? cart.OwnerPlayerId : 0;
        Debug.Log($"[BattleRevengeCOM][P{ownerId}] t:{Time.unscaledTime:F2} {key} {message}", this);
    }
}