using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(BombController))]
public sealed class BattleModeComController : MonoBehaviour
{
    private static bool EnableGeneralDecisionLogs => false;

    private const float BombTapCooldownSeconds = 0.35f;
    private const float ControlBombTapCooldownSeconds = 0.35f;
    private const float PunchTapCooldownSeconds = 0.15f;
    private const float SafetyHoldLogIntervalSeconds = 0.5f;
    private const float SafeTileCenterTolerance = 0.08f;
    private const float TurnAxisCenterTolerance = 0.045f;
    private const float DangerTimingMarginSeconds = 0.08f;
    private const float SuddenDeathUnsafeLeadSeconds = 1f;
    private const int ItemPriorityDistance = 7;
    private const int NearbyItemFarmBlockDistance = 5;
    private const int MaxDecisionBufferEntries = 32;
    private const float PersonalityWeightJitter = 0.18f;
    private const float ItemTargetJitter = 0.75f;
    private const float FarmTargetJitter = 0.5f;
    private const float ChainBombFuseSafetyMarginSeconds = 0.25f;
    private const int ChainBombMinimumEscapeBranches = 3;

    private static readonly Vector2Int[] CardinalTiles =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    private static readonly PlayerAction[] MovementActions =
    {
        PlayerAction.MoveUp,
        PlayerAction.MoveDown,
        PlayerAction.MoveLeft,
        PlayerAction.MoveRight
    };

    [Header("Diagnostics")]
    [SerializeField] private BattleModeComDiagnostics.LogLevel logLevel = BattleModeComDiagnostics.DefaultLogLevel;
    [SerializeField, Range(4, MaxDecisionBufferEntries)] private int decisionBufferSize = 16;

    private readonly List<string> recentDecisions = new();
    private readonly List<PlayerIdentity> activePlayers = new(6);
    private readonly List<CandidateAction> candidates = new(8);
    private readonly List<string> rejectedActions = new(16);
    private readonly List<IBattleModeComAbility> comAbilities = new(4);
    private readonly Dictionary<Vector2Int, PathNode> visited = new(128);
    private readonly Queue<Vector2Int> open = new(128);
    private readonly Collider2D[] obstacleHits = new Collider2D[16];
    private readonly List<Vector2Int> reachableTiles = new(128);

    private PlayerIdentity identity;
    private MovementController movement;
    private BombController bombController;
    private Collider2D[] ownColliders;
    private ContactFilter2D obstacleFilter;
    private GameManager gameManager;
    private Tilemap groundTilemap;
    private Tilemap destructibleTilemap;
    private Tilemap indestructibleTilemap;
    private BattleSuddenDeathController suddenDeathController;
    private int explosionMask;
    private int playerId = 1;
    private float tileSize = 1f;
    private float nextDecisionTime;
    private float lastBombTapTime = -10f;
    private float lastControlTapTime = -10f;
    private float lastPunchTapTime = -10f;
    private BattleModeComActionType currentAction = BattleModeComActionType.Stopped;
    private Vector2 currentMoveInput;
    private Vector2Int currentTargetTile;
    private bool hasCurrentTarget;
    private string currentReason = "startup";
    private string currentInputDescription = "none";
    private float lastSafetyHoldLogTime = -10f;
    private Vector2Int safeCenterTargetTile;
    private bool hasSafeCenterTarget;
    private bool currentMoveFollowsEscapeRoute;
    private bool initialized;
    private int personalitySeed;
    private int decisionSerial;
    private int abilitySystemVersion = -1;
    private int lastKickLoadDiagnosticFrame = -9999;

    private struct PathNode
    {
        public Vector2Int Tile;
        public Vector2Int Parent;
        public int Depth;
    }

    private struct CandidateAction
    {
        public BattleModeComActionType Action;
        public int Weight;
        public Vector2Int TargetTile;
        public bool HasTarget;
        public Vector2 FirstMove;
        public bool HasRoute;
        public string Reason;
        public string InputDescription;
        public bool TapBomb;
        public bool TapActionR;
        public bool TapActionC;
    }

    public IReadOnlyList<string> RecentDecisionLog => recentDecisions;

    public void Initialize(int id)
    {
        playerId = Mathf.Clamp(id, GameSession.MinPlayerId, GameSession.MaxPlayerId);
        initialized = true;
        CacheReferences();
        RefreshRuntimeEnabledState();
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();

        if (!initialized && identity != null)
            playerId = Mathf.Clamp(identity.playerId, GameSession.MinPlayerId, GameSession.MaxPlayerId);

        RefreshRuntimeEnabledState();
    }

    private void Start()
    {
        CacheReferences();
        RefreshRuntimeEnabledState();
    }

    private void OnDisable()
    {
        ClearSyntheticInputs();
    }

    private void CacheReferences()
    {
        if (identity == null)
            TryGetComponent(out identity);

        if (movement == null)
            TryGetComponent(out movement);

        if (bombController == null)
            TryGetComponent(out bombController);

        if (identity != null)
            playerId = Mathf.Clamp(identity.playerId, GameSession.MinPlayerId, GameSession.MaxPlayerId);

        RefreshComAbilities();

        ownColliders = GetComponentsInChildren<Collider2D>(true);

        if (movement != null)
        {
            tileSize = Mathf.Max(0.01f, movement.tileSize);
            obstacleFilter = new ContactFilter2D
            {
                useLayerMask = true,
                useTriggers = true
            };
            obstacleFilter.SetLayerMask(movement.obstacleMask);
        }

        gameManager = GameManager.Instance != null ? GameManager.Instance : FindAnyObjectByType<GameManager>();
        if (gameManager != null)
        {
            groundTilemap = gameManager.groundTilemap;
            destructibleTilemap = gameManager.destructibleTilemap;
            indestructibleTilemap = gameManager.indestructibleTilemap;
        }

        if (suddenDeathController == null || !suddenDeathController.isActiveAndEnabled)
            suddenDeathController = FindAnyObjectByType<BattleSuddenDeathController>();

        explosionMask = LayerMask.GetMask("Explosion");
    }

    private void RefreshComAbilities()
    {
        AbilitySystem abilitySystem = EnsureKnownComAbilityScripts();
        int currentVersion = abilitySystem != null ? abilitySystem.Version : -1;

        if (currentVersion == abilitySystemVersion && comAbilities.Count > 0)
            return;

        comAbilities.Clear();
        var monos = GetComponents<MonoBehaviour>();
        for (int i = 0; i < monos.Length; i++)
        {
            if (monos[i] is IBattleModeComAbility ability)
                comAbilities.Add(ability);
        }

        abilitySystemVersion = currentVersion;
    }

    private AbilitySystem EnsureKnownComAbilityScripts()
    {
        TryGetComponent<AbilitySystem>(out var abilitySystem);

        bool persistentKickEnabled = PlayerPersistentStats.GetRuntime(playerId).CanKickBombs;
        bool isCom = SaveSystem.GetBattleModePlayerControlMode(playerId) == BattleModePlayerControlMode.Com;
        if (persistentKickEnabled)
        {
            if (abilitySystem == null)
            {
                abilitySystem = gameObject.AddComponent<AbilitySystem>();
                LogKickLoadDiagnostic("created AbilitySystem from persistent kick");
            }

            if (!abilitySystem.IsEnabled(BombKickAbility.AbilityId))
            {
                abilitySystem.Enable(BombKickAbility.AbilityId);
                LogKickLoadDiagnostic("enabled BombKickAbility from persistent kick");
            }
        }

        bool yellowLouieKickEnabled =
            abilitySystem != null &&
            abilitySystem.IsEnabled(YellowLouieKickAbility.AbilityId);

        BattleModeComKickBombAbility exactKickCom = FindExactKickBombComAbility();
        bool hasYellowLouieKickCom = TryGetComponent<BattleModeComYellowLouieKickAbility>(out _);
        if (isCom &&
            yellowLouieKickEnabled &&
            !hasYellowLouieKickCom)
        {
            if (exactKickCom != null)
            {
                Destroy(exactKickCom);
                exactKickCom = null;
            }
            else
            {
                gameObject.AddComponent<BattleModeComYellowLouieKickAbility>();
                hasYellowLouieKickCom = true;
            }

            abilitySystemVersion = -2;
        }

        if (abilitySystem != null &&
            abilitySystem.IsEnabled(BombKickAbility.AbilityId) &&
            !yellowLouieKickEnabled &&
            !hasYellowLouieKickCom &&
            exactKickCom == null)
        {
            gameObject.AddComponent<BattleModeComKickBombAbility>();
            abilitySystemVersion = -2;
            LogKickLoadDiagnostic("added BattleModeComKickBombAbility");
        }

        // HazardAwareness é sempre ativa — não depende de power-up.
        // Torna a IA ciente de raios ampliados (HasFullFire, HasPowerBomb) de bombas inimigas.
        if (isCom && !TryGetComponent<BattleModeComHazardAwarenessAbility>(out _))
        {
            gameObject.AddComponent<BattleModeComHazardAwarenessAbility>();
            abilitySystemVersion = -2;
        }

        // PunchBomb: condicionado a CanPunchBombs, espelha o mesmo padrão do KickBomb.
        bool persistentPunchEnabled = PlayerPersistentStats.GetRuntime(playerId).CanPunchBombs;
        if (persistentPunchEnabled)
        {
            if (abilitySystem == null)
                abilitySystem = gameObject.AddComponent<AbilitySystem>();

            if (!abilitySystem.IsEnabled(BombPunchAbility.AbilityId))
                abilitySystem.Enable(BombPunchAbility.AbilityId);
        }

        if (abilitySystem != null &&
            abilitySystem.IsEnabled(BombPunchAbility.AbilityId) &&
            !TryGetComponent<BattleModeComPunchBombAbility>(out _))
        {
            gameObject.AddComponent<BattleModeComPunchBombAbility>();
            abilitySystemVersion = -2;
        }

        if (isCom && Time.frameCount - lastKickLoadDiagnosticFrame >= 120)
        {
            bool kickEnabled = abilitySystem != null && abilitySystem.IsEnabled(BombKickAbility.AbilityId);
            bool hasKickCom = TryGetComponent<BattleModeComKickBombAbility>(out _);
            LogKickLoadDiagnostic(
                $"load check persistentKick:{persistentKickEnabled} abilitySystem:{(abilitySystem != null)} " +
                $"kickEnabled:{kickEnabled} kickCom:{hasKickCom}");
        }

        return abilitySystem;
    }

    private BattleModeComKickBombAbility FindExactKickBombComAbility()
    {
        var abilities = GetComponents<BattleModeComKickBombAbility>();
        for (int i = 0; i < abilities.Length; i++)
        {
            BattleModeComKickBombAbility ability = abilities[i];
            if (ability != null && ability.GetType() == typeof(BattleModeComKickBombAbility))
                return ability;
        }

        return null;
    }

