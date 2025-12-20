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

    private MovementController movement;
    private GameObject currentLouie;

    private int mountedLouieHp;
    private CharacterHealth mountedLouieHealth;

    private CharacterHealth playerHealth;
    private float playerOriginalBlinkInterval;
    private float playerOriginalTempSlowdownStartNormalized;
    private float playerOriginalTempEndBlinkMultiplier;
    private Coroutine restorePlayerBlinkRoutine;

    private bool hadPunchBeforeMount;
    private BombPunchAbility punchAbility;

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

        if (!TryGetComponent<AbilitySystem>(out var ab))
            ab = gameObject.AddComponent<AbilitySystem>();

        ab.RebuildCache();
        punchAbility = ab.Get<BombPunchAbility>(BombPunchAbility.AbilityId);
    }

    public void MountBlueLouie() => MountLouieInternal(blueLouiePrefab);
    public void MountBlackLouie() => MountLouieInternal(blackLouiePrefab);

    private void MountLouieInternal(GameObject prefab)
    {
        if (prefab == null || movement == null)
            return;

        if (currentLouie != null)
            return;

        currentLouie = Instantiate(prefab, transform);
        currentLouie.transform.SetLocalPositionAndRotation(localOffset, Quaternion.identity);
        currentLouie.transform.localScale = Vector3.one;

        // Pode estar no root ou em child
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

        EnsurePunchBombWhileMounted(currentLouie);

        movement.Died += OnPlayerDied;
    }

    public void OnMountedLouieHit(int damage)
    {
        if (currentLouie == null)
            return;

        int dmg = Mathf.Max(1, damage);

        // Fonte da verdade: CharacterHealth do Louie (pra blink/invuln funcionar)
        if (mountedLouieHealth != null)
        {
            // Precisa do public bool IsInvulnerable => isInvulnerable; no CharacterHealth
            if (mountedLouieHealth.IsInvulnerable)
                return;

            mountedLouieHealth.TakeDamage(dmg);
            mountedLouieHp = Mathf.Max(0, mountedLouieHealth.life);

            // Se ainda está vivo, sincroniza o blink do player com o do Louie
            if (mountedLouieHealth.life > 0)
                SyncPlayerBlinkWithLouie();

            if (mountedLouieHealth.life <= 0)
                LoseLouie();

            return;
        }

        // Fallback (se por algum motivo não houver CharacterHealth)
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

        // Ajusta visual do Player pra bater com o Louie
        playerHealth.hitBlinkInterval = mountedLouieHealth.hitBlinkInterval;
        playerHealth.tempSlowdownStartNormalized = mountedLouieHealth.tempSlowdownStartNormalized;
        playerHealth.tempEndBlinkMultiplier = mountedLouieHealth.tempEndBlinkMultiplier;

        // Dispara o blink no player (isso também seta invulnerável, mas enquanto montado
        // o dano já está sendo roteado pro Louie, então aqui é basicamente visual)
        playerHealth.StartTemporaryInvulnerability(seconds);

        // Restaura os valores depois, pra não “poluir” as configs do player
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

    private void EnsurePunchBombWhileMounted(GameObject louieRoot)
    {
        if (!TryGetComponent<AbilitySystem>(out var ab))
            ab = gameObject.AddComponent<AbilitySystem>();

        ab.RebuildCache();

        hadPunchBeforeMount = ab.IsEnabled(BombPunchAbility.AbilityId);

        var external = louieRoot.GetComponentInChildren<IBombPunchExternalAnimator>(true);

        if (external != null)
        {
            ab.Enable(BombPunchAbility.AbilityId);

            punchAbility = ab.Get<BombPunchAbility>(BombPunchAbility.AbilityId);
            if (punchAbility != null)
                punchAbility.SetExternalAnimator(external);
        }
        else
        {
            ab.Disable(BombPunchAbility.AbilityId);
        }
    }

    private void RestorePunchBombAfterUnmount()
    {
        if (!TryGetComponent<AbilitySystem>(out var ab))
            return;

        ab.RebuildCache();

        punchAbility = ab.Get<BombPunchAbility>(BombPunchAbility.AbilityId);
        if (punchAbility != null)
            punchAbility.SetExternalAnimator(null);

        if (!hadPunchBeforeMount)
            ab.Disable(BombPunchAbility.AbilityId);

        hadPunchBeforeMount = false;
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
        RestorePunchBombAfterUnmount();

        // Aqui sim é invulnerabilidade “real” pós-perda do Louie
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
        RestorePunchBombAfterUnmount();
    }

    private void OnDestroy()
    {
        if (movement != null)
            movement.Died -= OnPlayerDied;
    }
}
