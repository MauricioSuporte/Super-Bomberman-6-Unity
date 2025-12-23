using UnityEngine;

public class LouieRiderVisual : MonoBehaviour
{
    [Header("Owner")]
    public MovementController owner;

    [Header("Visual Offset (local)")]
    public Vector2 localOffset = new(0f, -0.15f);

    [Header("Sprites (Louie)")]
    public AnimatedSpriteRenderer louieUp;
    public AnimatedSpriteRenderer louieDown;
    public AnimatedSpriteRenderer louieLeft;

    [Header("End Stage (Louie)")]
    public AnimatedSpriteRenderer louieEndStage;

    [Header("Pink Louie - Right X Fix")]
    public bool enablePinkRightFix = true;
    public float pinkRightFixedLocalX = -1f;

    [Header("Blink Sync")]
    public bool syncBlinkFromPlayerWhenMounted = true;

    private AnimatedSpriteRenderer active;
    private bool playingEndStage;

    private bool isPinkLouieMounted;

    private CharacterHealth ownerHealth;
    private PlayerLouieCompanion ownerCompanion;

    private SpriteRenderer[] louieSpriteRenderers;
    private Color[] louieOriginalColors;

    private SpriteRenderer[] ownerSpriteRenderers;

    public void Bind(MovementController movement)
    {
        owner = movement;
        playingEndStage = false;

        isPinkLouieMounted = DetectPinkMounted(owner);

        if (owner != null)
        {
            owner.TryGetComponent(out ownerHealth);
            owner.TryGetComponent(out ownerCompanion);

            ownerSpriteRenderers = owner.GetComponentsInChildren<SpriteRenderer>(true);
        }

        louieSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        louieOriginalColors = new Color[louieSpriteRenderers.Length];
        for (int i = 0; i < louieSpriteRenderers.Length; i++)
            louieOriginalColors[i] = louieSpriteRenderers[i] != null ? louieSpriteRenderers[i].color : Color.white;

        SetExclusive(louieDown != null ? louieDown : louieUp);
        ApplyDirection(Vector2.down, true);
    }

    private void LateUpdate()
    {
        if (owner == null || owner.isDead)
        {
            Destroy(gameObject);
            return;
        }

        transform.localPosition = localOffset;

        if (!playingEndStage)
        {
            bool isIdle = owner.Direction == Vector2.zero;
            Vector2 faceDir = isIdle ? owner.FacingDirection : owner.Direction;

            ApplyDirection(faceDir, isIdle);
        }

        ApplyBlinkSyncFromOwnerIfNeeded();
    }

    private void ApplyBlinkSyncFromOwnerIfNeeded()
    {
        if (!syncBlinkFromPlayerWhenMounted)
            return;

        if (owner == null || !owner.CompareTag("Player"))
            return;

        if (ownerCompanion == null || !ownerCompanion.blinkPlayerTogetherWithLouie)
            return;

        if (ownerHealth == null)
            return;

        if (!ownerHealth.IsInvulnerable)
        {
            RestoreLouieOriginalColors();
            return;
        }

        float alpha = ReadOwnerAlpha();
        ApplyLouieAlpha(alpha);
    }

    private float ReadOwnerAlpha()
    {
        if (ownerSpriteRenderers == null || ownerSpriteRenderers.Length == 0)
            return 1f;

        for (int i = 0; i < ownerSpriteRenderers.Length; i++)
        {
            var sr = ownerSpriteRenderers[i];
            if (sr == null || !sr.enabled)
                continue;

            return sr.color.a;
        }

        for (int i = 0; i < ownerSpriteRenderers.Length; i++)
        {
            var sr = ownerSpriteRenderers[i];
            if (sr != null)
                return sr.color.a;
        }

        return 1f;
    }

    private void ApplyLouieAlpha(float alpha)
    {
        if (louieSpriteRenderers == null)
            return;

        alpha = Mathf.Clamp01(alpha);

        for (int i = 0; i < louieSpriteRenderers.Length; i++)
        {
            var sr = louieSpriteRenderers[i];
            if (sr == null)
                continue;

            Color baseColor = (louieOriginalColors != null && i < louieOriginalColors.Length)
                ? louieOriginalColors[i]
                : sr.color;

            sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
        }
    }