    private void LogKickLoadDiagnostic(string message)
    {
        if (!BattleModeComKickBombAbility.EnableKickBombLoadDiagnostics)
            return;

        if (!IsBattleModeScene())
            return;

        if (BattleModeComKickBombAbility.DiagnosticPlayerIdFilter != 0 &&
            playerId != BattleModeComKickBombAbility.DiagnosticPlayerIdFilter)
            return;

        lastKickLoadDiagnosticFrame = Time.frameCount;
        Debug.Log($"[BattleCOMKick][P{playerId}] {message}", this);
    }

    private void RefreshRuntimeEnabledState()
    {
        bool shouldRun = IsBattleModeScene() &&
                         SaveSystem.GetBattleModePlayerControlMode(playerId) == BattleModePlayerControlMode.Com;

        if (!shouldRun)
        {
            ClearSyntheticInputs();
            enabled = false;
            return;
        }

        enabled = true;
    }

    private void Update()
    {
        CacheReferences();

        if (!IsReadyToThink())
        {
            SetMovementInput(Vector2.zero);
            return;
        }

        BattleModeComputerLevel difficulty = ResolveDifficulty();
        BattleModeComDifficultySettings settings = BattleModeComDifficultySettings.For(difficulty);
        Vector2Int myTile = WorldToTile(transform.position);
        float currentDangerSeconds = GetDangerSeconds(myTile, null);
        bool inDanger = IsTileThreatened(myTile, null);

        if (inDanger || Time.time >= nextDecisionTime)
        {
            Think(settings, myTile, currentDangerSeconds, inDanger);
            nextDecisionTime = Time.time + (inDanger ? settings.dangerDecisionInterval : settings.decisionInterval);
        }

        currentMoveInput = ApplySafeTileCentering(myTile, currentMoveInput);
        currentMoveInput = ApplyTurnAxisCentering(myTile, currentMoveInput);
        currentMoveInput = EnforceSafeMovement(settings, myTile, currentMoveInput);
        SetMovementInput(currentMoveInput);
    }

    private bool IsReadyToThink()
    {
        if (!enabled || !IsBattleModeScene())
            return false;

        if (identity == null || movement == null || bombController == null)
            return false;

        if (SaveSystem.GetBattleModePlayerControlMode(playerId) != BattleModePlayerControlMode.Com)
            return false;

        if (movement.InputLocked || movement.isDead || movement.IsEndingStage)
            return false;

        if (GamePauseController.IsPaused)
            return false;

        if (ClownMaskBoss.BossIntroRunning)
            return false;

        if (StageIntroTransition.Instance != null &&
            (StageIntroTransition.Instance.IntroRunning || StageIntroTransition.Instance.EndingRunning))
        {
            return false;
        }

        return true;
    }

    private void Think(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        float currentDangerSeconds,
        bool inDanger)
    {
        candidates.Clear();
        rejectedActions.Clear();
        currentInputDescription = "none";
        EnsurePersonalitySeed();

        if (inDanger)
        {
            // Habilidades (ex.: chute ofensivo de bomba) podem CONTINUAR uma jogada já iniciada
            // mesmo sob perigo — tipicamente a IA recuou 1 tile e está dentro do raio da própria
            // bomba, mas a jogada certa é voltar e chutá-la no adversário (removendo a bomba da
            // lane e ficando segura), não fugir. Damos prioridade a isso antes da fuga.
            if (TryBuildAbilityEmergencyCandidate(settings, myTile, currentDangerSeconds, out CandidateAction abilityEmergency))
            {
                ExecuteSelectedCandidate(settings, myTile, currentDangerSeconds, abilityEmergency, "ability");
                return;
            }

            if (TryBuildChainBombCandidate(settings, myTile, currentDangerSeconds, onlyCurrentTile: true, out CandidateAction chainNow))
            {
                ExecuteSelectedCandidate(settings, myTile, currentDangerSeconds, chainNow, "chainBomb");
                return;
            }

            ExecuteEscape(settings, myTile, currentDangerSeconds);
            return;
        }

        if (HasOwnUnresolvedBombOrExplosion())
        {
            if (TryBuildChainBombCandidate(settings, myTile, currentDangerSeconds, onlyCurrentTile: false, out CandidateAction chain))
            {
                ExecuteSelectedCandidate(settings, myTile, currentDangerSeconds, chain, "chainBomb");
                return;
            }

            if (TryBuildAbilityCandidate(settings, myTile, out CandidateAction abilityWithOwnBomb))
            {
                ExecuteSelectedCandidate(settings, myTile, currentDangerSeconds, abilityWithOwnBomb, "ability");
                return;
            }

            hasSafeCenterTarget = true;
            safeCenterTargetTile = myTile;

            SetCurrentDecision(
                BattleModeComActionType.Stopped,
                Vector2.zero,
                false,
                myTile,
                "hold safe tile until own bomb resolves",
                "none",
                currentDangerSeconds,
                "safetyHold");
            return;
        }

        hasSafeCenterTarget = false;

        if (TryEmitControlBomb(settings, myTile, currentDangerSeconds))
            return;

        BuildCandidates(settings, myTile);

        if (candidates.Count <= 0)
        {
            SetCurrentDecision(
                BattleModeComActionType.Stopped,
                Vector2.zero,
                false,
                myTile,
                "no candidate",
                "none",
                currentDangerSeconds,
                "none");
            return;
        }

        if (UnityEngine.Random.value < settings.hesitationChance)
        {
            SetCurrentDecision(
                BattleModeComActionType.Stopped,
                Vector2.zero,
                false,
                myTile,
                "hesitation",
                "none",
                currentDangerSeconds,
                "none");
            return;
        }

        CandidateAction selected = PickWeightedCandidate(candidates);
        ExecuteSelectedCandidate(settings, myTile, currentDangerSeconds, selected, selected.HasRoute ? "found" : "none");
    }

    private void ExecuteSelectedCandidate(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        float currentDangerSeconds,
        CandidateAction selected,
        string route)
    {
        currentAction = selected.Action;
        currentMoveInput = selected.FirstMove;
        currentTargetTile = selected.TargetTile;
        hasCurrentTarget = selected.HasTarget;
        currentReason = selected.Reason;
        currentInputDescription = selected.InputDescription;

        if (selected.TapBomb && Time.time - lastBombTapTime >= BombTapCooldownSeconds)
        {
            Tap(PlayerAction.ActionA);
            lastBombTapTime = Time.time;
            currentInputDescription = AppendInput(currentInputDescription, "ActionA");
        }

        if (selected.TapActionR && Time.time - lastControlTapTime >= ControlBombTapCooldownSeconds)
        {
            Tap(PlayerAction.ActionR);
            lastControlTapTime = Time.time;
            currentInputDescription = AppendInput(currentInputDescription, "ActionR");
        }

        if (selected.TapActionC && Time.time - lastPunchTapTime >= PunchTapCooldownSeconds)
        {
            Tap(PlayerAction.ActionC);
            lastPunchTapTime = Time.time;
            currentInputDescription = AppendInput(currentInputDescription, "ActionC");
        }

        LogDecision(settings, myTile, currentDangerSeconds, route);
    }

    private bool TryBuildAbilityEmergencyCandidate(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        float currentDangerSeconds,
        out CandidateAction candidate)
    {
        candidate = default;
        RefreshComAbilities();

        if (comAbilities.Count == 0)
        {
            AppendAbilityTrace("emergency", "none", BuildNoAbilityScriptsTrace());
            return false;
        }

        for (int i = 0; i < comAbilities.Count; i++)
        {
            IBattleModeComAbility ability = comAbilities[i];
            if (ability == null)
            {
                AppendAbilityTrace("emergency", "null", "null ability entry");
                continue;
            }

            if (!ability.IsAvailable)
            {
                AppendAbilityTrace(ability, "emergency unavailable");
                continue;
            }

            if (!ability.TryBuildEmergencyDecision(settings, this, myTile, currentDangerSeconds, out var decision))
            {
                AppendAbilityTrace(ability, "emergency");
                continue;
            }

            candidate = ToCandidateAction(decision);
            AppendAbilityTrace(ability, "emergency selected");
            return true;
        }

        return false;
    }

    private bool TryBuildAbilityCandidate(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out CandidateAction candidate)
    {
        candidate = default;
        RefreshComAbilities();

        if (comAbilities.Count == 0)
        {
            AppendAbilityTrace("candidate", "none", BuildNoAbilityScriptsTrace());
            return false;
        }

        CandidateAction best = default;
        int bestWeight = 0;

        for (int i = 0; i < comAbilities.Count; i++)
        {
            IBattleModeComAbility ability = comAbilities[i];
            if (ability == null)
            {
                AppendAbilityTrace("candidate", "null", "null ability entry");
                continue;
            }

            if (!ability.IsAvailable)
            {
                AppendAbilityTrace(ability, "candidate unavailable");
                continue;
            }

            if (!ability.TryBuildCandidateDecision(settings, this, myTile, out var decision))
            {
                AppendAbilityTrace(ability, "candidate");
                continue;
            }

            CandidateAction candidateAction = ToCandidateAction(decision);
            if (candidateAction.Weight <= bestWeight)
                continue;

            best = candidateAction;
            bestWeight = candidateAction.Weight;
            AppendAbilityTrace(ability, "candidate selected");
        }

        if (bestWeight <= 0)
            return false;

        candidate = best;
        return true;
    }

    private static CandidateAction ToCandidateAction(BattleModeComAbilityDecision decision)
    {
        return new CandidateAction
        {
            Action = decision.Action,
            Weight = decision.Weight,
            TargetTile = decision.TargetTile,
            HasTarget = decision.HasTarget,
            FirstMove = decision.FirstMove,
            HasRoute = true,
            Reason = decision.Reason,
            InputDescription = decision.InputDescription,
            TapBomb = decision.TapBomb,
            TapActionR = decision.TapActionR,
            TapActionC = decision.TapActionC
        };
    }

    private void AppendAbilityTrace(IBattleModeComAbility ability, string phase)
    {
        if (ability == null)
            return;

        string name = string.IsNullOrWhiteSpace(ability.DiagnosticName)
            ? ability.GetType().Name
            : ability.DiagnosticName;
        string trace = string.IsNullOrWhiteSpace(ability.LastDecisionTrace)
            ? "no trace"
            : ability.LastDecisionTrace;

        rejectedActions.Add($"Ability/{phase}/{name}:{trace}");
    }

    private void AppendAbilityTrace(string phase, string name, string trace)
    {
        rejectedActions.Add($"Ability/{phase}/{name}:{trace}");
    }

