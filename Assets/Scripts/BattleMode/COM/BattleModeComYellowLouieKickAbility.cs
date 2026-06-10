using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(BombController))]
public sealed class BattleModeComYellowLouieKickAbility : BattleModeComKickBombAbility
{
    private const float KickCommandConfirmationWaitSeconds = 0.35f;
    private const float DefensiveKickMinFuseSeconds = 0.55f;
    private const float DefensiveLogIntervalSeconds = 0.35f;
    private const float FailedKickRetryBlockSeconds = 1.0f;

    private static readonly Vector2Int[] CardinalTiles =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    private YellowLouieKickAbility yellowKickAbility;
    private PlayerMountCompanion mountCompanion;
    private Bomb pendingKickBomb;
    private Vector2Int pendingKickOriginTile;
    private Vector2Int pendingKickDirection;
    private Vector2Int pendingKickEscapeTile;
    private bool hasPendingKickEscape;
    private float pendingKickSentTime = -10f;
    private Bomb failedKickBomb;
    private Vector2Int failedKickOriginTile;
    private Vector2Int failedKickDirection;
    private float failedKickRetryBlockedUntil = -10f;
    private float lastDefensiveLogTime = -10f;
    private string lastDefensiveLogKey = string.Empty;
    private readonly Queue<Vector2Int> escapeOpen = new();
    private readonly Dictionary<Vector2Int, EscapeNode> escapeVisited = new();

    [Header("Debug")]
    [SerializeField] private bool debugYellowKickTrace;

    private struct EscapeNode
    {
        public Vector2Int Parent;
        public int Depth;
    }

    public override string DiagnosticName =>
        IsMountedYellowLouieKickAvailable ? "YellowLouieKick" : base.DiagnosticName;

    public bool IsMountedYellowLouieKickAvailable
    {
        get
        {
            CacheReferences();
            return yellowKickAbility != null &&
                   yellowKickAbility.IsEnabled &&
                   mountCompanion != null &&
                   mountCompanion.GetMountedLouieType() == MountedType.Yellow &&
                   Movement != null &&
                   !Movement.isDead;
        }
    }

    public override bool IsAvailable
    {
        get
        {
            CacheReferences();

            if (IsMountedYellowLouieKickAvailable)
                return true;

            return !HasSeparateBaseKickComAbility() && base.IsAvailable;
        }
    }

    protected override bool CanUseActionRStop => !IsMountedYellowLouieKickAvailable;

    protected override bool IsKickAbilityEnabled =>
        IsMountedYellowLouieKickAvailable || base.IsKickAbilityEnabled;

    protected override void CacheExtraReferences()
    {
        if (yellowKickAbility == null)
            TryGetComponent(out yellowKickAbility);

        if (mountCompanion == null)
            TryGetComponent(out mountCompanion);
    }

    protected override string BuildAbilityAvailabilityTrace()
    {
        if (IsMountedYellowLouieKickAvailable)
            return $"yellowKick:{(yellowKickAbility != null)} yellowEnabled:{(yellowKickAbility != null && yellowKickAbility.IsEnabled)} mounted:Yellow";

        return base.BuildAbilityAvailabilityTrace() +
               $" yellowKick:{(yellowKickAbility != null)} yellowEnabled:{(yellowKickAbility != null && yellowKickAbility.IsEnabled)} mounted:{GetMountedTypeLabel()}";
    }

    protected override void NotifySequenceBombPlanted(Bomb bomb, Vector2 retreatDirection)
    {
        if (IsMountedYellowLouieKickAvailable)
        {
            Movement?.NotifyBombPlanted(bomb, retreatDirection);
            yellowKickAbility?.NotifyBombPlanted(bomb, retreatDirection);
            return;
        }

        base.NotifySequenceBombPlanted(bomb, retreatDirection);
    }

