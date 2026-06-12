using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Ability de IA para usar o pulo do Pink Louie (PinkLouieJumpAbility).
///
/// MECÂNICA (PinkLouieJumpAbility.cs):
///   ActionC pula. A direção é o input direcional SEGURADO no frame do tap
///   (zero = pulo parado). Pulo direcional cai 2 tiles à frente (fallback 1 tile
///   se o pouso distante for inválido), atravessando tiles bloqueados no meio.
///   Durante o voo (~0.8s) o jogador fica INVULNERÁVEL.
///
/// COMPORTAMENTO DA IA:
///   1. DODGE DE EXPLOSÃO (emergency) — a ferramenta principal. Quando o perigo é
///      iminente e NÃO existe rota de fuga andando, pula no momento certo para a
///      explosão acontecer durante a invulnerabilidade do voo:
///        - prefere pulo direcional cujo pouso (2 ou 1 tile) fica fora do perigo;
///        - sem pouso seguro, pula parado se a explosão resolve durante o voo.
///      Precisão por dificuldade (roll por episódio de perigo, não por frame):
///        Hard 100% | Normal 50% | Easy 10%.
///   2. MOBILIDADE (candidate) — ocasionalmente pula 2 tiles por cima de tiles
///      bloqueados para alcançar áreas que andando não alcança.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(BombController))]
public sealed class BattleModeComPinkLouieJumpAbility : MonoBehaviour, IBattleModeComAbility
{
    // === Filtro de diagnóstico ===
    public const int DiagnosticPlayerIdFilter = 0; // 0 = todos
    public static readonly bool EnablePinkLouieJumpDiagnostics = true;
    private const float SurgicalLogIntervalSeconds = 0.35f;

    // === Constantes de comportamento ===
    // Espelham o PinkLouieJumpAbility.
    private const float JumpDurationSeconds = 0.8f;
    private const int JumpForwardCells = 2;
    // Janela de tap do dodge: a explosão precisa acontecer DURANTE o voo, com
    // margem antes do pouso. (danger <= janela → pular agora)
    private const float DirectionalDodgeWindowSeconds = 0.55f;
    // Pulo parado: precisa que a explosão comece cedo o bastante para o fogo
    // dissipar antes do pouso.
    private const float InPlaceDodgeWindowSeconds = 0.30f;
    // Cooldown do pulo de mobilidade.
    private const float TravelMinBlockedJump = 1; // precisa pular por cima de >=1 tile bloqueado

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

    // === Estado ===
    private float nextTravelJumpTime = -10f;
    private float nextPocketEscapeTime = -10f;
    private const float PocketEscapeCooldownSeconds = 1.0f;

    // Roll de precisão do dodge: UM roll por episódio de perigo. Se falhar, a IA
    // "errou o timing" e não pula durante este episódio.
    private bool dodgeEpisodeActive;
    private bool dodgeEpisodeRollSuccess;

    // === Cache de chance de mobilidade ===
    private float travelChanceCacheTime = -10f;
    private bool travelChanceCacheResult;

    // === BFS reutilizável (verificação de fuga andando) ===
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
    public string DiagnosticName => "PinkLouieJump";
    public string LastDecisionTrace => lastDecisionTrace;

