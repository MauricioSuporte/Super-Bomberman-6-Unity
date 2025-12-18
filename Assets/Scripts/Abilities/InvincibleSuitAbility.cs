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

    private CharacterHealth health;
    private Coroutine routine;

    private void Awake()
    {
        health = GetComponent<CharacterHealth>();
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
    }

    private IEnumerator Run()
    {
        if (health != null)
            health.StartTemporaryInvulnerability(durationSeconds);

        yield return new WaitForSeconds(durationSeconds);

        routine = null;
        Disable();
    }
}
