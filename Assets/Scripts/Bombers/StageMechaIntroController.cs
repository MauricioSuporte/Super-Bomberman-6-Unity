using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class StageMechaIntroController : MonoBehaviour
{
    public static StageMechaIntroController Instance;

    [Header("Fade")]
    public Image fadeImage;

    [Header("Mecha Boss Sequence")]
    public MechaBossSequence mechaBossSequence;

    public bool IntroRunning { get; private set; }
    public bool FlashRunning { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (mechaBossSequence == null)
            mechaBossSequence = FindFirstObjectByType<MechaBossSequence>();
    }

    public void SetIntroRunning(bool running)
    {
        IntroRunning = running;
    }

    public IEnumerator Flash(float halfDuration, int cycles)
    {
        yield return FlashInternal(halfDuration, cycles, null);
    }

    public IEnumerator FlashWithOnLastBlack(float halfDuration, int cycles, Action onLastBlack)
    {
        yield return FlashInternal(halfDuration, cycles, onLastBlack);
    }

    IEnumerator FlashInternal(float halfDuration, int cycles, Action onLastBlack)
    {
        if (fadeImage == null)
            yield break;

        FlashRunning = true;

        try
        {
            fadeImage.gameObject.SetActive(true);

            Color baseColor = fadeImage.color;
            float blackHold = 0.5f;

            for (int i = 0; i < cycles; i++)
            {
                float t = 0f;
                while (t < halfDuration)
                {
                    if (GamePauseController.IsPaused)
                    {
                        yield return null;
                        continue;
                    }

                    t += Time.deltaTime;
                    float a = Mathf.Clamp01(t / halfDuration);
                    fadeImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
                    yield return null;
                }

                fadeImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);

                if (mechaBossSequence == null)
                    mechaBossSequence = FindFirstObjectByType<MechaBossSequence>();

                if (mechaBossSequence != null)
                {
                    bool empty = (i == cycles - 1) || (i % 2 == 0);
                    mechaBossSequence.SetStandsEmpty(empty);
                }

                if (i == cycles - 1)
                    onLastBlack?.Invoke();

                float hold = 0f;
                while (hold < blackHold)
                {
                    if (GamePauseController.IsPaused)
                    {
                        yield return null;
                        continue;
                    }

                    hold += Time.deltaTime;
                    yield return null;
                }

                t = 0f;
                while (t < halfDuration)
                {
                    if (GamePauseController.IsPaused)
                    {
                        yield return null;
                        continue;
                    }

                    t += Time.deltaTime;
                    float a = 1f - Mathf.Clamp01(t / halfDuration);
                    fadeImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
                    yield return null;
                }

                fadeImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
                yield return null;
            }

            fadeImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
            fadeImage.gameObject.SetActive(false);
        }
        finally
        {
            FlashRunning = false;
        }
    }

    public void StartFadeOut(float fadeDuration)
    {
        if (!fadeImage) return;

        fadeImage.gameObject.SetActive(true);

        Color c = fadeImage.color;
        c.a = 0f;
        fadeImage.color = c;

        StopAllCoroutines();
        StartCoroutine(FadeOutRoutine(fadeDuration));
    }

    IEnumerator FadeOutRoutine(float fadeDuration)
    {
        if (!fadeImage) yield break;

        float t = 0f;
        Color baseColor = fadeImage.color;

        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / fadeDuration);
            fadeImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
            yield return null;
        }
    }
}
