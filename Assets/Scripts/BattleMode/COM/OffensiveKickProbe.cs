using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Probe cirúrgico para gravar a "jogada" do chute ofensivo de bomba executada
/// manualmente por um jogador humano (por padrão o Player 1).
///
/// IMPORTANTE: os jogadores do battle mode são instanciados em runtime pelo
/// PlayersSpawner, então este componente NÃO precisa (e não deve) estar no GameObject
/// do player. Coloque-o em qualquer objeto persistente da cena (ex: GameSession) que ele
/// localiza o player alvo pelo playerId em runtime e o re-localiza a cada round/respawn.
///
/// Não altera nenhum sistema existente: apenas observa, por polling, o estado público
/// do jogador e das bombas e emite logs compactos em uma linha cada.
///
/// Eventos capturados (configuráveis):
///  - PLANT  : quando o jogador planta uma bomba (tile da bomba, direção que olhava, raio).
///  - DIR    : cada mudança de direção enquanto há bomba própria viva no campo
///             (revela o padrão recuar/voltar/entrar-na-bomba que dispara o chute).
///  - SEQEND : quando todas as bombas próprias do jogador somem do campo (fim da janela).
///  - KICK   : (opcional, desligado por padrão) momento em que uma bomba própria começa
///             a ser chutada, com geometria e inimigo alinhado mais próximo.
/// </summary>
[DisallowMultipleComponent]
public sealed class OffensiveKickProbe : MonoBehaviour
{
    [Header("Alvo")]
    [Tooltip("Qual playerId observar (o player é localizado em runtime pelo PlayerIdentity).")]
    [SerializeField, Range(1, 6)] private int probePlayerId = 1;

    [Tooltip("Só loga quando esse player estiver no modo humano (Man). Desligue para logar também a COM.")]
    [SerializeField] private bool onlyWhenHuman = true;

    [Header("Eventos")]
    [Tooltip("Loga quando o jogador planta uma bomba (tile + facing + raio).")]
    [SerializeField] private bool logPlant = true;

    [Tooltip("Loga cada mudança de direção do jogador.")]
    [SerializeField] private bool logDirectionChanges = true;

    [Tooltip("Só loga mudanças de direção enquanto houver bomba própria viva no campo (mantém o log enxuto).")]
    [SerializeField] private bool onlyWhileArmed = true;

    [Tooltip("(Opcional) Loga o instante exato em que uma bomba própria começa a ser chutada + inimigo alinhado.")]
    [SerializeField] private bool logKickFire = false;

    [Header("Diagnóstico")]
    [Tooltip("Loga uma linha quando o probe conecta/perde o player alvo (útil para confirmar que está ativo).")]
    [SerializeField] private bool logBinding = true;

    private const int MaxOffensiveAlignDistance = 10;

    private PlayerIdentity identity;
    private MovementController movement;
    private BombController bombController;

    private readonly HashSet<Bomb> knownOwnBombs = new();
    private readonly HashSet<Bomb> kickingLogged = new();
    private Vector2 lastFacing = Vector2.zero;
    private bool wasArmed;
    private bool boundLastFrame;

    private void Update()
    {
        if (!IsBattleModeScene() || GamePauseController.IsPaused)
            return;

        if (!EnsureBoundPlayer())
            return;

        int id = probePlayerId;

        if (onlyWhenHuman &&
            SaveSystem.GetBattleModePlayerControlMode(id) != BattleModePlayerControlMode.Man)
        {
            return;
        }

        if (movement.isDead || movement.InputLocked)
            return;

        Vector2Int myTile = WorldToTile(movement.transform.position);

        bool armed = RefreshOwnBombs(id, myTile);

        if (logDirectionChanges)
            HandleDirectionChange(id, myTile, armed);

        if (logKickFire)
            HandleKickFire(id, myTile);

        wasArmed = armed;
    }

    /// <summary>
    /// Garante que temos referência viva ao player alvo. Localiza pelo playerId em runtime
    /// e re-localiza após respawn (novo round). Retorna false enquanto o player não existe.
    /// </summary>
    private bool EnsureBoundPlayer()
    {
        // Referência ainda válida? (Unity sobrecarrega == para objetos destruídos.)
        if (movement != null && bombController != null && identity != null)
            return true;

        identity = null;
        movement = null;
        bombController = null;

        PlayerIdentity[] players = FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerIdentity player = players[i];
            if (player == null || player.playerId != probePlayerId)
                continue;

            if (!player.TryGetComponent(out MovementController mv) ||
                !player.TryGetComponent(out BombController bc))
                continue;

            identity = player;
            movement = mv;
            bombController = bc;
            break;
        }

        bool bound = movement != null && bombController != null;

        if (bound && !boundLastFrame)
        {
            // Reset de estado para o player recém-localizado (início de round).
            knownOwnBombs.Clear();
            kickingLogged.Clear();
            lastFacing = Vector2.zero;
            wasArmed = false;

            if (logBinding)
                Debug.Log($"[OffKickProbe][P{probePlayerId}] bound to '{identity.name}' (player encontrado)", this);
        }
        else if (!bound && boundLastFrame && logBinding)
        {
            Debug.Log($"[OffKickProbe][P{probePlayerId}] player perdido (respawn/round) — re-localizando", this);
        }