    protected override bool TryBuildCustomEmergencyDecision(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        float currentDangerSeconds,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        if (!IsMountedYellowLouieKickAvailable)
            return false;

        if (TryBuildPendingKickEscapeDecision(settings, myTile, out decision))
            return true;

        Bomb bestBomb = null;
        Vector2Int bestDir = Vector2Int.zero;
        Vector2Int bestEscapeTile = myTile;
        float bestFuse = float.PositiveInfinity;
        int bestScore = int.MaxValue;
        string rejected = string.Empty;
        string candidates = string.Empty;

        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int dir = CardinalTiles[i];
            Vector2Int bombTile = myTile + dir;
            Bomb bomb = FindBombAt(bombTile);

            if (!CanYellowKickBomb(bomb))
            {
                AppendTracePart(ref rejected, $"{DirectionLabel(dir)}:{bombTile}:no-yellow-kick:{DescribeYellowBomb(bomb)}");
                continue;
            }

            float fuse = bomb.IsControlBomb ? 0.65f : bomb.RemainingFuseSeconds;
            if (fuse < DefensiveKickMinFuseSeconds)
            {
                AppendTracePart(ref rejected, $"{DirectionLabel(dir)}:{bombTile}:fuse-low:{fuse:F2}<min:{DefensiveKickMinFuseSeconds:F2}");
                continue;
            }

            if (IsRecentlyFailedKick(bomb, bombTile, dir, out float retrySecondsLeft))
            {
                AppendTracePart(ref rejected, $"{DirectionLabel(dir)}:{bombTile}:retry-blocked:{retrySecondsLeft:F2}s:{DescribeYellowBomb(bomb)}");
                LogYellowDefensive(
                    "defensive-reject-retry-blocked",
                    $"my:{myTile} dir:{DirectionLabel(dir)} bombTile:{bombTile} retryLeft:{retrySecondsLeft:F2}s bomb:{DescribeYellowBomb(bomb)}");
                continue;
            }

            Vector2Int bombDestinationTile = bombTile + dir;
            float destinationDangerSeconds = GetDangerSeconds(bombDestinationTile, bomb);
            if (destinationDangerSeconds <= settings.dangerReactionSeconds)
            {
                AppendTracePart(
                    ref rejected,
                    $"{DirectionLabel(dir)}:{bombTile}:dest-danger:{bombDestinationTile}:{FormatDanger(destinationDangerSeconds)}:{DescribeYellowBomb(bomb)}");
                LogYellowDefensive(
                    "defensive-reject-destination-danger",
                    $"my:{myTile} dir:{DirectionLabel(dir)} bombTile:{bombTile} destination:{bombDestinationTile} destinationDanger:{FormatDanger(destinationDangerSeconds)} bomb:{DescribeYellowBomb(bomb)}");
                continue;
            }

            if (IsKickCommandAwaitingConfirmation(bomb, bombTile, dir, out float pendingElapsed))
            {
                AppendTracePart(ref rejected, $"{DirectionLabel(dir)}:{bombTile}:pending-wait:{pendingElapsed:F2}/{KickCommandConfirmationWaitSeconds:F2}:{DescribeYellowBomb(bomb)}");
                continue;
            }

            if (!TryFindEscapeOpenedByKickingBomb(settings, myTile, dir, bomb, out Vector2Int escapeTile, out string escapeReason))
            {
                AppendTracePart(ref rejected, $"{DirectionLabel(dir)}:{bombTile}:no-open-escape:{escapeReason}:{DescribeYellowBomb(bomb)}");
                continue;
            }

            float escapeDangerSeconds = GetDangerSeconds(escapeTile, bomb);
            int score = Mathf.RoundToInt((2f - Mathf.Min(fuse, 2f)) * 100f);
            if (float.IsInfinity(escapeDangerSeconds))
                score -= 60;

            if (escapeTile != myTile)
                score -= 20;

            AppendTracePart(
                ref candidates,
                $"{DirectionLabel(dir)}:{bombTile}:score:{score}:fuse:{fuse:F2}:escape:{escapeTile}:escapeDanger:{FormatDanger(escapeDangerSeconds)}:{escapeReason}");

            if (score >= bestScore)
                continue;

            bestScore = score;
            bestBomb = bomb;
            bestDir = dir;
            bestEscapeTile = escapeTile;
            bestFuse = fuse;
        }

