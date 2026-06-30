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
    const string SaveFileMenuSceneName = "SaveFileMenu";
    const string SafeFrameName = "SafeFrame4x3";

    public static bool IsTransitionActive { get; private set; }
    static bool autoConfirmEndOnShow;
    static bool returnToSaveFileMenuOnEnd;

    [Header("Artwork")]
    [SerializeField] Sprite backgroundSprite;
    [SerializeField] Sprite[] cursorSprites;
    [SerializeField] string characterSpriteResourcesPath = "HUD/GameOver/Characters";

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
    [SerializeField, Min(0.01f)] float characterFrameSeconds = 0.13f;
    [SerializeField, Min(0f)] float confirmAnimationSeconds = 0.32f;

    [Header("Continue Animation")]
    [SerializeField, Min(0f)] float continueExitDistance = 180f;

    [Header("End Animation")]
    [SerializeField, Min(0.01f)] float endFallSeconds = 2.2f;
    [SerializeField, Min(0f)] float endFallQueueDelaySeconds = 0.08f;
    [SerializeField] Vector2 endBlackHolePosition = Vector2.zero;
    [SerializeField, Min(0f)] float endFallOrbitRadius = 64f;
    [SerializeField] float endFallOrbitDegrees = 540f;
    [SerializeField] float endFallSpriteRotationDegrees = 720f;
    [SerializeField, Range(0f, 1f)] float endFallFinalScale;

    [Header("Layout")]
    [SerializeField] Vector2 cursorSize = new(16f, 16f);

    readonly Image[] bomberImages = new Image[4];
    readonly Sprite[][] sufferingFrames = new Sprite[4][];
    readonly Sprite[][] gameOverFrames = new Sprite[4][];
    readonly Sprite[][] continueFrames = new Sprite[4][];
    Image cursorImage;
    CanvasGroup canvasGroup;
    RectMask2D viewportMask;
    bool inputEnabled;
    bool selectionCommitted;
    int selectedOption;
    float characterFrameTimer;
    int characterFrameIndex;
    bool confirmCursorAnimating;
    float confirmCursorTimer;
    int confirmCursorFrameIndex;

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

    public static void BeginGameOverTransition(bool forceEndSelection = false)
    {
        IsTransitionActive = true;
        autoConfirmEndOnShow = forceEndSelection;
        returnToSaveFileMenuOnEnd = forceEndSelection;
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
        bool shouldAutoConfirmEnd = autoConfirmEndOnShow;
        autoConfirmEndOnShow = false;

        canvasGroup = GetComponent<CanvasGroup>();
        RectTransform parentRect = transform.parent as RectTransform;
        ConfigureRoot(parentRect);
        BuildUi();
        if (shouldAutoConfirmEnd)
        {
            selectedOption = 1;
            RestartCharacterLoop();
            PositionCursor();
        }
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
        if (shouldAutoConfirmEnd)
        {
            yield return ConfirmSelectionRoutine();
            yield break;
        }

        inputEnabled = true;
    }

    void Update()
    {
        if (!gameObject.activeInHierarchy)
            return;

        TickCharacterAnimation();
        TickConfirmCursor();

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
        EnsureViewportMask();
        transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
    }

    void EnsureViewportMask()
    {
        if (viewportMask == null)
            viewportMask = GetComponent<RectMask2D>();

        if (viewportMask == null)
            viewportMask = gameObject.AddComponent<RectMask2D>();

        viewportMask.enabled = true;
    }

    void BuildUi()
    {
        LoadCharacterSprites();
        CreateImage("Background", transform, backgroundSprite, Vector2.zero, new Vector2(ScreenWidth, ScreenHeight));

        float[] bomberX = { -96f, -32f, 32f, 96f };
        for (int i = 0; i < bomberX.Length; i++)
        {
            Sprite bomber = GetFrame(sufferingFrames, i, 0);
            Image body = CreateImage($"Bomber_{i + 1}", transform, bomber, new Vector2(bomberX[i], -5f), new Vector2(96f, 120f));
            bomberImages[i] = body;
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

    void LoadCharacterSprites()
    {
        for (int playerIndex = 0; playerIndex < bomberImages.Length; playerIndex++)
        {
            string playerPath = $"{characterSpriteResourcesPath}/Player{playerIndex + 1}";
            sufferingFrames[playerIndex] = LoadNumberedSprites(playerPath, "dor_e_sofrimento", 9);
            gameOverFrames[playerIndex] = LoadNumberedSprites(playerPath, "AAAAAAAAAAAAAAAAAAAAAAAAAAAA", 5);
            continueFrames[playerIndex] = LoadNumberedSprites(playerPath, "morri_n_man", 12);
        }
    }

    static Sprite[] LoadNumberedSprites(string path, string prefix, int frameCount)
    {
        Sprite[] frames = new Sprite[frameCount];
        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            frames[frameIndex] = Resources.Load<Sprite>($"{path}/{prefix}{frameIndex + 1}");
        return frames;
    }

    void TickCharacterAnimation()
    {
        if (selectionCommitted)
            return;

        Sprite[][] activeFrames = sufferingFrames;
        int frameCount = activeFrames[0] != null ? activeFrames[0].Length : 0;
        if (frameCount <= 1)
            return;

        characterFrameTimer += Time.unscaledDeltaTime;
        while (characterFrameTimer >= characterFrameSeconds)
        {
            characterFrameTimer -= characterFrameSeconds;
            characterFrameIndex = (characterFrameIndex + 1) % frameCount;
            SetBomberFrame(activeFrames, characterFrameIndex);
        }
    }

    void RestartCharacterLoop()
    {
        characterFrameTimer = 0f;
        characterFrameIndex = 0;
        SetBomberFrame(sufferingFrames, 0);
    }

    void TickConfirmCursor()
    {
        if (!confirmCursorAnimating || cursorImage == null || cursorSprites == null || cursorSprites.Length <= 1)
            return;

        float frameSeconds = GetConfirmCursorFrameSeconds();
        confirmCursorTimer += Time.unscaledDeltaTime;
        while (confirmCursorTimer >= frameSeconds)
        {
            confirmCursorTimer -= frameSeconds;
            confirmCursorFrameIndex = (confirmCursorFrameIndex + 1) % cursorSprites.Length;
            cursorImage.sprite = cursorSprites[confirmCursorFrameIndex];
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

    Sprite GetCursorSprite()
    {
        return cursorSprites != null && cursorSprites.Length > 0 ? cursorSprites[0] : null;
    }

    IEnumerator ConfirmSelectionRoutine()
    {
        selectionCommitted = true;
        inputEnabled = false;
        PlaySfx(confirmSfx, confirmSfxVolume);
        BeginConfirmCursorLoop();

        if (selectedOption == 0)
            yield return PlayContinueSelectionRoutine();
        else
            yield return PlayEndSelectionRoutine();

        yield return FadeOutRoutine();
        LoadSelectedScene();
    }

    IEnumerator PlayContinueSelectionRoutine()
    {
        float continueFrameSeconds = Mathf.Max(0.01f, characterFrameSeconds * 0.5f);
        Vector2[] startingPositions = new Vector2[bomberImages.Length];
        for (int i = 0; i < bomberImages.Length; i++)
        {
            if (bomberImages[i] != null)
                startingPositions[i] = bomberImages[i].rectTransform.anchoredPosition;
        }

        for (int frameIndex = 0; frameIndex < continueFrames[0].Length; frameIndex++)
        {
            SetBomberFrame(continueFrames, frameIndex);
            float frameElapsed = 0f;
            while (frameElapsed < continueFrameSeconds)
            {
                frameElapsed += Time.unscaledDeltaTime;
                if (frameIndex >= 4)
                {
                    float exitProgress = Mathf.Clamp01((frameIndex - 4f + (frameElapsed / continueFrameSeconds)) / 8f);
                    Vector2 offset = Vector2.up * (continueExitDistance * exitProgress);

                    for (int i = 0; i < bomberImages.Length; i++)
                    {
                        if (bomberImages[i] != null)
                            bomberImages[i].rectTransform.anchoredPosition = startingPositions[i] + offset;
                    }
                }

                yield return null;
            }
        }
    }

    IEnumerator PlayEndSelectionRoutine()
    {
        SetBomberFrame(gameOverFrames, 0);

        Vector2[] originalOffsets = new Vector2[bomberImages.Length];
        Vector3[] startingScales = new Vector3[bomberImages.Length];
        Quaternion[] startingRotations = new Quaternion[bomberImages.Length];
        for (int i = 0; i < bomberImages.Length; i++)
        {
            if (bomberImages[i] == null)
                continue;

            RectTransform rect = bomberImages[i].rectTransform;
            originalOffsets[i] = rect.anchoredPosition - endBlackHolePosition;
            startingScales[i] = rect.localScale;
            startingRotations[i] = rect.localRotation;
        }

        float duration = Mathf.Max(0.01f, endFallSeconds);
        float queueDelay = Mathf.Max(0f, endFallQueueDelaySeconds);
        float elapsed = 0f;
        float totalDuration = duration + (queueDelay * (bomberImages.Length - 1));
        int lastAnimationFrame = -1;
        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            int animationFrame = Mathf.FloorToInt(elapsed / characterFrameSeconds) % gameOverFrames[0].Length;
            if (animationFrame != lastAnimationFrame)
            {
                SetBomberFrame(gameOverFrames, animationFrame);
                lastAnimationFrame = animationFrame;
            }

            for (int i = 0; i < bomberImages.Length; i++)
            {
                if (bomberImages[i] == null)
                    continue;

                float localElapsed = elapsed - (queueDelay * i);
                if (localElapsed < 0f)
                    continue;

                RectTransform rect = bomberImages[i].rectTransform;
                float progress = Mathf.Clamp01(localElapsed / duration);
                float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
                float remainingRadius = 1f - easedProgress;
                float orbitAngle = -endFallOrbitDegrees * easedProgress;
                Vector2 spiralOffset = GetEndFallSpiralOffset(originalOffsets[i], orbitAngle, remainingRadius, easedProgress);
                rect.localScale = Vector3.Lerp(startingScales[i], startingScales[i] * endFallFinalScale, easedProgress);
                rect.localRotation = startingRotations[i] * Quaternion.Euler(0f, 0f, -endFallSpriteRotationDegrees * easedProgress);
                rect.anchoredPosition = endBlackHolePosition + spiralOffset;
            }

            yield return null;
        }
    }

    Vector2 GetEndFallSpiralOffset(Vector2 originalOffset, float orbitAngle, float remainingRadius, float progress)
    {
        float originalRadius = originalOffset.magnitude;
        Vector2 direction = originalRadius > 0.01f ? originalOffset / originalRadius : Vector2.left;
        float enterOrbitProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress * 4f));
        float currentRadius = Mathf.Lerp(originalRadius, endFallOrbitRadius, enterOrbitProgress);
        return RotateVector(direction * currentRadius, orbitAngle) * remainingRadius;
    }

    static Vector2 RotateVector(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sine = Mathf.Sin(radians);
        float cosine = Mathf.Cos(radians);
        return new Vector2(
            (vector.x * cosine) - (vector.y * sine),
            (vector.x * sine) + (vector.y * cosine));
    }

    IEnumerator FadeOutRoutine()
    {
        float elapsed = 0f;
        while (elapsed < fadeOutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeOutSeconds);
            yield return null;
        }

        canvasGroup.alpha = 0f;
    }

    void BeginConfirmCursorLoop()
    {
        confirmCursorAnimating = true;
        confirmCursorTimer = 0f;
        confirmCursorFrameIndex = 0;
        if (cursorImage != null)
            cursorImage.sprite = GetCursorSprite();
    }

    void SetBomberFrame(Sprite[][] framesByPlayer, int frameIndex)
    {
        for (int playerIndex = 0; playerIndex < bomberImages.Length; playerIndex++)
        {
            Sprite sprite = GetFrame(framesByPlayer, playerIndex, frameIndex);
            if (bomberImages[playerIndex] != null && sprite != null)
                bomberImages[playerIndex].sprite = sprite;
        }
    }

    static Sprite GetFrame(Sprite[][] framesByPlayer, int playerIndex, int frameIndex)
    {
        if (framesByPlayer == null || playerIndex < 0 || playerIndex >= framesByPlayer.Length)
            return null;
        Sprite[] frames = framesByPlayer[playerIndex];
        return frames != null && frameIndex >= 0 && frameIndex < frames.Length ? frames[frameIndex] : null;
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
            returnToSaveFileMenuOnEnd = false;
            PlayerPersistentStats.ResetGameplayPersistenceToBaseValues();
            SceneManager.LoadScene(WorldMapSceneName);
            return;
        }

        PlayerPersistentStats.ResetSessionForReturnToTitle();
        if (returnToSaveFileMenuOnEnd)
        {
            returnToSaveFileMenuOnEnd = false;
            SaveFileMenu.SelectNewGameOnNextOpen = true;
            SceneManager.LoadScene(SaveFileMenuSceneName);
            return;
        }

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
