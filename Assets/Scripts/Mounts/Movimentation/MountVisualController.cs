using UnityEngine;

public class MountVisualController : MonoBehaviour
{
    [Header("Owner")]
    public MovementController owner;

    [Header("Player Visual While Mounted")]
    public bool useHeadOnlyPlayerVisual = false;

    [Header("Keep Move Animation When Idle")]
    [SerializeField] private bool keepMoveAnimationWhenIdle = false;

    [Header("HeadOnly Player Visual Offsets (local, per direction)")]
    [SerializeField] private Vector2 headOnlyUpLocalOffset = Vector2.zero;
    [SerializeField] private Vector2 headOnlyDownLocalOffset = Vector2.zero;
    [SerializeField] private Vector2 headOnlyLeftLocalOffset = Vector2.zero;
    [SerializeField] private Vector2 headOnlyRightLocalOffset = Vector2.zero;

    [Header("Visual Offset (local)")]
    public Vector2 localOffset = new(0f, -0.15f);

    [Header("Sprites (Louie)")]
    public AnimatedSpriteRenderer louieUp;
    public AnimatedSpriteRenderer louieDown;
    public AnimatedSpriteRenderer louieLeft;
    public AnimatedSpriteRenderer louieRight;

    [Header("End Stage (Louie)")]
    public AnimatedSpriteRenderer louieEndStage;

    [Header("Inactivity Emote (Louie)")]
    [SerializeField] private AnimatedSpriteRenderer louieInactivityEmoteLoop;

    [Header("Pink Louie - Right X Fix")]
    public bool enablePinkRightFix = true;
    public float pinkRightFixedLocalX = 0f;

    [Header("Blink Sync")]
    public bool syncBlinkFromPlayerWhenMounted = true;

    private AnimatedSpriteRenderer active;
    private bool playingEndStage;
    private bool playingInactivity;

    private bool isPinkLouieMounted;
    private MovementController louieMovement;

    private SpriteRenderer[] louieSpriteRenderers;
    private Color[] louieOriginalColors;

    private SpriteRenderer[] ownerSpriteRenderers;

    private SpriteRenderer[] allSpriteRenderers;
    private AnimatedSpriteRenderer[] allAnimatedRenderers;

    private bool suppressedByRedBoat;

    private bool headOnlyOffsetsApplied;

    public bool HasInactivityEmoteRenderer => louieInactivityEmoteLoop != null;

    public void Bind(MovementController movement)
    {
        owner = movement;
        playingEndStage = false;
        playingInactivity = false;
        suppressedByRedBoat = false;

        headOnlyOffsetsApplied = false;

        CacheAllRenderers();

        if (louieMovement == null)
            TryGetComponent(out louieMovement);

        isPinkLouieMounted = DetectPinkMounted(owner);

        if (isPinkLouieMounted && louieRight == louieLeft)
            louieRight = null;

        if (owner != null)
        {
            ownerSpriteRenderers = owner.GetComponentsInChildren<SpriteRenderer>(true);

            if (useHeadOnlyPlayerVisual)
            {
                owner.SetUseHeadOnlyWhenMounted(true);
                ApplyHeadOnlyOffsetsIfNeeded(force: true);
            }
        }

        louieSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        louieOriginalColors = new Color[louieSpriteRenderers.Length];
        for (int i = 0; i < louieSpriteRenderers.Length; i++)
            louieOriginalColors[i] = louieSpriteRenderers[i] != null ? louieSpriteRenderers[i].color : Color.white;

        if (louieInactivityEmoteLoop != null)
            SetRendererBranchEnabled(louieInactivityEmoteLoop, false);

        var start =
            louieDown != null ? louieDown :
            louieUp != null ? louieUp :
            louieLeft != null ? louieLeft : louieRight;

        if (start != null)
        {
            HardExclusive(start);
            ApplyDirection(Vector2.down, true);
        }
    }

    private void OnDestroy()
    {
        ClearHeadOnlyOffsetsIfNeeded();
    }

