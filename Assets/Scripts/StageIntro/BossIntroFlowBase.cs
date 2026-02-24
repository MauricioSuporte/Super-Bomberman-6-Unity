using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BossIntroFlowBase : MonoBehaviour
{
    [Header("Flow")]
    [SerializeField] protected bool enableFlow = true;
    [SerializeField, Min(0f)] protected float extraHoldAfterIntroSeconds = 0f;

    [Header("Players")]
    [SerializeField] protected bool disablePlayerBombControllerWhileLocked = true;

    protected readonly List<MovementController> players = new();
    protected readonly List<BombController> playerBombs = new();
    protected readonly List<PlayerMountCompanion> playerCompanions = new();

    int playersSafetyLocks;

    readonly Dictionary<MovementController, Collider2D> cachedPlayerColliders = new();
    readonly Dictionary<MovementController, bool> cachedColliderEnabled = new();

    bool started;
    bool released;

    protected virtual void Awake()
    {
        if (!enableFlow) return;
        RefreshPlayersRefs();
    }

    protected virtual void Start()
    {
        if (!enableFlow) return;
        if (started) return;
        started = true;

        StartCoroutine(FlowRoutine());
    }

    protected virtual IEnumerator FlowRoutine()
    {
        RefreshPlayersRefs();
        PushPlayersSafety();

        try
        {
            OnIntroStarted();

            RefreshPlayersRefs();
            MaintainPlayersSafety();
            LockPlayers(true);
            ForcePlayersIdleUp();
            LockBoss(true);

            if (StageIntroTransition.Instance != null)
            {
                while (StageIntroTransition.Instance.IntroRunning)
                {
                    if (!enableFlow) yield break;

                    RefreshPlayersRefs();
                    MaintainPlayersSafety();
                    LockPlayers(true);
                    ForcePlayersIdleUp();
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
                    MaintainPlayersSafety();
                    LockPlayers(true);
                    ForcePlayersIdleUp();
                    LockBoss(true);

                    yield return null;
                }
            }

            ReleaseCombat();
        }
        finally
        {
            while (playersSafetyLocks > 0)
                PopPlayersSafety();

            LockPlayers(false);
            UnlockBoss();

            OnIntroFinished();
        }
    }

    protected void ReleaseCombat()
    {
        if (released) return;
        released = true;

        while (playersSafetyLocks > 0)
            PopPlayersSafety();

        LockPlayers(false);
        UnlockBoss();
    }

    #region Players

    protected void RefreshPlayersRefs()
    {
        players.Clear();
        playerBombs.Clear();
        playerCompanions.Clear();

        var ids = FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < ids.Length; i++)
        {
            var id = ids[i];
            if (!id) continue;

            if (!id.TryGetComponent(out MovementController move))
                move = id.GetComponentInChildren<MovementController>(true);

            if (!move) continue;
            if (!move.CompareTag("Player")) continue;
            if (move.isDead) continue;

            players.Add(move);

            if (move.TryGetComponent(out BombController bomb))
                playerBombs.Add(bomb);

            if (move.TryGetComponent(out PlayerMountCompanion comp))
                playerCompanions.Add(comp);
        }
    }

    protected void LockPlayers(bool locked)
    {
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (!p) continue;

            p.SetInputLocked(locked, true);
            p.ApplyDirectionFromVector(Vector2.zero);
            p.SetExplosionInvulnerable(locked);

            if (p.Rigidbody != null)
                p.Rigidbody.linearVelocity = Vector2.zero;
        }

        if (disablePlayerBombControllerWhileLocked)
        {
            for (int i = 0; i < playerBombs.Count; i++)
                if (playerBombs[i])
                    playerBombs[i].enabled = !locked;
        }

        for (int i = 0; i < playerCompanions.Count; i++)
            if (playerCompanions[i])
                playerCompanions[i].SetLouieAbilitiesLocked(locked);
    }

    protected void ForcePlayersIdleUp()
    {
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (!p) continue;

            if (p.IsMountedOnLouie)
                p.ForceMountedUpExclusive();
            else
                p.ForceIdleUp();
        }
    }

    protected void PushPlayersSafety()
    {
        playersSafetyLocks++;

        if (playersSafetyLocks == 1)
        {
            cachedPlayerColliders.Clear();
            cachedColliderEnabled.Clear();
        }

        MaintainPlayersSafety();
    }

    protected void MaintainPlayersSafety()
    {
        if (playersSafetyLocks <= 0)
            return;

        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (!p) continue;

            p.SetExplosionInvulnerable(true);
            p.SetInputLocked(true, false);

            if (!cachedPlayerColliders.ContainsKey(p))
            {
                var col = p.GetComponent<Collider2D>();
                if (col)
                {
                    cachedPlayerColliders[p] = col;
                    cachedColliderEnabled[p] = col.enabled;
                    col.enabled = false;
                }
            }
            else
            {
                var col = cachedPlayerColliders[p];
                if (col) col.enabled = false;
            }

            if (p.TryGetComponent<CharacterHealth>(out var health))
                health.StopInvulnerability();
        }
    }

    protected void PopPlayersSafety()
    {
        if (playersSafetyLocks <= 0)
            return;

        playersSafetyLocks--;
        if (playersSafetyLocks > 0)
            return;

        foreach (var kv in cachedPlayerColliders)
        {
            var p = kv.Key;
            var col = kv.Value;
            if (!p || !col) continue;

            col.enabled = true;
        }

        cachedPlayerColliders.Clear();
        cachedColliderEnabled.Clear();
    }

    #endregion

    #region Boss Hooks

    protected abstract void LockBoss(bool locked);
    protected abstract void UnlockBoss();

    protected virtual void OnIntroStarted() { }
    protected virtual void OnIntroFinished() { }

    #endregion
}