using Assets.Scripts.Interface;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(CharacterHealth))]
public class PlayerLouieCompanion : MonoBehaviour
{
    [Header("Louie Prefabs")]
    public GameObject blueLouiePrefab;
    public GameObject blackLouiePrefab;
    public GameObject purpleLouiePrefab;
    public GameObject greenLouiePrefab;
    public GameObject yellowLouiePrefab;
    public GameObject pinkLouiePrefab;
    public GameObject redLouiePrefab;

    [Header("Local Offset")]
    public Vector2 localOffset = new(0f, -0.15f);

    [Header("Louie Death")]
    public float louieDeathSeconds = 0.5f;

    [Header("Player Invulnerability After Losing Louie")]
    public float playerInvulnerabilityAfterLoseLouieSeconds = 0.8f;

    [Header("Visual Sync")]
    public bool blinkPlayerTogetherWithLouie = true;

    [Header("Louie Mount SFX")]
    public AudioClip mountLouieSfx;

    [Range(0f, 1f)]
    public float mountLouieVolume = 1f;

    AudioClip pendingMountSfx;
    float pendingMountVolume = 1f;

    MovementController movement;
    GameObject currentLouie;

    int mountedLouieHp;
    CharacterHealth mountedLouieHealth;

    CharacterHealth playerHealth;
    float playerOriginalBlinkInterval;
    float playerOriginalTempSlowdownStartNormalized;
    float playerOriginalTempEndBlinkMultiplier;
    Coroutine restorePlayerBlinkRoutine;

    AbilitySystem abilitySystem;
    BombPunchAbility punchAbility;
    bool punchOwned;
    bool louieAbilitiesLocked;
    bool louieAbilitiesLockApplied;

    MountedLouieType mountedType = MountedLouieType.None;

    float dashInvulRemainingPlayer;
    float dashInvulRemainingLouie;

    void Awake()
    {
        movement = GetComponent<MovementController>();

        playerHealth = GetComponent<CharacterHealth>();
        if (playerHealth != null)
        {
            playerOriginalBlinkInterval = playerHealth.hitBlinkInterval;
            playerOriginalTempSlowdownStartNormalized = playerHealth.tempSlowdownStartNormalized;
            playerOriginalTempEndBlinkMultiplier = playerHealth.tempEndBlinkMultiplier;
        }

        if (!TryGetComponent(out abilitySystem))
            abilitySystem = gameObject.AddComponent<AbilitySystem>();

        abilitySystem.RebuildCache();
        punchAbility = abilitySystem.Get<BombPunchAbility>(BombPunchAbility.AbilityId);

        punchOwned = abilitySystem.IsEnabled(BombPunchAbility.AbilityId);

        if (punchAbility != null)
            punchAbility.SetLockedByLouie(false);
    }

    void Update()
    {
        punchOwned = PlayerPersistentStats.Get(GetPlayerId()).CanPunchBombs;

        bool shouldLock =
            MechaBossSequence.MechaIntroRunning ||
            (StageIntroTransition.Instance != null && StageIntroTransition.Instance.IntroRunning);

        if (shouldLock != louieAbilitiesLocked)
            SetLouieAbilitiesLocked(shouldLock);

        UpdateDashInvulnerabilityTimers();

        if (currentLouie != null && mountedType != MountedLouieType.Blue)
            EnforceNoPunchWhileMountedNonBlue();
    }

    void UpdateDashInvulnerabilityTimers()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        if (dashInvulRemainingPlayer > 0f)
            dashInvulRemainingPlayer = Mathf.Max(0f, dashInvulRemainingPlayer - dt);

