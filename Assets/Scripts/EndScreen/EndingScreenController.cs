using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EndingScreenController : MonoBehaviour
{
    private static readonly WaitForSecondsRealtime _waitFrame = new(0.01f);

    [Header("Debug")]
#pragma warning disable CS0414
    [SerializeField] bool enableSurgicallogs = false;
#pragma warning restore CS0414

    [Header("UI")]
    public Image endingImage;
    public TMP_Text messageText;

    [Header("Audio")]
    public AudioClip endingMusic100;
    public AudioClip endingMusic101To199;
    public AudioClip endingMusic200;
    public AudioClip endingMusic200AllBombers;
    [Range(0f, 1f)] public float musicVolume = 1f;

    [Header("Text Outline")]
    [SerializeField] Color outlineColor = Color.black;
    [SerializeField, Range(0f, 1f)] float outlineWidth = 0.4f;

    [Header("Celebration")]
    [SerializeField] EndingStarComemoration starComemoration;

    public static EndingScreenController Instance { get; private set; }

    bool creditsSkippedByUser;

    [Header("Return To Title")]
    [SerializeField] string titleSceneName = "TitleScreen";

    [Header("Static Labels")]
#pragma warning disable CS0414
    [SerializeField]
    string demoCompleteLabel =
        "<size=52><color=#1ABC00>DEMO 4</color>  <color=#E8E8E8>COMPLETE!</color></size>";

    [SerializeField]
    string openSourceBlock =
        "<size=32><color=#3392FF>OPEN SOURCE PROJECT</color></size>\n" +
        "<size=28><color=#E8E8E8>github.com/MauricioSuporte/</color></size>\n" +
        "<size=28><color=#E8E8E8>Super-Bomberman-6-Unity</color></size>";

    [SerializeField]
    string returnBlock =
        "<size=34><color=#FF6F31>PRESS START</color></size>\n" +
        "<size=30><color=#E8E8E8>TO RETURN TO TITLE SCREEN</color></size>";
#pragma warning restore CS0414

    [Header("Credits")]
#pragma warning disable CS0414
    [SerializeField, TextArea(12, 40)]
    string creditsBlock =
        "Super Bomberman 6 v0.4.0\n" +
        "Tribute to Bomberman\n\n" +
        "Bomberman\n" +
        "Copyright 1983\n" +
        "Hudson Soft/Konami\n\n" +
        "Super Bomberman 6\n\n" +
        "Coding\n" +
        "MauricioSuporte\n\n" +
        "Sprite Contribution\n" +
        "Srplay\n" +
        "Joao1417\n" +
        "WeirdFoxDreams\n" +
        "Juliocesargamesbr\n" +
        "Kurobon94\n" +
        "LeroyUrocyon\n\n" +
        "Playtesting/Feedback\n" +
        "Kaaos Gameplays\n" +
        "Joaololpvp\n" +
        "Blackingstar\n" +
        "Júlio Cesar\n" +
        "Nico Netsumu\n" +
        "Jei\n" +
        "Kurobon94\n" +
        "Tiago Deficigamer\n" +
        "Ruivo\n" +
        "Lopez238\n" +
        "Yamishitsuji\n" +
        "Luciandro Gamer\n" +
        "Mackson\n" +
        "perfig187\n" +
        "adrianokof games\n" +
        "Everton Def\n" +
        "Gleydson Retrogen\n" +
        "FLPStrike\n" +
        "Love Vixen\n" +
        "Juliocesargamesbr\n" +
        "Rangelukaz\n" +
        "JonasS JK Ninja\n\n" +
        "Sounds/Musics\n" +
        "wolfguarder\n\n" +
        "Base of the Game\n" +
        "Zigurous";
#pragma warning restore CS0414

    [SerializeField, Min(1f)] float creditsScrollSpeed = 80f;
    [SerializeField] float creditsStartBottomPadding = 80f;
    [SerializeField] float creditsEndTopPadding = 80f;
    [SerializeField, Min(1f)] float finalMessageScrollSpeed = 220f;
#pragma warning disable CS0414
    [SerializeField] float finalMessageTargetY = -12f;
#pragma warning restore CS0414

    [Header("Auto Layout Fix")]
    [SerializeField] bool autoRepositionMessageText = true;
    [SerializeField] bool forceTopAnchor = true;
    [SerializeField] float topMargin = 12f;
    [SerializeField] float sideMargin = 8f;
    [SerializeField] float bottomMargin = 10f;
    [SerializeField] float widthPercentOfParent = 0.94f;
    [SerializeField] float heightPercentOfParent = 0.90f;

    [Header("Message Formatting")]
    [SerializeField] bool compactVerticalSpacing = true;

    public bool Running { get; private set; }

    Material runtimeMsgMat;

    readonly struct EndingProgressInfo
    {
        public readonly int RegisteredStageCount;
        public readonly int ClearedStageCount;
        public readonly int CompletionPercent;
        public readonly int UnlockedAchievementsCount;
        public readonly int TotalAchievementsCount;
        public readonly int AchievementsPercent;

        public EndingProgressInfo(
            int registeredStageCount,
            int clearedStageCount,
            int completionPercent,
            int unlockedAchievementsCount,
            int totalAchievementsCount,
            int achievementsPercent)
        {
            RegisteredStageCount = registeredStageCount;
            ClearedStageCount = clearedStageCount;
            CompletionPercent = completionPercent;
            UnlockedAchievementsCount = unlockedAchievementsCount;
            TotalAchievementsCount = totalAchievementsCount;
            AchievementsPercent = achievementsPercent;
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (starComemoration == null)
            starComemoration = GetComponentInChildren<EndingStarComemoration>(true);

        if (endingImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].name == "EndingBackground")
                {
                    endingImage = images[i];
                    break;
                }
            }
        }

        if (messageText == null)
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].name == "EndingMessageText")
                {
                    messageText = texts[i];
                    break;
                }
            }
        }

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = GetComponentInChildren<Canvas>(true);

        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;
        }

        SetupMessageMaterial();
        ApplyMessageLayoutFix();
        ForceHide();
    }

    void OnDestroy()
    {
        if (runtimeMsgMat != null)
            Destroy(runtimeMsgMat);
    }

    void SetupMessageMaterial()
    {
        if (messageText == null)
            return;

        Material baseMat = messageText.fontSharedMaterial;

        if (baseMat == null && messageText.font != null)
            baseMat = messageText.font.material;

        if (baseMat == null)
            return;

        if (runtimeMsgMat != null)
            Destroy(runtimeMsgMat);

        runtimeMsgMat = new Material(baseMat);

        if (runtimeMsgMat.HasProperty("_OutlineWidth"))
            runtimeMsgMat.SetFloat("_OutlineWidth", outlineWidth);

        if (runtimeMsgMat.HasProperty("_OutlineColor"))
            runtimeMsgMat.SetColor("_OutlineColor", outlineColor);

        messageText.fontSharedMaterial = runtimeMsgMat;
    }

    public void ForceHide()
    {
        Running = false;

        if (starComemoration != null)
            starComemoration.StopAndClear();

        if (endingImage != null)
            endingImage.gameObject.SetActive(false);

        if (messageText != null)
            messageText.gameObject.SetActive(false);

        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    public IEnumerator Play(Image fadeImageOptional)
    {
        Running = true;
        creditsSkippedByUser = false;

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        SetupMessageMaterial();
        ApplyMessageLayoutFix();

        if (starComemoration != null)
            starComemoration.StopAndClear();

        EndingProgressInfo progress = BuildProgressInfo();
        string finalMessage = BuildEndingMessage(progress);
        AudioClip selectedMusic = GetEndingMusic();
        float creditsEndY = 0f;

        if (endingImage != null)
        {
            endingImage.gameObject.SetActive(true);
            endingImage.enabled = true;

            Color c = endingImage.color;
            c.a = 0f;
            endingImage.color = c;
        }

        if (messageText != null)
        {
            LocalizedTmpFontFallback.Apply(messageText);
            messageText.text = BuildCreditsMessage();
            messageText.gameObject.SetActive(true);
            messageText.alpha = 1f;

            ApplyCreditsLayout();
            SetCreditsStartPosition(out float creditsStartY, out creditsEndY, out float creditsTextHeight);

            Canvas.ForceUpdateCanvases();
            messageText.ForceMeshUpdate();
        }

        ApplyVisualHierarchyOrder();

        if (starComemoration != null)
            starComemoration.PlayIfEligible();

        if (fadeImageOptional != null)
        {
            fadeImageOptional.gameObject.SetActive(true);
            fadeImageOptional.transform.SetAsLastSibling();
        }

        if (selectedMusic != null && GameMusicController.Instance != null)
            GameMusicController.Instance.PlayMusic(selectedMusic, musicVolume, true);

        Coroutine creditsRoutine = messageText != null
            ? StartCoroutine(RollCreditsRoutine(creditsEndY))
            : null;

        float duration = 2f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);

            if (endingImage != null)
            {
                Color c = endingImage.color;
                c.a = p;
                endingImage.color = c;
            }

            if (fadeImageOptional != null)
            {
                Color fc = fadeImageOptional.color;
                fc.a = 1f - p;
                fadeImageOptional.color = fc;
            }

            yield return null;
        }

        if (fadeImageOptional != null)
            fadeImageOptional.gameObject.SetActive(false);

        PlayerInputManager input = PlayerInputManager.Instance;
        if (input != null && input.AnyGet(PlayerAction.Start))
        {
            while (input != null && input.AnyGet(PlayerAction.Start))
                yield return _waitFrame;

            yield return null;
        }

        if (creditsRoutine != null)
            yield return creditsRoutine;

        if (messageText != null)
        {
            LocalizedTmpFontFallback.Apply(messageText);
            messageText.text = finalMessage;
            messageText.alpha = 1f;
            ApplyFinalMessageLayout();
            Canvas.ForceUpdateCanvases();
            messageText.ForceMeshUpdate();
        }

        yield return RollFinalMessageRoutine(creditsSkippedByUser);

        input = PlayerInputManager.Instance;
        if (input != null && input.AnyGet(PlayerAction.Start))
        {
            while (input != null && input.AnyGet(PlayerAction.Start))
                yield return _waitFrame;

            yield return null;
        }

        while (true)
        {
            input = PlayerInputManager.Instance;
            if (input != null && input.AnyGetDown(PlayerAction.Start))
                break;

            yield return null;
        }

        if (GameMusicController.Instance != null)
            GameMusicController.Instance.StopMusic();

        GamePauseController.ForceUnpause();
        ForceHide();
        Running = false;

        PlayerPersistentStats.ResetSessionForReturnToTitle();
        TitleScreenSkip.SkipNextIntro = true;
        SceneManager.LoadScene(titleSceneName, LoadSceneMode.Single);
    }

    IEnumerator RollCreditsRoutine(float endY)
    {
        if (messageText == null)
            yield break;

        RectTransform rt = messageText.rectTransform;
        RectTransform parentRt = rt.parent as RectTransform;

        if (rt == null || parentRt == null)
            yield break;

        PlayerInputManager input;

        while (rt != null && rt.anchoredPosition.y < endY)
        {
            input = PlayerInputManager.Instance;
            if (input != null && input.AnyGetDown(PlayerAction.Start))
            {
                creditsSkippedByUser = true;
                break;
            }

            Vector2 pos = rt.anchoredPosition;
            pos.y += creditsScrollSpeed * Time.unscaledDeltaTime;
            rt.anchoredPosition = pos;

            yield return null;
        }
    }

    IEnumerator RollFinalMessageRoutine(bool instant)
    {
        if (messageText == null)
            yield break;

        RectTransform rt = messageText.rectTransform;
        RectTransform parentRt = rt.parent as RectTransform;

        if (rt == null || parentRt == null)
            yield break;

        Canvas.ForceUpdateCanvases();
        messageText.ForceMeshUpdate();

        float parentHeight = parentRt.rect.height > 1f ? parentRt.rect.height : Screen.height;
        float textHeight = Mathf.Max(messageText.preferredHeight, 32f);

        float halfParentHeight = parentHeight * 0.5f;
        float halfTextHeight = textHeight * 0.5f;

        float startY = -halfParentHeight - halfTextHeight - creditsStartBottomPadding;

        float targetTopMargin = 12f;
        float targetY = halfParentHeight - targetTopMargin - halfTextHeight;

        Vector2 pos = rt.anchoredPosition;

        if (instant)
        {
            pos.y = targetY;
            rt.anchoredPosition = pos;
            yield break;
        }

        pos.y = startY;
        rt.anchoredPosition = pos;

        float speed = finalMessageScrollSpeed * 2f;

        while (rt != null && rt.anchoredPosition.y < targetY)
        {
            pos = rt.anchoredPosition;
            pos.y = Mathf.Min(targetY, pos.y + speed * Time.unscaledDeltaTime);
            rt.anchoredPosition = pos;
            yield return null;
        }

        if (rt != null)
        {
            pos = rt.anchoredPosition;
            pos.y = targetY;
            rt.anchoredPosition = pos;
        }
    }

    void ApplyVisualHierarchyOrder()
    {
        Transform parent = null;

        if (endingImage != null)
            parent = endingImage.transform.parent;
        else if (starComemoration != null)
            parent = starComemoration.transform.parent;
        else if (messageText != null)
            parent = messageText.transform.parent;

        if (parent == null)
            return;

        if (endingImage != null && endingImage.transform.parent == parent)
            endingImage.transform.SetSiblingIndex(0);

        if (starComemoration != null && starComemoration.transform.parent == parent)
            starComemoration.transform.SetSiblingIndex(1);

        if (messageText != null && messageText.transform.parent == parent)
            messageText.transform.SetSiblingIndex(2);
    }

    EndingProgressInfo BuildProgressInfo()
    {
        int registeredStageCount = 0;
        int clearedStageCount = 0;

        var slot = SaveSystem.ActiveSlot;
        if (slot != null)
        {
            registeredStageCount = slot.stageOrder != null ? slot.stageOrder.Count : 0;
            clearedStageCount = slot.clearedStages != null ? slot.clearedStages.Count : 0;
        }

        int completionPercent = ComputeCompletionPercent(registeredStageCount, clearedStageCount);
        int totalAchievementsCount = AchievementCatalog.All != null ? AchievementCatalog.All.Length : 0;
        int unlockedAchievementsCount = CountUnlockedAchievements();
        int achievementsPercent = totalAchievementsCount > 0
            ? Mathf.RoundToInt((unlockedAchievementsCount / (float)totalAchievementsCount) * 100f)
            : 0;

        return new EndingProgressInfo(
            registeredStageCount,
            clearedStageCount,
            completionPercent,
            unlockedAchievementsCount,
            totalAchievementsCount,
            achievementsPercent);
    }

    int ComputeCompletionPercent(int totalStages, int clearedCount)
    {
        if (totalStages <= 0)
            return 0;

        int clampedCleared = Mathf.Clamp(clearedCount, 0, totalStages);
        if (clampedCleared >= totalStages)
        {
            return SaveSystem.GetActiveNormalGameDifficulty() switch
            {
                Assets.Scripts.SaveSystem.NormalGameDifficulty.Hard => 103,
                Assets.Scripts.SaveSystem.NormalGameDifficulty.Hardcore => 105,
                _ => 100
            };
        }

        return Mathf.RoundToInt((clampedCleared / (float)totalStages) * 100f);
    }

    int CountUnlockedAchievements()
    {
        if (AchievementCatalog.All == null)
            return 0;

        int count = 0;
        for (int i = 0; i < AchievementCatalog.All.Length; i++)
        {
            var achievement = AchievementCatalog.All[i];
            if (achievement.IsUnlocked != null && achievement.IsUnlocked())
                count++;
        }

        return count;
    }

    AudioClip GetEndingMusic()
    {
        return endingMusic200AllBombers != null ? endingMusic200AllBombers : endingMusic100;
    }

    string BuildCreditsMessage()
    {
        const string defaultColor = "#E8E8E8";
        const string greenTitleColor = "#8CFF8C";
        const string yellowTitleColor = "#FFF68A";
        CreditsText credits = GameTextDatabase.Credits;

        string text =
            $"<color={greenTitleColor}>Super Bomberman 6 v0.4.0</color>\n" +
            $"<color={defaultColor}>{credits.Tribute}</color>\n\n" +

            $"<color={greenTitleColor}>Bomberman</color>\n" +
            $"<color={defaultColor}>Copyright 1983</color>\n" +
            $"<color={defaultColor}>Hudson Soft/Konami</color>\n\n" +

            $"<color={greenTitleColor}>Super Bomberman 6</color>\n\n" +

            $"<color={yellowTitleColor}>{credits.Coding}</color>\n" +
            $"<color={defaultColor}>MauricioSuporte</color>\n\n" +

            $"<color={yellowTitleColor}>{credits.SpriteContribution}</color>\n" +
            $"<color={defaultColor}>Srplay</color>\n" +
            $"<color={defaultColor}>Joao1417</color>\n" +
            $"<color={defaultColor}>WeirdFoxDreams</color>\n" +
            $"<color={defaultColor}>Juliocesargamesbr</color>\n" +
            $"<color={defaultColor}>Kurobon94</color>\n" +
            $"<color={defaultColor}>LeroyUrocyon</color>\n\n" +

            $"<color={yellowTitleColor}>{credits.PlaytestingFeedback}</color>\n" +
            $"<color={defaultColor}>Kaaos Gameplays</color>\n" +
            $"<color={defaultColor}>Joaololpvp</color>\n" +
            $"<color={defaultColor}>Blackingstar</color>\n" +
            $"<color={defaultColor}>Júlio Cesar</color>\n" +
            $"<color={defaultColor}>Nico Netsumu</color>\n" +
            $"<color={defaultColor}>Jei</color>\n" +
            $"<color={defaultColor}>Kurobon94</color>\n" +
            $"<color={defaultColor}>Tiago Deficigamer</color>\n" +
            $"<color={defaultColor}>Ruivo</color>\n" +
            $"<color={defaultColor}>Lopez238</color>\n" +
            $"<color={defaultColor}>Yamishitsuji</color>\n" +
            $"<color={defaultColor}>Luciandro Gamer</color>\n" +
            $"<color={defaultColor}>Mackson</color>\n" +
            $"<color={defaultColor}>perfig187</color>\n" +
            $"<color={defaultColor}>adrianokof games</color>\n" +
            $"<color={defaultColor}>Everton Def</color>\n" +
            $"<color={defaultColor}>Gleydson Retrogen</color>\n" +
            $"<color={defaultColor}>FLPStrike</color>\n" +
            $"<color={defaultColor}>Love Vixen</color>\n" +
            $"<color={defaultColor}>Juliocesargamesbr</color>\n" +
            $"<color={defaultColor}>Rangelukaz</color>\n" +
            $"<color={defaultColor}>JonasS JK Ninja</color>\n\n" +

            $"<color={yellowTitleColor}>{credits.SoundsMusics}</color>\n" +
            $"<color={defaultColor}>wolfguarder</color>\n\n" +

            $"<color={yellowTitleColor}>{credits.BaseOfTheGame}</color>\n" +
            $"<color={defaultColor}>Zigurous</color>";

        return $"<size=36>{text}</size>";
    }

    string BuildEndingMessage(EndingProgressInfo progress)
    {
        string statsBlock = BuildStatsBlock(progress);

        string spacer = compactVerticalSpacing ? "\n\n" : "\n\n\n";
        string bigSpacer = compactVerticalSpacing ? "\n\n" : "\n\n\n";

        const string defaultColor = "#E8E8E8";
        const string greenTitleColor = "#8CFF8C";
        const string yellowTitleColor = "#FFF68A";
        const string blueTitleColor = "#3392FF";
        const string orangeTitleColor = "#FF6F31";
        CreditsText credits = GameTextDatabase.Credits;

        string text =
            $"<color={greenTitleColor}>{credits.DemoComplete}</color>{spacer}" +

            $"<color={yellowTitleColor}>{statsBlock}</color>{bigSpacer}" +

            $"<color={blueTitleColor}>{credits.OpenSourceProject}</color>\n" +
            $"<color={defaultColor}>github.com/MauricioSuporte/</color>\n" +
            $"<color={defaultColor}>Super-Bomberman-6-Unity</color>{bigSpacer}" +

            $"<color={orangeTitleColor}>{credits.PressStart}</color>\n" +
            $"<color={defaultColor}>{credits.ReturnToTitle}</color>";

        return $"<size=36>{text}</size>";
    }

    string BuildStatsBlock(EndingProgressInfo progress)
    {
        return
            $"{GameTextDatabase.Credits.StageCompletion}: {progress.CompletionPercent}%\n" +
            $"{GameTextDatabase.Credits.AchievementsUnlocked}: {progress.UnlockedAchievementsCount} {GameTextDatabase.Credits.Of} {progress.TotalAchievementsCount} {progress.AchievementsPercent}%";
    }

    void ApplyCreditsLayout()
    {
        if (messageText == null)
            return;

        RectTransform rt = messageText.rectTransform;
        RectTransform parentRt = rt.parent as RectTransform;

        if (rt == null || parentRt == null)
            return;

        float parentWidth = parentRt.rect.width > 1f ? parentRt.rect.width : Screen.width;
        float parentHeight = parentRt.rect.height > 1f ? parentRt.rect.height : Screen.height;
        float targetWidth = Mathf.Max(32f, parentWidth * Mathf.Clamp01(widthPercentOfParent) - (sideMargin * 2f));

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(targetWidth, parentHeight * 2f);
        rt.anchoredPosition = Vector2.zero;

        messageText.alignment = TextAlignmentOptions.Center;
        messageText.overflowMode = TextOverflowModes.Overflow;
        messageText.textWrappingMode = TextWrappingModes.NoWrap;

        Canvas.ForceUpdateCanvases();
        messageText.ForceMeshUpdate();

        float preferredHeight = Mathf.Max(messageText.preferredHeight, 32f);
        rt.sizeDelta = new Vector2(targetWidth, preferredHeight);
    }

    void ApplyFinalMessageLayout()
    {
        if (messageText == null)
            return;

        RectTransform rt = messageText.rectTransform;
        RectTransform parentRt = rt.parent as RectTransform;

        if (rt == null || parentRt == null)
            return;

        float parentWidth = parentRt.rect.width > 1f ? parentRt.rect.width : Screen.width;
        float targetWidth = Mathf.Max(32f, parentWidth * Mathf.Clamp01(widthPercentOfParent) - (sideMargin * 2f));

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(targetWidth, 800f);
        rt.anchoredPosition = Vector2.zero;

        messageText.alignment = TextAlignmentOptions.Center;
        messageText.overflowMode = TextOverflowModes.Overflow;
        messageText.textWrappingMode = TextWrappingModes.NoWrap;

        Canvas.ForceUpdateCanvases();
        messageText.ForceMeshUpdate();

        float preferredHeight = Mathf.Max(messageText.preferredHeight, 32f);
        rt.sizeDelta = new Vector2(targetWidth, preferredHeight);
    }

    void ApplyMessageLayoutFix()
    {
        if (!autoRepositionMessageText || messageText == null)
            return;

        RectTransform rt = messageText.rectTransform;
        RectTransform parentRt = rt.parent as RectTransform;

        if (rt == null || parentRt == null)
            return;

        if (forceTopAnchor)
        {
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
        }

        float parentWidth = parentRt.rect.width;
        float parentHeight = parentRt.rect.height;

        if (parentWidth <= 0f)
            parentWidth = Screen.width;

        if (parentHeight <= 0f)
            parentHeight = Screen.height;

        float targetWidth = Mathf.Max(32f, parentWidth * Mathf.Clamp01(widthPercentOfParent) - (sideMargin * 2f));
        float targetHeight = Mathf.Max(32f, parentHeight * Mathf.Clamp01(heightPercentOfParent) - topMargin - bottomMargin);

        rt.sizeDelta = new Vector2(targetWidth, targetHeight);
        rt.anchoredPosition = new Vector2(0f, -Mathf.Max(0f, topMargin));

        messageText.alignment = TextAlignmentOptions.TopGeoAligned;
        messageText.overflowMode = TextOverflowModes.Overflow;
        messageText.textWrappingMode = TextWrappingModes.NoWrap;
    }

    void SetCreditsStartPosition(out float startY, out float endY, out float textHeight)
    {
        startY = 0f;
        endY = 0f;
        textHeight = 0f;

        if (messageText == null)
            return;

        RectTransform rt = messageText.rectTransform;
        RectTransform parentRt = rt.parent as RectTransform;

        if (rt == null || parentRt == null)
            return;

        Canvas.ForceUpdateCanvases();
        messageText.ForceMeshUpdate();

        float parentHeight = parentRt.rect.height > 1f ? parentRt.rect.height : Screen.height;
        textHeight = Mathf.Max(messageText.preferredHeight, 32f);

        float halfParentHeight = parentHeight * 0.5f;
        float halfTextHeight = textHeight * 0.5f;

        startY = -halfParentHeight - halfTextHeight + creditsStartBottomPadding;
        endY = halfParentHeight + halfTextHeight + creditsEndTopPadding;

        Vector2 pos = rt.anchoredPosition;
        pos.y = startY;
        rt.anchoredPosition = pos;
    }
}