    public void SetInactivityEmote(bool on)
    {
        if (louieInactivityEmoteLoop == null)
            return;

        if (louieMovement != null && louieMovement.isDead)
        {
            playingInactivity = false;
            SetRendererBranchEnabled(louieInactivityEmoteLoop, false);
            return;
        }

        playingInactivity = on;

        if (on)
        {
            playingEndStage = false;

            louieInactivityEmoteLoop.loop = true;
            louieInactivityEmoteLoop.idle = false;
            louieInactivityEmoteLoop.pingPong = false;
            louieInactivityEmoteLoop.CurrentFrame = 0;

            HardExclusive(louieInactivityEmoteLoop);
            louieInactivityEmoteLoop.RefreshFrame();
            return;
        }

        if (owner == null)
        {
            SetRendererBranchEnabled(louieInactivityEmoteLoop, false);
            return;
        }

        bool isIdle = owner.Direction == Vector2.zero;
        Vector2 faceDir = isIdle ? owner.FacingDirection : owner.Direction;

        playingEndStage = false;

        ApplyDirection(faceDir, isIdle);
        SetRendererBranchEnabled(louieInactivityEmoteLoop, false);
    }

    public bool TryPlayEndStage(float totalTime, int frameCount)
    {
        if (louieEndStage == null)
            return false;

        playingInactivity = false;
        playingEndStage = true;

        HardExclusive(louieEndStage);

        louieEndStage.idle = false;
        louieEndStage.loop = true;
        louieEndStage.pingPong = false;
        louieEndStage.CurrentFrame = 0;
        louieEndStage.ClearRuntimeBaseLocalX();
        louieEndStage.RefreshFrame();

        if (frameCount > 0)
            louieEndStage.animationTime = totalTime / frameCount;

        return true;
    }

    public void ForceIdleUp()
    {
        playingInactivity = false;
        playingEndStage = false;

        if (louieUp == null)
            return;

        SetExclusive(louieUp);

        louieUp.idle = true;
        louieUp.loop = false;
        louieUp.pingPong = false;
        louieUp.RefreshFrame();
    }

    public void ForceOnlyUpEnabled()
    {
        ForceIdleUp();
    }

    private void CacheAllRenderers()
    {
        allSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        allAnimatedRenderers = GetComponentsInChildren<AnimatedSpriteRenderer>(true);
    }

    private void LateUpdate()
    {
        if (louieMovement != null && louieMovement.isDead)
        {
            playingInactivity = false;
            playingEndStage = false;
            transform.localPosition = localOffset;
            return;
        }

        if (owner == null || owner.isDead)
        {
            Destroy(gameObject);
            return;
        }

        transform.localPosition = localOffset;

        if (useHeadOnlyPlayerVisual)
            ApplyHeadOnlyOffsetsIfNeeded(force: false);
        else
            ClearHeadOnlyOffsetsIfNeeded();

        bool ownerOnRedBoat = BoatRideZone.IsRidingBoat(owner);

        if (ownerOnRedBoat)
        {
            if (!suppressedByRedBoat)
                SuppressAllLouieVisuals();

            return;
        }

        if (suppressedByRedBoat)
            RestoreAfterBoatSuppression();

        if (playingInactivity)
        {
            EnsureInactivityExclusive();
        }
        else if (playingEndStage)
        {
            EnsureEndStageExclusive();
        }
        else
        {
            bool isIdle = owner.Direction == Vector2.zero;
            Vector2 faceDir = isIdle ? owner.FacingDirection : owner.Direction;
            ApplyDirection(faceDir, isIdle);

            if (isPinkLouieMounted)
                ForceDisableRightRenderer();
        }

        ApplyBlinkSyncFromOwnerIfNeeded();
    }

