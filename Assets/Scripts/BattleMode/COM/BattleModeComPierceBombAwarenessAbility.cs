using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Ability passiva de consciência de PierceBombs no campo.
///
/// PROBLEMA RESOLVIDO:
///   O modelo de perigo do controller (e das outras abilities) considera que blocos
///   destrutíveis e bombas BLOQUEIAM a linha de explosão. A explosão de uma
///   PierceBomb ATRAVESSA destrutíveis e itens (BombController.ExplodeAndCollect),
///   então:
///     1. A IA acha que está protegida atrás de um bloco e morre quando a pierce
///        explode através dele.
///     2. A pierce alcança outra bomba através de blocos e ACIONA UMA CADEIA — a
///        bomba acionada explode imediatamente, atingindo a IA em lugares que o
///        modelo ingênuo considera seguros. Isso inclui cadeias iniciadas pelas
///        pierce bombs da PRÓPRIA IA.
///
/// SOLUÇÃO:
///   Modelo de perigo pierce-aware com propagação de cadeia: para cada bomba ativa,
///   calcula a linha de blast honrando o flag IsPierceBomb (atravessa destrutíveis;
///   para em indestrutível; ao encontrar outra bomba aciona a cadeia e para). O fuse
///   efetivo de uma bomba encadeada é o MENOR entre o dela e o de quem a aciona
///   (relaxação iterativa). Se a IA está numa zona pierce-aware perigosa que o modelo
///   nativo não enxerga, reposiciona — proativamente (candidate) e, sob perigo nativo,
///   garante que o tile de fuga também seja seguro no modelo pierce-aware (emergency).
///
/// Sempre ativa para IAs COM (como Hazard/RubberAwareness) — a ameaça independe dos
/// power-ups da própria IA.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(BombController))]
public sealed class BattleModeComPierceBombAwarenessAbility : MonoBehaviour, IBattleModeComAbility
{
    // === Filtro de diagnóstico ===
    public const int DiagnosticPlayerIdFilter = 0; // 0 = todos
    public static readonly bool EnablePierceAwarenessDiagnostics = false;
    private const float SurgicalLogIntervalSeconds = 0.35f;

    // === Constantes de comportamento ===
    // Janela de fuse: ameaças com fuse acima disso não interrompem o comportamento normal.
    private const float ThreatFuseWindowSeconds = 4.0f;
    // Fuse abaixo disso = urgente (bônus de peso na esquiva proativa).
    private const float UrgentFuseSeconds = 1.5f;
    // Iterações máximas da relaxação de fuses encadeados.
    private const int ChainRelaxationIterations = 4;
    private const float DodgeStuckSeconds = 0.25f;

    private static readonly Vector2Int[] CardinalTiles =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    // === Referências ===
    private PlayerIdentity identity;
    private MovementController movement;
    private BombController bombController;
    private BattleModeComController comController;
    private AbilitySystem abilitySystem;
    private GameManager gameManager;
    private Tilemap groundTilemap;
    private Tilemap destructibleTilemap;
    private Tilemap indestructibleTilemap;
    private ContactFilter2D obstacleFilter;
    private Collider2D[] ownColliders;
    private readonly Collider2D[] obstacleHits = new Collider2D[12];
    private float tileSize = 1f;
    private int explosionMask;

    // === Snapshot de bombas (recalculado por avaliação) ===
    private struct BombSnapshot
    {
        public Bomb Bomb;
        public Vector2Int Tile;
        public int Radius;
        public bool Pierce;
        public float EffectiveFuse; // já considerando cadeias
    }

    private readonly List<BombSnapshot> bombSnapshots = new List<BombSnapshot>(12);
    private bool snapshotHasPierce;
    private bool snapshotHasChainThroughBlocks;
    private float snapshotTime = -10f;

    // === Detecção de travamento na esquiva ===
    private Vector2Int dodgeLastTile;
    private float dodgeStuckSince = -10f;
    private Vector2Int dodgeLastAttemptedStep;
    private Vector2Int dodgeTrackedAttemptedStep;
    private Vector2 dodgeProgressPosition;
    private readonly List<Vector2Int> dodgeBlockedSteps = new List<Vector2Int>(4);

    // === BFS reutilizável ===
    private struct SearchNode
    {
        public Vector2Int Parent;
        public int Depth;
    }

