using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterHealth))]
[RequireComponent(typeof(MovementControllerAI))]
[RequireComponent(typeof(MovementController))]
public class BossEscapeOnLastLife : MonoBehaviour
{
    [Header("Boss Refs")]
    [SerializeField] private MovementController boss;
    [SerializeField] private MovementControllerAI aiMove;
    [SerializeField] private MonoBehaviour bossAIToDisable;

    [Header("Escape")]
    [SerializeField] private Vector2 escapeTarget = new(-3f, 2f);
    [SerializeField] private float reachEpsilon = 0.06f;
    [SerializeField, Min(0f)] private float waitBeforeEscapeSeconds = 2f;

    [Header("Finish Behavior")]
    [SerializeField] private bool destroyBossGameObject = false;
    [SerializeField] private bool snapToGoalOnFinish = true;

    [Header("Immediate Finish")]
    [SerializeField] private float immediateFinishDistance = 0.4f;

    [Header("Pathfinding")]
    [SerializeField] private int maxNodes = 4096;
    [SerializeField] private int maxExpandSteps = 20000;

    [Header("Players Lock")]
    [SerializeField] private bool disablePlayerBombControllerWhileLocked = true;
    [SerializeField] private bool disablePlayerCollidersWhileLocked = true;

    private CharacterHealth health;

    private bool escapeArmed;
    private bool escapeStarted;
    private bool escapeFinished;

    private readonly List<MovementController> players = new();
    private readonly List<BombController> playerBombs = new();
    private readonly Dictionary<MovementController, Collider2D> cachedPlayerColliders = new();
    private readonly Dictionary<MovementController, bool> cachedColliderEnabled = new();

    private Vector2 goal;

    private readonly GridAStarPathfinder2D pathfinder = new();

    private void Awake()
    {
        if (!boss) boss = GetComponent<MovementController>();
        if (!aiMove) aiMove = GetComponent<MovementControllerAI>();
        health = GetComponent<CharacterHealth>();
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.Damaged += OnDamaged;
            health.HitInvulnerabilityEnded += OnHitInvulnerabilityEnded;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Damaged -= OnDamaged;
            health.HitInvulnerabilityEnded -= OnHitInvulnerabilityEnded;
        }

