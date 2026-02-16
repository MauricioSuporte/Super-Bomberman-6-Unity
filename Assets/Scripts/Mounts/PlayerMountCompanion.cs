using Assets.Scripts.Interface;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MovementController))]
[RequireComponent(typeof(CharacterHealth))]
[RequireComponent(typeof(PlayerRidingController))]
public class PlayerMountCompanion : MonoBehaviour
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
    [Range(0f, 1f)] public float mountLouieVolume = 1f;
    [SerializeField] string blueLouieMountSfxName = "MountBlueLouie";
    [SerializeField] string blackLouieMountSfxName = "MountBlackLouie";
    [SerializeField] string purpleLouieMountSfxName = "MountPurpleLouie";
    [SerializeField] string greenLouieMountSfxName = "MountGreenLouie";
    [SerializeField] string yellowLouieMountSfxName = "MountYellowLouie";
    [SerializeField] string pinkLouieMountSfxName = "MountPinkLouie";
    [SerializeField] string redLouieMountSfxName = "MountRedLouie";

    readonly Dictionary<MountedType, AudioClip> _worldMountSfxCache = new();

    AudioClip pendingMountSfx;
    float pendingMountVolume = 1f;

    MovementController movement;
    CharacterHealth playerHealth;

    GameObject currentLouie;
    MountedType mountedType = MountedType.None;

    CharacterHealth mountedLouieHealth;
    int mountedLouieHp;

    AbilitySystem abilitySystem;
    BombPunchAbility punchAbility;
    bool punchOwned;

    bool louieAbilitiesLocked;
    bool louieAbilitiesLockApplied;

    float dashInvulRemainingPlayer;
    float dashInvulRemainingLouie;

    float ridingUninterruptibleUntil;

    float playerOriginalBlinkInterval;
    float playerOriginalTempSlowdownStartNormalized;
    float playerOriginalTempEndBlinkMultiplier;
    Coroutine restorePlayerBlinkRoutine;

    readonly Dictionary<MountedType, GameObject> prefabByType = new();
    readonly Dictionary<ItemPickup.ItemType, MountedType> mountedByEggType = new();

    readonly Dictionary<MountedType, Action> applyMountedAbility = new();
    MountAbilitySfxConfig currentAbilityCfg;

    Coroutine autoRemountRoutine;
    bool autoRemountRequested;

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

        BuildTypeMaps();
        BuildAbilityStrategy();

        abilitySystem.RebuildCache();
        punchAbility = abilitySystem.Get<BombPunchAbility>(BombPunchAbility.AbilityId);
        if (punchAbility != null)
            punchAbility.SetLockedByLouie(false);

        punchOwned = PlayerPersistentStats.Get(GetPlayerId()).CanPunchBombs;
    }

    void Update()
    {
        punchOwned = PlayerPersistentStats.Get(GetPlayerId()).CanPunchBombs;

        bool shouldLock =
            MechaBossSequence.MechaIntroRunning ||
            (StageIntroTransition.Instance != null && StageIntroTransition.Instance.IntroRunning);

        if (shouldLock != louieAbilitiesLocked)
            SetLouieAbilitiesLocked(shouldLock);

        TickDashInvulnerability();

        if (currentLouie != null && mountedType != MountedType.Blue)
            EnforceNoPunchWhileMountedNonBlue();
    }

    void OnDestroy()
    {
        if (movement != null)
            movement.Died -= OnPlayerDied;
    }

    #region Mount API (Public)

    public void MountBlueLouie() => Mount(MountedType.Blue);
    public void MountBlackLouie() => Mount(MountedType.Black);
    public void MountPurpleLouie() => Mount(MountedType.Purple);
    public void MountGreenLouie() => Mount(MountedType.Green);
    public void MountYellowLouie() => Mount(MountedType.Yellow);
    public void MountPinkLouie() => Mount(MountedType.Pink);
    public void MountRedLouie() => Mount(MountedType.Red);

    public void RestoreMountedBlueLouie() => RestoreMounted(MountedType.Blue);
    public void RestoreMountedBlackLouie() => RestoreMounted(MountedType.Black);
    public void RestoreMountedPurpleLouie() => RestoreMounted(MountedType.Purple);
    public void RestoreMountedGreenLouie() => RestoreMounted(MountedType.Green);
    public void RestoreMountedYellowLouie() => RestoreMounted(MountedType.Yellow);
    public void RestoreMountedPinkLouie() => RestoreMounted(MountedType.Pink);
    public void RestoreMountedRedLouie() => RestoreMounted(MountedType.Red);

    public MountedType GetMountedLouieType() => currentLouie == null ? MountedType.None : mountedType;
    public CharacterHealth GetMountedLouieHealth() => mountedLouieHealth;
    public bool HasMountedLouie() => currentLouie != null;

    public void RestoreMountedFromPersistent()
    {
        if (currentLouie != null)
            return;

        var state = PlayerPersistentStats.Get(GetPlayerId());
        RestoreMounted(state.MountedLouie);
    }

    public bool TryPlayMountedLouieEndStage(float totalTime, int frameCount)
    {
        var visual = GetLouieRidingVisual(currentLouie);
        if (visual == null)
            return false;

        return visual.TryPlayEndStage(totalTime, frameCount);
    }

    public void Mount(MountedType type)
    {
        if (type == MountedType.None)
            return;

        TryMount(GetPrefab(type), type);
    }

    public void RestoreMounted(MountedType type)
    {
        RestoreMountedImmediate(type);
    }

    #endregion

    #region Core Mount Flow

    void RestoreMountedImmediate(MountedType type)
    {
        if (currentLouie != null || movement == null || type == MountedType.None)
            return;

        var prefab = GetPrefab(type);
        if (prefab == null)
            return;

        mountedType = type;

        currentLouie = Instantiate(prefab, transform);
        SetupLouieAsChildMounted(currentLouie);

        CacheMountedLouieHealth(currentLouie);
        DisableLouieComponentsForMount(currentLouie);

        FinalizeMount(type);
    }

    void TryMount(GameObject prefab, MountedType type)
    {
        if (prefab == null || movement == null || currentLouie != null)
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

    void SpawnLouieForMount(GameObject prefab, MountedType type, bool duringRiding)
    {
        if (prefab == null || currentLouie != null)
            return;

        mountedType = type;

        currentLouie = Instantiate(prefab, transform);
        SetupLouieAsChildMounted(currentLouie);

        CacheMountedLouieHealth(currentLouie);
        DisableLouieComponentsForMount(currentLouie);

        if (duringRiding)
            SetMountedLouieVisible(true);

        PlayMountSfxIfAny();
    }

    void FinalizeMount(MountedType type)
    {
        if (currentLouie == null || movement == null)
            return;

        mountedType = type;

        movement.SetMountedOnLouie(true);

        bool isPink = mountedType == MountedType.Pink;
        movement.SetMountedSpritesLocalYOverride(isPink, movement.pinkMountedSpritesLocalY);

        EnableAndBindLouieRidingVisual(currentLouie);
        SetMountedLouieVisible(true);

        ApplyRulesForCurrentMount();

        movement.Died -= OnPlayerDied;
        movement.Died += OnPlayerDied;
    }

    void SetupLouieAsChildMounted(GameObject louie)
    {
        louie.transform.SetLocalPositionAndRotation(localOffset, Quaternion.identity);
        louie.transform.localScale = Vector3.one;
    }

    void CacheMountedLouieHealth(GameObject louie)
    {
        mountedLouieHealth = louie.GetComponentInChildren<CharacterHealth>(true);
        mountedLouieHp = 1;

        if (mountedLouieHealth != null)
            mountedLouieHp = Mathf.Max(1, mountedLouieHealth.life);
    }

    void DisableLouieComponentsForMount(GameObject louie)
    {
        if (louie == null)
            return;

        if (louie.TryGetComponent<MountMovementController>(out var lm))
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

        EnableAndBindLouieRidingVisual(louie);
    }

    #endregion

    #region Damage / Lose / Unmount

    bool skipQueueRemountOnce;

    public void OnMountedLouieHit(int damage, bool fromExplosion)
    {
        if (currentLouie == null)
            return;

        int dmg = Mathf.Max(1, damage);

        if (fromExplosion)
        {
            bool willDie = false;

            if (mountedLouieHealth != null)
                willDie = (mountedLouieHealth.life - dmg) <= 0;
            else
                willDie = (mountedLouieHp - dmg) <= 0;

            if (willDie)
            {
                if (TryGetComponent<MountEggQueue>(out var q) && q != null)
                    q.AllowEggExplosionDamageForFrames(2);
            }
        }

        if (fromExplosion && mountedLouieHealth != null)
        {
            int lifeAfter = mountedLouieHealth.life - dmg;
            if (lifeAfter <= 0)
                skipQueueRemountOnce = true;
        }

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
        {
            if (fromExplosion)
                skipQueueRemountOnce = true;

            LoseLouie();
        }
    }

    public void LoseLouie()
    {
        if (currentLouie == null)
            return;

        bool allowQueue = !skipQueueRemountOnce;
        skipQueueRemountOnce = false;

        bool hasQueuedEgg = false;
        GameObject queuedPrefab = null;
        MountedType queuedMountedType = MountedType.None;
        AudioClip queuedSfx = null;
        float queuedVol = 1f;

        if (allowQueue)
            hasQueuedEgg = TryPopQueuedEgg(out queuedPrefab, out queuedMountedType, out queuedSfx, out queuedVol);

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

        DetachLouieToWorld(louie, worldPos, worldRot, disableRidingVisual: false);
        KillDetachedLouieGuaranteed(louie);

        if (currentLouie == null)
            RequestAutoRemountFromQueue();
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

        DetachLouieToWorld(louie, worldPos, worldRot, disableRidingVisual: false);
        KillDetachedLouieGuaranteed(louie);
    }

    #endregion

    #region World Detach / Mount Existing

    public bool TryDetachMountedLouieToWorldStationary(out GameObject detachedLouie, out MountedType detachedType)
    {
        detachedLouie = null;
        detachedType = MountedType.None;

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

        DisableLouieRidingVisualForWorld(louie);

        LouieVisualUtils.ForceDetachedLouieIdleFacing(louie, movement.FacingDirection);

        if (louie.TryGetComponent<MountMovementController>(out var lm))
            lm.enabled = false;

        if (louie.TryGetComponent<Rigidbody2D>(out var rb))
            rb.simulated = true;

        if (louie.TryGetComponent<Collider2D>(out var col))
            col.enabled = true;

        if (louie.TryGetComponent<BombController>(out var bc))
            bc.enabled = false;

        if (louie.TryGetComponent<MovementController>(out var mc) && mc != null)
            mc.SetExplosionInvulnerable(false);

        detachedLouie = louie;
        detachedType = typeBeforeReset;
        return true;
    }

    public bool TryMountExistingLouieFromWorld(GameObject louieWorldInstance, MountedType louieType, MountEggQueue worldQueueToAdopt)
    {
        if (louieWorldInstance == null || movement == null || currentLouie != null)
            return false;

        PrepareWorldRemountSfxIfNone(louieType);

        var rider = GetComponent<PlayerRidingController>();

        void AdoptWorldQueueIfAny()
        {
            if (worldQueueToAdopt == null)
                return;

            if (!TryGetComponent<MountEggQueue>(out var playerQueue) || playerQueue == null)
                playerQueue = gameObject.AddComponent<MountEggQueue>();

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
            onStart: () => AttachExistingLouieForMount(louieWorldInstance, louieType, duringRiding: true)))
            return true;

        AttachExistingLouieForMount(louieWorldInstance, louieType, duringRiding: false);
        FinalizeMount(louieType);
        AdoptWorldQueueIfAny();
        return true;
    }


    void AttachExistingLouieForMount(GameObject louieWorldInstance, MountedType type, bool duringRiding)
    {
        if (louieWorldInstance == null || currentLouie != null)
            return;

        mountedType = type;
        currentLouie = louieWorldInstance;

        var pickup = currentLouie.GetComponent<MountWorldPickup>();
        if (pickup != null) Destroy(pickup);

        currentLouie.transform.SetParent(transform, true);
        SetupLouieAsChildMounted(currentLouie);

        LouieVisualUtils.RestoreLouieVisualAfterWorldDetach(currentLouie);
        EnableAndBindLouieRidingVisual(currentLouie);

        CacheMountedLouieHealth(currentLouie);
        DisableLouieComponentsForMount(currentLouie);

        if (duringRiding)
            SetMountedLouieVisible(true);

        PlayMountSfxIfAny();
    }

    void DetachCurrentLouieBeforeRiding()
    {
        if (currentLouie == null)
            return;

        currentLouie.transform.GetPositionAndRotation(out var worldPos, out var worldRot);
        DetachLouieToWorld(currentLouie, worldPos, worldRot, disableRidingVisual: true);
    }

    void DetachLouieToWorld(GameObject louie, Vector3 worldPos, Quaternion worldRot, bool disableRidingVisual)
    {
        if (louie == null)
            return;

        louie.transform.SetParent(null, true);
        louie.transform.SetPositionAndRotation(worldPos, worldRot);

        if (disableRidingVisual)
            DisableLouieRidingVisualForWorld(louie);
        else
        {
            var rv = GetLouieRidingVisual(louie);
            if (rv != null)
                Destroy(rv);
        }
    }

    #endregion

    #region Riding / Damage While Mounting

    bool IsRidingUninterruptible() => Time.time < ridingUninterruptibleUntil;

    void MarkRidingUninterruptible(float seconds, bool blink)
    {
        float s = Mathf.Max(0.01f, seconds);
        ridingUninterruptibleUntil = Mathf.Max(ridingUninterruptibleUntil, Time.time + s);

        if (playerHealth != null)
            playerHealth.StartTemporaryInvulnerability(s, withBlink: blink);
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

    #endregion

    #region Invulnerability (Dash)

    void TickDashInvulnerability()
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

    #endregion

    #region Blink Sync

    void SyncPlayerBlinkWithLouie()
    {
        if (!blinkPlayerTogetherWithLouie || playerHealth == null || mountedLouieHealth == null)
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

    #endregion

    #region Abilities

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
        EnsurePunchAbilityCached();

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

    void ApplyRulesForCurrentMount()
    {
        if (abilitySystem == null)
            return;

        abilitySystem.RebuildCache();
        EnsurePunchAbilityCached();

        if (currentLouie == null || mountedType == MountedType.None)
        {
            if (punchAbility != null)
            {
                punchAbility.SetExternalAnimator(null);
                punchAbility.SetLockedByLouie(louieAbilitiesLocked);
            }

            RestorePunchAfterUnmount();
            return;
        }

        ResetLouieAbilitiesExternalState();

        currentAbilityCfg = currentLouie.GetComponentInChildren<MountAbilitySfxConfig>(true);

        if (mountedType == MountedType.Blue)
        {
            abilitySystem.Enable(BombPunchAbility.AbilityId);

            var external = currentLouie.GetComponentInChildren<IBombPunchExternalAnimator>(true);

            EnsurePunchAbilityCached();
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

        if (applyMountedAbility.TryGetValue(mountedType, out var applier) && applier != null)
            applier();

        currentAbilityCfg = null;
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

    void EnforceNoPunchWhileMountedNonBlue()
    {
        if (abilitySystem == null)
            return;

        EnsurePunchAbilityCached();

        if (punchOwned)
        {
            if (!abilitySystem.IsEnabled(BombPunchAbility.AbilityId))
                abilitySystem.Enable(BombPunchAbility.AbilityId);

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
        if (abilitySystem == null)
            return;

        abilitySystem.RebuildCache();
        EnsurePunchAbilityCached();

        if (punchAbility != null)
        {
            punchAbility.SetExternalAnimator(null);
            punchAbility.SetLockedByLouie(false);
        }

        if (punchOwned) abilitySystem.Enable(BombPunchAbility.AbilityId);
        else abilitySystem.Disable(BombPunchAbility.AbilityId);
    }

    void EnsurePunchAbilityCached()
    {
        if (abilitySystem == null)
            return;

        if (punchAbility == null)
            punchAbility = abilitySystem.Get<BombPunchAbility>(BombPunchAbility.AbilityId);
    }

    void ApplyGreen()
    {
        abilitySystem.Enable(GreenLouieDashAbility.AbilityId);
        var dash = abilitySystem.Get<GreenLouieDashAbility>(GreenLouieDashAbility.AbilityId);
        if (dash == null) return;

        var anim = currentLouie.GetComponentInChildren<IGreenLouieDashExternalAnimator>(true);
        dash.SetExternalAnimator(anim);

        if (currentAbilityCfg != null) dash.SetDashSfx(currentAbilityCfg.abilitySfx, currentAbilityCfg.abilityVolume);
        else dash.SetDashSfx(null, 1f);
    }

    void ApplyYellow()
    {
        abilitySystem.Enable(YellowLouieDestructibleKickAbility.AbilityId);
        var kick = abilitySystem.Get<YellowLouieDestructibleKickAbility>(YellowLouieDestructibleKickAbility.AbilityId);
        if (kick == null) return;

        var anim = currentLouie.GetComponentInChildren<IYellowLouieDestructibleKickExternalAnimator>(true);
        kick.SetExternalAnimator(anim);

        if (currentAbilityCfg != null) kick.SetKickSfx(currentAbilityCfg.abilitySfx, currentAbilityCfg.abilityVolume);
        else kick.SetKickSfx(null, 1f);
    }

    void ApplyPink()
    {
        abilitySystem.Enable(PinkLouieJumpAbility.AbilityId);
        var jump = abilitySystem.Get<PinkLouieJumpAbility>(PinkLouieJumpAbility.AbilityId);
        if (jump == null) return;

        var anim = currentLouie.GetComponentInChildren<IPinkLouieJumpExternalAnimator>(true);
        jump.SetExternalAnimator(anim);

        if (currentAbilityCfg != null) jump.SetJumpSfx(currentAbilityCfg.abilitySfx, currentAbilityCfg.abilityVolume);
        else jump.SetJumpSfx(null, 1f);
    }

    void ApplyRed()
    {
        abilitySystem.Enable(RedLouiePunchStunAbility.AbilityId);
        var stun = abilitySystem.Get<RedLouiePunchStunAbility>(RedLouiePunchStunAbility.AbilityId);
        if (stun == null) return;

        var anim = currentLouie.GetComponentInChildren<IRedLouiePunchExternalAnimator>(true);
        stun.SetExternalAnimator(anim);

        if (currentAbilityCfg != null) stun.SetPunchSfx(currentAbilityCfg.abilitySfx, currentAbilityCfg.abilityVolume);
        else stun.SetPunchSfx(null, 1f);
    }

    void ApplyBlack()
    {
        abilitySystem.Enable(BlackLouieDashPushAbility.AbilityId);
        var blackDash = abilitySystem.Get<BlackLouieDashPushAbility>(BlackLouieDashPushAbility.AbilityId);
        if (blackDash == null) return;

        var anim = currentLouie.GetComponentInChildren<IBlackLouieDashExternalAnimator>(true);
        blackDash.SetExternalAnimator(anim);

        if (currentAbilityCfg != null) blackDash.SetDashSfx(currentAbilityCfg.abilitySfx, currentAbilityCfg.abilityVolume);
        else blackDash.SetDashSfx(null, 1f);
    }

    void ApplyPurple()
    {
        abilitySystem.Enable(PurpleLouieBombLineAbility.AbilityId);
        var purple = abilitySystem.Get<PurpleLouieBombLineAbility>(PurpleLouieBombLineAbility.AbilityId);
        if (purple == null) return;

        var anim = currentLouie.GetComponentInChildren<IPurpleLouieBombLineExternalAnimator>(true);
        purple.SetExternalAnimator(anim);
    }

    #endregion

    #region Reset State

    void ResetMountedStateAndAbilities()
    {
        mountedLouieHp = 0;
        mountedLouieHealth = null;

        movement.SetMountedSpritesLocalYOverride(false, 0f);
        movement.SetMountedOnLouie(false);

        mountedType = MountedType.None;

        ResetLouieAbilitiesExternalState();
        RestorePunchAfterUnmount();
    }

    #endregion

    #region Queue Mount

    bool TryPopQueuedEgg(out GameObject prefab, out MountedType type, out AudioClip sfx, out float vol)
    {
        prefab = null;
        type = MountedType.None;
        sfx = null;
        vol = 1f;

        if (!TryGetComponent<MountEggQueue>(out var q) || q == null || q.Count <= 0)
            return false;

        if (!q.TryDequeue(out var queuedEggType, out sfx, out vol))
            return false;

        type = EggToMountedType(queuedEggType);
        prefab = GetPrefab(type);

        return type != MountedType.None && prefab != null;
    }

    bool TryMountFromQueuedEgg()
    {
        if (currentLouie != null)
            return false;

        if (!TryGetComponent<MountEggQueue>(out var q) || q == null)
            return false;

        if (!q.TryDequeue(out var eggType, out var sfx, out var vol))
            return false;

        SetNextMountSfx(sfx, vol);

        var type = EggToMountedType(eggType);
        if (type == MountedType.None)
            return false;

        var prefab = GetPrefab(type);
        if (prefab == null)
            return false;

        TryMount(prefab, type);
        return true;
    }

    void RequestAutoRemountFromQueue()
    {
        autoRemountRequested = true;

        if (autoRemountRoutine != null)
            return;

        autoRemountRoutine = StartCoroutine(AutoRemountFromQueueRoutine());
    }

    IEnumerator AutoRemountFromQueueRoutine()
    {
        yield return null;

        int safetyFrames = 240;
        while (safetyFrames-- > 0)
        {
            autoRemountRequested = false;

            if (!gameObject.activeInHierarchy)
                break;

            if (currentLouie != null)
                break;

            var rider = GetComponent<PlayerRidingController>();
            if (rider != null && rider.IsPlaying)
            {
                yield return null;
                continue;
            }

            if (!TryGetComponent<MountEggQueue>(out var q) || q == null || q.Count <= 0)
                break;

            bool mounted = TryMountFromQueuedEgg();
            if (mounted)
                break;

            yield return null;

            if (autoRemountRequested)
                continue;
        }

        autoRemountRoutine = null;
        autoRemountRequested = false;
    }

    #endregion

    #region SFX

    AudioClip LoadWorldMountSfx(MountedType type)
    {
        if (type == MountedType.None)
            return null;

        if (_worldMountSfxCache.TryGetValue(type, out var cached))
            return cached;

        string name = type switch
        {
            MountedType.Blue => blueLouieMountSfxName,
            MountedType.Black => blackLouieMountSfxName,
            MountedType.Purple => purpleLouieMountSfxName,
            MountedType.Green => greenLouieMountSfxName,
            MountedType.Yellow => yellowLouieMountSfxName,
            MountedType.Pink => pinkLouieMountSfxName,
            MountedType.Red => redLouieMountSfxName,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(name))
        {
            _worldMountSfxCache[type] = null;
            return null;
        }

        var clip = Resources.Load<AudioClip>($"Sounds/{name}");
        _worldMountSfxCache[type] = clip;
        return clip;
    }

    void PrepareWorldRemountSfxIfNone(MountedType type)
    {
        if (pendingMountSfx != null)
            return;

        var clip = LoadWorldMountSfx(type);
        if (clip == null)
            return;

        SetNextMountSfx(clip, mountLouieVolume);
    }

    public void SetNextMountSfx(AudioClip clip, float volume)
    {
        pendingMountSfx = clip;
        pendingMountVolume = Mathf.Clamp01(volume);
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

    AudioSource FindAudioSource()
    {
        var a = GetComponent<AudioSource>();
        if (a != null) return a;

        a = GetComponentInChildren<AudioSource>(true);
        if (a != null) return a;

        return GetComponentInParent<AudioSource>(true);
    }

    #endregion

    #region Visibility

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

    #endregion

    #region Visual Binding (LouieRidingVisual)

    static MountVisualController GetLouieRidingVisual(GameObject louie)
    {
        if (louie == null)
            return null;

        if (louie.TryGetComponent<MountVisualController>(out var visual) && visual != null)
            return visual;

        return louie.GetComponentInChildren<MountVisualController>(true);
    }

    void DisableLouieRidingVisualForWorld(GameObject louie)
    {
        var visual = GetLouieRidingVisual(louie);
        if (visual != null)
            visual.enabled = false;
    }

    void EnableAndBindLouieRidingVisual(GameObject louie)
    {
        if (louie == null || movement == null)
            return;

        var visual = GetLouieRidingVisual(louie);
        if (visual == null)
            return;

        visual.localOffset = localOffset;
        visual.Bind(movement);
        visual.enabled = true;
    }

    #endregion

    #region Kill Detached (Guaranteed Death)

    void PrepareDetachedLouieForGuaranteedDeath(GameObject louie)
    {
        if (louie == null)
            return;

        if (!louie.TryGetComponent<MovementController>(out var mc) || mc == null)
            return;

        var riderVisual = louie.GetComponentInChildren<MountVisualController>(true);
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

        if (louie.TryGetComponent<MountMovementController>(out var lm) && lm != null)
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

    #endregion

    #region Helpers (Prefab / Egg / PlayerId / Maps)

    void BuildTypeMaps()
    {
        prefabByType.Clear();
        prefabByType[MountedType.Blue] = blueLouiePrefab;
        prefabByType[MountedType.Black] = blackLouiePrefab;
        prefabByType[MountedType.Purple] = purpleLouiePrefab;
        prefabByType[MountedType.Green] = greenLouiePrefab;
        prefabByType[MountedType.Yellow] = yellowLouiePrefab;
        prefabByType[MountedType.Pink] = pinkLouiePrefab;
        prefabByType[MountedType.Red] = redLouiePrefab;

        mountedByEggType.Clear();
        mountedByEggType[ItemPickup.ItemType.BlueLouieEgg] = MountedType.Blue;
        mountedByEggType[ItemPickup.ItemType.BlackLouieEgg] = MountedType.Black;
        mountedByEggType[ItemPickup.ItemType.PurpleLouieEgg] = MountedType.Purple;
        mountedByEggType[ItemPickup.ItemType.GreenLouieEgg] = MountedType.Green;
        mountedByEggType[ItemPickup.ItemType.YellowLouieEgg] = MountedType.Yellow;
        mountedByEggType[ItemPickup.ItemType.PinkLouieEgg] = MountedType.Pink;
        mountedByEggType[ItemPickup.ItemType.RedLouieEgg] = MountedType.Red;
    }

    void BuildAbilityStrategy()
    {
        applyMountedAbility.Clear();
        applyMountedAbility[MountedType.Purple] = ApplyPurple;
        applyMountedAbility[MountedType.Green] = ApplyGreen;
        applyMountedAbility[MountedType.Yellow] = ApplyYellow;
        applyMountedAbility[MountedType.Pink] = ApplyPink;
        applyMountedAbility[MountedType.Red] = ApplyRed;
        applyMountedAbility[MountedType.Black] = ApplyBlack;
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

    MountedType EggToMountedType(ItemPickup.ItemType eggType)
    {
        return mountedByEggType.TryGetValue(eggType, out var t) ? t : MountedType.None;
    }

    GameObject GetPrefab(MountedType type)
    {
        return prefabByType.TryGetValue(type, out var p) ? p : null;
    }

    #endregion

    #region Louie Visual Utils (World Idle Facing)

    static class LouieVisualUtils
    {
        static Vector2 Cardinalize(Vector2 f)
        {
            if (f == Vector2.zero)
                return Vector2.down;

            if (Mathf.Abs(f.x) >= Mathf.Abs(f.y))
                return f.x >= 0f ? Vector2.right : Vector2.left;

            return f.y >= 0f ? Vector2.up : Vector2.down;
        }

        static bool IsUnderRidingVisual(AnimatedSpriteRenderer a)
        {
            if (a == null) return false;
            return a.GetComponentInParent<MountVisualController>(true) != null;
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

            var face = Cardinalize(facing);

            if (forceFlipX)
            {
                sr.flipX = (face == Vector2.right);
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

            var face = Cardinalize(facing);

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

        public static void ForceDetachedLouieIdleFacing(GameObject louie, Vector2 facing)
        {
            if (louie == null)
                return;

            facing = Cardinalize(facing);

            var anims = louie.GetComponentsInChildren<AnimatedSpriteRenderer>(true);
            if (anims == null || anims.Length == 0)
                return;

            for (int i = 0; i < anims.Length; i++)
            {
                var a = anims[i];
                if (a == null || IsUnderRidingVisual(a))
                    continue;

                a.enabled = false;
                SetSpriteRenderersEnabled(a, false);
            }

            MovementController mc = louie.GetComponentInChildren<MountMovementController>(true);
            if (mc == null)
                mc = louie.GetComponentInChildren<MovementController>(true);

            AnimatedSpriteRenderer chosen = PickDirectionalFromController(mc, facing, out bool forceFlipX);

            if (chosen == null)
            {
                for (int i = 0; i < anims.Length; i++)
                {
                    if (anims[i] != null && !IsUnderRidingVisual(anims[i]))
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

        public static void RestoreLouieVisualAfterWorldDetach(GameObject louie)
        {
            if (louie == null)
                return;

            var anims = louie.GetComponentsInChildren<AnimatedSpriteRenderer>(true);
            if (anims == null || anims.Length == 0)
                return;

            for (int i = 0; i < anims.Length; i++)
            {
                var a = anims[i];
                if (a == null)
                    continue;

                if (!a.gameObject.activeSelf)
                    a.gameObject.SetActive(true);

                a.SetFrozen(false);
                a.ClearRuntimeBaseOffset();
                a.ClearRuntimeBaseLocalX();
            }
        }
    }

    #endregion
}