    private void ApplyHeadOnlyOffsetsIfNeeded(bool force)
    {
        if (owner == null)
            return;

        if (!useHeadOnlyPlayerVisual)
            return;

        if (!force && headOnlyOffsetsApplied)
            return;

        owner.SetHeadOnlyMountedOffsets(
            headOnlyUpLocalOffset,
            headOnlyDownLocalOffset,
            headOnlyLeftLocalOffset,
            headOnlyRightLocalOffset
        );

        headOnlyOffsetsApplied = true;
    }

    private void ClearHeadOnlyOffsetsIfNeeded()
    {
        if (!headOnlyOffsetsApplied)
            return;

        if (owner != null)
            owner.ClearHeadOnlyMountedOffsets();

        headOnlyOffsetsApplied = false;
    }

    private void SuppressAllLouieVisuals()
    {
        suppressedByRedBoat = true;

        if (allSpriteRenderers == null || allAnimatedRenderers == null)
            CacheAllRenderers();

        if (allAnimatedRenderers != null)
        {
            for (int i = 0; i < allAnimatedRenderers.Length; i++)
            {
                var a = allAnimatedRenderers[i];
                if (a == null) continue;
                a.enabled = false;
            }
        }

        if (allSpriteRenderers != null)
        {
            for (int i = 0; i < allSpriteRenderers.Length; i++)
            {
                var sr = allSpriteRenderers[i];
                if (sr == null) continue;
                sr.enabled = false;
            }
        }
    }

    private void RestoreAfterBoatSuppression()
    {
        suppressedByRedBoat = false;

        if (owner == null)
            return;

        if (playingInactivity)
        {
            EnsureInactivityExclusive();
            return;
        }

        if (playingEndStage)
        {
            EnsureEndStageExclusive();
            return;
        }

        bool isIdle = owner.Direction == Vector2.zero;
        Vector2 faceDir = isIdle ? owner.FacingDirection : owner.Direction;

        ApplyDirection(faceDir, isIdle);

        if (isPinkLouieMounted)
            ForceDisableRightRenderer();
    }

    private void EnsureInactivityExclusive()
    {
        if (louieMovement != null && louieMovement.isDead)
        {
            playingInactivity = false;
            return;
        }

        if (louieInactivityEmoteLoop == null)
        {
            playingInactivity = false;
            return;
        }

        HardExclusive(louieInactivityEmoteLoop);

        louieInactivityEmoteLoop.idle = false;
        louieInactivityEmoteLoop.loop = true;
        louieInactivityEmoteLoop.pingPong = false;
        louieInactivityEmoteLoop.RefreshFrame();
    }

    private void EnsureEndStageExclusive()
    {
        if (louieEndStage == null)
            return;

        HardExclusive(louieEndStage);

        louieEndStage.idle = false;
        louieEndStage.loop = true;
        louieEndStage.pingPong = false;
        louieEndStage.RefreshFrame();
    }

    private void HardExclusive(AnimatedSpriteRenderer keep)
    {
        if (allSpriteRenderers == null || allAnimatedRenderers == null)
            CacheAllRenderers();

        if (allAnimatedRenderers != null)
        {
            for (int i = 0; i < allAnimatedRenderers.Length; i++)
            {
                var a = allAnimatedRenderers[i];
                if (a == null) continue;
                a.enabled = (a == keep);
            }
        }

        if (allSpriteRenderers != null)
        {
            for (int i = 0; i < allSpriteRenderers.Length; i++)
            {
                var sr = allSpriteRenderers[i];
                if (sr == null) continue;
                sr.enabled = false;
            }
        }

        if (keep != null)
        {
            keep.enabled = true;

            var keepSrs = keep.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < keepSrs.Length; i++)
                if (keepSrs[i] != null)
                    keepSrs[i].enabled = true;

            var keepAnims = keep.GetComponentsInChildren<AnimatedSpriteRenderer>(true);
            for (int i = 0; i < keepAnims.Length; i++)
                if (keepAnims[i] != null)
                    keepAnims[i].enabled = true;
        }

        active = keep;

