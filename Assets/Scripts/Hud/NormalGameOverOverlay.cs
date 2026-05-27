using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public sealed class NormalGameOverOverlay : MonoBehaviour
{
    const float ScreenWidth = 256f;
    const float ScreenHeight = 224f;
    const string PrefabResourcesPath = "HUD/GameOver/NormalGameOverOverlay";
    const string WorldMapSceneName = "WorldMap";
    const string TitleScreenSceneName = "TitleScreen";
    const string SafeFrameName = "SafeFrame4x3";

    public static bool IsTransitionActive { get; private set; }

    [Header("Artwork")]
    [SerializeField] Sprite backgroundSprite;
    [SerializeField] Sprite[] seatedBomberSprites;
    [SerializeField] Sprite[] tauntEyeSprites;
    [SerializeField] Sprite[] cursorSprites;

    [Header("Game Over Audio")]
    [SerializeField] AudioClip gameOverMusic;
    [SerializeField, Range(0f, 1f)] float gameOverMusicVolume = 1f;
    [SerializeField] bool loopGameOverMusic;

    [Header("SFX")]
    [SerializeField] AudioClip moveCursorSfx;
    [SerializeField, Range(0f, 1f)] float moveCursorSfxVolume = 1f;
    [SerializeField] AudioClip confirmSfx;
    [SerializeField, Range(0f, 1f)] float confirmSfxVolume = 1f;

    [Header("Timing")]
    [SerializeField, Min(0f)] float waitBeforeFadeInSeconds = 3f;
    [SerializeField, Min(0.01f)] float fadeInSeconds = 1f;
    [SerializeField, Min(0.01f)] float fadeOutSeconds = 1f;
    [SerializeField, Min(0.01f)] float eyeFrameSeconds = 0.13f;
    [SerializeField, Min(0f)] float confirmAnimationSeconds = 0.32f;

    [Header("Layout")]
    [SerializeField] float eyesOffsetY = 17f;
    [SerializeField] Vector2 cursorSize = new(16f, 16f);

    readonly Image[] eyeImages = new Image[8];
    Image cursorImage;
    CanvasGroup canvasGroup;
    bool inputEnabled;
    bool selectionCommitted;
    int selectedOption;
    float eyeFrameTimer;
    int eyeFrameIndex;

    public static IEnumerator PlayAfterDeathFadeRoutine()
    {
        NormalGameOverOverlay overlay = CreateOverlay();
        if (overlay == null)
        {
            IsTransitionActive = false;
            yield break;
        }

        yield return overlay.Play();
    }

    public static void BeginGameOverTransition()
    {
        IsTransitionActive = true;
    }

    static NormalGameOverOverlay CreateOverlay()
    {
        GameObject prefab = Resources.Load<GameObject>(PrefabResourcesPath);
        RectTransform safeFrame = ResolveSafeFrame();
        Transform parent = safeFrame != null ? safeFrame : ResolveCanvasTransform();
        if (parent == null)
            return null;

        GameObject instance = prefab != null
            ? Instantiate(prefab, parent, false)
            : new GameObject("NormalGameOverOverlay", typeof(RectTransform), typeof(CanvasGroup), typeof(NormalGameOverOverlay));

        if (prefab == null)
            instance.transform.SetParent(parent, false);

        instance.name = "NormalGameOverOverlay";

        if (safeFrame != null)
            safeFrame.SetAsLastSibling();

        instance.transform.SetAsLastSibling();

        NormalGameOverOverlay overlay = instance.GetComponent<NormalGameOverOverlay>();
        if (overlay == null)
            overlay = instance.AddComponent<NormalGameOverOverlay>();

        return overlay;
    }

    static RectTransform ResolveSafeFrame()
    {
        Transform canvasTransform = ResolveCanvasTransform();
        if (canvasTransform == null)
            return null;

        Transform direct = canvasTransform.Find(SafeFrameName);
        if (direct is RectTransform existing)
            return existing;

        UICameraViewportFitter[] fitters = canvasTransform.GetComponentsInChildren<UICameraViewportFitter>(true);
        for (int i = 0; i < fitters.Length; i++)
        {
            if (fitters[i] != null && fitters[i].transform is RectTransform rect)
                return rect;
        }

        GameObject go = new(SafeFrameName, typeof(RectTransform), typeof(UICameraViewportFitter));
        RectTransform safeFrame = go.GetComponent<RectTransform>();
        safeFrame.SetParent(canvasTransform, false);
        safeFrame.anchorMin = Vector2.zero;
        safeFrame.anchorMax = Vector2.one;
        safeFrame.anchoredPosition = Vector2.zero;
        safeFrame.sizeDelta = Vector2.zero;
        safeFrame.localScale = Vector3.one;
        return safeFrame;
    }

    static Transform ResolveCanvasTransform()
    {
        if (StageIntroTransition.Instance != null && StageIntroTransition.Instance.fadeImage != null)
            return StageIntroTransition.Instance.fadeImage.canvas.transform;

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas != null)
            return canvas.transform;

        GameObject go = new GameObject("NormalGameOverCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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
        IsTransitionActive = true;
        GamePauseController.ClearPauseFlag();

        canvasGroup = GetComponent<CanvasGroup>();
        RectTransform parentRect = transform.parent as RectTransform;
        ConfigureRoot(parentRect);
        BuildUi();
        HideNormalGameHudInCurrentScene();

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        if (waitBeforeFadeInSeconds > 0f)
            yield return new WaitForSecondsRealtime(waitBeforeFadeInSeconds);

        Time.timeScale = 0f;
        PlayGameOverMusic();

        float elapsed = 0f;
        while (elapsed < fadeInSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInSeconds);
            yield return null;
        }

        canvasGroup.alpha = 1f;
        inputEnabled = true;
    }

    void Update()
    {
        if (!gameObject.activeInHierarchy)
            return;

        TickEyes();

        if (!inputEnabled || selectionCommitted)
            return;

        PlayerInputManager input = PlayerInputManager.Instance;
        if (input == null)
            return;

        if (input.AnyGetDown(PlayerAction.MoveUp) || input.AnyGetDown(PlayerAction.MoveDown))
        {
            selectedOption = 1 - selectedOption;
            PositionCursor();
            PlaySfx(moveCursorSfx, moveCursorSfxVolume);
        }

        if (input.AnyGetDown(PlayerAction.ActionA) || input.AnyGetDown(PlayerAction.Start))
            StartCoroutine(ConfirmSelectionRoutine());
    }

    void ConfigureRoot(RectTransform parentRect)
    {
        float uiScale = GetPixelPerfectUiScale(parentRect);
        RectTransform rect = (RectTransform)transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(ScreenWidth, ScreenHeight);
        rect.localScale = new Vector3(uiScale, uiScale, 1f);
        transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
    }

    void BuildUi()
    {
        CreateImage("Background", transform, backgroundSprite, Vector2.zero, new Vector2(ScreenWidth, ScreenHeight));

        float[] bomberX = { -75f, -25f, 25f, 75f };
        for (int i = 0; i < bomberX.Length; i++)
        {
            Sprite bomber = seatedBomberSprites != null && i < seatedBomberSprites.Length
                ? seatedBomberSprites[i]
                : null;
            Image body = CreateImage($"Bomber_{i + 1}", transform, bomber, new Vector2(bomberX[i], -12f), new Vector2(48f, 60f));

            eyeImages[i * 2] = CreateImage("Eye_Left", body.transform, GetEyeSprite(), new Vector2(-6f, eyesOffsetY), new Vector2(12f, 12f));
            eyeImages[(i * 2) + 1] = CreateImage("Eye_Right", body.transform, GetEyeSprite(), new Vector2(6f, eyesOffsetY), new Vector2(12f, 12f));
        }

        cursorImage = CreateImage("Cursor", transform, GetCursorSprite(), Vector2.zero, cursorSize);
        selectedOption = 0;
        PositionCursor();
    }

    static Image CreateImage(string objectName, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
    {
        GameObject go = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    void TickEyes()
    {
        if (tauntEyeSprites == null || tauntEyeSprites.Length <= 1)
            return;

        eyeFrameTimer += Time.unscaledDeltaTime;
        if (eyeFrameTimer < eyeFrameSeconds)
            return;

        eyeFrameTimer %= eyeFrameSeconds;
        eyeFrameIndex = (eyeFrameIndex + 1) % tauntEyeSprites.Length;
        Sprite sprite = tauntEyeSprites[eyeFrameIndex];

        for (int i = 0; i < eyeImages.Length; i++)
        {
            if (eyeImages[i] != null)
                eyeImages[i].sprite = sprite;
        }
    }

    void PositionCursor()
    {
        if (cursorImage == null)
            return;

        cursorImage.rectTransform.anchoredPosition = selectedOption == 0
            ? new Vector2(-43f, -55f)
            : new Vector2(-43f, -79f);
    }

    Sprite GetEyeSprite()
    {
        return tauntEyeSprites != null && tauntEyeSprites.Length > 0 ? tauntEyeSprites[0] : null;
    }

    Sprite GetCursorSprite()
    {
        return cursorSprites != null && cursorSprites.Length > 0 ? cursorSprites[0] : null;
    }

    IEnumerator ConfirmSelectionRoutine()
    {
        selectionCommitted = true;
        inputEnabled = false;
        PlaySfx(confirmSfx, confirmSfxVolume);

        float frameSeconds = GetConfirmCursorFrameSeconds();
        float transitionElapsed = 0f;
        float totalTransitionSeconds = confirmAnimationSeconds + fadeOutSeconds;
        float cursorElapsed = 0f;
        int cursorFrame = 0;

        while (transitionElapsed < totalTransitionSeconds)
        {
            float deltaTime = Time.unscaledDeltaTime;
            transitionElapsed += deltaTime;
            cursorElapsed += deltaTime;

            while (cursorImage != null &&
                   cursorSprites != null &&
                   cursorSprites.Length > 0 &&
                   cursorElapsed >= frameSeconds)
            {
                cursorElapsed -= frameSeconds;
                cursorFrame = (cursorFrame + 1) % cursorSprites.Length;
                cursorImage.sprite = cursorSprites[cursorFrame];
            }

            float fadeElapsed = Mathf.Max(0f, transitionElapsed - confirmAnimationSeconds);
            canvasGroup.alpha = 1f - Mathf.Clamp01(fadeElapsed / fadeOutSeconds);
            yield return null;
        }

        LoadSelectedScene();
    }

    float GetConfirmCursorFrameSeconds()
    {
        int frameCount = cursorSprites != null ? cursorSprites.Length : 0;
        if (frameCount <= 0)
            return Mathf.Max(0.01f, confirmAnimationSeconds);

        return Mathf.Max(0.01f, confirmAnimationSeconds / frameCount);
    }

    void LoadSelectedScene()
    {
        if (GameMusicController.Instance != null)
            GameMusicController.Instance.StopMusic();

        GamePauseController.ClearPauseFlag();
        Time.timeScale = 1f;
        IsTransitionActive = false;

        if (GameSession.Instance != null)
            GameSession.Instance.ResetNormalGameLivesSession();

        if (selectedOption == 0)
        {
            PlayerPersistentStats.ResetGameplayPersistenceToBaseValues();
            SceneManager.LoadScene(WorldMapSceneName);
            return;
        }

        PlayerPersistentStats.ResetSessionForReturnToTitle();
        TitleScreenSkip.SkipNextIntro = true;
        SceneManager.LoadScene(TitleScreenSceneName);
    }

    static void HideNormalGameHudInCurrentScene()
    {
        HudGridLayout[] hudRoots = FindObjectsByType<HudGridLayout>(FindObjectsInactive.Include);
        for (int i = 0; i < hudRoots.Length; i++)
        {
            HudGridLayout hud = hudRoots[i];
            if (hud == null || hud.GetComponent<BattleModeHud>() != null)
                continue;

            GameObject hudObject = hud.gameObject;
            if (hudObject.activeSelf)
                hudObject.SetActive(false);
        }

        HudLifePreviewLayout[] lifeCounters = FindObjectsByType<HudLifePreviewLayout>(FindObjectsInactive.Include);
        for (int i = 0; i < lifeCounters.Length; i++)
        {
            if (lifeCounters[i] != null && lifeCounters[i].gameObject.activeSelf)
                lifeCounters[i].gameObject.SetActive(false);
        }
    }

    void PlayGameOverMusic()
    {
        if (GameMusicController.Instance == null)
            return;

        GameMusicController.Instance.StopMusic();
        if (gameOverMusic != null)
            GameMusicController.Instance.PlayMusic(gameOverMusic, gameOverMusicVolume, loopGameOverMusic);
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
        return Mathf.Max(1f, Mathf.Round(rawScale));
    }

    static void PlaySfx(AudioClip clip, float volume)
    {
        if (clip != null && GameMusicController.Instance != null)
            GameMusicController.Instance.PlaySfx(clip, volume);
    }
}
