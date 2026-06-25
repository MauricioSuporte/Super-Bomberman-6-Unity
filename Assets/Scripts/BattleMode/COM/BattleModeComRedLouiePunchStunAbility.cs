using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ability de IA para usar o soco de stun do Red Louie (RedLouiePunchStunAbility).
///
/// MECÂNICA (RedLouiePunchStunAbility.cs):
///   ActionC soca o tile à frente na direção do movimento/facing e aplica stun
///   (~1s) em jogadores adversários no battle mode. Cooldown curto em erro,
///   maior em acerto.
///
/// COMPORTAMENTO DA IA:
///   Quando um adversário está num tile cardinal adjacente, a IA ocasionalmente
///   vira para ele (FirstMove na direção) e pressiona ActionC no mesmo frame.
///   A frequência escala com a dificuldade (chance e cooldown por nível):
///     Easy   ~15% por oportunidade, cooldown 3.0s
///     Normal ~35% por oportunidade, cooldown 2.0s
///     Hard   ~65% por oportunidade, cooldown 1.0s
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(BombController))]
public sealed class BattleModeComRedLouiePunchStunAbility : MonoBehaviour, IBattleModeComAbility
{
    // === Filtro de diagnóstico ===
    public const int DiagnosticPlayerIdFilter = 0; // 0 = todos
    public static readonly bool EnableRedLouieStunDiagnostics = false;
    private const float SurgicalLogIntervalSeconds = 0.35f;

    // Distância máxima (em tiles, centro a centro) para considerar o adversário
    // "no tile da frente" — o soco alcança ~1 tile (punchRange 0.75 + box).
    private const float MaxPunchTileDistance = 1.25f;

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
    private readonly List<PlayerIdentity> activePlayers = new List<PlayerIdentity>(6);
    private float tileSize = 1f;
    private int explosionMask;

    // === Estado ===
    private float nextAttemptTime = -10f;

    // === Cache de chance (evita re-roll no mesmo ciclo de Think) ===
    private float chanceCacheTime = -10f;
    private bool chanceCacheResult;

    // === Diagnóstico ===
    private string lastDecisionTrace = "not evaluated";
    private float lastSurgicalLogTime = -10f;
    private string lastSurgicalLogKey = string.Empty;

    // === IBattleModeComAbility ===
    public string DiagnosticName => "RedLouieStun";
    public string LastDecisionTrace => lastDecisionTrace;

    public bool IsAvailable
    {
        get
        {
            CacheReferences();
            if (identity == null || movement == null || movement.isDead)
                return false;

            return abilitySystem != null &&
                   abilitySystem.IsEnabled(RedLouiePunchStunAbility.AbilityId);
        }
    }

    private void Awake() => CacheReferences();
    private void OnEnable() => CacheReferences();

    private void CacheReferences()
    {
        if (identity == null) TryGetComponent(out identity);
        if (movement == null) TryGetComponent(out movement);
        if (bombController == null) TryGetComponent(out bombController);
        if (abilitySystem == null) TryGetComponent(out abilitySystem);

        if (movement != null)
            tileSize = Mathf.Max(0.01f, movement.tileSize);

        explosionMask = LayerMask.GetMask("Explosion");
    }

    // =====================================================================
    // Emergency — nunca interfere na fuga.
    // =====================================================================
    public bool TryBuildEmergencyDecision(
        BattleModeComDifficultySettings settings,
        BattleModeComController controller,
        Vector2Int myTile,
        float currentDangerSeconds,
        out BattleModeComAbilityDecision decision)
    {
        decision = default;
        lastDecisionTrace = "emergency: stun punch is candidate-only";
        return false;
    }

    // =====================================================================
    // Candidate — soco de stun oportunista
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

        if (Time.time < nextAttemptTime)
        {
            lastDecisionTrace = $"candidate cooldown {(nextAttemptTime - Time.time):F2}s";
            return false;
        }

        // Não tenta stun se o próprio tile está ameaçado — fugir vem primeiro.
        if (!float.IsInfinity(GetDangerSeconds(myTile)))
        {
            lastDecisionTrace = "candidate own tile threatened";
            return false;
        }

        // Procura adversário num tile cardinal adjacente.
        if (!TryFindAdjacentEnemy(myTile, out Vector2Int punchDirection, out int enemyId))
        {
            lastDecisionTrace = "candidate no adjacent enemy";
            return false;
        }

        // Frequência por dificuldade.
        if (!RollStunChance(settings))
        {
            lastDecisionTrace = "candidate chance fail";
            return false;
        }

