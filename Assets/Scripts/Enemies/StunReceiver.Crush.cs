using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class StunReceiver
{
    private readonly List<(SpriteRenderer renderer, bool hidden)> crushedSources = new();
    private readonly List<(SpriteRenderer renderer, Color color)> crushedSprites = new();
    private GameObject crushedVisualRoot;
    private bool crushedVisualActive;
    private bool crushSavedBombEnabled;
    private bool crushSavedDismountEnabled;
    private bool crushSavedSuppressInactivity;

    public bool TryCrushStun(float seconds)
    {
        if (!isActiveAndEnabled || !CanReceiveStun || isStunned || stunRoutine != null ||
            (cachedMovement != null && cachedMovement.isDead))
            return false;

        // Finish any deferred restoration before capturing the current appearance.
        CancelStun(true);
        suppressRestore = false;
        crushedVisualActive = true;
        isStunned = true;
        stunEndTime = Time.time + Mathf.Max(0.01f, seconds);

        SpriteRenderer[] sources = GetComponentsInChildren<SpriteRenderer>(true);
        crushedVisualRoot = new GameObject("CrushedPlayerVisual");
        crushedVisualRoot.transform.SetParent(transform, false);

        foreach (SpriteRenderer source in sources)
        {
            crushedSources.Add((source, source.forceRenderingOff));
            if (source.enabled && source.gameObject.activeInHierarchy &&
                !source.forceRenderingOff && source.sprite != null)
            {
                // Snapshot only rendering components: physics and gameplay keep their size.
                var visual = new GameObject(source.name + "_Crushed");
                visual.transform.SetParent(crushedVisualRoot.transform, false);
                visual.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                Vector3 parentScale = crushedVisualRoot.transform.lossyScale;
                Vector3 sourceScale = source.transform.lossyScale;
                visual.transform.localScale = new Vector3(
                    sourceScale.x / parentScale.x, sourceScale.y / parentScale.y, 1f);
                visual.layer = source.gameObject.layer;
                SpriteRenderer copy = visual.AddComponent<SpriteRenderer>();
                copy.sprite = source.sprite;
                copy.sharedMaterials = source.sharedMaterials;
                // Water submersion stores its surface/tint parameters per renderer.
                // Sharing only the material loses those values on the snapshot.
                var propertyBlock = new MaterialPropertyBlock();
                source.GetPropertyBlock(propertyBlock);
                copy.SetPropertyBlock(propertyBlock);
                copy.drawMode = source.drawMode;
                copy.size = source.size;
                copy.tileMode = source.tileMode;
                copy.adaptiveModeThreshold = source.adaptiveModeThreshold;
                copy.color = source.color;
                copy.flipX = source.flipX;
                copy.flipY = source.flipY;
                copy.sortingLayerID = source.sortingLayerID;
                copy.sortingOrder = source.sortingOrder;
                copy.maskInteraction = source.maskInteraction;
                copy.spriteSortPoint = source.spriteSortPoint;
                crushedSprites.Add((copy, copy.color));
            }
            source.forceRenderingOff = true;
        }

        // Compress the entire mounted pose around the player's ground position.
        crushedVisualRoot.transform.localScale = new Vector3(1.35f, 0.3f, 1f);
        if (cachedMovement != null)
        {
            crushSavedSuppressInactivity = cachedMovement.SuppressInactivityAnimation;
            cachedMovement.SetSuppressInactivityAnimation(true);
        }
        if (cachedBombController != null)
        {
            crushSavedBombEnabled = cachedBombController.enabled;
            cachedBombController.enabled = false;
        }
        if (cachedManualDismount != null)
        {
            crushSavedDismountEnabled = cachedManualDismount.enabled;
            cachedManualDismount.enabled = false;
        }
        PlayRandomPlayerStunSfx();
        stunRoutine = StartCoroutine(CrushStunRoutine());
        return true;
    }

    private IEnumerator CrushStunRoutine()
    {
        while (Time.time < stunEndTime)
        {
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            float remaining = stunEndTime - Time.time;
            float alpha = remaining <= 1f && Mathf.FloorToInt(remaining / 0.1f) % 2 == 0 ? 0.2f : 1f;
            foreach (var snapshot in crushedSprites)
            {
                if (snapshot.renderer == null)
                    continue;
                Color color = snapshot.color;
                color.a *= alpha;
                snapshot.renderer.color = color;
            }
            yield return null;
        }

        RestoreCrushedVisuals();
        isStunned = false;
        stunEndTime = 0f;
        stunRoutine = null;
    }

    private void RestoreCrushedVisuals()
    {
        if (!crushedVisualActive)
            return;

        crushedVisualActive = false;
        foreach (var source in crushedSources)
            if (source.renderer != null)
                source.renderer.forceRenderingOff = source.hidden;
        crushedSources.Clear();
        crushedSprites.Clear();
        if (crushedVisualRoot != null)
        {
            crushedVisualRoot.SetActive(false);
            Destroy(crushedVisualRoot);
            crushedVisualRoot = null;
        }
        if (cachedMovement != null)
            cachedMovement.SetSuppressInactivityAnimation(crushSavedSuppressInactivity);
        if (cachedBombController != null)
            cachedBombController.enabled = crushSavedBombEnabled;
        if (cachedManualDismount != null)
            cachedManualDismount.enabled = crushSavedDismountEnabled;
    }

}