    public bool IsAvailable
    {
        get
        {
            CacheReferences();
            if (identity == null || movement == null || movement.isDead)
                return false;

            return abilitySystem != null &&
                   abilitySystem.IsEnabled(PinkLouieJumpAbility.AbilityId);
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
    // Emergency — dodge de explosão
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
            dodgeEpisodeActive = false;
            return false;
        }

        if (float.IsInfinity(currentDangerSeconds))
        {
            lastDecisionTrace = "emergency no danger";
            dodgeEpisodeActive = false;
            return false;
        }

        // Só usa o pulo quando NÃO há como fugir andando — explosões fugíveis
        // ficam com a fuga nativa.
        if (HasWalkingEscape(settings, myTile, currentDangerSeconds))
        {
            lastDecisionTrace = "emergency walking escape exists";
            dodgeEpisodeActive = false;
            return false;
        }

        // Roll de precisão por episódio (Hard 100%, Normal 50%, Easy 10%).
        if (!dodgeEpisodeActive)
        {
            dodgeEpisodeActive = true;
            dodgeEpisodeRollSuccess = Random.value <= DodgePrecision(settings);
            LogSurgical("DODGE_ROLL",
                $"my:{myTile} precision:{DodgePrecision(settings):F2} success:{dodgeEpisodeRollSuccess}",
                force: true);
        }

        if (!dodgeEpisodeRollSuccess)
        {
            lastDecisionTrace = "emergency dodge roll failed (missed timing)";
            return false;
        }

        // 1. Pulo direcional para pouso seguro (preferido): pode pular cedo,
        // contanto que a explosão ocorra durante o voo OU o pouso seja seguro.
        if (TryFindDirectionalDodge(settings, myTile, currentDangerSeconds,
                out Vector2Int dir, out Vector2Int landing))
        {
            // Timing: espera a janela para garantir invulnerabilidade durante o
            // estouro (pular cedo demais = aterrissar antes da explosão).
            if (currentDangerSeconds > DirectionalDodgeWindowSeconds &&
                !float.IsInfinity(GetDangerSeconds(landing)))
            {
                lastDecisionTrace =
                    $"emergency waiting jump window danger:{currentDangerSeconds:F2}";
                return false;
            }

            decision = new BattleModeComAbilityDecision
            {
                Action = BattleModeComActionType.Reposition,
                Weight = 340 + DifficultyWeight(settings),
                TargetTile = landing,
                HasTarget = true,
                FirstMove = new Vector2(dir.x, dir.y),
                Reason = "pinklouie-jump dodge",
                InputDescription = AppendInput(FirstMoveDescription(dir), "ActionC"),
                TapActionC = true
            };

            lastDecisionTrace = $"emergency JUMP dir:{dir} landing:{landing} danger:{currentDangerSeconds:F2}";
            LogSurgical("JUMP_DODGE",
                $"my:{myTile} dir:{FirstMoveDescription(dir)} landing:{landing} " +
                $"danger:{FormatDanger(currentDangerSeconds)}",
                force: true);
            return true;
        }

        // 2. Pulo parado: sem pouso seguro em nenhuma direção. A explosão precisa
        // começar cedo o suficiente para dissipar antes do pouso.
        if (currentDangerSeconds <= InPlaceDodgeWindowSeconds)
        {
            decision = new BattleModeComAbilityDecision
            {
                Action = BattleModeComActionType.Stopped,
                Weight = 340 + DifficultyWeight(settings),
                TargetTile = myTile,
                HasTarget = true,
                FirstMove = Vector2.zero,
                Reason = "pinklouie-jump dodge in place",
                InputDescription = "ActionC",
                TapActionC = true
            };

            lastDecisionTrace = $"emergency JUMP_IN_PLACE danger:{currentDangerSeconds:F2}";
            LogSurgical("JUMP_IN_PLACE",
                $"my:{myTile} danger:{FormatDanger(currentDangerSeconds)}", force: true);
            return true;
        }

        lastDecisionTrace =
            $"emergency waiting in-place window danger:{currentDangerSeconds:F2}";
        return false;
    }