        if (bestBomb == null)
        {
            lastDecisionTrace =
                $"yellow defensive kick none danger:{FormatDanger(currentDangerSeconds)} rejected:{(string.IsNullOrEmpty(rejected) ? "empty" : rejected)} candidates:{(string.IsNullOrEmpty(candidates) ? "none" : candidates)}";
            LogYellowDefensive(
                "defensive-reject",
                $"my:{myTile} danger:{FormatDanger(currentDangerSeconds)} nearby:{DescribeAdjacentBombs(myTile)} rejected:{(string.IsNullOrEmpty(rejected) ? "empty" : rejected)} candidates:{(string.IsNullOrEmpty(candidates) ? "none" : candidates)}");
            return false;
        }

        bool built = TryBuildKickActivationInputDecision(
            settings,
            myTile,
            bestBomb,
            myTile + bestDir,
            bestDir,
            360 + DifficultyWeight(settings),
            $"yellow defensive kick open escape {bestEscapeTile}",
            out decision);

        if (!built)
        {
            lastDecisionTrace = $"yellow defensive kick failed input dir:{bestDir} bomb:{DescribeYellowBomb(bestBomb)}";
            LogYellowDefensive(
                "defensive-input-fail",
                $"my:{myTile} dir:{DirectionLabel(bestDir)} bomb:{DescribeYellowBomb(bestBomb)}");
            return false;
        }