    private readonly Dictionary<Vector2Int, SearchNode> searchVisited =
        new Dictionary<Vector2Int, SearchNode>(96);
    private readonly Queue<Vector2Int> searchOpen = new Queue<Vector2Int>(96);

    // === Diagnóstico ===
    private string lastDecisionTrace = "not evaluated";
    private float lastSurgicalLogTime = -10f;
    private string lastSurgicalLogKey = string.Empty;

    // === IBattleModeComAbility ===
    public string DiagnosticName => "PierceBombAwareness";
    public string LastDecisionTrace => lastDecisionTrace;

    public bool IsAvailable
    {
        get
        {
            CacheReferences();
            return identity != null && movement != null && !movement.isDead;
        }
    }

    private bool CanPassBombs =>
        abilitySystem != null && abilitySystem.IsEnabled(BombPassAbility.AbilityId);

    private bool CanPassDestructibles =>
        abilitySystem != null && abilitySystem.IsEnabled(DestructiblePassAbility.AbilityId);

    private void Awake() => CacheReferences();
    private void OnEnable() => CacheReferences();

    private void CacheReferences()
    {
        if (identity == null) TryGetComponent(out identity);
        if (movement == null) TryGetComponent(out movement);
        if (bombController == null) TryGetComponent(out bombController);
        if (comController == null) TryGetComponent(out comController);
        if (abilitySystem == null) TryGetComponent(out abilitySystem);

        ownColliders = GetComponentsInChildren<Collider2D>(true);

        if (movement != null)
        {
            tileSize = Mathf.Max(0.01f, movement.tileSize);
            obstacleFilter = new ContactFilter2D { useLayerMask = true, useTriggers = true };
            obstacleFilter.SetLayerMask(movement.obstacleMask);
        }

        explosionMask = LayerMask.GetMask("Explosion");

        gameManager = GameManager.Instance != null ? GameManager.Instance : FindAnyObjectByType<GameManager>();
        if (gameManager != null)
        {
            groundTilemap = gameManager.groundTilemap;
            destructibleTilemap = gameManager.destructibleTilemap;
            indestructibleTilemap = gameManager.indestructibleTilemap;
        }
    }

