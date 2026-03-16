using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EndingScreenController : MonoBehaviour
{
    private static readonly WaitForSecondsRealtime _waitFrame = new(0.01f);

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

    [Header("Return To Title")]
    [SerializeField] string titleSceneName = "TitleScreen";

    [Header("Static Labels")]
    [SerializeField]
    string worldCompleteLabel =
        "<size=52><color=#1ABC00>WORLD 2</color>  <color=#E8E8E8>DEMO COMPLETE!</color></size>";

    [SerializeField]
    string openSourceBlock =
        "<size=32><color=#3392FF>OPEN SOURCE PROJECT</color></size>\n" +
        "<size=28><color=#E8E8E8>github.com/MauricioSuporte/</color></size>\n" +
        "<size=28><color=#E8E8E8>Super-Bomberman-6-Unity</color></size>";

    [SerializeField]
    string returnBlock =
        "<size=34><color=#FF6F31>PRESS START</color></size>\n" +
        "<size=30><color=#E8E8E8>TO RETURN TO TITLE SCREEN</color></size>";

    [Header("Dynamic Messages")]
    [TextArea(4, 10)]
    [SerializeField]
    string message100 =
        "Congratulations!\n" +
        "You reached <color=#FFD54A>{PERCENT}%</color> completion.\n" +
        "That is only the beginning.\n" +
        "Can you go beyond 100%, clear every stage without using items,\n" +
        "and unlock the remaining Bombers?";

    [TextArea(4, 10)]
    [SerializeField]
    string message101To199 =
        "Amazing work!\n" +
        "You pushed the demo to <color=#FFD54A>{PERCENT}%</color> completion.\n" +
        "You are getting closer to total mastery.\n" +
        "Keep pushing further,\n" +
        "clear more stages without using items,\n" +
        "and unlock every Bomber!";

    [TextArea(4, 10)]
    [SerializeField]
    string message200NotAllBombers =
        "Incredible!\n" +
        "You reached the full <color=#FFD54A>{PERCENT}%</color> completion.\n" +
        "Every stage has been perfected.\n" +
        "Now finish the last challenge:\n" +
        "unlock every Bomber!";

    [TextArea(4, 10)]
    [SerializeField]
    string message200AllBombers =
        "TRUE COMPLETION!!\n" +
        "You reached the full <color=#FFD54A>{PERCENT}%</color> completion.\n" +
        "Every stage has been perfected,\n" +
        "and every Bomber has been unlocked.\n" +
        "You truly mastered the demo!";

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

    enum EndingTier
    {
        Exactly100 = 0,
        Between101And199 = 1,
        Exactly200 = 2
    }

    readonly struct EndingProgressInfo
    {
        public readonly int RegisteredStageCount;
        public readonly int ClearedStageCount;
        public readonly int PerfectStageCount;
        public readonly int CompletionPercent;
        public readonly int UnlockedBombersCount;
        public readonly int TotalBombersCount;

        public bool HasUnlockedAllBombers => TotalBombersCount > 0 && UnlockedBombersCount >= TotalBombersCount;

        public EndingProgressInfo(
            int registeredStageCount,
            int clearedStageCount,
            int perfectStageCount,
            int completionPercent,
            int unlockedBombersCount,
            int totalBombersCount)
        {
            RegisteredStageCount = registeredStageCount;
            ClearedStageCount = clearedStageCount;
            PerfectStageCount = perfectStageCount;
            CompletionPercent = completionPercent;
            UnlockedBombersCount = unlockedBombersCount;
            TotalBombersCount = totalBombersCount;
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

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        SetupMessageMaterial();
        ApplyMessageLayoutFix();

        if (starComemoration != null)
            starComemoration.StopAndClear();

        EndingProgressInfo progress = BuildProgressInfo();
        string finalMessage = BuildEndingMessage(progress);
        AudioClip selectedMusic = GetEndingMusic(progress);

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
            messageText.text = finalMessage;
            messageText.gameObject.SetActive(true);
            messageText.alpha = 0f;

            ApplyMessageLayoutFix();

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
            GameMusicController.Instance.PlayMusic(selectedMusic, musicVolume, false);

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

            if (messageText != null)
                messageText.alpha = p;

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
        SceneManager.LoadScene(titleSceneName, LoadSceneMode.Single);
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
        int perfectStageCount = 0;

        var slot = SaveSystem.ActiveSlot;
        if (slot != null)
        {
            registeredStageCount = slot.stageOrder != null ? slot.stageOrder.Count : 0;
            clearedStageCount = slot.clearedStages != null ? slot.clearedStages.Count : 0;
            perfectStageCount = slot.perfectStages != null ? slot.perfectStages.Count : 0;
        }

        int completionPercent = ComputeCompletionPercent(registeredStageCount, clearedStageCount, perfectStageCount);
        int totalBombersCount = Enum.GetValues(typeof(BomberSkin)).Length;
        int unlockedBombersCount = CountUnlockedBombers();

        return new EndingProgressInfo(
            registeredStageCount,
            clearedStageCount,
            perfectStageCount,
            completionPercent,
            unlockedBombersCount,
            totalBombersCount);
    }

    int ComputeCompletionPercent(int totalStages, int clearedCount, int perfectCount)
    {
        if (totalStages <= 0)
            return 0;

        float clearedPercent = (Mathf.Clamp(clearedCount, 0, totalStages) / (float)totalStages) * 100f;
        float perfectPercent = (Mathf.Clamp(perfectCount, 0, totalStages) / (float)totalStages) * 100f;

        return Mathf.RoundToInt(clearedPercent + perfectPercent);
    }

    int CountUnlockedBombers()
    {
        if (SaveSystem.Data == null || SaveSystem.Data.unlockedSkins == null)
            return 0;

        HashSet<string> uniqueUnlocked = new(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < SaveSystem.Data.unlockedSkins.Count; i++)
        {
            string skinName = SaveSystem.Data.unlockedSkins[i];
            if (string.IsNullOrWhiteSpace(skinName))
                continue;

            if (Enum.TryParse(skinName, true, out BomberSkin parsedSkin))
                uniqueUnlocked.Add(parsedSkin.ToString());
        }

        return uniqueUnlocked.Count;
    }

    EndingTier GetEndingTier(EndingProgressInfo progress)
    {
        if (progress.CompletionPercent >= 200)
            return EndingTier.Exactly200;

        if (progress.CompletionPercent == 100)
            return EndingTier.Exactly100;

        return EndingTier.Between101And199;
    }

    AudioClip GetEndingMusic(EndingProgressInfo progress)
    {
        EndingTier tier = GetEndingTier(progress);

        if (tier == EndingTier.Exactly200)
        {
            if (progress.HasUnlockedAllBombers && endingMusic200AllBombers != null)
                return endingMusic200AllBombers;

            return endingMusic200;
        }

        if (tier == EndingTier.Between101And199)
            return endingMusic101To199;

        return endingMusic100;
    }

    string BuildEndingMessage(EndingProgressInfo progress)
    {
        string bodyTemplate = GetBodyTemplate(progress);
        string bodyText = ReplaceTokens(bodyTemplate, progress);
        string statsBlock = BuildStatsBlock(progress);

        string spacer = compactVerticalSpacing ? "\n\n" : "\n\n\n";
        string bigSpacer = compactVerticalSpacing ? "\n\n" : "\n\n\n";

        return
            $"{worldCompleteLabel}{spacer}" +
            $"<size=30><color=#E8E8E8>{bodyText}</color></size>{spacer}" +
            $"<size=26><color=#FFD54A>{statsBlock}</color></size>{bigSpacer}" +
            $"{openSourceBlock}{bigSpacer}" +
            $"{returnBlock}";
    }

    string GetBodyTemplate(EndingProgressInfo progress)
    {
        EndingTier tier = GetEndingTier(progress);

        switch (tier)
        {
            case EndingTier.Exactly200:
                return progress.HasUnlockedAllBombers
                    ? message200AllBombers
                    : message200NotAllBombers;

            case EndingTier.Between101And199:
                return message101To199;

            default:
                return message100;
        }
    }

    string ReplaceTokens(string template, EndingProgressInfo progress)
    {
        if (string.IsNullOrEmpty(template))
            return string.Empty;

        return template
            .Replace("{PERCENT}", progress.CompletionPercent.ToString())
            .Replace("{CLEARED}", progress.ClearedStageCount.ToString())
            .Replace("{TOTAL}", progress.RegisteredStageCount.ToString())
            .Replace("{PERFECT}", progress.PerfectStageCount.ToString())
            .Replace("{BOMBERS}", progress.UnlockedBombersCount.ToString())
            .Replace("{BOMBERS_TOTAL}", progress.TotalBombersCount.ToString());
    }

    string BuildStatsBlock(EndingProgressInfo progress)
    {
        return
            $"STAGE COMPLETION: {progress.CompletionPercent}%\n" +
            $"BOMBERS UNLOCKED: {progress.UnlockedBombersCount}/{progress.TotalBombersCount}";
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
}