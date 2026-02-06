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

    [Header("Loop / Idle")]
    public bool loop = true;
    public bool idle = true;

    [Header("Frame Offsets")]
    public Vector2[] frameOffsets;

    [Header("Ping Pong")]
    public bool pingPong = false;

    [Header("Safety")]
    public bool disableOffsetsIfThisObjectHasRigidbody2D = true;

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

    void Awake()
    {
        EnsureTargets();

        initialVisualLocalPosition = visualTransform != null ? visualTransform.localPosition : Vector3.zero;

        bool hasRbHere = disableOffsetsIfThisObjectHasRigidbody2D && GetComponent<Rigidbody2D>() != null;
        canMoveVisualLocal = visualTransform != null && !hasRbHere;
    }

    void OnEnable()
    {
        EnsureTargets();

        if (spriteRenderer != null) spriteRenderer.enabled = true;
        if (uiImage != null) uiImage.enabled = true;

        direction = 1;

        SetupTiming();
        CancelInvoke(nameof(NextFrame));
        InvokeRepeating(nameof(NextFrame), animationTime, animationTime);

        ApplyFrame();
    }

    void OnDisable()
    {
        CancelInvoke(nameof(NextFrame));
        ResetOffset();

        EnsureTargets();

        if (spriteRenderer != null) spriteRenderer.enabled = false;
        if (uiImage != null) uiImage.enabled = false;
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
        ApplyFrame();
    }

    void NextFrame()
    {
        if (frozen) return;

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

        Vector3 basePos;

        if (hasExternalBase)
            basePos = externalBaseLocalPos;
        else
            basePos = initialVisualLocalPosition;

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
        ApplyFrame();
    }

    public void SetRuntimeBaseLocalX(float desiredLocalX)
    {
        EnsureTargets();
        if (visualTransform == null) return;

        runtimeLockX = true;
        runtimeLockedLocalX = desiredLocalX;
        ApplyFrame();
    }

    public void ClearRuntimeBaseLocalX()
    {
        runtimeLockX = false;
        ApplyFrame();
    }

    public void ClearRuntimeBaseOffset()
    {
        runtimeBaseOffset = Vector3.zero;
        runtimeLockX = false;
        ApplyFrame();
    }

    public IEnumerator PlayCycles(int cycles)
    {
        EnsureTargets();

        if (animationSprite == null || animationSprite.Length == 0 || cycles <= 0)
            yield break;

        if (!gameObject.activeInHierarchy)
            yield break;

        CancelInvoke(nameof(NextFrame));

        bool prevIdle = idle;
        bool prevLoop = loop;

        idle = false;
        loop = false;

        direction = 1;
        CurrentFrame = 0;

        SetupTiming();

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

        CancelInvoke(nameof(NextFrame));
        InvokeRepeating(nameof(NextFrame), animationTime, animationTime);
    }
}