    // =====================================================================
    // Candidate — pulo de mobilidade
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
            return false;
        }

        // Fora de perigo: encerra qualquer episódio de dodge.
        dodgeEpisodeActive = false;

        // PRIORIDADE: preso num bolsão sem saída andando (ex.: pulou para um tile
        // cercado). Planta bomba (para abrir os blocos) e pula para fora no mesmo
        // frame — fora do bolsão, a fuga nativa cuida do resto do fuse.
        if (TryBuildPocketEscape(settings, myTile, out decision))
            return true;

        if (Time.time < nextTravelJumpTime)
        {
            lastDecisionTrace = $"candidate cooldown {(nextTravelJumpTime - Time.time):F2}s";
            return false;
        }

        if (!RollTravelChance(settings))
        {
            lastDecisionTrace = "candidate chance fail";
            return false;
        }

        // Mobilidade: pula 2 tiles por cima de um tile bloqueado para alcançar
        // área inacessível andando.
        if (!TryFindTravelJump(settings, myTile, out Vector2Int dir, out Vector2Int landing))
        {
            lastDecisionTrace = "candidate no useful travel jump";
            return false;
        }

        nextTravelJumpTime = Time.time + DifficultyCooldown(settings);

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.Reposition,
            Weight = settings.patrolWeight + 50 + DifficultyWeight(settings),
            TargetTile = landing,
            HasTarget = true,
            FirstMove = new Vector2(dir.x, dir.y),
            Reason = "pinklouie-jump travel",
            InputDescription = AppendInput(FirstMoveDescription(dir), "ActionC"),
            TapActionC = true
        };

        lastDecisionTrace = $"candidate TRAVEL dir:{dir} landing:{landing}";
        LogSurgical("JUMP_TRAVEL",
            $"my:{myTile} dir:{FirstMoveDescription(dir)} landing:{landing}");
        return true;
    }

    // =====================================================================
    // Dodge — pouso e fuga andando
    // =====================================================================

    /// <summary>
    /// Existe rota de fuga ANDANDO? (BFS padrão com timing). Se sim, a fuga
    /// nativa resolve e o pulo não é necessário.
    /// </summary>
    private bool HasWalkingEscape(
        BattleModeComDifficultySettings settings,
        Vector2Int start,
        float currentDangerSeconds)
    {
        searchVisited.Clear();
        searchOpen.Clear();
        searchVisited[start] = new SearchNode { Parent = start, Depth = 0 };
        searchOpen.Enqueue(start);

        int maxDepth = Mathf.Max(4, settings.searchDepth);

        while (searchOpen.Count > 0)
        {
            Vector2Int tile = searchOpen.Dequeue();
            SearchNode node = searchVisited[tile];
            float eta = EstimateWalkSeconds(node.Depth);

            if (node.Depth > 0 &&
                float.IsInfinity(GetDangerSeconds(tile)) &&
                !IsDangerousAt(tile, eta + settings.safeTileMinimumSeconds, settings))
                return true;

            if (node.Depth >= maxDepth)
                continue;

            for (int i = 0; i < CardinalTiles.Length; i++)
            {
                Vector2Int next = tile + CardinalTiles[i];
                if (searchVisited.ContainsKey(next))
                    continue;

                if (!IsWalkableTile(next, start))
                    continue;

                if (IsDangerousAt(next, EstimateWalkSeconds(node.Depth + 1), settings))
                    continue;

                searchVisited[next] = new SearchNode { Parent = tile, Depth = node.Depth + 1 };
                searchOpen.Enqueue(next);
            }
        }

        return false;
    }

    /// <summary>
    /// Procura uma direção de pulo cujo pouso (2 tiles, fallback 1 — espelhando o
    /// IsLandingAllowed do PinkLouieJump) seja seguro após o voo.
    /// </summary>
    private bool TryFindDirectionalDodge(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        float currentDangerSeconds,
        out Vector2Int bestDir,
        out Vector2Int bestLanding)
    {
        bestDir = Vector2Int.zero;
        bestLanding = myTile;
        float bestScore = float.NegativeInfinity;

        for (int d = 0; d < CardinalTiles.Length; d++)
        {
            Vector2Int dir = CardinalTiles[d];
            if (!TryResolveLanding(myTile, dir, out Vector2Int landing))
                continue;

            // Pouso precisa estar seguro quando a IA aterrissar (após o voo).
            float landingDanger = GetDangerSeconds(landing);
            bool landingSafeForever = float.IsInfinity(landingDanger);
            bool landingSafeAtTouchdown =
                landingSafeForever ||
                landingDanger > JumpDurationSeconds + settings.dangerReactionSeconds;

            if (!landingSafeAtTouchdown)
                continue;

            // Prefere pousos permanentemente seguros e mais distantes.
            float score = (landingSafeForever ? 100f : 0f) +
                          Mathf.Abs(landing.x - myTile.x) + Mathf.Abs(landing.y - myTile.y);
            if (score > bestScore)
            {
                bestScore = score;
                bestDir = dir;
                bestLanding = landing;
            }
        }

        return bestDir != Vector2Int.zero;
    }

    /// <summary>
    /// Resolve o pouso do pulo numa direção: tenta 2 tiles à frente; se o pouso
    /// for inválido, tenta 1 tile (espelha o JumpRoutine). O tile do meio NÃO
    /// precisa ser livre — o pulo passa por cima.
    /// </summary>
    private bool TryResolveLanding(Vector2Int start, Vector2Int dir, out Vector2Int landing)
    {
        Vector2Int far = start + dir * JumpForwardCells;
        if (IsLandingAllowed(far))
        {
            landing = far;
            return true;
        }

        Vector2Int near = start + dir;
        if (IsLandingAllowed(near))
        {
            landing = near;
            return true;
        }

        landing = start;
        return false;
    }

    /// <summary>
    /// Espelha o IsLandingAllowed do PinkLouieJump em nível de tile: precisa de
    /// chão; indestrutível bloqueia; destrutível bloqueia sem DestructiblePass;
    /// bomba bloqueia sem BombPass.
    /// </summary>
    private bool IsLandingAllowed(Vector2Int tile)
    {
        if (!HasGroundTile(tile))
            return false;

        if (HasIndestructibleTile(tile))
            return false;

        if (HasDestructibleTile(tile) && !CanPassDestructibles)
            return false;

        if (FindBombAt(tile) != null && !CanPassBombs)
            return false;

        return true;
    }

    // =====================================================================
    // Pocket escape — preso num tile sem saída andando
    // =====================================================================

    /// <summary>
    /// Se a IA está num "bolsão" (nenhum vizinho caminhável), planta uma bomba
    /// (para abrir os destrutíveis ao redor) e pula para fora no mesmo frame.
    /// Sem bombas (ou com bomba já plantada aqui), apenas pula para fora.
    /// </summary>
    private bool TryBuildPocketEscape(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;

        if (Time.time < nextPocketEscapeTime)
            return false;

        if (CountWalkableNeighbors(myTile) > 0)
            return false; // não está preso

        // Direção de saída: pouso válido, seguro e que NÃO seja outro bolsão
        // (evita o ping-pong entre dois tiles fechados).
        if (!TryFindPocketExit(settings, myTile, out Vector2Int dir, out Vector2Int landing))
        {
            lastDecisionTrace = "pocket but no exit jump";
            LogSurgical("POCKET_NO_EXIT", $"my:{myTile}", force: true);
            return false;
        }

        bool bombAtMyTile = FindBombAt(myTile) != null;
        bool canPlant = bombController != null &&
                        bombController.BombsRemaining > 0 &&
                        !bombAtMyTile;

        nextPocketEscapeTime = Time.time + PocketEscapeCooldownSeconds;

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.CombatPlant,
            Weight = 600 + DifficultyWeight(settings),
            TargetTile = landing,
            HasTarget = true,
            FirstMove = new Vector2(dir.x, dir.y),
            Reason = "pinklouie-jump pocket escape",
            InputDescription = canPlant
                ? AppendInput(AppendInput(FirstMoveDescription(dir), "ActionA"), "ActionC")
                : AppendInput(FirstMoveDescription(dir), "ActionC"),
            TapBomb = canPlant,
            TapActionC = true
        };

        lastDecisionTrace = $"pocket escape dir:{dir} landing:{landing} plant:{canPlant}";
        LogSurgical("POCKET_ESCAPE",
            $"my:{myTile} dir:{FirstMoveDescription(dir)} landing:{landing} plant:{canPlant} " +
            $"bombsLeft:{(bombController != null ? bombController.BombsRemaining : -1)}",
            force: true);
        return true;
    }

    private bool TryFindPocketExit(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out Vector2Int bestDir,
        out Vector2Int bestLanding)
    {
        bestDir = Vector2Int.zero;
        bestLanding = myTile;
        float bestScore = float.NegativeInfinity;

        for (int d = 0; d < CardinalTiles.Length; d++)
        {
            Vector2Int dir = CardinalTiles[d];
            if (!TryResolveLanding(myTile, dir, out Vector2Int landing))
                continue;

            if (landing == myTile)
                continue;

            // Pouso precisa estar seguro após o voo.
            if (IsDangerousAt(landing, JumpDurationSeconds + settings.dangerReactionSeconds, settings))
                continue;

            int exits = CountWalkableNeighbors(landing);
            float distance = Mathf.Abs(landing.x - myTile.x) + Mathf.Abs(landing.y - myTile.y);

            // Prioriza pousos com saídas (não-bolsões) e mais distantes; fora da
            // blast line da bomba que vamos plantar (mesma linha do pulo conta
            // como alinhado — penaliza, mas não proíbe: às vezes é a única saída).
            bool offOwnBlastLine = !IsTileInBlastLine(
                myTile, landing,
                bombController != null ? Mathf.Max(1, bombController.explosionRadius) : 2);

            float score = exits * 100f + (offOwnBlastLine ? 50f : 0f) + distance;
            if (score > bestScore)
            {
                bestScore = score;
                bestDir = dir;
                bestLanding = landing;
            }
        }

        return bestDir != Vector2Int.zero;
    }

    private int CountWalkableNeighbors(Vector2Int tile)
    {
        int count = 0;
        for (int i = 0; i < CardinalTiles.Length; i++)
        {
            if (IsWalkableTile(tile + CardinalTiles[i], tile))
                count++;
        }

        return count;
    }

    // =====================================================================
    // Pulo de mobilidade
    // =====================================================================

    /// <summary>
    /// Procura um pulo que atravesse pelo menos um tile bloqueado (esse é o valor
    /// do pulo de mobilidade) para um pouso válido e seguro.
    /// </summary>
    private bool TryFindTravelJump(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        out Vector2Int bestDir,
        out Vector2Int bestLanding)
    {
        bestDir = Vector2Int.zero;
        bestLanding = myTile;

        for (int d = 0; d < CardinalTiles.Length; d++)
        {
            Vector2Int dir = CardinalTiles[d];
            Vector2Int mid = myTile + dir;
            Vector2Int far = myTile + dir * JumpForwardCells;

            // Só interessa se o tile do meio é INTRANSPONÍVEL andando — senão a
            // IA simplesmente anda.
            if (IsWalkableTile(mid, myTile))
                continue;

            if (!IsLandingAllowed(far))
                continue;

            // Pouso seguro (sem perigo, com margem do voo).
            if (!float.IsInfinity(GetDangerSeconds(far)))
                continue;

            if (IsDangerousAt(far, JumpDurationSeconds + settings.safeTileMinimumSeconds, settings))
                continue;

            // Não pula para bolsões sem saída andando — isso prendia a IA num
            // ping-pong de pulos entre tiles fechados.
            if (CountWalkableNeighbors(far) == 0)
                continue;

            bestDir = dir;
            bestLanding = far;
            return true;
        }

        return false;
    }

    // =====================================================================
    // Chance / precisão por dificuldade
    // =====================================================================
    private static float DodgePrecision(BattleModeComDifficultySettings settings) =>
        settings.difficulty switch
        {
            BattleModeComputerLevel.Easy => 0.10f,
            BattleModeComputerLevel.Hard => 1.00f,
            _ => 0.50f
        };

    private bool RollTravelChance(BattleModeComDifficultySettings settings)
    {
        if (Time.time - travelChanceCacheTime < 0.001f)
            return travelChanceCacheResult;

        float chance = settings.difficulty switch
        {
            BattleModeComputerLevel.Easy => 0.10f,
            BattleModeComputerLevel.Hard => 0.40f,
            _ => 0.25f
        };

        bool result = Random.value <= chance;
        travelChanceCacheTime = Time.time;
        travelChanceCacheResult = result;
        return result;
    }

    private static float DifficultyCooldown(BattleModeComDifficultySettings settings) =>
        settings.difficulty switch
        {
            BattleModeComputerLevel.Easy => 6.0f,
            BattleModeComputerLevel.Hard => 2.5f,
            _ => 4.0f
        };

    private static int DifficultyWeight(BattleModeComDifficultySettings settings) =>
        settings.difficulty switch
        {
            BattleModeComputerLevel.Easy => -20,
            BattleModeComputerLevel.Hard => 30,
            _ => 0
        };

    // =====================================================================
    // Perigo / walkability
    // =====================================================================
    private bool IsDangerousAt(
        Vector2Int tile,
        float arrivalSeconds,
        BattleModeComDifficultySettings settings)
    {
        float dangerSeconds = GetDangerSeconds(tile);
        if (float.IsInfinity(dangerSeconds))
            return false;

        return dangerSeconds <= arrivalSeconds + settings.dangerReactionSeconds;
    }

    private float GetDangerSeconds(Vector2Int tile)
    {
        if (explosionMask != 0)
        {
            Collider2D explosion = Physics2D.OverlapCircle(TileToWorld(tile), tileSize * 0.25f, explosionMask);
            if (explosion != null)
                return 0f;
        }

        float danger = float.PositiveInfinity;
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded || bomb.IsBeingHeldByPowerGlove)
                continue;

            Vector2Int bombTile = WorldToTile(bomb.GetLogicalPosition());
            int radius = bomb.Owner != null
                ? Mathf.Max(1, bomb.Owner.GetPredictedBlastRadius(bomb))
                : 2;
            if (!IsTileInBlastLine(bombTile, tile, radius))
                continue;

            float seconds = bomb.IsControlBomb ? 0.65f : bomb.RemainingFuseSeconds;
            danger = Mathf.Min(danger, seconds);
        }

        return danger;
    }

    private bool IsTileInBlastLine(Vector2Int origin, Vector2Int tile, int radius)
    {
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

        Vector2Int dir = new(Mathf.Clamp(delta.x, -1, 1), Mathf.Clamp(delta.y, -1, 1));
        for (int step = 1; step < distance; step++)
        {
            Vector2Int check = origin + dir * step;
            if (HasIndestructibleTile(check) || HasDestructibleTile(check) || FindBombAt(check) != null)
                return false;
        }

        return true;
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

                if (hit.GetComponentInParent<ItemPickup>() != null)
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

    // =====================================================================
    // Tilemaps / utilitários
    // =====================================================================
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

    private float EstimateWalkSeconds(int depth)
    {
        if (movement == null)
            return depth * 0.25f;

        float tilesPerSecond = Mathf.Max(1f, movement.speed);
        return depth / tilesPerSecond;
    }

    private static string FirstMoveDescription(Vector2Int dir)
    {
        if (dir == Vector2Int.right) return "MoveRight";
        if (dir == Vector2Int.left) return "MoveLeft";
        if (dir == Vector2Int.up) return "MoveUp";
        if (dir == Vector2Int.down) return "MoveDown";
        return "none";
    }

    private static string AppendInput(string existing, string input) =>
        string.IsNullOrEmpty(existing) || existing == "none" ? input : existing + "+" + input;

    private static string FormatDanger(float seconds)
    {
        if (float.IsInfinity(seconds)) return "safe";
        if (seconds <= 0f) return "now";
        return $"{seconds:F2}";
    }

    private void LogSurgical(string key, string message, bool force = false)
    {
        if (!EnablePinkLouieJumpDiagnostics) return;

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
