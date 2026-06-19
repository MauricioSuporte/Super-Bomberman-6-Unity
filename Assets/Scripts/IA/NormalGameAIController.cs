// ============================================================================
//  NormalGameAIController
// ----------------------------------------------------------------------------
//  IA simplificada para controlar players (P2/P3/P4) no Normal Game, pensada
//  para gravar gameplays de 4 jogadores estando sozinho.
//
//  Objetivos da IA:
//    - Plantar bombas para FARMAR (destruir blocos "Destructibles").
//    - Coletar itens (Layer "Item").
//    - Fugir de bombas, explosões e inimigos (Layer "Enemy") para não morrer.
//    - Plantar bomba a uma distância segura para matar inimigos SEM encostar.
//
//  Controle: injeta input sintético no PlayerInputManager (SetSyntheticHeld /
//  TryPlaceBombAtIgnoringInputLock), o mesmo mecanismo usado pelo
//  BattleModeComController. NÃO troca o MovementController do player.
//
//  Liga/desliga: todo o arquivo está sob o Scripting Define Symbol
//  ENABLE_NORMAL_GAME_AI. Sem esse define a IA não é compilada e não é
//  distribuída no build final. Em runtime, o NormalGameAIManager anexa/remove
//  este componente nos players desejados.
//
//  Inspirado em BrainIA.cs (IA de boss/inimigo) e na arquitetura do
//  BattleModeComController, porém com objetivos de "player que sobrevive,
//  farma e elimina inimigos a distância".
// ============================================================================

