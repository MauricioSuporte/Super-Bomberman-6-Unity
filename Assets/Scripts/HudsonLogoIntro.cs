using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(AudioSource))]
public class HudsonLogoIntro : MonoBehaviour
{
    [Header("UI")]
    public Image logoImage;

    [Header("Timing")]
    public float fadeInSeconds = 2f;
    public float holdSeconds = 2f;
    public float fadeOutSeconds = 2f;

    [Header("Audio")]
    public AudioClip hudsonFx;

    public bool Running { get; private set; }
    public bool Skipped { get; private set; }

    bool skipRequested;
    AudioSource sfxSource;

    void Awake()
    {
        sfxSource = GetComponent<AudioSource>();
        if (sfxSource != null)
        {
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
        }

        ForceHide();
    }

    void OnDisable()
    {
        StopHudsonFx();
    }

    void StopHudsonFx()
    {
        if (sfxSource == null)
            return;

        if (sfxSource.isPlaying)
            sfxSource.Stop();

        sfxSource.clip = null;
    }

    void PlayHudsonFx()
    {
        if (hudsonFx == null || sfxSource == null)
            return;

        sfxSource.Stop();
        sfxSource.clip = hudsonFx;
        sfxSource.loop = false;
        sfxSource.Play();
    }

    public void ForceHide()
    {
        Running = false;
        Skipped = false;
        skipRequested = false;

        StopHudsonFx();

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

        StopHudsonFx();
    }

    public IEnumerator Play()
    {
        if (logoImage == null)
            yield break;

        Running = true;
        Skipped = false;
        skipRequested = false;

        StopHudsonFx();

        logoImage.enabled = true;
        var baseColor = logoImage.color;
        logoImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);

        yield return FadeAlpha(baseColor, 0f, 1f, fadeInSeconds);
        if (ConsumeSkip())
            yield break;

        PlayHudsonFx();

        yield return Hold(holdSeconds);
        if (ConsumeSkip())
            yield break;

        yield return FadeAlpha(baseColor, 1f, 0f, fadeOutSeconds);
        if (ConsumeSkip())
            yield break;

        ForceHide();
        Running = false;
    }

    bool IsSkipPressed()
    {
        var input = PlayerInputManager.Instance;
        if (input == null)
            return false;

        return input.AnyGetDown(PlayerAction.Start) || input.AnyGetDown(PlayerAction.ActionA);
    }

    IEnumerator FadeAlpha(Color baseColor, float startA, float endA, float seconds)
    {
        float d = Mathf.Max(0.001f, seconds);
        float t = 0f;

        while (t < d)
        {
            if (IsSkipPressed())
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
            if (IsSkipPressed())
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