    private string BuildNoAbilityScriptsTrace()
    {
        bool hasAbilitySystem = TryGetComponent<AbilitySystem>(out var abilitySystem) && abilitySystem != null;
        bool kickEnabled = hasAbilitySystem && abilitySystem.IsEnabled(BombKickAbility.AbilityId);
        bool persistentKickEnabled = PlayerPersistentStats.GetRuntime(playerId).CanKickBombs;
        bool hasKickComponent = TryGetComponent<BattleModeComKickBombAbility>(out _);

        return $"no com ability scripts loaded persistentKick:{persistentKickEnabled} " +
               $"abilitySystem:{hasAbilitySystem} kickEnabled:{kickEnabled} kickCom:{hasKickComponent}";
    }

    private bool HasAbilityTrace()
    {
        for (int i = 0; i < rejectedActions.Count; i++)
        {
            string entry = rejectedActions[i];
            if (!string.IsNullOrEmpty(entry) &&
                entry.StartsWith("Ability/", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void ExecuteEscape(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        float currentDangerSeconds)
    {
        if (TryFindEscape(settings, myTile, null, out Vector2 firstMove, out Vector2Int target, out string route))
        {
            SetCurrentDecision(
                BattleModeComActionType.Reposition,
                firstMove,
                true,
                target,
                "escape danger",
                FirstMoveDescription(firstMove),
                currentDangerSeconds,
                route);
            return;
        }

        if (TryBuildAbilityEmergencyCandidate(settings, myTile, currentDangerSeconds, out CandidateAction abilityEmergency))
        {
            ExecuteSelectedCandidate(settings, myTile, currentDangerSeconds, abilityEmergency, "ability");
            return;
        }

        if (ShouldHoldDangerTile(settings, myTile, currentDangerSeconds, out string holdReason))
        {
            SetCurrentDecision(
                BattleModeComActionType.Stopped,
                Vector2.zero,
                false,
                myTile,
                holdReason,
                "none",
                currentDangerSeconds,
                "dangerWait");
            return;
        }

        Vector2 fallback = FindBestFallbackMove(settings, myTile, null, out string fallbackReason);
        SetCurrentDecision(
            BattleModeComActionType.Reposition,
            fallback,
            false,
            myTile,
            fallbackReason,
            FirstMoveDescription(fallback),
            currentDangerSeconds,
            "fallback");
    }

    private void BuildCandidates(BattleModeComDifficultySettings settings, Vector2Int myTile)
    {
        AddCandidate(new CandidateAction
        {
            Action = BattleModeComActionType.Stopped,
            Weight = settings.stoppedWeight,
            TargetTile = myTile,
            HasTarget = false,
            FirstMove = Vector2.zero,
            HasRoute = true,
            Reason = "idle weight",
            InputDescription = "none"
        });

        if (TryBuildCollectCandidate(settings, myTile, out CandidateAction collect))
            AddCandidate(collect);
        else
            Reject("CollectItem", "no useful reachable item");

        if (TryBuildFarmCandidate(settings, myTile, out CandidateAction farm))
            AddCandidate(farm);
        else if (bombController == null || bombController.BombsRemaining <= 0)
            Reject("FarmDestructible", "sem bomba");
        else
            Reject("FarmDestructible", "sem rota segura para farm");

        if (TryBuildCombatCandidate(settings, myTile, out CandidateAction combat))
            AddCandidate(combat);
        else if (bombController == null || bombController.BombsRemaining <= 0)
            Reject("CombatPlant", "sem bomba");
        else
            Reject("CombatPlant", "alvo fora de alcance ou sem fuga");

        if (TryBuildAbilityCandidate(settings, myTile, out CandidateAction ability))
            AddCandidate(ability);
        else
            Reject("Ability", "sem habilidade aplicavel");

        if (TryBuildPatrolCandidate(settings, myTile, out CandidateAction patrol))
            AddCandidate(patrol);
        else
            Reject("Patrol", "sem tile seguro");
    }

    private void AddCandidate(CandidateAction candidate)
    {
        candidate.Weight = GetPersonalizedWeight(candidate);
        candidate.Weight = ApplyCurrentGoalCommitment(candidate);
        if (candidate.Weight <= 0)
            return;

        candidates.Add(candidate);
    }

    private int ApplyCurrentGoalCommitment(CandidateAction candidate)
    {
        if (currentAction != BattleModeComActionType.CollectItem || !hasCurrentTarget)
            return candidate.Weight;

        if (candidate.Action == BattleModeComActionType.CollectItem &&
            candidate.HasTarget &&
            candidate.TargetTile == currentTargetTile)
        {
            return candidate.Weight + 90;
        }

        if (candidate.Action == BattleModeComActionType.Patrol ||
            candidate.Action == BattleModeComActionType.Stopped)
        {
            return Mathf.Max(1, candidate.Weight / 4);
        }

        return candidate.Weight;
    }

    private bool TryBuildCollectCandidate(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out CandidateAction candidate)
    {
        candidate = default;
        ItemPickup[] items = FindObjectsByType<ItemPickup>(FindObjectsInactive.Exclude);
        ItemPickup bestItem = null;
        Vector2 bestMove = Vector2.zero;
        Vector2Int bestTile = myTile;
        int bestDistance = int.MaxValue;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < items.Length; i++)
        {
            ItemPickup item = items[i];
            if (item == null || !item.gameObject.activeInHierarchy)
                continue;

            if (!IsUsefulItem(item.type))
            {
                RejectVerbose($"CollectItem item perigoso {item.type}");
                continue;
            }

            Vector2Int itemTile = WorldToTile(item.transform.position);
            if (!IsWalkableTile(itemTile, myTile))
            {
                RejectVerbose($"CollectItem item bloqueado {item.type}@{itemTile}");
                continue;
            }

            if (!TryFindPath(myTile, itemTile, settings.searchDepth + 5, true, settings, null, out PathResult path))
            {
                RejectVerbose($"CollectItem sem rota {item.type}@{itemTile}");
                continue;
            }

            float danger = GetDangerSeconds(itemTile, null);
            float dangerBonus = float.IsInfinity(danger) ? 2f : Mathf.Clamp(danger, 0f, 2f);
            float score = -path.Distance * 10f +
                          dangerBonus * 0.15f +
                          GetDecisionNoise(1000 + i, ItemTargetJitter);

            if (score > bestScore)
            {
                bestItem = item;
                bestMove = path.FirstMove;
                bestTile = itemTile;
                bestDistance = path.Distance;
                bestScore = score;
            }
        }

        if (bestItem == null)
            return false;

        candidate = new CandidateAction
        {
            Action = BattleModeComActionType.CollectItem,
            Weight = GetCollectItemWeight(settings, bestDistance),
            TargetTile = bestTile,
            HasTarget = true,
            FirstMove = bestMove,
            HasRoute = true,
            Reason = $"item {bestItem.type} distance {bestDistance}",
            InputDescription = FirstMoveDescription(bestMove)
        };
        return true;
    }

    private bool TryBuildFarmCandidate(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out CandidateAction candidate)
    {
        candidate = default;

        if (bombController == null || bombController.BombsRemaining <= 0)
            return false;

        if (destructibleTilemap == null)
            return false;

        bool hasNearbyUsefulItem = HasReachableUsefulItem(myTile, settings, NearbyItemFarmBlockDistance, out _, out int nearbyItemDistance);
        if (hasNearbyUsefulItem)
        {
            RejectVerbose($"FarmDestructible adiado por item util perto distance {nearbyItemDistance}");
            return false;
        }

        GatherReachableSafeTiles(myTile, settings.searchDepth + 3, settings);

        Vector2Int bestTile = myTile;
        Vector2 bestMove = Vector2.zero;
        int bestDistance = int.MaxValue;
        float bestScore = float.NegativeInfinity;
        bool bestNeedsBombTap = false;
        Vector2 bestEscapeMove = Vector2.zero;

        for (int i = 0; i < reachableTiles.Count; i++)
        {
            Vector2Int tile = reachableTiles[i];
            if (!CanBombHitDestructible(tile, Mathf.Max(1, bombController.explosionRadius)))
                continue;

            if (WouldBlastHitUsefulItem(tile, Mathf.Max(1, bombController.explosionRadius), out ItemType itemType, out Vector2Int itemTile))
            {
                RejectVerbose($"FarmDestructible recusado queimaria item {itemType}@{itemTile}");
                continue;
            }

            if (!CanPlantBombWithEscape(tile, Mathf.Max(1, bombController.explosionRadius), settings, out Vector2 escapeMove, out _))
            {
                RejectVerbose($"FarmDestructible recusado sem fuga {tile}");
                continue;
            }

            if (!TryFindPath(myTile, tile, settings.searchDepth + 3, true, settings, null, out PathResult path))
                continue;

            float score = -path.Distance * 10f + GetDecisionNoise(2000 + i, FarmTargetJitter);
            if (score > bestScore)
            {
                bestTile = tile;
                bestMove = path.FirstMove;
                bestDistance = path.Distance;
                bestScore = score;
                bestNeedsBombTap = path.Distance == 0;
                bestEscapeMove = escapeMove;
            }
        }

        if (bestDistance == int.MaxValue)
            return false;

        candidate = new CandidateAction
        {
            Action = BattleModeComActionType.FarmDestructible,
            Weight = settings.farmDestructibleWeight,
            TargetTile = bestTile,
            HasTarget = true,
            FirstMove = bestNeedsBombTap ? bestEscapeMove : bestMove,
            HasRoute = true,
            Reason = bestNeedsBombTap ? "plant farm bomb" : $"move to farm tile distance {bestDistance}",
            InputDescription = bestNeedsBombTap
                ? AppendInput("ActionA", FirstMoveDescription(bestEscapeMove))
                : FirstMoveDescription(bestMove),
            TapBomb = bestNeedsBombTap
        };
        return true;
    }

    private bool TryBuildCombatCandidate(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out CandidateAction candidate)
    {
        candidate = default;

        if (bombController == null || bombController.BombsRemaining <= 0)
            return false;

        if (!TryFindNearestEnemy(myTile, out PlayerIdentity target, out Vector2Int targetTile, out int targetDistance))
            return false;

        int radius = Mathf.Max(1, bombController.explosionRadius);
        if (IsTileInBlastLineRuntime(myTile, targetTile, radius) &&
            !WouldBlastHitUsefulItem(myTile, radius, out _, out _) &&
            CanPlantBombWithEscape(myTile, radius, settings, out Vector2 escapeMove, out _))
        {
            candidate = new CandidateAction
            {
                Action = BattleModeComActionType.CombatPlant,
                Weight = settings.combatPlantWeight,
                TargetTile = targetTile,
                HasTarget = true,
                FirstMove = escapeMove,
                HasRoute = true,
                Reason = $"target P{target.playerId} in blast line",
                InputDescription = AppendInput("ActionA", FirstMoveDescription(escapeMove)),
                TapBomb = true
            };
            return true;
        }

        if (targetDistance <= settings.searchDepth + radius + 2 &&
            TryFindPath(myTile, targetTile, settings.searchDepth + 3, true, settings, null, out PathResult path))
        {
            candidate = new CandidateAction
            {
                Action = BattleModeComActionType.CombatPlant,
                Weight = Mathf.Max(1, settings.combatPlantWeight / 2),
                TargetTile = targetTile,
                HasTarget = true,
                FirstMove = path.FirstMove,
                HasRoute = true,
                Reason = $"approach target P{target.playerId} distance {targetDistance}",
                InputDescription = FirstMoveDescription(path.FirstMove)
            };
            return true;
        }

        RejectVerbose($"CombatPlant alvo P{target.playerId} fora de alcance ou sem rota segura");
        return false;
    }

    private bool TryBuildChainBombCandidate(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        float currentDangerSeconds,
        bool onlyCurrentTile,
        out CandidateAction candidate)
    {
        candidate = default;

        if (bombController == null || bombController.BombsRemaining <= 0)
            return false;

        int radius = Mathf.Max(1, bombController.explosionRadius);
        GatherReachableSafeTiles(myTile, onlyCurrentTile ? 0 : settings.searchDepth + 2, settings);

        Vector2Int bestTile = myTile;
        Vector2 bestMove = Vector2.zero;
        Vector2 bestEscapeMove = Vector2.zero;
        float bestScore = float.NegativeInfinity;
        int bestBranches = 0;
        float bestTriggerSeconds = float.PositiveInfinity;
        int bestDistance = int.MaxValue;
        bool bestCreatesTurn = false;

        for (int i = 0; i < reachableTiles.Count; i++)
        {
            Vector2Int tile = reachableTiles[i];
            if (onlyCurrentTile && tile != myTile)
                continue;

            if (IsBombAtTile(tile) || WouldBlastHitUsefulItem(tile, radius, out _, out _))
                continue;

            if (!TryGetLinkedOwnBombTriggerSeconds(tile, radius, out float triggerSeconds, out bool createsTurn))
                continue;

            if (triggerSeconds <= settings.dangerReactionSeconds + ChainBombFuseSafetyMarginSeconds)
            {
                RejectVerbose($"ChainBomb fuse curto {tile} {triggerSeconds:F2}s");
                continue;
            }

            if (!TryBuildChainBlastTiles(tile, radius, out List<Vector2Int> chainBlastTiles))
                continue;

            int escapeBranches = CountImmediateEscapeBranches(tile, chainBlastTiles);
            if (escapeBranches < ChainBombMinimumEscapeBranches)
            {
                RejectVerbose($"ChainBomb poucas fugas {tile} branches {escapeBranches}");
                continue;
            }

            if (!TryFindChainEscape(settings, tile, chainBlastTiles, triggerSeconds, out Vector2 escapeMove, out _))
            {
                RejectVerbose($"ChainBomb sem fuga {tile}");
                continue;
            }

            if (!TryFindPath(myTile, tile, settings.searchDepth + 2, false, settings, null, out PathResult path))
                continue;

            float arrivalSeconds = EstimateTraversalSeconds(path.Distance);
            if (!float.IsInfinity(currentDangerSeconds) &&
                currentDangerSeconds <= arrivalSeconds + settings.dangerReactionSeconds)
            {
                continue;
            }

            if (triggerSeconds <= arrivalSeconds + settings.safeTileMinimumSeconds + ChainBombFuseSafetyMarginSeconds)
            {
                RejectVerbose($"ChainBomb chegada insegura {tile} fuse {triggerSeconds:F2}s eta {arrivalSeconds:F2}s");
                continue;
            }

            float score =
                escapeBranches * 35f +
                (createsTurn ? 45f : 0f) +
                chainBlastTiles.Count * 1.5f -
                path.Distance * 10f +
                Mathf.Min(triggerSeconds, bombController.bombFuseTime) * 2f +
                GetDecisionNoise(5000 + i, FarmTargetJitter);

            if (score <= bestScore)
                continue;

            bestTile = tile;
            bestMove = path.FirstMove;
            bestEscapeMove = escapeMove;
            bestScore = score;
            bestBranches = escapeBranches;
            bestTriggerSeconds = triggerSeconds;
            bestDistance = path.Distance;
            bestCreatesTurn = createsTurn;
        }

        if (bestDistance == int.MaxValue)
            return false;

        bool plantNow = bestDistance == 0;
        candidate = new CandidateAction
        {
            Action = BattleModeComActionType.CombatPlant,
            Weight = Mathf.Max(1, settings.combatPlantWeight + bestBranches * 8),
            TargetTile = bestTile,
            HasTarget = true,
            FirstMove = plantNow ? bestEscapeMove : bestMove,
            HasRoute = true,
            Reason = plantNow
                ? $"chain bomb{(bestCreatesTurn ? " turn" : string.Empty)} branches {bestBranches} trigger {bestTriggerSeconds:F2}s"
                : $"move to chain bomb{(bestCreatesTurn ? " turn" : string.Empty)} tile distance {bestDistance} branches {bestBranches}",
            InputDescription = plantNow
                ? AppendInput("ActionA", FirstMoveDescription(bestEscapeMove))
                : FirstMoveDescription(bestMove),
            TapBomb = plantNow
        };
        return true;
    }

    private int GetCollectItemWeight(BattleModeComDifficultySettings settings, int distance)
    {
        int urgency = Mathf.Max(0, ItemPriorityDistance - distance);
        return settings.collectItemWeight + 40 + urgency * 12;
    }

    private bool HasReachableUsefulItem(
        Vector2Int myTile,
        BattleModeComDifficultySettings settings,
        int maxDistance,
        out ItemPickup foundItem,
        out int foundDistance)
    {
        foundItem = null;
        foundDistance = int.MaxValue;

        ItemPickup[] items = FindObjectsByType<ItemPickup>(FindObjectsInactive.Exclude);
        for (int i = 0; i < items.Length; i++)
        {
            ItemPickup item = items[i];
            if (item == null || !item.gameObject.activeInHierarchy || !IsUsefulItem(item.type))
                continue;

            Vector2Int itemTile = WorldToTile(item.transform.position);
            int manhattan = Manhattan(myTile, itemTile);
            if (manhattan > maxDistance)
                continue;

            if (!IsWalkableTile(itemTile, myTile))
                continue;

            if (!TryFindPath(myTile, itemTile, maxDistance, true, settings, null, out PathResult path))
                continue;

            if (path.Distance >= foundDistance)
                continue;

            foundItem = item;
            foundDistance = path.Distance;
        }

        return foundItem != null;
    }

    private bool ShouldHoldDangerTile(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        float currentDangerSeconds,
        out string reason)
    {
        reason = "hold danger tile";

        if (float.IsInfinity(currentDangerSeconds) || currentDangerSeconds <= settings.dangerReactionSeconds)
            return false;

        bool foundWalkableExit = false;
        float bestExitMargin = float.NegativeInfinity;
        Vector2Int bestExitTile = myTile;

        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int next = myTile + CardinalTiles[i];
            if (!IsWalkableTile(next, myTile))
                continue;

            foundWalkableExit = true;
            float arrivalSeconds = EstimateTraversalSeconds(1);
            float margin = GetSurvivalMarginSeconds(next, arrivalSeconds, settings, null);
            if (margin > bestExitMargin)
            {
                bestExitMargin = margin;
                bestExitTile = next;
            }
        }

        if (!foundWalkableExit)
        {
            reason = "hold danger tile no walkable exit";
            return true;
        }

        float currentMargin = currentDangerSeconds - settings.dangerReactionSeconds;
        if (currentMargin + DangerTimingMarginSeconds >= bestExitMargin)
        {
            reason = $"hold danger tile current {currentMargin:F2}s exit {bestExitTile} {bestExitMargin:F2}s";
            return true;
        }

        return false;
    }

    private bool TryBuildPatrolCandidate(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out CandidateAction candidate)
    {
        candidate = default;

        Vector2 move = Vector2.zero;
        Vector2Int targetTile = myTile;
        string reason = "safe roam";

        if (TryFindNearestEnemy(myTile, out PlayerIdentity target, out Vector2Int enemyTile, out int enemyDistance) &&
            TryFindPath(myTile, enemyTile, settings.searchDepth + 4, true, settings, null, out PathResult enemyPath))
        {
            move = enemyPath.FirstMove;
            targetTile = enemyTile;
            reason = $"patrol toward P{target.playerId} distance {enemyDistance}";
        }
        else
        {
            move = FindBestFallbackMove(settings, myTile, null, out reason);
            if (move == Vector2.zero)
                return false;

            targetTile = myTile + DirectionToTile(move);
        }

        candidate = new CandidateAction
        {
            Action = BattleModeComActionType.Patrol,
            Weight = settings.patrolWeight,
            TargetTile = targetTile,
            HasTarget = true,
            FirstMove = move,
            HasRoute = move != Vector2.zero,
            Reason = reason,
            InputDescription = FirstMoveDescription(move)
        };
        return true;
    }

    private bool TryEmitControlBomb(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        float currentDangerSeconds)
    {
        if (bombController == null || Time.time - lastControlTapTime < ControlBombTapCooldownSeconds)
            return false;

        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded || !bomb.IsControlBomb || bomb.Owner != bombController)
                continue;

            Vector2Int bombTile = WorldToTile(bomb.GetLogicalPosition());
            int radius = Mathf.Max(1, bombController.explosionRadius);
            if (!WouldExplosionHitEnemyWithoutFriendlyRisk(bombTile, radius, myTile, out int enemyId))
                continue;

            Tap(PlayerAction.ActionB);
            lastControlTapTime = Time.time;

            SetCurrentDecision(
                BattleModeComActionType.CombatPlant,
                currentMoveInput,
                true,
                bombTile,
                $"control bomb hits P{enemyId}",
                "ActionB",
                currentDangerSeconds,
                "controlBomb");
            return true;
        }

        RejectVerbose("ControlBomb sem alvo seguro");
        return false;
    }