    private void RestoreLouieOriginalColors()
    {
        if (louieSpriteRenderers == null || louieOriginalColors == null)
            return;

        for (int i = 0; i < louieSpriteRenderers.Length; i++)
        {
            var sr = louieSpriteRenderers[i];
            if (sr == null)
                continue;

            if (i < louieOriginalColors.Length)
                sr.color = louieOriginalColors[i];
        }
    }

    private void ApplyDirection(Vector2 faceDir, bool isIdle)
    {
        AnimatedSpriteRenderer target;

        if (faceDir == Vector2.up)
            target = louieUp;
        else if (faceDir == Vector2.down)
            target = louieDown;
        else
            target = louieLeft;

        if (target == null)
            return;

        if (active != target)
            SetExclusive(target);

        active.idle = isIdle;

        if (active.TryGetComponent<SpriteRenderer>(out var sr))
        {
            if (faceDir == Vector2.right) sr.flipX = true;
            else if (faceDir == Vector2.left) sr.flipX = false;
        }

        ApplyPinkRightXFix(faceDir);

        active.RefreshFrame();
    }

    private void ApplyPinkRightXFix(Vector2 faceDir)
    {
        if (active == null)
            return;

        if (!enablePinkRightFix || !isPinkLouieMounted)
        {
            active.ClearRuntimeBaseLocalX();
            return;
        }

        if (faceDir == Vector2.right)
            active.SetRuntimeBaseLocalX(pinkRightFixedLocalX);
        else
            active.ClearRuntimeBaseLocalX();
    }

    private bool DetectPinkMounted(MovementController movement)
    {
        if (movement == null)
            return false;

        if (!movement.CompareTag("Player"))
            return false;

        if (!movement.TryGetComponent<PlayerLouieCompanion>(out var comp) || comp == null)
            return false;

        return comp.GetMountedLouieType() == MountedLouieType.Pink;
    }

    private void SetExclusive(AnimatedSpriteRenderer keep)
    {
        var anims = GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        for (int i = 0; i < anims.Length; i++)
        {
            var a = anims[i];
            if (a == null) continue;

            bool on = (a == keep);
            a.enabled = on;

            if (a.TryGetComponent<SpriteRenderer>(out var sr))
                sr.enabled = on;
        }

        if (louieUp != null && louieUp != keep) louieUp.enabled = false;
        if (louieDown != null && louieDown != keep) louieDown.enabled = false;
        if (louieLeft != null && louieLeft != keep) louieLeft.enabled = false;
        if (louieEndStage != null && louieEndStage != keep) louieEndStage.enabled = false;

        keep.enabled = true;
        if (keep.TryGetComponent<SpriteRenderer>(out var keepSr))
            keepSr.enabled = true;

        active = keep;

        if (owner != null && !playingEndStage)
        {
            bool isIdle = owner.Direction == Vector2.zero;
            Vector2 faceDir = isIdle ? owner.FacingDirection : owner.Direction;
            ApplyPinkRightXFix(faceDir);
        }
    }

    public bool TryPlayEndStage(float totalTime, int frameCount)
    {
        if (louieEndStage == null)
            return false;

        playingEndStage = true;

        SetExclusive(louieEndStage);

        louieEndStage.idle = false;
        louieEndStage.loop = true;
        louieEndStage.CurrentFrame = 0;
        louieEndStage.ClearRuntimeBaseLocalX();
        louieEndStage.RefreshFrame();

        if (frameCount > 0)
            louieEndStage.animationTime = totalTime / frameCount;

        return true;
    }

    public void ForceIdleUp()
    {
        playingEndStage = false;

        if (louieUp == null)
            return;

        SetExclusive(louieUp);

        louieUp.idle = true;
        louieUp.loop = false;
        louieUp.RefreshFrame();
    }

    public void ForceOnlyUpEnabled()
    {
        ForceIdleUp();
    }
}