        UnlockPlayers();
    }

    private void OnDamaged(int amount)
    {
        if (escapeStarted || escapeFinished) return;
        if (!health) return;

        if (health.life == 1)
            escapeArmed = true;
    }

    private void OnHitInvulnerabilityEnded()
    {
        if (escapeStarted || escapeFinished) return;
        if (!escapeArmed) return;

        escapeStarted = true;
        StartCoroutine(EscapeRoutine());
    }

    private IEnumerator EscapeRoutine()
    {
        if (!boss) yield break;

        RefreshPlayersRefs();
        LockPlayers();

        LockBossForEscape();

        var gate = FindFirstObjectByType<EndStageGateAnimated>();
        if (gate != null) gate.ForceUnlock();

        if (waitBeforeEscapeSeconds > 0f)
        {
            float elapsed = 0f;
            while (elapsed < waitBeforeEscapeSeconds)
            {
                if (aiMove) aiMove.SetAIDirection(Vector2.zero);
                if (boss.Rigidbody != null) boss.Rigidbody.linearVelocity = Vector2.zero;

                if (!GamePauseController.IsPaused)
                    elapsed += Time.deltaTime;

                yield return null;
            }
        }

        BombController.ExplodeAllControlBombsInStage();
        yield return null;

        float tile = Mathf.Max(0.0001f, boss.tileSize);

        Vector2 start = RoundToGrid(GetBossPos(), tile);
        goal = RoundToGrid(escapeTarget, tile);

        if (IsAt(goal))
        {
            FinalizeEscape(goal);
            yield break;
        }

        List<Vector2> path = pathfinder.FindPath(
            start,
            goal,
            tile,
            boss.obstacleMask,
            gameObject,
            maxNodes,
            maxExpandSteps,
            overlapBoxScale: 0.6f);

        if (path == null || path.Count == 0)
        {
            yield return StartCoroutine(FallbackWalkTowards(goal));
            if (!escapeFinished) FinalizeEscape(goal);
            yield break;
        }

        for (int i = 1; i < path.Count; i++)
        {
            if (escapeFinished) yield break;
            yield return MoveToTile(path[i], tile);
        }

        if (!escapeFinished)
            FinalizeEscape(goal);
    }

    private void FinalizeEscape(Vector2 finalGoal)
    {
        if (escapeFinished) return;
        escapeFinished = true;

        StopAllCoroutines();

        if (aiMove) aiMove.SetAIDirection(Vector2.zero);
        if (boss != null && boss.Rigidbody != null) boss.Rigidbody.linearVelocity = Vector2.zero;

        if (snapToGoalOnFinish && boss)
        {
            if (boss.Rigidbody != null)
            {
                boss.Rigidbody.position = finalGoal;
                boss.Rigidbody.linearVelocity = Vector2.zero;
            }
            else
            {
                boss.transform.position = finalGoal;
            }
        }

        UnlockPlayers();

        if (destroyBossGameObject)
        {
            if (boss) Destroy(boss.gameObject);
        }
        else
        {
            if (boss) boss.gameObject.SetActive(false);
        }
    }

    private void LockBossForEscape()
    {
        if (bossAIToDisable) bossAIToDisable.enabled = false;
        if (aiMove) aiMove.enabled = true;

        boss.SetInputLocked(false, true);
        boss.SetExplosionInvulnerable(true);

        if (boss.Rigidbody != null)
            boss.Rigidbody.linearVelocity = Vector2.zero;

        if (health != null)
        {
            health.StopInvulnerability();
            health.SetExternalInvulnerability(true);
        }

        if (aiMove) aiMove.SetAIDirection(Vector2.zero);
    }

    private IEnumerator MoveToTile(Vector2 tileCenter, float tile)
    {
        float maxTime = 6f;
        float t = 0f;

        Vector2 startPos = GetBossPos();
        Vector2 delta0 = tileCenter - startPos;

        bool vertical = Mathf.Abs(delta0.y) >= Mathf.Abs(delta0.x);
        Vector2 desiredDir = vertical
            ? new Vector2(0f, Mathf.Sign(delta0.y))
            : new Vector2(Mathf.Sign(delta0.x), 0f);

        if (desiredDir == Vector2.zero)
            desiredDir = vertical ? Vector2.up : Vector2.right;

        float targetCoord = vertical ? tileCenter.y : tileCenter.x;
        float dirSign = vertical ? desiredDir.y : desiredDir.x;

        float stepEps = Mathf.Max(reachEpsilon, tile * 0.08f);

        while (t < maxTime)
        {
            if (escapeFinished) yield break;

            if (GamePauseController.IsPaused)
            {
                if (aiMove) aiMove.SetAIDirection(Vector2.zero);
                yield return null;
                continue;
            }

            Vector2 pos = GetBossPos();

            float distToGoal = Vector2.Distance(pos, goal);
            if (distToGoal <= immediateFinishDistance)
            {
                SnapBossTo(goal);
                FinalizeEscape(goal);
                yield break;
            }

            float remaining = vertical ? (targetCoord - pos.y) : (targetCoord - pos.x);
            float absRemaining = Mathf.Abs(remaining);

            if (absRemaining <= stepEps || Mathf.Sign(remaining) != Mathf.Sign(dirSign))
            {
                SnapBossTo(tileCenter);
                break;
            }

            if (aiMove) aiMove.SetAIDirection(desiredDir);

            t += Time.deltaTime;
            yield return null;
        }

        if (aiMove) aiMove.SetAIDirection(Vector2.zero);
        yield return null;
    }

    private IEnumerator FallbackWalkTowards(Vector2 targetGoal)
    {
        float maxTime = 8f;
        float t = 0f;

        while (t < maxTime)
        {
            if (escapeFinished) yield break;

            if (GamePauseController.IsPaused)
            {
                if (aiMove) aiMove.SetAIDirection(Vector2.zero);
                yield return null;
                continue;
            }

            Vector2 pos = GetBossPos();
            Vector2 delta = targetGoal - pos;

            if (delta.sqrMagnitude <= (reachEpsilon * reachEpsilon))
                break;

            Vector2 dir = PickCardinal(delta);
            if (aiMove) aiMove.SetAIDirection(dir);

            t += Time.deltaTime;
            yield return null;
        }

        if (aiMove) aiMove.SetAIDirection(Vector2.zero);
    }

    private bool IsAt(Vector2 targetGoal)
        => (targetGoal - GetBossPos()).sqrMagnitude <= (reachEpsilon * reachEpsilon);

    private Vector2 GetBossPos()
    {
        if (boss != null)
        {
            if (boss.Rigidbody != null) return boss.Rigidbody.position;
            return (Vector2)boss.transform.position;
        }
        return (Vector2)transform.position;
    }

    private static Vector2 PickCardinal(Vector2 delta)
    {
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            return new Vector2(Mathf.Sign(delta.x), 0f);
        return new Vector2(0f, Mathf.Sign(delta.y));
    }

    private static Vector2 RoundToGrid(Vector2 p, float tile)
        => new Vector2(Mathf.Round(p.x / tile) * tile, Mathf.Round(p.y / tile) * tile);

    private void SnapBossTo(Vector2 worldPos)
    {
        if (boss == null) return;

        if (boss.Rigidbody != null)
        {
            boss.Rigidbody.position = worldPos;
            boss.Rigidbody.linearVelocity = Vector2.zero;
        }
        else
        {
            boss.transform.position = worldPos;
        }
    }

    private void RefreshPlayersRefs()
    {
        players.Clear();
        playerBombs.Clear();

        var ids = FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (var id in ids)
        {
            if (!id) continue;

            if (!id.TryGetComponent(out MovementController move))
                move = id.GetComponentInChildren<MovementController>(true);

            if (!move) continue;
            if (!move.CompareTag("Player")) continue;
            if (move.isDead) continue;

            players.Add(move);

            if (move.TryGetComponent(out BombController bomb))
                playerBombs.Add(bomb);
        }
    }

    private void LockPlayers()
    {
        cachedPlayerColliders.Clear();
        cachedColliderEnabled.Clear();

        foreach (var p in players)
        {
            if (!p) continue;

            p.SetInputLocked(true, true);
            p.ApplyDirectionFromVector(Vector2.zero);
            p.SetExplosionInvulnerable(true);

            if (p.Rigidbody != null)
                p.Rigidbody.linearVelocity = Vector2.zero;

            if (p.TryGetComponent<CharacterHealth>(out var ph))
            {
                ph.StopInvulnerability();
                ph.SetExternalInvulnerability(true);
            }

            if (disablePlayerCollidersWhileLocked)
            {
                var col = p.GetComponent<Collider2D>();
                if (col)
                {
                    cachedPlayerColliders[p] = col;
                    cachedColliderEnabled[p] = col.enabled;
                    col.enabled = false;
                }
            }
        }

        if (disablePlayerBombControllerWhileLocked)
        {
            foreach (var bc in playerBombs)
                if (bc) bc.enabled = false;
        }
    }

    private void UnlockPlayers()
    {
        foreach (var p in players)
        {
            if (!p) continue;

            p.SetInputLocked(false, true);
            p.SetExplosionInvulnerable(false);

            if (p.TryGetComponent<CharacterHealth>(out var ph))
                ph.SetExternalInvulnerability(false);
        }

        foreach (var kv in cachedPlayerColliders)
        {
            if (kv.Key && kv.Value && cachedColliderEnabled.TryGetValue(kv.Key, out bool wasEnabled))
                kv.Value.enabled = wasEnabled;
        }

        cachedPlayerColliders.Clear();
        cachedColliderEnabled.Clear();

        if (disablePlayerBombControllerWhileLocked)
        {
            foreach (var bc in playerBombs)
                if (bc) bc.enabled = true;
        }
    }
}