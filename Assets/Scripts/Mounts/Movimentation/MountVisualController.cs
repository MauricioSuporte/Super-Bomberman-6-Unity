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

    [Header("Sprites")]
    public AnimatedSpriteRenderer louieUp;
    public AnimatedSpriteRenderer louieDown;
    public AnimatedSpriteRenderer louieLeft;
    public AnimatedSpriteRenderer louieRight;

    [Header("Jump Ascend")]
    [SerializeField] private AnimatedSpriteRenderer louieJumpAscendUp;
    [SerializeField] private AnimatedSpriteRenderer louieJumpAscendDown;
    [SerializeField] private AnimatedSpriteRenderer louieJumpAscendLeft;
    [SerializeField] private AnimatedSpriteRenderer louieJumpAscendRight;

    [Header("Jump Descend")]
    [SerializeField] private AnimatedSpriteRenderer louieJumpDescendUp;
    [SerializeField] private AnimatedSpriteRenderer louieJumpDescendDown;
    [SerializeField] private AnimatedSpriteRenderer louieJumpDescendLeft;
    [SerializeField] private AnimatedSpriteRenderer louieJumpDescendRight;

    [Header("Jump Fallback (single phase)")]
    [SerializeField] private AnimatedSpriteRenderer louieJumpUp;
    [SerializeField] private AnimatedSpriteRenderer louieJumpDown;
    [SerializeField] private AnimatedSpriteRenderer louieJumpLeft;
    [SerializeField] private AnimatedSpriteRenderer louieJumpRight;

    [Header("End Stage")]
    public AnimatedSpriteRenderer louieEndStage;

    [Header("Inactivity Emote")]
    [SerializeField] private AnimatedSpriteRenderer louieInactivityEmoteLoop;
    [SerializeField] private AnimatedSpriteRenderer louieInactivityEmoteLoopAlt;

    [Header("Cornered")]
    public AnimatedSpriteRenderer louieCornered;

    [Header("Pink Louie - Right X Fix")]
    public bool enablePinkRightFix = true;
    public float pinkRightFixedLocalX = 0f;

    [Header("Blink Sync")]
    public bool syncBlinkFromPlayerWhenMounted = true;

    [Header("External Tint (Ability Cooldown)")]
    [SerializeField] private bool allowExternalTint = true;

    [Header("Louie Type")]
    [SerializeField] private MountedType visualMountedType = MountedType.None;

    private AnimatedSpriteRenderer active;
    private bool playingEndStage;
    private bool playingInactivity;
    private bool playingCornered;

    private bool isPinkLouieVisual;
    private MovementController louieMovement;

    private SpriteRenderer[] louieSpriteRenderers;
    private Color[] louieOriginalColors;

    private SpriteRenderer[] ownerSpriteRenderers;

    private SpriteRenderer[] allSpriteRenderers;
    private AnimatedSpriteRenderer[] allAnimatedRenderers;

    private bool suppressedByRedBoat;

    private bool headOnlyOffsetsApplied;

    private Vector2 temporaryHeadOnlyDownDelta;
    private bool temporaryHeadOnlyDownDeltaActive;

    private bool externalTintActive;
    private Color externalTintColor = Color.white;
    private float externalTintNormalized;

    private AnimatedSpriteRenderer activeLouieInactivityRenderer;

    public AnimatedSpriteRenderer LouieInactivityEmoteLoop => louieInactivityEmoteLoop;
    public AnimatedSpriteRenderer LouieInactivityEmoteLoopAlt => louieInactivityEmoteLoopAlt;

    public bool HasInactivityEmoteRenderer =>
        louieInactivityEmoteLoop != null || louieInactivityEmoteLoopAlt != null;

    private bool playingJump;
    private Vector2 jumpFacing = Vector2.down;

    private enum JumpPhase
    {
        Ascend,
        Descend
    }

    private JumpPhase jumpPhase = JumpPhase.Ascend;

    public void SetTemporaryHeadOnlyDownDelta(Vector2 delta, bool active)
    {
        temporaryHeadOnlyDownDelta = delta;
        temporaryHeadOnlyDownDeltaActive = active;

        headOnlyOffsetsApplied = false;
        ApplyHeadOnlyOffsetsIfNeeded(force: true);
    }

    public void SetExternalTint(bool active, Color tintColor, float normalized01)
    {
        if (!allowExternalTint)
            return;

        externalTintActive = active;
        externalTintColor = tintColor;
        externalTintNormalized = Mathf.Clamp01(normalized01);
    }

    public void Bind(MovementController movement)
    {
        owner = movement;
        playingEndStage = false;
        playingInactivity = false;
        playingCornered = false;
        suppressedByRedBoat = false;

        headOnlyOffsetsApplied = false;

        externalTintActive = false;
        externalTintColor = Color.white;
        externalTintNormalized = 1f;

        playingJump = false;
        jumpFacing = Vector2.down;
        jumpPhase = JumpPhase.Ascend;

        CacheAllRenderers();

        if (louieMovement == null)
            TryGetComponent(out louieMovement);

        isPinkLouieVisual = visualMountedType == MountedType.Pink;

        if (isPinkLouieVisual && louieRight == louieLeft)
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
        if (on)
        {
            SetInactivityEmote(louieInactivityEmoteLoop, true);
            return;
        }

        playingInactivity = false;

        if (louieInactivityEmoteLoop != null)
            SetRendererBranchEnabled(louieInactivityEmoteLoop, false);

        if (louieInactivityEmoteLoopAlt != null)
            SetRendererBranchEnabled(louieInactivityEmoteLoopAlt, false);

        activeLouieInactivityRenderer = null;

        if (owner == null)
            return;

        bool isIdle = owner.Direction == Vector2.zero;
        Vector2 faceDir = isIdle ? owner.FacingDirection : owner.Direction;
        ApplyDirection(faceDir, isIdle);
    }

    public void SetInactivityEmote(AnimatedSpriteRenderer chosenRenderer, bool refreshFrameOnEnter)
    {
        playingEndStage = false;
        playingCornered = false;
        playingJump = false;

        if (louieMovement != null && louieMovement.isDead)
        {
            playingInactivity = false;
            return;
        }

        if (louieInactivityEmoteLoop != null)
            SetRendererBranchEnabled(louieInactivityEmoteLoop, false);

        if (louieInactivityEmoteLoopAlt != null)
            SetRendererBranchEnabled(louieInactivityEmoteLoopAlt, false);

        activeLouieInactivityRenderer = chosenRenderer;

        if (activeLouieInactivityRenderer == null)
        {
            playingInactivity = false;

            if (owner == null)
                return;

            bool isIdle = owner.Direction == Vector2.zero;
            Vector2 faceDir = isIdle ? owner.FacingDirection : owner.Direction;
            ApplyDirection(faceDir, isIdle);
            return;
        }

        playingInactivity = true;

        activeLouieInactivityRenderer.loop = true;
        activeLouieInactivityRenderer.idle = false;
        activeLouieInactivityRenderer.pingPong = false;

        HardExclusive(activeLouieInactivityRenderer);

        if (refreshFrameOnEnter)
            activeLouieInactivityRenderer.RefreshFrame();
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
        playingCornered = false;

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

    public void SetInactivityEmoteRandom(float chanceAlt)
    {
        float chance = Mathf.Clamp01(chanceAlt);

        var chosen =
            (louieInactivityEmoteLoopAlt != null && Random.value <= chance)
                ? louieInactivityEmoteLoopAlt
                : louieInactivityEmoteLoop;

        SetInactivityEmote(chosen, true);
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
            playingJump = false;
            transform.localPosition = localOffset;
            return;
        }

        if (owner == null)
        {
            Destroy(gameObject);
            return;
        }

        if (owner.isDead)
        {
            if (owner.IsHoleDeathInProgress)
            {
                transform.localPosition = localOffset;
                return;
            }

            Destroy(gameObject);
            return;
        }

        transform.localPosition = localOffset;

        bool ownerOnRedBoat = BoatRideZone.IsRidingBoat(owner);

        if (ownerOnRedBoat)
        {
            ClearHeadOnlyOffsetsIfNeeded();

            if (!suppressedByRedBoat)
                SuppressAllLouieVisuals();

            return;
        }

        if (suppressedByRedBoat)
            RestoreAfterBoatSuppression();

        if (useHeadOnlyPlayerVisual)
            ApplyHeadOnlyOffsetsIfNeeded(force: false);
        else
            ClearHeadOnlyOffsetsIfNeeded();

        if (playingJump)
        {
            EnsureJumpExclusive();
        }
        else if (playingCornered)
        {
            EnsureCorneredExclusive();
        }
        else if (playingInactivity)
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

            ApplyPinkRidingHorizontalFlipOverride(faceDir);

            if (isPinkLouieVisual && !IsPinkRidingAnimationActive())
                ForceDisableRightRenderer();
        }

        ApplyBlinkSyncFromOwnerIfNeeded();
        ApplyExternalTintIfNeeded();
    }

    private void ApplyExternalTintIfNeeded()
    {
        if (!allowExternalTint || !externalTintActive)
            return;

        if (louieSpriteRenderers == null || louieSpriteRenderers.Length == 0)
            return;

        for (int i = 0; i < louieSpriteRenderers.Length; i++)
        {
            var sr = louieSpriteRenderers[i];
            if (sr == null)
                continue;

            var baseColor = sr.color;
            var tint = externalTintColor;
            tint.a = baseColor.a;

            sr.color = Color.Lerp(tint, baseColor, externalTintNormalized);
        }
    }

    private void ApplyHeadOnlyOffsetsIfNeeded(bool force)
    {
        if (owner == null)
            return;

        if (!useHeadOnlyPlayerVisual)
            return;

        if (!force && headOnlyOffsetsApplied)
            return;

        Vector2 down = headOnlyDownLocalOffset;
        if (temporaryHeadOnlyDownDeltaActive)
            down += temporaryHeadOnlyDownDelta;

        owner.SetHeadOnlyMountedOffsets(
            headOnlyUpLocalOffset,
            down,
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

        if (playingCornered)
        {
            EnsureCorneredExclusive();
            return;
        }

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

        if (isPinkLouieVisual)
            ForceDisableRightRenderer();
    }

    private void EnsureInactivityExclusive()
    {
        if (louieMovement != null && louieMovement.isDead)
        {
            playingInactivity = false;
            return;
        }

        var target = activeLouieInactivityRenderer != null
            ? activeLouieInactivityRenderer
            : louieInactivityEmoteLoop;

        if (target == null)
        {
            playingInactivity = false;
            return;
        }

        HardExclusive(target);

        target.idle = false;
        target.loop = true;
        target.pingPong = false;
        target.RefreshFrame();
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

        if (owner != null)
        {
            bool isIdle = owner.Direction == Vector2.zero;
            Vector2 faceDir = isIdle ? owner.FacingDirection : owner.Direction;

            ApplyPinkRidingHorizontalFlipOverride(faceDir);

            if (isPinkLouieVisual && !playingInactivity && !IsPinkRidingAnimationActive())
                ForceDisableRightRenderer();
        }
    }

    private void ApplyBlinkSyncFromOwnerIfNeeded()
    {
        if (!syncBlinkFromPlayerWhenMounted)
            return;

        if (owner == null || !owner.IsMounted)
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
        {
            return;
        }

        SetRendererBranchEnabled(louieRight, false);
    }

    private void ApplyDirection(Vector2 faceDir, bool isIdle)
    {
        if (faceDir == Vector2.zero)
            faceDir = Vector2.down;

        AnimatedSpriteRenderer target;
        if (faceDir == Vector2.up) target = louieUp;
        else if (faceDir == Vector2.down) target = louieDown;
        else if (faceDir == Vector2.left) target = louieLeft != null ? louieLeft : louieRight;
        else if (faceDir == Vector2.right)
        {
            if (isPinkLouieVisual)
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
            if (active == louieLeft && (louieRight == null || isPinkLouieVisual))
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

        if (!enablePinkRightFix || !isPinkLouieVisual)
        {
            active.ClearRuntimeBaseLocalX();
            return;
        }

        if (faceDir == Vector2.right)
            active.SetRuntimeBaseLocalX(pinkRightFixedLocalX);
        else
            active.ClearRuntimeBaseLocalX();
    }

    private void SetExclusive(AnimatedSpriteRenderer keep)
    {
        SetRendererBranchEnabled(louieUp, keep == louieUp);
        SetRendererBranchEnabled(louieDown, keep == louieDown);
        SetRendererBranchEnabled(louieLeft, keep == louieLeft);
        SetRendererBranchEnabled(louieRight, keep == louieRight);

        SetRendererBranchEnabled(louieJumpAscendUp, keep == louieJumpAscendUp);
        SetRendererBranchEnabled(louieJumpAscendDown, keep == louieJumpAscendDown);
        SetRendererBranchEnabled(louieJumpAscendLeft, keep == louieJumpAscendLeft);
        SetRendererBranchEnabled(louieJumpAscendRight, keep == louieJumpAscendRight);

        SetRendererBranchEnabled(louieJumpDescendUp, keep == louieJumpDescendUp);
        SetRendererBranchEnabled(louieJumpDescendDown, keep == louieJumpDescendDown);
        SetRendererBranchEnabled(louieJumpDescendLeft, keep == louieJumpDescendLeft);
        SetRendererBranchEnabled(louieJumpDescendRight, keep == louieJumpDescendRight);

        SetRendererBranchEnabled(louieJumpUp, keep == louieJumpUp);
        SetRendererBranchEnabled(louieJumpDown, keep == louieJumpDown);
        SetRendererBranchEnabled(louieJumpLeft, keep == louieJumpLeft);
        SetRendererBranchEnabled(louieJumpRight, keep == louieJumpRight);

        SetRendererBranchEnabled(louieEndStage, keep == louieEndStage);

        if (louieInactivityEmoteLoop != null)
            SetRendererBranchEnabled(louieInactivityEmoteLoop, false);

        active = keep;

        if (active != null)
            SetRendererBranchEnabled(active, true);

        if (isPinkLouieVisual)
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

    public void SetCornered(bool on)
    {
        if (louieCornered == null)
            return;

        if (louieMovement != null && louieMovement.isDead)
        {
            playingCornered = false;
            SetRendererBranchEnabled(louieCornered, false);
            return;
        }

        playingCornered = on;

        if (on)
        {
            playingInactivity = false;
            playingEndStage = false;

            louieCornered.loop = true;
            louieCornered.idle = false;
            louieCornered.pingPong = false;
            louieCornered.CurrentFrame = 0;

            HardExclusive(louieCornered);
            louieCornered.RefreshFrame();
            return;
        }

        if (owner == null)
        {
            SetRendererBranchEnabled(louieCornered, false);
            return;
        }

        bool isIdle = owner.Direction == Vector2.zero;
        Vector2 faceDir = isIdle ? owner.FacingDirection : owner.Direction;

        ApplyDirection(faceDir, isIdle);
        SetRendererBranchEnabled(louieCornered, false);
    }

    private void EnsureCorneredExclusive()
    {
        if (louieMovement != null && louieMovement.isDead)
        {
            playingCornered = false;
            return;
        }

        if (louieCornered == null)
        {
            playingCornered = false;
            return;
        }

        HardExclusive(louieCornered);

        louieCornered.idle = false;
        louieCornered.loop = true;
        louieCornered.pingPong = false;
        louieCornered.RefreshFrame();
    }

    public bool HasJumpVisuals()
    {
        return
            louieJumpAscendUp != null || louieJumpAscendDown != null || louieJumpAscendLeft != null || louieJumpAscendRight != null ||
            louieJumpDescendUp != null || louieJumpDescendDown != null || louieJumpDescendLeft != null || louieJumpDescendRight != null ||
            louieJumpUp != null || louieJumpDown != null || louieJumpLeft != null || louieJumpRight != null;
    }

    public void SetJumpVisual(bool on, Vector2 facing, bool descending = false)
    {
        if (on && !HasJumpVisuals())
            return;

        playingJump = on;
        jumpPhase = descending ? JumpPhase.Descend : JumpPhase.Ascend;

        if (facing != Vector2.zero)
            jumpFacing = Cardinalize(facing);
        else if (owner != null && owner.FacingDirection != Vector2.zero)
            jumpFacing = Cardinalize(owner.FacingDirection);
        else
            jumpFacing = Vector2.down;

        if (!playingJump)
        {
            if (owner == null)
                return;

            bool isIdle = owner.Direction == Vector2.zero;
            Vector2 faceDir = isIdle ? owner.FacingDirection : owner.Direction;
            ApplyDirection(faceDir, isIdle);
            return;
        }

        EnsureJumpExclusive();
    }

    public void SetJumpPhase(bool descending)
    {
        if (!playingJump)
            return;

        JumpPhase nextPhase = descending ? JumpPhase.Descend : JumpPhase.Ascend;
        if (jumpPhase == nextPhase)
            return;

        jumpPhase = nextPhase;
        EnsureJumpExclusive();
    }

    private void EnsureJumpExclusive()
    {
        if (!playingJump)
            return;

        AnimatedSpriteRenderer target = PickJumpRenderer(jumpFacing, jumpPhase, out bool flipX);
        if (target == null)
            return;

        HardExclusive(target);

        target.idle = false;
        target.loop = true;
        target.pingPong = false;

        if (target.TryGetComponent<SpriteRenderer>(out var sr) && sr != null)
            sr.flipX = flipX;

        target.RefreshFrame();
    }

    private AnimatedSpriteRenderer PickJumpRenderer(Vector2 faceDir, JumpPhase phase, out bool flipX)
    {
        flipX = false;
        faceDir = Cardinalize(faceDir);

        AnimatedSpriteRenderer direct = PickPhaseDirectional(faceDir, phase, out flipX);
        if (direct != null)
            return direct;

        return PickSinglePhaseDirectional(faceDir, out flipX);
    }

    private AnimatedSpriteRenderer PickPhaseDirectional(Vector2 faceDir, JumpPhase phase, out bool flipX)
    {
        flipX = false;

        AnimatedSpriteRenderer up = phase == JumpPhase.Ascend ? louieJumpAscendUp : louieJumpDescendUp;
        AnimatedSpriteRenderer down = phase == JumpPhase.Ascend ? louieJumpAscendDown : louieJumpDescendDown;
        AnimatedSpriteRenderer left = phase == JumpPhase.Ascend ? louieJumpAscendLeft : louieJumpDescendLeft;
        AnimatedSpriteRenderer right = phase == JumpPhase.Ascend ? louieJumpAscendRight : louieJumpDescendRight;

        if (faceDir == Vector2.up)
            return up;

        if (faceDir == Vector2.down)
            return down;

        if (faceDir == Vector2.left)
        {
            if (left != null)
                return left;

            if (right != null)
                return right;

            return null;
        }

        if (faceDir == Vector2.right)
        {
            if (right != null)
                return right;

            if (left != null)
            {
                flipX = true;
                return left;
            }

            return null;
        }

        return down ?? up ?? left ?? right;
    }

    private AnimatedSpriteRenderer PickSinglePhaseDirectional(Vector2 faceDir, out bool flipX)
    {
        flipX = false;

        if (faceDir == Vector2.up)
            return louieJumpUp;

        if (faceDir == Vector2.down)
            return louieJumpDown;

        if (faceDir == Vector2.left)
        {
            if (louieJumpLeft != null)
                return louieJumpLeft;

            if (louieJumpRight != null)
                return louieJumpRight;

            return null;
        }

        if (faceDir == Vector2.right)
        {
            if (louieJumpRight != null)
                return louieJumpRight;

            if (louieJumpLeft != null)
            {
                flipX = true;
                return louieJumpLeft;
            }

            return null;
        }

        return louieJumpDown ?? louieJumpUp ?? louieJumpLeft ?? louieJumpRight;
    }

    private static Vector2 Cardinalize(Vector2 v)
    {
        if (v == Vector2.zero)
            return Vector2.down;

        if (Mathf.Abs(v.x) >= Mathf.Abs(v.y))
            return v.x >= 0f ? Vector2.right : Vector2.left;

        return v.y >= 0f ? Vector2.up : Vector2.down;
    }

    private bool IsPinkRidingAnimationActive()
    {
        PlayerRidingController rider = null;

        bool hasRider =
            owner != null &&
            owner.TryGetComponent(out rider);

        bool isTransition =
            isPinkLouieVisual &&
            hasRider &&
            rider != null &&
            rider.IsPlaying;

        return isTransition;
    }

    private void ApplyPinkRidingHorizontalFlipOverride(Vector2 faceDir)
    {
        if (!isPinkLouieVisual)
            return;

        ApplyPinkRidingFlipForRenderer(louieLeft, faceDir);
        ApplyPinkRidingFlipForRenderer(louieRight, faceDir);
    }

    private void ApplyPinkRidingFlipForRenderer(AnimatedSpriteRenderer renderer, Vector2 faceDir)
    {
        if (renderer == null)
            return;

        if (!renderer.TryGetComponent<SpriteRenderer>(out var sr) || sr == null)
            return;

        bool isRiding = IsPinkRidingAnimationActive();

        if (!isRiding)
            return;

        if (renderer == louieRight)
        {
            sr.flipX = (faceDir == Vector2.right);
            return;
        }

        if (renderer == louieLeft)
        {
            sr.flipX = (faceDir == Vector2.left);
        }
    }
}
