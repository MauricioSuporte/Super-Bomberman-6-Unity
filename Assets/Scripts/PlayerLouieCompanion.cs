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

    [Header("Local Offset")]
    public Vector2 localOffset = new(0f, -0.15f);

    [Header("Louie Death")]
    public float louieDeathSeconds = 0.5f;

    [Header("Player Invulnerability After Losing Louie")]
    public float playerInvulnerabilityAfterLoseLouieSeconds = 0.8f;

    [Header("Visual Sync")]
    public bool blinkPlayerTogetherWithLouie = true;

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

    PlayerPersistentStats.MountedLouieType mountedType = PlayerPersistentStats.MountedLouieType.None;

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
    }

    void Update()
    {
        if (currentLouie != null && mountedType != PlayerPersistentStats.MountedLouieType.Blue)
            EnforceNoPunchWhileMountedNonBlue();
    }

    public void MountBlueLouie() => MountLouieInternal(blueLouiePrefab, PlayerPersistentStats.MountedLouieType.Blue);
    public void MountBlackLouie() => MountLouieInternal(blackLouiePrefab, PlayerPersistentStats.MountedLouieType.Black);
    public void MountPurpleLouie() => MountLouieInternal(purpleLouiePrefab, PlayerPersistentStats.MountedLouieType.Purple);
    public void MountGreenLouie() => MountLouieInternal(greenLouiePrefab, PlayerPersistentStats.MountedLouieType.Green);
    public void MountYellowLouie() => MountLouieInternal(yellowLouiePrefab, PlayerPersistentStats.MountedLouieType.Yellow);

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

        if (mountedType == PlayerPersistentStats.MountedLouieType.Purple)
            PlayerPersistentStats.MountedLouieLife = v;
    }

    void MountLouieInternal(GameObject prefab, PlayerPersistentStats.MountedLouieType type)
    {
        if (prefab == null || movement == null)
            return;

        if (currentLouie != null)
            return;

        mountedType = type;

        currentLouie = Instantiate(prefab, transform);
        currentLouie.transform.SetLocalPositionAndRotation(localOffset, Quaternion.identity);
        currentLouie.transform.localScale = Vector3.one;

        mountedLouieHealth = currentLouie.GetComponentInChildren<CharacterHealth>(true);

        mountedLouieHp = 1;
        if (mountedLouieHealth != null)
            mountedLouieHp = Mathf.Max(1, mountedLouieHealth.life);

        if (mountedType == PlayerPersistentStats.MountedLouieType.Purple)
        {
            if (PlayerPersistentStats.MountedLouieLife > 0)
                SetMountedLouieLife(PlayerPersistentStats.MountedLouieLife);
            else
                PlayerPersistentStats.MountedLouieLife = Mathf.Max(1, mountedLouieHp);
        }
        else
        {
            PlayerPersistentStats.MountedLouieLife = 0;
        }

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

        if (currentLouie.TryGetComponent<LouieRiderVisual>(out var visual))
        {
            visual.localOffset = localOffset;
            visual.Bind(movement);
        }

        abilitySystem.RebuildCache();
        punchAbility = abilitySystem.Get<BombPunchAbility>(BombPunchAbility.AbilityId);
        punchOwned |= abilitySystem.IsEnabled(BombPunchAbility.AbilityId);

        ApplyRulesForCurrentMount();

        movement.Died += OnPlayerDied;
    }

    void ApplyRulesForCurrentMount()
    {
        abilitySystem.Disable(PurpleLouieBombLineAbility.AbilityId);
        abilitySystem.Disable(GreenLouieDashAbility.AbilityId);
        abilitySystem.Disable(YellowLouieDestructibleKickAbility.AbilityId);

        if (mountedType == PlayerPersistentStats.MountedLouieType.Blue)
        {
            var external = currentLouie != null
                ? currentLouie.GetComponentInChildren<IBombPunchExternalAnimator>(true)
                : null;

            abilitySystem.Enable(BombPunchAbility.AbilityId);

            punchAbility = abilitySystem.Get<BombPunchAbility>(BombPunchAbility.AbilityId);
            if (punchAbility != null)
                punchAbility.SetExternalAnimator(external);

            return;
        }

        if (punchAbility != null)
            punchAbility.SetExternalAnimator(null);

        abilitySystem.Disable(BombPunchAbility.AbilityId);

        if (mountedType == PlayerPersistentStats.MountedLouieType.Purple)
        {
            abilitySystem.Enable(PurpleLouieBombLineAbility.AbilityId);

            var purpleAbility = abilitySystem.Get<PurpleLouieBombLineAbility>(PurpleLouieBombLineAbility.AbilityId);
            if (purpleAbility != null)
            {
                purpleAbility.triggerKey = KeyCode.B;

                var anim = currentLouie != null
                    ? currentLouie.GetComponentInChildren<IPurpleLouieBombLineExternalAnimator>(true)
                    : null;

                purpleAbility.SetExternalAnimator(anim);
            }

            return;
        }

        if (mountedType == PlayerPersistentStats.MountedLouieType.Green)
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
                    ? currentLouie.GetComponentInChildren<GreenLouieDashSfxConfig>(true)
                    : null;

                if (cfg != null)
                    dash.SetDashSfx(cfg.dashSfx, cfg.dashVolume);
                else
                    dash.SetDashSfx(null, 1f);
            }

            return;
        }

        if (mountedType == PlayerPersistentStats.MountedLouieType.Yellow)
        {
            abilitySystem.Enable(YellowLouieDestructibleKickAbility.AbilityId);

            var kick = abilitySystem.Get<YellowLouieDestructibleKickAbility>(YellowLouieDestructibleKickAbility.AbilityId);
            if (kick != null)
            {
                kick.triggerKey = KeyCode.B;

                var anim = currentLouie != null
                    ? currentLouie.GetComponentInChildren<IYellowLouieDestructibleKickExternalAnimator>(true)
                    : null;

                kick.SetExternalAnimator(anim);

                // se você criar um config tipo GreenLouieDashSfxConfig, pode setar aqui também
                // kick.SetKickSfx(...);
            }

            return;
        }
    }

    void EnforceNoPunchWhileMountedNonBlue()
    {
        abilitySystem.RebuildCache();

        if (abilitySystem.IsEnabled(BombPunchAbility.AbilityId))
        {
            punchOwned = true;

            punchAbility = abilitySystem.Get<BombPunchAbility>(BombPunchAbility.AbilityId);
            if (punchAbility != null)
                punchAbility.SetExternalAnimator(null);

            abilitySystem.Disable(BombPunchAbility.AbilityId);
        }
    }

    void RestorePunchAfterUnmount()
    {
        abilitySystem.RebuildCache();

        punchAbility = abilitySystem.Get<BombPunchAbility>(BombPunchAbility.AbilityId);
        if (punchAbility != null)
            punchAbility.SetExternalAnimator(null);

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

            if (mountedType == PlayerPersistentStats.MountedLouieType.Purple)
                PlayerPersistentStats.MountedLouieLife = Mathf.Max(0, mountedLouieHp);

            if (mountedLouieHealth.life > 0)
                SyncPlayerBlinkWithLouie();

            if (mountedLouieHealth.life <= 0)
                LoseLouie();

            return;
        }

        mountedLouieHp -= dmg;

        if (mountedType == PlayerPersistentStats.MountedLouieType.Purple)
            PlayerPersistentStats.MountedLouieLife = Mathf.Max(0, mountedLouieHp);

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

        var louie = currentLouie;
        currentLouie = null;

        mountedLouieHp = 0;
        mountedLouieHealth = null;

        PlayerPersistentStats.MountedLouieLife = 0;

        louie.transform.GetPositionAndRotation(out var worldPos, out var worldRot);

        movement.SetMountedOnLouie(false);
        mountedType = PlayerPersistentStats.MountedLouieType.None;

        abilitySystem.Disable(PurpleLouieBombLineAbility.AbilityId);
        abilitySystem.Disable(GreenLouieDashAbility.AbilityId);
        abilitySystem.Disable(YellowLouieDestructibleKickAbility.AbilityId);

        var kick = abilitySystem.Get<YellowLouieDestructibleKickAbility>(YellowLouieDestructibleKickAbility.AbilityId);
        if (kick != null)
            kick.SetExternalAnimator(null);

        var dash = abilitySystem.Get<GreenLouieDashAbility>(GreenLouieDashAbility.AbilityId);
        if (dash != null)
        {
            dash.SetExternalAnimator(null);
            dash.SetDashSfx(null, 1f);
        }

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

    void OnPlayerDied(MovementController _) => UnmountLouie();

    void UnmountLouie()
    {
        if (currentLouie != null)
            Destroy(currentLouie);

        currentLouie = null;

        mountedLouieHp = 0;
        mountedLouieHealth = null;

        PlayerPersistentStats.MountedLouieLife = 0;

        movement.SetMountedOnLouie(false);
        mountedType = PlayerPersistentStats.MountedLouieType.None;

        abilitySystem.Disable(PurpleLouieBombLineAbility.AbilityId);
        abilitySystem.Disable(GreenLouieDashAbility.AbilityId);

        abilitySystem.Disable(YellowLouieDestructibleKickAbility.AbilityId);

        var kick = abilitySystem.Get<YellowLouieDestructibleKickAbility>(YellowLouieDestructibleKickAbility.AbilityId);
        if (kick != null)
            kick.SetExternalAnimator(null);

        var dash = abilitySystem.Get<GreenLouieDashAbility>(GreenLouieDashAbility.AbilityId);
        if (dash != null)
        {
            dash.SetExternalAnimator(null);
            dash.SetDashSfx(null, 1f);
        }

        RestorePunchAfterUnmount();
    }

    void OnDestroy()
    {
        if (movement != null)
            movement.Died -= OnPlayerDied;
    }

    public PlayerPersistentStats.MountedLouieType GetMountedLouieType()
    {
        return currentLouie == null
            ? PlayerPersistentStats.MountedLouieType.None
            : mountedType;
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
}
