using System.Collections;
using UnityEngine;

[RequireComponent(typeof(MovementController))]
public class PlayerLouieCompanion : MonoBehaviour
{
    [Header("Louie Prefabs")]
    public GameObject blueLouiePrefab;
    public GameObject blackLouiePrefab;

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
    bool mountedIsBlue;

    private void Awake()
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

    private void Update()
    {
        if (currentLouie != null && !mountedIsBlue)
            EnforceNoPunchWhileMountedNonBlue();
    }

    public void MountBlueLouie() => MountLouieInternal(blueLouiePrefab, true);
    public void MountBlackLouie() => MountLouieInternal(blackLouiePrefab, false);

    private void MountLouieInternal(GameObject prefab, bool isBlue)
    {
        if (prefab == null || movement == null)
            return;

        if (currentLouie != null)
            return;

        mountedIsBlue = isBlue;

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

        if (currentLouie.TryGetComponent<LouieRiderVisual>(out var visual))
        {
            visual.localOffset = localOffset;
            visual.Bind(movement);
        }

        abilitySystem.RebuildCache();
        punchAbility = abilitySystem.Get<BombPunchAbility>(BombPunchAbility.AbilityId);

        punchOwned |= abilitySystem.IsEnabled(BombPunchAbility.AbilityId);

        ApplyPunchRulesForCurrentMount();

        movement.Died += OnPlayerDied;
    }

    private void ApplyPunchRulesForCurrentMount()
    {
        if (mountedIsBlue)
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
    }

    private void EnforceNoPunchWhileMountedNonBlue()
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

    private void RestorePunchAfterUnmount()
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

    private void SyncPlayerBlinkWithLouie()
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

    private IEnumerator RestorePlayerBlinkDefaultsAfter(float seconds)
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

        louie.transform.GetPositionAndRotation(out var worldPos, out var worldRot);

        movement.SetMountedOnLouie(false);
        mountedIsBlue = false;

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

    private void OnPlayerDied(MovementController _)
    {
        UnmountLouie();
    }

    private void UnmountLouie()
    {
        if (currentLouie != null)
            Destroy(currentLouie);

        currentLouie = null;

        mountedLouieHp = 0;
        mountedLouieHealth = null;

        movement.SetMountedOnLouie(false);
        mountedIsBlue = false;

        RestorePunchAfterUnmount();
    }

    private void OnDestroy()
    {
        if (movement != null)
            movement.Died -= OnPlayerDied;
    }

    public PlayerPersistentStats.MountedLouieType GetMountedLouieType()
    {
        if (currentLouie == null)
            return PlayerPersistentStats.MountedLouieType.None;

        return mountedIsBlue
            ? PlayerPersistentStats.MountedLouieType.Blue
            : PlayerPersistentStats.MountedLouieType.Black;
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
}
