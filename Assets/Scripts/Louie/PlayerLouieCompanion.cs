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

    float ridingUninterruptibleUntil;

    bool IsRidingUninterruptible()
    {
        return Time.time < ridingUninterruptibleUntil;
    }

    void MarkRidingUninterruptible(float seconds, bool blink)
    {
        float s = Mathf.Max(0.01f, seconds);
        ridingUninterruptibleUntil = Mathf.Max(ridingUninterruptibleUntil, Time.time + s);

        if (playerHealth != null)
            playerHealth.StartTemporaryInvulnerability(s, withBlink: blink);
    }

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

    public void MountBlueLouie() => TryMount(GetPrefab(MountedLouieType.Blue), MountedLouieType.Blue);
    public void MountBlackLouie() => TryMount(GetPrefab(MountedLouieType.Black), MountedLouieType.Black);
    public void MountPurpleLouie() => TryMount(GetPrefab(MountedLouieType.Purple), MountedLouieType.Purple);
    public void MountGreenLouie() => TryMount(GetPrefab(MountedLouieType.Green), MountedLouieType.Green);
    public void MountYellowLouie() => TryMount(GetPrefab(MountedLouieType.Yellow), MountedLouieType.Yellow);
    public void MountPinkLouie() => TryMount(GetPrefab(MountedLouieType.Pink), MountedLouieType.Pink);
    public void MountRedLouie() => TryMount(GetPrefab(MountedLouieType.Red), MountedLouieType.Red);

    public void RestoreMountedBlueLouie() => RestoreMountedImmediate(MountedLouieType.Blue);
    public void RestoreMountedBlackLouie() => RestoreMountedImmediate(MountedLouieType.Black);
    public void RestoreMountedPurpleLouie() => RestoreMountedImmediate(MountedLouieType.Purple);
    public void RestoreMountedGreenLouie() => RestoreMountedImmediate(MountedLouieType.Green);
    public void RestoreMountedYellowLouie() => RestoreMountedImmediate(MountedLouieType.Yellow);
    public void RestoreMountedPinkLouie() => RestoreMountedImmediate(MountedLouieType.Pink);
    public void RestoreMountedRedLouie() => RestoreMountedImmediate(MountedLouieType.Red);

    void RestoreMountedImmediate(MountedLouieType type)
    {
        if (currentLouie != null || movement == null)
            return;

        if (type == MountedLouieType.None)
            return;

        var prefab = GetPrefab(type);
        if (prefab == null)
            return;

        mountedType = type;

        currentLouie = Instantiate(prefab, transform);
        currentLouie.transform.SetLocalPositionAndRotation(localOffset, Quaternion.identity);
        currentLouie.transform.localScale = Vector3.one;

        mountedLouieHealth = currentLouie.GetComponentInChildren<CharacterHealth>(true);
        mountedLouieHp = 1;
        if (mountedLouieHealth != null)
            mountedLouieHp = Mathf.Max(1, mountedLouieHealth.life);

        DisableLouieComponentsForMount(currentLouie);

        FinalizeMount(type);
    }

    void TryMount(GameObject prefab, MountedLouieType type)
    {
        if (prefab == null || movement == null)
            return;

        if (currentLouie != null)
            return;

        var rider = GetComponent<PlayerRidingController>();
        if (rider != null && rider.TryPlayRiding(
            movement.FacingDirection,
            onComplete: () => FinalizeMount(type),
            onStart: () => SpawnLouieForMount(prefab, type, duringRiding: true)))
            return;

        SpawnLouieForMount(prefab, type, duringRiding: false);
        FinalizeMount(type);
    }

    void SpawnLouieForMount(GameObject prefab, MountedLouieType type, bool duringRiding)
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

        DisableLouieComponentsForMount(currentLouie);

        if (duringRiding)
            SetMountedLouieVisible(true);

        PlayMountSfxIfAny();
    }

    void DisableLouieComponentsForMount(GameObject louie)
    {
        if (louie == null)
            return;

        if (louie.TryGetComponent<LouieMovementController>(out var lm))
        {
            lm.BindOwner(movement, localOffset);
            lm.enabled = false;
        }

        if (louie.TryGetComponent<Rigidbody2D>(out var rb))
            rb.simulated = false;

        if (louie.TryGetComponent<Collider2D>(out var col))
            col.enabled = false;

        if (louie.TryGetComponent<BombController>(out var bc))
            bc.enabled = false;

        BindLouieVisualToOwnerIfAny(louie);
    }

    void FinalizeMount(MountedLouieType type)
    {
        if (currentLouie == null || movement == null)
            return;

        mountedType = type;

        movement.SetMountedOnLouie(true);

        bool isPink = mountedType == MountedLouieType.Pink;
        movement.SetMountedSpritesLocalYOverride(isPink, movement.pinkMountedSpritesLocalY);

        BindLouieVisualToOwnerIfAny(currentLouie);

        SetMountedLouieVisible(true);

        abilitySystem.RebuildCache();
        punchAbility = abilitySystem.Get<BombPunchAbility>(BombPunchAbility.AbilityId);
        punchOwned |= abilitySystem.IsEnabled(BombPunchAbility.AbilityId);

        ApplyRulesForCurrentMount();

        movement.Died -= OnPlayerDied;
        movement.Died += OnPlayerDied;
    }

    void BindLouieVisualToOwnerIfAny(GameObject louie)
    {
        if (louie == null || movement == null)
            return;

        if (!louie.TryGetComponent<LouieRidingVisual>(out var visual) || visual == null)
            visual = louie.GetComponentInChildren<LouieRidingVisual>(true);

        if (visual == null)
            return;

        visual.localOffset = localOffset;
        visual.Bind(movement);
        visual.enabled = true;
    }

    void ApplyRulesForCurrentMount()
    {
        abilitySystem.RebuildCache();

        if (currentLouie == null || mountedType == MountedLouieType.None)
        {
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
            if (punchOwned) abilitySystem.Enable(BombPunchAbility.AbilityId);
            else abilitySystem.Disable(BombPunchAbility.AbilityId);
        }

        if (currentLouie == null)
            return;

        var cfg = currentLouie.GetComponentInChildren<LouieAbilitySfxConfig>(true);

        if (mountedType == MountedLouieType.Purple)
        {
            abilitySystem.Enable(PurpleLouieBombLineAbility.AbilityId);

            var purple = abilitySystem.Get<PurpleLouieBombLineAbility>(PurpleLouieBombLineAbility.AbilityId);
            if (purple != null)
            {
                var anim = currentLouie.GetComponentInChildren<IPurpleLouieBombLineExternalAnimator>(true);
                purple.SetExternalAnimator(anim);
            }
            return;
        }

        if (mountedType == MountedLouieType.Green)
        {
            abilitySystem.Enable(GreenLouieDashAbility.AbilityId);

            var dash = abilitySystem.Get<GreenLouieDashAbility>(GreenLouieDashAbility.AbilityId);
            if (dash != null)
            {
                var anim = currentLouie.GetComponentInChildren<IGreenLouieDashExternalAnimator>(true);
                dash.SetExternalAnimator(anim);

                if (cfg != null) dash.SetDashSfx(cfg.abilitySfx, cfg.abilityVolume);
                else dash.SetDashSfx(null, 1f);
            }
            return;
        }

        if (mountedType == MountedLouieType.Yellow)
        {
            abilitySystem.Enable(YellowLouieDestructibleKickAbility.AbilityId);

            var kick = abilitySystem.Get<YellowLouieDestructibleKickAbility>(YellowLouieDestructibleKickAbility.AbilityId);
            if (kick != null)
            {
                var anim = currentLouie.GetComponentInChildren<IYellowLouieDestructibleKickExternalAnimator>(true);
                kick.SetExternalAnimator(anim);

                if (cfg != null) kick.SetKickSfx(cfg.abilitySfx, cfg.abilityVolume);
                else kick.SetKickSfx(null, 1f);
            }
            return;
        }

        if (mountedType == MountedLouieType.Pink)
        {
            abilitySystem.Enable(PinkLouieJumpAbility.AbilityId);

            var jump = abilitySystem.Get<PinkLouieJumpAbility>(PinkLouieJumpAbility.AbilityId);
            if (jump != null)
            {
                var anim = currentLouie.GetComponentInChildren<IPinkLouieJumpExternalAnimator>(true);
                jump.SetExternalAnimator(anim);

                if (cfg != null) jump.SetJumpSfx(cfg.abilitySfx, cfg.abilityVolume);
                else jump.SetJumpSfx(null, 1f);
            }
            return;
        }

        if (mountedType == MountedLouieType.Red)
        {
            abilitySystem.Enable(RedLouiePunchStunAbility.AbilityId);

            var stun = abilitySystem.Get<RedLouiePunchStunAbility>(RedLouiePunchStunAbility.AbilityId);
            if (stun != null)
            {
                var anim = currentLouie.GetComponentInChildren<IRedLouiePunchExternalAnimator>(true);
                stun.SetExternalAnimator(anim);

                if (cfg != null) stun.SetPunchSfx(cfg.abilitySfx, cfg.abilityVolume);
                else stun.SetPunchSfx(null, 1f);
            }
            return;
        }

        if (mountedType == MountedLouieType.Black)
        {
            abilitySystem.Enable(BlackLouieDashPushAbility.AbilityId);

            var blackDash = abilitySystem.Get<BlackLouieDashPushAbility>(BlackLouieDashPushAbility.AbilityId);
            if (blackDash != null)
            {
                var anim = currentLouie.GetComponentInChildren<IBlackLouieDashExternalAnimator>(true);
                blackDash.SetExternalAnimator(anim);

                if (cfg != null) blackDash.SetDashSfx(cfg.abilitySfx, cfg.abilityVolume);
                else blackDash.SetDashSfx(null, 1f);
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

        if (punchOwned) abilitySystem.Enable(BombPunchAbility.AbilityId);
        else abilitySystem.Disable(BombPunchAbility.AbilityId);
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

        bool hasQueuedEgg = TryPopQueuedEgg(out var queuedPrefab, out var queuedMountedType, out var queuedSfx, out var queuedVol);

        var rider = GetComponent<PlayerRidingController>();

        if (rider != null && movement != null)
        {
            DetachCurrentLouieBeforeRiding();

            if (hasQueuedEgg)
            {
                if (rider.TryPlayRiding(
                    movement.FacingDirection,
                    onComplete: () => FinalizeMount(queuedMountedType),
                    onStart: () =>
                    {
                        MarkRidingUninterruptible(rider.ridingSeconds, blink: true);

                        DetachAndKillCurrentLouieOnly();
                        SetNextMountSfx(queuedSfx, queuedVol);
                        SpawnLouieForMount(queuedPrefab, queuedMountedType, duringRiding: true);
                    }))
                    return;

                DetachAndKillCurrentLouieOnly();
                SetNextMountSfx(queuedSfx, queuedVol);
                TryMount(queuedPrefab, queuedMountedType);
                return;
            }

            if (rider.TryPlayRiding(
                movement.FacingDirection,
                onComplete: LoseLouie_AfterRiding,
                onStart: () => MarkRidingUninterruptible(rider.ridingSeconds, blink: true)))
                return;
        }

        LoseLouie_AfterRiding();
    }

    bool TryPopQueuedEgg(out GameObject prefab, out MountedLouieType type, out AudioClip sfx, out float vol)
    {
        prefab = null;
        type = MountedLouieType.None;
        sfx = null;
        vol = 1f;

        if (!TryGetComponent<LouieEggQueue>(out var q) || q == null || q.Count <= 0)
            return false;

        if (!q.TryDequeue(out var queuedType, out sfx, out vol))
            return false;

        if (!TryMapEggToLouie(queuedType, out prefab, out type) || prefab == null)
            return false;

        return true;
    }

    public void LoseLouie_AfterRiding()
    {
        if (currentLouie == null)
            return;

        var louie = currentLouie;
        currentLouie = null;

        louie.transform.GetPositionAndRotation(out var worldPos, out var worldRot);

        ClearDashInvulnerabilityNow();
        ResetMountedStateAndAbilities();

        if (movement.TryGetComponent<CharacterHealth>(out var health))
            health.StartTemporaryInvulnerability(playerInvulnerabilityAfterLoseLouieSeconds);

        DetachLouieToWorld(louie, worldPos, worldRot, disableVisual: false);
        KillDetachedLouieGuaranteed(louie);

        if (currentLouie == null)
            TryMountFromQueuedEgg();
    }

    void OnPlayerDied(MovementController _) => UnmountLouie();

    void UnmountLouie()
    {
        var rider = GetComponent<PlayerRidingController>();
        if (rider != null && movement != null && rider.TryPlayRiding(
            movement.FacingDirection,
            onComplete: UnmountLouie_AfterRiding,
            onStart: () => MarkRidingUninterruptible(rider.ridingSeconds, blink: true)))
            return;

        UnmountLouie_AfterRiding();
    }

    void UnmountLouie_AfterRiding()
    {
        ClearDashInvulnerabilityNow();

        if (currentLouie != null)
            Destroy(currentLouie);

        currentLouie = null;

        ResetMountedStateAndAbilities();
    }

    void ResetMountedStateAndAbilities()
    {
        mountedLouieHp = 0;
        mountedLouieHealth = null;

        movement.SetMountedSpritesLocalYOverride(false, 0f);
        movement.SetMountedOnLouie(false);
        mountedType = MountedLouieType.None;

        ResetLouieAbilitiesExternalState();
        RestorePunchAfterUnmount();
    }

    void ResetLouieAbilitiesExternalState()
    {
        if (abilitySystem == null)
            return;

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
    }

    void DetachLouieToWorld(GameObject louie, Vector3 worldPos, Quaternion worldRot, bool disableVisual)
    {
        if (louie == null)
            return;

        louie.transform.SetParent(null, true);
        louie.transform.SetPositionAndRotation(worldPos, worldRot);

        if (disableVisual)
        {
            if (louie.TryGetComponent<LouieRidingVisual>(out var rv) && rv != null)
                rv.enabled = false;
            else
            {
                var inChild = louie.GetComponentInChildren<LouieRidingVisual>(true);
                if (inChild != null) inChild.enabled = false;
            }
        }
        else
        {
            if (louie.TryGetComponent<LouieRidingVisual>(out var riderVisual))
                Destroy(riderVisual);
        }
    }

    void KillDetachedLouie(GameObject louie)
    {
        if (louie == null)
            return;

        if (louie.TryGetComponent<LouieMovementController>(out var lm))
        {
            lm.enabled = true;
            lm.Kill();
            return;
        }

        if (louie.TryGetComponent<MovementController>(out var mc))
        {
            mc.enabled = true;
            mc.Kill();
            return;
        }

        Destroy(louie);
    }

    void DetachAndKillCurrentLouieOnly()
    {
        if (currentLouie == null)
            return;

        var louie = currentLouie;
        currentLouie = null;

        louie.transform.GetPositionAndRotation(out var worldPos, out var worldRot);

        ClearDashInvulnerabilityNow();
        ResetMountedStateAndAbilities();

        if (movement.TryGetComponent<CharacterHealth>(out var health))
            health.StartTemporaryInvulnerability(playerInvulnerabilityAfterLoseLouieSeconds);

        DetachLouieToWorld(louie, worldPos, worldRot, disableVisual: false);
        KillDetachedLouieGuaranteed(louie);
    }

    void OnDestroy()
    {
        if (movement != null)
            movement.Died -= OnPlayerDied;
    }

    public MountedLouieType GetMountedLouieType()
    {
        return currentLouie == null ? MountedLouieType.None : mountedType;
    }

    public CharacterHealth GetMountedLouieHealth()
    {
        return mountedLouieHealth;
    }

    public bool HasMountedLouie()
    {
        return currentLouie != null;
    }

    public void RestoreMountedFromPersistent()
    {
        if (currentLouie != null)
            return;

        int playerId = GetPlayerId();
        var state = PlayerPersistentStats.Get(playerId);

        RestoreMountedImmediate(state.MountedLouie);
    }

    public bool TryPlayMountedLouieEndStage(float totalTime, int frameCount)
    {
        if (currentLouie == null)
            return false;

        if (!currentLouie.TryGetComponent<LouieRidingVisual>(out var visual) || visual == null)
            visual = currentLouie.GetComponentInChildren<LouieRidingVisual>(true);

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

            ResetLouieAbilitiesExternalState();
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

    bool TryMountFromQueuedEgg()
    {
        if (currentLouie != null)
            return false;

        if (!TryGetComponent<LouieEggQueue>(out var q) || q == null)
            return false;

        if (!q.TryDequeue(out var t, out var sfx, out var vol))
            return false;

        SetNextMountSfx(sfx, vol);

        var type = EggToMountedType(t);
        if (type == MountedLouieType.None)
            return false;

        var prefab = GetPrefab(type);
        if (prefab == null)
            return false;

        TryMount(prefab, type);
        return true;
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

    bool TryMapEggToLouie(ItemPickup.ItemType eggType, out GameObject prefab, out MountedLouieType type)
    {
        type = EggToMountedType(eggType);
        prefab = GetPrefab(type);
        return type != MountedLouieType.None && prefab != null;
    }

    MountedLouieType EggToMountedType(ItemPickup.ItemType eggType)
    {
        return eggType switch
        {
            ItemPickup.ItemType.BlueLouieEgg => MountedLouieType.Blue,
            ItemPickup.ItemType.BlackLouieEgg => MountedLouieType.Black,
            ItemPickup.ItemType.PurpleLouieEgg => MountedLouieType.Purple,
            ItemPickup.ItemType.GreenLouieEgg => MountedLouieType.Green,
            ItemPickup.ItemType.YellowLouieEgg => MountedLouieType.Yellow,
            ItemPickup.ItemType.PinkLouieEgg => MountedLouieType.Pink,
            ItemPickup.ItemType.RedLouieEgg => MountedLouieType.Red,
            _ => MountedLouieType.None
        };
    }

    GameObject GetPrefab(MountedLouieType type)
    {
        return type switch
        {
            MountedLouieType.Blue => blueLouiePrefab,
            MountedLouieType.Black => blackLouiePrefab,
            MountedLouieType.Purple => purpleLouiePrefab,
            MountedLouieType.Green => greenLouiePrefab,
            MountedLouieType.Yellow => yellowLouiePrefab,
            MountedLouieType.Pink => pinkLouiePrefab,
            MountedLouieType.Red => redLouiePrefab,
            _ => null
        };
    }

    void DetachCurrentLouieBeforeRiding()
    {
        if (currentLouie == null)
            return;

        currentLouie.transform.GetPositionAndRotation(out var worldPos, out var worldRot);
        DetachLouieToWorld(currentLouie, worldPos, worldRot, disableVisual: true);
    }

    public bool HandleDamageWhileMounting(int damage)
    {
        var rider = GetComponent<PlayerRidingController>();
        if (rider == null || !rider.IsPlaying)
            return false;

        if (movement != null && movement.isDead)
            return true;

        if (IsRidingUninterruptible())
            return true;

        rider.CancelRiding();

        if (currentLouie != null)
            DetachAndKillCurrentLouieOnly();

        if (movement != null && movement.TryGetComponent<CharacterHealth>(out var health) && health != null)
            health.StartTemporaryInvulnerability(playerInvulnerabilityAfterLoseLouieSeconds);

        return true;
    }

    void PrepareDetachedLouieForGuaranteedDeath(GameObject louie)
    {
        if (louie == null)
            return;

        if (!louie.TryGetComponent<MovementController>(out var mc) || mc == null)
            return;

        var riderVisual = louie.GetComponentInChildren<LouieRidingVisual>(true);
        if (riderVisual != null)
            Destroy(riderVisual);

        var allAnimated = louie.GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        for (int i = 0; i < allAnimated.Length; i++)
        {
            var a = allAnimated[i];
            if (a == null)
                continue;

            bool keep = (mc.spriteRendererDeath != null && a == mc.spriteRendererDeath);
            a.enabled = keep;

            if (a.TryGetComponent<SpriteRenderer>(out var sr) && sr != null)
                sr.enabled = keep;

            var childSrs = a.GetComponentsInChildren<SpriteRenderer>(true);
            for (int s = 0; s < childSrs.Length; s++)
                if (childSrs[s] != null)
                    childSrs[s].enabled = keep;
        }

        if (mc.spriteRendererDeath != null)
        {
            mc.spriteRendererDeath.enabled = true;

            if (mc.spriteRendererDeath.TryGetComponent<SpriteRenderer>(out var deathSr) && deathSr != null)
                deathSr.enabled = true;

            var deathChildSrs = mc.spriteRendererDeath.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < deathChildSrs.Length; i++)
                if (deathChildSrs[i] != null)
                    deathChildSrs[i].enabled = true;

            mc.spriteRendererDeath.idle = false;
            mc.spriteRendererDeath.loop = false;
            mc.spriteRendererDeath.pingPong = false;
            mc.spriteRendererDeath.CurrentFrame = 0;
            mc.spriteRendererDeath.RefreshFrame();
        }
    }

    void KillDetachedLouieGuaranteed(GameObject louie)
    {
        if (louie == null)
            return;

        PrepareDetachedLouieForGuaranteedDeath(louie);

        if (louie.TryGetComponent<LouieMovementController>(out var lm) && lm != null)
        {
            lm.enabled = true;
            lm.Kill();

            if (lm.spriteRendererDeath != null)
            {
                lm.spriteRendererDeath.enabled = true;
                lm.spriteRendererDeath.idle = false;
                lm.spriteRendererDeath.loop = false;
                lm.spriteRendererDeath.CurrentFrame = 0;
                lm.spriteRendererDeath.RefreshFrame();
            }

            return;
        }

        if (louie.TryGetComponent<MovementController>(out var mc) && mc != null)
        {
            mc.enabled = true;
            mc.Kill();

            if (mc.spriteRendererDeath != null)
            {
                mc.spriteRendererDeath.enabled = true;
                mc.spriteRendererDeath.idle = false;
                mc.spriteRendererDeath.loop = false;
                mc.spriteRendererDeath.CurrentFrame = 0;
                mc.spriteRendererDeath.RefreshFrame();
            }

            return;
        }

        Destroy(louie);
    }

    public bool TryDetachMountedLouieToWorldStationary(out GameObject detachedLouie, out MountedLouieType detachedType)
    {
        detachedLouie = null;
        detachedType = MountedLouieType.None;

        if (currentLouie == null || movement == null)
            return false;

        var louie = currentLouie;
        var typeBeforeReset = mountedType;

        currentLouie = null;

        louie.transform.GetPositionAndRotation(out var worldPos, out var worldRot);

        ClearDashInvulnerabilityNow();
        ResetMountedStateAndAbilities();

        louie.transform.SetParent(null, true);
        louie.transform.SetPositionAndRotation(worldPos, worldRot);

        // Remove visual de riding (como você já faz)
        if (louie.TryGetComponent<LouieRidingVisual>(out var rv))
            Destroy(rv);
        else
        {
            var childRv = louie.GetComponentInChildren<LouieRidingVisual>(true);
            if (childRv != null) Destroy(childRv);
        }

        // ✅ NOVO: força o Louie destacado a ficar em IDLE na direção atual do player
        ForceDetachedLouieIdleFacing(louie, movement.FacingDirection);

        // Mantém parado, mas colisão ligada para pickup
        if (louie.TryGetComponent<LouieMovementController>(out var lm))
            lm.enabled = false;

        if (louie.TryGetComponent<Rigidbody2D>(out var rb))
            rb.simulated = true; // precisa estar ativo para trigger/collision

        if (louie.TryGetComponent<Collider2D>(out var col))
            col.enabled = true;

        if (louie.TryGetComponent<BombController>(out var bc))
            bc.enabled = false;

        detachedLouie = louie;
        detachedType = typeBeforeReset;
        return true;
    }

    public bool TryMountExistingLouieFromWorld(GameObject louieWorldInstance, MountedLouieType louieType, LouieEggQueue worldQueueToAdopt)
    {
        if (louieWorldInstance == null || movement == null)
            return false;

        if (currentLouie != null)
            return false;

        var rider = GetComponent<PlayerRidingController>();

        void AdoptWorldQueueIfAny()
        {
            if (worldQueueToAdopt == null)
                return;

            if (!TryGetComponent<LouieEggQueue>(out var playerQueue) || playerQueue == null)
                playerQueue = gameObject.AddComponent<LouieEggQueue>();

            playerQueue.AbsorbAllEggsFromWorldQueue(worldQueueToAdopt, movement);
            Destroy(worldQueueToAdopt);
        }

        if (rider != null && rider.TryPlayRiding(
            movement.FacingDirection,
            onComplete: () =>
            {
                FinalizeMount(louieType);
                AdoptWorldQueueIfAny();
            },
            onStart: () =>
            {
                AttachExistingLouieForMount(louieWorldInstance, louieType, duringRiding: true);
            }))
            return true;

        AttachExistingLouieForMount(louieWorldInstance, louieType, duringRiding: false);
        FinalizeMount(louieType);
        AdoptWorldQueueIfAny();
        return true;
    }

    void AttachExistingLouieForMount(GameObject louieWorldInstance, MountedLouieType type, bool duringRiding)
    {
        if (louieWorldInstance == null || currentLouie != null)
            return;

        mountedType = type;

        currentLouie = louieWorldInstance;

        var pickup = currentLouie.GetComponent<LouieWorldPickup>();
        if (pickup != null) Destroy(pickup);

        currentLouie.transform.SetParent(transform, true);
        currentLouie.transform.SetLocalPositionAndRotation(localOffset, Quaternion.identity);
        currentLouie.transform.localScale = Vector3.one;

        mountedLouieHealth = currentLouie.GetComponentInChildren<CharacterHealth>(true);
        mountedLouieHp = 1;
        if (mountedLouieHealth != null)
            mountedLouieHp = Mathf.Max(1, mountedLouieHealth.life);

        DisableLouieComponentsForMount(currentLouie);

        if (duringRiding)
            SetMountedLouieVisible(true);

        PlayMountSfxIfAny();
    }

    static Vector2 CardinalizeFacing(Vector2 f)
    {
        if (f == Vector2.zero)
            return Vector2.down;

        if (Mathf.Abs(f.x) >= Mathf.Abs(f.y))
            return f.x >= 0f ? Vector2.right : Vector2.left;

        return f.y >= 0f ? Vector2.up : Vector2.down;
    }

    static void SetSpriteRenderersEnabled(Component root, bool enabled)
    {
        if (root == null) return;

        if (root.TryGetComponent<SpriteRenderer>(out var sr) && sr != null)
            sr.enabled = enabled;

        var childSrs = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < childSrs.Length; i++)
            if (childSrs[i] != null)
                childSrs[i].enabled = enabled;
    }

    static void ApplyFlipIfNeeded(AnimatedSpriteRenderer anim, Vector2 facing, bool forceFlipX)
    {
        if (anim == null) return;

        if (!anim.TryGetComponent<SpriteRenderer>(out var sr) || sr == null)
            return;

        var face = CardinalizeFacing(facing);

        if (forceFlipX)
        {
            if (face == Vector2.right) sr.flipX = true;
            else if (face == Vector2.left) sr.flipX = false;
            else sr.flipX = false;

            return;
        }

        if (!anim.allowFlipX)
        {
            sr.flipX = false;
            return;
        }

        if (face == Vector2.right) sr.flipX = true;
        else if (face == Vector2.left) sr.flipX = false;
        else sr.flipX = false;
    }

    static AnimatedSpriteRenderer PickDirectionalFromController(MovementController mc, Vector2 facing, out bool forceFlipX)
    {
        forceFlipX = false;

        if (mc == null) return null;

        var face = CardinalizeFacing(facing);

        if (face == Vector2.up) return mc.spriteRendererUp;
        if (face == Vector2.down) return mc.spriteRendererDown;

        if (face == Vector2.left)
        {
            if (mc.spriteRendererLeft != null) return mc.spriteRendererLeft;

            if (mc.spriteRendererRight != null)
            {
                forceFlipX = true;
                return mc.spriteRendererRight;
            }

            return null;
        }

        if (mc.spriteRendererRight != null) return mc.spriteRendererRight;

        if (mc.spriteRendererLeft != null)
        {
            forceFlipX = true;
            return mc.spriteRendererLeft;
        }

        return null;
    }

    static void ForceDetachedLouieIdleFacing(GameObject louie, Vector2 facing)
    {
        if (louie == null)
            return;

        facing = CardinalizeFacing(facing);

        var anims = louie.GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        if (anims == null || anims.Length == 0)
            return;

        for (int i = 0; i < anims.Length; i++)
        {
            var a = anims[i];
            if (a == null) continue;

            a.enabled = false;
            SetSpriteRenderersEnabled(a, false);
        }

        MovementController mc = louie.GetComponentInChildren<LouieMovementController>(true);
        if (mc == null)
            mc = louie.GetComponentInChildren<MovementController>(true);

        AnimatedSpriteRenderer chosen = PickDirectionalFromController(mc, facing, out bool forceFlipX);

        if (chosen == null)
        {
            for (int i = 0; i < anims.Length; i++)
            {
                if (anims[i] != null)
                {
                    chosen = anims[i];
                    forceFlipX = false;
                    break;
                }
            }
        }

        if (chosen == null)
            return;

        if (!chosen.gameObject.activeSelf)
            chosen.gameObject.SetActive(true);

        chosen.enabled = true;
        SetSpriteRenderersEnabled(chosen, true);

        chosen.idle = true;
        chosen.loop = false;
        chosen.pingPong = false;
        chosen.CurrentFrame = 0;

        ApplyFlipIfNeeded(chosen, facing, forceFlipX);

        chosen.RefreshFrame();
    }
}
