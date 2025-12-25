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
    public AnimatedSpriteRenderer louieRight;

    [Header("End Stage (Louie)")]
    public AnimatedSpriteRenderer louieEndStage;

    [Header("Pink Louie - Right X Fix")]
    public bool enablePinkRightFix = true;
    public float pinkRightFixedLocalX = 0f;

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

        if (isPinkLouieMounted && louieRight == louieLeft)
            louieRight = null;

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

        var start = louieDown != null ? louieDown : (louieUp != null ? louieUp : (louieLeft != null ? louieLeft : louieRight));
        if (start != null)
        {
            SetExclusive(start);
            ApplyDirection(Vector2.down, true);
        }
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

            if (isPinkLouieMounted)
                ForceDisableRightRenderer();
        }

        ApplyBlinkSyncFromOwnerIfNeeded();
    }

    private void ForceDisableRightRenderer()
    {
        if (louieRight == null)
            return;

        if (active == louieRight)
            return;

        louieRight.enabled = false;

        if (louieRight.TryGetComponent<SpriteRenderer>(out var sr))
            sr.enabled = false;
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
        if (faceDir == Vector2.zero)
            faceDir = Vector2.down;

        AnimatedSpriteRenderer target = null;

        if (faceDir == Vector2.up)
            target = louieUp;
        else if (faceDir == Vector2.down)
            target = louieDown;
        else if (faceDir == Vector2.left)
            target = louieLeft != null ? louieLeft : louieRight;
        else if (faceDir == Vector2.right)
        {
            if (isPinkLouieMounted)
                target = louieLeft != null ? louieLeft : louieRight;
            else
                target = louieRight != null ? louieRight : louieLeft;
        }
        else
            target = louieDown != null ? louieDown : (louieUp != null ? louieUp : (louieLeft != null ? louieLeft : louieRight));

        if (target == null)
            return;

        if (active != target)
            SetExclusive(target);

        EnsureEnabled(active);

        active.idle = isIdle;

        if (active.TryGetComponent<SpriteRenderer>(out var sr))
        {
            if (active == louieLeft && (louieRight == null || isPinkLouieMounted))
                sr.flipX = (faceDir == Vector2.right);
            else
                sr.flipX = false;
        }

        ApplyPinkRightXFix(faceDir);
        active.RefreshFrame();
    }

    private void EnsureEnabled(AnimatedSpriteRenderer renderer)
    {
        if (renderer == null)
            return;

        if (!renderer.enabled)
            renderer.enabled = true;

        if (renderer.TryGetComponent<SpriteRenderer>(out var sr) && !sr.enabled)
            sr.enabled = true;
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
        SetRendererEnabled(louieUp, keep == louieUp);
        SetRendererEnabled(louieDown, keep == louieDown);
        SetRendererEnabled(louieLeft, keep == louieLeft);
        SetRendererEnabled(louieRight, keep == louieRight);
        SetRendererEnabled(louieEndStage, keep == louieEndStage);

        active = keep;

        if (active != null)
            EnsureEnabled(active);

        if (isPinkLouieMounted)
            ForceDisableRightRenderer();

        if (owner != null && !playingEndStage)
        {
            bool isIdle = owner.Direction == Vector2.zero;
            Vector2 faceDir = isIdle ? owner.FacingDirection : owner.Direction;
            ApplyPinkRightXFix(faceDir);
        }
    }

    private void SetRendererEnabled(AnimatedSpriteRenderer r, bool on)
    {
        if (r == null)
            return;

        r.enabled = on;

        if (r.TryGetComponent<SpriteRenderer>(out var sr))
            sr.enabled = on;
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
