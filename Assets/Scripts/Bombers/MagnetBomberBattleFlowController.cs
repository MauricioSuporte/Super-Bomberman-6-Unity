using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class MagnetBomberBattleFlowController : MonoBehaviour
{
    [Header("Enable")]
    [SerializeField] private bool enableFlow = true;

    [Header("Timings")]
    [SerializeField, Min(0f)] private float extraHoldAfterIntroSeconds = 3f;

    [Header("Boss (optional overrides)")]
    [Tooltip("If null, will auto-find a BossBomberAI in scene and grab its MovementController.")]
    [SerializeField] private MovementController boss;

    [Tooltip("If null, auto from boss.")]
    [SerializeField] private BossBomberAI bossAI;

    [Tooltip("If null, auto from boss.")]
    [SerializeField] private AIMovementController aiMove;

    [Header("Boss facing (optional)")]
    [Tooltip("Optional: force boss to face down/up/left/right while idle. Keep as (0,0) to not force a facing.")]
    [SerializeField] private Vector2 forceBossFacing = Vector2.zero;

    [Header("Players")]
    [Tooltip("If true, will also disable BombController during locks.")]
    [SerializeField] private bool disablePlayerBombControllerWhileLocked = true;

    [Tooltip("If true, will also disable PlayerManualDismount during locks.")]
    [SerializeField] private bool disablePlayerManualDismountWhileLocked = true;

    [Header("Debug")]
    private bool debugLogs = true;

    private readonly List<MovementController> players = new();
    private readonly List<BombController> playerBombs = new();
    private readonly List<PlayerManualDismount> manualDismounts = new();
    private readonly List<PlayerMountCompanion> playerCompanions = new();

    private int playersSafetyLocks;
    private readonly Dictionary<MovementController, Collider2D> cachedPlayerColliders = new();
    private readonly Dictionary<MovementController, bool> cachedColliderEnabled = new();

    private int bossSafetyLocks;
    private Collider2D cachedBossCollider;
    private bool cachedBossColliderEnabled;

    private bool _started;
    private bool _released;

    private void Awake()
    {
        if (!enableFlow) return;

        ResolveBossRefs();
        RefreshPlayersRefs();
    }

    private void Start()
    {
        if (!enableFlow) return;
        if (_started) return;
        _started = true;

        StartCoroutine(FlowRoutine());
    }

    private void ResolveBossRefs()
    {
        if (boss == null)
        {
            var foundAI = FindFirstObjectByType<BossBomberAI>();
            if (foundAI != null)
                boss = foundAI.GetComponent<MovementController>();
        }

        if (boss != null)
        {
            if (bossAI == null) bossAI = boss.GetComponent<BossBomberAI>();
            if (aiMove == null) aiMove = boss.GetComponent<AIMovementController>();
        }
    }

    private void RefreshPlayersRefs()
    {
        players.Clear();
        playerBombs.Clear();
        manualDismounts.Clear();
        playerCompanions.Clear();

        var ids = FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (ids != null && ids.Length > 0)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                var id = ids[i];
                if (id == null) continue;

                MovementController move = null;
                if (!id.TryGetComponent(out move))
                    move = id.GetComponentInChildren<MovementController>(true);

                if (move == null) continue;
                if (!move.gameObject.activeInHierarchy) continue;
                if (!move.CompareTag("Player")) continue;
                if (move.isDead) continue;

                players.Add(move);
            }
        }
        else
        {
            var moves = FindObjectsByType<MovementController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < moves.Length; i++)
            {
                var m = moves[i];
                if (m == null) continue;
                if (!m.gameObject.activeInHierarchy) continue;
                if (!m.CompareTag("Player")) continue;
                if (m.isDead) continue;

                players.Add(m);
            }
        }

        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p == null) continue;

            var b = p.GetComponent<BombController>();
            if (b != null) playerBombs.Add(b);

            var d = p.GetComponent<PlayerManualDismount>();
            if (d != null) manualDismounts.Add(d);

            var comp = p.GetComponent<PlayerMountCompanion>();
            if (comp != null) playerCompanions.Add(comp);
        }
    }

    private IEnumerator FlowRoutine()
    {
        RefreshPlayersRefs();
        PushPlayersSafety();
        PushBossSafety();

        try
        {
            LockPlayers(true);
            ForcePlayersIdleUp();
            LockBoss(true);

            if (StageIntroTransition.Instance != null)
            {
                while (StageIntroTransition.Instance.IntroRunning)
                {
                    if (!enableFlow) yield break;

                    RefreshPlayersRefs();

                    PushPlayersSafety();
                    LockPlayers(true);
                    ForcePlayersIdleUp();

                    PushBossSafety();
                    LockBoss(true);

                    yield return null;
                }
            }

            if (extraHoldAfterIntroSeconds > 0f)
            {
                float elapsed = 0f;
                while (elapsed < extraHoldAfterIntroSeconds)
                {
                    if (!enableFlow) yield break;

                    if (!GamePauseController.IsPaused)
                        elapsed += Time.deltaTime;

                    RefreshPlayersRefs();

                    PushPlayersSafety();
                    LockPlayers(true);
                    ForcePlayersIdleUp();

                    PushBossSafety();
                    LockBoss(true);

                    yield return null;
                }
            }

            ReleaseCombat();
        }
        finally
        {
            while (playersSafetyLocks > 0) PopPlayersSafety();
            while (bossSafetyLocks > 0) PopBossSafety();

            LockPlayers(false);
            UnlockBoss();
        }
    }

    private void ReleaseCombat()
    {
        if (_released) return;
        _released = true;

        RefreshPlayersRefs();

        while (playersSafetyLocks > 0) PopPlayersSafety();
        while (bossSafetyLocks > 0) PopBossSafety();

        LockPlayers(false);
        UnlockBoss();
    }

    private void LockPlayers(bool locked)
    {
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p == null) continue;

            p.SetInputLocked(locked, true);
            p.ApplyDirectionFromVector(Vector2.zero);

            if (p.Rigidbody != null)
                p.Rigidbody.linearVelocity = Vector2.zero;

            p.SetExplosionInvulnerable(locked);
        }

        if (disablePlayerBombControllerWhileLocked)
        {
            for (int i = 0; i < playerBombs.Count; i++)
            {
                var b = playerBombs[i];
                if (b == null) continue;
                b.enabled = !locked;
            }
        }

        if (disablePlayerManualDismountWhileLocked)
        {
            for (int i = 0; i < manualDismounts.Count; i++)
            {
                var d = manualDismounts[i];
                if (d == null) continue;
                d.enabled = !locked;
            }
        }

        for (int i = 0; i < playerCompanions.Count; i++)
        {
            var c = playerCompanions[i];
            if (c == null) continue;
            c.SetLouieAbilitiesLocked(locked);
        }
    }

    private void ForcePlayersIdleUp()
    {
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p == null) continue;

            if (p.IsMountedOnLouie)
                p.ForceMountedUpExclusive();
            else
                p.ForceIdleUp();
        }
    }

    private void LockBoss(bool locked)
    {
        ResolveBossRefs();
        if (boss == null) return;

        boss.SetInputLocked(locked, true);
        boss.ApplyDirectionFromVector(Vector2.zero);

        if (boss.Rigidbody != null)
            boss.Rigidbody.linearVelocity = Vector2.zero;

        if (locked && forceBossFacing.sqrMagnitude > 0.0001f)
            boss.ForceFacingDirection(forceBossFacing);

        if (bossAI != null) bossAI.enabled = !locked;
        if (aiMove != null) aiMove.enabled = !locked;

        if (aiMove != null)
            aiMove.SetIntroIdle(locked);

        boss.SetExplosionInvulnerable(locked);
    }

    private void UnlockBoss()
    {
        ResolveBossRefs();
        if (boss == null) return;

        boss.SetInputLocked(false, true);
        boss.ApplyDirectionFromVector(Vector2.zero);

        if (boss.Rigidbody != null)
            boss.Rigidbody.linearVelocity = Vector2.zero;

        if (aiMove != null)
            aiMove.SetIntroIdle(false);

        if (aiMove != null) aiMove.enabled = true;
        if (bossAI != null) bossAI.enabled = true;

        boss.SetExplosionInvulnerable(false);
    }

    private void PushPlayersSafety()
    {
        RefreshPlayersRefs();

        playersSafetyLocks++;
        if (playersSafetyLocks > 1)
            return;

        cachedPlayerColliders.Clear();
        cachedColliderEnabled.Clear();

        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p == null) continue;

            p.SetExplosionInvulnerable(true);
            p.SetInputLocked(true, false);

            var col = p.GetComponent<Collider2D>();
            if (col != null)
            {
                cachedPlayerColliders[p] = col;
                cachedColliderEnabled[p] = col.enabled;
                col.enabled = false;
            }

            if (p.TryGetComponent<CharacterHealth>(out var health) && health != null)
                health.StopInvulnerability();
        }
    }

    private void PopPlayersSafety()
    {
        RefreshPlayersRefs();

        if (playersSafetyLocks <= 0)
            return;

        playersSafetyLocks--;
        if (playersSafetyLocks > 0)
            return;

        foreach (var kv in cachedPlayerColliders)
        {
            var p = kv.Key;
            var col = kv.Value;

            if (p == null || col == null) continue;

            if (cachedColliderEnabled.TryGetValue(p, out bool wasEnabled))
                col.enabled = wasEnabled;
        }

        cachedPlayerColliders.Clear();
        cachedColliderEnabled.Clear();
    }

    private void PushBossSafety()
    {
        ResolveBossRefs();
        if (boss == null) return;

        bossSafetyLocks++;
        if (bossSafetyLocks > 1)
            return;

        boss.SetExplosionInvulnerable(true);
        boss.SetInputLocked(true, false);

        cachedBossCollider = boss.GetComponent<Collider2D>();
        if (cachedBossCollider != null)
        {
            cachedBossColliderEnabled = cachedBossCollider.enabled;
            cachedBossCollider.enabled = false;
        }

        if (boss.TryGetComponent<CharacterHealth>(out var health) && health != null)
            health.StopInvulnerability();
    }

    private void PopBossSafety()
    {
        ResolveBossRefs();
        if (boss == null) return;

        if (bossSafetyLocks <= 0)
            return;

        bossSafetyLocks--;
        if (bossSafetyLocks > 0)
            return;

        if (cachedBossCollider != null)
            cachedBossCollider.enabled = cachedBossColliderEnabled;

        cachedBossCollider = null;
    }
}