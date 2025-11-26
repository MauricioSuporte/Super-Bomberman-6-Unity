using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class StageIntroTransition : MonoBehaviour
{
    public static StageIntroTransition Instance;

    public Image fadeImage;
    public float duration = 3f;
    public AudioClip introMusic;

    public bool IntroRunning { get; private set; }

    private MovementController[] movementControllers;
    private BombController[] bombControllers;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        IntroRunning = true;

        movementControllers = FindObjectsOfType<MovementController>();
        bombControllers = FindObjectsOfType<BombController>();

        foreach (var m in movementControllers)
            m.enabled = false;

        foreach (var b in bombControllers)
            b.enabled = false;

        Time.timeScale = 0f;

        if (fadeImage != null)
        {
            var c = fadeImage.color;
            c.a = 1f;
            fadeImage.color = c;
            fadeImage.gameObject.SetActive(true);
        }

        StartCoroutine(FadeInRoutine());
    }

    private IEnumerator FadeInRoutine()
    {
        if (introMusic != null && GameMusicController.Instance != null)
        {
            GameMusicController.Instance.PlayMusic(introMusic, 1f);
        }

        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float alpha = 1f - Mathf.Clamp01(t / duration);

            if (fadeImage != null)
            {
                var c = fadeImage.color;
                c.a = alpha;
                fadeImage.color = c;
            }

            yield return null;
        }

        if (GameMusicController.Instance != null &&
            GameMusicController.Instance.defaultMusic != null)
        {
            GameMusicController.Instance.PlayMusic(
                GameMusicController.Instance.defaultMusic, 1f);
        }

        Time.timeScale = 1f;

        foreach (var m in movementControllers)
            if (m != null)
                m.enabled = true;

        foreach (var b in bombControllers)
            if (b != null)
                b.enabled = true;

        if (fadeImage != null)
            fadeImage.gameObject.SetActive(false);

        IntroRunning = false;
    }

    public void StartFadeOut(float fadeDuration)
    {
        if (fadeImage == null)
            return;

        fadeImage.gameObject.SetActive(true);

        var c = fadeImage.color;
        c.a = 0f;
        fadeImage.color = c;

        StopAllCoroutines();
        StartCoroutine(FadeOutRoutine(fadeDuration));
    }

    private IEnumerator FadeOutRoutine(float fadeDuration)
    {
        float t = 0f;

        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float alpha = Mathf.Clamp01(t / fadeDuration);

            if (fadeImage != null)
            {
                var c = fadeImage.color;
                c.a = alpha;
                fadeImage.color = c;
            }

            yield return null;
        }
    }
}
