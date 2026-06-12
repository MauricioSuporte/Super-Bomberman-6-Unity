using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Ability de IA para usar o dash do Green Louie (GreenLouieDashAbility).
///
/// MECÂNICA (GreenLouieDashAbility.cs):
///   ActionC dispara um dash em LINHA RETA na direção do facing/movimento, com
///   velocidade movement.speed * 3.5, até colidir com algo (paredes, destrutíveis
///   sem DestructiblePass, bombas sem BombPass). Durante o dash o input fica
///   travado — não dá para abortar no meio.
///
/// COMPORTAMENTO DA IA:
///   1. FUGA (emergency) — quando em perigo, simula o corredor do dash nas 4
///      direções: se existe uma linha reta cujo percurso é seguro nas ETAs do
///      dash e cujo tile de parada é seguro, dispara o dash (muito mais rápido
///      que fugir andando).
///   2. MOVIMENTAÇÃO (candidate) — ocasionalmente usa o dash para se deslocar:
///      escolhe o corredor mais longo (>= MinTravelTiles) totalmente seguro.
///      Frequência por dificuldade.
///
///   A previsão do corredor espelha o IsBlocked do dash: anda tile a tile na
///   direção até um bloqueio, considerando os passes do próprio player.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerIdentity))]
[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(BombController))]
public sealed class BattleModeComGreenLouieDashAbility : MonoBehaviour, IBattleModeComAbility
{
    // === Filtro de diagnóstico ===
    public const int DiagnosticPlayerIdFilter = 0; // 0 = todos
    public static readonly bool EnableGreenLouieDashDiagnostics = true;
    private const float SurgicalLogIntervalSeconds = 0.35f;

    // === Constantes de comportamento ===
    // Multiplicador de velocidade do dash (espelha dashSpeedMultiplier).
    private const float DashSpeedMultiplier = 3.5f;
    // Comprimento máximo simulado do corredor.
    private const int MaxDashTiles = 14;
    // Mínimo de tiles para o dash de movimentação valer a pena.
    private const int MinTravelTiles = 3;
    // Mínimo de tiles para o dash de fuga (1 já tira do tile atual rapidamente,
    // mas exigimos 2 para garantir distância real do perigo).
    private const int MinEscapeTiles = 2;

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
    private float tileSize = 1f;
    private int explosionMask;

    // === Estado ===
    private float nextTravelDashTime = -10f;

    // === Cache de chance ===
    private float chanceCacheTime = -10f;
    private bool chanceCacheResult;

    // === Diagnóstico ===
    private string lastDecisionTrace = "not evaluated";
    private float lastSurgicalLogTime = -10f;
    private string lastSurgicalLogKey = string.Empty;

    // === IBattleModeComAbility ===
    public string DiagnosticName => "GreenLouieDash";
    public string LastDecisionTrace => lastDecisionTrace;

    public bool IsAvailable
    {
        get
        {
            CacheReferences();
            if (identity == null || movement == null || movement.isDead)
                return false;

            return abilitySystem != null &&
                   abilitySystem.IsEnabled(GreenLouieDashAbility.AbilityId);
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

        if (movement != null)
            tileSize = Mathf.Max(0.01f, movement.tileSize);

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
    // Emergency — dash de fuga
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
            return false;
        }

        // Procura o melhor corredor de fuga: percurso seguro nas ETAs do dash e
        // tile de parada totalmente seguro.
        if (!TryFindBestDashCorridor(settings, myTile, MinEscapeTiles, requireSafeStop: true,
                out Vector2Int dir, out Vector2Int stopTile, out int length))
        {
            lastDecisionTrace = "emergency no safe dash corridor";
            return false;
        }

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.Reposition,
            Weight = 320 + DifficultyWeight(settings),
            TargetTile = stopTile,
            HasTarget = true,
            FirstMove = new Vector2(dir.x, dir.y),
            Reason = "greenlouie-dash escape",
            InputDescription = AppendInput(FirstMoveDescription(dir), "ActionC"),
            TapActionC = true
        };

