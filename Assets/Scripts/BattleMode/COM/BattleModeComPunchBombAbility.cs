using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Ensina a IA a usar BombPunchAbility (ActionC) em dois contextos:
///
/// DEFENSIVO (Emergency):
///   Quando encurralada com bomba adjacente e poucas saídas, a IA vira em direção
///   à bomba e pressiona ActionC para socá-la para longe, abrindo espaço de fuga.
///
/// OFENSIVO (Candidate):
///   A IA planta uma bomba na posição atual, recua 1 tile para trás, vira em direção
///   à bomba e pressiona ActionC para socá-la exatamente punchDistanceTiles tiles
///   em direção ao adversário alinhado.
///
/// O soco pausa o fuse da bomba durante o arco de voo — depois o fuse retoma.
/// Após o soco a IA foge via BFS com detecção de travamento.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(BombController))]
public sealed class BattleModeComPunchBombAbility : MonoBehaviour, IBattleModeComAbility
{
    // Filtro de diagnóstico: 0 = todos os jogadores COM.
    public const int DiagnosticPlayerIdFilter = 0;
    private static readonly bool EnableDefensivePunchDiagnostics = true;
    private const float DefensivePunchLogIntervalSeconds = 0.35f;

    // Cooldown entre sequências ofensivas
    private const float OffensiveCooldownSeconds = 1.5f;

    // Distância padrão do soco (espelha BombPunchAbility.punchDistanceTiles = 3)
    private const int PunchDistanceTiles = 3;

    // Fuse mínima para considerar um soco defensivo ainda útil
    private const float DefensivePunchMinFuseSeconds = 0.5f;

    // Tempo máximo permitido para a sequência ofensiva completa (planta → soco → fuga)
    private const float OffensiveSequenceTimeoutSeconds = 4.0f;

    // Janela curta para o input sintético de plantar bomba ser processado pelo BombController.
    private const float PlantConfirmationGraceSeconds = 0.25f;

    // Tempo de fuga após soco (bomba pausou durante o arco, depois retoma o fuse)
    private const float PostPunchEscapeSeconds = 2.5f;

    private static readonly Vector2Int[] CardinalTiles =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    // ─── Máquina de estados ofensiva ───────────────────────────────────────
    private enum SequenceState
    {
        None,
        RetreatAfterPlant,  // plantou bomba, recuando 1 tile
        FaceToBomb,         // no tile de soco, virando para a bomba (1 frame)
        PunchNow,           // virado para a bomba → pressiona ActionC
        EscapeAfterPunch    // bomba socada, fugindo para tile seguro
    }

    private SequenceState sequenceState;
    private float sequenceStateStartedTime = -10f;

    // Dados da sequência atual
    private Vector2Int sequencePlantTile;
    private Vector2Int sequenceRetreatTile;
    private Vector2Int sequencePunchDir;
    private Bomb sequenceTrackedBomb;
    private float sequencePlantRequestedTime = -10f;
    private float postPunchEscapeUntil = -10f;
    private bool sequencePunchCommandSent;

    // Flag para garantir que BombPunchAbility.lastFacingDir esteja em punchDir
    // antes de pressionar ActionC. É necessário um ciclo de Think() com movimento
    // em punchDir para que movement.FacingDirection (e portanto lastFacingDir) se
    // atualize antes do soco.
    private bool sequenceFaceDirectionSent;

    // Detecção de travamento na fuga pós-soco
    private Vector2Int escapeLastTile;
    private float escapeStuckSince = -10f;
    private Vector2Int escapeLastAttemptedStep;
    private readonly List<Vector2Int> escapeBlockedSteps = new List<Vector2Int>(4);

    // Cache de estrutura BFS
    private struct EscapeNode { public Vector2Int Parent; public int Depth; }
    private readonly Queue<Vector2Int> escapeOpen = new Queue<Vector2Int>();
    private readonly Dictionary<Vector2Int, EscapeNode> escapeVisited = new Dictionary<Vector2Int, EscapeNode>();

    // Cooldown e cache de chance ofensiva
    private float nextOffensiveSequenceTime = -10f;
    private float offensiveTriggerChanceCacheTime = -10f;
    private bool offensiveTriggerChanceCacheResult;

    // ─── Referências ───────────────────────────────────────────────────────
    private PlayerIdentity identity;
    private MovementController movement;
    private BombController bombController;
    private BombPunchAbility punchAbility;
    private GameManager gameManager;
    private Tilemap groundTilemap;
    private Tilemap destructibleTilemap;
    private Tilemap indestructibleTilemap;
    private ContactFilter2D obstacleFilter;
    private Collider2D[] ownColliders;
    private readonly Collider2D[] obstacleHits = new Collider2D[12];
    private float tileSize = 1f;
    private int explosionMask;

    // ─── Diagnóstico ───────────────────────────────────────────────────────
    private string lastDecisionTrace = "not evaluated";
    private float lastDefensivePunchLogTime = -10f;
    private string lastDefensivePunchLogKey = string.Empty;
    private readonly List<string> defensivePunchScanNotes = new List<string>(4);
    private readonly List<string> defensiveSafeExitNotes = new List<string>(4);

    // ─── IBattleModeComAbility ─────────────────────────────────────────────
    public string DiagnosticName => "PunchBomb";
    public string LastDecisionTrace => lastDecisionTrace;

    public bool IsAvailable
    {
        get
        {
            CacheReferences();
            return punchAbility != null && punchAbility.IsEnabled;
        }
    }

    private void Awake() => CacheReferences();
    private void OnEnable() => CacheReferences();

    private void CacheReferences()
    {
        if (identity == null) TryGetComponent(out identity);
        if (movement == null) TryGetComponent(out movement);
        if (bombController == null) TryGetComponent(out bombController);
        if (punchAbility == null) TryGetComponent(out punchAbility);

        ownColliders = GetComponentsInChildren<Collider2D>(true);

        if (movement != null)
        {
            tileSize = Mathf.Max(0.01f, movement.tileSize);
            obstacleFilter = new ContactFilter2D { useLayerMask = true, useTriggers = true };
            obstacleFilter.SetLayerMask(movement.obstacleMask);
        }

        gameManager = GameManager.Instance != null ? GameManager.Instance : FindAnyObjectByType<GameManager>();
        if (gameManager != null)
        {
            groundTilemap = gameManager.groundTilemap;
            destructibleTilemap = gameManager.destructibleTilemap;
            indestructibleTilemap = gameManager.indestructibleTilemap;
        }

        explosionMask = LayerMask.GetMask("Explosion");
    }