        decision.TargetTile = bestEscapeTile;
        decision.HasTarget = true;
        decision.UsesEscapeAbilityChance = true;
        pendingKickEscapeTile = bestEscapeTile;
        hasPendingKickEscape = true;
        lastDecisionTrace =
            $"yellow defensive kick selected dir:{bestDir} bomb:{myTile + bestDir} escape:{bestEscapeTile} fuse:{bestFuse:F2} score:{bestScore}";
        LogYellowDefensive(
            "defensive-select",
            $"my:{myTile} dir:{DirectionLabel(bestDir)} bomb:{DescribeYellowBomb(bestBomb)} escape:{bestEscapeTile} escapeDanger:{FormatDanger(GetDangerSeconds(bestEscapeTile, bestBomb))} fuse:{bestFuse:F2} score:{bestScore} input:{decision.InputDescription}");
        return true;
    }

    protected override bool IsKickActivationComplete(Bomb bomb, Vector2Int originalBombTile)
    {
        if (base.IsKickActivationComplete(bomb, originalBombTile))
            return true;

        Bomb trackedBomb = bomb != null ? bomb : pendingKickBomb;
        if (trackedBomb == null || trackedBomb.HasExploded)
            return false;

        if (pendingKickBomb != null && trackedBomb != pendingKickBomb)
            return false;

        bool movedFromOrigin = WorldToTile(trackedBomb.GetLogicalPosition()) != originalBombTile;
        if (movedFromOrigin)
        {
            LogYellowDefensive(
                "kick-confirmed",
                $"origin:{originalBombTile} now:{WorldToTile(trackedBomb.GetLogicalPosition())} pendingDir:{pendingKickDirection} bomb:{DescribeYellowBomb(trackedBomb)} elapsed:{Time.time - pendingKickSentTime:F2}",
                force: true);
            ClearPendingKickCommand();
        }
        else if (pendingKickBomb == trackedBomb)
        {
            LogYellowDefensive(
                "kick-not-confirmed-yet",
                $"origin:{originalBombTile} logical:{WorldToTile(trackedBomb.GetLogicalPosition())} pendingDir:{pendingKickDirection} bomb:{DescribeYellowBomb(trackedBomb)} elapsed:{Time.time - pendingKickSentTime:F2}");
        }

        return movedFromOrigin;
    }

    protected override bool TryBuildKickActivationInputDecision(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        Bomb bomb,
        Vector2Int bombTile,
        Vector2Int kickDirection,
        int weight,
        string reason,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        if (!IsMountedYellowLouieKickAvailable)
            return false;

        if (bomb == null || bomb.HasExploded || bomb.IsBeingKicked || bomb.IsBeingPunched)
        {
            lastDecisionTrace = $"yellow kick unavailable bomb:{(bomb != null)}";
            return false;
        }

        if (IsKickCommandAwaitingConfirmation(bomb, bombTile, kickDirection, out float pendingElapsed))
        {
            Vector2 waitMove = TileDirectionToVector(kickDirection);
            decision = new BattleModeComAbilityDecision
            {
                Action = BattleModeComActionType.KickBomb,
                Weight = Mathf.Max(1, weight - 10),
                TargetTile = bombTile,
                HasTarget = true,
                FirstMove = waitMove,
                Reason = "wait yellow louie kick",
                InputDescription = FirstMoveDescription(waitMove)
            };
            lastDecisionTrace =
                $"yellow kick wait bomb:{bombTile} dir:{kickDirection} wait:{pendingElapsed:F2}/{KickCommandConfirmationWaitSeconds:F2}";
            LogYellowDefensive(
                "kick-wait-confirmation",
                $"my:{myTile} bombTile:{bombTile} dir:{DirectionLabel(kickDirection)} elapsed:{pendingElapsed:F2} bomb:{DescribeYellowBomb(bomb)}");
            return true;
        }

        Vector2 kickMove = TileDirectionToVector(kickDirection);
        if (Movement != null)
            Movement.ForceFacingDirection(kickMove);

        pendingKickBomb = bomb;
        pendingKickOriginTile = bombTile;
        pendingKickDirection = kickDirection;
        pendingKickSentTime = Time.time;

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.KickBomb,
            Weight = weight,
            TargetTile = bombTile + kickDirection,
            HasTarget = true,
            FirstMove = kickMove,
            Reason = reason,
            InputDescription = AppendInput(FirstMoveDescription(kickMove), "ActionC"),
            TapActionC = true
        };

        lastDecisionTrace = $"yellow kick ActionC bomb:{bombTile} dir:{kickDirection}";
        LogYellowDefensive(
            "kick-command",
            $"my:{myTile} bombTile:{bombTile} target:{decision.TargetTile} dir:{DirectionLabel(kickDirection)} input:{decision.InputDescription} bomb:{DescribeYellowBomb(bomb)}");
        return true;
    }

    protected override void OnOffensiveSequenceReset()
    {
        ClearPendingKickCommand();
    }

    public void CancelUnselectedPendingKickCommand(string reason)
    {
        if (!hasPendingKickEscape && pendingKickBomb == null)
            return;

        LogYellowDefensive(
            "pending-cancel-unselected",
            $"reason:{reason} origin:{pendingKickOriginTile} escape:{pendingKickEscapeTile} bomb:{DescribeYellowBomb(pendingKickBomb)}",
            force: true);
        ClearPendingKickCommand();
    }

    private bool HasSeparateBaseKickComAbility()
    {
        var abilities = GetComponents<BattleModeComKickBombAbility>();
        for (int i = 0; i < abilities.Length; i++)
        {
            BattleModeComKickBombAbility ability = abilities[i];
            if (ability != null && ability != this && ability.GetType() == typeof(BattleModeComKickBombAbility))
                return true;
        }

        return false;
    }

    private string GetMountedTypeLabel()
    {
        if (mountCompanion == null)
            return "none";

        return mountCompanion.GetMountedLouieType().ToString();
    }

    private void ClearPendingKickCommand()
    {
        pendingKickBomb = null;
        pendingKickOriginTile = default;
        pendingKickDirection = default;
        pendingKickEscapeTile = default;
        hasPendingKickEscape = false;
        pendingKickSentTime = -10f;
    }

    private bool TryBuildPendingKickEscapeDecision(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        if (!hasPendingKickEscape || pendingKickBomb == null || pendingKickBomb.HasExploded)
        {
            if (hasPendingKickEscape || pendingKickBomb != null)
                ClearPendingKickCommand();

            return false;
        }

        float elapsed = Time.time - pendingKickSentTime;
        bool bombMoved = WorldToTile(pendingKickBomb.GetLogicalPosition()) != pendingKickOriginTile;

        if (myTile == pendingKickEscapeTile)
        {
            LogYellowDefensive(
                "pending-escape-arrived",
                $"my:{myTile} escape:{pendingKickEscapeTile} bombMoved:{bombMoved} elapsed:{elapsed:F2} bomb:{DescribeYellowBomb(pendingKickBomb)}",
                force: true);
            ClearPendingKickCommand();
            return false;
        }

        if (!bombMoved && elapsed > KickCommandConfirmationWaitSeconds)
        {
            LogYellowDefensive(
                "pending-escape-cancel-not-moved",
                $"my:{myTile} origin:{pendingKickOriginTile} escape:{pendingKickEscapeTile} elapsed:{elapsed:F2} bomb:{DescribeYellowBomb(pendingKickBomb)}",
                force: true);
            RememberFailedKick(pendingKickBomb, pendingKickOriginTile, pendingKickDirection);
            ClearPendingKickCommand();
            return false;
        }

        if (!TryFindPendingEscapeStep(
                settings,
                myTile,
                pendingKickEscapeTile,
                pendingKickBomb,
                out Vector2Int step,
                out int routeDepth))
        {
            LogYellowDefensive(
                "pending-escape-no-route",
                $"my:{myTile} escape:{pendingKickEscapeTile} bombMoved:{bombMoved} elapsed:{elapsed:F2} bomb:{DescribeYellowBomb(pendingKickBomb)}",
                force: true);
            ClearPendingKickCommand();
            return false;
        }

        Vector2Int next = myTile + step;
        if (!IsOpenAfterKick(next, myTile, pendingKickBomb))
        {
            LogYellowDefensive(
                "pending-escape-blocked",
                $"my:{myTile} next:{next} escape:{pendingKickEscapeTile} bombMoved:{bombMoved} elapsed:{elapsed:F2} bomb:{DescribeYellowBomb(pendingKickBomb)}",
                force: true);
            ClearPendingKickCommand();
            return false;
        }

        float nextDanger = GetDangerSeconds(next, pendingKickBomb);
        float arrival = 1f / Mathf.Max(1f, Movement != null ? Movement.speed : 4f);
        if (nextDanger <= arrival + settings.dangerReactionSeconds)
        {
            LogYellowDefensive(
                "pending-escape-unsafe",
                $"my:{myTile} next:{next} escape:{pendingKickEscapeTile} nextDanger:{FormatDanger(nextDanger)} arrival:{arrival:F2} bombMoved:{bombMoved} bomb:{DescribeYellowBomb(pendingKickBomb)}",
                force: true);
            ClearPendingKickCommand();
            return false;
        }

        Vector2 move = TileDirectionToVector(step);
        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.Reposition,
            Weight = 390 + DifficultyWeight(settings),
            TargetTile = pendingKickEscapeTile,
            HasTarget = true,
            FirstMove = move,
            Reason = "escape after yellow louie kick",
            InputDescription = FirstMoveDescription(move)
        };

        lastDecisionTrace =
            $"yellow pending escape target:{pendingKickEscapeTile} step:{step} depth:{routeDepth} bombMoved:{bombMoved} elapsed:{elapsed:F2}";
        LogYellowDefensive(
            "pending-escape",
            $"my:{myTile} next:{next} escape:{pendingKickEscapeTile} move:{FirstMoveDescription(move)} depth:{routeDepth} bombMoved:{bombMoved} elapsed:{elapsed:F2} nextDanger:{FormatDanger(nextDanger)} bomb:{DescribeYellowBomb(pendingKickBomb)}");
        return true;
    }

    private bool IsRecentlyFailedKick(
        Bomb bomb,
        Vector2Int bombTile,
        Vector2Int direction,
        out float retrySecondsLeft)
    {
        retrySecondsLeft = failedKickRetryBlockedUntil - Time.time;
        if (retrySecondsLeft <= 0f)
            return false;

        return failedKickBomb == bomb &&
               failedKickOriginTile == bombTile &&
               failedKickDirection == direction;
    }

    private void RememberFailedKick(Bomb bomb, Vector2Int bombTile, Vector2Int direction)
    {
        if (bomb == null)
            return;

        failedKickBomb = bomb;
        failedKickOriginTile = bombTile;
        failedKickDirection = direction;
        failedKickRetryBlockedUntil = Time.time + FailedKickRetryBlockSeconds;
    }

    private bool IsKickCommandAwaitingConfirmation(
        Bomb bomb,
        Vector2Int bombTile,
        Vector2Int kickDirection,
        out float elapsedSeconds)
    {
        elapsedSeconds = Time.time - pendingKickSentTime;
        return pendingKickBomb == bomb &&
               pendingKickOriginTile == bombTile &&
               pendingKickDirection == kickDirection &&
               elapsedSeconds < KickCommandConfirmationWaitSeconds;
    }

    private bool TryFindEscapeOpenedByKickingBomb(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        Vector2Int kickDir,
        Bomb ignoredBomb,
        out Vector2Int escapeTile,
        out string reason)
    {
        escapeTile = myTile;
        reason = "none";

        Vector2Int vacatedBombTile = myTile + kickDir;
        if (TryFindSafeRouteThroughFirstStep(
                settings,
                myTile,
                kickDir,
                ignoredBomb,
                out escapeTile,
                out int vacatedRouteDepth))
        {
            reason = $"vacated-route-depth-{vacatedRouteDepth}";
            return true;
        }

        if (IsSafeIgnoringBomb(myTile, settings, ignoredBomb, 0))
        {
            escapeTile = myTile;
            reason = "current-safe-after-kick";
            return true;
        }

        Vector2Int back = myTile - kickDir;
        if (TryFindSafeRouteThroughFirstStep(
                settings,
                myTile,
                -kickDir,
                ignoredBomb,
                out escapeTile,
                out int backRouteDepth))
        {
            reason = $"back-route-depth-{backRouteDepth}";
            return true;
        }

        Vector2Int perpA = new(-kickDir.y, kickDir.x);
        Vector2Int perpB = new(kickDir.y, -kickDir.x);
        if (TryFindSafeRouteThroughFirstStep(
                settings,
                myTile,
                perpA,
                ignoredBomb,
                out escapeTile,
                out int perpARouteDepth))
        {
            reason = $"perpA-route-depth-{perpARouteDepth}";
            return true;
        }

        if (TryFindSafeRouteThroughFirstStep(
                settings,
                myTile,
                perpB,
                ignoredBomb,
                out escapeTile,
                out int perpBRouteDepth))
        {
            reason = $"perpB-route-depth-{perpBRouteDepth}";
            return true;
        }

        reason =
            $"danger current:{FormatDanger(GetDangerSeconds(myTile, ignoredBomb))} " +
            $"vacated:{FormatDanger(GetDangerSeconds(vacatedBombTile, ignoredBomb))} " +
            $"back:{FormatDanger(GetDangerSeconds(back, ignoredBomb))}";
        return false;
    }

    private bool TryFindSafeRouteThroughFirstStep(
        BattleModeComDifficultySettings settings,
        Vector2Int start,
        Vector2Int firstStep,
        Bomb ignoredBomb,
        out Vector2Int target,
        out int routeDepth)
    {
        target = start;
        routeDepth = -1;

        if (firstStep == Vector2Int.zero)
            return false;

        Vector2Int firstTile = start + firstStep;
        if (!IsOpenAfterKick(firstTile, start, ignoredBomb))
            return false;

        if (!IsSafeEnoughAtDepth(firstTile, settings, ignoredBomb, 1))
            return false;

        escapeOpen.Clear();
        escapeVisited.Clear();

        escapeVisited[start] = new EscapeNode { Parent = start, Depth = 0 };
        escapeVisited[firstTile] = new EscapeNode { Parent = start, Depth = 1 };
        escapeOpen.Enqueue(firstTile);

        int maxDepth = Mathf.Max(3, settings.searchDepth + 4);
        Vector2Int fallbackTarget = start;
        int fallbackDepth = -1;
        float fallbackDanger = float.NegativeInfinity;

        while (escapeOpen.Count > 0)
        {
            Vector2Int tile = escapeOpen.Dequeue();
            EscapeNode node = escapeVisited[tile];

            if (node.Depth > 0 &&
                IsCompletelySafeTile(tile, ignoredBomb))
            {
                target = tile;
                routeDepth = node.Depth;
                return true;
            }

            if (node.Depth > 1 &&
                IsFinalSafeTile(tile, settings, ignoredBomb, node.Depth))
            {
                float danger = GetDangerSeconds(tile, ignoredBomb);
                if (fallbackDepth < 0 || danger > fallbackDanger)
                {
                    fallbackTarget = tile;
                    fallbackDepth = node.Depth;
                    fallbackDanger = danger;
                }
            }

            if (node.Depth >= maxDepth)
                continue;

            for (int i = 0; i < CardinalTiles.Length; i++)
            {
                Vector2Int next = tile + CardinalTiles[i];
                if (escapeVisited.ContainsKey(next))
                    continue;

                if (!IsOpenAfterKick(next, tile, ignoredBomb))
                    continue;

                int nextDepth = node.Depth + 1;
                if (!IsSafeEnoughAtDepth(next, settings, ignoredBomb, nextDepth))
                    continue;

                escapeVisited[next] = new EscapeNode { Parent = tile, Depth = nextDepth };
                escapeOpen.Enqueue(next);
            }
        }

        if (fallbackDepth > 0)
        {
            target = fallbackTarget;
            routeDepth = fallbackDepth;
            return true;
        }

        return false;
    }

    private bool TryFindPendingEscapeStep(
        BattleModeComDifficultySettings settings,
        Vector2Int start,
        Vector2Int target,
        Bomb ignoredBomb,
        out Vector2Int step,
        out int routeDepth)
    {
        step = Vector2Int.zero;
        routeDepth = -1;

        if (start == target)
            return false;

        escapeOpen.Clear();
        escapeVisited.Clear();

        escapeVisited[start] = new EscapeNode { Parent = start, Depth = 0 };
        escapeOpen.Enqueue(start);

        int maxDepth = Mathf.Max(3, settings.searchDepth + 4);
        while (escapeOpen.Count > 0)
        {
            Vector2Int tile = escapeOpen.Dequeue();
            EscapeNode node = escapeVisited[tile];
            if (node.Depth >= maxDepth)
                continue;

            for (int i = 0; i < CardinalTiles.Length; i++)
            {
                Vector2Int next = tile + CardinalTiles[i];
                if (escapeVisited.ContainsKey(next))
                    continue;

                if (!IsOpenAfterKick(next, tile, ignoredBomb))
                    continue;

                int nextDepth = node.Depth + 1;
                if (!IsSafeEnoughAtDepth(next, settings, ignoredBomb, nextDepth))
                    continue;

                escapeVisited[next] = new EscapeNode { Parent = tile, Depth = nextDepth };
                if (next == target)
                {
                    step = ReconstructPendingEscapeStep(start, target);
                    routeDepth = nextDepth;
                    return step != Vector2Int.zero;
                }

                escapeOpen.Enqueue(next);
            }
        }

        return false;
    }

    private Vector2Int ReconstructPendingEscapeStep(Vector2Int start, Vector2Int target)
    {
        Vector2Int cursor = target;
        while (escapeVisited.TryGetValue(cursor, out EscapeNode node) &&
               node.Parent != start &&
               node.Parent != cursor)
        {
            cursor = node.Parent;
        }

        return cursor - start;
    }

    private bool IsOpenAfterKick(Vector2Int tile, Vector2Int startTile, Bomb ignoredBomb)
    {
        if (!HasGroundTile(tile) || HasIndestructibleTile(tile) || HasDestructibleTile(tile))
            return false;

        Bomb bomb = FindBombAt(tile);
        if (bomb != null && bomb != ignoredBomb)
            return false;

        return tile == startTile || IsWalkableTile(tile, startTile) || bomb == ignoredBomb;
    }

    private bool IsSafeIgnoringBomb(
        Vector2Int tile,
        BattleModeComDifficultySettings settings,
        Bomb ignoredBomb,
        int depth)
    {
        float dangerSeconds = GetDangerSeconds(tile, ignoredBomb);
        if (float.IsInfinity(dangerSeconds))
            return true;

        float arrivalSeconds = depth / Mathf.Max(1f, Movement != null ? Movement.speed : 4f);
        return dangerSeconds > arrivalSeconds + settings.dangerReactionSeconds;
    }

    private bool IsSafeEnoughAtDepth(
        Vector2Int tile,
        BattleModeComDifficultySettings settings,
        Bomb ignoredBomb,
        int depth)
    {
        float dangerSeconds = GetDangerSeconds(tile, ignoredBomb);
        if (float.IsInfinity(dangerSeconds))
            return true;

        float arrivalSeconds = depth / Mathf.Max(1f, Movement != null ? Movement.speed : 4f);
        return dangerSeconds > arrivalSeconds + settings.dangerReactionSeconds;
    }

    private bool IsFinalSafeTile(
        Vector2Int tile,
        BattleModeComDifficultySettings settings,
        Bomb ignoredBomb,
        int depth)
    {
        float dangerSeconds = GetDangerSeconds(tile, ignoredBomb);
        if (float.IsInfinity(dangerSeconds))
            return true;

        float arrivalSeconds = depth / Mathf.Max(1f, Movement != null ? Movement.speed : 4f);
        return dangerSeconds > arrivalSeconds + settings.safeTileMinimumSeconds + settings.dangerReactionSeconds;
    }

    private bool IsCompletelySafeTile(Vector2Int tile, Bomb ignoredBomb)
    {
        return float.IsInfinity(GetDangerSeconds(tile, ignoredBomb));
    }

    private static bool CanYellowKickBomb(Bomb bomb)
    {
        return bomb != null &&
               !bomb.HasExploded &&
               !bomb.IsBeingKicked &&
               !bomb.IsBeingPunched &&
               (bomb.CanBeKicked || bomb.CanBeKickedEarly);
    }

    private string DescribeAdjacentBombs(Vector2Int myTile)
    {
        string result = string.Empty;
        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int dir = CardinalTiles[i];
            AppendTracePart(ref result, $"{DirectionLabel(dir)}:{DescribeYellowBomb(FindBombAt(myTile + dir))}");
        }

        return string.IsNullOrEmpty(result) ? "none" : result;
    }

    private string DescribeYellowBomb(Bomb bomb)
    {
        if (bomb == null)
            return "null";

        return
            $"{WorldToTile(bomb.GetLogicalPosition())}/solid:{bomb.IsSolid}/can:{bomb.CanBeKicked}/early:{bomb.CanBeKickedEarly}/moving:{bomb.IsBeingKicked}/punched:{bomb.IsBeingPunched}/fuse:{FormatDanger(bomb.IsControlBomb ? 0.65f : bomb.RemainingFuseSeconds)}";
    }

    private void LogYellowDefensive(string key, string message, bool force = false)
    {
        if (!debugYellowKickTrace)
            return;

        string logKey = key + ":" + message;
        if (!force &&
            logKey == lastDefensiveLogKey &&
            Time.time - lastDefensiveLogTime < DefensiveLogIntervalSeconds)
        {
            return;
        }

        lastDefensiveLogKey = logKey;
        lastDefensiveLogTime = Time.time;
        Vector2Int tile = Movement != null ? WorldToTile(Movement.transform.position) : Vector2Int.zero;
        Debug.Log($"[BattleCOMYellowKickTrace][P{(Movement != null ? Movement.PlayerId : 0)}] t:{Time.time:F3} tile:{tile} {key} {message}", this);
    }

    private static string DirectionLabel(Vector2Int dir)
    {
        if (dir == Vector2Int.up) return "U";
        if (dir == Vector2Int.down) return "D";
        if (dir == Vector2Int.left) return "L";
        if (dir == Vector2Int.right) return "R";
        return dir.ToString();
    }

    private static string FormatDanger(float seconds)
    {
        if (float.IsInfinity(seconds)) return "safe";
        if (seconds <= 0f) return "now";
        return $"{seconds:F2}";
    }

    private static void AppendTracePart(ref string trace, string part)
    {
        if (string.IsNullOrEmpty(trace))
            trace = part;
        else
            trace += "; " + part;
    }
}