        lastDecisionTrace = $"emergency DASH dir:{dir} stop:{stopTile} len:{length}";
        LogSurgical("DASH_ESCAPE",
            $"my:{myTile} dir:{FirstMoveDescription(dir)} stop:{stopTile} len:{length} " +
            $"danger:{FormatDanger(currentDangerSeconds)}",
            force: true);
        return true;
    }

    // =====================================================================
    // Candidate — dash de movimentação ocasional
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

        if (Time.time < nextTravelDashTime)
        {
            lastDecisionTrace = $"candidate cooldown {(nextTravelDashTime - Time.time):F2}s";
            return false;
        }

        if (!RollTravelChance(settings))
        {
            lastDecisionTrace = "candidate chance fail";
            return false;
        }

        if (!TryFindBestDashCorridor(settings, myTile, MinTravelTiles, requireSafeStop: true,
                out Vector2Int dir, out Vector2Int stopTile, out int length))
        {
            lastDecisionTrace = "candidate no safe dash corridor";
            return false;
        }

        nextTravelDashTime = Time.time + DifficultyCooldown(settings);

        decision = new BattleModeComAbilityDecision
        {
            Action = BattleModeComActionType.Reposition,
            Weight = settings.patrolWeight + 50 + DifficultyWeight(settings),
            TargetTile = stopTile,
            HasTarget = true,
            FirstMove = new Vector2(dir.x, dir.y),
            Reason = "greenlouie-dash travel",
            InputDescription = AppendInput(FirstMoveDescription(dir), "ActionC"),
            TapActionC = true
        };

        lastDecisionTrace = $"candidate DASH dir:{dir} stop:{stopTile} len:{length}";
        LogSurgical("DASH_TRAVEL",
            $"my:{myTile} dir:{FirstMoveDescription(dir)} stop:{stopTile} len:{length} " +
            $"chance:{DifficultyChance(settings):F2}");
        return true;
    }

    // =====================================================================
    // Simulação do corredor de dash
    // =====================================================================

    /// <summary>
    /// Avalia as 4 direções e devolve o corredor de dash mais longo cujo percurso
    /// inteiro é seguro nas ETAs do dash. requireSafeStop exige perigo infinito no
    /// tile de parada (sempre true — parar dentro de blast é morte).
    /// </summary>
    private bool TryFindBestDashCorridor(
        BattleModeComDifficultySettings settings,
        Vector2Int myTile,
        int minLength,
        bool requireSafeStop,
        out Vector2Int bestDir,
        out Vector2Int bestStop,
        out int bestLength)
    {
        bestDir = Vector2Int.zero;
        bestStop = myTile;
        bestLength = 0;

        for (int d = 0; d < CardinalTiles.Length; d++)
        {
            Vector2Int dir = CardinalTiles[d];
            if (!TrySimulateDash(settings, myTile, dir, requireSafeStop,
                    out Vector2Int stopTile, out int length))
                continue;

            if (length < minLength)
                continue;

            if (length > bestLength)
            {
                bestDir = dir;
                bestStop = stopTile;
                bestLength = length;
            }
        }

        return bestDir != Vector2Int.zero;
    }

    /// <summary>
    /// Simula o dash numa direção: anda tile a tile até o bloqueio (espelhando o
    /// IsBlocked do GreenLouieDashAbility, com os passes do player) e valida que
    /// cada tile do percurso é seguro na ETA do dash. O tile de parada é o último
    /// tile livre antes do bloqueio.
    /// </summary>
    private bool TrySimulateDash(
        BattleModeComDifficultySettings settings,
        Vector2Int start,
        Vector2Int dir,
        bool requireSafeStop,
        out Vector2Int stopTile,
        out int length)
    {
        stopTile = start;
        length = 0;

        float dashTilesPerSecond =
            Mathf.Max(1f, (movement != null ? movement.speed : 4f) * DashSpeedMultiplier);

        for (int step = 1; step <= MaxDashTiles; step++)
        {
            Vector2Int tile = start + dir * step;

            if (DashBlockedAt(tile))
                break;

            float eta = step / dashTilesPerSecond;

            // Cada tile atravessado precisa ser seguro no momento da passagem.
            if (IsDangerousAt(tile, eta, settings))
            {
                // Perigo no caminho: corredor inválido a partir daqui — o dash
                // não pode ser abortado no meio, então rejeita a direção inteira
                // se ainda não alcançou um comprimento útil seguro.
                break;
            }

            stopTile = tile;
            length = step;
        }

        if (length <= 0)
            return false;

        // O dash para no último tile livre; é ali que a IA vai ficar.
        if (requireSafeStop)
        {
            float stopEta = length / dashTilesPerSecond;
            if (!float.IsInfinity(GetDangerSeconds(stopTile)) ||
                IsDangerousAt(stopTile, stopEta + settings.safeTileMinimumSeconds, settings))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Espelha o IsBlocked do dash em nível de tile: paredes/indestrutíveis sempre
    /// bloqueiam; destrutíveis bloqueiam sem DestructiblePass; bombas bloqueiam
    /// sem BombPass; outros players NÃO bloqueiam o dash.
    /// </summary>
    private bool DashBlockedAt(Vector2Int tile)
    {
        if (!HasGroundTile(tile))
            return true;

        if (HasIndestructibleTile(tile))
            return true;

        if (HasDestructibleTile(tile) && !CanPassDestructibles)
            return true;

        if (FindBombAt(tile) != null && !CanPassBombs)
            return true;

        return false;
    }

    // =====================================================================
    // Chance / cooldown por dificuldade
    // =====================================================================
    private bool RollTravelChance(BattleModeComDifficultySettings settings)
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
            BattleModeComputerLevel.Easy => 0.10f,
            BattleModeComputerLevel.Hard => 0.45f,
            _ => 0.25f
        };

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
    // Perigo
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

    private static string FormatDanger(float seconds)
    {
        if (float.IsInfinity(seconds)) return "safe";
        if (seconds <= 0f) return "now";
        return $"{seconds:F2}";
    }

    private void LogSurgical(string key, string message, bool force = false)
    {
        if (!EnableGreenLouieDashDiagnostics) return;

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
