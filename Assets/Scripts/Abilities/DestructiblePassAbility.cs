using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class DestructiblePassAbility : MonoBehaviour, IPlayerAbility
{
    public const string AbilityId = "DestructiblePass";

    [SerializeField] private bool enabledAbility;

    [Header("Tag")]
    [SerializeField] private string destructiblesTag = "Destructibles";

    [Header("Refresh")]
    [SerializeField] private float refreshInterval = 0.5f;

    private Collider2D selfCollider;
    private readonly HashSet<int> ignoredColliderIds = new();
    private Coroutine refreshRoutine;

    public string Id => AbilityId;
    public bool IsEnabled => enabledAbility;

    private void Awake()
    {
        selfCollider = GetComponent<Collider2D>();
    }

    public void Enable()
    {
        enabledAbility = true;

        if (CompareTag("Player"))
            PlayerPersistentStats.CanPassDestructibles = true;

        ApplyIgnoreToCurrentDestructibles();

        if (refreshRoutine != null)
            StopCoroutine(refreshRoutine);

        refreshRoutine = StartCoroutine(RefreshLoop());
    }

    public void Disable()
    {
        enabledAbility = false;

        if (CompareTag("Player"))
            PlayerPersistentStats.CanPassDestructibles = false;

        if (refreshRoutine != null)
        {
            StopCoroutine(refreshRoutine);
            refreshRoutine = null;
        }

        RestoreIgnoredCollisions();
    }

    private IEnumerator RefreshLoop()
    {
        var wait = new WaitForSeconds(refreshInterval);

        while (enabledAbility)
        {
            ApplyIgnoreToCurrentDestructibles();
            yield return wait;
        }

        refreshRoutine = null;
    }

    private void ApplyIgnoreToCurrentDestructibles()
    {
        if (selfCollider == null)
            return;

        var root = GameObject.FindGameObjectWithTag(destructiblesTag);
        if (root == null)
            return;

        var cols = root.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            var col = cols[i];
            if (col == null || col == selfCollider)
                continue;

            if (col.isTrigger)
                continue;

            int id = col.GetInstanceID();
            if (!ignoredColliderIds.Add(id))
                continue;

            Physics2D.IgnoreCollision(selfCollider, col, true);
        }
    }

    private void RestoreIgnoredCollisions()
    {
        if (selfCollider == null)
        {
            ignoredColliderIds.Clear();
            return;
        }

        var root = GameObject.FindGameObjectWithTag(destructiblesTag);
        if (root != null)
        {
            var cols = root.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                var col = cols[i];
                if (col == null || col == selfCollider)
                    continue;

                if (col.isTrigger)
                    continue;

                int id = col.GetInstanceID();
                if (!ignoredColliderIds.Contains(id))
                    continue;

                Physics2D.IgnoreCollision(selfCollider, col, false);
            }
        }

        ignoredColliderIds.Clear();
    }
}
