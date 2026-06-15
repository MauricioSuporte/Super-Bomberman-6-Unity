using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public sealed class BattleDrawOverlay : MonoBehaviour
{
    const float ScreenWidth = 256f;
    const float ScreenHeight = 224f;
    const float FadeInDuration = 0.5f;
    const float LetterDropDuration = 0.25f;
    const float LetterSpacing = 3f;
    const float LetterTargetYOffset = 5f;
    const float HoldAfterLetters = 3f;
    const string PrefabResourcesPath = "HUD/Draw/BattleDrawOverlay";
    const string BackgroundResourcesPath = "HUD/Draw/DrawBackground";
    const string SafeFrameName = "SafeFrame4x3";
    static readonly string[] LetterResourcesPaths =
    {
        "HUD/Draw/D",
        "HUD/Draw/R",
        "HUD/Draw/A",
        "HUD/Draw/W"
    };

    static BattleDrawOverlay activeOverlay;

    [Header("Draw Audio")]
    [SerializeField] private AudioClip drawMusic;
    [SerializeField, Range(0f, 1f)] private float drawMusicVolume = 1f;

    readonly List<BattleModeHudState> hiddenBattleHuds = new();
    readonly List<RectTransform> letterRects = new();
    readonly List<Vector2> letterTargets = new();

    RectTransform rootRect;
    RectTransform runtimeRoot;
    RectTransform blackBackdrop;
    CanvasGroup canvasGroup;
    BattleOverlayAudioIsolation audioIsolation;
    Sprite backgroundSprite;
    Sprite[] letterSprites;
    bool skipRequested;

    struct BattleModeHudState
    {
        public GameObject GameObject;
        public bool WasActive;
    }

    public static IEnumerator PlayRoutine()
    {
        BattleDrawOverlay overlay = CreateOverlay();
        if (overlay == null)
            yield break;

        activeOverlay = overlay;
        yield return overlay.Play();
    }

    static BattleDrawOverlay CreateOverlay()
    {
        GameObject prefab = Resources.Load<GameObject>(PrefabResourcesPath);
        RectTransform safeFrame = ResolveSafeFrame();
        Transform parent = safeFrame != null ? safeFrame : ResolveCanvasTransform();

        GameObject instance = prefab != null
            ? Instantiate(prefab, parent, false)
            : new GameObject("BattleDrawOverlay", typeof(RectTransform), typeof(CanvasGroup), typeof(BattleDrawOverlay));

        if (prefab == null)
            instance.transform.SetParent(parent, false);

        instance.name = "BattleDrawOverlay";

        if (safeFrame != null)
            safeFrame.SetAsLastSibling();

        instance.transform.SetAsLastSibling();

        BattleDrawOverlay overlay = instance.GetComponent<BattleDrawOverlay>();
        if (overlay == null)
            overlay = instance.AddComponent<BattleDrawOverlay>();

        if (instance.GetComponent<CanvasGroup>() == null)
            instance.AddComponent<CanvasGroup>();

        overlay.blackBackdrop = CreateBlackBackdrop(parent, instance.transform, instance.layer);
        return overlay;
    }

    static RectTransform CreateBlackBackdrop(Transform parent, Transform overlayTransform, int layer)
    {
        GameObject go = new GameObject("BattleDrawBlackBackdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = layer;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        Image image = go.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        rect.SetAsLastSibling();
        overlayTransform.SetAsLastSibling();
        return rect;
    }

    static RectTransform ResolveSafeFrame()
    {
        Transform canvasTransform = ResolveCanvasTransform();
        if (canvasTransform == null)
            return null;

        RectTransform existing = FindSafeFrame(canvasTransform);
        if (existing != null)
            return existing;

        GameObject go = new GameObject(SafeFrameName, typeof(RectTransform), typeof(UICameraViewportFitter));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(canvasTransform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
        return rect;
    }

    static RectTransform FindSafeFrame(Transform canvasTransform)
    {
        if (canvasTransform == null)
            return null;

        Transform direct = canvasTransform.Find(SafeFrameName);
        if (direct is RectTransform directRect)
            return directRect;

        UICameraViewportFitter[] fitters = canvasTransform.GetComponentsInChildren<UICameraViewportFitter>(true);
        for (int i = 0; i < fitters.Length; i++)
        {
            if (fitters[i] != null && fitters[i].transform is RectTransform rect)
                return rect;
        }

        return null;
    }

    static Transform ResolveCanvasTransform()
    {
        if (StageIntroTransition.Instance != null && StageIntroTransition.Instance.fadeImage != null)
            return StageIntroTransition.Instance.fadeImage.canvas.transform;

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas != null)
            return canvas.transform;

        GameObject go = new GameObject("BattleDrawOverlayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas createdCanvas = go.GetComponent<Canvas>();
        createdCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        createdCanvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ScreenWidth, ScreenHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 16f;

        return go.transform;
    }

    IEnumerator Play()
    {
        rootRect = (RectTransform)transform;
        canvasGroup = GetComponent<CanvasGroup>();
        RectTransform parentRect = transform.parent as RectTransform;

        audioIsolation = BattleOverlayAudioIsolation.Begin(gameObject);
        HideBattleModeHud();
        EnsureSpritesLoaded();
        BuildUi();
        ConfigureRoot(parentRect);
        PlayDrawMusic();

        yield return FadeCanvas(0f, 1f, FadeInDuration);
        if (skipRequested)
            yield break;

        for (int i = 0; i < letterRects.Count; i++)
        {
            yield return DropLetter(i);
            if (skipRequested)
                yield break;
        }

        yield return WaitRealtimeOrSkip(HoldAfterLetters);
        StopOverlayAudio();
    }

    void EnsureSpritesLoaded()
    {
        backgroundSprite = LoadFirstSprite(BackgroundResourcesPath);
        letterSprites = new Sprite[LetterResourcesPaths.Length];

        for (int i = 0; i < LetterResourcesPaths.Length; i++)
            letterSprites[i] = LoadFirstSprite(LetterResourcesPaths[i]);
    }

    static Sprite LoadFirstSprite(string resourcesPath)
    {
        Sprite sprite = Resources.Load<Sprite>(resourcesPath);
        if (sprite != null)
            return sprite;

        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcesPath);
        return sprites != null && sprites.Length > 0 ? sprites[0] : null;
    }

    void BuildUi()
    {
        EnsureRuntimeRoot();
        ClearRuntimeChildren();
        letterRects.Clear();
        letterTargets.Clear();

        Image background = CreateImage("DrawBackground", backgroundSprite);
        ApplyLogicalRect(background.rectTransform, 0f, 0f, ScreenWidth, ScreenHeight, ScreenWidth, ScreenHeight);

        BuildLetters();
    }

    void EnsureRuntimeRoot()
    {
        Transform existing = transform.Find("__BattleDrawRuntime");
        runtimeRoot = existing as RectTransform;
        if (runtimeRoot == null)
        {
            GameObject go = new GameObject("__BattleDrawRuntime", typeof(RectTransform));
            runtimeRoot = go.GetComponent<RectTransform>();
            runtimeRoot.SetParent(transform, false);
        }

        ApplyLogicalRect(runtimeRoot, 0f, 0f, ScreenWidth, ScreenHeight, ScreenWidth, ScreenHeight);
    }

    void ClearRuntimeChildren()
    {
        if (runtimeRoot == null)
            return;

        for (int i = runtimeRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = runtimeRoot.GetChild(i);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    void BuildLetters()
    {
        float totalWidth = 0f;
        float maxHeight = 0f;

        for (int i = 0; i < letterSprites.Length; i++)
        {
            Vector2 size = GetSpriteSize(letterSprites[i]);
            totalWidth += size.x;
            maxHeight = Mathf.Max(maxHeight, size.y);
        }

        totalWidth += LetterSpacing * Mathf.Max(0, letterSprites.Length - 1);
        float left = Mathf.Round((ScreenWidth - totalWidth) * 0.5f);
        float centerY = Mathf.Round(ScreenHeight * 0.55f) + LetterTargetYOffset;

        for (int i = 0; i < letterSprites.Length; i++)
        {
            Sprite sprite = letterSprites[i];
            Vector2 size = GetSpriteSize(sprite);
            Image image = CreateImage("DrawLetter" + (i + 1), sprite);
            RectTransform rect = image.rectTransform;
            float targetX = left + (size.x * 0.5f) - (ScreenWidth * 0.5f);
            float targetY = centerY - (ScreenHeight * 0.5f);
            float hiddenY = (ScreenHeight * 0.5f) + maxHeight + size.y;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(targetX, hiddenY);
            rect.localScale = Vector3.one;

            letterRects.Add(rect);
            letterTargets.Add(new Vector2(targetX, targetY));

            left += size.x + LetterSpacing;
        }
    }

    IEnumerator DropLetter(int letterIndex)
    {
        if (letterIndex < 0 || letterIndex >= letterRects.Count || letterIndex >= letterTargets.Count)
            yield break;

        RectTransform rect = letterRects[letterIndex];
        Vector2 from = rect.anchoredPosition;
        Vector2 to = letterTargets[letterIndex];

        float t = 0f;
        while (t < LetterDropDuration)
        {
            if (TrySkipOverlay())
                yield break;

            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / LetterDropDuration);
            rect.anchoredPosition = Vector2.Lerp(from, to, SmoothStep(p));
            yield return null;
        }

        rect.anchoredPosition = to;
    }

    void ConfigureRoot(RectTransform parentRect)
    {
        float uiScale = GetPixelPerfectUiScale(parentRect);
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.sizeDelta = new Vector2(ScreenWidth, ScreenHeight);
        rootRect.localScale = new Vector3(uiScale, uiScale, 1f);

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
    }

    IEnumerator FadeCanvas(float from, float to, float duration)
    {
        if (canvasGroup == null)
            yield break;

        if (duration <= 0f)
        {
            canvasGroup.alpha = to;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            if (TrySkipOverlay())
                yield break;

            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            canvasGroup.alpha = Mathf.Lerp(from, to, SmoothStep(p));
            yield return null;
        }

        canvasGroup.alpha = to;
    }

    IEnumerator WaitRealtimeOrSkip(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (TrySkipOverlay())
                yield break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    bool TrySkipOverlay()
    {
        if (!WasSkipPressed())
            return false;

        skipRequested = true;
        StopOverlayAudio();
        return true;
    }

    static bool WasSkipPressed()
    {
        PlayerInputManager input = PlayerInputManager.Instance;
        return input != null &&
               (input.AnyGetDown(PlayerAction.ActionA) || input.AnyGetDown(PlayerAction.Start));
    }

    void StopOverlayAudio()
    {
        if (audioIsolation != null)
            audioIsolation.Stop();
    }

    void PlayDrawMusic()
    {
        if (audioIsolation != null)
            audioIsolation.Play(drawMusic, drawMusicVolume);
    }

    void HideBattleModeHud()
    {
        hiddenBattleHuds.Clear();

        BattleModeHud[] huds = FindObjectsByType<BattleModeHud>(FindObjectsInactive.Include);
        for (int i = 0; i < huds.Length; i++)
        {
            BattleModeHud hud = huds[i];
            if (hud == null)
                continue;

            GameObject hudObject = hud.gameObject;
            hiddenBattleHuds.Add(new BattleModeHudState
            {
                GameObject = hudObject,
                WasActive = hudObject.activeSelf
            });

            if (hudObject.activeSelf)
                hudObject.SetActive(false);
        }
    }

    public static void DestroyActiveOverlay()
    {
        if (activeOverlay == null)
            return;

        Destroy(activeOverlay.gameObject);
        activeOverlay = null;
    }

    void OnDestroy()
    {
        if (blackBackdrop != null)
            Destroy(blackBackdrop.gameObject);

        if (activeOverlay == this)
            activeOverlay = null;
    }

    Image CreateImage(string childName, Sprite sprite)
    {
        GameObject go = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(runtimeRoot, false);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;
        image.sprite = sprite;
        image.preserveAspect = false;
        image.color = Color.white;
        return image;
    }

    static Vector2 GetSpriteSize(Sprite sprite)
    {
        if (sprite == null)
            return new Vector2(32f, 64f);

        return new Vector2(Mathf.Round(sprite.rect.width), Mathf.Round(sprite.rect.height));
    }

    static float SmoothStep(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - (2f * value));
    }

    static void ApplyLogicalRect(
        RectTransform rect,
        float left,
        float bottom,
        float width,
        float height,
        float logicalParentWidth,
        float logicalParentHeight)
    {
        if (rect == null || logicalParentWidth <= 0f || logicalParentHeight <= 0f)
            return;

        rect.anchorMin = new Vector2(left / logicalParentWidth, bottom / logicalParentHeight);
        rect.anchorMax = new Vector2((left + width) / logicalParentWidth, (bottom + height) / logicalParentHeight);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    static float GetPixelPerfectUiScale(RectTransform parentRect)
    {
        float parentWidth = parentRect != null ? parentRect.rect.width : 0f;
        float parentHeight = parentRect != null ? parentRect.rect.height : 0f;

        if (parentWidth <= 0f || parentHeight <= 0f)
        {
            Rect cameraPixelRect = Camera.main != null
                ? Camera.main.pixelRect
                : new Rect(0f, 0f, Screen.width, Screen.height);

            parentWidth = cameraPixelRect.width;
            parentHeight = cameraPixelRect.height;
        }

        float rawScale = Mathf.Min(parentWidth / ScreenWidth, parentHeight / ScreenHeight);
        float integerScale = Mathf.Max(1f, Mathf.Round(rawScale));
        return integerScale;
    }
}