        if (dashInvulRemainingLouie > 0f)
            dashInvulRemainingLouie = Mathf.Max(0f, dashInvulRemainingLouie - dt);
    }

    public void StartOrExtendDashInvulnerability(float seconds)
    {
        float s = Mathf.Max(0.01f, seconds);

        if (playerHealth != null)
        {
            dashInvulRemainingPlayer = Mathf.Max(dashInvulRemainingPlayer, s);
            playerHealth.StartTemporaryInvulnerability(dashInvulRemainingPlayer, withBlink: false);
        }

        if (mountedLouieHealth != null)
        {
            dashInvulRemainingLouie = Mathf.Max(dashInvulRemainingLouie, s);
            mountedLouieHealth.StartTemporaryInvulnerability(dashInvulRemainingLouie, withBlink: false);
        }
    }

    void ClearDashInvulnerabilityNow()
    {
        if (playerHealth != null && dashInvulRemainingPlayer > 0f)
            playerHealth.StopInvulnerability();

        if (mountedLouieHealth != null && dashInvulRemainingLouie > 0f)
            mountedLouieHealth.StopInvulnerability();

        dashInvulRemainingPlayer = 0f;
        dashInvulRemainingLouie = 0f;
    }

    public void MountBlueLouie() => MountLouieInternal(blueLouiePrefab, MountedLouieType.Blue);
    public void MountBlackLouie() => MountLouieInternal(blackLouiePrefab, MountedLouieType.Black);
    public void MountPurpleLouie() => MountLouieInternal(purpleLouiePrefab, MountedLouieType.Purple);
    public void MountGreenLouie() => MountLouieInternal(greenLouiePrefab, MountedLouieType.Green);
    public void MountYellowLouie() => MountLouieInternal(yellowLouiePrefab, MountedLouieType.Yellow);
    public void MountPinkLouie() => MountLouieInternal(pinkLouiePrefab, MountedLouieType.Pink);
    public void MountRedLouie() => MountLouieInternal(redLouiePrefab, MountedLouieType.Red);

    public int GetMountedLouieLife()
    {
        if (currentLouie == null)
            return 0;

        if (mountedLouieHealth != null)
            return mountedLouieHealth.life;

        return mountedLouieHp;
    }

    public void SetMountedLouieLife(int life)
    {
        if (currentLouie == null)
            return;

        int v = Mathf.Max(1, life);

        if (mountedLouieHealth != null)
            mountedLouieHealth.life = v;

        mountedLouieHp = v;
    }

    void MountLouieInternal(GameObject prefab, MountedLouieType type)
    {
        if (prefab == null || movement == null)
            return;

        if (currentLouie != null)
            return;

        var rider = GetComponent<PlayerRidingController>();
        if (rider != null && rider.TryPlayRiding(
            movement.FacingDirection,
            onComplete: () => FinalizeMountAfterRiding(type),
            onStart: () => SpawnLouieDuringRiding(prefab, type)))
            return;

        MountLouieInternal_AfterRiding(prefab, type);
    }

    void SpawnLouieDuringRiding(GameObject prefab, MountedLouieType type)
    {
        if (prefab == null || currentLouie != null)
            return;

        mountedType = type;

        currentLouie = Instantiate(prefab, transform);
        currentLouie.transform.SetLocalPositionAndRotation(localOffset, Quaternion.identity);
        currentLouie.transform.localScale = Vector3.one;

        mountedLouieHealth = currentLouie.GetComponentInChildren<CharacterHealth>(true);

        mountedLouieHp = 1;
        if (mountedLouieHealth != null)
            mountedLouieHp = Mathf.Max(1, mountedLouieHealth.life);

        if (currentLouie.TryGetComponent<LouieMovementController>(out var lm))
        {
            lm.BindOwner(movement, localOffset);
            lm.enabled = false;
        }

        if (currentLouie.TryGetComponent<Rigidbody2D>(out var rb))
            rb.simulated = false;

        if (currentLouie.TryGetComponent<Collider2D>(out var col))
            col.enabled = false;

        if (currentLouie.TryGetComponent<BombController>(out var bc))
            bc.enabled = false;

        ForceLouieFacingToPlayer();

        SetMountedLouieVisible(true);

        PlayMountSfxIfAny();
    }

    void FinalizeMountAfterRiding(MountedLouieType type)
    {
        if (currentLouie == null || movement == null)
            return;

        mountedType = type;

        movement.SetMountedOnLouie(true);

        bool isPink = mountedType == MountedLouieType.Pink;
        movement.SetMountedSpritesLocalYOverride(isPink, movement.pinkMountedSpritesLocalY);

        if (currentLouie.TryGetComponent<LouieRiderVisual>(out var visual))
        {
            visual.localOffset = localOffset;
            visual.Bind(movement);
        }

        SetMountedLouieVisible(true);

        abilitySystem.RebuildCache();
        punchAbility = abilitySystem.Get<BombPunchAbility>(BombPunchAbility.AbilityId);
        punchOwned |= abilitySystem.IsEnabled(BombPunchAbility.AbilityId);

        ApplyRulesForCurrentMount();

        movement.Died -= OnPlayerDied;
        movement.Died += OnPlayerDied;
    }

    void MountLouieInternal_AfterRiding(GameObject prefab, MountedLouieType type)
    {
        mountedType = type;

        currentLouie = Instantiate(prefab, transform);
        currentLouie.transform.SetLocalPositionAndRotation(localOffset, Quaternion.identity);
        currentLouie.transform.localScale = Vector3.one;

        mountedLouieHealth = currentLouie.GetComponentInChildren<CharacterHealth>(true);

        mountedLouieHp = 1;
        if (mountedLouieHealth != null)
            mountedLouieHp = Mathf.Max(1, mountedLouieHealth.life);

        if (currentLouie.TryGetComponent<LouieMovementController>(out var lm))
        {
            lm.BindOwner(movement, localOffset);
            lm.enabled = false;
        }

        if (currentLouie.TryGetComponent<Rigidbody2D>(out var rb))
            rb.simulated = false;

        if (currentLouie.TryGetComponent<Collider2D>(out var col))
            col.enabled = false;

        if (currentLouie.TryGetComponent<BombController>(out var bc))
            bc.enabled = false;

        movement.SetMountedOnLouie(true);
        PlayMountSfxIfAny();

        bool isPink = mountedType == MountedLouieType.Pink;
        movement.SetMountedSpritesLocalYOverride(isPink, movement.pinkMountedSpritesLocalY);

        if (currentLouie.TryGetComponent<LouieRiderVisual>(out var visual))
        {
            visual.localOffset = localOffset;
            visual.Bind(movement);
        }

        abilitySystem.RebuildCache();
        punchAbility = abilitySystem.Get<BombPunchAbility>(BombPunchAbility.AbilityId);

        punchOwned |= abilitySystem.IsEnabled(BombPunchAbility.AbilityId);

        ApplyRulesForCurrentMount();

        movement.Died -= OnPlayerDied;
        movement.Died += OnPlayerDied;
    }

    void ApplyRulesForCurrentMount()
    {
        if (currentLouie == null || mountedType == MountedLouieType.None)
        {
            abilitySystem.RebuildCache();

            var punch = abilitySystem.Get<BombPunchAbility>(BombPunchAbility.AbilityId);
            if (punch != null)
            {
                punch.SetExternalAnimator(null);
                punch.SetLockedByLouie(louieAbilitiesLocked);
            }

            return;
        }

        abilitySystem.Disable(BlackLouieDashPushAbility.AbilityId);
        abilitySystem.Disable(PurpleLouieBombLineAbility.AbilityId);
        abilitySystem.Disable(GreenLouieDashAbility.AbilityId);
        abilitySystem.Disable(YellowLouieDestructibleKickAbility.AbilityId);
        abilitySystem.Disable(PinkLouieJumpAbility.AbilityId);
        abilitySystem.Disable(RedLouiePunchStunAbility.AbilityId);

        punchAbility = abilitySystem.Get<BombPunchAbility>(BombPunchAbility.AbilityId);

        if (mountedType == MountedLouieType.Blue)
        {
            var external = currentLouie != null
                ? currentLouie.GetComponentInChildren<IBombPunchExternalAnimator>(true)
                : null;

            abilitySystem.Enable(BombPunchAbility.AbilityId);

            punchAbility = abilitySystem.Get<BombPunchAbility>(BombPunchAbility.AbilityId);
            if (punchAbility != null)
            {
                punchAbility.SetExternalAnimator(external);
                punchAbility.SetLockedByLouie(louieAbilitiesLocked);
            }

            return;
        }

        if (punchAbility != null)
        {
            punchAbility.SetExternalAnimator(null);

            if (punchOwned)
            {
                abilitySystem.Enable(BombPunchAbility.AbilityId);
                punchAbility.SetLockedByLouie(true);
            }
            else
            {
                abilitySystem.Disable(BombPunchAbility.AbilityId);
                punchAbility.SetLockedByLouie(false);
            }
        }
        else
        {
            if (punchOwned)
                abilitySystem.Enable(BombPunchAbility.AbilityId);
            else
                abilitySystem.Disable(BombPunchAbility.AbilityId);
        }

        if (mountedType == MountedLouieType.Purple)
        {
            abilitySystem.Enable(PurpleLouieBombLineAbility.AbilityId);

            var purpleAbility = abilitySystem.Get<PurpleLouieBombLineAbility>(PurpleLouieBombLineAbility.AbilityId);
            if (purpleAbility != null)
            {
                var anim = currentLouie != null
                    ? currentLouie.GetComponentInChildren<IPurpleLouieBombLineExternalAnimator>(true)
                    : null;

                purpleAbility.SetExternalAnimator(anim);
            }

            return;
        }

        if (mountedType == MountedLouieType.Green)
        {
            abilitySystem.Enable(GreenLouieDashAbility.AbilityId);

            var dash = abilitySystem.Get<GreenLouieDashAbility>(GreenLouieDashAbility.AbilityId);
            if (dash != null)
            {
                var anim = currentLouie != null
                    ? currentLouie.GetComponentInChildren<IGreenLouieDashExternalAnimator>(true)
                    : null;

                dash.SetExternalAnimator(anim);

                var cfg = currentLouie != null
                    ? currentLouie.GetComponentInChildren<LouieAbilitySfxConfig>(true)
                    : null;

                if (cfg != null)
                    dash.SetDashSfx(cfg.abilitySfx, cfg.abilityVolume);
                else
                    dash.SetDashSfx(null, 1f);
            }

            return;
        }

        if (mountedType == MountedLouieType.Yellow)
        {
            abilitySystem.Enable(YellowLouieDestructibleKickAbility.AbilityId);

            var kick = abilitySystem.Get<YellowLouieDestructibleKickAbility>(YellowLouieDestructibleKickAbility.AbilityId);
            if (kick != null)
            {
                var anim = currentLouie != null
                    ? currentLouie.GetComponentInChildren<IYellowLouieDestructibleKickExternalAnimator>(true)
                    : null;

                kick.SetExternalAnimator(anim);

                var cfg = currentLouie != null
                    ? currentLouie.GetComponentInChildren<LouieAbilitySfxConfig>(true)
                    : null;

                if (cfg != null)
                    kick.SetKickSfx(cfg.abilitySfx, cfg.abilityVolume);
                else
                    kick.SetKickSfx(null, 1f);
            }

            return;
        }

        if (mountedType == MountedLouieType.Pink)
        {
            abilitySystem.Enable(PinkLouieJumpAbility.AbilityId);

            var jump = abilitySystem.Get<PinkLouieJumpAbility>(PinkLouieJumpAbility.AbilityId);
            if (jump != null)
            {
                var anim = currentLouie != null
                    ? currentLouie.GetComponentInChildren<IPinkLouieJumpExternalAnimator>(true)
                    : null;

                jump.SetExternalAnimator(anim);

                var cfg = currentLouie != null
                    ? currentLouie.GetComponentInChildren<LouieAbilitySfxConfig>(true)
                    : null;

                if (cfg != null)
                    jump.SetJumpSfx(cfg.abilitySfx, cfg.abilityVolume);
                else
                    jump.SetJumpSfx(null, 1f);
            }

            return;
        }

        if (mountedType == MountedLouieType.Red)
        {
            abilitySystem.Enable(RedLouiePunchStunAbility.AbilityId);

            var stun = abilitySystem.Get<RedLouiePunchStunAbility>(RedLouiePunchStunAbility.AbilityId);
            if (stun != null)
            {
                var anim = currentLouie != null
                    ? currentLouie.GetComponentInChildren<IRedLouiePunchExternalAnimator>(true)
                    : null;

                stun.SetExternalAnimator(anim);

                var cfg = currentLouie != null
                    ? currentLouie.GetComponentInChildren<LouieAbilitySfxConfig>(true)
                    : null;

                if (cfg != null)
                    stun.SetPunchSfx(cfg.abilitySfx, cfg.abilityVolume);
                else
                    stun.SetPunchSfx(null, 1f);
            }

            return;
        }

        if (mountedType == MountedLouieType.Black)
        {
            abilitySystem.Enable(BlackLouieDashPushAbility.AbilityId);

            var dash = abilitySystem.Get<BlackLouieDashPushAbility>(BlackLouieDashPushAbility.AbilityId);
            if (dash != null)
            {
                var anim = currentLouie != null
                    ? currentLouie.GetComponentInChildren<IBlackLouieDashExternalAnimator>(true)
                    : null;

                dash.SetExternalAnimator(anim);

                var cfg = currentLouie != null
                    ? currentLouie.GetComponentInChildren<LouieAbilitySfxConfig>(true)
                    : null;

                if (cfg != null)
                    dash.SetDashSfx(cfg.abilitySfx, cfg.abilityVolume);
                else
                    dash.SetDashSfx(null, 1f);
            }

            return;
        }
    }

    void EnforceNoPunchWhileMountedNonBlue()
    {
        abilitySystem.RebuildCache();

        if (punchOwned)
        {
            if (!abilitySystem.IsEnabled(BombPunchAbility.AbilityId))
                abilitySystem.Enable(BombPunchAbility.AbilityId);

            punchAbility = abilitySystem.Get<BombPunchAbility>(BombPunchAbility.AbilityId);
            if (punchAbility != null)
            {
                punchAbility.SetExternalAnimator(null);
                punchAbility.SetLockedByLouie(true);
            }
        }
        else
        {
            if (abilitySystem.IsEnabled(BombPunchAbility.AbilityId))
                abilitySystem.Disable(BombPunchAbility.AbilityId);
        }
    }

    void RestorePunchAfterUnmount()
    {
        abilitySystem.RebuildCache();

        punchAbility = abilitySystem.Get<BombPunchAbility>(BombPunchAbility.AbilityId);
        if (punchAbility != null)
        {
            punchAbility.SetExternalAnimator(null);
            punchAbility.SetLockedByLouie(false);
        }

        if (punchOwned)
            abilitySystem.Enable(BombPunchAbility.AbilityId);
        else
            abilitySystem.Disable(BombPunchAbility.AbilityId);
    }

    public void OnMountedLouieHit(int damage)
    {
        if (currentLouie == null)
            return;

        int dmg = Mathf.Max(1, damage);

        if (mountedLouieHealth != null)
        {
            if (mountedLouieHealth.IsInvulnerable)
                return;

            mountedLouieHealth.TakeDamage(dmg);
            mountedLouieHp = Mathf.Max(0, mountedLouieHealth.life);

            if (mountedLouieHealth.life > 0)
                SyncPlayerBlinkWithLouie();

            if (mountedLouieHealth.life <= 0)
                LoseLouie();

            return;
        }

        mountedLouieHp -= dmg;

        if (mountedLouieHp <= 0)
            LoseLouie();
    }

    void SyncPlayerBlinkWithLouie()
    {
        if (!blinkPlayerTogetherWithLouie)
            return;

        if (playerHealth == null || mountedLouieHealth == null)
            return;

        float seconds = mountedLouieHealth.hitInvulnerableDuration;
        if (seconds <= 0f)
            return;

        playerHealth.hitBlinkInterval = mountedLouieHealth.hitBlinkInterval;
        playerHealth.tempSlowdownStartNormalized = mountedLouieHealth.tempSlowdownStartNormalized;
        playerHealth.tempEndBlinkMultiplier = mountedLouieHealth.tempEndBlinkMultiplier;

        playerHealth.StartTemporaryInvulnerability(seconds);

        if (restorePlayerBlinkRoutine != null)
            StopCoroutine(restorePlayerBlinkRoutine);

        restorePlayerBlinkRoutine = StartCoroutine(RestorePlayerBlinkDefaultsAfter(seconds));
    }

    IEnumerator RestorePlayerBlinkDefaultsAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        if (playerHealth != null)
        {
            playerHealth.hitBlinkInterval = playerOriginalBlinkInterval;
            playerHealth.tempSlowdownStartNormalized = playerOriginalTempSlowdownStartNormalized;
            playerHealth.tempEndBlinkMultiplier = playerOriginalTempEndBlinkMultiplier;
        }

        restorePlayerBlinkRoutine = null;
    }

    public void LoseLouie()
    {
        if (currentLouie == null)
            return;

        bool hasQueuedEgg = false;
        ItemPickup.ItemType queuedType = default;
        AudioClip queuedSfx = null;
        float queuedVol = 1f;
        GameObject queuedPrefab = null;
        MountedLouieType queuedMountedType = MountedLouieType.None;

        if (TryGetComponent<LouieEggQueue>(out var q) && q != null && q.Count > 0)
        {
            if (q.TryDequeue(out queuedType, out queuedSfx, out queuedVol))
            {
                if (TryMapEggToLouie(queuedType, out queuedPrefab, out queuedMountedType) && queuedPrefab != null)
                    hasQueuedEgg = true;
            }
        }

        var rider = GetComponent<PlayerRidingController>();

        if (rider != null && movement != null)
        {
            DetachCurrentLouieBeforeRiding();

            if (hasQueuedEgg)
            {
                if (rider.TryPlayRiding(
                    movement.FacingDirection,
                    onComplete: () => FinalizeMountAfterRiding(queuedMountedType),
                    onStart: () =>
                    {
                        DetachAndKillCurrentLouieOnly();

                        SetNextMountSfx(queuedSfx, queuedVol);
                        SpawnLouieDuringRiding(queuedPrefab, queuedMountedType);
                    }))
                    return;

                DetachAndKillCurrentLouieOnly();
                SetNextMountSfx(queuedSfx, queuedVol);
                MountLouieInternal_AfterRiding(queuedPrefab, queuedMountedType);
                return;
            }

            if (rider.TryPlayRiding(movement.FacingDirection, LoseLouie_AfterRiding))
                return;
        }

        LoseLouie_AfterRiding();
    }

    public void LoseLouie_AfterRiding()
    {
        if (currentLouie == null)
            return;

        ClearDashInvulnerabilityNow();

        var louie = currentLouie;
        currentLouie = null;

        mountedLouieHp = 0;
        mountedLouieHealth = null;

        louie.transform.GetPositionAndRotation(out var worldPos, out var worldRot);

        movement.SetMountedSpritesLocalYOverride(false, 0f);

        movement.SetMountedOnLouie(false);
        mountedType = MountedLouieType.None;

        abilitySystem.Disable(PurpleLouieBombLineAbility.AbilityId);
        abilitySystem.Disable(GreenLouieDashAbility.AbilityId);
        abilitySystem.Disable(YellowLouieDestructibleKickAbility.AbilityId);
        abilitySystem.Disable(PinkLouieJumpAbility.AbilityId);
        abilitySystem.Disable(RedLouiePunchStunAbility.AbilityId);
        abilitySystem.Disable(BlackLouieDashPushAbility.AbilityId);

        var kick = abilitySystem.Get<YellowLouieDestructibleKickAbility>(YellowLouieDestructibleKickAbility.AbilityId);
        if (kick != null)
        {
            kick.SetExternalAnimator(null);
            kick.SetKickSfx(null, 1f);
        }

        var dash = abilitySystem.Get<GreenLouieDashAbility>(GreenLouieDashAbility.AbilityId);
        if (dash != null)
        {
            dash.SetExternalAnimator(null);
            dash.SetDashSfx(null, 1f);
        }

        var jump = abilitySystem.Get<PinkLouieJumpAbility>(PinkLouieJumpAbility.AbilityId);
        if (jump != null)
        {
            jump.SetExternalAnimator(null);
            jump.SetJumpSfx(null, 1f);
        }

        var stun = abilitySystem.Get<RedLouiePunchStunAbility>(RedLouiePunchStunAbility.AbilityId);
        if (stun != null)
        {
            stun.SetExternalAnimator(null);
            stun.SetPunchSfx(null, 1f);
        }

        var blackDash = abilitySystem.Get<BlackLouieDashPushAbility>(BlackLouieDashPushAbility.AbilityId);
        if (blackDash != null)
        {
            blackDash.SetExternalAnimator(null);
            blackDash.SetDashSfx(null, 1f);
        }

        var purple = abilitySystem.Get<PurpleLouieBombLineAbility>(PurpleLouieBombLineAbility.AbilityId);
        if (purple != null)
            purple.SetExternalAnimator(null);

        RestorePunchAfterUnmount();

        if (movement.TryGetComponent<CharacterHealth>(out var health))
            health.StartTemporaryInvulnerability(playerInvulnerabilityAfterLoseLouieSeconds);

        louie.transform.SetParent(null, true);
        louie.transform.SetPositionAndRotation(worldPos, worldRot);

        if (louie.TryGetComponent<LouieRiderVisual>(out var riderVisual))
            Destroy(riderVisual);

        if (louie.TryGetComponent<LouieMovementController>(out var lm))
        {
            lm.enabled = true;
            lm.Kill();
        }
        else if (louie.TryGetComponent<MovementController>(out var mc))
        {
            mc.enabled = true;
            mc.Kill();
        }
        else
        {
            Destroy(louie);
        }

        if (currentLouie == null)
            TryMountFromQueuedEgg();
    }

    void OnPlayerDied(MovementController _) => UnmountLouie();

    void UnmountLouie()
    {
        var rider = GetComponent<PlayerRidingController>();
        if (rider != null && movement != null && rider.TryPlayRiding(movement.FacingDirection, UnmountLouie_AfterRiding))
            return;

        UnmountLouie_AfterRiding();
    }

    void UnmountLouie_AfterRiding()
    {
        ClearDashInvulnerabilityNow();

        if (currentLouie != null)
            Destroy(currentLouie);

        currentLouie = null;

        mountedLouieHp = 0;
        mountedLouieHealth = null;

        movement.SetMountedSpritesLocalYOverride(false, 0f);

        movement.SetMountedOnLouie(false);
        mountedType = MountedLouieType.None;

        abilitySystem.Disable(PurpleLouieBombLineAbility.AbilityId);
        abilitySystem.Disable(GreenLouieDashAbility.AbilityId);
        abilitySystem.Disable(YellowLouieDestructibleKickAbility.AbilityId);
        abilitySystem.Disable(PinkLouieJumpAbility.AbilityId);
        abilitySystem.Disable(RedLouiePunchStunAbility.AbilityId);
        abilitySystem.Disable(BlackLouieDashPushAbility.AbilityId);

        var kick = abilitySystem.Get<YellowLouieDestructibleKickAbility>(YellowLouieDestructibleKickAbility.AbilityId);
        if (kick != null)
        {
            kick.SetExternalAnimator(null);
            kick.SetKickSfx(null, 1f);
        }

        var dash = abilitySystem.Get<GreenLouieDashAbility>(GreenLouieDashAbility.AbilityId);
        if (dash != null)
        {
            dash.SetExternalAnimator(null);
            dash.SetDashSfx(null, 1f);
        }

        var jump = abilitySystem.Get<PinkLouieJumpAbility>(PinkLouieJumpAbility.AbilityId);
        if (jump != null)
        {
            jump.SetExternalAnimator(null);
            jump.SetJumpSfx(null, 1f);
        }

        var stun = abilitySystem.Get<RedLouiePunchStunAbility>(RedLouiePunchStunAbility.AbilityId);
        if (stun != null)
        {
            stun.SetExternalAnimator(null);
            stun.SetPunchSfx(null, 1f);
        }

        var blackDash = abilitySystem.Get<BlackLouieDashPushAbility>(BlackLouieDashPushAbility.AbilityId);
        if (blackDash != null)
        {
            blackDash.SetExternalAnimator(null);
            blackDash.SetDashSfx(null, 1f);
        }

        var purple = abilitySystem.Get<PurpleLouieBombLineAbility>(PurpleLouieBombLineAbility.AbilityId);
        if (purple != null)
            purple.SetExternalAnimator(null);

        RestorePunchAfterUnmount();
    }

    void OnDestroy()
    {
        if (movement != null)
            movement.Died -= OnPlayerDied;
    }

    public MountedLouieType GetMountedLouieType()
    {
        return currentLouie == null
            ? MountedLouieType.None
            : mountedType;
    }

    public CharacterHealth GetMountedLouieHealth()
    {
        return mountedLouieHealth;
    }

    public bool HasMountedLouie()
    {
        return currentLouie != null;
    }

    public void RestoreMountedBlueLouie()
    {
        if (currentLouie != null)
            return;

        MountBlueLouie();
    }

    public void RestoreMountedBlackLouie()
    {
        if (currentLouie != null)
            return;

        MountBlackLouie();
    }

    public void RestoreMountedPurpleLouie()
    {
        if (currentLouie != null)
            return;

        MountPurpleLouie();
    }

    public void RestoreMountedGreenLouie()
    {
        if (currentLouie != null)
            return;

        MountGreenLouie();
    }

    public void RestoreMountedYellowLouie()
    {
        if (currentLouie != null)
            return;

        MountYellowLouie();
    }

    public void RestoreMountedPinkLouie()
    {
        if (currentLouie != null)
            return;

        MountPinkLouie();
    }

    public void RestoreMountedRedLouie()
    {
        if (currentLouie != null)
            return;

        MountRedLouie();
    }

    public bool TryPlayMountedLouieEndStage(float totalTime, int frameCount)
    {
        if (currentLouie == null)
            return false;

        if (!currentLouie.TryGetComponent<LouieRiderVisual>(out var visual) || visual == null)
            visual = currentLouie.GetComponentInChildren<LouieRiderVisual>(true);

        if (visual == null)
            return false;

        return visual.TryPlayEndStage(totalTime, frameCount);
    }

    public void SetLouieAbilitiesLocked(bool locked)
    {
        louieAbilitiesLocked = locked;
        ApplyLouieAbilitiesLockState();
    }

    void ApplyLouieAbilitiesLockState()
    {
        if (abilitySystem == null)
            return;

        abilitySystem.RebuildCache();
        punchAbility = abilitySystem.Get<BombPunchAbility>(BombPunchAbility.AbilityId);

        if (louieAbilitiesLocked)
        {
            if (louieAbilitiesLockApplied)
                return;

            louieAbilitiesLockApplied = true;

            if (punchAbility != null)
            {
                punchAbility.SetLockedByLouie(true);
                punchAbility.SetExternalAnimator(null);
            }

            abilitySystem.Disable(BlackLouieDashPushAbility.AbilityId);
            abilitySystem.Disable(PurpleLouieBombLineAbility.AbilityId);
            abilitySystem.Disable(GreenLouieDashAbility.AbilityId);
            abilitySystem.Disable(YellowLouieDestructibleKickAbility.AbilityId);
            abilitySystem.Disable(PinkLouieJumpAbility.AbilityId);
            abilitySystem.Disable(RedLouiePunchStunAbility.AbilityId);

            var kick = abilitySystem.Get<YellowLouieDestructibleKickAbility>(YellowLouieDestructibleKickAbility.AbilityId);
            if (kick != null)
            {
                kick.SetExternalAnimator(null);
                kick.SetKickSfx(null, 1f);
            }

            var dash = abilitySystem.Get<GreenLouieDashAbility>(GreenLouieDashAbility.AbilityId);
            if (dash != null)
            {
                dash.SetExternalAnimator(null);
                dash.SetDashSfx(null, 1f);
            }

            var jump = abilitySystem.Get<PinkLouieJumpAbility>(PinkLouieJumpAbility.AbilityId);
            if (jump != null)
            {
                jump.SetExternalAnimator(null);
                jump.SetJumpSfx(null, 1f);
            }

            var stun = abilitySystem.Get<RedLouiePunchStunAbility>(RedLouiePunchStunAbility.AbilityId);
            if (stun != null)
            {
                stun.SetExternalAnimator(null);
                stun.SetPunchSfx(null, 1f);
            }

            var blackDash = abilitySystem.Get<BlackLouieDashPushAbility>(BlackLouieDashPushAbility.AbilityId);
            if (blackDash != null)
            {
                blackDash.SetExternalAnimator(null);
                blackDash.SetDashSfx(null, 1f);
            }

            var purple = abilitySystem.Get<PurpleLouieBombLineAbility>(PurpleLouieBombLineAbility.AbilityId);
            if (purple != null)
                purple.SetExternalAnimator(null);

            return;
        }

        if (!louieAbilitiesLockApplied)
            return;

        louieAbilitiesLockApplied = false;

        if (punchAbility != null)
            punchAbility.SetLockedByLouie(false);

        ApplyRulesForCurrentMount();
    }

    public void SetMountedLouieVisible(bool visible)
    {
        if (currentLouie == null)
            return;

        currentLouie.SetActive(visible);
    }

    public void SetPlayerAndLouieVisible(bool visible)
    {
        if (movement != null)
            movement.SetAllSpritesVisible(visible);

        SetMountedLouieVisible(visible);
    }

    int GetPlayerId()
    {
        if (TryGetComponent<PlayerIdentity>(out var id) && id != null)
            return Mathf.Clamp(id.playerId, 1, 4);

        var parentId = GetComponentInParent<PlayerIdentity>(true);
        if (parentId != null)
            return Mathf.Clamp(parentId.playerId, 1, 4);

        return 1;
    }

    public void RestoreMountedFromPersistent()
    {
        if (currentLouie != null)
            return;

        int playerId = GetPlayerId();
        var state = PlayerPersistentStats.Get(playerId);

        switch (state.MountedLouie)
        {
            case MountedLouieType.Blue: RestoreMountedBlueLouie(); break;
            case MountedLouieType.Black: RestoreMountedBlackLouie(); break;
            case MountedLouieType.Purple: RestoreMountedPurpleLouie(); break;
            case MountedLouieType.Green: RestoreMountedGreenLouie(); break;
            case MountedLouieType.Yellow: RestoreMountedYellowLouie(); break;
            case MountedLouieType.Pink: RestoreMountedPinkLouie(); break;
            case MountedLouieType.Red: RestoreMountedRedLouie(); break;
        }
    }

    bool TryMountFromQueuedEgg()
    {
        if (currentLouie != null)
            return false;

        if (!TryGetComponent<LouieEggQueue>(out var q) || q == null)
            return false;

        if (!q.TryDequeue(out var t, out var sfx, out var vol))
            return false;

        SetNextMountSfx(sfx, vol);

        switch (t)
        {
            case ItemPickup.ItemType.BlueLouieEgg: MountBlueLouie(); return true;
            case ItemPickup.ItemType.BlackLouieEgg: MountBlackLouie(); return true;
            case ItemPickup.ItemType.PurpleLouieEgg: MountPurpleLouie(); return true;
            case ItemPickup.ItemType.GreenLouieEgg: MountGreenLouie(); return true;
            case ItemPickup.ItemType.YellowLouieEgg: MountYellowLouie(); return true;
            case ItemPickup.ItemType.PinkLouieEgg: MountPinkLouie(); return true;
            case ItemPickup.ItemType.RedLouieEgg: MountRedLouie(); return true;
        }

        return false;
    }

    public void SetNextMountSfx(AudioClip clip, float volume)
    {
        pendingMountSfx = clip;
        pendingMountVolume = Mathf.Clamp01(volume);
    }

    AudioSource FindAudioSource()
    {
        var a = GetComponent<AudioSource>();
        if (a != null) return a;

        a = GetComponentInChildren<AudioSource>(true);
        if (a != null) return a;

        a = GetComponentInParent<AudioSource>(true);
        return a;
    }

    void PlayMountSfxIfAny()
    {
        var clip = pendingMountSfx != null ? pendingMountSfx : mountLouieSfx;
        var vol = pendingMountSfx != null ? pendingMountVolume : mountLouieVolume;

        pendingMountSfx = null;
        pendingMountVolume = 1f;

        var audio = FindAudioSource();
        if (audio != null && clip != null)
            audio.PlayOneShot(clip, Mathf.Clamp01(vol));
    }

    void ForceLouieFacingToPlayer()
    {
        if (currentLouie == null || movement == null)
            return;

        Vector2 facing = movement.FacingDirection;
        if (facing == Vector2.zero)
            facing = Vector2.down;

        if (!currentLouie.TryGetComponent<LouieRiderVisual>(out var visual) || visual == null)
            visual = currentLouie.GetComponentInChildren<LouieRiderVisual>(true);

        if (visual == null)
            return;

        visual.localOffset = localOffset;

        visual.Bind(movement);

        if (visual.TryGetComponent(out MonoBehaviour mb))
            mb.enabled = true;
    }

    bool TryMapEggToLouie(ItemPickup.ItemType eggType, out GameObject prefab, out MountedLouieType type)
    {
        prefab = null;
        type = MountedLouieType.None;

        switch (eggType)
        {
            case ItemPickup.ItemType.BlueLouieEgg: prefab = blueLouiePrefab; type = MountedLouieType.Blue; return true;
            case ItemPickup.ItemType.BlackLouieEgg: prefab = blackLouiePrefab; type = MountedLouieType.Black; return true;
            case ItemPickup.ItemType.PurpleLouieEgg: prefab = purpleLouiePrefab; type = MountedLouieType.Purple; return true;
            case ItemPickup.ItemType.GreenLouieEgg: prefab = greenLouiePrefab; type = MountedLouieType.Green; return true;
            case ItemPickup.ItemType.YellowLouieEgg: prefab = yellowLouiePrefab; type = MountedLouieType.Yellow; return true;
            case ItemPickup.ItemType.PinkLouieEgg: prefab = pinkLouiePrefab; type = MountedLouieType.Pink; return true;
            case ItemPickup.ItemType.RedLouieEgg: prefab = redLouiePrefab; type = MountedLouieType.Red; return true;
        }

        return false;
    }

    void DetachAndKillCurrentLouieOnly()
    {
        if (currentLouie == null)
            return;

        ClearDashInvulnerabilityNow();

        var louie = currentLouie;
        currentLouie = null;

        mountedLouieHp = 0;
        mountedLouieHealth = null;

        louie.transform.GetPositionAndRotation(out var worldPos, out var worldRot);

        movement.SetMountedSpritesLocalYOverride(false, 0f);
        movement.SetMountedOnLouie(false);

        mountedType = MountedLouieType.None;

        abilitySystem.Disable(PurpleLouieBombLineAbility.AbilityId);
        abilitySystem.Disable(GreenLouieDashAbility.AbilityId);
        abilitySystem.Disable(YellowLouieDestructibleKickAbility.AbilityId);
        abilitySystem.Disable(PinkLouieJumpAbility.AbilityId);
        abilitySystem.Disable(RedLouiePunchStunAbility.AbilityId);
        abilitySystem.Disable(BlackLouieDashPushAbility.AbilityId);

        var kick = abilitySystem.Get<YellowLouieDestructibleKickAbility>(YellowLouieDestructibleKickAbility.AbilityId);
        if (kick != null) { kick.SetExternalAnimator(null); kick.SetKickSfx(null, 1f); }

        var dash = abilitySystem.Get<GreenLouieDashAbility>(GreenLouieDashAbility.AbilityId);
        if (dash != null) { dash.SetExternalAnimator(null); dash.SetDashSfx(null, 1f); }

        var jump = abilitySystem.Get<PinkLouieJumpAbility>(PinkLouieJumpAbility.AbilityId);
        if (jump != null) { jump.SetExternalAnimator(null); jump.SetJumpSfx(null, 1f); }

        var stun = abilitySystem.Get<RedLouiePunchStunAbility>(RedLouiePunchStunAbility.AbilityId);
        if (stun != null) { stun.SetExternalAnimator(null); stun.SetPunchSfx(null, 1f); }

        var blackDash = abilitySystem.Get<BlackLouieDashPushAbility>(BlackLouieDashPushAbility.AbilityId);
        if (blackDash != null) { blackDash.SetExternalAnimator(null); blackDash.SetDashSfx(null, 1f); }

        var purple = abilitySystem.Get<PurpleLouieBombLineAbility>(PurpleLouieBombLineAbility.AbilityId);
        if (purple != null) purple.SetExternalAnimator(null);

        RestorePunchAfterUnmount();

        if (movement.TryGetComponent<CharacterHealth>(out var health))
            health.StartTemporaryInvulnerability(playerInvulnerabilityAfterLoseLouieSeconds);

        louie.transform.SetParent(null, true);
        louie.transform.SetPositionAndRotation(worldPos, worldRot);

        if (louie.TryGetComponent<LouieRiderVisual>(out var riderVisual))
            Destroy(riderVisual);

        if (louie.TryGetComponent<LouieMovementController>(out var lm))
        {
            lm.enabled = true;
            lm.Kill();
        }
        else if (louie.TryGetComponent<MovementController>(out var mc))
        {
            mc.enabled = true;
            mc.Kill();
        }
        else
        {
            Destroy(louie);
        }
    }

    void DetachCurrentLouieBeforeRiding()
    {
        if (currentLouie == null)
            return;

        currentLouie.transform.GetPositionAndRotation(out var worldPos, out var worldRot);

        if (currentLouie.TryGetComponent<LouieRiderVisual>(out var rv) && rv != null)
            rv.enabled = false;

        currentLouie.transform.SetParent(null, true);
        currentLouie.transform.SetPositionAndRotation(worldPos, worldRot);
    }
}
