using UnityEngine;

using System.Collections.Generic;

public class MountVisualController : MonoBehaviour
{
    [Header("Owner")]
    public MovementController owner;

    [Header("Player Visual While Mounted")]
    public bool useHeadOnlyPlayerVisual = false;

    [Header("Keep Move Animation When Idle")]
    [SerializeField] private bool keepMoveAnimationWhenIdle = false;

    [Header("Walk Animation Timing")]
    [SerializeField] private bool scaleWalkAnimationWithOwnerSpeed = true;

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

    [Header("HeadOnly Mount Visuals")]
    [SerializeField] private AnimatedSpriteRenderer louieHeadOnlyUp;
    [SerializeField] private AnimatedSpriteRenderer louieHeadOnlyDown;
    [SerializeField] private AnimatedSpriteRenderer louieHeadOnlyLeft;

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
    private bool playingExternalStun;
    private bool playingCartHeadOnly;
    private bool externalStunShakeActive;

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
    private bool playerEffectTintActive;
    private Color playerEffectTintColor = Color.white;
    private float playerEffectTintNormalized;

    private AnimatedSpriteRenderer activeLouieInactivityRenderer;
    private AnimatedSpriteRenderer activeExternalStunRenderer;
    private Vector3 externalStunShakeOffset;
    private float debugWalkAnimationNextLogTime;
    private int debugWalkAnimationLastLouieFrame = -1;
    private int debugWalkAnimationLastOwnerFrame = -1;
    private float debugWalkAnimationLastLouieFrameTime = -1f;
    private AnimatedSpriteRenderer debugWalkAnimationLastRenderer;
    private Vector2 debugWalkAnimationLastFaceDir = Vector2.zero;
    private bool hasLastWalkAnimationState;
    private Vector2 lastWalkAnimationFaceDir = Vector2.down;
    private bool lastWalkAnimationWasIdle = true;
    private AnimatedSpriteRenderer lastWalkAnimationRenderer;
    private Vector2 cartHeadOnlyFacing = Vector2.down;
    private Vector2 cartHeadOnlyUpOffset = Vector2.zero;
    private Vector2 cartHeadOnlyDownOffset = Vector2.zero;
    private Vector2 cartHeadOnlyLeftOffset = Vector2.zero;
    private Vector2 cartHeadOnlyRightOffset = Vector2.zero;
    private bool cartHeadOnlyOffsetsActive;
    private readonly Dictionary<AnimatedSpriteRenderer, AnimationTimingSnapshot> originalAnimationTiming = new();

    private struct AnimationTimingSnapshot
    {
        public float AnimationTime;
        public bool UseSequenceDuration;
        public float SequenceDuration;
    }

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

    private void OnValidate()
    {
        ResolveHeadOnlyVisualReferences();
    }

    public void SetTemporaryHeadOnlyDownDelta(Vector2 delta, bool active)
    {
        temporaryHeadOnlyDownDelta = delta;
        temporaryHeadOnlyDownDeltaActive = active;

        headOnlyOffsetsApplied = false;
        ApplyHeadOnlyOffsetsIfNeeded(force: true);
    }

