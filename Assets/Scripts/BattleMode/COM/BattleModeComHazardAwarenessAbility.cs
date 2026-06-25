using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Ability passiva de observação de perigos do ambiente.
///
/// PROBLEMA RESOLVIDO:
///   O BattleModeComController calculava o raio de explosão usando bomb.Owner.explosionRadius
///   (raio base), sem considerar que HasFullFire expande o raio para MaxExplosionRadius (9)
///   e que IsPowerBomb expande para powerBombRadius (15). A IA ficava em tiles que seriam
///   atingidos pela explosão real e morria sem perceber o perigo.
///
/// ARQUITETURA:
///   - BombController.GetPredictedBlastRadius(bomb) agora espelha a lógica de TriggerExplosion
///     e é usada pelo controller em GetDangerSeconds / GetBombRadiusAtTile (correção de base).
///   - Esta ability complementa a correção atuando PROATIVAMENTE: quando a bomba ainda tem
///     fuse longo (não disparou inDanger), ela detecta a zona ampliada e reposiciona a IA
///     para fora dela antes de estar em perigo imediato.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(BombController))]
public sealed class BattleModeComHazardAwarenessAbility : MonoBehaviour, IBattleModeComAbility
{
    // Filtro de diagnóstico: 0 = todos os jogadores COM.
    public const int DiagnosticPlayerIdFilter = 0;
    private static readonly bool EnableDiagnostics = false;
    private const float DiagnosticLogIntervalSeconds = 0.5f;

    // Buffer de tiles além do raio ampliado para considerar como zona de risco proativa.
    // Valor 1 = a IA foge se estiver dentro de (enhancedRadius + 1) tiles da bomba.
    private const int SafetyBufferTiles = 1;

    // Apenas bombas inimigas com fuse menor que esta janela disparam o reposicionamento
    // proativo. Bombas com fuse muito longo não justificam interromper o comportamento normal.
    private const float EnhancedThreatFuseWindowSeconds = 4.0f;

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
    private GameManager gameManager;
    private Tilemap groundTilemap;
    private Tilemap destructibleTilemap;
    private Tilemap indestructibleTilemap;
    private ContactFilter2D obstacleFilter;
    private Collider2D[] ownColliders;
    private readonly Collider2D[] obstacleHits = new Collider2D[12];
    private float tileSize = 1f;

    // === Estado de diagnóstico ===
    private string lastDecisionTrace = "not evaluated";
    private float lastDiagnosticLogTime = -10f;
    private string lastDiagnosticLogKey = string.Empty;

    // === IBattleModeComAbility ===
    public string DiagnosticName => "HazardAwareness";
    public string LastDecisionTrace => lastDecisionTrace;

    public bool IsAvailable
    {
        get
        {
            CacheReferences();
            return identity != null && movement != null && !movement.isDead;
        }
    }

    private void Awake() => CacheReferences();
    private void OnEnable() => CacheReferences();

    private void CacheReferences()
    {
        if (identity == null) TryGetComponent(out identity);
        if (movement == null) TryGetComponent(out movement);
        if (bombController == null) TryGetComponent(out bombController);

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
    }

    // -----------------------------------------------------------------
    // TryBuildEmergencyDecision
    // -----------------------------------------------------------------
    // A correção em GetDangerSeconds (que agora usa GetPredictedBlastRadius)
    // já garante que tiles em zonas de blast ampliado ativam inDanger=true e
    // disparam a fuga nativa do controller. Esta ability não precisa interferir.
    public bool TryBuildEmergencyDecision(
        BattleModeComDifficultySettings settings,
        BattleModeComController controller,
        Vector2Int myTile,
        float currentDangerSeconds,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;
        lastDecisionTrace = "emergency: handled by corrected GetDangerSeconds";
        return false;
    }

