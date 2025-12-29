using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HudsonLogoIntro : MonoBehaviour
{
    [Header("UI")]
    public Image logoImage;

    [Header("Input")]
    public KeyCode skipKey = KeyCode.Return;

    [Header("Timing")]
    public float fadeInSeconds = 2f;
    public float holdSeconds = 2f;
    public float fadeOutSeconds = 2f;

    [Header("Audio")]
    public AudioClip hudsonFx;

    public bool Running { get; private set; }
    public bool Skipped { get; private set; }

    bool skipRequested;

    void Awake()
    {
        ForceHide();
    }

    public void ForceHide()
    {
        Running = false;
        Skipped = false;
        skipRequested = false;

        if (logoImage != null)
        {
            logoImage.enabled = false;
            var c = logoImage.color;
            c.a = 0f;
            logoImage.color = c;
        }
    }

    public void Skip()
    {
        if (!Running)
            return;

        skipRequested = true;
        Skipped = true;
    }

    public IEnumerator Play()
    {
        if (logoImage == null)
            yield break;

        Running = true;
        Skipped = false;
        skipRequested = false;

        logoImage.enabled = true;
        var baseColor = logoImage.color;
        logoImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);

        yield return FadeAlpha(baseColor, 0f, 1f, fadeInSeconds);
        if (ConsumeSkip())
            yield break;

        if (hudsonFx != null && GameMusicController.Instance != null)
            GameMusicController.Instance.PlaySfx(hudsonFx, 1f);

        yield return Hold(holdSeconds);
        if (ConsumeSkip())
            yield break;

        yield return FadeAlpha(baseColor, 1f, 0f, fadeOutSeconds);

        ForceHide();
        Running = false;
    }

    IEnumerator FadeAlpha(Color baseColor, float startA, float endA, float seconds)
    {
        float d = Mathf.Max(0.001f, seconds);
        float t = 0f;

        while (t < d)
        {
            if (Input.GetKeyDown(skipKey))
                Skip();

            if (skipRequested)
                yield break;

            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(startA, endA, Mathf.Clamp01(t / d));
            logoImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
            yield return null;
        }

        logoImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, endA);
    }

    IEnumerator Hold(float seconds)
    {
        float d = Mathf.Max(0f, seconds);
        float t = 0f;

        while (t < d)
        {
            if (Input.GetKeyDown(skipKey))
                Skip();

            if (skipRequested)
                yield break;

            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    bool ConsumeSkip()
    {
        if (!skipRequested)
            return false;

        ForceHide();
        Running = false;
        return true;
    }
}