    public void RefreshHeadOnlyPlayerOffsets()
    {
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

    public void SetPlayerEffectTint(bool active, Color tintColor, float normalized01)
    {
        playerEffectTintActive = active;
        playerEffectTintColor = tintColor;
        playerEffectTintNormalized = Mathf.Clamp01(normalized01);

        if (!playerEffectTintActive)
            RestoreLouieOriginalColors();
    }

    public void Bind(MovementController movement)
    {
        owner = movement;
        playingEndStage = false;
        playingInactivity = false;
        playingCornered = false;
        playingExternalStun = false;
        playingCartHeadOnly = false;
        externalStunShakeActive = false;
        activeExternalStunRenderer = null;
        externalStunShakeOffset = Vector3.zero;
        suppressedByRedBoat = false;

        headOnlyOffsetsApplied = false;

        externalTintActive = false;
        externalTintColor = Color.white;
        externalTintNormalized = 1f;
        playerEffectTintActive = false;
        playerEffectTintColor = Color.white;
        playerEffectTintNormalized = 1f;
        RestoreLouieOriginalColors();

        playingJump = false;
        jumpFacing = Vector2.down;
        jumpPhase = JumpPhase.Ascend;
        cartHeadOnlyFacing = Vector2.down;
        cartHeadOnlyUpOffset = Vector2.zero;
        cartHeadOnlyDownOffset = Vector2.zero;
        cartHeadOnlyLeftOffset = Vector2.zero;
        cartHeadOnlyRightOffset = Vector2.zero;
        cartHeadOnlyOffsetsActive = false;

        CacheAllRenderers();
        RestoreLouieOriginalColors();
        ResolveHeadOnlyVisualReferences();

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
        if (playingExternalStun)
            return;

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
        if (playingExternalStun)
            return;

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
        if (playingExternalStun)
            return false;

        if (louieEndStage == null)
            return false;

        playingInactivity = false;
        playingJump = false;
        playingCartHeadOnly = false;
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
        if (playingExternalStun)
            return;

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
        if (playingExternalStun)
            return;

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
        CacheOriginalAnimationTiming();
    }

    private void CacheOriginalAnimationTiming()
    {
        if (allAnimatedRenderers == null)
            return;

        for (int i = 0; i < allAnimatedRenderers.Length; i++)
        {
            AnimatedSpriteRenderer renderer = allAnimatedRenderers[i];
            if (renderer == null || originalAnimationTiming.ContainsKey(renderer))
                continue;

            originalAnimationTiming.Add(renderer, new AnimationTimingSnapshot
            {
                AnimationTime = renderer.animationTime,
                UseSequenceDuration = renderer.useSequenceDuration,
                SequenceDuration = renderer.sequenceDuration
            });
        }
    }

    private void LateUpdate()
    {
        if (louieMovement != null && louieMovement.isDead)
        {
            playingInactivity = false;
            playingEndStage = false;
            playingJump = false;
            ApplyCurrentLocalOffset();
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
                ApplyCurrentLocalOffset();
                return;
            }

            Destroy(gameObject);
            return;
        }

        ApplyCurrentLocalOffset();

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

        if (playingCartHeadOnly)
        {
            EnsureCartHeadOnlyExclusive();
        }
        else if (playingJump)
        {
            EnsureJumpExclusive();
        }
        else if (playingExternalStun)
        {
            EnsureExternalStunExclusive();
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
            if (scaleWalkAnimationWithOwnerSpeed)
                AdvanceManualWalkAnimation();

            ApplyPinkRidingHorizontalFlipOverride(faceDir);

            if (isPinkLouieVisual && !IsPinkRidingAnimationActive())
                ForceDisableRightRenderer();
        }

        ApplyBlinkSyncFromOwnerIfNeeded();
        ApplyExternalTintIfNeeded();
        ApplyPlayerEffectTintIfNeeded();
        LogWalkAnimationFramesIfNeeded();
    }

    private void LogWalkAnimationFramesIfNeeded()
    {
        if (owner == null || active == null)
            return;

        if (playingEndStage || playingInactivity || playingCornered || playingExternalStun || playingJump || playingCartHeadOnly)
            return;

        AnimatedSpriteRenderer ownerRenderer = owner.ActiveSpriteRenderer;
        int louieFrame = active.CurrentFrame;
        int ownerFrame = ownerRenderer != null ? ownerRenderer.CurrentFrame : -1;
        int louieFrameCount = GetAnimationFrameCount(active);

        Vector2 ownerDir = owner.Direction;
        bool isIdle = ownerDir == Vector2.zero;
        Vector2 faceDir = isIdle ? owner.FacingDirection : ownerDir;

        bool rendererChanged = debugWalkAnimationLastRenderer != null && active != debugWalkAnimationLastRenderer;
        bool faceChanged = debugWalkAnimationLastFaceDir != Vector2.zero && faceDir != debugWalkAnimationLastFaceDir;
        bool louieFrameChanged = louieFrame != debugWalkAnimationLastLouieFrame;
        bool ownerFrameChanged = ownerFrame != debugWalkAnimationLastOwnerFrame;
        bool frameChanged = louieFrameChanged || ownerFrameChanged;

        if (!frameChanged && !rendererChanged && !faceChanged && Time.unscaledTime < debugWalkAnimationNextLogTime)
            return;

        float now = Time.unscaledTime;
        bool sameAnimationStream =
            !rendererChanged &&
            !faceChanged &&
            debugWalkAnimationLastLouieFrame >= 0 &&
            debugWalkAnimationLastRenderer == active;

        int framesAdvanced = sameAnimationStream && louieFrameChanged
            ? GetLoopedFrameAdvance(debugWalkAnimationLastLouieFrame, louieFrame, louieFrameCount)
            : 0;

        int skippedFrames = Mathf.Max(0, framesAdvanced - 1);
        float frameDelta = debugWalkAnimationLastLouieFrameTime >= 0f
            ? now - debugWalkAnimationLastLouieFrameTime
            : 0f;

        float expectedFrameTime = Mathf.Max(0.0001f, active.animationTime);
        float expectedSteps = frameDelta > 0f ? frameDelta / expectedFrameTime : 0f;

        debugWalkAnimationLastLouieFrame = louieFrame;
        debugWalkAnimationLastOwnerFrame = ownerFrame;
        debugWalkAnimationLastRenderer = active;
        debugWalkAnimationLastFaceDir = faceDir;
        debugWalkAnimationNextLogTime = now;

        if (louieFrameChanged || rendererChanged || faceChanged)
            debugWalkAnimationLastLouieFrameTime = now;
    }

