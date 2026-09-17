using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>Keeps the title cursor idle pose synchronized across its head and eye layers.</summary>
public sealed class TitleScreenCursorEyeIdle : MonoBehaviour
{
    const string ResetCompleteEyeName = "TitleScreenCursor_Eye_Row3_1";

    [SerializeField] AnimatedSpriteRenderer cursorRenderer;
    [SerializeField] Image headImage;
    [SerializeField] Sprite defaultHead;
    [SerializeField] Sprite defaultEye;
    [SerializeField] Sprite[] idleEyes;
    [SerializeField] Sprite resetCompleteEye;
    [SerializeField] Sprite[] headFrames;
    [SerializeField, Min(0.01f)] float idleDelay = 3f;
    [SerializeField, Min(0.01f)] float idleDuration = 0.5f;
    [SerializeField, Min(0.01f)] float alternateDuration = 0.5f;

    static readonly int[] HorizontalLeftToRight = { 0, 1, 2, 2, 1, 0, 3, 4, 4, 3, 0 };
    static readonly int[] VerticalBottomToTop = { 0, 7, 8, 8, 7, 0, 5, 6, 6, 5, 0 };

    float idleElapsed;
    Coroutine idleRoutine;
    Coroutine temporaryEyeRoutine;
    Vector3 currentEyeLocalOffset;

    public Vector3 CurrentEyeLocalOffset => currentEyeLocalOffset;

    void OnEnable() => ShowDefaultPose();

    void OnDisable() => StopActiveRoutines();

    void Update()
    {
        if (cursorRenderer == null || idleRoutine != null || temporaryEyeRoutine != null)
            return;

        idleElapsed += Time.unscaledDeltaTime;
        if (idleElapsed >= idleDelay)
            idleRoutine = StartCoroutine(PlayRandomIdlePose());
    }

    public void NotifyInput()
    {
        StopActiveRoutines();
        idleElapsed = 0f;
        ShowDefaultPose();
    }

    public void SetPose(Sprite head, Sprite eye, string reason)
    {
        int headIndex = FindHeadIndex(head);
        Vector2 eyeOffsetPixels = GetEyeOffsetPixels(headIndex);
        ApplyEyeOffset(eyeOffsetPixels);

        if (headImage != null && head != null)
            headImage.sprite = head;

        if (cursorRenderer != null && eye != null)
        {
            cursorRenderer.enabled = true;
            cursorRenderer.idleSprite = eye;
            cursorRenderer.RefreshFrame();
        }

    }

    public void ShowResetCompleteEye()
    {
        if (resetCompleteEye == null)
            resetCompleteEye = FindEyeByName(ResetCompleteEyeName);

        if (cursorRenderer == null || resetCompleteEye == null)
            return;

        StopActiveRoutines();
        SetPose(defaultHead, resetCompleteEye, "erased-start");
        temporaryEyeRoutine = StartCoroutine(ShowResetCompletePose());
    }

    IEnumerator PlayRandomIdlePose()
    {
        bool horizontal = Random.Range(0, 2) == 0;
        bool forward = Random.Range(0, 2) == 0;
        int[] frames = horizontal ? HorizontalLeftToRight : VerticalBottomToTop;
        string direction = horizontal ? "horizontal" : "vertical";
        if (!forward)
            direction += "-reverse";

        if (!HasAllHeadFrames() || !HasAllIdleEyes())
        {
            idleRoutine = null;
            idleElapsed = 0f;
            yield break;
        }

        Sprite selectedEye = idleEyes[Random.Range(0, idleEyes.Length)];
        float frameDuration = idleDuration / frames.Length;
        for (int i = 0; i < frames.Length; i++)
        {
            int frameIndex = forward ? frames[i] : frames[frames.Length - 1 - i];
            SetPose(
                headFrames[frameIndex],
                selectedEye,
                $"idle-{direction} frame={i + 1}/{frames.Length}");
            yield return new WaitForSecondsRealtime(frameDuration);
        }

        idleRoutine = null;
        idleElapsed = 0f;
        ShowDefaultPose();
    }

    IEnumerator ShowResetCompletePose()
    {
        yield return new WaitForSecondsRealtime(alternateDuration);
        temporaryEyeRoutine = null;
        ShowDefaultPose();
    }

    void StopActiveRoutines()
    {
        if (idleRoutine != null)
        {
            StopCoroutine(idleRoutine);
            idleRoutine = null;
        }

        if (temporaryEyeRoutine != null)
        {
            StopCoroutine(temporaryEyeRoutine);
            temporaryEyeRoutine = null;
        }
    }

    void ShowDefaultPose() => SetPose(defaultHead, defaultEye, "default");

    Sprite FindEyeByName(string spriteName)
    {
        return resetCompleteEye != null && resetCompleteEye.name == spriteName ? resetCompleteEye : null;
    }

    bool HasAllHeadFrames()
    {
        if (headFrames == null || headFrames.Length < 9)
            return false;

        for (int i = 0; i < 9; i++)
            if (headFrames[i] == null)
                return false;

        return true;
    }

    bool HasAllIdleEyes()
    {
        if (idleEyes == null || idleEyes.Length < 7)
            return false;

        for (int i = 0; i < 7; i++)
            if (idleEyes[i] == null)
                return false;

        return true;
    }

    int FindHeadIndex(Sprite head)
    {
        if (headFrames != null)
        {
            for (int i = 0; i < headFrames.Length; i++)
                if (headFrames[i] == head)
                    return i;
        }

        return head == defaultHead ? 0 : -1;
    }

    Vector2 GetEyeOffsetPixels(int headIndex)
    {
        return headIndex switch
        {
            1 => new Vector2(-1f, 0f),
            2 => new Vector2(-2f, 0f),
            3 => new Vector2(1f, 0f),
            4 => new Vector2(2f, 0f),
            5 => new Vector2(0f, -1f),
            6 => new Vector2(0f, -2f),
            7 => new Vector2(0f, 1f),
            8 => new Vector2(0f, 2f),
            _ => Vector2.zero
        };
    }

    void ApplyEyeOffset(Vector2 pixelOffset)
    {
        RectTransform eyesRect = cursorRenderer != null ? cursorRenderer.transform as RectTransform : null;
        if (eyesRect == null || defaultHead == null)
        {
            currentEyeLocalOffset = Vector3.zero;
            return;
        }

        float uiUnitsPerSpritePixel = eyesRect.rect.height * Mathf.Abs(eyesRect.localScale.y) /
                                      Mathf.Max(1f, defaultHead.rect.height);
        currentEyeLocalOffset = pixelOffset * uiUnitsPerSpritePixel;
        cursorRenderer.SetRuntimeBaseLocalOffset(currentEyeLocalOffset);
    }

}
