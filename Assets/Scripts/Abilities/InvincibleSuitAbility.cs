using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CharacterHealth))]
public class InvincibleSuitAbility : MonoBehaviour, IPlayerAbility
{
    public const string AbilityId = "InvincibleSuit";

    public string Id => AbilityId;
    public bool IsEnabled { get; private set; }

    [Header("Settings")]
    public float durationSeconds = 10f;

    CharacterHealth health;
    PlayerLouieCompanion companion;
    Coroutine routine;

    CharacterHealth appliedLouieHealth;

    void Awake()
    {
        health = GetComponent<CharacterHealth>();
        TryGetComponent(out companion);
    }

    public void Enable()
    {
        if (IsEnabled)
            return;

        IsEnabled = true;

        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(Run());
    }

    public void Disable()
    {
        if (!IsEnabled)
            return;

        IsEnabled = false;

        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (health != null)
            health.StopInvulnerability();

        if (appliedLouieHealth != null)
        {
            appliedLouieHealth.StopInvulnerability();
            appliedLouieHealth = null;
        }
    }

    IEnumerator Run()
    {
        float seconds = Mathf.Max(0.01f, durationSeconds);

        if (health != null)
            health.StartTemporaryInvulnerability(seconds);

        appliedLouieHealth = null;

        if (companion != null)
        {
            appliedLouieHealth = companion.GetMountedLouieHealth();
            if (appliedLouieHealth != null)
                appliedLouieHealth.StartTemporaryInvulnerability(seconds);
        }

        yield return new WaitForSeconds(seconds);

        routine = null;
        Disable();
    }
}