        nextAttemptTime = Time.time + DifficultyCooldown(settings);

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.CombatPlant,
            Weight = 600 + DifficultyWeight(settings),
            TargetTile = myTile + punchDirection,
            HasTarget = true,
            // Vira para o inimigo no mesmo frame do soco — o RedLouiePunchStun usa
            // movement.Direction/FacingDirection para definir a direção do golpe.
            FirstMove = new Vector2(punchDirection.x, punchDirection.y),
            Reason = $"redlouie-stun punch P{enemyId}",
            InputDescription = AppendInput(FirstMoveDescription(punchDirection), "ActionC"),
            TapActionC = true
        };

        lastDecisionTrace = $"candidate STUN dir:{punchDirection} enemy:P{enemyId}";
        LogSurgical("STUN_PUNCH",
            $"my:{myTile} dir:{FirstMoveDescription(punchDirection)} enemy:P{enemyId} " +
            $"chance:{DifficultyChance(settings):F2} cd:{DifficultyCooldown(settings):F1}s",
            force: true);
        return true;
    }

    // =====================================================================
    // Busca de alvo adjacente
    // =====================================================================
    private bool TryFindAdjacentEnemy(Vector2Int myTile, out Vector2Int direction, out int enemyId)
    {
        direction = Vector2Int.zero;
        enemyId = 0;
        float bestDistance = float.PositiveInfinity;

        activePlayers.Clear();
        PlayerIdentity.GetActivePlayers(activePlayers);

        Vector2 myWorld = movement != null
            ? (Vector2)movement.transform.position
            : (Vector2)transform.position;

        for (int i = 0; i < activePlayers.Count; i++)
        {
            PlayerIdentity player = activePlayers[i];
            if (player == null || player == identity)
                continue;

            if (!player.TryGetComponent<MovementController>(out var enemyMovement) ||
                enemyMovement == null || enemyMovement.isDead)
                continue;

            if (IsAlly(player.playerId))
                continue;

            Vector2 enemyWorld = player.transform.position;
            Vector2 delta = enemyWorld - myWorld;
            float distanceTiles = delta.magnitude / tileSize;
            if (distanceTiles > MaxPunchTileDistance)
                continue;

            // Direção cardinal dominante para o soco.
            Vector2Int dir = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? (delta.x >= 0f ? Vector2Int.right : Vector2Int.left)
                : (delta.y >= 0f ? Vector2Int.up : Vector2Int.down);

            if (distanceTiles < bestDistance)
            {
                bestDistance = distanceTiles;
                direction = dir;
                enemyId = player.playerId;
            }
        }

        return direction != Vector2Int.zero;
    }

    private bool IsAlly(int otherPlayerId)
    {
        if (BattleModeRules.Instance == null || !BattleModeRules.Instance.UsesTeams || identity == null)
            return false;

        return BattleModeRules.Instance.GetTeamForPlayer(identity.playerId) ==
               BattleModeRules.Instance.GetTeamForPlayer(otherPlayerId);
    }

    // =====================================================================
    // Chance / cooldown por dificuldade
    // =====================================================================
    private bool RollStunChance(BattleModeComDifficultySettings settings)
    {
        if (Time.time - chanceCacheTime < 0.001f)
            return chanceCacheResult;

        bool result = Random.value <= DifficultyChance(settings);
        chanceCacheTime = Time.time;
        chanceCacheResult = result;
        return result;
    }

    private static float DifficultyChance(BattleModeComDifficultySettings settings) =>
        settings.difficulty switch
        {
            BattleModeComputerLevel.Easy => 0.15f,
            BattleModeComputerLevel.Hard => 0.65f,
            _ => 0.35f
        };

    private static float DifficultyCooldown(BattleModeComDifficultySettings settings) =>
        settings.difficulty switch
        {
            BattleModeComputerLevel.Easy => 3.0f,
            BattleModeComputerLevel.Hard => 1.0f,
            _ => 2.0f
        };

    private static int DifficultyWeight(BattleModeComDifficultySettings settings) =>
        settings.difficulty switch
        {
            BattleModeComputerLevel.Easy => -20,
            BattleModeComputerLevel.Hard => 30,
            _ => 0
        };

    // =====================================================================
    // Perigo (modelo padrão)
    // =====================================================================
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

            Vector2Int delta = tile - bombTile;
            bool aligned = (delta.x == 0) != (delta.y == 0) || delta == Vector2Int.zero;
            if (!aligned)
                continue;

            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) > radius)
                continue;

            float seconds = bomb.IsControlBomb ? 0.65f : bomb.RemainingFuseSeconds;
            danger = Mathf.Min(danger, seconds);
        }

        return danger;
    }

    // =====================================================================
    // Utilitários / diagnóstico
    // =====================================================================
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

    private void LogSurgical(string key, string message, bool force = false)
    {
        if (!EnableRedLouieStunDiagnostics) return;

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
