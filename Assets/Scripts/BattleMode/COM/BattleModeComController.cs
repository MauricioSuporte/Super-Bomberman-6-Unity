using System;
using System.Collections.Generic;
using System.Text;
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
    private const float BehaviorProgressDistance = 0.08f;
    private const float BehaviorNoProgressSeconds = 0.75f;
    private const float BehaviorStoppedDangerSeconds = 0.5f;
    private const float BehaviorOscillationWindowSeconds = 1.5f;
    private const float BehaviorRepeatLogSeconds = 0.75f;
    private static readonly bool EnableEscapeAbilityChanceDiagnostics = false;
    private static readonly bool EnableDeathDiagnostics = false;
    private static readonly bool EnablePostPlantEscapeDiagnostics = false;
    private static readonly bool EnableKickBombRiskDiagnostics = false;

    // Log cirúrgico de ExecuteEscape — desativado enquanto investigamos o patrol.
    private static readonly bool EnableEscapeDiagnostics = false;
    private const float EscapeLogIntervalSeconds = 0.25f;
    private float lastEscapeLogTime = -10f;
    private string lastEscapeLogKey = string.Empty;

    // Log de patrol/wander — mostra tile atual, wander target, direção tomada e motivo.
    private static readonly bool EnablePatrolDiagnostics = false;
    private const float PatrolLogIntervalSeconds = 1.0f;
    private float lastPatrolLogTime = -10f;

    // Chain-bomb surgical log: only the decision points needed to explain why
    // COM does or does not execute a line/corner chain explosion plan.
    private static readonly bool EnableChainBombDiagnostics = false;

    // Diagnóstico cirúrgico de oscilação: loga reversões de decisão e do input aplicado
    // (com o pipeline de pós-processamento) para identificar qual estágio inverte a direção.
    private static readonly bool EnableOscillationDiagnostics = false;

    // Watchdog de comportamento ([BattleCOMBehavior]) — desativado durante a
    // investigação da fuga com BombPass para reduzir ruído no console.
    private static readonly bool EnableBehaviorDiagnostics = false;

    // Master switch do [BattleCOMDecisionTrace] (o campo serializado
    // debugDecisionTrace continua existindo, mas este flag tem precedência).
    private static readonly bool EnableDecisionTraceDiagnostics = false;
    private Vector2Int oscDiagLastDecisionDir;
    private float oscDiagLastDecisionTime = -10f;
    private string oscDiagLastDecisionReason = string.Empty;
    private string oscDiagLastDecisionRoute = string.Empty;
    private Vector2Int oscDiagLastDecisionTarget;
    private string oscDiagCurrentRoute = string.Empty;
    private Vector2Int oscDiagLastAppliedDir;
    private float oscDiagLastAppliedTime = -10f;
    private float oscDiagLastFlipLogTime = -10f;
    private const int ChainBombDiagnosticPlayerIdFilter = 0; // 0 = todos os jogadores
    private const float ChainBombDiagLogIntervalSeconds = 0.45f;
    private const int ChainBombRejectSampleLimit = 4;
    private const int SecondBombRejectSampleLimit = 6;
    private float lastChainBombLogTime = -10f;
    private string lastChainBombLogKey = string.Empty;

    private const int PostPlantDiagnosticPlayerIdFilter = 5;
    private const float KickBombRiskLogIntervalSeconds = 0.25f;
    private const string BattleMode3SceneName = "BattleMode_3";

    private const float BombTapCooldownSeconds = 0.35f;
    private const float ActionATapCooldownSeconds = 0.08f;
    private const float ControlBombTapCooldownSeconds = 0.35f;
    private const float PunchTapCooldownSeconds = 0.15f;
    private const float SafetyHoldLogIntervalSeconds = 0.5f;
    private const float SafeTileCenterTolerance = 0.08f;
    private const float TurnAxisCenterTolerance = 0.045f;

    // Histerese da centralização de eixo: depois de considerado "centrado", só volta a
    // centralizar se o desvio passar de tolerance * este multiplicador. Sem isso, o
    // corner-assist do MovementController empurra o personagem ~0.06 para fora da zona
    // morta (0.045) a cada frame e a IA oscila esquerda/direita sem nunca virar.
    private const float TurnAxisCenteringExitMultiplier = 3f;
    private Vector2Int turnAxisCenteredKeepDir = Vector2Int.zero;

    // Detecção de overshoot da centralização: o passo de movimento por frame (~0.125)
    // é maior que a zona morta inteira (2 * 0.045), então o personagem pula por cima
    // do centro sem nunca ficar "dentro da tolerância". Guardamos a direção e o sinal
    // do desvio do último frame de centralização; se o sinal inverter, cruzamos o
    // centro e consideramos centrado.
    private Vector2Int turnAxisCenteringDir = Vector2Int.zero;
    private int turnAxisCenteringSign;
    private const float DangerTimingMarginSeconds = 0.08f;
    private const float SuddenDeathUnsafeLeadSeconds = 1f;
    private const int ItemPriorityDistance = 7;
    private const int NearbyItemFarmBlockDistance = 5;
    private const int MaxDecisionBufferEntries = 32;
    private const float PersonalityWeightJitter = 0.18f;
    private const float ItemTargetJitter = 0.75f;
    private const float FarmTargetJitter = 0.5f;
    private const float ChainBombFuseSafetyMarginSeconds = 0.25f;
    private const int ChainBombMinimumEscapeBranches = 2;
    private const int AdditionalBombMinimumEscapeBranches = 2;
    private const float ChainBombMaxPlantingSeconds = 2.0f;
    private const float OwnChainPlanChance = 0.35f;
    private const int OwnChainSecondBombDistance = 2;
    private const float OwnChainPlanTimeoutSeconds = 2.25f;
    private const float OwnChainFirstFusePlantWindowSeconds = 1.5f;
    private const float OwnChainUnsafeMoveMarginSeconds = 0.45f;
    private const float PostPlantEmergencyEscapeMarginSeconds = 0.35f;
    private const float AbilityDecisionLogIntervalSeconds = 0.35f;

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
    [SerializeField] private bool debugDecisionTrace = true;
    [SerializeField] private int decisionTracePlayerIdFilter = 0;
    [SerializeField] private bool debugAbilityDecisionTrace = true;
    [SerializeField] private int abilityDecisionTracePlayerIdFilter = 0;

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
    private CharacterHealth health;
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
    private float lastActionATapTime = -10f;
    private float lastControlTapTime = -10f;
    private float lastPunchTapTime = -10f;
    private BattleModeComActionType currentAction = BattleModeComActionType.Stopped;
    private Vector2 currentMoveInput;
    private bool currentHoldActionA;
    private Vector2Int currentTargetTile;
    private bool hasCurrentTarget;
    private string currentReason = "startup";
    private string currentInputDescription = "none";
    private float lastSafetyHoldLogTime = -10f;
    private float chainBombPlantingStartedTime = -10f;
    private bool walkToChainCommitted;
    private Vector2Int walkToChainCommittedTile;
    private float walkToChainCommittedTime;
    private const float WalkToChainCommitTimeoutSeconds = 2.5f;
    private bool ownChainSeekCommitted;
    private Vector2Int ownChainSeekJunction;
    private float ownChainSeekCommittedTime;
    private bool ownChainPlanActive;
    private Vector2Int ownChainOriginTile;
    private Vector2Int ownChainSecondTile;
    private Vector2Int ownChainLastPlantedTile;
    private Vector2Int ownChainLineDirection;
    private int ownChainPlantedCount;
    private bool ownChainEscapeOnly;
    private float ownChainPlanStartedTime = -10f;
    private Vector2Int safeCenterTargetTile;
    private bool hasSafeCenterTarget;
    private bool currentMoveFollowsEscapeRoute;
    private bool initialized;
    private int personalitySeed;
    private int decisionSerial;
    private int abilitySystemVersion = -1;
    private int lastKickLoadDiagnosticFrame = -9999;
    private bool subscribedToHealthDeath;
    private bool subscribedToMovementDeath;
    private int lastDeathDiagnosticFrame = -1;
    private float escapeAbilityChanceCacheTime = -10f;
    private bool escapeAbilityChanceCacheResult;
    private float escapeAbilityChanceCacheRoll;
    private string escapeAbilityChanceCacheThreatKey = string.Empty;
    private BattleModeComputerLevel escapeAbilityChanceCacheDifficulty = BattleModeComputerLevel.Normal;
    private string lastEscapeAbilityChanceTrace = "not rolled";
    private float lastEscapeAbilityChanceLogTime = -10f;
    private string lastEscapeAbilityChanceLogKey = string.Empty;
    private bool postPlantEscapeWatchActive;
    private float postPlantEscapeWatchStartedTime = -10f;
    private float lastPostPlantEscapeLogTime = -10f;
    private string lastPostPlantEscapeLogKey = string.Empty;
    private float lastKickBombRiskLogTime = -10f;
    private string lastKickBombRiskLogKey = string.Empty;
    private float lastDecisionTraceLogTime = -10f;
    private string lastDecisionTraceLogKey = string.Empty;
    private float lastAbilityDecisionTraceLogTime = -10f;
    private string lastAbilityDecisionTraceLogKey = string.Empty;
    private float lastStage3PlantDiagnosticTime = -10f;
    private string lastStage3PlantDiagnosticKey = string.Empty;
    private bool abilityDecisionEvaluatedThisThink;
    private bool behaviorStartLogged;
    private Vector2 behaviorLastProgressPosition;
    private float behaviorLastProgressTime = -10f;
    private float behaviorStoppedDangerStartedTime = -1f;
    private float behaviorLastLogTime = -10f;
    private string behaviorLastLogKey = string.Empty;
    private Vector2Int behaviorLastTile;
    private Vector2Int behaviorPreviousTile;
    private bool behaviorHasLastTile;
    private bool behaviorWasRequestingMovement;
    private int behaviorOscillationCount;
    private float behaviorOscillationStartedTime = -10f;

    // Committed escape: quando TryBuildOwnPendingEscapeRoute encontra um target válido,
    // gravamos ele aqui. TryBuildOwnPendingSafetyMove usa BFS em direção a ele antes de
    // fazer a avaliação greedy de 1 passo, evitando oscilação entre dois tiles adjacentes.
    private Vector2Int committedEscapeTarget = new Vector2Int(int.MinValue, int.MinValue);
    private float committedEscapeExpiresTime = -10f;
    private const float CommittedEscapeLifetimeSeconds = 2.5f;

    // Wander target: tile distante escolhido via BFS para evitar que a IA fique presa no canto.
    // Substituído quando atingido, inválido, ou após o timeout.
    private Vector2Int wanderTarget = new Vector2Int(int.MinValue, int.MinValue);
    private float wanderTargetExpiresTime = -10f;
    private const float WanderTargetLifetimeSeconds = 6f;
    private const int WanderBfsDepth = 14;
    private const int WanderMinDistance = 5;

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
        public bool TapActionA;
        public bool HoldActionA;
        public bool TapActionB;
        public bool TapActionR;
        public bool TapActionC;
        public bool UsesEscapeAbilityChance;
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
        ResetBehaviorDiagnostics();
    }

    private void OnDestroy()
    {
        UnsubscribeFromHealthDeath();
        UnsubscribeFromMovementDeath();
    }

    private void CacheReferences()
    {
        if (identity == null)
            TryGetComponent(out identity);

        if (movement == null)
            TryGetComponent(out movement);

        if (bombController == null)
            TryGetComponent(out bombController);

        if (health == null)
            TryGetComponent(out health);

        SubscribeToHealthDeath();
        SubscribeToMovementDeath();

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

    private void SubscribeToHealthDeath()
    {
        if (health == null || subscribedToHealthDeath)
            return;

        health.Died += OnHealthDied;
        subscribedToHealthDeath = true;
    }

    private void UnsubscribeFromHealthDeath()
    {
        if (health == null || !subscribedToHealthDeath)
            return;

        health.Died -= OnHealthDied;
        subscribedToHealthDeath = false;
    }

    private void SubscribeToMovementDeath()
    {
        if (movement == null || subscribedToMovementDeath)
            return;

        movement.Died += OnMovementDied;
        subscribedToMovementDeath = true;
    }

    private void UnsubscribeFromMovementDeath()
    {
        if (movement == null || !subscribedToMovementDeath)
            return;

        movement.Died -= OnMovementDied;
        subscribedToMovementDeath = false;
    }

    private void RefreshComAbilities()
    {
        AbilitySystem abilitySystem = EnsureKnownComAbilityScripts();
        int currentVersion = abilitySystem != null ? abilitySystem.Version : -1;

        bool hasDestroyedEntry = false;
        for (int i = 0; i < comAbilities.Count; i++)
        {
            if (!IsComAbilityAlive(comAbilities[i]))
            {
                hasDestroyedEntry = true;
                break;
            }
        }

        if (currentVersion == abilitySystemVersion &&
            comAbilities.Count > 0 &&
            !hasDestroyedEntry)
        {
            return;
        }

        comAbilities.Clear();
        var monos = GetComponents<MonoBehaviour>();
        for (int i = 0; i < monos.Length; i++)
        {
            if (monos[i] != null &&
                monos[i] is IBattleModeComAbility ability &&
                IsComAbilityAlive(ability))
            {
                comAbilities.Add(ability);
            }
        }

        abilitySystemVersion = currentVersion;
    }

    private AbilitySystem EnsureKnownComAbilityScripts()
    {
        TryGetComponent<AbilitySystem>(out var abilitySystem);

        bool persistentKickEnabled = PlayerPersistentStats.GetRuntime(playerId).CanKickBombs;
        bool isCom = SaveSystem.GetBattleModePlayerControlMode(playerId) == BattleModePlayerControlMode.Com;
        if (isCom && BattleModeComStageAbilityLoader.EnsureForActiveStage(gameObject))
            abilitySystemVersion = -2;

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

        bool kickEnabled =
            abilitySystem != null &&
            abilitySystem.IsEnabled(BombKickAbility.AbilityId);
        bool yellowLouieKickEnabled =
            abilitySystem != null &&
            abilitySystem.IsEnabled(YellowLouieKickAbility.AbilityId);

        BattleModeComKickBombAbility exactKickCom = FindExactKickBombComAbility();
        TryGetComponent<BattleModeComYellowLouieKickAbility>(out var yellowLouieKickCom);

        if ((!isCom || !yellowLouieKickEnabled) && yellowLouieKickCom != null)
        {
            Destroy(yellowLouieKickCom);
            yellowLouieKickCom = null;
            abilitySystemVersion = -2;
        }

        if (isCom &&
            yellowLouieKickEnabled &&
            yellowLouieKickCom == null)
        {
            if (exactKickCom != null)
            {
                Destroy(exactKickCom);
                exactKickCom = null;
            }

            yellowLouieKickCom = gameObject.AddComponent<BattleModeComYellowLouieKickAbility>();
            abilitySystemVersion = -2;
        }

        if (isCom &&
            kickEnabled &&
            !yellowLouieKickEnabled &&
            yellowLouieKickCom == null &&
            exactKickCom == null)
        {
            gameObject.AddComponent<BattleModeComKickBombAbility>();
            abilitySystemVersion = -2;
            LogKickLoadDiagnostic("added BattleModeComKickBombAbility");
        }

        if ((!isCom || !kickEnabled || yellowLouieKickEnabled) && exactKickCom != null)
        {
            Destroy(exactKickCom);
            abilitySystemVersion = -2;
        }

        // HazardAwareness é sempre ativa — não depende de power-up.
        // Torna a IA ciente de raios ampliados (HasFullFire, HasPowerBomb) de bombas inimigas.
        if (isCom && !TryGetComponent<BattleModeComHazardAwarenessAbility>(out _))
        {
            gameObject.AddComponent<BattleModeComHazardAwarenessAbility>();
            abilitySystemVersion = -2;
        }

        // RubberBombAwareness é sempre ativa — qualquer RubberBomb chutada (de
        // qualquer jogador) ricocheteia e pode voltar na direção da IA.
        if (isCom && !TryGetComponent<BattleModeComRubberBombAwarenessAbility>(out _))
        {
            gameObject.AddComponent<BattleModeComRubberBombAwarenessAbility>();
            abilitySystemVersion = -2;
        }

        // PierceBombAwareness é sempre ativa — explosões pierce (de qualquer
        // jogador, inclusive da própria IA) atravessam destrutíveis e acionam
        // cadeias que o modelo de perigo nativo não enxerga.
        if (isCom && !TryGetComponent<BattleModeComPierceBombAwarenessAbility>(out _))
        {
            gameObject.AddComponent<BattleModeComPierceBombAwarenessAbility>();
            abilitySystemVersion = -2;
        }

        // ControlBombAwareness é sempre ativa — ControlBombs adversárias podem
        // explodir a qualquer momento; a zona delas é perigo máximo.
        if (isCom && !TryGetComponent<BattleModeComControlBombAwarenessAbility>(out _))
        {
            gameObject.AddComponent<BattleModeComControlBombAwarenessAbility>();
            abilitySystemVersion = -2;
        }

        // MagnetBombAwareness é sempre ativa — magnet bombs adversárias perseguem
        // a IA quando alinhada na mesma linha/coluna; quebrar o alinhamento é a defesa.
        if (isCom && !TryGetComponent<BattleModeComMagnetBombAwarenessAbility>(out _))
        {
            gameObject.AddComponent<BattleModeComMagnetBombAwarenessAbility>();
            abilitySystemVersion = -2;
        }

        // TankThreatAwareness is always active: it predicts ready enemy tank
        // firing lanes, live projectiles, and their radius-1 impact explosions.
        if (isCom && !TryGetComponent<BattleModeComTankThreatAwarenessAbility>(out _))
        {
            gameObject.AddComponent<BattleModeComTankThreatAwarenessAbility>();
            abilitySystemVersion = -2;
        }

        // RedLouie stun punch: condicionado à ability do mount (sem flag persistente,
        // mesmo padrão do YellowLouieKick).
        bool redLouieStunEnabled =
            abilitySystem != null &&
            abilitySystem.IsEnabled(RedLouiePunchStunAbility.AbilityId);
        TryGetComponent<BattleModeComRedLouiePunchStunAbility>(out var redLouieStunCom);
        if (isCom && redLouieStunEnabled && redLouieStunCom == null)
        {
            gameObject.AddComponent<BattleModeComRedLouiePunchStunAbility>();
            abilitySystemVersion = -2;
        }
        else if ((!isCom || !redLouieStunEnabled) && redLouieStunCom != null)
        {
            Destroy(redLouieStunCom);
            abilitySystemVersion = -2;
        }

        // GreenLouie dash: condicionado à ability do mount, mesmo padrão.
        bool greenLouieDashEnabled =
            abilitySystem != null &&
            abilitySystem.IsEnabled(GreenLouieDashAbility.AbilityId);
        TryGetComponent<BattleModeComGreenLouieDashAbility>(out var greenLouieDashCom);
        if (isCom && greenLouieDashEnabled && greenLouieDashCom == null)
        {
            gameObject.AddComponent<BattleModeComGreenLouieDashAbility>();
            abilitySystemVersion = -2;
        }
        else if ((!isCom || !greenLouieDashEnabled) && greenLouieDashCom != null)
        {
            Destroy(greenLouieDashCom);
            abilitySystemVersion = -2;
        }

        // BlackLouie dash push: condicionado à ability do mount, mesmo padrão.
        bool blackLouiePushEnabled =
            abilitySystem != null &&
            abilitySystem.IsEnabled(BlackLouieDashPushAbility.AbilityId);
        TryGetComponent<BattleModeComBlackLouieDashPushAbility>(out var blackLouiePushCom);
        if (isCom && blackLouiePushEnabled && blackLouiePushCom == null)
        {
            gameObject.AddComponent<BattleModeComBlackLouieDashPushAbility>();
            abilitySystemVersion = -2;
        }
        else if ((!isCom || !blackLouiePushEnabled) && blackLouiePushCom != null)
        {
            Destroy(blackLouiePushCom);
            abilitySystemVersion = -2;
        }

        // PinkLouie jump: condicionado à ability do mount, mesmo padrão.
        bool pinkLouieJumpEnabled =
            abilitySystem != null &&
            abilitySystem.IsEnabled(PinkLouieJumpAbility.AbilityId);
        TryGetComponent<BattleModeComPinkLouieJumpAbility>(out var pinkLouieJumpCom);
        if (isCom && pinkLouieJumpEnabled && pinkLouieJumpCom == null)
        {
            gameObject.AddComponent<BattleModeComPinkLouieJumpAbility>();
            abilitySystemVersion = -2;
        }
        else if ((!isCom || !pinkLouieJumpEnabled) && pinkLouieJumpCom != null)
        {
            Destroy(pinkLouieJumpCom);
            abilitySystemVersion = -2;
        }

        // Mole drill: preventive escape after its vulnerable startup phase.
        bool moleDrillEnabled =
            abilitySystem != null &&
            abilitySystem.IsEnabled(MoleMountDrillAbility.AbilityId);
        TryGetComponent<BattleModeComMoleDrillEscapeAbility>(out var moleDrillCom);
        if (isCom && moleDrillEnabled && moleDrillCom == null)
        {
            gameObject.AddComponent<BattleModeComMoleDrillEscapeAbility>();
            abilitySystemVersion = -2;
        }
        else if ((!isCom || !moleDrillEnabled) && moleDrillCom != null)
        {
            Destroy(moleDrillCom);
            abilitySystemVersion = -2;
        }

        // Tank shot: long-range offense with difficulty-specific cooldown.
        bool tankShootEnabled =
            abilitySystem != null &&
            abilitySystem.IsEnabled(TankMountShootAbility.AbilityId);
        TryGetComponent<BattleModeComTankMountShootAbility>(out var tankShootCom);
        if (isCom && tankShootEnabled && tankShootCom == null)
        {
            gameObject.AddComponent<BattleModeComTankMountShootAbility>();
            abilitySystemVersion = -2;
        }
        else if ((!isCom || !tankShootEnabled) && tankShootCom != null)
        {
            Destroy(tankShootCom);
            abilitySystemVersion = -2;
        }

        // PurpleLouie bomb line: condicionado à ability do mount, mesmo padrão.
        bool purpleLineEnabled =
            abilitySystem != null &&
            abilitySystem.IsEnabled(PurpleLouieBombLineAbility.AbilityId);
        TryGetComponent<BattleModeComPurpleLouieBombLineAbility>(out var purpleLineCom);
        if (isCom && purpleLineEnabled && purpleLineCom == null)
        {
            gameObject.AddComponent<BattleModeComPurpleLouieBombLineAbility>();
            abilitySystemVersion = -2;
        }
        else if ((!isCom || !purpleLineEnabled) && purpleLineCom != null)
        {
            Destroy(purpleLineCom);
            abilitySystemVersion = -2;
        }

        // ControlBomb (uso): condicionado a HasControlBombs, padrão do PunchBomb.
        bool persistentControlEnabled = PlayerPersistentStats.GetRuntime(playerId).HasControlBombs;
        if (persistentControlEnabled)
        {
            if (abilitySystem == null)
                abilitySystem = gameObject.AddComponent<AbilitySystem>();

            if (!abilitySystem.IsEnabled(ControlBombAbility.AbilityId))
                abilitySystem.Enable(ControlBombAbility.AbilityId);
        }

        bool controlEnabled =
            abilitySystem != null &&
            abilitySystem.IsEnabled(ControlBombAbility.AbilityId);
        TryGetComponent<BattleModeComControlBombAbility>(out var controlBombCom);
        if (isCom && controlEnabled && controlBombCom == null)
        {
            gameObject.AddComponent<BattleModeComControlBombAbility>();
            abilitySystemVersion = -2;
            LogControlBombLoadDiagnostic(
                $"ADDED BattleModeComControlBombAbility persistent:{persistentControlEnabled}");
        }
        else if ((!isCom || !controlEnabled) && controlBombCom != null)
        {
            Destroy(controlBombCom);
            abilitySystemVersion = -2;
            LogControlBombLoadDiagnostic(
                $"REMOVED BattleModeComControlBombAbility isCom:{isCom} enabled:{controlEnabled}");
        }

        if (isCom && Time.frameCount - lastControlBombLoadDiagnosticFrame >= 300)
        {
            lastControlBombLoadDiagnosticFrame = Time.frameCount;
            LogControlBombLoadDiagnostic(
                $"load check persistent:{persistentControlEnabled} enabled:{controlEnabled} " +
                $"com:{(controlBombCom != null || (isCom && controlEnabled))}");
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

        bool punchEnabled =
            abilitySystem != null &&
            abilitySystem.IsEnabled(BombPunchAbility.AbilityId);
        TryGetComponent<BattleModeComPunchBombAbility>(out var punchCom);
        if (isCom && punchEnabled && punchCom == null)
        {
            gameObject.AddComponent<BattleModeComPunchBombAbility>();
            abilitySystemVersion = -2;
        }
        else if ((!isCom || !punchEnabled) && punchCom != null)
        {
            Destroy(punchCom);
            abilitySystemVersion = -2;
        }

        bool persistentPowerGloveEnabled = PlayerPersistentStats.GetRuntime(playerId).HasPowerGlove;
        if (persistentPowerGloveEnabled)
        {
            if (abilitySystem == null)
                abilitySystem = gameObject.AddComponent<AbilitySystem>();

            if (!abilitySystem.IsEnabled(PowerGloveAbility.AbilityId))
                abilitySystem.Enable(PowerGloveAbility.AbilityId);
        }

        bool powerGloveEnabled =
            abilitySystem != null &&
            abilitySystem.IsEnabled(PowerGloveAbility.AbilityId);
        TryGetComponent<BattleModeComPowerGloveAbility>(out var powerGloveCom);
        if (isCom && powerGloveEnabled && powerGloveCom == null)
        {
            gameObject.AddComponent<BattleModeComPowerGloveAbility>();
            abilitySystemVersion = -2;
        }
        else if ((!isCom || !powerGloveEnabled) && powerGloveCom != null)
        {
            Destroy(powerGloveCom);
            abilitySystemVersion = -2;
        }

        // BombPass: condicionado a CanPassBombs, espelha o padrão do PunchBomb.
        // Mutuamente exclusivo com BombKick (PlayerPersistentStats já garante isso);
        // pode coexistir com BattleModeComYellowLouieKickAbility.
        bool persistentBombPassEnabled = PlayerPersistentStats.GetRuntime(playerId).CanPassBombs;
        if (persistentBombPassEnabled)
        {
            if (abilitySystem == null)
                abilitySystem = gameObject.AddComponent<AbilitySystem>();

            if (!abilitySystem.IsEnabled(BombPassAbility.AbilityId))
                abilitySystem.Enable(BombPassAbility.AbilityId);
        }

        bool bombPassEnabled =
            abilitySystem != null &&
            abilitySystem.IsEnabled(BombPassAbility.AbilityId);
        TryGetComponent<BattleModeComBombPassAbility>(out var bombPassCom);
        if (isCom && bombPassEnabled && bombPassCom == null)
        {
            gameObject.AddComponent<BattleModeComBombPassAbility>();
            abilitySystemVersion = -2;
            LogBombPassLoadDiagnostic(
                $"ADDED BattleModeComBombPassAbility persistent:{persistentBombPassEnabled}");
        }
        else if ((!isCom || !bombPassEnabled) && bombPassCom != null)
        {
            Destroy(bombPassCom);
            abilitySystemVersion = -2;
            LogBombPassLoadDiagnostic(
                $"REMOVED BattleModeComBombPassAbility isCom:{isCom} enabled:{bombPassEnabled}");
        }

        // DestructiblePass: condicionado a CanPassDestructibles, mesmo padrão do BombPass.
        bool persistentDestructiblePassEnabled =
            PlayerPersistentStats.GetRuntime(playerId).CanPassDestructibles;
        if (persistentDestructiblePassEnabled)
        {
            if (abilitySystem == null)
                abilitySystem = gameObject.AddComponent<AbilitySystem>();

            if (!abilitySystem.IsEnabled(DestructiblePassAbility.AbilityId))
                abilitySystem.Enable(DestructiblePassAbility.AbilityId);
        }

        bool destructiblePassEnabled =
            abilitySystem != null &&
            abilitySystem.IsEnabled(DestructiblePassAbility.AbilityId);
        TryGetComponent<BattleModeComDestructiblePassAbility>(out var destructiblePassCom);
        if (isCom && destructiblePassEnabled && destructiblePassCom == null)
        {
            gameObject.AddComponent<BattleModeComDestructiblePassAbility>();
            abilitySystemVersion = -2;
            LogDestructiblePassLoadDiagnostic(
                $"ADDED BattleModeComDestructiblePassAbility persistent:{persistentDestructiblePassEnabled}");
        }
        else if ((!isCom || !destructiblePassEnabled) && destructiblePassCom != null)
        {
            Destroy(destructiblePassCom);
            abilitySystemVersion = -2;
            LogDestructiblePassLoadDiagnostic(
                $"REMOVED BattleModeComDestructiblePassAbility isCom:{isCom} enabled:{destructiblePassEnabled}");
        }

        if (isCom && Time.frameCount - lastDestructiblePassLoadDiagnosticFrame >= 300)
        {
            lastDestructiblePassLoadDiagnosticFrame = Time.frameCount;
            LogDestructiblePassLoadDiagnostic(
                $"load check persistent:{persistentDestructiblePassEnabled} enabled:{destructiblePassEnabled} " +
                $"com:{(destructiblePassCom != null || (isCom && destructiblePassEnabled))}");
        }

        if (isCom && Time.frameCount - lastBombPassLoadDiagnosticFrame >= 300)
        {
            lastBombPassLoadDiagnosticFrame = Time.frameCount;
            LogBombPassLoadDiagnostic(
                $"load check persistent:{persistentBombPassEnabled} enabled:{bombPassEnabled} " +
                $"com:{(bombPassCom != null || (isCom && bombPassEnabled))}");
        }

        if (isCom && Time.frameCount - lastKickLoadDiagnosticFrame >= 120)
        {
            bool diagnosticKickEnabled =
                abilitySystem != null &&
                abilitySystem.IsEnabled(BombKickAbility.AbilityId);
            bool hasKickCom = TryGetComponent<BattleModeComKickBombAbility>(out _);
            LogKickLoadDiagnostic(
                $"load check persistentKick:{persistentKickEnabled} abilitySystem:{(abilitySystem != null)} " +
                $"kickEnabled:{diagnosticKickEnabled} kickCom:{hasKickCom}");
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

    private int lastBombPassLoadDiagnosticFrame = -9999;
    private int lastDestructiblePassLoadDiagnosticFrame = -9999;
    private int lastControlBombLoadDiagnosticFrame = -9999;

    // Log de cada tap sintético de ActionB/ActionC com o motivo da decisão —
    // identifica QUEM está pressionando o botão quando o comportamento parece
    // spam (ex.: ActionC repetido do PurpleLouie).
    private static readonly bool EnableInputTapDiagnostics = true;

    private void LogInputTapDiagnostic(
        string action,
        string reason,
        BattleModeComActionType actionType,
        Vector2Int myTile)
    {
        if (!EnableInputTapDiagnostics || !IsBattleModeScene())
            return;

        Debug.LogWarning(
            $"[BattleCOMInput][P{playerId}] TAP {action} tile:{myTile} decisionAction:{actionType} " +
            $"reason:{(string.IsNullOrEmpty(reason) ? "none" : reason)}",
            this);
    }

    private void LogControlBombLoadDiagnostic(string message)
    {
        if (!BattleModeComControlBombAbility.EnableControlBombDiagnostics)
            return;

        if (!IsBattleModeScene())
            return;

        if (BattleModeComControlBombAbility.DiagnosticPlayerIdFilter != 0 &&
            playerId != BattleModeComControlBombAbility.DiagnosticPlayerIdFilter)
            return;

        Debug.LogWarning($"[BattleCOMControlBomb][P{playerId}] LOAD {message}", this);
    }

    private void LogDestructiblePassLoadDiagnostic(string message)
    {
        if (!BattleModeComDestructiblePassAbility.EnableDestructiblePassDiagnostics)
            return;

        if (!IsBattleModeScene())
            return;

        if (BattleModeComDestructiblePassAbility.DiagnosticPlayerIdFilter != 0 &&
            playerId != BattleModeComDestructiblePassAbility.DiagnosticPlayerIdFilter)
            return;

        Debug.LogWarning($"[BattleCOMDestructiblePass][P{playerId}] LOAD {message}", this);
    }

    private void LogBombPassLoadDiagnostic(string message)
    {
        if (!BattleModeComBombPassAbility.EnableBombPassEscapeDiagnostics)
            return;

        if (!IsBattleModeScene())
            return;

        if (BattleModeComBombPassAbility.DiagnosticPlayerIdFilter != 0 &&
            playerId != BattleModeComBombPassAbility.DiagnosticPlayerIdFilter)
            return;

        Debug.LogWarning($"[BattleCOMBombPass][P{playerId}] LOAD {message}", this);
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
            ResetBehaviorDiagnostics();
            enabled = false;
            return;
        }

        enabled = true;
        LogBehaviorStart();
    }

    private void Update()
    {
        CacheReferences();

        if (!IsReadyToThink())
        {
            SetMovementInput(Vector2.zero);
            SetActionAHeld(currentHoldActionA);
            return;
        }

        BattleModeComputerLevel difficulty = ResolveDifficulty();
        BattleModeComDifficultySettings settings = BattleModeComDifficultySettings.For(difficulty);
        Vector2Int myTile = WorldToTile(transform.position);
        float currentDangerSeconds = GetDangerSeconds(myTile, null);
        bool inDanger = IsTileThreatened(myTile, null);
        if (TryGetComponent(out BattleModeComTankThreatAwarenessAbility tankAwareness) &&
            tankAwareness.HasImmediateThreat(myTile, out float tankThreatSeconds))
        {
            inDanger = true;
            currentDangerSeconds = Mathf.Min(currentDangerSeconds, tankThreatSeconds);
        }

        if (inDanger || Time.time >= nextDecisionTime)
        {
            Think(settings, myTile, currentDangerSeconds, inDanger);
            nextDecisionTime = Time.time + (inDanger ? settings.dangerDecisionInterval : settings.decisionInterval);
        }

        Vector2 oscDiagDecidedMove = currentMoveInput;
        currentMoveInput = ApplySafeTileCentering(myTile, currentMoveInput);
        Vector2 oscDiagAfterSafeCenter = currentMoveInput;
        currentMoveInput = ApplyTurnAxisCentering(myTile, currentMoveInput);
        Vector2 oscDiagAfterTurnAxis = currentMoveInput;
        if (TryGetComponent(out BattleModeComTankThreatAwarenessAbility committedTankAwareness) &&
            committedTankAwareness.TryGetCommittedDodgeMove(
                out Vector2 committedTankMove,
                out Vector2Int committedTankTarget))
        {
            currentMoveInput = committedTankMove;
            hasCurrentTarget = true;
            currentTargetTile = committedTankTarget;
            hasSafeCenterTarget = true;
            safeCenterTargetTile = committedTankTarget;
        }
        currentMoveInput = EnforceSafeMovement(settings, myTile, currentMoveInput);

        if (EnableOscillationDiagnostics)
        {
            Vector2Int appliedDir = DirectionToTile(currentMoveInput);
            if (appliedDir != Vector2Int.zero &&
                oscDiagLastAppliedDir == -appliedDir &&
                Time.time - oscDiagLastAppliedTime < 0.6f &&
                Time.time - oscDiagLastFlipLogTime > 0.1f)
            {
                oscDiagLastFlipLogTime = Time.time;
                Vector2 diagCenterOffset = (Vector2)transform.position - TileToWorld(myTile);
                Vector2Int firstStep = DirectionToTile(oscDiagDecidedMove);
                float nextDanger = firstStep == Vector2Int.zero
                    ? float.PositiveInfinity
                    : GetDangerSeconds(myTile + firstStep, null);
                Debug.LogWarning(
                    $"[BattleCOMOscDiag][P{playerId}] event:INPUT_FLIP frame:{Time.frameCount} t:{Time.time:F2} " +
                    $"tile:{myTile} pos:{transform.position} centerOffset:{diagCenterOffset} " +
                    $"danger:{FormatDanger(currentDangerSeconds)} nextDanger:{FormatDanger(nextDanger)} " +
                    $"prevApplied:{oscDiagLastAppliedDir} age:{Time.time - oscDiagLastAppliedTime:F2}s " +
                    $"pipeline[decided:{FirstMoveDescription(oscDiagDecidedMove)} " +
                    $"safeCenter:{FirstMoveDescription(oscDiagAfterSafeCenter)} " +
                    $"turnAxis:{FirstMoveDescription(oscDiagAfterTurnAxis)} " +
                    $"enforce:{FirstMoveDescription(currentMoveInput)}] " +
                    $"action:{currentAction} reason:{currentReason} route:{oscDiagCurrentRoute} " +
                    $"target:{(hasCurrentTarget ? currentTargetTile.ToString() : "none")} " +
                    $"safeCenterTarget:{(hasSafeCenterTarget ? safeCenterTargetTile.ToString() : "none")} " +
                    $"followsEscape:{currentMoveFollowsEscapeRoute}",
                    this);
            }

            if (appliedDir != Vector2Int.zero)
            {
                oscDiagLastAppliedDir = appliedDir;
                oscDiagLastAppliedTime = Time.time;
            }
        }

        SetMovementInput(currentMoveInput);
        TrackBehaviorDiagnostics(myTile, currentDangerSeconds);
        SetActionAHeld(currentHoldActionA);
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
        abilityDecisionEvaluatedThisThink = false;
        EnsurePersonalitySeed();

        if (inDanger)
        {
            if (TryGetComponent(out BattleModeComTankThreatAwarenessAbility tankAwareness) &&
                tankAwareness.HasImmediateThreat(myTile, out float immediateTankSeconds) &&
                tankAwareness.TryBuildEmergencyDecision(
                    settings,
                    this,
                    myTile,
                    immediateTankSeconds,
                    out BattleModeComAbilityDecision tankEmergency))
            {
                ExecuteSelectedCandidate(
                    settings,
                    myTile,
                    currentDangerSeconds,
                    ToCandidateAction(tankEmergency),
                    "tankEmergency");
                return;
            }

            if (TryContinueDangerEscapeRoute(settings, myTile, currentDangerSeconds))
                return;

            if (TryBuildStageAbilityEmergencyCandidate(
                    settings,
                    myTile,
                    currentDangerSeconds,
                    out CandidateAction stageEmergency))
            {
                ExecuteSelectedCandidate(
                    settings,
                    myTile,
                    currentDangerSeconds,
                    stageEmergency,
                    "stageAbility");
                return;
            }

            // Habilidades (ex.: chute ofensivo de bomba) podem CONTINUAR uma jogada já iniciada
            // mesmo sob perigo — tipicamente a IA recuou 1 tile e está dentro do raio da própria
            // bomba, mas a jogada certa é voltar e chutá-la no adversário (removendo a bomba da
            // lane e ficando segura), não fugir. Damos prioridade a isso antes da fuga.
            if (TryBuildAbilityEmergencyCandidate(settings, myTile, currentDangerSeconds, out CandidateAction abilityEmergency))
            {
                ExecuteSelectedCandidate(settings, myTile, currentDangerSeconds, abilityEmergency, "ability");
                return;
            }

            // Se comprometida a caminhar até um tile de blast inimigo e chegou: planta antes de fugir.
            if (ownChainPlanActive &&
                TryBuildOwnChainContinuationCandidate(settings, myTile, currentDangerSeconds, out CandidateAction ownChainDanger))
            {
                if (EnableChainBombDiagnostics)
                {
                    Debug.Log(
                        $"[BattleCOMOwnChain][P{playerId}] DANGER_CONTINUE tile:{myTile} " +
                        $"target:{ownChainDanger.TargetTile} move:{FirstMoveDescription(ownChainDanger.FirstMove)} " +
                        $"reason:{ownChainDanger.Reason}");
                }

                ExecuteSelectedCandidate(settings, myTile, currentDangerSeconds, ownChainDanger, "ownChain");
                return;
            }

            if (walkToChainCommitted &&
                myTile == walkToChainCommittedTile &&
                Time.time - walkToChainCommittedTime < WalkToChainCommitTimeoutSeconds &&
                TryBuildCommittedWalkToChainPlantCandidate(settings, myTile, out CandidateAction dangerCommittedPlant))
            {
                if (EnableChainBombDiagnostics)
                {
                    Debug.Log(
                        $"[BattleCOMWalkChain][P{playerId}] COMMIT_PLANT_DANGER myTile:{myTile} " +
                        $"committedTile:{walkToChainCommittedTile} age:{Time.time - walkToChainCommittedTime:F2}s " +
                        $"escape:{FirstMoveDescription(dangerCommittedPlant.FirstMove)}");
                }

                ExecuteSelectedCandidate(settings, myTile, currentDangerSeconds, dangerCommittedPlant, "walkToChain");
                walkToChainCommitted = false;
                return;
            }

            ExecuteEscape(settings, myTile, currentDangerSeconds);
            return;
        }

        if (HasOwnUnresolvedBombOrExplosion())
        {
            if (chainBombPlantingStartedTime < 0f)
            {
                chainBombPlantingStartedTime = Time.time;
                LogPostPlantEscapeDiagnostic(
                    "OWN_BOMB_START",
                    myTile,
                    currentDangerSeconds,
                    "own unresolved bomb/explosion entered post-plant flow",
                    force: true);
            }

            bool chainPlantingTimeExpired = Time.time - chainBombPlantingStartedTime > ChainBombMaxPlantingSeconds;

            if (ownChainPlanActive &&
                TryBuildOwnChainContinuationCandidate(settings, myTile, currentDangerSeconds, out CandidateAction ownChain))
            {
                ExecuteSelectedCandidate(settings, myTile, currentDangerSeconds, ownChain, "ownChain");
                return;
            }

            // Chain bomb tem prioridade sobre fuga simples: só tentamos fugir depois de verificar
            // se existe oportunidade de plantar mais uma bomba em chain. Isso preserva as
            // estratégias de kick-chain e chain-explosion que foram quebradas quando o Codex
            // colocou o escape route antes do chain candidate.
            if (!chainPlantingTimeExpired &&
                TryBuildChainBombCandidate(settings, myTile, currentDangerSeconds, onlyCurrentTile: false, out CandidateAction chain))
            {
                LogPostPlantEscapeDiagnostic(
                    "CHAIN_CONTINUE",
                    myTile,
                    currentDangerSeconds,
                    $"elapsed:{Time.time - chainBombPlantingStartedTime:F2}s action:{chain.Action} target:{chain.TargetTile} " +
                    $"move:{FirstMoveDescription(chain.FirstMove)} tapBomb:{chain.TapBomb} reason:{chain.Reason}");
                ExecuteSelectedCandidate(settings, myTile, currentDangerSeconds, chain, "chainBomb");
                return;
            }

            if (chainPlantingTimeExpired)
            {
                LogPostPlantEscapeDiagnostic(
                    "CHAIN_TIMEOUT",
                    myTile,
                    currentDangerSeconds,
                    $"elapsed:{Time.time - chainBombPlantingStartedTime:F2}s max:{ChainBombMaxPlantingSeconds:F2}s");
            }
            else
            {
                LogPostPlantEscapeDiagnostic(
                    "CHAIN_NO_CANDIDATE",
                    myTile,
                    currentDangerSeconds,
                    $"elapsed:{Time.time - chainBombPlantingStartedTime:F2}s rejected:{FormatRejectedForLog()}");
            }

            // Se a IA está segura e tem bombas sobrando, tenta plantar uma bomba independente
            // (sem precisar de chain link) em posição que atinja inimigo ou bloco destrutível.
            if (float.IsInfinity(currentDangerSeconds) &&
                TryBuildSecondBombCandidate(settings, myTile, currentDangerSeconds, out CandidateAction secondBomb))
            {
                LogPostPlantEscapeDiagnostic(
                    "SECOND_BOMB",
                    myTile,
                    currentDangerSeconds,
                    $"action:{secondBomb.Action} target:{secondBomb.TargetTile} " +
                    $"move:{FirstMoveDescription(secondBomb.FirstMove)} tapBomb:{secondBomb.TapBomb} reason:{secondBomb.Reason}");
                ExecuteSelectedCandidate(settings, myTile, currentDangerSeconds, secondBomb, "secondBomb");
                return;
            }

            if (TryBuildOwnPendingEscapeRoute(settings, myTile, currentDangerSeconds, out Vector2 ownEscapeMove, out Vector2Int ownEscapeTarget, out string ownEscapeReason))
            {
                LogPostPlantEscapeDiagnostic(
                    "OWN_PENDING_ESCAPE_ROUTE",
                    myTile,
                    currentDangerSeconds,
                    $"target:{ownEscapeTarget} move:{FirstMoveDescription(ownEscapeMove)} reason:{ownEscapeReason}");
                SetCurrentDecision(
                    BattleModeComActionType.Reposition,
                    ownEscapeMove,
                    true,
                    ownEscapeTarget,
                    ownEscapeReason,
                    FirstMoveDescription(ownEscapeMove),
                    currentDangerSeconds,
                    "escape ownPending");
                return;
            }

            if (TryBuildAbilityCandidate(settings, myTile, out CandidateAction abilityWithOwnBomb))
            {
                LogPostPlantEscapeDiagnostic(
                    "ABILITY_AFTER_PLANT",
                    myTile,
                    currentDangerSeconds,
                    $"action:{abilityWithOwnBomb.Action} target:{abilityWithOwnBomb.TargetTile} " +
                    $"move:{FirstMoveDescription(abilityWithOwnBomb.FirstMove)} reason:{abilityWithOwnBomb.Reason}");
                ExecuteSelectedCandidate(settings, myTile, currentDangerSeconds, abilityWithOwnBomb, "ability");
                return;
            }

            if (TryBuildOwnPendingSafetyMove(settings, myTile, currentDangerSeconds, out Vector2 safetyMove, out Vector2Int safetyTarget, out string safetyReason))
            {
                LogPostPlantEscapeDiagnostic(
                    "OWN_PENDING_REPOSITION",
                    myTile,
                    currentDangerSeconds,
                    $"target:{safetyTarget} move:{FirstMoveDescription(safetyMove)} reason:{safetyReason}");
                SetCurrentDecision(
                    BattleModeComActionType.Reposition,
                    safetyMove,
                    true,
                    safetyTarget,
                    safetyReason,
                    FirstMoveDescription(safetyMove),
                    currentDangerSeconds,
                    "ownPendingSafety");
                return;
            }

            hasSafeCenterTarget = true;
            safeCenterTargetTile = myTile;

            LogPostPlantEscapeDiagnostic(
                "HOLD_SAFE_TILE",
                myTile,
                currentDangerSeconds,
                $"elapsed:{Time.time - chainBombPlantingStartedTime:F2}s bombsRemaining:{bombController.BombsRemaining} " +
                $"rejected:{FormatRejectedForLog()}");
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

        if (chainBombPlantingStartedTime >= 0f || postPlantEscapeWatchActive)
        {
            LogPostPlantEscapeDiagnostic(
                "OWN_BOMB_RESOLVED",
                myTile,
                currentDangerSeconds,
                chainBombPlantingStartedTime >= 0f
                    ? $"elapsed:{Time.time - chainBombPlantingStartedTime:F2}s"
                    : "elapsed:none",
                force: true);
        }

        chainBombPlantingStartedTime = -10f;
        postPlantEscapeWatchActive = false;
        hasSafeCenterTarget = false;
        committedEscapeExpiresTime = -10f;
        ownChainPlanActive = false;
        ownChainEscapeOnly = false;

        if (TryBuildTankShootPriorityCandidate(settings, myTile, out CandidateAction tankShoot))
        {
            ExecuteSelectedCandidate(
                settings,
                myTile,
                currentDangerSeconds,
                tankShoot,
                "tankShootPriority");
            return;
        }

        bool walkToChainCommitActive =
            walkToChainCommitted && Time.time - walkToChainCommittedTime < WalkToChainCommitTimeoutSeconds;

        if (!walkToChainCommitActive &&
            float.IsInfinity(currentDangerSeconds) &&
            TryBuildOwnChainStartCandidate(settings, myTile, out CandidateAction ownChainStart))
        {
            if (ownChainStart.TapBomb)
            {
                ownChainPlanActive = true;
                ownChainOriginTile = myTile;
                ownChainSecondTile = ownChainStart.TargetTile;
                ownChainLastPlantedTile = myTile;
                ownChainLineDirection = DirectionToTile(ownChainStart.FirstMove);
                ownChainPlantedCount = 1;
                ownChainEscapeOnly = false;
                ownChainPlanStartedTime = Time.time;
            }

            if (EnableChainBombDiagnostics)
            {
                Debug.Log(
                    $"[BattleCOMOwnChain][P{playerId}] {(ownChainStart.TapBomb ? "START" : "SEEK_JUNCTION")} " +
                    $"tile:{myTile} target:{ownChainStart.TargetTile} " +
                    $"move:{FirstMoveDescription(ownChainStart.FirstMove)} reason:{ownChainStart.Reason}");
            }

            ExecuteSelectedCandidate(settings, myTile, currentDangerSeconds, ownChainStart, "ownChain");
            return;
        }

        // --- Walk-to-chain: caminhar até o blast de bomba inimiga para plantar chain ---
        // Se já temos um tile comprometido e ainda dentro do timeout, honrar o compromisso.
        if (walkToChainCommitted && Time.time - walkToChainCommittedTime < WalkToChainCommitTimeoutSeconds)
        {
            if (float.IsInfinity(currentDangerSeconds))
            {
                if (TryBuildCommittedWalkToChainPlantCandidate(settings, myTile, out CandidateAction committedPlant))
                {
                    if (EnableChainBombDiagnostics)
                    {
                        Debug.Log(
                            $"[BattleCOMWalkChain][P{playerId}] COMMIT_PLANT myTile:{myTile} " +
                            $"committedTile:{walkToChainCommittedTile} age:{Time.time - walkToChainCommittedTime:F2}s " +
                            $"escape:{FirstMoveDescription(committedPlant.FirstMove)}");
                    }

                    walkToChainCommitted = false;
                    ExecuteSelectedCandidate(settings, myTile, currentDangerSeconds, committedPlant, "walkToChain");
                    return;
                }

                if (TryFindPath(myTile, walkToChainCommittedTile, settings.searchDepth + 4, false, settings, null, out PathResult commitPath))
                {
                    if (EnableChainBombDiagnostics)
                        Debug.Log($"[BattleCOMWalkChain][P{playerId}] COMMIT_HONOR tile:{walkToChainCommittedTile} myTile:{myTile} dist:{commitPath.Distance} age:{Time.time - walkToChainCommittedTime:F2}s");
                    SetCurrentDecision(BattleModeComActionType.Reposition, commitPath.FirstMove, true, walkToChainCommittedTile,
                        "walk to committed chain tile", FirstMoveDescription(commitPath.FirstMove), currentDangerSeconds, "walkToChain");
                    return;
                }
            }
            // Não conseguiu caminho ou em perigo: cancela
            walkToChainCommitted = false;
        }
        else if (walkToChainCommitted)
        {
            walkToChainCommitted = false;
        }

        // Tenta encontrar um tile no blast de bomba inimiga para plantar uma chain
        if (float.IsInfinity(currentDangerSeconds) &&
            TryBuildWalkToChainCandidate(settings, myTile, currentDangerSeconds, out CandidateAction walkChain))
        {
            if (!walkChain.TapBomb)
            {
                walkToChainCommitted = true;
                walkToChainCommittedTile = walkChain.TargetTile;
                walkToChainCommittedTime = Time.time;
                if (EnableChainBombDiagnostics)
                    Debug.Log($"[BattleCOMWalkChain][P{playerId}] COMMIT_WALK target:{walkChain.TargetTile} myTile:{myTile}");
            }
            else
            {
                walkToChainCommitted = false;
                if (EnableChainBombDiagnostics)
                    Debug.Log($"[BattleCOMWalkChain][P{playerId}] PLANT_NOW tile:{walkChain.TargetTile} myTile:{myTile}");
            }
            ExecuteSelectedCandidate(settings, myTile, currentDangerSeconds, walkChain, "walkToChain");
            return;
        }

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
        currentHoldActionA = selected.HoldActionA;
        currentMoveFollowsEscapeRoute =
            selected.Action == BattleModeComActionType.Reposition &&
            !string.IsNullOrEmpty(selected.Reason) &&
            (selected.Reason.Contains("escape", StringComparison.OrdinalIgnoreCase) ||
             selected.Reason.StartsWith("tank-threat dodge", StringComparison.Ordinal));

        if (selected.Action == BattleModeComActionType.Reposition && selected.HasTarget)
        {
            hasSafeCenterTarget = true;
            safeCenterTargetTile = selected.TargetTile;
        }

        if (route == "chainBomb" && !selected.TapBomb)
        {
            LogChainBombDiagnostic(
                "EXEC_MOVE_TO_CHAIN_TILE",
                myTile,
                currentDangerSeconds,
                $"target:{selected.TargetTile} move:{FirstMoveDescription(selected.FirstMove)} " +
                $"reason:{selected.Reason} input:{selected.InputDescription}",
                force: true);
        }

        if (selected.TapBomb && Time.time - lastBombTapTime >= BombTapCooldownSeconds)
        {
            LogStage3P5PlantDiagnostic(
                "TAP",
                myTile,
                $"action:{selected.Action} route:{route} target:{selected.TargetTile} " +
                $"move:{FirstMoveDescription(selected.FirstMove)} danger:{FormatDanger(currentDangerSeconds)} " +
                $"reason:{selected.Reason}");
            Tap(PlayerAction.ActionA);
            lastBombTapTime = Time.time;
            currentInputDescription = AppendInput(currentInputDescription, "ActionA");
            if (route == "chainBomb")
            {
                LogChainBombDiagnostic(
                    "EXEC_TAP_BOMB",
                    myTile,
                    currentDangerSeconds,
                    $"target:{selected.TargetTile} escapeMove:{FirstMoveDescription(selected.FirstMove)} " +
                    $"reason:{selected.Reason} activeOwn:{FormatOwnChainBombs()}",
                    force: true);
                postPlantEscapeWatchActive = true;
                postPlantEscapeWatchStartedTime = Time.time;
                LogPostPlantEscapeDiagnostic(
                    "BOMB_TAPPED",
                    myTile,
                    currentDangerSeconds,
                    $"action:{selected.Action} target:{selected.TargetTile} move:{FirstMoveDescription(selected.FirstMove)} " +
                    $"reason:{selected.Reason}",
                    force: true);
            }
        }
        else if (selected.TapBomb && route == "chainBomb")
        {
            LogChainBombDiagnostic(
                "EXEC_TAP_BLOCKED_COOLDOWN",
                myTile,
                currentDangerSeconds,
                $"target:{selected.TargetTile} cooldownLeft:{Mathf.Max(0f, BombTapCooldownSeconds - (Time.time - lastBombTapTime)):F2}s " +
                $"lastTapAge:{Time.time - lastBombTapTime:F2}s reason:{selected.Reason}",
                force: true);
        }

        if (selected.TapActionA &&
            Time.time - lastActionATapTime >= ActionATapCooldownSeconds)
        {
            Tap(PlayerAction.ActionA);
            lastActionATapTime = Time.time;
            currentInputDescription = AppendInput(currentInputDescription, "ActionA");
        }

        if (selected.TapActionB && Time.time - lastControlTapTime >= ControlBombTapCooldownSeconds)
        {
            Tap(PlayerAction.ActionB);
            lastControlTapTime = Time.time;
            currentInputDescription = AppendInput(currentInputDescription, "ActionB");
            LogInputTapDiagnostic("ActionB", selected.Reason, selected.Action, myTile);
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
            LogInputTapDiagnostic("ActionC", selected.Reason, selected.Action, myTile);
        }

        if (selected.Action == BattleModeComActionType.KickBomb)
        {
            LogKickBombRiskDiagnostic(
                "DECISION_SELECTED",
                myTile,
                currentDangerSeconds,
                $"route:{route} target:{(selected.HasTarget ? selected.TargetTile.ToString() : "none")} " +
                $"move:{FirstMoveDescription(selected.FirstMove)} input:{currentInputDescription} " +
                $"tapBomb:{selected.TapBomb} tapA:{selected.TapActionA} " +
                $"tapR:{selected.TapActionR} tapC:{selected.TapActionC} " +
                $"reason:{selected.Reason} candidates:{FormatCandidates()} rejected:{FormatRejectedForLog()} " +
                $"abilities:{FormatCurrentAbilityTraces()} activeBombs:{FormatActiveBombThreats(myTile)}",
                force: true);
        }

        LogDecision(settings, myTile, currentDangerSeconds, route);
    }

    private bool TryBuildStageAbilityEmergencyCandidate(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        float currentDangerSeconds,
        out CandidateAction candidate)
    {
        candidate = default;
        abilityDecisionEvaluatedThisThink = true;
        RefreshComAbilities();

        for (int i = 0; i < comAbilities.Count; i++)
        {
            if (comAbilities[i] is not IBattleModeComStageAbility stageAbility ||
                !IsComAbilityAlive(stageAbility) ||
                !stageAbility.IsAvailable)
            {
                continue;
            }

            if (!stageAbility.TryBuildEmergencyDecision(
                    settings,
                    this,
                    myTile,
                    currentDangerSeconds,
                    out BattleModeComAbilityDecision decision))
            {
                continue;
            }

            candidate = ToCandidateAction(decision);
            AppendAbilityTrace(stageAbility, "stage emergency selected");
            return true;
        }

        return false;
    }

    private bool TryBuildAbilityEmergencyCandidate(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        float currentDangerSeconds,
        out CandidateAction candidate)
    {
        candidate = default;
        abilityDecisionEvaluatedThisThink = true;
        RefreshComAbilities();

        if (comAbilities.Count == 0)
        {
            string noAbilitiesTrace = BuildNoAbilityScriptsTrace();
            AppendAbilityTrace("emergency", "none", noAbilitiesTrace);
            LogAbilityDecisionTrace(
                "emergency",
                myTile,
                currentDangerSeconds,
                "none",
                noAbilitiesTrace);
            return false;
        }

        string escapeAbilityCandidates = string.Empty;
        string evaluations = string.Empty;
        BattleModeComPowerGloveAbility prioritizedPowerGlove = null;

        for (int i = 0; i < comAbilities.Count; i++)
        {
            if (IsComAbilityAlive(comAbilities[i]) &&
                comAbilities[i] is BattleModeComPowerGloveAbility powerGloveAbility &&
                powerGloveAbility.ShouldPrioritizeEmergency(myTile))
            {
                prioritizedPowerGlove = powerGloveAbility;
                break;
            }
        }

        if (prioritizedPowerGlove != null)
        {
            string abilityName = GetAbilityDiagnosticName(prioritizedPowerGlove);
            if (prioritizedPowerGlove.IsAvailable &&
                prioritizedPowerGlove.TryBuildEmergencyDecision(
                    settings,
                    this,
                    myTile,
                    currentDangerSeconds,
                    out var powerGloveDecision))
            {
                candidate = ToCandidateAction(powerGloveDecision);
                AppendAbilityTrace(prioritizedPowerGlove, "emergency priority selected");
                AppendAbilityEvaluation(
                    ref evaluations,
                    abilityName,
                    "priority-candidate",
                    FormatAbilityDecision(
                        powerGloveDecision,
                        prioritizedPowerGlove.LastDecisionTrace));
                LogAbilityDecisionTrace(
                    "emergency",
                    myTile,
                    currentDangerSeconds,
                    $"selected:{abilityName}:active-sequence",
                    evaluations,
                    powerGloveDecision);
                return true;
            }

            AppendAbilityEvaluation(
                ref evaluations,
                abilityName,
                "priority-rejected",
                prioritizedPowerGlove.LastDecisionTrace);
        }

        for (int i = 0; i < comAbilities.Count; i++)
        {
            IBattleModeComAbility ability = comAbilities[i];
            if (!IsComAbilityAlive(ability))
            {
                AppendAbilityTrace("emergency", "null", "null ability entry");
                AppendAbilityEvaluation(ref evaluations, "null", "unavailable", "null ability entry");
                continue;
            }

            if (ReferenceEquals(ability, prioritizedPowerGlove))
                continue;

            string abilityName = GetAbilityDiagnosticName(ability);
            if (!ability.IsAvailable)
            {
                AppendAbilityTrace(ability, "emergency unavailable");
                AppendAbilityEvaluation(ref evaluations, abilityName, "unavailable", ability.LastDecisionTrace);
                continue;
            }

            if (!ability.TryBuildEmergencyDecision(settings, this, myTile, currentDangerSeconds, out var decision))
            {
                AppendAbilityTrace(ability, "emergency");
                AppendAbilityEvaluation(ref evaluations, abilityName, "rejected", ability.LastDecisionTrace);
                continue;
            }

            AppendAbilityEvaluation(
                ref evaluations,
                abilityName,
                "candidate",
                FormatAbilityDecision(decision, ability.LastDecisionTrace));

            if (decision.UsesEscapeAbilityChance)
                AppendEscapeAbilityCandidate(ref escapeAbilityCandidates, ability, decision);

            if (decision.UsesEscapeAbilityChance &&
                !RollEscapeAbilityChance(
                    settings,
                    abilityName,
                    escapeAbilityCandidates,
                    myTile,
                    currentDangerSeconds,
                    out string chanceTrace))
            {
                AppendAbilityTrace(
                    "emergency chance",
                    abilityName,
                    $"{chanceTrace} trace:{ability.LastDecisionTrace}");
                AppendAbilityEvaluation(ref evaluations, abilityName, "chance-rejected", chanceTrace);
                if (ability is BattleModeComYellowLouieKickAbility yellowLouieKickAbility)
                    yellowLouieKickAbility.CancelUnselectedPendingKickCommand("escape ability chance failed");
                continue;
            }

            candidate = ToCandidateAction(decision);
            AppendAbilityTrace(ability, "emergency selected");
            LogAbilityDecisionTrace(
                "emergency",
                myTile,
                currentDangerSeconds,
                $"selected:{abilityName}",
                evaluations,
                decision);
            return true;
        }

        LogAbilityDecisionTrace(
            "emergency",
            myTile,
            currentDangerSeconds,
            "none-selected",
            evaluations);
        return false;
    }

    private bool TryContinueDangerEscapeRoute(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        float currentDangerSeconds)
    {
        if (!currentMoveFollowsEscapeRoute || !hasSafeCenterTarget || myTile == safeCenterTargetTile)
            return false;

        float targetDanger = GetDangerSeconds(safeCenterTargetTile, null);
        if (!float.IsInfinity(targetDanger))
            return false;

        // Primeiro tenta seguir o safe center já escolhido. Refazer um BFS livre a cada
        // frame faz a raiz da busca alternar entre dois tiles na fronteira (sub-posição),
        // produzindo first moves opostos e oscilação até a morte.
        Vector2 firstMove;
        Vector2Int escapeTarget;
        string route;
        if (TryFindPath(myTile, safeCenterTargetTile, settings.searchDepth + 4, false, settings, null, out PathResult safeCenterPath) &&
            safeCenterPath.FirstMove != Vector2.zero)
        {
            firstMove = safeCenterPath.FirstMove;
            escapeTarget = safeCenterTargetTile;
            route = "escape safeCenterPath";
        }
        else if (!TryFindEscape(settings, myTile, null, out firstMove, out escapeTarget, out route))
        {
            return false;
        }

        Vector2Int firstStep = DirectionToTile(firstMove);
        Vector2Int nextTile = myTile + firstStep;
        float arrivalSeconds = EstimateFirstMoveTraversalSeconds(firstStep);
        bool firstStepUnsafe =
            firstStep == Vector2Int.zero || IsDangerousAt(nextTile, arrivalSeconds, settings, null);

        // Se o caminho ao safe center ficou bloqueado/perigoso no primeiro passo,
        // cai para o BFS de fuga genérico antes de desistir.
        if (firstStepUnsafe && route == "escape safeCenterPath")
        {
            if (!TryFindEscape(settings, myTile, null, out firstMove, out escapeTarget, out route))
                return false;

            firstStep = DirectionToTile(firstMove);
            nextTile = myTile + firstStep;
            arrivalSeconds = EstimateFirstMoveTraversalSeconds(firstStep);
            firstStepUnsafe =
                firstStep == Vector2Int.zero || IsDangerousAt(nextTile, arrivalSeconds, settings, null);
        }

        if (firstStepUnsafe)
            return false;

        LogPostPlantEscapeDiagnostic(
            "DANGER_CONTINUE_ESCAPE_ROUTE",
            myTile,
            currentDangerSeconds,
            $"safeCenter:{safeCenterTargetTile} bfsTarget:{escapeTarget} next:{nextTile} " +
            $"move:{FirstMoveDescription(firstMove)} route:{route}");
        SetCurrentDecision(
            BattleModeComActionType.Reposition,
            firstMove,
            true,
            escapeTarget,
            "continue escape danger",
            FirstMoveDescription(firstMove),
            currentDangerSeconds,
            route);
        return true;
    }

    private bool TryBuildAbilityCandidate(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out CandidateAction candidate)
    {
        candidate = default;
        abilityDecisionEvaluatedThisThink = true;
        RefreshComAbilities();

        if (comAbilities.Count == 0)
        {
            string noAbilitiesTrace = BuildNoAbilityScriptsTrace();
            AppendAbilityTrace("candidate", "none", noAbilitiesTrace);
            LogAbilityDecisionTrace(
                "candidate",
                myTile,
                float.PositiveInfinity,
                "none",
                noAbilitiesTrace);
            return false;
        }

        CandidateAction best = default;
        int bestWeight = 0;
        BattleModeComAbilityDecision bestDecision = default;
        string bestAbilityName = "none";
        string evaluations = string.Empty;

        for (int i = 0; i < comAbilities.Count; i++)
        {
            IBattleModeComAbility ability = comAbilities[i];
            if (!IsComAbilityAlive(ability))
            {
                AppendAbilityTrace("candidate", "null", "null ability entry");
                AppendAbilityEvaluation(ref evaluations, "null", "unavailable", "null ability entry");
                continue;
            }

            string abilityName = GetAbilityDiagnosticName(ability);
            if (!ability.IsAvailable)
            {
                AppendAbilityTrace(ability, "candidate unavailable");
                AppendAbilityEvaluation(ref evaluations, abilityName, "unavailable", ability.LastDecisionTrace);
                continue;
            }

            if (!ability.TryBuildCandidateDecision(settings, this, myTile, out var decision))
            {
                AppendAbilityTrace(ability, "candidate");
                AppendAbilityEvaluation(ref evaluations, abilityName, "rejected", ability.LastDecisionTrace);
                continue;
            }

            CandidateAction candidateAction = ToCandidateAction(decision);
            string outcome = candidateAction.Weight > bestWeight
                ? "leading"
                : $"lower-weight-than:{bestAbilityName}:{bestWeight}";
            AppendAbilityEvaluation(
                ref evaluations,
                abilityName,
                outcome,
                FormatAbilityDecision(decision, ability.LastDecisionTrace));

            if (candidateAction.Weight <= bestWeight)
                continue;

            best = candidateAction;
            bestWeight = candidateAction.Weight;
            bestDecision = decision;
            bestAbilityName = abilityName;
            AppendAbilityTrace(ability, "candidate selected");
        }

        if (bestWeight <= 0)
        {
            LogAbilityDecisionTrace(
                "candidate",
                myTile,
                float.PositiveInfinity,
                "none-selected",
                evaluations);
            return false;
        }

        candidate = best;
        LogAbilityDecisionTrace(
            "candidate",
            myTile,
            float.PositiveInfinity,
            $"selected:{bestAbilityName}",
            evaluations,
            bestDecision);
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
            TapActionA = decision.TapActionA,
            HoldActionA = decision.HoldActionA,
            TapActionB = decision.TapActionB,
            TapActionR = decision.TapActionR,
            TapActionC = decision.TapActionC,
            UsesEscapeAbilityChance = decision.UsesEscapeAbilityChance
        };
    }

    private bool TryBuildTankShootPriorityCandidate(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out CandidateAction candidate)
    {
        candidate = default;
        RefreshComAbilities();

        for (int i = 0; i < comAbilities.Count; i++)
        {
            if (comAbilities[i] is not BattleModeComTankMountShootAbility tankShoot ||
                !tankShoot.IsAvailable)
                continue;

            if (!tankShoot.TryBuildCandidateDecision(
                    settings,
                    this,
                    myTile,
                    out BattleModeComAbilityDecision decision))
                return false;

            candidate = ToCandidateAction(decision);
            AppendAbilityTrace(tankShoot, "candidate priority selected");
            return candidate.Weight > 0;
        }

        return false;
    }

    private void AppendAbilityTrace(IBattleModeComAbility ability, string phase)
    {
        if (!IsComAbilityAlive(ability))
            return;

        string name = GetAbilityDiagnosticName(ability);
        string trace = string.IsNullOrWhiteSpace(ability.LastDecisionTrace)
            ? "no trace"
            : ability.LastDecisionTrace;

        rejectedActions.Add($"Ability/{phase}/{name}:{trace}");
    }

    private void AppendAbilityTrace(string phase, string name, string trace)
    {
        rejectedActions.Add($"Ability/{phase}/{name}:{trace}");
    }

    private static string GetAbilityDiagnosticName(IBattleModeComAbility ability)
    {
        if (!IsComAbilityAlive(ability))
            return "null";

        return string.IsNullOrWhiteSpace(ability.DiagnosticName)
            ? ability.GetType().Name
            : ability.DiagnosticName;
    }

    private static void AppendAbilityEvaluation(
        ref string evaluations,
        string abilityName,
        string outcome,
        string trace)
    {
        if (!string.IsNullOrEmpty(evaluations))
            evaluations += " | ";

        evaluations +=
            $"{abilityName}[{outcome} trace:{(string.IsNullOrWhiteSpace(trace) ? "no trace" : trace)}]";
    }

    private static string FormatAbilityDecision(
        BattleModeComAbilityDecision decision,
        string trace)
    {
        string target = decision.HasTarget ? decision.TargetTile.ToString() : "none";
        string input = string.IsNullOrWhiteSpace(decision.InputDescription)
            ? "none"
            : decision.InputDescription;
        string reason = string.IsNullOrWhiteSpace(decision.Reason)
            ? "none"
            : decision.Reason;

        return
            $"action:{decision.Action} weight:{decision.Weight} target:{target} " +
            $"move:{FirstMoveDescription(decision.FirstMove)} input:{input} " +
            $"tapBomb:{decision.TapBomb} tapA:{decision.TapActionA} " +
            $"holdA:{decision.HoldActionA} " +
            $"tapR:{decision.TapActionR} tapC:{decision.TapActionC} " +
            $"escapeChance:{decision.UsesEscapeAbilityChance} reason:{reason} " +
            $"trace:{(string.IsNullOrWhiteSpace(trace) ? "no trace" : trace)}";
    }

    private void LogAbilityDecisionTrace(
        string phase,
        Vector2Int myTile,
        float dangerSeconds,
        string outcome,
        string evaluations,
        BattleModeComAbilityDecision? selectedDecision = null)
    {
        // Replaced by the unified behavior watchdog.
    }

    private bool RollEscapeAbilityChance(
        BattleModeComDifficultySettings settings,
        string abilityName,
        string escapeCandidates,
        Vector2Int myTile,
        float currentDangerSeconds,
        out string trace)
    {
        float chance = Mathf.Clamp01(settings.escapeAbilityChance);
        float roll;
        bool result;
        string threatKey = ResolveEscapeAbilityThreatKey(myTile);
        bool cacheMatches =
            !string.IsNullOrEmpty(escapeAbilityChanceCacheThreatKey) &&
            string.Equals(escapeAbilityChanceCacheThreatKey, threatKey, StringComparison.Ordinal) &&
            escapeAbilityChanceCacheDifficulty == settings.difficulty;

        if (cacheMatches)
        {
            result = escapeAbilityChanceCacheResult;
            trace = FormatEscapeAbilityChanceTrace(
                settings,
                abilityName,
                chance,
                escapeAbilityChanceCacheRoll,
                result,
                cached: true,
                threatKey,
                escapeCandidates);
            lastEscapeAbilityChanceTrace = trace;
            LogEscapeAbilityChanceDiagnostic(trace, myTile, currentDangerSeconds, escapeCandidates, force: false);
            return result;
        }

        if (chance >= 1f)
        {
            roll = 0f;
            result = true;
            escapeAbilityChanceCacheThreatKey = threatKey;
            escapeAbilityChanceCacheDifficulty = settings.difficulty;
            escapeAbilityChanceCacheTime = Time.time;
            escapeAbilityChanceCacheResult = result;
            escapeAbilityChanceCacheRoll = roll;
            trace = FormatEscapeAbilityChanceTrace(settings, abilityName, chance, roll, result, cached: false, threatKey, escapeCandidates);
            lastEscapeAbilityChanceTrace = trace;
            LogEscapeAbilityChanceDiagnostic(trace, myTile, currentDangerSeconds, escapeCandidates, force: true);
            return true;
        }

        escapeAbilityChanceCacheThreatKey = threatKey;
        escapeAbilityChanceCacheDifficulty = settings.difficulty;
        escapeAbilityChanceCacheTime = Time.time;
        roll = UnityEngine.Random.value;
        result = roll <= chance;
        escapeAbilityChanceCacheResult = result;
        escapeAbilityChanceCacheRoll = roll;
        trace = FormatEscapeAbilityChanceTrace(settings, abilityName, chance, roll, result, cached: false, threatKey, escapeCandidates);
        lastEscapeAbilityChanceTrace = trace;
        LogEscapeAbilityChanceDiagnostic(trace, myTile, currentDangerSeconds, escapeCandidates, force: true);
        return result;
    }

    private static string FormatEscapeAbilityChanceTrace(
        BattleModeComDifficultySettings settings,
        string abilityName,
        float chance,
        float roll,
        bool result,
        bool cached,
        string threatKey,
        string escapeCandidates)
    {
        return
            $"escapeAbilityRoll ability:{abilityName} difficulty:{settings.difficulty} " +
            $"chance:{chance:F2} roll:{roll:F2} result:{(result ? "pass" : "fail")} threat:{threatKey}" +
            (cached ? " cached" : string.Empty) +
            $" candidates:{(string.IsNullOrWhiteSpace(escapeCandidates) ? "none" : escapeCandidates)}";
    }

    private void AppendEscapeAbilityCandidate(
        ref string candidatesText,
        IBattleModeComAbility ability,
        BattleModeComAbilityDecision decision)
    {
        string name = GetAbilityDiagnosticName(ability);
        string target = decision.HasTarget ? decision.TargetTile.ToString() : "none";
        string move = FirstMoveDescription(decision.FirstMove);
        string input = string.IsNullOrWhiteSpace(decision.InputDescription) ? "none" : decision.InputDescription;
        string reason = string.IsNullOrWhiteSpace(decision.Reason) ? "none" : decision.Reason;
        string trace = IsComAbilityAlive(ability) && !string.IsNullOrWhiteSpace(ability.LastDecisionTrace)
            ? ability.LastDecisionTrace
            : "no trace";

        string entry =
            $"{name}[action:{decision.Action} weight:{decision.Weight} target:{target} " +
            $"move:{move} input:{input} reason:{reason} trace:{trace}]";

        if (string.IsNullOrWhiteSpace(candidatesText))
            candidatesText = entry;
        else
            candidatesText += " | " + entry;
    }

    private void LogEscapeAbilityChanceDiagnostic(
        string chanceTrace,
        Vector2Int myTile,
        float currentDangerSeconds,
        string escapeCandidates,
        bool force)
    {
        if (!EnableEscapeAbilityChanceDiagnostics)
            return;

        string key = chanceTrace;
        if (!force &&
            key == lastEscapeAbilityChanceLogKey &&
            Time.time - lastEscapeAbilityChanceLogTime < 0.35f)
        {
            return;
        }

        lastEscapeAbilityChanceLogKey = key;
        lastEscapeAbilityChanceLogTime = Time.time;
        Debug.Log(
            $"[BattleCOMEscapeRoll][P{playerId}] tile:{myTile} pos:{transform.position} " +
            $"danger:{FormatDanger(currentDangerSeconds)} {chanceTrace} " +
            $"escapeCandidates:{(string.IsNullOrWhiteSpace(escapeCandidates) ? "none" : escapeCandidates)}",
            this);
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

    private void LogPostPlantEscapeDiagnostic(
        string key,
        Vector2Int myTile,
        float currentDangerSeconds,
        string message,
        bool force = false)
    {
        if (!EnablePostPlantEscapeDiagnostics)
            return;

        if (PostPlantDiagnosticPlayerIdFilter != 0 && playerId != PostPlantDiagnosticPlayerIdFilter)
            return;

        if (!force && !postPlantEscapeWatchActive && chainBombPlantingStartedTime < 0f)
            return;

        string logKey = $"{key}:{myTile}:{currentAction}:{currentReason}";
        if (!force &&
            logKey == lastPostPlantEscapeLogKey &&
            Time.time - lastPostPlantEscapeLogTime < 0.25f)
        {
            return;
        }

        lastPostPlantEscapeLogKey = logKey;
        lastPostPlantEscapeLogTime = Time.time;

        string watch = postPlantEscapeWatchActive
            ? $"watch:{Time.time - postPlantEscapeWatchStartedTime:F2}s"
            : "watch:off";
        string chain = chainBombPlantingStartedTime >= 0f
            ? $"chainElapsed:{Time.time - chainBombPlantingStartedTime:F2}s"
            : "chainElapsed:none";
        string safe = hasSafeCenterTarget ? safeCenterTargetTile.ToString() : "none";

        Debug.Log(
            $"[BattleCOMPostPlant][P{playerId}] frame:{Time.frameCount} t:{Time.time:F2} tile:{myTile} " +
            $"pos:{transform.position} danger:{FormatDanger(currentDangerSeconds)} key:{key} {watch} {chain} " +
            $"action:{currentAction} move:{FirstMoveDescription(currentMoveInput)} target:{(hasCurrentTarget ? currentTargetTile.ToString() : "none")} " +
            $"safeCenter:{safe} bombsRemaining:{(bombController != null ? bombController.BombsRemaining : -1)} " +
            $"ownPending:{HasOwnUnresolvedBombOrExplosion()} msg:{message}",
            this);
    }

    private void LogKickBombRiskDiagnostic(
        string key,
        Vector2Int myTile,
        float currentDangerSeconds,
        string message,
        bool force = false)
    {
        if (!ShouldLogKickBombRiskDiagnostics())
            return;

        string logKey = $"{key}:{myTile}:{currentAction}:{currentReason}:{currentInputDescription}";
        if (!force &&
            logKey == lastKickBombRiskLogKey &&
            Time.time - lastKickBombRiskLogTime < KickBombRiskLogIntervalSeconds)
        {
            return;
        }

        lastKickBombRiskLogKey = logKey;
        lastKickBombRiskLogTime = Time.time;

        Debug.Log(
            $"[BattleCOMKickRisk][P{playerId}] frame:{Time.frameCount} t:{Time.time:F2} " +
            $"tile:{myTile} pos:{transform.position} danger:{FormatDanger(currentDangerSeconds)} " +
            $"key:{key} action:{currentAction} move:{FirstMoveDescription(currentMoveInput)} " +
            $"target:{(hasCurrentTarget ? currentTargetTile.ToString() : "none")} msg:{message}",
            this);
    }

    private bool ShouldLogKickBombRiskDiagnostics()
    {
        if (!EnableKickBombRiskDiagnostics)
            return false;

        return BattleModeComKickBombAbility.DiagnosticPlayerIdFilter == 0 ||
               playerId == BattleModeComKickBombAbility.DiagnosticPlayerIdFilter;
    }

    private void LogEscapeDiagnostic(string key, Vector2Int myTile, float dangerSeconds, string message, bool force = false)
    {
        if (!EnableEscapeDiagnostics)
            return;

        if (PostPlantDiagnosticPlayerIdFilter != 0 && playerId != PostPlantDiagnosticPlayerIdFilter)
            return;

        string logKey = $"{key}:{myTile}:{currentAction}";
        if (!force && logKey == lastEscapeLogKey && Time.time - lastEscapeLogTime < EscapeLogIntervalSeconds)
            return;

        lastEscapeLogKey = logKey;
        lastEscapeLogTime = Time.time;

        Debug.Log(
            $"[BattleCOMEscape][P{playerId}] frame:{Time.frameCount} t:{Time.time:F2} tile:{myTile} " +
            $"danger:{FormatDanger(dangerSeconds)} key:{key} " +
            $"action:{currentAction} move:{FirstMoveDescription(currentMoveInput)} " +
            $"target:{(hasCurrentTarget ? currentTargetTile.ToString() : "none")} " +
            $"chain:{(chainBombPlantingStartedTime >= 0f ? $"{Time.time - chainBombPlantingStartedTime:F2}s" : "none")} " +
            $"watch:{postPlantEscapeWatchActive} ownPending:{HasOwnUnresolvedBombOrExplosion()} " +
            $"msg:{message}",
            this);
    }

    private string FormatRejectedForLog()
    {
        if (rejectedActions.Count <= 0)
            return "none";

        string joined = string.Join(" | ", rejectedActions);
        return joined.Length <= 420 ? joined : joined.Substring(0, 420) + "...";
    }

    private void ExecuteEscape(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        float currentDangerSeconds)
    {
        if (TryFindEscape(settings, myTile, null, out Vector2 firstMove, out Vector2Int target, out string route))
        {
            LogPostPlantEscapeDiagnostic(
                "DANGER_ESCAPE",
                myTile,
                currentDangerSeconds,
                $"target:{target} move:{FirstMoveDescription(firstMove)} route:{route}");
            LogEscapeDiagnostic(
                "ESCAPE",
                myTile,
                currentDangerSeconds,
                $"target:{target} move:{FirstMoveDescription(firstMove)} route:{route}");
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

        // BFS falhou — logar antes de tentar ability/hold/fallback
        LogEscapeDiagnostic(
            "ESCAPE_FAIL",
            myTile,
            currentDangerSeconds,
            "TryFindEscape returned false, trying ability/hold/fallback",
            force: true);

        if (TryBuildAbilityEmergencyCandidate(settings, myTile, currentDangerSeconds, out CandidateAction abilityEmergency))
        {
            LogPostPlantEscapeDiagnostic(
                "DANGER_ABILITY",
                myTile,
                currentDangerSeconds,
                $"action:{abilityEmergency.Action} target:{abilityEmergency.TargetTile} " +
                $"move:{FirstMoveDescription(abilityEmergency.FirstMove)} reason:{abilityEmergency.Reason}");
            LogEscapeDiagnostic(
                "ESCAPE_ABILITY",
                myTile,
                currentDangerSeconds,
                $"action:{abilityEmergency.Action} target:{abilityEmergency.TargetTile} reason:{abilityEmergency.Reason}");
            ExecuteSelectedCandidate(settings, myTile, currentDangerSeconds, abilityEmergency, "ability");
            return;
        }

        if (ShouldHoldDangerTile(settings, myTile, currentDangerSeconds, out string holdReason))
        {
            LogPostPlantEscapeDiagnostic(
                "DANGER_HOLD",
                myTile,
                currentDangerSeconds,
                holdReason);
            // force=true: HOLD é sempre importante — indica que TryFindEscape falhou
            LogEscapeDiagnostic(
                "HOLD",
                myTile,
                currentDangerSeconds,
                $"reason:{holdReason} bfsEscapeFailed:true",
                force: true);
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
        Vector2Int fallbackTarget = myTile + DirectionToTile(fallback);
        bool hasFallbackTarget = fallback != Vector2.zero && fallbackTarget != myTile;
        LogPostPlantEscapeDiagnostic(
            "DANGER_FALLBACK",
            myTile,
            currentDangerSeconds,
            $"target:{(hasFallbackTarget ? fallbackTarget.ToString() : "none")} move:{FirstMoveDescription(fallback)} " +
            $"reason:{fallbackReason} rejected:{FormatRejectedForLog()}");
        // force=true: FALLBACK significa que BFS falhou E hold também foi rejeitado
        LogEscapeDiagnostic(
            "FALLBACK",
            myTile,
            currentDangerSeconds,
            $"target:{(hasFallbackTarget ? fallbackTarget.ToString() : "none")} move:{FirstMoveDescription(fallback)} " +
            $"reason:{fallbackReason}",
            force: true);
        SetCurrentDecision(
            BattleModeComActionType.Reposition,
            fallback,
            hasFallbackTarget,
            hasFallbackTarget ? fallbackTarget : myTile,
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
            if (!CanBombHitDestructible(tile, Mathf.Max(1, bombController.GetPlannedExplosionRadius())))
                continue;

            if (WouldBlastHitUsefulItem(tile, Mathf.Max(1, bombController.GetPlannedExplosionRadius()), out ItemType itemType, out Vector2Int itemTile))
            {
                RejectVerbose($"FarmDestructible recusado queimaria item {itemType}@{itemTile}");
                continue;
            }

            if (!CanPlantBombWithEscape(tile, Mathf.Max(1, bombController.GetPlannedExplosionRadius()), settings, out Vector2 escapeMove, out _))
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

        int radius = Mathf.Max(1, bombController.GetPlannedExplosionRadius());
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

    // Planta uma segunda bomba independente (não precisa de chain link) quando a IA já tem
    // uma bomba ativa mas está segura e tem bombas sobrando. Prioriza atingir inimigo;
    // caso não haja inimigo no raio, aceita atingir bloco destrutível.
    private bool TryBuildSecondBombCandidate(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        float currentDangerSeconds,
        out CandidateAction candidate)
    {
        candidate = default;

        if (bombController == null || bombController.BombsRemaining <= 0)
            return false;

        int radius = Mathf.Max(1, bombController.GetPlannedExplosionRadius());
        GatherReachableSafeTiles(myTile, settings.searchDepth + 3, settings);

        // --- Passo 1: tenta acertar inimigo diretamente (como TryBuildCombatCandidate) ---
        if (TryFindNearestEnemy(myTile, out PlayerIdentity target, out Vector2Int targetTile, out int _targetDist))
        {
            // Na posição atual
            if (IsTileInBlastLineRuntime(myTile, targetTile, radius) &&
                !WouldBlastHitUsefulItem(myTile, radius, out _, out _) &&
                CanPlantAdditionalBombWithEscape(
                    myTile,
                    radius,
                    settings,
                    out Vector2 combatEscape,
                    out _,
                    out _))
            {
                candidate = new CandidateAction
                {
                    Action = BattleModeComActionType.CombatPlant,
                    Weight = settings.combatPlantWeight,
                    TargetTile = targetTile,
                    HasTarget = true,
                    FirstMove = combatEscape,
                    HasRoute = true,
                    Reason = $"second bomb combat P{target.playerId} in blast line",
                    InputDescription = AppendInput("ActionA", FirstMoveDescription(combatEscape)),
                    TapBomb = true
                };
                return true;
            }

            // Em tile reachable que alcance o inimigo
            Vector2Int bestCombatTile = myTile;
            Vector2 bestCombatMove = Vector2.zero;
            Vector2 bestCombatEscapeMove = Vector2.zero;
            int bestCombatDist = int.MaxValue;
            for (int i = 0; i < reachableTiles.Count; i++)
            {
                Vector2Int tile = reachableTiles[i];
                if (IsBombAtTile(tile)) continue;
                if (WouldBlastHitUsefulItem(tile, radius, out _, out _)) continue;
                if (!IsTileInBlastLineRuntime(tile, targetTile, radius)) continue;
                if (!CanPlantAdditionalBombWithEscape(tile, radius, settings, out Vector2 esc, out _, out _)) continue;
                if (!TryFindPath(myTile, tile, settings.searchDepth + 3, true, settings, null, out PathResult path)) continue;
                if (path.Distance < bestCombatDist)
                {
                    bestCombatTile = tile;
                    bestCombatMove = path.FirstMove;
                    bestCombatEscapeMove = esc;
                    bestCombatDist = path.Distance;
                }
            }
            if (bestCombatDist != int.MaxValue)
            {
                bool plantNow = bestCombatDist == 0;
                candidate = new CandidateAction
                {
                    Action = BattleModeComActionType.CombatPlant,
                    Weight = settings.combatPlantWeight,
                    TargetTile = bestCombatTile,
                    HasTarget = true,
                    FirstMove = plantNow ? bestCombatEscapeMove : bestCombatMove,
                    HasRoute = true,
                    Reason = plantNow
                        ? $"second bomb combat P{target.playerId} dist {bestCombatDist}"
                        : $"move to second bomb combat P{target.playerId} dist {bestCombatDist}",
                    InputDescription = plantNow
                        ? AppendInput("ActionA", FirstMoveDescription(bestCombatEscapeMove))
                        : FirstMoveDescription(bestCombatMove),
                    TapBomb = plantNow
                };
                return true;
            }
        }

        // --- Passo 2: fallback — atingir bloco destrutível (como TryBuildFarmCandidate) ---
        Vector2Int bestFarmTile = myTile;
        Vector2 bestFarmMove = Vector2.zero;
        Vector2 bestFarmEscapeMove = Vector2.zero;
        int bestFarmDist = int.MaxValue;
        float bestFarmScore = float.NegativeInfinity;

        for (int i = 0; i < reachableTiles.Count; i++)
        {
            Vector2Int tile = reachableTiles[i];
            if (IsBombAtTile(tile)) continue;
            if (!CanBombHitDestructible(tile, radius)) continue;
            if (WouldBlastHitUsefulItem(tile, radius, out _, out _)) continue;
            if (!CanPlantAdditionalBombWithEscape(tile, radius, settings, out Vector2 esc, out _, out _)) continue;
            if (!TryFindPath(myTile, tile, settings.searchDepth + 3, true, settings, null, out PathResult path)) continue;

            float score = -path.Distance * 10f + GetDecisionNoise(9000 + i, FarmTargetJitter);
            if (score > bestFarmScore)
            {
                bestFarmTile = tile;
                bestFarmMove = path.FirstMove;
                bestFarmEscapeMove = esc;
                bestFarmDist = path.Distance;
                bestFarmScore = score;
            }
        }

        if (bestFarmDist == int.MaxValue)
            return false;

        bool plantFarmNow = bestFarmDist == 0;
        candidate = new CandidateAction
        {
            Action = BattleModeComActionType.FarmDestructible,
            Weight = settings.farmDestructibleWeight,
            TargetTile = bestFarmTile,
            HasTarget = true,
            FirstMove = plantFarmNow ? bestFarmEscapeMove : bestFarmMove,
            HasRoute = true,
            Reason = plantFarmNow
                ? $"second bomb farm dist {bestFarmDist}"
                : $"move to second bomb farm dist {bestFarmDist}",
            InputDescription = plantFarmNow
                ? AppendInput("ActionA", FirstMoveDescription(bestFarmEscapeMove))
                : FirstMoveDescription(bestFarmMove),
            TapBomb = plantFarmNow
        };
        return true;
    }

    private bool TryBuildOwnChainStartCandidate(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out CandidateAction candidate)
    {
        candidate = default;

        if (bombController == null || bombController.BombsRemaining < 2)
        {
            ownChainSeekCommitted = false;
            return false;
        }

        // Compromisso com uma junction já escolhida: não re-rolar o plano a cada frame,
        // senão a IA oscila entre planos concorrentes (vai-e-volta até morrer).
        bool seekCommitActive =
            ownChainSeekCommitted &&
            Time.time - ownChainSeekCommittedTime < WalkToChainCommitTimeoutSeconds;
        if (!seekCommitActive)
        {
            ownChainSeekCommitted = false;
            if (UnityEngine.Random.value > OwnChainPlanChance)
                return false;
        }

        int radius = Mathf.Max(1, bombController.GetPlannedExplosionRadius());
        if (radius < OwnChainSecondBombDistance)
        {
            ownChainSeekCommitted = false;
            return false;
        }

        Vector2Int bestDirection = Vector2Int.zero;
        Vector2Int bestJunction = myTile;
        Vector2 bestFirstMove = Vector2.zero;
        Vector2Int bestSecondTile = myTile;
        int bestScore = int.MinValue;

        GatherReachableSafeTiles(myTile, settings.searchDepth + 2, settings);

        for (int r = 0; r < reachableTiles.Count; r++)
        {
            Vector2Int junction = reachableTiles[r];
            // Com compromisso ativo, só considerar a junction comprometida.
            if (seekCommitActive && junction != ownChainSeekJunction)
                continue;
            if (IsBombAtTile(junction) || CountOpenNeighbors(junction) < 4)
                continue;

            if (!TryFindPath(myTile, junction, settings.searchDepth + 2, true, settings, null, out PathResult junctionPath))
                continue;

            if (!TryFindOwnChainDirectionAtJunction(settings, junction, radius, out Vector2Int direction, out Vector2Int secondTile, out int directionScore))
                continue;

            int score = directionScore - junctionPath.Distance * 6 + (junction == myTile ? 30 : 0);
            if (score <= bestScore)
                continue;

            bestScore = score;
            bestJunction = junction;
            bestDirection = direction;
            bestFirstMove = junctionPath.FirstMove;
            bestSecondTile = secondTile;
        }

        if (bestDirection == Vector2Int.zero)
        {
            ownChainSeekCommitted = false;
            return false;
        }

        bool plantNow = bestJunction == myTile;
        if (plantNow)
        {
            bestFirstMove = TileDirectionToVector(bestDirection);
            ownChainSeekCommitted = false;
        }
        else if (!seekCommitActive)
        {
            ownChainSeekCommitted = true;
            ownChainSeekJunction = bestJunction;
            ownChainSeekCommittedTime = Time.time;
        }

        candidate = new CandidateAction
        {
            Action = plantNow ? BattleModeComActionType.CombatPlant : BattleModeComActionType.Reposition,
            Weight = Mathf.Max(1, Mathf.RoundToInt(settings.combatPlantWeight * (plantNow ? 1.8f : 1.2f))),
            TargetTile = plantNow ? bestSecondTile : bestJunction,
            HasTarget = true,
            FirstMove = bestFirstMove,
            HasRoute = true,
            Reason = plantNow
                ? $"own-chain start second {bestSecondTile} dir {DirectionLabel(bestDirection)}"
                : $"own-chain move to junction {bestJunction} for second {bestSecondTile}",
            InputDescription = plantNow
                ? AppendInput("ActionA", FirstMoveDescription(bestFirstMove))
                : FirstMoveDescription(bestFirstMove),
            TapBomb = plantNow
        };
        return true;
    }

    private bool TryFindOwnChainDirectionAtJunction(
        BattleModeComDifficultySettings settings,
        Vector2Int junction,
        int radius,
        out Vector2Int bestDirection,
        out Vector2Int bestSecondTile,
        out int bestScore)
    {
        bestDirection = Vector2Int.zero;
        bestSecondTile = junction;
        bestScore = int.MinValue;

        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int direction = CardinalTiles[i];
            Vector2Int midTile = junction + direction;
            Vector2Int secondTile = junction + direction * OwnChainSecondBombDistance;

            if (!IsWalkableTile(midTile, junction) || !IsWalkableTile(secondTile, junction))
                continue;

            if (IsBombAtTile(secondTile))
                continue;

            if (!TryFindPath(junction, secondTile, OwnChainSecondBombDistance + 1, false, settings, null, out PathResult path))
                continue;

            if (path.Distance != OwnChainSecondBombDistance)
                continue;

            List<Vector2Int> chainBlastTiles = BuildOwnChainPlanBlastTiles(junction, secondTile, radius);
            if (!TryFindOwnChainEscape(settings, secondTile, direction, chainBlastTiles, out _, out _))
                continue;

            int score = ScoreOwnChainDirection(junction, secondTile, radius, direction);
            if (score <= bestScore)
                continue;

            bestScore = score;
            bestDirection = direction;
            bestSecondTile = secondTile;
        }

        return bestDirection != Vector2Int.zero;
    }

    private bool TryBuildOwnChainContinuationCandidate(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        float currentDangerSeconds,
        out CandidateAction candidate)
    {
        candidate = default;

        if (!ownChainPlanActive || ownChainEscapeOnly || bombController == null)
            return false;

        if (Time.time - ownChainPlanStartedTime > OwnChainPlanTimeoutSeconds)
        {
            if (EnableChainBombDiagnostics)
                Debug.Log($"[BattleCOMOwnChain][P{playerId}] TIMEOUT origin:{ownChainOriginTile} second:{ownChainSecondTile} tile:{myTile}");

            ownChainPlanActive = false;
            ownChainEscapeOnly = false;
            return false;
        }

        int radius = Mathf.Max(1, bombController.GetPlannedExplosionRadius());
        if (!HasOwnBombAtTile(ownChainOriginTile, out float firstFuseSeconds))
        {
            ownChainPlanActive = false;
            ownChainEscapeOnly = false;
            return false;
        }

        List<Vector2Int> chainBlastTiles = BuildOwnChainPlanBlastTiles(ownChainOriginTile, ownChainSecondTile, radius);

        if (myTile == ownChainSecondTile)
        {
            if (bombController.BombsRemaining <= 0 || IsBombAtTile(myTile))
                return false;

            if (Time.time - lastBombTapTime < BombTapCooldownSeconds)
                return false;

            if (!IsWithinOwnChainPlantWindow(firstFuseSeconds) ||
                firstFuseSeconds <= settings.dangerReactionSeconds + ChainBombFuseSafetyMarginSeconds)
            {
                ownChainPlanActive = false;
                ownChainEscapeOnly = false;
                return false;
            }

            if (!TryFindOwnChainEscape(settings, myTile, ownChainLineDirection, chainBlastTiles, out Vector2 escapeMove, out Vector2Int escapeTarget))
                return false;

            Vector2Int nextChainTile = myTile;
            Vector2Int nextChainDirection = Vector2Int.zero;
            Vector2 nextChainMove = escapeMove;
            bool hasNextChainTarget =
                IsWithinOwnChainPlantWindow(firstFuseSeconds) &&
                bombController.BombsRemaining > 1 &&
                TryFindNextOwnChainTarget(
                    settings,
                    myTile,
                    radius,
                    firstFuseSeconds,
                    chainBlastTiles,
                    out nextChainTile,
                    out nextChainDirection,
                    out nextChainMove);

            Vector2 selectedMove = hasNextChainTarget ? nextChainMove : escapeMove;
            Vector2Int selectedTarget = hasNextChainTarget ? nextChainTile : myTile;
            string selectedReason = hasNextChainTarget
                ? $"own-chain plant continue #{ownChainPlantedCount + 1} origin {ownChainOriginTile} next {nextChainTile} fuse {firstFuseSeconds:F2}s"
                : $"own-chain final plant origin {ownChainOriginTile} fuse {firstFuseSeconds:F2}s escape {escapeTarget}";

            candidate = new CandidateAction
            {
                Action = BattleModeComActionType.CombatPlant,
                Weight = Mathf.Max(1, Mathf.RoundToInt(settings.combatPlantWeight * 2.0f)),
                TargetTile = selectedTarget,
                HasTarget = true,
                FirstMove = selectedMove,
                HasRoute = true,
                Reason = selectedReason,
                InputDescription = AppendInput("ActionA", FirstMoveDescription(selectedMove)),
                TapBomb = true
            };

            if (EnableChainBombDiagnostics)
            {
                Debug.Log(
                    $"[BattleCOMOwnChain][P{playerId}] PLANT_CHAIN index:{ownChainPlantedCount + 1} " +
                    $"origin:{ownChainOriginTile} planted:{myTile} fuse:{firstFuseSeconds:F2}s " +
                    $"next:{(hasNextChainTarget ? nextChainTile.ToString() : "none")} " +
                    $"escape:{escapeTarget} move:{FirstMoveDescription(selectedMove)}");
            }

            ownChainLastPlantedTile = myTile;
            ownChainPlantedCount++;

            if (hasNextChainTarget)
            {
                ownChainSecondTile = nextChainTile;
                ownChainLineDirection = nextChainDirection;
                ownChainEscapeOnly = false;
            }
            else
            {
                ownChainEscapeOnly = true;
            }

            return true;
        }

        if (!TryFindPath(myTile, ownChainSecondTile, settings.searchDepth + 4, false, settings, null, out PathResult path))
            return false;

        float arrivalSeconds = EstimateTraversalSeconds(path.Distance);
        if (!IsWithinOwnChainPlantWindow(firstFuseSeconds) ||
            firstFuseSeconds <= arrivalSeconds + settings.dangerReactionSeconds + ChainBombFuseSafetyMarginSeconds)
        {
            ownChainPlanActive = false;
            ownChainEscapeOnly = false;
            return false;
        }

        candidate = new CandidateAction
        {
            Action = BattleModeComActionType.Reposition,
            Weight = Mathf.Max(1, Mathf.RoundToInt(settings.combatPlantWeight * 1.5f)),
            TargetTile = ownChainSecondTile,
            HasTarget = true,
            FirstMove = path.FirstMove,
            HasRoute = true,
            Reason = $"own-chain move to second {ownChainSecondTile} fuse {firstFuseSeconds:F2}s",
            InputDescription = FirstMoveDescription(path.FirstMove),
            TapBomb = false
        };
        return true;
    }

    private List<Vector2Int> BuildOwnChainPlanBlastTiles(Vector2Int firstTile, Vector2Int secondTile, int radius)
    {
        List<Vector2Int> blastTiles = BuildBlastTiles(firstTile, radius);
        List<Vector2Int> secondBlast = BuildBlastTiles(secondTile, radius);

        for (int i = 0; i < secondBlast.Count; i++)
        {
            if (!blastTiles.Contains(secondBlast[i]))
                blastTiles.Add(secondBlast[i]);
        }

        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null ||
                bomb.HasExploded ||
                bomb.IsBeingHeldByPowerGlove ||
                bomb.Owner != bombController ||
                bomb.IsControlBomb)
                continue;

            Vector2Int bombTile = WorldToTile(bomb.GetLogicalPosition());
            List<Vector2Int> bombBlast = BuildBlastTiles(bombTile, radius);
            for (int i = 0; i < bombBlast.Count; i++)
            {
                if (!blastTiles.Contains(bombBlast[i]))
                    blastTiles.Add(bombBlast[i]);
            }
        }

        return blastTiles;
    }

    private bool IsWithinOwnChainPlantWindow(float firstFuseSeconds)
    {
        if (bombController == null)
            return false;

        float initialFuseSeconds = Mathf.Max(0.01f, bombController.bombFuseTime);
        float elapsedFuseSeconds = Mathf.Max(0f, initialFuseSeconds - firstFuseSeconds);
        return elapsedFuseSeconds <= OwnChainFirstFusePlantWindowSeconds;
    }

    private bool TryFindNextOwnChainTarget(
        BattleModeComDifficultySettings settings,
        Vector2Int currentTile,
        int radius,
        float firstFuseSeconds,
        List<Vector2Int> currentBlastTiles,
        out Vector2Int nextChainTile,
        out Vector2Int nextChainDirection,
        out Vector2 nextChainMove)
    {
        nextChainTile = currentTile;
        nextChainDirection = Vector2Int.zero;
        nextChainMove = Vector2.zero;

        Vector2Int backDelta = ownChainLastPlantedTile - currentTile;
        Vector2Int backDirection = backDelta == Vector2Int.zero
            ? Vector2Int.zero
            : DirectionToTile(new Vector2(backDelta.x, backDelta.y));

        int bestScore = int.MinValue;

        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int direction = CardinalTiles[i];
            if (direction == backDirection)
                continue;

            Vector2Int midTile = currentTile + direction;
            Vector2Int candidateTile = currentTile + direction * OwnChainSecondBombDistance;

            if (!IsWalkableTile(midTile, currentTile) || !IsWalkableTile(candidateTile, currentTile))
                continue;

            if (IsBombAtTile(candidateTile))
                continue;

            if (!TryFindPath(currentTile, candidateTile, OwnChainSecondBombDistance + 1, false, settings, null, out PathResult path))
                continue;

            if (path.Distance != OwnChainSecondBombDistance)
                continue;

            float arrivalSeconds = EstimateTraversalSeconds(path.Distance);
            if (firstFuseSeconds <= arrivalSeconds + settings.dangerReactionSeconds + ChainBombFuseSafetyMarginSeconds)
                continue;

            List<Vector2Int> plannedBlastTiles = new List<Vector2Int>(currentBlastTiles);
            List<Vector2Int> candidateBlastTiles = BuildBlastTiles(candidateTile, radius);
            for (int b = 0; b < candidateBlastTiles.Count; b++)
            {
                if (!plannedBlastTiles.Contains(candidateBlastTiles[b]))
                    plannedBlastTiles.Add(candidateBlastTiles[b]);
            }

            if (!TryFindOwnChainEscape(settings, candidateTile, direction, plannedBlastTiles, out _, out Vector2Int escapeTarget))
                continue;

            int score =
                ScoreOwnChainDirection(currentTile, candidateTile, radius, direction) +
                (direction == ownChainLineDirection ? 12 : 6) +
                CountOpenNeighbors(escapeTarget) * 3;

            if (score <= bestScore)
                continue;

            bestScore = score;
            nextChainTile = candidateTile;
            nextChainDirection = direction;
            nextChainMove = path.FirstMove;
        }

        return nextChainDirection != Vector2Int.zero;
    }

    private bool TryFindOwnChainEscape(
        BattleModeComDifficultySettings settings,
        Vector2Int secondTile,
        Vector2Int lineDirection,
        List<Vector2Int> chainBlastTiles,
        out Vector2 escapeMove,
        out Vector2Int escapeTarget)
    {
        escapeMove = Vector2.zero;
        escapeTarget = secondTile;

        Vector2Int towardFirstBomb = -lineDirection;
        Vector2Int bestStep = Vector2Int.zero;
        Vector2Int bestTarget = secondTile;
        int bestScore = int.MinValue;

        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int firstStep = CardinalTiles[i];
            if (firstStep == towardFirstBomb)
                continue;

            Vector2Int next = secondTile + firstStep;
            if (!IsWalkableTile(next, secondTile))
                continue;

            if (!TryFindEscapeWithPreferredFirstStep(settings, secondTile, firstStep, chainBlastTiles, out Vector2Int target, out int depth))
                continue;

            int score =
                (firstStep == lineDirection ? 5 : 20) +
                CountOpenNeighbors(target) * 4 -
                depth;

            if (score <= bestScore)
                continue;

            bestScore = score;
            bestStep = firstStep;
            bestTarget = target;
        }

        if (bestStep == Vector2Int.zero)
            return false;

        escapeMove = TileDirectionToVector(bestStep);
        escapeTarget = bestTarget;
        return true;
    }

    private bool TryFindEscapeWithPreferredFirstStep(
        BattleModeComDifficultySettings settings,
        Vector2Int start,
        Vector2Int firstStep,
        List<Vector2Int> plannedBlastTiles,
        out Vector2Int target,
        out int depth)
    {
        target = start;
        depth = 0;

        Vector2Int firstTile = start + firstStep;
        if (!IsWalkableTile(firstTile, start))
            return false;

        visited.Clear();
        open.Clear();
        visited[start] = new PathNode { Tile = start, Parent = start, Depth = 0 };
        visited[firstTile] = new PathNode { Tile = firstTile, Parent = start, Depth = 1 };
        open.Enqueue(firstTile);

        while (open.Count > 0)
        {
            Vector2Int tile = open.Dequeue();
            PathNode node = visited[tile];
            float eta = EstimateEscapeTraversalSeconds(start, tile, node.Depth);
            bool inChainBlast = plannedBlastTiles != null && plannedBlastTiles.Contains(tile);
            float dangerSeconds = GetDangerSeconds(tile, plannedBlastTiles);

            if (node.Depth > 0 &&
                !inChainBlast &&
                (float.IsInfinity(dangerSeconds) ||
                 dangerSeconds > eta + settings.dangerReactionSeconds + settings.safeTileMinimumSeconds))
            {
                target = tile;
                depth = node.Depth;
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

                int nextDepth = node.Depth + 1;
                float nextEta = EstimateEscapeTraversalSeconds(start, tile, next, nextDepth);
                if (IsDangerousAt(next, nextEta, settings, plannedBlastTiles))
                    continue;

                visited[next] = new PathNode { Tile = next, Parent = tile, Depth = nextDepth };
                open.Enqueue(next);
            }
        }

        return false;
    }

    private int ScoreOwnChainDirection(Vector2Int origin, Vector2Int secondTile, int radius, Vector2Int direction)
    {
        int score = CountOpenNeighbors(secondTile) * 8;

        if (TryFindNearestEnemy(origin, out _, out Vector2Int enemyTile, out _))
        {
            int before = Manhattan(origin, enemyTile);
            int after = Manhattan(secondTile, enemyTile);
            score += Mathf.Max(0, before - after) * 6;

            if (IsTileInBlastLineRuntime(secondTile, enemyTile, radius))
                score += 30;
        }

        if (CanBombHitDestructible(secondTile, radius))
            score += 10;

        score += Mathf.RoundToInt(GetDecisionNoise(6100 + DirectionToScoreSalt(direction), FarmTargetJitter) * 10f);
        return score;
    }

    private int DirectionToScoreSalt(Vector2Int direction)
    {
        if (direction == Vector2Int.up)
            return 1;

        if (direction == Vector2Int.down)
            return 2;

        if (direction == Vector2Int.left)
            return 3;

        if (direction == Vector2Int.right)
            return 4;

        return 0;
    }

    private bool HasOwnBombAtTile(Vector2Int tile, out float remainingFuseSeconds)
    {
        remainingFuseSeconds = float.PositiveInfinity;

        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null ||
                bomb.HasExploded ||
                bomb.IsBeingHeldByPowerGlove ||
                bomb.Owner != bombController ||
                bomb.IsControlBomb)
                continue;

            if (WorldToTile(bomb.GetLogicalPosition()) != tile)
                continue;

            remainingFuseSeconds = bomb.RemainingFuseSeconds;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Tenta encontrar um tile dentro do blast de uma bomba INIMIGA onde podemos
    /// plantar nossa bomba para criar uma chain explosion. A IA caminha até o tile
    /// e planta lá enquanto o fuse inimigo ainda está ativo.
    /// </summary>
    private bool TryBuildCommittedWalkToChainPlantCandidate(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out CandidateAction candidate)
    {
        candidate = default;

        if (!walkToChainCommitted || myTile != walkToChainCommittedTile)
            return false;

        if (bombController == null || bombController.BombsRemaining <= 0)
            return false;

        if (IsBombAtTile(myTile))
            return false;

        if (Time.time - lastBombTapTime < BombTapCooldownSeconds)
            return false;

        if (!HasActiveNonOwnChainTriggerAtTile(myTile, settings, out float triggerSeconds))
            return false;

        int radius = Mathf.Max(1, bombController.GetPlannedExplosionRadius());
        if (!CanPlantBombWithEscape(myTile, radius, settings, out Vector2 escapeMove, out _))
            return false;

        candidate = new CandidateAction
        {
            Action = BattleModeComActionType.CombatPlant,
            Weight = Mathf.Max(1, Mathf.RoundToInt(settings.combatPlantWeight * 1.5f)),
            TargetTile = myTile,
            HasTarget = true,
            FirstMove = escapeMove,
            HasRoute = true,
            Reason = $"walk-to-chain committed PLANT at {myTile} trigger {triggerSeconds:F2}s",
            InputDescription = AppendInput("ActionA", FirstMoveDescription(escapeMove)),
            TapBomb = true
        };
        return true;
    }

    private bool HasActiveNonOwnChainTriggerAtTile(
        Vector2Int tile,
        BattleModeComDifficultySettings settings,
        out float triggerSeconds)
    {
        triggerSeconds = float.PositiveInfinity;

        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null ||
                bomb.HasExploded ||
                bomb.IsBeingHeldByPowerGlove ||
                bomb.Owner == bombController)
                continue;

            float fuseLeft = bomb.IsControlBomb ? 0.65f : bomb.RemainingFuseSeconds;
            if (fuseLeft <= settings.dangerReactionSeconds + ChainBombFuseSafetyMarginSeconds)
                continue;

            Vector2Int bombTile = WorldToTile(bomb.GetLogicalPosition());
            int bombRadius = bomb.Owner != null
                ? Mathf.Max(1, bomb.Owner.GetPredictedBlastRadius(bomb))
                : Mathf.Max(1, bombController != null ? bombController.GetPlannedExplosionRadius() : 1);

            if (!IsTileInBlastLineRuntime(bombTile, tile, bombRadius))
                continue;

            triggerSeconds = Mathf.Min(triggerSeconds, fuseLeft);
        }

        return !float.IsInfinity(triggerSeconds);
    }

    private bool TryBuildWalkToChainCandidate(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        float currentDangerSeconds,
        out CandidateAction candidate)
    {
        candidate = default;

        if (bombController == null || bombController.BombsRemaining <= 0)
            return false;

        int myRadius = Mathf.Max(1, bombController.GetPlannedExplosionRadius());

        int wtc_noPath = 0, wtc_arrivalLate = 0, wtc_noEscape = 0;
        int wtc_noUseful = 0, wtc_bombAtTile = 0, wtc_evaluated = 0;

        Vector2Int bestTile = default;
        Vector2 bestMove = Vector2.zero;
        Vector2 bestEscapeMove = Vector2.zero;
        bool bestPlantNow = false;
        float bestScore = float.NegativeInfinity;
        bool found = false;

        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded || bomb.IsBeingHeldByPowerGlove)
                continue;

            // Só bombas inimigas (não nossas)
            if (bomb.Owner == bombController)
                continue;

            float fuseLeft = bomb.RemainingFuseSeconds;
            if (fuseLeft <= 0f)
                continue;

            Vector2Int bombTile = WorldToTile(bomb.GetLogicalPosition());
            int bombRadius = bomb.Owner != null ? Mathf.Max(1, bomb.Owner.GetPredictedBlastRadius(bomb)) : myRadius;
            List<Vector2Int> blastTiles = BuildBlastTiles(bombTile, bombRadius);

            for (int i = 0; i < blastTiles.Count; i++)
            {
                Vector2Int plantTile = blastTiles[i];

                if (IsBombAtTile(plantTile))
                { wtc_bombAtTile++; continue; }

                // Precisamos chegar antes do fuse (com margem de segurança)
                float requiredArrival = fuseLeft - settings.dangerReactionSeconds - ChainBombFuseSafetyMarginSeconds;
                if (requiredArrival <= 0f)
                    continue;

                if (!TryFindPath(myTile, plantTile, settings.searchDepth + 4, false, settings, null, out PathResult path))
                { wtc_noPath++; continue; }

                float arrivalSeconds = path.Distance == 0 ? 0f : EstimateTraversalSeconds(path.Distance);
                if (arrivalSeconds >= requiredArrival)
                { wtc_arrivalLate++; continue; }

                if (!CanPlantBombWithEscape(plantTile, myRadius, settings, out Vector2 escapeMove, out _))
                { wtc_noEscape++; continue; }

                wtc_evaluated++;

                // Diagnóstico de utilidade (não bloqueia)
                bool hitsEnemy = TryFindNearestEnemy(myTile, out _, out Vector2Int enemyTile, out _) &&
                                 IsTileInBlastLineRuntime(plantTile, enemyTile, myRadius);
                bool hitsUseful = CanBombHitDestructible(plantTile, myRadius) || hitsEnemy;
                if (!hitsUseful) wtc_noUseful++;

                bool plantNow = path.Distance == 0;
                // Preferir tiles mais perto e que sejam úteis
                float score = -arrivalSeconds * 10f + (hitsUseful ? 5f : 0f) + (plantNow ? 20f : 0f);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTile = plantTile;
                    bestMove = path.FirstMove;
                    bestEscapeMove = escapeMove;
                    bestPlantNow = plantNow;
                    found = true;
                }
            }
        }

        if (!found)
        {
            if (EnableChainBombDiagnostics && (wtc_noPath + wtc_arrivalLate + wtc_noEscape + wtc_bombAtTile) > 0)
                Debug.Log($"[BattleCOMWalkChain][P{playerId}] NO_CANDIDATE noPath:{wtc_noPath} late:{wtc_arrivalLate} noEscape:{wtc_noEscape} bombAt:{wtc_bombAtTile} noUseful:{wtc_noUseful}");
            return false;
        }

        if (EnableChainBombDiagnostics)
            Debug.Log($"[BattleCOMWalkChain][P{playerId}] FOUND target:{bestTile} plantNow:{bestPlantNow} evaluated:{wtc_evaluated} noUseful:{wtc_noUseful}");

        candidate = new CandidateAction
        {
            Action = bestPlantNow ? BattleModeComActionType.CombatPlant : BattleModeComActionType.Reposition,
            Weight = Mathf.Max(1, Mathf.RoundToInt(settings.combatPlantWeight * 1.5f)),
            TargetTile = bestTile,
            HasTarget = true,
            FirstMove = bestPlantNow ? bestEscapeMove : bestMove,
            HasRoute = true,
            Reason = bestPlantNow
                ? $"walk-to-chain PLANT at {bestTile}"
                : $"walk-to-chain MOVE to {bestTile}",
            InputDescription = bestPlantNow
                ? AppendInput("ActionA", FirstMoveDescription(bestEscapeMove))
                : FirstMoveDescription(bestMove),
            TapBomb = bestPlantNow
        };
        return true;
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
        {
            if (HasOwnUnresolvedBombOrExplosion())
            {
                LogPostPlantEscapeDiagnostic(
                    "CHAIN_NO_BOMBS",
                    myTile,
                    currentDangerSeconds,
                    $"bombController:{(bombController != null)} bombsRemaining:{(bombController != null ? bombController.BombsRemaining : -1)}");
            }
            LogChainBombDiagnostic("NO_BOMBS", myTile, currentDangerSeconds,
                $"sem bomba disponível bombController:{bombController != null} " +
                $"bombsRemaining:{(bombController != null ? bombController.BombsRemaining : -1)}/" +
                $"{(bombController != null ? bombController.bombAmout : -1)}");
            return false;
        }

        int radius = Mathf.Max(1, bombController.GetPlannedExplosionRadius());
        GatherReachableSafeTiles(myTile, onlyCurrentTile ? 0 : settings.searchDepth + 2, settings);

        LogChainBombDiagnostic("SCAN", myTile, currentDangerSeconds,
            $"radius:{radius} reachable:{reachableTiles.Count} onlyCurrent:{onlyCurrentTile} " +
            $"own:{FormatOwnChainBombs()} fuseMargin:{ChainBombFuseSafetyMarginSeconds:F2}s " +
            $"minBranches:{ChainBombMinimumEscapeBranches} reaction:{settings.dangerReactionSeconds:F2}s");

        Vector2Int bestTile = myTile;
        Vector2 bestMove = Vector2.zero;
        Vector2 bestEscapeMove = Vector2.zero;
        float bestScore = float.NegativeInfinity;
        int bestBranches = 0;
        float bestTriggerSeconds = float.PositiveInfinity;
        int bestDistance = int.MaxValue;
        bool bestCreatesTurn = false;
        Vector2Int bestEscapeTarget = myTile;

        int cntBombAtTile = 0, cntBlastHitsItem = 0, cntNoLink = 0, cntFuseShort = 0;
        int cntNoBlastTiles = 0, cntFewBranches = 0, cntNoChainEscape = 0;
        int cntNoPath = 0, cntDangerArrival = 0, cntFuseArrivalUnsafe = 0, cntEvaluated = 0;
        string rejectSamples = string.Empty;

        for (int i = 0; i < reachableTiles.Count; i++)
        {
            Vector2Int tile = reachableTiles[i];
            if (onlyCurrentTile && tile != myTile)
                continue;

            if (IsBombAtTile(tile))
            {
                cntBombAtTile++;
                AddChainRejectSample(ref rejectSamples, $"bombAt:{tile}");
                continue;
            }

            if (WouldBlastHitUsefulItem(tile, radius, out ItemType hitItemType, out Vector2Int hitItemTile))
            {
                cntBlastHitsItem++;
                AddChainRejectSample(ref rejectSamples, $"item:{tile}->{hitItemType}@{hitItemTile}");
                RejectVerbose($"ChainBomb recusado queimaria item {hitItemType}@{hitItemTile} tile:{tile}");
                continue;
            }

            if (!TryGetLinkedOwnBombTriggerSeconds(
                    tile,
                    radius,
                    out float triggerSeconds,
                    out bool createsTurn,
                    out Vector2Int linkedBombTile,
                    out float linkedBombRemainingSeconds))
            {
                cntNoLink++;
                AddChainRejectSample(ref rejectSamples, $"noLink:{tile}");
                continue;
            }

            if (triggerSeconds <= settings.dangerReactionSeconds + ChainBombFuseSafetyMarginSeconds)
            {
                cntFuseShort++;
                AddChainRejectSample(ref rejectSamples, $"fuseShort:{tile} link:{linkedBombTile} fuse:{linkedBombRemainingSeconds:F2}s");
                RejectVerbose($"ChainBomb fuse curto {tile} {triggerSeconds:F2}s");
                continue;
            }

            if (!TryBuildChainBlastTiles(tile, radius, out List<Vector2Int> chainBlastTiles))
            {
                cntNoBlastTiles++;
                AddChainRejectSample(ref rejectSamples, $"noBlast:{tile} link:{linkedBombTile}");
                continue;
            }

            int escapeBranches = CountImmediateEscapeBranches(tile, chainBlastTiles);
            if (escapeBranches < ChainBombMinimumEscapeBranches)
            {
                cntFewBranches++;
                AddChainRejectSample(ref rejectSamples, $"fewBranches:{tile} link:{linkedBombTile} branches:{escapeBranches}");
                RejectVerbose($"ChainBomb poucas fugas {tile} branches {escapeBranches}");
                continue;
            }

            if (!TryFindChainEscape(settings, tile, chainBlastTiles, triggerSeconds, out Vector2 escapeMove, out Vector2Int escapeTarget))
            {
                cntNoChainEscape++;
                AddChainRejectSample(ref rejectSamples, $"noEscape:{tile} link:{linkedBombTile} trigger:{triggerSeconds:F2}s blast:{chainBlastTiles.Count}");
                RejectVerbose($"ChainBomb sem fuga {tile}");
                continue;
            }

            if (!TryFindPath(myTile, tile, settings.searchDepth + 2, false, settings, null, out PathResult path))
            {
                cntNoPath++;
                AddChainRejectSample(ref rejectSamples, $"noPath:{tile} link:{linkedBombTile}");
                continue;
            }

            float arrivalSeconds = EstimateTraversalSeconds(path.Distance);
            if (!float.IsInfinity(currentDangerSeconds) &&
                currentDangerSeconds <= arrivalSeconds + settings.dangerReactionSeconds)
            {
                cntDangerArrival++;
                AddChainRejectSample(ref rejectSamples, $"dangerArrival:{tile} eta:{arrivalSeconds:F2}s danger:{FormatDanger(currentDangerSeconds)}");
                continue;
            }

            if (triggerSeconds <= arrivalSeconds + settings.safeTileMinimumSeconds + ChainBombFuseSafetyMarginSeconds)
            {
                cntFuseArrivalUnsafe++;
                AddChainRejectSample(ref rejectSamples, $"arrivalUnsafe:{tile} link:{linkedBombTile} fuse:{triggerSeconds:F2}s eta:{arrivalSeconds:F2}s");
                RejectVerbose($"ChainBomb chegada insegura {tile} fuse {triggerSeconds:F2}s eta {arrivalSeconds:F2}s");
                continue;
            }

            cntEvaluated++;

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
            bestEscapeTarget = escapeTarget;
        }

        if (bestDistance == int.MaxValue)
        {
            if (HasOwnUnresolvedBombOrExplosion())
            {
                LogPostPlantEscapeDiagnostic(
                    onlyCurrentTile ? "CHAIN_CURRENT_NONE" : "CHAIN_SEARCH_NONE",
                    myTile,
                    currentDangerSeconds,
                    $"reachable:{reachableTiles.Count} rejected:{FormatRejectedForLog()}");
            }

            LogChainBombDiagnostic(
                onlyCurrentTile ? "FAIL_CURRENT_TILE" : "FAIL_ALL_TILES",
                myTile,
                currentDangerSeconds,
                $"reachable:{reachableTiles.Count} " +
                $"bombAtTile:{cntBombAtTile} blastHitsItem:{cntBlastHitsItem} " +
                $"noLink:{cntNoLink} fuseShort:{cntFuseShort} noBlastTiles:{cntNoBlastTiles} " +
                $"fewBranches(min:{ChainBombMinimumEscapeBranches}):{cntFewBranches} " +
                $"noChainEscape:{cntNoChainEscape} noPath:{cntNoPath} " +
                $"dangerArrival:{cntDangerArrival} fuseArrivalUnsafe:{cntFuseArrivalUnsafe} " +
                $"evaluated:{cntEvaluated} samples:{(string.IsNullOrEmpty(rejectSamples) ? "none" : rejectSamples)} " +
                $"own:{FormatOwnChainBombs()}",
                force: true);
            return false;
        }

        LogChainBombDiagnostic(
            bestDistance == 0 ? "FOUND_PLANT_NOW" : "FOUND_MOVE_TO",
            myTile,
            currentDangerSeconds,
            $"bestTile:{bestTile} dist:{bestDistance} branches:{bestBranches} " +
            $"trigger:{bestTriggerSeconds:F2}s createsTurn:{bestCreatesTurn} " +
            $"escapeMove:{FirstMoveDescription(bestEscapeMove)} escapeTarget:{bestEscapeTarget} " +
            $"reachable:{reachableTiles.Count} noLink:{cntNoLink} fewBranches:{cntFewBranches} " +
            $"noChainEscape:{cntNoChainEscape} evaluated:{cntEvaluated} own:{FormatOwnChainBombs()}",
            force: true);

        bool plantNow = bestDistance == 0;
        if (plantNow && !float.IsInfinity(currentDangerSeconds))
        {
            LogPostPlantEscapeDiagnostic(
                "CHAIN_REJECT_DANGER_PLANT",
                myTile,
                currentDangerSeconds,
                $"best:{bestTile} trigger:{bestTriggerSeconds:F2}s branches:{bestBranches} escapeMove:{FirstMoveDescription(bestEscapeMove)}");
            RejectVerbose($"ChainBomb recusado em perigo {myTile} danger:{FormatDanger(currentDangerSeconds)}");
            return false;
        }

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
        if (HasOwnUnresolvedBombOrExplosion())
        {
            LogPostPlantEscapeDiagnostic(
                plantNow ? "CHAIN_PLANT_NOW" : "CHAIN_MOVE_TO_PLANT",
                myTile,
                currentDangerSeconds,
                $"best:{bestTile} distance:{bestDistance} move:{FirstMoveDescription(candidate.FirstMove)} " +
                $"escapeMove:{FirstMoveDescription(bestEscapeMove)} branches:{bestBranches} trigger:{bestTriggerSeconds:F2}s " +
                $"createsTurn:{bestCreatesTurn}");
        }
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
            float arrivalSeconds = EstimateFirstMoveTraversalSeconds(CardinalTiles[i]);
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

    private bool TryBuildOwnPendingSafetyMove(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        float currentDangerSeconds,
        out Vector2 move,
        out Vector2Int target,
        out string reason)
    {
        move = Vector2.zero;
        target = myTile;
        reason = "no own pending safety move";

        int currentThreatDistance = GetClosestOwnPendingThreatDistance(myTile);
        if (currentThreatDistance == int.MaxValue)
            return false;

        // Se temos um escape target comprometido (vindo de TryBuildOwnPendingEscapeRoute),
        // tentamos navegar BFS em direção a ele em vez de fazer a avaliação greedy de 1 passo.
        // Isso evita que o safety move oscile entre dois tiles adjacentes com scores similares.
        if (committedEscapeExpiresTime > Time.time &&
            committedEscapeTarget.x != int.MinValue &&
            committedEscapeTarget != myTile)
        {
            float targetDanger = GetDangerSeconds(committedEscapeTarget, null);
            bool targetStillSafe = float.IsInfinity(targetDanger);
            if (targetStillSafe &&
                TryFindEscape(settings, myTile, null, out Vector2 escapeFm, out Vector2Int escapeT, out string escapeR))
            {
                // Só usa o BFS se ele nos leva em direção ao committed target (ou a algum tile seguro próximo)
                move = escapeFm;
                target = committedEscapeTarget;
                reason = $"committed escape target {committedEscapeTarget} bfs:{escapeT} {escapeR}";
                return true;
            }
            else
            {
                // Target ficou perigoso ou BFS falhou — invalida o commit
                committedEscapeExpiresTime = -10f;
            }
        }

        float currentScore = ScoreOwnPendingSafetyTile(myTile, 0f, settings);
        float bestScore = currentScore;
        Vector2Int bestDirection = Vector2Int.zero;
        Vector2Int bestTile = myTile;
        int bestThreatDistance = currentThreatDistance;
        float bestArrival = 0f;
        bool standingOnOwnThreat = currentThreatDistance == 0;

        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int direction = CardinalTiles[i];
            Vector2Int next = myTile + direction;
            if (!IsWalkableTile(next, myTile))
                continue;

            float arrivalSeconds = EstimateFirstMoveTraversalSeconds(direction);
            if (IsDangerousAt(next, arrivalSeconds, settings, null))
                continue;

            float score = ScoreOwnPendingSafetyTile(next, arrivalSeconds, settings);
            int threatDistance = GetClosestOwnPendingThreatDistance(next);
            if (standingOnOwnThreat)
            {
                if (threatDistance <= currentThreatDistance)
                    continue;
            }
            else if (score <= bestScore + 0.5f)
            {
                continue;
            }

            bestScore = score;
            bestDirection = direction;
            bestTile = next;
            bestThreatDistance = threatDistance;
            bestArrival = arrivalSeconds;
        }

        if (bestDirection == Vector2Int.zero)
            return false;

        move = TileDirectionToVector(bestDirection);
        target = bestTile;
        reason =
            $"own pending safety dist {currentThreatDistance}->{bestThreatDistance} " +
            $"score {currentScore:F2}->{bestScore:F2} eta {bestArrival:F2}s";
        return true;
    }

    private bool TryBuildOwnPendingEscapeRoute(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        float currentDangerSeconds,
        out Vector2 move,
        out Vector2Int target,
        out string reason)
    {
        move = Vector2.zero;
        target = myTile;
        reason = "no own pending escape route";

        int currentThreatDistance = GetClosestOwnPendingThreatDistance(myTile);
        if (currentThreatDistance == int.MaxValue)
            return false;

        if (!TryFindEscape(settings, myTile, null, out Vector2 firstMove, out Vector2Int escapeTarget, out string route))
        {
            reason = $"own pending no bfs escape dist {currentThreatDistance}";
            return false;
        }

        Vector2Int firstStep = DirectionToTile(firstMove);
        if (firstStep == Vector2Int.zero)
            return false;

        float arrivalSeconds = EstimateFirstMoveTraversalSeconds(firstStep);
        float firstStepMargin = GetSurvivalMarginSeconds(myTile + firstStep, arrivalSeconds, settings, null);
        bool standingOnOwnThreat = currentThreatDistance == 0;
        int nextThreatDistance = GetClosestOwnPendingThreatDistance(myTile + firstStep);

        if (standingOnOwnThreat && nextThreatDistance <= currentThreatDistance)
        {
            reason = $"own pending escape first step does not leave threat dist {currentThreatDistance}->{nextThreatDistance}";
            return false;
        }

        if (!standingOnOwnThreat &&
            firstStepMargin < PostPlantEmergencyEscapeMarginSeconds &&
            !float.IsInfinity(currentDangerSeconds))
        {
            reason =
                $"own pending escape rejected low first margin {firstStepMargin:F2}s " +
                $"danger:{FormatDanger(currentDangerSeconds)} target:{escapeTarget}";
            return false;
        }

        // Não fazemos centering explícito aqui: o SetCurrentDecision com route "escape ownPending"
        // ativa currentMoveFollowsEscapeRoute = true, e ApplyTurnAxisCentering (post-processing)
        // já faz o centering de eixo corretamente. Fazer centering aqui é redundante e causa
        // oscilação porque a direção retornada muda frame-a-frame conforme a sub-posição do tile.
        move = firstMove;
        target = escapeTarget;
        reason =
            $"own pending escape route {route} first:{FirstMoveDescription(firstMove)} " +
            $"target:{escapeTarget} margin:{firstStepMargin:F2}s dist {currentThreatDistance}->{nextThreatDistance}";

        // Grava o target de fuga comprometido para que TryBuildOwnPendingSafetyMove
        // não oscile entre direções greedy enquanto ainda não chegamos lá.
        committedEscapeTarget = escapeTarget;
        committedEscapeExpiresTime = Time.time + CommittedEscapeLifetimeSeconds;

        return true;
    }

    private float ScoreOwnPendingSafetyTile(
        Vector2Int tile,
        float arrivalSeconds,
        BattleModeComDifficultySettings settings)
    {
        float dangerSeconds = GetDangerSeconds(tile, null);
        int threatDistance = GetClosestOwnPendingThreatDistance(tile);
        if (!float.IsInfinity(dangerSeconds))
        {
            float distanceBonus = threatDistance == int.MaxValue ? 0f : threatDistance * 0.75f;
            return dangerSeconds - arrivalSeconds - settings.dangerReactionSeconds + distanceBonus;
        }

        float distanceScore = threatDistance == int.MaxValue ? 0f : threatDistance * 6f;
        return 50f + distanceScore + CountOpenNeighbors(tile) * 1.5f - arrivalSeconds;
    }

    private int GetClosestOwnPendingThreatDistance(Vector2Int tile)
    {
        if (bombController == null)
            return int.MaxValue;

        int best = int.MaxValue;
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded || bomb.IsBeingHeldByPowerGlove)
                continue;

            if (bomb.Owner != bombController || bomb.IsControlBomb || bomb.WasMovedByKickOrPunch)
                continue;

            best = Mathf.Min(best, Manhattan(tile, WorldToTile(bomb.GetLogicalPosition())));
        }

        BombExplosion[] explosions = FindObjectsByType<BombExplosion>(FindObjectsInactive.Exclude);
        for (int i = 0; i < explosions.Length; i++)
        {
            BombExplosion explosion = explosions[i];
            if (explosion == null || explosion.Owner != bombController)
                continue;

            best = Mathf.Min(best, Manhattan(tile, WorldToTile(explosion.transform.position)));
        }

        return best;
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

        // Só persegue inimigo se estiver a distância útil.
        // Se enemyDistance < PatrolMinEnemyDistance, todos se amontoam e ficam com move:none (distance 0).
        // Se enemyPath.FirstMove == zero (mesmo tile), também cai no wander.
        const int PatrolMinEnemyDistance = 3;
        PathResult enemyPath = default;
        bool hasUsefulEnemyPath =
            TryFindNearestEnemy(myTile, out PlayerIdentity target, out Vector2Int enemyTile, out int enemyDistance) &&
            enemyDistance >= PatrolMinEnemyDistance &&
            TryFindPath(myTile, enemyTile, settings.searchDepth + 4, true, settings, null, out enemyPath) &&
            enemyPath.FirstMove != Vector2.zero;

        if (hasUsefulEnemyPath)
        {
            move = enemyPath.FirstMove;
            targetTile = enemyTile;
            reason = $"patrol toward P{target.playerId} distance {enemyDistance}";
        }
        else
        {
            // Usa wander target persistente para forçar a IA a explorar toda a arena
            // em vez de ficar ciclando entre tiles adjacentes no mesmo canto.
            if (TryGetWanderTarget(settings, myTile, out Vector2Int wander) &&
                TryFindPath(myTile, wander, WanderBfsDepth, true, settings, null, out PathResult wanderPath) &&
                wanderPath.FirstMove != Vector2.zero)
            {
                move = wanderPath.FirstMove;
                targetTile = wander;
                reason = $"wander toward {wander} dist {Manhattan(myTile, wander)}";
            }
            else
            {
                // Invalida o wander target se BFS falhou
                wanderTargetExpiresTime = -10f;
                move = FindBestFallbackMove(settings, myTile, null, out reason);
                if (move == Vector2.zero)
                    return false;

                targetTile = myTile + DirectionToTile(move);
            }
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

        LogPatrolDiagnostic(myTile, targetTile, move, reason);
        return true;
    }

    private void LogChainBombDiagnostic(string key, Vector2Int myTile, float currentDangerSeconds, string message, bool force = false)
    {
        if (!EnableChainBombDiagnostics)
            return;

        if (ChainBombDiagnosticPlayerIdFilter != 0 && playerId != ChainBombDiagnosticPlayerIdFilter)
            return;

        string logKey = $"{key}:{myTile}";
        if (!force &&
            logKey == lastChainBombLogKey &&
            Time.time - lastChainBombLogTime < ChainBombDiagLogIntervalSeconds)
        {
            return;
        }

        lastChainBombLogKey = logKey;
        lastChainBombLogTime = Time.time;

        int bombsRemaining = bombController != null ? bombController.BombsRemaining : -1;
        int totalBombs = bombController != null ? bombController.bombAmout : -1;
        Debug.Log(
            $"[BattleCOMChain][P{playerId}] frame:{Time.frameCount} t:{Time.time:F2} " +
            $"tile:{myTile} danger:{FormatDanger(currentDangerSeconds)} " +
            $"bombsRemaining:{bombsRemaining}/{totalBombs} key:{key} {message}",
            this);
    }

    private void AddChainRejectSample(ref string samples, string sample)
    {
        if (string.IsNullOrEmpty(sample))
            return;

        int count = 0;
        if (!string.IsNullOrEmpty(samples))
        {
            for (int i = 0; i < samples.Length; i++)
            {
                if (samples[i] == '|')
                    count++;
            }

            count++;
        }

        if (count >= ChainBombRejectSampleLimit)
            return;

        if (!string.IsNullOrEmpty(samples))
            samples += " | ";

        samples += sample;
    }

    private string FormatOwnChainBombs()
    {
        if (bombController == null)
            return "none";

        string result = string.Empty;
        int count = 0;
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null ||
                bomb.HasExploded ||
                bomb.IsBeingHeldByPowerGlove ||
                bomb.Owner != bombController ||
                bomb.IsControlBomb)
                continue;

            if (count > 0)
                result += ",";

            Vector2Int tile = WorldToTile(bomb.GetLogicalPosition());
            result += $"{tile}:{bomb.RemainingFuseSeconds:F2}s";
            count++;
        }

        return count > 0 ? result : "none";
    }

    private void LogPatrolDiagnostic(Vector2Int myTile, Vector2Int targetTile, Vector2 move, string reason)
    {
        if (!EnablePatrolDiagnostics)
            return;
        if (Time.time - lastPatrolLogTime < PatrolLogIntervalSeconds)
            return;
        lastPatrolLogTime = Time.time;

        string wanderStr = wanderTarget.x != int.MinValue
            ? $"{wanderTarget}(dist:{Manhattan(myTile, wanderTarget)})"
            : "none";
        string wanderExpiry = wanderTarget.x != int.MinValue
            ? $"exp:{wanderTargetExpiresTime - Time.time:F1}s"
            : "";

        Debug.Log(
            $"[BattleCOMPatrol][P{playerId}] frame:{Time.frameCount} t:{Time.time:F2} " +
            $"tile:{myTile} -> target:{targetTile} move:{FirstMoveDescription(move)} " +
            $"wander:{wanderStr} {wanderExpiry} reason:{reason}",
            this);
    }

    /// <summary>
    /// Retorna um wander target para que a IA explore toda a arena.
    /// Cada AI tem preferência direcional por quadrante (baseada em playerId),
    /// garantindo que IAs no mesmo local caminhem em direções diferentes.
    /// Se o BFS não encontrar tile em WanderMinDistance, aceita qualquer tile seguro (dist>=1)
    /// para nunca cair no greedy fallback quando há muitas bombas no cluster.
    /// </summary>
    private bool TryGetWanderTarget(BattleModeComDifficultySettings settings, Vector2Int myTile, out Vector2Int result)
    {
        result = Vector2Int.zero;

        bool expired = wanderTargetExpiresTime < Time.time;
        bool reached = wanderTarget.x != int.MinValue && Manhattan(myTile, wanderTarget) <= 1;
        bool targetDangerous = wanderTarget.x != int.MinValue &&
                               !float.IsInfinity(GetDangerSeconds(wanderTarget, null));

        if (!expired && !reached && !targetDangerous && wanderTarget.x != int.MinValue)
        {
            result = wanderTarget;
            return true;
        }

        // Preferência direcional por playerId — distribui IAs em quadrantes diferentes.
        // pid0=0 → NE(+x,+y), pid0=1 → SE(+x,-y), pid0=2 → SW(-x,-y), pid0=3 → NW(-x,+y)
        int pid0 = (playerId - 1) % 4;
        int prefX = (pid0 < 2) ? 1 : -1;
        int prefY = (pid0 == 0 || pid0 == 3) ? 1 : -1;
        int tieBreakSeed = playerId * 7919 + Mathf.RoundToInt(Time.time) * 1021;

        // Primeira tentativa: tile seguro em dist >= WanderMinDistance
        Vector2Int bestTile = RunWanderBfs(myTile, settings, prefX, prefY, tieBreakSeed, WanderMinDistance);

        // Fallback: aceita qualquer tile seguro (dist >= 1) para nunca cair no greedy fallback
        if (bestTile.x == int.MinValue)
            bestTile = RunWanderBfs(myTile, settings, prefX, prefY, tieBreakSeed, 1);

        if (bestTile.x == int.MinValue)
            return false;

        wanderTarget = bestTile;
        wanderTargetExpiresTime = Time.time + WanderTargetLifetimeSeconds;
        result = bestTile;
        return true;
    }

    private Vector2Int RunWanderBfs(
        Vector2Int myTile,
        BattleModeComDifficultySettings settings,
        int prefX, int prefY,
        int tieBreakSeed,
        int minDist)
    {
        visited.Clear();
        open.Clear();
        visited[myTile] = new PathNode { Tile = myTile, Parent = myTile, Depth = 0 };
        open.Enqueue(myTile);

        Vector2Int bestTile = new Vector2Int(int.MinValue, int.MinValue);
        int bestScore = int.MinValue;

        while (open.Count > 0)
        {
            Vector2Int tile = open.Dequeue();
            PathNode node = visited[tile];
            if (node.Depth > WanderBfsDepth) continue;

            if (node.Depth >= minDist && float.IsInfinity(GetDangerSeconds(tile, null)))
            {
                // Score direcional: dot product com quadrante preferido + desempate pseudo-aleatório
                int dirScore = tile.x * prefX + tile.y * prefY;
                int tieBreak = ((tile.x * 73 + tile.y * 37 + tieBreakSeed) & 0x7FFFFFFF) % 4;
                int score = dirScore * 4 + tieBreak;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTile = tile;
                }
            }

            for (int i = 0; i < CardinalTiles.Length; i++)
            {
                Vector2Int next = tile + CardinalTiles[i];
                if (visited.ContainsKey(next)) continue;
                if (!IsWalkableTile(next, myTile)) continue;
                float eta = EstimateEscapeTraversalSeconds(myTile, tile, next, node.Depth + 1);
                if (IsDangerousAt(next, eta, settings, null)) continue;
                visited[next] = new PathNode { Tile = next, Parent = tile, Depth = node.Depth + 1 };
                open.Enqueue(next);
            }
        }

        return bestTile;
    }

    private bool TryEmitControlBomb(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        float currentDangerSeconds)
    {
        if (bombController == null || Time.time - lastControlTapTime < ControlBombTapCooldownSeconds)
            return false;

        // ActionB detona a MAIS ANTIGA ControlBomb da IA — avaliar qualquer outra
        // bomba aqui causaria detonações erradas. Só a mais antiga importa.
        Bomb oldest = bombController.PeekOldestControlledBomb();
        if (oldest == null || oldest.HasExploded || oldest.IsBeingHeldByPowerGlove)
        {
            RejectVerbose("ControlBomb sem alvo seguro");
            return false;
        }

        Vector2Int bombTile = WorldToTile(oldest.GetLogicalPosition());
        int radius = Mathf.Max(1, bombController.GetPredictedBlastRadius(oldest));
        if (!WouldExplosionHitEnemyWithoutFriendlyRisk(bombTile, radius, myTile, out int enemyId))
        {
            RejectVerbose("ControlBomb sem alvo seguro");
            return false;
        }

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
        if (EnableOscillationDiagnostics)
        {
            Vector2Int newDir = DirectionToTile(move);
            if (newDir != Vector2Int.zero &&
                oscDiagLastDecisionDir == -newDir &&
                Time.time - oscDiagLastDecisionTime < 1.0f &&
                Time.time - oscDiagLastFlipLogTime > 0.1f)
            {
                oscDiagLastFlipLogTime = Time.time;
                Vector2Int diagTile = WorldToTile(transform.position);
                Vector2 diagCenterOffset = (Vector2)transform.position - TileToWorld(diagTile);
                Debug.LogWarning(
                    $"[BattleCOMOscDiag][P{playerId}] event:DECISION_FLIP frame:{Time.frameCount} t:{Time.time:F2} " +
                    $"tile:{diagTile} pos:{transform.position} centerOffset:{diagCenterOffset} " +
                    $"danger:{FormatDanger(dangerSeconds)} " +
                    $"PREV[dir:{oscDiagLastDecisionDir} age:{Time.time - oscDiagLastDecisionTime:F2}s " +
                    $"reason:{oscDiagLastDecisionReason} route:{oscDiagLastDecisionRoute} target:{oscDiagLastDecisionTarget}] " +
                    $"NEW[dir:{newDir} action:{action} reason:{reason} route:{route} target:{target} input:{input}] " +
                    $"safeCenter:{(hasSafeCenterTarget ? safeCenterTargetTile.ToString() : "none")} " +
                    $"followsEscape:{currentMoveFollowsEscapeRoute}",
                    this);
            }

            if (newDir != Vector2Int.zero)
            {
                oscDiagLastDecisionDir = newDir;
                oscDiagLastDecisionTime = Time.time;
                oscDiagLastDecisionReason = reason;
                oscDiagLastDecisionRoute = route;
                oscDiagLastDecisionTarget = target;
            }
        }

        oscDiagCurrentRoute = route;
        currentAction = action;
        currentMoveInput = move;
        hasCurrentTarget = hasTarget;
        currentTargetTile = target;
        currentReason = reason;
        currentInputDescription = input;
        currentHoldActionA = false;
        currentMoveFollowsEscapeRoute =
            action == BattleModeComActionType.Reposition &&
            hasTarget &&
            ((!string.IsNullOrEmpty(route) &&
              route.StartsWith("escape", StringComparison.Ordinal)) ||
             // Fugas das abilities de atravessar (BombPass/DestructiblePass) chegam
             // com route "ability" mas são rotas de fuga legítimas — sem isso o
             // dangerTimingGate bloqueia o passo para dentro da blast line e a IA congela.
             (!string.IsNullOrEmpty(reason) &&
              (reason.StartsWith("bomb-pass", StringComparison.Ordinal) ||
               reason.StartsWith("destructible-pass", StringComparison.Ordinal) ||
               reason.StartsWith("rubber-dodge", StringComparison.Ordinal) ||
               reason.StartsWith("pierce-dodge", StringComparison.Ordinal) ||
               reason.StartsWith("control-dodge", StringComparison.Ordinal) ||
               reason.StartsWith("control-retreat", StringComparison.Ordinal) ||
               reason.StartsWith("magnet-dodge", StringComparison.Ordinal) ||
               reason.StartsWith("greenlouie-dash", StringComparison.Ordinal) ||
               reason.StartsWith("pinklouie-jump", StringComparison.Ordinal) ||
               reason.StartsWith("tank-threat dodge", StringComparison.Ordinal) ||
               reason.StartsWith("purple-line retreat", StringComparison.Ordinal))));

        if (action == BattleModeComActionType.Reposition && hasTarget)
        {
            hasSafeCenterTarget = true;
            safeCenterTargetTile = target;
        }

        BattleModeComDifficultySettings settings = BattleModeComDifficultySettings.For(ResolveDifficulty());
        LogDecision(settings, WorldToTile(transform.position), dangerSeconds, route);
    }

    private void LogBehaviorStart()
    {
        if (behaviorStartLogged || !IsBattleModeScene())
            return;

        if (!EnableBehaviorDiagnostics)
        {
            behaviorStartLogged = true;
            behaviorLastProgressPosition = transform.position;
            behaviorLastProgressTime = Time.time;
            Vector2Int startTile = WorldToTile(transform.position);
            behaviorLastTile = startTile;
            behaviorPreviousTile = startTile;
            behaviorHasLastTile = true;
            return;
        }

        behaviorStartLogged = true;
        behaviorLastProgressPosition = transform.position;
        behaviorLastProgressTime = Time.time;

        Vector2Int tile = WorldToTile(transform.position);
        behaviorLastTile = tile;
        behaviorPreviousTile = tile;
        behaviorHasLastTile = true;

        Debug.LogWarning(
            $"[BattleCOMBehavior][P{playerId}] event:START frame:{Time.frameCount} t:{Time.time:F2} " +
            $"scene:{SceneManager.GetActiveScene().name} tile:{tile} pos:{transform.position} " +
            $"difficulty:{ResolveDifficulty()}",
            this);
    }

    private void ResetBehaviorDiagnostics()
    {
        behaviorStartLogged = false;
        behaviorHasLastTile = false;
        behaviorWasRequestingMovement = false;
        behaviorLastProgressPosition = transform.position;
        behaviorLastProgressTime = Time.time;
        behaviorStoppedDangerStartedTime = -1f;
        behaviorLastLogTime = -10f;
        behaviorLastLogKey = string.Empty;
        behaviorOscillationCount = 0;
        behaviorOscillationStartedTime = -10f;
    }

    private void TrackBehaviorDiagnostics(Vector2Int myTile, float dangerSeconds)
    {
        LogBehaviorStart();

        Vector2 position = transform.position;
        if (Vector2.Distance(position, behaviorLastProgressPosition) >=
            Mathf.Max(0.01f, tileSize * BehaviorProgressDistance))
        {
            behaviorLastProgressPosition = position;
            behaviorLastProgressTime = Time.time;
        }

        if (!behaviorHasLastTile)
        {
            behaviorLastTile = myTile;
            behaviorPreviousTile = myTile;
            behaviorHasLastTile = true;
        }
        else if (myTile != behaviorLastTile)
        {
            if (Time.time - behaviorOscillationStartedTime > BehaviorOscillationWindowSeconds)
            {
                behaviorOscillationCount = 0;
                behaviorOscillationStartedTime = Time.time;
            }

            if (myTile == behaviorPreviousTile)
            {
                behaviorOscillationCount++;
                LogBehaviorDiagnostic(
                    "OSCILLATION",
                    myTile,
                    dangerSeconds,
                    $"between:{behaviorPreviousTile}<->{behaviorLastTile} reversals:{behaviorOscillationCount}");
            }

            behaviorPreviousTile = behaviorLastTile;
            behaviorLastTile = myTile;
        }

        bool requestedMovement = currentMoveInput != Vector2.zero;
        if (requestedMovement && !behaviorWasRequestingMovement)
        {
            behaviorLastProgressPosition = position;
            behaviorLastProgressTime = Time.time;
        }

        behaviorWasRequestingMovement = requestedMovement;
        float noProgressSeconds = Time.time - behaviorLastProgressTime;
        if (requestedMovement && noProgressSeconds >= BehaviorNoProgressSeconds)
        {
            LogBehaviorDiagnostic(
                "NO_PROGRESS",
                myTile,
                dangerSeconds,
                $"duration:{noProgressSeconds:F2}s requested:{FirstMoveDescription(currentMoveInput)}");
        }

        bool stoppedInDanger =
            !float.IsInfinity(dangerSeconds) &&
            (currentMoveInput == Vector2.zero || currentAction == BattleModeComActionType.Stopped);

        if (!stoppedInDanger)
        {
            behaviorStoppedDangerStartedTime = -1f;
            return;
        }

        if (behaviorStoppedDangerStartedTime < 0f)
            behaviorStoppedDangerStartedTime = Time.time;

        float stoppedSeconds = Time.time - behaviorStoppedDangerStartedTime;
        if (stoppedSeconds >= BehaviorStoppedDangerSeconds)
        {
            LogBehaviorDiagnostic(
                "STOPPED_IN_DANGER",
                myTile,
                dangerSeconds,
                $"duration:{stoppedSeconds:F2}s");
        }
    }

    private void LogBehaviorDiagnostic(
        string eventName,
        Vector2Int myTile,
        float dangerSeconds,
        string symptom = "",
        bool force = false)
    {
        if (!EnableBehaviorDiagnostics)
            return;

        string key =
            $"{eventName}:{myTile}:{currentAction}:{currentTargetTile}:{currentMoveInput}:{currentReason}";
        if (!force &&
            key == behaviorLastLogKey &&
            Time.time - behaviorLastLogTime < BehaviorRepeatLogSeconds)
        {
            return;
        }

        behaviorLastLogKey = key;
        behaviorLastLogTime = Time.time;

        string target = hasCurrentTarget ? currentTargetTile.ToString() : "none";
        string safeCenter = hasSafeCenterTarget ? safeCenterTargetTile.ToString() : "none";
        string rejected = rejectedActions.Count > 0 ? string.Join(" | ", rejectedActions) : "none";
        string candidatesText = FormatCandidates();
        Vector2 centerOffset = TileToWorld(myTile) - (Vector2)transform.position;

        Debug.LogWarning(
            $"[BattleCOMBehavior][P{playerId}] event:{eventName} frame:{Time.frameCount} t:{Time.time:F2} " +
            $"tile:{myTile} pos:{transform.position} centerOffset:{centerOffset} " +
            $"danger:{FormatDanger(dangerSeconds)} symptom:{(string.IsNullOrEmpty(symptom) ? "none" : symptom)} " +
            $"action:{currentAction} target:{target} move:{FirstMoveDescription(currentMoveInput)} " +
            $"input:{currentInputDescription} reason:{currentReason} escapeRoute:{currentMoveFollowsEscapeRoute} " +
            $"safeCenter:{safeCenter} openNeighbors:{CountOpenNeighbors(myTile)} " +
            $"bombs:{FormatActiveBombThreats(myTile)} own:{FormatOwnChainBombs()} " +
            $"candidates:{candidatesText} rejected:{rejected} abilities:{FormatCurrentAbilityTraces()}",
            this);
    }

    private void LogDecision(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        float dangerSeconds,
        string route)
    {
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

        string detail = BuildDecisionDetailLine();
        if (!string.IsNullOrEmpty(detail))
            PushDecision($"[BattleCOM][P{playerId}] {detail}");
    }

    private void LogSurgicalDecisionTrace(
        Vector2Int myTile,
        float dangerSeconds,
        string route,
        string detail)
    {
        if (!EnableDecisionTraceDiagnostics || !debugDecisionTrace)
            return;

        if (decisionTracePlayerIdFilter != 0 && playerId != decisionTracePlayerIdFilter)
            return;

        bool plantedBomb = !string.IsNullOrEmpty(currentInputDescription) &&
                           currentInputDescription.Contains("ActionA", StringComparison.OrdinalIgnoreCase);
        bool stopped = currentAction == BattleModeComActionType.Stopped;
        bool abilityAction = currentAction == BattleModeComActionType.KickBomb;
        bool abilityContext = abilityDecisionEvaluatedThisThink || abilityAction;
        bool postPlantContext =
            postPlantEscapeWatchActive ||
            chainBombPlantingStartedTime >= 0f ||
            HasOwnUnresolvedBombOrExplosion();

        if (!plantedBomb && !stopped && !abilityContext && !postPlantContext)
            return;

        string target = hasCurrentTarget ? currentTargetTile.ToString() : "none";
        string key =
            $"{currentAction}:{myTile}:{target}:{route}:{currentReason}:{currentInputDescription}:{FormatOwnChainBombs()}";

        if (key == lastDecisionTraceLogKey &&
            Time.time - lastDecisionTraceLogTime < 0.25f)
        {
            return;
        }

        lastDecisionTraceLogKey = key;
        lastDecisionTraceLogTime = Time.time;

        int bombsRemaining = bombController != null ? bombController.BombsRemaining : -1;
        int bombsTotal = bombController != null ? bombController.bombAmout : -1;
        string safeCenter = hasSafeCenterTarget ? safeCenterTargetTile.ToString() : "none";
        string rejected = rejectedActions.Count > 0 ? string.Join(" | ", rejectedActions) : "none";
        string extraDetail = string.IsNullOrEmpty(detail) ? "none" : detail;

        Debug.Log(
            $"[BattleCOMDecisionTrace][P{playerId}] frame:{Time.frameCount} t:{Time.time:F2} " +
            $"tile:{myTile} pos:{transform.position} danger:{FormatDanger(dangerSeconds)} " +
            $"action:{currentAction} route:{route} target:{target} move:{FirstMoveDescription(currentMoveInput)} " +
            $"input:{currentInputDescription} reason:{currentReason} planted:{plantedBomb} stopped:{stopped} " +
            $"postPlant:{postPlantContext} safeCenter:{safeCenter} escapeRoute:{currentMoveFollowsEscapeRoute} " +
            $"bombs:{bombsRemaining}/{bombsTotal} own:{FormatOwnChainBombs()} " +
            $"activeBombs:{FormatActiveBombThreats(myTile)} rejected:{rejected} detail:{extraDetail}",
            this);
    }

    private string BuildDecisionDetailLine()
    {
        string candidatesLine = FormatCandidates();
        string rejectedLine = rejectedActions.Count > 0 ? string.Join(" | ", rejectedActions) : "none";
        string abilityLine = FormatCurrentAbilityTraces();

        if (candidatesLine == "none" && rejectedLine == "none" && abilityLine == "none")
            return string.Empty;

        return $"candidates:{candidatesLine} rejected:{rejectedLine} abilities:{abilityLine}";
    }

    private void OnHealthDied()
    {
        currentHoldActionA = false;
        ClearSyntheticInputs();

        if (lastDeathDiagnosticFrame == Time.frameCount)
            return;

        lastDeathDiagnosticFrame = Time.frameCount;

        if (!IsBattleModeScene())
            return;

        if (SaveSystem.GetBattleModePlayerControlMode(playerId) != BattleModePlayerControlMode.Com)
            return;

        Vector2Int tile = WorldToTile(transform.position);
        LogBehaviorDiagnostic("DEATH", tile, GetDangerSeconds(tile, null), force: true);

        if (IsStage3P5SurgicalDiagnostic())
        {
            Debug.LogWarning(
                BuildDeathDiagnosticReport(
                    $"[BattleCOMStage3Plant][P5] DEATH"),
                this);
        }
    }

    private void OnMovementDied(MovementController deadMovement)
    {
        OnHealthDied();
    }

    private string BuildDeathDiagnosticReport(string header = null)
    {
        CacheReferences();

        BattleModeComputerLevel difficulty = ResolveDifficulty();
        Vector2Int myTile = WorldToTile(transform.position);
        float dangerSeconds = GetDangerSeconds(myTile, null);
        string scene = SceneManager.GetActiveScene().name;

        StringBuilder sb = new();
        sb.AppendLine(header ?? $"[BattleCOMDeath][P{playerId}] IA morreu - diagnostico de logica");
        sb.AppendLine(
            $"scene:{scene} frame:{Time.frameCount} time:{Time.time:F2} difficulty:{difficulty} " +
            $"tile:{myTile} pos:{transform.position} danger:{FormatDanger(dangerSeconds)}");
        sb.AppendLine(
            $"current action:{currentAction} target:{(hasCurrentTarget ? currentTargetTile.ToString() : "none")} " +
            $"move:{FirstMoveDescription(currentMoveInput)} input:{currentInputDescription} reason:{currentReason}");
        sb.AppendLine(
            $"state safeCenter:{hasSafeCenterTarget}@{safeCenterTargetTile} escapeRoute:{currentMoveFollowsEscapeRoute} " +
            $"ownBombOrExplosion:{HasOwnUnresolvedBombOrExplosion()}");
        sb.AppendLine($"escapeAbilityChance:{lastEscapeAbilityChanceTrace}");
        sb.AppendLine($"abilities:{FormatCurrentAbilityTraces()}");
        sb.AppendLine($"activeBombs:{FormatActiveBombThreats(myTile)}");
        sb.AppendLine($"activeExplosions:{FormatActiveExplosionThreats(myTile)}");
        sb.AppendLine($"lastCandidates:{FormatCandidates()}");
        sb.AppendLine($"lastRejected:{(rejectedActions.Count > 0 ? string.Join(" | ", rejectedActions) : "none")}");
        sb.AppendLine("recentDecisions:");

        if (recentDecisions.Count <= 0)
        {
            sb.AppendLine("  none");
        }
        else
        {
            for (int i = 0; i < recentDecisions.Count; i++)
                sb.AppendLine($"  {i + 1:00}. {recentDecisions[i]}");
        }

        return sb.ToString();
    }

    private string BuildKickBombRiskDeathDiagnosticReport()
    {
        string report = BuildDeathDiagnosticReport($"[BattleCOMKickRisk][P{playerId}] KICK_BOMB_DEATH_DETAIL");
        Vector2Int myTile = WorldToTile(transform.position);

        StringBuilder sb = new();
        sb.AppendLine($"[BattleCOMKickRisk][P{playerId}] KICK_BOMB_DEATH");
        sb.AppendLine(
            $"kickContext currentAction:{currentAction} move:{FirstMoveDescription(currentMoveInput)} " +
            $"target:{(hasCurrentTarget ? currentTargetTile.ToString() : "none")} reason:{currentReason} input:{currentInputDescription}");
        sb.AppendLine($"kickAbilities:{FormatCurrentAbilityTraces()}");
        sb.AppendLine($"kickActiveBombs:{FormatActiveBombThreats(myTile)}");
        sb.AppendLine($"kickActiveExplosions:{FormatActiveExplosionThreats(myTile)}");
        sb.Append(report);
        return sb.ToString();
    }

    private bool IsRecentKickBombRiskContext()
    {
        if (currentAction == BattleModeComActionType.KickBomb)
            return true;

        for (int i = recentDecisions.Count - 1; i >= 0; i--)
        {
            string line = recentDecisions[i];
            if (!string.IsNullOrEmpty(line) &&
                line.Contains("KickBomb", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private string FormatCurrentAbilityTraces()
    {
        RefreshComAbilities();

        if (comAbilities.Count <= 0)
            return BuildNoAbilityScriptsTrace();

        string result = string.Empty;
        for (int i = 0; i < comAbilities.Count; i++)
        {
            IBattleModeComAbility ability = comAbilities[i];
            if (!IsComAbilityAlive(ability))
                continue;

            if (!string.IsNullOrEmpty(result))
                result += " | ";

            string name = string.IsNullOrWhiteSpace(ability.DiagnosticName)
                ? ability.GetType().Name
                : ability.DiagnosticName;
            string trace = string.IsNullOrWhiteSpace(ability.LastDecisionTrace)
                ? "no trace"
                : ability.LastDecisionTrace;

            result += $"{name}:{trace}";
        }

        return string.IsNullOrEmpty(result) ? "none" : result;
    }

    private static bool IsComAbilityAlive(IBattleModeComAbility ability)
    {
        if (ability == null)
            return false;

        if (ability is UnityEngine.Object unityObject)
            return unityObject != null;

        return true;
    }

    private string FormatActiveBombThreats(Vector2Int myTile)
    {
        string result = string.Empty;
        int count = 0;

        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded)
                continue;

            Vector2Int bombTile = WorldToTile(bomb.GetLogicalPosition());
            int radius = bomb.Owner != null ? Mathf.Max(1, bomb.Owner.GetPredictedBlastRadius(bomb)) : 2;
            bool threatensAi =
                !bomb.IsBeingHeldByPowerGlove &&
                IsTileInBlastLineRuntime(bombTile, myTile, radius);
            int ownerId = 0;
            if (bomb.Owner != null && bomb.Owner.TryGetComponent<PlayerIdentity>(out var ownerIdentity) && ownerIdentity != null)
                ownerId = ownerIdentity.playerId;

            if (count > 0)
                result += " | ";

            result +=
                $"#{count + 1} owner:P{ownerId} tile:{bombTile} radius:{radius} " +
                $"fuse:{FormatDanger(bomb.IsControlBomb ? 0.65f : bomb.RemainingFuseSeconds)} " +
                $"control:{bomb.IsControlBomb} held:{bomb.IsBeingHeldByPowerGlove} " +
                $"moved:{bomb.WasMovedByKickOrPunch} solid:{bomb.IsSolid} threatensAi:{threatensAi}";
            count++;
        }

        return count <= 0 ? "none" : result;
    }

    private string FormatActiveExplosionThreats(Vector2Int myTile)
    {
        if (explosionMask == 0)
            return "none";

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            TileToWorld(myTile),
            Mathf.Max(tileSize * 2.5f, 0.5f),
            explosionMask);

        if (hits == null || hits.Length <= 0)
            return "none";

        string result = string.Empty;
        int count = 0;
        int max = Mathf.Min(hits.Length, 8);
        for (int i = 0; i < max; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            Vector2Int tile = WorldToTile(hit.bounds.center);
            bool onAiTile = tile == myTile;
            bool touchesAi = hit.OverlapPoint(transform.position);

            if (count > 0)
                result += " | ";

            result +=
                $"#{count + 1} tile:{tile} center:{hit.bounds.center} " +
                $"onAiTile:{onAiTile} touchesAi:{touchesAi}";
            count++;
        }

        if (hits.Length > max)
            result += $" | +{hits.Length - max} more";

        return count <= 0 ? "none" : result;
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
        rejectedActions.Add($"{action}:{reason}");
    }

    private void RejectVerbose(string reason)
    {
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
            float eta = EstimateEscapeTraversalSeconds(start, tile, node.Depth);
            float dangerSeconds = GetDangerSeconds(tile, plannedBlastTiles);
            bool plannedDanger = plannedBlastTiles != null && plannedBlastTiles.Contains(tile);

            if (!plannedDanger &&
                node.Depth > 0 &&
                float.IsInfinity(dangerSeconds) &&
                !IsDangerousAt(tile, eta + settings.safeTileMinimumSeconds, settings, plannedBlastTiles) &&
                dangerSeconds > eta + settings.safeTileMinimumSeconds)
            {
                Vector2Int firstStep = ReconstructFirstStep(start, tile);
                float firstMoveEta = EstimateFirstMoveTraversalSeconds(firstStep);
                float currentDangerSeconds = GetDangerSeconds(start, plannedBlastTiles);
                if (!float.IsInfinity(currentDangerSeconds) &&
                    currentDangerSeconds <= firstMoveEta + DangerTimingMarginSeconds)
                {
                    RejectVerbose(
                        $"Escape {tile} rejected current danger {FormatDanger(currentDangerSeconds)} " +
                        $"before first step eta:{firstMoveEta:F2}s");
                    continue;
                }

                target = tile;
                firstMove = TileDirectionToVector(firstStep);
                route = $"escape depth {node.Depth} eta {eta:F2}s";
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

                float nextEta = EstimateEscapeTraversalSeconds(start, tile, next, node.Depth + 1);
                if (IsDangerousAt(next, nextEta, settings, plannedBlastTiles))
                    continue;

                visited[next] = new PathNode { Tile = next, Parent = tile, Depth = node.Depth + 1 };
                open.Enqueue(next);
            }
        }

        return false;
    }

    public bool TryFindAbilityEscape(
        BattleModeComDifficultySettings settings,
        Vector2Int start,
        out Vector2 firstMove,
        out Vector2Int target,
        out string route)
    {
        return TryFindEscape(
            settings,
            start,
            null,
            out firstMove,
            out target,
            out route);
    }

    public float GetAbilityDangerSeconds(Vector2Int tile)
        => GetDangerSeconds(tile, null);

    public bool TryFindAbilitySpringEscapeRoute(
        BattleModeComDifficultySettings settings,
        Vector2Int start,
        Vector2Int springTile,
        out Vector2 firstMove,
        out int distance,
        out float arrivalSeconds,
        out string route)
    {
        firstMove = Vector2.zero;
        distance = 0;
        arrivalSeconds = 0f;
        route = "none";

        if (start == springTile)
        {
            route = "spring current tile";
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

            if (node.Depth >= settings.searchDepth + 3)
                continue;

            for (int i = 0; i < CardinalTiles.Length; i++)
            {
                Vector2Int next = tile + CardinalTiles[i];
                if (visited.ContainsKey(next) || !IsWalkableTile(next, start))
                    continue;

                int nextDepth = node.Depth + 1;
                float nextEta =
                    EstimateEscapeTraversalSeconds(start, tile, next, nextDepth);
                bool isSpringGoal = next == springTile;

                if (isSpringGoal)
                {
                    float springDanger = GetDangerSeconds(next, null);
                    if (!float.IsInfinity(springDanger) &&
                        springDanger <= nextEta + DangerTimingMarginSeconds)
                    {
                        continue;
                    }
                }
                else if (IsDangerousAt(next, nextEta, settings, null))
                {
                    continue;
                }

                Vector2Int firstStep = Vector2Int.zero;

                if (isSpringGoal)
                {
                    firstStep = tile == start
                        ? next - start
                        : ReconstructFirstStep(start, tile);
                    float firstEta = EstimateFirstMoveTraversalSeconds(firstStep);
                    float currentDanger = GetDangerSeconds(start, null);
                    if (!float.IsInfinity(currentDanger) &&
                        currentDanger <= firstEta + DangerTimingMarginSeconds)
                    {
                        continue;
                    }
                }

                visited[next] = new PathNode
                {
                    Tile = next,
                    Parent = tile,
                    Depth = nextDepth
                };

                if (!isSpringGoal)
                {
                    open.Enqueue(next);
                    continue;
                }

                firstMove = TileDirectionToVector(firstStep);
                distance = nextDepth;
                arrivalSeconds = nextEta;
                route = $"spring depth {nextDepth} eta {nextEta:F2}s";
                return true;
            }
        }

        return false;
    }

    public int CountSafeEscapeFirstSteps(
        BattleModeComDifficultySettings settings,
        Vector2Int start)
    {
        int count = 0;
        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            if (HasSafeEscapeRouteThroughFirstStep(settings, start, CardinalTiles[i]))
                count++;
        }

        return count;
    }

    private bool HasSafeEscapeRouteThroughFirstStep(
        BattleModeComDifficultySettings settings,
        Vector2Int start,
        Vector2Int firstStep)
    {
        Vector2Int firstTile = start + firstStep;
        if (!IsWalkableTile(firstTile, start))
            return false;

        float firstEta = EstimateFirstMoveTraversalSeconds(firstStep);
        float currentDanger = GetDangerSeconds(start, null);
        if (!float.IsInfinity(currentDanger) &&
            currentDanger <= firstEta + DangerTimingMarginSeconds)
            return false;

        if (IsDangerousAt(firstTile, firstEta, settings, null))
            return false;

        visited.Clear();
        open.Clear();
        visited[start] = new PathNode { Tile = start, Parent = start, Depth = 0 };
        visited[firstTile] = new PathNode { Tile = firstTile, Parent = start, Depth = 1 };
        open.Enqueue(firstTile);

        while (open.Count > 0)
        {
            Vector2Int tile = open.Dequeue();
            PathNode node = visited[tile];
            float eta = EstimateEscapeTraversalSeconds(start, tile, node.Depth);
            float danger = GetDangerSeconds(tile, null);

            if (float.IsInfinity(danger) &&
                !IsDangerousAt(tile, eta + settings.safeTileMinimumSeconds, settings, null))
                return true;

            if (node.Depth >= settings.searchDepth + 3)
                continue;

            for (int i = 0; i < CardinalTiles.Length; i++)
            {
                Vector2Int next = tile + CardinalTiles[i];
                if (visited.ContainsKey(next) || !IsWalkableTile(next, start))
                    continue;

                float nextEta = EstimateEscapeTraversalSeconds(start, tile, next, node.Depth + 1);
                if (IsDangerousAt(next, nextEta, settings, null))
                    continue;

                visited[next] = new PathNode
                {
                    Tile = next,
                    Parent = tile,
                    Depth = node.Depth + 1
                };
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
            {
                RejectVerbose($"Fallback {DirectionLabel(CardinalTiles[i])} {next} blocked");
                continue;
            }

            float arrivalSeconds = EstimateFirstMoveTraversalSeconds(CardinalTiles[i]);
            float score = GetSurvivalMarginSeconds(next, arrivalSeconds, settings, plannedBlastTiles) +
                          CountOpenNeighbors(next) * 0.25f;
            if (plannedBlastTiles != null && plannedBlastTiles.Contains(next))
                score -= 10f;

            RejectVerbose(
                $"Fallback {DirectionLabel(CardinalTiles[i])} {next} score:{score:F2} " +
                $"eta:{arrivalSeconds:F2}s danger:{FormatDanger(GetDangerSeconds(next, plannedBlastTiles))} " +
                $"margin:{GetSurvivalMarginSeconds(next, arrivalSeconds, settings, plannedBlastTiles):F2}");

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
        return TileDirectionToVector(ReconstructFirstStep(start, goal));
    }

    private Vector2Int ReconstructFirstStep(Vector2Int start, Vector2Int goal)
    {
        if (start == goal)
            return Vector2Int.zero;

        Vector2Int cursor = goal;
        while (visited.TryGetValue(cursor, out PathNode node) && node.Parent != start && node.Parent != cursor)
            cursor = node.Parent;

        Vector2Int delta = cursor - start;
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            return delta.x > 0 ? Vector2Int.right : Vector2Int.left;

        if (delta.y != 0)
            return delta.y > 0 ? Vector2Int.up : Vector2Int.down;

        return Vector2Int.zero;
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
        int directBlastTileCount = plannedBlast.Count;
        bool expandedByStage = AppendPlannedBombStageDanger(
            plantTile,
            plannedBlast);
        bool foundEscape = TryFindEscape(
            settings,
            plantTile,
            plannedBlast,
            out escapeMove,
            out escapeTile,
            out string escapeRoute);

        if (expandedByStage && plantTile == WorldToTile(transform.position))
        {
            LogStage3P5PlantDiagnostic(
                "PREPLANT",
                plantTile,
                $"pathResult:{(foundEscape ? "FOUND" : "NOT_FOUND")} radius:{radius} " +
                $"directTiles:{directBlastTileCount} expandedTiles:{plannedBlast.Count} " +
                $"escape:{escapeTile} move:{FirstMoveDescription(escapeMove)} route:{escapeRoute}");
        }

        if (!foundEscape)
            return false;

        Vector2Int currentTile = WorldToTile(transform.position);
        if (plantTile == currentTile)
        {
            Vector2Int firstStep = DirectionToTile(escapeMove);
            if (firstStep == Vector2Int.zero)
            {
                RejectVerbose($"PlantEscape recusado sem primeiro passo tile:{plantTile} escape:{escapeTile}");
                return false;
            }

            if (!IsCenteredOnTile(plantTile) || NeedsTurnAxisCentering(plantTile, firstStep))
                return false;

            Vector2Int firstEscapeTile = plantTile + firstStep;
            float currentDanger = GetDangerSeconds(currentTile, null);
            float activeFirstDanger = GetDangerSeconds(firstEscapeTile, null);

            if (float.IsInfinity(currentDanger) && !float.IsInfinity(activeFirstDanger))
            {
                RejectVerbose(
                    $"PlantEscape recusado primeiro passo ameacado por bomba ativa " +
                    $"plant:{plantTile} first:{firstEscapeTile} danger:{FormatDanger(activeFirstDanger)} escape:{escapeTile}");
                return false;
            }

            if (HasOwnUnresolvedBombOrExplosion())
            {
                float activeTargetDanger = GetDangerSeconds(escapeTile, null);
                if (!float.IsInfinity(activeTargetDanger))
                {
                    RejectVerbose(
                        $"PlantEscape recusado alvo ameacado por bomba propria ativa " +
                        $"plant:{plantTile} first:{firstEscapeTile} target:{escapeTile} danger:{FormatDanger(activeTargetDanger)}");
                    return false;
                }
            }
        }

        return true;
    }

    private bool AppendPlannedBombStageDanger(
        Vector2Int plantTile,
        List<Vector2Int> plannedDangerTiles)
    {
        bool expanded = false;

        for (int i = 0; i < comAbilities.Count; i++)
        {
            if (comAbilities[i] is not IBattleModeComPlannedBombDangerProvider provider)
                continue;

            expanded |= provider.TryAppendPlannedBombDangerTiles(
                plantTile,
                plannedDangerTiles);
        }

        return expanded;
    }

    private bool IsStage3P5SurgicalDiagnostic()
    {
        return playerId == 5 &&
               string.Equals(
                   SceneManager.GetActiveScene().name,
                   BattleMode3SceneName,
                   StringComparison.Ordinal);
    }

    private void LogStage3P5PlantDiagnostic(
        string key,
        Vector2Int tile,
        string detail)
    {
        if (!IsStage3P5SurgicalDiagnostic())
            return;

        string diagnosticKey = $"{key}:{tile}:{detail}";
        if (key == "PREPLANT" &&
            diagnosticKey == lastStage3PlantDiagnosticKey &&
            Time.time - lastStage3PlantDiagnosticTime < 0.35f)
        {
            return;
        }

        lastStage3PlantDiagnosticKey = diagnosticKey;
        lastStage3PlantDiagnosticTime = Time.time;

        Debug.LogWarning(
            $"[BattleCOMStage3Plant][P5] {key} frame:{Time.frameCount} " +
            $"t:{Time.time:F2} tile:{tile} pos:{transform.position} {detail}",
            this);
    }

    private bool CanPlantAdditionalBombWithEscape(
        Vector2Int plantTile,
        int radius,
        BattleModeComDifficultySettings settings,
        out Vector2 escapeMove,
        out Vector2Int escapeTile,
        out int escapeBranches)
    {
        escapeMove = Vector2.zero;
        escapeTile = plantTile;
        escapeBranches = 0;

        if (!CanPlantBombWithEscape(plantTile, radius, settings, out escapeMove, out escapeTile))
            return false;

        if (!TryBuildChainBlastTiles(plantTile, radius, out List<Vector2Int> combinedBlastTiles))
        {
            RejectSecondBombSafety($"sem simulacao de explosao tile:{plantTile}");
            return false;
        }

        string branchSummary = string.Empty;
        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int firstStep = CardinalTiles[i];
            Vector2Int firstTile = plantTile + firstStep;
            if (!IsWalkableTile(firstTile, plantTile))
            {
                AppendSecondBombBranchSummary(
                    ref branchSummary,
                    firstStep,
                    "blocked");
                continue;
            }

            float firstStepEta = EstimateFirstMoveTraversalSeconds(firstStep);
            if (IsDangerousAt(firstTile, firstStepEta, settings, combinedBlastTiles))
            {
                AppendSecondBombBranchSummary(
                    ref branchSummary,
                    firstStep,
                    $"unsafe-first:{FormatDanger(GetDangerSeconds(firstTile, combinedBlastTiles))}");
                continue;
            }

            if (!TryFindEscapeWithPreferredFirstStep(
                    settings,
                    plantTile,
                    firstStep,
                    combinedBlastTiles,
                    out Vector2Int branchTarget,
                    out int branchDepth))
            {
                AppendSecondBombBranchSummary(
                    ref branchSummary,
                    firstStep,
                    "no-safe-target");
                continue;
            }

            if (escapeBranches == 0)
            {
                escapeMove = TileDirectionToVector(firstStep);
                escapeTile = branchTarget;
            }

            escapeBranches++;
            AppendSecondBombBranchSummary(
                ref branchSummary,
                firstStep,
                $"safe:{branchTarget}/depth:{branchDepth}");
        }

        if (escapeBranches >= AdditionalBombMinimumEscapeBranches)
            return true;

        RejectSecondBombSafety(
            $"poucas fugas tile:{plantTile} branches:{escapeBranches}/" +
            $"{AdditionalBombMinimumEscapeBranches} escape:{escapeTile} routes:{branchSummary} " +
            $"own:{FormatOwnChainBombs()}");
        return false;
    }

    private static void AppendSecondBombBranchSummary(
        ref string summary,
        Vector2Int direction,
        string result)
    {
        if (!string.IsNullOrEmpty(summary))
            summary += " | ";

        summary += $"{DirectionLabel(direction)}:{result}";
    }

    private void RejectSecondBombSafety(string reason)
    {
        int samples = 0;
        for (int i = 0; i < rejectedActions.Count; i++)
        {
            if (rejectedActions[i].StartsWith("SecondBomb recusada", StringComparison.Ordinal))
                samples++;
        }

        if (samples < SecondBombRejectSampleLimit)
            RejectVerbose($"SecondBomb recusada {reason}");
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
            float eta = EstimateEscapeTraversalSeconds(start, tile, node.Depth);
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

                float nextEta = EstimateEscapeTraversalSeconds(start, tile, next, node.Depth + 1);
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
        out bool createsTurn,
        out Vector2Int linkedBombTile,
        out float linkedBombRemainingSeconds)
    {
        triggerSeconds = float.PositiveInfinity;
        bool linkedToOwnBomb = false;
        createsTurn = false;
        linkedBombTile = new Vector2Int(int.MinValue, int.MinValue);
        linkedBombRemainingSeconds = float.PositiveInfinity;

        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded || bomb.IsBeingHeldByPowerGlove)
                continue;

            Vector2Int bombTile = WorldToTile(bomb.GetLogicalPosition());
            int bombRadius = bomb.Owner != null ? Mathf.Max(1, bomb.Owner.GetPredictedBlastRadius(bomb)) : radius;
            if (!IsTileInBlastLineRuntime(bombTile, plantTile, bombRadius))
                continue;

            if (bomb.Owner == bombController && !bomb.IsControlBomb)
            {
                linkedToOwnBomb = true;
                float ownRemaining = bomb.RemainingFuseSeconds;
                if (ownRemaining < linkedBombRemainingSeconds)
                {
                    linkedBombRemainingSeconds = ownRemaining;
                    linkedBombTile = bombTile;
                }

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

        AppendPlannedBombStageDanger(plantTile, chainBlastTiles);
        return chainBlastTiles.Count > 0;
    }

    private void EnqueueBombsHitByBlast(
        List<Vector2Int> blastTiles,
        Queue<Vector2Int> triggeredBombTiles,
        HashSet<Vector2Int> visitedBombTiles)
    {
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded || bomb.IsBeingHeldByPowerGlove)
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
            if (bomb == null || bomb.HasExploded || bomb.IsBeingHeldByPowerGlove)
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

        if (HasIndestructibleTile(tile))
            return false;

        // DestructiblePass: blocos destrutíveis deixam de ser obstáculo — abre rotas
        // de fuga, farm e perseguição através dos blocos em todo o pathfinding.
        if (HasDestructibleTile(tile) && !ComCanPassThroughDestructibles())
            return false;

        // BombPass: bombas deixam de ser obstáculo para a IA — isso abre rotas de
        // fuga, desencurralamento e aproximação ofensiva através de bombas em TODOS
        // os pathfindings do controller (fuga nativa inclusive).
        if (IsBombAtTile(tile) && tile != startTile && !ComCanPassThroughBombs())
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

                if (hit.GetComponentInParent<Bomb>() != null &&
                    (tile == startTile || ComCanPassThroughBombs()))
                    continue;

                // DestructiblePass: ignora os colliders dos blocos destrutíveis.
                if (ComCanPassThroughDestructibles() && IsDestructibleCollider(hit))
                    continue;

                return false;
            }
        }

        return true;
    }

    private AbilitySystem bombPassCheckAbilitySystem;

    /// <summary>
    /// True quando a IA possui BombPassAbility ativa — bombas são atravessáveis.
    /// </summary>
    private bool ComCanPassThroughBombs()
    {
        if (bombPassCheckAbilitySystem == null)
            TryGetComponent(out bombPassCheckAbilitySystem);

        return bombPassCheckAbilitySystem != null &&
               bombPassCheckAbilitySystem.IsEnabled(BombPassAbility.AbilityId);
    }

    /// <summary>
    /// True quando a IA possui DestructiblePassAbility ativa — blocos destrutíveis
    /// são atravessáveis.
    /// </summary>
    private bool ComCanPassThroughDestructibles()
    {
        if (bombPassCheckAbilitySystem == null)
            TryGetComponent(out bombPassCheckAbilitySystem);

        return bombPassCheckAbilitySystem != null &&
               bombPassCheckAbilitySystem.IsEnabled(DestructiblePassAbility.AbilityId);
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
            if (bomb == null || bomb.HasExploded || bomb.IsBeingHeldByPowerGlove)
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
        float danger = float.PositiveInfinity;
        if (plannedBlastTiles != null && plannedBlastTiles.Contains(tile))
            danger = bombController != null ? Mathf.Max(0.5f, bombController.bombFuseTime) : 2f;

        // Stage abilities can announce hazards before their runtime object exists.
        for (int i = 0; i < comAbilities.Count; i++)
        {
            if (comAbilities[i] is IBattleModeComDangerProvider dangerProvider &&
                dangerProvider.TryGetDangerSeconds(tile, out float stageDangerSeconds))
            {
                danger = Mathf.Min(danger, Mathf.Max(0f, stageDangerSeconds));
            }
        }

        if (explosionMask != 0)
        {
            Collider2D explosion = Physics2D.OverlapCircle(TileToWorld(tile), tileSize * 0.25f, explosionMask);
            if (explosion != null)
                return 0f;
        }

        if (suddenDeathController != null &&
            suddenDeathController.TryGetSecondsUntilSuddenDeathWorldPosition(TileToWorld(tile), out float suddenDeathSeconds) &&
            suddenDeathSeconds <= SuddenDeathUnsafeLeadSeconds)
        {
            danger = Mathf.Min(danger, suddenDeathSeconds);
        }

        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded || bomb.IsBeingHeldByPowerGlove)
                continue;

            Vector2Int bombTile = WorldToTile(bomb.GetLogicalPosition());
            int radius = bomb.Owner != null ? Mathf.Max(1, bomb.Owner.GetPredictedBlastRadius(bomb)) : 2;
            if (!IsBombBlastReachingTile(bomb, bombTile, tile, radius))
                continue;

            float seconds = bomb.IsControlBomb ? 0.65f : bomb.RemainingFuseSeconds;
            danger = Mathf.Min(danger, seconds);
        }

        return danger;
    }

    /// <summary>
    /// Blast line ciente do tipo da bomba: explosões PIERCE atravessam blocos
    /// destrutíveis (param só em indestrutíveis e outras bombas). Sem isso, o
    /// modelo nativo achava "seguro" um tile atrás de um bloco que a pierce
    /// atravessa — e a IA plantava/fugia para dentro da zona real de explosão.
    /// </summary>
    private bool IsBombBlastReachingTile(Bomb bomb, Vector2Int bombTile, Vector2Int tile, int radius)
    {
        if (bomb != null && bomb.IsPierceBomb)
            return IsTileInBlastLine(bombTile, tile, radius, BlocksExplosionForPierce);

        return IsTileInBlastLineRuntime(bombTile, tile, radius);
    }

    public bool DoesBombBlastReachTile(Bomb bomb, Vector2Int tile)
    {
        return DoesBombBlastReachTile(bomb, tile, null);
    }

    public bool DoesBombBlastReachTile(
        Bomb bomb,
        Vector2Int tile,
        ICollection<Vector2Int> ignoredBombTiles)
    {
        if (bomb == null || bomb.HasExploded || bomb.IsBeingHeldByPowerGlove)
            return false;

        Vector2Int bombTile = WorldToTile(bomb.GetLogicalPosition());
        int radius = bomb.Owner != null
            ? Mathf.Max(1, bomb.Owner.GetPredictedBlastRadius(bomb))
            : Mathf.Max(1, bomb.ExplosionRadiusOverride > 0 ? bomb.ExplosionRadiusOverride : 2);

        if (ignoredBombTiles == null || ignoredBombTiles.Count == 0)
            return IsBombBlastReachingTile(bomb, bombTile, tile, radius);

        bool BlocksPredictedExplosion(Vector2Int check)
        {
            if (HasIndestructibleTile(check))
                return true;

            if (!bomb.IsPierceBomb && HasDestructibleTile(check))
                return true;

            return !ignoredBombTiles.Contains(check) && IsBombAtTile(check);
        }

        return IsTileInBlastLine(
            bombTile,
            tile,
            radius,
            BlocksPredictedExplosion);
    }

    private bool BlocksExplosionForPierce(Vector2Int tile)
    {
        return HasIndestructibleTile(tile) || IsBombAtTile(tile);
    }

    private string ResolveEscapeAbilityThreatKey(Vector2Int tile)
    {
        Bomb bestBomb = null;
        float bestSeconds = float.PositiveInfinity;
        int bestDistance = int.MaxValue;

        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded || bomb.IsBeingHeldByPowerGlove)
                continue;

            Vector2Int bombTile = WorldToTile(bomb.GetLogicalPosition());
            int radius = bomb.Owner != null ? Mathf.Max(1, bomb.Owner.GetPredictedBlastRadius(bomb)) : 2;
            if (!IsBombBlastReachingTile(bomb, bombTile, tile, radius))
                continue;

            float seconds = bomb.IsControlBomb ? 0.65f : bomb.RemainingFuseSeconds;
            int distance = Manhattan(tile, bombTile);
            if (seconds > bestSeconds || (Mathf.Approximately(seconds, bestSeconds) && distance >= bestDistance))
                continue;

            bestBomb = bomb;
            bestSeconds = seconds;
            bestDistance = distance;
        }

        if (bestBomb != null)
            return $"bomb:{bestBomb.GetEntityId()}";

        if (explosionMask != 0)
        {
            Collider2D explosion = Physics2D.OverlapCircle(TileToWorld(tile), tileSize * 0.25f, explosionMask);
            if (explosion != null)
                return $"explosion:{WorldToTile(explosion.bounds.center)}";
        }

        if (suddenDeathController != null &&
            suddenDeathController.TryGetSecondsUntilSuddenDeathWorldPosition(TileToWorld(tile), out float suddenDeathSeconds) &&
            suddenDeathSeconds <= SuddenDeathUnsafeLeadSeconds)
        {
            return $"suddenDeath:{tile}";
        }

        return $"tile:{tile}";
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

        if (TryGetComponent(out BattleModeComTankThreatAwarenessAbility tankAwareness))
        {
            bool currentTankThreat =
                tankAwareness.HasImmediateThreat(currentTile, out float currentTankThreatSeconds);
            bool nextTankThreat =
                tankAwareness.HasImmediateThreat(nextTile, out float nextTankThreatSeconds);
            if (currentTankThreat || nextTankThreat)
            {
                float traversalSeconds =
                    EstimateFirstMoveTraversalSeconds(DirectionToTile(requestedMove));
                if (!tankAwareness.CanSafelyTraverseThreatenedTile(
                        currentTile,
                        nextTile,
                        traversalSeconds,
                        out string blockReason))
                {
                    float tankThreatSeconds = Mathf.Min(
                        currentTankThreatSeconds,
                        nextTankThreatSeconds);
                    tankAwareness.LogPreventedEntry(
                        currentTile,
                        nextTile,
                        tankThreatSeconds,
                        blockReason);
                    return Vector2.zero;
                }
            }
        }

        if (currentAction == BattleModeComActionType.KickBomb)
        {
            float kickCurrentDanger = GetDangerSeconds(currentTile, null);
            float kickNextDanger = GetDangerSeconds(nextTile, null);
            float kickNextArrivalSeconds = EstimateFirstMoveTraversalSeconds(DirectionToTile(requestedMove));
            bool kickNextWalkable = IsWalkableTile(nextTile, currentTile);
            float kickNextMargin = GetSurvivalMarginSeconds(nextTile, kickNextArrivalSeconds, settings, null);

            LogKickBombRiskDiagnostic(
                "SAFETY_BYPASS",
                currentTile,
                kickCurrentDanger,
                $"requested:{FirstMoveDescription(requestedMove)} next:{nextTile} nextWalkable:{kickNextWalkable} " +
                $"nextDanger:{FormatDanger(kickNextDanger)} nextArrival:{kickNextArrivalSeconds:F2}s " +
                $"nextMargin:{kickNextMargin:F2}s target:{(hasCurrentTarget ? currentTargetTile.ToString() : "none")} " +
                $"reason:{currentReason} input:{currentInputDescription} abilities:{FormatCurrentAbilityTraces()} " +
                $"activeBombs:{FormatActiveBombThreats(currentTile)} activeExplosions:{FormatActiveExplosionThreats(currentTile)}");
            LogKickLoadDiagnostic($"allow KickBomb move {currentTile}->{nextTile} input:{FirstMoveDescription(requestedMove)} reason:{currentReason}");
            return requestedMove;
        }

        if (!IsWalkableTile(nextTile, currentTile))
        {
            LogPostPlantEscapeDiagnostic(
                "SAFETY_BLOCKED",
                currentTile,
                GetDangerSeconds(currentTile, null),
                $"requested:{FirstMoveDescription(requestedMove)} next:{nextTile} action:{currentAction} reason:{currentReason}");
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
        float nextArrivalSecondsForOwnChain = EstimateFirstMoveTraversalSeconds(DirectionToTile(requestedMove));

        if (ShouldAllowOwnChainTimedMove(nextTile, nextArrivalSecondsForOwnChain, settings))
            return requestedMove;

        if (TryGetComponent(out BattleModeComStage4SpringEscapeAbility springAbility))
        {
            bool committedSpringMove =
                springAbility.IsCommittedSpringTarget(nextTile) &&
                !string.IsNullOrEmpty(currentReason) &&
                currentReason.StartsWith(
                    "escape stage 4 spring",
                    StringComparison.Ordinal);

            if (springAbility.IsSpringTile(nextTile) &&
                !committedSpringMove &&
                !springAbility.HasSafeImmediateLanding(
                    nextTile,
                    out string springBlockTrace))
            {
                springAbility.LogSpringEntryBlocked(nextTile, springBlockTrace);
                return Vector2.zero;
            }
        }

        if (springAbility != null &&
            springAbility.IsCommittedSpringTarget(nextTile) &&
            !string.IsNullOrEmpty(currentReason) &&
            currentReason.StartsWith("escape stage 4 spring", StringComparison.Ordinal))
        {
            float springArrivalSeconds =
                EstimateFirstMoveTraversalSeconds(DirectionToTile(requestedMove));
            if ((float.IsInfinity(currentDanger) ||
                 currentDanger > springArrivalSeconds + DangerTimingMarginSeconds) &&
                (float.IsInfinity(nextDanger) ||
                 nextDanger > springArrivalSeconds + DangerTimingMarginSeconds))
            {
                return requestedMove;
            }
        }

        if (!float.IsInfinity(currentDanger))
        {
            float nextArrivalSeconds = EstimateFirstMoveTraversalSeconds(DirectionToTile(requestedMove));
            if (currentMoveFollowsEscapeRoute &&
                currentDanger > nextArrivalSeconds + DangerTimingMarginSeconds &&
                !IsDangerousAt(nextTile, nextArrivalSeconds, settings, null))
            {
                return requestedMove;
            }

            float nextMargin = GetSurvivalMarginSeconds(nextTile, nextArrivalSeconds, settings, null);
            float currentMargin = currentDanger - settings.dangerReactionSeconds;
            if (currentMargin <= 0f)
            {
                LogPostPlantEscapeDiagnostic(
                    "SAFETY_ALLOW_FATAL_ESCAPE_MOVE",
                    currentTile,
                    currentDanger,
                    $"requested:{FirstMoveDescription(requestedMove)} next:{nextTile} " +
                    $"currentMargin:{currentMargin:F2}s nextMargin:{nextMargin:F2}s nextArrival:{nextArrivalSeconds:F2}s");
                return requestedMove;
            }

            if (nextMargin + DangerTimingMarginSeconds < currentMargin)
            {
                LogPostPlantEscapeDiagnostic(
                    "SAFETY_HOLD_DANGER_TIMING",
                    currentTile,
                    currentDanger,
                    $"requested:{FirstMoveDescription(requestedMove)} next:{nextTile} " +
                    $"currentMargin:{currentMargin:F2}s nextMargin:{nextMargin:F2}s nextArrival:{nextArrivalSeconds:F2}s");
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
            LogPostPlantEscapeDiagnostic(
                "SAFETY_BLOCK_UNSAFE_NEXT",
                currentTile,
                GetDangerSeconds(currentTile, null),
                $"requested:{FirstMoveDescription(requestedMove)} next:{nextTile} nextDanger:{FormatDanger(nextDanger)}");
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

    private bool ShouldAllowOwnChainTimedMove(
        Vector2Int nextTile,
        float arrivalSeconds,
        BattleModeComDifficultySettings settings)
    {
        if (!ownChainPlanActive)
            return false;

        if (currentAction != BattleModeComActionType.Reposition &&
            currentAction != BattleModeComActionType.CombatPlant)
        {
            return false;
        }

        if (string.IsNullOrEmpty(currentReason) ||
            !currentReason.Contains("own-chain", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        float margin = GetSurvivalMarginSeconds(nextTile, arrivalSeconds, settings, null);
        return margin >= OwnChainUnsafeMoveMarginSeconds;
    }

    private Vector2 ApplyTurnAxisCentering(Vector2Int currentTile, Vector2 requestedMove)
    {
        if (requestedMove == Vector2.zero)
        {
            turnAxisCenteredKeepDir = Vector2Int.zero;
            return Vector2.zero;
        }

        Vector2Int requestedDirection = DirectionToTile(requestedMove);
        if (requestedDirection == Vector2Int.zero)
        {
            turnAxisCenteredKeepDir = Vector2Int.zero;
            return Vector2.zero;
        }

        bool needsCentering = NeedsTurnAxisCentering(currentTile, requestedDirection);

        // Histerese: se já consideramos o personagem centrado para esta direção, só
        // retomamos a centralização quando o desvio crescer bem além da tolerância.
        // Sem isso, centralizar -> entrar na tolerância -> ser empurrado de volta gera
        // input alternado (esq/dir ou cima/baixo) e a IA nunca executa a virada.
        if (needsCentering &&
            turnAxisCenteredKeepDir == requestedDirection &&
            !TurnAxisOffsetExceeds(currentTile, requestedDirection, TurnAxisCenteringExitMultiplier))
        {
            needsCentering = false;
        }

        // Overshoot: o passo por frame é maior que a zona morta, então o personagem
        // cruza o centro sem nunca cair dentro da tolerância (sinal do desvio inverte
        // a cada frame de centralização). Se estávamos centralizando para esta direção
        // e o sinal inverteu, consideramos centrado e seguimos o move pedido.
        if (needsCentering)
        {
            int offsetSign = GetTurnAxisOffsetSign(currentTile, requestedDirection);
            if (turnAxisCenteringDir == requestedDirection &&
                turnAxisCenteringSign != 0 &&
                offsetSign != 0 &&
                offsetSign != turnAxisCenteringSign &&
                !TurnAxisOffsetExceeds(currentTile, requestedDirection, TurnAxisCenteringExitMultiplier))
            {
                needsCentering = false;
                turnAxisCenteredKeepDir = requestedDirection;
                turnAxisCenteringDir = Vector2Int.zero;
                turnAxisCenteringSign = 0;
            }
            else
            {
                turnAxisCenteringDir = requestedDirection;
                turnAxisCenteringSign = offsetSign;
            }
        }
        else
        {
            turnAxisCenteringDir = Vector2Int.zero;
            turnAxisCenteringSign = 0;
        }

        if (currentMoveFollowsEscapeRoute && needsCentering)
        {
            // A perpendicular turn cannot pass the tile collision while the player is
            // between grid lanes. Finish that short alignment even during an escape.
            turnAxisCenteredKeepDir = Vector2Int.zero;
            return GetTurnAxisCenteringMove(currentTile, requestedDirection);
        }

        if (ShouldKeepTargetedMove(currentTile, requestedDirection))
        {
            if (!needsCentering)
                turnAxisCenteredKeepDir = requestedDirection;
            return requestedMove;
        }

        if (needsCentering)
        {
            turnAxisCenteredKeepDir = Vector2Int.zero;
            return GetTurnAxisCenteringMove(currentTile, requestedDirection);
        }

        turnAxisCenteredKeepDir = requestedDirection;
        return requestedMove;
    }

    private int GetTurnAxisOffsetSign(Vector2Int currentTile, Vector2Int requestedDirection)
    {
        Vector2 delta = TileToWorld(currentTile) - (Vector2)transform.position;
        float axisDelta = requestedDirection.x != 0 ? delta.y : delta.x;

        if (Mathf.Abs(axisDelta) < 0.001f)
            return 0;

        return axisDelta > 0f ? 1 : -1;
    }

    private bool TurnAxisOffsetExceeds(Vector2Int currentTile, Vector2Int requestedDirection, float toleranceMultiplier)
    {
        Vector2 delta = TileToWorld(currentTile) - (Vector2)transform.position;
        float tolerance = Mathf.Max(0.01f, tileSize * TurnAxisCenterTolerance) * toleranceMultiplier;

        if (requestedDirection.x != 0)
            return Mathf.Abs(delta.y) > tolerance;

        if (requestedDirection.y != 0)
            return Mathf.Abs(delta.x) > tolerance;

        return false;
    }

    private bool NeedsTurnAxisCentering(Vector2Int currentTile, Vector2Int requestedDirection)
    {
        Vector2 delta = TileToWorld(currentTile) - (Vector2)transform.position;
        float tolerance = Mathf.Max(0.01f, tileSize * TurnAxisCenterTolerance);

        if (requestedDirection.x != 0)
            return Mathf.Abs(delta.y) > tolerance;

        if (requestedDirection.y != 0)
            return Mathf.Abs(delta.x) > tolerance;

        return false;
    }

    private Vector2 GetTurnAxisCenteringMove(Vector2Int currentTile, Vector2Int requestedDirection)
    {
        Vector2 delta = TileToWorld(currentTile) - (Vector2)transform.position;

        if (requestedDirection.x != 0)
            return delta.y > 0f ? Vector2.up : Vector2.down;

        if (requestedDirection.y != 0)
            return delta.x > 0f ? Vector2.right : Vector2.left;

        return Vector2.zero;
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
            if (bomb == null || bomb.HasExploded || bomb.IsBeingHeldByPowerGlove)
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

    private float EstimateEscapeTraversalSeconds(Vector2Int start, Vector2Int target, int depth)
    {
        if (depth <= 0 || target == start)
            return 0f;

        Vector2Int firstStep = ReconstructFirstStep(start, target);
        return EstimateTraversalSeconds(depth) + EstimateTurnAxisCenteringSeconds(firstStep);
    }

    private float EstimateEscapeTraversalSeconds(Vector2Int start, Vector2Int parent, Vector2Int next, int depth)
    {
        if (depth <= 0 || next == start)
            return 0f;

        Vector2Int firstStep = parent == start
            ? next - start
            : ReconstructFirstStep(start, parent);

        return EstimateTraversalSeconds(depth) + EstimateTurnAxisCenteringSeconds(firstStep);
    }

    private float EstimateFirstMoveTraversalSeconds(Vector2Int firstStep)
    {
        return EstimateTraversalSeconds(1) + EstimateTurnAxisCenteringSeconds(firstStep);
    }

    private float EstimateTurnAxisCenteringSeconds(Vector2Int requestedDirection)
    {
        if (movement == null || requestedDirection == Vector2Int.zero)
            return 0f;

        Vector2Int currentTile = WorldToTile(transform.position);
        Vector2 delta = TileToWorld(currentTile) - (Vector2)transform.position;
        float offAxisDistance = requestedDirection.x != 0
            ? Mathf.Abs(delta.y)
            : requestedDirection.y != 0
                ? Mathf.Abs(delta.x)
                : 0f;

        float tolerance = Mathf.Max(0.01f, tileSize * TurnAxisCenterTolerance);
        if (offAxisDistance <= tolerance)
            return 0f;

        return (offAxisDistance - tolerance) / Mathf.Max(1f, movement.speed);
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

    private static string DirectionLabel(Vector2Int direction)
    {
        if (direction == Vector2Int.up)
            return "U";

        if (direction == Vector2Int.down)
            return "D";

        if (direction == Vector2Int.left)
            return "L";

        if (direction == Vector2Int.right)
            return "R";

        return direction.ToString();
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

    private void SetActionAHeld(bool held)
    {
        PlayerInputManager input = PlayerInputManager.Instance;
        if (input != null)
            input.SetSyntheticHeld(playerId, PlayerAction.ActionA, held);
    }

    private void ClearSyntheticInputs()
    {
        currentHoldActionA = false;

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
