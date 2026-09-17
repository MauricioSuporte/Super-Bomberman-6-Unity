using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Plays the non-blocking cursor response for unavailable menu options.</summary>
public sealed class TitleScreenCursorDeniedAnimator : MonoBehaviour
{
    [SerializeField] AnimatedSpriteRenderer eyesRenderer;
    [SerializeField] TitleScreenCursorEyeIdle eyeIdle;
    [SerializeField] Image headImage;
    [SerializeField] Sprite defaultHead;
    [SerializeField] Sprite eyeRow3_2;
    [SerializeField] Sprite head1;
    [SerializeField] Sprite head2;
    [SerializeField] Sprite head3;
    [SerializeField] Sprite head4;
    [SerializeField, Min(0.01f)] float duration = 0.5f;

    Coroutine routine;

    public void Play()
    {
        Cancel();
        if (!CanAnimate())
            return;

        routine = StartCoroutine(PlayRoutine());
    }

    public void Cancel()
    {
        if (routine == null)
            return;

        StopCoroutine(routine);
        routine = null;
        RestoreDefault();
    }

    IEnumerator PlayRoutine()
    {
        bool restoreEyeIdle = eyeIdle != null && eyeIdle.enabled;
        if (eyeIdle != null)
            eyeIdle.enabled = false;

        Sprite[] heads = { head1, head2, defaultHead, head3, head4, head3, defaultHead };
        bool[] showEyes = { false, false, true, false, false, false, true };
        float frameDuration = duration / heads.Length;

        for (int i = 0; i < heads.Length; i++)
        {
            headImage.sprite = heads[i];
            eyesRenderer.enabled = showEyes[i];
            if (showEyes[i])
            {
                eyesRenderer.idleSprite = eyeRow3_2;
                eyesRenderer.RefreshFrame();
            }

            yield return new WaitForSecondsRealtime(frameDuration);
        }

        routine = null;
        RestoreDefault(restoreEyeIdle);
    }

    void RestoreDefault(bool restoreEyeIdle = true)
    {
        if (headImage != null)
            headImage.sprite = defaultHead;

        if (eyesRenderer != null)
            eyesRenderer.enabled = true;

        if (eyeIdle != null && restoreEyeIdle)
            eyeIdle.enabled = true;
    }

    bool CanAnimate()
    {
        return eyesRenderer != null && headImage != null && defaultHead != null && eyeRow3_2 != null &&
               head1 != null && head2 != null && head3 != null && head4 != null;
    }
}