        if (isPinkLouieMounted && !playingInactivity)
            ForceDisableRightRenderer();
    }

    private void ApplyBlinkSyncFromOwnerIfNeeded()
    {
        if (!syncBlinkFromPlayerWhenMounted)
            return;

        if (owner == null || !owner.IsMountedOnLouie)
            return;

        if (ownerSpriteRenderers == null || louieSpriteRenderers == null)
            return;

        for (int i = 0; i < louieSpriteRenderers.Length; i++)
        {
            var louieSr = louieSpriteRenderers[i];
            if (louieSr == null)
                continue;

            Color c = louieOriginalColors[i];

            for (int j = 0; j < ownerSpriteRenderers.Length; j++)
            {
                var ownerSr = ownerSpriteRenderers[j];
                if (ownerSr == null || !ownerSr.enabled)
                    continue;

                c.a = ownerSr.color.a;
                break;
            }

            louieSr.color = c;
        }
    }

    private void ForceDisableRightRenderer()
    {
        if (louieRight == null)
            return;

        if (active == louieRight)
            return;

        SetRendererBranchEnabled(louieRight, false);
    }

    private void ApplyDirection(Vector2 faceDir, bool isIdle)
    {
        if (faceDir == Vector2.zero)
            faceDir = Vector2.down;

        AnimatedSpriteRenderer target = null;

        if (faceDir == Vector2.up) target = louieUp;
        else if (faceDir == Vector2.down) target = louieDown;
        else if (faceDir == Vector2.left) target = louieLeft != null ? louieLeft : louieRight;
        else if (faceDir == Vector2.right)
        {
            if (isPinkLouieMounted)
                target = louieLeft != null ? louieLeft : louieRight;
            else
                target = louieRight != null ? louieRight : louieLeft;
        }
        else
            target = louieDown != null ? louieDown :
                     (louieUp != null ? louieUp :
                     (louieLeft != null ? louieLeft : louieRight));

        if (target == null)
            return;

        if (active != target)
            SetExclusive(target);

        SetRendererBranchEnabled(active, true);

        bool shouldIdle = isIdle && !keepMoveAnimationWhenIdle;

        active.pingPong = false;
        active.idle = shouldIdle;
        active.loop = !shouldIdle;

        if (active != null && active.TryGetComponent<SpriteRenderer>(out var sr) && sr != null)
        {
            if (active == louieLeft && (louieRight == null || isPinkLouieMounted))
                sr.flipX = (faceDir == Vector2.right);
            else
                sr.flipX = false;
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

        if (!movement.TryGetComponent<PlayerMountCompanion>(out var comp) || comp == null)
            return false;

        return comp.GetMountedLouieType() == MountedType.Pink;
    }

    private void SetExclusive(AnimatedSpriteRenderer keep)
    {
        SetRendererBranchEnabled(louieUp, keep == louieUp);
        SetRendererBranchEnabled(louieDown, keep == louieDown);
        SetRendererBranchEnabled(louieLeft, keep == louieLeft);
        SetRendererBranchEnabled(louieRight, keep == louieRight);
        SetRendererBranchEnabled(louieEndStage, keep == louieEndStage);

        if (louieInactivityEmoteLoop != null)
            SetRendererBranchEnabled(louieInactivityEmoteLoop, false);

        active = keep;

        if (active != null)
            SetRendererBranchEnabled(active, true);

        if (isPinkLouieMounted)
            ForceDisableRightRenderer();

        if (owner != null && !playingEndStage)
        {
            bool isIdle = owner.Direction == Vector2.zero;
            Vector2 faceDir = isIdle ? owner.FacingDirection : owner.Direction;
            ApplyPinkRightXFix(faceDir);
        }
    }

    private void SetRendererBranchEnabled(AnimatedSpriteRenderer r, bool on)
    {
        if (r == null)
            return;

        r.enabled = on;

        var srs = r.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
            if (srs[i] != null)
                srs[i].enabled = on;

        var anims = r.GetComponentsInChildren<AnimatedSpriteRenderer>(true);
        for (int i = 0; i < anims.Length; i++)
            if (anims[i] != null)
                anims[i].enabled = on;
    }
}
