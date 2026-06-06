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
    private static readonly bool EnableYellowLouieKickSurgicalDiagnostics = false;

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
    private float pendingKickSentTime = -10f;
    private float lastDefensiveLogTime = -10f;
    private string lastDefensiveLogKey = string.Empty;
    private readonly Queue<Vector2Int> escapeOpen = new();
    private readonly Dictionary<Vector2Int, EscapeNode> escapeVisited = new();

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
                "YELLOW_DEF_REJECT",
                $"my:{myTile} danger:{FormatDanger(currentDangerSeconds)} rejected:{(string.IsNullOrEmpty(rejected) ? "empty" : rejected)} candidates:{(string.IsNullOrEmpty(candidates) ? "none" : candidates)} nearby:{DescribeAdjacentBombs(myTile)}",
                force: true);
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
                "YELLOW_DEF_INPUT_FAIL",
                $"my:{myTile} dir:{DirectionLabel(bestDir)} bomb:{DescribeYellowBomb(bestBomb)}",
                force: true);
            return false;
        }

        decision.TargetTile = bestEscapeTile;
        decision.HasTarget = true;
        decision.UsesEscapeAbilityChance = true;
        lastDecisionTrace =
            $"yellow defensive kick selected dir:{bestDir} bomb:{myTile + bestDir} escape:{bestEscapeTile} fuse:{bestFuse:F2} score:{bestScore}";
        LogYellowDefensive(
            "YELLOW_DEF_ACCEPT",
            $"my:{myTile} dir:{DirectionLabel(bestDir)} bomb:{DescribeYellowBomb(bestBomb)} escape:{bestEscapeTile} fuse:{bestFuse:F2} score:{bestScore} input:{decision.InputDescription} candidates:{(string.IsNullOrEmpty(candidates) ? "none" : candidates)} rejected:{(string.IsNullOrEmpty(rejected) ? "none" : rejected)}",
            force: true);
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
            ClearPendingKickCommand();

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
        return true;
    }

    protected override void OnOffensiveSequenceReset()
    {
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
        pendingKickSentTime = -10f;
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
        while (escapeOpen.Count > 0)
        {
            Vector2Int tile = escapeOpen.Dequeue();
            EscapeNode node = escapeVisited[tile];

            if (node.Depth > 0 &&
                IsFinalSafeTile(tile, settings, ignoredBomb, node.Depth))
            {
                target = tile;
                routeDepth = node.Depth;
                return true;
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

        return false;
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
        if (!EnableYellowLouieKickSurgicalDiagnostics)
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
        Debug.Log($"[BattleCOMYellowKick][P{(Movement != null ? Movement.PlayerId : 0)}] tile:{tile} {key} {message}", this);
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
