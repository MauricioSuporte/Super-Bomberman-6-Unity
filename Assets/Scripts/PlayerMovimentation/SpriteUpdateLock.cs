using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MovementController))]
public sealed class SpriteUpdateLock : MonoBehaviour
{
    [Header("State (read-only)")]
    [SerializeField] private bool locked;

    private MovementController mc;

    private readonly List<SpriteRenderer> spriteRenderers = new();
    private readonly List<AnimatedSpriteRenderer> animRenderers = new();

    public bool IsLocked => locked;

    private void Awake()
    {
        mc = GetComponent<MovementController>();
    }

    public void BeginLock()
    {
        if (locked) return;
        locked = true;

        spriteRenderers.Clear();
        animRenderers.Clear();

        GetComponentsInChildren(true, spriteRenderers);
        GetComponentsInChildren(true, animRenderers);

        for (int i = 0; i < animRenderers.Count; i++)
            if (animRenderers[i] != null)
                animRenderers[i].enabled = false;

        for (int i = 0; i < spriteRenderers.Count; i++)
            if (spriteRenderers[i] != null)
                spriteRenderers[i].enabled = false;
    }

    public void EndLock()
    {
        if (!locked) return;
        locked = false;

        if (mc == null || mc.isDead || mc.IsEndingStage)
            return;

        mc.EnableExclusiveFromState();
    }
}
