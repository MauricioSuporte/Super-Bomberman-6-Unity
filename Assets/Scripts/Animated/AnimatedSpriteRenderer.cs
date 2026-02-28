using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class AnimatedSpriteRenderer : MonoBehaviour
{
    SpriteRenderer spriteRenderer;
    Image uiImage;
    RectTransform uiRect;

    [Header("Sprites")]
    public Sprite idleSprite;
    public Sprite[] animationSprite;

    [Header("Flip")]
    public bool allowFlipX = false;

    [Header("Timing")]
    public float animationTime = 0.25f;
    public bool useSequenceDuration = false;
    public float sequenceDuration = 0.5f;

    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool respectGamePause = true;

    [Header("Loop / Idle")]
    public bool loop = true;
    public bool idle = true;

    [Header("Frame Offsets")]
    public Vector2[] frameOffsets;

    [Header("Ping Pong")]
    public bool pingPong = false;

    [Header("Safety")]
    public bool disableOffsetsIfThisObjectHasRigidbody2D = true;

    [Header("Safety (Standalone Objects)")]
    [SerializeField] private bool disableOffsetsIfStandaloneRoot = true;

    // ---------------------------
    // DEBUG (SURGICAL)
    // ---------------------------
    [Header("Debug (Surgical)")]
    [SerializeField] private bool debug = false;

    [SerializeField, Min(0.01f)] private float debugLogEverySeconds = 0.25f;
    [SerializeField] private bool debugLogFrameAdvance = true;
    [SerializeField] private bool debugLogEarlyReturns = true;

    private float nextDebugLogAtUnscaled;
    private int lastLoggedFrame = -999;

    // Expose for external logs without reflection
    public bool UseUnscaledTime => useUnscaledTime;
    public bool RespectGamePause => respectGamePause;

    int animationFrame;
    int direction = 1;

    Transform visualTransform;
    Vector3 initialVisualLocalPosition;
    bool canMoveVisualLocal;
    bool frozen;

    Vector3 runtimeBaseOffset;
    bool runtimeLockX;
    float runtimeLockedLocalX;

    bool hasExternalBase;
    Vector3 externalBaseLocalPos;

    bool savedRuntimeLockX;
    float savedRuntimeLockedLocalX;
    bool hasSavedRuntimeLockXState;

    float frameTimer;

    public int CurrentFrame
    {
        get => animationFrame;
        set
        {
            animationFrame = value;
            if (animationSprite != null && animationSprite.Length > 0)
            {
                if (animationFrame < 0) animationFrame = 0;
                if (animationFrame >= animationSprite.Length) animationFrame = animationSprite.Length - 1;
            }
        }
    }

    void EnsureTargets()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (uiImage == null)
            uiImage = GetComponent<Image>();

        if (spriteRenderer == null && uiImage == null)
            uiImage = GetComponentInChildren<Image>(true);

        if (spriteRenderer == null && uiImage == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (uiImage != null && uiRect == null)
            uiRect = uiImage.rectTransform;

        if (visualTransform == null)
        {
            if (spriteRenderer != null) visualTransform = spriteRenderer.transform;
            else if (uiImage != null) visualTransform = uiImage.transform;
            else visualTransform = transform;
        }
    }

    private void Dbg(string msg)
    {
        if (!debug) return;
        Debug.Log($"[AnimSR:{name}] {msg}");
    }

    private string TargetsDesc()
    {
        string sr = spriteRenderer != null ? $"SR=Y flipX={spriteRenderer.flipX} enabled={spriteRenderer.enabled} sprite={(spriteRenderer.sprite ? spriteRenderer.sprite.name : "NULL")}" : "SR=N";
        string ui = uiImage != null ? $"UI=Y enabled={uiImage.enabled} sprite={(uiImage.sprite ? uiImage.sprite.name : "NULL")}" : "UI=N";
        return $"{sr} {ui}";
    }

    void Awake()
    {
        EnsureTargets();

        initialVisualLocalPosition = visualTransform != null ? visualTransform.localPosition : Vector3.zero;

        bool hasRbHere = disableOffsetsIfThisObjectHasRigidbody2D && GetComponent<Rigidbody2D>() != null;

        bool isStandaloneRoot =
            disableOffsetsIfStandaloneRoot &&
            transform.parent == null &&
            visualTransform == transform;

        canMoveVisualLocal = visualTransform != null && !hasRbHere && !isStandaloneRoot;

        if (debug)
        {
            Dbg($"AWAKE canMoveVisualLocal={canMoveVisualLocal} hasRbHere={hasRbHere} isStandaloneRoot={isStandaloneRoot} " +
                $"parent={(transform.parent ? transform.parent.name : "NULL")} visualTransform={(visualTransform ? visualTransform.name : "NULL")} {TargetsDesc()}");
        }
    }

    void OnEnable()
    {
        EnsureTargets();

        if (spriteRenderer != null) spriteRenderer.enabled = true;
        if (uiImage != null) uiImage.enabled = true;

        direction = 1;
        frameTimer = 0f;

        SetupTiming();
        ApplyFrame();

        if (debug)
        {
            nextDebugLogAtUnscaled = Time.unscaledTime; // log imediato
            Dbg($"ON_ENABLE idle={idle} loop={loop} pingPong={pingPong} useUnscaled={useUnscaledTime} respectPause={respectGamePause} " +
                $"animLen={(animationSprite != null ? animationSprite.Length : 0)} idleSprite={(idleSprite ? idleSprite.name : "NULL")} " +
                $"curFrame={animationFrame} animTime={animationTime:0.###} seqDur={sequenceDuration:0.###} useSeq={useSequenceDuration} " +
                $"{TargetsDesc()} activeInHierarchy={gameObject.activeInHierarchy} activeEnabled={isActiveAndEnabled}");
        }
    }

    void OnDisable()
    {
        ResetOffset();

        EnsureTargets();

        if (spriteRenderer != null) spriteRenderer.enabled = false;
        if (uiImage != null) uiImage.enabled = false;

        if (debug)
            Dbg($"ON_DISABLE curFrame={animationFrame} frameTimer={frameTimer:0.###} idle={idle} loop={loop} {TargetsDesc()}");
    }

    void Update()
    {
        if (!isActiveAndEnabled)
            return;

        if (debug && Time.unscaledTime >= nextDebugLogAtUnscaled)
        {
            float dtNow = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            Dbg($"TICK idle={idle} frozen={frozen} paused={GamePauseController.IsPaused} frame={animationFrame} frameTimer={frameTimer:0.###} " +
                $"animTime={animationTime:0.###} loop={loop} pingPong={pingPong} dt={dtNow:0.####} {TargetsDesc()}");
            nextDebugLogAtUnscaled = Time.unscaledTime + debugLogEverySeconds;
        }

        if (frozen)
        {
            if (debug && debugLogEarlyReturns) Dbg("EARLY_RETURN reason=frozen");
            return;
        }

        if (respectGamePause && GamePauseController.IsPaused)
        {
            if (debug && debugLogEarlyReturns) Dbg("EARLY_RETURN reason=paused");
            return;
        }

        if (idle)
        {
            ApplyFrame();
            if (debug && debugLogEarlyReturns) Dbg("EARLY_RETURN reason=idle_true (ApplyFrame)");
            return;
        }

        if (animationSprite == null || animationSprite.Length == 0)
        {
            if (debug && debugLogEarlyReturns) Dbg("EARLY_RETURN reason=no_animationSprite");
            return;
        }

        SetupTiming();

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (dt <= 0f)
        {
            if (debug && debugLogEarlyReturns) Dbg("EARLY_RETURN reason=dt<=0");
            return;
        }

        frameTimer += dt;

        float step = Mathf.Max(0.0001f, animationTime);
        bool advanced = false;

        while (frameTimer >= step)
        {
            frameTimer -= step;
            int before = animationFrame;

            NextFrameInternal();
            advanced = true;

            if (debug && debugLogFrameAdvance && before != animationFrame)
                Dbg($"FRAME_ADV before={before} after={animationFrame} step={step:0.###} frameTimerNow={frameTimer:0.###}");
        }

        if (debug && advanced && debugLogFrameAdvance && lastLoggedFrame != animationFrame)
            lastLoggedFrame = animationFrame;
    }

    void SetupTiming()
    {
        if (animationSprite == null || animationSprite.Length == 0) return;
        if (!useSequenceDuration) return;

        sequenceDuration = Mathf.Max(sequenceDuration, 0.0001f);

        int framesInCycle;
        if (pingPong && animationSprite.Length > 1)
            framesInCycle = animationSprite.Length * 2 - 2;
        else
            framesInCycle = animationSprite.Length;

        animationTime = sequenceDuration / Mathf.Max(1, framesInCycle);
        animationTime = Mathf.Max(animationTime, 0.0001f);
    }

    public void RefreshFrame() => ApplyFrame();

    public void SetFrozen(bool value)
    {
        frozen = value;

        if (debug)
            Dbg($"SET_FROZEN value={value} idle={idle} frame={animationFrame}");

        if (frozen)
        {
            ApplyFrame();
            ResetOffset();
        }
    }

    public void SetExternalBaseLocalPosition(Vector3 baseLocalPos)
    {
        hasExternalBase = true;
        externalBaseLocalPos = baseLocalPos;

        if (debug)
            Dbg($"SET_EXTERNAL_BASE_LOCAL_POS base={baseLocalPos} runtimeLockX={runtimeLockX} runtimeLockedLocalX={runtimeLockedLocalX:0.###}");

        ApplyFrame();
    }

    void NextFrameInternal()
    {
        if (idle)
        {
            ApplyFrame();
            return;
        }

        if (animationSprite == null || animationSprite.Length == 0) return;

        if (!pingPong)
        {
            animationFrame++;

            if (loop && animationFrame >= animationSprite.Length) animationFrame = 0;
            else if (!loop && animationFrame >= animationSprite.Length) animationFrame = animationSprite.Length - 1;
        }
        else
        {
            animationFrame += direction;

            if (animationFrame >= animationSprite.Length)
            {
                if (animationSprite.Length == 1) animationFrame = 0;
                else
                {
                    animationFrame = animationSprite.Length - 2;
                    direction = -1;
                }
            }
            else if (animationFrame < 0)
            {
                if (animationSprite.Length == 1) animationFrame = 0;
                else
                {
                    animationFrame = 1;
                    direction = 1;
                }
            }
        }

        ApplyFrame();
    }

    void SetSprite(Sprite s)
    {
        EnsureTargets();

        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = s;
            return;
        }

        if (uiImage != null)
            uiImage.sprite = s;
    }

    void ApplyFrame()
    {
        EnsureTargets();
        if (spriteRenderer == null && uiImage == null) return;

        int frameToUse = animationFrame;

        if (idle)
        {
            SetSprite(idleSprite);
        }
        else
        {
            if (animationSprite == null || animationSprite.Length == 0) return;
            if (animationFrame < 0 || animationFrame >= animationSprite.Length) return;

            SetSprite(animationSprite[animationFrame]);
        }

        ApplyOffset(frameToUse);
    }

    void ApplyOffset(int frame)
    {
        if (!canMoveVisualLocal || visualTransform == null) return;

        Vector3 basePos = hasExternalBase ? externalBaseLocalPos : initialVisualLocalPosition;
        Vector3 pos = basePos + runtimeBaseOffset;

        if (frameOffsets != null && frameOffsets.Length > 0)
        {
            int idx = Mathf.Clamp(frame, 0, frameOffsets.Length - 1);
            Vector2 offset = frameOffsets[idx];

            if (spriteRenderer != null && spriteRenderer.flipX)
                offset.x = -offset.x;

            pos += (Vector3)offset;
        }

        if (runtimeLockX)
            pos.x = runtimeLockedLocalX;

        visualTransform.localPosition = pos;
    }

    void ResetOffset()
    {
        if (visualTransform == null) return;
        if (!canMoveVisualLocal) return;

        Vector3 basePos = hasExternalBase ? externalBaseLocalPos : initialVisualLocalPosition;
        Vector3 pos = basePos + runtimeBaseOffset;

        if (runtimeLockX)
            pos.x = runtimeLockedLocalX;

        visualTransform.localPosition = pos;
    }

    public void SetRuntimeBaseLocalY(float desiredLocalY)
    {
        EnsureTargets();
        if (visualTransform == null) return;

        runtimeBaseOffset = new Vector3(runtimeBaseOffset.x, desiredLocalY - initialVisualLocalPosition.y, 0f);

        if (debug)
            Dbg($"SET_RUNTIME_BASE_Y desiredLocalY={desiredLocalY:0.###} runtimeBaseOffset={runtimeBaseOffset} initLocalY={initialVisualLocalPosition.y:0.###}");

        ApplyFrame();
    }

    public void SetRuntimeBaseLocalX(float desiredLocalX)
    {
        EnsureTargets();
        if (visualTransform == null) return;

        runtimeLockX = true;
        runtimeLockedLocalX = desiredLocalX;

        if (debug)
            Dbg($"SET_RUNTIME_BASE_X desiredLocalX={desiredLocalX:0.###} runtimeLockX={runtimeLockX}");

        ApplyFrame();
    }

    public void ClearRuntimeBaseLocalX()
    {
        runtimeLockX = false;
        hasSavedRuntimeLockXState = false;

        if (debug)
            Dbg("CLEAR_RUNTIME_BASE_X");

        ApplyFrame();
    }

    public void ClearRuntimeBaseOffset()
    {
        runtimeBaseOffset = Vector3.zero;
        runtimeLockX = false;
        hasSavedRuntimeLockXState = false;

        if (debug)
            Dbg("CLEAR_RUNTIME_BASE_OFFSET");

        ApplyFrame();
    }

    public IEnumerator PlayCycles(int cycles)
    {
        EnsureTargets();

        if (animationSprite == null || animationSprite.Length == 0 || cycles <= 0)
            yield break;

        if (!gameObject.activeInHierarchy)
            yield break;

        bool prevIdle = idle;
        bool prevLoop = loop;

        idle = false;
        loop = false;

        direction = 1;
        CurrentFrame = 0;

        SetupTiming();

        if (debug)
            Dbg($"PLAY_CYCLES begin cycles={cycles} prevIdle={prevIdle} prevLoop={prevLoop} animLen={animationSprite.Length} animTime={animationTime:0.###}");

        for (int c = 0; c < cycles; c++)
        {
            if (!pingPong || animationSprite.Length <= 1)
            {
                for (int i = 0; i < animationSprite.Length; i++)
                {
                    CurrentFrame = i;
                    RefreshFrame();
                    yield return new WaitForSecondsRealtime(animationTime);
                }
            }
            else
            {
                int frame = 0;
                int dir = 1;

                int framesInCycle = animationSprite.Length * 2 - 2;

                for (int i = 0; i < framesInCycle; i++)
                {
                    CurrentFrame = frame;
                    RefreshFrame();
                    yield return new WaitForSecondsRealtime(animationTime);

                    frame += dir;

                    if (frame >= animationSprite.Length)
                    {
                        frame = animationSprite.Length - 2;
                        dir = -1;
                    }
                    else if (frame < 0)
                    {
                        frame = 1;
                        dir = 1;
                    }
                }
            }
        }

        idle = prevIdle;
        loop = prevLoop;

        CurrentFrame = 0;
        RefreshFrame();

        frameTimer = 0f;

        if (debug)
            Dbg($"PLAY_CYCLES end restored idle={idle} loop={loop} curFrame={animationFrame}");
    }

    public void SetExternalBaseOffsetFromInitial(Vector3 offsetFromInitial)
    {
        EnsureTargets();

        hasExternalBase = true;
        externalBaseLocalPos = initialVisualLocalPosition + offsetFromInitial;

        if (runtimeLockX)
        {
            if (!hasSavedRuntimeLockXState)
            {
                savedRuntimeLockX = runtimeLockX;
                savedRuntimeLockedLocalX = runtimeLockedLocalX;
                hasSavedRuntimeLockXState = true;
            }

            runtimeLockX = false;
        }

        if (debug)
            Dbg($"SET_EXTERNAL_BASE_OFFSET offset={offsetFromInitial} extBase={externalBaseLocalPos} savedLockX={hasSavedRuntimeLockXState}");

        ApplyFrame();
    }

    public void ClearExternalBase()
    {
        hasExternalBase = false;
        externalBaseLocalPos = Vector3.zero;

        if (hasSavedRuntimeLockXState)
        {
            runtimeLockX = savedRuntimeLockX;
            runtimeLockedLocalX = savedRuntimeLockedLocalX;
            hasSavedRuntimeLockXState = false;
        }

        if (debug)
            Dbg("CLEAR_EXTERNAL_BASE");

        ApplyFrame();
    }
}