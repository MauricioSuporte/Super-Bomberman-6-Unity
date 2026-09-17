using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Plays the title cursor confirmation with synchronized, offset-free layers.</summary>
public sealed class TitleScreenCursorConfirmAnimator : MonoBehaviour
{
    [SerializeField] AnimatedSpriteRenderer eyesRenderer;
    [SerializeField] TitleScreenCursorEyeIdle eyeIdle;
    [SerializeField] Image headImage;
    [SerializeField] Sprite defaultHead;
    [SerializeField] Sprite confirmEye;
    [SerializeField] Sprite[] headFrames;
    [SerializeField, Min(0.01f)] float duration = 0.5f;

    bool playing;

    public void Configure(
        AnimatedSpriteRenderer configuredEyesRenderer,
        TitleScreenCursorEyeIdle configuredEyeIdle,
        Image configuredHeadImage,
        Sprite configuredDefaultHead,
        Sprite[] configuredHeadFrames)
    {
        eyesRenderer = configuredEyesRenderer;
        eyeIdle = configuredEyeIdle;
        headImage = configuredHeadImage;
        defaultHead = configuredDefaultHead;
        headFrames = configuredHeadFrames;
    }

    public IEnumerator Play()
    {
        if (playing)
            yield break;

        playing = true;
        bool canAnimate = eyesRenderer != null && headImage != null && defaultHead != null &&
                          confirmEye != null && HasAllHeadFrames();
        if (!canAnimate)
        {
            yield return new WaitForSecondsRealtime(duration);
            playing = false;
            yield break;
        }

        bool restoreEyeIdle = eyeIdle != null && eyeIdle.enabled;
        if (eyeIdle != null)
            eyeIdle.enabled = false;

        float frameDuration = duration / headFrames.Length;
        for (int i = 0; i < headFrames.Length; i++)
        {
            SetPose(headFrames[i], confirmEye, $"confirm frame={i + 1}/{headFrames.Length}");
            yield return new WaitForSecondsRealtime(frameDuration);
        }

        SetPose(defaultHead, confirmEye, "confirm-end");
        if (eyeIdle != null && restoreEyeIdle)
            eyeIdle.enabled = true;

        playing = false;
    }

    void SetPose(Sprite head, Sprite eye, string reason)
    {
        if (eyeIdle != null)
        {
            eyeIdle.SetPose(head, eye, reason);
            return;
        }

        headImage.sprite = head;
        eyesRenderer.enabled = true;
        eyesRenderer.idleSprite = eye;
        eyesRenderer.RefreshFrame();
    }

    bool HasAllHeadFrames()
    {
        if (headFrames == null || headFrames.Length != 11)
            return false;

        for (int i = 0; i < headFrames.Length; i++)
            if (headFrames[i] == null)
                return false;

        return true;
    }
}