    private CandidateAction PickWeightedCandidate(List<CandidateAction> source)
    {
        decisionSerial++;
        int total = 0;
        for (int i = 0; i < source.Count; i++)
            total += Mathf.Max(0, source[i].Weight);

        if (total <= 0)
            return source[0];

        int roll = UnityEngine.Random.Range(0, total);
        int cursor = 0;

        for (int i = 0; i < source.Count; i++)
        {
            cursor += Mathf.Max(0, source[i].Weight);
            if (roll < cursor)
                return source[i];
        }

        return source[source.Count - 1];
    }

    private int GetPersonalizedWeight(CandidateAction candidate)
    {
        int baseWeight = Mathf.Max(0, candidate.Weight);
        if (baseWeight <= 0)
            return 0;

        float stableBias = GetStableNoise(3000 + (int)candidate.Action) * PersonalityWeightJitter;
        float decisionJitter = GetDecisionNoise(4000 + (int)candidate.Action, PersonalityWeightJitter * 0.5f);
        float multiplier = Mathf.Clamp(1f + stableBias + decisionJitter, 0.65f, 1.35f);

        return Mathf.Max(1, Mathf.RoundToInt(baseWeight * multiplier));
    }

    private float GetStableNoise(int salt)
    {
        EnsurePersonalitySeed();
        return HashToSignedUnit(personalitySeed, salt);
    }

