using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
[RequireComponent(typeof(Image))]
public sealed class BattleTimeUpOverlay : MonoBehaviour
{
    const float ScreenWidth = 256f;
    const float ScreenHeight = 224f;
    const float TopHudHeight = 22f;
    const float DisplayWidth = 420f;
    const float DisplayHeight = 64f;
    const float DropDuration = 0.5f;
    const float TotalDuration = 3f;
    const string PrefabResourcesPath = "HUD/Draw/BattleTimeUpOverlay";
    const string SpriteResourcesPath = "HUD/Draw/TimeUp";
    const string SafeFrameName = "SafeFrame4x3";

    [SerializeField] private AudioClip timeUpMusic;
    [SerializeField, Range(0f, 1f)] private float timeUpMusicVolume = 1f;

    RectTransform rootRect;
    CanvasGroup canvasGroup;
    BattleOverlayAudioIsolation audioIsolation;

    public static IEnumerator PlayRoutine()
    {
        BattleTimeUpOverlay overlay = CreateOverlay();
        if (overlay == null)
            yield break;

        yield return overlay.Play();
        Destroy(overlay.gameObject);
        yield return null;
    }

    static BattleTimeUpOverlay CreateOverlay()
    {
        GameObject prefab = Resources.Load<GameObject>(PrefabResourcesPath);
        if (prefab == null)
            return null;

        Transform parent = ResolveParent();
        GameObject instance = Instantiate(prefab, parent, false);
        instance.name = "BattleTimeUpOverlay";
        instance.transform.SetAsFirstSibling();
        return instance.GetComponent<BattleTimeUpOverlay>();
    }

    IEnumerator Play()
    {
        rootRect = (RectTransform)transform;
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Image image = GetComponent<Image>();
        if (image == null)
            image = gameObject.AddComponent<Image>();

        image.sprite = LoadTimeUpSprite();
        image.color = Color.white;
        image.preserveAspect = false;
        image.raycastTarget = false;

        float uiScale = GetUiScale(image.canvas);
        Vector2 target = new(0f, -TopHudHeight * 0.5f * uiScale);
        Vector2 start = new(0f, ScreenHeight * 0.5f * uiScale);

        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(
            Mathf.Round(DisplayWidth * uiScale),
            Mathf.Round(DisplayHeight * uiScale));
        rootRect.localScale = Vector3.one;
        rootRect.anchoredPosition = RoundToPixel(start);

        audioIsolation = BattleOverlayAudioIsolation.Begin(gameObject);
        audioIsolation?.Play(timeUpMusic, timeUpMusicVolume);

        float elapsed = 0f;
        while (elapsed < DropDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / DropDuration);
            rootRect.anchoredPosition = RoundToPixel(
                Vector2.Lerp(start, target, SmoothStep(progress)));
            yield return null;
        }

        rootRect.anchoredPosition = RoundToPixel(target);

        float holdDuration = Mathf.Max(0f, TotalDuration - DropDuration);
        if (holdDuration > 0f)
            yield return new WaitForSecondsRealtime(holdDuration);

        audioIsolation?.Stop();
    }

    static Sprite LoadTimeUpSprite()
    {
        Sprite sprite = Resources.Load<Sprite>(SpriteResourcesPath);
        if (sprite != null)
            return sprite;

        Sprite[] sprites = Resources.LoadAll<Sprite>(SpriteResourcesPath);
        return sprites != null && sprites.Length > 0 ? sprites[0] : null;
    }

    static Transform ResolveParent()
    {
        Canvas canvas = StageIntroTransition.Instance != null &&
                        StageIntroTransition.Instance.fadeImage != null
            ? StageIntroTransition.Instance.fadeImage.canvas
            : FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new(
                "BattleTimeUpCanvas",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
        }

        UICameraViewportFitter fitter =
            canvas.GetComponentInChildren<UICameraViewportFitter>(true);
        if (fitter != null)
            return fitter.transform;

        Transform safeFrame = canvas.transform.Find(SafeFrameName);
        return safeFrame != null ? safeFrame : canvas.transform;
    }

    static float GetUiScale(Canvas canvas)
    {
        float canvasScale = canvas != null ? Mathf.Max(0.01f, canvas.scaleFactor) : 1f;
        Camera camera = Camera.main;
        float width = camera != null ? camera.pixelRect.width : Screen.width;
        float height = camera != null ? camera.pixelRect.height : Screen.height;
        float integerScale = Mathf.Max(1f, Mathf.Round(Mathf.Min(
            width / ScreenWidth,
            height / ScreenHeight)));

        return integerScale / 4f / canvasScale;
    }

    static float SmoothStep(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    static Vector2 RoundToPixel(Vector2 value)
    {
        return new Vector2(Mathf.Round(value.x), Mathf.Round(value.y));
    }
}
