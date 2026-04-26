using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BattleRoundWinScoreboardOverlay : MonoBehaviour
{
    const float ScreenWidth = 256f;
    const float ScreenHeight = 224f;
    const float SlideDuration = 0.5f;
    const float TrophyRevealDelay = 1f;
    const float HoldAfterReveal = 2f;
    const string PrefabResourcesPath = "HUD/RoundWin/RoundWinScoreboard";
    const string MatchEndJingleResourcesPath = "Sounds/Match End Jingle";
    const string SafeFrameName = "SafeFrame4x3";
    const string LogPrefix = "[RoundWinScoreboard]";
    static BattleRoundWinScoreboardOverlay activeOverlay;
    static AudioClip matchEndJingleClip;

    readonly List<int> activePlayerIds = new(GameSession.MaxPlayerId);
    readonly List<BattleModeHudState> hiddenBattleHuds = new();

    struct BattleModeHudState
    {
        public GameObject GameObject;
        public bool WasActive;
    }

    public static IEnumerator PlayRoutine(int winnerPlayerId)
    {
        BattleRoundWinScoreboardOverlay overlay = CreateOverlay();
        if (overlay == null)
            yield break;

        activeOverlay = overlay;
        yield return overlay.Play(winnerPlayerId);
    }

    static BattleRoundWinScoreboardOverlay CreateOverlay()
    {
        GameObject prefab = Resources.Load<GameObject>(PrefabResourcesPath);
        RectTransform safeFrame = ResolveSafeFrame();
        Transform parent = safeFrame != null ? safeFrame : ResolveCanvasTransform();
        Log("CreateOverlay", safeFrame, null);

        BattleRoundWinScoreboardPresenter presenter = null;
        if (prefab != null)
        {
            GameObject instance = Instantiate(prefab, parent, false);
            presenter = instance.GetComponent<BattleRoundWinScoreboardPresenter>();
        }
        else
        {
            GameObject fallback = new GameObject("RoundWinScoreboard", typeof(RectTransform), typeof(CanvasGroup), typeof(BattleRoundWinScoreboardPresenter));
            fallback.transform.SetParent(parent, false);
            presenter = fallback.GetComponent<BattleRoundWinScoreboardPresenter>();
        }

        if (presenter == null)
            return null;

        presenter.gameObject.name = "RoundWinScoreboard";
        if (safeFrame != null)
            safeFrame.SetAsLastSibling();

        presenter.transform.SetAsLastSibling();

        BattleRoundWinScoreboardOverlay overlay = presenter.GetComponent<BattleRoundWinScoreboardOverlay>();
        if (overlay == null)
            overlay = presenter.gameObject.AddComponent<BattleRoundWinScoreboardOverlay>();

        Log("OverlayCreated", safeFrame, presenter.transform as RectTransform);
        return overlay;
    }

    static RectTransform ResolveSafeFrame()
    {
        Transform canvasTransform = ResolveCanvasTransform();
        if (canvasTransform == null)
            return null;

        RectTransform existing = FindSafeFrame(canvasTransform);
        if (existing != null)
        {
            Log("SafeFrameFound", existing, null);
            return existing;
        }

        GameObject go = new GameObject(SafeFrameName, typeof(RectTransform), typeof(UICameraViewportFitter));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(canvasTransform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
        Log("SafeFrameCreated", rect, null);
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

        GameObject go = new GameObject("RoundWinScoreboardCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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

    IEnumerator Play(int winnerPlayerId)
    {
        BattleRoundWinScoreboardPresenter presenter = GetComponent<BattleRoundWinScoreboardPresenter>();
        RectTransform rect = transform as RectTransform;
        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        RectTransform parentRect = transform.parent as RectTransform;

        if (presenter == null || rect == null)
            yield break;

        HideBattleModeHud();
        PopulateActivePlayerIds();

        bool usesTeams = BattleModeRules.Instance != null && BattleModeRules.Instance.UsesTeams;
        int targetVictories = BattleModeRules.Instance != null
            ? BattleModeRules.Instance.VictoriesToWinMatch
            : 3;

        presenter.Configure(activePlayerIds, winnerPlayerId, usesTeams, targetVictories);

        Canvas.ForceUpdateCanvases();

        float uiScale = GetPixelPerfectUiScale(parentRect);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(ScreenWidth, ScreenHeight);
        rect.localScale = new Vector3(uiScale, uiScale, 1f);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();

        float slideDistance = GetSlideDistance(parentRect, rect);
        Vector2 hiddenPosition = new(0f, -slideDistance);
        rect.anchoredPosition = hiddenPosition;
        Log(
            "PlayStart winner=" + winnerPlayerId +
            " players=" + activePlayerIds.Count +
            " teams=" + usesTeams +
            " target=" + targetVictories +
            " uiScale=" + uiScale.ToString("0.###") +
            " slideDistance=" + slideDistance.ToString("0.##"),
            parentRect,
            rect);

        float t = 0f;
        while (t < SlideDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / SlideDuration);
            rect.anchoredPosition = Vector2.Lerp(hiddenPosition, Vector2.zero, SmoothStep(p));
            yield return null;
        }

        rect.anchoredPosition = Vector2.zero;
        Log("SlideComplete elapsed=" + SlideDuration.ToString("0.###"), parentRect, rect);
        PlayMatchEndJingle();

        yield return new WaitForSecondsRealtime(TrophyRevealDelay);
        presenter.RevealRoundWinTrophies();
        Log("TrophyReveal delay=" + TrophyRevealDelay.ToString("0.###"), parentRect, rect);

        yield return new WaitForSecondsRealtime(HoldAfterReveal);
        Log("HoldComplete hold=" + HoldAfterReveal.ToString("0.###"), parentRect, rect);
    }

    void PopulateActivePlayerIds()
    {
        activePlayerIds.Clear();

        if (GameSession.Instance != null)
            GameSession.Instance.GetActivePlayerIds(activePlayerIds);

        if (activePlayerIds.Count <= 0)
            activePlayerIds.Add(GameSession.MinPlayerId);
    }

    static float SmoothStep(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - (2f * value));
    }

    void PlayMatchEndJingle()
    {
        if (matchEndJingleClip == null)
            matchEndJingleClip = Resources.Load<AudioClip>(MatchEndJingleResourcesPath);

        if (matchEndJingleClip == null)
        {
            Debug.LogWarning(LogPrefix + " MatchEndJingleMissing path=" + MatchEndJingleResourcesPath);
            return;
        }

        AudioSource audioSource = FindAnyObjectByType<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.PlayOneShot(matchEndJingleClip);
        Debug.Log(LogPrefix + " t=" + Time.unscaledTime.ToString("0.###") + " MatchEndJinglePlay path=" + MatchEndJingleResourcesPath);
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

        Log("BattleHudHidden count=" + hiddenBattleHuds.Count, transform.parent as RectTransform, transform as RectTransform);
    }

    void RestoreBattleModeHud()
    {
        for (int i = 0; i < hiddenBattleHuds.Count; i++)
        {
            BattleModeHudState state = hiddenBattleHuds[i];
            if (state.GameObject != null && state.GameObject.scene.isLoaded)
                state.GameObject.SetActive(state.WasActive);
        }

        Log("BattleHudRestored count=" + hiddenBattleHuds.Count, transform.parent as RectTransform, transform as RectTransform);
        hiddenBattleHuds.Clear();
    }

    public static void DestroyActiveOverlay()
    {
        if (activeOverlay == null)
            return;

        Destroy(activeOverlay.gameObject);
        activeOverlay = null;
    }

    static float GetSlideDistance(RectTransform parentRect, RectTransform ownRect)
    {
        float parentHeight = parentRect != null ? parentRect.rect.height : 0f;
        float ownHeight = ownRect != null ? ownRect.rect.height * Mathf.Abs(ownRect.localScale.y) : 0f;
        float distance = Mathf.Max(parentHeight, ownHeight, ScreenHeight);
        return Mathf.Ceil(distance);
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

    static void Log(string message, RectTransform parentRect, RectTransform ownRect)
    {
        Rect cameraPixelRect = Camera.main != null
            ? Camera.main.pixelRect
            : new Rect(0f, 0f, Screen.width, Screen.height);
        Rect cameraViewportRect = Camera.main != null
            ? Camera.main.rect
            : new Rect(0f, 0f, 1f, 1f);

        string parentSize = parentRect != null
            ? parentRect.rect.size.ToString("F1")
            : "null";
        string ownSize = ownRect != null
            ? ownRect.rect.size.ToString("F1") + " scale=" + ownRect.localScale.ToString("F2")
            : "null";
        Vector2 anchorsMin = ownRect != null ? ownRect.anchorMin : Vector2.zero;
        Vector2 anchorsMax = ownRect != null ? ownRect.anchorMax : Vector2.zero;
        Vector2 anchoredPosition = ownRect != null ? ownRect.anchoredPosition : Vector2.zero;

        Debug.Log(
            LogPrefix +
            " t=" + Time.unscaledTime.ToString("0.###") +
            " " + message +
            " screen=" + Screen.width + "x" + Screen.height +
            " camPixel=" + cameraPixelRect +
            " camViewport=" + cameraViewportRect +
            " parentSize=" + parentSize +
            " rectSize=" + ownSize +
            " anchors=" + anchorsMin.ToString("F2") + ".." + anchorsMax.ToString("F2") +
            " pos=" + anchoredPosition.ToString("F1"));
    }
}