    // =========================================================================
    // TryBuildEmergencyDecision — soco DEFENSIVO
    // =========================================================================
    // Chamado quando inDanger=true. Detecta se há uma bomba adjacente que pode
    // ser socada para longe, liberando espaço de fuga quando a IA está encurralada.
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
            lastDecisionTrace = "emergency: punch unavailable";
            return false;
        }

        // Continua sequência ofensiva já em andamento sob perigo da própria bomba.
        if (sequenceState != SequenceState.None)
        {
            if (TryContinueOffensiveSequence(settings, myTile, out decision))
            {
                lastDecisionTrace = "emergency continue offensive -> " + lastDecisionTrace;
                return true;
            }
        }

        // Soco defensivo: bomba adjacente com fuse baixo, poucas saídas disponíveis.
        if (TryBuildDefensivePunch(settings, myTile, currentDangerSeconds, out decision))
            return true;

        lastDecisionTrace = $"emergency no punch option danger:{FormatDanger(currentDangerSeconds)}";
        return false;
    }

    // =========================================================================
    // TryBuildCandidateDecision — soco OFENSIVO
    // =========================================================================
    // Chamado quando inDanger=false. Planta bomba, recua, soca em direção ao inimigo.
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
            lastDecisionTrace = "candidate: punch unavailable";
            return false;
        }

        // Continua sequência ofensiva já em andamento.
        if (sequenceState != SequenceState.None)
        {
            if (TryContinueOffensiveSequence(settings, myTile, out decision))
                return true;
        }

        // Cooldown entre ativações ofensivas.
        if (Time.time < nextOffensiveSequenceTime)
        {
            lastDecisionTrace = $"candidate cooldown {(nextOffensiveSequenceTime - Time.time):F2}s";
            return false;
        }

        // Roll de chance (com cache para evitar re-rolls no mesmo ciclo de Think).
        if (!RollOffensiveChance(settings))
            return false;

        // Pré-condições: precisa de bomba disponível.
        if (bombController == null || bombController.BombsRemaining <= 0)
        {
            lastDecisionTrace = "candidate no bombs remaining";
            return false;
        }

        // Encontra inimigo alinhado a exatamente PunchDistanceTiles + 1 tiles de distância.
        // "+1" porque a bomba é plantada no tile do AI, e a IA fica 1 tile atrás para socar.
        if (!TryFindOffensivePunchTarget(myTile, out PlayerIdentity target,
                out Vector2Int targetTile, out Vector2Int punchDir))
        {
            lastDecisionTrace = "candidate no aligned target at punch range";
            return false;
        }

        // Verifica se o tile de recuo é acessível.
        Vector2Int retreatTile = myTile - punchDir;
        if (!IsWalkableTile(retreatTile, myTile))
        {
            lastDecisionTrace = $"candidate retreat tile blocked dir:{punchDir}";
            return false;
        }

        // Verifica fuga pós-soco: após socar a bomba, a IA precisa ter pelo menos
        // um tile seguro para correr.
        if (!HasEscapeAfterPunch(settings, retreatTile, punchDir, myTile))
        {
            lastDecisionTrace = $"candidate no escape after punch target P{target.playerId}@{targetTile}";
            return false;
        }

        // Configura a sequência ofensiva.
        sequencePlantTile = myTile;
        sequenceRetreatTile = retreatTile;
        sequencePunchDir = punchDir;
        sequenceTrackedBomb = null;
        sequencePlantRequestedTime = Time.time;
        sequencePunchCommandSent = false;
        SetSequenceState(SequenceState.RetreatAfterPlant);
        nextOffensiveSequenceTime = Time.time + OffensiveCooldownSeconds;

        Vector2 retreatMove = TileDirectionToVector(retreatTile - myTile);
        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.KickBomb,
            Weight = Mathf.Max(1, settings.combatPlantWeight + 80 + DifficultyWeight(settings)),
            TargetTile = targetTile,
            HasTarget = true,
            FirstMove = retreatMove,
            Reason = $"punch bomb toward P{target.playerId}",
            InputDescription = AppendInput("ActionA", FirstMoveDescription(retreatMove)),
            TapBomb = true
        };

        lastDecisionTrace =
            $"candidate PLAN plant:{myTile} retreat:{retreatTile} punchDir:{punchDir} target P{target.playerId}@{targetTile}";
        LogSurgical("PLAN",
            $"plant:{myTile} retreat:{retreatTile} punchDir:{punchDir} target:P{target.playerId}@{targetTile}",
            force: true);
        return true;
    }

    // =========================================================================
    // Sequência Ofensiva — máquina de estados
    // =========================================================================

    private bool TryContinueOffensiveSequence(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        // Aborta se a ability foi desabilitada durante a sequência.
        if (!IsAvailable)
        {
            LogSurgical("SEQUENCE_RESET", "ability became unavailable mid-sequence", force: true);
            ResetSequence();
            return false;
        }

        // Aborta se a sequência ficou órfã por muito tempo.
        if (Time.time - sequenceStateStartedTime > OffensiveSequenceTimeoutSeconds &&
            sequenceState != SequenceState.EscapeAfterPunch)
        {
            LogSurgical("SEQUENCE_RESET", $"timeout state:{sequenceState}", force: true);
            ResetSequence();
            return false;
        }

        return sequenceState switch
        {
            SequenceState.RetreatAfterPlant => TryContinueRetreat(settings, myTile, out decision),
            SequenceState.FaceToBomb        => TryContinueFace(settings, myTile, out decision),
            SequenceState.PunchNow          => TryContinuePunch(settings, myTile, out decision),
            SequenceState.EscapeAfterPunch  => TryContinueEscapeAfterPunch(settings, myTile, out decision),
            _ => false
        };
    }

    // Estado: RetreatAfterPlant
    // A IA plantou a bomba e precisa recuar para sequenceRetreatTile.
    private bool TryContinueRetreat(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        // Tenta encontrar a bomba recém-plantada no tile alvo.
        if (sequenceTrackedBomb == null)
            sequenceTrackedBomb = FindBombAt(sequencePlantTile);

        if (ShouldAbortMissingPlantedBomb("RETREAT_MISSING_BOMB", myTile))
        {
            ResetSequence();
            return false;
        }

        if (myTile == sequenceRetreatTile)
        {
            // Chegou no tile de recuo — vira para a bomba no próximo estado.
            SetSequenceState(SequenceState.FaceToBomb);
            LogSurgical("RETREAT_DONE", $"at:{myTile} bomb:{DescribeBomb(sequenceTrackedBomb)}");
            return TryContinueFace(settings, myTile, out decision);
        }

        // Abortamos se a bomba explodiu antes de podermos socar.
        if (sequenceTrackedBomb != null && sequenceTrackedBomb.HasExploded)
        {
            LogSurgical("SEQUENCE_RESET", "bomb exploded during retreat", force: true);
            ResetSequence();
            return false;
        }

        // Ainda recuando: continua movendo em direção ao retreatTile.
        Vector2 retreatMove = TileDirectionToVector(sequenceRetreatTile - myTile);
        decision = MakeSequenceDecision(retreatMove, 280 + DifficultyWeight(settings), "retreating after plant");

        lastDecisionTrace = $"RETREAT from:{myTile} to:{sequenceRetreatTile}";
        LogSurgical("RETREAT", $"from:{myTile} to:{sequenceRetreatTile} bomb:{DescribeBomb(sequenceTrackedBomb)}");
        return true;
    }

    // Estado: FaceToBomb
    // Garante que a IA está virada em direção à bomba antes de socar.
    private bool TryContinueFace(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        if (sequenceTrackedBomb == null)
            sequenceTrackedBomb = FindBombAt(sequencePlantTile);

        if (ShouldAbortMissingPlantedBomb("FACE_MISSING_BOMB", myTile))
        {
            ResetSequence();
            return false;
        }

        if (sequenceTrackedBomb != null && sequenceTrackedBomb.HasExploded)
        {
            LogSurgical("SEQUENCE_RESET", "bomb exploded during face", force: true);
            ResetSequence();
            return false;
        }

        // Usa condição de early punch: BombPunchAbility não precisa de IsSolid para socar
        // quando o jogador está a ≥ earlyPunchMinExitFraction (0.3 tiles) da bomba.
        // A IA está a exatamente 1 tile → early punch sempre válido.
        // Só precisamos garantir que a bomba não explodiu, não foi chutada nem socada.
        bool punchable = sequenceTrackedBomb != null &&
                         !sequenceTrackedBomb.HasExploded &&
                         !sequenceTrackedBomb.IsBeingKicked &&
                         !sequenceTrackedBomb.IsBeingPunched;

        if (!punchable)
        {
            // Bomba sendo chutada/socada por outro motivo — aguarda virado para ela.
            // Reseta o flag para que haja um ciclo de alinhamento de facing quando
            // a bomba ficar disponível novamente.
            sequenceFaceDirectionSent = false;
            decision = MakeSequenceDecision(
                TileDirectionToVector(sequencePunchDir), 280 + DifficultyWeight(settings),
                "waiting for punch ready");
            lastDecisionTrace = "FACE_WAIT bomb not punchable yet";
            LogSurgical("FACE_WAIT", $"bomb:{DescribeBomb(sequenceTrackedBomb)}");
            return true;
        }

        // Antes de pressionar ActionC, garantimos que BombPunchAbility.lastFacingDir
        // já aponta em punchDir. Sem esse ciclo, lastFacingDir ainda reflete a direção
        // do recuo (oposta ao soco), e a ability busca a bomba na direção errada —
        // o soco nunca dispara, especialmente no eixo X (horizontal).
        // Enviamos um ciclo com FirstMove = punchDir e sem ActionC; o player é
        // bloqueado fisicamente pela bomba mas movement.FacingDirection fica = punchDir,
        // atualizando lastFacingDir corretamente.
        if (!sequenceFaceDirectionSent)
        {
            sequenceFaceDirectionSent = true;
            decision = MakeSequenceDecision(
                TileDirectionToVector(sequencePunchDir), 290 + DifficultyWeight(settings),
                "aligning facing before punch");
            lastDecisionTrace = "FACE_SEND aligning facing direction";
            LogSurgical("FACE_SEND", $"aligning facing dir:{sequencePunchDir} bomb:{DescribeBomb(sequenceTrackedBomb)}");
            return true;
        }

        // Facing alinhado → transiciona para PunchNow e pressiona ActionC.
        SetSequenceState(SequenceState.PunchNow);
        LogSurgical("FACE_DONE", $"bomb ready for punch, transitioning to PunchNow");
        return TryContinuePunch(settings, myTile, out decision);
    }

    // Estado: PunchNow
    // IA está 1 tile atrás da bomba, virada para ela → pressiona ActionC.
    private bool TryContinuePunch(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        if (sequenceTrackedBomb == null)
            sequenceTrackedBomb = FindBombAt(sequencePlantTile);

        if (sequenceTrackedBomb == null && !sequencePunchCommandSent)
        {
            lastDecisionTrace = $"missing planted bomb before punch plant:{sequencePlantTile} my:{myTile}";
            LogSurgical("SEQUENCE_RESET",
                lastDecisionTrace,
                force: true);
            ResetSequence();
            return false;
        }

        // Verifica se o soco já aconteceu:
        // - bomba sumiu (null): soco enviou para fora do mapa ou explodiu em voo
        // - IsBeingPunched: soco em andamento (arco de voo)
        // NÃO usar !CanBePunched como indicador — early punch é válido mesmo sem IsSolid.
        if (sequenceTrackedBomb == null || sequenceTrackedBomb.IsBeingPunched)
        {
            // Soco disparado → iniciar fuga.
            sequencePunchCommandSent = true;
            SetSequenceState(SequenceState.EscapeAfterPunch);
            postPunchEscapeUntil = Time.time + PostPunchEscapeSeconds;
            escapeLastTile = myTile;
            escapeStuckSince = -1f;
            escapeBlockedSteps.Clear();
            escapeLastAttemptedStep = Vector2Int.zero;
            LogSurgical("PUNCH_FIRED", $"my:{myTile} bomb:{DescribeBomb(sequenceTrackedBomb)}", force: true);
            return TryContinueEscapeAfterPunch(settings, myTile, out decision);
        }

        // Bomba explodiu antes de ser socada — aborta sequência.
        if (sequenceTrackedBomb.HasExploded)
        {
            LogSurgical("SEQUENCE_RESET", "bomb exploded before punch", force: true);
            ResetSequence();
            return false;
        }

        // Aborta se fuse muito baixo (bomba vai explodir antes do soco ter efeito).
        float fuse = sequenceTrackedBomb.RemainingFuseSeconds;
        if (fuse < 0.25f)
        {
            LogSurgical("SEQUENCE_RESET", $"fuse too low to punch fuse:{fuse:F2}", force: true);
            ResetSequence();
            return false;
        }

        // Detecta se o player passou pelo tile da bomba sem socar.
        // Calcula o dot product de (myTile - bombTile) com punchDir:
        //   dot < 0 → player está atrás da bomba (posição correta)
        //   dot > 0 → player passou pela bomba e está na frente (zona de explosão)
        var relToBomb = myTile - sequencePlantTile;
        int passDot = relToBomb.x * sequencePunchDir.x + relToBomb.y * sequencePunchDir.y;
        if (passDot > 0)
        {
            LogSurgical("SEQUENCE_RESET", $"player passed bomb tile my:{myTile} bombTile:{sequencePlantTile}", force: true);
            ResetSequence();
            return false;
        }

        // Move em direção à bomba (para atualizar lastFacingDir na BombPunchAbility)
        // E pressiona ActionC no mesmo frame.
        Vector2 punchMove = TileDirectionToVector(sequencePunchDir);
        sequencePunchCommandSent = true;
        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.KickBomb,
            Weight = 300 + DifficultyWeight(settings),
            TargetTile = sequencePlantTile,
            HasTarget = true,
            FirstMove = punchMove,
            Reason = "punch bomb offensive",
            InputDescription = AppendInput(FirstMoveDescription(punchMove), "ActionC"),
            TapActionC = true
        };

        lastDecisionTrace = $"PUNCH my:{myTile} bomb:{sequencePlantTile} dir:{sequencePunchDir} fuse:{fuse:F2}";
        LogSurgical("PUNCH", $"my:{myTile} bombTile:{sequencePlantTile} dir:{sequencePunchDir} fuse:{fuse:F2}");
        return true;
    }

    // Estado: EscapeAfterPunch
    // BFS de fuga com detecção de travamento (mesmo padrão do KickBombAbility).
    private bool TryContinueEscapeAfterPunch(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        float dangerHere = GetDangerSeconds(myTile);

        // Chegou em tile seguro.
        // Guarda: não declarar ESCAPE_DONE enquanto a bomba socada ainda está em voo.
        // Durante o arco de punch, a bomba ainda está em voo, mas a posição lógica
        // já aponta para o pouso previsto. Não encerra a fuga antes do pouso.
        bool trackedBombStillFlying = sequenceTrackedBomb != null
                                      && !sequenceTrackedBomb.HasExploded
                                      && sequenceTrackedBomb.IsBeingPunched;
        if (float.IsInfinity(dangerHere) && !trackedBombStillFlying)
        {
            LogSurgical("ESCAPE_DONE", $"my:{myTile}");
            ResetSequence();
            return false;
        }

        // Timer de fuga expirou — entrega ao sistema de fuga nativo do controller.
        if (Time.time > postPunchEscapeUntil)
        {
            LogSurgical("ESCAPE_EXPIRED",
                $"my:{myTile} danger:{FormatDanger(dangerHere)}", force: true);
            ResetSequence();
            return false;
        }

        // Detecção de travamento.
        UpdateEscapeStuckDetection(myTile);

        if (!TryFindEscapeMove(settings, myTile, escapeBlockedSteps,
                out Vector2 firstMove, out Vector2Int target, out string route))
        {
            LogSurgical("ESCAPE_FAILED",
                $"my:{myTile} danger:{FormatDanger(dangerHere)}", force: true);
            ResetSequence();
            return false;
        }

        escapeLastAttemptedStep = Vector2Int.RoundToInt(firstMove);

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.Reposition,
            Weight = 290 + DifficultyWeight(settings),
            TargetTile = target,
            HasTarget = true,
            FirstMove = firstMove,
            Reason = "escape after punch",
            InputDescription = FirstMoveDescription(firstMove)
        };

        lastDecisionTrace = $"ESCAPE my:{myTile} target:{target} route:{route} danger:{FormatDanger(dangerHere)}";
        LogSurgical("ESCAPE",
            $"my:{myTile} target:{target} move:{FirstMoveDescription(firstMove)} route:{route} danger:{FormatDanger(dangerHere)}");
        return true;
    }

    // =========================================================================
    // Soco Defensivo
    // =========================================================================

    private bool TryBuildDefensivePunch(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        float currentDangerSeconds,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        // Soco defensivo só vale a pena se o fuse ainda dá tempo.
        if (currentDangerSeconds < DefensivePunchMinFuseSeconds)
        {
            lastDecisionTrace = "defensive punch: fuse too low";
            LogSurgical("DEF_REJECT",
                $"reason:danger-fuse-low my:{myTile} danger:{FormatDanger(currentDangerSeconds)} min:{DefensivePunchMinFuseSeconds:F2}");
            return false;
        }

        // Conta saídas seguras sem socar — se há saídas suficientes, não precisa socar.
        int safeExits = CountSafeExits(settings, myTile);
        if (safeExits > 0)
        {
            lastDecisionTrace = $"defensive punch: not cornered exits:{safeExits}";
            LogSurgical("DEF_REJECT",
                $"reason:not-cornered my:{myTile} danger:{FormatDanger(currentDangerSeconds)} exits:{safeExits} exitsScan:{BuildDefensiveSafeExitSummary()} adjacentBombs:{BuildAdjacentPunchBombSummary(myTile)}");
            return false;
        }

        // Busca bomba adjacente que pode ser socada.
        Bomb bestBomb = null;
        Vector2Int bestPunchDir = Vector2Int.zero;
        float bestFuse = float.PositiveInfinity;
        defensivePunchScanNotes.Clear();

        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int dir = CardinalTiles[i];
            Vector2Int neighborTile = myTile + dir;
            Bomb bomb = FindBombAt(neighborTile);

            if (bomb == null)
            {
                defensivePunchScanNotes.Add($"{DirectionLabel(dir)}:{neighborTile}:no-bomb");
                continue;
            }

            bool canPunchNow = CanDefensivePunchBomb(bomb);
            if (!canPunchNow)
            {
                defensivePunchScanNotes.Add($"{DirectionLabel(dir)}:{DescribeBomb(bomb)}:cannot-punch");
                continue;
            }

            float fuse = bomb.IsControlBomb ? 0.65f : bomb.RemainingFuseSeconds;
            if (fuse < DefensivePunchMinFuseSeconds)
            {
                defensivePunchScanNotes.Add($"{DirectionLabel(dir)}:{DescribeBomb(bomb)}:fuse-low");
                continue;
            }

            // Verifica que socar a bomba nessa direção a move para longe (não volta na IA).
            // Ao socar em direção "dir", a bomba voa mais "dir" tiles, saindo do range.
            int blastRadius = bomb.Owner != null
                ? Mathf.Max(1, bomb.Owner.GetPredictedBlastRadius(bomb))
                : 2;

            // Após o soco, bomba estará a (1 + PunchDistanceTiles) tiles → safe se radius < 1+PunchDistanceTiles
            bool punchMakesCurrentTileSafe = blastRadius < 1 + PunchDistanceTiles;

            if (!punchMakesCurrentTileSafe)
            {
                // Mesmo que não fique safe imediatamente, tenta: pode abrir caminho perpendicular.
                // Verifica se ao socar, pelo menos um tile perpendicular se torna acessível.
                bool opensPerpendicular = false;
                Vector2Int perp1 = new Vector2Int(-dir.y, dir.x);
                Vector2Int perp2 = new Vector2Int(dir.y, -dir.x);
                if (IsWalkableTile(myTile + perp1, myTile) || IsWalkableTile(myTile + perp2, myTile))
                    opensPerpendicular = true;

                if (!opensPerpendicular)
                {
                    defensivePunchScanNotes.Add(
                        $"{DirectionLabel(dir)}:{DescribeBomb(bomb)}:blast:{blastRadius}:no-perp-open");
                    continue;
                }
            }

            defensivePunchScanNotes.Add(
                $"{DirectionLabel(dir)}:{DescribeBomb(bomb)}:candidate-{PunchReadinessLabel(bomb)}:blast:{blastRadius}:current-safe:{punchMakesCurrentTileSafe}");

            if (fuse < bestFuse)
            {
                bestFuse = fuse;
                bestBomb = bomb;
                bestPunchDir = dir;
            }
        }

        if (bestBomb == null)
        {
            lastDecisionTrace = "defensive punch: no punchable bomb adjacent";
            LogSurgical("DEF_REJECT",
                $"reason:no-eligible-adjacent-bomb my:{myTile} danger:{FormatDanger(currentDangerSeconds)} exits:{safeExits} scan:{BuildDefensiveScanSummary()}",
                force: true);
            return false;
        }

        // Soco defensivo: move em direção à bomba (para atualizar facing) + ActionC.
        Vector2 punchMove = TileDirectionToVector(bestPunchDir);
        if (movement != null)
            movement.ForceFacingDirection(punchMove);

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.KickBomb,
            Weight = 320 + DifficultyWeight(settings),
            TargetTile = myTile + bestPunchDir * (1 + PunchDistanceTiles),
            HasTarget = true,
            FirstMove = punchMove,
            Reason = "defensive punch to escape",
            InputDescription = AppendInput(FirstMoveDescription(punchMove), "ActionC"),
            TapActionC = true
        };

        lastDecisionTrace =
            $"DEFENSIVE_PUNCH dir:{bestPunchDir} bomb:{myTile + bestPunchDir} fuse:{bestFuse:F2} exits:{safeExits}";
        LogSurgical("DEF_ACCEPT",
            $"my:{myTile} dir:{DirectionLabel(bestPunchDir)} action:{decision.Action} forcedFacing:{FirstMoveDescription(punchMove)} bomb:{DescribeBomb(bestBomb)} danger:{FormatDanger(currentDangerSeconds)} exits:{safeExits} target:{decision.TargetTile} input:{decision.InputDescription} scan:{BuildDefensiveScanSummary()}",
            force: true);
        return true;
    }

    // =========================================================================
    // Auxiliares — busca de alvo ofensivo
    // =========================================================================

    /// <summary>
    /// Procura inimigo alinhado a PunchDistanceTiles + 1 tiles (bomba 1 tile à frente,
    /// soco alcança PunchDistanceTiles mais = total de PunchDistanceTiles+1 a partir do AI).
    /// Aceita até +2 tiles de margem para inimigos um pouco mais distantes.
    /// </summary>
    private bool TryFindOffensivePunchTarget(
        Vector2Int myTile,
        out PlayerIdentity target,
        out Vector2Int targetTile,
        out Vector2Int punchDir)
    {
        target = null;
        targetTile = myTile;
        punchDir = Vector2Int.zero;

        float bestScore = float.PositiveInfinity;
        var activePlayers = new List<PlayerIdentity>(6);
        PlayerIdentity.GetActivePlayers(activePlayers);

        for (int i = 0; i < activePlayers.Count; i++)
        {
            PlayerIdentity player = activePlayers[i];
            if (player == null) continue;
            if (identity != null && player == identity) continue;
            if (!player.TryGetComponent<MovementController>(out var tgt) ||
                tgt == null || tgt.isDead) continue;

            Vector2Int enemyTile = WorldToTile(player.transform.position);

            int dx = enemyTile.x - myTile.x;
            int dy = enemyTile.y - myTile.y;

            // Deve estar alinhado em linha ou coluna.
            if (dx != 0 && dy != 0) continue;

            Vector2Int dir;
            int dist;
            if (dx != 0) { dir = dx > 0 ? Vector2Int.right : Vector2Int.left; dist = Mathf.Abs(dx); }
            else         { dir = dy > 0 ? Vector2Int.up : Vector2Int.down;   dist = Mathf.Abs(dy); }

            // A bomba é plantada em myTile. O soco viaja PunchDistanceTiles tiles a partir do tile
            // adjacente que a bomba ocupa após solidificar (myTile).
            // Portanto, a bomba aterrissa em myTile + dir * PunchDistanceTiles.
            // Para atingir o inimigo: inimigo deve estar em myTile + dir * PunchDistanceTiles ± margem.
            // Distância mínima: PunchDistanceTiles (inimigo EXATAMENTE onde a bomba aterrissa).
            // Distância máxima: PunchDistanceTiles + 2 (com margem de explosão).
            int punchLandingDist = PunchDistanceTiles;
            if (dist < punchLandingDist || dist > punchLandingDist + 2) continue;

            // Verifica que o lane está aberto (sem paredes ou destrutíveis bloqueando).
            bool laneOpen = true;
            for (int step = 1; step <= PunchDistanceTiles; step++)
            {
                Vector2Int checkTile = myTile + dir * step;
                if (HasIndestructibleTile(checkTile))
                {
                    laneOpen = false;
                    break;
                }
            }
            if (!laneOpen) continue;

            float score = dist;
            if (score >= bestScore) continue;

            bestScore = score;
            target = player;
            targetTile = enemyTile;
            punchDir = dir;
        }

        return target != null;
    }

    // Verifica se após socar a bomba a IA terá pelo menos 1 tile seguro para fugir.
    private bool HasEscapeAfterPunch(
        BattleModeComDifficultySettings settings,
        Vector2Int punchPosition,
        Vector2Int punchDir,
        Vector2Int plantTile)
    {
        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int dir = CardinalTiles[i];
            if (dir == punchDir) continue; // na direção da bomba = perigoso

            Vector2Int escapeTile = punchPosition + dir;
            if (!IsWalkableTile(escapeTile, punchPosition)) continue;

            // Simula que a bomba será socada — verifica se o escape tile ficaria seguro
            // (sem bombas ativas além da que será socada para longe).
            float danger = GetDangerSeconds(escapeTile);
            if (float.IsInfinity(danger) || danger > settings.safeTileMinimumSeconds + 0.5f)
                return true;
        }

        // Também aceita a direção oposta ao soco se for segura.
        Vector2Int backTile = punchPosition - punchDir;
        if (IsWalkableTile(backTile, punchPosition))
        {
            float danger = GetDangerSeconds(backTile);
            if (float.IsInfinity(danger) || danger > settings.safeTileMinimumSeconds + 0.5f)
                return true;
        }

        return false;
    }

    // =========================================================================
    // BFS de Fuga Pós-Soco
    // =========================================================================

    private bool TryFindEscapeMove(
        BattleModeComDifficultySettings settings,
        Vector2Int start,
        List<Vector2Int> blockedFirstSteps,
        out Vector2 firstMove,
        out Vector2Int target,
        out string route)
    {
        firstMove = Vector2.zero;
        target = start;
        route = "none";

        escapeVisited.Clear();
        escapeOpen.Clear();
        escapeVisited[start] = new EscapeNode { Parent = start, Depth = 0 };

        for (int i = 0; i < blockedFirstSteps.Count; i++)
            if (blockedFirstSteps[i] != Vector2Int.zero)
                escapeVisited[start + blockedFirstSteps[i]] = new EscapeNode { Parent = start, Depth = 0 };

        escapeOpen.Enqueue(start);
        int maxDepth = Mathf.Max(3, settings.searchDepth + 4);

        while (escapeOpen.Count > 0)
        {
            Vector2Int tile = escapeOpen.Dequeue();
            EscapeNode node = escapeVisited[tile];
            float eta = EstimateTraversalSeconds(node.Depth);
            float dangerSeconds = GetDangerSeconds(tile);

            if (node.Depth > 0 &&
                float.IsInfinity(dangerSeconds) &&
                !IsDangerousAt(tile, eta + settings.safeTileMinimumSeconds, settings))
            {
                target = tile;
                Vector2Int firstStep = ReconstructFirstStep(start, tile);
                firstMove = TileDirectionToVector(firstStep);
                route = $"escape depth {node.Depth}";
                return firstMove != Vector2.zero;
            }

            if (node.Depth >= maxDepth) continue;

            for (int i = 0; i < CardinalTiles.Length; i++)
            {
                Vector2Int next = tile + CardinalTiles[i];
                if (escapeVisited.ContainsKey(next)) continue;
                if (!IsWalkableTile(next, start)) continue;
                if (IsDangerousAt(next, EstimateTraversalSeconds(node.Depth + 1), settings)) continue;

                escapeVisited[next] = new EscapeNode { Parent = tile, Depth = node.Depth + 1 };
                escapeOpen.Enqueue(next);
            }
        }

        return false;
    }

    private Vector2Int ReconstructFirstStep(Vector2Int start, Vector2Int goal)
    {
        Vector2Int current = goal;
        int guard = 0;
        while (escapeVisited.TryGetValue(current, out EscapeNode node) &&
               node.Parent != start && guard++ < 64)
            current = node.Parent;
        return current - start;
    }

    private void UpdateEscapeStuckDetection(Vector2Int myTile)
    {
        if (myTile == escapeLastTile)
        {
            if (escapeStuckSince < 0f)
                escapeStuckSince = Time.time;
            else if (Time.time - escapeStuckSince > 0.25f
                     && escapeLastAttemptedStep != Vector2Int.zero
                     && !escapeBlockedSteps.Contains(escapeLastAttemptedStep))
            {
                escapeBlockedSteps.Add(escapeLastAttemptedStep);
                escapeStuckSince = -1f;
                LogSurgical("ESCAPE_STUCK",
                    $"my:{myTile} blocking:{escapeLastAttemptedStep} total:{escapeBlockedSteps.Count}",
                    force: true);
            }
        }
        else
        {
            escapeLastTile = myTile;
            escapeStuckSince = -1f;
            escapeBlockedSteps.Clear();
            escapeLastAttemptedStep = Vector2Int.zero;
        }
    }

    // =========================================================================
    // Auxiliares gerais
    // =========================================================================

    private void SetSequenceState(SequenceState state)
    {
        sequenceStateStartedTime = Time.time;
        sequenceState = state;
    }

    private void ResetSequence()
    {
        SetSequenceState(SequenceState.None);
        sequenceTrackedBomb = null;
        sequencePlantRequestedTime = -10f;
        sequenceFaceDirectionSent = false;
        sequencePunchCommandSent = false;
        postPunchEscapeUntil = -10f;
        escapeLastTile = default;
        escapeStuckSince = -10f;
        escapeLastAttemptedStep = Vector2Int.zero;
        escapeBlockedSteps.Clear();
        offensiveTriggerChanceCacheTime = -10f;
        offensiveTriggerChanceCacheResult = false;
    }

    private bool ShouldAbortMissingPlantedBomb(string key, Vector2Int myTile)
    {
        if (sequenceTrackedBomb != null)
            return false;

        float elapsed = Time.time - sequencePlantRequestedTime;
        if (elapsed <= PlantConfirmationGraceSeconds)
            return false;

        lastDecisionTrace = $"{key} plant:{sequencePlantTile} retreat:{sequenceRetreatTile} my:{myTile} elapsed:{elapsed:F2}";
        LogSurgical("SEQUENCE_RESET",
            lastDecisionTrace,
            force: true);
        return true;
    }

    private BattleModeComAbilityDecision MakeSequenceDecision(
        Vector2 firstMove, int weight, string reason) =>
        new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.KickBomb,
            Weight = weight,
            TargetTile = sequencePlantTile,
            HasTarget = true,
            FirstMove = firstMove,
            Reason = reason,
            InputDescription = FirstMoveDescription(firstMove)
        };

    private bool RollOffensiveChance(BattleModeComDifficultySettings settings)
    {
        if (Time.time - offensiveTriggerChanceCacheTime < 0.001f)
            return offensiveTriggerChanceCacheResult;

        float chance = DifficultyChance(settings, 0.1f, 0.25f, 0.50f);
        bool result = Random.value <= chance;
        offensiveTriggerChanceCacheTime = Time.time;
        offensiveTriggerChanceCacheResult = result;

        if (!result)
        {
            lastDecisionTrace = $"chance fail chance:{chance:F2}";
            LogSurgical("CHANCE_FAIL", $"chance:{chance:F2}");
        }

        return result;
    }

    private int CountSafeExits(BattleModeComDifficultySettings settings, Vector2Int myTile)
    {
        int count = 0;
        defensiveSafeExitNotes.Clear();

        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int dir = CardinalTiles[i];
            Vector2Int next = myTile + dir;
            Bomb bomb = FindBombAt(next);

            if (!IsWalkableTile(next, myTile))
            {
                defensiveSafeExitNotes.Add(
                    $"{DirectionLabel(dir)}:{next}:blocked bomb:{DescribeBomb(bomb)}");
                continue;
            }

            float eta = EstimateTraversalSeconds(1);
            float danger = GetDangerSeconds(next);
            float requiredSafeSeconds = eta + settings.safeTileMinimumSeconds + settings.dangerReactionSeconds;
            bool safeEnough = HasSafeEscapeRouteThroughFirstStep(settings, myTile, dir, out int routeDepth);
            defensiveSafeExitNotes.Add(
                $"{DirectionLabel(dir)}:{next}:walkable danger:{FormatDanger(danger)} eta:{eta:F2} required:{requiredSafeSeconds:F2} routeDepth:{routeDepth} safe:{safeEnough}");

            if (safeEnough)
                count++;
        }

        return count;
    }

    private bool HasSafeEscapeRouteThroughFirstStep(
        BattleModeComDifficultySettings settings,
        Vector2Int start,
        Vector2Int firstStep,
        out int routeDepth)
    {
        routeDepth = -1;

        Vector2Int firstTile = start + firstStep;
        if (IsDangerousAt(firstTile, EstimateTraversalSeconds(1), settings))
            return false;

        escapeVisited.Clear();
        escapeOpen.Clear();

        escapeVisited[start] = new EscapeNode { Parent = start, Depth = 0 };
        escapeVisited[firstTile] = new EscapeNode { Parent = start, Depth = 1 };
        escapeOpen.Enqueue(firstTile);

        int maxDepth = Mathf.Max(3, settings.searchDepth + 4);

        while (escapeOpen.Count > 0)
        {
            Vector2Int tile = escapeOpen.Dequeue();
            EscapeNode node = escapeVisited[tile];
            float eta = EstimateTraversalSeconds(node.Depth);
            float dangerSeconds = GetDangerSeconds(tile);

            if (float.IsInfinity(dangerSeconds) &&
                !IsDangerousAt(tile, eta + settings.safeTileMinimumSeconds, settings))
            {
                routeDepth = node.Depth;
                return true;
            }

            if (node.Depth >= maxDepth)
                continue;

            for (int i = 0; i < CardinalTiles.Length; i++)
            {
                Vector2Int next = tile + CardinalTiles[i];
                if (escapeVisited.ContainsKey(next)) continue;
                if (!IsWalkableTile(next, tile)) continue;
                if (IsDangerousAt(next, EstimateTraversalSeconds(node.Depth + 1), settings)) continue;

                escapeVisited[next] = new EscapeNode { Parent = tile, Depth = node.Depth + 1 };
                escapeOpen.Enqueue(next);
            }
        }

        return false;
    }

    private Bomb FindBombAt(Vector2Int tile)
    {
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded) continue;
            if (WorldToTile(bomb.GetLogicalPosition()) == tile) return bomb;
        }
        return null;
    }

    private static bool CanDefensivePunchBomb(Bomb bomb)
    {
        if (bomb == null)
            return false;

        if (bomb.CanBePunched)
            return true;

        // BombPunchAbility/StartPunch both allow early punch before the bomb collider
        // becomes solid, which is exactly the trap case after an adjacent bomb is placed.
        return !bomb.HasExploded &&
               !bomb.IsBeingKicked &&
               !bomb.IsBeingPunched;
    }

    private static string PunchReadinessLabel(Bomb bomb) =>
        bomb != null && bomb.CanBePunched ? "solid" : "early";

    private float GetDangerSeconds(Vector2Int tile)
    {
        if (explosionMask != 0)
        {
            Collider2D explosion = Physics2D.OverlapCircle(TileToWorld(tile), tileSize * 0.25f, explosionMask);
            if (explosion != null) return 0f;
        }

        float danger = float.PositiveInfinity;
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded) continue;
            Vector2Int bombTile = WorldToTile(bomb.GetLogicalPosition());
            int radius = bomb.Owner != null ? Mathf.Max(1, bomb.Owner.GetPredictedBlastRadius(bomb)) : 2;
            if (!IsTileInBlastLine(bombTile, tile, radius)) continue;
            float seconds = bomb.IsControlBomb ? 0.65f : bomb.RemainingFuseSeconds;
            danger = Mathf.Min(danger, seconds);
        }
        return danger;
    }

    private bool IsDangerousAt(Vector2Int tile, float arrivalSeconds,
        BattleModeComDifficultySettings settings)
    {
        float dangerSeconds = GetDangerSeconds(tile);
        if (float.IsInfinity(dangerSeconds)) return false;
        return dangerSeconds <= arrivalSeconds + settings.dangerReactionSeconds;
    }

    private bool IsTileInBlastLine(Vector2Int origin, Vector2Int tile, int radius)
    {
        if (origin == tile) return true;
        int dx = tile.x - origin.x;
        int dy = tile.y - origin.y;
        if (dx != 0 && dy != 0) return false;
        if (dx == 0) return Mathf.Abs(dy) <= radius && !IsBlastBlockedBetween(origin, tile);
        return Mathf.Abs(dx) <= radius && !IsBlastBlockedBetween(origin, tile);
    }

    private bool IsBlastBlockedBetween(Vector2Int a, Vector2Int b)
    {
        Vector2Int dir = Vector2Int.zero;
        if (a.x == b.x) dir = b.y > a.y ? Vector2Int.up : Vector2Int.down;
        else dir = b.x > a.x ? Vector2Int.right : Vector2Int.left;

        Vector2Int cur = a + dir;
        while (cur != b)
        {
            if (HasIndestructibleTile(cur) || HasDestructibleTile(cur)) return true;
            cur += dir;
        }
        return false;
    }

    private bool IsWalkableTile(Vector2Int tile, Vector2Int origin)
    {
        if (groundTilemap != null &&
            !groundTilemap.HasTile(groundTilemap.WorldToCell(TileToWorld(tile))))
            return false;
        if (HasIndestructibleTile(tile)) return false;
        if (HasDestructibleTile(tile)) return false;

        Vector2 center = TileToWorld(tile);
        Vector2 size = Vector2.one * (tileSize * 0.6f);
        int hitCount = Physics2D.OverlapBox(center, size, 0f, obstacleFilter, obstacleHits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D col = obstacleHits[i];
            if (col == null) continue;
            bool isOwn = false;
            for (int j = 0; j < ownColliders.Length; j++)
                if (ownColliders[j] == col) { isOwn = true; break; }
            if (!isOwn) return false;
        }
        return true;
    }

    private bool HasIndestructibleTile(Vector2Int tile) =>
        indestructibleTilemap != null &&
        indestructibleTilemap.HasTile(indestructibleTilemap.WorldToCell(TileToWorld(tile)));

    private bool HasDestructibleTile(Vector2Int tile) =>
        destructibleTilemap != null &&
        destructibleTilemap.HasTile(destructibleTilemap.WorldToCell(TileToWorld(tile)));

    private bool HasGroundTile(Vector2Int tile) =>
        groundTilemap == null ||
        groundTilemap.HasTile(groundTilemap.WorldToCell(TileToWorld(tile)));

    private float EstimateTraversalSeconds(int depth) =>
        depth * (tileSize / Mathf.Max(0.01f, movement != null ? movement.speed : 4f));

    private Vector2Int WorldToTile(Vector3 pos) =>
        new Vector2Int(
            Mathf.RoundToInt(pos.x / tileSize),
            Mathf.RoundToInt(pos.y / tileSize));

    private Vector3 TileToWorld(Vector2Int tile) =>
        new Vector3(tile.x * tileSize, tile.y * tileSize, 0f);

    private static Vector2 TileDirectionToVector(Vector2Int dir) => new Vector2(dir.x, dir.y);

    private static string FirstMoveDescription(Vector2 move)
    {
        if (move == Vector2.zero) return "none";
        if (move.x > 0.5f) return "MoveRight";
        if (move.x < -0.5f) return "MoveLeft";
        if (move.y > 0.5f) return "MoveUp";
        return "MoveDown";
    }

    private static string AppendInput(string existing, string input) =>
        string.IsNullOrEmpty(existing) ? input : existing + "+" + input;

    private static string FormatDanger(float seconds)
    {
        if (float.IsInfinity(seconds)) return "safe";
        if (seconds <= 0f) return "now";
        return $"{seconds:F2}";
    }

    private string DescribeBomb(Bomb bomb)
    {
        if (bomb == null) return "null";
        Vector2Int tile = WorldToTile(bomb.GetLogicalPosition());
        return $"{tile}/punched:{bomb.IsBeingPunched}/can:{bomb.CanBePunched}/fuse:{FormatDanger(bomb.RemainingFuseSeconds)}";
    }

    private static string DirectionLabel(Vector2Int dir)
    {
        if (dir == Vector2Int.up) return "U";
        if (dir == Vector2Int.down) return "D";
        if (dir == Vector2Int.left) return "L";
        if (dir == Vector2Int.right) return "R";
        return dir.ToString();
    }

    private string BuildDefensiveScanSummary() =>
        defensivePunchScanNotes.Count == 0
            ? "empty"
            : string.Join("|", defensivePunchScanNotes);

    private string BuildDefensiveSafeExitSummary() =>
        defensiveSafeExitNotes.Count == 0
            ? "empty"
            : string.Join("|", defensiveSafeExitNotes);

    private string BuildAdjacentPunchBombSummary(Vector2Int myTile)
    {
        defensivePunchScanNotes.Clear();

        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int dir = CardinalTiles[i];
            Vector2Int neighborTile = myTile + dir;
            Bomb bomb = FindBombAt(neighborTile);

            if (bomb == null)
            {
                defensivePunchScanNotes.Add($"{DirectionLabel(dir)}:{neighborTile}:no-bomb");
                continue;
            }

            int blastRadius = bomb.Owner != null
                ? Mathf.Max(1, bomb.Owner.GetPredictedBlastRadius(bomb))
                : 2;
            Vector2Int kickLandingTile = neighborTile + dir;
            bool kickLandingBlocked =
                !HasGroundTile(kickLandingTile) ||
                HasIndestructibleTile(kickLandingTile) ||
                HasDestructibleTile(kickLandingTile) ||
                FindBombAt(kickLandingTile) != null;

            defensivePunchScanNotes.Add(
                $"{DirectionLabel(dir)}:{DescribeBomb(bomb)}:punch:{CanDefensivePunchBomb(bomb)}:{PunchReadinessLabel(bomb)}:blast:{blastRadius}:kickLanding:{kickLandingTile}:kickBlocked:{kickLandingBlocked}");
        }

        return BuildDefensiveScanSummary();
    }

    private static int DifficultyWeight(BattleModeComDifficultySettings settings) =>
        settings.difficulty switch
        {
            BattleModeComputerLevel.Easy => -20,
            BattleModeComputerLevel.Hard => 30,
            _ => 0
        };

    private static float DifficultyChance(BattleModeComDifficultySettings settings,
        float easy, float normal, float hard) =>
        settings.difficulty switch
        {
            BattleModeComputerLevel.Easy => easy,
            BattleModeComputerLevel.Hard => hard,
            _ => normal
        };

    private void LogSurgical(string key, string message, bool force = false)
    {
        if (!EnableDefensivePunchDiagnostics) return;
        if (!key.StartsWith("DEF_")) return;

        int id = identity != null ? Mathf.Clamp(identity.playerId, 1, 6) : 0;
        if (DiagnosticPlayerIdFilter != 0 && id != DiagnosticPlayerIdFilter) return;

        string logKey = key + ":" + message;
        if (!force &&
            logKey == lastDefensivePunchLogKey &&
            Time.time - lastDefensivePunchLogTime < DefensivePunchLogIntervalSeconds)
            return;

        lastDefensivePunchLogKey = logKey;
        lastDefensivePunchLogTime = Time.time;
        Vector2Int tile = movement != null ? WorldToTile(movement.transform.position) : Vector2Int.zero;
        Debug.Log($"[BattleCOMPunch][P{id}] tile:{tile} state:{sequenceState} {key} {message}", this);
    }
}