#if ENABLE_NORMAL_GAME_AI
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class NormalGameAIController : MonoBehaviour
{
    [Header("Thinking")]
    [Tooltip("Intervalo entre decisões quando NÃO está em perigo (segundos).")]
    public float thinkIntervalSafe = 0.12f;
    [Tooltip("Intervalo entre decisões quando está em perigo (segundos).")]
    public float thinkIntervalDanger = 0.04f;

    [Header("Danger / Escape")]
    [Tooltip("Folga mínima (s) para conseguir atravessar um tile antes da explosão.")]
    public float safeTileMinTime = 0.75f;
    [Tooltip("Profundidade máxima do BFS de fuga (em tiles).")]
    public int escapeLookaheadDepth = 10;
    [Tooltip("Maximo de passos ate um tile fora da linha da bomba para aceitar plantar.")]
    public int maxPlantEscapeDepth = 5;
    [Tooltip("Folga extra (s) entre chegar ao esconderijo e a bomba explodir para aceitar plantar.")]
    public float plantEscapeFuseMargin = 0.55f;
    [Tooltip("Margem opcional para considerar um esconderijo confortável depois do plantio. 0 replica a regra do Battle Mode COM: fora da blast line e seguro no tempo de chegada.")]
    public int plantHideBlastPaddingTiles = 0;
    [Tooltip("Distancia maxima do centro do tile para considerar que a IA realmente chegou ao esconderijo.")]
    public float hideCenterToleranceWorld = 0.08f;

    [Header("Enemy Avoidance")]
    [Tooltip("Raio em tiles ao redor de um inimigo tratado como mortal (não pisar).")]
    public int enemyTouchRadiusTiles = 1;
    [Tooltip("Folga extra em mundo contra inimigos entre tiles, para evitar contato durante o movimento.")]
    public float enemyContactPaddingWorld = 0.35f;
    [Tooltip("Distancia minima em tiles para aceitar plantar bomba quando ha inimigos por perto.")]
    public int minEnemyTilesToPlant = 2;
    [Tooltip("Raio (mundo) para escanear inimigos próximos.")]
    public float enemyScanRadiusWorld = 12f;

    [Header("Bombs")]
    [Tooltip("Tempo mínimo entre plantios de bomba (segundos).")]
    public float bombCooldown = 0.5f;
    [Tooltip("Tempo (s) usado para estimar a explosão de uma bomba que a IA vai plantar.")]
    public float assumedBombFuseSeconds = 2f;
    [Tooltip("Plantar bombas para destruir blocos destrutíveis (farm).")]
    public bool farmDestructibles = true;
    [Tooltip("Plantar bombas para matar inimigos a distância.")]
    public bool huntEnemies = true;

    [Header("Items")]
    [Tooltip("Ir atrás de itens para coletá-los.")]
    public bool collectItems = true;
    [Tooltip("Raio (mundo) para escanear itens.")]
    public float itemScanRadiusWorld = 14f;

    [Header("Navigation")]
    [Tooltip("Profundidade máxima do BFS de navegação até objetivos.")]
    public int navLookaheadDepth = 18;
    [Tooltip("Raio (mundo) para detectar e perseguir o tile de end stage quando a fase estiver limpa.")]
    public float endStageScanRadiusWorld = 64f;
    [Tooltip("Distancia maxima do centro do tile de end stage para considerar que a IA ja esta posicionada.")]
    public float endStageCenterToleranceWorld = 0.05f;

    [Header("Debug")]
    [Tooltip("Liga logs de diagnóstico no Console.")]
    public bool debugLogs = false;

    // ===== Referências =====
    private MovementController movement;
    private BombController bomb;
    private StunReceiver stun;       // opcional
    private int playerId = 1;

    // ===== Masks / layers =====
    private int explosionMask;
    private int bombMask;
    private int stageMask;
    private int enemyMask;
    private int itemMask;

    // ===== Estado =====
    private float thinkTimer;
    private float lastBombTime = -999f;
    private Vector2 currentMove = Vector2.zero;
    private float retreatUntilTime = -999f;
    private Vector2 lastPlantTile = Vector2.zero;
    private float startupPlantBlockUntilTime;

    // anti-stuck
    private Vector2 lastWorldPos;
    private float stuckSince = -1f;
    private Vector2 stuckAvoidDir = Vector2.zero;

    // diagnóstico
    private float lastTickLogTime = -10f;
    private Vector2 lastDangerTile;
    private float dangerStuckSince = -1f;
    private float lastEndStageRequirementCheckTime = -999f;
    private bool cachedEndStageRequirementsMet;
    private float lastEndStageTileScanTime = -999f;
    private float lastPlantDecisionLogTime = -10f;
    private string lastEscapeRejectReason = "none";
    private Vector2 lastPlannedHideTile = Vector2.zero;
    private int lastPlannedHideDepth;
    private float lastPlannedHideEta;
    private bool loggedDeadState;

    // ===== Buffers compartilhados =====
    private static readonly List<Bomb> bombSnapshot = new(32);
    private static int bombSnapshotFrame = -1;

    private readonly List<Vector2> enemyTiles = new(16);
    private readonly List<Vector2> enemyWorldPositions = new(16);
    private readonly List<Vector2> endStageTiles = new(4);

    private static readonly Vector2[] Dirs =
    {
        Vector2.up, Vector2.down, Vector2.left, Vector2.right
    };

    private static readonly PlayerAction[] MoveActions =
    {
        PlayerAction.MoveUp, PlayerAction.MoveDown,
        PlayerAction.MoveLeft, PlayerAction.MoveRight
    };

    // ========================================================================
    //  Setup
    // ========================================================================

    /// <summary>Chamado pelo NormalGameAIManager ao anexar a IA a um player.</summary>
    public void Configure(int id)
    {
        playerId = Mathf.Clamp(id, 1, 6);
        startupPlantBlockUntilTime = Time.time + 0.15f;
        CacheReferences();
    }

    private void Awake() => CacheReferences();

    private void CacheReferences()
    {
        if (movement == null) TryGetComponent(out movement);
        if (bomb == null) TryGetComponent(out bomb);
        if (stun == null) TryGetComponent(out stun);

        explosionMask = MaskOf("Explosion");
        bombMask = MaskOf("Bomb");
        enemyMask = MaskOf("Enemy");
        itemMask = MaskOf("Item");
        stageMask = LayerMask.GetMask("Stage");

        lastWorldPos = GetWorldPos();
    }

    private static int MaskOf(string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        return layer >= 0 ? (1 << layer) : 0;
    }

    private void OnDisable() => ClearSyntheticInputs();
    private void OnDestroy() => ClearSyntheticInputs();

    // ========================================================================
    //  Loop
    // ========================================================================

    private void Update()
    {
        if (GamePauseController.IsPaused)
        {
            Hold(Vector2.zero);
            return;
        }

        if (movement == null || movement.isDead || movement.InputLocked || movement.IsEndingStage || IsStunned())
        {
            if (debugLogs && !loggedDeadState && movement != null && movement.isDead)
            {
                Vector2 tile = RoundToTile(GetWorldPos());
                IReadOnlyList<Bomb> deathBombs = GetBombSnapshot();
                float tHit = TimeUntilBlast(tile, deathBombs, false, Vector2.zero, 0, 0f);
                string here = float.IsInfinity(tHit) ? "safe" : tHit.ToString("F2");
                Vector2 world = GetWorldPos();
                float hideDist = lastPlannedHideTile == Vector2.zero ? -1f : Vector2.Distance(world, lastPlannedHideTile);
                Debug.Log($"[NormalGameAI][P{playerId}] DEAD world:{world} tile:{tile} tHitHere:{here} " +
                          $"bombs:{deathBombs.Count} lastPlant:{lastPlantTile} plannedHide:{lastPlannedHideTile} " +
                          $"hideDist:{hideDist:F2} depth:{lastPlannedHideDepth} eta:{lastPlannedHideEta:F2} " +
                          $"lastEscape:{lastEscapeRejectReason}", this);
                loggedDeadState = true;
            }
            Hold(Vector2.zero);
            return;
        }

        Vector2 myWorld = GetWorldPos();
        Vector2 myTile = RoundToTile(myWorld);

        UpdateStuck(myWorld);

        IReadOnlyList<Bomb> bombs = GetBombSnapshot();
        ScanEnemies(myWorld);

        bool danger = IsDangerNow(myTile, bombs);
        float interval = danger ? thinkIntervalDanger : thinkIntervalSafe;

        thinkTimer -= Time.deltaTime;
        if (thinkTimer <= 0f)
        {
            Think(myTile, bombs, danger);
            thinkTimer = interval;
        }

        LogTick(myTile, danger, bombs);
        Hold(currentMove);
    }

    private void Think(Vector2 myTile, IReadOnlyList<Bomb> bombs, bool danger)
    {
        // 1) Em perigo: fugir é prioridade máxima.
        if (danger)
        {
            Vector2 hideOrigin = lastPlantTile != Vector2.zero ? lastPlantTile : myTile;
            if (TryFindBombHideStep(
                myTile, bombs,
                hasVirtual: false, virtualTile: Vector2.zero, virtualRadius: 0, virtualFuse: 0f,
                hideOrigin: hideOrigin, requireComfortableHide: false,
                out Vector2 hideStep, out _, out _))
            {
                currentMove = ApplyAntiStuck(myTile, hideStep, bombs);
                Log($"ESCAPE-HIDE tile:{myTile} -> {DescribeMove(currentMove)}");
                return;
            }

            currentMove = ApplyAntiStuck(myTile, EscapeStep(myTile, bombs), bombs);
            if (currentMove == Vector2.zero)
            {
                currentMove = BestSafestNeighbor(myTile, bombs);
                Log($"ESCAPE-FALLBACK tile:{myTile} -> {DescribeMove(currentMove)} (BFS nao achou saida segura)");
            }
            else
            {
                Log($"ESCAPE tile:{myTile} -> {DescribeMove(currentMove)}");
            }
            return;
        }

        // 2) Matar inimigo a distância segura (sem encostar).
        if (huntEnemies && enemyTiles.Count > 0 && TryPlantToHitEnemy(myTile, bombs))
        {
            Log("plant to hit enemy");
            return;
        }

        // 3) Coletar item alcançável.
        if (ShouldContinuePostPlantRetreat(bombs))
        {
            if (TryPostPlantRetreatStep(myTile, bombs, out Vector2 retreatDir))
            {
                currentMove = retreatDir;
                Log($"post-plant retreat -> {currentMove}");
            }
            else
            {
                currentMove = Vector2.zero;
                string state = IsComfortableBombHideTile(myTile, bombs, false, Vector2.zero, 0, 0f, lastPlantTile)
                    ? "hidden"
                    : "hold";
                float hideDist = lastPlannedHideTile == Vector2.zero ? -1f : Vector2.Distance(GetWorldPos(), lastPlannedHideTile);
                Log($"post-plant {state} world:{GetWorldPos()} tile:{myTile} plannedHide:{lastPlannedHideTile} hideDist:{hideDist:F2} pad:{plantHideBlastPaddingTiles}");
            }
            return;
        }

        if (collectItems && TryStepToward(myTile, bombs, IsItemTile, itemScanRadiusWorld, out Vector2 itemDir))
        {
            currentMove = ApplyAntiStuck(myTile, itemDir, bombs);
            Log($"go item -> {currentMove}");
            return;
        }

        // 4) Farm: bloco destrutível adjacente -> plantar com fuga garantida.
        if (farmDestructibles && HasAdjacentDestructible(myTile) && TryPlantWithEscape(myTile, bombs))
        {
            Log("plant farm");
            return;
        }

        // 5) Navegar até um tile de farm (adjacente a destrutível).
        if (farmDestructibles && TryStepToward(myTile, bombs, HasAdjacentDestructible, 999f, out Vector2 farmDir))
        {
            currentMove = ApplyAntiStuck(myTile, farmDir, bombs);
            Log($"go farm -> {currentMove}");
            return;
        }

        // 6) Com o mapa aberto, procurar posicao de ataque contra inimigos sem
        //    entrar no tile de contato deles.
        if (huntEnemies && enemyTiles.Count > 0 && TryStepTowardEnemyAttackTile(myTile, bombs, out Vector2 enemyDir))
        {
            currentMove = ApplyAntiStuck(myTile, enemyDir, bombs);
            Log($"go enemy trap -> {currentMove}");
            return;
        }

        // 7) Fase limpa: entrar no end stage somente depois de eliminar
        //    inimigos, blocos destrutiveis e itens ativos.
        if (AreEndStageEntryRequirementsMet() &&
            TryStepTowardEndStage(myTile, bombs, out Vector2 endStageDir, out bool centeringEndStage))
        {
            currentMove = centeringEndStage ? endStageDir : ApplyAntiStuck(myTile, endStageDir, bombs);
            Log(centeringEndStage
                ? $"center endstage -> {currentMove} world:{GetWorldPos()} tile:{myTile}"
                : $"go endstage -> {currentMove}");
            return;
        }

        // 8) Sem objetivo: vaguear apenas por tiles seguros (nunca de volta ao
        //    raio de uma bomba ativa). Pode ficar parado se for o mais seguro.
        currentMove = ApplyAntiStuck(myTile, SafeWanderStep(myTile, bombs), bombs);
        Log($"wander -> {currentMove}");
    }

    // ========================================================================
    //  Combate: plantar para matar inimigo sem encostar
    // ========================================================================

    private bool TryPlantToHitEnemy(Vector2 myTile, IReadOnlyList<Bomb> bombs)
    {
        if (!CanPlantNow(myTile, out string reason))
        {
            LogPlantDecision($"plant enemy blocked tile:{myTile} reason:{reason}");
            return false;
        }

        int radius = EffectiveBombRadius();
        bool useful = false;

        for (int i = 0; i < enemyTiles.Count; i++)
        {
            Vector2 enemyTile = enemyTiles[i];
            int dTiles = ManhattanTiles(myTile, enemyTile);

            // Distância mínima de 2 para não encostar no inimigo ao plantar.
            if (dTiles < minEnemyTilesToPlant || dTiles > radius)
                continue;

            if (IsTileInBlastLine(myTile, enemyTile, radius))
            {
                useful = true;
                break;
            }
        }

        if (!useful)
            return false;

        // Só planta se houver fuga segura considerando a própria bomba.
        if (!HasSafeEscapeIfPlantAt(myTile, bombs, out Vector2 firstStep))
        {
            LogPlantDecision($"plant enemy blocked tile:{myTile} reason:{lastEscapeRejectReason} radius:{EffectiveBombRadius()} steps:{CountImmediateEscapeSteps(myTile, bombs)} bombs:{bombs.Count}");
            return false;
        }

        if (!PlantBomb(myTile))
            return false;

        RefreshBombSnapshot();
        currentMove = firstStep;
        Log($"plant enemy escape -> {DescribeMove(firstStep)} hide:{lastPlannedHideTile} depth:{lastPlannedHideDepth} eta:{lastPlannedHideEta:F2}");
        return true;
    }

    private bool TryStepTowardEnemyAttackTile(Vector2 myTile, IReadOnlyList<Bomb> bombs, out Vector2 firstStep)
    {
        firstStep = Vector2.zero;
        if (bomb == null || bomb.BombsRemaining <= 0)
            return false;

        float stepTime = SingleStepTravelTime();

        var visited = new HashSet<Vector2>();
        var queue = new Queue<EscapeNode>();
        visited.Add(myTile);

        for (int i = 0; i < Dirs.Length; i++)
        {
            Vector2 n = myTile + Dirs[i] * TileSize;
            if (!CanTraverseTowardEnemyAttack(n, bombs, stepTime))
                continue;

            visited.Add(n);
            queue.Enqueue(new EscapeNode(n, 1, Dirs[i]));
        }

        while (queue.Count > 0)
        {
            EscapeNode cur = queue.Dequeue();

            if (IsEnemyAttackTile(cur.pos, bombs))
            {
                firstStep = cur.firstStep;
                return firstStep != Vector2.zero;
            }

            if (cur.depth >= navLookaheadDepth)
                continue;

            for (int i = 0; i < Dirs.Length; i++)
            {
                Vector2 nx = cur.pos + Dirs[i] * TileSize;
                if (visited.Contains(nx))
                    continue;

                float eta = (cur.depth + 1) * stepTime;
                if (!CanTraverseTowardEnemyAttack(nx, bombs, eta))
                    continue;

                visited.Add(nx);
                queue.Enqueue(new EscapeNode(nx, cur.depth + 1, cur.firstStep));
            }
        }

        return false;
    }

    private bool CanTraverseTowardEnemyAttack(Vector2 tile, IReadOnlyList<Bomb> bombs, float eta)
    {
        if (!IsWalkable(tile) || IsEnemyHazard(tile))
            return false;
        if (IsEnemyWithinTiles(tile, minEnemyTilesToPlant - 1))
            return false;
        if (BlastReachesBefore(tile, bombs, eta, false, Vector2.zero, 0, 0f))
            return false;

        return true;
    }

    private bool IsEnemyAttackTile(Vector2 tile, IReadOnlyList<Bomb> bombs)
    {
        if (!CanBombHitEnemyFrom(tile, EffectiveBombRadius()))
            return false;
        if (IsOnActiveBombLine(tile, bombs))
            return false;

        return HasSafeEscapeIfPlantAt(tile, bombs, out _);
    }

    private bool CanBombHitEnemyFrom(Vector2 tile, int radius)
    {
        if (IsEnemyWithinTiles(tile, minEnemyTilesToPlant - 1))
            return false;

        for (int i = 0; i < enemyTiles.Count; i++)
        {
            Vector2 enemyTile = enemyTiles[i];
            int dTiles = ManhattanTiles(tile, enemyTile);
            if (dTiles < minEnemyTilesToPlant || dTiles > radius)
                continue;
            if (IsTileInBlastLine(tile, enemyTile, radius))
                return true;
        }

        return false;
    }

    // ========================================================================
    //  Farm: plantar com fuga garantida
    // ========================================================================

    private bool TryPlantWithEscape(Vector2 myTile, IReadOnlyList<Bomb> bombs)
    {
        if (!CanPlantNow(myTile, out string reason))
        {
            LogPlantDecision($"plant farm blocked tile:{myTile} reason:{reason}");
            return false;
        }

        if (!HasSafeEscapeIfPlantAt(myTile, bombs, out Vector2 firstStep))
        {
            LogPlantDecision($"plant farm blocked tile:{myTile} reason:{lastEscapeRejectReason} radius:{EffectiveBombRadius()} steps:{CountImmediateEscapeSteps(myTile, bombs)} bombs:{bombs.Count}");
            return false;
        }

        if (!PlantBomb(myTile))
            return false;

        RefreshBombSnapshot();
        currentMove = firstStep;
        Log($"plant farm escape -> {DescribeMove(firstStep)} hide:{lastPlannedHideTile} depth:{lastPlannedHideDepth} eta:{lastPlannedHideEta:F2}");
        return true;
    }

    private bool CanPlantNow(Vector2 myTile, out string reason)
    {
        reason = "ok";
        if (bomb == null)
        {
            reason = "missing_bomb_controller";
            return false;
        }
        if (bomb.BombsRemaining <= 0)
        {
            reason = "no_bombs_left";
            return false;
        }
        if (Time.time < startupPlantBlockUntilTime)
        {
            reason = $"startup_wait:{startupPlantBlockUntilTime - Time.time:F2}";
            return false;
        }
        if (Time.time - lastBombTime < bombCooldown)
        {
            reason = $"cooldown:{bombCooldown - (Time.time - lastBombTime):F2}";
            return false;
        }
        if (IsTileWithBomb(myTile))
        {
            reason = "tile_has_bomb";
            return false;
        }
        if (IsEnemyWithinTiles(myTile, minEnemyTilesToPlant - 1))
        {
            reason = "enemy_too_close";
            return false;
        }
        return true;
    }

    private void LogPlantDecision(string message)
    {
        if (!debugLogs)
            return;
        if (Time.time - lastPlantDecisionLogTime < 0.5f)
            return;

        lastPlantDecisionLogTime = Time.time;
        Log(message);
    }

    private bool PlantBomb(Vector2 myTile)
    {
        if (!bomb.TryPlaceBombAtIgnoringInputLock(myTile))
        {
            Log($"PLANT-FAIL tile:{myTile} (TryPlaceBomb recusou)");
            return false;
        }

        lastBombTime = Time.time;
        float fuse = EstimatedNextBombFuseSeconds();
        retreatUntilTime = Time.time + Mathf.Max(fuse, 2f) + 0.35f;
        lastPlantTile = myTile;
        Log($"PLANT tile:{myTile} radius:{EffectiveBombRadius()} fuse:{fuse:F1} bombsLeft:{bomb.BombsRemaining}");
        return true;
    }

    /// <summary>
    /// Roda um BFS de fuga assumindo que existe uma bomba virtual em
    /// <paramref name="bombTile"/> (raio = raio efetivo da IA). Retorna o
    /// primeiro passo para um tile seguro, ou falso se não houver saída.
    /// </summary>
    private bool HasSafeEscapeIfPlantAt(Vector2 bombTile, IReadOnlyList<Bomb> bombs, out Vector2 firstStep)
    {
        firstStep = Vector2.zero;
        lastEscapeRejectReason = "no_escape";
        lastPlannedHideTile = Vector2.zero;
        lastPlannedHideDepth = 0;
        lastPlannedHideEta = 0f;

        int radius = EffectiveBombRadius();
        float fuse = EstimatedNextBombFuseSeconds();
        if (!TryFindBombHideStep(
            bombTile, bombs,
            hasVirtual: true, virtualTile: bombTile, virtualRadius: radius, virtualFuse: fuse,
            hideOrigin: bombTile, requireComfortableHide: false,
            out firstStep, out Vector2 hideTile, out int hideDepth))
        {
            lastEscapeRejectReason = $"no_bfs_escape maxDepth:{Mathf.Max(escapeLookaheadDepth, radius + 4)}";
            return false;
        }

        float eta = hideDepth * SingleStepTravelTime();
        lastPlannedHideTile = hideTile;
        lastPlannedHideDepth = hideDepth;
        lastPlannedHideEta = eta;

        if (hideDepth > maxPlantEscapeDepth)
        {
            lastEscapeRejectReason = $"escape_too_long depth:{hideDepth} max:{maxPlantEscapeDepth} hide:{hideTile}";
            return false;
        }

        if (eta + plantEscapeFuseMargin > fuse)
        {
            lastEscapeRejectReason = $"escape_margin_low eta:{eta:F2} fuse:{fuse:F2} hide:{hideTile}";
            return false;
        }

        lastEscapeRejectReason = "ok";
        return firstStep != Vector2.zero;
    }

    private int CountImmediateEscapeSteps(Vector2 bombTile, IReadOnlyList<Bomb> bombs)
    {
        int count = 0;
        float stepTime = SingleStepTravelTime();
        int radius = EffectiveBombRadius();
        float fuse = EstimatedNextBombFuseSeconds();

        for (int i = 0; i < Dirs.Length; i++)
        {
            Vector2 n = bombTile + Dirs[i] * TileSize;
            if (CanTraverseEscapeTile(n, bombs, stepTime, true, bombTile, radius, fuse))
                count++;
        }

        return count;
    }

    // ========================================================================
    //  Fuga (BFS)
    // ========================================================================

    private Vector2 EscapeStep(Vector2 myTile, IReadOnlyList<Bomb> bombs)
        => EscapeStep(myTile, bombs, false, Vector2.zero, 0, 0f);

    private Vector2 EscapeStep(
        Vector2 myTile, IReadOnlyList<Bomb> bombs,
        bool hasVirtual, Vector2 virtualTile, int virtualRadius, float virtualFuse,
        bool requireBlastFreeDestination = false)
    {
        float stepTime = SingleStepTravelTime();

        var visited = new HashSet<Vector2>();
        var queue = new Queue<EscapeNode>();
        visited.Add(myTile);
        Vector2 bestFirstStep = Vector2.zero;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < Dirs.Length; i++)
        {
            Vector2 n = myTile + Dirs[i] * TileSize;
            if (!IsWalkable(n) || IsEnemyHazard(n))
                continue;

            // Só atravessa um tile se a explosão não chegar antes de eu passar.
            if (BlastReachesBefore(n, bombs, stepTime, hasVirtual, virtualTile, virtualRadius, virtualFuse))
                continue;

            visited.Add(n);
            queue.Enqueue(new EscapeNode(n, 1, Dirs[i]));
        }

        while (queue.Count > 0)
        {
            EscapeNode cur = queue.Dequeue();

            // Destino de fuga VÁLIDO somente se o tile está FORA de qualquer
            // linha de explosão (tHit infinito). Um tile dentro do raio, por
            // mais que a explosão demore, é um beco em potencial e NÃO conta
            // como saída — só serve de passagem. Isso impede a IA de "fugir"
            // para dentro do próprio raio e morrer.
            float tHit = TimeUntilBlast(cur.pos, bombs, hasVirtual, virtualTile, virtualRadius, virtualFuse);
            float eta = cur.depth * stepTime;
            if (float.IsInfinity(tHit) && !IsEnemyHazard(cur.pos))
                return cur.firstStep;

            if (!requireBlastFreeDestination)
            {
                float score = ScoreEscapeCandidate(cur.pos, cur.depth, eta, tHit);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestFirstStep = cur.firstStep;
                }
            }

            if (cur.depth >= escapeLookaheadDepth)
                continue;

            for (int i = 0; i < Dirs.Length; i++)
            {
                Vector2 nx = cur.pos + Dirs[i] * TileSize;
                if (visited.Contains(nx) || !IsWalkable(nx) || IsEnemyHazard(nx))
                    continue;

                float nEta = (cur.depth + 1) * stepTime;
                if (BlastReachesBefore(nx, bombs, nEta, hasVirtual, virtualTile, virtualRadius, virtualFuse))
                    continue;

                visited.Add(nx);
                queue.Enqueue(new EscapeNode(nx, cur.depth + 1, cur.firstStep));
            }
        }

        // Nenhum tile fora do raio é alcançável a tempo.
        return requireBlastFreeDestination ? Vector2.zero : bestFirstStep;
    }

    // A explosão (real ou a bomba virtual de planejamento) atinge 'tile' antes
    // de eu conseguir passar por ele (chegada estimada em 'eta')?
    private bool BlastReachesBefore(
        Vector2 tile, IReadOnlyList<Bomb> bombs, float eta,
        bool hasVirtual, Vector2 virtualTile, int virtualRadius, float virtualFuse)
    {
        if (IsTileWithExplosion(tile))
            return true;

        float tHit = TimeUntilBlast(tile, bombs, hasVirtual, virtualTile, virtualRadius, virtualFuse);
        return tHit <= eta + safeTileMinTime;
    }

    private Vector2 BestSafestNeighbor(Vector2 myTile, IReadOnlyList<Bomb> bombs)
    {
        float stepTime = SingleStepTravelTime();
        Vector2 best = Vector2.zero;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < Dirs.Length; i++)
        {
            Vector2 n = myTile + Dirs[i] * TileSize;
            if (!IsWalkable(n) || IsEnemyHazard(n))
                continue;

            float tHit = TimeUntilBlast(n, bombs, false, Vector2.zero, 0, 0f);
            float score = ScoreEscapeCandidate(n, 1, stepTime, tHit);
            if (score > bestScore)
            {
                bestScore = score;
                best = Dirs[i];
            }
        }

        return best;
    }

    private float ScoreEscapeCandidate(Vector2 tile, int depth, float eta, float tHit)
    {
        float blastScore = float.IsInfinity(tHit)
            ? 10000f
            : Mathf.Max(-1000f, (tHit - eta - safeTileMinTime) * 100f);

        float enemyScore = Mathf.Min(6f, DistanceToClosestEnemyTiles(tile)) * 25f;
        float openScore = CountWalkableNonEnemyNeighbors(tile) * 8f;

        return blastScore + enemyScore + openScore - depth;
    }

    // Passo de "vaguear" usado quando NÃO há perigo: anda apenas para tiles que
    // estão fora de qualquer linha de bomba ativa. Se nenhum vizinho é seguro,
    // FICA PARADO (Vector2.zero) — melhor esperar a bomba explodir do que entrar
    // de volta no raio. Isso elimina a oscilação na borda do raio.
    private bool ShouldContinuePostPlantRetreat(IReadOnlyList<Bomb> bombs)
    {
        if (Time.time > retreatUntilTime)
            return false;

        for (int i = 0; i < bombs.Count; i++)
        {
            Bomb b = bombs[i];
            if (b == null || b.HasExploded)
                continue;

            if (b.Owner == bomb)
                return true;
        }

        return false;
    }

    private bool TryPostPlantRetreatStep(Vector2 myTile, IReadOnlyList<Bomb> bombs, out Vector2 step)
    {
        step = Vector2.zero;

        if (lastPlannedHideTile != Vector2.zero &&
            myTile == lastPlannedHideTile &&
            TryGetCenteringStep(lastPlannedHideTile, out step))
        {
            return step != Vector2.zero;
        }

        if (IsComfortableBombHideTile(myTile, bombs, false, Vector2.zero, 0, 0f, lastPlantTile))
            return false;

        if (TryFindBombHideStep(
            myTile, bombs,
            hasVirtual: false, virtualTile: Vector2.zero, virtualRadius: 0, virtualFuse: 0f,
            hideOrigin: lastPlantTile, requireComfortableHide: true,
            out step, out _, out _))
        {
            return step != Vector2.zero;
        }

        if (IsOnActiveBombLine(myTile, bombs))
        {
            step = EscapeStep(myTile, bombs);
            if (step == Vector2.zero)
                step = BestSafestNeighbor(myTile, bombs);
            return step != Vector2.zero;
        }

        float currentDistance = ManhattanTiles(myTile, lastPlantTile);
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < Dirs.Length; i++)
        {
            Vector2 n = myTile + Dirs[i] * TileSize;
            if (!IsWalkable(n) || IsEnemyHazard(n) || IsOnActiveBombLine(n, bombs))
                continue;

            float distance = ManhattanTiles(n, lastPlantTile);
            if (distance < currentDistance)
                continue;

            float score =
                distance * 30f +
                CountWalkableNonEnemyNeighbors(n) * 8f +
                Mathf.Min(6f, DistanceToClosestEnemyTiles(n)) * 5f;

            if (score <= bestScore)
                continue;

            bestScore = score;
            step = Dirs[i];
        }

        return step != Vector2.zero;
    }

    private bool TryFindBombHideStep(
        Vector2 myTile, IReadOnlyList<Bomb> bombs,
        bool hasVirtual, Vector2 virtualTile, int virtualRadius, float virtualFuse,
        Vector2 hideOrigin, bool requireComfortableHide,
        out Vector2 firstStep, out Vector2 hideTile, out int hideDepth)
    {
        firstStep = Vector2.zero;
        hideTile = Vector2.zero;
        hideDepth = 0;
        float stepTime = SingleStepTravelTime();
        int maxDepth = Mathf.Max(escapeLookaheadDepth, Mathf.Max(virtualRadius + 4, EffectiveBombRadius() + 4));

        var visited = new HashSet<Vector2>();
        var queue = new Queue<EscapeNode>();
        visited.Add(myTile);

        Vector2 bestStep = Vector2.zero;
        Vector2 bestHideTile = Vector2.zero;
        int bestHideDepth = 0;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < Dirs.Length; i++)
        {
            Vector2 n = myTile + Dirs[i] * TileSize;
            if (!CanTraverseEscapeTile(n, bombs, stepTime, hasVirtual, virtualTile, virtualRadius, virtualFuse))
                continue;

            visited.Add(n);
            queue.Enqueue(new EscapeNode(n, 1, Dirs[i]));
        }

        while (queue.Count > 0)
        {
            EscapeNode cur = queue.Dequeue();
            float tHit = TimeUntilBlast(cur.pos, bombs, hasVirtual, virtualTile, virtualRadius, virtualFuse);
            bool outsideBlast = float.IsInfinity(tHit);
            bool comfortable = outsideBlast &&
                IsComfortableBombHideTile(cur.pos, bombs, hasVirtual, virtualTile, virtualRadius, virtualFuse, hideOrigin);

            if (outsideBlast && !requireComfortableHide)
            {
                firstStep = cur.firstStep;
                hideTile = cur.pos;
                hideDepth = cur.depth;
                return firstStep != Vector2.zero;
            }

            if (comfortable)
            {
                float score = ScoreBombHideCandidate(cur.pos, cur.depth, hideOrigin, bombs,
                    hasVirtual, virtualTile, virtualRadius, virtualFuse);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestStep = cur.firstStep;
                    bestHideTile = cur.pos;
                    bestHideDepth = cur.depth;
                }
            }

            if (cur.depth >= maxDepth)
                continue;

            for (int i = 0; i < Dirs.Length; i++)
            {
                Vector2 nx = cur.pos + Dirs[i] * TileSize;
                if (visited.Contains(nx))
                    continue;

                float nEta = (cur.depth + 1) * stepTime;
                if (!CanTraverseEscapeTile(nx, bombs, nEta, hasVirtual, virtualTile, virtualRadius, virtualFuse))
                    continue;

                visited.Add(nx);
                queue.Enqueue(new EscapeNode(nx, cur.depth + 1, cur.firstStep));
            }
        }

        firstStep = bestStep;
        hideTile = bestHideTile;
        hideDepth = bestHideDepth;
        return firstStep != Vector2.zero;
    }

    private bool TryGetCenteringStep(Vector2 targetTile, out Vector2 step)
    {
        return TryGetCenteringStep(targetTile, hideCenterToleranceWorld, out step);
    }

    private bool TryGetCenteringStep(Vector2 targetTile, float toleranceWorld, out Vector2 step)
    {
        step = Vector2.zero;

        Vector2 delta = targetTile - GetWorldPos();
        float tolerance = Mathf.Max(0.01f, toleranceWorld);
        if (delta.sqrMagnitude <= tolerance * tolerance)
            return false;

        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            step = delta.x > 0f ? Vector2.right : Vector2.left;
        else
            step = delta.y > 0f ? Vector2.up : Vector2.down;

        return step != Vector2.zero;
    }

    private bool CanTraverseEscapeTile(
        Vector2 tile, IReadOnlyList<Bomb> bombs, float eta,
        bool hasVirtual, Vector2 virtualTile, int virtualRadius, float virtualFuse)
    {
        if (!IsWalkable(tile) || IsEnemyHazard(tile))
            return false;

        return !BlastReachesBefore(tile, bombs, eta, hasVirtual, virtualTile, virtualRadius, virtualFuse);
    }

    private bool IsComfortableBombHideTile(
        Vector2 tile, IReadOnlyList<Bomb> bombs,
        bool hasVirtual, Vector2 virtualTile, int virtualRadius, float virtualFuse,
        Vector2 hideOrigin)
    {
        if (!IsWalkable(tile) || IsEnemyHazard(tile))
            return false;
        if (!float.IsInfinity(TimeUntilBlast(tile, bombs, hasVirtual, virtualTile, virtualRadius, virtualFuse)))
            return false;
        if (IsTooCloseToAnyBlastLine(tile, bombs, hasVirtual, virtualTile, virtualRadius))
            return false;

        int distanceFromPlant = ManhattanTiles(tile, hideOrigin);
        int radius = virtualRadius > 0 ? virtualRadius : EffectiveBombRadius();
        int neededDistance = Mathf.Max(2, Mathf.Min(EffectiveBombRadius(), radius) + Mathf.Max(0, plantHideBlastPaddingTiles));
        if (distanceFromPlant < neededDistance)
            return false;

        int safeNeighbors = CountSafeLingerNeighbors(tile, bombs, hasVirtual, virtualTile, virtualRadius, virtualFuse);
        return safeNeighbors > 0 || distanceFromPlant >= neededDistance + 2;
    }

    private float ScoreBombHideCandidate(
        Vector2 tile, int depth, Vector2 hideOrigin, IReadOnlyList<Bomb> bombs,
        bool hasVirtual, Vector2 virtualTile, int virtualRadius, float virtualFuse)
    {
        float distanceScore = Mathf.Min(8, ManhattanTiles(tile, hideOrigin)) * 35f;
        float openScore = CountSafeLingerNeighbors(tile, bombs, hasVirtual, virtualTile, virtualRadius, virtualFuse) * 20f;
        float enemyScore = Mathf.Min(6f, DistanceToClosestEnemyTiles(tile)) * 20f;
        return distanceScore + openScore + enemyScore - depth * 4f;
    }

    private int CountSafeLingerNeighbors(
        Vector2 tile, IReadOnlyList<Bomb> bombs,
        bool hasVirtual, Vector2 virtualTile, int virtualRadius, float virtualFuse)
    {
        int count = 0;
        for (int i = 0; i < Dirs.Length; i++)
        {
            Vector2 n = tile + Dirs[i] * TileSize;
            if (!IsWalkable(n) || IsEnemyHazard(n))
                continue;
            if (!float.IsInfinity(TimeUntilBlast(n, bombs, hasVirtual, virtualTile, virtualRadius, virtualFuse)))
                continue;
            if (IsTooCloseToAnyBlastLine(n, bombs, hasVirtual, virtualTile, virtualRadius))
                continue;

            count++;
        }

        return count;
    }

    private bool IsTooCloseToAnyBlastLine(
        Vector2 tile, IReadOnlyList<Bomb> bombs,
        bool hasVirtual, Vector2 virtualTile, int virtualRadius)
    {
        int padding = Mathf.Max(0, plantHideBlastPaddingTiles);
        if (padding <= 0)
            return false;

        for (int i = 0; i < bombs.Count; i++)
        {
            Bomb b = bombs[i];
            if (b == null || b.HasExploded)
                continue;

            Vector2 bombTile = RoundToTile(b.GetLogicalPosition());
            if (IsWithinBlastPadding(tile, bombTile, BombRadius(b), padding))
                return true;
        }

        return hasVirtual && IsWithinBlastPadding(tile, virtualTile, virtualRadius, padding);
    }

    private bool IsWithinBlastPadding(Vector2 tile, Vector2 bombTile, int radiusTiles, int paddingTiles)
    {
        if (radiusTiles <= 0 || paddingTiles <= 0)
            return false;

        float tileSize = TileSize;
        for (int x = -paddingTiles; x <= paddingTiles; x++)
        {
            for (int y = -paddingTiles; y <= paddingTiles; y++)
            {
                if (x == 0 && y == 0)
                    continue;

                Vector2 paddedTile = tile + new Vector2(x * tileSize, y * tileSize);
                if (IsTileInBlastLine(bombTile, paddedTile, radiusTiles))
                    return true;
            }
        }

        return false;
    }

    private Vector2 SafeWanderStep(Vector2 myTile, IReadOnlyList<Bomb> bombs)
    {
        for (int i = 0; i < Dirs.Length; i++)
        {
            Vector2 n = myTile + Dirs[i] * TileSize;
            if (!IsWalkable(n) || IsEnemyHazard(n))
                continue;
            if (IsOnActiveBombLine(n, bombs))
                continue;

            return Dirs[i];
        }

        return Vector2.zero;
    }

    // ========================================================================
    //  Navegação até objetivos (BFS de menor caminho seguro)
    // ========================================================================

    private delegate bool TilePredicate(Vector2 tile);

    /// <summary>
    /// BFS por tiles caminháveis e seguros até o tile mais próximo que
    /// satisfaz <paramref name="goal"/>. Retorna o primeiro passo.
    /// </summary>
    private bool TryStepToward(Vector2 myTile, IReadOnlyList<Bomb> bombs, TilePredicate goal, float maxWorldRadius, out Vector2 firstStep)
    {
        firstStep = Vector2.zero;
        float stepTime = SingleStepTravelTime();
        float maxRadiusSqr = maxWorldRadius * maxWorldRadius;

        var visited = new HashSet<Vector2>();
        var queue = new Queue<EscapeNode>();
        visited.Add(myTile);

        for (int i = 0; i < Dirs.Length; i++)
        {
            Vector2 n = myTile + Dirs[i] * TileSize;
            if (!IsWalkable(n) || IsEnemyHazard(n))
                continue;
            // Nunca navegar/farmar para dentro do raio de uma bomba ativa.
            if (IsOnActiveBombLine(n, bombs))
                continue;

            visited.Add(n);
            queue.Enqueue(new EscapeNode(n, 1, Dirs[i]));
        }

        while (queue.Count > 0)
        {
            EscapeNode cur = queue.Dequeue();

            if (goal(cur.pos))
            {
                firstStep = cur.firstStep;
                return firstStep != Vector2.zero;
            }

            if (cur.depth >= navLookaheadDepth)
                continue;

            for (int i = 0; i < Dirs.Length; i++)
            {
                Vector2 nx = cur.pos + Dirs[i] * TileSize;
                if (visited.Contains(nx) || !IsWalkable(nx) || IsEnemyHazard(nx))
                    continue;
                if ((nx - myTile).sqrMagnitude > maxRadiusSqr)
                    continue;
                if (IsOnActiveBombLine(nx, bombs))
                    continue;

                visited.Add(nx);
                queue.Enqueue(new EscapeNode(nx, cur.depth + 1, cur.firstStep));
            }
        }

        return false;
    }

    // ========================================================================
    //  Perigo / segurança de tiles
    // ========================================================================

    private bool TryStepTowardEndStage(
        Vector2 myTile, IReadOnlyList<Bomb> bombs, out Vector2 firstStep, out bool centeringOnTile)
    {
        firstStep = Vector2.zero;
        centeringOnTile = false;
        RefreshEndStageTilesIfNeeded();

        if (endStageTiles.Count <= 0)
            return false;

        for (int i = 0; i < endStageTiles.Count; i++)
        {
            if (myTile == endStageTiles[i])
            {
                if (TryGetCenteringStep(endStageTiles[i], endStageCenterToleranceWorld, out firstStep))
                {
                    centeringOnTile = true;
                    return true;
                }

                return true;
            }
        }

        return TryStepToward(myTile, bombs, IsEndStageTile, endStageScanRadiusWorld, out firstStep);
    }

    private bool AreEndStageEntryRequirementsMet()
    {
        if (Time.time - lastEndStageRequirementCheckTime < 0.25f)
            return cachedEndStageRequirementsMet;

        lastEndStageRequirementCheckTime = Time.time;

        GameManager gm = GameManager.Instance != null
            ? GameManager.Instance
            : FindAnyObjectByType<GameManager>();

        bool enemiesCleared = gm != null
            ? gm.AreAllEnemiesCleared()
            : FindObjectsByType<EnemyMovementController>(FindObjectsInactive.Exclude).Length == 0;

        bool destructiblesCleared = gm == null || !gm.HasDestructiblesInStage();
        bool itemsCollected = FindObjectsByType<ItemPickup>(FindObjectsInactive.Exclude).Length == 0;

        cachedEndStageRequirementsMet = enemiesCleared && destructiblesCleared && itemsCollected;
        return cachedEndStageRequirementsMet;
    }

    private bool IsEndStageTile(Vector2 tile)
    {
        RefreshEndStageTilesIfNeeded();

        for (int i = 0; i < endStageTiles.Count; i++)
        {
            if (tile == endStageTiles[i])
                return true;
        }

        return false;
    }

    private bool IsEndStageTileBlocked(Vector2 tile)
    {
        return IsEndStageTile(tile) && !AreEndStageEntryRequirementsMet();
    }

    private void RefreshEndStageTilesIfNeeded()
    {
        if (Time.time - lastEndStageTileScanTime < 0.5f)
            return;

        lastEndStageTileScanTime = Time.time;
        endStageTiles.Clear();

        EndStage[] endStages = FindObjectsByType<EndStage>(FindObjectsInactive.Include);
        for (int i = 0; i < endStages.Length; i++)
        {
            EndStage endStage = endStages[i];
            if (endStage == null || !endStage.gameObject.scene.IsValid())
                continue;

            if (TryGetEndStageCenter(endStage, out Vector2 center))
                AddEndStageTile(RoundToTile(center));
        }
    }

    private void AddEndStageTile(Vector2 tile)
    {
        for (int i = 0; i < endStageTiles.Count; i++)
        {
            if (endStageTiles[i] == tile)
                return;
        }

        endStageTiles.Add(tile);
    }

    private bool TryGetEndStageCenter(EndStage endStage, out Vector2 center)
    {
        Collider2D[] colliders = endStage.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D c = colliders[i];
            if (c == null || !c.isTrigger)
                continue;

            center = ColliderCenter(c);
            return true;
        }

        if (colliders.Length > 0 && colliders[0] != null)
        {
            center = ColliderCenter(colliders[0]);
            return true;
        }

        center = endStage.transform.position;
        return true;
    }

    private static Vector2 ColliderCenter(Collider2D c)
    {
        if (c is BoxCollider2D box)
            return box.transform.TransformPoint(box.offset);
        if (c is CircleCollider2D circle)
            return circle.transform.TransformPoint(circle.offset);
        if (c is CapsuleCollider2D capsule)
            return capsule.transform.TransformPoint(capsule.offset);

        Vector2 boundsCenter = c.bounds.center;
        if (boundsCenter != Vector2.zero)
            return boundsCenter;

        return c.transform.position;
    }

    private bool IsDangerNow(Vector2 myTile, IReadOnlyList<Bomb> bombs)
    {
        if (IsEnemyHazard(myTile))
            return true;

        // Qualquer tile que será atingido por uma bomba ativa é perigo: a IA
        // precisa SAIR do raio assim que planta e continuar fugindo até estar
        // fora da linha de explosão.
        return IsOnActiveBombLine(myTile, bombs);
    }

    // Tile que já tem explosão ou que será atingido por uma bomba ATIVA (em
    // qualquer tempo até explodir). Usado para impedir a IA de NAVEGAR/vaguear
    // para dentro do raio de uma bomba — só a fuga (EscapeStep) atravessa essas
    // zonas, e mesmo assim apenas de passagem.
    private bool IsOnActiveBombLine(Vector2 tile, IReadOnlyList<Bomb> bombs)
    {
        if (IsTileWithExplosion(tile))
            return true;

        return !float.IsInfinity(TimeUntilBlast(tile, bombs, false, Vector2.zero, 0, 0f));
    }

    private float TimeUntilBlast(
        Vector2 tile, IReadOnlyList<Bomb> bombs,
        bool hasVirtual, Vector2 virtualTile, int virtualRadius, float virtualFuse)
    {
        float best = float.PositiveInfinity;

        for (int i = 0; i < bombs.Count; i++)
        {
            Bomb b = bombs[i];
            if (b == null || b.HasExploded)
                continue;

            int radius = BombRadius(b);
            Vector2 bombTile = RoundToTile(b.GetLogicalPosition());

            if (!IsTileInBlastLine(bombTile, tile, radius))
                continue;

            float remaining = BombFuseRemaining(b);
            if (remaining < best)
                best = remaining;
        }

        if (hasVirtual && IsTileInBlastLine(virtualTile, tile, virtualRadius))
        {
            if (virtualFuse < best)
                best = virtualFuse;
        }

        return best;
    }

    /// <summary>Linha de explosão em cruz, bloqueada por tiles sólidos/destrutíveis.</summary>
    private bool IsTileInBlastLine(Vector2 bombTile, Vector2 tile, int radiusTiles)
    {
        Vector2 delta = tile - bombTile;
        bool sameRow = Mathf.Abs(delta.y) < 0.001f;
        bool sameCol = Mathf.Abs(delta.x) < 0.001f;

        if (!sameRow && !sameCol)
            return false;

        float distWorld = sameRow ? Mathf.Abs(delta.x) : Mathf.Abs(delta.y);
        int steps = Mathf.RoundToInt(distWorld / TileSize);

        if (steps == 0)
            return true; // mesmo tile da bomba
        if (steps > radiusTiles)
            return false;

        Vector2 dir = sameRow
            ? new Vector2(Mathf.Sign(delta.x), 0f)
            : new Vector2(0f, Mathf.Sign(delta.y));

        Vector2 cur = bombTile;
        for (int i = 0; i < steps; i++)
        {
            cur += dir * TileSize;
            if (cur == tile)
                return true;
            if (IsBlastBlockedAt(cur))
                return false;
        }

        return true;
    }

    private bool IsBlastBlockedAt(Vector2 tile)
    {
        float size = TileSize * 0.55f;
        Collider2D[] hits = Physics2D.OverlapBoxAll(tile, Vector2.one * size, 0f, stageMask);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D h = hits[i];
            if (h == null || h.isTrigger)
                continue;
            if (h.CompareTag("Destructibles") || h.CompareTag("Indestructibles"))
                return true;
        }

        if (bombMask != 0 && Physics2D.OverlapBox(tile, Vector2.one * size, 0f, bombMask) != null)
            return true;

        return false;
    }

    // ========================================================================
    //  Detecção de mundo
    // ========================================================================

    private void ScanEnemies(Vector2 myWorld)
    {
        enemyTiles.Clear();
        enemyWorldPositions.Clear();
        if (enemyMask == 0)
            return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(myWorld, enemyScanRadiusWorld, enemyMask);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D c = hits[i];
            if (c == null)
                continue;

            Vector2 enemyWorld = c.attachedRigidbody != null
                ? c.attachedRigidbody.position
                : (Vector2)c.transform.position;

            enemyWorldPositions.Add(enemyWorld);
            enemyTiles.Add(RoundToTile(enemyWorld));
        }
    }

    private bool IsEnemyHazard(Vector2 tile)
    {
        float contactRadius = Mathf.Max(0f, enemyTouchRadiusTiles * TileSize + enemyContactPaddingWorld);
        float contactRadiusSqr = contactRadius * contactRadius;

        for (int i = 0; i < enemyWorldPositions.Count; i++)
        {
            if ((tile - enemyWorldPositions[i]).sqrMagnitude <= contactRadiusSqr)
                return true;
        }

        for (int i = 0; i < enemyTiles.Count; i++)
        {
            if (ManhattanTiles(tile, enemyTiles[i]) <= enemyTouchRadiusTiles)
                return true;
        }
        return false;
    }

    private bool IsEnemyWithinTiles(Vector2 tile, int radiusTiles)
    {
        if (radiusTiles < 0)
            return false;

        for (int i = 0; i < enemyTiles.Count; i++)
        {
            if (ManhattanTiles(tile, enemyTiles[i]) <= radiusTiles)
                return true;
        }

        float radiusWorld = Mathf.Max(0f, radiusTiles * TileSize + enemyContactPaddingWorld);
        float radiusWorldSqr = radiusWorld * radiusWorld;
        for (int i = 0; i < enemyWorldPositions.Count; i++)
        {
            if ((tile - enemyWorldPositions[i]).sqrMagnitude <= radiusWorldSqr)
                return true;
        }

        return false;
    }

    private float DistanceToClosestEnemyTiles(Vector2 tile)
    {
        if (enemyTiles.Count == 0 && enemyWorldPositions.Count == 0)
            return 99f;

        float best = 99f;
        for (int i = 0; i < enemyTiles.Count; i++)
            best = Mathf.Min(best, ManhattanTiles(tile, enemyTiles[i]));

        float tileSize = TileSize;
        for (int i = 0; i < enemyWorldPositions.Count; i++)
            best = Mathf.Min(best, Vector2.Distance(tile, enemyWorldPositions[i]) / tileSize);

        return best;
    }

    private int CountWalkableNonEnemyNeighbors(Vector2 tile)
    {
        int count = 0;
        for (int i = 0; i < Dirs.Length; i++)
        {
            Vector2 n = tile + Dirs[i] * TileSize;
            if (IsWalkable(n) && !IsEnemyHazard(n))
                count++;
        }

        return count;
    }

    private bool IsItemTile(Vector2 tile)
    {
        if (itemMask == 0)
            return false;
        return Physics2D.OverlapBox(tile, Vector2.one * (TileSize * 0.5f), 0f, itemMask) != null;
    }

    private bool HasAdjacentDestructible(Vector2 tile)
    {
        float size = TileSize * 0.6f;
        for (int i = 0; i < Dirs.Length; i++)
        {
            Vector2 adj = tile + Dirs[i] * TileSize;
            Collider2D[] hits = Physics2D.OverlapBoxAll(adj, Vector2.one * size, 0f, stageMask);
            for (int h = 0; h < hits.Length; h++)
            {
                if (hits[h] != null && hits[h].CompareTag("Destructibles"))
                    return true;
            }
        }
        return false;
    }

    private bool IsWalkable(Vector2 tile)
    {
        if (IsTileWithExplosion(tile))
            return false;
        if (IsEndStageTileBlocked(tile))
            return false;

        float size = TileSize * 0.6f;
        Collider2D[] hits = Physics2D.OverlapBoxAll(tile, Vector2.one * size, 0f, movement.obstacleMask);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D h = hits[i];
            if (h == null || h.isTrigger)
                continue;
            if (h.gameObject == gameObject)
                continue;
            return false;
        }
        return true;
    }

    private bool IsTileWithExplosion(Vector2 tile)
    {
        if (explosionMask == 0)
            return false;
        return Physics2D.OverlapBox(tile, Vector2.one * (TileSize * 0.55f), 0f, explosionMask) != null;
    }

    private bool IsTileWithBomb(Vector2 tile)
    {
        if (bombMask == 0)
            return false;
        return Physics2D.OverlapBox(tile, Vector2.one * (TileSize * 0.55f), 0f, bombMask) != null;
    }

    // ========================================================================
    //  Input sintético
    // ========================================================================

    private void Hold(Vector2 move)
    {
        PlayerInputManager input = PlayerInputManager.Instance;
        if (input == null)
            return;

        PlayerAction held = PlayerAction.MoveUp;
        bool hasMove = false;

        if (Mathf.Abs(move.x) > Mathf.Abs(move.y))
        {
            if (move.x > 0f) { held = PlayerAction.MoveRight; hasMove = true; }
            else if (move.x < 0f) { held = PlayerAction.MoveLeft; hasMove = true; }
        }
        else if (Mathf.Abs(move.y) > 0f)
        {
            if (move.y > 0f) { held = PlayerAction.MoveUp; hasMove = true; }
            else { held = PlayerAction.MoveDown; hasMove = true; }
        }

        for (int i = 0; i < MoveActions.Length; i++)
            input.SetSyntheticHeld(playerId, MoveActions[i], hasMove && MoveActions[i] == held);
    }

    private void ClearSyntheticInputs()
    {
        currentMove = Vector2.zero;
        PlayerInputManager input = PlayerInputManager.Instance;
        if (input != null)
            input.ClearSyntheticPlayer(playerId);
    }

    // ========================================================================
    //  Anti-stuck
    // ========================================================================

    private void UpdateStuck(Vector2 myWorld)
    {
        if ((myWorld - lastWorldPos).sqrMagnitude > 0.0009f)
        {
            lastWorldPos = myWorld;
            stuckSince = -1f;
            stuckAvoidDir = Vector2.zero;
            return;
        }

        if (currentMove == Vector2.zero)
            return;

        if (stuckSince < 0f)
            stuckSince = Time.time;
    }

    private Vector2 ApplyAntiStuck(Vector2 myTile, Vector2 desired, IReadOnlyList<Bomb> bombs)
    {
        if (desired == Vector2.zero)
            return desired;

        bool stuck = stuckSince >= 0f && (Time.time - stuckSince) > 0.35f;
        if (!stuck)
            return desired;

        // Tenta uma direção perpendicular livre e segura.
        Vector2[] alts = Mathf.Abs(desired.x) > 0.01f
            ? new[] { Vector2.up, Vector2.down }
            : new[] { Vector2.left, Vector2.right };

        if (stuckAvoidDir != Vector2.zero)
        {
            Vector2 keep = myTile + stuckAvoidDir * TileSize;
            if (IsWalkable(keep) && !IsEnemyHazard(keep) && !IsOnActiveBombLine(keep, bombs))
                return stuckAvoidDir;
        }

        for (int i = 0; i < alts.Length; i++)
        {
            Vector2 n = myTile + alts[i] * TileSize;
            if (IsWalkable(n) && !IsEnemyHazard(n) && !IsOnActiveBombLine(n, bombs))
            {
                stuckAvoidDir = alts[i];
                stuckSince = Time.time;
                return alts[i];
            }
        }

        return desired;
    }

    // ========================================================================
    //  Snapshots / utilidades
    // ========================================================================

    private static IReadOnlyList<Bomb> GetBombSnapshot()
    {
        if (bombSnapshotFrame == Time.frameCount)
            return bombSnapshot;
        return RefreshBombSnapshot();
    }

    private static IReadOnlyList<Bomb> RefreshBombSnapshot()
    {
        bombSnapshot.Clear();
        foreach (Bomb b in Bomb.ActiveBombs)
        {
            if (b == null || !b.isActiveAndEnabled || !b.gameObject.activeInHierarchy)
                continue;
            bombSnapshot.Add(b);
        }
        bombSnapshotFrame = Time.frameCount;
        return bombSnapshot;
    }

    private int EffectiveBombRadius() => bomb != null ? Mathf.Max(1, bomb.explosionRadius) : 2;

    private float EstimatedNextBombFuseSeconds()
    {
        if (bomb != null && bomb.bombFuseTime > 0f)
            return bomb.bombFuseTime;

        return Mathf.Max(0.1f, assumedBombFuseSeconds);
    }

    private int BombRadius(Bomb b)
    {
        if (b != null && b.Owner != null)
            return Mathf.Max(1, b.Owner.explosionRadius);
        return 2;
    }

    private float BombFuseRemaining(Bomb b)
    {
        if (b == null) return float.PositiveInfinity;
        if (b.HasExploded) return 0f;
        if (b.IsControlBomb) return float.PositiveInfinity;
        return Mathf.Max(0f, b.RemainingFuseSeconds);
    }

    private float TileSize => movement != null ? Mathf.Max(0.01f, movement.tileSize) : 1f;

    private float SingleStepTravelTime()
    {
        float speed = movement != null ? Mathf.Max(0.01f, movement.speed) : 5f;
        return 1f / speed;
    }

    private Vector2 GetWorldPos()
    {
        if (movement != null && movement.Rigidbody != null)
            return movement.Rigidbody.position;
        return transform.position;
    }

    private Vector2 RoundToTile(Vector2 p)
    {
        float t = TileSize;
        return new Vector2(Mathf.Round(p.x / t) * t, Mathf.Round(p.y / t) * t);
    }

    private int ManhattanTiles(Vector2 a, Vector2 b)
    {
        float t = TileSize;
        return Mathf.RoundToInt(Mathf.Abs(a.x - b.x) / t + Mathf.Abs(a.y - b.y) / t);
    }

    private bool IsStunned() => stun != null && stun.IsStunned;

    private void Log(string msg)
    {
        if (debugLogs)
            Debug.Log($"[NormalGameAI][P{playerId}] {msg}", this);
    }

    // Log de estado por frame (throttled). Detecta e destaca o caso em que a IA
    // fica presa dentro do raio de uma bomba (provável causa de auto-morte).
    private void LogTick(Vector2 myTile, bool danger, IReadOnlyList<Bomb> bombs)
    {
        if (!debugLogs)
            return;

        if (danger && myTile == lastDangerTile)
        {
            if (dangerStuckSince < 0f)
                dangerStuckSince = Time.time;
        }
        else
        {
            dangerStuckSince = -1f;
            lastDangerTile = myTile;
        }

        bool stuckInDanger = danger && dangerStuckSince >= 0f && (Time.time - dangerStuckSince) > 0.6f;

        if (Time.time - lastTickLogTime < 0.33f)
            return;
        lastTickLogTime = Time.time;

        float tHit = TimeUntilBlast(myTile, bombs, false, Vector2.zero, 0, 0f);
        string here = float.IsInfinity(tHit) ? "safe" : tHit.ToString("F2");
        int bombsLeft = bomb != null ? bomb.BombsRemaining : 0;
        string tag = stuckInDanger ? "STUCK-IN-DANGER" : (danger ? "danger" : "ok");

        Debug.Log($"[NormalGameAI][P{playerId}] {tag} tile:{myTile} move:{DescribeMove(currentMove)} " +
                  $"tHitHere:{here} inputMgr:{(PlayerInputManager.Instance != null)} " +
                  $"bombs:{bombs.Count} enemies:{enemyTiles.Count} bombsLeft:{bombsLeft}", this);
    }

    private static string DescribeMove(Vector2 m)
    {
        if (m == Vector2.zero) return "stay";
        if (Mathf.Abs(m.x) > Mathf.Abs(m.y)) return m.x > 0f ? "right" : "left";
        return m.y > 0f ? "up" : "down";
    }

    private readonly struct EscapeNode
    {
        public readonly Vector2 pos;
        public readonly int depth;
        public readonly Vector2 firstStep;

        public EscapeNode(Vector2 pos, int depth, Vector2 firstStep)
        {
            this.pos = pos;
            this.depth = depth;
            this.firstStep = firstStep;
        }
    }
}
#endif