    private float GetDecisionNoise(int salt, float amplitude)
    {
        EnsurePersonalitySeed();
        return HashToSignedUnit(personalitySeed, salt + decisionSerial * 101) * amplitude;
    }

    private void EnsurePersonalitySeed()
    {
        if (personalitySeed != 0)
            return;

        unchecked
        {
            int seed = 17;
            seed = seed * 31 + playerId;
            seed = seed * 31 + SceneManager.GetActiveScene().buildIndex;
            personalitySeed = seed != 0 ? seed : 1;
        }
    }

    private static float HashToSignedUnit(int seed, int salt)
    {
        unchecked
        {
            uint hash = (uint)seed;
            hash ^= (uint)salt + 0x9E3779B9u + (hash << 6) + (hash >> 2);
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return (hash / (float)uint.MaxValue) * 2f - 1f;
        }
    }

    private void SetCurrentDecision(
        BattleModeComActionType action,
        Vector2 move,
        bool hasTarget,
        Vector2Int target,
        string reason,
        string input,
        float dangerSeconds,
        string route)
    {
        currentAction = action;
        currentMoveInput = move;
        hasCurrentTarget = hasTarget;
        currentTargetTile = target;
        currentReason = reason;
        currentInputDescription = input;
        currentMoveFollowsEscapeRoute =
            action == BattleModeComActionType.Reposition &&
            hasTarget &&
            !string.IsNullOrEmpty(route) &&
            route.StartsWith("escape", StringComparison.Ordinal);

        if (action == BattleModeComActionType.Reposition && hasTarget)
        {
            hasSafeCenterTarget = true;
            safeCenterTargetTile = target;
        }

        BattleModeComDifficultySettings settings = BattleModeComDifficultySettings.For(ResolveDifficulty());
        LogDecision(settings, WorldToTile(transform.position), dangerSeconds, route);
    }

    private void LogDecision(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        float dangerSeconds,
        string route)
    {
        if (!EnableGeneralDecisionLogs)
            return;

        if (!BattleModeComDiagnostics.ShouldLog(logLevel, BattleModeComDiagnostics.LogLevel.Summary))
            return;

        string target = hasCurrentTarget ? currentTargetTile.ToString() : "none";
        string danger = FormatDanger(dangerSeconds);
        string scene = SceneManager.GetActiveScene().name;
        string summary = BattleModeComDiagnostics.FormatSummary(
            scene,
            Time.frameCount,
            playerId,
            settings.difficulty,
            currentAction,
            target,
            myTile,
            danger,
            route,
            currentReason,
            currentInputDescription);

        string line = $"[BattleCOM][P{playerId}] {summary}";
        PushDecision(line);
        Debug.Log(line, this);

        bool shouldLogVerbose = BattleModeComDiagnostics.ShouldLog(logLevel, BattleModeComDiagnostics.LogLevel.Verbose);
        bool shouldLogAbilityFailure =
            HasAbilityTrace() &&
            (string.Equals(route, "fallback", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(route, "dangerWait", StringComparison.OrdinalIgnoreCase) ||
             currentReason.Contains("fallback", StringComparison.OrdinalIgnoreCase) ||
             currentReason.Contains("hold danger tile", StringComparison.OrdinalIgnoreCase) ||
             currentReason.Contains("no fallback", StringComparison.OrdinalIgnoreCase));

        if (shouldLogVerbose || shouldLogAbilityFailure)
        {
            Debug.Log($"[BattleCOM][P{playerId}] candidates:{FormatCandidates()} rejected:{string.Join(" | ", rejectedActions)}", this);
        }
    }

    private void PushDecision(string line)
    {
        recentDecisions.Add(line);

        int max = Mathf.Clamp(decisionBufferSize, 4, MaxDecisionBufferEntries);
        while (recentDecisions.Count > max)
            recentDecisions.RemoveAt(0);
    }

    private string FormatCandidates()
    {
        if (candidates.Count <= 0)
            return "none";

        string result = string.Empty;
        for (int i = 0; i < candidates.Count; i++)
        {
            CandidateAction c = candidates[i];
            if (i > 0)
                result += ", ";

            result += $"{c.Action}:{c.Weight}:{c.Reason}";
        }

        return result;
    }

    private void Reject(string action, string reason)
    {
        if (BattleModeComDiagnostics.ShouldLog(logLevel, BattleModeComDiagnostics.LogLevel.Verbose))
            rejectedActions.Add($"{action}:{reason}");
    }

    private void RejectVerbose(string reason)
    {
        if (BattleModeComDiagnostics.ShouldLog(logLevel, BattleModeComDiagnostics.LogLevel.Verbose))
            rejectedActions.Add(reason);
    }

    private bool TryFindEscape(
        BattleModeComDifficultySettings settings,
        Vector2Int start,
        List<Vector2Int> plannedBlastTiles,
        out Vector2 firstMove,
        out Vector2Int target,
        out string route)
    {
        firstMove = Vector2.zero;
        target = start;
        route = "none";

        visited.Clear();
        open.Clear();

        visited[start] = new PathNode { Tile = start, Parent = start, Depth = 0 };
        open.Enqueue(start);

        while (open.Count > 0)
        {
            Vector2Int tile = open.Dequeue();
            PathNode node = visited[tile];
            float eta = EstimateTraversalSeconds(node.Depth);
            float dangerSeconds = GetDangerSeconds(tile, plannedBlastTiles);
            bool plannedDanger = plannedBlastTiles != null && plannedBlastTiles.Contains(tile);

            if (!plannedDanger &&
                node.Depth > 0 &&
                float.IsInfinity(dangerSeconds) &&
                !IsDangerousAt(tile, eta + settings.safeTileMinimumSeconds, settings, plannedBlastTiles) &&
                dangerSeconds > eta + settings.safeTileMinimumSeconds)
            {
                target = tile;
                firstMove = ReconstructFirstMove(start, tile);
                route = $"escape depth {node.Depth}";
                return true;
            }

            if (node.Depth >= settings.searchDepth + 3)
                continue;

            for (int i = 0; i < CardinalTiles.Length; i++)
            {
                Vector2Int next = tile + CardinalTiles[i];
                if (visited.ContainsKey(next))
                    continue;

                if (!IsWalkableTile(next, start))
                    continue;

                float nextEta = EstimateTraversalSeconds(node.Depth + 1);
                if (IsDangerousAt(next, nextEta, settings, plannedBlastTiles))
                    continue;

                visited[next] = new PathNode { Tile = next, Parent = tile, Depth = node.Depth + 1 };
                open.Enqueue(next);
            }
        }

        return false;
    }

    private Vector2 FindBestFallbackMove(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        List<Vector2Int> plannedBlastTiles,
        out string reason)
    {
        float bestScore = float.NegativeInfinity;
        Vector2 bestMove = Vector2.zero;
        reason = "no fallback";

        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int next = myTile + CardinalTiles[i];
            if (!IsWalkableTile(next, myTile))
                continue;

            float arrivalSeconds = EstimateTraversalSeconds(1);
            float score = GetSurvivalMarginSeconds(next, arrivalSeconds, settings, plannedBlastTiles) +
                          CountOpenNeighbors(next) * 0.25f;
            if (plannedBlastTiles != null && plannedBlastTiles.Contains(next))
                score -= 10f;

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = TileDirectionToVector(CardinalTiles[i]);
                reason = $"fallback score {score:F2}";
            }
        }