    // -----------------------------------------------------------------
    // TryBuildCandidateDecision
    // -----------------------------------------------------------------
    // Executa quando inDanger=false. Verifica se há bombas inimigas com raio
    // ampliado (HasFullFire ou IsPowerBomb) cujo fuse está dentro da janela de
    // ameaça e cujo blast zona alcança o tile atual. Se sim, reposiciona proativamente
    // para fora da zona antes que o perigo se torne imediato.
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
            return false;
        }

        // Encontra a bomba inimiga com raio ampliado mais urgente que ameaça o tile atual.
        if (!TryFindWorstEnhancedThreat(myTile, out Bomb worstBomb, out int enhancedRadius, out float worstFuse))
        {
            lastDecisionTrace = "candidate no enhanced threat in range";
            return false;
        }

        Vector2Int threatTile = WorldToTile(worstBomb.GetLogicalPosition());
        string threatType = worstBomb.IsPowerBomb ? "PowerBomb" : "FullFire";

        // Busca rota de fuga para fora da zona ampliada.
        if (!TryFindEscapeFromEnhancedZone(settings, myTile, threatTile, enhancedRadius,
                out Vector2 firstMove, out Vector2Int targetTile, out string route))
        {
            lastDecisionTrace =
                $"candidate {threatType} bomb:{threatTile} enhancedR:{enhancedRadius} " +
                $"fuse:{worstFuse:F2} no escape route";
            LogDiagnostic("ENHANCED_NO_ESCAPE",
                $"my:{myTile} bomb:{threatTile} type:{threatType} enhancedR:{enhancedRadius} fuse:{worstFuse:F2}",
                force: true);
            return false;
        }

        lastDecisionTrace =
            $"candidate reposition from {threatType} bomb:{threatTile} " +
            $"enhancedR:{enhancedRadius} fuse:{worstFuse:F2} target:{targetTile} route:{route}";
        LogDiagnostic("ENHANCED_REPOSITION",
            $"my:{myTile} type:{threatType} bomb:{threatTile} enhancedR:{enhancedRadius} " +
            $"fuse:{worstFuse:F2} target:{targetTile} move:{FirstMoveDescription(firstMove)} route:{route}");

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.Reposition,
            // Peso maior que patrulha mas menor que ações de combate, para que o
            // reposicionamento de segurança vença o movimento idle sem bloquear ofensiva.
            Weight = settings.patrolWeight + 60,
            TargetTile = targetTile,
            HasTarget = true,
            FirstMove = firstMove,
            Reason = $"avoid {threatType} enhanced blast r:{enhancedRadius}",
            InputDescription = FirstMoveDescription(firstMove)
        };
        return true;
    }

    // -----------------------------------------------------------------
    // Helpers privados
    // -----------------------------------------------------------------

    /// <summary>
    /// Varre as bombas ativas buscando a ameaça inimiga com raio ampliado mais urgente
    /// que alcança o tile do AI. Retorna false se nenhuma ameaça for encontrada.
    /// </summary>
    private bool TryFindWorstEnhancedThreat(
        Vector2Int myTile,
        out Bomb worstBomb,
        out int worstEnhancedRadius,
        out float worstFuse)
    {
        worstBomb = null;
        worstEnhancedRadius = 0;
        worstFuse = float.PositiveInfinity;

        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded || bomb.IsBeingHeldByPowerGlove)
                continue;

            // Ignora bombas próprias — o sistema base cuida delas.
            if (bomb.Owner == bombController)
                continue;

            if (bomb.Owner == null)
                continue;

            int baseRadius = Mathf.Max(1, bomb.Owner.explosionRadius);
            int enhancedRadius = Mathf.Max(1, bomb.Owner.GetPredictedBlastRadius(bomb));

            // Só interessa se o raio ampliado supera o base — caso contrário o
            // controller já detecta o perigo corretamente sem ajuda desta ability.
            if (enhancedRadius <= baseRadius)
                continue;

            float fuse = bomb.IsControlBomb ? 0.65f : bomb.RemainingFuseSeconds;
            if (fuse > EnhancedThreatFuseWindowSeconds)
                continue;

            Vector2Int bombTile = WorldToTile(bomb.GetLogicalPosition());

            // Verifica se o AI está na zona de blast ampliado (com buffer).
            if (!IsTileInSimpleBlastZone(bombTile, myTile, enhancedRadius + SafetyBufferTiles))
                continue;

            // Prioriza a bomba com fuse mais curto (ameaça mais urgente).
            if (fuse < worstFuse || worstBomb == null)
            {
                worstBomb = bomb;
                worstEnhancedRadius = enhancedRadius;
                worstFuse = fuse;
            }
        }

        return worstBomb != null;
    }

    /// <summary>
    /// BFS que encontra o tile seguro mais próximo fora da zona de blast ampliado.
    /// Usa checagem simplificada de blast (sem paredes) — intencional, pois paredes
    /// destrutíveis não são proteção confiável.
    /// </summary>
    private bool TryFindEscapeFromEnhancedZone(
        BattleModeComDifficultySettings settings,
        Vector2Int start,
        Vector2Int threatBombTile,
        int enhancedRadius,
        out Vector2 firstMove,
        out Vector2Int target,
        out string route)
    {
        firstMove = Vector2.zero;
        target = start;
        route = "none";

        // parent[tile] = tile anterior no caminho BFS (start aponta para si mesmo)
        var parent = new Dictionary<Vector2Int, Vector2Int>();
        var depthMap = new Dictionary<Vector2Int, int>();
        var open = new Queue<Vector2Int>();

        parent[start] = start;
        depthMap[start] = 0;
        open.Enqueue(start);

        int maxDepth = Mathf.Max(4, settings.searchDepth);

        while (open.Count > 0)
        {
            Vector2Int tile = open.Dequeue();
            int depth = depthMap[tile];

            // Tile fora da zona de ameaça ampliada → rota encontrada.
            if (tile != start && !IsTileInSimpleBlastZone(threatBombTile, tile, enhancedRadius + SafetyBufferTiles))
            {
                target = tile;
                Vector2Int firstStep = ReconstructFirstStep(parent, start, tile);
                firstMove = new Vector2(firstStep.x, firstStep.y);
                route = $"hazard escape depth {depth}";
                return firstMove != Vector2.zero;
            }

            if (depth >= maxDepth)
                continue;

            for (int i = 0; i < CardinalTiles.Length; i++)
            {
                Vector2Int next = tile + CardinalTiles[i];
                if (parent.ContainsKey(next)) continue;
                if (!IsWalkableTile(next, start)) continue;

                parent[next] = tile;
                depthMap[next] = depth + 1;
                open.Enqueue(next);
            }
        }

        return false;
    }

    /// <summary>
    /// Reconstrói o primeiro passo do caminho BFS de start até goal.
    /// </summary>
    private static Vector2Int ReconstructFirstStep(
        Dictionary<Vector2Int, Vector2Int> parentMap,
        Vector2Int start,
        Vector2Int goal)
    {
        Vector2Int current = goal;
        int guard = 0;
        while (parentMap.TryGetValue(current, out Vector2Int p) && p != start && guard++ < 64)
            current = p;
        return current - start;
    }

    /// <summary>
    /// Verifica se o tile está na linha de explosão da bomba dentro do raio fornecido.
    /// Intencionalmente ignora paredes — destrutíveis não são proteção confiável e
    /// a checagem conservadora é preferível para esta ability proativa.
    /// </summary>
    private static bool IsTileInSimpleBlastZone(Vector2Int bombTile, Vector2Int tile, int radius)
    {
        if (bombTile == tile)
            return true;

        int dx = tile.x - bombTile.x;
        int dy = tile.y - bombTile.y;

        // Mesma coluna, dentro do raio
        if (dx == 0 && Mathf.Abs(dy) <= radius)
            return true;

        // Mesma linha, dentro do raio
        if (dy == 0 && Mathf.Abs(dx) <= radius)
            return true;

        return false;
    }

    private bool IsWalkableTile(Vector2Int tile, Vector2Int origin)
    {
        // Verifica presença de tile de chão
        if (groundTilemap != null &&
            !groundTilemap.HasTile(groundTilemap.WorldToCell(TileToWorld(tile))))
            return false;

        // Bloqueia em indestrut íveis
        if (indestructibleTilemap != null &&
            indestructibleTilemap.HasTile(indestructibleTilemap.WorldToCell(TileToWorld(tile))))
            return false;

        // Bloqueia em destrutíveis
        if (destructibleTilemap != null &&
            destructibleTilemap.HasTile(destructibleTilemap.WorldToCell(TileToWorld(tile))))
            return false;

        // Verifica colisões físicas (outros players, bombas sólidas, etc.)
        Vector3 worldPos = TileToWorld(tile);
        int hitCount = Physics2D.OverlapCircle(
            worldPos, tileSize * 0.3f, obstacleFilter, obstacleHits);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D col = obstacleHits[i];
            if (col == null) continue;

            bool isOwn = false;
            for (int j = 0; j < ownColliders.Length; j++)
            {
                if (ownColliders[j] == col) { isOwn = true; break; }
            }

            if (!isOwn) return false;
        }

        return true;
    }

    // -----------------------------------------------------------------
    // Utilitários
    // -----------------------------------------------------------------

    private Vector2Int WorldToTile(Vector3 pos) =>
        new Vector2Int(
            Mathf.RoundToInt(pos.x / tileSize),
            Mathf.RoundToInt(pos.y / tileSize));

    private Vector3 TileToWorld(Vector2Int tile) =>
        new Vector3(tile.x * tileSize, tile.y * tileSize, 0f);

    private static string FirstMoveDescription(Vector2 move)
    {
        if (move == Vector2.zero) return "none";
        if (move.x > 0.5f) return "MoveRight";
        if (move.x < -0.5f) return "MoveLeft";
        if (move.y > 0.5f) return "MoveUp";
        if (move.y < -0.5f) return "MoveDown";
        return "none";
    }

    private void LogDiagnostic(string key, string message, bool force = false)
    {
        if (!EnableDiagnostics) return;

        int id = identity != null ? Mathf.Clamp(identity.playerId, 1, 6) : 0;
        if (DiagnosticPlayerIdFilter != 0 && id != DiagnosticPlayerIdFilter) return;

        if (!force &&
            key == lastDiagnosticLogKey &&
            Time.time - lastDiagnosticLogTime < DiagnosticLogIntervalSeconds)
            return;

        lastDiagnosticLogKey = key;
        lastDiagnosticLogTime = Time.time;
        Vector2Int tile = movement != null ? WorldToTile(movement.transform.position) : Vector2Int.zero;
        Debug.Log($"[BattleCOMHazard][P{id}] tile:{tile} {key} {message}", this);
    }
}