        boundLastFrame = bound;
        return bound;
    }

    /// <summary>
    /// Atualiza o conjunto de bombas próprias vivas, emitindo PLANT para novas e SEQEND
    /// quando a janela ofensiva termina. Retorna true se há ao menos uma bomba própria viva.
    /// </summary>
    private bool RefreshOwnBombs(int id, Vector2Int myTile)
    {
        foreach (Bomb bomb in Bomb.ActiveBombs)
        {
            if (bomb == null || bomb.HasExploded)
                continue;

            if (bomb.Owner != bombController)
                continue;

            if (knownOwnBombs.Add(bomb) && logPlant)
            {
                Vector2Int bombTile = WorldToTile(bomb.GetLogicalPosition());
                int radius = bomb.Owner != null ? Mathf.Max(1, bomb.Owner.explosionRadius) : 1;
                Log(id, myTile,
                    $"PLANT bomb@{bombTile} facing={DirLabel(movement.FacingDirection)} radius={radius}");
            }
        }

        knownOwnBombs.RemoveWhere(b => b == null || b.HasExploded || b.Owner != bombController);
        kickingLogged.RemoveWhere(b => b == null || b.HasExploded || !b.IsBeingKicked);

        bool armed = knownOwnBombs.Count > 0;

        if (!armed && wasArmed)
            Log(id, myTile, "SEQEND no own bombs on field");

        return armed;
    }

    private void HandleDirectionChange(int id, Vector2Int myTile, bool armed)
    {
        Vector2 facing = movement.FacingDirection;
        if (facing == Vector2.zero || facing == lastFacing)
            return;

        Vector2 previous = lastFacing;
        lastFacing = facing;

        if (onlyWhileArmed && !armed)
            return;

        // Marca quando a direção aponta para uma bomba própria adjacente: é o gesto que
        // dispara o chute ofensivo.
        Vector2Int facingTile = DirToTile(facing);
        bool intoOwnBomb = facingTile != Vector2Int.zero && IsOwnBombAt(myTile + facingTile);

        Log(id, myTile,
            $"DIR {DirLabel(previous)}->{DirLabel(facing)} intoOwnBomb={intoOwnBomb}");
    }

    private void HandleKickFire(int id, Vector2Int myTile)
    {
        foreach (Bomb bomb in knownOwnBombs)
        {
            if (bomb == null || bomb.HasExploded || !bomb.IsBeingKicked)
                continue;

            if (!kickingLogged.Add(bomb))
                continue;

            Vector2Int bombTile = WorldToTile(bomb.GetLogicalPosition());

            string enemyInfo = "enemy=none";
            if (TryFindNearestAlignedEnemy(bombTile, out int enemyId, out Vector2Int enemyTile, out int dist))
                enemyInfo = $"enemy=P{enemyId}@{enemyTile} aligned dist={dist}";

            Log(id, myTile,
                $"KICK standTile={myTile} bombTile={bombTile} dir={DirLabel(movement.FacingDirection)} {enemyInfo}");
        }
    }

    private bool TryFindNearestAlignedEnemy(
        Vector2Int origin,
        out int enemyId,
        out Vector2Int enemyTile,
        out int distance)
    {
        enemyId = 0;
        enemyTile = origin;
        distance = int.MaxValue;

        PlayerIdentity[] players = FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerIdentity player = players[i];
            if (player == null || player == identity)
                continue;

            if (player.TryGetComponent<CharacterHealth>(out var health) && health.life <= 0)
                continue;

            Vector2Int tile = WorldToTile(player.transform.position);
            Vector2Int delta = tile - origin;
            bool aligned = (delta.x == 0) ^ (delta.y == 0);
            if (!aligned)
                continue;

            int manhattan = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
            if (manhattan <= 0 || manhattan > MaxOffensiveAlignDistance || manhattan >= distance)
                continue;

            distance = manhattan;
            enemyId = player.playerId;
            enemyTile = tile;
        }

        return distance != int.MaxValue;
    }

    private bool IsOwnBombAt(Vector2Int tile)
    {
        foreach (Bomb bomb in knownOwnBombs)
        {
            if (bomb == null || bomb.HasExploded)
                continue;

            if (WorldToTile(bomb.GetLogicalPosition()) == tile)
                return true;
        }

        return false;
    }

    private Vector2Int WorldToTile(Vector2 world)
    {
        float size = movement != null ? Mathf.Max(0.01f, movement.tileSize) : 1f;
        return new Vector2Int(
            Mathf.RoundToInt(world.x / size),
            Mathf.RoundToInt(world.y / size));
    }

    private static Vector2Int DirToTile(Vector2 dir)
    {
        if (dir == Vector2.zero)
            return Vector2Int.zero;

        if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
            return new Vector2Int(dir.x > 0 ? 1 : -1, 0);

        return new Vector2Int(0, dir.y > 0 ? 1 : -1);
    }

    private static string DirLabel(Vector2 dir)
    {
        if (dir == Vector2.zero)
            return "None";

        if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
            return dir.x > 0 ? "Right" : "Left";

        return dir.y > 0 ? "Up" : "Down";
    }

    private void Log(int id, Vector2Int myTile, string message)
    {
        Debug.Log($"[OffKickProbe][P{id}] t={Time.time:F2} f={Time.frameCount} tile={myTile} {message}", this);
    }

    private static bool IsBattleModeScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return sceneName.StartsWith("BattleMode_", StringComparison.OrdinalIgnoreCase);
    }
}
