using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Plays the title cursor's fixed-duration confirmation pose sequence.</summary>
public sealed class TitleScreenCursorConfirmAnimator : MonoBehaviour
{
    [SerializeField] AnimatedSpriteRenderer eyesRenderer;
    [SerializeField] TitleScreenCursorEyeIdle eyeIdle;
    [SerializeField] RectTransform eyesRect;
    [SerializeField] Image headImage;
    [SerializeField] Sprite defaultHead;
    [SerializeField] Sprite[] headFrames;
    [SerializeField, Min(0.01f)] float duration = 0.5f;

    bool playing;

    public void Configure(
        AnimatedSpriteRenderer configuredEyesRenderer,
        TitleScreenCursorEyeIdle configuredEyeIdle,
        RectTransform configuredEyesRect,
        Image configuredHeadImage,
        Sprite configuredDefaultHead,
        Sprite[] configuredHeadFrames)
    {
        eyesRenderer = configuredEyesRenderer;
        eyeIdle = configuredEyeIdle;
        eyesRect = configuredEyesRect;
        headImage = configuredHeadImage;
        defaultHead = configuredDefaultHead;
        headFrames = configuredHeadFrames;
    }

    public IEnumerator Play()
    {
        if (playing)
            yield break;

        playing = true;

        bool canAnimate = eyesRenderer != null && eyesRect != null && headImage != null &&
                          defaultHead != null && HasAllHeadFrames();
        if (!canAnimate)
        {
            yield return new WaitForSecondsRealtime(duration);
            playing = false;
            yield break;
        }

        bool restoreEyeIdle = eyeIdle != null && eyeIdle.enabled;
        if (eyeIdle != null)
            eyeIdle.enabled = false;

        Vector3 baseEyesPosition = eyesRect.localPosition;
        float frameDuration = duration / headFrames.Length;
        // localPosition is measured in the parent UI space, so include the cursor's
        // own scale. This keeps the bounce proportional to the displayed cursor
        // when TitleScreenController changes its UI scale for the resolution.
        float cursorScaleY = Mathf.Abs(eyesRect.localScale.y);
        float uiUnitsPerSpritePixel = eyesRect.rect.height * cursorScaleY /
                                      Mathf.Max(1f, defaultHead.rect.height);

        // Confirmation head sprites already contain their eyes.
        eyesRenderer.enabled = false;

        for (int i = 0; i < headFrames.Length; i++)
        {
            headImage.sprite = headFrames[i];
            Vector2 bounceOffsetPixels = GetFrameOffset(i);
            Vector2 bounceOffset = bounceOffsetPixels * uiUnitsPerSpritePixel;
            eyesRect.localPosition = baseEyesPosition + (Vector3)bounceOffset;
            yield return new WaitForSecondsRealtime(frameDuration);
        }

        eyesRect.localPosition = baseEyesPosition;
        headImage.sprite = defaultHead;
        eyesRenderer.enabled = true;

        if (eyeIdle != null && restoreEyeIdle)
            eyeIdle.enabled = true;

        playing = false;
    }

    bool HasAllHeadFrames()
    {
        if (headFrames == null || headFrames.Length != 9)
            return false;

        for (int i = 0; i < headFrames.Length; i++)
            if (headFrames[i] == null)
                return false;

        return true;
    }

    static Vector2 GetFrameOffset(int frame)
    {
        return frame switch
        {
            3 or 4 or 5 => new Vector2(0f, 2f),
            7 => new Vector2(0f, -1f),
            _ => Vector2.zero
        };
    }

}
