using System.Collections.Generic;
using UnityEngine;

// Like the Barrel trap, snapshot the current rendered pose while gameplay is stunned.
[DisallowMultipleComponent]
public sealed class QueenOyobeeFreeze : MonoBehaviour
{
    private StunReceiver stun;
    private MovementController movement;
    private CharacterHealth health;
    private InactivityAnimation inactivity;
    private float remaining;
    private float protection;
    private bool tinted;
    private GameObject frozenVisualRoot;
    private readonly List<(SpriteRenderer renderer, bool hidden)> frozenSources = new();

    private void Awake()
    {
        stun = GetComponent<StunReceiver>();
        movement = GetComponent<MovementController>();
        health = GetComponent<CharacterHealth>();
        inactivity = GetComponent<InactivityAnimation>();
    }

    public bool TryFreeze(float seconds = 2f, bool force = false)
    {
        if (stun == null && force) stun = gameObject.AddComponent<StunReceiver>();
        if (stun == null || (!force && (!stun.CanReceiveStun || stun.IsStunned || protection > 0f))) return false;
        CaptureFrozenPose();
        // Keep the visible emote in the ice snapshot, but do not save its active
        // renderer in StunReceiver's restoration state.
        if (inactivity != null) inactivity.CancelForExternalOverride();
        if (force) stun.ForceStun(seconds, preserveCurrentSprites: true);
        else stun.Stun(seconds, preserveCurrentSprites: true);
        remaining = seconds;
        protection = seconds + 1.5f;
        if (health != null)
        {
            health.SetPersistentTint(new Color(0.55f, 0.85f, 1f, 1f));
            tinted = true;
        }
        return true;
    }

    private void Update()
    {
        if (!Assets.Scripts.Netcode.NetSync.ShouldSimulateLocally) return;
        if (GamePauseController.IsPaused) return;
        protection = Mathf.Max(0f, protection - Time.deltaTime);
        remaining = Mathf.Max(0f, remaining - Time.deltaTime);
        if (remaining <= 0f || movement == null || movement.isDead || movement.IsEndingStage ||
            stun == null || !stun.IsStunned)
        {
            RestoreFrozenPose();
            ClearTint();
        }
    }

    private void CaptureFrozenPose()
    {
        if (frozenVisualRoot != null) return;
        // Capture before StunReceiver can change a mounted pose or idle frame.
        var sources = GetComponentsInChildren<SpriteRenderer>(true);
        frozenVisualRoot = new GameObject("QueenFrozenCharacterVisual");
        frozenVisualRoot.transform.SetParent(transform, false);
        foreach (var source in sources)
        {
            frozenSources.Add((source, source.forceRenderingOff));
            if (source.enabled && source.gameObject.activeInHierarchy && !source.forceRenderingOff && source.sprite != null)
            {
                var visual = new GameObject(source.name + "_Frozen");
                visual.transform.SetParent(frozenVisualRoot.transform, false);
                visual.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                Vector3 parentScale = frozenVisualRoot.transform.lossyScale;
                Vector3 sourceScale = source.transform.lossyScale;
                visual.transform.localScale = new Vector3(sourceScale.x / parentScale.x, sourceScale.y / parentScale.y, 1f);
                visual.layer = source.gameObject.layer;
                var copy = visual.AddComponent<SpriteRenderer>();
                copy.sprite = source.sprite;
                copy.sharedMaterials = source.sharedMaterials;
                var properties = new MaterialPropertyBlock();
                source.GetPropertyBlock(properties);
                copy.SetPropertyBlock(properties);
                copy.drawMode = source.drawMode;
                copy.size = source.size;
                copy.tileMode = source.tileMode;
                copy.adaptiveModeThreshold = source.adaptiveModeThreshold;
                copy.color = source.color * new Color(0.55f, 0.85f, 1f, 1f);
                copy.flipX = source.flipX;
                copy.flipY = source.flipY;
                copy.sortingLayerID = source.sortingLayerID;
                copy.sortingOrder = source.sortingOrder;
                copy.maskInteraction = source.maskInteraction;
                copy.spriteSortPoint = source.spriteSortPoint;
            }
            source.forceRenderingOff = true;
        }
        if (health != null) health.Damaged += OnDamaged;
    }

    private void OnDamaged(int amount)
    {
        remaining = 0f;
        protection = Mathf.Min(protection, 1.5f);
        if (stun != null && stun.IsStunned) stun.CancelStun(restoreVisuals: true);
        if (inactivity != null) inactivity.CancelForExternalOverride();
        // Damage now owns the visuals and the player is immediately free to move.
        RestoreFrozenPose();
        ClearTint();
    }

    private void RestoreFrozenPose()
    {
        if (health != null) health.Damaged -= OnDamaged;
        foreach (var source in frozenSources)
            if (source.renderer != null) source.renderer.forceRenderingOff = source.hidden;
        frozenSources.Clear();
        if (frozenVisualRoot == null) return;
        frozenVisualRoot.SetActive(false);
        Destroy(frozenVisualRoot);
        frozenVisualRoot = null;
    }

    private void ClearTint()
    {
        if (tinted && health != null) health.ClearPersistentTint();
        tinted = false;
    }

    private void OnDisable()
    {
        remaining = 0f;
        protection = 0f;
        RestoreFrozenPose();
        ClearTint();
    }
}