    private static int GetLoopedFrameAdvance(int previousFrame, int currentFrame, int frameCount)
    {
        frameCount = Mathf.Max(1, frameCount);

        if (currentFrame >= previousFrame)
            return currentFrame - previousFrame;

        return currentFrame + frameCount - previousFrame;
    }

    private static string GetRendererDebug(AnimatedSpriteRenderer renderer)
    {
        if (renderer == null)
            return "null";

        int frameCount = GetAnimationFrameCount(renderer);
        string spriteName = "null";
        bool flipX = false;
        Vector3 localPos = renderer.transform.localPosition;

        if (renderer.TryGetComponent<SpriteRenderer>(out var sr) && sr != null)
        {
            spriteName = sr.sprite != null ? sr.sprite.name : "null";
            flipX = sr.flipX;
            localPos = sr.transform.localPosition;
        }

        return $"{renderer.name} frame:{renderer.CurrentFrame}/{frameCount} sprite:{spriteName} " +
               $"animTime:{renderer.animationTime:F4} timer:{renderer.DebugFrameTimer:F4} " +
               $"idle:{renderer.idle} loop:{renderer.loop} flipX:{flipX} " +
               $"localPos:{localPos} enabled:{renderer.isActiveAndEnabled}";
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

    private void ApplyPlayerEffectTintIfNeeded()
    {
        if (!playerEffectTintActive)
            return;

        if (louieSpriteRenderers == null || louieSpriteRenderers.Length == 0)
            return;

        for (int i = 0; i < louieSpriteRenderers.Length; i++)
        {
            var sr = louieSpriteRenderers[i];
            if (sr == null)
                continue;

            var baseColor = sr.color;
            var tint = playerEffectTintColor;
            tint.a = baseColor.a;

            sr.color = Color.Lerp(tint, baseColor, playerEffectTintNormalized);
        }
    }

    private void RestoreLouieOriginalColors()
    {
        if (louieSpriteRenderers == null || louieOriginalColors == null)
            return;

        int count = Mathf.Min(louieSpriteRenderers.Length, louieOriginalColors.Length);
        for (int i = 0; i < count; i++)
        {
            var sr = louieSpriteRenderers[i];
            if (sr == null)
                continue;

            Color original = louieOriginalColors[i];
            original.a = sr.color.a;
            sr.color = original;
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

    public void SetExternalStunVisual(AnimatedSpriteRenderer stunRenderer, bool on)
    {
        if (!on || stunRenderer == null)
        {
            bool wasPlayingExternalStun = playingExternalStun;
            playingExternalStun = false;
            activeExternalStunRenderer = null;

            if (!wasPlayingExternalStun || owner == null)
                return;

            bool isIdle = owner.Direction == Vector2.zero;
            Vector2 faceDir = isIdle ? owner.FacingDirection : owner.Direction;
            ApplyDirection(faceDir, isIdle);
            return;
        }

        playingInactivity = false;
        playingEndStage = false;
        playingCornered = false;
        playingJump = false;
        playingExternalStun = true;
        activeExternalStunRenderer = stunRenderer;

        EnsureExternalStunExclusive();
    }

    public void SetExternalStunShake(Vector3 localOffset, bool on)
    {
        externalStunShakeActive = on;
        externalStunShakeOffset = on ? localOffset : Vector3.zero;
        ApplyCurrentLocalOffset();
    }

    private void ApplyCurrentLocalOffset()
    {
        Vector3 offset = localOffset;
        if (externalStunShakeActive)
            offset += externalStunShakeOffset;

        transform.localPosition = offset;
    }

    private void EnsureExternalStunExclusive()
    {
        if (activeExternalStunRenderer == null)
        {
            playingExternalStun = false;
            return;
        }

        HardExclusive(activeExternalStunRenderer);
        activeExternalStunRenderer.loop = true;
        activeExternalStunRenderer.idle = false;
        activeExternalStunRenderer.pingPong = false;
        activeExternalStunRenderer.RefreshFrame();
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

        if (scaleWalkAnimationWithOwnerSpeed)
            ApplyOwnerWalkAnimationTiming(target);
        else
            RestoreOriginalAnimationTiming(target);

        bool rendererChanged = active != target;
        if (rendererChanged)
        {
            if (active != null)
                active.SetManualAnimationUpdate(false);

            SetExclusive(target);
        }

        SetRendererBranchEnabled(active, true);

        bool shouldIdle = isIdle && !keepMoveAnimationWhenIdle;
        string restartReason = scaleWalkAnimationWithOwnerSpeed && !shouldIdle && !rendererChanged
            ? GetWalkAnimationRestartReason(target, faceDir)
            : null;
        bool shouldRestart = restartReason != null;

        active.pingPong = false;
        active.idle = shouldIdle;
        active.loop = !shouldIdle;
        active.SetManualAnimationUpdate(scaleWalkAnimationWithOwnerSpeed && !shouldIdle);

        if (active != null && active.TryGetComponent<SpriteRenderer>(out var sr) && sr != null)
        {
            if (active == louieLeft && (louieRight == null || isPinkLouieVisual))
                sr.flipX = (faceDir == Vector2.right);
            else
                sr.flipX = false;
        }

        ApplyPinkRightXFix(faceDir);

        if (shouldRestart)
            active.RestartAnimation();
        else
            active.RefreshFrame();

        hasLastWalkAnimationState = true;
        lastWalkAnimationRenderer = active;
        lastWalkAnimationFaceDir = faceDir;
        lastWalkAnimationWasIdle = shouldIdle;
    }

    private string GetWalkAnimationRestartReason(AnimatedSpriteRenderer target, Vector2 faceDir)
    {
        if (!hasLastWalkAnimationState)
            return "first-active-frame";

        if (lastWalkAnimationRenderer != target)
            return null;

        if (lastWalkAnimationWasIdle)
            return "idle-to-move";

        if (UsesMirroredHorizontalRenderer(target) &&
            IsHorizontalDirectionFlip(lastWalkAnimationFaceDir, faceDir))
            return "mirrored-horizontal-flip";

        return null;
    }

    private bool UsesMirroredHorizontalRenderer(AnimatedSpriteRenderer renderer)
    {
        return renderer == louieLeft && (louieRight == null || isPinkLouieVisual);
    }

    private void AdvanceManualWalkAnimation()
    {
        if (active == null)
            return;

        if (active.idle)
            return;

        if (!IsWalkRenderer(active))
            return;

        if (active.RespectGamePause && GamePauseController.IsPaused)
            return;

        active.AdvanceAnimation(Time.unscaledDeltaTime, Time.deltaTime);
    }

    private bool IsWalkRenderer(AnimatedSpriteRenderer renderer)
    {
        return renderer == louieUp ||
               renderer == louieDown ||
               renderer == louieLeft ||
               renderer == louieRight;
    }

    private static bool IsHorizontalDirectionFlip(Vector2 previousFaceDir, Vector2 currentFaceDir)
    {
        return Mathf.Abs(previousFaceDir.x) > 0.01f &&
               Mathf.Abs(currentFaceDir.x) > 0.01f &&
               Mathf.Sign(previousFaceDir.x) != Mathf.Sign(currentFaceDir.x);
    }

    private void ApplyOwnerWalkAnimationTiming(AnimatedSpriteRenderer renderer)
    {
        if (renderer == null || owner == null)
            return;

        renderer.useSequenceDuration = false;
        renderer.animationTime = owner.GetWalkAnimationFrameTimeForFrameCount(GetAnimationFrameCount(renderer));
    }

    private void RestoreOriginalAnimationTiming(AnimatedSpriteRenderer renderer)
    {
        if (renderer == null)
            return;

        if (!originalAnimationTiming.TryGetValue(renderer, out var snapshot))
            return;

        renderer.useSequenceDuration = snapshot.UseSequenceDuration;
        renderer.sequenceDuration = snapshot.SequenceDuration;
        renderer.animationTime = snapshot.AnimationTime;
    }

    private static int GetAnimationFrameCount(AnimatedSpriteRenderer renderer)
    {
        return renderer != null && renderer.animationSprite != null && renderer.animationSprite.Length > 0
            ? renderer.animationSprite.Length
            : 4;
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

        SetRendererBranchEnabled(louieHeadOnlyUp, keep == louieHeadOnlyUp);
        SetRendererBranchEnabled(louieHeadOnlyDown, keep == louieHeadOnlyDown);
        SetRendererBranchEnabled(louieHeadOnlyLeft, keep == louieHeadOnlyLeft);

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

        if (!on)
            r.SetManualAnimationUpdate(false);

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
        if (playingExternalStun)
            return;

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

    public bool HasJumpDescendVisuals()
    {
        return
            louieJumpDescendUp != null ||
            louieJumpDescendDown != null ||
            louieJumpDescendLeft != null ||
            louieJumpDescendRight != null;
    }

    public bool HasHeadOnlyVisuals()
    {
        ResolveHeadOnlyVisualReferences();

        return
            louieHeadOnlyUp != null ||
            louieHeadOnlyDown != null ||
            louieHeadOnlyLeft != null;
    }

    public void SetCartHeadOnlyVisual(bool on, Vector2 facing)
    {
        ResolveHeadOnlyVisualReferences();

        if (on && !HasHeadOnlyVisuals())
        {
            playingCartHeadOnly = false;
            HideCartHeadOnlyVisuals();
            return;
        }

        playingCartHeadOnly = on;

        if (facing != Vector2.zero)
            cartHeadOnlyFacing = Cardinalize(facing);

        if (!playingCartHeadOnly)
        {
            HideCartHeadOnlyVisuals();
            return;
        }

        playingInactivity = false;
        playingEndStage = false;
        playingCornered = false;
        playingJump = false;
        playingExternalStun = false;
        EnsureCartHeadOnlyExclusive();
    }

    public void SetCartHeadOnlyOffsets(Vector2 up, Vector2 down, Vector2 left, Vector2 right)
    {
        cartHeadOnlyUpOffset = up;
        cartHeadOnlyDownOffset = down;
        cartHeadOnlyLeftOffset = left;
        cartHeadOnlyRightOffset = right;
        cartHeadOnlyOffsetsActive = true;

        if (playingCartHeadOnly)
            EnsureCartHeadOnlyExclusive();
    }

    public void ClearCartHeadOnlyOffsets()
    {
        ClearHeadOnlyRendererOffset(louieHeadOnlyUp);
        ClearHeadOnlyRendererOffset(louieHeadOnlyDown);
        ClearHeadOnlyRendererOffset(louieHeadOnlyLeft);

        cartHeadOnlyUpOffset = Vector2.zero;
        cartHeadOnlyDownOffset = Vector2.zero;
        cartHeadOnlyLeftOffset = Vector2.zero;
        cartHeadOnlyRightOffset = Vector2.zero;
        cartHeadOnlyOffsetsActive = false;
    }

    private void EnsureCartHeadOnlyExclusive()
    {
        if (!playingCartHeadOnly)
            return;

        AnimatedSpriteRenderer target = PickHeadOnlyRenderer(cartHeadOnlyFacing);
        if (target == null)
        {
            playingCartHeadOnly = false;
            return;
        }

        HardExclusive(target);
        target.idle = true;
        target.loop = false;
        target.pingPong = false;
        ApplyHeadOnlyFlip(target, cartHeadOnlyFacing);
        ApplyCartHeadOnlyOffset(target, cartHeadOnlyFacing);
        target.RefreshFrame();
    }

    private AnimatedSpriteRenderer PickHeadOnlyRenderer(Vector2 faceDir)
    {
        ResolveHeadOnlyVisualReferences();

        faceDir = Cardinalize(faceDir);

        if (faceDir == Vector2.up)
            return louieHeadOnlyUp != null ? louieHeadOnlyUp : louieHeadOnlyDown;

        if (faceDir == Vector2.left)
            return louieHeadOnlyLeft != null ? louieHeadOnlyLeft : louieHeadOnlyDown;

        if (faceDir == Vector2.right)
            return louieHeadOnlyLeft != null ? louieHeadOnlyLeft : louieHeadOnlyDown;

        return louieHeadOnlyDown ?? louieHeadOnlyUp ?? louieHeadOnlyLeft;
    }

    private bool ShouldFlipHeadOnlyRenderer(AnimatedSpriteRenderer renderer, Vector2 faceDir)
    {
        return faceDir == Vector2.right && renderer == louieHeadOnlyLeft;
    }

    private void ApplyHeadOnlyFlip(AnimatedSpriteRenderer renderer, Vector2 faceDir)
    {
        if (renderer == null)
            return;

        if (!renderer.TryGetComponent<SpriteRenderer>(out var sr) || sr == null)
            return;

        sr.flipX = ShouldFlipHeadOnlyRenderer(renderer, faceDir);
    }

    private void ApplyCartHeadOnlyOffset(AnimatedSpriteRenderer renderer, Vector2 faceDir)
    {
        if (renderer == null)
            return;

        ClearHeadOnlyRendererOffset(louieHeadOnlyUp);
        ClearHeadOnlyRendererOffset(louieHeadOnlyDown);
        ClearHeadOnlyRendererOffset(louieHeadOnlyLeft);

        if (!cartHeadOnlyOffsetsActive)
            return;

        Vector2 offset = PickCartHeadOnlyOffset(faceDir);
        renderer.SetRuntimeBaseLocalX(offset.x);
        renderer.SetRuntimeBaseLocalY(offset.y);
    }

    private Vector2 PickCartHeadOnlyOffset(Vector2 faceDir)
    {
        faceDir = Cardinalize(faceDir);

        if (faceDir == Vector2.up)
            return cartHeadOnlyUpOffset;

        if (faceDir == Vector2.left)
            return cartHeadOnlyLeftOffset;

        if (faceDir == Vector2.right)
            return cartHeadOnlyRightOffset;

        return cartHeadOnlyDownOffset;
    }

    private static void ClearHeadOnlyRendererOffset(AnimatedSpriteRenderer renderer)
    {
        if (renderer == null)
            return;

        renderer.ClearRuntimeBaseOffset();
    }

    private void HideCartHeadOnlyVisuals()
    {
        SetRendererBranchEnabled(louieHeadOnlyUp, false);
        SetRendererBranchEnabled(louieHeadOnlyDown, false);
        SetRendererBranchEnabled(louieHeadOnlyLeft, false);
    }

    private void ResolveHeadOnlyVisualReferences()
    {
        if (louieHeadOnlyUp == null)
            louieHeadOnlyUp = FindDirectOrNestedHeadOnly("HeadOnlyUp");

        if (louieHeadOnlyDown == null)
            louieHeadOnlyDown = FindDirectOrNestedHeadOnly("HeadOnlyDown");

        if (louieHeadOnlyLeft == null)
            louieHeadOnlyLeft = FindDirectOrNestedHeadOnly("HeadOnlyLeft");
    }

    private AnimatedSpriteRenderer FindDirectOrNestedHeadOnly(params string[] childNames)
    {
        if (childNames == null || childNames.Length == 0)
            return null;

        var trs = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < trs.Length; i++)
        {
            Transform t = trs[i];
            if (t == null)
                continue;

            if (!MatchesAnyName(t.name, childNames))
                continue;

            AnimatedSpriteRenderer renderer = t.GetComponent<AnimatedSpriteRenderer>();
            if (renderer != null)
                return renderer;
        }

        return null;
    }

    private static bool MatchesAnyName(string value, params string[] names)
    {
        if (string.IsNullOrWhiteSpace(value) || names == null)
            return false;

        for (int i = 0; i < names.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(names[i]) && value == names[i])
                return true;
        }

        return false;
    }

    public void SetJumpVisual(bool on, Vector2 facing, bool descending = false)
    {
        if (playingExternalStun)
            return;

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
            if (playingEndStage)
            {
                EnsureEndStageExclusive();
                return;
            }

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