    // =====================================================================
    // TryBuildEmergencyDecision — fuga com modelo pierce-aware
    // =====================================================================
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
            lastDecisionTrace = "emergency unavailable";
            LogSurgical("EMERGENCY_UNAVAILABLE", $"my:{myTile} nativeDanger:{FormatDanger(currentDangerSeconds)}", force: true);
            return false;
        }

        RefreshBombSnapshots();
        LogOwnPierceContext("EMERGENCY", myTile, currentDangerSeconds);

        // Só interfere se há pierce no campo ou cadeia atravessando blocos —
        // sem isso o modelo nativo é equivalente e a fuga nativa resolve.
        if (!snapshotHasPierce && !snapshotHasChainThroughBlocks)
        {
            lastDecisionTrace = "emergency no pierce/chain threat on field";
            LogSurgical("EMERGENCY_GATE_NO_PIERCE",
                $"my:{myTile} bombs:{bombSnapshots.Count} pierce:{snapshotHasPierce} chainThroughBlocks:{snapshotHasChainThroughBlocks} nativeDanger:{FormatDanger(currentDangerSeconds)}",
                force: true);
            return false;
        }

        float pierceDanger = GetPierceAwareDangerSeconds(myTile);
        if (float.IsInfinity(pierceDanger) || pierceDanger > ThreatFuseWindowSeconds)
        {
            lastDecisionTrace = $"emergency tile not pierce-threatened ({FormatDanger(pierceDanger)})";
            LogSurgical("EMERGENCY_GATE_TILE_SAFE",
                $"my:{myTile} pierceDanger:{FormatDanger(pierceDanger)} window:{ThreatFuseWindowSeconds:F1} nativeDanger:{FormatDanger(currentDangerSeconds)} scan:{BuildNeighborScanSummary(settings, myTile)}",
                force: true);
            return false;
        }

        LogSurgical("EMERGENCY_DODGE_ATTEMPT",
            $"my:{myTile} pierceDanger:{FormatDanger(pierceDanger)} nativeDanger:{FormatDanger(currentDangerSeconds)}",
            force: true);

        // Sob perigo nativo + ameaça pierce: fornece fuga cujo destino é seguro
        // TAMBÉM no modelo pierce-aware (a fuga nativa pode escolher um tile atrás
        // de um bloco que a pierce atravessa).
        return TryBuildDodgeDecision(settings, myTile, pierceDanger, 295, "emergency", out decision);
    }

    // =====================================================================
    // TryBuildCandidateDecision — esquiva proativa
    // =====================================================================
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
            LogSurgical("CANDIDATE_UNAVAILABLE", $"my:{myTile}", force: true);
            return false;
        }

        RefreshBombSnapshots();
        LogOwnPierceContext("CANDIDATE", myTile, float.PositiveInfinity);

        if (!snapshotHasPierce && !snapshotHasChainThroughBlocks)
        {
            lastDecisionTrace = "candidate no pierce/chain threat on field";
            LogSurgical("CANDIDATE_GATE_NO_PIERCE",
                $"my:{myTile} bombs:{bombSnapshots.Count} pierce:{snapshotHasPierce} chainThroughBlocks:{snapshotHasChainThroughBlocks}");
            ClearDodgeStuckState();
            dodgeLastTile = myTile;
            return false;
        }

        float pierceDanger = GetPierceAwareDangerSeconds(myTile);
        if (float.IsInfinity(pierceDanger) || pierceDanger > ThreatFuseWindowSeconds)
        {
            lastDecisionTrace = $"candidate tile not pierce-threatened ({FormatDanger(pierceDanger)})";
            LogSurgical("CANDIDATE_GATE_TILE_SAFE",
                $"my:{myTile} pierceDanger:{FormatDanger(pierceDanger)} window:{ThreatFuseWindowSeconds:F1} scan:{BuildNeighborScanSummary(settings, myTile)}",
                force: true);
            ClearDodgeStuckState();
            dodgeLastTile = myTile;
            return false;
        }

        LogSurgical("CANDIDATE_DODGE_ATTEMPT",
            $"my:{myTile} pierceDanger:{FormatDanger(pierceDanger)}",
            force: true);

        // O ponto desta ability: o tile parece seguro no modelo NATIVO (senão o
        // controller já estaria em inDanger e a fuga nativa/emergency cuidaria),
        // mas é perigoso no modelo pierce-aware. Reposiciona proativamente.
        //
        // IMPORTANTE: a seleção de candidates do controller é SORTEIO PONDERADO.
        // Com peso modesto a esquiva perde o sorteio para farm/patrol e a IA morre
        // parada dentro da zona pierce (causa de morte observada nos P3/P4).
        // Por isso o peso escala agressivamente com a urgência do fuse:
        //   fuse <= 1.5s  → 4000 (esquiva vence o sorteio na prática)
        //   fuse <= 2.5s  → 400  (domina a maioria dos pools)
        //   fuse <= 4.0s  → patrol+60 (proativo, sem atropelar a ofensiva)
        int weight;
        if (pierceDanger <= UrgentFuseSeconds)
            weight = 4000;
        else if (pierceDanger <= 2.5f)
            weight = 400 + DifficultyWeight(settings);
        else
            weight = settings.patrolWeight + 60 + DifficultyWeight(settings);

        return TryBuildDodgeDecision(settings, myTile, pierceDanger, weight, "candidate", out decision);
    }

    // =====================================================================
    // Esquiva
    // =====================================================================
    private bool TryBuildDodgeDecision(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        float pierceDanger,
        int weight,
        string phase,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        UpdateDodgeStuckDetection(myTile);

        if (!TryFindPierceSafeTile(settings, myTile, dodgeBlockedSteps,
                out Vector2 firstMove, out Vector2Int target, out int depth))
        {
            lastDecisionTrace = $"{phase} pierce-threatened but no safe route";
            LogSurgical("DODGE_NO_ROUTE",
                $"my:{myTile} pierceDanger:{FormatDanger(pierceDanger)} " +
                $"scan:{BuildNeighborScanSummary(settings, myTile)}",
                force: true);
            return false;
        }

        dodgeLastAttemptedStep = Vector2Int.RoundToInt(firstMove);

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.Reposition,
            Weight = weight,
            TargetTile = target,
            HasTarget = true,
            FirstMove = firstMove,
            Reason = "pierce escape dodge blast through blocks",
            InputDescription = FirstMoveDescription(firstMove)
        };

        lastDecisionTrace =
            $"{phase} pierce dodge danger:{FormatDanger(pierceDanger)} target:{target} depth:{depth} w:{weight}";
        LogSurgical("DODGE",
            $"phase:{phase} my:{myTile} pierceDanger:{FormatDanger(pierceDanger)} " +
            $"target:{target} move:{FirstMoveDescription(firstMove)} depth:{depth} weight:{weight}");
        return true;
    }

    // =====================================================================
    // Snapshot de bombas + relaxação de cadeias
    // =====================================================================

    /// <summary>
    /// Reconstrói o snapshot de bombas ativas e propaga fuses por cadeia:
    /// se a linha de blast pierce-aware da bomba A alcança a bomba B, então
    /// B explode junto com A (fuse efetivo de B = min(B, A)).
    /// Também marca se existe pierce no campo e se alguma cadeia só acontece
    /// graças a pierce atravessando blocos (informação de diagnóstico/gate).
    /// </summary>
    private void RefreshBombSnapshots()
    {
        // Cache por frame: emergency + candidate no mesmo Think reutilizam.
        if (Mathf.Abs(Time.time - snapshotTime) < 0.001f)
            return;

        snapshotTime = Time.time;
        bombSnapshots.Clear();
        snapshotHasPierce = false;
        snapshotHasChainThroughBlocks = false;

        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded || bomb.IsBeingHeldByPowerGlove)
                continue;

            int radius = bomb.Owner != null
                ? Mathf.Max(1, bomb.Owner.GetPredictedBlastRadius(bomb))
                : 2;

            var snapshot = new BombSnapshot
            {
                Bomb = bomb,
                Tile = WorldToTile(bomb.GetLogicalPosition()),
                Radius = radius,
                Pierce = bomb.IsPierceBomb,
                EffectiveFuse = bomb.IsControlBomb ? 0.65f : bomb.RemainingFuseSeconds
            };

            if (snapshot.Pierce)
                snapshotHasPierce = true;

            bombSnapshots.Add(snapshot);
        }

        if (bombSnapshots.Count < 1)
            return;

        // Relaxação: propaga o menor fuse pelas cadeias até estabilizar.
        for (int iteration = 0; iteration < ChainRelaxationIterations; iteration++)
        {
            bool changed = false;

            for (int a = 0; a < bombSnapshots.Count; a++)
            {
                BombSnapshot source = bombSnapshots[a];

                for (int b = 0; b < bombSnapshots.Count; b++)
                {
                    if (a == b)
                        continue;

                    BombSnapshot other = bombSnapshots[b];
                    if (other.EffectiveFuse <= source.EffectiveFuse + 0.0001f)
                        continue;

                    if (!BlastReachesBomb(source, other.Tile, out bool passedThroughBlock))
                        continue;

                    other.EffectiveFuse = source.EffectiveFuse;
                    bombSnapshots[b] = other;
                    changed = true;

                    if (passedThroughBlock)
                        snapshotHasChainThroughBlocks = true;
                }
            }

            if (!changed)
                break;
        }
    }

    /// <summary>
    /// True se a linha de blast pierce-aware de source alcança bombTile.
    /// passedThroughBlock indica que a corrente só existe por causa do pierce
    /// (atravessou pelo menos um destrutível no caminho).
    /// </summary>
    private bool BlastReachesBomb(BombSnapshot source, Vector2Int bombTile, out bool passedThroughBlock)
    {
        passedThroughBlock = false;

        Vector2Int delta = bombTile - source.Tile;
        bool sameColumn = delta.x == 0 && delta.y != 0;
        bool sameRow = delta.y == 0 && delta.x != 0;
        if (!sameColumn && !sameRow)
            return false;

        int distance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        if (distance > source.Radius)
            return false;

        Vector2Int dir = new(Mathf.Clamp(delta.x, -1, 1), Mathf.Clamp(delta.y, -1, 1));
        for (int step = 1; step < distance; step++)
        {
            Vector2Int check = source.Tile + dir * step;

            if (BlocksExplosionAtIndestructible(check))
                return false;

            if (HasDestructibleTile(check))
            {
                if (!source.Pierce)
                    return false;

                passedThroughBlock = true;
                continue;
            }

            // Outra bomba no caminho: a explosão aciona ELA e para — não alcança
            // a bomba alvo diretamente (mas a cadeia continua via a intermediária,
            // que a relaxação cobre em outra iteração).
            if (FindBombTileIndexAt(check) >= 0)
                return false;
        }

        return true;
    }

    // =====================================================================
    // Modelo de perigo pierce-aware
    // =====================================================================

    /// <summary>
    /// Perigo no tile considerando explosões pierce (atravessam destrutíveis)
    /// e fuses encadeados. Infinity = seguro.
    /// </summary>
    private float GetPierceAwareDangerSeconds(Vector2Int tile)
    {
        if (explosionMask != 0)
        {
            Collider2D explosion = Physics2D.OverlapCircle(TileToWorld(tile), tileSize * 0.25f, explosionMask);
            if (explosion != null)
                return 0f;
        }

        float danger = float.PositiveInfinity;
        for (int i = 0; i < bombSnapshots.Count; i++)
        {
            BombSnapshot snapshot = bombSnapshots[i];
            if (!IsTileInPierceAwareBlastLine(snapshot, tile))
                continue;

            danger = Mathf.Min(danger, snapshot.EffectiveFuse);
        }

        return danger;
    }

    /// <summary>
    /// Linha de blast honrando o pierce da bomba: destrutíveis bloqueiam apenas
    /// bombas normais; indestrutíveis bloqueiam sempre; outra bomba no caminho
    /// para a linha (a cadeia é tratada pelos fuses efetivos).
    /// </summary>
    private bool IsTileInPierceAwareBlastLine(BombSnapshot snapshot, Vector2Int tile)
    {
        if (tile == snapshot.Tile)
            return true;

        Vector2Int delta = tile - snapshot.Tile;
        bool sameColumn = delta.x == 0 && delta.y != 0;
        bool sameRow = delta.y == 0 && delta.x != 0;
        if (!sameColumn && !sameRow)
            return false;

        int distance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        if (distance > snapshot.Radius)
            return false;

        Vector2Int dir = new(Mathf.Clamp(delta.x, -1, 1), Mathf.Clamp(delta.y, -1, 1));
        for (int step = 1; step < distance; step++)
        {
            Vector2Int check = snapshot.Tile + dir * step;

            if (BlocksExplosionAtIndestructible(check))
                return false;

            if (HasDestructibleTile(check))
            {
                if (!snapshot.Pierce)
                    return false;

                continue;
            }

            if (FindBombTileIndexAt(check) >= 0)
                return false;
        }

        return true;
    }

    private int FindBombTileIndexAt(Vector2Int tile)
    {
        for (int i = 0; i < bombSnapshots.Count; i++)
        {
            if (bombSnapshots[i].Tile == tile)
                return i;
        }

        return -1;
    }

    private bool IsPierceAwareDangerousAt(
        Vector2Int tile,
        float arrivalSeconds,
        BattleModeComDifficultySettings settings)
    {
        float dangerSeconds = GetPierceAwareDangerSeconds(tile);
        if (float.IsInfinity(dangerSeconds))
            return false;

        return dangerSeconds <= arrivalSeconds + settings.dangerReactionSeconds;
    }

    // =====================================================================
    // BFS para tile pierce-safe
    // =====================================================================
    private bool TryFindPierceSafeTile(
        BattleModeComDifficultySettings settings,
        Vector2Int start,
        List<Vector2Int> blockedFirstSteps,
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

        if (blockedFirstSteps != null)
        {
            for (int i = 0; i < blockedFirstSteps.Count; i++)
            {
                if (blockedFirstSteps[i] != Vector2Int.zero)
                    searchVisited[start + blockedFirstSteps[i]] =
                        new SearchNode { Parent = start, Depth = 0 };
            }
        }

        searchOpen.Enqueue(start);
        int maxDepth = Mathf.Max(4, settings.searchDepth + 2);

        while (searchOpen.Count > 0)
        {
            Vector2Int tile = searchOpen.Dequeue();
            SearchNode node = searchVisited[tile];
            float eta = EstimateTraversalSeconds(node.Depth);

            if (node.Depth > 0 &&
                float.IsInfinity(GetPierceAwareDangerSeconds(tile)) &&
                !IsPierceAwareDangerousAt(tile, eta + settings.safeTileMinimumSeconds, settings))
            {
                target = tile;
                resultDepth = node.Depth;
                firstMove = TileDirectionToVector(ReconstructFirstStep(start, tile));
                return firstMove != Vector2.zero;
            }

            if (node.Depth >= maxDepth)
                continue;

            for (int i = 0; i < CardinalTiles.Length; i++)
            {
                Vector2Int next = tile + CardinalTiles[i];
                if (searchVisited.ContainsKey(next))
                    continue;

                if (!IsWalkableTile(next, start))
                    continue;

                // Expansão usa o modelo pierce-aware: não atravessa zonas que a
                // pierce alcança dentro da janela de chegada.
                if (IsPierceAwareDangerousAt(next, EstimateTraversalSeconds(node.Depth + 1), settings))
                    continue;

                searchVisited[next] = new SearchNode { Parent = tile, Depth = node.Depth + 1 };
                searchOpen.Enqueue(next);
            }
        }

        return false;
    }

    // =====================================================================
    // Walkability (respeita BombPass/DestructiblePass se presentes)
    // =====================================================================
    private bool IsWalkableTile(Vector2Int tile, Vector2Int startTile)
    {
        if (!HasGroundTile(tile))
            return false;

        if (HasIndestructibleTile(tile))
            return false;

        if (HasDestructibleTile(tile) && !CanPassDestructibles)
            return false;

        if (FindBombAt(tile) != null && tile != startTile && !CanPassBombs)
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

                if (hit.GetComponentInParent<ItemPickup>() != null ||
                    hit.GetComponentInParent<MountWorldPickup>() != null)
                    continue;

                if (hit.GetComponentInParent<PlayerIdentity>() != null)
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

    // =====================================================================
    // Detecção de travamento
    // =====================================================================
    private void UpdateDodgeStuckDetection(Vector2Int myTile)
    {
        Vector2 currentPosition = transform.position;

        if (myTile != dodgeLastTile)
        {
            dodgeLastTile = myTile;
            ClearDodgeStuckState();
            dodgeProgressPosition = currentPosition;
            return;
        }

        if (dodgeLastAttemptedStep == Vector2Int.zero)
        {
            dodgeStuckSince = -1f;
            dodgeTrackedAttemptedStep = Vector2Int.zero;
            dodgeProgressPosition = currentPosition;
            return;
        }

        if (dodgeLastAttemptedStep != dodgeTrackedAttemptedStep)
        {
            dodgeTrackedAttemptedStep = dodgeLastAttemptedStep;
            dodgeStuckSince = Time.time;
            dodgeProgressPosition = currentPosition;
            return;
        }

        float progressThreshold = Mathf.Max(0.01f, tileSize * 0.03f);
        if ((currentPosition - dodgeProgressPosition).sqrMagnitude >=
            progressThreshold * progressThreshold)
        {
            // Permanecer no mesmo tile durante centralizacao ou troca de eixo nao
            // significa travamento se o personagem continua se deslocando.
            dodgeStuckSince = Time.time;
            dodgeProgressPosition = currentPosition;
            return;
        }

        if (dodgeStuckSince < 0f)
        {
            dodgeStuckSince = Time.time;
            dodgeProgressPosition = currentPosition;
        }
        else if (Time.time - dodgeStuckSince > DodgeStuckSeconds &&
                 !dodgeBlockedSteps.Contains(dodgeLastAttemptedStep))
        {
            dodgeBlockedSteps.Add(dodgeLastAttemptedStep);
            dodgeStuckSince = -1f;
            LogSurgical("DODGE_STUCK",
                $"my:{myTile} blocking:{dodgeLastAttemptedStep} total:{dodgeBlockedSteps.Count}",
                force: true);
        }
    }

    private void ClearDodgeStuckState()
    {
        dodgeStuckSince = -1f;
        dodgeBlockedSteps.Clear();
        dodgeLastAttemptedStep = Vector2Int.zero;
        dodgeTrackedAttemptedStep = Vector2Int.zero;
        dodgeProgressPosition = transform.position;
    }

    // =====================================================================
    // Utilitários / diagnóstico
    // =====================================================================
    private static int DifficultyWeight(BattleModeComDifficultySettings settings) =>
        settings.difficulty switch
        {
            BattleModeComputerLevel.Easy => -20,
            BattleModeComputerLevel.Hard => 30,
            _ => 0
        };

    private string BuildNeighborScanSummary(BattleModeComDifficultySettings settings, Vector2Int myTile)
    {
        var sb = new System.Text.StringBuilder(96);
        string[] labels = { "U", "D", "L", "R" };
        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            Vector2Int next = myTile + CardinalTiles[i];
            sb.Append(labels[i]).Append('[');

            if (!IsWalkableTile(next, myTile))
            {
                sb.Append("blocked");
            }
            else
            {
                float danger = GetPierceAwareDangerSeconds(next);
                sb.Append(float.IsInfinity(danger) ? "walk" : $"pierce-danger {danger:F2}");
            }

            sb.Append("] ");
        }

        sb.Append($"bombs:{bombSnapshots.Count} pierce:{snapshotHasPierce} chainThroughBlocks:{snapshotHasChainThroughBlocks}");
        return sb.ToString();
    }

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

    private bool BlocksExplosionAtIndestructible(Vector2Int tile)
    {
        return HasIndestructibleTile(tile) &&
               (comController == null ||
                !comController.IsExplosionPassThroughTile(tile));
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
        if (movement == null)
            return depth * 0.25f;

        float tilesPerSecond = Mathf.Max(1f, movement.speed);
        return depth / tilesPerSecond;
    }

    private static Vector2 TileDirectionToVector(Vector2Int dir) => new Vector2(dir.x, dir.y);

    private static string FirstMoveDescription(Vector2 move)
    {
        if (move == Vector2.zero) return "none";
        if (move.x > 0.5f) return "MoveRight";
        if (move.x < -0.5f) return "MoveLeft";
        if (move.y > 0.5f) return "MoveUp";
        if (move.y < -0.5f) return "MoveDown";
        return "none";
    }

    private static string FormatDanger(float seconds)
    {
        if (float.IsInfinity(seconds)) return "safe";
        if (seconds <= 0f) return "now";
        return $"{seconds:F2}";
    }

    /// <summary>
    /// Loga, sob perigo ou avaliação, o contexto das bombas PRÓPRIAS no campo — em
    /// especial pierce bombs recém-plantadas. Permite ver no console exatamente o
    /// fuse efetivo da própria pierce, se a IA está em cima dela e qual o perigo
    /// pierce-aware no tile atual. Diagnóstico do caso "planta pierce e fica parada".
    /// </summary>
    private void LogOwnPierceContext(string phase, Vector2Int myTile, float nativeDanger)
    {
        if (!EnablePierceAwarenessDiagnostics) return;

        int ownPierce = 0;
        bool standingOnOwnPierce = false;
        float nearestOwnPierceFuse = float.PositiveInfinity;
        Vector2Int nearestOwnPierceTile = myTile;

        for (int i = 0; i < bombSnapshots.Count; i++)
        {
            BombSnapshot snapshot = bombSnapshots[i];
            bool isMine = snapshot.Bomb != null && snapshot.Bomb.Owner == bombController;
            if (!isMine || !snapshot.Pierce)
                continue;

            ownPierce++;
            if (snapshot.Tile == myTile)
                standingOnOwnPierce = true;

            if (snapshot.EffectiveFuse < nearestOwnPierceFuse)
            {
                nearestOwnPierceFuse = snapshot.EffectiveFuse;
                nearestOwnPierceTile = snapshot.Tile;
            }
        }

        if (ownPierce == 0)
            return;

        float pierceDangerHere = GetPierceAwareDangerSeconds(myTile);
        LogSurgical($"{phase}_OWN_PIERCE",
            $"my:{myTile} ownPierce:{ownPierce} onTopOfOwn:{standingOnOwnPierce} " +
            $"nearestTile:{nearestOwnPierceTile} nearestFuse:{FormatDanger(nearestOwnPierceFuse)} " +
            $"pierceDangerHere:{FormatDanger(pierceDangerHere)} nativeDanger:{FormatDanger(nativeDanger)}",
            force: true);
    }

    private void LogSurgical(string key, string message, bool force = false)
    {
        if (!EnablePierceAwarenessDiagnostics) return;

        int id = identity != null ? Mathf.Clamp(identity.playerId, 1, 6) : 0;
        if (DiagnosticPlayerIdFilter != 0 && id != DiagnosticPlayerIdFilter) return;

        if (!force &&
            key == lastSurgicalLogKey &&
            Time.time - lastSurgicalLogTime < SurgicalLogIntervalSeconds)
            return;

        lastSurgicalLogKey = key;
        lastSurgicalLogTime = Time.time;
        Vector2Int tile = movement != null ? WorldToTile(movement.transform.position) : Vector2Int.zero;
        Debug.LogWarning($"[BattleCOM{DiagnosticName}][P{id}] tile:{tile} {key} {message}", this);
    }
}
