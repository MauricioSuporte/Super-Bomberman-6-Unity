using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class OwlEyeMovementController : JunctionTurningEnemyMovementController
{
    private const float DeathBodyDuration = 0.5f;
    private const float EyeJumpDuration = 1f;
    private const float EyeFrameDuration = 0.1f;
    private const float EyeJumpHeight = 2f;
    private const float EyeOffset = 0.5f;
    private const float PixelsPerUnit = 16f;
    private const float CoreFinishDuration = 0.5f;

    [Header("OwlEye death")]
    [SerializeField] private AnimatedSpriteRenderer leftEye;
    [SerializeField] private AnimatedSpriteRenderer rightEye;
    [SerializeField] private Sprite[] coreDestructionFrames;

    private Vector3 deathOrigin;
    private readonly List<(GameObject visual, bool wasActive)> barrelHiddenMovementVisuals = new();
    private StunReceiver barrelStun;

    public bool TryBarrelCrushStun(float seconds)
    {
        if (isDead || !TryGetComponent<StunReceiver>(out var stun))
            return false;

        Transform squished = transform.Find("Squished");
        SpriteRenderer pose = squished != null ? squished.GetComponent<SpriteRenderer>() : null;
        if (!stun.TryCrushStun(seconds, pose))
            return false;

        barrelStun = stun;
        HideMovementVisualForBarrel(spriteUp);
        HideMovementVisualForBarrel(spriteDown);
        HideMovementVisualForBarrel(spriteLeft);
        HideMovementVisualForBarrel(spriteRight);
        return true;
    }

    private void HideMovementVisualForBarrel(AnimatedSpriteRenderer animation)
    {
        if (animation == null || animation.gameObject == gameObject)
            return;

        GameObject visual = animation.gameObject;
        foreach (var entry in barrelHiddenMovementVisuals)
            if (entry.visual == visual)
                return;

        barrelHiddenMovementVisuals.Add((visual, visual.activeSelf));
        visual.SetActive(false);
    }

    private void LateUpdate()
    {
        if (barrelHiddenMovementVisuals.Count == 0)
            return;

        if (isDead || barrelStun == null || !barrelStun.IsStunned)
        {
            RestoreBarrelMovementVisuals();
            return;
        }

        foreach (var entry in barrelHiddenMovementVisuals)
            if (entry.visual != null && entry.visual.activeSelf)
                entry.visual.SetActive(false);
    }

    protected override void UpdateSpriteDirection(Vector2 dir)
    {
        if (!isDead && barrelStun != null && barrelStun.IsStunned)
            return;

        base.UpdateSpriteDirection(dir);
    }

    private void RestoreBarrelMovementVisuals()
    {
        foreach (var entry in barrelHiddenMovementVisuals)
            if (entry.visual != null)
                entry.visual.SetActive(entry.wasActive);
        barrelHiddenMovementVisuals.Clear();
        barrelStun = null;
    }

    private void OnDisable()
    {
        RestoreBarrelMovementVisuals();
    }

    protected override void Die()
    {
        if (isDead) return;
        RestoreBarrelMovementVisuals();
        deathOrigin = SnapToPixel(spriteDeath != null ? spriteDeath.transform.position : transform.position);

        // Hide custom poses as well as walking directions. The base death path
        // still owns health, collision, sound and the one enemy-death notification.
        foreach (AnimatedSpriteRenderer animation in GetComponentsInChildren<AnimatedSpriteRenderer>(true))
        {
            animation.enabled = false;
            if (animation.TryGetComponent<SpriteRenderer>(out var renderer)) renderer.enabled = false;
        }
        if (spriteDeath != null)
        {
            spriteDeath.useSequenceDuration = true;
            spriteDeath.sequenceDuration = DeathBodyDuration;
        }
        base.Die();
    }

    protected override float GetDeathAnimationDuration() => DeathBodyDuration;

    protected override void OnDeathAnimationEnded()
    {
        StartCoroutine(AnimateEyes());
    }

    private IEnumerator AnimateEyes()
    {
        if (leftEye == null || rightEye == null ||
            !leftEye.TryGetComponent<SpriteRenderer>(out var leftRenderer) ||
            !rightEye.TryGetComponent<SpriteRenderer>(out var rightRenderer))
        {
            Debug.LogWarning("[OwlEye] Missing death eye renderers.", this);
            base.OnDeathAnimationEnded();
            yield break;
        }

        if (spriteDeath != null)
        {
            spriteDeath.enabled = false;
            if (spriteDeath.TryGetComponent<SpriteRenderer>(out var body)) body.enabled = false;
        }

        // Both eyes begin on Death2; Death3 is the alternating frame, not a
        // different phase for the right eye. Keep existing renderers for blackout masks.
        Sprite death2 = leftEye.idleSprite;
        Sprite death3 = rightEye.idleSprite;
        leftEye.enabled = false;
        rightEye.enabled = false;
        leftEye.gameObject.SetActive(true);
        rightEye.gameObject.SetActive(true);
        leftRenderer.enabled = true;
        rightRenderer.enabled = true;
        leftRenderer.flipX = rightRenderer.flipX = false;
        leftRenderer.flipY = rightRenderer.flipY = false;

        float elapsed = 0f;
        while (elapsed < EyeJumpDuration)
        {
            float t = elapsed / EyeJumpDuration;
            float height = 4f * EyeJumpHeight * t * (1f - t);
            PlaceEyes(leftRenderer, rightRenderer, height);
            Sprite frame = Mathf.FloorToInt(elapsed / EyeFrameDuration) % 2 == 0 ? death2 : death3;
            leftRenderer.sprite = rightRenderer.sprite = frame;
            yield return null;
            elapsed += Time.deltaTime;
        }

        PlaceEyes(leftRenderer, rightRenderer, 0f);
        if (coreDestructionFrames != null && coreDestructionFrames.Length > 0)
        {
            elapsed = 0f;
            while (elapsed < CoreFinishDuration)
            {
                int index = Mathf.Min(coreDestructionFrames.Length - 1,
                    Mathf.FloorToInt(elapsed / CoreFinishDuration * coreDestructionFrames.Length));
                leftRenderer.sprite = rightRenderer.sprite = coreDestructionFrames[index];
                yield return null;
                elapsed += Time.deltaTime;
            }
        }
        leftRenderer.enabled = rightRenderer.enabled = false;
        base.OnDeathAnimationEnded();
    }

    private void PlaceEyes(SpriteRenderer leftRenderer, SpriteRenderer rightRenderer, float height)
    {
        leftRenderer.transform.position = SnapToPixel(deathOrigin + new Vector3(-EyeOffset, height, 0f));
        rightRenderer.transform.position = SnapToPixel(deathOrigin + new Vector3(EyeOffset, height, 0f));
    }

    private static Vector3 SnapToPixel(Vector3 position)
    {
        position.x = Mathf.Round(position.x * PixelsPerUnit) / PixelsPerUnit;
        position.y = Mathf.Round(position.y * PixelsPerUnit) / PixelsPerUnit;
        return position;
    }
}