        return bestMove;
    }

    private bool TryFindPath(
        Vector2Int start,
        Vector2Int goal,
        int maxDepth,
        bool avoidDanger,
        BattleModeComDifficultySettings settings,
        List<Vector2Int> plannedBlastTiles,
        out PathResult result)
    {
        result = default;

        if (start == goal)
        {
            result = new PathResult
            {
                Found = true,
                FirstMove = Vector2.zero,
                Distance = 0
            };
            return true;
        }

        visited.Clear();
        open.Clear();

        visited[start] = new PathNode { Tile = start, Parent = start, Depth = 0 };
        open.Enqueue(start);

        while (open.Count > 0)
        {
            Vector2Int tile = open.Dequeue();
            PathNode node = visited[tile];

            if (node.Depth >= maxDepth)
                continue;

            Vector2Int[] directions = GetPathDirectionOrder(tile, goal);
            for (int i = 0; i < directions.Length; i++)
            {
                Vector2Int next = tile + directions[i];
                if (visited.ContainsKey(next))
                    continue;

                if (!IsWalkableTile(next, start))
                    continue;

                int nextDepth = node.Depth + 1;
                if (avoidDanger && IsTileThreatened(next, plannedBlastTiles))
                    continue;

                visited[next] = new PathNode { Tile = next, Parent = tile, Depth = nextDepth };

                if (next == goal)
                {
                    result = new PathResult
                    {
                        Found = true,
                        FirstMove = ReconstructFirstMove(start, goal),
                        Distance = nextDepth
                    };
                    return true;
                }

                open.Enqueue(next);
            }
        }

        RejectVerbose($"pathfinding failed {start}->{goal}");
        return false;
    }

    private struct PathResult
    {
        public bool Found;
        public Vector2 FirstMove;
        public int Distance;
    }

    private void GatherReachableSafeTiles(Vector2Int start, int maxDepth, BattleModeComDifficultySettings settings)
    {
        reachableTiles.Clear();
        visited.Clear();
        open.Clear();

        visited[start] = new PathNode { Tile = start, Parent = start, Depth = 0 };
        open.Enqueue(start);
        reachableTiles.Add(start);

        while (open.Count > 0)
        {
            Vector2Int tile = open.Dequeue();
            PathNode node = visited[tile];
            if (node.Depth >= maxDepth)
                continue;

            for (int i = 0; i < CardinalTiles.Length; i++)
            {
                Vector2Int next = tile + CardinalTiles[i];
                if (visited.ContainsKey(next))
                    continue;

                if (!IsWalkableTile(next, start))
                    continue;

                int nextDepth = node.Depth + 1;
                if (IsTileThreatened(next, null))
                    continue;

                visited[next] = new PathNode { Tile = next, Parent = tile, Depth = nextDepth };
                reachableTiles.Add(next);
                open.Enqueue(next);
            }
        }
    }

    private Vector2 ReconstructFirstMove(Vector2Int start, Vector2Int goal)
    {
        if (start == goal)
            return Vector2.zero;

        Vector2Int cursor = goal;
        while (visited.TryGetValue(cursor, out PathNode node) && node.Parent != start && node.Parent != cursor)
            cursor = node.Parent;

        Vector2Int delta = cursor - start;
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            return delta.x > 0 ? Vector2.right : Vector2.left;

        if (delta.y != 0)
            return delta.y > 0 ? Vector2.up : Vector2.down;

        return Vector2.zero;
    }

    private Vector2Int[] GetPathDirectionOrder(Vector2Int start, Vector2Int goal)
    {
        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        Vector2Int preferredDirection = DirectionToTile(currentMoveInput);
        for (int i = 1; i < directions.Length; i++)
        {
            Vector2Int direction = directions[i];
            int score = GetPathDirectionScore(start, goal, direction, preferredDirection);
            int j = i - 1;

            while (j >= 0 &&
                   score < GetPathDirectionScore(start, goal, directions[j], preferredDirection))
            {
                directions[j + 1] = directions[j];
                j--;
            }

            directions[j + 1] = direction;
        }

        return directions;
    }

    private int GetPathDirectionScore(
        Vector2Int start,
        Vector2Int goal,
        Vector2Int direction,
        Vector2Int preferredDirection)
    {
        int score = Manhattan(start + direction, goal) * 10;

        if (direction == preferredDirection)
            score -= 2;

        Vector2Int delta = goal - start;
        if ((direction.x != 0 && Math.Sign(delta.x) == direction.x) ||
            (direction.y != 0 && Math.Sign(delta.y) == direction.y))
        {
            score -= 1;
        }

        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y) &&
            direction.x != 0 &&
            Math.Sign(delta.x) == direction.x)
        {
            score -= 1;
        }
        else if (Mathf.Abs(delta.y) > Mathf.Abs(delta.x) &&
                 direction.y != 0 &&
                 Math.Sign(delta.y) == direction.y)
        {
            score -= 1;
        }

        return score;
    }

    private bool CanPlantBombWithEscape(
        Vector2Int plantTile,
        int radius,
        BattleModeComDifficultySettings settings,
        out Vector2 escapeMove,
        out Vector2Int escapeTile)
    {
        List<Vector2Int> plannedBlast = BuildBlastTiles(plantTile, radius);
        return TryFindEscape(settings, plantTile, plannedBlast, out escapeMove, out escapeTile, out _);
    }

    private bool TryFindChainEscape(
        BattleModeComDifficultySettings settings,
        Vector2Int start,
        List<Vector2Int> chainBlastTiles,
        float triggerSeconds,
        out Vector2 firstMove,
        out Vector2Int target)
    {
        firstMove = Vector2.zero;
        target = start;

        visited.Clear();
        open.Clear();

        visited[start] = new PathNode { Tile = start, Parent = start, Depth = 0 };
        open.Enqueue(start);

        while (open.Count > 0)
        {
            Vector2Int tile = open.Dequeue();
            PathNode node = visited[tile];
            float eta = EstimateTraversalSeconds(node.Depth);
            bool inChainBlast = chainBlastTiles != null && chainBlastTiles.Contains(tile);

            if (node.Depth > 0 && !inChainBlast)
            {
                float dangerSeconds = GetDangerSeconds(tile, null);
                float requiredSeconds =
                    eta +
                    settings.dangerReactionSeconds +
                    settings.safeTileMinimumSeconds +
                    ChainBombFuseSafetyMarginSeconds;

                if (triggerSeconds > requiredSeconds &&
                    (float.IsInfinity(dangerSeconds) || dangerSeconds > requiredSeconds))
                {
                    target = tile;
                    firstMove = ReconstructFirstMove(start, tile);
                    return true;
                }
            }

            if (node.Depth >= settings.searchDepth + 3)
                continue;

            for (int i = 0; i < CardinalTiles.Length; i++)
            {
                Vector2Int next = tile + CardinalTiles[i];
                if (visited.ContainsKey(next))
                    continue;

                if (!IsWalkableTile(next, start))
                    continue;

                float nextEta = EstimateTraversalSeconds(node.Depth + 1);
                float nextDanger = GetDangerSeconds(next, null);
                if (!float.IsInfinity(nextDanger) &&
                    nextDanger <= nextEta + settings.dangerReactionSeconds)
                {
                    continue;
                }

                visited[next] = new PathNode { Tile = next, Parent = tile, Depth = node.Depth + 1 };
                open.Enqueue(next);
            }
        }

        return false;
    }

    private List<Vector2Int> BuildBlastTiles(Vector2Int origin, int radius)
    {
        List<Vector2Int> tiles = new();
        tiles.Add(origin);

        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int dir = CardinalTiles[i];
            for (int step = 1; step <= radius; step++)
            {
                Vector2Int tile = origin + dir * step;
                tiles.Add(tile);

                if (BlocksExplosion(tile))
                    break;
            }
        }

        return tiles;
    }

    private bool TryGetLinkedOwnBombTriggerSeconds(
        Vector2Int plantTile,
        int radius,
        out float triggerSeconds,
        out bool createsTurn)
    {
        triggerSeconds = float.PositiveInfinity;
        bool linkedToOwnBomb = false;
        createsTurn = false;

        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded)
                continue;

            Vector2Int bombTile = WorldToTile(bomb.GetLogicalPosition());
            int bombRadius = bomb.Owner != null ? Mathf.Max(1, bomb.Owner.GetPredictedBlastRadius(bomb)) : radius;
            if (!IsTileInBlastLineRuntime(bombTile, plantTile, bombRadius))
                continue;

            if (bomb.Owner == bombController && !bomb.IsControlBomb)
            {
                linkedToOwnBomb = true;
                createsTurn |= WouldCreateChainTurn(plantTile, bombTile);
            }

            float seconds = bomb.IsControlBomb ? 0.65f : bomb.RemainingFuseSeconds;
            triggerSeconds = Mathf.Min(triggerSeconds, seconds);
        }

        return linkedToOwnBomb && !float.IsInfinity(triggerSeconds);
    }

    private bool WouldCreateChainTurn(Vector2Int plantTile, Vector2Int linkedBombTile)
    {
        Vector2Int newLeg = GetAxisDirection(plantTile - linkedBombTile);
        if (newLeg == Vector2Int.zero)
            return false;

        foreach (Bomb other in Bomb.ActiveBombs)
        {
            if (other == null || other.HasExploded || other.Owner != bombController || other.IsControlBomb)
                continue;

            Vector2Int otherTile = WorldToTile(other.GetLogicalPosition());
            if (otherTile == linkedBombTile || otherTile == plantTile)
                continue;

            Vector2Int existingLeg = GetAxisDirection(otherTile - linkedBombTile);
            if (existingLeg == Vector2Int.zero)
                continue;

            if (IsPerpendicular(newLeg, existingLeg) &&
                IsTileInBlastLineRuntime(linkedBombTile, otherTile, GetBombRadiusAtTile(linkedBombTile, 1)))
            {
                return true;
            }
        }

        return false;
    }

    private static Vector2Int GetAxisDirection(Vector2Int delta)
    {
        if (delta.x == 0 && delta.y != 0)
            return delta.y > 0 ? Vector2Int.up : Vector2Int.down;

        if (delta.y == 0 && delta.x != 0)
            return delta.x > 0 ? Vector2Int.right : Vector2Int.left;

        return Vector2Int.zero;
    }

    private static bool IsPerpendicular(Vector2Int a, Vector2Int b)
    {
        return a != Vector2Int.zero && b != Vector2Int.zero && a.x * b.x + a.y * b.y == 0;
    }

    private bool TryBuildChainBlastTiles(Vector2Int plantTile, int radius, out List<Vector2Int> chainBlastTiles)
    {
        chainBlastTiles = BuildBlastTiles(plantTile, radius);
        Queue<Vector2Int> triggeredBombTiles = new();
        HashSet<Vector2Int> visitedBombTiles = new();

        EnqueueBombsHitByBlast(chainBlastTiles, triggeredBombTiles, visitedBombTiles);

        while (triggeredBombTiles.Count > 0)
        {
            Vector2Int bombTile = triggeredBombTiles.Dequeue();
            int bombRadius = GetBombRadiusAtTile(bombTile, radius);
            List<Vector2Int> bombBlast = BuildBlastTiles(bombTile, bombRadius);

            for (int i = 0; i < bombBlast.Count; i++)
            {
                if (!chainBlastTiles.Contains(bombBlast[i]))
                    chainBlastTiles.Add(bombBlast[i]);
            }

            EnqueueBombsHitByBlast(chainBlastTiles, triggeredBombTiles, visitedBombTiles);
        }

        return chainBlastTiles.Count > 0;
    }

    private void EnqueueBombsHitByBlast(
        List<Vector2Int> blastTiles,
        Queue<Vector2Int> triggeredBombTiles,
        HashSet<Vector2Int> visitedBombTiles)
    {
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded)
                continue;

            Vector2Int bombTile = WorldToTile(bomb.GetLogicalPosition());
            if (visitedBombTiles.Contains(bombTile) || !blastTiles.Contains(bombTile))
                continue;

            visitedBombTiles.Add(bombTile);
            triggeredBombTiles.Enqueue(bombTile);
        }
    }

    private int GetBombRadiusAtTile(Vector2Int tile, int fallbackRadius)
    {
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded)
                continue;

            if (WorldToTile(bomb.GetLogicalPosition()) != tile)
                continue;

            return bomb.Owner != null ? Mathf.Max(1, bomb.Owner.GetPredictedBlastRadius(bomb)) : fallbackRadius;
        }

        return fallbackRadius;
    }

    private int CountImmediateEscapeBranches(Vector2Int tile, List<Vector2Int> chainBlastTiles)
    {
        int count = 0;
        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int next = tile + CardinalTiles[i];
            if (!IsWalkableTile(next, tile))
                continue;

            count++;
        }

        return count;
    }

    private bool WouldBlastHitUsefulItem(Vector2Int origin, int radius, out ItemType itemType, out Vector2Int itemTile)
    {
        itemType = default;
        itemTile = origin;

        ItemPickup[] items = FindObjectsByType<ItemPickup>(FindObjectsInactive.Exclude);
        for (int i = 0; i < items.Length; i++)
        {
            ItemPickup item = items[i];
            if (item == null || !item.gameObject.activeInHierarchy || !IsUsefulItem(item.type))
                continue;

            Vector2Int tile = WorldToTile(item.transform.position);
            if (!IsTileInBlastLineRuntime(origin, tile, radius))
                continue;

            itemType = item.type;
            itemTile = tile;
            return true;
        }

        return false;
    }

    private bool CanBombHitDestructible(Vector2Int plantTile, int radius)
    {
        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int dir = CardinalTiles[i];
            for (int step = 1; step <= radius; step++)
            {
                Vector2Int tile = plantTile + dir * step;

                if (HasIndestructibleTile(tile))
                    break;

                if (HasDestructibleTile(tile))
                    return true;

                if (IsBombAtTile(tile))
                    break;
            }
        }

        return false;
    }

    private bool TryFindNearestEnemy(
        Vector2Int myTile,
        out PlayerIdentity target,
        out Vector2Int targetTile,
        out int distance)
    {
        target = null;
        targetTile = myTile;
        distance = int.MaxValue;

        activePlayers.Clear();
        PlayerIdentity.GetActivePlayers(activePlayers);

        for (int i = 0; i < activePlayers.Count; i++)
        {
            PlayerIdentity player = activePlayers[i];
            if (player == null || player == identity)
                continue;

            if (!player.TryGetComponent<MovementController>(out var targetMovement) ||
                targetMovement == null ||
                targetMovement.isDead ||
                targetMovement.IsEndingStage)
            {
                continue;
            }

            if (IsAlly(player.playerId))
                continue;

            Vector2Int tile = WorldToTile(player.transform.position);
            int d = Manhattan(myTile, tile);
            if (d < distance)
            {
                target = player;
                targetTile = tile;
                distance = d;
            }
        }

        return target != null;
    }

    private bool WouldExplosionHitEnemyWithoutFriendlyRisk(
        Vector2Int bombTile,
        int radius,
        Vector2Int myTile,
        out int enemyId)
    {
        enemyId = 0;
        bool hitsEnemy = false;

        activePlayers.Clear();
        PlayerIdentity.GetActivePlayers(activePlayers);

        for (int i = 0; i < activePlayers.Count; i++)
        {
            PlayerIdentity player = activePlayers[i];
            if (player == null)
                continue;

            if (!player.TryGetComponent<MovementController>(out var targetMovement) ||
                targetMovement == null ||
                targetMovement.isDead ||
                targetMovement.IsEndingStage)
            {
                continue;
            }

            Vector2Int tile = WorldToTile(player.transform.position);
            if (!IsTileInBlastLineRuntime(bombTile, tile, radius))
                continue;

            if (player == identity || IsAlly(player.playerId))
                return false;

            hitsEnemy = true;
            enemyId = player.playerId;
        }

        return hitsEnemy && !IsTileInBlastLineRuntime(bombTile, myTile, radius);
    }

    private bool IsAlly(int otherPlayerId)
    {
        if (BattleModeRules.Instance == null || !BattleModeRules.Instance.UsesTeams)
            return false;

        return BattleModeRules.Instance.GetTeamForPlayer(playerId) ==
               BattleModeRules.Instance.GetTeamForPlayer(otherPlayerId);
    }

    private bool IsUsefulItem(ItemType type)
    {
        return type != ItemType.Skull &&
               type != ItemType.LandMine &&
               type != ItemType.Clock;
    }

    private bool IsWalkableTile(Vector2Int tile, Vector2Int startTile)
    {
        if (!HasGroundTile(tile))
            return false;

        if (HasIndestructibleTile(tile) || HasDestructibleTile(tile))
            return false;

        if (IsBombAtTile(tile) && tile != startTile)
            return false;

        if (movement != null && movement.obstacleMask.value != 0)
        {
            Vector2 center = TileToWorld(tile);
            Vector2 size = Vector2.one * (tileSize * 0.55f);
            int hitCount = Physics2D.OverlapBox(center, size, 0f, obstacleFilter, obstacleHits);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = obstacleHits[i];
                if (hit == null || IsOwnCollider(hit))
                    continue;

                if (hit.GetComponentInParent<ItemPickup>() != null)
                    continue;

                if (hit.GetComponentInParent<PlayerIdentity>() != null)
                    continue;

                if (tile == startTile && hit.GetComponentInParent<Bomb>() != null)
                    continue;

                return false;
            }
        }

        return true;
    }

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

    private bool HasGroundTile(Vector2Int tile)
    {
        if (groundTilemap == null)
            return true;

        return groundTilemap.HasTile(WorldToCell(groundTilemap, tile));
    }

    private bool HasDestructibleTile(Vector2Int tile)
    {
        return destructibleTilemap != null && destructibleTilemap.HasTile(WorldToCell(destructibleTilemap, tile));
    }

    private bool HasIndestructibleTile(Vector2Int tile)
    {
        return indestructibleTilemap != null && indestructibleTilemap.HasTile(WorldToCell(indestructibleTilemap, tile));
    }

    private bool IsBombAtTile(Vector2Int tile)
    {
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded)
                continue;

            if (WorldToTile(bomb.GetLogicalPosition()) == tile)
                return true;
        }

        return false;
    }

    private bool BlocksExplosion(Vector2Int tile)
    {
        return HasIndestructibleTile(tile) || HasDestructibleTile(tile) || IsBombAtTile(tile);
    }

    private float GetDangerSeconds(Vector2Int tile, List<Vector2Int> plannedBlastTiles)
    {
        if (plannedBlastTiles != null && plannedBlastTiles.Contains(tile))
            return bombController != null ? Mathf.Max(0.5f, bombController.bombFuseTime) : 2f;

        if (explosionMask != 0)
        {
            Collider2D explosion = Physics2D.OverlapCircle(TileToWorld(tile), tileSize * 0.25f, explosionMask);
            if (explosion != null)
                return 0f;
        }

        float danger = float.PositiveInfinity;
        if (suddenDeathController != null &&
            suddenDeathController.TryGetSecondsUntilSuddenDeathWorldPosition(TileToWorld(tile), out float suddenDeathSeconds) &&
            suddenDeathSeconds <= SuddenDeathUnsafeLeadSeconds)
        {
            danger = Mathf.Min(danger, suddenDeathSeconds);
        }

        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded)
                continue;

            Vector2Int bombTile = WorldToTile(bomb.GetLogicalPosition());
            int radius = bomb.Owner != null ? Mathf.Max(1, bomb.Owner.GetPredictedBlastRadius(bomb)) : 2;
            if (!IsTileInBlastLineRuntime(bombTile, tile, radius))
                continue;

            float seconds = bomb.IsControlBomb ? 0.65f : bomb.RemainingFuseSeconds;
            danger = Mathf.Min(danger, seconds);
        }

        return danger;
    }

    private bool IsDangerousAt(
        Vector2Int tile,
        float arrivalSeconds,
        BattleModeComDifficultySettings settings,
        List<Vector2Int> plannedBlastTiles)
    {
        float dangerSeconds = GetDangerSeconds(tile, plannedBlastTiles);
        if (float.IsInfinity(dangerSeconds))
            return false;

        return dangerSeconds <= arrivalSeconds + settings.dangerReactionSeconds;
    }

    private float GetSurvivalMarginSeconds(
        Vector2Int tile,
        float arrivalSeconds,
        BattleModeComDifficultySettings settings,
        List<Vector2Int> plannedBlastTiles)
    {
        float dangerSeconds = GetDangerSeconds(tile, plannedBlastTiles);
        if (float.IsInfinity(dangerSeconds))
            return 999f;

        return dangerSeconds - arrivalSeconds - settings.dangerReactionSeconds;
    }

    private bool IsTileThreatened(Vector2Int tile, List<Vector2Int> plannedBlastTiles)
    {
        return !float.IsInfinity(GetDangerSeconds(tile, plannedBlastTiles));
    }

    private Vector2 EnforceSafeMovement(
        BattleModeComDifficultySettings settings,
        Vector2Int currentTile,
        Vector2 requestedMove)
    {
        if (requestedMove == Vector2.zero)
            return Vector2.zero;

        if (IsCenteringTowardTileCenter(currentTile, requestedMove))
            return requestedMove;

        if (hasSafeCenterTarget && currentTile == safeCenterTargetTile)
            return requestedMove;

        Vector2Int nextTile = currentTile + DirectionToTile(requestedMove);
        if (nextTile == currentTile)
            return Vector2.zero;

        if (currentAction == BattleModeComActionType.KickBomb)
        {
            LogKickLoadDiagnostic($"allow KickBomb move {currentTile}->{nextTile} input:{FirstMoveDescription(requestedMove)} reason:{currentReason}");
            return requestedMove;
        }

        if (!IsWalkableTile(nextTile, currentTile))
        {
            if (Time.time - lastSafetyHoldLogTime >= SafetyHoldLogIntervalSeconds)
            {
                lastSafetyHoldLogTime = Time.time;
                SetCurrentDecision(
                    BattleModeComActionType.Stopped,
                    Vector2.zero,
                    false,
                    currentTile,
                    $"blocked move to {nextTile}",
                    "none",
                    GetDangerSeconds(currentTile, null),
                    "safetyGate");
            }

            return Vector2.zero;
        }

        float currentDanger = GetDangerSeconds(currentTile, null);
        float nextDanger = GetDangerSeconds(nextTile, null);

        if (!float.IsInfinity(currentDanger))
        {
            if (currentMoveFollowsEscapeRoute &&
                !IsDangerousAt(nextTile, EstimateTraversalSeconds(1), settings, null))
            {
                return requestedMove;
            }

            float nextMargin = GetSurvivalMarginSeconds(nextTile, EstimateTraversalSeconds(1), settings, null);
            float currentMargin = currentDanger - settings.dangerReactionSeconds;
            if (nextMargin + DangerTimingMarginSeconds < currentMargin)
            {
                if (Time.time - lastSafetyHoldLogTime >= SafetyHoldLogIntervalSeconds)
                {
                    lastSafetyHoldLogTime = Time.time;
                    SetCurrentDecision(
                        BattleModeComActionType.Stopped,
                        Vector2.zero,
                        false,
                        currentTile,
                        $"hold safer current {currentMargin:F2}s than {nextTile} {nextMargin:F2}s",
                        "none",
                        currentDanger,
                        "dangerTimingGate");
                }

                return Vector2.zero;
            }

            return requestedMove;
        }

        if (!float.IsInfinity(nextDanger))
        {
            if (Time.time - lastSafetyHoldLogTime >= SafetyHoldLogIntervalSeconds)
            {
                lastSafetyHoldLogTime = Time.time;
                SetCurrentDecision(
                    BattleModeComActionType.Stopped,
                    Vector2.zero,
                    false,
                    currentTile,
                    $"blocked unsafe move to {nextTile}",
                    "none",
                    GetDangerSeconds(currentTile, null),
                    "safetyGate");
            }

            return Vector2.zero;
        }

        return requestedMove;
    }

    private Vector2 ApplyTurnAxisCentering(Vector2Int currentTile, Vector2 requestedMove)
    {
        if (requestedMove == Vector2.zero)
            return Vector2.zero;

        Vector2Int requestedDirection = DirectionToTile(requestedMove);
        if (requestedDirection == Vector2Int.zero)
            return Vector2.zero;

        if (ShouldKeepTargetedMove(currentTile, requestedDirection))
            return requestedMove;

        Vector2 delta = TileToWorld(currentTile) - (Vector2)transform.position;
        float tolerance = Mathf.Max(0.01f, tileSize * TurnAxisCenterTolerance);

        if (requestedDirection.x != 0 && Mathf.Abs(delta.y) > tolerance)
            return delta.y > 0f ? Vector2.up : Vector2.down;

        if (requestedDirection.y != 0 && Mathf.Abs(delta.x) > tolerance)
            return delta.x > 0f ? Vector2.right : Vector2.left;

        return requestedMove;
    }

    private bool ShouldKeepTargetedMove(Vector2Int currentTile, Vector2Int requestedDirection)
    {
        if (!hasCurrentTarget)
            return false;

        if (requestedDirection == Vector2Int.zero)
            return false;

        int currentDistance = Manhattan(currentTile, currentTargetTile);

        Vector2Int nextTile = currentTile + requestedDirection;
        if (Manhattan(nextTile, currentTargetTile) >= currentDistance)
            return false;

        return IsWalkableTile(nextTile, currentTile);
    }

    private Vector2 ApplySafeTileCentering(Vector2Int currentTile, Vector2 requestedMove)
    {
        if (!hasSafeCenterTarget)
            return requestedMove;

        if (IsTileThreatened(currentTile, null))
            return requestedMove;

        if (currentTile != safeCenterTargetTile)
            return requestedMove;

        if (IsCenteredOnTile(currentTile))
        {
            hasSafeCenterTarget = false;
            return Vector2.zero;
        }

        return GetMoveTowardTileCenter(currentTile);
    }

    private bool IsCenteredOnTile(Vector2Int tile)
    {
        Vector2 delta = TileToWorld(tile) - (Vector2)transform.position;
        float tolerance = Mathf.Max(0.01f, tileSize * SafeTileCenterTolerance);
        return Mathf.Abs(delta.x) <= tolerance && Mathf.Abs(delta.y) <= tolerance;
    }

    private Vector2 GetMoveTowardTileCenter(Vector2Int tile)
    {
        Vector2 delta = TileToWorld(tile) - (Vector2)transform.position;
        float tolerance = Mathf.Max(0.01f, tileSize * SafeTileCenterTolerance);

        if (Mathf.Abs(delta.x) > tolerance && Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            return delta.x > 0f ? Vector2.right : Vector2.left;

        if (Mathf.Abs(delta.y) > tolerance)
            return delta.y > 0f ? Vector2.up : Vector2.down;

        return Vector2.zero;
    }

    private bool IsCenteringTowardTileCenter(Vector2Int tile, Vector2 requestedMove)
    {
        Vector2Int requestedDirection = DirectionToTile(requestedMove);
        if (requestedDirection == Vector2Int.zero)
            return false;

        Vector2 delta = TileToWorld(tile) - (Vector2)transform.position;
        float tolerance = Mathf.Max(0.01f, tileSize * TurnAxisCenterTolerance);

        if (requestedDirection.x != 0)
            return Mathf.Abs(delta.x) > tolerance && Mathf.Sign(delta.x) == requestedDirection.x;

        return Mathf.Abs(delta.y) > tolerance && Mathf.Sign(delta.y) == requestedDirection.y;
    }

    private bool HasOwnUnresolvedBombOrExplosion()
    {
        if (bombController == null)
            return false;

        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded)
                continue;

            if (bomb.Owner == bombController && !bomb.IsControlBomb && !bomb.WasMovedByKickOrPunch)
                return true;
        }

        BombExplosion[] explosions = FindObjectsByType<BombExplosion>(FindObjectsInactive.Exclude);
        for (int i = 0; i < explosions.Length; i++)
        {
            BombExplosion explosion = explosions[i];
            if (explosion != null && explosion.Owner == bombController)
                return true;
        }

        return false;
    }

    private bool IsTileInBlastLineRuntime(Vector2Int origin, Vector2Int tile, int radius)
    {
        return IsTileInBlastLine(origin, tile, radius, BlocksExplosion);
    }

    public static bool DebugIsTileInExplosionLine(
        Vector2Int origin,
        Vector2Int tile,
        int radius,
        ICollection<Vector2Int> blockingTiles)
    {
        return IsTileInBlastLine(origin, tile, radius, t => blockingTiles != null && blockingTiles.Contains(t));
    }

    public static bool DebugCanPlantBombWithEscape(
        Vector2Int plantTile,
        int radius,
        ICollection<Vector2Int> walkableTiles,
        ICollection<Vector2Int> blockingTiles,
        int maxDepth)
    {
        if (walkableTiles == null || !walkableTiles.Contains(plantTile))
            return false;

        List<Vector2Int> plannedBlast = BuildStaticBlastTiles(plantTile, radius, blockingTiles);
        Queue<Vector2Int> queue = new();
        Dictionary<Vector2Int, int> depthByTile = new();
        queue.Enqueue(plantTile);
        depthByTile[plantTile] = 0;

        while (queue.Count > 0)
        {
            Vector2Int tile = queue.Dequeue();
            int depth = depthByTile[tile];

            if (depth > 0 && !plannedBlast.Contains(tile))
                return true;

            if (depth >= maxDepth)
                continue;

            for (int i = 0; i < CardinalTiles.Length; i++)
            {
                Vector2Int next = tile + CardinalTiles[i];
                if (depthByTile.ContainsKey(next))
                    continue;

                if (!walkableTiles.Contains(next))
                    continue;

                depthByTile[next] = depth + 1;
                queue.Enqueue(next);
            }
        }

        return false;
    }

    private static bool IsTileInBlastLine(
        Vector2Int origin,
        Vector2Int tile,
        int radius,
        Func<Vector2Int, bool> blocksExplosion)
    {
        if (radius < 0)
            return false;

        if (tile == origin)
            return true;

        Vector2Int delta = tile - origin;
        bool sameColumn = delta.x == 0 && delta.y != 0;
        bool sameRow = delta.y == 0 && delta.x != 0;

        if (!sameColumn && !sameRow)
            return false;

        int distance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        if (distance > radius)
            return false;

        Vector2Int dir = new(Math.Sign(delta.x), Math.Sign(delta.y));
        for (int step = 1; step < distance; step++)
        {
            Vector2Int check = origin + dir * step;
            if (blocksExplosion != null && blocksExplosion(check))
                return false;
        }

        return true;
    }

    private static List<Vector2Int> BuildStaticBlastTiles(
        Vector2Int origin,
        int radius,
        ICollection<Vector2Int> blockingTiles)
    {
        List<Vector2Int> tiles = new();
        tiles.Add(origin);

        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int dir = CardinalTiles[i];
            for (int step = 1; step <= radius; step++)
            {
                Vector2Int tile = origin + dir * step;
                tiles.Add(tile);

                if (blockingTiles != null && blockingTiles.Contains(tile))
                    break;
            }
        }

        return tiles;
    }

    private Vector3Int WorldToCell(Tilemap tilemap, Vector2Int tile)
    {
        return tilemap.WorldToCell(TileToWorld(tile));
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

    private int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private int CountOpenNeighbors(Vector2Int tile)
    {
        int count = 0;
        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            if (IsWalkableTile(tile + CardinalTiles[i], tile))
                count++;
        }

        return count;
    }

    private float EstimateTraversalSeconds(int depth)
    {
        if (movement == null)
            return depth * 0.25f;

        float tilesPerSecond = Mathf.Max(1f, movement.speed);
        return depth / tilesPerSecond;
    }

    private static Vector2 TileDirectionToVector(Vector2Int direction)
    {
        if (direction == Vector2Int.up)
            return Vector2.up;

        if (direction == Vector2Int.down)
            return Vector2.down;

        if (direction == Vector2Int.left)
            return Vector2.left;

        if (direction == Vector2Int.right)
            return Vector2.right;

        return Vector2.zero;
    }

    private static Vector2Int DirectionToTile(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            return direction.x > 0f ? Vector2Int.right : Vector2Int.left;

        if (Mathf.Abs(direction.y) > 0f)
            return direction.y > 0f ? Vector2Int.up : Vector2Int.down;

        return Vector2Int.zero;
    }

    private static string FirstMoveDescription(Vector2 move)
    {
        if (move == Vector2.up)
            return "MoveUp";

        if (move == Vector2.down)
            return "MoveDown";

        if (move == Vector2.left)
            return "MoveLeft";

        if (move == Vector2.right)
            return "MoveRight";

        return "none";
    }

    private static string AppendInput(string current, string next)
    {
        if (string.IsNullOrWhiteSpace(next) || next == "none")
            return string.IsNullOrWhiteSpace(current) ? "none" : current;

        if (string.IsNullOrWhiteSpace(current) || current == "none")
            return next;

        if (current.Contains(next, StringComparison.Ordinal))
            return current;

        return current + "+" + next;
    }

    private string FormatDanger(float seconds)
    {
        if (float.IsInfinity(seconds))
            return "safe";

        if (seconds <= 0f)
            return "now";

        return $"{seconds:F2}s";
    }

    private BattleModeComputerLevel ResolveDifficulty()
    {
        return BattleModeRules.Instance != null
            ? BattleModeRules.Instance.CurrentComputerLevel
            : SaveSystem.GetBattleModeComputerLevel();
    }

    private void SetMovementInput(Vector2 move)
    {
        PlayerInputManager input = PlayerInputManager.Instance;
        if (input == null)
            return;

        PlayerAction heldAction = PlayerAction.Start;
        bool hasMove = false;

        if (Mathf.Abs(move.x) > Mathf.Abs(move.y))
        {
            if (move.x > 0f)
            {
                heldAction = PlayerAction.MoveRight;
                hasMove = true;
            }
            else if (move.x < 0f)
            {
                heldAction = PlayerAction.MoveLeft;
                hasMove = true;
            }
        }
        else if (Mathf.Abs(move.y) > 0f)
        {
            if (move.y > 0f)
            {
                heldAction = PlayerAction.MoveUp;
                hasMove = true;
            }
            else if (move.y < 0f)
            {
                heldAction = PlayerAction.MoveDown;
                hasMove = true;
            }
        }

        for (int i = 0; i < MovementActions.Length; i++)
            input.SetSyntheticHeld(playerId, MovementActions[i], hasMove && MovementActions[i] == heldAction);
    }

    private void Tap(PlayerAction action)
    {
        PlayerInputManager input = PlayerInputManager.Instance;
        if (input != null)
            input.TapSynthetic(playerId, action);
    }

    private void ClearSyntheticInputs()
    {
        PlayerInputManager input = PlayerInputManager.Instance;
        if (input != null)
            input.ClearSyntheticPlayer(playerId);
    }

    private static bool IsBattleModeScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return sceneName.StartsWith("BattleMode_", StringComparison.OrdinalIgnoreCase);
    }
}
